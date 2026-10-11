using GolfLeague.Domain.Entities;

namespace GolfLeague.Domain.Interfaces;

public interface ICourseRepository
{
    Task<Course?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Course>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CourseHole>> GetHolesAsync(int courseId, CancellationToken cancellationToken = default);
    Task AddAsync(Course course, CancellationToken cancellationToken = default);
    Task UpdateHolesAsync(int courseId, IEnumerable<CourseHole> holes, CancellationToken cancellationToken = default);
    Task DeleteAsync(int courseId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Adds a tee box. When <see cref="TeeBox.IsDefault"/> is set, the course's
    /// previous default is cleared first and the course-level rating/slope are
    /// mirrored from the new default.
    /// </summary>
    Task AddTeeBoxAsync(TeeBox teeBox, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a tee box's editable fields. Same default handling as
    /// <see cref="AddTeeBoxAsync"/>.
    /// </summary>
    Task UpdateTeeBoxAsync(TeeBox teeBox, CancellationToken cancellationToken = default);

    /// <summary>True when any round participant was (or will be) played from this tee.</summary>
    Task<bool> IsTeeBoxInUseAsync(int teeBoxId, CancellationToken cancellationToken = default);

    /// <summary>Deletes a tee box and any per-half player defaults pointing at it.</summary>
    Task DeleteTeeBoxAsync(int teeBoxId, CancellationToken cancellationToken = default);
    Task UpdateHoleTeeBoxesAsync(int teeBoxId, IEnumerable<HoleTeeBox> holeTeeBoxes, CancellationToken cancellationToken = default);
}
