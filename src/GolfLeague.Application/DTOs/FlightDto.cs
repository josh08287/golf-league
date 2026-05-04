namespace GolfLeague.Application.DTOs;

public sealed record FlightDto(
    int Id,
    int SeasonId,
    int? HalfId,
    string Name,
    int DisplayOrder,
    int PlayerCount = 0);
