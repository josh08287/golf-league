using GolfLeague.Application.Interfaces;
using GolfLeague.Application.Leagues;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Functions.Functions;

/// <summary>
/// Timer-triggered autofill, plus the sign-up reminder and schedule emails.
/// The cutoff is per-round and per-league — each league configures its own
/// cutoff time of day via <see cref="KnownSettings.TeeTimeCutoffTime"/>
/// (default 6:00pm US/Eastern), applied to the calendar day before each
/// round (see <see cref="TeeTimeSchedule.ComputeCutoffUtc"/>). This runs
/// hourly. Autofill itself re-runs harmlessly every hour past cutoff (it's a
/// no-op once everyone is seated), but each automated email is gated by its
/// own "already sent" flag on <see cref="Domain.Entities.Round"/> so it goes
/// out exactly once per round:
///  - Sign-up reminder: <see cref="TeeTimeSchedule.IsWithinReminderWindow"/>
///    + <see cref="Domain.Entities.Round.SignUpReminderSentAt"/>.
///  - Substitute spots available: sent in the same window as the sign-up
///    reminder, to the league's substitute pool, only when at least one
///    roster player has skipped the round. Guarded by
///    <see cref="Domain.Entities.Round.SubSpotEmailSentAt"/>.
///  - Tee-time schedule: <see cref="TeeTimeSchedule.IsAfterCutoff"/>
///    + <see cref="Domain.Entities.Round.TeeTimeScheduleEmailSentAt"/>. The
///    admin "resend" endpoint bypasses this flag on purpose.
///
/// CRON: "0 0 * * * *" — top of every hour, UTC.
/// </summary>
public sealed class TeeTimeAutofillTimer
{
    private readonly IRoundRepository _rounds;
    private readonly ILeagueSettingRepository _leagueSettings;
    private readonly ITeeTimeAutofillService _autofill;
    private readonly IMediator _mediator;
    private readonly string _webBaseUrl;
    private readonly ILogger<TeeTimeAutofillTimer> _logger;

    public TeeTimeAutofillTimer(
        IRoundRepository rounds,
        ILeagueSettingRepository leagueSettings,
        ITeeTimeAutofillService autofill,
        IMediator mediator,
        IConfiguration configuration,
        ILogger<TeeTimeAutofillTimer> logger)
    {
        _rounds = rounds;
        _leagueSettings = leagueSettings;
        _autofill = autofill;
        _mediator = mediator;
        _webBaseUrl = configuration["WEB_BASE_URL"] ?? "http://localhost:5173";
        _logger = logger;
    }

    [Function("TeeTimeAutofillTimer")]
    public async Task Run([TimerTrigger("0 0 * * * *")] TimerInfo timer, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var horizon = today.AddDays(7);

        _logger.LogInformation(
            "TeeTimeAutofillTimer firing at {Now}. Looking for Scheduled rounds with RoundDate in [{Today}, {Horizon}].",
            now, today, horizon);

        var rounds = await _rounds.GetAllAsync(cancellationToken);
        var inWindow = rounds
            .Where(r => r.Status == RoundStatus.Scheduled
                     && r.RoundDate >= today
                     && r.RoundDate <= horizon)
            .ToList();

        var cutoffTimeByLeague = new Dictionary<int, TimeOnly>();
        async Task<TimeOnly> GetCutoffTimeAsync(int leagueId)
        {
            if (cutoffTimeByLeague.TryGetValue(leagueId, out var cached)) return cached;
            var setting = await _leagueSettings.GetAsync(leagueId, KnownSettings.TeeTimeCutoffTime, cancellationToken);
            var time = KnownSettings.ParseCutoffTime(setting?.Value);
            cutoffTimeByLeague[leagueId] = time;
            return time;
        }

        var candidates = new List<Domain.Entities.Round>();
        var reminderCandidates = new List<Domain.Entities.Round>();
        foreach (var round in inWindow)
        {
            var cutoffTime = await GetCutoffTimeAsync(round.LeagueId);
            if (TeeTimeSchedule.IsAfterCutoff(round.RoundDate, now, cutoffTime))
                candidates.Add(round);
            else if (round.SignUpReminderSentAt is null
                     && TeeTimeSchedule.IsWithinReminderWindow(round.RoundDate, now, cutoffTime))
                reminderCandidates.Add(round);
        }
        candidates = candidates.OrderBy(r => r.RoundDate).ToList();
        reminderCandidates = reminderCandidates.OrderBy(r => r.RoundDate).ToList();

        foreach (var round in reminderCandidates)
        {
            var reminderResult = await _mediator.Send(new SendSignUpReminderEmailsCommand(round.Id), cancellationToken);
            if (reminderResult.IsSuccess)
            {
                _logger.LogInformation(
                    "Sign-up reminder emails sent for round {RoundId} ({Date}): {Count} recipient(s).",
                    round.Id, round.RoundDate, reminderResult.Value);
                await _rounds.MarkSignUpReminderSentAsync(round.Id, now, cancellationToken);
            }
            else
            {
                _logger.LogWarning(
                    "Sign-up reminder email send failed for round {RoundId}: {Error}",
                    round.Id, reminderResult.Error);
            }

            if (round.SubSpotEmailSentAt is null)
            {
                var subSpotResult = await _mediator.Send(new SendSubSpotAvailableEmailsCommand(round.Id, _webBaseUrl), cancellationToken);
                if (subSpotResult.IsSuccess)
                {
                    if (subSpotResult.Value > 0)
                        _logger.LogInformation(
                            "Sub-spot-available emails sent for round {RoundId} ({Date}): {Count} recipient(s).",
                            round.Id, round.RoundDate, subSpotResult.Value);
                    await _rounds.MarkSubSpotEmailSentAsync(round.Id, now, cancellationToken);
                }
                else
                {
                    _logger.LogWarning(
                        "Sub-spot-available email send failed for round {RoundId}: {Error}",
                        round.Id, subSpotResult.Error);
                }
            }
        }

        foreach (var round in candidates)
        {
            var result = await _autofill.RunAsync(round.Id, cancellationToken);
            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Autofill round {RoundId} ({Date}): assigned {Assigned} player(s) across {Slots} slot(s)",
                    round.Id, round.RoundDate, result.Value!.AssignedCount, result.Value!.SlotsTouched);

                if (round.TeeTimeScheduleEmailSentAt is null)
                {
                    var emailResult = await _mediator.Send(new SendTeeTimeScheduleEmailsCommand(round.Id), cancellationToken);
                    if (emailResult.IsSuccess)
                    {
                        _logger.LogInformation("Tee time emails sent for round {RoundId}: {Count} recipient(s).", round.Id, emailResult.Value);
                        await _rounds.MarkTeeTimeScheduleEmailSentAsync(round.Id, now, cancellationToken);
                    }
                    else
                    {
                        _logger.LogWarning("Tee time email send skipped or failed for round {RoundId}: {Error}", round.Id, emailResult.Error);
                    }
                }
            }
            else
            {
                _logger.LogError(
                    "Autofill round {RoundId} failed: {Error}",
                    round.Id, result.Error);
            }
        }

        if (candidates.Count == 0 && reminderCandidates.Count == 0)
        {
            _logger.LogDebug("No eligible rounds for autofill or sign-up reminders this run.");
        }
    }
}
