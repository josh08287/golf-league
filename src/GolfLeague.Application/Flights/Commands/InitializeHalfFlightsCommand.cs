using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Flights.Commands;

/// <summary>
/// Initializes flights for a half from scratch:
///  - Deletes any existing flights (and memberships) for the half.
///  - Sorts all active players by current handicap index (lowest first).
///  - Assigns them to named flights (A, B, C…) with a max of <see cref="MaxPlayersPerFlight"/> per flight.
///  - Persists FlightMembership rows for every player.
/// Fails if the half already has any InProgress or Finalized rounds (i.e., it is locked).
/// </summary>
public sealed record InitializeHalfFlightsCommand(
    int HalfId,
    string UserId,
    int MaxPlayersPerFlight = 8) : IRequest<Result<List<FlightDto>>>, IAmAuditableCommand;

public sealed class InitializeHalfFlightsCommandHandler
    : IRequestHandler<InitializeHalfFlightsCommand, Result<List<FlightDto>>>
{
    private static readonly string[] FlightNames = ["A", "B", "C", "D", "E", "F", "G", "H"];

    private readonly IFlightRepository _flightRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IHandicapRepository _handicapRepository;

    public InitializeHalfFlightsCommandHandler(
        IFlightRepository flightRepository,
        IPlayerRepository playerRepository,
        IHandicapRepository handicapRepository)
    {
        _flightRepository = flightRepository;
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
    }

    public async Task<Result<List<FlightDto>>> Handle(
        InitializeHalfFlightsCommand request,
        CancellationToken cancellationToken)
    {
        var half = await _flightRepository.GetHalfByIdAsync(request.HalfId, cancellationToken);
        if (half is null)
            return Result<List<FlightDto>>.Fail($"Half with ID {request.HalfId} not found.");

        // Lock check — refuse if any round in this half has started
        if (await _flightRepository.IsHalfLockedAsync(request.HalfId, cancellationToken))
            return Result<List<FlightDto>>.Fail(
                "Cannot re-initialize flights once rounds have started for this half.");

        // Remove existing flights for this half (cascades memberships via FK)
        var existingFlights = await _flightRepository.GetByHalfAsync(request.HalfId, cancellationToken);
        foreach (var f in existingFlights)
            await _flightRepository.DeleteAsync(f.Id, cancellationToken);

        // Load all active players with their current handicap
        var players = await _playerRepository.GetAllActiveAsync(cancellationToken);
        var playerHandicaps = new List<(Player Player, double HcpIndex)>();

        foreach (var player in players)
        {
            var hcp = await _handicapRepository.GetCurrentAsync(player.Id, cancellationToken);
            playerHandicaps.Add((player, hcp?.HandicapIndex ?? 99.0));
        }

        // Sort low→high handicap (best players in A flight)
        var sorted = playerHandicaps
            .OrderBy(p => p.HcpIndex)
            .ThenBy(p => p.Player.LastName)
            .ThenBy(p => p.Player.FirstName)
            .ToList();

        var max = Math.Max(1, request.MaxPlayersPerFlight);
        var flightCount = (int)Math.Ceiling((double)sorted.Count / max);
        flightCount = Math.Min(flightCount, FlightNames.Length);

        var createdFlights = new List<(Flight Flight, List<(Player Player, double HcpIndex)> Members)>();

        for (int i = 0; i < flightCount; i++)
        {
            var flight = new Flight
            {
                Name = $"{FlightNames[i]} Flight",
                SeasonId = half.SeasonId,
                HalfId = half.Id,
                DisplayOrder = i,
            };
            await _flightRepository.AddAsync(flight, cancellationToken);

            var slice = sorted.Skip(i * max).Take(max).ToList();
            createdFlights.Add((flight, slice));
        }

        // Assign memberships
        foreach (var (flight, members) in createdFlights)
        {
            foreach (var (player, _) in members)
            {
                await _flightRepository.AddMembershipAsync(new FlightMembership
                {
                    PlayerId = player.Id,
                    FlightId = flight.Id,
                    SeasonId = flight.SeasonId,
                    HalfId = flight.HalfId,
                    JoinedAt = DateTime.UtcNow,
                }, cancellationToken);
            }
        }

        var dtos = createdFlights.Select((cf, _) =>
            new FlightDto(cf.Flight.Id, cf.Flight.SeasonId, cf.Flight.HalfId,
                          cf.Flight.Name, cf.Flight.DisplayOrder, cf.Members.Count))
            .ToList();

        return Result<List<FlightDto>>.Ok(dtos);
    }
}
