using GolfLeague.Domain.Entities;

namespace GolfLeague.Domain.Interfaces;

public interface ICourseHoleGeoRepository
{
    Task<IReadOnlyList<CourseHoleGeo>> GetByCourseAsync(int courseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The course's saved centre point. Read straight from the database
    /// rather than through the cached ICourseRepository, which isn't
    /// invalidated when the centre changes.
    /// </summary>
    Task<(double? Latitude, double? Longitude)> GetCourseCentreAsync(int courseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces every hole's coordinates for the course and stores the course
    /// centre point, in one transaction.
    /// </summary>
    Task ReplaceForCourseAsync(int courseId, double? latitude, double? longitude, IEnumerable<CourseHoleGeo> holes, CancellationToken cancellationToken = default);
}
