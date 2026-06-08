using GolfLeague.Application.Common;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Sends the weekly tee time schedule email to every player in the current
/// half who has an email address. Called by the autofill timer after a
/// successful autofill when the league setting "tee_time_email_enabled" is true.
/// </summary>
public sealed record SendTeeTimeScheduleEmailsCommand(int RoundId) : IRequest<Result<int>>;

public sealed class SendTeeTimeScheduleEmailsCommandHandler
    : IRequestHandler<SendTeeTimeScheduleEmailsCommand, Result<int>>
{
    public const string SettingKey = "tee_time_email_enabled";

    private readonly IRoundRepository _roundRepository;
    private readonly ITeeTimeRepository _teeTimeRepository;
    private readonly ILeagueSettingRepository _settingRepository;
    private readonly ILeagueRepository _leagueRepository;
    private readonly IEmailService _emailService;

    public SendTeeTimeScheduleEmailsCommandHandler(
        IRoundRepository roundRepository,
        ITeeTimeRepository teeTimeRepository,
        ILeagueSettingRepository settingRepository,
        ILeagueRepository leagueRepository,
        IEmailService emailService)
    {
        _roundRepository = roundRepository;
        _teeTimeRepository = teeTimeRepository;
        _settingRepository = settingRepository;
        _leagueRepository = leagueRepository;
        _emailService = emailService;
    }

    public async Task<Result<int>> Handle(SendTeeTimeScheduleEmailsCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<int>.Fail($"Round {request.RoundId} not found.");

        // Check the feature flag for this league
        var setting = await _settingRepository.GetAsync(round.LeagueId, SettingKey, cancellationToken);
        if (setting is null || !bool.TryParse(setting.Value, out var enabled) || !enabled)
            return Result<int>.Ok(0);

        // Load tee times with their participants
        var slots = await _teeTimeRepository.GetByRoundAsync(round.Id, cancellationToken);

        // Build the slot display list (only occupied slots)
        var emailSlots = slots
            .Where(s => s.Participants.Any(p => !p.IsWithdrawn && !p.SkippedWeek))
            .OrderBy(s => s.ScheduledTime)
            .Select(s => new TeeTimeEmailSlot(
                s.ScheduledTime.ToString("h:mm tt"),
                s.Participants
                    .Where(p => !p.IsWithdrawn && !p.SkippedWeek)
                    .OrderBy(p => p.Player.LastName)
                    .Select(p => p.Player.FullName)
                    .ToList()))
            .ToList();

        // Build a lookup: playerId → slot time (null = not assigned)
        var playerSlotMap = slots
            .SelectMany(s => s.Participants
                .Where(p => !p.IsWithdrawn && !p.SkippedWeek)
                .Select(p => (p.PlayerId, SlotTime: s.ScheduledTime.ToString("h:mm tt"))))
            .ToDictionary(x => x.PlayerId, x => x.SlotTime);

        // Only participants in the current half with an email address
        var recipients = round.Participants
            .Where(p => !p.IsWithdrawn && !p.SkippedWeek
                        && p.Player.Email is not null)
            .ToList();

        var roundDate = round.RoundDate.ToString("dddd, MMMM d");
        var league = await _leagueRepository.GetByIdAsync(round.LeagueId, cancellationToken);
        var leagueName = league?.Name ?? "Golf League";
        var sent = 0;

        foreach (var participant in recipients)
        {
            playerSlotMap.TryGetValue(participant.PlayerId, out var slotTime);
            try
            {
                await _emailService.SendTeeTimeScheduleAsync(
                    participant.Player.Email!,
                    participant.Player.FullName,
                    leagueName,
                    roundDate,
                    slotTime,
                    emailSlots,
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
