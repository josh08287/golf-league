using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Players.Queries;
using GolfLeague.Domain.Enums;
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
    private readonly IAppUserRepository _appUserRepository;

    public UpdatePlayerCommandHandler(
        IPlayerRepository playerRepository,
        IHandicapRepository handicapRepository,
        IAppUserRepository appUserRepository)
    {
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
        _appUserRepository = appUserRepository;
    }

    public async Task<Result<PlayerDto>> Handle(UpdatePlayerCommand request, CancellationToken cancellationToken)
    {
        var player = await _playerRepository.GetByIdAsync(request.Id, cancellationToken);
        if (player is null)
            return Result<PlayerDto>.Fail($"Player with ID {request.Id} not found.");

        player.FirstName = request.FirstName;
        player.LastName = request.LastName;
        player.Email = request.Email;

        await _playerRepository.UpdateAsync(player, cancellationToken);

        // Role lives on the linked AppUser (authoritative). If the Player has
        // no linked account yet, role updates are deferred until they claim
        // an invite.
        var roleString = "player";
        if (player.AppUserId is Guid appUserId)
        {
            if (!string.IsNullOrWhiteSpace(request.Role) &&
                Enum.TryParse<PlayerRole>(request.Role, true, out var newRole))
            {
                await _appUserRepository.UpdateRoleAsync(appUserId, newRole, cancellationToken);
                roleString = newRole.ToString().ToLowerInvariant();
            }
            else
            {
                var user = await _appUserRepository.GetByIdAsync(appUserId, cancellationToken);
                if (user is not null)
                    roleString = user.Role.ToString().ToLowerInvariant();
            }
        }

        var currentHandicap = await _handicapRepository.GetCurrentAsync(player.Id, cancellationToken);
        var dto = GetPlayersQueryHandler.ToDto(player, currentHandicap?.HandicapIndex, roleString);

        return Result<PlayerDto>.Ok(dto);
    }
}
