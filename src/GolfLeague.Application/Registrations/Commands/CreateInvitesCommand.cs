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
/// an existing player record in this league. When the email already belongs to an AppUser (e.g. a
/// member of another league), the account is linked immediately — a new Player/LeagueMembership is
/// created for this league and no invite email is sent, since there's nothing for that user to accept.
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
    List<string> Skipped,
    List<string> AutoLinked);

public sealed class CreateInvitesCommandHandler : IRequestHandler<CreateInvitesCommand, Result<CreateInvitesResult>>
{
    private readonly IInviteRepository _inviteRepo;
    private readonly IPlayerRepository _playerRepo;
    private readonly IAppUserRepository _appUserRepo;
    private readonly ILeagueRepository _leagueRepo;
    private readonly IHandicapRepository _handicapRepo;
    private readonly IUserRoleService _roleService;
    private readonly IEmailService _emailService;
    private readonly ILeagueContext _leagueContext;

    public CreateInvitesCommandHandler(
        IInviteRepository inviteRepo,
        IPlayerRepository playerRepo,
        IAppUserRepository appUserRepo,
        ILeagueRepository leagueRepo,
        IHandicapRepository handicapRepo,
        IUserRoleService roleService,
        IEmailService emailService,
        ILeagueContext leagueContext)
    {
        _inviteRepo = inviteRepo;
        _playerRepo = playerRepo;
        _appUserRepo = appUserRepo;
        _leagueRepo = leagueRepo;
        _handicapRepo = handicapRepo;
        _roleService = roleService;
        _emailService = emailService;
        _leagueContext = leagueContext;
    }

    public async Task<Result<CreateInvitesResult>> Handle(CreateInvitesCommand request, CancellationToken cancellationToken)
    {
        if (_leagueContext.LeagueId is null)
            return Result<CreateInvitesResult>.Fail("No league context.");

        var leagueId = _leagueContext.LeagueId.Value;
        var created = new List<PlayerInvite>();
        var autoLinked = new List<PlayerInvite>();
        var skipped = new List<string>();

        var role = Enum.TryParse<PlayerRole>(request.Role, true, out var parsedRole)
            ? parsedRole
            : PlayerRole.Player;

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

        // A pre-linked player with no email on file should show the invite's
        // address on their profile until they accept and it's replaced by
        // their account email. Don't touch it if the admin already set one.
        if (prelinkPlayer is not null && prelinkPlayer.Email is null && normalised.Count == 1)
        {
            prelinkPlayer.Email = normalised[0];
            await _playerRepo.UpdateAsync(prelinkPlayer, cancellationToken);
        }

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

            // The email already belongs to a login (e.g. a member of another
            // league). There's nothing for them to "accept" by email — link
            // them into this league immediately.
            var existingUser = await _appUserRepo.GetByEmailAsync(email, cancellationToken);
            if (existingUser is not null)
            {
                var invite = await AutoLinkExistingUserAsync(
                    existingUser, email, role, prelinkPlayer, request, leagueId, cancellationToken);
                autoLinked.Add(invite);
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
                Role = role,
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

        var autoLinkedEmails = autoLinked.Select(i => i.Email).ToList();

        return Result<CreateInvitesResult>.Ok(new CreateInvitesResult(dtos, skipped, autoLinkedEmails));
    }

    /// <summary>
    /// Links an AppUser that already exists (from another league) into this
    /// league: creates/adopts a Player row, grants league membership and role,
    /// seeds an initial handicap for a freshly-created Player, and records an
    /// already-Accepted PlayerInvite for audit history. No email is sent.
    /// </summary>
    private async Task<PlayerInvite> AutoLinkExistingUserAsync(
        AppUser existingUser,
        string email,
        PlayerRole role,
        Player? prelinkPlayer,
        CreateInvitesCommand request,
        int leagueId,
        CancellationToken cancellationToken)
    {
        Player player;
        if (prelinkPlayer is not null)
        {
            prelinkPlayer.AppUserId = existingUser.Id;
            prelinkPlayer.Email = email;
            await _playerRepo.UpdateAsync(prelinkPlayer, cancellationToken);
            player = prelinkPlayer;
        }
        else
        {
            // Reuse the name from any existing profile for this user (other
            // leagues) since invite creation collects no name — the admin can
            // edit it afterward if the new league needs a different display.
            var otherProfiles = await _playerRepo.GetAllByAppUserIdAsync(existingUser.Id, cancellationToken);
            var nameSource = otherProfiles.FirstOrDefault();

            player = new Player
            {
                LeagueId = leagueId,
                FirstName = nameSource?.FirstName ?? string.Empty,
                LastName = nameSource?.LastName ?? string.Empty,
                Email = email,
                IsActive = true,
                AppUserId = existingUser.Id,
            };
            await _playerRepo.AddAsync(player, cancellationToken);
            await _handicapRepo.AddAsync(new Handicap
            {
                PlayerId = player.Id,
                HandicapIndex = 0,
                EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Source = HandicapSource.Initial,
            }, cancellationToken);
        }

        await _leagueRepo.AddMembershipAsync(new LeagueMembership
        {
            LeagueId = leagueId,
            UserId = existingUser.Id,
            Role = role,
            JoinedAt = DateTime.UtcNow,
        }, cancellationToken);

        var roleName = role.ToString().ToLowerInvariant();
        var currentRoles = await _roleService.GetRolesAsync(existingUser.Id, cancellationToken);
        if (!currentRoles.Contains(roleName))
            await _roleService.SetRolesAsync(existingUser.Id, currentRoles.Append(roleName).ToList(), cancellationToken);

        var invite = new PlayerInvite
        {
            LeagueId = leagueId,
            Email = email,
            Token = GenerateToken(),
            Status = InviteStatus.Accepted,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(request.ExpiryDays),
            InvitedByUserId = request.AdminUserId,
            Role = role,
            PreLinkedPlayerId = request.PreLinkedPlayerId,
            AcceptedAt = DateTime.UtcNow,
            AcceptedByAppUserId = existingUser.Id,
            PlayerId = player.Id,
        };
        await _inviteRepo.AddAsync(invite, cancellationToken);
        return invite;
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
