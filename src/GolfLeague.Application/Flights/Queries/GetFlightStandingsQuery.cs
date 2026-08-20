using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Interfaces;
using GolfLeague.Application.Leagues;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Flights.Queries;

public sealed record GetFlightStandingsQuery(
    int FlightId,
    int HalfId,
    bool UseGrossPoints = false,
    SortRequest? Sort = null) : IRequest<Result<List<StandingDto>>>;

public sealed class GetFlightStandingsQueryHandler : IRequestHandler<GetFlightStandingsQuery, Result<List<StandingDto>>>
{
    private readonly IFlightRepository _flightRepository;
    private readonly IHandicapRepository _handicapRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly ILeagueSettingRepository _settings;
    private readonly ILeagueContext _leagueContext;

    /// <summary>
    /// Default sort: by Position, which is the league rank (highest total
    /// points, ties broken by higher average). Position is assigned based
    /// on this ranking before any user sort is applied, so re-sorting by
    /// other columns doesn't shuffle the positions.
    /// </summary>
    private static readonly SortMap<StandingDto> SortMap = new SortMap<StandingDto>(
            source => source.OrderBy(s => s.Position))
        .Add("position", s => s.Position)
        .Add("player", s => s.PlayerFullName)
        .Add("playerName", s => s.PlayerFullName)
        .Add("playerFullName", s => s.PlayerFullName)
        .Add("rounds", s => s.RoundsPlayed)
        .Add("roundsPlayed", s => s.RoundsPlayed)
        .Add("points", s => s.TotalPoints)
        .Add("totalPoints", s => s.TotalPoints)
        .Add("avg", s => s.AveragePoints)
        .Add("averagePoints", s => s.AveragePoints)
        .Add("score", s => s.AverageScore ?? double.MaxValue)
        .Add("averageScore", s => s.AverageScore ?? double.MaxValue)
        .Add("hcp", s => s.CurrentHandicapIndex)
        .Add("currentHandicapIndex", s => s.CurrentHandicapIndex);

    public GetFlightStandingsQueryHandler(
        IFlightRepository flightRepository,
        IHandicapRepository handicapRepository,
        IPlayerRepository playerRepository,
        ILeagueSettingRepository settings,
        ILeagueContext leagueContext)
    {
        _flightRepository = flightRepository;
        _handicapRepository = handicapRepository;
        _playerRepository = playerRepository;
        _settings = settings;
        _leagueContext = leagueContext;
    }

