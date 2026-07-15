using GolfLeague.Application.Common;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Leagues;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public sealed record LeagueSettingDto(string Key, string Value);

// ── Get all settings ──────────────────────────────────────────────────────────

public sealed record GetLeagueSettingsQuery : IRequest<Result<IReadOnlyList<LeagueSettingDto>>>;

public sealed class GetLeagueSettingsQueryHandler
    : IRequestHandler<GetLeagueSettingsQuery, Result<IReadOnlyList<LeagueSettingDto>>>
{
    private readonly ILeagueSettingRepository _settings;
    private readonly ILeagueContext _leagueContext;

    public GetLeagueSettingsQueryHandler(ILeagueSettingRepository settings, ILeagueContext leagueContext)
    {
        _settings = settings;
        _leagueContext = leagueContext;
    }

    public async Task<Result<IReadOnlyList<LeagueSettingDto>>> Handle(GetLeagueSettingsQuery request, CancellationToken cancellationToken)
    {
        if (_leagueContext.LeagueId is null)
            return Result<IReadOnlyList<LeagueSettingDto>>.Fail("No league context.");

        var rows = await _settings.GetAllAsync(_leagueContext.LeagueId.Value, cancellationToken);

        // Merge with known defaults so the response always includes every key
        var result = KnownSettings.Defaults
            .Select(kvp =>
            {
                var row = rows.FirstOrDefault(r => r.Key == kvp.Key);
                return new LeagueSettingDto(kvp.Key, row?.Value ?? kvp.Value);
            })
            .ToList();

        return Result<IReadOnlyList<LeagueSettingDto>>.Ok(result);
    }
}

// ── Update a single setting ───────────────────────────────────────────────────

public sealed record UpdateLeagueSettingCommand(string Key, string Value, string UserId)
    : IRequest<Result<LeagueSettingDto>>, IAmAuditableCommand
{
    public string AuditEntityType => "LeagueSetting";
    public string AuditEntityId => Key;
}

public sealed class UpdateLeagueSettingCommandHandler
    : IRequestHandler<UpdateLeagueSettingCommand, Result<LeagueSettingDto>>
{
    private readonly ILeagueSettingRepository _settings;
    private readonly ILeagueContext _leagueContext;

    public UpdateLeagueSettingCommandHandler(ILeagueSettingRepository settings, ILeagueContext leagueContext)
    {
        _settings = settings;
        _leagueContext = leagueContext;
    }

    public async Task<Result<LeagueSettingDto>> Handle(UpdateLeagueSettingCommand request, CancellationToken cancellationToken)
    {
        if (_leagueContext.LeagueId is null)
            return Result<LeagueSettingDto>.Fail("No league context.");

        if (!KnownSettings.Defaults.ContainsKey(request.Key))
            return Result<LeagueSettingDto>.Fail($"Unknown setting key '{request.Key}'.");

        await _settings.UpsertAsync(_leagueContext.LeagueId.Value, request.Key, request.Value, cancellationToken);
        return Result<LeagueSettingDto>.Ok(new LeagueSettingDto(request.Key, request.Value));
    }
}

// ── Get public settings (anonymous) ───────────────────────────────────────────

public sealed record PublicLeagueSettingsDto(string WhatsAppGroupLink);

public sealed record GetPublicLeagueSettingsQuery : IRequest<Result<PublicLeagueSettingsDto>>;

public sealed class GetPublicLeagueSettingsQueryHandler
    : IRequestHandler<GetPublicLeagueSettingsQuery, Result<PublicLeagueSettingsDto>>
{
    private readonly ILeagueSettingRepository _settings;
    private readonly ILeagueContext _leagueContext;

    public GetPublicLeagueSettingsQueryHandler(ILeagueSettingRepository settings, ILeagueContext leagueContext)
    {
        _settings = settings;
        _leagueContext = leagueContext;
    }

    public async Task<Result<PublicLeagueSettingsDto>> Handle(GetPublicLeagueSettingsQuery request, CancellationToken cancellationToken)
    {
        if (_leagueContext.LeagueId is null)
            return Result<PublicLeagueSettingsDto>.Fail("No league context.");

        var whatsAppLink = await _settings.GetAsync(_leagueContext.LeagueId.Value, KnownSettings.WhatsAppGroupLink, cancellationToken);
        return Result<PublicLeagueSettingsDto>.Ok(new PublicLeagueSettingsDto(whatsAppLink?.Value ?? ""));
    }
}

// ── Known keys & defaults ─────────────────────────────────────────────────────

public static class KnownSettings
{
    public const string TeeTimeEmailEnabled = "tee_time_email_enabled";
    public const string StandingsDropCount = "standings_drop_count";

    /// <summary>
    /// Tee-time sign-up cutoff time of day, US/Eastern, as "HH:mm" (24-hour).
    /// The cutoff falls on the calendar day before each round. See
    /// <see cref="GolfLeague.Domain.Services.TeeTimeSchedule.ComputeCutoffUtc"/>.
    /// </summary>
    public const string TeeTimeCutoffTime = "tee_time_cutoff_time";

    /// <summary>
    /// Whether to send the sign-up reminder email — sent once per round, a
    /// few hours before the sign-up cutoff, to every active player in the
    /// half who doesn't yet have a tee time for that round. See
    /// <see cref="GolfLeague.Domain.Services.TeeTimeSchedule.ComputeReminderTimeUtc"/>.
    /// </summary>
    public const string SignUpReminderEmailEnabled = "sign_up_reminder_email_enabled";

    /// <summary>
    /// Whether the substitute-players feature is enabled: an admin-managed
    /// pool of substitute players, and letting a participant add a
    /// substitute to their tee time when players have skipped the round.
    /// </summary>
    public const string SubstitutesEnabled = "substitutes_enabled";

    /// <summary>
    /// Dollar cost of a round, charged to substitutes and shown in the
    /// substitute-pool "spots available" email as "$&lt;RoundCost&gt; payable to
    /// any league officer." Stored as a plain integer/decimal string.
    /// </summary>
    public const string RoundCost = "round_cost";

    /// <summary>
    /// Invite link to the league's WhatsApp group, shown as a "Join our
    /// WhatsApp group" link in the site footer. Empty by default; the
    /// footer link only renders once this is populated.
    /// </summary>
    public const string WhatsAppGroupLink = "whatsapp_group_link";

    public static readonly Dictionary<string, string> Defaults = new()
    {
        [TeeTimeEmailEnabled] = "false",
        [StandingsDropCount] = "1",
        [TeeTimeCutoffTime] = "18:00",
        [SignUpReminderEmailEnabled] = "false",
        [SubstitutesEnabled] = "false",
        [RoundCost] = "20",
        [WhatsAppGroupLink] = "",
    };

    /// <summary>
    /// Parses a stored <see cref="TeeTimeCutoffTime"/> value ("HH:mm"), falling
    /// back to the default cutoff time if missing or malformed.
    /// </summary>
    public static TimeOnly ParseCutoffTime(string? storedValue)
        => TimeOnly.TryParse(storedValue, out var parsed)
            ? parsed
            : TimeOnly.Parse(Defaults[TeeTimeCutoffTime]);
}
