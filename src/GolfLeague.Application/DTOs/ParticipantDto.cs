namespace GolfLeague.Application.DTOs;

public sealed record ParticipantDto(
    int Id,
    int RoundId,
    int PlayerId,
    string PlayerFullName,
    string PlayerInitials,
    int FlightId,
    double HandicapIndex,
    int CourseHandicap,
    int? TotalGrossStrokes,
    int? TotalNetStrokes,
    int? TotalGrossStablefordPoints,
    int? TotalNetStablefordPoints,
    bool IsWithdrawn,
    bool SkippedWeek);
