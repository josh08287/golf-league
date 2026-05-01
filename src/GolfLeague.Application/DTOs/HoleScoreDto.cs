namespace GolfLeague.Application.DTOs;

public sealed record HoleScoreDto(
    int Id,
    int HoleNumber,
    int Par,
    int StrokeIndex,
    int GrossStrokes,
    int HandicapStrokes,
    int NetStrokes,
    int StablefordPoints,
    bool IsMaxScore);
