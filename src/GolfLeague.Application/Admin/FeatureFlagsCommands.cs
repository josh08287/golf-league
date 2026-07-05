using GolfLeague.Application.Common;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Admin;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public sealed record FeatureFlagDto(string Key, bool Enabled);

// ── Get all flags (super-admin) ───────────────────────────────────────────────

public sealed record GetFeatureFlagsQuery : IRequest<Result<IReadOnlyList<FeatureFlagDto>>>;

public sealed class GetFeatureFlagsQueryHandler
    : IRequestHandler<GetFeatureFlagsQuery, Result<IReadOnlyList<FeatureFlagDto>>>
{
    private readonly IFeatureFlagRepository _flags;

    public GetFeatureFlagsQueryHandler(IFeatureFlagRepository flags)
    {
        _flags = flags;
    }

    public async Task<Result<IReadOnlyList<FeatureFlagDto>>> Handle(GetFeatureFlagsQuery request, CancellationToken cancellationToken)
    {
        var rows = await _flags.GetAllAsync(cancellationToken);

        // Merge with known defaults so the response always includes every key
        var result = KnownFeatureFlags.Defaults
            .Select(kvp =>
            {
                var row = rows.FirstOrDefault(r => r.Key == kvp.Key);
                return new FeatureFlagDto(kvp.Key, row?.Enabled ?? kvp.Value);
            })
            .ToList();

        return Result<IReadOnlyList<FeatureFlagDto>>.Ok(result);
    }
}

// ── Get flags for the authenticated-user-facing enablement check ─────────────

/// <summary>
/// Returns just the enabled/disabled state of every known flag. Any
/// authenticated user may call this — it never exposes anything beyond a
/// key → bool map, so it's safe for non-admins to read in order to decide
/// whether to render a flagged feature's UI.
/// </summary>
public sealed record GetFeatureFlagStatesQuery : IRequest<Result<IReadOnlyDictionary<string, bool>>>;

public sealed class GetFeatureFlagStatesQueryHandler
    : IRequestHandler<GetFeatureFlagStatesQuery, Result<IReadOnlyDictionary<string, bool>>>
{
    private readonly IFeatureFlagRepository _flags;

    public GetFeatureFlagStatesQueryHandler(IFeatureFlagRepository flags)
    {
        _flags = flags;
    }

    public async Task<Result<IReadOnlyDictionary<string, bool>>> Handle(GetFeatureFlagStatesQuery request, CancellationToken cancellationToken)
    {
        var rows = await _flags.GetAllAsync(cancellationToken);

        IReadOnlyDictionary<string, bool> result = KnownFeatureFlags.Defaults
            .ToDictionary(
                kvp => kvp.Key,
                kvp => rows.FirstOrDefault(r => r.Key == kvp.Key)?.Enabled ?? kvp.Value);

        return Result<IReadOnlyDictionary<string, bool>>.Ok(result);
    }
}

// ── Update a single flag (super-admin) ────────────────────────────────────────

public sealed record UpdateFeatureFlagCommand(string Key, bool Enabled, string UserId)
    : IRequest<Result<FeatureFlagDto>>, IAmAuditableCommand;

public sealed class UpdateFeatureFlagCommandHandler
    : IRequestHandler<UpdateFeatureFlagCommand, Result<FeatureFlagDto>>
{
    private readonly IFeatureFlagRepository _flags;

    public UpdateFeatureFlagCommandHandler(IFeatureFlagRepository flags)
    {
        _flags = flags;
    }

    public async Task<Result<FeatureFlagDto>> Handle(UpdateFeatureFlagCommand request, CancellationToken cancellationToken)
    {
        if (!KnownFeatureFlags.Defaults.ContainsKey(request.Key))
            return Result<FeatureFlagDto>.Fail($"Unknown feature flag key '{request.Key}'.");

        await _flags.UpsertAsync(request.Key, request.Enabled, cancellationToken);
        return Result<FeatureFlagDto>.Ok(new FeatureFlagDto(request.Key, request.Enabled));
    }
}

// ── Known keys & defaults ─────────────────────────────────────────────────────

public static class KnownFeatureFlags
{
    /// <summary>
    /// Lets a player skip/unskip their own upcoming rounds from their player
    /// profile page (in addition to the existing tee-times-page self-skip flow).
    /// </summary>
    public const string SelfSkipRoundsEnabled = "self_skip_rounds_enabled";

    public static readonly Dictionary<string, bool> Defaults = new()
    {
        [SelfSkipRoundsEnabled] = false,
    };
}
