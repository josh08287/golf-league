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

    public async Task<Course?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await _context.Courses
            .Include(c => c.Holes)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Course>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Courses
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CourseHole>> GetHolesAsync(int courseId, CancellationToken cancellationToken = default)
        => await _context.CourseHoles
            .Where(h => h.CourseId == courseId)
            .OrderBy(h => h.HoleNumber)
            .ToListAsync(cancellationToken);
}
