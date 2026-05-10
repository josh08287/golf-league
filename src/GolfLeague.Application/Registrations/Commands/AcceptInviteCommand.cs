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
    private readonly IEntraRoleService _entraRoleService;
    private readonly ILogger<AcceptInviteCommandHandler> _logger;

    public AcceptInviteCommandHandler(
        IInviteRepository inviteRepo,
        IPlayerRepository playerRepo,
        IHandicapRepository handicapRepo,
        IEntraRoleService entraRoleService,
        ILogger<AcceptInviteCommandHandler> logger)
    {
        _inviteRepo = inviteRepo;
        _playerRepo = playerRepo;
        _handicapRepo = handicapRepo;
        _entraRoleService = entraRoleService;
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
            IsActive = true,
            Role = invite.Role
        };

        await _playerRepo.AddAsync(player, cancellationToken);

        // Ensure user exists in Entra ID (important for external identity providers like Google)
        // The user may have signed in via federation but not be immediately queryable in the tenant
        // Pass the EntraObjectId from the JWT token so we can look them up directly
        var ensureUserResult = await _entraRoleService.EnsureUserExistsAsync(
            request.Email,
            $"{request.FirstName} {request.LastName}",
            request.EntraObjectId,
            cancellationToken);

        if (!ensureUserResult.IsSuccess)
        {
            _logger.LogError(
                "Failed to verify user exists in Entra ID: {Error}",
                ensureUserResult.Error);
            return Result<PlayerDto>.Fail($"User verification failed: {ensureUserResult.Error}");
        }

        var userObjectId = ensureUserResult.Value!;
        _logger.LogInformation(
            "User {Email} verified in Entra ID with object ID {UserId}",
            request.Email,
            userObjectId);

        // Assign the role in Entra ID (source of truth for authorization)
        var roleResult = await _entraRoleService.AssignRoleAsync(
            userObjectId,
            invite.Role.ToString().ToLowerInvariant(),
            cancellationToken);

        if (!roleResult.IsSuccess)
        {
            _logger.LogError(
                "Failed to assign role {Role} to user {UserId} in Entra ID: {Error}",
                invite.Role,
                userObjectId,
                roleResult.Error);
            // Return error so frontend knows role assignment failed
            return Result<PlayerDto>.Fail($"Role assignment failed: {roleResult.Error}");
        }

        _logger.LogInformation(
            "Successfully assigned role {Role} to user {UserId} in Entra ID",
            invite.Role,
            userObjectId);

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
            player.Id, player.FullName, player.Email, player.IsActive, 0.0, null, null, player.Role.ToString().ToLowerInvariant()));
    }
}
