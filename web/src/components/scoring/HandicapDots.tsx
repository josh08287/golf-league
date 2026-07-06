// Standard scorecard convention: one dot per handicap stroke a player
// receives on a hole.
export function HandicapDots({ strokes }: { strokes: number }) {
  if (strokes <= 0) return null;
  return (
    <span className="tracking-tight text-primary-700" aria-label={`${strokes} handicap stroke${strokes === 1 ? '' : 's'}`}>
      {'•'.repeat(strokes)}
    </span>
  );
}
