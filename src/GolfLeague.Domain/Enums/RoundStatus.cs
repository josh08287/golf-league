using System.Text.Json.Serialization;

namespace GolfLeague.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RoundStatus
{
    Scheduled,
    InProgress,
    PendingFinalization,
    Finalized,
    Cancelled
}
