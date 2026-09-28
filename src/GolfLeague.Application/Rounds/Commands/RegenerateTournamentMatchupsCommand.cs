using GolfLeague.Application.Common;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Replaces a tournament round's matchups with a fresh default pairing:
/// regular players are paired by ascending handicap index (1v2, 3v4, ...) —
/// same algorithm as <see cref="CreateTournamentRoundCommand"/>'s default
/// pairing. Substitutes are excluded from that handicap-based pairing (they're
/// filling in ad hoc, often without a season-tracked handicap history, so a
/// handicap-based match among them is not meaningful) and are instead paired
/// randomly against other substitutes, numbered after every regular matchup.
/// An odd player out in either group is left unmatched, same as creation.
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

        var regulars = round.Participants.Where(p => !p.IsSubstitute).OrderBy(p => p.HandicapIndex).ToList();
        var subs = round.Participants.Where(p => p.IsSubstitute).OrderBy(_ => Random.Shared.Next()).ToList();

        var matchupEntities = new List<TournamentMatchup>();
        var matchupDtos = new List<TournamentMatchupDto>();
        var matchupNum = 1;

        void PairGroup(List<RoundParticipant> group)
        {
            for (int i = 0; i + 1 < group.Count; i += 2)
            {
                var p1 = group[i];
                var p2 = group[i + 1];

                matchupEntities.Add(new TournamentMatchup
                {
                    RoundId = round.Id,
                    MatchupNumber = matchupNum,
                    Player1Id = p1.PlayerId,
                    Player2Id = p2.PlayerId,
                });
                matchupDtos.Add(new TournamentMatchupDto(
                    matchupNum,
                    p1.PlayerId, p1.Player.FullName, p1.HandicapIndex, p1.CourseHandicap,
                    p2.PlayerId, p2.Player.FullName, p2.HandicapIndex, p2.CourseHandicap,
                    null));
                matchupNum++;
            }
        }

        PairGroup(regulars);
        PairGroup(subs);

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
