using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
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
        .Add("hcp", s => s.CurrentHandicapIndex)
        .Add("currentHandicapIndex", s => s.CurrentHandicapIndex);

    public GetFlightStandingsQueryHandler(
        IFlightRepository flightRepository,
        IHandicapRepository handicapRepository,
        IPlayerRepository playerRepository)
    {
        _flightRepository = flightRepository;
        _handicapRepository = handicapRepository;
        _playerRepository = playerRepository;
    }

    public async Task<Result<List<StandingDto>>> Handle(GetFlightStandingsQuery request, CancellationToken cancellationToken)
    {
        var flight = await _flightRepository.GetByIdAsync(request.FlightId, cancellationToken);
        if (flight is null)
            return Result<List<StandingDto>>.Fail($"Flight with ID {request.FlightId} not found.");

        var participants = await _flightRepository.GetStandingsAsync(request.FlightId, request.HalfId, cancellationToken);

        var grouped = participants
            .Where(rp => !rp.IsWithdrawn)
            .GroupBy(rp => rp.PlayerId)
            .ToList();

        var dtos = new List<StandingDto>(grouped.Count);

        foreach (var group in grouped)
        {
            var player = await _playerRepository.GetByIdAsync(group.Key, cancellationToken);
            if (player is null)
                continue;

            var totalNet = group.Sum(rp => rp.TotalNetStablefordPoints ?? 0);
            var totalGross = group.Sum(rp => rp.TotalGrossStablefordPoints ?? 0);
            var totalPoints = request.UseGrossPoints ? totalGross : totalNet;

            var roundsPlayed = group.Count();
            var avgPoints = roundsPlayed > 0 ? (double)totalPoints / roundsPlayed : 0.0;

            var currentHandicap = await _handicapRepository.GetCurrentAsync(group.Key, cancellationToken);

            dtos.Add(new StandingDto(
                Position: 0,
                PlayerId: player.Id,
                PlayerFullName: player.FullName,
                PlayerInitials: player.Initials,
                RoundsPlayed: roundsPlayed,
                TotalPoints: totalPoints,
                AveragePoints: Math.Round(avgPoints, 2),
                CurrentHandicapIndex: currentHandicap?.HandicapIndex ?? 0.0));
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
