using GolfLeague.Application.Common;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Queries;

public sealed record RoundParticipantDto(
    int Id,
    int RoundId,
    int PlayerId,
    string PlayerName,
    int? FlightId,
    double HandicapAtTime,
    int CourseHandicap,
    bool IsWithdrawn,
    bool SkippedWeek);

public sealed record GetRoundParticipantsQuery(int RoundId, SortRequest? Sort = null)
    : IRequest<Result<List<RoundParticipantDto>>>;

public sealed class GetRoundParticipantsQueryHandler : IRequestHandler<GetRoundParticipantsQuery, Result<List<RoundParticipantDto>>>
{
    private readonly IRoundRepository _roundRepository;

    /// <summary>
    /// Default sort: flight then player name (the natural scorecard order).
    /// </summary>
    private static readonly SortMap<RoundParticipantDto> SortMap = new SortMap<RoundParticipantDto>(
            source => source.OrderBy(p => p.FlightId).ThenBy(p => p.PlayerName, StringComparer.OrdinalIgnoreCase))
        .Add("player", p => p.PlayerName)
        .Add("playerName", p => p.PlayerName)
        .Add("flight", p => p.FlightId)
        .Add("flightId", p => p.FlightId)
        .Add("hcp", p => p.HandicapAtTime)
        .Add("handicapAtTime", p => p.HandicapAtTime)
        .Add("courseHcp", p => p.CourseHandicap)
        .Add("courseHandicap", p => p.CourseHandicap)
        .Add("withdrawn", p => p.IsWithdrawn)
        .Add("isWithdrawn", p => p.IsWithdrawn)
        .Add("skipped", p => p.SkippedWeek)
        .Add("skippedWeek", p => p.SkippedWeek);

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

        var sorted = SortMap.Apply(dtos, request.Sort);
        return Result<List<RoundParticipantDto>>.Ok(sorted.ToList());
    }
}
