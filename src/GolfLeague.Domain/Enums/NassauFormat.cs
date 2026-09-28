using System.Text.Json.Serialization;

namespace GolfLeague.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NassauFormat
{
    /// <summary>2v2, best-ball per side compared head-to-head each hole.</summary>
    TeamVsTeam,

    /// <summary>Every opted-in player plays a separate 1v1 match against every other.</summary>
    Individual
}
