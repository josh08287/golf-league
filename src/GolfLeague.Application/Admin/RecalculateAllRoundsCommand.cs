using GolfLeague.Application.Common;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Application.Admin;

public sealed record RecalculateAllRoundsCommand(string UserId) : IRequest<Result<RecalculateAllRoundsResult>>;

public sealed record RecalculateAllRoundsResult(
    int RoundsProcessed,
    int ParticipantsProcessed,
    int HoleScoresUpdated);

public sealed class RecalculateAllRoundsCommandHandler
    : IRequestHandler<RecalculateAllRoundsCommand, Result<RecalculateAllRoundsResult>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IHandicapRepository _handicapRepository;
    private readonly ILogger<RecalculateAllRoundsCommandHandler> _logger;

    public RecalculateAllRoundsCommandHandler(
        IRoundRepository roundRepository,
        ICourseRepository courseRepository,
        IHandicapRepository handicapRepository,
        ILogger<RecalculateAllRoundsCommandHandler> logger)
    {
        _roundRepository = roundRepository;
        _courseRepository = courseRepository;
        _handicapRepository = handicapRepository;
        _logger = logger;
    }

    public async Task<Result<RecalculateAllRoundsResult>> Handle(
        RecalculateAllRoundsCommand request,
        CancellationToken cancellationToken)
    {
        var allRounds = await _roundRepository.GetAllAsync(cancellationToken);
        var finalizedRounds = allRounds
            .Where(r => r.Status == RoundStatus.Finalized)
            .OrderBy(r => r.RoundDate)
            .ToList();

        var roundsProcessed = 0;
        var participantsProcessed = 0;
        var holeScoresUpdated = 0;

        foreach (var round in finalizedRounds)
        {
            var course = await _courseRepository.GetByIdAsync(round.CourseId, cancellationToken);
            if (course is null)
            {
                _logger.LogWarning("Round {RoundId} has invalid course {CourseId}, skipping", round.Id, round.CourseId);
                continue;
            }

            var courseHoles = await _courseRepository.GetHolesAsync(round.CourseId, cancellationToken);
            var relevantHoles = round.NineHoleSide == NineHoleSide.Back
                ? courseHoles.Where(h => h.HoleNumber >= 10).ToList()
                : courseHoles.Where(h => h.HoleNumber <= 9).ToList();

            var participants = await _roundRepository.GetParticipantsAsync(round.Id, cancellationToken);
            var activeParticipants = participants
                .Where(p => !p.IsWithdrawn && !p.SkippedWeek)
                .ToList();

            foreach (var participant in activeParticipants)
            {
                // Recalculate course handicap based on stored HandicapIndex and course slope
                var recalculatedCourseHandicap = StablefordScoringService.CourseHandicap(
                    participant.HandicapIndex,
                    course.SlopeRating,
                    RoundType.NineHole);

                // Update the course handicap if it changed
                if (participant.CourseHandicap != recalculatedCourseHandicap)
                {
                    _logger.LogInformation(
                        "Updating CourseHandicap for participant {ParticipantId} in round {RoundId}: {OldValue} -> {NewValue}",
                        participant.Id, round.Id, participant.CourseHandicap, recalculatedCourseHandicap);
                    participant.CourseHandicap = recalculatedCourseHandicap;
                }

                // Get existing hole scores
                var holeScores = (await _roundRepository.GetHoleScoresAsync(participant.Id, cancellationToken)).ToList();

                if (holeScores.Count == 0)
                {
                    // No scores to recalculate
                    continue;
                }

                // Recalculate each hole score with the (possibly updated) course handicap
                foreach (var holeScore in holeScores)
                {
                    var courseHole = relevantHoles.FirstOrDefault(h => h.HoleNumber == holeScore.HoleNumber);
                    if (courseHole is null)
                    {
                        _logger.LogWarning(
                            "Hole {HoleNumber} not found for round {RoundId}, skipping recalculation",
                            holeScore.HoleNumber, round.Id);
                        continue;
                    }

                    // Recalculate handicap strokes and net strokes
                    var strokesOnHole = StablefordScoringService.StrokesOnHole(participant.CourseHandicap, courseHole.StrokeIndex);
                    var maxGross = StablefordScoringService.MaxGross(courseHole.Par, strokesOnHole);
                    var actualGross = Math.Min(holeScore.GrossStrokes, maxGross);
                    var isMaxScore = holeScore.GrossStrokes >= maxGross;
                    var netStrokes = StablefordScoringService.NetStrokes(actualGross, strokesOnHole);
                    var netPoints = StablefordScoringService.StablefordPoints(courseHole.Par, netStrokes);
                    var grossPoints = StablefordScoringService.StablefordPoints(courseHole.Par, actualGross);

                    // Update hole score
                    holeScore.HandicapStrokes = strokesOnHole;
                    holeScore.NetStrokes = netStrokes;
                    holeScore.GrossStablefordPoints = grossPoints;
                    holeScore.NetStablefordPoints = netPoints;
                    holeScore.IsMaxScore = isMaxScore;
                    holeScore.Par = courseHole.Par;
                    holeScore.StrokeIndex = courseHole.StrokeIndex;

                    holeScoresUpdated++;
                }

                // Recalculate participant totals
                participant.TotalGrossStrokes = holeScores.Sum(h => h.GrossStrokes);
                participant.TotalNetStrokes = holeScores.Sum(h => h.NetStrokes);
                participant.TotalGrossStablefordPoints = holeScores.Sum(h => h.GrossStablefordPoints);
                participant.TotalNetStablefordPoints = holeScores.Sum(h => h.NetStablefordPoints);

                await _roundRepository.UpdateParticipantAsync(participant, cancellationToken);
                participantsProcessed++;
            }

            roundsProcessed++;
            _logger.LogInformation(
                "Recalculated round {RoundId}: {ParticipantCount} participants processed",
                round.Id, activeParticipants.Count);
        }

        return Result<RecalculateAllRoundsResult>.Ok(
            new RecalculateAllRoundsResult(roundsProcessed, participantsProcessed, holeScoresUpdated));
    }
}
