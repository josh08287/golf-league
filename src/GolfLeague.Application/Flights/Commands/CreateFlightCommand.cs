using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using MediatR;
using static GolfLeague.Application.Common.FlightDisplayName;

namespace GolfLeague.Application.Flights.Commands;

public sealed record CreateFlightCommand(
    string Name,
    int HalfId,
    int DisplayOrder,
    string UserId) : IRequest<Result<FlightDto>>, IAmAuditableCommand
{
    public string AuditEntityType => "Flight";
    public string AuditEntityId => "0"; // assigned by the DB; resolved from the response
}

public sealed class CreateFlightCommandHandler : IRequestHandler<CreateFlightCommand, Result<FlightDto>>
{
    private readonly IFlightRepository _flightRepository;
    private readonly ILeagueContext _leagueContext;

    public CreateFlightCommandHandler(IFlightRepository flightRepository, ILeagueContext leagueContext)
    {
        _flightRepository = flightRepository;
        _leagueContext = leagueContext;
    }

    public async Task<Result<FlightDto>> Handle(CreateFlightCommand request, CancellationToken cancellationToken)
    {
        if (_leagueContext.LeagueId is null)
            return Result<FlightDto>.Fail("No league context.");

        var half = await _flightRepository.GetHalfByIdAsync(request.HalfId, cancellationToken);
        if (half is null)
            return Result<FlightDto>.Fail($"Half with ID {request.HalfId} not found.");

        var flight = new Flight
        {
            LeagueId = _leagueContext.LeagueId.Value,
            Name = request.Name,
            SeasonId = half.SeasonId,
            HalfId = half.Id,
            DisplayOrder = request.DisplayOrder,
        };

        await _flightRepository.AddAsync(flight, cancellationToken);

        var dto = new FlightDto(flight.Id, flight.SeasonId, flight.HalfId, Format(half.Season.Year, half.HalfNumber, flight.Name), flight.DisplayOrder, 0);
        return Result<FlightDto>.Ok(dto);
    }
}
