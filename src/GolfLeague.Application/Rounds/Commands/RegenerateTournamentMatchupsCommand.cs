using GolfLeague.Application.Common;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Replaces a tournament round's matchups with a fresh handicap-based
/// default pairing — same algorithm as <see cref="CreateTournamentRoundCommand"/>'s
/// default pairing (sort by handicap index ascending, pair 1v2, 3v4, ...),
/// except regular players and substitutes are paired within their own group
/// only. A substitute would otherwise have no meaningful handicap-based
/// match among regular roster players (they're filling in ad hoc, often
/// without a season-tracked handicap history), so they're kept to
/// substitute-vs-substitute matchups, numbered after every regular matchup.
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

    public RegenerateTournamentMatchupsCommandHandler(IRoundRepository roundRepository)
    {
        _roundRepository = roundRepository;
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

        var regulars = round.Participants.Where(p => !p.IsSubstitute).OrderBy(p => p.HandicapIndex).ToList();
        var subs = round.Participants.Where(p => p.IsSubstitute).OrderBy(p => p.HandicapIndex).ToList();

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

        return Result<List<TournamentMatchupDto>>.Ok(matchupDtos);
    }
}
