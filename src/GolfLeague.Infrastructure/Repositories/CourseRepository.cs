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
            // PlayerHalfTeeBox → Course/TeeBox are NoAction (multiple cascade
            // paths), so clear those defaults before the cascade delete.
            await _context.PlayerHalfTeeBoxes
                .Where(t => t.CourseId == courseId)
                .ExecuteDeleteAsync(cancellationToken);
            _context.Courses.Remove(course);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public Task AddTeeBoxAsync(TeeBox teeBox, CancellationToken cancellationToken = default)
        => SaveTeeBoxAsync(teeBox, isNew: true, cancellationToken);

    public Task UpdateTeeBoxAsync(TeeBox teeBox, CancellationToken cancellationToken = default)
        => SaveTeeBoxAsync(teeBox, isNew: false, cancellationToken);

    private async Task SaveTeeBoxAsync(TeeBox teeBox, bool isNew, CancellationToken cancellationToken)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

            if (teeBox.IsDefault)
            {
                // Clear the previous default in its own statement first — the
                // filtered unique index allows only one default per course.
                await _context.TeeBoxes
                    .Where(t => t.CourseId == teeBox.CourseId && t.IsDefault && t.Id != teeBox.Id)
                    .ExecuteUpdateAsync(u => u.SetProperty(t => t.IsDefault, false), cancellationToken);

                // Course-level rating/slope mirror the default tee so screens
                // that still read them stay accurate.
                await _context.Courses
                    .Where(c => c.Id == teeBox.CourseId)
                    .ExecuteUpdateAsync(u => u
                        .SetProperty(c => c.CourseRating, teeBox.CourseRating)
                        .SetProperty(c => c.SlopeRating, teeBox.SlopeRating), cancellationToken);
            }

            if (isNew)
            {
                await _context.TeeBoxes.AddAsync(teeBox, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }
            else
            {
                await _context.TeeBoxes
                    .Where(t => t.Id == teeBox.Id)
                    .ExecuteUpdateAsync(u => u
                        .SetProperty(t => t.Name, teeBox.Name)
                        .SetProperty(t => t.CourseRating, teeBox.CourseRating)
                        .SetProperty(t => t.SlopeRating, teeBox.SlopeRating)
                        .SetProperty(t => t.TotalYardage, teeBox.TotalYardage)
                        .SetProperty(t => t.Par, teeBox.Par)
                        .SetProperty(t => t.IsDefault, teeBox.IsDefault), cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
        });
    }

    public Task<bool> IsTeeBoxInUseAsync(int teeBoxId, CancellationToken cancellationToken = default)
        => _context.RoundParticipants.AnyAsync(rp => rp.TeeBoxId == teeBoxId, cancellationToken);

    public async Task DeleteTeeBoxAsync(int teeBoxId, CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
            await _context.PlayerHalfTeeBoxes
                .Where(t => t.TeeBoxId == teeBoxId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.HoleTeeBoxes
                .Where(h => h.TeeBoxId == teeBoxId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.TeeBoxes
                .Where(t => t.Id == teeBoxId)
                .ExecuteDeleteAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        });
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
