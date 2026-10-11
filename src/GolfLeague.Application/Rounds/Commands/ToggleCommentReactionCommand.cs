using GolfLeague.Application.Common;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

public sealed record ToggleCommentReactionCommand(
    int CommentId,
    int PlayerId,
    string Emoji) : IRequest<Result<CommentReactionSummary>>;

public sealed class ToggleCommentReactionCommandHandler : IRequestHandler<ToggleCommentReactionCommand, Result<CommentReactionSummary>>
{
    private static readonly HashSet<string> AllowedEmoji = ["👍", "😂", "🔥", "⛳", "🎉", "👏"];

    private readonly IRoundRepository _roundRepository;

    public ToggleCommentReactionCommandHandler(IRoundRepository roundRepository)
    {
        _roundRepository = roundRepository;
    }

    public async Task<Result<CommentReactionSummary>> Handle(ToggleCommentReactionCommand request, CancellationToken cancellationToken)
    {
        if (!AllowedEmoji.Contains(request.Emoji))
            return Result<CommentReactionSummary>.Fail($"Emoji '{request.Emoji}' is not allowed.");

        var comment = await _roundRepository.GetTournamentCommentByIdAsync(request.CommentId, cancellationToken);
        if (comment is null)
            return Result<CommentReactionSummary>.Fail("Comment not found.");

        var existing = await _roundRepository.GetCommentReactionsAsync(request.CommentId, cancellationToken);
        var mine = existing.FirstOrDefault(r => r.PlayerId == request.PlayerId && r.Emoji == request.Emoji);

        if (mine is not null)
        {
            await _roundRepository.RemoveCommentReactionAsync(request.CommentId, request.PlayerId, request.Emoji, cancellationToken);
        }
        else
        {
            var reaction = new CommentReaction
            {
                CommentId = request.CommentId,
                PlayerId = request.PlayerId,
                Emoji = request.Emoji,
                CreatedAt = DateTime.UtcNow,
            };
            await _roundRepository.AddCommentReactionAsync(reaction, cancellationToken);
        }

        // Return updated reaction summary for this emoji on this comment
        var updated = await _roundRepository.GetCommentReactionsAsync(request.CommentId, cancellationToken);
        var group = updated.Where(r => r.Emoji == request.Emoji).ToList();
        var summary = new CommentReactionSummary(
            request.Emoji,
            group.Count,
            group.Select(r => r.PlayerId).ToList());

        return Result<CommentReactionSummary>.Ok(summary);
    }
}
