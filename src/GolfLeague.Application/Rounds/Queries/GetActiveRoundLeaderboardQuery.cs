using GolfLeague.Application.Common;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;
using static GolfLeague.Application.Common.FlightDisplayName;

namespace GolfLeague.Application.Rounds.Queries;

public sealed record ActiveRoundLeaderboardHoleDto(
    int HoleNumber,
    int Par,
    int StrokeIndex,
    int GrossStrokes,
    int HandicapStrokes);

public sealed record ActiveRoundLeaderboardEntryDto(
    int PlayerId,
    string PlayerName,
    int? FlightId,
    string FlightName,
    int ThruHoles,
    int? GrossScore,
    int? NetScore,
    int? GrossPoints,
    int? NetPoints,
    int Rank,
    List<ActiveRoundLeaderboardHoleDto> Holes);

public sealed record ActiveRoundLeaderboardFlightDto(
    int? FlightId,
    string FlightName,
    List<ActiveRoundLeaderboardEntryDto> Entries);

public sealed record ActiveRoundLeaderboardDto(
    int RoundId,
    string CourseName,
    DateOnly ScheduledDate,
    string NineHoleSide,
    bool IsTournament,
    List<ActiveRoundLeaderboardFlightDto> Flights);

public sealed record GetActiveRoundLeaderboardQuery : IRequest<Result<ActiveRoundLeaderboardDto?>>;

public sealed class GetActiveRoundLeaderboardQueryHandler
    : IRequestHandler<GetActiveRoundLeaderboardQuery, Result<ActiveRoundLeaderboardDto?>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IFlightRepository _flightRepository;

    public GetActiveRoundLeaderboardQueryHandler(
        IRoundRepository roundRepository,
        ICourseRepository courseRepository,
        IFlightRepository flightRepository)
    {
        _roundRepository = roundRepository;
        _courseRepository = courseRepository;
        _flightRepository = flightRepository;
    }

    public async Task<Result<ActiveRoundLeaderboardDto?>> Handle(GetActiveRoundLeaderboardQuery request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetInProgressRoundAsync(cancellationToken);
        if (round is null)
            return Result<ActiveRoundLeaderboardDto?>.Ok(null);

        var course = await _courseRepository.GetByIdAsync(round.CourseId, cancellationToken);

        if (round.RoundType == RoundType.Tournament)
        {
            return Result<ActiveRoundLeaderboardDto?>.Ok(new ActiveRoundLeaderboardDto(
                round.Id,
                course?.Name ?? string.Empty,
                round.RoundDate,
                round.NineHoleSide.ToString(),
                true,
                []));
        }

        var participants = await _roundRepository.GetParticipantsAsync(round.Id, cancellationToken);
        var flights = round.HalfId.HasValue
            ? await _flightRepository.GetByHalfAsync(round.HalfId.Value, cancellationToken)
            : [];
        var courseName = course?.Name ?? string.Empty;
        var flightNameLookup = flights.ToDictionary(f => f.Id, f => Format(f));

        var flightGroups = participants
            .Where(p => !p.IsWithdrawn && !p.SkippedWeek && !p.IsSubstitute)
            .GroupBy(p => p.FlightId)
            .Select(group =>
            {
                var flightId = group.Key;
                var flightName = flightId.HasValue && flightNameLookup.TryGetValue(flightId.Value, out var name)
                    ? name
                    : string.Empty;

                var ranked = group
                    .OrderByDescending(p => p.TotalNetStablefordPoints ?? int.MinValue)
                    .ThenBy(p => p.TotalGrossStrokes ?? int.MaxValue)
                    .ToList();

                var entries = new List<ActiveRoundLeaderboardEntryDto>();
                var rank = 0;
                var previousPoints = (int?)null;
                var previousGross = (int?)null;
                for (var i = 0; i < ranked.Count; i++)
                {
                    var p = ranked[i];
                    var samePoints = p.TotalNetStablefordPoints == previousPoints && p.TotalGrossStrokes == previousGross;
                    if (!samePoints)
                        rank = i + 1;

                    var holes = p.HoleScores
                        .OrderBy(h => h.HoleNumber)
                        .Select(h => new ActiveRoundLeaderboardHoleDto(
                            h.HoleNumber,
                            h.Par,
                            h.StrokeIndex,
                            h.GrossStrokes,
                            h.HandicapStrokes))
                        .ToList();

                    entries.Add(new ActiveRoundLeaderboardEntryDto(
                        p.PlayerId,
                        p.Player.FullName,
                        p.FlightId,
                        flightName,
                        p.HoleScores.Count,
                        p.TotalGrossStrokes,
                        p.TotalNetStrokes,
                        p.TotalGrossStablefordPoints,
                        p.TotalNetStablefordPoints,
                        rank,
                        holes));

                    previousPoints = p.TotalNetStablefordPoints;
                    previousGross = p.TotalGrossStrokes;
                }

                return new ActiveRoundLeaderboardFlightDto(flightId, flightName, entries);
            })
            .OrderBy(f => f.FlightId ?? int.MaxValue)
            .ToList();

        var dto = new ActiveRoundLeaderboardDto(
            round.Id,
            courseName,
            round.RoundDate,
            round.NineHoleSide.ToString(),
            false,
            flightGroups);

        return Result<ActiveRoundLeaderboardDto?>.Ok(dto);
    }
}
