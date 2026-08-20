using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;

namespace GolfLeague.Domain.Interfaces;

public interface IRoundRepository
{
    Task<Round?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Round>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Round?> GetInProgressRoundAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Round>> GetBySeasonAsync(int seasonId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Round>> GetByHalfAsync(int halfId, CancellationToken cancellationToken = default);
    Task<RoundParticipant?> GetParticipantAsync(int roundId, int playerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoundParticipant>> GetParticipantsAsync(int roundId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HoleScore>> GetHoleScoresAsync(int participantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HoleScore>> GetHoleScoresForParticipantsAsync(int holeNumber, IEnumerable<int> participantIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HoleScore>> GetHoleScoresForParticipantsAsync(IEnumerable<int> participantIds, CancellationToken cancellationToken = default);
    Task AddAsync(Round round, CancellationToken cancellationToken = default);
    Task UpdateAsync(Round round, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(int roundId, RoundStatus status, CancellationToken cancellationToken = default);
    Task DeleteAsync(int roundId, CancellationToken cancellationToken = default);
    Task AddParticipantAsync(RoundParticipant participant, CancellationToken cancellationToken = default);
    Task UpdateParticipantAsync(RoundParticipant participant, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a RoundParticipant row outright (not a withdraw/skip toggle).
    /// Used to remove a substitute who was added by mistake before the round
    /// is played — a sub who never played shouldn't linger as a phantom row.
    /// </summary>
    Task DeleteParticipantAsync(int participantId, CancellationToken cancellationToken = default);
    Task AddHoleScoresAsync(IEnumerable<HoleScore> holeScores, CancellationToken cancellationToken = default);
    Task ClearHoleScoresAsync(int participantId, CancellationToken cancellationToken = default);
    Task ReplaceHoleScoresAsync(int participantId, IEnumerable<HoleScore> holeScores, CancellationToken cancellationToken = default);
    Task UpsertHoleScoresAsync(int holeNumber, IEnumerable<HoleScore> holeScores, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoundParticipant>> GetParticipantsAsyncByPlayer(int playerId, CancellationToken cancellationToken = default);
    Task<Round?> GetPreviousRoundAsync(int halfId, int currentWeekNumber, CancellationToken cancellationToken = default);

    /// <summary>Stamps <see cref="Round.SignUpReminderSentAt"/> so the reminder isn't re-sent on a later timer run.</summary>
    Task MarkSignUpReminderSentAsync(int roundId, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stamps <see cref="Round.TeeTimeScheduleEmailSentAt"/> so the autofill
    /// timer doesn't automatically re-send the schedule email on a later run.
    /// Not called by the admin "resend" action, which should always be able
    /// to send on demand.
    /// </summary>
    Task MarkTeeTimeScheduleEmailSentAsync(int roundId, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>Stamps <see cref="Round.SubSpotEmailSentAt"/> so the substitute-pool email isn't re-sent on a later timer run.</summary>
    Task MarkSubSpotEmailSentAsync(int roundId, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>
    /// All rounds in <paramref name="seasonId"/> strictly before <paramref name="currentRoundDate"/>,
    /// ordered chronologically. Scoped to the season (not the half) so that state carried
    /// across rounds — e.g. par-3 skins carryover — persists across a season's half boundary
    /// and only resets when a new season begins.
    /// </summary>
    Task<IReadOnlyList<Round>> GetPreviousRoundsAsync(int seasonId, DateOnly currentRoundDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Shifts RoundDate and WeekNumber of all non-cancelled rounds in <paramref name="halfId"/>
    /// whose WeekNumber is strictly greater than <paramref name="afterWeekNumber"/> by the given
    /// <paramref name="daysToAdd"/> and <paramref name="weekNumberIncrement"/>.
    /// </summary>
    Task ShiftRoundsForwardAsync(int halfId, int afterWeekNumber, int daysToAdd, int weekNumberIncrement, CancellationToken cancellationToken = default);

    // Tournament-specific
    Task AddTournamentMatchupsAsync(IEnumerable<TournamentMatchup> matchups, CancellationToken cancellationToken = default);
    Task ReplaceTournamentMatchupsAsync(int roundId, IEnumerable<TournamentMatchup> matchups, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TournamentMatchup>> GetTournamentMatchupsAsync(int roundId, CancellationToken cancellationToken = default);
    Task UpsertTournamentHoleExtrasAsync(IEnumerable<TournamentHoleExtra> extras, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TournamentHoleExtra>> GetTournamentHoleExtrasAsync(int roundId, CancellationToken cancellationToken = default);

    /// <summary>Sets (or clears, when playerId is null) the longest-drive winner for one tournament flight.</summary>
    Task SetLongestDriveWinnerAsync(int roundId, int tournamentFlightId, int? playerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TournamentLongestDriveWinner>> GetLongestDriveWinnersAsync(int roundId, CancellationToken cancellationToken = default);

    /// <summary>Replaces a tournament round's flights (used by the handicap-based auto-regroup). Deletes existing flights and their participant links first.</summary>
    Task ReplaceTournamentFlightsAsync(int roundId, IEnumerable<TournamentFlight> flights, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TournamentFlight>> GetTournamentFlightsAsync(int roundId, CancellationToken cancellationToken = default);
    Task SetParticipantTournamentFlightAsync(int participantId, int? tournamentFlightId, CancellationToken cancellationToken = default);

    // Closest to the pin (regular league rounds)
    Task SetClosestToPinWinnersAsync(int roundId, IEnumerable<(int HoleNumber, int PlayerId)> winners, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoundClosestToPin>> GetClosestToPinWinnersAsync(int roundId, CancellationToken cancellationToken = default);
}
