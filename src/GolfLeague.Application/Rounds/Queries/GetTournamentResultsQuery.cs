using GolfLeague.Application.Common;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Queries;

// ── Result DTOs ────────────────────────────────────────────────────────────────

public sealed record TournamentSkinHoleDto(
    int HoleNumber,
    int Par,
    int SkinValue,
    int? WinnerPlayerId,
    string? WinnerPlayerName,
    int? WinningScore,
    bool WasCarryover,
    bool IsTie);

public sealed record TournamentPlayerSkinDto(
    int PlayerId,
    string PlayerName,
    int TotalSkinsWon,
    int TotalSkinValue,
    List<TournamentSkinHoleDto> HolesWon,
    decimal? PayoutAmount);

public sealed record TournamentSkinsResultDto(
    string SkinType,
    List<TournamentSkinHoleDto> HoleResults,
    List<TournamentPlayerSkinDto> PlayerSummaries,
    decimal? PoolAmount,
    /// <summary>Pool ÷ total skin points won — each point (a plain skin, or one unit of carryover) pays the same.</summary>
    decimal? PerSkinPayout);

/// <summary>
/// Player2* fields are null for a "bye" matchup (an odd player out with no
/// opponent) — that matchup always resolves as a win for Player1.
/// </summary>
public sealed record TournamentMatchupResultDto(
    int MatchupNumber,
    int Player1Id,
    string Player1Name,
    double Player1HandicapIndex,
    int Player1CourseHandicap,
    int? Player1NetStrokes,
    int? Player1NetPoints,
    int? Player2Id,
    string? Player2Name,
    double? Player2HandicapIndex,
    int? Player2CourseHandicap,
    int? Player2NetStrokes,
    int? Player2NetPoints,
    int? WinnerPlayerId,
    string? WinnerPlayerName,
    bool IsHalved,
    List<MatchPlayHoleDto> HoleByHole,
    int? WinnerPlayerIdByPoints,
    string? WinnerPlayerNameByPoints,
    bool IsHalvedByPoints,
    List<MatchPlayHoleDto> HoleByHolePoints);

/// <summary>
/// Match-play status after one hole is complete for both players in a matchup.
/// StatusAfterHole is from Player1's perspective: positive = Player1 up N, negative =
/// Player2 up N, 0 = all square. Null once the match is already decided (closed out
/// before hole 18) or the hole hasn't been played by both players yet. Used for both
/// the strokes-based match (hole winner = fewer net strokes) and the Stableford
/// points match (hole winner = more net Stableford points) — Player1/2NetStrokes
/// hold net strokes in the former and net Stableford points in the latter.
/// </summary>
public sealed record MatchPlayHoleDto(
    int HoleNumber,
    int? Player1NetStrokes,
    int? Player2NetStrokes,
    int? StatusAfterHole,
    bool IsConceded);

/// <summary>
/// Score is the player's current total over the holes entered so far (not
/// just a finished card). HolesCompleted / IsFinished drive the leaderboard's
/// "Thru" column (a hole count, or "F" once every hole of the round is in).
/// ToPar is Score relative to par for the holes played (stroke rankings only;
/// null for Stableford).
/// </summary>
public sealed record TournamentRankingEntryDto(
    int Rank,
    int PlayerId,
    string PlayerName,
    double HandicapIndex,
    int CourseHandicap,
    int? Score,
    bool IsTied,
    int HolesCompleted,
    bool IsFinished,
    int? ToPar);

public sealed record LongestDriveWinnerDto(int TournamentFlightId, string FlightName, int? PlayerId, string? PlayerName);

/// <summary>One player's score on one hole, for the flight scorecard grid. HandicapStrokes is the
/// standard "dots" notation — the number of strokes this player receives on this hole for net purposes.
/// Gross/NetStablefordPoints are null until a score is posted for the hole (0 points still displays
/// as a real value — these are null only when there's no HoleScore row yet).</summary>
public sealed record TournamentFlightHoleScoreDto(
    int HoleNumber,
    int? GrossStrokes,
    int? NetStrokes,
    int HandicapStrokes,
    int? GrossStablefordPoints,
    int? NetStablefordPoints);

