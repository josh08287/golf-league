using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;

namespace GolfLeague.Domain.Interfaces;

public interface IRoundRepository
{
    Task<Round?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Round>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Round>> GetBySeasonAsync(int seasonId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Round>> GetByHalfAsync(int halfId, CancellationToken cancellationToken = default);
    Task<RoundParticipant?> GetParticipantAsync(int roundId, int playerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoundParticipant>> GetParticipantsAsync(int roundId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HoleScore>> GetHoleScoresAsync(int participantId, CancellationToken cancellationToken = default);
    Task AddAsync(Round round, CancellationToken cancellationToken = default);
    Task UpdateAsync(Round round, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(int roundId, RoundStatus status, CancellationToken cancellationToken = default);
    Task DeleteAsync(int roundId, CancellationToken cancellationToken = default);
    Task AddParticipantAsync(RoundParticipant participant, CancellationToken cancellationToken = default);
    Task UpdateParticipantAsync(RoundParticipant participant, CancellationToken cancellationToken = default);
    Task AddHoleScoresAsync(IEnumerable<HoleScore> holeScores, CancellationToken cancellationToken = default);
    Task ClearHoleScoresAsync(int participantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoundParticipant>> GetParticipantsAsyncByPlayer(int playerId, CancellationToken cancellationToken = default);
    Task<Round?> GetPreviousRoundAsync(int halfId, int currentWeekNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Round>> GetPreviousRoundsAsync(int halfId, int currentWeekNumber, CancellationToken cancellationToken = default);

    // Tournament-specific
    Task AddTournamentMatchupsAsync(IEnumerable<TournamentMatchup> matchups, CancellationToken cancellationToken = default);
    Task ReplaceTournamentMatchupsAsync(int roundId, IEnumerable<TournamentMatchup> matchups, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TournamentMatchup>> GetTournamentMatchupsAsync(int roundId, CancellationToken cancellationToken = default);
    Task UpsertTournamentHoleExtrasAsync(IEnumerable<TournamentHoleExtra> extras, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TournamentHoleExtra>> GetTournamentHoleExtrasAsync(int roundId, CancellationToken cancellationToken = default);
}
