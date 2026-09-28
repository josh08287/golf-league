using GolfLeague.Application.Common;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Opts a tee-time group out of a side game it previously opted into. Any
/// player currently in the group may opt out on the group's behalf.
/// </summary>
public sealed record OptOutTeeTimeSideGameCommand(
    int TeeTimeId,
    int SideGameId,
    int PlayerId,
    string UserId) : IRequest<Result<bool>>, IAmAuditableCommand
{
    public string AuditEntityType => "TeeTime";
    public string AuditEntityId => TeeTimeId.ToString();
}

public sealed class OptOutTeeTimeSideGameCommandHandler
    : IRequestHandler<OptOutTeeTimeSideGameCommand, Result<bool>>
{
    private readonly ITeeTimeRepository _teeTimeRepository;
    private readonly ITeeTimeSideGameRepository _sideGameRepository;

    public OptOutTeeTimeSideGameCommandHandler(
        ITeeTimeRepository teeTimeRepository,
        ITeeTimeSideGameRepository sideGameRepository)
    {
        _teeTimeRepository = teeTimeRepository;
        _sideGameRepository = sideGameRepository;
    }

    public async Task<Result<bool>> Handle(OptOutTeeTimeSideGameCommand request, CancellationToken cancellationToken)
    {
        var teeTime = await _teeTimeRepository.GetByIdAsync(request.TeeTimeId, cancellationToken);
        if (teeTime is null)
            return Result<bool>.Fail($"Tee time {request.TeeTimeId} not found.");

        if (teeTime.Participants.All(p => p.PlayerId != request.PlayerId))
            return Result<bool>.Fail("You aren't a player in this tee-time group.");

        var sideGame = await _sideGameRepository.GetByIdAsync(request.SideGameId, cancellationToken);
        if (sideGame is null || sideGame.TeeTimeId != request.TeeTimeId)
            return Result<bool>.Fail($"Side game {request.SideGameId} not found for this tee time.");

        await _sideGameRepository.RemoveAsync(request.SideGameId, cancellationToken);
        return Result<bool>.Ok(true);
    }
}
