using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Registrations.Commands;

/// <summary>
/// Called when an invited user signs in and confirms their details.
/// Creates the Player record and marks the invite accepted.
/// </summary>
public sealed record AcceptInviteCommand(
    string Token,
    string EntraObjectId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone) : IRequest<Result<PlayerDto>>;

public sealed class AcceptInviteCommandHandler : IRequestHandler<AcceptInviteCommand, Result<PlayerDto>>
{
    private readonly IInviteRepository _inviteRepo;
    private readonly IPlayerRepository _playerRepo;
    private readonly IHandicapRepository _handicapRepo;

    public AcceptInviteCommandHandler(
        IInviteRepository inviteRepo,
        IPlayerRepository playerRepo,
        IHandicapRepository handicapRepo)
    {
        _inviteRepo = inviteRepo;
        _playerRepo = playerRepo;
        _handicapRepo = handicapRepo;
    }

    public async Task<Result<PlayerDto>> Handle(AcceptInviteCommand request, CancellationToken cancellationToken)
    {
        var invite = await _inviteRepo.GetByTokenAsync(request.Token, cancellationToken);

        if (invite is null)
            return Result<PlayerDto>.Fail("Invite not found.");

        if (invite.Status == InviteStatus.Revoked)
            return Result<PlayerDto>.Fail("This invite has been revoked.");

        if (invite.Status == InviteStatus.Accepted)
            return Result<PlayerDto>.Fail("This invite has already been used.");

        if (invite.ExpiresAt < DateTime.UtcNow)
            return Result<PlayerDto>.Fail("This invite has expired. Please ask the admin to send a new one.");

        // Guard against re-accepting with the same Entra identity
        var existing = await _playerRepo.GetByEntraObjectIdAsync(request.EntraObjectId, cancellationToken);
        if (existing is not null)
            return Result<PlayerDto>.Fail("Your account is already linked to a player profile.");

        var player = new Player
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            EntraObjectId = request.EntraObjectId,
            IsActive = true
        };

        await _playerRepo.AddAsync(player, cancellationToken);

        // Placeholder handicap — admin sets the real value in Player Detail
        await _handicapRepo.AddAsync(new Handicap
        {
            PlayerId = player.Id,
            HandicapIndex = 0.0,
            EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Source = HandicapSource.Initial
        }, cancellationToken);

        invite.Status = InviteStatus.Accepted;
        invite.AcceptedAt = DateTime.UtcNow;
        invite.AcceptedByEntraObjectId = request.EntraObjectId;
        invite.PlayerId = player.Id;
        await _inviteRepo.UpdateAsync(invite, cancellationToken);

        return Result<PlayerDto>.Ok(new PlayerDto(
            player.Id, player.FullName, player.Email, player.IsActive, 0.0, null, null));
    }
}
