using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Registrations.Commands;

public sealed record SubmitRegistrationCommand(
    string EntraObjectId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone) : IRequest<Result<RegistrationDto>>;

public sealed class SubmitRegistrationCommandHandler
    : IRequestHandler<SubmitRegistrationCommand, Result<RegistrationDto>>
{
    private readonly IRegistrationRepository _repo;
    private readonly IPlayerRepository _playerRepo;

    public SubmitRegistrationCommandHandler(
        IRegistrationRepository repo,
        IPlayerRepository playerRepo)
    {
        _repo = repo;
        _playerRepo = playerRepo;
    }

    public async Task<Result<RegistrationDto>> Handle(
        SubmitRegistrationCommand request,
        CancellationToken cancellationToken)
    {
        // Already a player
        var existing = await _playerRepo.GetByEntraObjectIdAsync(request.EntraObjectId, cancellationToken);
        if (existing is not null)
            return Result<RegistrationDto>.Fail("You are already registered as a player.");

        // Already has a pending/rejected request
        var existingReg = await _repo.GetByEntraObjectIdAsync(request.EntraObjectId, cancellationToken);
        if (existingReg is not null)
        {
            if (existingReg.Status == RegistrationStatus.Pending)
                return Result<RegistrationDto>.Fail("A registration request is already pending.");

            if (existingReg.Status == RegistrationStatus.Rejected)
            {
                // Allow re-submission after rejection — reset and re-queue
                existingReg.Status = RegistrationStatus.Pending;
                existingReg.RequestedAt = DateTime.UtcNow;
                existingReg.ReviewedAt = null;
                existingReg.ReviewedByUserId = null;
                existingReg.RejectionReason = null;
                existingReg.FirstName = request.FirstName;
                existingReg.LastName = request.LastName;
                existingReg.Email = request.Email;
                existingReg.Phone = request.Phone;
                await _repo.UpdateAsync(existingReg, cancellationToken);
                return Result<RegistrationDto>.Ok(ToDto(existingReg));
            }
        }

        var registration = new PlayerRegistration
        {
            EntraObjectId = request.EntraObjectId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Phone = request.Phone,
            Status = RegistrationStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };

        await _repo.AddAsync(registration, cancellationToken);
        return Result<RegistrationDto>.Ok(ToDto(registration));
    }

    private static RegistrationDto ToDto(PlayerRegistration r) =>
        new(r.Id, r.EntraObjectId, r.FirstName, r.LastName, r.Email,
            r.Phone, r.Status.ToString(), r.RequestedAt, r.ReviewedAt,
            r.RejectionReason, r.PlayerId);
}
