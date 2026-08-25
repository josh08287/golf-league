using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using MediatR;
using static GolfLeague.Domain.Services.RoundRobinScheduler;

namespace GolfLeague.Application.Flights.Commands;

public sealed record GenerateMatchPlayScheduleCommand(int HalfId, string UserId)
    : IRequest<Result<GenerateMatchPlayScheduleResult>>, IAmAuditableCommand
{
    public string AuditEntityType => "SeasonHalf";
    public string AuditEntityId => HalfId.ToString();
}

public sealed record FlightScheduleSummaryDto(int FlightId, string FlightName, int MatchesScheduled, bool HasBye);

public sealed record GenerateMatchPlayScheduleResult(
    List<FlightScheduleSummaryDto> FlightSummaries,
    List<string> Warnings);

public sealed class GenerateMatchPlayScheduleCommandHandler
    : IRequestHandler<GenerateMatchPlayScheduleCommand, Result<GenerateMatchPlayScheduleResult>>
{
    private readonly IFlightRepository _flightRepository;
    private readonly IRoundRepository _roundRepository;
    private readonly IFlightMatchRepository _flightMatchRepository;
    private readonly ILeagueContext _leagueContext;

    public GenerateMatchPlayScheduleCommandHandler(
        IFlightRepository flightRepository,
        IRoundRepository roundRepository,
        IFlightMatchRepository flightMatchRepository,
        ILeagueContext leagueContext)
    {
        _flightRepository = flightRepository;
        _roundRepository = roundRepository;
        _flightMatchRepository = flightMatchRepository;
        _leagueContext = leagueContext;
    }

    public async Task<Result<GenerateMatchPlayScheduleResult>> Handle(
        GenerateMatchPlayScheduleCommand request,
        CancellationToken cancellationToken)
    {
        if (_leagueContext.LeagueId is null)
            return Result<GenerateMatchPlayScheduleResult>.Fail("No league context.");

        var half = await _flightRepository.GetHalfByIdAsync(request.HalfId, cancellationToken);
        if (half is null)
            return Result<GenerateMatchPlayScheduleResult>.Fail($"Half with ID {request.HalfId} not found.");

        if (half.ScoringFormat != ScoringFormat.MatchPlay)
            return Result<GenerateMatchPlayScheduleResult>.Fail("This half is not configured for match play scoring.");

        if (await _flightRepository.IsHalfLockedAsync(request.HalfId, cancellationToken))
            return Result<GenerateMatchPlayScheduleResult>.Fail(
                "Cannot regenerate the match schedule once rounds have started for this half.");

        var flights = await _flightRepository.GetByHalfAsync(request.HalfId, cancellationToken);
        if (flights.Count == 0)
            return Result<GenerateMatchPlayScheduleResult>.Fail("This half has no flights yet — initialize flights first.");

        var rounds = await _roundRepository.GetByHalfAsync(request.HalfId, cancellationToken);
        var availableWeeks = rounds
            .OrderBy(r => r.WeekNumber)
            .Select(r => (RoundId: r.Id, WeekNumber: r.WeekNumber))
            .ToList();

        if (availableWeeks.Count == 0)
            return Result<GenerateMatchPlayScheduleResult>.Fail("This half has no scheduled rounds to assign matches to.");

        await _flightMatchRepository.DeleteByHalfAsync(request.HalfId, cancellationToken);

        var summaries = new List<FlightScheduleSummaryDto>();
        var warnings = new List<string>();

        foreach (var flight in flights)
        {
            var playerIds = flight.Memberships.Select(m => m.PlayerId).ToList();
            if (playerIds.Count < 2)
            {
                warnings.Add($"{flight.Name}: needs at least 2 players to schedule matches — skipped.");
                summaries.Add(new FlightScheduleSummaryDto(flight.Id, flight.Name, 0, false));
                continue;
            }

            var circle = GenerateCircle(playerIds);
            var (scheduled, fit) = MapToWeeks(circle, availableWeeks);

            var matches = new List<FlightMatch>();
            var hasBye = false;
            foreach (var (roundId, weekNumber, pairings) in scheduled)
            {
                foreach (var pairing in pairings)
                {
                    if (pairing.Player2Id is null) hasBye = true;
                    matches.Add(new FlightMatch
                    {
                        FlightId = flight.Id,
                        HalfId = half.Id,
                        RoundId = roundId,
                        WeekNumber = weekNumber,
                        Player1Id = pairing.Player1Id,
                        Player2Id = pairing.Player2Id,
                    });
                }
            }

            await _flightMatchRepository.AddRangeAsync(matches, cancellationToken);
            summaries.Add(new FlightScheduleSummaryDto(flight.Id, flight.Name, matches.Count, hasBye));

            switch (fit)
            {
                case WeekFitResult.MoreWeeksThanNeeded:
                    warnings.Add($"{flight.Name}: round robin completes before the half's last scheduled week — later weeks have no match for this flight.");
                    break;
                case WeekFitResult.FewerWeeksThanNeeded:
                    warnings.Add($"{flight.Name}: not enough scheduled weeks to complete a full round robin — not every pair will play this half.");
                    break;
            }
        }

        return Result<GenerateMatchPlayScheduleResult>.Ok(new GenerateMatchPlayScheduleResult(summaries, warnings));
    }
}
