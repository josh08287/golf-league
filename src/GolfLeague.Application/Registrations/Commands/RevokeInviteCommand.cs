using GolfLeague.Application.Common;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Registrations.Commands;

public sealed record RevokeInviteCommand(
    int InviteId,
    string AdminUserId) : IRequest<Result<bool>>, IAmAuditableCommand
{
    public string UserId => AdminUserId;
}

public sealed class RevokeInviteCommandHandler : IRequestHandler<RevokeInviteCommand, Result<bool>>
{
    private readonly IInviteRepository _repo;

    public RevokeInviteCommandHandler(IInviteRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<bool>> Handle(RevokeInviteCommand request, CancellationToken cancellationToken)
    {
        var invite = await _repo.GetByIdAsync(request.InviteId, cancellationToken);
        if (invite is null)
            return Result<bool>.Fail("Invite not found.");

        if (invite.Status != InviteStatus.Pending)
            return Result<bool>.Fail("Only pending invites can be revoked.");

        invite.Status = InviteStatus.Revoked;
        await _repo.UpdateAsync(invite, cancellationToken);
        return Result<bool>.Ok(true);
    }
}
