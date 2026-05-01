namespace GolfLeague.Application.DTOs;

public sealed record ParticipantDto(
    int Id,
    int RoundId,
    int PlayerId,
    string PlayerFullName,
    string PlayerInitials,
    double HandicapIndex,
    int CourseHandicap,
    int? TotalGrossStrokes,
    int? TotalNetStrokes,
    int? TotalStablefordPoints,
    bool IsWithdrawn);
