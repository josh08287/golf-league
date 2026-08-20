using GolfLeague.Application.Interfaces;
using GolfLeague.Application.Leagues;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Functions.Functions;

/// <summary>
/// Three timer-triggered jobs, each running once daily instead of hourly —
/// hourly firing meant every job re-scanned every in-window round on every
/// tick even though each fires at most once per round (gated by its own
/// "already sent"/"already filled" flag), which was pure wasted SQL query
/// volume the other 23 hours of the day. Each job's CRON fires once daily at
/// a fixed UTC time chosen to land shortly after its target US/Eastern clock
/// time even in the worst-case DST offset, then filters in code to the exact
/// round(s) whose Eastern-local trigger instant (see
/// <see cref="TeeTimeSchedule.ComputeDailyTriggerUtc"/>) has just passed —
/// so a round is only ever acted on once, and firing a little early/late
/// relative to the nominal clock time still lands on the correct calendar day.
///  - Sub-spot-available email: 2 days before the round, gated by
///    <see cref="Domain.Entities.Round.SubSpotEmailSentAt"/>.
///  - Sign-up reminder email: 8 hours before the sign-up cutoff, gated by
///    <see cref="Domain.Entities.Round.SignUpReminderSentAt"/>.
///  - Autofill (+ tee-time schedule email): 8:00pm US/Eastern the day before
///    the round, gated by autofill being a no-op once everyone is seated and
///    by <see cref="Domain.Entities.Round.TeeTimeScheduleEmailSentAt"/> for
///    the email specifically.
/// </summary>
public sealed class TeeTimeAutofillTimer
{
    private static readonly TimeOnly AutofillTriggerTime = new(20, 0); // 8:00pm ET, day before round
    private const int SubSpotDaysBeforeRound = 2;

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

    /// <summary>
    /// Sub-spot-available emails, once daily. Fires at 04:00 UTC (11:00pm/
    /// midnight ET depending on DST) — comfortably after the 2-days-before
    /// mark has begun for any round, in code we only act on rounds where that
    /// mark falls on "today or earlier and not yet sent" so a missed/late run
    /// still catches up on the next one.
    /// </summary>
    [Function("SubSpotEmailTimer")]
    public async Task RunSubSpotEmails([TimerTrigger("0 0 4 * * *")] TimerInfo timer, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var horizon = today.AddDays(SubSpotDaysBeforeRound + 1);

        var inWindow = await _rounds.GetScheduledInDateRangeAsync(today, horizon, cancellationToken);
        var due = inWindow
            .Where(r => r.SubSpotEmailSentAt is null
                        && now >= TeeTimeSchedule.ComputeDailyTriggerUtc(r.RoundDate, SubSpotDaysBeforeRound, TimeOnly.MinValue))
            .OrderBy(r => r.RoundDate)
            .ToList();

        if (due.Count == 0)
        {
            _logger.LogDebug("No rounds due for sub-spot-available emails this run.");
            return;
        }

        foreach (var round in due)
        {
            if (round.Participants.Count(p => p.SkippedWeek && !p.IsWithdrawn) == 0)
            {
                // No open spots yet — leave SubSpotEmailSentAt unset so a later
                // skip (before the round) still triggers the email that day.
                continue;
            }

            var result = await _mediator.Send(new SendSubSpotAvailableEmailsCommand(round.Id, _webBaseUrl), cancellationToken);
            if (result.IsSuccess)
            {
                if (result.Value > 0)
                    _logger.LogInformation(
                        "Sub-spot-available emails sent for round {RoundId} ({Date}): {Count} recipient(s).",
                        round.Id, round.RoundDate, result.Value);
                await _rounds.MarkSubSpotEmailSentAsync(round.Id, now, cancellationToken);
            }
            else
            {
                _logger.LogWarning("Sub-spot-available email send failed for round {RoundId}: {Error}", round.Id, result.Error);
            }
        }
    }

