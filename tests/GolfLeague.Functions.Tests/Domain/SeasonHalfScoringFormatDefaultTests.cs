using FluentAssertions;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using Xunit;

namespace GolfLeague.Tests.Domain;

/// <summary>
/// Guards backward compatibility for existing halves: a newly-constructed
/// SeasonHalf (as EF materializes an existing row with no explicit value)
/// must default to Stableford, so GetFlightStandingsQueryHandler's behavior
/// is unaffected by the match-play feature for every half created before it.
/// </summary>
public class SeasonHalfScoringFormatDefaultTests
{
    [Fact]
    public void NewSeasonHalf_DefaultsToStablefordWithNoCustomFormula()
    {
        var half = new SeasonHalf();

        half.ScoringFormat.Should().Be(ScoringFormat.Stableford);
        half.MatchPlayCustomFormula.Should().BeNull();
    }
}
