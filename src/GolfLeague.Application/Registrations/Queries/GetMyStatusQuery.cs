using GolfLeague.Application.Common;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Registrations.Queries;

/// <summary>
/// Query to get the current user's status. Roles come from the linked
/// AppUser (authoritative for authorization).
/// </summary>
public sealed record GetMyStatusQuery(Guid AppUserId) : IRequest<Result<MyStatusResult>>;

/// <summary>
/// Status values:
///   "approved"  — user is a linked, active player
///   "none"      — signed in but no player record (admin-only users)
/// </summary>
public sealed record MyStatusResult(string Status, int? PlayerId, IReadOnlyList<string> Roles);

public sealed class GetMyStatusQueryHandler : IRequestHandler<GetMyStatusQuery, Result<MyStatusResult>>
{
    private readonly IPlayerRepository _playerRepo;
    private readonly IUserRoleService _roleService;

    public GetMyStatusQueryHandler(IPlayerRepository playerRepo, IUserRoleService roleService)
    {
        _playerRepo = playerRepo;
        _roleService = roleService;
    }

    public async Task<Result<MyStatusResult>> Handle(GetMyStatusQuery request, CancellationToken cancellationToken)
    {
        var roles = await _roleService.GetRolesAsync(request.AppUserId, cancellationToken);

        var player = await _playerRepo.GetByAppUserIdAsync(request.AppUserId, cancellationToken);
        var status = player is not null ? "approved" : "none";
        return Result<MyStatusResult>.Ok(new MyStatusResult(status, player?.Id, roles));
    }
}
