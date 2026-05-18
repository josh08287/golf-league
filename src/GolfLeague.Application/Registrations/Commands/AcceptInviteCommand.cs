using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Application.Registrations.Commands;

/// <summary>
/// Legacy "I'm already signed in, claim this invite" flow. The main
/// registration paths (POST /auth/register and the social callback) consume
/// the invite themselves now. This handler still exists for the case where
/// a user already has an AppUser (e.g. previously signed in via social with
/// an invite) and the admin issues them a *new* invite to attach a Player
/// profile.
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
    private readonly IUserRoleService _roleService;
    private readonly ILogger<AcceptInviteCommandHandler> _logger;

    public AcceptInviteCommandHandler(
        IInviteRepository inviteRepo,
        IPlayerRepository playerRepo,
        IHandicapRepository handicapRepo,
        IUserRoleService roleService,
        ILogger<AcceptInviteCommandHandler> logger)
    {
        _inviteRepo = inviteRepo;
        _playerRepo = playerRepo;
        _handicapRepo = handicapRepo;
        _roleService = roleService;
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

        Player player;
        var existing = await _playerRepo.GetByAppUserIdAsync(request.AppUserId, cancellationToken);

        if (invite.PreLinkedPlayerId is int preId)
        {
            var prelinkPlayer = await _playerRepo.GetByIdAsync(preId, cancellationToken);
            if (prelinkPlayer is null)
                return Result<PlayerDto>.Fail("The pre-linked player profile no longer exists.");

            if (prelinkPlayer.AppUserId is not null && prelinkPlayer.AppUserId != request.AppUserId)
                return Result<PlayerDto>.Fail("This player profile is already linked to another account.");

            if (existing is not null && existing.Id != prelinkPlayer.Id)
                return Result<PlayerDto>.Fail("Your account is already linked to a different player profile.");

            // Update name/email even if already linked (idempotent re-accept).
            prelinkPlayer.AppUserId = request.AppUserId;
            prelinkPlayer.FirstName = request.FirstName;
            prelinkPlayer.LastName = request.LastName;
            prelinkPlayer.Email = request.Email;

            await _playerRepo.UpdateAsync(prelinkPlayer, cancellationToken);
            player = prelinkPlayer;
        }
        else
        {
            if (existing is not null)
            {
                // User already has a player profile — accept idempotently (e.g. role-only invite,
                // or a retry after a partial failure). Just add the role below.
                player = existing;
            }
            else
            {
                // Look for an admin-pre-created Player row by email and adopt it.
                var byEmail = await _playerRepo.GetByEmailAsync(request.Email, cancellationToken);

                if (byEmail is not null && byEmail.AppUserId is null)
                {
                    byEmail.AppUserId = request.AppUserId;
                    byEmail.FirstName = request.FirstName;
                    byEmail.LastName = request.LastName;
                    byEmail.Email = request.Email;
                    await _playerRepo.UpdateAsync(byEmail, cancellationToken);
                    player = byEmail;
                }
                else
                {
                    player = new Player
                    {
                        LeagueId = invite.LeagueId,
                        FirstName = request.FirstName,
                        LastName = request.LastName,
                        Email = request.Email,
                        IsActive = true,
                        AppUserId = request.AppUserId,
                    };

                    await _playerRepo.AddAsync(player, cancellationToken);

                    await _handicapRepo.AddAsync(new Handicap
                    {
                        PlayerId = player.Id,
                        HandicapIndex = 0.0,
                        EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
                        Source = HandicapSource.Initial
                    }, cancellationToken);
                }
            }
        }

        // Add the invite's role on top of whatever the user already has.
        var currentRoles = await _roleService.GetRolesAsync(request.AppUserId, cancellationToken);
        var inviteRole = invite.Role.ToString().ToLowerInvariant();
        if (!currentRoles.Contains(inviteRole))
        {
            var combined = currentRoles.Append(inviteRole).ToList();
            await _roleService.SetRolesAsync(request.AppUserId, combined, cancellationToken);
        }

        _logger.LogInformation(
            "Linked player {PlayerId} to AppUser {UserId} with role {Role}",
            player.Id,
            request.AppUserId,
            invite.Role);

        invite.Status = InviteStatus.Accepted;
        invite.AcceptedAt = DateTime.UtcNow;
        invite.AcceptedByAppUserId = request.AppUserId;
        invite.PlayerId = player.Id;
        await _inviteRepo.UpdateAsync(invite, cancellationToken);

        var refreshedRoles = await _roleService.GetRolesAsync(request.AppUserId, cancellationToken);
        return Result<PlayerDto>.Ok(new PlayerDto(
            player.Id, player.FullName, player.Email, player.IsActive, 0.0, null, null, refreshedRoles));
    }
}
