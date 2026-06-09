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
    : IRequest<Result<LeagueSettingDto>>, IAmAuditableCommand;

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

// ── Known keys & defaults ─────────────────────────────────────────────────────

public static class KnownSettings
{
    public const string TeeTimeEmailEnabled = "tee_time_email_enabled";
    public const string StandingsDropCount = "standings_drop_count";

    public static readonly Dictionary<string, string> Defaults = new()
    {
        [TeeTimeEmailEnabled] = "false",
        [StandingsDropCount] = "1",
    };
}
