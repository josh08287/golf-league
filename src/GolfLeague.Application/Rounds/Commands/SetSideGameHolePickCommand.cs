using GolfLeague.Application.Common;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

public sealed record SideGameHolePickResultDto(int HoleNumber, BbbHonor Honor, int? WinnerParticipantId, string? WinnerPlayerName);

/// <summary>
/// Records (or clears) one Bingo Bango Bongo honor's winner for one hole,
/// saved immediately as whoever is entering scores picks it — same pattern
/// as SetTeeTimeTournamentCtpCommand. Any active member of the tee-time
/// group may call this; the winner must be a member of that same group.
/// </summary>
public sealed record SetSideGameHolePickCommand(
    int TeeTimeId,
    int SideGameId,
    int HoleNumber,
    BbbHonor Honor,
    int? WinnerParticipantId,
    int SubmittedByPlayerId,
    string UserId) : IRequest<Result<SideGameHolePickResultDto>>, IAmAuditableCommand
{
    public string AuditEntityType => "TeeTime";
    public string AuditEntityId => TeeTimeId.ToString();
}

public sealed class SetSideGameHolePickCommandHandler
    : IRequestHandler<SetSideGameHolePickCommand, Result<SideGameHolePickResultDto>>
{
    private readonly ITeeTimeRepository _teeTimeRepository;
    private readonly ITeeTimeSideGameRepository _sideGameRepository;

    public SetSideGameHolePickCommandHandler(
        ITeeTimeRepository teeTimeRepository,
        ITeeTimeSideGameRepository sideGameRepository)
    {
        _teeTimeRepository = teeTimeRepository;
        _sideGameRepository = sideGameRepository;
    }

    public async Task<Result<SideGameHolePickResultDto>> Handle(SetSideGameHolePickCommand request, CancellationToken cancellationToken)
    {
        var teeTime = await _teeTimeRepository.GetByIdAsync(request.TeeTimeId, cancellationToken);
        if (teeTime is null)
            return Result<SideGameHolePickResultDto>.Fail($"Tee time {request.TeeTimeId} not found.");

        var submitter = teeTime.Participants.FirstOrDefault(p => p.PlayerId == request.SubmittedByPlayerId);
        if (submitter is null || submitter.IsWithdrawn)
            return Result<SideGameHolePickResultDto>.Fail("You must be an active member of this tee time to record this pick.");

        var sideGame = await _sideGameRepository.GetByIdAsync(request.SideGameId, cancellationToken);
        if (sideGame is null || sideGame.TeeTimeId != request.TeeTimeId)
            return Result<SideGameHolePickResultDto>.Fail($"Side game {request.SideGameId} not found for this tee time.");
        if (sideGame.GameType != SideGameType.BingoBangoBongo)
            return Result<SideGameHolePickResultDto>.Fail("This side game isn't Bingo Bango Bongo.");

        RoundParticipant? winner = null;
        if (request.WinnerParticipantId is int winnerId)
        {
            winner = teeTime.Participants.FirstOrDefault(p => p.Id == winnerId && !p.IsWithdrawn);
            if (winner is null)
                return Result<SideGameHolePickResultDto>.Fail("The winner must be an active member of this tee time group.");
        }

        await _sideGameRepository.UpsertHolePickAsync(
            request.SideGameId, request.HoleNumber, request.Honor, winner?.Id, request.SubmittedByPlayerId, cancellationToken);

        return Result<SideGameHolePickResultDto>.Ok(new SideGameHolePickResultDto(request.HoleNumber, request.Honor, winner?.Id, winner?.Player.FullName));
    }
}
