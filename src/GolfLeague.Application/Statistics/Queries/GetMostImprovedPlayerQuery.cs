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

    public GetMostImprovedPlayerQueryHandler(
        ISeasonRepository seasonRepository,
        IRoundRepository roundRepository)
    {
        _seasonRepository = seasonRepository;
        _roundRepository = roundRepository;
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

        // Collect each player's participations in chronological round order
        // using the HandicapIndex recorded on the RoundParticipant
        var playerEntries = new Dictionary<int, List<(int RoundIndex, double HandicapIndex, string Name)>>();

        for (var i = 0; i < finalizedRounds.Count; i++)
        {
            var participants = await _roundRepository.GetParticipantsAsync(
                finalizedRounds[i].Id, cancellationToken);

            foreach (var p in participants)
            {
                if (p.IsWithdrawn || p.SkippedWeek || !p.TotalGrossStrokes.HasValue)
                    continue;

                if (!playerEntries.TryGetValue(p.PlayerId, out var list))
                {
                    list = [];
                    playerEntries[p.PlayerId] = list;
                }
                list.Add((i, p.HandicapIndex, p.Player?.FullName ?? $"Player #{p.PlayerId}"));
            }
        }

        // Build leaderboard for players with enough rounds
        var leaderboard = new List<MostImprovedPlayerDto>();

        foreach (var (playerId, entries) in playerEntries)
        {
            if (entries.Count < MinRounds)
                continue;

            var first = entries.First();  // first finalized round in the half
            var last = entries.Last();    // last finalized round in the half

            if (first.RoundIndex == last.RoundIndex)
                continue;

            var startingHi = first.HandicapIndex;
            var currentHi = last.HandicapIndex;

            // USGA Rule 7e: Improvement Factor = (A) / (B)
            //   A = Starting Handicap Index + 12
            //   B = Current Handicap Index + 12
            var a = startingHi + 12.0;
            var b = currentHi + 12.0;

            // Guard against division by zero (shouldn't happen with +12)
            if (b <= 0) continue;

            var improvementFactor = Math.Round(a / b, 3);
            var reduction = startingHi - currentHi;

            leaderboard.Add(new MostImprovedPlayerDto(
                playerId,
                first.Name,
                currentHalf.Name,
                Math.Round(startingHi, 1),
                Math.Round(currentHi, 1),
                improvementFactor,
                Math.Round(reduction, 1),
                entries.Count));
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
