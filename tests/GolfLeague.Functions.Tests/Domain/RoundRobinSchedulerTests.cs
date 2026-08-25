using FluentAssertions;
using GolfLeague.Domain.Services;
using Xunit;
using static GolfLeague.Domain.Services.RoundRobinScheduler;

namespace GolfLeague.Tests.Domain;

public class RoundRobinSchedulerTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public void GenerateCircle_EvenPlayerCount_EveryPairPlaysExactlyOnce(int n)
    {
        var playerIds = Enumerable.Range(1, n).ToList();
        var rounds = GenerateCircle(playerIds);

        rounds.Should().HaveCount(n - 1);

        var allPairs = rounds.SelectMany(r => r).ToList();
        allPairs.Should().OnlyContain(p => p.Player2Id.HasValue, "no byes expected for an even player count");
        allPairs.Should().OnlyContain(p => p.Player1Id != p.Player2Id);

        var normalizedPairs = allPairs
            .Select(p => (Math.Min(p.Player1Id, p.Player2Id!.Value), Math.Max(p.Player1Id, p.Player2Id!.Value)))
            .ToList();
        normalizedPairs.Should().OnlyHaveUniqueItems();
        normalizedPairs.Should().HaveCount(n * (n - 1) / 2);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(7)]
    public void GenerateCircle_OddPlayerCount_OneByePerRoundAndFullRoundRobin(int n)
    {
        var playerIds = Enumerable.Range(1, n).ToList();
        var rounds = GenerateCircle(playerIds);

        rounds.Should().HaveCount(n);

        foreach (var round in rounds)
        {
            round.Count(p => p.Player2Id is null).Should().Be(1, "each round should have exactly one bye when player count is odd");
        }

        // Every player gets exactly one bye across the full circle.
        var byePlayers = rounds.SelectMany(r => r.Where(p => p.Player2Id is null)).Select(p => p.Player1Id).ToList();
        byePlayers.Should().HaveCount(n);
        byePlayers.Distinct().Should().HaveCount(n);

        // Remaining pairings still form a full round robin.
        var realPairs = rounds.SelectMany(r => r.Where(p => p.Player2Id.HasValue))
            .Select(p => (Math.Min(p.Player1Id, p.Player2Id!.Value), Math.Max(p.Player1Id, p.Player2Id!.Value)))
            .ToList();
        realPairs.Should().OnlyHaveUniqueItems();
        realPairs.Should().HaveCount(n * (n - 1) / 2);
    }

    [Fact]
    public void GenerateCircle_TwoPlayers_OneRoundOnePairing()
    {
        var rounds = GenerateCircle([1, 2]);
        rounds.Should().HaveCount(1);
        rounds[0].Should().ContainSingle();
        rounds[0][0].Player2Id.Should().NotBeNull();
    }

    [Fact]
    public void GenerateCircle_OnePlayer_SingleByeRound()
    {
        var rounds = GenerateCircle([1]);
        rounds.Should().HaveCount(1);
        rounds[0].Should().ContainSingle();
        rounds[0][0].Player1Id.Should().Be(1);
        rounds[0][0].Player2Id.Should().BeNull();
    }

    [Fact]
    public void GenerateCircle_NoPlayers_ReturnsEmpty()
    {
        var rounds = GenerateCircle([]);
        rounds.Should().BeEmpty();
    }

    [Fact]
    public void MapToWeeks_ExactFit_SchedulesEveryRound()
    {
        var circle = GenerateCircle([1, 2, 3, 4]); // 3 rounds
        var weeks = new List<(int, int)> { (10, 1), (11, 2), (12, 3) };

        var (scheduled, fit) = MapToWeeks(circle, weeks);

        fit.Should().Be(WeekFitResult.ExactFit);
        scheduled.Should().HaveCount(3);
        scheduled.Select(s => s.RoundId).Should().Equal(10, 11, 12);
    }

    [Fact]
    public void MapToWeeks_MoreWeeksThanNeeded_OnlyFillsEarliestWeeks()
    {
        var circle = GenerateCircle([1, 2, 3, 4]); // 3 rounds
        var weeks = new List<(int, int)> { (10, 1), (11, 2), (12, 3), (13, 4), (14, 5) };

        var (scheduled, fit) = MapToWeeks(circle, weeks);

        fit.Should().Be(WeekFitResult.MoreWeeksThanNeeded);
        scheduled.Should().HaveCount(3);
        scheduled.Select(s => s.RoundId).Should().Equal(10, 11, 12);
    }

    [Fact]
    public void MapToWeeks_FewerWeeksThanNeeded_SchedulesPartialInCircleOrder()
    {
        var circle = GenerateCircle([1, 2, 3, 4]); // 3 rounds
        var weeks = new List<(int, int)> { (10, 1) };

        var (scheduled, fit) = MapToWeeks(circle, weeks);

        fit.Should().Be(WeekFitResult.FewerWeeksThanNeeded);
        scheduled.Should().ContainSingle();
        scheduled[0].RoundId.Should().Be(10);
        scheduled[0].Pairings.Should().BeEquivalentTo(circle[0]);
    }
}
