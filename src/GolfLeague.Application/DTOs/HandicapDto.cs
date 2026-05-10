using GolfLeague.Domain.Enums;

namespace GolfLeague.Application.DTOs;

public sealed record HandicapDto(
    int Id,
    int PlayerId,
    double HandicapIndex,
    double NineHoleHandicapIndex,
    DateOnly EffectiveDate,
    HandicapSource Source,
    string? Notes);
