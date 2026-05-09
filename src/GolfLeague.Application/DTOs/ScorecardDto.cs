namespace GolfLeague.Application.DTOs;

public sealed record ScorecardDto(
    int RoundId,
    DateOnly RoundDate,
    string CourseName,
    double CourseRating,
    int SlopeRating,
    ParticipantDto Participant,
    List<HoleScoreDto> HoleScores,
    int TotalPar,
    int TotalGross,
    int TotalNet,
    int TotalGrossPoints,
    int TotalNetPoints);
