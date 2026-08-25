namespace GolfLeague.Application.DTOs;

public sealed record SeasonHalfDto(
    int Id,
    int SeasonId,
    int HalfNumber,
    string Name,
    string StartDate,
    string EndDate,
    bool IsLocked = false,
    string ScoringFormat = "stableford",
    string? MatchPlayCustomFormula = null);

public sealed record SeasonDto(
    int Id,
    string Name,
    int Year,
    string StartDate,
    string EndDate,
    bool IsActive,
    int? BestNRounds,
    List<SeasonHalfDto> Halves);
