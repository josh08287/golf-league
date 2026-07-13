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
    string? Email,
    string UserId,
    IReadOnlyList<string>? Roles = null) : IRequest<Result<PlayerDto>>, IAmAuditableCommand
{
    public string AuditEntityType => "Player";
    public string AuditEntityId => Id.ToString();
}

public sealed class UpdatePlayerCommandHandler : IRequestHandler<UpdatePlayerCommand, Result<PlayerDto>>
{
    private readonly IPlayerRepository _playerRepository;
    private readonly IHandicapRepository _handicapRepository;
    private readonly IUserRoleService _roleService;

    public UpdatePlayerCommandHandler(
        IPlayerRepository playerRepository,
        IHandicapRepository handicapRepository,
        IUserRoleService roleService)
    {
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
        _roleService = roleService;
    }

    public async Task<Result<PlayerDto>> Handle(UpdatePlayerCommand request, CancellationToken cancellationToken)
    {
        var player = await _playerRepository.GetByIdAsync(request.Id, cancellationToken);
        if (player is null)
            return Result<PlayerDto>.Fail($"Player with ID {request.Id} not found.");

        player.FirstName = request.FirstName;
        player.LastName = request.LastName;
        // Treat whitespace as "no email" so admins can clear the field.
        player.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email;
        await _playerRepository.UpdateAsync(player, cancellationToken);

        // Roles live on the linked AppUser. If the Player has no linked
        // account yet, role updates are deferred until they claim an invite.
        IReadOnlyList<string> currentRoles = Array.Empty<string>();
        if (player.AppUserId is Guid appUserId)
        {
            if (request.Roles is not null)
            {
                var setResult = await _roleService.SetRolesAsync(appUserId, request.Roles, cancellationToken);
                if (!setResult.IsSuccess)
                    return Result<PlayerDto>.Fail(setResult.Error!);
            }
            currentRoles = await _roleService.GetRolesAsync(appUserId, cancellationToken);
        }

        var currentHandicap = await _handicapRepository.GetCurrentAsync(player.Id, cancellationToken);
        var dto = GetPlayersQueryHandler.ToDto(player, currentHandicap?.HandicapIndex, currentRoles);

        return Result<PlayerDto>.Ok(dto);
    }
}
