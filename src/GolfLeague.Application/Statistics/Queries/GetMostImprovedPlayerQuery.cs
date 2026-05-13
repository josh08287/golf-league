using GolfLeague.Application.Common;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Statistics.Queries;

// ── DTOs ─────────────────────────────────────────────────────────────────────

/// <summary>
/// USGA Rule 7e — Most Improved Player.
///
/// Improvement Factor = (Starting HI + 12) / (Current HI + 12)
/// calculated to three decimal places. Highest factor wins.
///
/// The constant 12 normalises across handicap ranges so that a
/// reduction at the low end of the scale is weighted more heavily
/// than an equivalent raw drop at a higher handicap.
/// </summary>
public sealed record MostImprovedPlayerDto(
    int PlayerId,
    string PlayerName,
    string SeasonHalfName,
    double StartingHandicapIndex,
    double CurrentHandicapIndex,
    double ImprovementFactor,
    double HandicapReduction,
    int RoundsPlayedInHalf);

public sealed record MostImprovedResultDto(
    MostImprovedPlayerDto? Winner,
    List<MostImprovedPlayerDto> Leaderboard,
    string SeasonHalfName,
    int MinRoundsRequired);

// ── Query + Handler ──────────────────────────────────────────────────────────

public sealed record GetMostImprovedPlayerQuery : IRequest<Result<MostImprovedResultDto>>;

public sealed class GetMostImprovedPlayerQueryHandler
    : IRequestHandler<GetMostImprovedPlayerQuery, Result<MostImprovedResultDto>>
{
    /// <summary>
    /// Minimum finalized rounds a player must have in the half to qualify.
    /// </summary>
    private const int MinRounds = 3;

    private readonly ISeasonRepository _seasonRepository;
    private readonly IRoundRepository _roundRepository;
    private readonly IHandicapRepository _handicapRepository;
    private readonly IPlayerRepository _playerRepository;

    public GetMostImprovedPlayerQueryHandler(
        ISeasonRepository seasonRepository,
        IRoundRepository roundRepository,
        IHandicapRepository handicapRepository,
        IPlayerRepository playerRepository)
    {
        _seasonRepository = seasonRepository;
        _roundRepository = roundRepository;
        _handicapRepository = handicapRepository;
        _playerRepository = playerRepository;
    }

    public async Task<Result<MostImprovedResultDto>> Handle(
        GetMostImprovedPlayerQuery request,
        CancellationToken cancellationToken)
    {
        var season = await _seasonRepository.GetActiveAsync(cancellationToken);
        if (season is null)
            return Result<MostImprovedResultDto>.Fail("No active season found.");

        // Determine the current half based on today's date
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentHalf = season.Halves
            .Where(h => h.StartDate <= today)
            .OrderByDescending(h => h.StartDate)
            .FirstOrDefault();

        currentHalf ??= season.Halves.OrderBy(h => h.HalfNumber).FirstOrDefault();

        if (currentHalf is null)
            return Result<MostImprovedResultDto>.Fail("No season halves configured.");

        var halfStartDate = currentHalf.StartDate;

        // Get all finalized rounds in this half to count per-player participation
        var rounds = await _roundRepository.GetByHalfAsync(currentHalf.Id, cancellationToken);
        var finalizedRounds = rounds
            .Where(r => r.Status == RoundStatus.Finalized)
            .ToList();

        if (finalizedRounds.Count == 0)
        {
            return Result<MostImprovedResultDto>.Ok(new MostImprovedResultDto(
                null, [], currentHalf.Name, MinRounds));
        }

        // Count finalized rounds per player
        var roundsPerPlayer = new Dictionary<int, int>();
        var playerNames = new Dictionary<int, string>();
        foreach (var round in finalizedRounds)
        {
            var participants = await _roundRepository.GetParticipantsAsync(round.Id, cancellationToken);
            foreach (var p in participants)
            {
                if (p.IsWithdrawn || p.SkippedWeek || !p.TotalGrossStrokes.HasValue)
                    continue;

                roundsPerPlayer[p.PlayerId] = roundsPerPlayer.GetValueOrDefault(p.PlayerId) + 1;
                playerNames.TryAdd(p.PlayerId, p.Player?.FullName ?? $"Player #{p.PlayerId}");
            }
        }

        // Filter to players with enough rounds
        var qualifyingPlayerIds = roundsPerPlayer
            .Where(kv => kv.Value >= MinRounds)
            .Select(kv => kv.Key)
            .ToList();

        if (qualifyingPlayerIds.Count == 0)
        {
            return Result<MostImprovedResultDto>.Ok(new MostImprovedResultDto(
                null, [], currentHalf.Name, MinRounds));
        }

        // For each qualifying player, find their starting and current handicap
        var leaderboard = new List<MostImprovedPlayerDto>();

        foreach (var playerId in qualifyingPlayerIds)
        {
            var history = await _handicapRepository.GetHistoryAsync(playerId, cancellationToken);
            if (history.Count < 2)
                continue; // Need at least a starting and current handicap

            var ordered = history.OrderBy(h => h.EffectiveDate).ThenBy(h => h.Id).ToList();

            // Starting handicap: the latest handicap on or before the half start date.
            // If none exist before the half, use the earliest handicap in the history.
            var startingHandicap = ordered
                .Where(h => h.EffectiveDate <= halfStartDate)
                .OrderByDescending(h => h.EffectiveDate)
                .ThenByDescending(h => h.Id)
                .FirstOrDefault();

            startingHandicap ??= ordered.First();

            // Current handicap: the most recent one
            var currentHandicap = ordered.Last();

            // Only count if the current handicap is strictly after the starting one
            if (currentHandicap.Id == startingHandicap.Id)
                continue;

            // USGA Rule 7e: Improvement Factor = (A) / (B)
            //   A = Starting Handicap Index + 12
            //   B = Current Handicap Index + 12
            var a = startingHandicap.HandicapIndex + 12.0;
            var b = currentHandicap.HandicapIndex + 12.0;

            // Guard against division by zero (shouldn't happen with +12)
            if (b <= 0) continue;

            var improvementFactor = Math.Round(a / b, 3);
            var reduction = startingHandicap.HandicapIndex - currentHandicap.HandicapIndex;

            leaderboard.Add(new MostImprovedPlayerDto(
                playerId,
                playerNames[playerId],
                currentHalf.Name,
                Math.Round(startingHandicap.HandicapIndex, 1),
                Math.Round(currentHandicap.HandicapIndex, 1),
                improvementFactor,
                Math.Round(reduction, 1),
                roundsPerPlayer[playerId]));
        }

        // Highest improvement factor wins (>1.000 means handicap went down)
        leaderboard = leaderboard
            .OrderByDescending(p => p.ImprovementFactor)
            .ToList();

        var winner = leaderboard.FirstOrDefault(p => p.ImprovementFactor > 1.0);

        return Result<MostImprovedResultDto>.Ok(new MostImprovedResultDto(
            winner, leaderboard, currentHalf.Name, MinRounds));
    }
}
