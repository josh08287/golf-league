using FluentAssertions;
using GolfLeague.Application.Common;
using GolfLeague.Application.Leagues;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

public class SendSignUpReminderEmailsCommandTests
{
    private static Player MakePlayer(int id, bool hasEmail = true, bool isActive = true, bool optedOut = false) => new()
    {
        Id = id,
        FirstName = "P",
        LastName = id.ToString(),
        Email = hasEmail ? $"p{id}@example.com" : null,
        IsActive = isActive,
        TeeTimeEmailOptOut = optedOut,
    };

    private static FlightMembership MakeMembership(int playerId, int halfId, Player player) => new()
    {
        Id = playerId,
        PlayerId = playerId,
        HalfId = halfId,
        FlightId = 1,
        SeasonId = 1,
        Player = player,
    };

    private sealed class Fixture
    {
        public Mock<IRoundRepository> Rounds { get; } = new();
        public Mock<IFlightRepository> Flights { get; } = new();
        public Mock<ILeagueSettingRepository> Settings { get; } = new();
        public Mock<ILeagueRepository> Leagues { get; } = new();
        public Mock<IEmailService> Email { get; } = new();

        public SendSignUpReminderEmailsCommandHandler BuildSut() => new(
            Rounds.Object, Flights.Object, Settings.Object, Leagues.Object, Email.Object);

        public void SetSettingEnabled(int leagueId, bool enabled)
        {
            Settings.Setup(s => s.GetAsync(leagueId, KnownSettings.SignUpReminderEmailEnabled, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LeagueSetting { LeagueId = leagueId, Key = KnownSettings.SignUpReminderEmailEnabled, Value = enabled ? "true" : "false" });
        }

        public void SetCutoffTime(int leagueId, string hhmm)
        {
            Settings.Setup(s => s.GetAsync(leagueId, KnownSettings.TeeTimeCutoffTime, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LeagueSetting { LeagueId = leagueId, Key = KnownSettings.TeeTimeCutoffTime, Value = hhmm });
        }
    }

    [Fact]
    public async Task Handle_RoundNotFound_ReturnsFailure()
    {
        var fx = new Fixture();
        fx.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Round?)null);

        var result = await fx.BuildSut().Handle(new SendSignUpReminderEmailsCommand(1), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_RoundHasNoHalf_ReturnsZeroWithoutQueryingMemberships()
    {
        var fx = new Fixture();
        var round = new Round { Id = 1, LeagueId = 1, HalfId = null };
        fx.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);

        var result = await fx.BuildSut().Handle(new SendSignUpReminderEmailsCommand(1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
        fx.Flights.Verify(f => f.GetMembershipsByHalfAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SettingDisabled_ReturnsZero()
    {
        var fx = new Fixture();
        var round = new Round { Id = 1, LeagueId = 1, HalfId = 5, RoundDate = new DateOnly(2026, 6, 10) };
        fx.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        fx.SetSettingEnabled(1, enabled: false);

        var result = await fx.BuildSut().Handle(new SendSignUpReminderEmailsCommand(1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
        fx.Email.Verify(e => e.SendSignUpReminderAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_SettingEnabled_SkipsPlayersAlreadyAssignedWithdrawnOrSkipped()
    {
        var fx = new Fixture();
        var assigned = MakePlayer(1);
        var withdrawn = MakePlayer(2);
        var skipped = MakePlayer(3);
        var needsReminder = MakePlayer(4);

        var round = new Round
        {
            Id = 1,
            LeagueId = 1,
            HalfId = 5,
            RoundDate = new DateOnly(2026, 6, 10),
            Participants = new List<RoundParticipant>
            {
                new() { PlayerId = 1, Player = assigned, TeeTimeId = 100 },
                new() { PlayerId = 2, Player = withdrawn, IsWithdrawn = true },
                new() { PlayerId = 3, Player = skipped, SkippedWeek = true },
            },
        };
        fx.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        fx.SetSettingEnabled(1, enabled: true);
        fx.SetCutoffTime(1, "18:00");
        fx.Leagues.Setup(l => l.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new League { Id = 1, Name = "Test League" });

        fx.Flights.Setup(f => f.GetMembershipsByHalfAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FlightMembership>
            {
                MakeMembership(1, 5, assigned),
                MakeMembership(2, 5, withdrawn),
                MakeMembership(3, 5, skipped),
                MakeMembership(4, 5, needsReminder),
            });

        var result = await fx.BuildSut().Handle(new SendSignUpReminderEmailsCommand(1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
        fx.Email.Verify(e => e.SendSignUpReminderAsync(
            "p4@example.com", "P 4", "Test League", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        fx.Email.Verify(e => e.SendSignUpReminderAsync(
            It.Is<string>(s => s != "p4@example.com"), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_SkipsPlayersWithNoEmailInactiveOrOptedOut()
    {
        var fx = new Fixture();
        var noEmail = MakePlayer(1, hasEmail: false);
        var inactive = MakePlayer(2, isActive: false);
        var optedOut = MakePlayer(3, optedOut: true);
        var eligible = MakePlayer(4);

        var round = new Round { Id = 1, LeagueId = 1, HalfId = 5, RoundDate = new DateOnly(2026, 6, 10) };
        fx.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        fx.SetSettingEnabled(1, enabled: true);
        fx.SetCutoffTime(1, "18:00");
        fx.Leagues.Setup(l => l.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new League { Id = 1, Name = "Test League" });

        fx.Flights.Setup(f => f.GetMembershipsByHalfAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FlightMembership>
            {
                MakeMembership(1, 5, noEmail),
                MakeMembership(2, 5, inactive),
                MakeMembership(3, 5, optedOut),
                MakeMembership(4, 5, eligible),
            });

        var result = await fx.BuildSut().Handle(new SendSignUpReminderEmailsCommand(1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
    }

    [Fact]
    public async Task Handle_OneSendFails_DoesNotBlockRemainingSends()
    {
        var fx = new Fixture();
        var player1 = MakePlayer(1);
        var player2 = MakePlayer(2);

        var round = new Round { Id = 1, LeagueId = 1, HalfId = 5, RoundDate = new DateOnly(2026, 6, 10) };
        fx.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        fx.SetSettingEnabled(1, enabled: true);
        fx.SetCutoffTime(1, "18:00");
        fx.Leagues.Setup(l => l.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new League { Id = 1, Name = "Test League" });

        fx.Flights.Setup(f => f.GetMembershipsByHalfAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FlightMembership>
            {
                MakeMembership(1, 5, player1),
                MakeMembership(2, 5, player2),
            });

        fx.Email.Setup(e => e.SendSignUpReminderAsync(
                "p1@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("send failed"));

        var result = await fx.BuildSut().Handle(new SendSignUpReminderEmailsCommand(1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
    }
}
