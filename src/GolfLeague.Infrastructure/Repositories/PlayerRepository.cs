using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GolfLeague.Infrastructure.Repositories;

public sealed class PlayerRepository : IPlayerRepository
{
    private readonly AppDbContext _context;

    public PlayerRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Player?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => _context.Players
            .IgnoreQueryFilters()
            .Include(p => p.FlightMemberships).ThenInclude(fm => fm.Flight)
            .Include(p => p.FlightMemberships).ThenInclude(fm => fm.Season)
            .Include(p => p.FlightMemberships).ThenInclude(fm => fm.Half)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Player>> GetAllActiveAsync(CancellationToken cancellationToken = default)
        => await _context.Players
            .Include(p => p.FlightMemberships).ThenInclude(fm => fm.Flight)
            .Include(p => p.FlightMemberships).ThenInclude(fm => fm.Season)
            .Include(p => p.FlightMemberships).ThenInclude(fm => fm.Half)
            .Where(p => p.IsActive)
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync(cancellationToken);

    // A user may hold one Player row per league, so the lookup must be
    // scoped by league to return a unique result. IgnoreQueryFilters is used
    // because this can run outside the current league context (e.g. invite
    // acceptance for a different league than the caller's session).
    public Task<Player?> GetByAppUserIdAsync(Guid appUserId, int leagueId, CancellationToken cancellationToken = default)
        => _context.Players.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.AppUserId == appUserId && p.LeagueId == leagueId, cancellationToken);

    public async Task<IReadOnlyList<Player>> GetAllByAppUserIdAsync(Guid appUserId, CancellationToken cancellationToken = default)
        => await _context.Players.IgnoreQueryFilters()
            .Where(p => p.AppUserId == appUserId)
            .ToListAsync(cancellationToken);

    public Task<Player?> GetByEmailAsync(string email, int leagueId, CancellationToken cancellationToken = default)
        => _context.Players.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Email == email && p.LeagueId == leagueId, cancellationToken);

    public async Task<IReadOnlyList<Player>> GetUnlinkedActiveAsync(CancellationToken cancellationToken = default)
        => await _context.Players
            .Where(p => p.IsActive && p.AppUserId == null)
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Player player, CancellationToken cancellationToken = default)
    {
        await _context.Players.AddAsync(player, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Player player, CancellationToken cancellationToken = default)
    {
        _context.Entry(player).State = EntityState.Modified;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int playerId, CancellationToken cancellationToken = default)
    {
        var player = await _context.Players
            .IgnoreQueryFilters()
            .AsTracking()
            .Include(p => p.RoundParticipants).ThenInclude(rp => rp.HoleScores)
            .Include(p => p.FlightMemberships)
            .Include(p => p.Handicaps)
            .FirstOrDefaultAsync(p => p.Id == playerId, cancellationToken);
        if (player is null) return;

        // PreLinkedPlayerId uses NoAction (SQL Server disallows two SetNull paths
        // to the same table). Null it out on any pending invites before deletion.
        var pendingPreLinks = await _context.PlayerInvites
            .IgnoreQueryFilters()
            .AsTracking()
            .Where(i => i.PreLinkedPlayerId == playerId)
            .ToListAsync(cancellationToken);
        foreach (var invite in pendingPreLinks)
            invite.PreLinkedPlayerId = null;

        // FK cascades from Player are Restrict — wipe child rows explicitly.
        _context.RoundParticipants.RemoveRange(player.RoundParticipants);
        _context.FlightMemberships.RemoveRange(player.FlightMemberships);
        _context.Handicaps.RemoveRange(player.Handicaps);
        _context.Players.Remove(player);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignToFlightAsync(int playerId, int? flightId, CancellationToken cancellationToken = default)
    {
        if (flightId is not null)
        {
            var flight = await _context.Flights
                .FirstOrDefaultAsync(f => f.Id == flightId.Value, cancellationToken);
            if (flight is null) return;

            var existingInHalf = await _context.FlightMemberships
                .AsTracking()
                .FirstOrDefaultAsync(
                    fm => fm.PlayerId == playerId && fm.HalfId == flight.HalfId,
                    cancellationToken);

            if (existingInHalf is not null)
                _context.FlightMemberships.Remove(existingInHalf);

            await _context.FlightMemberships.AddAsync(new FlightMembership
            {
                PlayerId = playerId,
                FlightId = flight.Id,
                SeasonId = flight.SeasonId,
                HalfId = flight.HalfId,
                JoinedAt = DateTime.UtcNow,
            }, cancellationToken);
        }
        else
        {
            // Unassign: drop the player's memberships in the active season's halves.
            var activeSeason = await _context.Seasons
                .FirstOrDefaultAsync(s => s.IsActive, cancellationToken);
            if (activeSeason is null) return;

            var existing = await _context.FlightMemberships
                .AsTracking()
                .Where(fm => fm.PlayerId == playerId && fm.SeasonId == activeSeason.Id)
                .ToListAsync(cancellationToken);

            _context.FlightMemberships.RemoveRange(existing);
        }

        await _context.SaveChangesAsync(cancellationToken);
        _context.ChangeTracker.Clear();
    }

    public async Task SetHalfMembershipAsync(int playerId, int halfId, int? flightId, CancellationToken cancellationToken = default)
    {
        // Drop any existing membership for this player in this half. We only
        // touch the one half, leaving the player's other half memberships alone.
        var existing = await _context.FlightMemberships
            .AsTracking()
            .Where(fm => fm.PlayerId == playerId && fm.HalfId == halfId)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
            _context.FlightMemberships.RemoveRange(existing);

        if (flightId is not null)
        {
            var flight = await _context.Flights
                .FirstOrDefaultAsync(f => f.Id == flightId.Value && f.HalfId == halfId, cancellationToken);
            if (flight is null) return;

            await _context.FlightMemberships.AddAsync(new FlightMembership
            {
                PlayerId = playerId,
                FlightId = flight.Id,
                SeasonId = flight.SeasonId,
                HalfId = flight.HalfId,
                JoinedAt = DateTime.UtcNow,
            }, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
