import { type ClassValue, clsx } from 'clsx';
import { twMerge } from 'tailwind-merge';

export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}

export function formatDate(dateString: string): string {
  return new Intl.DateTimeFormat('en-US', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    timeZone: 'UTC',
  }).format(new Date(dateString));
}

export function formatShortDate(dateString: string): string {
  return new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    timeZone: 'UTC',
  }).format(new Date(dateString));
}

const EASTERN_TIME_ZONE = 'America/New_York';

/**
 * Offset in minutes between the given IANA time zone and UTC, for "today"
 * (i.e. using the current UTC offset, which reflects DST if applicable).
 * Positive means the zone is ahead of UTC.
 */
function utcOffsetMinutes(timeZone: string, reference: Date = new Date()): number {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone,
    hourCycle: 'h23',
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', second: '2-digit',
  }).formatToParts(reference);
  const get = (type: string) => Number(parts.find((p) => p.type === type)?.value);
  const asUtc = Date.UTC(get('year'), get('month') - 1, get('day'), get('hour'), get('minute'), get('second'));
  return Math.round((asUtc - reference.getTime()) / 60_000);
}

/**
 * Converts an "HH:mm" time of day from the browser's local time zone to the
 * equivalent "HH:mm" in US/Eastern, so admins can enter the tee-time cutoff
 * in their own time zone while the backend stores/interprets it as Eastern.
 * Uses today's UTC offsets for both zones, which is correct except in the
 * rare case the entry happens to straddle a DST transition for either zone —
 * an acceptable approximation for a settings field, not a scheduling engine.
 */
export function localTimeToEastern(hhmm: string): string {
  const [hours, minutes] = hhmm.split(':').map(Number);
  const totalMinutes = hours * 60 + minutes
    - utcOffsetMinutes(Intl.DateTimeFormat().resolvedOptions().timeZone)
    + utcOffsetMinutes(EASTERN_TIME_ZONE);
  return minutesToHhMm(totalMinutes);
}

/** Inverse of {@link localTimeToEastern}: US/Eastern "HH:mm" to local "HH:mm". */
export function easternTimeToLocal(hhmm: string): string {
  const [hours, minutes] = hhmm.split(':').map(Number);
  const totalMinutes = hours * 60 + minutes
    - utcOffsetMinutes(EASTERN_TIME_ZONE)
    + utcOffsetMinutes(Intl.DateTimeFormat().resolvedOptions().timeZone);
  return minutesToHhMm(totalMinutes);
}

function minutesToHhMm(totalMinutes: number): string {
  const normalized = ((totalMinutes % 1440) + 1440) % 1440;
  const hours = Math.floor(normalized / 60);
  const minutes = normalized % 60;
  return `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}`;
}

/** Abbreviated local time zone label, e.g. "EDT", "PST" — for UI hints. */
export function localTimeZoneAbbreviation(): string {
  const parts = new Intl.DateTimeFormat('en-US', { timeZoneName: 'short' }).formatToParts(new Date());
  return parts.find((p) => p.type === 'timeZoneName')?.value ?? '';
}

/**
 * Render an 18-hole handicap index plus its 9-hole equivalent in compact form,
 * e.g. "18.4 / 9.2". Returns "—" when no index is set. The 9-hole index is
 * computed as index / 2 to match the domain model's NineHoleHandicapIndex.
 */
export function formatHandicapPair(index18: number | null | undefined): string {
  if (index18 === null || index18 === undefined) return '—';
  return `${index18.toFixed(1)} / ${(index18 / 2).toFixed(1)}`;
}

/** Tooltip text for the formatted pair — explains what the slash means. */
export const HANDICAP_PAIR_TOOLTIP = '18-hole / 9-hole handicap index';
