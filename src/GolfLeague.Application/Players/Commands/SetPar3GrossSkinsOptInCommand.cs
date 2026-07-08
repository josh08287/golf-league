using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Players.Queries;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Players.Commands;

/// <summary>
/// Sets whether a player is opted in to par-3 gross skins for a specific
/// half. Stored independently of FlightMembership so it survives flight
/// reassignment within the half.
/// </summary>
public sealed record SetPar3GrossSkinsOptInCommand(
    int PlayerId,
    int HalfId,
    bool OptIn,
    string UserId) : IRequest<Result<PlayerDto>>, IAmAuditableCommand;

public sealed class SetPar3GrossSkinsOptInCommandHandler : IRequestHandler<SetPar3GrossSkinsOptInCommand, Result<PlayerDto>>
{
    private readonly IPlayerRepository _playerRepository;
    private readonly IFlightRepository _flightRepository;
    private readonly IPlayerHalfSettingRepository _halfSettingRepository;
    private readonly IHandicapRepository _handicapRepository;

    public SetPar3GrossSkinsOptInCommandHandler(
        IPlayerRepository playerRepository,
        IFlightRepository flightRepository,
        IPlayerHalfSettingRepository halfSettingRepository,
        IHandicapRepository handicapRepository)
    {
        _playerRepository = playerRepository;
        _flightRepository = flightRepository;
        _halfSettingRepository = halfSettingRepository;
        _handicapRepository = handicapRepository;
    }

    public async Task<Result<PlayerDto>> Handle(SetPar3GrossSkinsOptInCommand request, CancellationToken cancellationToken)
    {
        var player = await _playerRepository.GetByIdAsync(request.PlayerId, cancellationToken);
        if (player is null)
            return Result<PlayerDto>.Fail($"Player with ID {request.PlayerId} not found.");

        var half = await _flightRepository.GetHalfByIdAsync(request.HalfId, cancellationToken);
        if (half is null)
            return Result<PlayerDto>.Fail($"Half with ID {request.HalfId} not found.");

        await _halfSettingRepository.SetPar3GrossSkinsOptInAsync(
            request.PlayerId, request.HalfId, half.SeasonId, request.OptIn, cancellationToken);

        var updated = await _playerRepository.GetByIdAsync(request.PlayerId, cancellationToken);
        var currentHandicap = await _handicapRepository.GetCurrentAsync(request.PlayerId, cancellationToken);
        return Result<PlayerDto>.Ok(GetPlayersQueryHandler.ToDto(updated!, currentHandicap?.HandicapIndex));
    }
}
