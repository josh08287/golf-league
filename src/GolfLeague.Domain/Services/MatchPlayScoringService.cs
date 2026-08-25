namespace GolfLeague.Domain.Services;

/// <summary>
/// Standard match-play scoring: 2 points for winning a hole, 1 point each
/// for halving, 0 for losing, plus a 4-point bonus for winning more holes
/// than the opponent overall. Pure functions — absence/bye and custom-formula
/// orchestration live in MatchPlayResultCalculator.
/// </summary>
public static class MatchPlayScoringService
{
    public const int WinPoints = 2;
    public const int HalvePoints = 1;
    public const int LossPoints = 0;
    public const int MatchBonusPoints = 4;

    /// <summary>Standard per-hole points for both players, comparing net strokes.</summary>
    public static (int PlayerPoints, int OpponentPoints) HolePoints(int playerNetStrokes, int opponentNetStrokes)
    {
        if (playerNetStrokes < opponentNetStrokes) return (WinPoints, LossPoints);
        if (playerNetStrokes > opponentNetStrokes) return (LossPoints, WinPoints);
        return (HalvePoints, HalvePoints);
    }

    /// <summary>Against-the-card scoring for an absence/bye: compare net strokes to par directly.</summary>
    public static int AgainstCardHolePoints(int netStrokes, int par)
    {
        if (netStrokes < par) return WinPoints;
        if (netStrokes > par) return LossPoints;
        return HalvePoints;
    }

    /// <summary>
    /// Computes the 4-point overall match bonus given each player's
    /// holes-won count. Ties (including both zero) award no bonus.
    /// </summary>
    public static (int PlayerBonus, int OpponentBonus) MatchBonus(int playerHolesWon, int opponentHolesWon)
    {
        if (playerHolesWon > opponentHolesWon) return (MatchBonusPoints, 0);
        if (playerHolesWon < opponentHolesWon) return (0, MatchBonusPoints);
        return (0, 0);
    }
}