/// <summary>Totals are running totals over the holes entered so far (null until a hole is scored).</summary>
public sealed record TournamentFlightPlayerDto(
    int PlayerId,
    string PlayerName,
    int CourseHandicap,
    List<TournamentFlightHoleScoreDto> HoleScores,
    int? TotalGrossStrokes,
    int? TotalNetStrokes,
    int? TotalGrossStablefordPoints,
    int? TotalNetStablefordPoints,
    int HolesCompleted,
    bool IsFinished);

public sealed record TournamentFlightDto(int Id, int FlightNumber, string Name, List<int> PlayerIds, List<TournamentFlightPlayerDto> Players);

public sealed record TournamentCourseHoleDto(int HoleNumber, int Par, int StrokeIndex);

public sealed record TournamentResultsDto(
    int RoundId,
    string RoundDate,
    string CourseName,
    int CourseId,
    List<TournamentCourseHoleDto> Holes,
    TournamentSkinsResultDto GrossSkins,
    TournamentSkinsResultDto NetSkins,
    List<TournamentHoleExtraDto> HoleExtras,
    int? LongestDriveHoleNumber,
    List<LongestDriveWinnerDto> LongestDriveWinners,
    List<TournamentFlightDto> Flights,
    List<TournamentMatchupResultDto> MatchupResults,
    List<TournamentRankingEntryDto> GrossStrokeRanking,
    List<TournamentRankingEntryDto> NetStrokeRanking,
    List<TournamentRankingEntryDto> GrossStablefordRanking,
    List<TournamentRankingEntryDto> NetStablefordRanking);

// ── Query ──────────────────────────────────────────────────────────────────────

public sealed record GetTournamentResultsQuery(int RoundId) : IRequest<Result<TournamentResultsDto>>;

