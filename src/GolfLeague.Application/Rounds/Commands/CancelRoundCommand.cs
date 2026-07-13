using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Reschedules a round by pushing it forward one week. The round itself and all
/// subsequent non-cancelled rounds in the same half shift +7 days and +1 week number.
/// The half's end date (and the second half's window if this is the first half) also
/// extend by 7 days so the schedule remains sequential with no gaps.
/// </summary>
public sealed record CancelRoundCommand(int RoundId, string UserId)
    : IRequest<Result<RoundDto>>, IAmAuditableCommand
{
    public string AuditEntityType => "Round";
    public string AuditEntityId => RoundId.ToString();
}

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
        if (!round.HalfId.HasValue)
            return Result<RoundDto>.Fail("Tournament rounds cannot be rescheduled.");

        var course = await _courseRepository.GetByIdAsync(round.CourseId, cancellationToken);
        if (course is null)
            return Result<RoundDto>.Fail($"Course with ID {round.CourseId} not found.");

        // Shift the target round forward one week
        round.RoundDate = round.RoundDate.AddDays(7);
        round.WeekNumber += 1;
        await _roundRepository.UpdateAsync(round, cancellationToken);

        // Shift all subsequent non-cancelled rounds in this half forward one week
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

        return Result<RoundDto>.Ok(RoundDtoMapper.Map(round, course.Name, round.Participants.Count));
    }
}
