using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Registrations.Queries;

public sealed record GetInvitesQuery(string BaseUrl) : IRequest<Result<List<InviteDto>>>;

public sealed class GetInvitesQueryHandler : IRequestHandler<GetInvitesQuery, Result<List<InviteDto>>>
{
    private readonly IInviteRepository _repo;

    public GetInvitesQueryHandler(IInviteRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<List<InviteDto>>> Handle(GetInvitesQuery request, CancellationToken cancellationToken)
    {
        var invites = await _repo.GetAllAsync(cancellationToken);
        var dtos = invites.Select(i => ToDto(i, request.BaseUrl)).ToList();
        return Result<List<InviteDto>>.Ok(dtos);
    }

    internal static InviteDto ToDto(Domain.Entities.PlayerInvite i, string baseUrl) =>
        new(i.Id, i.Email, i.Token, i.Status.ToString(),
            i.CreatedAt, i.ExpiresAt, i.AcceptedAt, i.PlayerId,
            $"{baseUrl.TrimEnd('/')}/accept-invite?token={i.Token}");
}
