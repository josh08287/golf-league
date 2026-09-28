namespace GolfLeague.Domain.Services;

/// <summary>
/// 2v2 best-ball match play: each hole, a team's score is the lower of its
/// two players' strokes on that hole (net, matching how other match-play
/// formats in this app compare net strokes — see MatchPlayScoringService).
/// The lower team score wins the hole outright; a tie halves it. Uneven
/// teams (e.g. 1 vs 3 players) are supported — a team's best-ball score is
/// just the minimum across however many players it has.
/// </summary>
public static class BestBallScoringService
{
    public enum HoleWinner { Halved, TeamA, TeamB }

    public sealed record HoleResult(int HoleNumber, HoleWinner Winner, int TeamAStrokes, int TeamBStrokes);

    public sealed record MatchStatus(int TeamAHolesWon, int TeamBHolesWon, int HolesHalved, int HolesRemaining);

    /// <summary>
    /// Scores one hole given each team's players' net strokes on it.
    /// </summary>
    public static HoleResult ScoreHole(int holeNumber, IEnumerable<int> teamAStrokes, IEnumerable<int> teamBStrokes)
    {
        var a = NassauScoringService.BestBallStrokes(teamAStrokes);
        var b = NassauScoringService.BestBallStrokes(teamBStrokes);

        var winner = a < b ? HoleWinner.TeamA : a > b ? HoleWinner.TeamB : HoleWinner.Halved;
        return new HoleResult(holeNumber, winner, a, b);
    }

    public static MatchStatus Summarize(IReadOnlyList<HoleResult> holes, int totalHolesInRound)
    {
        int aWon = holes.Count(h => h.Winner == HoleWinner.TeamA);
        int bWon = holes.Count(h => h.Winner == HoleWinner.TeamB);
        int halved = holes.Count(h => h.Winner == HoleWinner.Halved);

        return new MatchStatus(aWon, bWon, halved, totalHolesInRound - holes.Count);
    }
}