    /// <summary>
    /// Sign-up reminder emails, once daily. Trigger instant is per-league
    /// (8 hours before that league's configurable sign-up cutoff), so unlike
    /// the other two jobs this can't use one fixed clock time — fires at
    /// 03:00 UTC, comfortably ahead of the earliest realistic per-league
    /// reminder time, and filters to rounds whose reminder instant has passed.
    /// </summary>
    [Function("SignUpReminderTimer")]
    public async Task RunSignUpReminders([TimerTrigger("0 0 3 * * *")] TimerInfo timer, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var horizon = today.AddDays(2);

        var inWindow = await _rounds.GetScheduledInDateRangeAsync(today, horizon, cancellationToken);

        var cutoffTimeByLeague = new Dictionary<int, TimeOnly>();
        async Task<TimeOnly> GetCutoffTimeAsync(int leagueId)
        {
            if (cutoffTimeByLeague.TryGetValue(leagueId, out var cached)) return cached;
            var setting = await _leagueSettings.GetAsync(leagueId, KnownSettings.TeeTimeCutoffTime, cancellationToken);
            var time = KnownSettings.ParseCutoffTime(setting?.Value);
            cutoffTimeByLeague[leagueId] = time;
            return time;
        }

        var due = new List<Domain.Entities.Round>();
        foreach (var round in inWindow)
        {
            if (round.SignUpReminderSentAt is not null) continue;
            var cutoffTime = await GetCutoffTimeAsync(round.LeagueId);
            if (now >= TeeTimeSchedule.ComputeReminderTimeUtc(round.RoundDate, cutoffTime)
                && now < TeeTimeSchedule.ComputeCutoffUtc(round.RoundDate, cutoffTime))
                due.Add(round);
        }
        due = due.OrderBy(r => r.RoundDate).ToList();

        if (due.Count == 0)
        {
            _logger.LogDebug("No rounds due for sign-up reminder emails this run.");
            return;
        }

        foreach (var round in due)
        {
            var result = await _mediator.Send(new SendSignUpReminderEmailsCommand(round.Id), cancellationToken);
            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Sign-up reminder emails sent for round {RoundId} ({Date}): {Count} recipient(s).",
                    round.Id, round.RoundDate, result.Value);
                await _rounds.MarkSignUpReminderSentAsync(round.Id, now, cancellationToken);
            }
            else
            {
                _logger.LogWarning("Sign-up reminder email send failed for round {RoundId}: {Error}", round.Id, result.Error);
            }
        }
    }

    /// <summary>
    /// Autofill + tee-time schedule email, once daily at a fixed 8:00pm
    /// US/Eastern trigger (see <see cref="AutofillTriggerTime"/>), independent
    /// of each league's configurable sign-up cutoff. Fires at 01:00 UTC,
    /// which is always at or after 8:00pm ET (9:00pm EDT / 8:00pm EST), and
    /// filters to rounds whose local 8pm-the-day-before instant has passed.
    /// </summary>
    [Function("TeeTimeAutofillTimer")]
    public async Task Run([TimerTrigger("0 0 1 * * *")] TimerInfo timer, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var horizon = today.AddDays(2);

        _logger.LogInformation(
            "TeeTimeAutofillTimer firing at {Now}. Looking for Scheduled rounds with RoundDate in [{Today}, {Horizon}].",
            now, today, horizon);

        var inWindow = await _rounds.GetScheduledInDateRangeAsync(today, horizon, cancellationToken);

        var candidates = inWindow
            .Where(r => now >= TeeTimeSchedule.ComputeDailyTriggerUtc(r.RoundDate, 1, AutofillTriggerTime))
            .OrderBy(r => r.RoundDate)
            .ToList();

        if (candidates.Count == 0)
        {
            _logger.LogDebug("No eligible rounds for autofill this run.");
            return;
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
                _logger.LogError("Autofill round {RoundId} failed: {Error}", round.Id, result.Error);
            }
        }
    }
}
