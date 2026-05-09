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
    int? GrossScore,
    int? NetScore,
    int? GrossPoints,
    int? NetPoints,
    List<RoundScorecardHoleDto> Holes);

public sealed record GetRoundScorecardsQuery(int RoundId) : IRequest<Result<PagedResult<RoundScorecardDto>>>;

public sealed class GetRoundScorecardsQueryHandler : IRequestHandler<GetRoundScorecardsQuery, Result<PagedResult<RoundScorecardDto>>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ICourseRepository _courseRepository;

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
                p.TotalGrossStrokes,
                p.TotalNetStrokes,
                p.TotalGrossStablefordPoints,
                p.TotalNetStablefordPoints,
                holes);
        }).ToList();

        return Result<PagedResult<RoundScorecardDto>>.Ok(new PagedResult<RoundScorecardDto>(dtos, 1, dtos.Count, dtos.Count));
    }
}
