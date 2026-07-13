using GolfLeague.Application.Common;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

public sealed record SetTournamentMatchupsCommand(
    int RoundId,
    List<MatchupInput> Matchups,
    string UserId) : IRequest<Result<List<TournamentMatchupDto>>>, IAmAuditableCommand
{
    public string AuditEntityType => "Round";
    public string AuditEntityId => RoundId.ToString();
}

public sealed class SetTournamentMatchupsCommandHandler : IRequestHandler<SetTournamentMatchupsCommand, Result<List<TournamentMatchupDto>>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IHandicapRepository _handicapRepository;

    public SetTournamentMatchupsCommandHandler(
        IRoundRepository roundRepository,
        IPlayerRepository playerRepository,
        IHandicapRepository handicapRepository)
    {
        _roundRepository = roundRepository;
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
    }

    public async Task<Result<List<TournamentMatchupDto>>> Handle(SetTournamentMatchupsCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<List<TournamentMatchupDto>>.Fail($"Round {request.RoundId} not found.");
        if (round.RoundType != RoundType.Tournament)
            return Result<List<TournamentMatchupDto>>.Fail("This round is not a tournament round.");
        if (round.Status == RoundStatus.Finalized || round.Status == RoundStatus.Cancelled)
            return Result<List<TournamentMatchupDto>>.Fail("Cannot modify matchups for a finalized or cancelled round.");

        var participants = await _roundRepository.GetParticipantsAsync(request.RoundId, cancellationToken);
        var participantIds = participants.Select(p => p.PlayerId).ToHashSet();

        var matchupEntities = new List<TournamentMatchup>();
        var matchupDtos = new List<TournamentMatchupDto>();

        var matchupNum = 1;
        foreach (var m in request.Matchups)
        {
            if (!participantIds.Contains(m.Player1Id) || !participantIds.Contains(m.Player2Id))
                return Result<List<TournamentMatchupDto>>.Fail($"Players {m.Player1Id} and/or {m.Player2Id} are not participants of this round.");

            var p1Part = participants.First(p => p.PlayerId == m.Player1Id);
            var p2Part = participants.First(p => p.PlayerId == m.Player2Id);

            matchupEntities.Add(new TournamentMatchup
            {
                RoundId = request.RoundId,
                MatchupNumber = matchupNum,
                Player1Id = m.Player1Id,
                Player2Id = m.Player2Id,
            });
            matchupDtos.Add(new TournamentMatchupDto(
                matchupNum,
                m.Player1Id, p1Part.Player.FullName, p1Part.HandicapIndex, p1Part.CourseHandicap,
                m.Player2Id, p2Part.Player.FullName, p2Part.HandicapIndex, p2Part.CourseHandicap,
                null));
            matchupNum++;
        }

        await _roundRepository.ReplaceTournamentMatchupsAsync(request.RoundId, matchupEntities, cancellationToken);
        return Result<List<TournamentMatchupDto>>.Ok(matchupDtos);
    }
}
