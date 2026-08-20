using GolfLeague.Application.Common;
using GolfLeague.Application.Flights.Queries;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;
using static GolfLeague.Application.Common.FlightDisplayName;

namespace GolfLeague.Application.Statistics.Queries;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record FlightHalfWinnersDto(
    int FlightId,
    string FlightName,
    PlayerScoreDto? NetWinner,
    PlayerScoreDto? GrossWinner);

public sealed record PlayerScoreDto(
    int PlayerId,
    string PlayerName,
    double Value,
    int RoundsPlayed);

public sealed record SingleRoundPointsDto(
    int PlayerId,
    string PlayerName,
    int Points,
    int RoundId,
    DateOnly RoundDate,
    int WeekNumber);

public sealed record HalfWrapUpDto(
    int HalfId,
    string HalfName,
    List<FlightHalfWinnersDto> FlightWinners);

public sealed record SeasonWrapUpDto(
    int SeasonId,
    string SeasonName,
    List<HalfWrapUpDto> Halves,
    List<PlayerScoreDto> SeasonLowGross,
    List<PlayerScoreDto> SeasonLowNet,
    SingleRoundPointsDto? SeasonHighGrossPoints,
    SingleRoundPointsDto? SeasonHighNetPoints,
    MostImprovedPlayerDto? MostImproved);

// ── Query + Handler ──────────────────────────────────────────────────────────

public sealed record GetSeasonWrapUpQuery(int SeasonId) : IRequest<Result<SeasonWrapUpDto>>;

public sealed class GetSeasonWrapUpQueryHandler : IRequestHandler<GetSeasonWrapUpQuery, Result<SeasonWrapUpDto>>
{
    private readonly ISeasonRepository _seasonRepository;
    private readonly IRoundRepository _roundRepository;
    private readonly IFlightRepository _flightRepository;
    private readonly IMediator _mediator;

    public GetSeasonWrapUpQueryHandler(
        ISeasonRepository seasonRepository,
        IRoundRepository roundRepository,
        IFlightRepository flightRepository,
        IMediator mediator)
    {
        _seasonRepository = seasonRepository;
        _roundRepository = roundRepository;
        _flightRepository = flightRepository;
        _mediator = mediator;
    }

