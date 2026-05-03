using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using MediatR;
using static GolfLeague.Domain.Services.StablefordScoringService;

namespace GolfLeague.Application.Rounds.Commands;

public sealed record CreateRoundCommand(
    int SeasonId,
    int? FlightId, // Optional - kept for backwards compatibility
    List<int> FlightIds, // Multiple flights for multi-flight rounds
    int CourseId,
    DateOnly RoundDate,
    string? Notes,
    string UserId,
    RoundType RoundType = RoundType.NineHole,
    NineHoleSide NineHoleSide = NineHoleSide.Front) : IRequest<Result<RoundDto>>, IAmAuditableCommand;

public sealed class CreateRoundCommandHandler : IRequestHandler<CreateRoundCommand, Result<RoundDto>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IHandicapRepository _handicapRepository;
    private readonly IFlightRepository _flightRepository;

    public CreateRoundCommandHandler(
        IRoundRepository roundRepository,
        ICourseRepository courseRepository,
        IPlayerRepository playerRepository,
        IHandicapRepository handicapRepository,
        IFlightRepository flightRepository)
    {
        _roundRepository = roundRepository;
        _courseRepository = courseRepository;
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
        _flightRepository = flightRepository;
    }

    public async Task<Result<RoundDto>> Handle(CreateRoundCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetByIdAsync(request.CourseId, cancellationToken);
        if (course is null)
            return Result<RoundDto>.Fail($"Course with ID {request.CourseId} not found.");

        // Validate all flights exist
        var flightIds = request.FlightIds.Count > 0 ? request.FlightIds : 
                        request.FlightId.HasValue ? new List<int> { request.FlightId.Value } : 
                        new List<int>();
        
        if (flightIds.Count == 0)
            return Result<RoundDto>.Fail("At least one flight is required.");

        var flights = new List<Flight>();
        foreach (var flightId in flightIds)
        {
            var flight = await _flightRepository.GetByIdAsync(flightId, cancellationToken);
            if (flight is null)
                return Result<RoundDto>.Fail($"Flight with ID {flightId} not found.");
            flights.Add(flight);
        }

        // Create round - no single flight association (or use first for backwards compatibility)
        var round = new Round
        {
            SeasonId = request.SeasonId,
            FlightId = request.FlightId ?? flightIds.First(), // Backwards compatibility
            CourseId = request.CourseId,
            RoundDate = request.RoundDate,
            Status = RoundStatus.Scheduled,
            RoundType = request.RoundType,
            NineHoleSide = request.NineHoleSide,
            Notes = request.Notes
        };

        await _roundRepository.AddAsync(round, cancellationToken);

        // Add all players from all flights as participants
        var totalParticipants = 0;
        foreach (var flight in flights)
        {
            var memberships = await _flightRepository.GetMembershipsAsync(flight.Id, cancellationToken);
            foreach (var membership in memberships)
            {
                var player = await _playerRepository.GetByIdAsync(membership.PlayerId, cancellationToken);
                if (player is null || !player.IsActive) continue;

                var currentHandicap = await _handicapRepository.GetCurrentAsync(membership.PlayerId, cancellationToken);
                var handicapIndex = currentHandicap?.HandicapIndex ?? 0.0;
                var courseHandicap = CourseHandicap(handicapIndex, course.SlopeRating, request.RoundType);

                var participant = new RoundParticipant
                {
                    RoundId = round.Id,
                    PlayerId = membership.PlayerId,
                    FlightId = flight.Id, // Track flight at time of round creation
                    HandicapIndex = handicapIndex,
                    CourseHandicap = courseHandicap,
                    IsWithdrawn = false
                };

                await _roundRepository.AddParticipantAsync(participant, cancellationToken);
                totalParticipants++;
            }
        }

        var dto = new RoundDto(
            round.Id,
            round.SeasonId,
            round.FlightId,
            flights.Count == 1 ? flights[0].Name : $"{flights.Count} Flights",
            round.CourseId,
            course.Name,
            round.RoundDate,
            round.Status,
            round.RoundType,
            round.NineHoleSide,
            totalParticipants);

        return Result<RoundDto>.Ok(dto);
    }
}