public sealed class GetTournamentResultsQueryHandler : IRequestHandler<GetTournamentResultsQuery, Result<TournamentResultsDto>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly ITeeTimeRepository _teeTimeRepository;

    public GetTournamentResultsQueryHandler(
        IRoundRepository roundRepository,
        ICourseRepository courseRepository,
        ITeeTimeRepository teeTimeRepository)
    {
        _roundRepository = roundRepository;
        _courseRepository = courseRepository;
        _teeTimeRepository = teeTimeRepository;
    }

    public async Task<Result<TournamentResultsDto>> Handle(GetTournamentResultsQuery request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<TournamentResultsDto>.Fail($"Round {request.RoundId} not found.");
        if (round.RoundType != RoundType.Tournament)
            return Result<TournamentResultsDto>.Fail("This round is not a tournament round.");

        var course = await _courseRepository.GetByIdAsync(round.CourseId, cancellationToken);
        var courseHoles = await _courseRepository.GetHolesAsync(round.CourseId, cancellationToken);
        var holeDtos = courseHoles
            .OrderBy(h => h.HoleNumber)
            .Select(h => new TournamentCourseHoleDto(h.HoleNumber, h.Par, h.StrokeIndex))
            .ToList();
        var participants = await _roundRepository.GetParticipantsAsync(request.RoundId, cancellationToken);
        var matchups = await _roundRepository.GetTournamentMatchupsAsync(request.RoundId, cancellationToken);
        var holeExtras = await _roundRepository.GetTournamentHoleExtrasAsync(request.RoundId, cancellationToken);
        var flights = await _roundRepository.GetTournamentFlightsAsync(request.RoundId, cancellationToken);
        var ldWinners = await _roundRepository.GetLongestDriveWinnersAsync(request.RoundId, cancellationToken);

        var active = participants
            .Where(p => !p.IsWithdrawn && !p.SkippedWeek && !p.IsSubstitute && p.HoleScores.Any())
            .ToList();

        // Substitutes are left out of skins unless the admin made them eligible
        // for this tournament; they never count toward rankings either way.
        var skinsField = round.SubstitutesCanWinSkins
            ? participants.Where(p => !p.IsWithdrawn && !p.SkippedWeek && p.HoleScores.Any()).ToList()
            : active;
        // Course Handicap is known at tee-off regardless of scoring, so look matchup
        // players up in the full roster (not `active`, which requires a submitted
        // score) — otherwise CH shows as 0 for every matchup until scores start.
        //
        // Match play always spans every hole of the round, not just the holes
        // scored so far, so status/closeout reflect the holes still to play.
        var roundHoleNumbers = courseHoles
            .Select(h => h.HoleNumber)
            .Where(n => round.NineHoleSide switch
            {
                NineHoleSide.Front => n <= 9,
                NineHoleSide.Back => n >= 10,
                _ => true,
            })
            .OrderBy(n => n)
            .ToList();
        if (roundHoleNumbers.Count == 0)
        {
            // No hole layout on file for the course — fall back to whatever has been scored.
            roundHoleNumbers = participants.SelectMany(p => p.HoleScores).Select(h => h.HoleNumber).Distinct().OrderBy(n => n).ToList();
        }
        var teeTimes = await _teeTimeRepository.GetByRoundAsync(request.RoundId, cancellationToken);
        var startingHoleByTeeTime = teeTimes.ToDictionary(t => t.Id, t => t.StartingHoleNumber);
        var matchupResults = CalculateMatchupResults(matchups, participants.ToList(), roundHoleNumbers, startingHoleByTeeTime);
        var extraDtos = holeExtras.Select(e => new TournamentHoleExtraDto(
            e.HoleNumber,
            e.ClosestToPinPlayerId,
            e.ClosestToPinPlayer?.FullName,
            e.LongestDrivePlayerId,
            e.LongestDrivePlayer?.FullName)).ToList();

        var roundHoleCount = roundHoleNumbers.Count;
        var grossSkins = CalculateSkins(skinsField, useNet: false, round.GrossSkinsPool, roundHoleNumbers);
        var netSkins = CalculateSkins(skinsField, useNet: true, round.NetSkinsPool, roundHoleNumbers);
        var grossStrokeRanking = BuildRanking(active, h => h.GrossStrokes, ascending: true, rankByPar: true, roundHoleCount);
        var netStrokeRanking = BuildRanking(active, h => h.NetStrokes, ascending: true, rankByPar: true, roundHoleCount);
        var grossStablefordRanking = BuildRanking(active, h => h.GrossStablefordPoints, ascending: false, rankByPar: false, roundHoleCount);
        var netStablefordRanking = BuildRanking(active, h => h.NetStablefordPoints, ascending: false, rankByPar: false, roundHoleCount);

        // Totals are summed from the hole scores rather than read from the
        // participant's stored totals, which group score entry only fills in
        // once a card is complete — the leaderboard shows the round as it stands.
        TournamentFlightPlayerDto ToFlightPlayerDto(RoundParticipant p)
        {
            var hs = p.HoleScores;
            var any = hs.Count > 0;
            return new(
                p.PlayerId,
                p.Player.FullName,
                p.CourseHandicap,
                hs.OrderBy(h => h.HoleNumber)
                    .Select(h => new TournamentFlightHoleScoreDto(
                        h.HoleNumber, h.GrossStrokes, h.NetStrokes, h.HandicapStrokes, h.GrossStablefordPoints, h.NetStablefordPoints))
                    .ToList(),
                any ? hs.Sum(h => h.GrossStrokes) : null,
                any ? hs.Sum(h => h.NetStrokes) : null,
                any ? hs.Sum(h => h.GrossStablefordPoints) : null,
                any ? hs.Sum(h => h.NetStablefordPoints) : null,
                hs.Count,
                roundHoleCount > 0 && hs.Count >= roundHoleCount);
        }

        var flightDtos = flights
            .Select(f =>
            {
                var flightParticipants = participants.Where(p => p.TournamentFlightId == f.Id).ToList();
                return new TournamentFlightDto(
                    f.Id,
                    f.FlightNumber,
                    f.Name,
                    flightParticipants.Select(p => p.PlayerId).ToList(),
                    flightParticipants.Select(ToFlightPlayerDto).ToList());
            })
            .ToList();

        // Substitutes aren't part of the handicap-based flight grouping (see
        // TournamentFoursomeService) — they're never assigned a
        // TournamentFlightId. Show them in one extra, unflighted group at the
        // end of the list rather than dropping them from this view entirely.
        var substituteParticipants = participants.Where(p => p.IsSubstitute && !p.IsWithdrawn).ToList();
        if (substituteParticipants.Count > 0)
        {
            flightDtos.Add(new TournamentFlightDto(
                0,
                flights.Count + 1,
                "Substitutes",
                substituteParticipants.Select(p => p.PlayerId).ToList(),
                substituteParticipants.Select(ToFlightPlayerDto).ToList()));
        }

        var ldByFlight = ldWinners.ToDictionary(w => w.TournamentFlightId);
        var ldWinnerDtos = flights
            .Select(f =>
            {
                ldByFlight.TryGetValue(f.Id, out var winner);
                return new LongestDriveWinnerDto(f.Id, f.Name, winner?.PlayerId, winner?.Player.FullName);
            })
            .ToList();

        var result = new TournamentResultsDto(
            round.Id,
            round.RoundDate.ToString("yyyy-MM-dd"),
            course?.Name ?? "Unknown Course",
            round.CourseId,
            holeDtos,
            grossSkins,
            netSkins,
            extraDtos,
            round.LongestDriveHoleNumber,
            ldWinnerDtos,
            flightDtos,
            matchupResults,
            grossStrokeRanking,
            netStrokeRanking,
            grossStablefordRanking,
            netStablefordRanking);

        return Result<TournamentResultsDto>.Ok(result);
    }

    /// <summary>
    /// Skins over the field, hole by hole in hole-number order. A tie for the low
    /// score carries the skin to the next hole. Once every hole of the round has
    /// been scored, carryover still unclaimed after the last hole wraps around
    /// (18 → 1 for an 18-hole round) and goes to the first hole that had an
    /// outright winner. The pool is split evenly per skin point won, so a hole
    /// worth 3 (1 + 2 carried) pays three times a plain skin.
    /// </summary>
    private static TournamentSkinsResultDto CalculateSkins(
        List<RoundParticipant> participants,
        bool useNet,
        decimal? poolAmount,
        IReadOnlyList<int> roundHoleNumbers)
    {
        var skinType = useNet ? "Net" : "Gross";
        var holeNumbers = participants
            .SelectMany(p => p.HoleScores)
            .Select(h => h.HoleNumber)
            .Distinct()
            .OrderBy(h => h)
            .ToList();

        var holeResults = new List<TournamentSkinHoleDto>();
        int carryover = 0;

        foreach (var holeNumber in holeNumbers)
        {
            var scores = participants
                .Select(p => new
                {
                    p.PlayerId,
                    p.Player.FullName,
                    HoleScore = p.HoleScores.FirstOrDefault(h => h.HoleNumber == holeNumber)
                })
                .Where(x => x.HoleScore != null)
                .Select(x => new
                {
                    x.PlayerId,
                    x.FullName,
                    Par = x.HoleScore!.Par,
                    Score = useNet ? x.HoleScore!.NetStrokes : x.HoleScore!.GrossStrokes
                })
                .ToList();

            if (scores.Count == 0) continue;

            var minScore = scores.Min(s => s.Score);
            var lowest = scores.Where(s => s.Score == minScore).ToList();
            var par = scores[0].Par;

            if (lowest.Count == 1)
            {
                var winner = lowest[0];
                holeResults.Add(new TournamentSkinHoleDto(holeNumber, par, 1 + carryover, winner.PlayerId, winner.FullName, minScore, carryover > 0, false));
                carryover = 0;
            }
            else
            {
                holeResults.Add(new TournamentSkinHoleDto(holeNumber, par, 0, null, null, minScore, false, true));
                carryover += 1;
            }
        }

        // Wrap a trailing carryover back to the start of the round, but only once
        // the whole round is in — mid-round, the next hole may still decide it.
        var roundComplete = roundHoleNumbers.Count > 0 && roundHoleNumbers.All(holeNumbers.Contains);
        if (carryover > 0 && roundComplete)
        {
            var firstWonIndex = holeResults.FindIndex(h => !h.IsTie);
            if (firstWonIndex >= 0)
            {
                var first = holeResults[firstWonIndex];
                holeResults[firstWonIndex] = first with { SkinValue = first.SkinValue + carryover, WasCarryover = true };
            }
        }

        var playerAccumulators = participants.ToDictionary(
            p => p.PlayerId,
            p => new PlayerSkinAccumulator(p.PlayerId, p.Player.FullName));
        foreach (var hole in holeResults.Where(h => h.WinnerPlayerId is not null))
            playerAccumulators[hole.WinnerPlayerId!.Value].Add(hole);

        var totalSkinPoints = playerAccumulators.Values.Sum(a => a.TotalSkinValue);
        var perSkinPayout = poolAmount is decimal pool && totalSkinPoints > 0
            ? pool / totalSkinPoints
            : (decimal?)null;

        var playerSummaries = playerAccumulators.Values
            .Where(a => a.TotalSkinsWon > 0)
            .OrderByDescending(a => a.TotalSkinValue)
            .ThenByDescending(a => a.TotalSkinsWon)
            .Select(a => new TournamentPlayerSkinDto(
                a.PlayerId, a.PlayerName, a.TotalSkinsWon, a.TotalSkinValue, a.HolesWon,
                perSkinPayout is decimal perPoint ? perPoint * a.TotalSkinValue : null))
            .ToList();

        return new TournamentSkinsResultDto(skinType, holeResults, playerSummaries, poolAmount, perSkinPayout);
    }

    private static List<TournamentMatchupResultDto> CalculateMatchupResults(
        IReadOnlyList<TournamentMatchup> matchups,
        List<RoundParticipant> participants,
        IReadOnlyList<int> roundHoleNumbers,
        IReadOnlyDictionary<int, int?> startingHoleByTeeTime)
    {
        var participantLookup = participants.ToDictionary(p => p.PlayerId);
        var results = new List<TournamentMatchupResultDto>();

        foreach (var matchup in matchups.OrderBy(m => m.MatchupNumber))
        {
            participantLookup.TryGetValue(matchup.Player1Id, out var p1);
            RoundParticipant? p2 = null;
            if (matchup.Player2Id is int player2Id)
                participantLookup.TryGetValue(player2Id, out p2);

            // Bye — no opponent. Always counts as a win for Player1, regardless
            // of whether they've posted a score yet.
            if (matchup.Player2Id is null)
            {
                results.Add(new TournamentMatchupResultDto(
                    matchup.MatchupNumber,
                    matchup.Player1Id,
                    matchup.Player1.FullName,
                    p1?.HandicapIndex ?? 0,
                    p1?.CourseHandicap ?? 0,
                    p1?.TotalNetStrokes,
                    p1?.TotalNetStablefordPoints,
                    null, null, null, null, null, null,
                    matchup.Player1Id,
                    matchup.Player1.FullName,
                    false,
                    [],
                    matchup.Player1Id,
                    matchup.Player1.FullName,
                    false,
                    []));
                continue;
            }

            var p1Net = p1?.TotalNetStrokes;
            var p2Net = p2?.TotalNetStrokes;
            var p1Points = p1?.TotalNetStablefordPoints;
            var p2Points = p2?.TotalNetStablefordPoints;

            // The overall result is only decided once both cards cover every
            // hole of the round — admin entry can save running totals from a
            // partial card, which would otherwise crown a winner mid-round.
            var bothComplete = IsCardComplete(p1, roundHoleNumbers) && IsCardComplete(p2, roundHoleNumbers);

            int? winnerId = null;
            string? winnerName = null;
            bool halved = false;

            if (bothComplete && p1Net.HasValue && p2Net.HasValue)
            {
                if (p1Net < p2Net)
                {
                    winnerId = matchup.Player1Id;
                    winnerName = matchup.Player1.FullName;
                }
                else if (p2Net < p1Net)
                {
                    winnerId = matchup.Player2Id;
                    winnerName = matchup.Player2?.FullName;
                }
                else
                {
                    halved = true;
                    winnerId = 0;
                }
            }

            // Stableford points match: same matchup, but each hole (and the overall
            // result) is decided by who scores MORE net Stableford points rather than
            // who takes FEWER net strokes.
            int? winnerIdByPoints = null;
            string? winnerNameByPoints = null;
            bool halvedByPoints = false;

            if (bothComplete && p1Points.HasValue && p2Points.HasValue)
            {
                if (p1Points > p2Points)
                {
                    winnerIdByPoints = matchup.Player1Id;
                    winnerNameByPoints = matchup.Player1.FullName;
                }
                else if (p2Points > p1Points)
                {
                    winnerIdByPoints = matchup.Player2Id;
                    winnerNameByPoints = matchup.Player2?.FullName;
                }
                else
                {
                    halvedByPoints = true;
                    winnerIdByPoints = 0;
                }
            }

            // Matchup partners share a tee time, so either player's group
            // start gives the play order (relevant for shotgun starts).
            var startingHole = StartingHoleFor(p1, startingHoleByTeeTime) ?? StartingHoleFor(p2, startingHoleByTeeTime);
            var playOrder = PlayOrder(roundHoleNumbers, startingHole);
            var holeByHole = BuildMatchPlayHoles(p1, p2, playOrder, byPoints: false);
            var holeByHolePoints = BuildMatchPlayHoles(p1, p2, playOrder, byPoints: true);

            results.Add(new TournamentMatchupResultDto(
                matchup.MatchupNumber,
                matchup.Player1Id,
                matchup.Player1.FullName,
                p1?.HandicapIndex ?? 0,
                p1?.CourseHandicap ?? 0,
                p1Net,
                p1Points,
                matchup.Player2Id,
                matchup.Player2?.FullName,
                p2?.HandicapIndex ?? 0,
                p2?.CourseHandicap ?? 0,
                p2Net,
                p2Points,
                winnerId,
                winnerName,
                halved,
                holeByHole,
                winnerIdByPoints,
                winnerNameByPoints,
                halvedByPoints,
                holeByHolePoints));
        }

        return results;
    }

    private static bool IsCardComplete(RoundParticipant? p, IReadOnlyList<int> roundHoleNumbers) =>
        p is not null
        && roundHoleNumbers.All(n => p.HoleScores.Any(h => h.HoleNumber == n));

    private static int? StartingHoleFor(RoundParticipant? p, IReadOnlyDictionary<int, int?> startingHoleByTeeTime) =>
        p?.TeeTimeId is int teeTimeId && startingHoleByTeeTime.TryGetValue(teeTimeId, out var start) ? start : (int?)null;

    /// <summary>The round's holes rotated to begin at the group's starting hole (shotgun), else in hole order.</summary>
    private static List<int> PlayOrder(IReadOnlyList<int> roundHoleNumbers, int? startingHole)
    {
        var startIdx = startingHole is int s ? roundHoleNumbers.ToList().IndexOf(s) : -1;
        if (startIdx <= 0)
            return roundHoleNumbers.ToList();
        return roundHoleNumbers.Skip(startIdx).Concat(roundHoleNumbers.Take(startIdx)).ToList();
    }

    /// <summary>
    /// Hole-by-hole match-play status (1-up style) for a matchup, always spanning
    /// every hole of the round in play order — holes not yet scored by both players
    /// are included with a null status. Each hole where both players have posted a
    /// net score contributes to a running tally from Player1's perspective. Once a
    /// player is mathematically closed out (up by more holes than remain unplayed in
    /// the round), later holes stop updating status (IsConceded = true) even if both
    /// players later post scores for them, matching the "3&amp;2"-style stop convention.
    /// Strokes mode (byPoints: false) awards the hole to whoever has fewer net strokes;
    /// Stableford points mode (byPoints: true) awards it to whoever has more net points.
    /// </summary>
    private static List<MatchPlayHoleDto> BuildMatchPlayHoles(
        RoundParticipant? p1,
        RoundParticipant? p2,
        IReadOnlyList<int> playOrder,
        bool byPoints)
    {
        if (p1 is null || p2 is null) return [];

        var result = new List<MatchPlayHoleDto>();
        int status = 0;
        int holesCompleted = 0;
        bool closedOut = false;

        foreach (var holeNumber in playOrder)
        {
            var h1 = p1.HoleScores.FirstOrDefault(h => h.HoleNumber == holeNumber);
            var h2 = p2.HoleScores.FirstOrDefault(h => h.HoleNumber == holeNumber);
            var h1Value = byPoints ? h1?.NetStablefordPoints : h1?.NetStrokes;
            var h2Value = byPoints ? h2?.NetStablefordPoints : h2?.NetStrokes;

            if (closedOut)
            {
                result.Add(new MatchPlayHoleDto(holeNumber, h1Value, h2Value, null, true));
                continue;
            }

            if (h1Value is not null && h2Value is not null)
            {
                var p1WinsHole = byPoints ? h1Value > h2Value : h1Value < h2Value;
                var p2WinsHole = byPoints ? h2Value > h1Value : h2Value < h1Value;
                if (p1WinsHole) status += 1;
                else if (p2WinsHole) status -= 1;

                holesCompleted++;
                var holesRemaining = playOrder.Count - holesCompleted;
                if (Math.Abs(status) > holesRemaining)
                    closedOut = true;
            }

            result.Add(new MatchPlayHoleDto(
                holeNumber,
                h1Value,
                h2Value,
                h1Value is not null && h2Value is not null ? status : null,
                false));
        }

        return result;
    }

    /// <summary>
    /// Ranks players on their current total over the holes entered so far.
    /// Stroke rankings (rankByPar) order by score relative to par for the holes
    /// played, so a player thru 3 isn't ranked ahead of one thru 9 just for
    /// having fewer strokes on the card; ties break toward more holes completed.
    /// Stableford rankings order by points as-is (points are already relative to par).
    /// </summary>
    private static List<TournamentRankingEntryDto> BuildRanking(
        List<RoundParticipant> participants,
        Func<HoleScore, int> holeValue,
        bool ascending,
        bool rankByPar,
        int roundHoleCount)
    {
        var withScores = participants
            .Where(p => p.HoleScores.Count > 0)
            .Select(p =>
            {
                var score = p.HoleScores.Sum(holeValue);
                var sortKey = rankByPar ? score - p.HoleScores.Sum(h => h.Par) : score;
                return new { Participant = p, Score = score, SortKey = sortKey, Holes = p.HoleScores.Count };
            })
            .ToList();

        var ordered = (ascending
                ? withScores.OrderBy(x => x.SortKey)
                : withScores.OrderByDescending(x => x.SortKey))
            .ThenByDescending(x => x.Holes)
            .ToList();

        var rankings = new List<TournamentRankingEntryDto>();
        int rank = 1;
        for (int i = 0; i < ordered.Count; i++)
        {
            // Tied entries share the previous rank.
            if (i == 0 || ordered[i].SortKey != ordered[i - 1].SortKey)
                rank = i + 1;

            bool isTied = (i > 0 && ordered[i].SortKey == ordered[i - 1].SortKey)
                       || (i < ordered.Count - 1 && ordered[i].SortKey == ordered[i + 1].SortKey);

            rankings.Add(new TournamentRankingEntryDto(
                rank,
                ordered[i].Participant.PlayerId,
                ordered[i].Participant.Player.FullName,
                ordered[i].Participant.HandicapIndex,
                ordered[i].Participant.CourseHandicap,
                ordered[i].Score,
                isTied,
                ordered[i].Holes,
                roundHoleCount > 0 && ordered[i].Holes >= roundHoleCount,
                rankByPar ? ordered[i].SortKey : null));
        }

        return rankings;
    }

    private sealed class PlayerSkinAccumulator
    {
        public int PlayerId { get; }
        public string PlayerName { get; }
        public int TotalSkinsWon { get; private set; }
        public int TotalSkinValue { get; private set; }
        public List<TournamentSkinHoleDto> HolesWon { get; } = [];

        public PlayerSkinAccumulator(int playerId, string playerName)
        {
            PlayerId = playerId;
            PlayerName = playerName;
        }

        public void Add(TournamentSkinHoleDto skin)
        {
            TotalSkinsWon++;
            TotalSkinValue += skin.SkinValue;
            HolesWon.Add(skin);
        }
    }
}
