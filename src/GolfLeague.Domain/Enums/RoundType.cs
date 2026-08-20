using System.Text.Json.Serialization;

namespace GolfLeague.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RoundType
{
    NineHole,
    EighteenHole,
    Tournament
}
