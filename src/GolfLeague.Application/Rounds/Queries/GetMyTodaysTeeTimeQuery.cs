using GolfLeague.Application.Common;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Queries;

/// <summary>
/// Information about a tee time slot for today (if the player is in a round).
/// </summary>
public sealed record MyTodaysTeeTimeDto(
    int RoundId,
    DateOnly RoundDate,
    string CourseName,
    int CourseId,
    NineHoleSide NineHoleSide,
    RoundStatus RoundStatus,
    int TeeTimeId,
    TimeOnly ScheduledTime,
    string ScheduledTimeFormatted, // "15:28"
    int TeeTimeNumber,
    bool CanEnterScores); // true if round is Scheduled or InProgress

public sealed record GetMyTodaysTeeTimeQuery(int PlayerId, DateOnly Today)
    : IRequest<Result<MyTodaysTeeTimeDto?>>;

public sealed class GetMyTodaysTeeTimeQueryHandler
    : IRequestHandler<GetMyTodaysTeeTimeQuery, Result<MyTodaysTeeTimeDto?>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ITeeTimeRepository _teeTimeRepository;

    public GetMyTodaysTeeTimeQueryHandler(
        IRoundRepository roundRepository,
        ITeeTimeRepository teeTimeRepository)
    {
        _roundRepository = roundRepository;
        _teeTimeRepository = teeTimeRepository;
    }

    public async Task<Result<MyTodaysTeeTimeDto?>> Handle(
        GetMyTodaysTeeTimeQuery request,
        CancellationToken cancellationToken)
    {
        // Find the round scheduled for today
        var rounds = await _roundRepository.GetAllAsync(cancellationToken);
        var todaysRound = rounds
            .FirstOrDefault(r => r.RoundDate == request.Today &&
                                  r.Status != RoundStatus.Cancelled);

        if (todaysRound is null)
            return Result<MyTodaysTeeTimeDto?>.Ok(null);

        // Check if player is a participant in this round
        var participant = todaysRound.Participants
            .FirstOrDefault(p => p.PlayerId == request.PlayerId &&
                                !p.IsWithdrawn &&
                                !p.SkippedWeek);

        if (participant is null)
            return Result<MyTodaysTeeTimeDto?>.Ok(null);

        // Check if player has a tee time assignment
        if (participant.TeeTimeId is null)
            return Result<MyTodaysTeeTimeDto?>.Ok(null);

        // Get tee time details
        var teeTimes = await _teeTimeRepository.GetByRoundAsync(todaysRound.Id, cancellationToken);
        var myTeeTime = teeTimes.FirstOrDefault(t => t.Id == participant.TeeTimeId);

        if (myTeeTime is null)
            return Result<MyTodaysTeeTimeDto?>.Ok(null);

        var canEnterScores = todaysRound.Status == RoundStatus.Scheduled ||
                            todaysRound.Status == RoundStatus.InProgress;

        var dto = new MyTodaysTeeTimeDto(
            todaysRound.Id,
            todaysRound.RoundDate,
            todaysRound.Course.Name,
            todaysRound.CourseId,
            todaysRound.NineHoleSide,
            todaysRound.Status,
            myTeeTime.Id,
            myTeeTime.ScheduledTime,
            myTeeTime.ScheduledTime.ToString("HH:mm"),
            myTeeTime.TeeTimeNumber,
            canEnterScores);

        return Result<MyTodaysTeeTimeDto?>.Ok(dto);
    }
}
