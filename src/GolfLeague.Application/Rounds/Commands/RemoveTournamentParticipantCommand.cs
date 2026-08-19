using GolfLeague.Application.Common;
using GolfLeague.Application.Rounds;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Removes a player from a tournament round's roster. Only allowed while
/// the round is still Scheduled. Any matchup involving the removed player
/// is dropped and the remaining matchups are renumbered.
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
        if (round.Status != RoundStatus.Scheduled)
            return Result<bool>.Fail("Players can only be removed while the round is still Scheduled.");

        var participant = round.Participants.FirstOrDefault(p => p.PlayerId == request.PlayerId);
        if (participant is null)
            return Result<bool>.Fail("That player is not a participant in this round.");

        var existingMatchups = await _roundRepository.GetTournamentMatchupsAsync(request.RoundId, cancellationToken);
        var remainingMatchups = existingMatchups
            .Where(m => m.Player1Id != request.PlayerId && m.Player2Id != request.PlayerId)
            .OrderBy(m => m.MatchupNumber)
            .Select((m, i) => { m.MatchupNumber = i + 1; return m; })
            .ToList();

        if (remainingMatchups.Count != existingMatchups.Count)
            await _roundRepository.ReplaceTournamentMatchupsAsync(request.RoundId, remainingMatchups, cancellationToken);

        await _roundRepository.DeleteParticipantAsync(participant.Id, cancellationToken);

        var remainingParticipants = round.Participants.Where(p => p.Id != participant.Id).ToList();
        await _foursomeService.RegroupAsync(request.RoundId, remainingParticipants, cancellationToken);

        return Result<bool>.Ok(true);
    }
}
