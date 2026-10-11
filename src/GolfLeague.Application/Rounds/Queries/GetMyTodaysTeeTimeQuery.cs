using GolfLeague.Application.Common;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Queries;

/// <summary>
/// Information about the player's tee time for today's round, or for their most
/// recent round that hasn't been finalized yet.
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
    bool CanEnterScores); // true until the round is finalized (or cancelled)

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
        // Today's round, or else the player's most recent round that an admin
        // hasn't finalized yet — players can keep entering scores until then.
        var round = await _roundRepository.GetLatestScoreEntryRoundForPlayerAsync(
            request.PlayerId, request.Today, cancellationToken);
        if (round is null)
            return Result<MyTodaysTeeTimeDto?>.Ok(null);

        var participant = round.Participants
            .FirstOrDefault(p => p.PlayerId == request.PlayerId &&
                                !p.IsWithdrawn &&
                                !p.SkippedWeek);

        if (participant is null)
            return Result<MyTodaysTeeTimeDto?>.Ok(null);

        // Check if player has a tee time assignment
        if (participant.TeeTimeId is null)
            return Result<MyTodaysTeeTimeDto?>.Ok(null);

        // Get tee time details
        var teeTimes = await _teeTimeRepository.GetByRoundAsync(round.Id, cancellationToken);
        var myTeeTime = teeTimes.FirstOrDefault(t => t.Id == participant.TeeTimeId);

        if (myTeeTime is null)
            return Result<MyTodaysTeeTimeDto?>.Ok(null);

        var canEnterScores = round.Status is not (RoundStatus.Finalized or RoundStatus.Cancelled);

        var dto = new MyTodaysTeeTimeDto(
            round.Id,
            round.RoundDate,
            round.Course.Name,
            round.CourseId,
            round.NineHoleSide,
            round.Status,
            myTeeTime.Id,
            myTeeTime.ScheduledTime,
            myTeeTime.ScheduledTime.ToString("HH:mm"),
            myTeeTime.TeeTimeNumber,
            canEnterScores);

        return Result<MyTodaysTeeTimeDto?>.Ok(dto);
    }
}
