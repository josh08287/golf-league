using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;
using static GolfLeague.Domain.Services.StablefordScoringService;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Generates the weekly round schedule for a half. One round per week — all flights
/// in the half participate together. NineHoleSide auto-alternates Front/Back starting
/// from <see cref="StartingSide"/>. Replaces any previously generated rounds for the half.
/// </summary>
public sealed record GenerateHalfScheduleCommand(
    int HalfId,
    int CourseId,
    IReadOnlyList<DateOnly> WeekDates,
    NineHoleSide StartingSide,
    string UserId) : IRequest<Result<List<RoundDto>>>, IAmAuditableCommand
{
    public string AuditEntityType => "SeasonHalf";
    public string AuditEntityId => HalfId.ToString();
}

public sealed class GenerateHalfScheduleCommandHandler : IRequestHandler<GenerateHalfScheduleCommand, Result<List<RoundDto>>>
{
    private readonly IFlightRepository _flightRepository;
    private readonly IRoundRepository _roundRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IHandicapRepository _handicapRepository;
    private readonly ILeagueContext _leagueContext;

    public GenerateHalfScheduleCommandHandler(
        IFlightRepository flightRepository,
        IRoundRepository roundRepository,
        ICourseRepository courseRepository,
        IPlayerRepository playerRepository,
        IHandicapRepository handicapRepository,
        ILeagueContext leagueContext)
    {
        _flightRepository = flightRepository;
        _roundRepository = roundRepository;
        _courseRepository = courseRepository;
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
        _leagueContext = leagueContext;
    }

    public async Task<Result<List<RoundDto>>> Handle(GenerateHalfScheduleCommand request, CancellationToken cancellationToken)
    {
        if (_leagueContext.LeagueId is null)
            return Result<List<RoundDto>>.Fail("No league context.");

        if (request.WeekDates.Count == 0)
            return Result<List<RoundDto>>.Fail("At least one week date is required.");

        var half = await _flightRepository.GetHalfByIdAsync(request.HalfId, cancellationToken);
        if (half is null)
            return Result<List<RoundDto>>.Fail($"Half with ID {request.HalfId} not found.");

        var course = await _courseRepository.GetByIdAsync(request.CourseId, cancellationToken);
        if (course is null)
            return Result<List<RoundDto>>.Fail($"Course with ID {request.CourseId} not found.");

        var flights = await _flightRepository.GetByHalfAsync(request.HalfId, cancellationToken);
        if (flights.Count == 0)
            return Result<List<RoundDto>>.Fail("Half has no flights. Create flights first.");

        // Replace any existing scheduled rounds for this half (admin can regenerate).
        var existing = await _roundRepository.GetByHalfAsync(request.HalfId, cancellationToken);
        foreach (var r in existing)
            await _roundRepository.DeleteAsync(r.Id, cancellationToken);

        var orderedDates = request.WeekDates.OrderBy(d => d).ToList();
        var created = new List<RoundDto>();

        // Batch-load all memberships for the half, active players, and current
        // handicaps once instead of a per-flight/per-membership loop repeated
        // for every week — this was previously 2-3 SQL round trips per player
        // per week being scheduled.
        var allMemberships = await _flightRepository.GetMembershipsByHalfAsync(request.HalfId, cancellationToken);
        var activePlayerIds = (await _playerRepository.GetAllActiveAsync(cancellationToken))
            .Select(p => p.Id)
            .ToHashSet();
        var currentHandicapByPlayerId = (await _handicapRepository.GetAllAsync(cancellationToken))
            .GroupBy(h => h.PlayerId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(h => h.EffectiveDate).ThenByDescending(h => h.Id).First());

        for (var weekIndex = 0; weekIndex < orderedDates.Count; weekIndex++)
        {
            var side = AlternateSide(request.StartingSide, weekIndex);

            var round = new Round
            {
                LeagueId = _leagueContext.LeagueId!.Value,
                SeasonId = half.SeasonId,
                HalfId = half.Id,
                CourseId = course.Id,
                WeekNumber = weekIndex + 1,
                RoundDate = orderedDates[weekIndex],
                Status = RoundStatus.Scheduled,
                NineHoleSide = side,
            };

            await _roundRepository.AddAsync(round, cancellationToken);

            foreach (var flight in flights)
            {
                var memberships = allMemberships.Where(m => m.FlightId == flight.Id);
                foreach (var membership in memberships)
                {
                    if (!activePlayerIds.Contains(membership.PlayerId)) continue;

                    var index = currentHandicapByPlayerId.TryGetValue(membership.PlayerId, out var current)
                        ? current.HandicapIndex
                        : 0.0;

                    await _roundRepository.AddParticipantAsync(new RoundParticipant
                    {
                        RoundId = round.Id,
                        PlayerId = membership.PlayerId,
                        FlightId = flight.Id,
                        HandicapIndex = index,
                        CourseHandicap = CourseHandicap(index, course.SlopeRating, course.CourseRating, course.Holes.Sum(h => h.Par), RoundType.NineHole),
                        IsWithdrawn = false,
                    }, cancellationToken);
                }
            }

            created.Add(RoundDtoMapper.Map(round, course.Name, round.Participants.Count));
        }

        return Result<List<RoundDto>>.Ok(created);
    }

    private static NineHoleSide AlternateSide(NineHoleSide starting, int weekIndex)
    {
        if (starting == NineHoleSide.Back)
            return weekIndex % 2 == 0 ? NineHoleSide.Back : NineHoleSide.Front;
        return weekIndex % 2 == 0 ? NineHoleSide.Front : NineHoleSide.Back;
    }
}

internal static class RoundDtoMapper
{
    public static RoundDto Map(Round round, string courseName, int participantCount)
        => new(
            round.Id,
            round.SeasonId,
            round.HalfId,
            round.CourseId,
            courseName,
            round.WeekNumber,
            round.RoundDate,
            round.Status,
            round.NineHoleSide,
            round.RoundType,
            participantCount,
            round.LongestDriveHoleNumber);
}
