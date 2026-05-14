using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using MediatR;

namespace GolfLeague.Application.Players.Queries;

/// <summary>
/// Lists all rounds a player has ever participated in, newest first, as a
/// compact summary suitable for the public player profile page.
/// </summary>
public sealed record GetPlayerRoundsQuery(int PlayerId, SortRequest? Sort = null)
    : IRequest<Result<List<PlayerRoundSummaryDto>>>;

public sealed class GetPlayerRoundsQueryHandler
    : IRequestHandler<GetPlayerRoundsQuery, Result<List<PlayerRoundSummaryDto>>>
{
    private readonly IRoundRepository _roundRepository;

    /// <summary>
    /// Default sort: most recent round date first.
    /// </summary>
    private static readonly SortMap<PlayerRoundSummaryDto> SortMap = new SortMap<PlayerRoundSummaryDto>(
            source => source.OrderByDescending(r => r.RoundDate).ThenByDescending(r => r.RoundId))
        .Add("date", r => r.RoundDate)
        .Add("roundDate", r => r.RoundDate)
        .Add("course", r => r.CourseName)
        .Add("courseName", r => r.CourseName)
        .Add("week", r => r.WeekNumber)
        .Add("weekNumber", r => r.WeekNumber)
        .Add("status", r => r.Status.ToString())
        .Add("gross", r => r.TotalGrossStrokes)
        .Add("totalGrossStrokes", r => r.TotalGrossStrokes)
        .Add("net", r => r.TotalNetStrokes)
        .Add("totalNetStrokes", r => r.TotalNetStrokes)
        .Add("grossPts", r => r.TotalGrossStablefordPoints)
        .Add("totalGrossStablefordPoints", r => r.TotalGrossStablefordPoints)
        .Add("netPts", r => r.TotalNetStablefordPoints)
        .Add("totalNetStablefordPoints", r => r.TotalNetStablefordPoints);

    public GetPlayerRoundsQueryHandler(IRoundRepository roundRepository)
    {
        _roundRepository = roundRepository;
    }

    public async Task<Result<List<PlayerRoundSummaryDto>>> Handle(
        GetPlayerRoundsQuery request,
        CancellationToken cancellationToken)
    {
        var participants = await _roundRepository.GetParticipantsAsyncByPlayer(
            request.PlayerId, cancellationToken);

        var dtos = participants
            .Select(rp =>
            {
                double? scoreDifferential = null;
                double? nineHoleScoreDifferential = null;
                if (!rp.SkippedWeek && rp.TotalGrossStrokes.HasValue && rp.Round.Course is not null)
                {
                    nineHoleScoreDifferential = Math.Round(
                        StablefordScoringService.NineHoleScoreDifferential(
                            rp.TotalGrossStrokes.Value,
                            rp.Round.Course.CourseRating,
                            rp.Round.Course.SlopeRating),
                        1, MidpointRounding.ToEven);
                    // 18-hole equivalent = 2× the 9-hole differential (USGA method)
                    scoreDifferential = Math.Round(nineHoleScoreDifferential.Value * 2, 1, MidpointRounding.ToEven);
                }

                return new PlayerRoundSummaryDto(
                    rp.Round.Id,
                    rp.Round.RoundDate,
                    rp.Round.WeekNumber,
                    rp.Round.Course?.Name ?? string.Empty,
                    rp.Round.NineHoleSide,
                    rp.Round.Status,
                    rp.TotalGrossStrokes,
                    rp.TotalNetStrokes,
                    rp.TotalGrossStablefordPoints,
                    rp.TotalNetStablefordPoints,
                    rp.IsWithdrawn,
                    rp.SkippedWeek,
                    scoreDifferential,
                    nineHoleScoreDifferential);
            })
            .ToList();

        var sorted = SortMap.Apply(dtos, request.Sort);
        return Result<List<PlayerRoundSummaryDto>>.Ok(sorted.ToList());
    }
}
