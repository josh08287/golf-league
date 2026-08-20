using GolfLeague.Domain.Enums;

namespace GolfLeague.Application.DTOs;

public sealed record RoundDto(
    int Id,
    int SeasonId,
    int? HalfId,
    int CourseId,
    string CourseName,
    int WeekNumber,
    DateOnly ScheduledDate,
    RoundStatus Status,
    NineHoleSide NineHoleSide,
    RoundType RoundType,
    int ParticipantCount,
    int? LongestDriveHoleNumber,
    decimal? GrossSkinsPool,
    decimal? NetSkinsPool);
