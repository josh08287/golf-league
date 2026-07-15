using GolfLeague.Application.Common;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Infrastructure.ScorecardOcr;

/// <summary>
/// Used when DOCUMENT_INTELLIGENCE_ENDPOINT/DOCUMENT_INTELLIGENCE_KEY aren't
/// configured (or the Key Vault reference for the key hasn't resolved) —
/// returns no matches so the confirm screen simply starts blank instead of
/// the request failing outright.
/// </summary>
public sealed class NoOpScorecardOcrService : IScorecardOcrService
{
    private readonly ILogger<NoOpScorecardOcrService> _logger;

    public NoOpScorecardOcrService(ILogger<NoOpScorecardOcrService> logger) => _logger = logger;

    public Task<ScorecardOcrResult> ParseAsync(
        byte[] imageBytes,
        IReadOnlyList<ScorecardOcrCandidatePlayer> candidatePlayers,
        IReadOnlyList<int> holeNumbers,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Scorecard OCR not configured (Document Intelligence endpoint/key missing) — returning empty parse result.");
        return Task.FromResult(new ScorecardOcrResult([]));
    }
}
