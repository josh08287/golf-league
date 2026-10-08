using FluentAssertions;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Services;
using Xunit;

namespace GolfLeague.Tests.Domain;

public class WolfRotationTests
{
    private static readonly List<int> Rotation = [101, 102, 103, 104];

    [Fact]
    public void PlayOrder_NoStartingHole_IsAscendingHoles()
    {
        WolfRotation.PlayOrder(NineHoleSide.NotApplicable, null).Should().Equal(Enumerable.Range(1, 18));
        WolfRotation.PlayOrder(NineHoleSide.Back, null).Should().Equal(Enumerable.Range(10, 9));
    }

    [Fact]
    public void PlayOrder_ShotgunStart_BeginsAtStartingHoleAndWraps()
    {
        WolfRotation.PlayOrder(NineHoleSide.NotApplicable, 6)
            .Should().Equal([6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 1, 2, 3, 4, 5]);
    }

    [Fact]
    public void PlayOrder_StartingHoleOutsideRound_IsIgnored()
    {
        WolfRotation.PlayOrder(NineHoleSide.Front, 14).Should().Equal(Enumerable.Range(1, 9));
    }

    [Fact]
    public void WolfForHole_ShotgunStart_FirstHolePlayedGoesToFirstInRotation()
    {
        var playOrder = WolfRotation.PlayOrder(NineHoleSide.NotApplicable, 6);

        WolfRotation.WolfForHole(6, playOrder, Rotation).Should().Be(101);
        WolfRotation.WolfForHole(7, playOrder, Rotation).Should().Be(102);
        WolfRotation.WolfForHole(9, playOrder, Rotation).Should().Be(104);
        WolfRotation.WolfForHole(10, playOrder, Rotation).Should().Be(101);
        // Hole 1 is the 14th hole played (index 13) -> 13 % 4 = 1.
        WolfRotation.WolfForHole(1, playOrder, Rotation).Should().Be(102);
    }

    [Fact]
    public void WolfForHole_BackNine_FirstHolePlayedGoesToFirstInRotation()
    {
        var playOrder = WolfRotation.PlayOrder(NineHoleSide.Back, null);

        WolfRotation.WolfForHole(10, playOrder, Rotation).Should().Be(101);
        WolfRotation.WolfForHole(14, playOrder, Rotation).Should().Be(101);
    }

    [Fact]
    public void WolfForHole_HoleNotInRound_ReturnsNull()
    {
        var playOrder = WolfRotation.PlayOrder(NineHoleSide.Front, null);

        WolfRotation.WolfForHole(12, playOrder, Rotation).Should().BeNull();
    }
}
