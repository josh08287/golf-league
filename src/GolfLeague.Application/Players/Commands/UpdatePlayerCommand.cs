using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Interfaces;
using GolfLeague.Application.Players.Queries;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Players.Commands;

public sealed record UpdatePlayerCommand(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    string UserId,
    string? Role = null) : IRequest<Result<PlayerDto>>, IAmAuditableCommand;

public sealed class UpdatePlayerCommandHandler : IRequestHandler<UpdatePlayerCommand, Result<PlayerDto>>
{
    private readonly IPlayerRepository _playerRepository;
    private readonly IHandicapRepository _handicapRepository;
    private readonly IEntraRoleService _entraRoleService;

    public UpdatePlayerCommandHandler(
        IPlayerRepository playerRepository,
        IHandicapRepository handicapRepository,
        IEntraRoleService entraRoleService)
    {
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
        _entraRoleService = entraRoleService;
    }

    public async Task<Result<PlayerDto>> Handle(UpdatePlayerCommand request, CancellationToken cancellationToken)
    {
        var player = await _playerRepository.GetByIdAsync(request.Id, cancellationToken);
        if (player is null)
            return Result<PlayerDto>.Fail($"Player with ID {request.Id} not found.");

        player.FirstName = request.FirstName;
        player.LastName = request.LastName;
        player.Email = request.Email;

        // Update role in Entra ID (authorization source of truth)
        if (!string.IsNullOrWhiteSpace(request.Role) &&
            Enum.TryParse<Domain.Enums.PlayerRole>(request.Role, true, out var role))
        {
            // Assign the new role in Entra ID via Microsoft Graph API
            var roleResult = await _entraRoleService.AssignRoleAsync(
                player.EntraObjectId,
                request.Role,
                cancellationToken);

            if (!roleResult.IsSuccess)
            {
                return Result<PlayerDto>.Fail($"Failed to update role in Entra ID: {roleResult.Error}");
            }

            // Update database role for reference (not used for authorization)
            player.Role = role;
        }

        await _playerRepository.UpdateAsync(player, cancellationToken);

        var currentHandicap = await _handicapRepository.GetCurrentAsync(player.Id, cancellationToken);
        var dto = GetPlayersQueryHandler.ToDto(player, currentHandicap?.HandicapIndex);

        return Result<PlayerDto>.Ok(dto);
    }
}
