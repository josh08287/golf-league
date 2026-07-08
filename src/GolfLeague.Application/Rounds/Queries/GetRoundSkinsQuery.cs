using GolfLeague.Application.Common;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using MediatR;
using static GolfLeague.Application.Common.FlightDisplayName;

namespace GolfLeague.Application.Rounds.Queries;

/// <summary>
/// Represents a skin won by a player on a specific hole.
/// Skins carry over when there's a tie for lowest net score.
/// </summary>
public sealed record HoleSkinDto(
    int HoleNumber,
    int SkinValue,
    int WinnerPlayerId,
    string WinnerPlayerName,
    int WinningNetScore,
    bool WasCarryover);

/// <summary>
/// Skin summary for a single player in a flight.
/// </summary>
public sealed record PlayerSkinSummaryDto(
    int PlayerId,
    string PlayerName,
    int TotalSkinsWon,
    int TotalSkinValue,
    List<HoleSkinDto> HolesWon);

/// <summary>
/// Skins result for a single flight.
/// </summary>
public sealed record FlightSkinsDto(
    int FlightId,
    string FlightName,
    int TotalHolesWithSkins,
    int TotalSkinValueAwarded,
    List<PlayerSkinSummaryDto> PlayerSummaries,
    List<HoleSkinDto> AllHoleResults);

/// <summary>
/// Represents a gross skin won on a par 3 hole (all flights combined).
/// </summary>
public sealed record GrossPar3SkinDto(
    int HoleNumber,
    int Par,
    int SkinValue,
    int WinnerPlayerId,
    string WinnerPlayerName,
    int? WinnerFlightId,
    string WinnerFlightName,
    int WinningGrossScore,
    bool WasCarryover);

/// <summary>
/// Summary of gross par 3 skins for the entire round.
/// </summary>
public sealed record GrossPar3SkinsSummaryDto(
    int TotalHolesWithSkins,
    int TotalSkinValueAwarded,
    int Par3HoleCount,
    int IncomingCarryover,
    List<GrossPar3SkinDto> HoleResults,
    List<PlayerSkinSummaryDto> PlayerSummaries);

/// <summary>
/// Complete skins result for a round, grouped by flight.
/// </summary>
public sealed record RoundSkinsDto(
    int RoundId,
    string RoundDate,
    string CourseName,
    List<FlightSkinsDto> FlightSkins,
    GrossPar3SkinsSummaryDto? GrossPar3Skins);

public sealed record GetRoundSkinsQuery(int RoundId) : IRequest<Result<RoundSkinsDto>>;

