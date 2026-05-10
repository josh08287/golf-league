using GolfLeague.Application.Common;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Registrations.Queries;

public sealed record GetMyStatusQuery(string EntraObjectId) : IRequest<Result<MyStatusResult>>;

/// <summary>
/// Status values:
///   "approved"  — user is a linked, active player
///   "none"      — signed in but no invite / player record (should not normally happen in invite-only model)
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

        return Result<MyStatusResult>.Ok(new MyStatusResult("none", null, "player"));
    }
}
