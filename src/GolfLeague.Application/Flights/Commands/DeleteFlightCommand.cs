using GolfLeague.Application.Common;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Flights.Commands;

public sealed record DeleteFlightCommand(
    int Id,
    string UserId) : IRequest<Result<bool>>, IAmAuditableCommand
{
    public string AuditEntityType => "Flight";
    public string AuditEntityId => Id.ToString();
}

public sealed class DeleteFlightCommandHandler : IRequestHandler<DeleteFlightCommand, Result<bool>>
{
    private readonly IFlightRepository _flightRepository;

    public DeleteFlightCommandHandler(IFlightRepository flightRepository)
    {
        _flightRepository = flightRepository;
    }

    public async Task<Result<bool>> Handle(DeleteFlightCommand request, CancellationToken cancellationToken)
    {
        var flight = await _flightRepository.GetByIdAsync(request.Id, cancellationToken);
        if (flight is null)
            return Result<bool>.Fail($"Flight with ID {request.Id} not found.");

        await _flightRepository.DeleteAsync(request.Id, cancellationToken);
        return Result<bool>.Ok(true);
    }
}
