using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Flights.Queries;

public sealed record GetFlightsQuery(int? HalfId = null, int? SeasonId = null) : IRequest<Result<PagedResult<FlightDto>>>;

public sealed class GetFlightsQueryHandler : IRequestHandler<GetFlightsQuery, Result<PagedResult<FlightDto>>>
{
    private readonly IFlightRepository _flightRepository;

    public GetFlightsQueryHandler(IFlightRepository flightRepository)
    {
        _flightRepository = flightRepository;
    }

    public async Task<Result<PagedResult<FlightDto>>> Handle(GetFlightsQuery request, CancellationToken cancellationToken)
    {
        var flights = request.HalfId.HasValue
            ? await _flightRepository.GetByHalfAsync(request.HalfId.Value, cancellationToken)
            : request.SeasonId.HasValue
                ? await _flightRepository.GetBySeasonAsync(request.SeasonId.Value, cancellationToken)
                : await _flightRepository.GetAllAsync(cancellationToken);

        var dtos = flights.Select(f => new FlightDto(
            f.Id,
            f.SeasonId,
            f.HalfId,
            f.Name,
            f.DisplayOrder,
            f.Memberships.Count
        )).ToList();

        return Result<PagedResult<FlightDto>>.Ok(new PagedResult<FlightDto>(dtos, 1, dtos.Count, dtos.Count));
    }
}
