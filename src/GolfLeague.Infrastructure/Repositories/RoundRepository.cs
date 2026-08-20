using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
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

    public Task<Round?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => _context.Rounds
            .Include(r => r.Course).ThenInclude(c => c.Holes)
            .Include(r => r.Half)
            .Include(r => r.Season)
            .Include(r => r.Participants).ThenInclude(rp => rp.Player)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<Round?> GetInProgressRoundAsync(CancellationToken cancellationToken = default)
        => _context.Rounds
            .Include(r => r.Course)
            .FirstOrDefaultAsync(r => r.Status == RoundStatus.InProgress, cancellationToken);

    public async Task<IReadOnlyList<Round>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Rounds
            .Include(r => r.Course)
            .Include(r => r.Half)
            .Include(r => r.Participants)
            .OrderByDescending(r => r.RoundDate)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Round>> GetBySeasonAsync(int seasonId, CancellationToken cancellationToken = default)
        => await _context.Rounds
            .Include(r => r.Course)
            .Include(r => r.Half)
            .Include(r => r.Participants)
            .Where(r => r.SeasonId == seasonId)
            .OrderByDescending(r => r.RoundDate)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Round>> GetByHalfAsync(int halfId, CancellationToken cancellationToken = default)
        => await _context.Rounds
            .Include(r => r.Course)
            .Include(r => r.Half)
            .Include(r => r.Participants)
            .Where(r => r.HalfId == halfId)
            .OrderBy(r => r.WeekNumber)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RoundParticipant>> GetParticipantsAsync(int roundId, CancellationToken cancellationToken = default)
        => await _context.RoundParticipants
            .Include(rp => rp.Player)
            .Include(rp => rp.HoleScores)
            .Where(rp => rp.RoundId == roundId)
            .OrderBy(rp => rp.FlightId)
            .ThenBy(rp => rp.Player.LastName)
            .ThenBy(rp => rp.Player.FirstName)
            .ToListAsync(cancellationToken);

    public Task<RoundParticipant?> GetParticipantAsync(int roundId, int playerId, CancellationToken cancellationToken = default)
        => _context.RoundParticipants
            .Include(rp => rp.Player)
            .Include(rp => rp.Round).ThenInclude(r => r.Course)
            .FirstOrDefaultAsync(rp => rp.RoundId == roundId && rp.PlayerId == playerId, cancellationToken);

    public async Task<IReadOnlyList<HoleScore>> GetHoleScoresAsync(int participantId, CancellationToken cancellationToken = default)
        => await _context.HoleScores
            .Where(h => h.ParticipantId == participantId)
            .OrderBy(h => h.HoleNumber)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<HoleScore>> GetHoleScoresForParticipantsAsync(int holeNumber, IEnumerable<int> participantIds, CancellationToken cancellationToken = default)
    {
        var ids = participantIds.ToList();
        return await _context.HoleScores
            .Where(h => h.HoleNumber == holeNumber && ids.Contains(h.ParticipantId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HoleScore>> GetHoleScoresForParticipantsAsync(IEnumerable<int> participantIds, CancellationToken cancellationToken = default)
    {
        var ids = participantIds.ToList();
        return await _context.HoleScores
            .Where(h => ids.Contains(h.ParticipantId))
            .ToListAsync(cancellationToken);
    }

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

    public async Task UpdateStatusAsync(int roundId, RoundStatus status, CancellationToken cancellationToken = default)
    {
        // Set-based update so we don't reattach the Round graph (Participants
        // included) and clash with any participant we just tracked.
        await _context.Rounds
            .Where(r => r.Id == roundId)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, status), cancellationToken);
    }

    public async Task DeleteAsync(int roundId, CancellationToken cancellationToken = default)
    {
        // Clear TeeTimeId for all participants in this round to avoid FK constraint
        await _context.RoundParticipants
            .Where(rp => rp.RoundId == roundId && rp.TeeTimeId != null)
            .ExecuteUpdateAsync(
                u => u.SetProperty(rp => rp.TeeTimeId, (int?)null),
                cancellationToken);

        // Delete all tee times for this round
        await _context.RoundTeeTimes
            .Where(rt => rt.RoundId == roundId)
            .ExecuteDeleteAsync(cancellationToken);

        // Get all participant IDs for this round
        var participantIds = await _context.RoundParticipants
            .Where(rp => rp.RoundId == roundId)
            .Select(rp => rp.Id)
            .ToListAsync(cancellationToken);

        // Delete hole scores for all participants
        if (participantIds.Count > 0)
        {
            await _context.HoleScores
                .Where(hs => participantIds.Contains(hs.ParticipantId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        // Delete all participants for this round
        await _context.RoundParticipants
            .Where(rp => rp.RoundId == roundId)
            .ExecuteDeleteAsync(cancellationToken);

        // Delete the round
        await _context.Rounds
            .Where(r => r.Id == roundId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task AddParticipantAsync(RoundParticipant participant, CancellationToken cancellationToken = default)
    {
        await _context.RoundParticipants.AddAsync(participant, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateParticipantAsync(RoundParticipant participant, CancellationToken cancellationToken = default)
    {
        await _context.RoundParticipants
            .Where(rp => rp.Id == participant.Id)
            .ExecuteUpdateAsync(u => u
                .SetProperty(rp => rp.TotalGrossStrokes, participant.TotalGrossStrokes)
                .SetProperty(rp => rp.TotalNetStrokes, participant.TotalNetStrokes)
                .SetProperty(rp => rp.TotalGrossStablefordPoints, participant.TotalGrossStablefordPoints)
                .SetProperty(rp => rp.TotalNetStablefordPoints, participant.TotalNetStablefordPoints)
                .SetProperty(rp => rp.IsWithdrawn, participant.IsWithdrawn)
                .SetProperty(rp => rp.SkippedWeek, participant.SkippedWeek)
                .SetProperty(rp => rp.HandicapIndex, participant.HandicapIndex)
                .SetProperty(rp => rp.CourseHandicap, participant.CourseHandicap)
                .SetProperty(rp => rp.FlightId, participant.FlightId),
            cancellationToken);
    }

    public async Task DeleteParticipantAsync(int participantId, CancellationToken cancellationToken = default)
    {
        await _context.RoundParticipants
            .Where(rp => rp.Id == participantId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task AddHoleScoresAsync(IEnumerable<HoleScore> holeScores, CancellationToken cancellationToken = default)
    {
        await _context.HoleScores.AddRangeAsync(holeScores, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearHoleScoresAsync(int participantId, CancellationToken cancellationToken = default)
    {
        await _context.HoleScores
            .Where(hs => hs.ParticipantId == participantId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task ReplaceHoleScoresAsync(int participantId, IEnumerable<HoleScore> holeScores, CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
            await _context.HoleScores
                .Where(hs => hs.ParticipantId == participantId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.HoleScores.AddRangeAsync(holeScores, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        });
    }

    public async Task UpsertHoleScoresAsync(int holeNumber, IEnumerable<HoleScore> holeScores, CancellationToken cancellationToken = default)
    {
        var participantIds = holeScores.Select(h => h.ParticipantId).ToList();
        await _context.HoleScores
            .Where(hs => participantIds.Contains(hs.ParticipantId) && hs.HoleNumber == holeNumber)
            .ExecuteDeleteAsync(cancellationToken);
        await _context.HoleScores.AddRangeAsync(holeScores, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RoundParticipant>> GetParticipantsAsyncByPlayer(int playerId, CancellationToken cancellationToken = default)
        => await _context.RoundParticipants
            .Include(rp => rp.Round).ThenInclude(r => r.Course)
            .Include(rp => rp.HoleScores)
            .Where(rp => rp.PlayerId == playerId)
            .OrderBy(rp => rp.Round.RoundDate)
            .ToListAsync(cancellationToken);

    public Task<Round?> GetPreviousRoundAsync(int halfId, int currentWeekNumber, CancellationToken cancellationToken = default)
        => _context.Rounds
            .Include(r => r.Course)
            .Include(r => r.Half)
            .Include(r => r.Participants).ThenInclude(rp => rp.Player)
            .Include(r => r.Participants).ThenInclude(rp => rp.HoleScores)
            .Where(r => r.HalfId == halfId && r.WeekNumber < currentWeekNumber)
            .OrderByDescending(r => r.WeekNumber)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task MarkSignUpReminderSentAsync(int roundId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var round = await _context.Rounds
            .AsTracking()
            .FirstOrDefaultAsync(r => r.Id == roundId, cancellationToken);
        if (round is null) return;
        round.SignUpReminderSentAt = utcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkTeeTimeScheduleEmailSentAsync(int roundId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var round = await _context.Rounds
            .AsTracking()
            .FirstOrDefaultAsync(r => r.Id == roundId, cancellationToken);
        if (round is null) return;
        round.TeeTimeScheduleEmailSentAt = utcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkSubSpotEmailSentAsync(int roundId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var round = await _context.Rounds
            .AsTracking()
            .FirstOrDefaultAsync(r => r.Id == roundId, cancellationToken);
        if (round is null) return;
        round.SubSpotEmailSentAt = utcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Round>> GetPreviousRoundsAsync(int seasonId, DateOnly currentRoundDate, CancellationToken cancellationToken = default)
        => await _context.Rounds
            .Include(r => r.Course)
            .Include(r => r.Half)
            .Include(r => r.Participants).ThenInclude(rp => rp.Player)
            .Include(r => r.Participants).ThenInclude(rp => rp.HoleScores)
            .Where(r => r.SeasonId == seasonId && r.RoundDate < currentRoundDate)
            .OrderBy(r => r.RoundDate)
            .ThenBy(r => r.WeekNumber)
            .ToListAsync(cancellationToken);

    public async Task ShiftRoundsForwardAsync(int halfId, int afterWeekNumber, int daysToAdd, int weekNumberIncrement, CancellationToken cancellationToken = default)
    {
        var toShift = await _context.Rounds
            .Where(r => r.HalfId == halfId
                     && r.WeekNumber > afterWeekNumber
                     && r.Status != RoundStatus.Cancelled)
            .ToListAsync(cancellationToken);

        foreach (var r in toShift)
        {
            r.RoundDate = r.RoundDate.AddDays(daysToAdd);
            r.WeekNumber += weekNumberIncrement;
        }

        if (toShift.Count > 0)
            await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddTournamentMatchupsAsync(IEnumerable<TournamentMatchup> matchups, CancellationToken cancellationToken = default)
    {
        await _context.TournamentMatchups.AddRangeAsync(matchups, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceTournamentMatchupsAsync(int roundId, IEnumerable<TournamentMatchup> matchups, CancellationToken cancellationToken = default)
    {
        await _context.TournamentMatchups
            .Where(m => m.RoundId == roundId)
            .ExecuteDeleteAsync(cancellationToken);
        await _context.TournamentMatchups.AddRangeAsync(matchups, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceTournamentFlightsAsync(int roundId, IEnumerable<TournamentFlight> flights, CancellationToken cancellationToken = default)
    {
        // Winners reference flights (NoAction FK — see AppDbContext), so
        // clear them first or the delete below would violate the constraint.
        await _context.TournamentLongestDriveWinners
            .Where(w => w.RoundId == roundId)
            .ExecuteDeleteAsync(cancellationToken);
        await _context.TournamentFlights
            .Where(f => f.RoundId == roundId)
            .ExecuteDeleteAsync(cancellationToken);

        var flightsList = flights.ToList();
        if (flightsList.Count > 0)
        {
            await _context.TournamentFlights.AddRangeAsync(flightsList, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<TournamentFlight>> GetTournamentFlightsAsync(int roundId, CancellationToken cancellationToken = default)
        => await _context.TournamentFlights
            .Where(f => f.RoundId == roundId)
            .OrderBy(f => f.FlightNumber)
            .ToListAsync(cancellationToken);

    public async Task SetParticipantTournamentFlightAsync(int participantId, int? tournamentFlightId, CancellationToken cancellationToken = default)
    {
        var participant = await _context.RoundParticipants.FindAsync([participantId], cancellationToken);
        if (participant is null) return;
        participant.TournamentFlightId = tournamentFlightId;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TournamentMatchup>> GetTournamentMatchupsAsync(int roundId, CancellationToken cancellationToken = default)
        => await _context.TournamentMatchups
            .Include(m => m.Player1)
            .Include(m => m.Player2)
            .Where(m => m.RoundId == roundId)
            .OrderBy(m => m.MatchupNumber)
            .ToListAsync(cancellationToken);

    public async Task UpsertTournamentHoleExtrasAsync(IEnumerable<TournamentHoleExtra> extras, CancellationToken cancellationToken = default)
    {
        var extrasList = extras.ToList();
        var roundId = extrasList.First().RoundId;
        var holeNumbers = extrasList.Select(e => e.HoleNumber).ToList();

        await _context.TournamentHoleExtras
            .Where(e => e.RoundId == roundId && holeNumbers.Contains(e.HoleNumber))
            .ExecuteDeleteAsync(cancellationToken);

        await _context.TournamentHoleExtras.AddRangeAsync(extrasList, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TournamentHoleExtra>> GetTournamentHoleExtrasAsync(int roundId, CancellationToken cancellationToken = default)
        => await _context.TournamentHoleExtras
            .Include(e => e.ClosestToPinPlayer)
            .Include(e => e.LongestDrivePlayer)
            .Where(e => e.RoundId == roundId)
            .OrderBy(e => e.HoleNumber)
            .ToListAsync(cancellationToken);

    public async Task SetLongestDriveWinnerAsync(int roundId, int tournamentFlightId, int? playerId, CancellationToken cancellationToken = default)
    {
        await _context.TournamentLongestDriveWinners
            .Where(w => w.RoundId == roundId && w.TournamentFlightId == tournamentFlightId)
            .ExecuteDeleteAsync(cancellationToken);

        if (playerId is int pid)
        {
            await _context.TournamentLongestDriveWinners.AddAsync(new TournamentLongestDriveWinner
            {
                RoundId = roundId,
                TournamentFlightId = tournamentFlightId,
                PlayerId = pid,
            }, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<TournamentLongestDriveWinner>> GetLongestDriveWinnersAsync(int roundId, CancellationToken cancellationToken = default)
        => await _context.TournamentLongestDriveWinners
            .Include(w => w.Player)
            .Include(w => w.TournamentFlight)
            .Where(w => w.RoundId == roundId)
            .OrderBy(w => w.TournamentFlight.FlightNumber)
            .ToListAsync(cancellationToken);

    public async Task SetClosestToPinWinnersAsync(int roundId, IEnumerable<(int HoleNumber, int PlayerId)> winners, CancellationToken cancellationToken = default)
    {
        await _context.RoundClosestToPins
            .Where(w => w.RoundId == roundId)
            .ExecuteDeleteAsync(cancellationToken);

        var rows = winners
            .GroupBy(w => w.HoleNumber)
            .Select(g => new RoundClosestToPin
            {
                RoundId = roundId,
                HoleNumber = g.Key,
                PlayerId = g.First().PlayerId,
            })
            .ToList();

        if (rows.Count > 0)
        {
            await _context.RoundClosestToPins.AddRangeAsync(rows, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<RoundClosestToPin>> GetClosestToPinWinnersAsync(int roundId, CancellationToken cancellationToken = default)
        => await _context.RoundClosestToPins
            .Include(w => w.Player)
            .Where(w => w.RoundId == roundId)
            .OrderBy(w => w.HoleNumber)
            .ToListAsync(cancellationToken);
}
