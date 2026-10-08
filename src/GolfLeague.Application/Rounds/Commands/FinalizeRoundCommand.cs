using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Handicaps;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Application.Rounds.Commands;

public sealed record FinalizeRoundCommand(
    int RoundId,
    string UserId) : IRequest<Result<RoundDto>>, IAmAuditableCommand
{
    public string AuditEntityType => "Round";
    public string AuditEntityId => RoundId.ToString();
}

public sealed class FinalizeRoundCommandHandler : IRequestHandler<FinalizeRoundCommand, Result<RoundDto>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IHandicapRepository _handicapRepository;
    private readonly HandicapRecalculationService _handicapCalc;
    private readonly ILeagueContext _leagueContext;
    private readonly ILogger<FinalizeRoundCommandHandler> _logger;

    public FinalizeRoundCommandHandler(
        IRoundRepository roundRepository,
        ICourseRepository courseRepository,
        IHandicapRepository handicapRepository,
        HandicapRecalculationService handicapCalc,
        ILeagueContext leagueContext,
        ILogger<FinalizeRoundCommandHandler> logger)
    {
        _roundRepository = roundRepository;
        _courseRepository = courseRepository;
        _handicapRepository = handicapRepository;
        _handicapCalc = handicapCalc;
        _leagueContext = leagueContext;
        _logger = logger;
    }

    public async Task<Result<RoundDto>> Handle(FinalizeRoundCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<RoundDto>.Fail($"Round with ID {request.RoundId} not found.");

        if (round.Status == RoundStatus.Finalized)
            return Result<RoundDto>.Fail($"Round {request.RoundId} is already finalized.");

        if (round.Status == RoundStatus.Cancelled)
            return Result<RoundDto>.Fail("Cannot finalize a cancelled round.");

        var course = await _courseRepository.GetByIdAsync(round.CourseId, cancellationToken);
        if (course is null)
            return Result<RoundDto>.Fail($"Course with ID {round.CourseId} not found.");

        var courseHoles = await _courseRepository.GetHolesAsync(round.CourseId, cancellationToken);
        var roundHoles = round.NineHoleSide switch
        {
            NineHoleSide.Back => courseHoles.Where(h => h.HoleNumber >= 10),
            NineHoleSide.Front => courseHoles.Where(h => h.HoleNumber <= 9),
            _ => courseHoles,
        };
        var expectedHoleNumbers = roundHoles.Select(h => h.HoleNumber).ToHashSet();

        var participants = await _roundRepository.GetParticipantsAsync(round.Id, cancellationToken);
        var playing = participants.Where(p => !p.IsWithdrawn && !p.SkippedWeek).ToList();

        // A tournament can't be finalized until every player in the field has a
        // score on every hole — otherwise results, rankings, and skins would be
        // locked in with holes missing. Withdraw or skip anyone who didn't finish.
        if (round.RoundType == RoundType.Tournament)
        {
            var incomplete = playing
                .Select(p => (Player: p, Missing: expectedHoleNumbers.Except(p.HoleScores.Select(h => h.HoleNumber)).OrderBy(h => h).ToList()))
                .Where(x => x.Missing.Count > 0)
                .OrderBy(x => x.Player.Player.FullName)
                .ToList();

            if (incomplete.Count > 0)
            {
                var details = string.Join("; ", incomplete.Select(x =>
                    x.Missing.Count == expectedHoleNumbers.Count
                        ? $"{x.Player.Player.FullName} (no scores)"
                        : $"{x.Player.Player.FullName} (hole{(x.Missing.Count == 1 ? "" : "s")} {string.Join(", ", x.Missing)})"));
                return Result<RoundDto>.Fail(
                    $"Can't finalize yet — {incomplete.Count} player{(incomplete.Count == 1 ? " is" : "s are")} missing scores: {details}. " +
                    "Enter the missing scores, or withdraw players who didn't finish.");
            }
        }

        // Rebuild totals from the saved hole scores, so groups that entered every
        // hole but never pressed Submit are still counted.
        foreach (var participant in playing)
        {
            RoundTotals.Apply(participant, participant.HoleScores.ToList(), expectedHoleNumbers.Count);
            await _roundRepository.UpdateParticipantAsync(participant, cancellationToken);
        }

        // Set-based status update first — avoids reattaching the Round graph
        // (Participants + their HoleScores) which can confuse EF tracking
        // across the subsequent Handicap inserts.
        await _roundRepository.UpdateStatusAsync(round.Id, RoundStatus.Finalized, cancellationToken);
        round.Status = RoundStatus.Finalized;

        // Tournament rounds don't count toward handicaps — league handicaps are
        // built from weekly 9-hole rounds only.
        if (round.RoundType != RoundType.Tournament)
        {
            // Recalculate each finalized participant's handicap index using the
            // league's configured mode and best-X-of-Y window (see
            // HandicapRecalculationService) — not full WHS cap rules.
            var settings = await _handicapCalc.LoadSettingsAsync(_leagueContext.LeagueId ?? 0, cancellationToken);

            foreach (var participant in playing.Where(p => !p.IsSubstitute && p.TotalGrossStrokes.HasValue))
            {
                await RecalculateAndPersistAsync(participant.PlayerId, round.RoundDate, settings, cancellationToken);
            }
        }

        return Result<RoundDto>.Ok(RoundDtoMapper.Map(round, course.Name, round.Participants.Count));
    }

    private async Task RecalculateAndPersistAsync(int playerId, DateOnly roundDate, HandicapCalcSettings settings, CancellationToken cancellationToken)
    {
        var roundInputs = await _handicapRepository
            .GetLastNRoundInputsAsync(
                playerId,
                settings.WindowY,
                asOfDate: roundDate,
                cancellationToken);

        var newIndex = _handicapCalc.CalculateNewIndex(roundInputs, settings);
        if (newIndex is null)
        {
            _logger.LogDebug(
                "Player {PlayerId} has no qualifying rounds yet; skipping handicap recalc.",
                playerId);
            return;
        }

        await _handicapRepository.AddAsync(new Handicap
        {
            PlayerId = playerId,
            HandicapIndex = newIndex.Value,
            EffectiveDate = roundDate,
            Source = HandicapSource.Calculated,
            Notes = $"Recalculated from last {roundInputs.Count} 9-hole round(s)",
        }, cancellationToken);

        _logger.LogInformation(
            "Recalculated handicap for player {PlayerId}: {NewIndex} (over {Count} rounds)",
            playerId, newIndex.Value, roundInputs.Count);
    }
}
