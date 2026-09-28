using GolfLeague.Application.Common;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

public sealed record WolfHolePickResultDto(
    int HoleNumber,
    int WolfParticipantId,
    string WolfPlayerName,
    bool IsLoneWolf,
    bool IsBlindWolf,
    int? PartnerParticipantId,
    string? PartnerPlayerName);

/// <summary>
/// Records one hole's Wolf call: who the current-turn Wolf picked as a
/// partner, or that they went it alone (lone wolf, or the higher-stakes
/// blind wolf — declared before watching anyone else's tee shot). Any
/// active member of the tee-time group may call this. The Wolf for the hole
/// is derived from the rotation order set at opt-in (WolfParticipantId is
/// cross-checked against it, not trusted from the caller alone).
/// </summary>
public sealed record SetWolfHolePickCommand(
    int TeeTimeId,
    int SideGameId,
    int HoleNumber,
    int WolfParticipantId,
    bool IsLoneWolf,
    bool IsBlindWolf,
    int? PartnerParticipantId,
    int SubmittedByPlayerId,
    string UserId) : IRequest<Result<WolfHolePickResultDto>>, IAmAuditableCommand
{
    public string AuditEntityType => "TeeTime";
    public string AuditEntityId => TeeTimeId.ToString();
}

public sealed class SetWolfHolePickCommandHandler
    : IRequestHandler<SetWolfHolePickCommand, Result<WolfHolePickResultDto>>
{
    private readonly ITeeTimeRepository _teeTimeRepository;
    private readonly ITeeTimeSideGameRepository _sideGameRepository;

    public SetWolfHolePickCommandHandler(
        ITeeTimeRepository teeTimeRepository,
        ITeeTimeSideGameRepository sideGameRepository)
    {
        _teeTimeRepository = teeTimeRepository;
        _sideGameRepository = sideGameRepository;
    }

    public async Task<Result<WolfHolePickResultDto>> Handle(SetWolfHolePickCommand request, CancellationToken cancellationToken)
    {
        var teeTime = await _teeTimeRepository.GetByIdAsync(request.TeeTimeId, cancellationToken);
        if (teeTime is null)
            return Result<WolfHolePickResultDto>.Fail($"Tee time {request.TeeTimeId} not found.");

        var submitter = teeTime.Participants.FirstOrDefault(p => p.PlayerId == request.SubmittedByPlayerId);
        if (submitter is null || submitter.IsWithdrawn)
            return Result<WolfHolePickResultDto>.Fail("You must be an active member of this tee time to record this pick.");

        var sideGame = await _sideGameRepository.GetByIdAsync(request.SideGameId, cancellationToken);
        if (sideGame is null || sideGame.TeeTimeId != request.TeeTimeId)
            return Result<WolfHolePickResultDto>.Fail($"Side game {request.SideGameId} not found for this tee time.");
        if (sideGame.GameType != SideGameType.Wolf)
            return Result<WolfHolePickResultDto>.Fail("This side game isn't Wolf.");

        var rotation = sideGame.Teams.OrderBy(t => t.TeamNumber).Select(t => t.ParticipantId).ToList();
        if (rotation.Count == 0)
            return Result<WolfHolePickResultDto>.Fail("This Wolf game has no rotation order configured.");

        var expectedWolfParticipantId = rotation[(request.HoleNumber - 1) % rotation.Count];
        if (expectedWolfParticipantId != request.WolfParticipantId)
            return Result<WolfHolePickResultDto>.Fail("It isn't this player's turn to be the Wolf on this hole.");

        var wolf = teeTime.Participants.FirstOrDefault(p => p.Id == request.WolfParticipantId && !p.IsWithdrawn);
        if (wolf is null)
            return Result<WolfHolePickResultDto>.Fail("The Wolf must be an active member of this tee time group.");

        if (request.IsBlindWolf && !request.IsLoneWolf)
            return Result<WolfHolePickResultDto>.Fail("Blind wolf is always a lone-wolf call.");

        RoundParticipant? partner = null;
        if (!request.IsLoneWolf)
        {
            if (request.PartnerParticipantId is not int partnerId)
                return Result<WolfHolePickResultDto>.Fail("A partner is required unless going lone wolf.");
            if (partnerId == request.WolfParticipantId)
                return Result<WolfHolePickResultDto>.Fail("The Wolf can't partner with themselves.");

            partner = teeTime.Participants.FirstOrDefault(p => p.Id == partnerId && !p.IsWithdrawn);
            if (partner is null)
                return Result<WolfHolePickResultDto>.Fail("The partner must be an active member of this tee time group.");
        }

        await _sideGameRepository.UpsertWolfPickAsync(
            request.SideGameId, request.HoleNumber, wolf.Id, request.IsLoneWolf, request.IsBlindWolf, partner?.Id, request.SubmittedByPlayerId, cancellationToken);

        return Result<WolfHolePickResultDto>.Ok(new WolfHolePickResultDto(
            request.HoleNumber, wolf.Id, wolf.Player.FullName, request.IsLoneWolf, request.IsBlindWolf, partner?.Id, partner?.Player.FullName));
    }
}
