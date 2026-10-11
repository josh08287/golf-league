using GolfLeague.Application.Flights.Services;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;

namespace GolfLeague.Application.Rounds;

/// <summary>
/// Moves an existing participant onto a tee box: re-stamps the rating/slope
/// snapshot and course handicap, then rescores any holes already entered so
/// handicap strokes, net, and Stableford points match the new tee.
/// </summary>
public sealed class ParticipantTeeUpdater
{
    private readonly IRoundRepository _rounds;
    private readonly ICourseRepository _courses;
    private readonly MatchPlayResultCalculator _matchPlay;

    public ParticipantTeeUpdater(IRoundRepository rounds, ICourseRepository courses, MatchPlayResultCalculator matchPlay)
    {
        _rounds = rounds;
        _courses = courses;
        _matchPlay = matchPlay;
    }

    /// <summary>
    /// Applies <paramref name="tee"/> to <paramref name="participant"/> and
    /// persists it. <paramref name="round"/> must not be finalized or
    /// cancelled — callers check. <paramref name="holeScores"/> are the
    /// participant's current saved scores (may be empty).
    /// </summary>
    public async Task ApplyAsync(
        RoundParticipant participant,
        TeeBox tee,
        bool overridden,
        Round round,
        IReadOnlyList<HoleScore> holeScores,
        CancellationToken cancellationToken)
    {
        var courseHoles = await _courses.GetHolesAsync(round.CourseId, cancellationToken);

        ParticipantTeeResolver.Apply(participant, tee, courseHoles.Sum(h => h.Par), round.RoundType);
        participant.TeeBoxOverridden = overridden;

        if (holeScores.Count > 0)
        {
            var rescored = Rescore(participant, holeScores, round, courseHoles);
            await _rounds.ReplaceHoleScoresAsync(participant.Id, rescored, cancellationToken);

            // Keep whatever "complete" state the totals were already in: a
            // round with totals keeps them (recomputed); one without stays null.
            if (participant.TotalGrossStrokes.HasValue)
            {
                participant.TotalGrossStrokes = rescored.Sum(h => h.GrossStrokes);
                participant.TotalNetStrokes = rescored.Sum(h => h.NetStrokes);
                participant.TotalGrossStablefordPoints = rescored.Sum(h => h.GrossStablefordPoints);
                participant.TotalNetStablefordPoints = rescored.Sum(h => h.NetStablefordPoints);
            }
        }

        await _rounds.UpdateParticipantAsync(participant, cancellationToken);

        if (holeScores.Count > 0 && round.Half?.ScoringFormat == ScoringFormat.MatchPlay)
        {
            await _matchPlay.RecomputeForRoundPlayerAsync(
                round.Id, participant.PlayerId, round.Half.MatchPlayCustomFormula, cancellationToken);
        }
    }

    /// <summary>
    /// Recomputes each saved hole against the participant's current course
    /// handicap. Returns new (unsaved) rows carrying every entered field over,
    /// for <see cref="IRoundRepository.ReplaceHoleScoresAsync"/>.
    /// </summary>
    public static List<HoleScore> Rescore(
        RoundParticipant participant,
        IReadOnlyList<HoleScore> holeScores,
        Round round,
        IReadOnlyList<CourseHole> courseHoles)
    {
        var relevantHoles = round.NineHoleSide switch
        {
            NineHoleSide.Back => courseHoles.Where(h => h.HoleNumber >= 10).ToList(),
            NineHoleSide.Front => courseHoles.Where(h => h.HoleNumber <= 9).ToList(),
            // NotApplicable (18-hole rounds, e.g. tournaments) plays every hole.
            _ => courseHoles.ToList(),
        };
        var allStrokeIndices = relevantHoles.Select(h => h.StrokeIndex).ToList();

        var result = new List<HoleScore>(holeScores.Count);
        foreach (var score in holeScores)
        {
            var hole = relevantHoles.FirstOrDefault(h => h.HoleNumber == score.HoleNumber);
            var par = hole?.Par ?? score.Par;
            var strokeIndex = hole?.StrokeIndex ?? score.StrokeIndex;

            var strokesOnHole = StablefordScoringService.StrokesOnHole(participant.CourseHandicap, strokeIndex, allStrokeIndices);
            var maxGross = StablefordScoringService.MaxGross(par, strokesOnHole);
            var adjustedGross = Math.Min(score.GrossStrokes, maxGross);
            var netStrokes = StablefordScoringService.NetStrokes(adjustedGross, strokesOnHole);

            result.Add(new HoleScore
            {
                ParticipantId = participant.Id,
                HoleNumber = score.HoleNumber,
                Par = par,
                StrokeIndex = strokeIndex,
                GrossStrokes = score.GrossStrokes,
                HandicapStrokes = strokesOnHole,
                NetStrokes = netStrokes,
                GrossStablefordPoints = StablefordScoringService.StablefordPoints(par, score.GrossStrokes),
                NetStablefordPoints = StablefordScoringService.StablefordPoints(par, netStrokes),
                IsMaxScore = score.GrossStrokes >= maxGross,
                Putts = score.Putts,
                FirstPuttDistanceFeet = score.FirstPuttDistanceFeet,
                FairwayHit = score.FairwayHit,
                Gir = score.Gir,
                LastModifiedByPlayerId = score.LastModifiedByPlayerId,
            });
        }
        return result;
    }
}
