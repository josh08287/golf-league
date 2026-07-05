namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Describes a detected conflict where a different player already saved a
/// different gross score for the same participant/hole.
/// </summary>
public sealed record HoleScoreConflictDto(
    int PlayerId,
    string PlayerName,
    int HoleNumber,
    int ExistingGrossStrokes,
    string? ExistingEnteredByName,
    int NewGrossStrokes);

/// <summary>
/// A (playerId, holeNumber) pair the client has already shown a conflict
/// dialog for and the user chose to overwrite with their new value.
/// </summary>
public sealed record ConfirmedOverwrite(int PlayerId, int HoleNumber);
