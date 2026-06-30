using GolfLeague.Domain.Entities;

namespace GolfLeague.Application.Common;

public static class FlightDisplayName
{
    public static string Format(int year, int halfNumber, string flightName)
        => $"{year} · H{halfNumber} · {flightName}";

    public static string Format(Flight flight)
        => Format(flight.Season.Year, flight.Half.HalfNumber, flight.Name);
}
