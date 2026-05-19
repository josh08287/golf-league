namespace GolfLeague.Application.DTOs;

public sealed record InviteDto(
    int Id,
    string Email,
    string Token,
    string Status,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? AcceptedAt,
    int? PlayerId,
    string InviteLink,
    string Role,
    string LeagueSlug,
    int? PreLinkedPlayerId,
    string? PreLinkedPlayerName);
