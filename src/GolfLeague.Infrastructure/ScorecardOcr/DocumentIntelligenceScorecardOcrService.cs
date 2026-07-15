using Azure.AI.DocumentIntelligence;
using GolfLeague.Application.Common;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Infrastructure.ScorecardOcr;

/// <summary>
/// Parses a scorecard photo using Azure AI Document Intelligence's prebuilt
/// Layout model. Unlike a raw-text OCR engine, Layout detects table
/// structure natively (row/column indices per cell), so this service does
/// not need to infer a grid from word positions — it only maps the
/// service's row/column indices onto hole numbers and player names.
/// No per-course template — the grid is read fresh from each image, same as
/// before. Accuracy on handwriting still isn't perfect, so every result
/// stays confidence-scored and the caller surfaces a confirm/edit step
/// rather than trusting this output outright.
/// </summary>
public sealed class DocumentIntelligenceScorecardOcrService : IScorecardOcrService
{
    /// <summary>
    /// Non-player row labels printed on most scorecards (hole number, tee
    /// markers, handicap/par reference rows). A row whose first-column text
    /// starts with one of these is a scorecard fixture, not a player's
    /// score line, and must never be offered up for player mapping.
    /// </summary>
    private static readonly string[] NonPlayerRowLabels =
    [
        "Hole", "Black", "Blue", "White", "Gold", "Handicap", "Par", "Red"
    ];

    private readonly DocumentIntelligenceClient _client;
    private readonly ILogger<DocumentIntelligenceScorecardOcrService> _logger;

