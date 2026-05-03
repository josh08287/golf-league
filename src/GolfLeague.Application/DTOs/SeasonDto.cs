namespace GolfLeague.Application.DTOs;

public sealed record SeasonDto(
    int Id,
    string Name,
    int Year,
    string StartDate,
    string EndDate,
    bool IsActive,
    int? BestNRounds);