    public async Task<Result<SeasonWrapUpDto>> Handle(GetSeasonWrapUpQuery request, CancellationToken cancellationToken)
    {
        var season = await _seasonRepository.GetByIdAsync(request.SeasonId, cancellationToken);
        if (season is null)
            return Result<SeasonWrapUpDto>.Fail($"Season with ID {request.SeasonId} not found.");

        var seasonRounds = await _roundRepository.GetBySeasonAsync(request.SeasonId, cancellationToken);
        var finalizedSeasonRounds = seasonRounds
            .Where(r => r.Status == RoundStatus.Finalized)
            .ToList();
        var seasonRoundIds = finalizedSeasonRounds.Select(r => r.Id).ToList();
        var seasonParticipants = await _roundRepository.GetParticipantsForRoundsAsync(seasonRoundIds, cancellationToken);

        // Season-wide low gross / low net — average across every finalized round
        // in the season, top 2 positions.
        var seasonLowGross = AveragePositions(seasonParticipants, useGross: true).Take(2).ToList();
        var seasonLowNet = AveragePositions(seasonParticipants, useGross: false).Take(2).ToList();

        // Season-wide high gross / net Stableford points — best single round.
        var roundsById = finalizedSeasonRounds.ToDictionary(r => r.Id);
        var seasonHighGrossPoints = HighestSingleRoundPoints(seasonParticipants, roundsById, useGross: true);
        var seasonHighNetPoints = HighestSingleRoundPoints(seasonParticipants, roundsById, useGross: false);

        // Per-half breakdown.
        var halves = season.Halves.OrderBy(h => h.HalfNumber).ToList();
        var halfDtos = new List<HalfWrapUpDto>(halves.Count);

        foreach (var half in halves)
        {
            var flights = await _flightRepository.GetByHalfAsync(half.Id, cancellationToken);
            var flightWinners = new List<FlightHalfWinnersDto>(flights.Count);

            foreach (var flight in flights.OrderBy(f => f.DisplayOrder))
            {
                var netStandings = await _mediator.Send(
                    new GetFlightStandingsQuery(flight.Id, half.Id, UseGrossPoints: false), cancellationToken);
                var grossStandings = await _mediator.Send(
                    new GetFlightStandingsQuery(flight.Id, half.Id, UseGrossPoints: true), cancellationToken);

                var netWinner = netStandings.IsSuccess
                    ? netStandings.Value!.FirstOrDefault(s => s.Position == 1)
                    : null;
                var grossWinner = grossStandings.IsSuccess
                    ? grossStandings.Value!.FirstOrDefault(s => s.Position == 1)
                    : null;

                flightWinners.Add(new FlightHalfWinnersDto(
                    flight.Id,
                    Format(season.Year, half.HalfNumber, flight.Name),
                    netWinner is null ? null : new PlayerScoreDto(netWinner.PlayerId, netWinner.PlayerFullName, netWinner.TotalPoints, netWinner.RoundsPlayed),
                    grossWinner is null ? null : new PlayerScoreDto(grossWinner.PlayerId, grossWinner.PlayerFullName, grossWinner.TotalPoints, grossWinner.RoundsPlayed)));
            }

            halfDtos.Add(new HalfWrapUpDto(half.Id, half.Name, flightWinners));
        }

        // Season-long Most Improved — reuse the existing handler, scoped to this season.
        var mostImprovedResult = await _mediator.Send(new GetMostImprovedPlayerQuery(SeasonId: request.SeasonId), cancellationToken);
        var mostImproved = mostImprovedResult.IsSuccess ? mostImprovedResult.Value!.Winner : null;

        return Result<SeasonWrapUpDto>.Ok(new SeasonWrapUpDto(
            season.Id, season.Name, halfDtos, seasonLowGross, seasonLowNet,
            seasonHighGrossPoints, seasonHighNetPoints, mostImproved));
    }

    private static SingleRoundPointsDto? HighestSingleRoundPoints(
        IReadOnlyList<Domain.Entities.RoundParticipant> participants,
        IReadOnlyDictionary<int, Domain.Entities.Round> roundsById,
        bool useGross)
    {
        return participants
            .Where(p => !p.IsWithdrawn && !p.SkippedWeek && !p.IsSubstitute)
            .Select(p => new
            {
                Participant = p,
                Points = useGross ? p.TotalGrossStablefordPoints : p.TotalNetStablefordPoints,
            })
            .Where(x => x.Points.HasValue)
            .OrderByDescending(x => x.Points!.Value)
            .Select(x =>
            {
                var round = roundsById[x.Participant.RoundId];
                return new SingleRoundPointsDto(
                    x.Participant.PlayerId,
                    x.Participant.Player?.FullName ?? string.Empty,
                    x.Points!.Value,
                    round.Id,
                    round.RoundDate,
                    round.WeekNumber);
            })
            .FirstOrDefault();
    }

    private static IEnumerable<PlayerScoreDto> AveragePositions(
        IReadOnlyList<Domain.Entities.RoundParticipant> participants, bool useGross)
    {
        var active = participants.Where(p => !p.IsWithdrawn && !p.SkippedWeek && !p.IsSubstitute);

        return active
            .Where(p => useGross ? p.TotalGrossStrokes.HasValue : p.TotalNetStrokes.HasValue)
            .GroupBy(p => p.PlayerId)
            .Select(g => new
            {
                PlayerId = g.Key,
                PlayerName = g.First().Player?.FullName ?? string.Empty,
                Average = useGross
                    ? (double)g.Sum(p => p.TotalGrossStrokes!.Value) / g.Count()
                    : (double)g.Sum(p => p.TotalNetStrokes!.Value) / g.Count(),
                Rounds = g.Count(),
            })
            .OrderBy(x => x.Average)
            .ThenBy(x => x.PlayerName)
            .Select(x => new PlayerScoreDto(x.PlayerId, x.PlayerName, Math.Round(x.Average, 1), x.Rounds));
    }
}
