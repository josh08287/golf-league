using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;
using static GolfLeague.Domain.Services.StablefordScoringService;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Adds a single weekly round to a half (e.g. an admin appending a make-up week).
/// All flights in the half become participants. NineHoleSide is admin-supplied
/// (or auto-alternated from the previous round if not provided).
/// </summary>
public sealed record CreateRoundCommand(
    int HalfId,
    int CourseId,
    DateOnly RoundDate,
    NineHoleSide? NineHoleSide,
    string? Notes,
    string UserId) : IRequest<Result<RoundDto>>, IAmAuditableCommand;

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
        var half = await _flightRepository.GetHalfByIdAsync(request.HalfId, cancellationToken);
        if (half is null)
            return Result<RoundDto>.Fail($"Half with ID {request.HalfId} not found.");

        var course = await _courseRepository.GetByIdAsync(request.CourseId, cancellationToken);
        if (course is null)
            return Result<RoundDto>.Fail($"Course with ID {request.CourseId} not found.");

        var flights = await _flightRepository.GetByHalfAsync(request.HalfId, cancellationToken);
        if (flights.Count == 0)
            return Result<RoundDto>.Fail("Half has no flights. Create flights first.");

        var existing = await _roundRepository.GetByHalfAsync(request.HalfId, cancellationToken);
        var nextWeek = existing.Count == 0 ? 1 : existing.Max(r => r.WeekNumber) + 1;

        var side = request.NineHoleSide ?? NextSide(existing);

        var round = new Round
        {
            SeasonId = half.SeasonId,
            HalfId = half.Id,
            CourseId = course.Id,
            WeekNumber = nextWeek,
            RoundDate = request.RoundDate,
            Status = RoundStatus.Scheduled,
            NineHoleSide = side,
            Notes = request.Notes,
        };

        await _roundRepository.AddAsync(round, cancellationToken);

        var totalParticipants = 0;
        foreach (var flight in flights)
        {
            var memberships = await _flightRepository.GetMembershipsAsync(flight.Id, cancellationToken);
            foreach (var membership in memberships)
            {
                var player = await _playerRepository.GetByIdAsync(membership.PlayerId, cancellationToken);
                if (player is null || !player.IsActive) continue;

                var current = await _handicapRepository.GetCurrentAsync(membership.PlayerId, cancellationToken);
                var index = current?.HandicapIndex ?? 0.0;

                await _roundRepository.AddParticipantAsync(new RoundParticipant
                {
                    RoundId = round.Id,
                    PlayerId = membership.PlayerId,
                    FlightId = flight.Id,
                    HandicapIndex = index,
                    CourseHandicap = CourseHandicap(index, course.SlopeRating, RoundType.NineHole),
                    IsWithdrawn = false,
                }, cancellationToken);
                totalParticipants++;
            }
        }

        return Result<RoundDto>.Ok(RoundDtoMapper.Map(round, course.Name, totalParticipants));
    }

    private static NineHoleSide NextSide(IReadOnlyList<Round> existing)
    {
        if (existing.Count == 0) return NineHoleSide.Front;
        var last = existing.OrderBy(r => r.WeekNumber).Last();
        return last.NineHoleSide == NineHoleSide.Front ? NineHoleSide.Back : NineHoleSide.Front;
    }
}
