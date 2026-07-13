using GolfLeague.Application.Common;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Registrations.Commands;

public sealed record DeleteInviteCommand(
    int InviteId,
    string AdminUserId) : IRequest<Result<bool>>, IAmAuditableCommand
{
    public string UserId => AdminUserId;
    public string AuditEntityType => "Invite";
    public string AuditEntityId => InviteId.ToString();
}

public sealed class DeleteInviteCommandHandler : IRequestHandler<DeleteInviteCommand, Result<bool>>
{
    private readonly IInviteRepository _repo;

    public DeleteInviteCommandHandler(IInviteRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<bool>> Handle(DeleteInviteCommand request, CancellationToken cancellationToken)
    {
        var invite = await _repo.GetByIdAsync(request.InviteId, cancellationToken);
        if (invite is null)
            return Result<bool>.Fail("Invite not found.");

        // Block deletion of pending non-expired invites — revoke first
        var isExpired = invite.Status == InviteStatus.Pending && invite.ExpiresAt < DateTime.UtcNow;
        if (invite.Status == InviteStatus.Pending && !isExpired)
            return Result<bool>.Fail("Revoke the invite before deleting it.");

        await _repo.DeleteAsync(invite, cancellationToken);
        return Result<bool>.Ok(true);
    }
}
