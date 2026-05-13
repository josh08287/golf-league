import { useState, useMemo, useCallback } from 'react';
import { useParams, Link } from 'react-router-dom';
import { ArrowLeft, ChevronLeft, ChevronRight, Flag, Save, CheckCircle } from 'lucide-react';
import {
  useTeeTimeGroupScorecard,
  useSubmitTeeTimeGroupScores,
} from '@/hooks/useTeeTimeScoreEntry';
import { PageHeader } from '@/components/ui/PageHeader';
import { Button } from '@/components/ui/Button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/Card';
import { Spinner } from '@/components/ui/Spinner';
import { ErrorMessage } from '@/components/ui/ErrorMessage';
import { Badge } from '@/components/ui/Badge';
import { formatHandicapPair } from '@/lib/utils';
import type { TeeTimePlayerScore, TeeTimeHoleInfo, PlayerScoreInput } from '@/types/api';

// Helper to calculate stableford points
function calculateStablefordPoints(par: number, netStrokes: number): number {
  return Math.max(0, Math.min(6, par + 2 - netStrokes));
}

// Helper to calculate handicap strokes on a hole
function calculateHandicapStrokes(courseHandicap: number, strokeIndex: number): number {
  const base = Math.floor(courseHandicap / 18);
  const extra = courseHandicap % 18;
  return base + (strokeIndex <= extra ? 1 : 0);
}

// Helper to calculate net strokes and cap at max
function calculateNetStrokes(
  grossStrokes: number,
  par: number,
  handicapStrokes: number
): { netStrokes: number; isMaxScore: boolean } {
  const maxGross = par + 2 + handicapStrokes;
  const cappedGross = Math.min(grossStrokes, maxGross);
  return {
    netStrokes: cappedGross - handicapStrokes,
    isMaxScore: grossStrokes >= maxGross,
  };
}

interface ScoreInputProps {
  label: string;
  value: number | '';
  onChange: (value: number | '') => void;
  disabled?: boolean;
  maxScore?: number;
}

function ScoreInput({ label, value, onChange, disabled, maxScore }: ScoreInputProps) {
  return (
    <div className="flex flex-col items-center gap-1">
      <label className="text-xs font-medium text-gray-500">{label}</label>
      <input
        type="number"
        min={1}
        max={12}
        value={value === '' ? '' : value}
        disabled={disabled}
        onChange={(e) => {
          const v = e.target.value;
          if (v === '') {
            onChange('');
          } else {
            const n = parseInt(v, 10);
            if (!isNaN(n) && n >= 1 && n <= 12) {
              onChange(n);
            }
          }
        }}
        className="h-12 w-16 rounded-lg border border-gray-300 text-center text-lg font-semibold focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20] disabled:bg-gray-100 disabled:text-gray-400"
      />
      {maxScore && value !== '' && value > maxScore && (
        <span className="text-[10px] text-amber-600">Max: {maxScore}</span>
      )}
    </div>
  );
}

interface HoleViewProps {
  hole: TeeTimeHoleInfo;
  players: TeeTimePlayerScore[];
  scores: Record<number, Record<number, number | ''>>; // playerId -> holeNumber -> score
  onScoreChange: (playerId: number, value: number | '') => void;
  canEdit: boolean;
}

function HoleView({ hole, players, scores, onScoreChange, canEdit }: HoleViewProps) {
  return (
    <div className="space-y-4">
      <div className="rounded-lg bg-primary-50 px-4 py-3">
        <div className="flex items-center justify-between">
          <div>
            <h3 className="text-lg font-bold text-primary-900">
              Hole {hole.holeNumber}
            </h3>
            <p className="text-sm text-primary-700">
              Par {hole.par} · Stroke Index {hole.strokeIndex}
            </p>
          </div>
          <div className="text-right">
            <span className="text-2xl font-bold text-primary-900">{hole.par}</span>
            <p className="text-xs text-primary-600">Par</p>
          </div>
        </div>
      </div>

      <div className="space-y-3">
        {players.map((player) => {
          const playerScore = scores[player.playerId]?.[hole.holeNumber] ?? '';
          const maxScore = hole.par + 2 + calculateHandicapStrokes(player.courseHandicap, hole.strokeIndex);
          const isSkipped = player.skippedWeek;

          return (
            <Card key={player.playerId} className={isSkipped ? 'opacity-50' : ''}>
              <CardContent className="p-4">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="font-semibold text-gray-900">
                      {player.playerName}
                      {isSkipped && (
                        <span className="ml-2 text-xs text-gray-500">(Skipped)</span>
                      )}
                    </p>
                    <p className="text-xs text-gray-500">
                      HCP {formatHandicapPair(player.handicapIndex)} · CH {player.courseHandicap}
                    </p>
                  </div>
                  <ScoreInput
                    label="Gross"
                    value={isSkipped ? '' : playerScore}
                    onChange={(v) => onScoreChange(player.playerId, v)}
                    disabled={!canEdit || isSkipped}
                    maxScore={maxScore}
                  />
                </div>
              </CardContent>
            </Card>
          );
        })}
      </div>
    </div>
  );
}

