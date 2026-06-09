using GolfLeague.Application.Common;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Admin;

public sealed record SendBroadcastMessageCommand(
    string Subject,
    string Body,
    /// <summary>
    /// Player IDs to include. When null or empty, falls back to all active
    /// players in the current half who have an email address.
    /// </summary>
    IReadOnlyList<int>? PlayerIds,
    /// <summary>Ad-hoc email addresses added directly by the admin.</summary>
    IReadOnlyList<string>? AdHocEmails,
    string UserId) : IRequest<Result<BroadcastMessageResultDto>>, IAmAuditableCommand;

public sealed record BroadcastMessageResultDto(int Sent, int Skipped, IReadOnlyList<string> SkippedNames);

public sealed class SendBroadcastMessageCommandHandler
    : IRequestHandler<SendBroadcastMessageCommand, Result<BroadcastMessageResultDto>>
{
    private readonly IPlayerRepository _players;
    private readonly ILeagueRepository _leagues;
    private readonly ILeagueContext _leagueContext;
    private readonly IEmailService _email;

    public SendBroadcastMessageCommandHandler(
        IPlayerRepository players,
        ILeagueRepository leagues,
        ILeagueContext leagueContext,
        IEmailService email)
    {
        _players = players;
        _leagues = leagues;
        _leagueContext = leagueContext;
        _email = email;
    }

    public async Task<Result<BroadcastMessageResultDto>> Handle(
        SendBroadcastMessageCommand request,
        CancellationToken cancellationToken)
    {
        if (_leagueContext.LeagueId is null)
            return Result<BroadcastMessageResultDto>.Fail("No league context.");

        var league = await _leagues.GetByIdAsync(_leagueContext.LeagueId.Value, cancellationToken);
        var leagueName = league?.Name ?? "Golf League";

        // Resolve player-based recipients.
        IReadOnlyList<(string Email, string Name)> playerRecipients;
        if (request.PlayerIds is { Count: > 0 })
        {
            var resolved = new List<(string, string)>();
            foreach (var id in request.PlayerIds)
            {
                var p = await _players.GetByIdAsync(id, cancellationToken);
                if (p?.Email is not null)
                    resolved.Add((p.Email, p.FullName));
            }
            playerRecipients = resolved;
        }
        else
        {
            var active = await _players.GetAllActiveAsync(cancellationToken);
            playerRecipients = active
                .Where(p => p.Email is not null)
                .Select(p => (p.Email!, p.FullName))
                .ToList();
        }

        // Combine with ad-hoc addresses, deduplicating by email (case-insensitive).
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allRecipients = new List<(string Email, string Name)>();

        foreach (var (email, name) in playerRecipients)
        {
            if (seen.Add(email))
                allRecipients.Add((email, name));
        }

        foreach (var adHoc in request.AdHocEmails ?? [])
        {
            var trimmed = adHoc.Trim();
            if (!string.IsNullOrEmpty(trimmed) && seen.Add(trimmed))
                allRecipients.Add((trimmed, trimmed));
        }

        var sent = 0;
        var skippedNames = new List<string>();

        foreach (var (email, name) in allRecipients)
        {
            try
            {
                await _email.SendBroadcastMessageAsync(
                    email,
                    leagueName,
                    request.Subject,
                    request.Body,
                    cancellationToken);
                sent++;
            }
            catch
            {
                skippedNames.Add(name);
            }
        }

        return Result<BroadcastMessageResultDto>.Ok(
            new BroadcastMessageResultDto(sent, skippedNames.Count, skippedNames));
    }
}
