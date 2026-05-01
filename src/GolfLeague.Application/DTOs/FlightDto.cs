namespace GolfLeague.Application.DTOs;

public sealed record FlightDto(
    int Id,
    int SeasonId,
    string Name,
    double? MinHandicap,
    double? MaxHandicap,
    int DisplayOrder);
