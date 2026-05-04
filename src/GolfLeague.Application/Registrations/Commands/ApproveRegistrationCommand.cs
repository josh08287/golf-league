using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Registrations.Commands;

public sealed record ApproveRegistrationCommand(
    int RegistrationId,
    string AdminUserId) : IRequest<Result<PlayerDto>>, IAmAuditableCommand
{
    public string UserId => AdminUserId;
}

public sealed class ApproveRegistrationCommandHandler
    : IRequestHandler<ApproveRegistrationCommand, Result<PlayerDto>>
{
    private readonly IRegistrationRepository _repo;
    private readonly IPlayerRepository _playerRepo;
    private readonly IHandicapRepository _handicapRepo;

    public ApproveRegistrationCommandHandler(
        IRegistrationRepository repo,
        IPlayerRepository playerRepo,
        IHandicapRepository handicapRepo)
    {
        _repo = repo;
        _playerRepo = playerRepo;
        _handicapRepo = handicapRepo;
    }

    public async Task<Result<PlayerDto>> Handle(
        ApproveRegistrationCommand request,
        CancellationToken cancellationToken)
    {
        var reg = await _repo.GetByIdAsync(request.RegistrationId, cancellationToken);
        if (reg is null)
            return Result<PlayerDto>.Fail("Registration not found.");

        if (reg.Status != RegistrationStatus.Pending)
            return Result<PlayerDto>.Fail("Only pending registrations can be approved.");

        // Guard against duplicate player (race condition)
        var duplicate = await _playerRepo.GetByEntraObjectIdAsync(reg.EntraObjectId, cancellationToken);
        if (duplicate is not null)
            return Result<PlayerDto>.Fail("A player with this identity already exists.");

        var player = new Player
        {
            FirstName = reg.FirstName,
            LastName = reg.LastName,
            Email = reg.Email,
            EntraObjectId = reg.EntraObjectId,
            IsActive = true
        };

        await _playerRepo.AddAsync(player, cancellationToken);

        // Create placeholder handicap (0.0); admin sets the real value in Player Detail
        var handicap = new Handicap
        {
            PlayerId = player.Id,
            HandicapIndex = 0.0,
            EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Source = HandicapSource.Initial
        };
        await _handicapRepo.AddAsync(handicap, cancellationToken);

        reg.Status = RegistrationStatus.Approved;
        reg.ReviewedAt = DateTime.UtcNow;
        reg.ReviewedByUserId = request.AdminUserId;
        reg.PlayerId = player.Id;
        await _repo.UpdateAsync(reg, cancellationToken);

        return Result<PlayerDto>.Ok(new PlayerDto(
            player.Id, player.FullName, player.Email, player.IsActive,
            0.0, null, null));
    }
}
