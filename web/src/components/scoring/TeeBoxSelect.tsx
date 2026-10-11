import { teeBoxLabel } from '@/hooks/useTeeBoxes';
import { cn } from '@/lib/utils';
import type { TeeBoxOption } from '@/types/api';

interface TeeBoxSelectProps {
  teeBoxes: TeeBoxOption[];
  /** The participant's current tee for this round. */
  value: number;
  /** True when the tee was changed for this round (shows a reset link). */
  overridden: boolean;
  /** `null` reverts to the player's default tee. */
  onChange: (teeBoxId: number | null) => void;
  disabled?: boolean;
  className?: string;
  'aria-label'?: string;
}

/**
 * Per-player tee picker for score entry. Starts on the player's default tee
 * (set by the server when the round was created); changing it re-rates the
 * player's round and rescores any holes already entered.
 */
export function TeeBoxSelect({ teeBoxes, value, overridden, onChange, disabled, className, ...rest }: TeeBoxSelectProps) {
  if (teeBoxes.length === 0) return null;

  return (
    <span className={cn('inline-flex items-center gap-1.5', className)}>
      <select
        aria-label={rest['aria-label'] ?? 'Tee box'}
        value={value}
        disabled={disabled}
        onChange={(e) => onChange(Number(e.target.value))}
        onClick={(e) => e.stopPropagation()}
        className="max-w-[11rem] rounded border border-gray-300 bg-white px-1.5 py-0.5 text-xs text-gray-700 focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20] disabled:opacity-60"
      >
        {teeBoxes.map((t) => (
          <option key={t.id} value={t.id}>
            {teeBoxLabel(t)}
          </option>
        ))}
      </select>
      {overridden && (
        <button
          type="button"
          disabled={disabled}
          onClick={(e) => {
            e.stopPropagation();
            onChange(null);
          }}
          className="text-xs text-gray-500 underline hover:text-gray-700 disabled:opacity-60"
          title="Go back to this player's default tee"
        >
          reset
        </button>
      )}
    </span>
  );
}
