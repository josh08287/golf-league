using GolfLeague.Domain.Enums;

namespace GolfLeague.Application.DTOs;

public sealed record RoundDto(
    int Id,
    int SeasonId,
    int FlightId,
    string FlightName,
    int CourseId,
    string CourseName,
    DateOnly ScheduledDate,
    RoundStatus Status,
    int ParticipantCount);
