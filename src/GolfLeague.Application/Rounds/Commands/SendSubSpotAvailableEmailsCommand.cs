using GolfLeague.Application.Common;
using GolfLeague.Application.Leagues;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Notifies every active, opted-in player in the league's substitute pool
/// when a round has open spots (i.e. at least one roster player has skipped
/// the week). Sent by the autofill timer in the same window as the sign-up
/// reminder — a few hours before the sign-up cutoff, before autofill runs —
/// gated by <see cref="Domain.Entities.Round.SubSpotEmailSentAt"/> so it goes
/// out at most once per round.
/// </summary>
public sealed record SendSubSpotAvailableEmailsCommand(int RoundId, string WebBaseUrl) : IRequest<Result<int>>;

public sealed class SendSubSpotAvailableEmailsCommandHandler
    : IRequestHandler<SendSubSpotAvailableEmailsCommand, Result<int>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IInviteRepository _inviteRepository;
    private readonly ILeagueSettingRepository _settingRepository;
    private readonly ILeagueRepository _leagueRepository;
    private readonly IEmailService _emailService;

    public SendSubSpotAvailableEmailsCommandHandler(
        IRoundRepository roundRepository,
        IPlayerRepository playerRepository,
        IInviteRepository inviteRepository,
        ILeagueSettingRepository settingRepository,
        ILeagueRepository leagueRepository,
        IEmailService emailService)
    {
        _roundRepository = roundRepository;
        _playerRepository = playerRepository;
        _inviteRepository = inviteRepository;
        _settingRepository = settingRepository;
        _leagueRepository = leagueRepository;
        _emailService = emailService;
    }

    public async Task<Result<int>> Handle(SendSubSpotAvailableEmailsCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<int>.Fail($"Round {request.RoundId} not found.");

        var substitutesEnabledSetting = await _settingRepository.GetAsync(round.LeagueId, KnownSettings.SubstitutesEnabled, cancellationToken);
        if (substitutesEnabledSetting is null || !bool.TryParse(substitutesEnabledSetting.Value, out var substitutesEnabled) || !substitutesEnabled)
            return Result<int>.Ok(0);

        // Each skipped (but not withdrawn) participant represents one open
        // seat a substitute could fill in that player's spot.
        var openSpots = round.Participants.Count(p => p.SkippedWeek && !p.IsWithdrawn);
        if (openSpots == 0)
            return Result<int>.Ok(0);

        var allPlayers = await _playerRepository.GetAllAsync(cancellationToken);
        var recipients = allPlayers
            .Where(p => p.LeagueId == round.LeagueId
                        && p.IsSubstitute
                        && p.IsActive
                        && p.Email is not null
                        && !p.TeeTimeEmailOptOut)
            .DistinctBy(p => p.Id)
            .ToList();

        if (recipients.Count == 0)
            return Result<int>.Ok(0);

        var roundDate = round.RoundDate.ToString("dddd, MMMM d");
        var costSetting = await _settingRepository.GetAsync(round.LeagueId, KnownSettings.RoundCost, cancellationToken);
        var roundCostDisplay = $"${costSetting?.Value ?? KnownSettings.Defaults[KnownSettings.RoundCost]}";

        var league = await _leagueRepository.GetByIdAsync(round.LeagueId, cancellationToken);
        var leagueName = league?.Name ?? "Golf League";
        var baseUrl = request.WebBaseUrl.TrimEnd('/');
        var sent = 0;

        foreach (var player in recipients)
        {
            try
            {
                var loginLink = await ResolveLoginLinkAsync(player, baseUrl, cancellationToken);
                await _emailService.SendSubSpotAvailableAsync(
                    player.Email!,
                    player.FullName,
                    leagueName,
                    roundDate,
                    openSpots,
                    roundCostDisplay,
                    loginLink,
                    cancellationToken);
                sent++;
            }
            catch
            {
                // One failed send should not block the rest
            }
        }

        return Result<int>.Ok(sent);
    }

    /// <summary>
    /// Players who've already activated their account get a normal login
    /// link; players who haven't get sent straight to their pending invite's
    /// accept-invite link. Falls back to a plain login link if neither
    /// applies (e.g. their invite expired and was never resent) rather than
    /// silently generating a new invite as a side effect of this email.
    /// </summary>
    private async Task<string> ResolveLoginLinkAsync(Domain.Entities.Player player, string baseUrl, CancellationToken cancellationToken)
    {
        if (player.AppUserId is not null)
            return $"{baseUrl}/login";

        if (player.Email is not null)
        {
            var pendingInvite = await _inviteRepository.GetPendingByEmailAsync(player.Email, cancellationToken);
            if (pendingInvite is not null)
                return $"{baseUrl}/accept-invite?token={pendingInvite.Token}";
        }

        return $"{baseUrl}/login";
    }
}
