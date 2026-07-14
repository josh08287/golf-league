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

public sealed record GetMostImprovedPlayerQuery(int? SeasonId = null, int? HalfId = null, bool AllTime = false)
    : IRequest<Result<MostImprovedResultDto>>;

public sealed class GetMostImprovedPlayerQueryHandler
    : IRequestHandler<GetMostImprovedPlayerQuery, Result<MostImprovedResultDto>>
{
    /// <summary>
    /// Minimum finalized rounds a player must have in the period to qualify.
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
        string periodName;
        IReadOnlyList<Domain.Entities.Round> rounds;

        if (request.HalfId is int halfId)
        {
            // Resolve the half's display name across all seasons
            var seasons = await _seasonRepository.GetAllAsync(cancellationToken);
            var half = seasons.SelectMany(s => s.Halves).FirstOrDefault(h => h.Id == halfId);
            if (half is null)
                return Result<MostImprovedResultDto>.Fail("Season half not found.");

            periodName = half.Name;
            rounds = await _roundRepository.GetByHalfAsync(halfId, cancellationToken);
        }
        else if (request.SeasonId is int seasonId)
        {
            var season = await _seasonRepository.GetByIdAsync(seasonId, cancellationToken);
            if (season is null)
                return Result<MostImprovedResultDto>.Fail("Season not found.");

            periodName = season.Name;
            rounds = await _roundRepository.GetBySeasonAsync(seasonId, cancellationToken);
        }
        else if (request.AllTime)
        {
            periodName = "Overall";
            rounds = await _roundRepository.GetAllAsync(cancellationToken);
        }
        else
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

            periodName = currentHalf.Name;
            rounds = await _roundRepository.GetByHalfAsync(currentHalf.Id, cancellationToken);
        }

        var finalizedRounds = rounds
            .Where(r => r.Status == RoundStatus.Finalized)
            .OrderBy(r => r.RoundDate)
            .ThenBy(r => r.WeekNumber)
            .ToList();

        if (finalizedRounds.Count == 0)
        {
            return Result<MostImprovedResultDto>.Ok(new MostImprovedResultDto(
                null, [], periodName, MinRounds));
        }

        // Count finalized rounds per player and track their first round date
        var roundsPerPlayer = new Dictionary<int, int>();
        var playerNames = new Dictionary<int, string>();
        var firstRoundDatePerPlayer = new Dictionary<int, DateOnly>();

        foreach (var round in finalizedRounds)
        {
            var participants = await _roundRepository.GetParticipantsAsync(round.Id, cancellationToken);
            foreach (var p in participants)
            {
                if (p.IsWithdrawn || p.SkippedWeek || p.IsSubstitute || !p.TotalGrossStrokes.HasValue)
                    continue;

                roundsPerPlayer[p.PlayerId] = roundsPerPlayer.GetValueOrDefault(p.PlayerId) + 1;
                playerNames.TryAdd(p.PlayerId, p.Player?.FullName ?? $"Player #{p.PlayerId}");

                // Track earliest round date for each player in this half
                if (!firstRoundDatePerPlayer.TryGetValue(p.PlayerId, out var existing) ||
                    round.RoundDate < existing)
                {
                    firstRoundDatePerPlayer[p.PlayerId] = round.RoundDate;
                }
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
                null, [], periodName, MinRounds));
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

            // Starting HI: the most recent handicap recorded before the player's
            // first round in this half (not the half start date).
            var firstRoundDate = firstRoundDatePerPlayer[playerId];
            var startingRecord = ordered
                .Where(h => h.EffectiveDate < firstRoundDate)
                .OrderByDescending(h => h.EffectiveDate)
                .ThenByDescending(h => h.Id)
                .FirstOrDefault();

            // If no handicap existed before their first round, use the earliest one
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
                periodName,
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
            winner, leaderboard, periodName, MinRounds));
    }
}
