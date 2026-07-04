import { Fragment, useEffect, useRef, useState, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { formatHandicapPair, HANDICAP_PAIR_TOOLTIP } from '../../lib/utils';
import { useRound, useRoundScorecards } from '../../hooks/useRounds';
import { useSubmitHoleScores, useSetParticipantSkipped } from '../../hooks/admin/useRoundMutations';
import { PageHeader } from '../../components/ui/PageHeader';
import { Button } from '../../components/ui/Button';
import { Spinner } from '../../components/ui/Spinner';
import { ErrorMessage } from '../../components/ui/ErrorMessage';
import { ArrowLeft, Save } from 'lucide-react';
import { api } from '../../lib/api';
import { normalizeNineHoleSide, isRoundFinalized } from '../../lib/enumUtils';
import type { CourseDetail, Participant } from '../../types/api';

function holesForSide(nineHoleSide: string | number): number[] {
  const side = normalizeNineHoleSide(nineHoleSide);
  return side === 'Back'
    ? Array.from({ length: 9 }, (_, i) => i + 10)
    : Array.from({ length: 9 }, (_, i) => i + 1);
}

function handicapStrokesForHole(courseHandicap: number, strokeIndex: number): number {
  const base = Math.floor(courseHandicap / 18);
  const extra = courseHandicap % 18;
  return base + (strokeIndex <= extra ? 1 : 0);
}

function clampNetScore(gross: number, par: number, handicapStrokes: number) {
  const maxGross = par + 2 + handicapStrokes;
  return Math.min(gross, maxGross);
}

function netStableford(gross: number, par: number, handicapStrokes: number): number {
  const cappedGross = clampNetScore(gross, par, handicapStrokes);
  const net = cappedGross - handicapStrokes;
  return Math.max(0, Math.min(6, par + 2 - net));
}

function grossStableford(gross: number, par: number): number {
  return Math.max(0, Math.min(6, par + 2 - gross));
}

type ScoreGrid = Record<string, Record<number, number | ''>>;

function buildDraftKey(roundId: string) {
  return `score_draft_${roundId}`;
}

interface ScoreCellProps {
  value: number | '';
  onChange: (value: number | '') => void;
  onKeyDown: (e: React.KeyboardEvent<HTMLInputElement>) => void;
  inputRef: (el: HTMLInputElement | null) => void;
  readonly: boolean;
}

function ScoreCell({ value, onChange, onKeyDown, inputRef, readonly }: ScoreCellProps) {
  return (
    <input
      ref={inputRef}
      type="number"
      min={1}
      value={value === '' ? '' : value}
      readOnly={readonly}
      onChange={(e) => {
        const v = e.target.value;
        if (v === '') {
          onChange('');
        } else {
          const n = parseInt(v, 10);
          if (!isNaN(n) && n >= 1) onChange(n);
        }
      }}
      onKeyDown={onKeyDown}
      className={[
        'h-9 w-14 rounded border text-center text-sm transition-colors',
        readonly
          ? 'cursor-default bg-gray-50 text-gray-400'
          : 'border-gray-300 bg-white focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20]',
      ].join(' ')}
    />
  );
}

interface DerivedRowProps {
  label: string;
  scores: Record<number, number | ''>;
  courseHandicap: number;
  strokeIndexes: Record<number, number>;
  pars: Record<number, number>;
  holes: number[];
  mode: 'gross' | 'net';
  rowClass: string;
}

function DerivedStablefordRow({
  label,
  scores,
  courseHandicap,
  strokeIndexes,
  pars,
  holes,
  mode,
  rowClass,
}: DerivedRowProps) {
  const points = holes.map((h) => {
    const gross = scores[h];
    if (gross === '' || gross === undefined) return null;
    const par = pars[h] ?? 4;
    if (mode === 'gross') return grossStableford(gross, par);
    const si = strokeIndexes[h] ?? h;
    const hStrokes = handicapStrokesForHole(courseHandicap, si);
    return netStableford(gross, par, hStrokes);
  });

  const total = points.reduce<number>((sum, p) => sum + (p ?? 0), 0);

  return (
    <tr className={rowClass}>
      <td className="sticky left-0 z-10 px-3 py-1.5 text-xs font-medium" style={{ background: 'inherit' }}>
        {label}
      </td>
      {holes.map((h, i) => (
        <td key={h} className="px-1 py-1.5 text-center text-xs font-semibold">
          {points[i] ?? '—'}
        </td>
      ))}
      <td className="px-3 py-1.5 text-center text-xs font-bold">{total}</td>
    </tr>
  );
}

export function ScoreEntryPage() {
  const { id: roundId = '' } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const { data: round, isLoading, error } = useRound(roundId);

  const { data: participantsData, isLoading: participantsLoading } = useQuery<Participant[]>({
    queryKey: ['rounds', roundId, 'participants'],
    queryFn: () => api.get(`/rounds/${roundId}/participants`).then((r) => r.data),
    enabled: Boolean(roundId),
  });
  const participants = participantsData ?? [];

  const { data: courseDetail } = useQuery<CourseDetail>({
    queryKey: ['course', round?.courseId],
    queryFn: () => api.get(`/courses/${round!.courseId}`).then((r) => r.data),
    enabled: Boolean(round?.courseId),
  });

  const { data: scorecardsData } = useRoundScorecards(roundId);

  const submitScores = useSubmitHoleScores(roundId);
  const setSkipped = useSetParticipantSkipped(roundId);

  const [scores, setScores] = useState<ScoreGrid>({});
  // Optimistic skip overlay: maps playerId → true (skip) | false (unskip).
  // Cleared once the corresponding mutation settles, since the invalidated
  // participants query then becomes the source of truth.
  const [localSkips, setLocalSkips] = useState<Record<number, boolean>>({});
  const [skipError, setSkipError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitSuccess, setSubmitSuccess] = useState(false);
  // True when the round already has server-side scores and the user has not yet
  // confirmed they intend to overwrite them.
  const [overwritePending, setOverwritePending] = useState(false);

  const cellRefs = useRef<Array<Array<HTMLInputElement | null>>>([]);

  useEffect(() => {
    const raw = localStorage.getItem(buildDraftKey(roundId));
    if (raw) {
      try {
        setScores(JSON.parse(raw) as ScoreGrid);
      } catch {
        // ignore corrupt draft
      }
    }
  }, [roundId]);

  // Seed (or backfill) the grid from server-side scorecards. Without this
  // a finalized round opened in a fresh browser shows empty cells because
  // there's no local draft. We never overwrite a value the user has typed
  // locally — only fill in cells that are still blank.
  useEffect(() => {
    const sheets = scorecardsData?.data;
    if (!sheets || sheets.length === 0) return;
    setScores((prev) => {
      const next = { ...prev };
      let changed = false;
      for (const sc of sheets) {
        const key = String(sc.playerId);
        const existing = next[key] ?? {};
        const merged: Record<number, number | ''> = { ...existing };
        for (const h of sc.holes) {
          if (merged[h.holeNumber] === undefined || merged[h.holeNumber] === '') {
            merged[h.holeNumber] = h.strokes;
            changed = true;
          }
        }
        if (changed) next[key] = merged;
      }
      return changed ? next : prev;
    });
  }, [scorecardsData]);

  useEffect(() => {
    if (Object.keys(scores).length > 0) {
      localStorage.setItem(buildDraftKey(roundId), JSON.stringify(scores));
    }
  }, [scores, roundId]);

  const handleScoreChange = useCallback((playerId: string, hole: number, value: number | '') => {
    setScores((prev) => ({
      ...prev,
      [playerId]: { ...(prev[playerId] ?? {}), [hole]: value },
    }));
  }, []);

  function handleKeyDown(
    e: React.KeyboardEvent<HTMLInputElement>,
    playerIdx: number,
    holeIdx: number
  ) {
    if (e.key === 'Tab') {
      e.preventDefault();
      const nextHole = holeIdx + 1;
      if (nextHole < holes.length) {
        cellRefs.current[playerIdx]?.[nextHole]?.focus();
      } else if (playerIdx + 1 < participants.length) {
        cellRefs.current[playerIdx + 1]?.[0]?.focus();
      }
    }
  }

  function resolveSkipped(p: Participant): boolean {
    return p.playerId in localSkips ? localSkips[p.playerId] : p.skippedWeek;
  }

  async function handleToggleSkipped(playerId: number, currentlySkipped: boolean) {
    const newSkipped = !currentlySkipped;
    setLocalSkips((prev) => ({ ...prev, [playerId]: newSkipped }));
    setSkipError(null);
    // Clear any draft scores for a player being marked as skipped.
    if (newSkipped) {
      setScores((prev) => {
        const next = { ...prev };
        delete next[String(playerId)];
        return next;
      });
    }
    try {
      await setSkipped.mutateAsync({ playerId, skipped: newSkipped });
      setLocalSkips((prev) => {
        const next = { ...prev };
        delete next[playerId];
        return next;
      });
    } catch {
      setSkipError('Failed to update skip status. Please try again.');
      setLocalSkips((prev) => {
        const next = { ...prev };
        delete next[playerId];
        return next;
      });
    }
  }

  async function handleSubmitAll() {
    // Validate scores for non-skipped players.
    for (const p of participants) {
      if (resolveSkipped(p)) continue;
      for (const h of holes) {
        const v = scores[String(p.playerId)]?.[h];
        if (v === '' || v === undefined) {
          setSubmitError(`Missing score for ${p.playerName} on hole ${h}.`);
          return;
        }
      }
    }

    // If the round already has scores on the server, require an explicit
    // overwrite confirmation before proceeding.
    const hasExistingScores = (scorecardsData?.data ?? []).some((sc) => sc.holes.length > 0);
    if (hasExistingScores && !overwritePending) {
      setOverwritePending(true);
      return;
    }

    setOverwritePending(false);
    setSubmitting(true);
    setSubmitError(null);

    try {
      // Submit scores for non-skipped players.
      for (const p of participants) {
        if (resolveSkipped(p)) continue;
        await submitScores.mutateAsync({
          playerId: p.playerId,
          scores: holes.map((h) => ({
            holeNumber: h,
            grossScore: scores[String(p.playerId)][h] as number,
          })),
        });
      }

      localStorage.removeItem(buildDraftKey(roundId));
      setSubmitSuccess(true);
    } catch {
      setSubmitError('Submission failed. Please try again.');
    } finally {
      setSubmitting(false);
    }
  }

  if (isLoading || participantsLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Spinner />
      </div>
    );
  }

  if (error || !round) {
    return <ErrorMessage message="Round not found." />;
  }

  const isFinalized = isRoundFinalized(round.status);
  const holes = holesForSide(round.nineHoleSide);

  const strokeIndexes: Record<number, number> = {};
  const pars: Record<number, number> = {};
  for (const hole of courseDetail?.holeDetails ?? []) {
    pars[hole.holeNumber] = hole.par;
    strokeIndexes[hole.holeNumber] = hole.strokeIndex;
  }

  cellRefs.current = participants.map((_, pi) => cellRefs.current[pi] ?? []);

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <button
          onClick={() => navigate('/admin/rounds')}
          className="text-gray-400 hover:text-gray-600"
        >
          <ArrowLeft className="h-5 w-5" />
        </button>
        <PageHeader
          title="Score Entry"
          subtitle={`${round.courseName} — ${new Date(round.scheduledDate).toLocaleDateString()} — ${round.nineHoleSide} 9 (Week ${round.weekNumber})`}
        />
      </div>

      {!isFinalized && (scorecardsData?.data ?? []).some((sc) => sc.holes.length > 0) && (
        <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
          <strong>Scores already recorded</strong> — this round has existing scores. Submitting will overwrite them.
          Confirm you are editing the correct round: <strong>Week {round.weekNumber} ({new Date(round.scheduledDate).toLocaleDateString(undefined, { timeZone: 'UTC' })})</strong>.
        </div>
      )}

      {submitSuccess && (
        <div className="rounded-lg border border-green-200 bg-green-50 px-4 py-3 text-sm text-green-800">
          All scores submitted successfully!
        </div>
      )}

      {submitError && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {submitError}
        </div>
      )}

      {skipError && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {skipError}
        </div>
      )}

      <div className="overflow-x-auto rounded-xl border border-gray-200 bg-white shadow-sm">
        <table className="min-w-full border-collapse text-sm">
          <thead>
            <tr className="border-b border-gray-200 bg-gray-50">
              <th className="sticky left-0 z-10 bg-gray-50 px-3 py-3 text-left text-xs font-semibold uppercase tracking-wider text-gray-500">
                Player
              </th>
              {holes.map((h) => (
                <th key={h} className="px-1 py-3 text-center text-xs font-semibold text-gray-500">
                  {h}
                </th>
              ))}
              <th className="px-3 py-3 text-center text-xs font-semibold text-gray-500">Total</th>
            </tr>

            <tr className="border-b border-gray-100 bg-gray-50/50">
              <td className="sticky left-0 z-10 bg-gray-50/50 px-3 py-1.5 text-xs font-medium text-gray-400">
                Par
              </td>
              {holes.map((h) => (
                <td key={h} className="px-1 py-1.5 text-center text-xs text-gray-400">
                  {pars[h] ?? 4}
                </td>
              ))}
              <td className="px-3 py-1.5 text-center text-xs text-gray-400">
                {holes.reduce((sum, h) => sum + (pars[h] ?? 4), 0)}
              </td>
            </tr>
          </thead>

          <tbody className="divide-y divide-gray-100">
            {participants.map((participant, pi) => {
              const playerScores = scores[String(participant.playerId)] ?? {};
              const grossTotal = holes.reduce<number>((sum, h) => {
                const v = playerScores[h];
                return sum + (typeof v === 'number' ? v : 0);
              }, 0);
              const courseHandicap = participant.courseHandicap;
              const skipped = resolveSkipped(participant);

              return (
                <Fragment key={participant.playerId}>
                  <tr className={skipped ? 'bg-gray-50' : 'hover:bg-gray-50/50'}>
                    <td
                      className="sticky left-0 z-10 px-3 py-2"
                      style={{ background: skipped ? '#F9FAFB' : 'white' }}
                    >
                      <div className="flex items-start justify-between gap-2">
                        <div>
                          <div className={`font-medium ${skipped ? 'text-gray-400 line-through' : 'text-gray-900'}`}>
                            {participant.playerName}
                          </div>
                          <div className="text-xs text-gray-400" title={HANDICAP_PAIR_TOOLTIP}>
                            HCP {formatHandicapPair(participant.handicapAtTime)} · CH {courseHandicap}
                          </div>
                        </div>
                        {!isFinalized && (
                          <label className="flex shrink-0 items-center gap-1 text-xs text-gray-500 cursor-pointer">
                            <input
                              type="checkbox"
                              checked={skipped}
                              onChange={() => handleToggleSkipped(participant.playerId, skipped)}
                              disabled={submitting || setSkipped.isPending}
                              className="h-3.5 w-3.5 rounded border-gray-300 text-[#1B5E20] focus:ring-[#1B5E20]"
                            />
                            Skip
                          </label>
                        )}
                      </div>
                      {skipped && (
                        <div className="mt-1 text-[11px] font-medium text-amber-700">
                          Skipped — 0 pts, no handicap impact
                        </div>
                      )}
                    </td>
                    {holes.map((h, hi) => (
                      <td key={h} className="px-1 py-2">
                        <ScoreCell
                          value={skipped ? '' : (playerScores[h] ?? '')}
                          readonly={isFinalized || skipped}
                          onChange={(v: number | '') => handleScoreChange(String(participant.playerId), h, v)}
                          onKeyDown={(e: React.KeyboardEvent<HTMLInputElement>) => handleKeyDown(e, pi, hi)}
                          inputRef={(el: HTMLInputElement | null) => {
                            if (!cellRefs.current[pi]) cellRefs.current[pi] = [];
                            cellRefs.current[pi][hi] = el;
                          }}
                        />
                      </td>
                    ))}
                    <td className="px-3 py-2 text-center font-semibold text-gray-700">
                      {skipped ? '—' : grossTotal > 0 ? grossTotal : '—'}
                    </td>
                  </tr>

                  {!skipped && (
                    <>
                      <DerivedStablefordRow
                        label="Gross Stableford"
                        scores={playerScores}
                        courseHandicap={courseHandicap}
                        strokeIndexes={strokeIndexes}
                        pars={pars}
                        holes={holes}
                        mode="gross"
                        rowClass="bg-blue-50/60 text-blue-800"
                      />
                      <DerivedStablefordRow
                        label="Net Stableford"
                        scores={playerScores}
                        courseHandicap={courseHandicap}
                        strokeIndexes={strokeIndexes}
                        pars={pars}
                        holes={holes}
                        mode="net"
                        rowClass="bg-green-50 text-[#1B5E20]"
                      />
                    </>
                  )}
                </Fragment>
              );
            })}
          </tbody>
        </table>
      </div>

      {!isFinalized && overwritePending && (
        <div className="rounded-lg border border-amber-300 bg-amber-50 px-4 py-3 text-sm text-amber-800">
          <p className="font-semibold">This round already has scores recorded.</p>
          <p className="mt-1">
            Submitting will permanently replace the existing scores for all players.
            Make sure you are on the correct round (Week {round.weekNumber} —{' '}
            {new Date(round.scheduledDate).toLocaleDateString(undefined, { timeZone: 'UTC' })}).
          </p>
          <div className="mt-3 flex gap-2">
            <Button variant="primary" onClick={handleSubmitAll} disabled={submitting}>
              <Save className="mr-1.5 h-4 w-4" />
              Yes, overwrite scores
            </Button>
            <Button variant="secondary" onClick={() => setOverwritePending(false)}>
              Cancel
            </Button>
          </div>
        </div>
      )}

      {!isFinalized && !overwritePending && (
        <div className="flex items-center justify-end gap-3">
          <p className="text-sm text-gray-500">
            Auto-saved to local draft. Submit when all {holes.length} holes are complete.
          </p>
          <Button variant="primary" onClick={handleSubmitAll} disabled={submitting}>
            <Save className="mr-1.5 h-4 w-4" />
            {submitting ? 'Submitting…' : 'Submit All Scores'}
          </Button>
        </div>
      )}
    </div>
  );
}
