using GolfLeague.Application.Common;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Queries;

public sealed record RoundScorecardHoleDto(
    int HoleNumber,
    int Par,
    int Strokes,
    int NetStrokes,
    int StrokeIndex,
    int GrossPoints,
    int NetPoints);

public sealed record RoundScorecardDto(
    int RoundId,
    int PlayerId,
    string PlayerName,
    int FlightId,
    string CourseName,
    DateOnly ScheduledDate,
    double HandicapAtTime,
    int CourseHandicap,
    int? GrossScore,
    int? NetScore,
    int? GrossPoints,
    int? NetPoints,
    List<RoundScorecardHoleDto> Holes);

public sealed record GetRoundScorecardsQuery(int RoundId, SortRequest? Sort = null)
    : IRequest<Result<PagedResult<RoundScorecardDto>>>;

public sealed class GetRoundScorecardsQueryHandler : IRequestHandler<GetRoundScorecardsQuery, Result<PagedResult<RoundScorecardDto>>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ICourseRepository _courseRepository;

    /// <summary>
    /// Default sort: flight then player name (the natural scorecard listing).
    /// </summary>
    private static readonly SortMap<RoundScorecardDto> SortMap = new SortMap<RoundScorecardDto>(
            source => source.OrderBy(s => s.FlightId).ThenBy(s => s.PlayerName, StringComparer.OrdinalIgnoreCase))
        .Add("player", s => s.PlayerName)
        .Add("playerName", s => s.PlayerName)
        .Add("flight", s => s.FlightId)
        .Add("flightId", s => s.FlightId)
        .Add("hcp", s => s.HandicapAtTime)
        .Add("handicapAtTime", s => s.HandicapAtTime)
        .Add("gross", s => s.GrossScore)
        .Add("grossScore", s => s.GrossScore)
        .Add("net", s => s.NetScore)
        .Add("netScore", s => s.NetScore)
        .Add("grossPts", s => s.GrossPoints)
        .Add("grossPoints", s => s.GrossPoints)
        .Add("netPts", s => s.NetPoints)
        .Add("netPoints", s => s.NetPoints);

    public GetRoundScorecardsQueryHandler(IRoundRepository roundRepository, ICourseRepository courseRepository)
    {
        _roundRepository = roundRepository;
        _courseRepository = courseRepository;
    }

    public async Task<Result<PagedResult<RoundScorecardDto>>> Handle(GetRoundScorecardsQuery request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<PagedResult<RoundScorecardDto>>.Fail($"Round with ID {request.RoundId} not found.");

        var participants = await _roundRepository.GetParticipantsAsync(request.RoundId, cancellationToken);
        var course = await _courseRepository.GetByIdAsync(round.CourseId, cancellationToken);
        var courseName = course?.Name ?? string.Empty;

        var dtos = participants.Select(p =>
        {
            var holes = p.HoleScores
                .OrderBy(h => h.HoleNumber)
                .Select(h => new RoundScorecardHoleDto(
                    h.HoleNumber,
                    h.Par,
                    h.GrossStrokes,
                    h.NetStrokes,
                    h.StrokeIndex,
                    h.GrossStablefordPoints,
                    h.NetStablefordPoints))
                .ToList();

            return new RoundScorecardDto(
                p.RoundId,
                p.PlayerId,
                p.Player.FullName,
                p.FlightId,
                courseName,
                round.RoundDate,
                p.HandicapIndex,
                p.CourseHandicap,
                p.TotalGrossStrokes,
                p.TotalNetStrokes,
                p.TotalGrossStablefordPoints,
                p.TotalNetStablefordPoints,
                holes);
        }).ToList();

        var sorted = SortMap.Apply(dtos, request.Sort);
        return Result<PagedResult<RoundScorecardDto>>.Ok(new PagedResult<RoundScorecardDto>(sorted.ToList(), 1, sorted.Count, sorted.Count));
    }
}
