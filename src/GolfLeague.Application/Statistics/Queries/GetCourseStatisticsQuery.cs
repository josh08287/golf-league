using GolfLeague.Application.Common;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Statistics.Queries;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record HoleStatisticsDto(
    int HoleNumber,
    int Par,
    int StrokeIndex,
    double AverageGrossStrokes,
    double AverageNetStrokes,
    double AverageGrossStablefordPoints,
    double AverageNetStablefordPoints,
    double AverageScoreToPar,
    int TotalScoresRecorded,
    int EagleOrBetterCount,
    int BirdieCount,
    int ParCount,
    int BogeyCount,
    int DoubleBogeyOrWorseCount,
    int DifficultyRank);

public sealed record CourseStatisticsDto(
    int CourseId,
    string CourseName,
    double CourseRating,
    int SlopeRating,
    int TotalRoundsPlayed,
    int TotalScorecardsRecorded,
    double? AverageTotalGrossStrokes,
    double? AverageTotalNetStrokes,
    double? AverageTotalGrossStablefordPoints,
    double? AverageTotalNetStablefordPoints,
    double? AverageScoreToPar,
    List<HoleStatisticsDto> HoleStatistics);

// ── Query + Handler ──────────────────────────────────────────────────────────

public sealed record GetCourseStatisticsQuery(int CourseId) : IRequest<Result<CourseStatisticsDto>>;

public sealed class GetCourseStatisticsQueryHandler
    : IRequestHandler<GetCourseStatisticsQuery, Result<CourseStatisticsDto>>
{
    private readonly ICourseRepository _courseRepository;
    private readonly IRoundRepository _roundRepository;

    public GetCourseStatisticsQueryHandler(
        ICourseRepository courseRepository,
        IRoundRepository roundRepository)
    {
        _courseRepository = courseRepository;
        _roundRepository = roundRepository;
    }

    public async Task<Result<CourseStatisticsDto>> Handle(
        GetCourseStatisticsQuery request,
        CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetByIdAsync(request.CourseId, cancellationToken);
        if (course is null)
            return Result<CourseStatisticsDto>.Fail($"Course with ID {request.CourseId} not found.");

        var holes = await _courseRepository.GetHolesAsync(request.CourseId, cancellationToken);
        var allRounds = await _roundRepository.GetAllAsync(cancellationToken);
        var courseRounds = allRounds
            .Where(r => r.CourseId == request.CourseId && r.Status == RoundStatus.Finalized)
            .ToList();

        // Gather all participants + hole scores from finalized rounds on this course
        var allHoleScores = new List<Domain.Entities.HoleScore>();
        var participantWithRound = new List<(Domain.Entities.RoundParticipant Participant, Domain.Entities.Round Round)>();

        foreach (var round in courseRounds)
        {
            var participants = await _roundRepository.GetParticipantsAsync(round.Id, cancellationToken);
            foreach (var p in participants)
            {
                if (p.IsWithdrawn || p.SkippedWeek) continue;
                participantWithRound.Add((p, round));
                var scores = await _roundRepository.GetHoleScoresAsync(p.Id, cancellationToken);
                allHoleScores.AddRange(scores);
            }
        }

        // Build per-hole statistics
        var holeStats = holes
            .OrderBy(h => h.HoleNumber)
            .Select(h =>
            {
                var scoresForHole = allHoleScores.Where(s => s.HoleNumber == h.HoleNumber).ToList();
                var count = scoresForHole.Count;

                if (count == 0)
                {
                    return new HoleStatisticsDto(
                        h.HoleNumber, h.Par, h.StrokeIndex,
                        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
                }

                var avgGross = scoresForHole.Average(s => s.GrossStrokes);
                var avgNet = scoresForHole.Average(s => s.NetStrokes);
                var avgGrossPts = scoresForHole.Average(s => s.GrossStablefordPoints);
                var avgNetPts = scoresForHole.Average(s => s.NetStablefordPoints);
                var avgScoreToPar = scoresForHole.Average(s => s.GrossStrokes - s.Par);

                var eagleOrBetter = scoresForHole.Count(s => s.GrossStrokes <= s.Par - 2);
                var birdies = scoresForHole.Count(s => s.GrossStrokes == s.Par - 1);
                var pars = scoresForHole.Count(s => s.GrossStrokes == s.Par);
                var bogeys = scoresForHole.Count(s => s.GrossStrokes == s.Par + 1);
                var doublePlus = scoresForHole.Count(s => s.GrossStrokes >= s.Par + 2);

                return new HoleStatisticsDto(
                    h.HoleNumber, h.Par, h.StrokeIndex,
                    Math.Round(avgGross, 2),
                    Math.Round(avgNet, 2),
                    Math.Round(avgGrossPts, 2),
                    Math.Round(avgNetPts, 2),
                    Math.Round(avgScoreToPar, 2),
                    count,
                    eagleOrBetter,
                    birdies,
                    pars,
                    bogeys,
                    doublePlus,
                    0); // rank filled in below
            })
            .ToList();

        // Assign difficulty rank (hardest = 1, based on avg score to par descending)
        var ranked = holeStats
            .OrderByDescending(h => h.AverageScoreToPar)
            .Select((h, i) => h with { DifficultyRank = i + 1 })
            .OrderBy(h => h.HoleNumber)
            .ToList();

        // Aggregate course-level stats
        var validEntries = participantWithRound
            .Where(e => e.Participant.TotalGrossStrokes.HasValue)
            .ToList();

        // Pre-compute 9-hole par for each side
        var frontPar = holes.Where(h => h.HoleNumber <= 9).Sum(h => h.Par);
        var backPar = holes.Where(h => h.HoleNumber > 9).Sum(h => h.Par);
        var fullPar = holes.Sum(h => h.Par);

        int NineHolePar(Domain.Entities.Round r) => r.NineHoleSide switch
        {
            NineHoleSide.Back => backPar,
            _ => frontPar, // Front or NotApplicable defaults to front
        };

        var dto = new CourseStatisticsDto(
            course.Id,
            course.Name,
            course.CourseRating,
            course.SlopeRating,
            courseRounds.Count,
            validEntries.Count,
            validEntries.Count > 0 ? Math.Round(validEntries.Average(e => e.Participant.TotalGrossStrokes!.Value), 1) : null,
            validEntries.Count > 0 ? Math.Round(validEntries.Average(e => e.Participant.TotalNetStrokes ?? 0), 1) : null,
            validEntries.Count > 0 ? Math.Round(validEntries.Average(e => e.Participant.TotalGrossStablefordPoints ?? 0), 1) : null,
            validEntries.Count > 0 ? Math.Round(validEntries.Average(e => e.Participant.TotalNetStablefordPoints ?? 0), 1) : null,
            validEntries.Count > 0 ? Math.Round(validEntries.Average(e => e.Participant.TotalGrossStrokes!.Value - NineHolePar(e.Round)), 1) : null,
            ranked);

        return Result<CourseStatisticsDto>.Ok(dto);
    }
}
