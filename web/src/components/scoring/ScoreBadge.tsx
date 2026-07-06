import { cn } from '@/lib/utils';

// Standard scorecard convention: circle the score for birdie or better
// (double circle for eagle+), square it for bogey or worse (double
// square for double-bogey+). Par is shown plain.
function scoreShapeClass(strokes: number, par: number): string {
  const diff = strokes - par;
  if (diff <= -2) return 'rounded-full ring-2 ring-inset ring-offset-1 ring-primary-700 border-2 border-primary-700';
  if (diff === -1) return 'rounded-full border-2 border-primary-700';
  if (diff === 1) return 'border-2 border-gray-700';
  if (diff >= 2) return 'ring-2 ring-inset ring-offset-1 ring-gray-700 border-2 border-gray-700';
  return 'border-2 border-transparent';
}

interface ScoreBadgeProps {
  strokes: number;
  par: number;
  handicapStrokes: number;
  /** Highlights the badge for a standout result (e.g. a skins winner). */
  highlight?: boolean;
}

export function ScoreBadge({ strokes, par, handicapStrokes, highlight }: ScoreBadgeProps) {
  return (
    <span className="relative inline-flex h-7 w-7 items-center justify-center">
      <span
        className={cn(
          'flex h-6 w-6 items-center justify-center rounded text-sm leading-none',
          highlight ? 'bg-amber-100 ring-offset-amber-100' : 'ring-offset-white',
          scoreShapeClass(strokes, par),
        )}
      >
        {strokes}
      </span>
      {handicapStrokes > 0 && (
        <span
          className="absolute -top-0.5 -right-0.5 flex items-center gap-px leading-none text-primary-700"
          aria-label={`${handicapStrokes} handicap stroke${handicapStrokes === 1 ? '' : 's'}`}
        >
          {Array.from({ length: handicapStrokes }).map((_, i) => (
            <span key={i} className="block h-[3px] w-[3px] rounded-full bg-primary-700" />
          ))}
        </span>
      )}
    </span>
  );
}
