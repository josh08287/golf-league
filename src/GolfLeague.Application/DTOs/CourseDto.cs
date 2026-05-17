namespace GolfLeague.Application.DTOs;

public sealed record CourseHoleDto(
    int Id,
    int HoleNumber,
    int Par,
    int StrokeIndex);

public sealed record HoleTeeBoxDto(
    int CourseHoleId,
    int Yardage,
    int Par);

public sealed record TeeBoxDto(
    int Id,
    string Name,
    double CourseRating,
    double SlopeRating,
    int TotalYardage,
    int Par,
    List<HoleTeeBoxDto> Holes);

public sealed record CourseDto(
    int Id,
    string Name,
    double Rating,
    int Slope,
    int HoleCount,
    List<CourseHoleDto> HoleDetails,
    List<TeeBoxDto>? TeeBoxes = null);
