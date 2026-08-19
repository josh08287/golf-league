using GolfLeague.Application.Common;
using GolfLeague.Application.Rounds;
using GolfLeague.Application.Rounds.Queries;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;
using static GolfLeague.Domain.Services.StablefordScoringService;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Adds players to an already-created tournament round. Only allowed while
/// the round is still Scheduled — once play begins (or the round is
/// finalized/cancelled), the roster is locked.
/// </summary>
public sealed record AddTournamentParticipantsCommand(
    int RoundId,
    List<int> PlayerIds,
    string UserId) : IRequest<Result<List<RoundParticipantDto>>>, IAmAuditableCommand
{
    public string AuditEntityType => "Round";
    public string AuditEntityId => RoundId.ToString();
}

public sealed class AddTournamentParticipantsCommandHandler : IRequestHandler<AddTournamentParticipantsCommand, Result<List<RoundParticipantDto>>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IHandicapRepository _handicapRepository;
    private readonly TournamentFoursomeService _foursomeService;

    public AddTournamentParticipantsCommandHandler(
        IRoundRepository roundRepository,
        IPlayerRepository playerRepository,
        IHandicapRepository handicapRepository,
        TournamentFoursomeService foursomeService)
    {
        _roundRepository = roundRepository;
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
        _foursomeService = foursomeService;
    }

    public async Task<Result<List<RoundParticipantDto>>> Handle(AddTournamentParticipantsCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<List<RoundParticipantDto>>.Fail($"Round {request.RoundId} not found.");
        if (round.RoundType != RoundType.Tournament)
            return Result<List<RoundParticipantDto>>.Fail("This round is not a tournament round.");
        if (round.Status != RoundStatus.Scheduled)
            return Result<List<RoundParticipantDto>>.Fail("Players can only be added while the round is still Scheduled.");

        if (request.PlayerIds.Count == 0)
            return Result<List<RoundParticipantDto>>.Fail("At least one player ID is required.");

        var existingPlayerIds = round.Participants.Select(p => p.PlayerId).ToHashSet();
        var coursePar = round.Course.Holes.Sum(h => h.Par);

        var added = new List<RoundParticipantDto>();
        foreach (var playerId in request.PlayerIds.Distinct())
        {
            if (existingPlayerIds.Contains(playerId)) continue;

            var player = await _playerRepository.GetByIdAsync(playerId, cancellationToken);
            if (player is null || (!player.IsActive && !player.IsSubstitute)) continue;

            var current = await _handicapRepository.GetCurrentAsync(playerId, cancellationToken);
            var index = current?.HandicapIndex ?? 0.0;
            var courseHcp = CourseHandicap(index, round.Course.SlopeRating, round.Course.CourseRating, coursePar, RoundType.Tournament);

            var participant = new RoundParticipant
            {
                RoundId = round.Id,
                PlayerId = playerId,
                FlightId = null,
                HandicapIndex = index,
                CourseHandicap = courseHcp,
                IsWithdrawn = false,
                IsSubstitute = player.IsSubstitute,
            };
            await _roundRepository.AddParticipantAsync(participant, cancellationToken);

            added.Add(new RoundParticipantDto(
                participant.Id, participant.RoundId, participant.PlayerId, player.FullName,
                participant.FlightId, participant.HandicapIndex, participant.CourseHandicap,
                participant.IsWithdrawn, participant.SkippedWeek));
        }

        if (added.Count > 0)
        {
            var allParticipants = await _roundRepository.GetParticipantsAsync(round.Id, cancellationToken);
            await _foursomeService.RegroupAsync(round.Id, allParticipants, cancellationToken);
        }

        return Result<List<RoundParticipantDto>>.Ok(added);
    }
}