interface ScoreSummaryProps {
  players: TeeTimePlayerScore[];
  holes: TeeTimeHoleInfo[];
  scores: Record<number, Record<number, number | ''>>;
  onSubmit: () => void;
  isSubmitting: boolean;
  canEdit: boolean;
}

function ScoreSummary({ players, holes, scores, onSubmit, isSubmitting, canEdit }: ScoreSummaryProps) {
  const playerSummaries = useMemo(() => {
    return players.map((player) => {
      let totalGross = 0;
      let totalNet = 0;
      let totalGrossPoints = 0;
      let totalNetPoints = 0;
      let holesEntered = 0;

      if (!player.skippedWeek) {
        holes.forEach((hole) => {
          const gross = scores[player.playerId]?.[hole.holeNumber];
          if (gross !== '' && gross !== undefined && gross !== null) {
            holesEntered++;
            totalGross += gross;

            const hcpStrokes = calculateHandicapStrokes(player.courseHandicap, hole.strokeIndex);
            const { netStrokes } = calculateNetStrokes(gross, hole.par, hcpStrokes);

            totalNet += netStrokes;
            totalGrossPoints += calculateStablefordPoints(hole.par, gross);
            totalNetPoints += calculateStablefordPoints(hole.par, netStrokes);
          }
        });
      }

      return {
        player,
        totalGross: player.skippedWeek ? null : totalGross,
        totalNet: player.skippedWeek ? null : totalNet,
        totalGrossPoints: player.skippedWeek ? 0 : totalGrossPoints,
        totalNetPoints: player.skippedWeek ? 0 : totalNetPoints,
        holesEntered,
        isComplete: player.skippedWeek || holesEntered === holes.length,
      };
    });
  }, [players, holes, scores]);

  const allComplete = playerSummaries.every((s) => s.isComplete);

  return (
    <div className="space-y-6">
      <div className="rounded-lg bg-green-50 px-4 py-3">
        <div className="flex items-center gap-2">
          <Flag className="h-5 w-5 text-green-600" />
          <h3 className="text-lg font-bold text-green-900">Round Complete!</h3>
        </div>
        <p className="text-sm text-green-700">
          Review the scores below and submit for admin approval.
        </p>
      </div>

      <div className="space-y-3">
        {playerSummaries.map((summary) => (
          <Card key={summary.player.playerId}>
            <CardHeader className="pb-2">
              <div className="flex items-center justify-between">
                <CardTitle className="text-base">{summary.player.playerName}</CardTitle>
                {summary.isComplete ? (
                  <Badge variant="green" className="flex items-center gap-1">
                    <CheckCircle className="h-3 w-3" />
                    Complete
                  </Badge>
                ) : (
                  <Badge variant="amber">{summary.holesEntered}/{holes.length} holes</Badge>
                )}
              </div>
              <p className="text-xs text-gray-500">
                HCP {formatHandicapPair(summary.player.handicapIndex)} · CH {summary.player.courseHandicap}
              </p>
            </CardHeader>
            <CardContent>
              {summary.player.skippedWeek ? (
                <p className="text-sm text-gray-500 italic">Skipped this round</p>
              ) : (
                <div className="grid grid-cols-4 gap-4 text-center">
                  <div>
                    <p className="text-2xl font-bold text-gray-900">{summary.totalGross ?? '—'}</p>
                    <p className="text-xs text-gray-500">Gross</p>
                  </div>
                  <div>
                    <p className="text-2xl font-bold text-gray-900">{summary.totalNet ?? '—'}</p>
                    <p className="text-xs text-gray-500">Net</p>
                  </div>
                  <div>
                    <p className="text-2xl font-bold text-blue-600">{summary.totalGrossPoints}</p>
                    <p className="text-xs text-gray-500">Gross Pts</p>
                  </div>
                  <div>
                    <p className="text-2xl font-bold text-green-600">{summary.totalNetPoints}</p>
                    <p className="text-xs text-gray-500">Net Pts</p>
                  </div>
                </div>
              )}
            </CardContent>
          </Card>
        ))}
      </div>

      {canEdit && (
        <div className="flex flex-col gap-3">
          {!allComplete && (
            <p className="text-sm text-amber-600 text-center">
              Please enter scores for all players and all holes before submitting.
            </p>
          )}
          <Button
            variant="primary"
            size="lg"
            onClick={onSubmit}
            disabled={isSubmitting || !allComplete}
            className="w-full"
          >
            {isSubmitting ? (
              <>
                <Spinner className="mr-2 h-4 w-4" />
                Submitting...
              </>
            ) : (
              <>
                <Save className="mr-2 h-4 w-4" />
                Submit Scores for Admin Review
              </>
            )}
          </Button>
          <p className="text-xs text-gray-500 text-center">
            An admin will review and finalize the round. You won&apos;t be able to edit these scores after submission.
          </p>
        </div>
      )}
    </div>
  );
}

