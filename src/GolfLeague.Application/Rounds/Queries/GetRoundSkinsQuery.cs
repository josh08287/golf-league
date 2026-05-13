using GolfLeague.Application.Common;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using MediatR;

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
/// Complete skins result for a round, grouped by flight.
/// </summary>
public sealed record RoundSkinsDto(
    int RoundId,
    string RoundDate,
    string CourseName,
    List<FlightSkinsDto> FlightSkins);

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
        var flights = await _flightRepository.GetByHalfAsync(round.HalfId, cancellationToken);

        // Group participants by flight
        var participantsByFlight = participants
            .Where(p => !p.IsWithdrawn && !p.SkippedWeek && p.HoleScores.Any())
            .GroupBy(p => p.FlightId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var flightSkinsList = new List<FlightSkinsDto>();

        foreach (var flight in flights.OrderBy(f => f.DisplayOrder))
        {
            if (!participantsByFlight.TryGetValue(flight.Id, out var flightParticipants))
                continue;

            var flightSkins = CalculateFlightSkins(flight, flightParticipants);
            flightSkinsList.Add(flightSkins);
        }

        var result = new RoundSkinsDto(
            round.Id,
            round.RoundDate.ToString("yyyy-MM-dd"),
            course?.Name ?? "Unknown Course",
            flightSkinsList);

        return Result<RoundSkinsDto>.Ok(result);
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
            flight.Name,
            totalHolesWithSkins,
            totalSkinValueAwarded,
            playerSummaries,
            allHoleResults);
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
