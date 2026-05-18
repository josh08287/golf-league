using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GolfLeague.Infrastructure.Repositories;

public sealed class CourseRepository : ICourseRepository
{
    private readonly AppDbContext _context;

    public CourseRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Course?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => _context.Courses
            .Include(c => c.Holes)
            .Include(c => c.TeeBoxes)
                .ThenInclude(t => t.HoleTeeBoxes)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Course>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Courses
            .Include(c => c.Holes)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CourseHole>> GetHolesAsync(int courseId, CancellationToken cancellationToken = default)
        => await _context.CourseHoles
            .Where(h => h.CourseId == courseId)
            .OrderBy(h => h.HoleNumber)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Course course, CancellationToken cancellationToken = default)
    {
        await _context.Courses.AddAsync(course, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateHolesAsync(int courseId, IEnumerable<CourseHole> holes, CancellationToken cancellationToken = default)
    {
        // Replace-by-delete-and-insert. Wrapped in a transaction via the
        // execution strategy so a transient failure between DELETE and INSERT
        // doesn't leave the course with no holes.
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

            await _context.CourseHoles
                .Where(h => h.CourseId == courseId)
                .ExecuteDeleteAsync(cancellationToken);

            await _context.CourseHoles.AddRangeAsync(holes, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            await tx.CommitAsync(cancellationToken);
        });
    }

    public async Task DeleteAsync(int courseId, CancellationToken cancellationToken = default)
    {
        var course = await _context.Courses
            .Include(c => c.Holes)
            .Include(c => c.TeeBoxes)
                .ThenInclude(t => t.HoleTeeBoxes)
            .FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken);

        if (course is not null)
        {
            _context.Courses.Remove(course);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task AddTeeBoxAsync(TeeBox teeBox, CancellationToken cancellationToken = default)
    {
        await _context.TeeBoxes.AddAsync(teeBox, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateHoleTeeBoxesAsync(int teeBoxId, IEnumerable<HoleTeeBox> holeTeeBoxes, CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

            await _context.HoleTeeBoxes
                .Where(h => h.TeeBoxId == teeBoxId)
                .ExecuteDeleteAsync(cancellationToken);

            await _context.HoleTeeBoxes.AddRangeAsync(holeTeeBoxes, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            await tx.CommitAsync(cancellationToken);
        });
    }
}
