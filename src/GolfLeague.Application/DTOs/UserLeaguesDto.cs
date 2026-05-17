namespace GolfLeague.Application.DTOs;

public sealed record UserLeagueDto(int LeagueId, string Name, string Slug, string Role);

public sealed record UserLeaguesDto(
    IReadOnlyList<UserLeagueDto> Leagues,
    bool IsSuperAdmin);
