using GolfLeague.Application.Common;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Makes substitutes eligible (or ineligible) to win skins in a tournament
/// round. Locked once the round is finalized, like the skins pool itself.
/// </summary>
public sealed record SetTournamentSubstituteSkinsCommand(
    int RoundId,
    bool SubstitutesCanWinSkins,
    string UserId) : IRequest<Result<bool>>, IAmAuditableCommand
{
    public string AuditEntityType => "Round";
    public string AuditEntityId => RoundId.ToString();
}

public sealed class SetTournamentSubstituteSkinsCommandHandler
    : IRequestHandler<SetTournamentSubstituteSkinsCommand, Result<bool>>
{
    private readonly IRoundRepository _roundRepository;

    public SetTournamentSubstituteSkinsCommandHandler(IRoundRepository roundRepository)
    {
        _roundRepository = roundRepository;
    }

    public async Task<Result<bool>> Handle(SetTournamentSubstituteSkinsCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<bool>.Fail($"Round {request.RoundId} not found.");
        if (round.RoundType != RoundType.Tournament)
            return Result<bool>.Fail("This round is not a tournament round.");
        if (round.Status == RoundStatus.Finalized)
            return Result<bool>.Fail("Skins eligibility can only be changed before the round is finalized. Re-open the round to change it.");

        round.SubstitutesCanWinSkins = request.SubstitutesCanWinSkins;
        await _roundRepository.UpdateAsync(round, cancellationToken);

        return Result<bool>.Ok(true);
    }
}
