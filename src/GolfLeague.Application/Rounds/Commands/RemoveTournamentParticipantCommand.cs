using GolfLeague.Application.Common;
using GolfLeague.Application.Rounds;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Removes a player from a tournament round's roster, along with any hole
/// scores entered for them. Allowed until the round is Finalized, so an
/// admin can drop a no-show while the round is underway.
///
/// While Scheduled, any matchup involving the removed player is dropped,
/// the remaining matchups are renumbered, and foursomes are regrouped.
/// Once play has started, groups are on the course, so tee times and
/// flights are left alone and the removed player's opponent keeps their
/// matchup as a bye. Any CTP / longest-drive award held by the removed
/// player is cleared either way.
/// </summary>
public sealed record RemoveTournamentParticipantCommand(
    int RoundId,
    int PlayerId,
    string UserId) : IRequest<Result<bool>>, IAmAuditableCommand
{
    public string AuditEntityType => "Round";
    public string AuditEntityId => RoundId.ToString();
}

public sealed class RemoveTournamentParticipantCommandHandler : IRequestHandler<RemoveTournamentParticipantCommand, Result<bool>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly TournamentFoursomeService _foursomeService;

    public RemoveTournamentParticipantCommandHandler(IRoundRepository roundRepository, TournamentFoursomeService foursomeService)
    {
        _roundRepository = roundRepository;
        _foursomeService = foursomeService;
    }

    public async Task<Result<bool>> Handle(RemoveTournamentParticipantCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<bool>.Fail($"Round {request.RoundId} not found.");
        if (round.RoundType != RoundType.Tournament)
            return Result<bool>.Fail("This round is not a tournament round.");
        if (round.Status is RoundStatus.Finalized or RoundStatus.Cancelled)
            return Result<bool>.Fail($"Players can't be removed from a {round.Status.ToString().ToLowerInvariant()} round.");

        var participant = round.Participants.FirstOrDefault(p => p.PlayerId == request.PlayerId);
        if (participant is null)
            return Result<bool>.Fail("That player is not a participant in this round.");

        var isScheduled = round.Status == RoundStatus.Scheduled;
        var existingMatchups = await _roundRepository.GetTournamentMatchupsAsync(request.RoundId, cancellationToken);
        var affected = existingMatchups.Any(m => m.Player1Id == request.PlayerId || m.Player2Id == request.PlayerId);

        if (affected)
        {
            // Rebuilt as fresh rows (no Id / navigations) since the
            // repository replaces the whole set, and a loaded Player1/Player2
            // navigation would otherwise re-point a swapped player ID.
            var updatedMatchups = new List<TournamentMatchup>();
            foreach (var m in existingMatchups.OrderBy(m => m.MatchupNumber))
            {
                int player1Id = m.Player1Id;
                int? player2Id = m.Player2Id;
                var involvesRemoved = player1Id == request.PlayerId || player2Id == request.PlayerId;

                if (involvesRemoved)
                {
                    // Before play starts the pairing is simply dropped (and
                    // regrouping follows); once underway the opponent keeps
                    // their matchup as a bye.
                    if (isScheduled)
                        continue;
                    var opponentId = player1Id == request.PlayerId ? player2Id : player1Id;
                    if (opponentId is not int opponent)
                        continue; // the removed player's own bye — nothing left
                    player1Id = opponent;
                    player2Id = null;
                }

                updatedMatchups.Add(new TournamentMatchup
                {
                    RoundId = m.RoundId,
                    MatchupNumber = updatedMatchups.Count + 1,
                    Player1Id = player1Id,
                    Player2Id = player2Id,
                    WinnerPlayerId = m.WinnerPlayerId == request.PlayerId ? null : m.WinnerPlayerId,
                });
            }

            await _roundRepository.ReplaceTournamentMatchupsAsync(request.RoundId, updatedMatchups, cancellationToken);
        }

        await _roundRepository.ClearTournamentAwardsForPlayerAsync(request.RoundId, request.PlayerId, cancellationToken);
        await _roundRepository.DeleteParticipantAsync(participant.Id, cancellationToken);

        if (isScheduled)
        {
            var remainingParticipants = round.Participants.Where(p => p.Id != participant.Id).ToList();
            await _foursomeService.RegroupAsync(request.RoundId, remainingParticipants, cancellationToken);
        }

        return Result<bool>.Ok(true);
    }
}
