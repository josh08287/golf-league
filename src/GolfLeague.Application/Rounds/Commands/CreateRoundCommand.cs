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
/// Adds a single weekly round to a half (e.g. an admin appending a make-up week).
/// All flights in the half become participants. NineHoleSide is admin-supplied
/// (or auto-alternated from the previous round if not provided).
/// </summary>
public sealed record CreateRoundCommand(
    int HalfId,
    int CourseId,
    DateOnly RoundDate,
    NineHoleSide? NineHoleSide,
    string? Notes,
    string UserId) : IRequest<Result<RoundDto>>, IAmAuditableCommand
{
    public string AuditEntityType => "Round";
    public string AuditEntityId => "0"; // assigned by the DB; resolved from the response
}

public sealed class CreateRoundCommandHandler : IRequestHandler<CreateRoundCommand, Result<RoundDto>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IHandicapRepository _handicapRepository;
    private readonly IFlightRepository _flightRepository;
    private readonly ILeagueContext _leagueContext;

    public CreateRoundCommandHandler(
        IRoundRepository roundRepository,
        ICourseRepository courseRepository,
        IPlayerRepository playerRepository,
        IHandicapRepository handicapRepository,
        IFlightRepository flightRepository,
        ILeagueContext leagueContext)
    {
        _roundRepository = roundRepository;
        _courseRepository = courseRepository;
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
        _flightRepository = flightRepository;
        _leagueContext = leagueContext;
    }

    public async Task<Result<RoundDto>> Handle(CreateRoundCommand request, CancellationToken cancellationToken)
    {
        if (_leagueContext.LeagueId is null)
            return Result<RoundDto>.Fail("No league context.");

        var half = await _flightRepository.GetHalfByIdAsync(request.HalfId, cancellationToken);
        if (half is null)
            return Result<RoundDto>.Fail($"Half with ID {request.HalfId} not found.");

        var course = await _courseRepository.GetByIdAsync(request.CourseId, cancellationToken);
        if (course is null)
            return Result<RoundDto>.Fail($"Course with ID {request.CourseId} not found.");

        var flights = await _flightRepository.GetByHalfAsync(request.HalfId, cancellationToken);
        if (flights.Count == 0)
            return Result<RoundDto>.Fail("Half has no flights. Create flights first.");

        var existing = await _roundRepository.GetByHalfAsync(request.HalfId, cancellationToken);
        var nextWeek = existing.Count == 0 ? 1 : existing.Max(r => r.WeekNumber) + 1;

        var side = request.NineHoleSide ?? NextSide(existing);

        var round = new Round
        {
            LeagueId = _leagueContext.LeagueId!.Value,
            SeasonId = half.SeasonId,
            HalfId = half.Id,
            CourseId = course.Id,
            WeekNumber = nextWeek,
            RoundDate = request.RoundDate,
            Status = RoundStatus.Scheduled,
            NineHoleSide = side,
            Notes = request.Notes,
        };

        await _roundRepository.AddAsync(round, cancellationToken);

        // Batch-load all memberships for the half, active players, and current
        // handicaps once instead of a per-flight/per-membership loop of
        // individual lookups — this was previously 2-3 SQL round trips per
        // player being seated into the round.
        var allMemberships = await _flightRepository.GetMembershipsByHalfAsync(request.HalfId, cancellationToken);
        var activePlayerIds = (await _playerRepository.GetAllActiveAsync(cancellationToken))
            .Select(p => p.Id)
            .ToHashSet();
        var currentHandicapByPlayerId = (await _handicapRepository.GetAllAsync(cancellationToken))
            .GroupBy(h => h.PlayerId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(h => h.EffectiveDate).ThenByDescending(h => h.Id).First());

        var totalParticipants = 0;
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
                totalParticipants++;
            }
        }

        return Result<RoundDto>.Ok(RoundDtoMapper.Map(round, course.Name, totalParticipants));
    }

    private static NineHoleSide NextSide(IReadOnlyList<Round> existing)
    {
        if (existing.Count == 0) return NineHoleSide.Front;
        var last = existing.OrderBy(r => r.WeekNumber).Last();
        return last.NineHoleSide == NineHoleSide.Front ? NineHoleSide.Back : NineHoleSide.Front;
    }
}
