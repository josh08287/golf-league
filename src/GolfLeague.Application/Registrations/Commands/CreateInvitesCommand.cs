using System.Security.Cryptography;
using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Interfaces;
using GolfLeague.Application.Registrations.Queries;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Registrations.Commands;

/// <summary>
/// Creates one or more invites (single or bulk). Skips emails that already have a pending invite or
/// an existing player record.
/// </summary>
public sealed record CreateInvitesCommand(
    IReadOnlyList<string> Emails,
    string AdminUserId,
    string BaseUrl,
    int ExpiryDays = 7,
    string Role = "player",
    int? PreLinkedPlayerId = null) : IRequest<Result<CreateInvitesResult>>, IAmAuditableCommand
{
    public string UserId => AdminUserId;
}

public sealed record CreateInvitesResult(
    List<InviteDto> Created,
    List<string> Skipped);

public sealed class CreateInvitesCommandHandler : IRequestHandler<CreateInvitesCommand, Result<CreateInvitesResult>>
{
    private readonly IInviteRepository _inviteRepo;
    private readonly IPlayerRepository _playerRepo;
    private readonly IEmailService _emailService;
    private readonly ILeagueContext _leagueContext;

    public CreateInvitesCommandHandler(IInviteRepository inviteRepo, IPlayerRepository playerRepo, IEmailService emailService, ILeagueContext leagueContext)
    {
        _inviteRepo = inviteRepo;
        _playerRepo = playerRepo;
        _emailService = emailService;
        _leagueContext = leagueContext;
    }

    public async Task<Result<CreateInvitesResult>> Handle(CreateInvitesCommand request, CancellationToken cancellationToken)
    {
        if (_leagueContext.LeagueId is null)
            return Result<CreateInvitesResult>.Fail("No league context.");

        var leagueId = _leagueContext.LeagueId.Value;
        var created = new List<PlayerInvite>();
        var skipped = new List<string>();

        // Pre-link only makes sense for a single invite — a single Player
        // can only be attached to one AppUser.
        if (request.PreLinkedPlayerId is not null && request.Emails.Count != 1)
        {
            return Result<CreateInvitesResult>.Fail(
                "Pre-attaching a player only works for a single-email invite.");
        }

        // When a player is pre-linked, use their profile email as the canonical
        // invite email so the signup flow can find the invite by email regardless
        // of what the admin typed.
        Player? prelinkPlayer = null;
        if (request.PreLinkedPlayerId is int prelinkId)
        {
            prelinkPlayer = await _playerRepo.GetByIdAsync(prelinkId, cancellationToken);
            if (prelinkPlayer is null)
                return Result<CreateInvitesResult>.Fail("The selected player no longer exists.");
            if (prelinkPlayer.AppUserId is not null)
                return Result<CreateInvitesResult>.Fail("The selected player is already linked to a user account.");
        }

        var normalised = (prelinkPlayer?.Email is not null
                ? new[] { prelinkPlayer.Email.Trim().ToLowerInvariant() }
                : request.Emails.Select(e => e.Trim().ToLowerInvariant()))
            .Distinct()
            .ToList();

        var allPlayers = await _playerRepo.GetAllActiveAsync(cancellationToken);

        foreach (var email in normalised)
        {
            // Skip if a pending invite already exists for this email.
            if (await _inviteRepo.PendingInviteExistsForEmailAsync(email, cancellationToken))
            {
                skipped.Add(email);
                continue;
            }

            // Skip if there's already a linked player with this email (and it's not the pre-linked one).
            if (allPlayers.Any(p => p.Email is not null
                    && p.Email.Equals(email, StringComparison.OrdinalIgnoreCase)
                    && p.AppUserId is not null
                    && p.Id != request.PreLinkedPlayerId))
            {
                skipped.Add(email);
                continue;
            }

            created.Add(new PlayerInvite
            {
                LeagueId = leagueId,
                Email = email,
                Token = GenerateToken(),
                Status = InviteStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(request.ExpiryDays),
                InvitedByUserId = request.AdminUserId,
                Role = Enum.TryParse<Domain.Enums.PlayerRole>(request.Role, true, out var role)
                    ? role
                    : Domain.Enums.PlayerRole.Player,
                PreLinkedPlayerId = request.PreLinkedPlayerId,
            });
        }

        if (created.Count > 0)
            await _inviteRepo.AddRangeAsync(created, cancellationToken);

        var dtos = created
            .Select(i => GetInvitesQueryHandler.ToDto(i, request.BaseUrl))
            .ToList();

        foreach (var invite in dtos)
            await _emailService.SendInviteAsync(invite.Email, invite.InviteLink, invite.ExpiresAt, cancellationToken);

        return Result<CreateInvitesResult>.Ok(new CreateInvitesResult(dtos, skipped));
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
