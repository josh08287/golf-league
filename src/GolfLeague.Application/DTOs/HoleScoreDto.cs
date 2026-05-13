namespace GolfLeague.Application.DTOs;

public sealed record HoleScoreDto(
    int Id,
    int HoleNumber,
    int Par,
    int StrokeIndex,
    int GrossStrokes,
    int HandicapStrokes,
    int NetStrokes,
    int GrossStablefordPoints,
    int NetStablefordPoints,
    bool IsMaxScore,
    int? Putts = null,
    double? FirstPuttDistanceFeet = null,
    bool? FairwayHit = null,
    bool? Gir = null);
