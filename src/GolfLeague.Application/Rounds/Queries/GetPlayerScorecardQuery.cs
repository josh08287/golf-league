using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Queries;

public sealed record GetPlayerScorecardQuery(int RoundId, int PlayerId) : IRequest<Result<ScorecardDto>>;

public sealed class GetPlayerScorecardQueryHandler : IRequestHandler<GetPlayerScorecardQuery, Result<ScorecardDto>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IPlayerRepository _playerRepository;

    public GetPlayerScorecardQueryHandler(
        IRoundRepository roundRepository,
        ICourseRepository courseRepository,
        IPlayerRepository playerRepository)
    {
        _roundRepository = roundRepository;
        _courseRepository = courseRepository;
        _playerRepository = playerRepository;
    }

    public async Task<Result<ScorecardDto>> Handle(GetPlayerScorecardQuery request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<ScorecardDto>.Fail($"Round with ID {request.RoundId} not found.");

        var participant = await _roundRepository.GetParticipantAsync(request.RoundId, request.PlayerId, cancellationToken);
        if (participant is null)
            return Result<ScorecardDto>.Fail($"Player {request.PlayerId} is not a participant in round {request.RoundId}.");

        var course = await _courseRepository.GetByIdAsync(round.CourseId, cancellationToken);
        if (course is null)
            return Result<ScorecardDto>.Fail($"Course with ID {round.CourseId} not found.");

        var player = await _playerRepository.GetByIdAsync(request.PlayerId, cancellationToken);
        if (player is null)
            return Result<ScorecardDto>.Fail($"Player with ID {request.PlayerId} not found.");

        var holeScores = await _roundRepository.GetHoleScoresAsync(participant.Id, cancellationToken);

        var holeScoreDtos = holeScores
            .OrderBy(h => h.HoleNumber)
            .Select(h => new HoleScoreDto(
                h.Id,
                h.HoleNumber,
                h.Par,
                h.StrokeIndex,
                h.GrossStrokes,
                h.HandicapStrokes,
                h.NetStrokes,
                h.GrossStablefordPoints,
                h.NetStablefordPoints,
                h.IsMaxScore,
                h.Putts,
                h.FirstPuttDistanceFeet,
                h.FairwayHit,
                h.Gir))
            .ToList();

        var participantDto = new ParticipantDto(
            participant.Id,
            participant.RoundId,
            participant.PlayerId,
            player.FullName,
            player.Initials,
            participant.FlightId,
            participant.HandicapIndex,
            participant.CourseHandicap,
            participant.TotalGrossStrokes,
            participant.TotalNetStrokes,
            participant.TotalGrossStablefordPoints,
            participant.TotalNetStablefordPoints,
            participant.IsWithdrawn,
            participant.SkippedWeek);

        var dto = new ScorecardDto(
            round.Id,
            round.RoundDate,
            course.Name,
            course.CourseRating,
            course.SlopeRating,
            participantDto,
            holeScoreDtos,
            holeScoreDtos.Sum(h => h.Par),
            holeScoreDtos.Sum(h => h.GrossStrokes),
            holeScoreDtos.Sum(h => h.NetStrokes),
            holeScoreDtos.Sum(h => h.GrossStablefordPoints),
            holeScoreDtos.Sum(h => h.NetStablefordPoints));

        return Result<ScorecardDto>.Ok(dto);
    }
}
