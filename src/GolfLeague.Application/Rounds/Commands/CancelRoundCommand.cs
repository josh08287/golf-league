using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;
using static GolfLeague.Domain.Services.StablefordScoringService;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Cancels a scheduled round. The cancelled round keeps its date and side for history.
/// A make-up round with the same NineHoleSide is appended one week after the half's
/// last round; Half 1's EndDate extends, and Half 2's start/end dates shift forward
/// by 7 days so the season schedule remains contiguous.
/// </summary>
public sealed record CancelRoundCommand(int RoundId, string UserId)
    : IRequest<Result<RoundDto>>, IAmAuditableCommand;

public sealed class CancelRoundCommandHandler : IRequestHandler<CancelRoundCommand, Result<RoundDto>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly IFlightRepository _flightRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IHandicapRepository _handicapRepository;

    public CancelRoundCommandHandler(
        IRoundRepository roundRepository,
        IFlightRepository flightRepository,
        ICourseRepository courseRepository,
        IPlayerRepository playerRepository,
        IHandicapRepository handicapRepository)
    {
        _roundRepository = roundRepository;
        _flightRepository = flightRepository;
        _courseRepository = courseRepository;
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
    }

    public async Task<Result<RoundDto>> Handle(CancelRoundCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<RoundDto>.Fail($"Round with ID {request.RoundId} not found.");

        if (round.Status == RoundStatus.Cancelled)
            return Result<RoundDto>.Fail("Round is already cancelled.");
        if (round.Status == RoundStatus.Finalized)
            return Result<RoundDto>.Fail("Cannot cancel a finalized round.");

        round.Status = RoundStatus.Cancelled;
        await _roundRepository.UpdateAsync(round, cancellationToken);

        // Append a make-up round one week after the half's current last round, same side.
        var halfRounds = await _roundRepository.GetByHalfAsync(round.HalfId, cancellationToken);
        var lastDate = halfRounds.Where(r => r.Status != RoundStatus.Cancelled)
                                  .Select(r => r.RoundDate)
                                  .DefaultIfEmpty(round.RoundDate)
                                  .Max();
        var makeupDate = lastDate.AddDays(7);
        var nextWeek = halfRounds.Max(r => r.WeekNumber) + 1;

        var course = await _courseRepository.GetByIdAsync(round.CourseId, cancellationToken);
        if (course is null)
            return Result<RoundDto>.Fail($"Course with ID {round.CourseId} not found.");

        var flights = await _flightRepository.GetByHalfAsync(round.HalfId, cancellationToken);

        var makeup = new Round
        {
            SeasonId = round.SeasonId,
            HalfId = round.HalfId,
            CourseId = round.CourseId,
            WeekNumber = nextWeek,
            RoundDate = makeupDate,
            Status = RoundStatus.Scheduled,
            NineHoleSide = round.NineHoleSide, // preserve the side that was missed
            Notes = $"Make-up for cancelled round on {round.RoundDate:yyyy-MM-dd}",
        };
        await _roundRepository.AddAsync(makeup, cancellationToken);

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
                    RoundId = makeup.Id,
                    PlayerId = membership.PlayerId,
                    FlightId = flight.Id,
                    HandicapIndex = index,
                    CourseHandicap = CourseHandicap(index, course.SlopeRating, RoundType.NineHole),
                    IsWithdrawn = false,
                }, cancellationToken);
            }
        }

        // Extend the half's end date and shift the second half forward by 7 days.
        var thisHalf = await _flightRepository.GetHalfByIdAsync(round.HalfId, cancellationToken);
        if (thisHalf is not null)
        {
            if (makeupDate > thisHalf.EndDate)
            {
                thisHalf.EndDate = makeupDate;
                await _flightRepository.UpdateHalfAsync(thisHalf, cancellationToken);
            }

            if (thisHalf.HalfNumber == 1)
            {
                var halves = await _flightRepository.GetHalvesBySeasonAsync(thisHalf.SeasonId, cancellationToken);
                var second = halves.FirstOrDefault(h => h.HalfNumber == 2);
                if (second is not null)
                {
                    second.StartDate = second.StartDate.AddDays(7);
                    second.EndDate = second.EndDate.AddDays(7);
                    await _flightRepository.UpdateHalfAsync(second, cancellationToken);
                }
            }
        }

        return Result<RoundDto>.Ok(RoundDtoMapper.Map(round, course.Name, round.Participants.Count));
    }
}
