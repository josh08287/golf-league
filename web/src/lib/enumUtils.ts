// ── Enum Normalization Utilities ───────────────────────────────────────────────
// All backend enums the frontend reads are now serialized as strings
// (System.Text.Json's JsonStringEnumConverter). These helpers remain to
// normalize a numeric value defensively (e.g. a stale cached response) —
// they should never need to run their numeric branch in practice.

export type RoundStatus = 'Scheduled' | 'InProgress' | 'PendingFinalization' | 'Finalized' | 'Cancelled';
export type RoundType = 'NineHole' | 'EighteenHole' | 'Tournament';
export type NineHoleSide = 'NotApplicable' | 'Front' | 'Back';

// Backend enum ordinals, for the numeric-fallback branch below.
// RoundStatus: Scheduled=0, InProgress=1, PendingFinalization=2, Finalized=3, Cancelled=4
// RoundType: NineHole=0, EighteenHole=1, Tournament=2
// NineHoleSide: NotApplicable=0, Front=1, Back=2

export function normalizeRoundStatus(status: string | number | undefined): RoundStatus {
  if (status === undefined || status === null) return 'Scheduled';
  if (typeof status === 'number') {
    const map: Record<number, RoundStatus> = {
      0: 'Scheduled',
      1: 'InProgress',
      2: 'PendingFinalization',
      3: 'Finalized',
      4: 'Cancelled',
    };
    return map[status] ?? 'Scheduled';
  }
  // It's already a string, just return it
  return status as RoundStatus;
}

export function normalizeRoundType(type: string | number | undefined): RoundType {
  if (type === undefined || type === null) return 'NineHole';
  if (typeof type === 'number') {
    const map: Record<number, RoundType> = {
      0: 'NineHole',
      1: 'EighteenHole',
      2: 'Tournament',
    };
    return map[type] ?? 'NineHole';
  }
  return type as RoundType;
}

export function normalizeNineHoleSide(side: string | number | undefined): NineHoleSide {
  if (side === undefined || side === null) return 'NotApplicable';
  if (typeof side === 'number') {
    const map: Record<number, NineHoleSide> = {
      0: 'NotApplicable',
      1: 'Front',
      2: 'Back',
    };
    return map[side] ?? 'NotApplicable';
  }
  return side as NineHoleSide;
}

// Helper to check if round is finalized (handles both int and string)
export function isRoundFinalized(status: string | number | undefined): boolean {
  return normalizeRoundStatus(status) === 'Finalized';
}

// Helper to check if round is in progress
export function isRoundInProgress(status: string | number | undefined): boolean {
  return normalizeRoundStatus(status) === 'InProgress';
}

// Helper to check if round is scheduled
export function isRoundScheduled(status: string | number | undefined): boolean {
  return normalizeRoundStatus(status) === 'Scheduled';
}

// Helper to check if round is nine hole
export function isNineHole(type: string | number | undefined): boolean {
  return normalizeRoundType(type) === 'NineHole';
}

// Helper to check if nine hole side is back
export function isBackNine(side: string | number | undefined): boolean {
  return normalizeNineHoleSide(side) === 'Back';
}
