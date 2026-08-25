namespace GolfLeague.Domain.Enums;

/// <summary>Wire-format (lower-camel string) conversion for <see cref="ScoringFormat"/>, used by DTOs/commands at the API boundary.</summary>
public static class ScoringFormatExtensions
{
    public const string Stableford = "stableford";
    public const string MatchPlay = "matchPlay";

    public static string ToWireString(this ScoringFormat format) => format switch
    {
        ScoringFormat.MatchPlay => MatchPlay,
        _ => Stableford,
    };

    public static bool TryParse(string? value, out ScoringFormat format)
    {
        switch (value)
        {
            case MatchPlay:
                format = ScoringFormat.MatchPlay;
                return true;
            case Stableford:
            case null:
            case "":
                format = ScoringFormat.Stableford;
                return true;
            default:
                format = ScoringFormat.Stableford;
                return false;
        }
    }
}
