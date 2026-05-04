using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GolfLeague.Infrastructure.Repositories;

public sealed class RegistrationRepository : IRegistrationRepository
{
    private readonly AppDbContext _context;

    public RegistrationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PlayerRegistration?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await _context.PlayerRegistrations
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<PlayerRegistration?> GetByEntraObjectIdAsync(string entraObjectId, CancellationToken cancellationToken = default)
        => await _context.PlayerRegistrations
            .Where(r => r.EntraObjectId == entraObjectId)
            .OrderByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PlayerRegistration>> GetByStatusAsync(RegistrationStatus status, CancellationToken cancellationToken = default)
        => await _context.PlayerRegistrations
            .Where(r => r.Status == status)
            .OrderBy(r => r.RequestedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(PlayerRegistration registration, CancellationToken cancellationToken = default)
    {
        await _context.PlayerRegistrations.AddAsync(registration, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PlayerRegistration registration, CancellationToken cancellationToken = default)
    {
        _context.PlayerRegistrations.Update(registration);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
