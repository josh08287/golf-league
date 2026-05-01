namespace GolfLeague.Domain.Entities;

public class FlightMembership
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public int FlightId { get; set; }
    public int SeasonId { get; set; }
    public DateTime JoinedAt { get; set; }

    public Player Player { get; set; } = null!;
    public Flight Flight { get; set; } = null!;
    public Season Season { get; set; } = null!;
}
