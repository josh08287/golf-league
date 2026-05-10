using GolfLeague.Application.Common;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Registrations.Queries;

/// <summary>
/// Query to get the current user's status. Role comes from the linked
/// AppUser (authoritative for authorization).
/// </summary>
public sealed record GetMyStatusQuery(Guid AppUserId) : IRequest<Result<MyStatusResult>>;

/// <summary>
/// Status values:
///   "approved"  — user is a linked, active player
///   "none"      — signed in but no invite / player record (admin-only users)
/// </summary>
public sealed record MyStatusResult(string Status, int? PlayerId, string Role);

public sealed class GetMyStatusQueryHandler : IRequestHandler<GetMyStatusQuery, Result<MyStatusResult>>
{
    private readonly IPlayerRepository _playerRepo;
    private readonly IAppUserRepository _appUserRepo;

    public GetMyStatusQueryHandler(IPlayerRepository playerRepo, IAppUserRepository appUserRepo)
    {
        _playerRepo = playerRepo;
        _appUserRepo = appUserRepo;
    }

    public async Task<Result<MyStatusResult>> Handle(GetMyStatusQuery request, CancellationToken cancellationToken)
    {
        var user = await _appUserRepo.GetByIdAsync(request.AppUserId, cancellationToken);
        var role = (user?.Role.ToString() ?? "player").ToLowerInvariant();

        var player = await _playerRepo.GetByAppUserIdAsync(request.AppUserId, cancellationToken);
        if (player is not null)
            return Result<MyStatusResult>.Ok(new MyStatusResult("approved", player.Id, role));

        return Result<MyStatusResult>.Ok(new MyStatusResult("none", null, role));
    }
}
