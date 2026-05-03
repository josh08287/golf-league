using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GolfLeague.Infrastructure.Repositories;

public sealed class RoundRepository : IRoundRepository
{
    private readonly AppDbContext _context;

    public RoundRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Round?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await _context.Rounds
            .Include(r => r.Course)
            .Include(r => r.Flight)
            .Include(r => r.Season)
            .Include(r => r.Participants)
                .ThenInclude(rp => rp.Player)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Round>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Rounds
            .Include(r => r.Course)
            .Include(r => r.Flight)
            .Include(r => r.Participants)
            .OrderByDescending(r => r.RoundDate)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Round>> GetBySeasonAsync(int seasonId, CancellationToken cancellationToken = default)
        => await _context.Rounds
            .Include(r => r.Course)
            .Include(r => r.Flight)
            .Include(r => r.Participants)
            .Where(r => r.SeasonId == seasonId)
            .OrderByDescending(r => r.RoundDate)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RoundParticipant>> GetParticipantsAsync(int roundId, CancellationToken cancellationToken = default)
        => await _context.RoundParticipants
            .Include(rp => rp.Player)
            .Include(rp => rp.HoleScores)
            .Where(rp => rp.RoundId == roundId)
            .OrderBy(rp => rp.Player.LastName)
            .ThenBy(rp => rp.Player.FirstName)
            .ToListAsync(cancellationToken);

    public async Task<RoundParticipant?> GetParticipantAsync(int roundId, int playerId, CancellationToken cancellationToken = default)
        => await _context.RoundParticipants
            .Include(rp => rp.Player)
            .Include(rp => rp.Round)
                .ThenInclude(r => r.Course)
            .FirstOrDefaultAsync(rp => rp.RoundId == roundId && rp.PlayerId == playerId, cancellationToken);

    public async Task<IReadOnlyList<HoleScore>> GetHoleScoresAsync(int participantId, CancellationToken cancellationToken = default)
        => await _context.HoleScores
            .Where(h => h.ParticipantId == participantId)
            .OrderBy(h => h.HoleNumber)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Round round, CancellationToken cancellationToken = default)
    {
        await _context.Rounds.AddAsync(round, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Round round, CancellationToken cancellationToken = default)
    {
        _context.Rounds.Update(round);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int roundId, CancellationToken cancellationToken = default)
    {
        var round = await _context.Rounds.FindAsync([roundId], cancellationToken);
        if (round is not null)
        {
            _context.Rounds.Remove(round);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task AddParticipantAsync(RoundParticipant participant, CancellationToken cancellationToken = default)
    {
        await _context.RoundParticipants.AddAsync(participant, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateParticipantAsync(RoundParticipant participant, CancellationToken cancellationToken = default)
    {
        _context.RoundParticipants.Update(participant);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddHoleScoresAsync(IEnumerable<HoleScore> holeScores, CancellationToken cancellationToken = default)
    {
        await _context.HoleScores.AddRangeAsync(holeScores, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearHoleScoresAsync(int participantId, CancellationToken cancellationToken = default)
    {
        var existingScores = await _context.HoleScores
            .Where(hs => hs.ParticipantId == participantId)
            .ToListAsync(cancellationToken);
        _context.HoleScores.RemoveRange(existingScores);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RoundParticipant>> GetParticipantsAsyncByPlayer(int playerId, CancellationToken cancellationToken = default)
        => await _context.RoundParticipants
            .Include(rp => rp.Round)
            .Include(rp => rp.HoleScores)
            .Where(rp => rp.PlayerId == playerId)
            .OrderBy(rp => rp.Round.RoundDate)
            .ToListAsync(cancellationToken);
}
