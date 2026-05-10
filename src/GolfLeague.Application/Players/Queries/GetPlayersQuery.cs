using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Players.Queries;

public sealed record GetPlayersQuery(
    int Page = 1,
    int PageSize = 20,
    SortRequest? Sort = null) : IRequest<Result<PagedResult<PlayerDto>>>;

public sealed class GetPlayersQueryHandler : IRequestHandler<GetPlayersQuery, Result<PagedResult<PlayerDto>>>
{
    private readonly IPlayerRepository _playerRepository;
    private readonly IHandicapRepository _handicapRepository;

    /// <summary>
    /// Sortable columns for the players list. Default sort is the
    /// natural roster order: flight, then last name, then first name.
    /// </summary>
    private static readonly SortMap<PlayerDto> SortMap = new SortMap<PlayerDto>(
            source => source
                .OrderBy(p => p.FlightName ?? string.Empty)
                .ThenBy(p => LastName(p.FullName), StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => FirstName(p.FullName), StringComparer.OrdinalIgnoreCase))
        .Add("name", p => p.FullName)
        .Add("fullName", p => p.FullName)
        .Add("email", p => p.Email)
        .Add("flight", p => p.FlightName)
        .Add("flightName", p => p.FlightName)
        .Add("handicap", p => p.CurrentHandicap)
        .Add("currentHandicap", p => p.CurrentHandicap)
        .Add("isActive", p => p.IsActive);

    public GetPlayersQueryHandler(
        IPlayerRepository playerRepository,
        IHandicapRepository handicapRepository)
    {
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
    }

    public async Task<Result<PagedResult<PlayerDto>>> Handle(GetPlayersQuery request, CancellationToken cancellationToken)
    {
        var players = await _playerRepository.GetAllActiveAsync(cancellationToken);

        // Project everyone, then sort, then page. Reordering Skip/Take
        // before sort would only sort the page, not the full list.
        var dtos = new List<PlayerDto>(players.Count);
        foreach (var player in players)
        {
            var currentHandicap = await _handicapRepository.GetCurrentAsync(player.Id, cancellationToken);
            dtos.Add(ToDto(player, currentHandicap?.HandicapIndex));
        }

        var sorted = SortMap.Apply(dtos, request.Sort);
        var totalCount = sorted.Count;
        var paged = sorted
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var result = new PagedResult<PlayerDto>(paged, request.Page, request.PageSize, totalCount);
        return Result<PagedResult<PlayerDto>>.Ok(result);
    }

    private static string LastName(string fullName)
    {
        var i = fullName.LastIndexOf(' ');
        return i < 0 ? fullName : fullName[(i + 1)..];
    }

    private static string FirstName(string fullName)
    {
        var i = fullName.IndexOf(' ');
        return i < 0 ? fullName : fullName[..i];
    }

    internal static PlayerDto ToDto(Player player, double? currentHandicap)
    {
        var activeMembership = player.FlightMemberships
            .Where(fm => fm.Season.IsActive)
            .OrderByDescending(fm => fm.JoinedAt)
            .FirstOrDefault();

        return new PlayerDto(
            player.Id,
            player.FullName,
            player.Email,
            player.IsActive,
            currentHandicap,
            activeMembership?.FlightId,
            activeMembership?.Flight.Name);
    }
}