public sealed class GetRoundSkinsQueryHandler : IRequestHandler<GetRoundSkinsQuery, Result<RoundSkinsDto>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IFlightRepository _flightRepository;

    public GetRoundSkinsQueryHandler(
        IRoundRepository roundRepository,
        ICourseRepository courseRepository,
        IFlightRepository flightRepository)
    {
        _roundRepository = roundRepository;
        _courseRepository = courseRepository;
        _flightRepository = flightRepository;
    }

    public async Task<Result<RoundSkinsDto>> Handle(GetRoundSkinsQuery request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<RoundSkinsDto>.Fail($"Round with ID {request.RoundId} not found.");

        var participants = await _roundRepository.GetParticipantsAsync(request.RoundId, cancellationToken);
        if (participants.Count == 0)
            return Result<RoundSkinsDto>.Fail("No participants found for this round.");

        var course = await _courseRepository.GetByIdAsync(round.CourseId, cancellationToken);
        var flights = round.HalfId.HasValue
            ? await _flightRepository.GetByHalfAsync(round.HalfId.Value, cancellationToken)
            : [];

        // Group participants by flight
        var participantsByFlight = participants
            .Where(p => !p.IsWithdrawn && !p.SkippedWeek && p.HoleScores.Any() && p.FlightId.HasValue)
            .GroupBy(p => p.FlightId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var flightSkinsList = new List<FlightSkinsDto>();

        foreach (var flight in flights.OrderBy(f => f.DisplayOrder))
        {
            if (!participantsByFlight.TryGetValue(flight.Id, out var flightParticipants))
                continue;

            var flightSkins = CalculateFlightSkins(flight, flightParticipants);
            flightSkinsList.Add(flightSkins);
        }

        // Build flight name lookup for par-3 skins
        var flightNameLookup = flights.ToDictionary(f => f.Id, f => Format(f));

        // Calculate gross par-3 skins across all flights
        var allParticipants = participants
            .Where(p => !p.IsWithdrawn && !p.SkippedWeek && p.HoleScores.Any())
            .ToList();

        // Check for carryover from previous round
        int incomingCarryover = await CalculateIncomingPar3CarryoverAsync(round, flightNameLookup, cancellationToken);

        var grossPar3Skins = CalculateGrossPar3Skins(allParticipants, flightNameLookup, incomingCarryover);

        var result = new RoundSkinsDto(
            round.Id,
            round.RoundDate.ToString("yyyy-MM-dd"),
            course?.Name ?? "Unknown Course",
            flightSkinsList,
            grossPar3Skins);

        return Result<RoundSkinsDto>.Ok(result);
    }

    private async Task<int> CalculateIncomingPar3CarryoverAsync(Round round, Dictionary<int, string> flightNameLookup, CancellationToken cancellationToken)
    {
        // Walk every prior round in the season (chronological order) and feed each round's
        // ending carryover into the next, so unresolved par-3 ties accumulate across rounds.
        // Scoped to the season (not the half) — carryover only resets on a new season.
        var previousRounds = await _roundRepository.GetPreviousRoundsAsync(round.SeasonId, round.RoundDate, cancellationToken);

        int carryover = 0;
        foreach (var prev in previousRounds)
        {
            var prevParticipants = prev.Participants
                .Where(p => !p.IsWithdrawn && !p.SkippedWeek && p.HoleScores.Any())
                .ToList();

            if (prevParticipants.Count == 0)
                continue;

            var prevSkins = CalculateGrossPar3Skins(prevParticipants, flightNameLookup, carryover);
            if (prevSkins is null)
                continue;

            carryover = ComputeEndingCarryover(prevSkins.HoleResults, carryover);
        }

        return carryover;
    }

    private static int ComputeEndingCarryover(List<GrossPar3SkinDto> holeResults, int incomingCarryover)
    {
        // Replay the per-hole outcomes to find what's still carrying after the last par 3.
        // A win clears the running carryover; a tie adds 1.
        int running = incomingCarryover;
        foreach (var hole in holeResults)
        {
            if (hole.SkinValue > 0)
                running = 0;
            else
                running += 1;
        }
        return running;
    }

    private static FlightSkinsDto CalculateFlightSkins(Flight flight, List<RoundParticipant> participants)
    {
        // Get all hole numbers played (typically 1-9 for nine-hole rounds)
        var holeNumbers = participants
            .SelectMany(p => p.HoleScores)
            .Select(h => h.HoleNumber)
            .Distinct()
            .OrderBy(h => h)
            .ToList();

        var allHoleResults = new List<HoleSkinDto>();
        var playerSkinCounts = participants.ToDictionary(
            p => p.PlayerId,
            p => new PlayerSkinAccumulator(p.PlayerId, p.Player.FullName));

        int carryoverSkins = 0;

        foreach (var holeNumber in holeNumbers)
        {
            // Get all net scores for this hole from participants who have a score
            var holeScores = participants
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
                    x.HoleScore!.NetStrokes,
                    x.HoleScore!.GrossStrokes
                })
                .ToList();

            if (holeScores.Count == 0)
                continue;

            // Find the lowest net score
            var minNetScore = holeScores.Min(h => h.NetStrokes);
            var lowestScorers = holeScores.Where(h => h.NetStrokes == minNetScore).ToList();

            // Calculate skin value for this hole (1 + any carryover)
            int skinValue = 1 + carryoverSkins;

            if (lowestScorers.Count == 1)
            {
                // Single winner - award the skin(s)
                var winner = lowestScorers[0];
                var holeSkin = new HoleSkinDto(
                    holeNumber,
                    skinValue,
                    winner.PlayerId,
                    winner.FullName,
                    winner.NetStrokes,
                    carryoverSkins > 0);

                allHoleResults.Add(holeSkin);
                playerSkinCounts[winner.PlayerId].AddSkin(holeSkin);

                // Reset carryover
                carryoverSkins = 0;
            }
            else
            {
                // Tie - skin carries over to next hole
                carryoverSkins += 1;

                // Record that this hole had a tie (no winner)
                allHoleResults.Add(new HoleSkinDto(
                    holeNumber,
                    0, // No skins awarded
                    0, // No winner
                    string.Empty,
                    minNetScore,
                    false));
            }
        }

        // Any remaining carryover skins are lost (not awarded)

        // Build player summaries
        var playerSummaries = playerSkinCounts
            .Select(x => x.Value)
            .Where(p => p.TotalSkinsWon > 0)
            .OrderByDescending(p => p.TotalSkinValue)
            .ThenByDescending(p => p.TotalSkinsWon)
            .Select(p => new PlayerSkinSummaryDto(
                p.PlayerId,
                p.PlayerName,
                p.TotalSkinsWon,
                p.TotalSkinValue,
                p.HolesWon))
            .ToList();

        var totalHolesWithSkins = allHoleResults.Count(h => h.SkinValue > 0);
        var totalSkinValueAwarded = playerSummaries.Sum(p => p.TotalSkinValue);

        return new FlightSkinsDto(
            flight.Id,
            Format(flight),
            totalHolesWithSkins,
            totalSkinValueAwarded,
            playerSummaries,
            allHoleResults);
    }

    private static GrossPar3SkinsSummaryDto? CalculateGrossPar3Skins(
        List<RoundParticipant> allParticipants,
        Dictionary<int, string> flightNameLookup,
        int incomingCarryover = 0)
    {
        // Get all par 3 hole scores across all participants
        var par3HoleScores = allParticipants
            .SelectMany(p => p.HoleScores.Where(h => h.Par == 3).Select(h => new
            {
                p.PlayerId,
                p.Player.FullName,
                p.FlightId,
                HoleScore = h
            }))
            .ToList();

        if (par3HoleScores.Count == 0)
            return null;

        // Group by hole number
        var holesPlayed = par3HoleScores
            .Select(x => x.HoleScore.HoleNumber)
            .Distinct()
            .OrderBy(h => h)
            .ToList();

        var holeResults = new List<GrossPar3SkinDto>();
        var playerSkinCounts = new Dictionary<int, PlayerSkinAccumulator>();

        // Start with any carryover from the previous round
        int carryoverSkins = incomingCarryover;

        foreach (var holeNumber in holesPlayed)
        {
            // Get all gross scores for this par 3 hole
            var holeScores = par3HoleScores
                .Where(x => x.HoleScore.HoleNumber == holeNumber)
                .Select(x => new
                {
                    x.PlayerId,
                    x.FullName,
                    x.FlightId,
                    x.HoleScore.GrossStrokes,
                    x.HoleScore.Par
                })
                .ToList();

            if (holeScores.Count == 0)
                continue;

            // Find the lowest gross score
            var minGrossScore = holeScores.Min(h => h.GrossStrokes);
            var lowestScorers = holeScores.Where(h => h.GrossStrokes == minGrossScore).ToList();

            int skinValue = 1 + carryoverSkins;

            if (lowestScorers.Count == 1)
            {
                var winner = lowestScorers[0];
                var flightName = winner.FlightId.HasValue && flightNameLookup.TryGetValue(winner.FlightId.Value, out var fn) ? fn : "Unknown";

                var holeSkin = new GrossPar3SkinDto(
                    holeNumber,
                    winner.Par,
                    skinValue,
                    winner.PlayerId,
                    winner.FullName,
                    winner.FlightId,
                    flightName,
                    winner.GrossStrokes,
                    carryoverSkins > 0);

                holeResults.Add(holeSkin);

                if (!playerSkinCounts.ContainsKey(winner.PlayerId))
                    playerSkinCounts[winner.PlayerId] = new PlayerSkinAccumulator(winner.PlayerId, winner.FullName);
                playerSkinCounts[winner.PlayerId].AddSkin(new HoleSkinDto(holeNumber, skinValue, winner.PlayerId, winner.FullName, winner.GrossStrokes, carryoverSkins > 0));

                carryoverSkins = 0;
            }
            else
            {
                carryoverSkins += 1;

                holeResults.Add(new GrossPar3SkinDto(
                    holeNumber,
                    3, // par 3
                    0,
                    0,
                    string.Empty,
                    0,
                    string.Empty,
                    minGrossScore,
                    false));
            }
        }

        var playerSummaries = playerSkinCounts
            .Select(x => x.Value)
            .Where(p => p.TotalSkinsWon > 0)
            .OrderByDescending(p => p.TotalSkinValue)
            .ThenByDescending(p => p.TotalSkinsWon)
            .Select(p => new PlayerSkinSummaryDto(
                p.PlayerId,
                p.PlayerName,
                p.TotalSkinsWon,
                p.TotalSkinValue,
                p.HolesWon))
            .ToList();

        var totalHolesWithSkins = holeResults.Count(h => h.SkinValue > 0);
        var totalSkinValueAwarded = playerSummaries.Sum(p => p.TotalSkinValue);

        return new GrossPar3SkinsSummaryDto(
            totalHolesWithSkins,
            totalSkinValueAwarded,
            holesPlayed.Count,
            incomingCarryover,
            holeResults,
            playerSummaries);
    }

    private class PlayerSkinAccumulator
    {
        public int PlayerId { get; }
        public string PlayerName { get; }
        public int TotalSkinsWon { get; private set; }
        public int TotalSkinValue { get; private set; }
        public List<HoleSkinDto> HolesWon { get; }

        public PlayerSkinAccumulator(int playerId, string playerName)
        {
            PlayerId = playerId;
            PlayerName = playerName;
            HolesWon = new List<HoleSkinDto>();
        }

        public void AddSkin(HoleSkinDto skin)
        {
            TotalSkinsWon++;
            TotalSkinValue += skin.SkinValue;
            HolesWon.Add(skin);
        }
    }
}
