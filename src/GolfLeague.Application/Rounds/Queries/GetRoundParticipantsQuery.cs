using GolfLeague.Application.Common;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Queries;

public sealed record RoundParticipantDto(
    int Id,
    int RoundId,
    int PlayerId,
    string PlayerName,
    int FlightId,
    double HandicapAtTime,
    int CourseHandicap,
    bool IsWithdrawn,
    bool SkippedWeek);

public sealed record GetRoundParticipantsQuery(int RoundId) : IRequest<Result<List<RoundParticipantDto>>>;

public sealed class GetRoundParticipantsQueryHandler : IRequestHandler<GetRoundParticipantsQuery, Result<List<RoundParticipantDto>>>
{
    private readonly IRoundRepository _roundRepository;

    public GetRoundParticipantsQueryHandler(IRoundRepository roundRepository)
    {
        _roundRepository = roundRepository;
    }

    public async Task<Result<List<RoundParticipantDto>>> Handle(GetRoundParticipantsQuery request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<List<RoundParticipantDto>>.Fail($"Round with ID {request.RoundId} not found.");

        var dtos = round.Participants
            .OrderBy(p => p.FlightId)
            .ThenBy(p => p.Player.LastName)
            .ThenBy(p => p.Player.FirstName)
            .Select(p => new RoundParticipantDto(
                p.Id,
                p.RoundId,
                p.PlayerId,
                p.Player.FullName,
                p.FlightId,
                p.HandicapIndex,
                p.CourseHandicap,
                p.IsWithdrawn,
                p.SkippedWeek))
            .ToList();

        return Result<List<RoundParticipantDto>>.Ok(dtos);
    }
}
