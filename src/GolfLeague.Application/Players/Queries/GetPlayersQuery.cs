using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using MediatR;
using static GolfLeague.Application.Common.FlightDisplayName;

namespace GolfLeague.Application.Players.Queries;

public sealed record GetPlayersQuery(
    int Page = 1,
    int PageSize = 20,
    SortRequest? Sort = null) : IRequest<Result<PagedResult<PlayerDto>>>;

public sealed class GetPlayersQueryHandler : IRequestHandler<GetPlayersQuery, Result<PagedResult<PlayerDto>>>
{
    private readonly IPlayerRepository _playerRepository;
    private readonly IHandicapRepository _handicapRepository;
    private readonly IAppUserRepository _appUserRepository;

    /// <summary>
    /// Sortable columns for the players list. Default sort is the
    /// natural roster order: flight, then last name, then first name.
    /// </summary>
    private static readonly SortMap<PlayerDto> SortMap = new SortMap<PlayerDto>(
            source => source
                .OrderByDescending(p => p.IsActive)
                .ThenBy(p => p.FlightName ?? string.Empty)
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
        IHandicapRepository handicapRepository,
        IAppUserRepository appUserRepository)
    {
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
        _appUserRepository = appUserRepository;
    }

    public async Task<Result<PagedResult<PlayerDto>>> Handle(GetPlayersQuery request, CancellationToken cancellationToken)
    {
        // Substitutes are managed in their own admin section (GetSubstitutesQuery),
        // not the regular roster — keep the two lists strictly partitioned.
        var players = (await _playerRepository.GetAllAsync(cancellationToken))
            .Where(p => !p.IsSubstitute)
            .ToList();

        var appUserIds = players
            .Where(p => p.AppUserId.HasValue)
            .Select(p => p.AppUserId!.Value)
            .ToList();

        var rolesByUserId = await _appUserRepository.GetRolesAsync(appUserIds, cancellationToken);

        var dtos = new List<PlayerDto>(players.Count);
        foreach (var player in players)
        {
            var currentHandicap = await _handicapRepository.GetCurrentAsync(player.Id, cancellationToken);
            IReadOnlyList<string> roles = player.AppUserId.HasValue
                && rolesByUserId.TryGetValue(player.AppUserId.Value, out var r)
                ? r
                : Array.Empty<string>();
            dtos.Add(ToDto(player, currentHandicap?.HandicapIndex, roles));
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

    internal static PlayerDto ToDto(Player player, double? currentHandicap, IReadOnlyList<string>? roles = null)
    {
        var activeMemberships = player.FlightMemberships
            .Where(fm => fm.Season.IsActive)
            .OrderByDescending(fm => fm.JoinedAt)
            .ToList();

        // "Current" flight = most recent membership in the active season (legacy field kept for compatibility)
        var latestMembership = activeMemberships.FirstOrDefault();

        // Per-half memberships: one entry per half (latest membership wins if multiple exist per half)
        var optInByHalf = player.HalfSettings.ToDictionary(s => s.HalfId, s => s.Par3GrossSkinsOptIn);
        var perHalf = activeMemberships
            .GroupBy(fm => fm.HalfId)
            .Select(g => g.OrderByDescending(fm => fm.JoinedAt).First())
            .Select(fm => new HalfFlightMembership(
                fm.HalfId,
                fm.FlightId,
                Format(fm.Season.Year, fm.Half.HalfNumber, fm.Flight.Name),
                optInByHalf.TryGetValue(fm.HalfId, out var optIn) ? optIn : true))
            .ToList();

        return new PlayerDto(
            player.Id,
            player.FullName,
            player.Email,
            player.IsActive,
            currentHandicap,
            latestMembership?.FlightId,
            latestMembership is null ? null : Format(latestMembership.Season.Year, latestMembership.Half.HalfNumber, latestMembership.Flight.Name),
            roles ?? Array.Empty<string>(),
            player.AppUserId,
            player.PreferredTeeTimeSlots,
            perHalf,
            player.TeeTimeEmailOptOut,
            player.IsSubstitute);
    }
}
