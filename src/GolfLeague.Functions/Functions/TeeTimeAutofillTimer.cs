using GolfLeague.Application.Interfaces;
using GolfLeague.Application.Leagues;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Functions.Functions;

/// <summary>
/// Timer-triggered autofill. The cutoff is per-round and per-league — each
/// league configures its own cutoff time of day via
/// <see cref="KnownSettings.TeeTimeCutoffTime"/> (default 6:00pm US/Eastern),
/// applied to the calendar day before each round (see
/// <see cref="TeeTimeSchedule.ComputeCutoffUtc"/>). This runs hourly and
/// relies on the per-round guard (<see cref="TeeTimeSchedule.IsAfterCutoff"/>)
/// to only act on rounds whose own league's cutoff has actually passed.
///
/// CRON: "0 0 * * * *" — top of every hour, UTC.
/// </summary>
public sealed class TeeTimeAutofillTimer
{
    private readonly IRoundRepository _rounds;
    private readonly ILeagueSettingRepository _leagueSettings;
    private readonly ITeeTimeAutofillService _autofill;
    private readonly IMediator _mediator;
    private readonly ILogger<TeeTimeAutofillTimer> _logger;

    public TeeTimeAutofillTimer(
        IRoundRepository rounds,
        ILeagueSettingRepository leagueSettings,
        ITeeTimeAutofillService autofill,
        IMediator mediator,
        ILogger<TeeTimeAutofillTimer> logger)
    {
        _rounds = rounds;
        _leagueSettings = leagueSettings;
        _autofill = autofill;
        _mediator = mediator;
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
        foreach (var round in inWindow)
        {
            var cutoffTime = await GetCutoffTimeAsync(round.LeagueId);
            if (TeeTimeSchedule.IsAfterCutoff(round.RoundDate, now, cutoffTime))
                candidates.Add(round);
        }
        candidates = candidates.OrderBy(r => r.RoundDate).ToList();

        foreach (var round in candidates)
        {
            var result = await _autofill.RunAsync(round.Id, cancellationToken);
            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Autofill round {RoundId} ({Date}): assigned {Assigned} player(s) across {Slots} slot(s)",
                    round.Id, round.RoundDate, result.Value!.AssignedCount, result.Value!.SlotsTouched);

                var emailResult = await _mediator.Send(new SendTeeTimeScheduleEmailsCommand(round.Id), cancellationToken);
                if (emailResult.IsSuccess)
                    _logger.LogInformation("Tee time emails sent for round {RoundId}: {Count} recipient(s).", round.Id, emailResult.Value);
                else
                    _logger.LogWarning("Tee time email send skipped or failed for round {RoundId}: {Error}", round.Id, emailResult.Error);
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
