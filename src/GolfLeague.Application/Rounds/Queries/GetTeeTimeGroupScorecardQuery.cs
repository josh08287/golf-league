using GolfLeague.Application.Common;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Queries;

/// <summary>
/// Score information for a single player in a tee time group.
/// </summary>
public sealed record TeeTimePlayerScoreDto(
    int ParticipantId,
    int PlayerId,
    string PlayerName,
    string PlayerInitials,
    int? FlightId,
    string FlightName,
    double HandicapIndex,
    int CourseHandicap,
    bool IsWithdrawn,
    bool SkippedWeek,
    List<TeeTimeHoleScoreDto> HoleScores,
    int? TotalGrossStrokes,
    int? TotalNetStrokes,
    int? TotalGrossStablefordPoints,
    int? TotalNetStablefordPoints);

/// <summary>
/// Score for a single hole (if entered).
/// </summary>
public sealed record TeeTimeHoleScoreDto(
    int HoleNumber,
    int Par,
    int StrokeIndex,
    int? GrossStrokes,
    int? NetStrokes,
    int? GrossStablefordPoints,
    int? NetStablefordPoints,
    int? Putts,
    double? FirstPuttDistanceFeet,
    bool? FairwayHit,
    bool? Gir);

/// <summary>
/// Complete scorecard for a tee time group including all players and their scores.
/// </summary>
public sealed record TeeTimeGroupScorecardDto(
    int RoundId,
    DateOnly RoundDate,
    string CourseName,
    int CourseId,
    NineHoleSide NineHoleSide,
    RoundStatus RoundStatus,
    int TeeTimeId,
    string ScheduledTimeFormatted,
    int TeeTimeNumber,
    List<CourseHoleInfoDto> Holes,
    List<TeeTimePlayerScoreDto> Players);

/// <summary>
/// Basic course hole information.
/// </summary>
public sealed record CourseHoleInfoDto(
    int HoleNumber,
    int Par,
    int StrokeIndex);

public sealed record GetTeeTimeGroupScorecardQuery(int TeeTimeId, int? CallingPlayerId = null)
    : IRequest<Result<TeeTimeGroupScorecardDto>>;

public sealed class GetTeeTimeGroupScorecardQueryHandler
    : IRequestHandler<GetTeeTimeGroupScorecardQuery, Result<TeeTimeGroupScorecardDto>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ITeeTimeRepository _teeTimeRepository;
    private readonly ICourseRepository _courseRepository;

    public GetTeeTimeGroupScorecardQueryHandler(
        IRoundRepository roundRepository,
        ITeeTimeRepository teeTimeRepository,
        ICourseRepository courseRepository)
    {
        _roundRepository = roundRepository;
        _teeTimeRepository = teeTimeRepository;
        _courseRepository = courseRepository;
    }

    public async Task<Result<TeeTimeGroupScorecardDto>> Handle(
        GetTeeTimeGroupScorecardQuery request,
        CancellationToken cancellationToken)
    {
        // Get the tee time with participants
        var teeTime = await _teeTimeRepository.GetByIdAsync(request.TeeTimeId, cancellationToken);
        if (teeTime is null)
            return Result<TeeTimeGroupScorecardDto>.Fail($"Tee time with ID {request.TeeTimeId} not found.");

        // Get the round
        var round = await _roundRepository.GetByIdAsync(teeTime.RoundId, cancellationToken);
        if (round is null)
            return Result<TeeTimeGroupScorecardDto>.Fail($"Round for tee time {request.TeeTimeId} not found.");

        // Get course details for hole info
        var course = await _courseRepository.GetByIdAsync(round.CourseId, cancellationToken);
        if (course is null)
            return Result<TeeTimeGroupScorecardDto>.Fail($"Course with ID {round.CourseId} not found.");

        // Build hole info based on nine-hole side
        var allHoles = await _courseRepository.GetHolesAsync(round.CourseId, cancellationToken);
        var relevantHoles = round.NineHoleSide == NineHoleSide.Back
            ? allHoles.Where(h => h.HoleNumber >= 10).OrderBy(h => h.HoleNumber).ToList()
            : allHoles.Where(h => h.HoleNumber <= 9).OrderBy(h => h.HoleNumber).ToList();

        var holeDtos = relevantHoles
            .Select(h => new CourseHoleInfoDto(h.HoleNumber, h.Par, h.StrokeIndex))
            .ToList();

        // Build player scores
        var playerDtos = new List<TeeTimePlayerScoreDto>();
        foreach (var participant in teeTime.Participants.OrderBy(p => p.Player.LastName).ThenBy(p => p.Player.FirstName))
        {
            var holeScores = await _roundRepository.GetHoleScoresAsync(participant.Id, cancellationToken);
            var holeScoreDtos = holeScores
                .OrderBy(h => h.HoleNumber)
                .Select(h => new TeeTimeHoleScoreDto(
                    h.HoleNumber,
                    h.Par,
                    h.StrokeIndex,
                    h.GrossStrokes,
                    h.NetStrokes,
                    h.GrossStablefordPoints,
                    h.NetStablefordPoints,
                    h.Putts,
                    h.FirstPuttDistanceFeet,
                    h.FairwayHit,
                    h.Gir))
                .ToList();

            playerDtos.Add(new TeeTimePlayerScoreDto(
                participant.Id,
                participant.PlayerId,
                participant.Player.FullName,
                participant.Player.Initials,
                participant.FlightId,
                participant.Flight?.Name ?? string.Empty,
                participant.HandicapIndex,
                participant.CourseHandicap,
                participant.IsWithdrawn,
                participant.SkippedWeek,
                holeScoreDtos,
                participant.TotalGrossStrokes,
                participant.TotalNetStrokes,
                participant.TotalGrossStablefordPoints,
                participant.TotalNetStablefordPoints));
        }

        var dto = new TeeTimeGroupScorecardDto(
            round.Id,
            round.RoundDate,
            course.Name,
            course.Id,
            round.NineHoleSide,
            round.Status,
            teeTime.Id,
            teeTime.ScheduledTime.ToString("HH:mm"),
            teeTime.TeeTimeNumber,
            holeDtos,
            playerDtos);

        return Result<TeeTimeGroupScorecardDto>.Ok(dto);
    }
}
