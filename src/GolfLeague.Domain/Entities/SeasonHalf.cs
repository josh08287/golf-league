namespace GolfLeague.Domain.Entities;

public class SeasonHalf
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public int HalfNumber { get; set; } // 1 or 2
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateTime CreatedAt { get; set; }

    public Season Season { get; set; } = null!;
    public ICollection<Flight> Flights { get; set; } = [];
    public ICollection<Round> Rounds { get; set; } = [];
}
