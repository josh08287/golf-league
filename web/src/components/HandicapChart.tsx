import { useMemo } from 'react';
import type { HandicapHistoryEntry } from '@/types/api';

interface HandicapChartProps {
  history: HandicapHistoryEntry[];
}

/**
 * Inline-SVG line chart of handicap index over time. No external chart
 * library needed — kept simple on purpose so it stays in our bundle and
 * matches the rest of the public-facing UI.
 *
 * Domain: oldest entry on the left, newest on the right. Y axis is
 * inverted so a *lower* handicap (better golfer) appears higher on the
 * chart, matching how golfers think about improvement.
 */
export function HandicapChart({ history }: HandicapChartProps) {
  const chronological = useMemo(
    () =>
      [...history].sort(
        (a, b) => new Date(a.effectiveDate).getTime() - new Date(b.effectiveDate).getTime(),
      ),
    [history],
  );

  if (chronological.length < 2) {
    return (
      <p className="text-sm text-gray-500">
        Need at least two handicap entries to draw a trend.
      </p>
    );
  }

  const width = 720;
  const height = 220;
  const padding = { top: 16, right: 24, bottom: 32, left: 40 };
  const innerW = width - padding.left - padding.right;
  const innerH = height - padding.top - padding.bottom;

  const indices = chronological.map((h) => h.handicapIndex);
  const minIndex = Math.min(...indices);
  const maxIndex = Math.max(...indices);
  const yPad = Math.max((maxIndex - minIndex) * 0.1, 0.5);
  const yMin = minIndex - yPad;
  const yMax = maxIndex + yPad;

  const xFor = (i: number) =>
    padding.left + (chronological.length === 1 ? innerW / 2 : (i / (chronological.length - 1)) * innerW);
  // Inverted: lower handicap (better) plotted higher on the chart.
  const yFor = (v: number) =>
    padding.top + ((yMax - v) / (yMax - yMin)) * innerH;

  const linePath = chronological
    .map((h, i) => `${i === 0 ? 'M' : 'L'} ${xFor(i).toFixed(1)} ${yFor(h.handicapIndex).toFixed(1)}`)
    .join(' ');

  // Five horizontal grid lines at evenly spaced handicap values
  const gridSteps = 4;
  const gridValues = Array.from({ length: gridSteps + 1 }, (_, i) =>
    yMin + ((yMax - yMin) * i) / gridSteps,
  );

  // Show date labels on the first, middle, and last points
  const dateLabelIndexes = chronological.length <= 3
    ? chronological.map((_, i) => i)
    : [0, Math.floor(chronological.length / 2), chronological.length - 1];

  function formatDate(iso: string) {
    const d = new Date(iso);
    return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: '2-digit' });
  }

  return (
    <div className="rounded-lg border border-gray-200 bg-white p-3">
      <svg
        viewBox={`0 0 ${width} ${height}`}
        className="w-full h-auto"
        role="img"
        aria-label="Handicap index over time"
      >
        {/* Grid lines + Y axis labels */}
        {gridValues.map((v, i) => {
          const y = yFor(v);
          return (
            <g key={i}>
              <line
                x1={padding.left}
                x2={width - padding.right}
                y1={y}
                y2={y}
                stroke="#E5E7EB"
                strokeWidth={1}
              />
              <text
                x={padding.left - 6}
                y={y + 3}
                textAnchor="end"
                className="fill-gray-500"
                style={{ fontSize: 10 }}
              >
                {v.toFixed(1)}
              </text>
            </g>
          );
        })}

        {/* X axis date labels */}
        {dateLabelIndexes.map((i) => (
          <text
            key={`xl-${i}`}
            x={xFor(i)}
            y={height - padding.bottom + 14}
            textAnchor="middle"
            className="fill-gray-500"
            style={{ fontSize: 10 }}
          >
            {formatDate(chronological[i].effectiveDate)}
          </text>
        ))}

        {/* Line */}
        <path d={linePath} fill="none" stroke="#1B5E20" strokeWidth={2} />

        {/* Points with tooltips */}
        {chronological.map((h, i) => (
          <g key={`p-${i}`}>
            <circle
              cx={xFor(i)}
              cy={yFor(h.handicapIndex)}
              r={3.5}
              fill="#1B5E20"
            >
              <title>{`${formatDate(h.effectiveDate)} — ${h.handicapIndex.toFixed(1)} (${h.source})`}</title>
            </circle>
          </g>
        ))}
      </svg>
    </div>
  );
}
