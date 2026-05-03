import { useEffect, useRef, useState, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { useRound } from '../../hooks/useRounds';
import { useSubmitHoleScores } from '../../hooks/admin/useRoundMutations';
import { PageHeader } from '../../components/ui/PageHeader';
import { Button } from '../../components/ui/Button';
import { Spinner } from '../../components/ui/Spinner';
import { ErrorMessage } from '../../components/ui/ErrorMessage';
import { ArrowLeft, Save } from 'lucide-react';
import { api } from '../../lib/api';
import type { CourseDetail, Participant } from '../../types/api';

const HOLES = Array.from({ length: 18 }, (_, i) => i + 1);

function computeStablefordPoints(gross: number, par: number, handicapStrokes: number): number {
  return Math.max(0, par + 2 - (gross - handicapStrokes));
}

function handicapStrokesForHole(playerCourseHandicap: number, strokeIndex: number): number {
  const baseStrokes = Math.floor(playerCourseHandicap / 18);
  const extra = playerCourseHandicap % 18;
  return baseStrokes + (strokeIndex <= extra ? 1 : 0);
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
      max={12}
      value={value === '' ? '' : value}
      readOnly={readonly}
      onChange={(e) => {
        const v = e.target.value;
        if (v === '') {
          onChange('');
        } else {
          const n = parseInt(v, 10);
          if (!isNaN(n) && n >= 1 && n <= 12) onChange(n);
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

interface StablefordRowProps {
  scores: Record<number, number | ''>;
  courseHandicap: number;
  strokeIndexes: Record<number, number>;
  pars: Record<number, number>;
}

function StablefordRow({ scores, courseHandicap, strokeIndexes, pars }: StablefordRowProps) {
  const points = HOLES.map((h) => {
    const gross = scores[h];
    if (gross === '' || gross === undefined) return null;
    const si = strokeIndexes[h] ?? h;
    const par = pars[h] ?? 4;
    const hStrokes = handicapStrokesForHole(courseHandicap, si);
    return computeStablefordPoints(gross, par, hStrokes);
  });

  const total = points.reduce<number>((sum, p) => sum + (p ?? 0), 0);

  return (
    <tr className="bg-green-50">
      <td className="sticky left-0 z-10 bg-green-50 px-3 py-1.5 text-xs font-medium text-[#1B5E20]">
        Stableford
      </td>
      {HOLES.map((h, i) => (
        <td key={h} className="px-1 py-1.5 text-center text-xs font-semibold text-[#1B5E20]">
          {points[i] ?? '—'}
        </td>
      ))}
      <td className="px-3 py-1.5 text-center text-xs font-bold text-[#1B5E20]">{total}</td>
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

  const submitScores = useSubmitHoleScores(roundId);

  const [scores, setScores] = useState<ScoreGrid>({});
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitSuccess, setSubmitSuccess] = useState(false);

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
      if (nextHole < 18) {
        cellRefs.current[playerIdx]?.[nextHole]?.focus();
      } else if (playerIdx + 1 < participants.length) {
        cellRefs.current[playerIdx + 1]?.[0]?.focus();
      }
    }
  }

  async function handleSubmitAll() {
    for (const p of participants) {
      for (const h of HOLES) {
        const v = scores[String(p.playerId)]?.[h];
        if (v === '' || v === undefined) {
          setSubmitError(`Missing score for ${p.playerName} on hole ${h}.`);
          return;
        }
      }
    }

    setSubmitting(true);
    setSubmitError(null);

    try {
      for (const p of participants) {
        await submitScores.mutateAsync({
          playerId: p.playerId,
          scores: HOLES.map((h) => ({
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

  const isFinalized = round.status === 'Finalized';

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
          subtitle={`${round.courseName} — ${new Date(round.scheduledDate).toLocaleDateString()}`}
        />
      </div>

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

      <div className="overflow-x-auto rounded-xl border border-gray-200 bg-white shadow-sm">
        <table className="min-w-full border-collapse text-sm">
          <thead>
            <tr className="border-b border-gray-200 bg-gray-50">
              <th className="sticky left-0 z-10 bg-gray-50 px-3 py-3 text-left text-xs font-semibold uppercase tracking-wider text-gray-500">
                Player
              </th>
              {HOLES.map((h) => (
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
              {HOLES.map((h) => (
                <td key={h} className="px-1 py-1.5 text-center text-xs text-gray-400">
                  {pars[h] ?? 4}
                </td>
              ))}
              <td className="px-3 py-1.5 text-center text-xs text-gray-400">
                {HOLES.reduce((sum, h) => sum + (pars[h] ?? 4), 0)}
              </td>
            </tr>
          </thead>

          <tbody className="divide-y divide-gray-100">
            {participants.map((participant, pi) => {
              const playerScores = scores[String(participant.playerId)] ?? {};
              const grossTotal = HOLES.reduce<number>((sum, h) => {
                const v = playerScores[h];
                return sum + (typeof v === 'number' ? v : 0);
              }, 0);
              const courseHandicap = participant.courseHandicap;

              return (
                <>
                  <tr key={`${participant.playerId}-scores`} className="hover:bg-gray-50/50">
                    <td className="sticky left-0 z-10 bg-white px-3 py-2">
                      <div className="font-medium text-gray-900">{participant.playerName}</div>
                      <div className="text-xs text-gray-400">HCP {participant.handicapAtTime}</div>
                    </td>
                    {HOLES.map((h, hi) => (
                      <td key={h} className="px-1 py-2">
                        <ScoreCell
                          value={playerScores[h] ?? ''}
                          readonly={isFinalized}
                          onChange={(v) => handleScoreChange(String(participant.playerId), h, v)}
                          onKeyDown={(e) => handleKeyDown(e, pi, hi)}
                          inputRef={(el) => {
                            if (!cellRefs.current[pi]) cellRefs.current[pi] = [];
                            cellRefs.current[pi][hi] = el;
                          }}
                        />
                      </td>
                    ))}
                    <td className="px-3 py-2 text-center font-semibold text-gray-700">
                      {grossTotal > 0 ? grossTotal : '—'}
                    </td>
                  </tr>

                  <StablefordRow
                    key={`${participant.playerId}-stableford`}
                    scores={playerScores}
                    courseHandicap={courseHandicap}
                    strokeIndexes={strokeIndexes}
                    pars={pars}
                  />
                </>
              );
            })}
          </tbody>
        </table>
      </div>

      {!isFinalized && (
        <div className="flex items-center justify-end gap-3">
          <p className="text-sm text-gray-500">
            Auto-saved to local draft. Submit when all 18 holes are complete.
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
