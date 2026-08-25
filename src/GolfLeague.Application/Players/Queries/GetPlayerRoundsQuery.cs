using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Handicaps;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Interfaces;
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
    private readonly HandicapRecalculationService _handicapCalc;
    private readonly ILeagueContext _leagueContext;

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

    public GetPlayerRoundsQueryHandler(
        IRoundRepository roundRepository,
        HandicapRecalculationService handicapCalc,
        ILeagueContext leagueContext)
    {
        _roundRepository = roundRepository;
        _handicapCalc = handicapCalc;
        _leagueContext = leagueContext;
    }

    public async Task<Result<List<PlayerRoundSummaryDto>>> Handle(
        GetPlayerRoundsQuery request,
        CancellationToken cancellationToken)
    {
        var participants = await _roundRepository.GetParticipantsAsyncByPlayer(
            request.PlayerId, cancellationToken);

        var settings = await _handicapCalc.LoadSettingsAsync(_leagueContext.LeagueId ?? 0, cancellationToken);

        var dtos = participants
            .Select(rp =>
            {
                double? scoreDifferential = null;
                if (!rp.SkippedWeek && rp.TotalGrossStrokes.HasValue && rp.Round.Course is not null)
                {
                    var roundInput = new HandicapRoundInput(
                        rp.TotalGrossStrokes.Value,
                        rp.Round.Course.CourseRating,
                        rp.Round.Course.SlopeRating,
                        rp.Round.Course.Holes.Sum(h => h.Par));

                    scoreDifferential = Math.Round(
                        _handicapCalc.ComputeDifferential(roundInput, settings),
                        1, MidpointRounding.ToEven);
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
                    scoreDifferential);
            })
            .ToList();

        var sorted = SortMap.Apply(dtos, request.Sort);
        return Result<List<PlayerRoundSummaryDto>>.Ok(sorted.ToList());
    }
}
