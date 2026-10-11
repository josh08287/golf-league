using FluentAssertions;
using GolfLeague.Application.Admin;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Application.Rounds.Queries;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

public class TournamentCommentsHandlerTests
{
    private static Round MakeRound(int id = 1) => new()
    {
        Id = id,
        RoundDate = DateOnly.FromDateTime(DateTime.UtcNow),
    };

    private static Player MakePlayer(int id = 10, string firstName = "Tiger", string lastName = "Woods") => new()
    {
        Id = id,
        FirstName = firstName,
        LastName = lastName,
    };

    private static Mock<IFeatureFlagRepository> CreateFeatureFlagsMock(bool enabled = true)
    {
        var mock = new Mock<IFeatureFlagRepository>();
        mock.Setup(f => f.GetAsync(KnownFeatureFlags.TournamentCommentsEnabled, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureFlag { Key = KnownFeatureFlags.TournamentCommentsEnabled, Enabled = enabled });
        return mock;
    }

    [Fact]
    public async Task PostComment_WhenFeatureFlagDisabled_Fails()
    {
        var rounds = new Mock<IRoundRepository>();
        var flags = CreateFeatureFlagsMock(enabled: false);

        var handler = new PostTournamentCommentCommandHandler(rounds.Object, flags.Object);
        var result = await handler.Handle(
            new PostTournamentCommentCommand(1, 10, "Great round!", "user-1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not enabled");
    }

    [Fact]
    public async Task PostComment_WhenRoundNotFound_Fails()
    {
        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Round?)null);
        var flags = CreateFeatureFlagsMock(enabled: true);

        var handler = new PostTournamentCommentCommandHandler(rounds.Object, flags.Object);
        var result = await handler.Handle(
            new PostTournamentCommentCommand(1, 10, "Great round!", "user-1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Round 1 not found");
    }

    [Fact]
    public async Task PostComment_TopLevel_Succeeds()
    {
        var round = MakeRound(1);
        var player = MakePlayer(10);
        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);

        TournamentComment? savedComment = null;
        rounds.Setup(r => r.AddTournamentCommentAsync(It.IsAny<TournamentComment>(), It.IsAny<CancellationToken>()))
            .Callback<TournamentComment, CancellationToken>((c, _) =>
            {
                c.Id = 101;
                c.Player = player;
                savedComment = c;
            })
            .ReturnsAsync((TournamentComment c, CancellationToken _) => c);

        rounds.Setup(r => r.GetTournamentCommentsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new List<TournamentComment> { savedComment! });

        var flags = CreateFeatureFlagsMock(enabled: true);
        var handler = new PostTournamentCommentCommandHandler(rounds.Object, flags.Object);

        var result = await handler.Handle(
            new PostTournamentCommentCommand(1, 10, "Nice birdie on 18!", "user-1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(101);
        result.Value.PlayerName.Should().Be("Tiger Woods");
        result.Value.Body.Should().Be("Nice birdie on 18!");
        result.Value.ParentCommentId.Should().BeNull();
        result.Value.Replies.Should().BeEmpty();
        result.Value.Reactions.Should().BeEmpty();
    }

    [Fact]
    public async Task PostComment_ReplyToTopLevel_Succeeds()
    {
        var round = MakeRound(1);
        var parentPlayer = MakePlayer(10, "Tiger", "Woods");
        var replyPlayer = MakePlayer(20, "Phil", "Mickelson");

        var parent = new TournamentComment
        {
            Id = 100,
            RoundId = 1,
            PlayerId = 10,
            Player = parentPlayer,
            Body = "Great tournament everyone!",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
        };

        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        rounds.Setup(r => r.GetTournamentCommentByIdAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(parent);

        TournamentComment? savedReply = null;
        rounds.Setup(r => r.AddTournamentCommentAsync(It.IsAny<TournamentComment>(), It.IsAny<CancellationToken>()))
            .Callback<TournamentComment, CancellationToken>((c, _) =>
            {
                c.Id = 102;
                c.Player = replyPlayer;
                savedReply = c;
                parent.Replies.Add(c);
            })
            .ReturnsAsync((TournamentComment c, CancellationToken _) => c);

        rounds.Setup(r => r.GetTournamentCommentsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new List<TournamentComment> { parent });

        var flags = CreateFeatureFlagsMock(enabled: true);
        var handler = new PostTournamentCommentCommandHandler(rounds.Object, flags.Object);

        var result = await handler.Handle(
            new PostTournamentCommentCommand(1, 20, "Thanks Tiger!", "user-2", ParentCommentId: 100),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(102);
        result.Value.ParentCommentId.Should().Be(100);
        result.Value.PlayerName.Should().Be("Phil Mickelson");
        result.Value.Body.Should().Be("Thanks Tiger!");
    }

    [Fact]
    public async Task PostComment_ReplyToNestedReply_IsRejected()
    {
        var round = MakeRound(1);
        var parent = new TournamentComment
        {
            Id = 105,
            RoundId = 1,
            PlayerId = 10,
            ParentCommentId = 100, // already a reply
            Body = "I am a reply",
        };

        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        rounds.Setup(r => r.GetTournamentCommentByIdAsync(105, It.IsAny<CancellationToken>())).ReturnsAsync(parent);

        var flags = CreateFeatureFlagsMock(enabled: true);
        var handler = new PostTournamentCommentCommandHandler(rounds.Object, flags.Object);

        var result = await handler.Handle(
            new PostTournamentCommentCommand(1, 20, "Reply to reply", "user-2", ParentCommentId: 105),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Cannot reply to a reply");
    }

    [Fact]
    public async Task PostComment_ReplyToParentInDifferentRound_IsRejected()
    {
        var round = MakeRound(1);
        var parent = new TournamentComment
        {
            Id = 100,
            RoundId = 2, // different round!
            PlayerId = 10,
            Body = "Different round",
        };

        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        rounds.Setup(r => r.GetTournamentCommentByIdAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(parent);

        var flags = CreateFeatureFlagsMock(enabled: true);
        var handler = new PostTournamentCommentCommandHandler(rounds.Object, flags.Object);

        var result = await handler.Handle(
            new PostTournamentCommentCommand(1, 20, "Reply", "user-2", ParentCommentId: 100),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("does not belong to this round");
    }

    [Fact]
    public async Task ToggleCommentReaction_WhenEmojiDisallowed_Fails()
    {
        var rounds = new Mock<IRoundRepository>();
        var handler = new ToggleCommentReactionCommandHandler(rounds.Object);

        var result = await handler.Handle(
            new ToggleCommentReactionCommand(100, 10, "💩"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not allowed");
    }

    [Fact]
    public async Task ToggleCommentReaction_WhenCommentNotFound_Fails()
    {
        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetTournamentCommentByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TournamentComment?)null);

        var handler = new ToggleCommentReactionCommandHandler(rounds.Object);
        var result = await handler.Handle(
            new ToggleCommentReactionCommand(999, 10, "👍"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Comment not found");
    }

    [Fact]
    public async Task ToggleCommentReaction_AddsReaction_WhenNoneExists()
    {
        var comment = new TournamentComment { Id = 100, RoundId = 1, PlayerId = 1 };
        var reactionsList = new List<CommentReaction>();

        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetTournamentCommentByIdAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(comment);
        rounds.Setup(r => r.GetCommentReactionsAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(reactionsList);
        rounds.Setup(r => r.AddCommentReactionAsync(It.IsAny<CommentReaction>(), It.IsAny<CancellationToken>()))
            .Callback<CommentReaction, CancellationToken>((cr, _) => reactionsList.Add(cr))
            .ReturnsAsync((CommentReaction cr, CancellationToken _) => cr);

        var handler = new ToggleCommentReactionCommandHandler(rounds.Object);
        var result = await handler.Handle(
            new ToggleCommentReactionCommand(100, 10, "👍"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Emoji.Should().Be("👍");
        result.Value.Count.Should().Be(1);
        result.Value.PlayerIds.Should().Contain(10);
        rounds.Verify(r => r.AddCommentReactionAsync(It.IsAny<CommentReaction>(), It.IsAny<CancellationToken>()), Times.Once);
        rounds.Verify(r => r.RemoveCommentReactionAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ToggleCommentReaction_RemovesReaction_WhenAlreadyExists()
    {
        var comment = new TournamentComment { Id = 100, RoundId = 1, PlayerId = 1 };
        var existing = new CommentReaction { CommentId = 100, PlayerId = 10, Emoji = "👍" };
        var reactionsList = new List<CommentReaction> { existing };

        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetTournamentCommentByIdAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(comment);
        rounds.Setup(r => r.GetCommentReactionsAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(reactionsList);
        rounds.Setup(r => r.RemoveCommentReactionAsync(100, 10, "👍", It.IsAny<CancellationToken>()))
            .Callback<int, int, string, CancellationToken>((_, _, _, _) => reactionsList.Remove(existing))
            .Returns(Task.CompletedTask);

        var handler = new ToggleCommentReactionCommandHandler(rounds.Object);
        var result = await handler.Handle(
            new ToggleCommentReactionCommand(100, 10, "👍"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Emoji.Should().Be("👍");
        result.Value.Count.Should().Be(0);
        result.Value.PlayerIds.Should().BeEmpty();
        rounds.Verify(r => r.RemoveCommentReactionAsync(100, 10, "👍", It.IsAny<CancellationToken>()), Times.Once);
        rounds.Verify(r => r.AddCommentReactionAsync(It.IsAny<CommentReaction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetTournamentComments_ReturnsTopLevelComments_WithRepliesAndReactions()
    {
        var player1 = MakePlayer(1, "Jack", "Nicklaus");
        var player2 = MakePlayer(2, "Arnold", "Palmer");

        var topLevel = new TournamentComment
        {
            Id = 50,
            RoundId = 1,
            PlayerId = 1,
            Player = player1,
            Body = "Top comment",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            Reactions = new List<CommentReaction>
            {
                new() { CommentId = 50, PlayerId = 1, Player = player1, Emoji = "🔥" },
                new() { CommentId = 50, PlayerId = 2, Player = player2, Emoji = "🔥" },
                new() { CommentId = 50, PlayerId = 2, Player = player2, Emoji = "⛳" },
            },
            Replies = new List<TournamentComment>
            {
                new()
                {
                    Id = 51,
                    RoundId = 1,
                    PlayerId = 2,
                    Player = player2,
                    ParentCommentId = 50,
                    Body = "Nested reply",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                    Reactions = new List<CommentReaction>
                    {
                        new() { CommentId = 51, PlayerId = 1, Player = player1, Emoji = "👏" },
                    },
                }
            }
        };

        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetTournamentCommentsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TournamentComment> { topLevel });

        var handler = new GetTournamentCommentsQueryHandler(rounds.Object);
        var result = await handler.Handle(new GetTournamentCommentsQuery(1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);

        var commentDto = result.Value![0];
        commentDto.Id.Should().Be(50);
        commentDto.PlayerName.Should().Be("Jack Nicklaus");
        commentDto.Body.Should().Be("Top comment");
        commentDto.Reactions.Should().HaveCount(2);

        var fireReaction = commentDto.Reactions.First(r => r.Emoji == "🔥");
        fireReaction.Count.Should().Be(2);
        fireReaction.PlayerIds.Should().BeEquivalentTo([1, 2]);

        var golfReaction = commentDto.Reactions.First(r => r.Emoji == "⛳");
        golfReaction.Count.Should().Be(1);
        golfReaction.PlayerIds.Should().BeEquivalentTo([2]);

        commentDto.Replies.Should().HaveCount(1);
        var replyDto = commentDto.Replies[0];
        replyDto.Id.Should().Be(51);
        replyDto.PlayerName.Should().Be("Arnold Palmer");
        replyDto.Body.Should().Be("Nested reply");
        replyDto.ParentCommentId.Should().Be(50);
        replyDto.Reactions.Should().HaveCount(1);
        replyDto.Reactions[0].Emoji.Should().Be("👏");
        replyDto.Reactions[0].Count.Should().Be(1);
    }
}
