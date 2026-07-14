using FluentAssertions;
using GolfLeague.Application.Leagues;
using Xunit;

namespace GolfLeague.Tests.Application;

public class LeagueSettingsCommandsTests
{
    [Fact]
    public void ParseCutoffTime_ValidHhMm_ParsesExactValue()
    {
        KnownSettings.ParseCutoffTime("09:30").Should().Be(new TimeOnly(9, 30));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-time")]
    [InlineData("25:00")]
    public void ParseCutoffTime_MissingOrMalformed_FallsBackToDefault(string? storedValue)
    {
        KnownSettings.ParseCutoffTime(storedValue).Should().Be(new TimeOnly(18, 0));
    }
}
