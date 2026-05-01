namespace GolfLeague.Application.DTOs;

public sealed record CourseDto(
    int Id,
    string Name,
    double CourseRating,
    int SlopeRating);
