using GolfLeague.Application.Common;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using MediatR;
using DomainEnums = GolfLeague.Domain.Enums;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>One participant's team assignment for a team-based side game (2v2 best ball, or team Nassau).</summary>
public sealed record SideGameTeamAssignment(int ParticipantId, int TeamNumber);

/// <summary>
/// Result of opting in. Deliberately a plain DTO, not the tracked
/// TeeTimeSideGame entity — that entity's navigation properties
/// (Teams[].SideGame pointing back to itself, TeeTime, etc.) form a
/// reference cycle that System.Text.Json throws on by default, which
/// previously surfaced as a "failed to opt in" error on the client even
/// though the opt-in had already been committed to the database.
/// </summary>
public sealed record OptInSideGameResultDto(
    int Id,
    DomainEnums.SideGameType GameType,
    DomainEnums.ScoringBasis ScoringBasis,
    DomainEnums.NassauFormat? NassauFormat);

/// <summary>
/// Opts a tee-time group into an optional side game (Nassau, 2v2 best ball,
/// Bingo Bango Bongo, or Wolf). Any player currently in the group may opt in
/// on the group's behalf. Every player still enters their own gross/net
/// score exactly as normal — opting in never changes that, though Bingo
/// Bango Bongo and Wolf additionally need a few per-hole picks captured
/// alongside score entry (see SetSideGameHolePickCommand / SetWolfHolePickCommand).
/// </summary>
public sealed record OptInTeeTimeSideGameCommand(
    int TeeTimeId,
    DomainEnums.SideGameType GameType,
    DomainEnums.ScoringBasis? ScoringBasis,
    DomainEnums.NassauFormat? NassauFormat,
    List<SideGameTeamAssignment>? Teams,
    /// <summary>Wolf only: participant ids in tee-off/rotation order (≥4 active players required).</summary>
    List<int>? WolfRotationOrder,
    int PlayerId,
    string UserId) : IRequest<Result<OptInSideGameResultDto>>, IAmAuditableCommand
{
    public string AuditEntityType => "TeeTime";
    public string AuditEntityId => TeeTimeId.ToString();
}

