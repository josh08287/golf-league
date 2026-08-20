using GolfLeague.Application.Common;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Sets (or clears, when null) the tournament round's gross and net skins
/// pools — the dollar amount split evenly across all skins won in each game
/// (see <see cref="Domain.Entities.Round.GrossSkinsPool"/>). Unlike the
/// longest-drive hole, editable any time up to Finalized: the pool amount
/// is often not settled until players are paying in at the course, well
/// after the round starts.
/// </summary>
public sealed record SetTournamentSkinsPoolCommand(
    int RoundId,
    decimal? GrossSkinsPool,
    decimal? NetSkinsPool,
    string UserId) : IRequest<Result<bool>>, IAmAuditableCommand
{
    public string AuditEntityType => "Round";
    public string AuditEntityId => RoundId.ToString();
}

public sealed class SetTournamentSkinsPoolCommandHandler : IRequestHandler<SetTournamentSkinsPoolCommand, Result<bool>>
{
    private readonly IRoundRepository _roundRepository;

    public SetTournamentSkinsPoolCommandHandler(IRoundRepository roundRepository)
    {
        _roundRepository = roundRepository;
    }

    public async Task<Result<bool>> Handle(SetTournamentSkinsPoolCommand request, CancellationToken cancellationToken)
    {
        if (request.GrossSkinsPool is < 0 || request.NetSkinsPool is < 0)
            return Result<bool>.Fail("Skins pool amounts cannot be negative.");

        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<bool>.Fail($"Round {request.RoundId} not found.");
        if (round.RoundType != RoundType.Tournament)
            return Result<bool>.Fail("This round is not a tournament round.");
        if (round.Status == RoundStatus.Finalized)
            return Result<bool>.Fail("The skins pool can only be changed before the round is finalized.");

        round.GrossSkinsPool = request.GrossSkinsPool;
        round.NetSkinsPool = request.NetSkinsPool;
        await _roundRepository.UpdateAsync(round, cancellationToken);

        return Result<bool>.Ok(true);
    }
}
