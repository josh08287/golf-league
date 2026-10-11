using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using MediatR;

namespace GolfLeague.Application.Rounds.Queries;

public sealed record GetRoundsQuery(
    int? SeasonId = null,
    int? HalfId = null,
    int Page = 1,
    int PageSize = 20,
    SortRequest? Sort = null) : IRequest<Result<PagedResult<RoundDto>>>;

public sealed class GetRoundsQueryHandler : IRequestHandler<GetRoundsQuery, Result<PagedResult<RoundDto>>>
{
    private readonly IRoundRepository _roundRepository;

    /// <summary>
    /// Default sort: round date ascending (chronological), then week number.
    /// </summary>
    private static readonly SortMap<RoundDto> SortMap = new SortMap<RoundDto>(
            source => source.OrderBy(r => r.ScheduledDate).ThenBy(r => r.WeekNumber))
        .Add("date", r => r.ScheduledDate)
        .Add("scheduledDate", r => r.ScheduledDate)
        .Add("course", r => r.CourseName)
        .Add("courseName", r => r.CourseName)
        .Add("week", r => r.WeekNumber)
        .Add("weekNumber", r => r.WeekNumber)
        .Add("status", r => r.Status.ToString())
        .Add("nineHoleSide", r => r.NineHoleSide.ToString())
        .Add("participants", r => r.ParticipantCount)
        .Add("participantCount", r => r.ParticipantCount);

    /// <summary>sortBy value for the schedule ordering (see <see cref="ScheduleOrder"/>); direction is ignored.</summary>
    public const string ScheduleSortColumn = "schedule";

    /// <summary>
    /// The next round first, then past rounds newest to oldest, then the
    /// remaining upcoming rounds soonest first. A round is upcoming when it's
    /// dated today or later and not finalized or cancelled.
    /// </summary>
    public static List<RoundDto> ScheduleOrder(IEnumerable<RoundDto> rounds, DateOnly today)
    {
        var all = rounds.ToList();
        bool IsUpcoming(RoundDto r) => r.ScheduledDate >= today && r.Status is not (RoundStatus.Finalized or RoundStatus.Cancelled);

        var upcoming = all.Where(IsUpcoming).OrderBy(r => r.ScheduledDate).ThenBy(r => r.WeekNumber).ThenBy(r => r.Id).ToList();
        var past = all.Where(r => !IsUpcoming(r)).OrderByDescending(r => r.ScheduledDate).ThenByDescending(r => r.WeekNumber).ThenByDescending(r => r.Id);

        return upcoming.Take(1).Concat(past).Concat(upcoming.Skip(1)).ToList();
    }

    public GetRoundsQueryHandler(IRoundRepository roundRepository)
    {
        _roundRepository = roundRepository;
    }

    public async Task<Result<PagedResult<RoundDto>>> Handle(GetRoundsQuery request, CancellationToken cancellationToken)
    {
        var rounds = request.HalfId.HasValue
            ? await _roundRepository.GetByHalfAsync(request.HalfId.Value, cancellationToken)
            : request.SeasonId.HasValue
                ? await _roundRepository.GetBySeasonAsync(request.SeasonId.Value, cancellationToken)
                : await _roundRepository.GetAllAsync(cancellationToken);

        // Project everyone, sort, then page so sort applies across the
        // entire result set rather than the current page slice.
        var dtos = rounds
            .Select(r => RoundDtoMapper.Map(r, r.Course?.Name ?? string.Empty, r.Participants.Count))
            .ToList();

        var sorted = string.Equals(request.Sort?.Column, ScheduleSortColumn, StringComparison.OrdinalIgnoreCase)
            ? (IReadOnlyList<RoundDto>)ScheduleOrder(dtos, DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TeeTimeSchedule.EasternTimeZone)))
            : SortMap.Apply(dtos, request.Sort);
        var totalCount = sorted.Count;
        var paged = sorted
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return Result<PagedResult<RoundDto>>.Ok(new PagedResult<RoundDto>(paged, request.Page, request.PageSize, totalCount));
    }
}
