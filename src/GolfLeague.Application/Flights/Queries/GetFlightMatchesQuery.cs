using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Flights.Queries;

/// <summary>Fetches the scheduled/scored match-play matches for a half, optionally scoped to one flight.</summary>
public sealed record GetFlightMatchesQuery(int HalfId, int? FlightId = null) : IRequest<Result<List<FlightMatchDto>>>;

public sealed class GetFlightMatchesQueryHandler : IRequestHandler<GetFlightMatchesQuery, Result<List<FlightMatchDto>>>
{
    private readonly IFlightMatchRepository _flightMatchRepository;

    public GetFlightMatchesQueryHandler(IFlightMatchRepository flightMatchRepository)
    {
        _flightMatchRepository = flightMatchRepository;
    }

    public async Task<Result<List<FlightMatchDto>>> Handle(GetFlightMatchesQuery request, CancellationToken cancellationToken)
    {
        var matches = request.FlightId.HasValue
            ? await _flightMatchRepository.GetByFlightAsync(request.FlightId.Value, request.HalfId, cancellationToken)
            : await _flightMatchRepository.GetByHalfAsync(request.HalfId, cancellationToken);

        var dtos = matches
            .OrderBy(m => m.WeekNumber)
            .ThenBy(m => m.FlightId)
            .Select(m => new FlightMatchDto(
                m.Id,
                m.FlightId,
                m.RoundId,
                m.WeekNumber,
                m.Round.RoundDate.ToString("yyyy-MM-dd"),
                m.Player1Id,
                m.Player1.FullName,
                m.Player2Id,
                m.Player2?.FullName,
                m.Player1Points,
                m.Player2Points,
                m.Player1Absent,
                m.Player2Absent))
            .ToList();

        return Result<List<FlightMatchDto>>.Ok(dtos);
    }
}
