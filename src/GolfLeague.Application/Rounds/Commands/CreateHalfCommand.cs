using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using MediatR;
using static GolfLeague.Domain.Services.StablefordScoringService;

namespace GolfLeague.Application.Rounds.Commands;

public sealed record CreateHalfCommand(
    int SeasonId,
    int CourseId,
    DateOnly StartDate,
    IReadOnlyList<DateOnly> RoundDates,
    string UserId,
    RoundType RoundType = RoundType.NineHole,
    IReadOnlyList<NineHoleSide>? NineHoleSides = null) : IRequest<Result<List<RoundDto>>>, IAmAuditableCommand;

public sealed class CreateHalfCommandHandler : IRequestHandler<CreateHalfCommand, Result<List<RoundDto>>>
{
    private const int PreferredFlightSize = 8;

    private readonly IFlightRepository _flightRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IHandicapRepository _handicapRepository;
    private readonly IRoundRepository _roundRepository;

    public CreateHalfCommandHandler(
        IFlightRepository flightRepository,
        ICourseRepository courseRepository,
        IPlayerRepository playerRepository,
        IHandicapRepository handicapRepository,
        IRoundRepository roundRepository)
    {
        _flightRepository = flightRepository;
        _courseRepository = courseRepository;
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
        _roundRepository = roundRepository;
    }

    public async Task<Result<List<RoundDto>>> Handle(CreateHalfCommand request, CancellationToken cancellationToken)
    {
        if (request.RoundDates.Count == 0)
            return Result<List<RoundDto>>.Fail("At least one round date is required.");

        var course = await _courseRepository.GetByIdAsync(request.CourseId, cancellationToken);
        if (course is null)
            return Result<List<RoundDto>>.Fail($"Course with ID {request.CourseId} not found.");

        var activePlayers = await _playerRepository.GetAllActiveAsync(cancellationToken);
        if (activePlayers.Count == 0)
            return Result<List<RoundDto>>.Fail("At least one active player is required to create a half.");

        var playerHandicaps = new List<(Player Player, double HandicapIndex)>();
        foreach (var player in activePlayers)
        {
            var handicap = await _handicapRepository.GetCurrentAsync(player.Id, cancellationToken);
            playerHandicaps.Add((player, handicap?.HandicapIndex ?? 0.0));
        }

        var orderedPlayers = playerHandicaps
            .OrderBy(p => p.HandicapIndex)
            .ThenBy(p => p.Player.LastName)
            .ThenBy(p => p.Player.FirstName)
            .ToList();

        var half = new SeasonHalf
        {
            SeasonId = request.SeasonId,
            Name = $"Half starting {request.StartDate:yyyy-MM-dd}",
            StartDate = request.StartDate,
            EndDate = request.RoundDates.Max(),
            CreatedAt = DateTime.UtcNow
        };

        var flightSizes = CalculateFlightSizes(orderedPlayers.Count);
        var offset = 0;
        for (var i = 0; i < flightSizes.Count; i++)
        {
            var flight = new Flight
            {
                SeasonId = request.SeasonId,
                Half = half,
                Name = $"Flight {(char)('A' + i)}",
                DisplayOrder = i + 1,
            };

            foreach (var player in orderedPlayers.Skip(offset).Take(flightSizes[i]))
            {
                flight.Memberships.Add(new FlightMembership
                {
                    PlayerId = player.Player.Id,
                    SeasonId = request.SeasonId,
                    Half = half,
                    JoinedAt = DateTime.UtcNow
                });
            }

            half.Flights.Add(flight);
            offset += flightSizes[i];
        }

        await _flightRepository.AddHalfAsync(half, cancellationToken);

        var createdRounds = new List<RoundDto>();
        for (var roundIndex = 0; roundIndex < request.RoundDates.Count; roundIndex++)
        {
            var firstFlight = half.Flights.OrderBy(f => f.DisplayOrder).First();
            var nineHoleSide = request.NineHoleSides is not null && roundIndex < request.NineHoleSides.Count
                ? request.NineHoleSides[roundIndex]
                : NineHoleSide.Front;

            var round = new Round
            {
                SeasonId = request.SeasonId,
                HalfId = half.Id,
                FlightId = firstFlight.Id,
                CourseId = request.CourseId,
                RoundDate = request.RoundDates[roundIndex],
                Status = RoundStatus.Scheduled,
                RoundType = request.RoundType,
                NineHoleSide = nineHoleSide,
            };

            await _roundRepository.AddAsync(round, cancellationToken);

            var totalParticipants = 0;
            foreach (var flight in half.Flights.OrderBy(f => f.DisplayOrder))
            {
                foreach (var membership in flight.Memberships)
                {
                    var handicapIndex = playerHandicaps.First(p => p.Player.Id == membership.PlayerId).HandicapIndex;
                    var participant = new RoundParticipant
                    {
                        RoundId = round.Id,
                        PlayerId = membership.PlayerId,
                        FlightId = flight.Id,
                        HandicapIndex = handicapIndex,
                        CourseHandicap = CourseHandicap(handicapIndex, course.SlopeRating, request.RoundType),
                        IsWithdrawn = false
                    };

                    await _roundRepository.AddParticipantAsync(participant, cancellationToken);
                    totalParticipants++;
                }
            }

            createdRounds.Add(new RoundDto(
                round.Id,
                round.SeasonId,
                round.FlightId,
                $"{half.Flights.Count} Flights",
                round.CourseId,
                course.Name,
                round.RoundDate,
                round.Status,
                round.RoundType,
                round.NineHoleSide,
                totalParticipants));
        }

        return Result<List<RoundDto>>.Ok(createdRounds);
    }

    private static List<int> CalculateFlightSizes(int playerCount)
    {
        var flightCount = Math.Max(1, (int)Math.Ceiling(playerCount / (double)PreferredFlightSize));
        var sizes = Enumerable.Repeat(playerCount / flightCount, flightCount).ToList();
        for (var i = 0; i < playerCount % flightCount; i++)
            sizes[i]++;
        return sizes;
    }
}
