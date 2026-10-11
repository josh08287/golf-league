using GolfLeague.Application.Rounds;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using Moq;

namespace GolfLeague.Tests.Helpers;

/// <summary>
/// Builds a <see cref="ParticipantTeeResolver"/> over mocks for handler tests
/// that create round participants. Tee selection is off unless a flag mock
/// says otherwise, so every player gets the course's default tee.
/// </summary>
internal static class TeeResolverFactory
{
    public static ParticipantTeeResolver Create(
        Course? course = null,
        Mock<IFeatureFlagRepository>? featureFlags = null,
        Mock<IPlayerHalfTeeBoxRepository>? halfTeeBoxes = null,
        Mock<ISeasonRepository>? seasons = null)
    {
        var courses = new Mock<ICourseRepository>();
        courses.Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => WithDefaultTee(
                course is not null && course.Id == id ? course : new Course { Id = id, CourseRating = 72.0, SlopeRating = 113 }));

        return new ParticipantTeeResolver(
            (halfTeeBoxes ?? new Mock<IPlayerHalfTeeBoxRepository>()).Object,
            courses.Object,
            (featureFlags ?? new Mock<IFeatureFlagRepository>()).Object,
            (seasons ?? new Mock<ISeasonRepository>()).Object);
    }

    /// <summary>Gives a course fixture a default tee mirroring its course-level rating/slope, if it has none.</summary>
    public static Course WithDefaultTee(Course course)
    {
        if (course.TeeBoxes.Count == 0)
        {
            course.TeeBoxes.Add(new TeeBox
            {
                Id = 1000 + course.Id,
                CourseId = course.Id,
                Name = "Default",
                CourseRating = course.CourseRating,
                SlopeRating = course.SlopeRating,
                IsDefault = true,
            });
        }
        return course;
    }
}
