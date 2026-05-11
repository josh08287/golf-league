using System.Security.Cryptography;
using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
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
    string Role = "player") : IRequest<Result<CreateInvitesResult>>, IAmAuditableCommand
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

    public CreateInvitesCommandHandler(IInviteRepository inviteRepo, IPlayerRepository playerRepo, IEmailService emailService)
    {
        _inviteRepo = inviteRepo;
        _playerRepo = playerRepo;
        _emailService = emailService;
    }

    public async Task<Result<CreateInvitesResult>> Handle(CreateInvitesCommand request, CancellationToken cancellationToken)
    {
        var created = new List<PlayerInvite>();
        var skipped = new List<string>();

        var normalised = request.Emails
            .Select(e => e.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        foreach (var email in normalised)
        {
            // Skip if a pending invite already exists for this email
            if (await _inviteRepo.PendingInviteExistsForEmailAsync(email, cancellationToken))
            {
                skipped.Add(email);
                continue;
            }

            // Skip if they're already a player. Players without an email
            // can't collide so we just skip the null check.
            var allPlayers = await _playerRepo.GetAllActiveAsync(cancellationToken);
            if (allPlayers.Any(p => p.Email is not null && p.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
            {
                skipped.Add(email);
                continue;
            }

            created.Add(new PlayerInvite
            {
                Email = email,
                Token = GenerateToken(),
                Status = InviteStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(request.ExpiryDays),
                InvitedByUserId = request.AdminUserId,
                Role = Enum.TryParse<Domain.Enums.PlayerRole>(request.Role, true, out var role)
                    ? role
                    : Domain.Enums.PlayerRole.Player
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
