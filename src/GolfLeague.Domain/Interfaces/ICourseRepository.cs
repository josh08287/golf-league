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
    Task AddTeeBoxAsync(TeeBox teeBox, CancellationToken cancellationToken = default);
    Task UpdateHoleTeeBoxesAsync(int teeBoxId, IEnumerable<HoleTeeBox> holeTeeBoxes, CancellationToken cancellationToken = default);
}
