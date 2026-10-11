using GolfLeague.Application.Admin;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;

namespace GolfLeague.Application.Rounds;

/// <summary>
/// Decides which tee box a participant starts a round on and stamps that
/// tee's rating/slope (and the resulting course handicap) onto the
/// participant. Every participant-creation path goes through here so a
/// round's rating snapshot is never taken from the live course values.
/// </summary>
public sealed class ParticipantTeeResolver
{
    private readonly IPlayerHalfTeeBoxRepository _halfTeeBoxes;
    private readonly ICourseRepository _courses;
    private readonly IFeatureFlagRepository _featureFlags;
    private readonly ISeasonRepository _seasons;

    public ParticipantTeeResolver(
        IPlayerHalfTeeBoxRepository halfTeeBoxes,
        ICourseRepository courses,
        IFeatureFlagRepository featureFlags,
        ISeasonRepository seasons)
    {
        _halfTeeBoxes = halfTeeBoxes;
        _courses = courses;
        _featureFlags = featureFlags;
        _seasons = seasons;
    }

    public async Task<bool> IsSelectionEnabledAsync(CancellationToken cancellationToken)
    {
        var flag = await _featureFlags.GetAsync(KnownFeatureFlags.TeeBoxSelectionEnabled, cancellationToken);
        return flag?.Enabled ?? KnownFeatureFlags.Defaults[KnownFeatureFlags.TeeBoxSelectionEnabled];
    }

    /// <summary>
    /// The tee each of <paramref name="playerIds"/> starts <paramref name="round"/>
    /// on: their per-half default for the round's course when tee selection is
    /// enabled, otherwise the course's default tee.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, TeeBox>> ResolveDefaultsAsync(
        Round round, IEnumerable<int> playerIds, CancellationToken cancellationToken)
    {
        var course = await _courses.GetByIdAsync(round.CourseId, cancellationToken)
            ?? throw new InvalidOperationException($"Course {round.CourseId} not found.");
        var courseDefault = DefaultTee(course);

        var ids = playerIds.Distinct().ToList();
        var result = ids.ToDictionary(id => id, _ => courseDefault);
        if (ids.Count == 0 || !await IsSelectionEnabledAsync(cancellationToken))
            return result;

        var halfId = await ResolveHalfIdAsync(round, cancellationToken);
        if (halfId is null)
            return result;

        var assigned = await _halfTeeBoxes.GetTeeBoxIdsAsync(halfId.Value, course.Id, ids, cancellationToken);
        foreach (var (playerId, teeBoxId) in assigned)
        {
            var tee = course.TeeBoxes.FirstOrDefault(t => t.Id == teeBoxId);
            if (tee is not null) result[playerId] = tee;
        }
        return result;
    }

    public async Task<TeeBox> ResolveDefaultAsync(Round round, int playerId, CancellationToken cancellationToken)
        => (await ResolveDefaultsAsync(round, [playerId], cancellationToken))[playerId];

    /// <summary>
    /// The half a round's player defaults come from. Weekly rounds carry it
    /// directly; tournaments use the season half whose dates contain the round.
    /// </summary>
    public async Task<int?> ResolveHalfIdAsync(Round round, CancellationToken cancellationToken)
    {
        if (round.HalfId.HasValue) return round.HalfId;

        var season = await _seasons.GetByIdAsync(round.SeasonId, cancellationToken);
        return season?.Halves
            .FirstOrDefault(h => h.StartDate <= round.RoundDate && round.RoundDate <= h.EndDate)
            ?.Id;
    }

    /// <summary>The course's default tee. Every course has one (created with the course, backfilled by migration).</summary>
    public static TeeBox DefaultTee(Course course)
        => course.TeeBoxes.FirstOrDefault(t => t.IsDefault)
           ?? course.TeeBoxes.OrderBy(t => t.Id).FirstOrDefault()
           ?? throw new InvalidOperationException($"Course {course.Id} has no tee boxes.");

    /// <summary>
    /// Stamps <paramref name="tee"/> onto the participant and recomputes the
    /// course handicap from it. <paramref name="coursePar"/> is the full
    /// course's par (hole par is unchanged by the tee).
    /// </summary>
    public static void Apply(RoundParticipant participant, TeeBox tee, int coursePar, Domain.Enums.RoundType roundType)
    {
        participant.TeeBoxId = tee.Id;
        participant.CourseRating = tee.CourseRating;
        participant.SlopeRating = tee.SlopeRating;
        participant.CourseHandicap = StablefordScoringService.CourseHandicap(
            participant.HandicapIndex, tee.SlopeRating, tee.CourseRating, coursePar, roundType);
    }
}
