namespace GolfLeague.Application.DTOs;

public sealed record RoundScoreDto(
    int RoundId,
    DateOnly RoundDate,
    int WeekNumber,
    int? Points,
    int? GrossStrokes,
    int? NetStrokes,
    bool IsSkipped,
    bool IsDropped);

public sealed record StandingDto(
    int Position,
    int PlayerId,
    string PlayerFullName,
    string PlayerInitials,
    int RoundsPlayed,
    int TotalPoints,
    double AveragePoints,
    double CurrentHandicapIndex,
    double? AverageScore,
    IReadOnlyList<RoundScoreDto> RoundScores);
