using GolfLeague.Application.Admin;
using GolfLeague.Application.Common;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

public sealed record TournamentCommentDto(
    int Id,
    int PlayerId,
    string PlayerName,
    string Body,
    DateTime CreatedAt);

public sealed record PostTournamentCommentCommand(
    int RoundId,
    int PlayerId,
    string Body,
    string UserId) : IRequest<Result<TournamentCommentDto>>, IAmAuditableCommand
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

        var comment = new TournamentComment
        {
            RoundId = request.RoundId,
            PlayerId = request.PlayerId,
            Body = body,
            CreatedAt = DateTime.UtcNow,
        };

        var saved = await _roundRepository.AddTournamentCommentAsync(comment, cancellationToken);

        // Re-fetch through the list query so Player is populated for the DTO
        // without a second round trip to load just this one comment's player.
        var all = await _roundRepository.GetTournamentCommentsAsync(request.RoundId, cancellationToken);
        var withPlayer = all.First(c => c.Id == saved.Id);

        return Result<TournamentCommentDto>.Ok(new TournamentCommentDto(
            withPlayer.Id,
            withPlayer.PlayerId,
            withPlayer.Player.FullName,
            withPlayer.Body,
            withPlayer.CreatedAt));
    }
}
