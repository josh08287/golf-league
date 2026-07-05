using GolfLeague.Application.Common;

namespace GolfLeague.Application.Interfaces;

public interface ITeeTimeAutofillService
{
    /// <summary>
    /// Run autofill for a single round: top off any partial existing tee
    /// times, then assign remaining participants to new slots front to back,
    /// filling each to a full foursome. Same-flight (2+2) grouping is a soft
    /// preference that's relaxed whenever needed to complete a foursome.
    /// Idempotent — if all participants are already assigned, does nothing.
    /// </summary>
    Task<Result<AutofillResult>> RunAsync(int roundId, CancellationToken cancellationToken = default);
}

public sealed record AutofillResult(
    int AssignedCount,
    int SlotsTouched);
