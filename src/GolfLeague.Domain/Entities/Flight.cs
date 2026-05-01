namespace GolfLeague.Domain.Entities;

public class Flight
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double? MinHandicap { get; set; }
    public double? MaxHandicap { get; set; }
    public int DisplayOrder { get; set; }

    public Season Season { get; set; } = null!;
    public ICollection<FlightMembership> Memberships { get; set; } = [];
    public ICollection<Round> Rounds { get; set; } = [];
}