public sealed class OptInTeeTimeSideGameCommandHandler
    : IRequestHandler<OptInTeeTimeSideGameCommand, Result<OptInSideGameResultDto>>
{
    private readonly ITeeTimeRepository _teeTimeRepository;
    private readonly IRoundRepository _roundRepository;
    private readonly ITeeTimeSideGameRepository _sideGameRepository;

    public OptInTeeTimeSideGameCommandHandler(
        ITeeTimeRepository teeTimeRepository,
        IRoundRepository roundRepository,
        ITeeTimeSideGameRepository sideGameRepository)
    {
        _teeTimeRepository = teeTimeRepository;
        _roundRepository = roundRepository;
        _sideGameRepository = sideGameRepository;
    }

    public async Task<Result<OptInSideGameResultDto>> Handle(OptInTeeTimeSideGameCommand request, CancellationToken cancellationToken)
    {
        var teeTime = await _teeTimeRepository.GetByIdAsync(request.TeeTimeId, cancellationToken);
        if (teeTime is null)
            return Result<OptInSideGameResultDto>.Fail($"Tee time {request.TeeTimeId} not found.");

        var activeParticipants = teeTime.Participants.Where(p => !p.IsWithdrawn && !p.SkippedWeek).ToList();
        if (activeParticipants.All(p => p.PlayerId != request.PlayerId))
            return Result<OptInSideGameResultDto>.Fail("You aren't an active player in this tee-time group.");

        var round = await _roundRepository.GetByIdAsync(teeTime.RoundId, cancellationToken);
        if (round is null)
            return Result<OptInSideGameResultDto>.Fail($"Round for tee time {request.TeeTimeId} not found.");

        var holeCount = round.NineHoleSide == DomainEnums.NineHoleSide.NotApplicable ? 18 : 9;
        if (!SideGameEligibility.IsValidFor(request.GameType, holeCount))
            return Result<OptInSideGameResultDto>.Fail($"{request.GameType} isn't available for a {holeCount}-hole round.");

        var existing = await _sideGameRepository.GetForTeeTimeAsync(request.TeeTimeId, cancellationToken);
        if (existing.Any(g => g.GameType == request.GameType))
            return Result<OptInSideGameResultDto>.Fail($"This group has already opted into {request.GameType}.");

        var isTeamGame = request.GameType == DomainEnums.SideGameType.TwoVTwoBestBall
            || (request.GameType == DomainEnums.SideGameType.Nassau && request.NassauFormat == DomainEnums.NassauFormat.TeamVsTeam);

        List<TeeTimeSideGameTeam> teams = [];
        if (isTeamGame)
        {
            if (request.Teams is null || request.Teams.Count == 0)
                return Result<OptInSideGameResultDto>.Fail("Team assignments are required for this game.");

            var activeParticipantIds = activeParticipants.Select(p => p.Id).ToHashSet();
            if (request.Teams.Any(t => !activeParticipantIds.Contains(t.ParticipantId)))
                return Result<OptInSideGameResultDto>.Fail("A team assignment references a player not active in this group.");
            if (request.Teams.Select(t => t.ParticipantId).Distinct().Count() != request.Teams.Count)
                return Result<OptInSideGameResultDto>.Fail("Each player can only be assigned to one team.");

            var teamNumbers = request.Teams.Select(t => t.TeamNumber).Distinct().ToList();
            if (teamNumbers.Count != 2 || teamNumbers.Any(n => n is not (1 or 2)))
                return Result<OptInSideGameResultDto>.Fail("Exactly two teams (1 and 2) are required, each with at least one player.");

            teams = request.Teams
                .Select(t => new TeeTimeSideGameTeam { TeamNumber = t.TeamNumber, ParticipantId = t.ParticipantId })
                .ToList();
        }
        var needsScoringBasis = request.GameType is DomainEnums.SideGameType.Nassau
            or DomainEnums.SideGameType.TwoVTwoBestBall
            or DomainEnums.SideGameType.Wolf;
        if (needsScoringBasis && request.ScoringBasis is null)
            return Result<OptInSideGameResultDto>.Fail($"A scoring basis (gross or net) is required for {request.GameType}.");

        if (request.GameType == DomainEnums.SideGameType.Wolf)
        {
            if (request.WolfRotationOrder is null || request.WolfRotationOrder.Count < 4)
                return Result<OptInSideGameResultDto>.Fail("Wolf needs a rotation order of at least 4 active players.");

            var activeParticipantIds = activeParticipants.Select(p => p.Id).ToHashSet();
            if (request.WolfRotationOrder.Any(id => !activeParticipantIds.Contains(id)))
                return Result<OptInSideGameResultDto>.Fail("The Wolf rotation references a player not active in this group.");
            if (request.WolfRotationOrder.Distinct().Count() != request.WolfRotationOrder.Count)
                return Result<OptInSideGameResultDto>.Fail("Each player can only appear once in the Wolf rotation.");

            // Rotation position stored 1-based in TeamNumber, same table 2v2/Nassau use for team sides.
            teams = request.WolfRotationOrder
                .Select((participantId, index) => new TeeTimeSideGameTeam { TeamNumber = index + 1, ParticipantId = participantId })
                .ToList();
        }

        var sideGame = new TeeTimeSideGame
        {
            TeeTimeId = request.TeeTimeId,
            GameType = request.GameType,
            ScoringBasis = request.ScoringBasis ?? DomainEnums.ScoringBasis.Net,
            NassauFormat = request.GameType == DomainEnums.SideGameType.Nassau ? request.NassauFormat : null,
            OptedInByPlayerId = request.PlayerId,
            OptedInAt = DateTime.UtcNow,
            Teams = teams,
        };

        var created = await _sideGameRepository.AddAsync(sideGame, cancellationToken);
        return Result<OptInSideGameResultDto>.Ok(new OptInSideGameResultDto(created.Id, created.GameType, created.ScoringBasis, created.NassauFormat));
    }
}
