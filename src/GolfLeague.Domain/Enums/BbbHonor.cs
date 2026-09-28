using System.Text.Json.Serialization;

namespace GolfLeague.Domain.Enums;

/// <summary>The 3 individual honors awarded per hole in Bingo Bango Bongo.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BbbHonor
{
    FirstOnGreen,
    ClosestOnceOn,
    FirstInHole
}
