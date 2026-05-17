using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Registrations.Queries;

public sealed record GetInvitesQuery(string BaseUrl, SortRequest? Sort = null)
    : IRequest<Result<List<InviteDto>>>;

public sealed class GetInvitesQueryHandler : IRequestHandler<GetInvitesQuery, Result<List<InviteDto>>>
{
    private readonly IInviteRepository _repo;

    /// <summary>
    /// Default sort: newest invites first.
    /// </summary>
    private static readonly SortMap<InviteDto> SortMap = new SortMap<InviteDto>(
            source => source.OrderByDescending(i => i.CreatedAt))
        .Add("email", i => i.Email)
        .Add("status", i => i.Status)
        .Add("created", i => i.CreatedAt)
        .Add("createdAt", i => i.CreatedAt)
        .Add("expires", i => i.ExpiresAt)
        .Add("expiresAt", i => i.ExpiresAt)
        .Add("accepted", i => i.AcceptedAt)
        .Add("acceptedAt", i => i.AcceptedAt);

    public GetInvitesQueryHandler(IInviteRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<List<InviteDto>>> Handle(GetInvitesQuery request, CancellationToken cancellationToken)
    {
        var invites = await _repo.GetAllAsync(cancellationToken);
        var dtos = invites.Select(i => ToDto(i, request.BaseUrl)).ToList();
        var sorted = SortMap.Apply(dtos, request.Sort);
        return Result<List<InviteDto>>.Ok(sorted.ToList());
    }

    internal static InviteDto ToDto(Domain.Entities.PlayerInvite i, string baseUrl)
    {
        var slug = i.League?.Slug ?? string.Empty;
        var link = $"{baseUrl.TrimEnd('/')}/accept-invite?token={i.Token}";
        return new(i.Id, i.Email, i.Token, i.Status.ToString(),
            i.CreatedAt, i.ExpiresAt, i.AcceptedAt, i.PlayerId,
            link, i.Role.ToString().ToLowerInvariant(), slug);
    }
}
