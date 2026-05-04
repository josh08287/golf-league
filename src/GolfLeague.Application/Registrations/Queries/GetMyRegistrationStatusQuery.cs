using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Registrations.Queries;

public sealed record GetMyRegistrationStatusQuery(string EntraObjectId) : IRequest<Result<MyStatusDto>>;

public sealed record MyStatusDto(
    string Status,        // "none" | "pending" | "approved" | "rejected"
    int? PlayerId,
    string? RejectionReason);

public sealed class GetMyRegistrationStatusQueryHandler
    : IRequestHandler<GetMyRegistrationStatusQuery, Result<MyStatusDto>>
{
    private readonly IPlayerRepository _playerRepo;
    private readonly IRegistrationRepository _registrationRepo;

    public GetMyRegistrationStatusQueryHandler(
        IPlayerRepository playerRepo,
        IRegistrationRepository registrationRepo)
    {
        _playerRepo = playerRepo;
        _registrationRepo = registrationRepo;
    }

    public async Task<Result<MyStatusDto>> Handle(
        GetMyRegistrationStatusQuery request,
        CancellationToken cancellationToken)
    {
        // Already a fully linked player?
        var player = await _playerRepo.GetByEntraObjectIdAsync(request.EntraObjectId, cancellationToken);
        if (player is not null)
            return Result<MyStatusDto>.Ok(new MyStatusDto("approved", player.Id, null));

        // Pending or rejected registration?
        var reg = await _registrationRepo.GetByEntraObjectIdAsync(request.EntraObjectId, cancellationToken);
        if (reg is not null)
            return Result<MyStatusDto>.Ok(new MyStatusDto(reg.Status.ToString().ToLowerInvariant(), reg.PlayerId, reg.RejectionReason));

        return Result<MyStatusDto>.Ok(new MyStatusDto("none", null, null));
    }
}
