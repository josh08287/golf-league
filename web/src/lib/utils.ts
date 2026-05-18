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
