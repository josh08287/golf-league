using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Players.Queries;

public sealed record GetPlayerQuery(int Id) : IRequest<Result<PlayerDto>>;

public sealed class GetPlayerQueryHandler : IRequestHandler<GetPlayerQuery, Result<PlayerDto>>
{
    private readonly IPlayerRepository _playerRepository;
    private readonly IHandicapRepository _handicapRepository;
    private readonly IUserRoleService _roleService;
    private readonly IAdminUserService _adminUserService;

    public GetPlayerQueryHandler(
        IPlayerRepository playerRepository,
        IHandicapRepository handicapRepository,
        IUserRoleService roleService,
        IAdminUserService adminUserService)
    {
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
        _roleService = roleService;
        _adminUserService = adminUserService;
    }

    public async Task<Result<PlayerDto>> Handle(GetPlayerQuery request, CancellationToken cancellationToken)
    {
        var player = await _playerRepository.GetByIdAsync(request.Id, cancellationToken);
        if (player is null)
            return Result<PlayerDto>.Fail($"Player with ID {request.Id} not found.");

        var currentHandicap = await _handicapRepository.GetCurrentAsync(player.Id, cancellationToken);
        IReadOnlyList<string> roles = Array.Empty<string>();
        AccountInfoDto? account = null;
        if (player.AppUserId.HasValue)
        {
            roles = await _roleService.GetRolesAsync(player.AppUserId.Value, cancellationToken);
            account = await _adminUserService.GetAccountInfoAsync(player.AppUserId.Value, cancellationToken);
        }

        var dto = GetPlayersQueryHandler.ToDto(player, currentHandicap?.HandicapIndex, roles) with { Account = account };
        return Result<PlayerDto>.Ok(dto);
    }
}
