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
    List<TournamentSkinHoleDto> HolesWon);

public sealed record TournamentSkinsResultDto(
    string SkinType,
    List<TournamentSkinHoleDto> HoleResults,
    List<TournamentPlayerSkinDto> PlayerSummaries);

public sealed record TournamentMatchupResultDto(
    int MatchupNumber,
    int Player1Id,
    string Player1Name,
    double Player1HandicapIndex,
    int Player1CourseHandicap,
    int? Player1NetStrokes,
    int? Player1NetPoints,
    int Player2Id,
    string Player2Name,
    double Player2HandicapIndex,
    int Player2CourseHandicap,
    int? Player2NetStrokes,
    int? Player2NetPoints,
    int? WinnerPlayerId,
    string? WinnerPlayerName,
    bool IsHalved);

public sealed record TournamentRankingEntryDto(
    int Rank,
    int PlayerId,
    string PlayerName,
    double HandicapIndex,
    int CourseHandicap,
    int? Score,
    bool IsTied);

public sealed record LongestDriveWinnerDto(int TournamentFlightId, string FlightName, int? PlayerId, string? PlayerName);

public sealed record TournamentFlightDto(int Id, int FlightNumber, string Name, List<int> PlayerIds);

public sealed record TournamentResultsDto(
    int RoundId,
    string RoundDate,
    string CourseName,
    int CourseId,
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

    public GetTournamentResultsQueryHandler(
        IRoundRepository roundRepository,
        ICourseRepository courseRepository)
    {
        _roundRepository = roundRepository;
        _courseRepository = courseRepository;
    }

    public async Task<Result<TournamentResultsDto>> Handle(GetTournamentResultsQuery request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<TournamentResultsDto>.Fail($"Round {request.RoundId} not found.");
        if (round.RoundType != RoundType.Tournament)
            return Result<TournamentResultsDto>.Fail("This round is not a tournament round.");

        var course = await _courseRepository.GetByIdAsync(round.CourseId, cancellationToken);
        var participants = await _roundRepository.GetParticipantsAsync(request.RoundId, cancellationToken);
        var matchups = await _roundRepository.GetTournamentMatchupsAsync(request.RoundId, cancellationToken);
        var holeExtras = await _roundRepository.GetTournamentHoleExtrasAsync(request.RoundId, cancellationToken);
        var flights = await _roundRepository.GetTournamentFlightsAsync(request.RoundId, cancellationToken);
        var ldWinners = await _roundRepository.GetLongestDriveWinnersAsync(request.RoundId, cancellationToken);

        var active = participants
            .Where(p => !p.IsWithdrawn && !p.SkippedWeek && !p.IsSubstitute && p.HoleScores.Any())
            .ToList();

        var grossSkins = CalculateSkins(active, useNet: false);
        var netSkins = CalculateSkins(active, useNet: true);
        var matchupResults = CalculateMatchupResults(matchups, active);
        var extraDtos = holeExtras.Select(e => new TournamentHoleExtraDto(
            e.HoleNumber,
            e.ClosestToPinPlayerId,
            e.ClosestToPinPlayer?.FullName,
            e.LongestDrivePlayerId,
            e.LongestDrivePlayer?.FullName)).ToList();

        var grossStrokeRanking = BuildRanking(active, p => p.TotalGrossStrokes, ascending: true);
        var netStrokeRanking = BuildRanking(active, p => p.TotalNetStrokes, ascending: true);
        var grossStablefordRanking = BuildRanking(active, p => p.TotalGrossStablefordPoints, ascending: false);
        var netStablefordRanking = BuildRanking(active, p => p.TotalNetStablefordPoints, ascending: false);

        var flightDtos = flights
            .Select(f => new TournamentFlightDto(
                f.Id,
                f.FlightNumber,
                f.Name,
                participants.Where(p => p.TournamentFlightId == f.Id).Select(p => p.PlayerId).ToList()))
            .ToList();

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

    private static TournamentSkinsResultDto CalculateSkins(List<RoundParticipant> participants, bool useNet)
    {
        var skinType = useNet ? "Net" : "Gross";
        var holeNumbers = participants
            .SelectMany(p => p.HoleScores)
            .Select(h => h.HoleNumber)
            .Distinct()
            .OrderBy(h => h)
            .ToList();

        var playerAccumulators = participants.ToDictionary(
            p => p.PlayerId,
            p => new PlayerSkinAccumulator(p.PlayerId, p.Player.FullName));

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
            int skinValue = 1 + carryover;
            var par = scores[0].Par;

            if (lowest.Count == 1)
            {
                var winner = lowest[0];
                var holeSkin = new TournamentSkinHoleDto(holeNumber, par, skinValue, winner.PlayerId, winner.FullName, minScore, carryover > 0, false);
                holeResults.Add(holeSkin);
                playerAccumulators[winner.PlayerId].Add(holeSkin);
                carryover = 0;
            }
            else
            {
                holeResults.Add(new TournamentSkinHoleDto(holeNumber, par, 0, null, null, minScore, false, true));
                carryover += 1;
            }
        }

        var playerSummaries = playerAccumulators.Values
            .Where(a => a.TotalSkinsWon > 0)
            .OrderByDescending(a => a.TotalSkinValue)
            .ThenByDescending(a => a.TotalSkinsWon)
            .Select(a => new TournamentPlayerSkinDto(a.PlayerId, a.PlayerName, a.TotalSkinsWon, a.TotalSkinValue, a.HolesWon))
            .ToList();

        return new TournamentSkinsResultDto(skinType, holeResults, playerSummaries);
    }

    private static List<TournamentMatchupResultDto> CalculateMatchupResults(
        IReadOnlyList<TournamentMatchup> matchups,
        List<RoundParticipant> participants)
    {
        var participantLookup = participants.ToDictionary(p => p.PlayerId);
        var results = new List<TournamentMatchupResultDto>();

        foreach (var matchup in matchups.OrderBy(m => m.MatchupNumber))
        {
            participantLookup.TryGetValue(matchup.Player1Id, out var p1);
            participantLookup.TryGetValue(matchup.Player2Id, out var p2);

            var p1Net = p1?.TotalNetStrokes;
            var p2Net = p2?.TotalNetStrokes;
            var p1Points = p1?.TotalNetStablefordPoints;
            var p2Points = p2?.TotalNetStablefordPoints;

            int? winnerId = null;
            string? winnerName = null;
            bool halved = false;

            if (p1Net.HasValue && p2Net.HasValue)
            {
                if (p1Net < p2Net)
                {
                    winnerId = matchup.Player1Id;
                    winnerName = matchup.Player1.FullName;
                }
                else if (p2Net < p1Net)
                {
                    winnerId = matchup.Player2Id;
                    winnerName = matchup.Player2.FullName;
                }
                else
                {
                    halved = true;
                    winnerId = 0;
                }
            }

            results.Add(new TournamentMatchupResultDto(
                matchup.MatchupNumber,
                matchup.Player1Id,
                matchup.Player1.FullName,
                p1?.HandicapIndex ?? 0,
                p1?.CourseHandicap ?? 0,
                p1Net,
                p1Points,
                matchup.Player2Id,
                matchup.Player2.FullName,
                p2?.HandicapIndex ?? 0,
                p2?.CourseHandicap ?? 0,
                p2Net,
                p2Points,
                winnerId,
                winnerName,
                halved));
        }

        return results;
    }

    private static List<TournamentRankingEntryDto> BuildRanking(
        List<RoundParticipant> participants,
        Func<RoundParticipant, int?> scoreSelector,
        bool ascending)
    {
        var withScores = participants
            .Where(p => scoreSelector(p).HasValue)
            .Select(p => new { Participant = p, Score = scoreSelector(p)!.Value })
            .ToList();

        var ordered = ascending
            ? withScores.OrderBy(x => x.Score).ToList()
            : withScores.OrderByDescending(x => x.Score).ToList();

        var rankings = new List<TournamentRankingEntryDto>();
        int rank = 1;
        for (int i = 0; i < ordered.Count; i++)
        {
            if (i > 0 && ordered[i].Score == ordered[i - 1].Score)
            {
                // Tied — use same rank as previous
            }
            else
            {
                rank = i + 1;
            }

            bool isTied = (i > 0 && ordered[i].Score == ordered[i - 1].Score)
                       || (i < ordered.Count - 1 && ordered[i].Score == ordered[i + 1].Score);

            rankings.Add(new TournamentRankingEntryDto(
                rank,
                ordered[i].Participant.PlayerId,
                ordered[i].Participant.Player.FullName,
                ordered[i].Participant.HandicapIndex,
                ordered[i].Participant.CourseHandicap,
                ordered[i].Score,
                isTied));
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
