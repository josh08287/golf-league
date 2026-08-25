using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Flights.Queries;

public sealed record GetMatchPlayStandingsQuery(
    int FlightId,
    int HalfId,
    SortRequest? Sort = null) : IRequest<Result<List<MatchPlayStandingDto>>>;

public sealed class GetMatchPlayStandingsQueryHandler : IRequestHandler<GetMatchPlayStandingsQuery, Result<List<MatchPlayStandingDto>>>
{
    private readonly IFlightRepository _flightRepository;
    private readonly IFlightMatchRepository _flightMatchRepository;
    private readonly IHandicapRepository _handicapRepository;
    private readonly IPlayerRepository _playerRepository;

    private static readonly SortMap<MatchPlayStandingDto> SortMap = new SortMap<MatchPlayStandingDto>(
            source => source.OrderBy(s => s.Position))
        .Add("position", s => s.Position)
        .Add("player", s => s.PlayerFullName)
        .Add("playerName", s => s.PlayerFullName)
        .Add("playerFullName", s => s.PlayerFullName)
        .Add("matches", s => s.MatchesPlayed)
        .Add("matchesPlayed", s => s.MatchesPlayed)
        .Add("points", s => s.TotalPoints)
        .Add("totalPoints", s => s.TotalPoints)
        .Add("avg", s => s.AveragePointsPerMatch)
        .Add("averagePointsPerMatch", s => s.AveragePointsPerMatch)
        .Add("hcp", s => s.CurrentHandicapIndex)
        .Add("currentHandicapIndex", s => s.CurrentHandicapIndex);

    public GetMatchPlayStandingsQueryHandler(
        IFlightRepository flightRepository,
        IFlightMatchRepository flightMatchRepository,
        IHandicapRepository handicapRepository,
        IPlayerRepository playerRepository)
    {
        _flightRepository = flightRepository;
        _flightMatchRepository = flightMatchRepository;
        _handicapRepository = handicapRepository;
        _playerRepository = playerRepository;
    }

    public async Task<Result<List<MatchPlayStandingDto>>> Handle(GetMatchPlayStandingsQuery request, CancellationToken cancellationToken)
    {
        var flight = await _flightRepository.GetByIdAsync(request.FlightId, cancellationToken);
        if (flight is null)
            return Result<List<MatchPlayStandingDto>>.Fail($"Flight with ID {request.FlightId} not found.");

        var matches = await _flightMatchRepository.GetByFlightAsync(request.FlightId, request.HalfId, cancellationToken);
        var scoredMatches = matches.Where(m => m.Player1Points.HasValue).ToList();

        var playerIds = scoredMatches
            .SelectMany(m => new[] { (int?)m.Player1Id, m.Player2Id })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();

        if (playerIds.Count == 0)
            return Result<List<MatchPlayStandingDto>>.Ok([]);

        var playersById = (await _playerRepository.GetAllAsync(cancellationToken))
            .Where(p => playerIds.Contains(p.Id))
            .ToDictionary(p => p.Id);
        var currentHandicapByPlayerId = (await _handicapRepository.GetAllAsync(cancellationToken))
            .Where(h => playerIds.Contains(h.PlayerId))
            .GroupBy(h => h.PlayerId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(h => h.EffectiveDate).ThenByDescending(h => h.Id).First());

        var matchResultsByPlayer = new Dictionary<int, List<MatchPlayMatchResultDto>>();

        void AddResult(int playerId, MatchPlayMatchResultDto dto)
        {
            if (!matchResultsByPlayer.TryGetValue(playerId, out var list))
                matchResultsByPlayer[playerId] = list = [];
            list.Add(dto);
        }

        foreach (var match in scoredMatches)
        {
            var isBye = match.Player2Id is null;
            var opponent2 = !isBye && match.Player2Id.HasValue ? playersById.GetValueOrDefault(match.Player2Id.Value) : null;
            var opponent1 = playersById.GetValueOrDefault(match.Player1Id);

            AddResult(match.Player1Id, new MatchPlayMatchResultDto(
                match.RoundId, match.WeekNumber, match.Round.RoundDate.ToString("yyyy-MM-dd"),
                match.Player2Id, opponent2?.FullName,
                match.Player1Points ?? 0, match.Player2Points ?? 0,
                match.Player1HolesWon ?? 0, match.Player2HolesWon ?? 0,
                WasBye: isBye, WasAgainstCard: match.Player1Absent || match.Player2Absent));

            if (!isBye && match.Player2Id.HasValue)
            {
                AddResult(match.Player2Id.Value, new MatchPlayMatchResultDto(
                    match.RoundId, match.WeekNumber, match.Round.RoundDate.ToString("yyyy-MM-dd"),
                    match.Player1Id, opponent1?.FullName,
                    match.Player2Points ?? 0, match.Player1Points ?? 0,
                    match.Player2HolesWon ?? 0, match.Player1HolesWon ?? 0,
                    WasBye: false, WasAgainstCard: match.Player1Absent || match.Player2Absent));
            }
        }

        var dtos = new List<MatchPlayStandingDto>();
        foreach (var (playerId, results) in matchResultsByPlayer)
        {
            if (!playersById.TryGetValue(playerId, out var player))
                continue;

            var totalPoints = results.Sum(r => r.PlayerPoints);
            var matchesPlayed = results.Count;
            var avg = matchesPlayed > 0 ? (double)totalPoints / matchesPlayed : 0.0;

            var wins = results.Count(r => !r.WasBye && r.PlayerHolesWon > r.OpponentHolesWon);
            var losses = results.Count(r => !r.WasBye && r.PlayerHolesWon < r.OpponentHolesWon);
            var halves = results.Count(r => !r.WasBye && r.PlayerHolesWon == r.OpponentHolesWon);

            currentHandicapByPlayerId.TryGetValue(playerId, out var currentHandicap);

            dtos.Add(new MatchPlayStandingDto(
                Position: 0,
                PlayerId: player.Id,
                PlayerFullName: player.FullName,
                PlayerInitials: player.Initials,
                MatchesPlayed: matchesPlayed,
                TotalPoints: totalPoints,
                AveragePointsPerMatch: Math.Round(avg, 2),
                Wins: wins,
                Halves: halves,
                Losses: losses,
                CurrentHandicapIndex: currentHandicap?.HandicapIndex ?? 0.0,
                MatchResults: results.OrderBy(r => r.WeekNumber).ToList()));
        }

        var ranked = dtos
            .OrderByDescending(d => d.TotalPoints)
            .ThenByDescending(d => d.AveragePointsPerMatch)
            .Select((d, index) => d with { Position = index + 1 })
            .ToList();

        var sorted = SortMap.Apply(ranked, request.Sort);
        return Result<List<MatchPlayStandingDto>>.Ok(sorted.ToList());
    }
}
