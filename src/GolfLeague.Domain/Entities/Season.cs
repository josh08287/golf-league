namespace GolfLeague.Domain.Entities;

public class Season
{
    public int Id { get; set; }
    public int LeagueId { get; set; }
    public League League { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public int Year { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsActive { get; set; }

    public ICollection<SeasonHalf> Halves { get; set; } = [];
    public ICollection<Round> Rounds { get; set; } = [];
}