    public DocumentIntelligenceScorecardOcrService(
        DocumentIntelligenceClient client,
        ILogger<DocumentIntelligenceScorecardOcrService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<ScorecardOcrResult> ParseAsync(
        byte[] imageBytes,
        IReadOnlyList<ScorecardOcrCandidatePlayer> candidatePlayers,
        IReadOnlyList<int> holeNumbers,
        CancellationToken cancellationToken = default)
    {
        var operation = await _client.AnalyzeDocumentAsync(
            Azure.WaitUntil.Completed, "prebuilt-layout", BinaryData.FromBytes(imageBytes), cancellationToken);

        var table = operation.Value.Tables.OrderByDescending(t => t.Cells.Count).FirstOrDefault();
        if (table is null)
        {
            _logger.LogWarning("Document Intelligence found no table in the uploaded scorecard image.");
            return new ScorecardOcrResult([]);
        }

        var rows = table.Cells
            .GroupBy(c => c.RowIndex)
            .OrderBy(g => g.Key)
            .ToList();

        var headerRowIndex = FindHeaderRowIndex(rows, holeNumbers);
        var headerCells = rows.First(g => g.Key == headerRowIndex).ToList();
        var columnToHole = MapColumnsToHoles(headerCells, holeNumbers);

        var candidateLookup = candidatePlayers
            .Select(c => (c.FullName, c.Initials))
            .ToList();

        var playerRows = new List<ScorecardOcrPlayerRow>();
        foreach (var row in rows.Where(g => g.Key != headerRowIndex))
        {
            var cells = row.OrderBy(c => c.ColumnIndex).ToList();
            var nameCell = cells.FirstOrDefault(c => c.ColumnIndex == 0);
            var rawName = nameCell?.Content?.Trim();
            if (string.IsNullOrWhiteSpace(rawName))
                continue;

            if (NonPlayerRowLabels.Any(label => rawName.StartsWith(label, StringComparison.OrdinalIgnoreCase)))
                continue;

            var matchIndex = NameMatcher.FindBestMatch(rawName, candidateLookup);
            var matched = matchIndex.HasValue ? candidatePlayers[matchIndex.Value] : null;

            var holes = new List<ScorecardOcrHole>();
            foreach (var (columnIndex, holeNumber) in columnToHole)
            {
                var cell = cells.FirstOrDefault(c => c.ColumnIndex == columnIndex);
                if (cell is null || string.IsNullOrWhiteSpace(cell.Content))
                {
                    holes.Add(new ScorecardOcrHole(holeNumber, null, 0));
                    continue;
                }

                var strokes = ParseStrokeCount(cell.Content);
                holes.Add(new ScorecardOcrHole(holeNumber, strokes, EstimateConfidence(cell.Content, strokes)));
            }

            playerRows.Add(new ScorecardOcrPlayerRow(rawName, matched?.PlayerId, matched?.FullName, holes));
        }

        return new ScorecardOcrResult(playerRows);
    }

    /// <summary>
    /// The header row is the row whose numeric cells, in left-to-right
    /// order, most closely match the actual printed hole-number sequence
    /// (e.g. 1,2,3,...9). Requiring the values to line up with
    /// <paramref name="holeNumbers"/> — not just "parses as 1-18" — rejects
    /// score rows that happen to contain several small numbers (e.g. a row
    /// of all-single-digit scores), which a raw-count heuristic would
    /// otherwise confuse for the header and silently misalign every column.
    /// Falls back to the topmost row (Document Intelligence returns table
    /// rows top-down) if no row matches well.
    /// </summary>
    private static int FindHeaderRowIndex(List<IGrouping<int, DocumentTableCell>> rows, IReadOnlyList<int> holeNumbers)
    {
        var bestRowIndex = rows.Count > 0 ? rows[0].Key : 0;
        var bestMatchCount = -1;

        foreach (var row in rows)
        {
            var numericCells = row
                .Where(c => int.TryParse(c.Content?.Trim(), out _))
                .OrderBy(c => c.ColumnIndex)
                .Select(c => int.Parse(c.Content!.Trim()))
                .ToList();

            // Count how many cells, in order, match the expected hole number
            // at that position — a real header row lines up 1:1 with
            // holeNumbers; a row of scores rarely does.
            var matchCount = 0;
            for (var i = 0; i < numericCells.Count && i < holeNumbers.Count; i++)
            {
                if (numericCells[i] == holeNumbers[i])
                    matchCount++;
            }

            if (matchCount > bestMatchCount)
            {
                bestMatchCount = matchCount;
                bestRowIndex = row.Key;
            }
        }

        // Require at least half the holes to line up before trusting the
        // match; otherwise assume the topmost row is the header rather than
        // committing to a low-confidence guess.
        return bestMatchCount >= holeNumbers.Count / 2 ? bestRowIndex : (rows.Count > 0 ? rows[0].Key : 0);
    }

    private static List<(int ColumnIndex, int HoleNumber)> MapColumnsToHoles(
        List<DocumentTableCell> headerCells, IReadOnlyList<int> holeNumbers)
    {
        var numericHeaderCells = headerCells
            .Where(c => int.TryParse(c.Content?.Trim(), out _))
            .OrderBy(c => c.ColumnIndex)
            .ToList();

        var result = new List<(int, int)>();
        var count = Math.Min(numericHeaderCells.Count, holeNumbers.Count);
        for (var i = 0; i < count; i++)
            result.Add((numericHeaderCells[i].ColumnIndex, holeNumbers[i]));

        return result;
    }

    /// <summary>
    /// DocumentTableCell exposes no per-cell confidence score, so this
    /// derives a real (if coarse) signal from the cell's own content rather
    /// than reporting a constant that would make the frontend's low-
    /// confidence highlighting purely decorative: a cell that parsed
    /// cleanly as a bare 1-2 digit number is treated as high-confidence; a
    /// cell that needed non-digit characters stripped, was empty, or fell
    /// outside a plausible stroke range is flagged low-confidence so the
    /// user is prompted to double-check it.
    /// </summary>
    private static double EstimateConfidence(string rawContent, int? parsedStrokes)
    {
        if (parsedStrokes is null)
            return 0.0;

        var trimmed = rawContent.Trim();
        return trimmed == parsedStrokes.Value.ToString() ? 0.9 : 0.4;
    }

    private static int? ParseStrokeCount(string text)
    {
        var digitsOnly = new string(text.Where(char.IsDigit).ToArray());
        if (int.TryParse(digitsOnly, out var value) && value is >= 1 and <= 20)
            return value;

        return null;
    }
}
