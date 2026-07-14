using GolfLeague.Application.Common;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Queries;

public sealed record AvailableSubstituteDto(int PlayerId, string FullName, bool IsActive);

/// <summary>
/// Lists substitute-pool players available to add to a specific round —
/// every IsSubstitute player (active first) except ones already seated in
/// this round. Backs the player-facing "add a substitute" picker.
/// </summary>
public sealed record GetAvailableSubstitutesQuery(int RoundId) : IRequest<Result<IReadOnlyList<AvailableSubstituteDto>>>;

public sealed class GetAvailableSubstitutesQueryHandler
    : IRequestHandler<GetAvailableSubstitutesQuery, Result<IReadOnlyList<AvailableSubstituteDto>>>
{
    private readonly IPlayerRepository _playerRepository;
    private readonly IRoundRepository _roundRepository;

    public GetAvailableSubstitutesQueryHandler(IPlayerRepository playerRepository, IRoundRepository roundRepository)
    {
        _playerRepository = playerRepository;
        _roundRepository = roundRepository;
    }

    public async Task<Result<IReadOnlyList<AvailableSubstituteDto>>> Handle(GetAvailableSubstitutesQuery request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<IReadOnlyList<AvailableSubstituteDto>>.Fail($"Round {request.RoundId} not found.");

        var alreadyInRound = round.Participants.Select(p => p.PlayerId).ToHashSet();

        var players = (await _playerRepository.GetAllAsync(cancellationToken))
            .Where(p => p.IsSubstitute && !alreadyInRound.Contains(p.Id))
            .OrderByDescending(p => p.IsActive)
            .ThenBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .Select(p => new AvailableSubstituteDto(p.Id, p.FullName, p.IsActive))
            .ToList();

        return Result<IReadOnlyList<AvailableSubstituteDto>>.Ok(players);
    }
}
