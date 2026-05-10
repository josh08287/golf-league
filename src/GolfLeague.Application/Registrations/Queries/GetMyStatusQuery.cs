using GolfLeague.Application.Common;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Registrations.Queries;

/// <summary>
/// Query to get the current user's status. TokenRole comes from Entra ID app roles claim.
/// </summary>
public sealed record GetMyStatusQuery(string EntraObjectId, string TokenRole) : IRequest<Result<MyStatusResult>>;

/// <summary>
/// Status values:
///   "approved"  — user is a linked, active player
///   "none"      — signed in but no invite / player record (admin-only users)
/// </summary>
public sealed record MyStatusResult(string Status, int? PlayerId, string Role);

public sealed class GetMyStatusQueryHandler : IRequestHandler<GetMyStatusQuery, Result<MyStatusResult>>
{
    private readonly IPlayerRepository _playerRepo;

    public GetMyStatusQueryHandler(IPlayerRepository playerRepo)
    {
        _playerRepo = playerRepo;
    }

    public async Task<Result<MyStatusResult>> Handle(GetMyStatusQuery request, CancellationToken cancellationToken)
    {
        var player = await _playerRepo.GetByEntraObjectIdAsync(request.EntraObjectId, cancellationToken);
        if (player is not null)
            return Result<MyStatusResult>.Ok(new MyStatusResult(
                "approved",
                player.Id,
                player.Role.ToString().ToLowerInvariant()));

        // For non-players (e.g., admin-only accounts), use the role from the Entra ID token
        return Result<MyStatusResult>.Ok(new MyStatusResult("none", null, request.TokenRole.ToLowerInvariant()));
    }
}
