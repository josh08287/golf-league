using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GolfLeague.Infrastructure.Repositories;

public sealed class PlayerHalfTeeBoxRepository : IPlayerHalfTeeBoxRepository
{
    private readonly AppDbContext _context;

    public PlayerHalfTeeBoxRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PlayerHalfTeeBox>> GetForPlayerAsync(int playerId, CancellationToken cancellationToken = default)
        => await _context.PlayerHalfTeeBoxes
            .Where(t => t.PlayerId == playerId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<int, int>> GetTeeBoxIdsAsync(
        int halfId, int courseId, IEnumerable<int> playerIds, CancellationToken cancellationToken = default)
    {
        var ids = playerIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, int>();

        return await _context.PlayerHalfTeeBoxes
            .Where(t => t.HalfId == halfId && t.CourseId == courseId && ids.Contains(t.PlayerId))
            .ToDictionaryAsync(t => t.PlayerId, t => t.TeeBoxId, cancellationToken);
    }

    public async Task SetAsync(int playerId, int halfId, int courseId, int? teeBoxId, CancellationToken cancellationToken = default)
    {
        // The context defaults to NoTracking; track explicitly so the mutation is saved.
        var existing = await _context.PlayerHalfTeeBoxes
            .AsTracking()
            .FirstOrDefaultAsync(t => t.PlayerId == playerId && t.HalfId == halfId && t.CourseId == courseId, cancellationToken);

        if (teeBoxId is null)
        {
            if (existing is not null) _context.PlayerHalfTeeBoxes.Remove(existing);
        }
        else if (existing is null)
        {
            _context.PlayerHalfTeeBoxes.Add(new PlayerHalfTeeBox
            {
                PlayerId = playerId,
                HalfId = halfId,
                CourseId = courseId,
                TeeBoxId = teeBoxId.Value,
            });
        }
        else
        {
            existing.TeeBoxId = teeBoxId.Value;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
