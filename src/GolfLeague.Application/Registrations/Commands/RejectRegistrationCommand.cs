using GolfLeague.Application.Common;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Registrations.Commands;

public sealed record RejectRegistrationCommand(
    int RegistrationId,
    string? Reason,
    string AdminUserId) : IRequest<Result<bool>>, IAmAuditableCommand
{
    public string UserId => AdminUserId;
}

public sealed class RejectRegistrationCommandHandler
    : IRequestHandler<RejectRegistrationCommand, Result<bool>>
{
    private readonly IRegistrationRepository _repo;

    public RejectRegistrationCommandHandler(IRegistrationRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<bool>> Handle(
        RejectRegistrationCommand request,
        CancellationToken cancellationToken)
    {
        var reg = await _repo.GetByIdAsync(request.RegistrationId, cancellationToken);
        if (reg is null)
            return Result<bool>.Fail("Registration not found.");

        if (reg.Status != RegistrationStatus.Pending)
            return Result<bool>.Fail("Only pending registrations can be rejected.");

        reg.Status = RegistrationStatus.Rejected;
        reg.ReviewedAt = DateTime.UtcNow;
        reg.ReviewedByUserId = request.AdminUserId;
        reg.RejectionReason = request.Reason;
        await _repo.UpdateAsync(reg, cancellationToken);

        return Result<bool>.Ok(true);
    }
}
