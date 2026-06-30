using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Interfaces;
using MediatR;
using static GolfLeague.Application.Common.FlightDisplayName;

namespace GolfLeague.Application.Flights.Queries;

public sealed record GetFlightQuery(int Id) : IRequest<Result<FlightDto>>;

public sealed class GetFlightQueryHandler : IRequestHandler<GetFlightQuery, Result<FlightDto>>
{
    private readonly IFlightRepository _flightRepository;

    public GetFlightQueryHandler(IFlightRepository flightRepository)
    {
        _flightRepository = flightRepository;
    }

    public async Task<Result<FlightDto>> Handle(GetFlightQuery request, CancellationToken cancellationToken)
    {
        var flight = await _flightRepository.GetByIdAsync(request.Id, cancellationToken);
        if (flight is null)
            return Result<FlightDto>.Fail($"Flight with ID {request.Id} not found.");

        var memberships = await _flightRepository.GetMembershipsAsync(request.Id, cancellationToken);

        return Result<FlightDto>.Ok(new FlightDto(
            flight.Id,
            flight.SeasonId,
            flight.HalfId,
            Format(flight),
            flight.DisplayOrder,
            memberships.Count));
    }
}
