using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Interfaces;
using GolfLeague.Application.Players.Queries;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Players.Commands;

/// <summary>
/// Sets a player's flight membership for a single half. A non-null
/// <see cref="FlightId"/> adds the player to that flight (and half); a null
/// <see cref="FlightId"/> removes them from the half. Allowed even after the
/// half's rounds have started — admins are prompted to run
/// RecalculateAllRoundsCommand afterward to bring historical rounds back in sync.
/// </summary>
public sealed record SetHalfMembershipCommand(
    int PlayerId,
    int HalfId,
    int? FlightId,
    string UserId) : IRequest<Result<PlayerDto>>, IAmAuditableCommand;

public sealed class SetHalfMembershipCommandHandler : IRequestHandler<SetHalfMembershipCommand, Result<PlayerDto>>
{
    private readonly IPlayerRepository _playerRepository;
    private readonly IFlightRepository _flightRepository;
    private readonly IHandicapRepository _handicapRepository;

    public SetHalfMembershipCommandHandler(
        IPlayerRepository playerRepository,
        IFlightRepository flightRepository,
        IHandicapRepository handicapRepository)
    {
        _playerRepository = playerRepository;
        _flightRepository = flightRepository;
        _handicapRepository = handicapRepository;
    }

    public async Task<Result<PlayerDto>> Handle(SetHalfMembershipCommand request, CancellationToken cancellationToken)
    {
        var player = await _playerRepository.GetByIdAsync(request.PlayerId, cancellationToken);
        if (player is null)
            return Result<PlayerDto>.Fail($"Player with ID {request.PlayerId} not found.");

        var half = await _flightRepository.GetHalfByIdAsync(request.HalfId, cancellationToken);
        if (half is null)
            return Result<PlayerDto>.Fail($"Half with ID {request.HalfId} not found.");

        if (request.FlightId is int flightId)
        {
            var flight = await _flightRepository.GetByIdAsync(flightId, cancellationToken);
            if (flight is null)
                return Result<PlayerDto>.Fail($"Flight with ID {flightId} not found.");
            if (flight.HalfId != request.HalfId)
                return Result<PlayerDto>.Fail("Selected flight does not belong to the specified half.");
        }

        await _playerRepository.SetHalfMembershipAsync(request.PlayerId, request.HalfId, request.FlightId, cancellationToken);

        // Reload so the returned DTO reflects the updated memberships.
        var updated = await _playerRepository.GetByIdAsync(request.PlayerId, cancellationToken);
        var currentHandicap = await _handicapRepository.GetCurrentAsync(request.PlayerId, cancellationToken);
        return Result<PlayerDto>.Ok(GetPlayersQueryHandler.ToDto(updated!, currentHandicap?.HandicapIndex));
    }
}
