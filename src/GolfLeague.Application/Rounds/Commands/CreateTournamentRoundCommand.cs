using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;
using static GolfLeague.Domain.Services.StablefordScoringService;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Creates an 18-hole tournament round for a season/half.
/// Players are selected explicitly (not pulled from flight memberships).
/// Matchups are stored in TournamentMatchup rows; default pairing is low-to-high handicap: 1v2, 3v4, etc.
/// </summary>
public sealed record CreateTournamentRoundCommand(
    int SeasonId,
    int HalfId,
    int CourseId,
    DateOnly RoundDate,
    List<int> PlayerIds,
    List<MatchupInput>? Matchups,
    string? Notes,
    string UserId) : IRequest<Result<TournamentRoundDto>>, IAmAuditableCommand;

public sealed record MatchupInput(int Player1Id, int Player2Id);

public sealed record TournamentMatchupDto(
    int MatchupNumber,
    int Player1Id,
    string Player1Name,
    double Player1HandicapIndex,
    int Player1CourseHandicap,
    int Player2Id,
    string Player2Name,
    double Player2HandicapIndex,
    int Player2CourseHandicap,
    int? WinnerPlayerId);

public sealed record TournamentRoundDto(
    RoundDto Round,
    List<TournamentMatchupDto> Matchups);

public sealed class CreateTournamentRoundCommandHandler : IRequestHandler<CreateTournamentRoundCommand, Result<TournamentRoundDto>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IHandicapRepository _handicapRepository;
    private readonly IFlightRepository _flightRepository;
    private readonly ISeasonRepository _seasonRepository;

    public CreateTournamentRoundCommandHandler(
        IRoundRepository roundRepository,
        ICourseRepository courseRepository,
        IPlayerRepository playerRepository,
        IHandicapRepository handicapRepository,
        IFlightRepository flightRepository,
        ISeasonRepository seasonRepository)
    {
        _roundRepository = roundRepository;
        _courseRepository = courseRepository;
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
        _flightRepository = flightRepository;
        _seasonRepository = seasonRepository;
    }

    public async Task<Result<TournamentRoundDto>> Handle(CreateTournamentRoundCommand request, CancellationToken cancellationToken)
    {
        if (request.PlayerIds.Count < 2)
            return Result<TournamentRoundDto>.Fail("A tournament round requires at least 2 players.");

        var half = await _flightRepository.GetHalfByIdAsync(request.HalfId, cancellationToken);
        if (half is null)
            return Result<TournamentRoundDto>.Fail($"Half with ID {request.HalfId} not found.");

        var course = await _courseRepository.GetByIdAsync(request.CourseId, cancellationToken);
        if (course is null)
            return Result<TournamentRoundDto>.Fail($"Course with ID {request.CourseId} not found.");

        var courseHoles = await _courseRepository.GetHolesAsync(request.CourseId, cancellationToken);
        if (courseHoles.Count < 18)
            return Result<TournamentRoundDto>.Fail("Tournament rounds require a course with all 18 holes configured.");

        var flights = await _flightRepository.GetByHalfAsync(request.HalfId, cancellationToken);

        var existing = await _roundRepository.GetByHalfAsync(request.HalfId, cancellationToken);
        var nextWeek = existing.Count == 0 ? 1 : existing.Max(r => r.WeekNumber) + 1;

        var round = new Round
        {
            SeasonId = request.SeasonId,
            HalfId = request.HalfId,
            CourseId = course.Id,
            WeekNumber = nextWeek,
            RoundDate = request.RoundDate,
            Status = RoundStatus.Scheduled,
            NineHoleSide = NineHoleSide.NotApplicable,
            RoundType = RoundType.Tournament,
            Notes = request.Notes,
        };

        await _roundRepository.AddAsync(round, cancellationToken);

        // Build participant records (use full 18-hole handicap)
        var participantHandicaps = new List<(int PlayerId, double HcpIndex, int CourseHcp, string FullName)>();
        var defaultFlightId = flights.OrderBy(f => f.DisplayOrder).FirstOrDefault()?.Id ?? 0;

        foreach (var playerId in request.PlayerIds.Distinct())
        {
            var player = await _playerRepository.GetByIdAsync(playerId, cancellationToken);
            if (player is null || !player.IsActive) continue;

            var current = await _handicapRepository.GetCurrentAsync(playerId, cancellationToken);
            var index = current?.HandicapIndex ?? 0.0;
            var courseHcp = CourseHandicap(index, course.SlopeRating, RoundType.Tournament);

            // Assign to the player's current flight, or the first flight as fallback
            var membership = player.FlightMemberships.FirstOrDefault(fm => fm.HalfId == request.HalfId);
            var flightId = membership?.FlightId ?? defaultFlightId;

            await _roundRepository.AddParticipantAsync(new RoundParticipant
            {
                RoundId = round.Id,
                PlayerId = playerId,
                FlightId = flightId,
                HandicapIndex = index,
                CourseHandicap = courseHcp,
                IsWithdrawn = false,
            }, cancellationToken);

            participantHandicaps.Add((playerId, index, courseHcp, player.FullName));
        }

        // Build matchups
        var matchupEntities = new List<TournamentMatchup>();
        var matchupDtos = new List<TournamentMatchupDto>();

        if (request.Matchups is { Count: > 0 })
        {
            // Use admin-supplied matchups
            var matchupNum = 1;
            foreach (var m in request.Matchups)
            {
                var p1 = participantHandicaps.FirstOrDefault(p => p.PlayerId == m.Player1Id);
                var p2 = participantHandicaps.FirstOrDefault(p => p.PlayerId == m.Player2Id);
                if (p1 == default || p2 == default) continue;

                matchupEntities.Add(new TournamentMatchup
                {
                    RoundId = round.Id,
                    MatchupNumber = matchupNum,
                    Player1Id = m.Player1Id,
                    Player2Id = m.Player2Id,
                });
                matchupDtos.Add(new TournamentMatchupDto(matchupNum, p1.PlayerId, p1.FullName, p1.HcpIndex, p1.CourseHcp,
                    p2.PlayerId, p2.FullName, p2.HcpIndex, p2.CourseHcp, null));
                matchupNum++;
            }
        }
        else
        {
            // Default: sort by handicap index ascending (lowest = best), pair 1v2, 3v4, etc.
            var sorted = participantHandicaps.OrderBy(p => p.HcpIndex).ToList();
            for (int i = 0; i + 1 < sorted.Count; i += 2)
            {
                var p1 = sorted[i];
                var p2 = sorted[i + 1];
                var matchupNum = i / 2 + 1;

                matchupEntities.Add(new TournamentMatchup
                {
                    RoundId = round.Id,
                    MatchupNumber = matchupNum,
                    Player1Id = p1.PlayerId,
                    Player2Id = p2.PlayerId,
                });
                matchupDtos.Add(new TournamentMatchupDto(matchupNum, p1.PlayerId, p1.FullName, p1.HcpIndex, p1.CourseHcp,
                    p2.PlayerId, p2.FullName, p2.HcpIndex, p2.CourseHcp, null));
            }
        }

        if (matchupEntities.Count > 0)
            await _roundRepository.AddTournamentMatchupsAsync(matchupEntities, cancellationToken);

        var roundDto = RoundDtoMapper.Map(round, course.Name, participantHandicaps.Count);
        return Result<TournamentRoundDto>.Ok(new TournamentRoundDto(roundDto, matchupDtos));
    }
}
