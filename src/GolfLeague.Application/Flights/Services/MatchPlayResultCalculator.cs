using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;

namespace GolfLeague.Application.Flights.Services;

/// <summary>
/// Computes and persists a FlightMatch's per-hole and total points once both
/// players' hole scores for the week are available (or one/both are
/// absent/on a bye). Invoked from SubmitHoleScoresCommand's handler after a
/// match-play half's participant scores are saved.
/// </summary>
public sealed class MatchPlayResultCalculator
{
    private readonly IFlightMatchRepository _flightMatchRepository;
    private readonly IRoundRepository _roundRepository;
    private readonly IMatchPlayFormulaEvaluator _formulaEvaluator;

    public MatchPlayResultCalculator(
        IFlightMatchRepository flightMatchRepository,
        IRoundRepository roundRepository,
        IMatchPlayFormulaEvaluator formulaEvaluator)
    {
        _flightMatchRepository = flightMatchRepository;
        _roundRepository = roundRepository;
        _formulaEvaluator = formulaEvaluator;
    }

    /// <summary>
    /// Recomputes every FlightMatch scheduled for <paramref name="roundId"/> that
    /// involves <paramref name="playerId"/>, if the match is ready to score
    /// (present side(s) have complete hole scores, or are skipped/on a bye).
    /// No-op if no FlightMatch exists for this round/player (e.g. Stableford half).
    /// </summary>
    public async Task RecomputeForRoundPlayerAsync(int roundId, int playerId, string? customFormula, CancellationToken cancellationToken)
    {
        var matches = await _flightMatchRepository.GetByRoundAsync(roundId, cancellationToken);
        var relevant = matches.Where(m => m.Player1Id == playerId || m.Player2Id == playerId);

        foreach (var match in relevant)
            await RecomputeAsync(match, customFormula, cancellationToken);
    }

    public async Task RecomputeAsync(FlightMatch match, string? customFormula, CancellationToken cancellationToken)
    {
        var participant1 = await _roundRepository.GetParticipantAsync(match.RoundId, match.Player1Id, cancellationToken);
        var participant2 = match.Player2Id.HasValue
            ? await _roundRepository.GetParticipantAsync(match.RoundId, match.Player2Id.Value, cancellationToken)
            : null;

        if (participant1 is null)
            return;

        var isBye = match.Player2Id is null;
        var player1Absent = participant1.SkippedWeek;
        var player2Absent = isBye || (participant2?.SkippedWeek ?? true);

        if (player1Absent && player2Absent)
        {
            await ScoreBothAbsentAsync(match, participant1, cancellationToken);
            return;
        }

        if (player2Absent)
        {
            await ScoreAgainstCardAsync(match, participant1, forPlayer1: true, customFormula, cancellationToken);
            return;
        }

        if (player1Absent)
        {
            await ScoreAgainstCardAsync(match, participant2!, forPlayer1: false, customFormula, cancellationToken);
            return;
        }

        await ScoreBothPresentAsync(match, participant1, participant2!, customFormula, cancellationToken);
    }

    private async Task ScoreBothPresentAsync(FlightMatch match, RoundParticipant p1, RoundParticipant p2, string? customFormula, CancellationToken cancellationToken)
    {
        var holes1 = (await _roundRepository.GetHoleScoresAsync(p1.Id, cancellationToken)).ToDictionary(h => h.HoleNumber);
        var holes2 = (await _roundRepository.GetHoleScoresAsync(p2.Id, cancellationToken)).ToDictionary(h => h.HoleNumber);

        var holeNumbers = holes1.Keys.Intersect(holes2.Keys).OrderBy(n => n).ToList();
        if (holeNumbers.Count == 0)
            return; // scores not yet entered for both sides

        var results = new List<FlightMatchHoleResult>();
        var p1HolesWon = 0;
        var p2HolesWon = 0;

        foreach (var holeNumber in holeNumbers)
        {
            var h1 = holes1[holeNumber];
            var h2 = holes2[holeNumber];

            int p1Points, p2Points;
            if (!string.IsNullOrWhiteSpace(customFormula))
            {
                p1Points = (int)Math.Round(_formulaEvaluator.Evaluate(customFormula, BuildInput(h1, p1, h2, p2, isAgainstCard: false)));
                p2Points = (int)Math.Round(_formulaEvaluator.Evaluate(customFormula, BuildInput(h2, p2, h1, p1, isAgainstCard: false)));
            }
            else
            {
                (p1Points, p2Points) = MatchPlayScoringService.HolePoints(h1.NetStrokes, h2.NetStrokes);
            }

            if (p1Points > p2Points) p1HolesWon++;
            else if (p2Points > p1Points) p2HolesWon++;

            results.Add(new FlightMatchHoleResult
            {
                HoleNumber = holeNumber,
                Player1Points = p1Points,
                Player2Points = p2Points,
                IsAgainstCard = false,
            });
        }

        var (bonus1, bonus2) = MatchPlayScoringService.MatchBonus(p1HolesWon, p2HolesWon);

        match.Player1Absent = false;
        match.Player2Absent = false;
        match.Player1HolesWon = p1HolesWon;
        match.Player2HolesWon = p2HolesWon;
        match.Player1Points = results.Sum(r => r.Player1Points) + bonus1;
        match.Player2Points = results.Sum(r => r.Player2Points) + bonus2;

        await _flightMatchRepository.ReplaceHoleResultsAsync(match.Id, results, cancellationToken);
        await _flightMatchRepository.UpdateMatchTotalsAsync(match, cancellationToken);
    }

