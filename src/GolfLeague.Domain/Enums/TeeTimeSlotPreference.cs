namespace GolfLeague.Domain.Enums;

/// <summary>
/// Bit flags representing a player's preferred tee-time slot(s).
/// None = no preference (autofill may place anywhere).
/// Multiple flags = player is happy with any of the selected slots.
/// </summary>
[Flags]
public enum TeeTimeSlotPreference
{
    None   = 0,
    Early  = 1,
    Middle = 2,
    Late   = 4,
}
