using FluentAssertions;
using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Interfaces;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Functions.Functions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Xunit;

namespace GolfLeague.Tests;

/// <summary>
/// Covers TeeTimeFunctions admin endpoints that bypass MediatR and embed
/// their own capacity/group-membership logic directly: AdminMoveParticipant,
/// AdminRemoveParticipant, SetTeeTimeParticipantSkipped, and
/// GetRoundParticipantsForAdmin. None of these had prior test coverage.
/// </summary>
public class TeeTimeFunctionsTests
{
    private static HttpRequest MakeRequest(string? body = null, string? role = null, int? playerId = null)
    {
        var context = new DefaultHttpContext();
        if (role is not null || playerId is not null)
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "user1") };
            if (role is not null) claims.Add(new Claim(ClaimTypes.Role, role));
            if (playerId is not null) claims.Add(new Claim("playerId", playerId.Value.ToString()));
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        }
        if (body is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentType = "application/json";
            context.Request.ContentLength = bytes.Length;
        }
        return context.Request;
    }

    private static Player MakePlayer(int id) => new() { Id = id, FirstName = "P", LastName = id.ToString() };

    private sealed class Mocks
    {
        public Mock<ITeeTimeService> Service { get; } = new();
        public Mock<ITeeTimeAutofillService> Autofill { get; } = new();
        public Mock<IRoundRepository> Rounds { get; } = new();
        public Mock<ITeeTimeRepository> TeeTimes { get; } = new();
        public Mock<IMediator> Mediator { get; } = new();
        public Mock<IAuditRepository> AuditRepo { get; } = new();
        public Mock<ILogger<TeeTimeFunctions>> Logger { get; } = new();

        public TeeTimeFunctions BuildSut() => new(
            Service.Object, Autofill.Object, Rounds.Object, TeeTimes.Object, Mediator.Object,
            new AuditWriter(AuditRepo.Object, new Mock<ILogger<AuditWriter>>().Object), Logger.Object);
    }

    // ── AdminMoveParticipantToTeeTime ────────────────────────────────────

    [Fact]
    public async Task AdminMoveParticipant_WhenNotAdmin_ReturnsForbidden()
    {
        var m = new Mocks();
        var sut = m.BuildSut();

        var result = await sut.AdminMoveParticipant(MakeRequest(), 1, 2, 3, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task AdminMoveParticipant_WhenRoundNotFound_ReturnsNotFound()
    {
        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Round?)null);
        var sut = m.BuildSut();

        var result = await sut.AdminMoveParticipant(MakeRequest(role: "admin"), 1, 2, 3, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AdminMoveParticipant_WhenParticipantNotInRound_ReturnsNotFound()
    {
        var m = new Mocks();
        var round = new Round { Id = 1, Participants = [] };
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        var sut = m.BuildSut();

        var result = await sut.AdminMoveParticipant(MakeRequest(role: "admin"), 1, 2, 99, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AdminMoveParticipant_WhenSlotNotInRound_ReturnsNotFound()
    {
        var m = new Mocks();
        var participant = new RoundParticipant { Id = 3, RoundId = 1, PlayerId = 10, Player = MakePlayer(10) };
        var round = new Round { Id = 1, Participants = [participant] };
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        // Slot belongs to a different round.
        m.TeeTimes.Setup(t => t.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoundTeeTime { Id = 2, RoundId = 999, Participants = [] });
        var sut = m.BuildSut();

        var result = await sut.AdminMoveParticipant(MakeRequest(role: "admin"), 1, 2, 3, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AdminMoveParticipant_WhenSlotFull_ReturnsConflict()
    {
        var m = new Mocks();
        var participant = new RoundParticipant { Id = 3, RoundId = 1, PlayerId = 10, Player = MakePlayer(10) };
        var round = new Round { Id = 1, Participants = [participant] };
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);

        // 4 other occupants already fill the slot (capacity is 4).
        var slot = new RoundTeeTime
        {
            Id = 2,
            RoundId = 1,
            Participants = Enumerable.Range(100, 4)
                .Select(i => new RoundParticipant { Id = i, RoundId = 1, PlayerId = i, Player = MakePlayer(i) })
                .ToList(),
        };
        m.TeeTimes.Setup(t => t.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(slot);
        var sut = m.BuildSut();

        var result = await sut.AdminMoveParticipant(MakeRequest(role: "admin"), 1, 2, 3, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
        m.TeeTimes.Verify(t => t.SetParticipantTeeTimeAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AdminMoveParticipant_WhenParticipantAlreadyInTargetSlot_DoesNotCountSelfTowardCapacity()
    {
        // 4 occupants in the slot, but one of them IS the participant being
        // "moved" (a no-op move) — must not be double-counted against capacity.
        var m = new Mocks();
        var participant = new RoundParticipant { Id = 3, RoundId = 1, PlayerId = 10, TeeTimeId = 2, Player = MakePlayer(10) };
        var round = new Round { Id = 1, Participants = [participant] };
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);

        var others = Enumerable.Range(100, 3)
            .Select(i => new RoundParticipant { Id = i, RoundId = 1, PlayerId = i, Player = MakePlayer(i) })
            .ToList();
        var slot = new RoundTeeTime { Id = 2, RoundId = 1, Participants = [.. others, participant] };
        m.TeeTimes.Setup(t => t.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(slot);
        m.Service.Setup(s => s.GetScheduleAsync(1, It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GolfLeague.Application.DTOs.RoundTeeTimeScheduleDto>.Fail("n/a"));
        var sut = m.BuildSut();

        var result = await sut.AdminMoveParticipant(MakeRequest(role: "admin"), 1, 2, 3, CancellationToken.None);

        result.Should().NotBeOfType<ConflictObjectResult>();
        // Already in this slot — no actual move call needed.
        m.TeeTimes.Verify(t => t.SetParticipantTeeTimeAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AdminMoveParticipant_WhenSlotHasRoom_MovesParticipant()
    {
        var m = new Mocks();
        var participant = new RoundParticipant { Id = 3, RoundId = 1, PlayerId = 10, TeeTimeId = 5, Player = MakePlayer(10) };
        var round = new Round { Id = 1, Participants = [participant] };
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);

        var slot = new RoundTeeTime { Id = 2, RoundId = 1, Participants = [] };
        m.TeeTimes.Setup(t => t.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(slot);
        m.Service.Setup(s => s.GetScheduleAsync(1, It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GolfLeague.Application.DTOs.RoundTeeTimeScheduleDto>.Fail("n/a"));
        var sut = m.BuildSut();

        await sut.AdminMoveParticipant(MakeRequest(role: "admin"), 1, 2, 3, CancellationToken.None);

        m.TeeTimes.Verify(t => t.SetParticipantTeeTimeAsync(3, 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── AdminSwapTeeTimeParticipants ─────────────────────────────────────

    [Fact]
    public async Task AdminSwapParticipants_WhenNotAdmin_ReturnsForbidden()
    {
        var m = new Mocks();
        var sut = m.BuildSut();

        var result = await sut.AdminSwapParticipants(MakeRequest(), 1, 3, 4, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task AdminSwapParticipants_WhenSameParticipantTwice_ReturnsBadRequest()
    {
        var m = new Mocks();
        var sut = m.BuildSut();

        var result = await sut.AdminSwapParticipants(MakeRequest(role: "admin"), 1, 3, 3, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        m.TeeTimes.Verify(t => t.SwapParticipantTeeTimesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AdminSwapParticipants_WhenRoundNotFound_ReturnsNotFound()
    {
        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Round?)null);
        var sut = m.BuildSut();

        var result = await sut.AdminSwapParticipants(MakeRequest(role: "admin"), 1, 3, 4, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AdminSwapParticipants_WhenEitherParticipantNotInRound_ReturnsNotFound()
    {
        var m = new Mocks();
        var participant = new RoundParticipant { Id = 3, RoundId = 1, PlayerId = 10, TeeTimeId = 2, Player = MakePlayer(10) };
        var round = new Round { Id = 1, Participants = [participant] };
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        var sut = m.BuildSut();

        var result = await sut.AdminSwapParticipants(MakeRequest(role: "admin"), 1, 3, 99, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
        m.TeeTimes.Verify(t => t.SwapParticipantTeeTimesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AdminSwapParticipants_WhenBothInRound_SwapsEvenWhenSlotsAreFull()
    {
        // Swapping never changes slot occupant counts, so it must succeed
        // even when both participants are already sitting in full foursomes.
        var m = new Mocks();
        var participantA = new RoundParticipant { Id = 3, RoundId = 1, PlayerId = 10, TeeTimeId = 2, Player = MakePlayer(10) };
        var participantB = new RoundParticipant { Id = 4, RoundId = 1, PlayerId = 11, TeeTimeId = 5, Player = MakePlayer(11) };
        var round = new Round { Id = 1, Participants = [participantA, participantB] };
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        m.Service.Setup(s => s.GetScheduleAsync(1, It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GolfLeague.Application.DTOs.RoundTeeTimeScheduleDto>.Fail("n/a"));
        var sut = m.BuildSut();

        await sut.AdminSwapParticipants(MakeRequest(role: "admin"), 1, 3, 4, CancellationToken.None);

        m.TeeTimes.Verify(t => t.SwapParticipantTeeTimesAsync(3, 4, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── AdminRemoveParticipantFromTeeTime ────────────────────────────────

    [Fact]
    public async Task AdminRemoveParticipant_WhenNotAdmin_ReturnsForbidden()
    {
        var m = new Mocks();
        var sut = m.BuildSut();

        var result = await sut.AdminRemoveParticipant(MakeRequest(), 1, 3, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task AdminRemoveParticipant_WhenParticipantNotInRound_ReturnsNotFound()
    {
        var m = new Mocks();
        var round = new Round { Id = 1, Participants = [] };
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        var sut = m.BuildSut();

        var result = await sut.AdminRemoveParticipant(MakeRequest(role: "admin"), 1, 99, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AdminRemoveParticipant_WhenNotAssignedToAnySlot_DoesNotCallRepository()
    {
        var m = new Mocks();
        var participant = new RoundParticipant { Id = 3, RoundId = 1, PlayerId = 10, TeeTimeId = null, Player = MakePlayer(10) };
        var round = new Round { Id = 1, Participants = [participant] };
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        m.Service.Setup(s => s.GetScheduleAsync(1, It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GolfLeague.Application.DTOs.RoundTeeTimeScheduleDto>.Fail("n/a"));
        var sut = m.BuildSut();

        await sut.AdminRemoveParticipant(MakeRequest(role: "admin"), 1, 3, CancellationToken.None);

        m.TeeTimes.Verify(t => t.SetParticipantTeeTimeAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AdminRemoveParticipant_WhenAssigned_ClearsTeeTime()
    {
        var m = new Mocks();
        var participant = new RoundParticipant { Id = 3, RoundId = 1, PlayerId = 10, TeeTimeId = 5, Player = MakePlayer(10) };
        var round = new Round { Id = 1, Participants = [participant] };
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        m.Service.Setup(s => s.GetScheduleAsync(1, It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GolfLeague.Application.DTOs.RoundTeeTimeScheduleDto>.Fail("n/a"));
        var sut = m.BuildSut();

        await sut.AdminRemoveParticipant(MakeRequest(role: "admin"), 1, 3, CancellationToken.None);

        m.TeeTimes.Verify(t => t.SetParticipantTeeTimeAsync(3, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── SetTeeTimeParticipantSkipped ─────────────────────────────────────

    [Fact]
    public async Task SetTeeTimeParticipantSkipped_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var m = new Mocks();
        var sut = m.BuildSut();
        var body = JsonSerializer.Serialize(new { Skipped = true });

        var result = await sut.SetTeeTimeParticipantSkipped(MakeRequest(body), 1, 5, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task SetTeeTimeParticipantSkipped_WhenCallerHasNoPlayerProfile_ReturnsConflict()
    {
        var m = new Mocks();
        var sut = m.BuildSut();
        var body = JsonSerializer.Serialize(new { Skipped = true });

        var result = await sut.SetTeeTimeParticipantSkipped(MakeRequest(body, role: "player"), 1, 5, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task SetTeeTimeParticipantSkipped_WhenTeeTimeNotFound_ReturnsNotFound()
    {
        var m = new Mocks();
        m.TeeTimes.Setup(t => t.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((RoundTeeTime?)null);
        var sut = m.BuildSut();
        var body = JsonSerializer.Serialize(new { Skipped = true });

        var result = await sut.SetTeeTimeParticipantSkipped(MakeRequest(body, playerId: 7), 1, 5, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task SetTeeTimeParticipantSkipped_WhenCallerNotInGroup_ReturnsForbidden()
    {
        // Caller (playerId 7) is not a participant in this tee time's group.
        var m = new Mocks();
        var teeTime = new RoundTeeTime
        {
            Id = 1,
            RoundId = 10,
            Participants = [new RoundParticipant { Id = 1, PlayerId = 99, Player = MakePlayer(99) }],
        };
        m.TeeTimes.Setup(t => t.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(teeTime);
        var sut = m.BuildSut();
        var body = JsonSerializer.Serialize(new { Skipped = true });

        var result = await sut.SetTeeTimeParticipantSkipped(MakeRequest(body, playerId: 7), 1, 99, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task SetTeeTimeParticipantSkipped_WhenCallerIsWithdrawn_ReturnsForbidden()
    {
        // Caller is technically a participant row but has been withdrawn —
        // should not count as "in the group" for authorization purposes.
        var m = new Mocks();
        var teeTime = new RoundTeeTime
        {
            Id = 1,
            RoundId = 10,
            Participants = [new RoundParticipant { Id = 1, PlayerId = 7, IsWithdrawn = true, Player = MakePlayer(7) }],
        };
        m.TeeTimes.Setup(t => t.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(teeTime);
        var sut = m.BuildSut();
        var body = JsonSerializer.Serialize(new { Skipped = true });

        var result = await sut.SetTeeTimeParticipantSkipped(MakeRequest(body, playerId: 7), 1, 7, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task SetTeeTimeParticipantSkipped_WhenCallerInGroup_SetsSkippedForTargetPlayer()
    {
        // Caller (7) is in the group and is setting *another* player's (99)
        // skip status — allowed since the command validates group membership.
        var m = new Mocks();
        var teeTime = new RoundTeeTime
        {
            Id = 1,
            RoundId = 10,
            Participants =
            [
                new RoundParticipant { Id = 1, PlayerId = 7, Player = MakePlayer(7) },
                new RoundParticipant { Id = 2, PlayerId = 99, Player = MakePlayer(99) },
            ],
        };
        m.TeeTimes.Setup(t => t.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(teeTime);
        SetParticipantSkippedCommand? captured = null;
        m.Mediator.Setup(med => med.Send(It.IsAny<SetParticipantSkippedCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<bool>>, CancellationToken>((c, _) => captured = (SetParticipantSkippedCommand)c)
            .ReturnsAsync(Result<bool>.Ok(true));
        var sut = m.BuildSut();
        var body = JsonSerializer.Serialize(new { Skipped = true });

        var result = await sut.SetTeeTimeParticipantSkipped(MakeRequest(body, playerId: 7), 1, 99, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        captured!.RoundId.Should().Be(10);
        captured.PlayerId.Should().Be(99);
        captured.Skipped.Should().BeTrue();
    }

    // ── SkipMyWeek ────────────────────────────────────────────────────────

    [Fact]
    public async Task SkipMyWeek_WhenRoundAlreadyStarted_ReturnsBadRequest()
    {
        // RoundDate is in the past relative to "now" — should be rejected
        // before the command even runs, regardless of round Status.
        // Uses -2 days (not -1) because the endpoint compares against
        // Eastern-time "today", which can trail the UTC date by up to a
        // full day; -1 day flaked depending on time-of-day in CI.
        var m = new Mocks();
        var round = new Round { Id = 1, RoundDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)) };
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        var sut = m.BuildSut();
        var body = JsonSerializer.Serialize(new { Skipped = true });

        var result = await sut.SkipMyWeek(MakeRequest(body, playerId: 7), 1, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        m.Mediator.Verify(med => med.Send(It.IsAny<SetParticipantSkippedCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SkipMyWeek_WhenRoundIsUpcoming_SkipsForCallingPlayer()
    {
        var m = new Mocks();
        var round = new Round { Id = 1, RoundDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)) };
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        SetParticipantSkippedCommand? captured = null;
        m.Mediator.Setup(med => med.Send(It.IsAny<SetParticipantSkippedCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<bool>>, CancellationToken>((c, _) => captured = (SetParticipantSkippedCommand)c)
            .ReturnsAsync(Result<bool>.Ok(true));
        m.Service.Setup(s => s.GetScheduleAsync(1, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RoundTeeTimeScheduleDto>.Ok(new RoundTeeTimeScheduleDto(
                RoundId: 1,
                CutoffUtc: DateTime.UtcNow,
                IsLocked: false,
                ParticipantCount: 1,
                CurrentUserParticipantId: 1,
                CurrentUserTeeTimeId: null,
                Slots: [],
                WeekNumber: 1,
                RoundDate: "2026-07-08",
                CourseName: "Test Course")));
        var sut = m.BuildSut();
        var body = JsonSerializer.Serialize(new { Skipped = true });

        var result = await sut.SkipMyWeek(MakeRequest(body, playerId: 7), 1, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        captured!.RoundId.Should().Be(1);
        captured.PlayerId.Should().Be(7);
        captured.Skipped.Should().BeTrue();
    }

    // ── GetRoundParticipantsForAdmin ─────────────────────────────────────

    [Fact]
    public async Task GetRoundParticipantsForAdmin_WhenNotAdmin_ReturnsForbidden()
    {
        var m = new Mocks();
        var sut = m.BuildSut();

        var result = await sut.GetRoundParticipantsForAdmin(MakeRequest(), 1, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetRoundParticipantsForAdmin_WhenRoundNotFound_ReturnsNotFound()
    {
        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Round?)null);
        var sut = m.BuildSut();

        var result = await sut.GetRoundParticipantsForAdmin(MakeRequest(role: "admin"), 1, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetRoundParticipantsForAdmin_ExcludesWithdrawnAndSkippedParticipants()
    {
        var m = new Mocks();
        var round = new Round
        {
            Id = 1,
            Participants =
            [
                new RoundParticipant { Id = 1, PlayerId = 10, Player = MakePlayer(10) },
                new RoundParticipant { Id = 2, PlayerId = 20, Player = MakePlayer(20), IsWithdrawn = true },
                new RoundParticipant { Id = 3, PlayerId = 30, Player = MakePlayer(30), SkippedWeek = true },
            ],
        };
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        var sut = m.BuildSut();

        var result = await sut.GetRoundParticipantsForAdmin(MakeRequest(role: "admin"), 1, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("\"PlayerId\":10");
        json.Should().NotContain("\"PlayerId\":20");
        json.Should().NotContain("\"PlayerId\":30");
    }
}
