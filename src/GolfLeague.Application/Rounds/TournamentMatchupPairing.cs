using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Domain.Entities;

namespace GolfLeague.Application.Rounds;

/// <summary>
/// One participant as seen by the default matchup-pairing algorithm —
/// deliberately minimal so both <see cref="Commands.CreateTournamentRoundCommand"/>
/// (pairing brand-new participants, which don't have a RoundParticipant.Id yet)
/// and <see cref="Commands.RegenerateTournamentMatchupsCommand"/> (pairing
/// existing RoundParticipant rows) can map into it.
/// </summary>
public readonly record struct PairablePlayer(
    int PlayerId,
    string PlayerName,
    double HandicapIndex,
    int CourseHandicap,
    bool IsSubstitute);

/// <summary>
/// Shared default tournament-matchup pairing: regular players are paired by
/// ascending handicap index (1v2, 3v4, ...). Substitutes are excluded from
/// that handicap-based pairing (they're filling in ad hoc, often without a
/// season-tracked handicap history, so a handicap-based match among them
/// isn't meaningful) and are instead paired randomly against other
/// substitutes, numbered after every regular matchup. An odd player out in
/// either group gets a "bye" matchup (Player2 is null) — one per group when
/// that group's count is odd — rather than being dropped.
/// Used identically by round creation's default pairing and by
/// "regenerate from handicaps."
/// </summary>
public static class TournamentMatchupPairing
{
    public static (List<TournamentMatchup> Entities, List<TournamentMatchupDto> Dtos) Build(
        int roundId, IReadOnlyList<PairablePlayer> players)
    {
        var regulars = players.Where(p => !p.IsSubstitute).OrderBy(p => p.HandicapIndex).ToList();
        var subs = players.Where(p => p.IsSubstitute).OrderBy(_ => Random.Shared.Next()).ToList();

        var matchupEntities = new List<TournamentMatchup>();
        var matchupDtos = new List<TournamentMatchupDto>();
        var matchupNum = 1;

        void PairGroup(List<PairablePlayer> group)
        {
            for (int i = 0; i + 1 < group.Count; i += 2)
            {
                var p1 = group[i];
                var p2 = group[i + 1];

                matchupEntities.Add(new TournamentMatchup
                {
                    RoundId = roundId,
                    MatchupNumber = matchupNum,
                    Player1Id = p1.PlayerId,
                    Player2Id = p2.PlayerId,
                });
                matchupDtos.Add(new TournamentMatchupDto(
                    matchupNum,
                    p1.PlayerId, p1.PlayerName, p1.HandicapIndex, p1.CourseHandicap,
                    p2.PlayerId, p2.PlayerName, p2.HandicapIndex, p2.CourseHandicap,
                    null));
                matchupNum++;
            }

            // Odd count — the leftover player gets a bye matchup instead of
            // being dropped from the round's matchups entirely.
            if (group.Count % 2 == 1)
            {
                var bye = group[^1];
                matchupEntities.Add(new TournamentMatchup
                {
                    RoundId = roundId,
                    MatchupNumber = matchupNum,
                    Player1Id = bye.PlayerId,
                    Player2Id = null,
                });
                matchupDtos.Add(new TournamentMatchupDto(
                    matchupNum,
                    bye.PlayerId, bye.PlayerName, bye.HandicapIndex, bye.CourseHandicap,
                    null, null, null, null,
                    null));
                matchupNum++;
            }
        }

        PairGroup(regulars);
        PairGroup(subs);

        return (matchupEntities, matchupDtos);
    }
}
