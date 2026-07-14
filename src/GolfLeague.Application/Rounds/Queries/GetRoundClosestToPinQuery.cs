using GolfLeague.Application.Common;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Queries;

// ── DTOs ─────────────────────────────────────────────────────────────────────

/// <summary>A par-3 hole of the round and its current closest-to-pin winner (null = none selected).</summary>
public sealed record ClosestToPinHoleDto(
    int HoleNumber,
    int Par,
    int? PlayerId,
    string? PlayerName);

/// <summary>A round participant eligible to be picked as a closest-to-pin winner.</summary>
public sealed record ClosestToPinParticipantDto(
    int PlayerId,
    string PlayerName);

public sealed record RoundClosestToPinDto(
    int RoundId,
    List<ClosestToPinHoleDto> Par3Holes,
    List<ClosestToPinParticipantDto> Participants);

// ── Query + Handler ──────────────────────────────────────────────────────────

/// <summary>
/// Returns the round's par-3 holes with any recorded closest-to-pin winners,
/// plus the active (non-withdrawn, non-skipped) participants of the whole
/// round for the winner picker.
/// </summary>
public sealed record GetRoundClosestToPinQuery(int RoundId) : IRequest<Result<RoundClosestToPinDto>>;

public sealed class GetRoundClosestToPinQueryHandler
    : IRequestHandler<GetRoundClosestToPinQuery, Result<RoundClosestToPinDto>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ICourseRepository _courseRepository;

    public GetRoundClosestToPinQueryHandler(
        IRoundRepository roundRepository,
        ICourseRepository courseRepository)
    {
        _roundRepository = roundRepository;
        _courseRepository = courseRepository;
    }

    public async Task<Result<RoundClosestToPinDto>> Handle(
        GetRoundClosestToPinQuery request,
        CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<RoundClosestToPinDto>.Fail($"Round {request.RoundId} not found.");

        var allHoles = await _courseRepository.GetHolesAsync(round.CourseId, cancellationToken);
        var par3Holes = (round.NineHoleSide == NineHoleSide.Back
                ? allHoles.Where(h => h.HoleNumber >= 10)
                : allHoles.Where(h => h.HoleNumber <= 9))
            .Where(h => h.Par == 3)
            .OrderBy(h => h.HoleNumber)
            .ToList();

        var winners = await _roundRepository.GetClosestToPinWinnersAsync(request.RoundId, cancellationToken);
        var winnersByHole = winners.ToDictionary(w => w.HoleNumber);

        var holeDtos = par3Holes
            .Select(h =>
            {
                winnersByHole.TryGetValue(h.HoleNumber, out var winner);
                return new ClosestToPinHoleDto(
                    h.HoleNumber,
                    h.Par,
                    winner?.PlayerId,
                    winner?.Player?.FullName);
            })
            .ToList();

        var participants = await _roundRepository.GetParticipantsAsync(request.RoundId, cancellationToken);
        var participantDtos = participants
            .Where(p => !p.IsWithdrawn && !p.SkippedWeek)
            .OrderBy(p => p.Player?.LastName)
            .ThenBy(p => p.Player?.FirstName)
            .Select(p => new ClosestToPinParticipantDto(p.PlayerId, p.Player?.FullName ?? string.Empty))
            .ToList();

        return Result<RoundClosestToPinDto>.Ok(
            new RoundClosestToPinDto(round.Id, holeDtos, participantDtos));
    }
}
