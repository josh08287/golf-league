using GolfLeague.Application.Common;
using GolfLeague.Application.Leagues;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Sends the sign-up reminder email to every active player in the round's
/// half who doesn't yet have a tee time for this round. Called by the
/// autofill timer a few hours before the sign-up cutoff (see
/// <see cref="GolfLeague.Domain.Services.TeeTimeSchedule.ComputeReminderTimeUtc"/>)
/// when the league setting "sign_up_reminder_email_enabled" is true.
/// Players already assigned a tee time (manually or otherwise) are skipped —
/// they don't need to be reminded to sign up.
/// </summary>
public sealed record SendSignUpReminderEmailsCommand(int RoundId) : IRequest<Result<int>>;

public sealed class SendSignUpReminderEmailsCommandHandler
    : IRequestHandler<SendSignUpReminderEmailsCommand, Result<int>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly IFlightRepository _flightRepository;
    private readonly ILeagueSettingRepository _settingRepository;
    private readonly ILeagueRepository _leagueRepository;
    private readonly IEmailService _emailService;

    public SendSignUpReminderEmailsCommandHandler(
        IRoundRepository roundRepository,
        IFlightRepository flightRepository,
        ILeagueSettingRepository settingRepository,
        ILeagueRepository leagueRepository,
        IEmailService emailService)
    {
        _roundRepository = roundRepository;
        _flightRepository = flightRepository;
        _settingRepository = settingRepository;
        _leagueRepository = leagueRepository;
        _emailService = emailService;
    }

    public async Task<Result<int>> Handle(SendSignUpReminderEmailsCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<int>.Fail($"Round {request.RoundId} not found.");

        if (round.HalfId is null)
            return Result<int>.Ok(0);

        var setting = await _settingRepository.GetAsync(round.LeagueId, KnownSettings.SignUpReminderEmailEnabled, cancellationToken);
        if (setting is null || !bool.TryParse(setting.Value, out var enabled) || !enabled)
            return Result<int>.Ok(0);

        // Everyone already assigned a tee time (or withdrawn/skipped) for
        // this round doesn't need a reminder to sign up.
        var alreadyHandled = round.Participants
            .Where(p => p.TeeTimeId is not null || p.IsWithdrawn || p.SkippedWeek)
            .Select(p => p.PlayerId)
            .ToHashSet();

        var memberships = await _flightRepository.GetMembershipsByHalfAsync(round.HalfId.Value, cancellationToken);
        var recipients = memberships
            .Select(fm => fm.Player)
            .Where(p => p.Email is not null
                        && p.IsActive
                        && !p.TeeTimeEmailOptOut
                        && !alreadyHandled.Contains(p.Id))
            .DistinctBy(p => p.Id)
            .ToList();

        var roundDate = round.RoundDate.ToString("dddd, MMMM d");
        var cutoffSetting = await _settingRepository.GetAsync(round.LeagueId, KnownSettings.TeeTimeCutoffTime, cancellationToken);
        var cutoffTime = KnownSettings.ParseCutoffTime(cutoffSetting?.Value);
        var cutoffDisplay = $"{cutoffTime:h:mm tt} ET, {round.RoundDate.AddDays(-1):dddd}";

        var league = await _leagueRepository.GetByIdAsync(round.LeagueId, cancellationToken);
        var leagueName = league?.Name ?? "Golf League";
        var sent = 0;

        foreach (var player in recipients)
        {
            try
            {
                await _emailService.SendSignUpReminderAsync(
                    player.Email!,
                    player.FullName,
                    leagueName,
                    roundDate,
                    cutoffDisplay,
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
}
