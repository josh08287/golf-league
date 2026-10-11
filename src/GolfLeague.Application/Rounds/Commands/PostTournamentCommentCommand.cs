using GolfLeague.Application.Admin;
using GolfLeague.Application.Common;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

public sealed record CommentReactionSummary(
    string Emoji,
    int Count,
    List<int> PlayerIds);

public sealed record TournamentCommentDto(
    int Id,
    int PlayerId,
    string PlayerName,
    string Body,
    DateTime CreatedAt,
    int? ParentCommentId,
    List<TournamentCommentDto> Replies,
    List<CommentReactionSummary> Reactions);

public sealed record PostTournamentCommentCommand(
    int RoundId,
    int PlayerId,
    string Body,
    string UserId,
    int? ParentCommentId = null) : IRequest<Result<TournamentCommentDto>>, IAmAuditableCommand
{
    public string AuditEntityType => "Round";
    public string AuditEntityId => RoundId.ToString();
}

public sealed class PostTournamentCommentCommandHandler : IRequestHandler<PostTournamentCommentCommand, Result<TournamentCommentDto>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly IFeatureFlagRepository _featureFlags;

    public PostTournamentCommentCommandHandler(IRoundRepository roundRepository, IFeatureFlagRepository featureFlags)
    {
        _roundRepository = roundRepository;
        _featureFlags = featureFlags;
    }

    public async Task<Result<TournamentCommentDto>> Handle(PostTournamentCommentCommand request, CancellationToken cancellationToken)
    {
        var flag = await _featureFlags.GetAsync(KnownFeatureFlags.TournamentCommentsEnabled, cancellationToken);
        var enabled = flag?.Enabled ?? KnownFeatureFlags.Defaults[KnownFeatureFlags.TournamentCommentsEnabled];
        if (!enabled)
            return Result<TournamentCommentDto>.Fail("Tournament comments are not enabled.");

        var body = request.Body.Trim();
        if (body.Length == 0)
            return Result<TournamentCommentDto>.Fail("Message cannot be empty.");
        if (body.Length > 1000)
            return Result<TournamentCommentDto>.Fail("Message cannot exceed 1000 characters.");

        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<TournamentCommentDto>.Fail($"Round {request.RoundId} not found.");

        if (request.ParentCommentId is not null)
        {
            var parent = await _roundRepository.GetTournamentCommentByIdAsync(request.ParentCommentId.Value, cancellationToken);
            if (parent is null)
                return Result<TournamentCommentDto>.Fail("Parent comment not found.");
            if (parent.RoundId != request.RoundId)
                return Result<TournamentCommentDto>.Fail("Parent comment does not belong to this round.");
            if (parent.ParentCommentId is not null)
                return Result<TournamentCommentDto>.Fail("Cannot reply to a reply. Only top-level comments accept replies.");
        }

        var comment = new TournamentComment
        {
            RoundId = request.RoundId,
            PlayerId = request.PlayerId,
            Body = body,
            CreatedAt = DateTime.UtcNow,
            ParentCommentId = request.ParentCommentId,
        };

        var saved = await _roundRepository.AddTournamentCommentAsync(comment, cancellationToken);

        var all = await _roundRepository.GetTournamentCommentsAsync(request.RoundId, cancellationToken);
        var withPlayer = FindComment(all, saved.Id);

        return Result<TournamentCommentDto>.Ok(MapToDto(withPlayer!));
    }

    private static TournamentComment? FindComment(IReadOnlyList<TournamentComment> comments, int id)
    {
        foreach (var c in comments)
        {
            if (c.Id == id) return c;
            foreach (var r in c.Replies)
                if (r.Id == id) return r;
        }
        return null;
    }

    internal static TournamentCommentDto MapToDto(TournamentComment c) =>
        new(
            c.Id,
            c.PlayerId,
            c.Player.FullName,
            c.Body,
            c.CreatedAt,
            c.ParentCommentId,
            (c.Replies ?? []).OrderBy(r => r.CreatedAt).Select(MapToDto).ToList(),
            MapReactions(c.Reactions));

    internal static List<CommentReactionSummary> MapReactions(ICollection<CommentReaction>? reactions)
    {
        if (reactions is null || reactions.Count == 0)
            return [];
        return reactions
            .GroupBy(r => r.Emoji)
            .Select(g => new CommentReactionSummary(g.Key, g.Count(), g.Select(r => r.PlayerId).ToList()))
            .OrderByDescending(s => s.Count)
            .ToList();
    }
}
