using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Registrations.Queries;

public sealed record GetInviteByTokenQuery(string Token, string BaseUrl) : IRequest<Result<InviteDto>>;

public sealed class GetInviteByTokenQueryHandler : IRequestHandler<GetInviteByTokenQuery, Result<InviteDto>>
{
    private readonly IInviteRepository _repo;

    public GetInviteByTokenQueryHandler(IInviteRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<InviteDto>> Handle(GetInviteByTokenQuery request, CancellationToken cancellationToken)
    {
        var invite = await _repo.GetByTokenAsync(request.Token, cancellationToken);
        if (invite is null)
            return Result<InviteDto>.Fail("Invite not found.");

        return Result<InviteDto>.Ok(GetInvitesQueryHandler.ToDto(invite, request.BaseUrl));
    }
}
