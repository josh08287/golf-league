namespace GolfLeague.Application.Common;

public sealed record ScorecardOcrHole(int HoleNumber, int? GrossStrokes, double Confidence);

public sealed record ScorecardOcrPlayerRow(
    string RawOcrName,
    int? MatchedPlayerId,
    string? MatchedPlayerName,
    IReadOnlyList<ScorecardOcrHole> Holes);

public sealed record ScorecardOcrResult(IReadOnlyList<ScorecardOcrPlayerRow> Players);

public sealed record ScorecardOcrCandidatePlayer(int PlayerId, string FullName, string Initials);

/// <summary>
/// Parses a photographed paper scorecard into per-player, per-hole gross
/// strokes, via Azure AI Document Intelligence. The image is sent to that
/// Azure service for processing but is never persisted by this app — the
/// caller discards the bytes once this returns.
/// </summary>
public interface IScorecardOcrService
{
    Task<ScorecardOcrResult> ParseAsync(
        byte[] imageBytes,
        IReadOnlyList<ScorecardOcrCandidatePlayer> candidatePlayers,
        IReadOnlyList<int> holeNumbers,
        CancellationToken cancellationToken = default);
}
