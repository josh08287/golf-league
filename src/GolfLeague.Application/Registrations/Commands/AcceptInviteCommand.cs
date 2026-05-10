using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Application.Registrations.Commands;

/// <summary>
/// Called when an invited user signs in and confirms their details.
/// Creates the Player record, links it to the calling AppUser, and marks
/// the invite accepted. The user's role is set on the AppUser to match
/// the invite.
/// </summary>
public sealed record AcceptInviteCommand(
    string Token,
    Guid AppUserId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone) : IRequest<Result<PlayerDto>>;

public sealed class AcceptInviteCommandHandler : IRequestHandler<AcceptInviteCommand, Result<PlayerDto>>
{
    private readonly IInviteRepository _inviteRepo;
    private readonly IPlayerRepository _playerRepo;
    private readonly IHandicapRepository _handicapRepo;
    private readonly IAppUserRepository _appUserRepo;
    private readonly ILogger<AcceptInviteCommandHandler> _logger;

    public AcceptInviteCommandHandler(
        IInviteRepository inviteRepo,
        IPlayerRepository playerRepo,
        IHandicapRepository handicapRepo,
        IAppUserRepository appUserRepo,
        ILogger<AcceptInviteCommandHandler> logger)
    {
        _inviteRepo = inviteRepo;
        _playerRepo = playerRepo;
        _handicapRepo = handicapRepo;
        _appUserRepo = appUserRepo;
        _logger = logger;
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

        // Guard against re-accepting with the same identity
        var existing = await _playerRepo.GetByAppUserIdAsync(request.AppUserId, cancellationToken);
        if (existing is not null)
            return Result<PlayerDto>.Fail("Your account is already linked to a player profile.");

        var player = new Player
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            IsActive = true,
            AppUserId = request.AppUserId,
        };

        await _playerRepo.AddAsync(player, cancellationToken);

        // Assign the invite's role to the AppUser (authoritative for authorization).
        await _appUserRepo.UpdateRoleAsync(request.AppUserId, invite.Role, cancellationToken);

        _logger.LogInformation(
            "Linked player {PlayerId} to AppUser {UserId} with role {Role}",
            player.Id,
            request.AppUserId,
            invite.Role);

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
        invite.AcceptedByAppUserId = request.AppUserId;
        invite.PlayerId = player.Id;
        await _inviteRepo.UpdateAsync(invite, cancellationToken);

        return Result<PlayerDto>.Ok(new PlayerDto(
            player.Id, player.FullName, player.Email, player.IsActive, 0.0, null, null,
            invite.Role.ToString().ToLowerInvariant()));
    }
}
