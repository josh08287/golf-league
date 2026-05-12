using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Functions.Functions;

/// <summary>
/// Timer-triggered autofill. NCRONTAB fires every Sunday at 12:00 noon
/// Eastern (handled at the schedule level via the timezone host setting
/// in <c>host.json</c> or via the <c>WEBSITE_TIME_ZONE</c> app setting on
/// Windows; on Linux the host's TZ var is used). The expression is in
/// UTC by default, so we use a redundant per-round guard that also checks
/// <see cref="TeeTimeSchedule.IsAfterCutoff"/> to handle missed runs.
///
/// CRON: "0 0 16 * * 0" — 16:00 UTC every Sunday, which is 12:00 noon EDT
/// (Mar-Nov). Outside DST this fires at 11am ET — fine; close enough and
/// the per-round guard checks the actual cutoff before assigning.
/// </summary>
public sealed class TeeTimeAutofillTimer
{
    private readonly IRoundRepository _rounds;
    private readonly ITeeTimeAutofillService _autofill;
    private readonly ILogger<TeeTimeAutofillTimer> _logger;

    public TeeTimeAutofillTimer(
        IRoundRepository rounds,
        ITeeTimeAutofillService autofill,
        ILogger<TeeTimeAutofillTimer> logger)
    {
        _rounds = rounds;
        _autofill = autofill;
        _logger = logger;
    }

    [Function("TeeTimeAutofillTimer")]
    public async Task Run([TimerTrigger("0 0 16 * * 0")] TimerInfo timer, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var horizon = today.AddDays(7);

        _logger.LogInformation(
            "TeeTimeAutofillTimer firing at {Now}. Looking for Scheduled rounds with RoundDate in [{Today}, {Horizon}].",
            now, today, horizon);

        var rounds = await _rounds.GetAllAsync(cancellationToken);
        var candidates = rounds
            .Where(r => r.Status == RoundStatus.Scheduled
                     && r.RoundDate >= today
                     && r.RoundDate <= horizon
                     && TeeTimeSchedule.IsAfterCutoff(r.RoundDate, now))
            .OrderBy(r => r.RoundDate)
            .ToList();

        foreach (var round in candidates)
        {
            var result = await _autofill.RunAsync(round.Id, cancellationToken);
            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Autofill round {RoundId} ({Date}): assigned {Assigned} player(s) across {Slots} slot(s)",
                    round.Id, round.RoundDate, result.Value!.AssignedCount, result.Value!.SlotsTouched);
            }
            else
            {
                _logger.LogError(
                    "Autofill round {RoundId} failed: {Error}",
                    round.Id, result.Error);
            }
        }

        if (candidates.Count == 0)
        {
            _logger.LogDebug("No eligible rounds for autofill this run.");
        }
    }
}
