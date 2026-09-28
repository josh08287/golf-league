using GolfLeague.Application.Common;
using GolfLeague.Application.Rounds;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Replaces a tournament round's matchups with a fresh default pairing —
/// see <see cref="TournamentMatchupPairing"/> for the algorithm, shared with
/// <see cref="CreateTournamentRoundCommand"/>'s default pairing.
/// </summary>
public sealed record RegenerateTournamentMatchupsCommand(int RoundId, string UserId)
    : IRequest<Result<List<TournamentMatchupDto>>>, IAmAuditableCommand
{
    public string AuditEntityType => "Round";
    public string AuditEntityId => RoundId.ToString();
}

public sealed class RegenerateTournamentMatchupsCommandHandler
    : IRequestHandler<RegenerateTournamentMatchupsCommand, Result<List<TournamentMatchupDto>>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly TournamentFoursomeService _foursomeService;

    public RegenerateTournamentMatchupsCommandHandler(
        IRoundRepository roundRepository, TournamentFoursomeService foursomeService)
    {
        _roundRepository = roundRepository;
        _foursomeService = foursomeService;
    }

    public async Task<Result<List<TournamentMatchupDto>>> Handle(RegenerateTournamentMatchupsCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<List<TournamentMatchupDto>>.Fail($"Round {request.RoundId} not found.");
        if (round.RoundType != RoundType.Tournament)
            return Result<List<TournamentMatchupDto>>.Fail("This round is not a tournament round.");
        if (round.Status != RoundStatus.Scheduled)
            return Result<List<TournamentMatchupDto>>.Fail("Matchups can only be regenerated while the round is Scheduled.");

        // Use the player's current substitute-pool status, not the
        // RoundParticipant.IsSubstitute snapshot taken when they were added —
        // that snapshot goes stale if the player's substitute flag changes
        // afterward (e.g. added as a regular, then later marked a substitute).
        // Persist the refreshed snapshot so downstream reads (standings,
        // scoring exclusions) stay in sync too.
        foreach (var participant in round.Participants.Where(p => p.IsSubstitute != p.Player.IsSubstitute))
        {
            participant.IsSubstitute = participant.Player.IsSubstitute;
            await _roundRepository.UpdateParticipantAsync(participant, cancellationToken);
        }

        var players = round.Participants
            .Select(p => new PairablePlayer(p.PlayerId, p.Player.FullName, p.HandicapIndex, p.CourseHandicap, p.IsSubstitute))
            .ToList();
        var (matchupEntities, matchupDtos) = TournamentMatchupPairing.Build(round.Id, players);

        await _roundRepository.ReplaceTournamentMatchupsAsync(round.Id, matchupEntities, cancellationToken);

        // Also re-run flight/tee-time grouping: rounds created or added-to
        // before the substitute-exclusion fix can have substitutes stuck in a
        // handicap flight from a prior grouping. Regenerating matchups is the
        // action an admin reaches for to "fix up" a round's pairings, so make
        // it correct the flights too rather than requiring a separate step.
        await _foursomeService.RegroupAsync(round.Id, round.Participants.ToList(), cancellationToken);

        return Result<List<TournamentMatchupDto>>.Ok(matchupDtos);
    }
}
