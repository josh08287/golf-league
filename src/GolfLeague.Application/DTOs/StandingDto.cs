namespace GolfLeague.Application.DTOs;

public sealed record StandingDto(
    int Position,
    int PlayerId,
    string PlayerFullName,
    string PlayerInitials,
    int RoundsPlayed,
    int TotalPoints,
    double AveragePoints,
    double CurrentHandicapIndex);
