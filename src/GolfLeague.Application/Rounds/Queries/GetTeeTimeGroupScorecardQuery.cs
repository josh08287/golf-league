using GolfLeague.Application.Common;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;
using static GolfLeague.Application.Common.FlightDisplayName;

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
    int? TotalNetStablefordPoints,
    int? TournamentFlightId,
    string? TournamentFlightName);

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
    bool? Gir,
    int? LastModifiedByPlayerId,
    string? LastModifiedByPlayerName);

/// <summary>Existing closest-to-pin winner (if any) for one par-3 hole, tournament rounds only.</summary>
public sealed record TeeTimeCtpHoleDto(int HoleNumber, int? WinnerPlayerId);

/// <summary>
/// Existing longest-drive winner (if any) for one tournament flight
/// represented in this tee-time group.
/// </summary>
public sealed record TeeTimeLongestDriveFlightDto(int TournamentFlightId, string FlightName, int? WinnerPlayerId);

/// <summary>
/// Complete scorecard for a tee time group including all players and their scores.
/// </summary>
public sealed record TeeTimeGroupScorecardDto(
    int RoundId,
    DateOnly RoundDate,
    string CourseName,
    int CourseId,
    NineHoleSide NineHoleSide,
    RoundType RoundType,
    RoundStatus RoundStatus,
    int TeeTimeId,
    string ScheduledTimeFormatted,
    int TeeTimeNumber,
    int? LongestDriveHoleNumber,
    List<CourseHoleInfoDto> Holes,
    List<TeeTimePlayerScoreDto> Players,
    List<TeeTimeCtpHoleDto> TournamentCtp,
    List<TeeTimeLongestDriveFlightDto> TournamentLongestDrive);

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
        var relevantHoles = round.NineHoleSide switch
        {
            NineHoleSide.Back => allHoles.Where(h => h.HoleNumber >= 10).OrderBy(h => h.HoleNumber).ToList(),
            NineHoleSide.Front => allHoles.Where(h => h.HoleNumber <= 9).OrderBy(h => h.HoleNumber).ToList(),
            // NotApplicable (18-hole rounds, e.g. tournaments) plays every hole.
            _ => allHoles.OrderBy(h => h.HoleNumber).ToList(),
        };

        // Normalize stroke indices to 1–9 rank within this nine so the frontend
        // can apply the same algorithm as StrokesOnHole(courseHandicap, strokeIndex, allIndices).
        var sortedStrokeIndices = relevantHoles.Select(h => h.StrokeIndex).OrderBy(si => si).ToList();
        var holeDtos = relevantHoles
            .Select(h => new CourseHoleInfoDto(
                h.HoleNumber,
                h.Par,
                sortedStrokeIndices.IndexOf(h.StrokeIndex) + 1))
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
                    h.Gir,
                    h.LastModifiedByPlayerId,
                    h.LastModifiedByPlayerId.HasValue
                        ? teeTime.Participants.FirstOrDefault(p => p.PlayerId == h.LastModifiedByPlayerId.Value)?.Player.FullName
                        : null))
                .ToList();

            playerDtos.Add(new TeeTimePlayerScoreDto(
                participant.Id,
                participant.PlayerId,
                participant.Player.FullName,
                participant.Player.Initials,
                participant.FlightId,
                participant.Flight is null ? string.Empty : Format(participant.Flight),
                participant.HandicapIndex,
                participant.CourseHandicap,
                participant.IsWithdrawn,
                participant.SkippedWeek,
                holeScoreDtos,
                participant.TotalGrossStrokes,
                participant.TotalNetStrokes,
                participant.TotalGrossStablefordPoints,
                participant.TotalNetStablefordPoints,
                participant.TournamentFlightId,
                participant.TournamentFlight?.Name));
        }

        var ctpDtos = new List<TeeTimeCtpHoleDto>();
        var ldDtos = new List<TeeTimeLongestDriveFlightDto>();
        if (round.RoundType == RoundType.Tournament)
        {
            var par3Holes = relevantHoles.Where(h => h.Par == 3).Select(h => h.HoleNumber).ToHashSet();
            if (par3Holes.Count > 0)
            {
                var extras = await _roundRepository.GetTournamentHoleExtrasAsync(round.Id, cancellationToken);
                var extrasByHole = extras.ToDictionary(e => e.HoleNumber);
                ctpDtos = par3Holes
                    .OrderBy(h => h)
                    .Select(h => new TeeTimeCtpHoleDto(h, extrasByHole.TryGetValue(h, out var e) ? e.ClosestToPinPlayerId : null))
                    .ToList();
            }

            var groupFlightIds = teeTime.Participants
                .Where(p => p.TournamentFlightId.HasValue)
                .Select(p => p.TournamentFlightId!.Value)
                .Distinct()
                .ToList();
            if (groupFlightIds.Count > 0)
            {
                var flights = await _roundRepository.GetTournamentFlightsAsync(round.Id, cancellationToken);
                var winners = await _roundRepository.GetLongestDriveWinnersAsync(round.Id, cancellationToken);
                var winnersByFlight = winners.ToDictionary(w => w.TournamentFlightId);
                ldDtos = flights
                    .Where(f => groupFlightIds.Contains(f.Id))
                    .OrderBy(f => f.FlightNumber)
                    .Select(f => new TeeTimeLongestDriveFlightDto(f.Id, f.Name, winnersByFlight.TryGetValue(f.Id, out var w) ? w.PlayerId : null))
                    .ToList();
            }
        }

        var dto = new TeeTimeGroupScorecardDto(
            round.Id,
            round.RoundDate,
            course.Name,
            course.Id,
            round.NineHoleSide,
            round.RoundType,
            round.Status,
            teeTime.Id,
            teeTime.ScheduledTime.ToString("HH:mm"),
            teeTime.TeeTimeNumber,
            round.LongestDriveHoleNumber,
            holeDtos,
            playerDtos,
            ctpDtos,
            ldDtos);

        return Result<TeeTimeGroupScorecardDto>.Ok(dto);
    }
}
