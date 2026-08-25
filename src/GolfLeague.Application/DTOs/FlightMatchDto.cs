namespace GolfLeague.Application.DTOs;

public sealed record FlightMatchDto(
    int Id,
    int FlightId,
    int RoundId,
    int WeekNumber,
    string RoundDate,
    int Player1Id,
    string Player1FullName,
    int? Player2Id,
    string? Player2FullName,
    int? Player1Points,
    int? Player2Points,
    bool Player1Absent,
    bool Player2Absent);
