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
    /// A player needs at least 2 rounds so we can compare first vs last.
    /// </summary>
    private const int MinRounds = 2;

    private readonly ISeasonRepository _seasonRepository;
    private readonly IRoundRepository _roundRepository;
    private readonly IHandicapRepository _handicapRepository;

    public GetMostImprovedPlayerQueryHandler(
        ISeasonRepository seasonRepository,
        IRoundRepository roundRepository,
        IHandicapRepository handicapRepository)
    {
        _seasonRepository = seasonRepository;
        _roundRepository = roundRepository;
        _handicapRepository = handicapRepository;
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

        // Get all finalized rounds in this half, ordered chronologically
        var rounds = await _roundRepository.GetByHalfAsync(currentHalf.Id, cancellationToken);
        var finalizedRounds = rounds
            .Where(r => r.Status == RoundStatus.Finalized)
            .OrderBy(r => r.RoundDate)
            .ThenBy(r => r.WeekNumber)
            .ToList();

        if (finalizedRounds.Count == 0)
        {
            return Result<MostImprovedResultDto>.Ok(new MostImprovedResultDto(
                null, [], currentHalf.Name, MinRounds));
        }

        // Count finalized rounds per player and collect names
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

        // For each qualifying player, look up their handicap history to find:
        //   Starting HI = latest handicap effective on or before the half start date
        //   Current HI  = their most recent handicap (the latest entry overall)
        var leaderboard = new List<MostImprovedPlayerDto>();

        foreach (var playerId in qualifyingPlayerIds)
        {
            var history = await _handicapRepository.GetHistoryAsync(playerId, cancellationToken);
            if (history.Count == 0)
                continue;

            var ordered = history.OrderBy(h => h.EffectiveDate).ThenBy(h => h.Id).ToList();

            // Starting HI: the most recent handicap on or before the half start date
            var startingRecord = ordered
                .Where(h => h.EffectiveDate < halfStartDate)
                .OrderByDescending(h => h.EffectiveDate)
                .ThenByDescending(h => h.Id)
                .FirstOrDefault();

            // If no handicap existed before the half started, use the earliest one
            startingRecord ??= ordered.First();

            // Current HI: the player's latest handicap record
            var currentRecord = ordered.Last();

            // Need distinct records to compare
            if (currentRecord.Id == startingRecord.Id)
                continue;

            var startingHi = startingRecord.HandicapIndex;
            var currentHi = currentRecord.HandicapIndex;

            // USGA Rule 7e: Improvement Factor = (A) / (B)
            //   A = Starting Handicap Index + 12
            //   B = Current Handicap Index + 12
            var a = startingHi + 12.0;
            var b = currentHi + 12.0;

            if (b <= 0) continue;

            var improvementFactor = Math.Round(a / b, 3);
            var reduction = startingHi - currentHi;

            leaderboard.Add(new MostImprovedPlayerDto(
                playerId,
                playerNames[playerId],
                currentHalf.Name,
                Math.Round(startingHi, 1),
                Math.Round(currentHi, 1),
                improvementFactor,
                Math.Round(reduction, 1),
                roundsPerPlayer[playerId]));
        }

        // Highest improvement factor wins (>1.000 means handicap went down)
        leaderboard = leaderboard
            .OrderByDescending(p => p.ImprovementFactor)
            .ToList();

        var winner = leaderboard.FirstOrDefault();

        return Result<MostImprovedResultDto>.Ok(new MostImprovedResultDto(
            winner, leaderboard, currentHalf.Name, MinRounds));
    }
}
