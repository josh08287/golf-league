using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GolfLeague.Infrastructure.Repositories;

public sealed class CourseHoleGeoRepository : ICourseHoleGeoRepository
{
    private readonly AppDbContext _context;

    public CourseHoleGeoRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CourseHoleGeo>> GetByCourseAsync(int courseId, CancellationToken cancellationToken = default)
        => await _context.CourseHoleGeos
            .Where(g => g.CourseId == courseId)
            .OrderBy(g => g.HoleNumber)
            .ToListAsync(cancellationToken);

    public async Task<(double? Latitude, double? Longitude)> GetCourseCentreAsync(int courseId, CancellationToken cancellationToken = default)
    {
        var centre = await _context.Courses
            .Where(c => c.Id == courseId)
            .Select(c => new { c.Latitude, c.Longitude })
            .FirstOrDefaultAsync(cancellationToken);
        return (centre?.Latitude, centre?.Longitude);
    }

    public async Task ReplaceForCourseAsync(int courseId, double? latitude, double? longitude, IEnumerable<CourseHoleGeo> holes, CancellationToken cancellationToken = default)
    {
        // Same delete-and-insert-in-a-transaction approach as
        // CourseRepository.UpdateHolesAsync, so a transient failure can't
        // leave the course half-mapped.
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

            await _context.Courses
                .Where(c => c.Id == courseId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Latitude, latitude)
                    .SetProperty(c => c.Longitude, longitude), cancellationToken);

            await _context.CourseHoleGeos
                .Where(g => g.CourseId == courseId)
                .ExecuteDeleteAsync(cancellationToken);

            await _context.CourseHoleGeos.AddRangeAsync(holes, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            await tx.CommitAsync(cancellationToken);
        });
    }
}
