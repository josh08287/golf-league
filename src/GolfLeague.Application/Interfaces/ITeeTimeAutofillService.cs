using GolfLeague.Application.Common;

namespace GolfLeague.Application.Interfaces;

public interface ITeeTimeAutofillService
{
    /// <summary>
    /// Run autofill for a single round: top off any partial existing tee
    /// times, then assign remaining participants to new slots using a greedy
    /// 2+2 pairing across the largest remaining flights. Idempotent — if all
    /// participants are already assigned, does nothing.
    /// </summary>
    Task<Result<AutofillResult>> RunAsync(int roundId, CancellationToken cancellationToken = default);
}

public sealed record AutofillResult(
    int AssignedCount,
    int SlotsTouched);
