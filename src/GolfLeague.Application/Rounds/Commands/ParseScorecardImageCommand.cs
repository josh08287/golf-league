using GolfLeague.Application.Admin;
using GolfLeague.Application.Common;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

public sealed record ScorecardOcrHoleDto(int HoleNumber, int? GrossStrokes, double Confidence);

public sealed record ScorecardOcrPlayerDto(
    string RawOcrName,
    int? PlayerId,
    string? PlayerName,
    IReadOnlyList<ScorecardOcrHoleDto> Holes);

public sealed record ScorecardOcrResultDto(IReadOnlyList<ScorecardOcrPlayerDto> Players);

/// <summary>
/// Parses an uploaded scorecard photo into per-player hole scores for the
/// user to confirm/edit — never writes to the database and never persists
/// the image; the caller (Function) discards the bytes once this returns.
/// </summary>
public sealed record ParseScorecardImageCommand(
    int TeeTimeId,
    int SubmittedByPlayerId,
    byte[] ImageBytes) : IRequest<Result<ScorecardOcrResultDto>>;

public sealed class ParseScorecardImageCommandHandler
    : IRequestHandler<ParseScorecardImageCommand, Result<ScorecardOcrResultDto>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ITeeTimeRepository _teeTimeRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IFeatureFlagRepository _featureFlags;
    private readonly IScorecardOcrService _ocrService;

    public ParseScorecardImageCommandHandler(
        IRoundRepository roundRepository,
        ITeeTimeRepository teeTimeRepository,
        ICourseRepository courseRepository,
        IFeatureFlagRepository featureFlags,
        IScorecardOcrService ocrService)
    {
        _roundRepository = roundRepository;
        _teeTimeRepository = teeTimeRepository;
        _courseRepository = courseRepository;
        _featureFlags = featureFlags;
        _ocrService = ocrService;
    }

    public async Task<Result<ScorecardOcrResultDto>> Handle(ParseScorecardImageCommand request, CancellationToken cancellationToken)
    {
        var flag = await _featureFlags.GetAsync(KnownFeatureFlags.ScorecardOcrEnabled, cancellationToken);
        var enabled = flag?.Enabled ?? KnownFeatureFlags.Defaults[KnownFeatureFlags.ScorecardOcrEnabled];
        if (!enabled)
            return Result<ScorecardOcrResultDto>.Fail("Scorecard scanning is not enabled.");

        var teeTime = await _teeTimeRepository.GetByIdAsync(request.TeeTimeId, cancellationToken);
        if (teeTime is null)
            return Result<ScorecardOcrResultDto>.Fail($"Tee time {request.TeeTimeId} not found.");

        var submitter = teeTime.Participants.FirstOrDefault(p => p.PlayerId == request.SubmittedByPlayerId);
        if (submitter is null)
            return Result<ScorecardOcrResultDto>.Fail("You must be a member of this tee time to scan a scorecard.");

        var round = await _roundRepository.GetByIdAsync(teeTime.RoundId, cancellationToken);
        if (round is null)
            return Result<ScorecardOcrResultDto>.Fail($"Round for tee time {request.TeeTimeId} not found.");

        if (round.Status is RoundStatus.Finalized or RoundStatus.Cancelled)
            return Result<ScorecardOcrResultDto>.Fail("Cannot scan a scorecard — this round is no longer active.");

        var courseHoles = await _courseRepository.GetHolesAsync(round.CourseId, cancellationToken);
        var relevantHoles = round.NineHoleSide == NineHoleSide.Back
            ? courseHoles.Where(h => h.HoleNumber >= 10).OrderBy(h => h.HoleNumber).ToList()
            : courseHoles.Where(h => h.HoleNumber <= 9).OrderBy(h => h.HoleNumber).ToList();
        var holeNumbers = relevantHoles.Select(h => h.HoleNumber).ToList();

        var candidatePlayers = teeTime.Participants
            .Where(p => !p.IsWithdrawn && !p.SkippedWeek)
            .Select(p => new ScorecardOcrCandidatePlayer(p.PlayerId, p.Player.FullName, p.Player.Initials))
            .ToList();

        var ocrResult = await _ocrService.ParseAsync(request.ImageBytes, candidatePlayers, holeNumbers, cancellationToken);

        var playerDtos = ocrResult.Players
            .Select(p => new ScorecardOcrPlayerDto(
                p.RawOcrName,
                p.MatchedPlayerId,
                p.MatchedPlayerName,
                p.Holes.Select(h => new ScorecardOcrHoleDto(h.HoleNumber, h.GrossStrokes, h.Confidence)).ToList()))
            .ToList();

        return Result<ScorecardOcrResultDto>.Ok(new ScorecardOcrResultDto(playerDtos));
    }
}
