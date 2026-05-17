using GolfLeague.Application.Common;
using GolfLeague.Application.Rounds.Queries;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

public sealed record SetLongestDriveWinnersCommand(
    int RoundId,
    List<int> PlayerIds,
    string UserId) : IRequest<Result<List<LongestDriveWinnerDto>>>, IAmAuditableCommand;

public sealed class SetLongestDriveWinnersCommandHandler : IRequestHandler<SetLongestDriveWinnersCommand, Result<List<LongestDriveWinnerDto>>>
{
    private readonly IRoundRepository _roundRepository;

    public SetLongestDriveWinnersCommandHandler(IRoundRepository roundRepository)
    {
        _roundRepository = roundRepository;
    }

    public async Task<Result<List<LongestDriveWinnerDto>>> Handle(SetLongestDriveWinnersCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<List<LongestDriveWinnerDto>>.Fail($"Round {request.RoundId} not found.");
        if (round.RoundType != RoundType.Tournament)
            return Result<List<LongestDriveWinnerDto>>.Fail("This round is not a tournament round.");
        if (round.Status == RoundStatus.Finalized)
            return Result<List<LongestDriveWinnerDto>>.Fail("Cannot modify a finalized round.");

        await _roundRepository.SetLongestDriveWinnersAsync(request.RoundId, request.PlayerIds, cancellationToken);

        var saved = await _roundRepository.GetLongestDriveWinnersAsync(request.RoundId, cancellationToken);
        var dtos = saved.Select(w => new LongestDriveWinnerDto(w.PlayerId, w.Player.FullName)).ToList();
        return Result<List<LongestDriveWinnerDto>>.Ok(dtos);
    }
}
