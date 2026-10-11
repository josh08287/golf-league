namespace GolfLeague.Application.DTOs;

/// <summary>A tee box as offered in a tee picker (score entry, player defaults).</summary>
public sealed record TeeBoxOptionDto(int Id, string Name, double CourseRating, int SlopeRating, bool IsDefault);

/// <summary>A participant's tee for one round after a change.</summary>
public sealed record ParticipantTeeDto(
    int PlayerId,
    int TeeBoxId,
    string TeeBoxName,
    double CourseRating,
    int SlopeRating,
    int CourseHandicap,
    bool TeeBoxOverridden);

/// <summary>A player's default tee for one course in one half.</summary>
public sealed record PlayerHalfTeeBoxDto(int HalfId, int CourseId, int TeeBoxId);

public sealed record CourseTeeOptionsDto(int CourseId, string CourseName, List<TeeBoxOptionDto> TeeBoxes);

/// <summary>Everything the admin player page needs to show and edit per-half default tees.</summary>
public sealed record PlayerTeeBoxSettingsDto(List<CourseTeeOptionsDto> Courses, List<PlayerHalfTeeBoxDto> Defaults);

public static class TeeBoxOptionMapper
{
    public static List<TeeBoxOptionDto> Map(IEnumerable<Domain.Entities.TeeBox> teeBoxes)
        => teeBoxes
            .OrderByDescending(t => t.IsDefault)
            .ThenByDescending(t => t.TotalYardage)
            .ThenBy(t => t.Name)
            .Select(t => new TeeBoxOptionDto(t.Id, t.Name, t.CourseRating, t.SlopeRating, t.IsDefault))
            .ToList();
}
