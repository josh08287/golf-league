using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Flights.Commands;

public sealed record CreateFlightCommand(
    string Name,
    int HalfId,
    int DisplayOrder,
    string UserId) : IRequest<Result<FlightDto>>, IAmAuditableCommand;

public sealed class CreateFlightCommandHandler : IRequestHandler<CreateFlightCommand, Result<FlightDto>>
{
    private readonly IFlightRepository _flightRepository;

    public CreateFlightCommandHandler(IFlightRepository flightRepository)
    {
        _flightRepository = flightRepository;
    }

    public async Task<Result<FlightDto>> Handle(CreateFlightCommand request, CancellationToken cancellationToken)
    {
        var half = await _flightRepository.GetHalfByIdAsync(request.HalfId, cancellationToken);
        if (half is null)
            return Result<FlightDto>.Fail($"Half with ID {request.HalfId} not found.");

        var flight = new Flight
        {
            Name = request.Name,
            SeasonId = half.SeasonId,
            HalfId = half.Id,
            DisplayOrder = request.DisplayOrder,
        };

        await _flightRepository.AddAsync(flight, cancellationToken);

        var dto = new FlightDto(flight.Id, flight.SeasonId, flight.HalfId, flight.Name, flight.DisplayOrder, 0);
        return Result<FlightDto>.Ok(dto);
    }
}
