using GolfLeague.Application.Common;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

public sealed record HoleExtraInput(
    int HoleNumber,
    int? ClosestToPinPlayerId,
    int? LongestDrivePlayerId);

public sealed record TournamentHoleExtraDto(
    int HoleNumber,
    int? ClosestToPinPlayerId,
    string? ClosestToPinPlayerName,
    int? LongestDrivePlayerId,
    string? LongestDrivePlayerName);

public sealed record SaveTournamentExtrasCommand(
    int RoundId,
    List<HoleExtraInput> HoleExtras,
    string UserId) : IRequest<Result<List<TournamentHoleExtraDto>>>, IAmAuditableCommand
{
    public string AuditEntityType => "Round";
    public string AuditEntityId => RoundId.ToString();
}

public sealed class SaveTournamentExtrasCommandHandler : IRequestHandler<SaveTournamentExtrasCommand, Result<List<TournamentHoleExtraDto>>>
{
    private readonly IRoundRepository _roundRepository;

    public SaveTournamentExtrasCommandHandler(IRoundRepository roundRepository)
    {
        _roundRepository = roundRepository;
    }

    public async Task<Result<List<TournamentHoleExtraDto>>> Handle(SaveTournamentExtrasCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<List<TournamentHoleExtraDto>>.Fail($"Round {request.RoundId} not found.");
        if (round.RoundType != RoundType.Tournament)
            return Result<List<TournamentHoleExtraDto>>.Fail("This round is not a tournament round.");
        if (round.Status == RoundStatus.Finalized)
            return Result<List<TournamentHoleExtraDto>>.Fail("Cannot modify extras for a finalized round.");

        if (request.HoleExtras.Count == 0)
            return Result<List<TournamentHoleExtraDto>>.Ok([]);

        var extras = request.HoleExtras.Select(e => new TournamentHoleExtra
        {
            RoundId = request.RoundId,
            HoleNumber = e.HoleNumber,
            ClosestToPinPlayerId = e.ClosestToPinPlayerId,
            LongestDrivePlayerId = e.LongestDrivePlayerId,
        }).ToList();

        await _roundRepository.UpsertTournamentHoleExtrasAsync(extras, cancellationToken);

        var saved = await _roundRepository.GetTournamentHoleExtrasAsync(request.RoundId, cancellationToken);
        var dtos = saved.Select(e => new TournamentHoleExtraDto(
            e.HoleNumber,
            e.ClosestToPinPlayerId,
            e.ClosestToPinPlayer?.FullName,
            e.LongestDrivePlayerId,
            e.LongestDrivePlayer?.FullName)).ToList();

        return Result<List<TournamentHoleExtraDto>>.Ok(dtos);
    }
}
