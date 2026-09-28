using GolfLeague.Application.Common;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Queries;

public sealed record GetTournamentCommentsQuery(int RoundId) : IRequest<Result<List<TournamentCommentDto>>>;

public sealed class GetTournamentCommentsQueryHandler : IRequestHandler<GetTournamentCommentsQuery, Result<List<TournamentCommentDto>>>
{
    private readonly IRoundRepository _roundRepository;

    public GetTournamentCommentsQueryHandler(IRoundRepository roundRepository)
    {
        _roundRepository = roundRepository;
    }

    public async Task<Result<List<TournamentCommentDto>>> Handle(GetTournamentCommentsQuery request, CancellationToken cancellationToken)
    {
        var comments = await _roundRepository.GetTournamentCommentsAsync(request.RoundId, cancellationToken);

        var dtos = comments.Select(c => new TournamentCommentDto(
            c.Id,
            c.PlayerId,
            c.Player.FullName,
            c.Body,
            c.CreatedAt)).ToList();

        return Result<List<TournamentCommentDto>>.Ok(dtos);
    }
}
