using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GolfLeague.Infrastructure.Repositories;

public sealed class TeeTimeSideGameRepository : ITeeTimeSideGameRepository
{
    private readonly AppDbContext _context;

    public TeeTimeSideGameRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TeeTimeSideGame>> GetForTeeTimeAsync(int teeTimeId, CancellationToken cancellationToken = default)
        => await _context.TeeTimeSideGames
            .Include(g => g.Teams)
            .Where(g => g.TeeTimeId == teeTimeId)
            .ToListAsync(cancellationToken);

    public Task<TeeTimeSideGame?> GetByIdAsync(int sideGameId, CancellationToken cancellationToken = default)
        => _context.TeeTimeSideGames
            .Include(g => g.Teams)
            .FirstOrDefaultAsync(g => g.Id == sideGameId, cancellationToken);

    public async Task<TeeTimeSideGame> AddAsync(TeeTimeSideGame sideGame, CancellationToken cancellationToken = default)
    {
        _context.TeeTimeSideGames.Add(sideGame);
        await _context.SaveChangesAsync(cancellationToken);
        return sideGame;
    }

    public async Task RemoveAsync(int sideGameId, CancellationToken cancellationToken = default)
    {
        await _context.TeeTimeSideGames
            .Where(g => g.Id == sideGameId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
