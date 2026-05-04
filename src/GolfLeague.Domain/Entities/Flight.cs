namespace GolfLeague.Domain.Entities;

public class Flight
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public int? HalfId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    public Season Season { get; set; } = null!;
    public SeasonHalf? Half { get; set; }
    public ICollection<FlightMembership> Memberships { get; set; } = [];
    public ICollection<Round> Rounds { get; set; } = [];
}