    public async Task<Result<List<StandingDto>>> Handle(GetFlightStandingsQuery request, CancellationToken cancellationToken)
    {
        var flight = await _flightRepository.GetByIdAsync(request.FlightId, cancellationToken);
        if (flight is null)
            return Result<List<StandingDto>>.Fail($"Flight with ID {request.FlightId} not found.");

        var dropCount = 1;
        if (_leagueContext.LeagueId.HasValue)
        {
            var dropSetting = await _settings.GetAsync(_leagueContext.LeagueId.Value, KnownSettings.StandingsDropCount, cancellationToken);
            if (dropSetting is not null && int.TryParse(dropSetting.Value, out var parsed) && parsed >= 0)
                dropCount = parsed;
        }

        var participants = await _flightRepository.GetStandingsAsync(request.FlightId, request.HalfId, cancellationToken);

        var grouped = participants
            .Where(rp => !rp.IsWithdrawn)
            .GroupBy(rp => rp.PlayerId)
            .ToList();

        // Batch-load players and current handicaps once instead of per-group
        // lookups — avoids 2 SQL round trips per player in the flight.
        var playerIds = grouped.Select(g => g.Key).ToHashSet();
        var playersById = (await _playerRepository.GetAllAsync(cancellationToken))
            .Where(p => playerIds.Contains(p.Id))
            .ToDictionary(p => p.Id);
        var currentHandicapByPlayerId = (await _handicapRepository.GetAllAsync(cancellationToken))
            .Where(h => playerIds.Contains(h.PlayerId))
            .GroupBy(h => h.PlayerId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(h => h.EffectiveDate).ThenByDescending(h => h.Id).First());

        var dtos = new List<StandingDto>(grouped.Count);

        foreach (var group in grouped)
        {
            if (!playersById.TryGetValue(group.Key, out var player))
                continue;

            var roundsPlayed = group.Count();

            // All rounds (including skipped 0-pt weeks) are candidates for dropping.
            var allRounds = group.ToList();

            // Drop the N lowest-scoring rounds from each player's totals.
            // Only drop up to (allRounds.Count - 1) so at least one round always counts.
            var effectiveDrop = Math.Min(dropCount, Math.Max(0, allRounds.Count - 1));
            var droppedRounds = allRounds
                .OrderBy(rp => request.UseGrossPoints
                    ? rp.TotalGrossStablefordPoints ?? 0
                    : rp.TotalNetStablefordPoints ?? 0)
                .Take(effectiveDrop)
                .ToHashSet();
            var countingRounds = allRounds.Where(rp => !droppedRounds.Contains(rp)).ToList();

            var totalPoints = countingRounds.Sum(rp =>
                request.UseGrossPoints
                    ? rp.TotalGrossStablefordPoints ?? 0
                    : rp.TotalNetStablefordPoints ?? 0);

            // Skipped weeks must not deflate the average even if they aren't dropped.
            var countingScored = countingRounds.Where(rp => !rp.SkippedWeek).ToList();
            var scoredCount = countingScored.Count;
            var avgPoints = scoredCount > 0
                ? (double)countingScored.Sum(rp =>
                    request.UseGrossPoints
                        ? rp.TotalGrossStablefordPoints ?? 0
                        : rp.TotalNetStablefordPoints ?? 0) / scoredCount
                : 0.0;

            var scoreList = countingScored.Where(rp =>
                request.UseGrossPoints ? rp.TotalGrossStrokes.HasValue : rp.TotalNetStrokes.HasValue).ToList();
            double? avgScore = scoreList.Count > 0
                ? Math.Round(
                    (double)(request.UseGrossPoints
                        ? scoreList.Sum(rp => rp.TotalGrossStrokes!.Value)
                        : scoreList.Sum(rp => rp.TotalNetStrokes!.Value)) / scoreList.Count, 1)
                : null;

            currentHandicapByPlayerId.TryGetValue(group.Key, out var currentHandicap);

            var roundScores = group
                .OrderBy(rp => rp.Round.WeekNumber)
                .Select(rp => new RoundScoreDto(
                    RoundId: rp.RoundId,
                    RoundDate: rp.Round.RoundDate,
                    WeekNumber: rp.Round.WeekNumber,
                    Points: request.UseGrossPoints ? rp.TotalGrossStablefordPoints : rp.TotalNetStablefordPoints,
                    GrossStrokes: rp.TotalGrossStrokes,
                    NetStrokes: rp.TotalNetStrokes,
                    IsSkipped: rp.SkippedWeek,
                    IsDropped: droppedRounds.Contains(rp)))
                .ToList();

            dtos.Add(new StandingDto(
                Position: 0,
                PlayerId: player.Id,
                PlayerFullName: player.FullName,
                PlayerInitials: player.Initials,
                RoundsPlayed: roundsPlayed,
                TotalPoints: totalPoints,
                AveragePoints: Math.Round(avgPoints, 2),
                CurrentHandicapIndex: currentHandicap?.HandicapIndex ?? 0.0,
                AverageScore: avgScore,
                RoundScores: roundScores));
        }

        // Position is the league rank based on the default ordering — assign
        // it BEFORE applying any user sort so the displayed position stays
        // meaningful even when the table is sorted by another column.
        var ranked = dtos
            .OrderByDescending(d => d.TotalPoints)
            .ThenByDescending(d => d.AveragePoints)
            .Select((d, index) => d with { Position = index + 1 })
            .ToList();

        var sorted = SortMap.Apply(ranked, request.Sort);
        return Result<List<StandingDto>>.Ok(sorted.ToList());
    }
}
