using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Cancels a scheduled round. All non-cancelled rounds in the same half with a higher
/// week number are shifted forward by 7 days (date and week number) so the schedule
/// remains sequential with no gaps. The half's end date and any subsequent half's
/// dates are extended by the same 7 days.
/// </summary>
public sealed record CancelRoundCommand(int RoundId, string UserId)
    : IRequest<Result<RoundDto>>, IAmAuditableCommand;

public sealed class CancelRoundCommandHandler : IRequestHandler<CancelRoundCommand, Result<RoundDto>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly IFlightRepository _flightRepository;
    private readonly ICourseRepository _courseRepository;

    public CancelRoundCommandHandler(
        IRoundRepository roundRepository,
        IFlightRepository flightRepository,
        ICourseRepository courseRepository)
    {
        _roundRepository = roundRepository;
        _flightRepository = flightRepository;
        _courseRepository = courseRepository;
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

        var course = await _courseRepository.GetByIdAsync(round.CourseId, cancellationToken);
        if (course is null)
            return Result<RoundDto>.Fail($"Course with ID {round.CourseId} not found.");

        // Mark cancelled
        round.Status = RoundStatus.Cancelled;
        await _roundRepository.UpdateAsync(round, cancellationToken);

        // Tournament rounds have no half — no schedule shift needed
        if (round.HalfId.HasValue)
        {
            // Shift every later non-cancelled round in this half forward by one week
            await _roundRepository.ShiftRoundsForwardAsync(
                round.HalfId.Value,
                afterWeekNumber: round.WeekNumber,
                daysToAdd: 7,
                weekNumberIncrement: 1,
                cancellationToken);

            // Extend the half's end date by 7 days to accommodate the shifted schedule
            var thisHalf = await _flightRepository.GetHalfByIdAsync(round.HalfId.Value, cancellationToken);
            if (thisHalf is not null)
            {
                thisHalf.EndDate = thisHalf.EndDate.AddDays(7);
                await _flightRepository.UpdateHalfAsync(thisHalf, cancellationToken);

                // If this is the first half, shift the second half's window forward too
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
        }

        return Result<RoundDto>.Ok(RoundDtoMapper.Map(round, course.Name, round.Participants.Count));
    }
}