    private async Task ScoreAgainstCardAsync(FlightMatch match, RoundParticipant present, bool forPlayer1, string? customFormula, CancellationToken cancellationToken)
    {
        var holes = await _roundRepository.GetHoleScoresAsync(present.Id, cancellationToken);
        if (holes.Count == 0)
            return; // scores not yet entered

        var results = new List<FlightMatchHoleResult>();
        var presentHolesWon = 0;
        var opponentHolesWon = 0;

        foreach (var hole in holes.OrderBy(h => h.HoleNumber))
        {
            int presentPoints;
            if (!string.IsNullOrWhiteSpace(customFormula))
            {
                presentPoints = (int)Math.Round(_formulaEvaluator.Evaluate(customFormula, BuildAgainstCardInput(hole, present)));
            }
            else
            {
                presentPoints = MatchPlayScoringService.AgainstCardHolePoints(hole.NetStrokes, hole.Par);
            }

            if (presentPoints > MatchPlayScoringService.HalvePoints) presentHolesWon++;
            else if (presentPoints < MatchPlayScoringService.HalvePoints) opponentHolesWon++;

            results.Add(new FlightMatchHoleResult
            {
                HoleNumber = hole.HoleNumber,
                Player1Points = forPlayer1 ? presentPoints : 0,
                Player2Points = forPlayer1 ? 0 : presentPoints,
                IsAgainstCard = true,
            });
        }

        // Bonus only applies between two real players — a missing/absent
        // opponent earns nothing regardless of holes lost to the card.
        var (presentBonus, _) = MatchPlayScoringService.MatchBonus(presentHolesWon, opponentHolesWon);
        var presentTotal = results.Sum(r => forPlayer1 ? r.Player1Points : r.Player2Points) + presentBonus;

        match.Player1Absent = !forPlayer1;
        match.Player2Absent = forPlayer1;
        match.Player1HolesWon = forPlayer1 ? presentHolesWon : 0;
        match.Player2HolesWon = forPlayer1 ? 0 : presentHolesWon;
        match.Player1Points = forPlayer1 ? presentTotal : 0;
        match.Player2Points = forPlayer1 ? 0 : presentTotal;

        await _flightMatchRepository.ReplaceHoleResultsAsync(match.Id, results, cancellationToken);
        await _flightMatchRepository.UpdateMatchTotalsAsync(match, cancellationToken);
    }

    private async Task ScoreBothAbsentAsync(FlightMatch match, RoundParticipant p1, CancellationToken cancellationToken)
    {
        // Neither side has a card to play against (both skipped) — full halve,
        // consistent with Stableford's fully-skipped-week handling.
        var holes = await _roundRepository.GetHoleScoresAsync(p1.Id, cancellationToken);
        var holeCount = holes.Count > 0 ? holes.Count : 9;

        var results = Enumerable.Range(1, holeCount)
            .Select(n => new FlightMatchHoleResult
            {
                HoleNumber = n,
                Player1Points = MatchPlayScoringService.HalvePoints,
                Player2Points = MatchPlayScoringService.HalvePoints,
                IsAgainstCard = false,
            })
            .ToList();

        match.Player1Absent = true;
        match.Player2Absent = true;
        match.Player1HolesWon = 0;
        match.Player2HolesWon = 0;
        match.Player1Points = holeCount * MatchPlayScoringService.HalvePoints;
        match.Player2Points = holeCount * MatchPlayScoringService.HalvePoints;

        await _flightMatchRepository.ReplaceHoleResultsAsync(match.Id, results, cancellationToken);
        await _flightMatchRepository.UpdateMatchTotalsAsync(match, cancellationToken);
    }

    private static MatchPlayFormulaInput BuildInput(HoleScore self, RoundParticipant selfParticipant, HoleScore opponent, RoundParticipant opponentParticipant, bool isAgainstCard)
        => new(
            NetStrokes: self.NetStrokes,
            OpponentNetStrokes: opponent.NetStrokes,
            GrossStrokes: self.GrossStrokes,
            OpponentGrossStrokes: opponent.GrossStrokes,
            Par: self.Par,
            StrokeIndex: self.StrokeIndex,
            HoleNumber: self.HoleNumber,
            CourseRating: selfParticipant.Round.Course.CourseRating,
            SlopeRating: selfParticipant.Round.Course.SlopeRating,
            HandicapIndex: selfParticipant.HandicapIndex,
            OpponentHandicapIndex: opponentParticipant.HandicapIndex,
            IsAgainstCard: isAgainstCard);

    private static MatchPlayFormulaInput BuildAgainstCardInput(HoleScore self, RoundParticipant selfParticipant)
        => new(
            NetStrokes: self.NetStrokes,
            OpponentNetStrokes: self.Par,
            GrossStrokes: self.GrossStrokes,
            OpponentGrossStrokes: self.Par,
            Par: self.Par,
            StrokeIndex: self.StrokeIndex,
            HoleNumber: self.HoleNumber,
            CourseRating: selfParticipant.Round.Course.CourseRating,
            SlopeRating: selfParticipant.Round.Course.SlopeRating,
            HandicapIndex: selfParticipant.HandicapIndex,
            OpponentHandicapIndex: selfParticipant.HandicapIndex,
            IsAgainstCard: true);
}
