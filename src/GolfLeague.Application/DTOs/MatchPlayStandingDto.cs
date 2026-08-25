namespace GolfLeague.Application.DTOs;

public sealed record MatchPlayMatchResultDto(
    int RoundId,
    int WeekNumber,
    string RoundDate,
    int? OpponentPlayerId,
    string? OpponentFullName,
    int PlayerPoints,
    int OpponentPoints,
    int PlayerHolesWon,
    int OpponentHolesWon,
    bool WasBye,
    bool WasAgainstCard);

public sealed record MatchPlayStandingDto(
    int Position,
    int PlayerId,
    string PlayerFullName,
    string PlayerInitials,
    int MatchesPlayed,
    int TotalPoints,
    double AveragePointsPerMatch,
    int Wins,
    int Halves,
    int Losses,
    double CurrentHandicapIndex,
    List<MatchPlayMatchResultDto> MatchResults);
