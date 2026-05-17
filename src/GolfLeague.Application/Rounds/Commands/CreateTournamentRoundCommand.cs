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
/// Creates an 18-hole tournament round for a season (not tied to a half).
/// Players are selected explicitly (not pulled from flight memberships).
/// Matchups are stored in TournamentMatchup rows; default pairing is low-to-high handicap: 1v2, 3v4, etc.
/// </summary>
public sealed record CreateTournamentRoundCommand(
    int SeasonId,
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
    private readonly ISeasonRepository _seasonRepository;
    private readonly ILeagueContext _leagueContext;

    public CreateTournamentRoundCommandHandler(
        IRoundRepository roundRepository,
        ICourseRepository courseRepository,
        IPlayerRepository playerRepository,
        IHandicapRepository handicapRepository,
        ISeasonRepository seasonRepository,
        ILeagueContext leagueContext)
    {
        _roundRepository = roundRepository;
        _courseRepository = courseRepository;
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
        _seasonRepository = seasonRepository;
        _leagueContext = leagueContext;
    }

    public async Task<Result<TournamentRoundDto>> Handle(CreateTournamentRoundCommand request, CancellationToken cancellationToken)
    {
        if (_leagueContext.LeagueId is null)
            return Result<TournamentRoundDto>.Fail("No league context.");

        if (request.PlayerIds.Count < 2)
            return Result<TournamentRoundDto>.Fail("A tournament round requires at least 2 players.");

        var season = await _seasonRepository.GetByIdAsync(request.SeasonId, cancellationToken);
        if (season is null)
            return Result<TournamentRoundDto>.Fail($"Season with ID {request.SeasonId} not found.");

        var course = await _courseRepository.GetByIdAsync(request.CourseId, cancellationToken);
        if (course is null)
            return Result<TournamentRoundDto>.Fail($"Course with ID {request.CourseId} not found.");

        var courseHoles = await _courseRepository.GetHolesAsync(request.CourseId, cancellationToken);
        if (courseHoles.Count < 18)
            return Result<TournamentRoundDto>.Fail("Tournament rounds require a course with all 18 holes configured.");

        // Tournament rounds are numbered within the season across all tournament rounds
        var existingTournamentRounds = (await _roundRepository.GetBySeasonAsync(request.SeasonId, cancellationToken))
            .Where(r => r.RoundType == RoundType.Tournament)
            .ToList();
        var nextWeek = existingTournamentRounds.Count == 0 ? 1 : existingTournamentRounds.Max(r => r.WeekNumber) + 1;

        var round = new Round
        {
            LeagueId = _leagueContext.LeagueId!.Value,
            SeasonId = request.SeasonId,
            HalfId = null,
            CourseId = course.Id,
            WeekNumber = nextWeek,
            RoundDate = request.RoundDate,
            Status = RoundStatus.Scheduled,
            NineHoleSide = NineHoleSide.NotApplicable,
            RoundType = RoundType.Tournament,
            Notes = request.Notes,
        };

        await _roundRepository.AddAsync(round, cancellationToken);

        // Build participant records (use full 18-hole handicap; no flight grouping for tournament rounds)
        var participantHandicaps = new List<(int PlayerId, double HcpIndex, int CourseHcp, string FullName)>();

        foreach (var playerId in request.PlayerIds.Distinct())
        {
            var player = await _playerRepository.GetByIdAsync(playerId, cancellationToken);
            if (player is null || !player.IsActive) continue;

            var current = await _handicapRepository.GetCurrentAsync(playerId, cancellationToken);
            var index = current?.HandicapIndex ?? 0.0;
            var courseHcp = CourseHandicap(index, course.SlopeRating, RoundType.Tournament);

            await _roundRepository.AddParticipantAsync(new RoundParticipant
            {
                RoundId = round.Id,
                PlayerId = playerId,
                FlightId = null,
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