export function TeeTimeScoreEntryPage() {
  const { teeTimeId } = useParams<{ teeTimeId: string }>();
  const teeTimeIdNum = parseInt(teeTimeId ?? '0', 10);

  const { data: scorecard, isLoading, error } = useTeeTimeGroupScorecard(teeTimeIdNum);
  const submitScores = useSubmitTeeTimeGroupScores(teeTimeIdNum);

  const [currentHoleIndex, setCurrentHoleIndex] = useState(0);
  const [scores, setScores] = useState<Record<number, Record<number, number | ''>>>({});
  const [showSummary, setShowSummary] = useState(false);
  const [submitSuccess, setSubmitSuccess] = useState(false);

  const players = scorecard?.players ?? [];
  const holes = scorecard?.holes ?? [];
  const canEdit = scorecard?.roundStatus === 'Scheduled' || scorecard?.roundStatus === 'InProgress';

  // Initialize scores from existing data
  useMemo(() => {
    if (!scorecard) return;

    const initialScores: Record<number, Record<number, number | ''>> = {};
    scorecard.players.forEach((player) => {
      initialScores[player.playerId] = {};
      player.holeScores.forEach((holeScore) => {
        if (holeScore.grossStrokes != null) {
          initialScores[player.playerId][holeScore.holeNumber] = holeScore.grossStrokes;
        }
      });
    });
    setScores(initialScores);
  }, [scorecard]);

  const handleScoreChange = useCallback((playerId: number, holeNumber: number, value: number | '') => {
    setScores((prev) => ({
      ...prev,
      [playerId]: {
        ...prev[playerId],
        [holeNumber]: value,
      },
    }));
  }, []);

  const handleNext = () => {
    if (currentHoleIndex < holes.length - 1) {
      setCurrentHoleIndex((prev) => prev + 1);
    } else {
      setShowSummary(true);
    }
  };

  const handlePrev = () => {
    if (showSummary) {
      setShowSummary(false);
    } else if (currentHoleIndex > 0) {
      setCurrentHoleIndex((prev) => prev - 1);
    }
  };

  const handleSubmit = async () => {
    const playerScores: PlayerScoreInput[] = players
      .filter((p) => !p.skippedWeek)
      .map((player) => ({
        playerId: player.playerId,
        holeScores: holes.map((hole) => ({
          holeNumber: hole.holeNumber,
          grossStrokes: scores[player.playerId]?.[hole.holeNumber] as number || 0,
        })),
      }));

    try {
      await submitScores.mutateAsync(playerScores);
      setSubmitSuccess(true);
    } catch {
      // Error handled by mutation
    }
  };

  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Spinner />
      </div>
    );
  }

  if (error || !scorecard) {
    return <ErrorMessage message="Could not load tee time scorecard." />;
  }

  if (submitSuccess) {
    return (
      <div className="space-y-6">
        <div className="rounded-lg border border-green-200 bg-green-50 px-6 py-8 text-center">
          <CheckCircle className="mx-auto h-12 w-12 text-green-600" />
          <h2 className="mt-4 text-xl font-bold text-green-900">Scores Submitted!</h2>
          <p className="mt-2 text-green-700">
            {submitScores.data?.message || "Your scores have been submitted for admin review."}
          </p>
          <div className="mt-6">
            <Button variant="primary" asChild>
              <Link to="/">Return Home</Link>
            </Button>
          </div>
        </div>
      </div>
    );
  }

  const currentHole = holes[currentHoleIndex];

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="sm" asChild className="-ml-2">
          <Link to="/">
            <ArrowLeft className="h-4 w-4 mr-1" />
            Home
          </Link>
        </Button>
      </div>

      <PageHeader
        title="Enter Group Scores"
        subtitle={`${scorecard.courseName} · ${scorecard.nineHoleSide} 9 · Tee Time ${scorecard.scheduledTimeFormatted}`}
      >
        <Badge variant={canEdit ? 'amber' : 'green'}>
          {scorecard.roundStatus}
        </Badge>
      </PageHeader>

      {submitScores.isError && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          Failed to submit scores. Please try again.
        </div>
      )}

      {/* Progress indicator */}
      {!showSummary && (
        <div className="flex items-center justify-between">
          <Button
            variant="outline"
            size="sm"
            onClick={handlePrev}
            disabled={currentHoleIndex === 0}
          >
            <ChevronLeft className="h-4 w-4 mr-1" />
            Prev
          </Button>
          <div className="flex gap-1">
            {holes.map((hole, idx) => (
              <button
                key={hole.holeNumber}
                onClick={() => setCurrentHoleIndex(idx)}
                className={`h-2 w-2 rounded-full transition-colors ${
                  idx === currentHoleIndex
                    ? 'bg-[#1B5E20]'
                    : idx < currentHoleIndex
                    ? 'bg-gray-400'
                    : 'bg-gray-200'
                }`}
              />
            ))}
          </div>
          <Button
            variant="outline"
            size="sm"
            onClick={handleNext}
          >
            {currentHoleIndex === holes.length - 1 ? 'Review' : 'Next'}
            {currentHoleIndex < holes.length - 1 && <ChevronRight className="h-4 w-4 ml-1" />}
          </Button>
        </div>
      )}

      {/* Hole number indicator */}
      {!showSummary && (
        <p className="text-center text-sm text-gray-500">
          Hole {currentHoleIndex + 1} of {holes.length}
        </p>
      )}

      {/* Main content */}
      {showSummary ? (
        <ScoreSummary
          players={players}
          holes={holes}
          scores={scores}
          onSubmit={handleSubmit}
          isSubmitting={submitScores.isPending}
          canEdit={canEdit}
        />
      ) : currentHole ? (
        <HoleView
          hole={currentHole}
          players={players}
          scores={scores}
          onScoreChange={(playerId, value) =>
            handleScoreChange(playerId, currentHole.holeNumber, value)
          }
          canEdit={canEdit}
        />
      ) : null}

      {/* Navigation buttons for mobile */}
      {!showSummary && (
        <div className="flex justify-between pt-4">
          <Button
            variant="outline"
            onClick={handlePrev}
            disabled={currentHoleIndex === 0}
          >
            <ChevronLeft className="h-4 w-4 mr-1" />
            Previous Hole
          </Button>
          <Button
            variant={currentHoleIndex === holes.length - 1 ? 'primary' : 'outline'}
            onClick={handleNext}
          >
            {currentHoleIndex === holes.length - 1 ? (
              <>
                Review & Submit
                <Flag className="h-4 w-4 ml-1" />
              </>
            ) : (
              <>
                Next Hole
                <ChevronRight className="h-4 w-4 ml-1" />
              </>
            )}
          </Button>
        </div>
      )}
    </div>
  );
}
