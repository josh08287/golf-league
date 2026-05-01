namespace GolfLeague.Application.DTOs;

public sealed record FlightStandingDto(
    int Rank,
    int PlayerId,
    string PlayerName,
    string Initials,
    double CurrentHandicapIndex,
    int RoundsPlayed,
    int SeasonTotalPoints,
    double AvgPointsPerRound,
    int? LastRoundPoints);
