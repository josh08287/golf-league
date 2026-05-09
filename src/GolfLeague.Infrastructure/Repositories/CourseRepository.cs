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
        var existing = await _context.CourseHoles
            .Where(h => h.CourseId == courseId)
            .ToListAsync(cancellationToken);

        _context.CourseHoles.RemoveRange(existing);
        await _context.CourseHoles.AddRangeAsync(holes, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int courseId, CancellationToken cancellationToken = default)
    {
        var course = await _context.Courses.FindAsync([courseId], cancellationToken);
        if (course is null) return;
        _context.Courses.Remove(course);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
