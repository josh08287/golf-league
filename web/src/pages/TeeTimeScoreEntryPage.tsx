import { useState, useMemo, useCallback, useEffect, useRef } from 'react';
import { useParams, Link } from 'react-router-dom';
import { ArrowLeft, ChevronLeft, ChevronRight, Flag, Save, CheckCircle, BarChart2, Target, AlertTriangle, Repeat, Camera } from 'lucide-react';
import {
  useTeeTimeGroupScorecard,
  useSubmitTeeTimeGroupScores,
  useSaveTeeTimeHoleScores,
  useSetTeeTimeParticipantSkipped,
  useSetTeeTimeTournamentCtp,
  useSetTeeTimeTournamentLongestDrive,
  useParseScorecardImage,
} from '@/hooks/useTeeTimeScoreEntry';
import { useRoundTeeTimes, useSwitchTeeTimeParticipant } from '@/hooks/useTeeTimes';
import { useRoundClosestToPin, useSetRoundClosestToPin } from '@/hooks/useClosestToPin';
import { useFeatureFlagStates } from '@/hooks/admin/useFeatureFlags';
import { useAuthStore } from '@/store/authStore';
import { FEATURE_FLAG_KEYS } from '@/types/api';
import { PageHeader } from '@/components/ui/PageHeader';
import { Button } from '@/components/ui/Button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/Card';
import { Spinner } from '@/components/ui/Spinner';
import { ErrorMessage } from '@/components/ui/ErrorMessage';
import { Badge } from '@/components/ui/Badge';
import { formatHandicapPair } from '@/lib/utils';
import { HandicapDots } from '@/components/scoring/HandicapDots';
import type {
  TeeTimePlayerScore,
  TeeTimeHoleInfo,
  PlayerScoreInput,
  TeeTimeGroupScorecard,
  HoleScoreConflict,
  ConfirmedOverwrite,
  TeeTimeParticipant,
  ScorecardOcrResult,
} from '@/types/api';

// Helper to calculate stableford points
function calculateStablefordPoints(par: number, netStrokes: number): number {
  return Math.max(0, Math.min(6, par + 2 - netStrokes));
}

// Helper to calculate handicap strokes on a hole.
// strokeIndex is the normalized 1–9 rank within the nine (sent by the scorecard API).
function calculateHandicapStrokes(courseHandicap: number, strokeIndex: number): number {
  return Math.floor(courseHandicap / 9) + (strokeIndex <= courseHandicap % 9 ? 1 : 0);
}

// Helper to calculate net strokes and cap at max
function calculateNetStrokes(
  grossStrokes: number,
  par: number,
  handicapStrokes: number
): { netStrokes: number; isMaxScore: boolean } {
  const maxGross = par + 2 + handicapStrokes;
  // Net Stableford uses adjusted gross (capped at net double bogey); gross is stored as-entered.
  const adjustedGross = Math.min(grossStrokes, maxGross);
  return {
    netStrokes: adjustedGross - handicapStrokes,
    isMaxScore: grossStrokes >= maxGross,
  };
}

interface ScoreInputProps {
  label: string;
  value: number | '';
  onChange: (value: number | '') => void;
  disabled?: boolean;
}

function ScoreInput({ label, value, onChange, disabled }: ScoreInputProps) {
  return (
    <div className="flex flex-col items-center gap-1">
      <label className="text-xs font-medium text-gray-500">{label}</label>
      <input
        type="number"
        min={1}
        value={value === '' ? '' : value}
        disabled={disabled}
        onChange={(e) => {
          const v = e.target.value;
          if (v === '') {
            onChange('');
          } else {
            const n = parseInt(v, 10);
            if (!isNaN(n) && n >= 1) {
              onChange(n);
            }
          }
        }}
        className="h-12 w-16 rounded-lg border border-gray-300 text-center text-lg font-semibold focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20] disabled:bg-gray-100 disabled:text-gray-400"
      />
    </div>
  );
}

interface HoleData {
  putts: number | '';
  firstPuttDistanceFeet: number | '';
  fairwayHit: boolean | null;
}

interface SwitchPlayerPickerProps {
  roundId: number;
  currentTeeTimeId: number;
  onClose: () => void;
  onSwitched: (otherPlayerName: string) => void;
}

/**
 * Lists every other participant in the round who is in a different
 * foursome, so the caller can swap tee-time groups with them. A straight
 * swap is always capacity-safe (each side takes the other's seat), so there
 * can never be two of the same player in one foursome as a result.
 */
function SwitchPlayerPicker({ roundId, currentTeeTimeId, onClose, onSwitched }: SwitchPlayerPickerProps) {
  const { data: schedule, isLoading } = useRoundTeeTimes(roundId);
  const switchParticipant = useSwitchTeeTimeParticipant();

  const otherGroupPlayers: { participant: TeeTimeParticipant; teeTimeNumber: number; scheduledTime: string }[] = [];
  (schedule?.slots ?? [])
    .filter((slot) => slot.id !== currentTeeTimeId)
    .forEach((slot) => {
      slot.players.forEach((p) => {
        otherGroupPlayers.push({ participant: p, teeTimeNumber: slot.teeTimeNumber, scheduledTime: slot.scheduledTime });
      });
    });

  const handlePick = async (other: TeeTimeParticipant) => {
    try {
      await switchParticipant.mutateAsync({ roundId, otherParticipantId: other.participantId });
      onSwitched(other.playerName);
    } catch {
      // Error surfaced via switchParticipant.isError below
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <Card className="w-full max-w-md max-h-[80vh] overflow-y-auto">
        <CardHeader>
          <CardTitle>Switch Groups With…</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {switchParticipant.isError && (
            <div className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {(() => {
                const err = switchParticipant.error as { response?: { data?: { error?: string } } } | null;
                return err?.response?.data?.error ?? 'Failed to switch groups. Please try again.';
              })()}
            </div>
          )}
          {isLoading ? (
            <div className="flex justify-center py-6">
              <Spinner />
            </div>
          ) : otherGroupPlayers.length === 0 ? (
            <p className="text-sm text-gray-500">No other players available to switch with.</p>
          ) : (
            <div className="space-y-2">
              {otherGroupPlayers.map(({ participant, teeTimeNumber, scheduledTime }) => (
                <button
                  key={participant.participantId}
                  type="button"
                  disabled={switchParticipant.isPending}
                  onClick={() => handlePick(participant)}
                  className="flex w-full items-center justify-between rounded-lg border border-gray-200 px-3 py-2 text-left text-sm hover:border-[#1B5E20] hover:bg-primary-50 disabled:opacity-50"
                >
                  <span className="font-medium text-gray-900">{participant.playerName}</span>
                  <span className="text-xs text-gray-500">
                    Group {teeTimeNumber} · {scheduledTime}
                  </span>
                </button>
              ))}
            </div>
          )}
          <Button variant="outline" className="w-full" onClick={onClose} disabled={switchParticipant.isPending}>
            Cancel
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}

interface ScorecardScanModalProps {
  teeTimeId: number;
  players: TeeTimePlayerScore[];
  holes: TeeTimeHoleInfo[];
  onClose: () => void;
  onConfirm: (scores: Record<number, Record<number, number | ''>>) => void;
}

const LOW_CONFIDENCE_THRESHOLD = 0.6;

/**
 * Upload a scorecard photo, OCR it, then let the user confirm/edit every
 * cell before applying it to the round. The photo lives only in this
 * component's local state for preview and is never uploaded anywhere except
 * the one-shot OCR request — discarded (state cleared) on close either way.
 */
function ScorecardScanModal({ teeTimeId, players, holes, onClose, onConfirm }: ScorecardScanModalProps) {
  const parseImage = useParseScorecardImage(teeTimeId);
  const [ocrResult, setOcrResult] = useState<ScorecardOcrResult | null>(null);
  // Editable grid keyed by our own row index (not playerId — a row may be
  // unmatched until the user assigns it), plus a parallel player-assignment map.
  const [rowScores, setRowScores] = useState<Record<number, Record<number, number | ''>>>({});
  const [rowPlayerId, setRowPlayerId] = useState<Record<number, number | null>>({});

  const handleFileSelected = async (file: File) => {
    const result = await parseImage.mutateAsync(file);
    setOcrResult(result);

    const nextScores: Record<number, Record<number, number | ''>> = {};
    const nextPlayerId: Record<number, number | null> = {};
    result.players.forEach((row, idx) => {
      nextPlayerId[idx] = row.playerId;
      nextScores[idx] = {};
      row.holes.forEach((h) => {
        if (h.grossStrokes != null) nextScores[idx][h.holeNumber] = h.grossStrokes;
      });
    });
    setRowScores(nextScores);
    setRowPlayerId(nextPlayerId);
  };

  const handleCellChange = (rowIdx: number, holeNumber: number, value: number | '') => {
    setRowScores((prev) => ({
      ...prev,
      [rowIdx]: { ...prev[rowIdx], [holeNumber]: value },
    }));
  };

  // OCR can miss a player's row entirely (not just misread their name) —
  // this adds a blank row so every active player always has somewhere to
  // land, even when the photo only captured part of the group.
  const handleAddMissingPlayer = (playerId: number) => {
    const nextRowIdx = Math.max(-1, ...Object.keys(rowPlayerId).map(Number)) + 1;
    setRowPlayerId((prev) => ({ ...prev, [nextRowIdx]: playerId }));
    setRowScores((prev) => ({ ...prev, [nextRowIdx]: {} }));
  };

  const handleConfirm = () => {
    const merged: Record<number, Record<number, number | ''>> = {};
    Object.entries(rowPlayerId).forEach(([rowIdxStr, playerId]) => {
      if (playerId == null) return;
      merged[playerId] = { ...merged[playerId], ...rowScores[Number(rowIdxStr)] };
    });
    onConfirm(merged);
  };

  const assignedPlayerIds = new Set(Object.values(rowPlayerId).filter((id): id is number => id != null));
  // Every active player in the group must be assigned before confirming —
  // this replaces the full group setup step, so a partial match can't be
  // allowed to silently skip setup (skip/advanced-stats) for the rest.
  const unassignedPlayers = players.filter((p) => !assignedPlayerIds.has(p.playerId));
  const canConfirm = ocrResult !== null && unassignedPlayers.length === 0;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <Card className="w-full max-w-3xl max-h-[85vh] overflow-y-auto">
        <CardHeader>
          <CardTitle>Scan Scorecard</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {parseImage.isError && (
            <div className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {(() => {
                const err = parseImage.error as { response?: { data?: { error?: string } } } | null;
                return err?.response?.data?.error ?? 'Failed to scan scorecard. Please try again.';
              })()}
            </div>
          )}

          {!ocrResult && (
            <div className="space-y-3">
              <p className="text-sm text-gray-600">
                Take or upload a photo of the completed paper scorecard. It's used once to read the
                scores and is not saved.
              </p>
              <label className="flex cursor-pointer flex-col items-center justify-center gap-2 rounded-lg border-2 border-dashed border-gray-300 px-4 py-10 text-gray-500 hover:border-[#1B5E20] hover:text-[#1B5E20]">
                {parseImage.isPending ? (
                  <>
                    <Spinner />
                    <span className="text-sm">Reading scorecard…</span>
                  </>
                ) : (
                  <>
                    <Camera className="h-8 w-8" />
                    <span className="text-sm font-medium">Choose photo</span>
                  </>
                )}
                <input
                  type="file"
                  accept="image/*"
                  capture="environment"
                  className="hidden"
                  disabled={parseImage.isPending}
                  onChange={(e) => {
                    const file = e.target.files?.[0];
                    if (file) void handleFileSelected(file);
                  }}
                />
              </label>
            </div>
          )}

          {ocrResult && (
            <div className="space-y-4">
              <p className="text-sm text-gray-600">
                Review each score below — cells outlined in amber had low OCR confidence. Unmatched
                rows need a player assigned before they can be saved.
              </p>
              <div className="overflow-x-auto">
                <table className="w-full border-collapse text-sm">
                  <thead>
                    <tr>
                      <th className="border-b border-gray-200 px-2 py-2 text-left">Player</th>
                      {holes.map((h) => (
                        <th key={h.holeNumber} className="border-b border-gray-200 px-2 py-2 text-center">
                          {h.holeNumber}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {Object.keys(rowPlayerId)
                      .map(Number)
                      .sort((a, b) => a - b)
                      .map((rowIdx) => {
                        const ocrRow = ocrResult.players[rowIdx] as (typeof ocrResult.players)[number] | undefined;
                        const rawOcrName = ocrRow?.rawOcrName ?? 'Added manually';
                        return (
                          <tr key={rowIdx}>
                            <td className="border-b border-gray-100 px-2 py-2">
                              {rowPlayerId[rowIdx] != null ? (
                                <span className="font-medium text-gray-900">
                                  {players.find((p) => p.playerId === rowPlayerId[rowIdx])?.playerName ?? rawOcrName}
                                </span>
                              ) : (
                                <select
                                  className="rounded border border-amber-400 bg-amber-50 px-1 py-1 text-xs"
                                  value=""
                                  onChange={(e) => {
                                    const v = e.target.value;
                                    setRowPlayerId((prev) => ({ ...prev, [rowIdx]: v === '' ? null : Number(v) }));
                                  }}
                                >
                                  <option value="">Unmatched: "{rawOcrName}" — assign…</option>
                                  {players
                                    .filter((p) => !Object.values(rowPlayerId).includes(p.playerId))
                                    .map((p) => (
                                      <option key={p.playerId} value={p.playerId}>
                                        {p.playerName}
                                      </option>
                                    ))}
                                </select>
                              )}
                            </td>
                            {holes.map((h) => {
                              const cell = ocrRow?.holes.find((c) => c.holeNumber === h.holeNumber);
                              const lowConfidence = (cell?.confidence ?? 0) < LOW_CONFIDENCE_THRESHOLD;
                              const value = rowScores[rowIdx]?.[h.holeNumber] ?? '';
                              return (
                                <td key={h.holeNumber} className="border-b border-gray-100 px-1 py-1 text-center">
                                  <input
                                    type="number"
                                    min={1}
                                    value={value}
                                    onChange={(e) => {
                                      const v = e.target.value;
                                      handleCellChange(rowIdx, h.holeNumber, v === '' ? '' : parseInt(v, 10));
                                    }}
                                    className={`h-9 w-12 rounded border text-center text-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20] ${
                                      lowConfidence ? 'border-amber-400 bg-amber-50' : 'border-gray-300'
                                    }`}
                                  />
                                </td>
                              );
                            })}
                          </tr>
                        );
                      })}
                  </tbody>
                </table>
              </div>

              {unassignedPlayers.length > 0 && (
                <div className="flex flex-wrap items-center gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm">
                  <span className="text-amber-800">Not on the scanned card:</span>
                  {unassignedPlayers.map((p) => (
                    <Button key={p.playerId} variant="outline" size="sm" onClick={() => handleAddMissingPlayer(p.playerId)}>
                      + {p.playerName}
                    </Button>
                  ))}
                </div>
              )}
            </div>
          )}

          {ocrResult && unassignedPlayers.length > 0 && (
            <p className="text-sm text-amber-700">
              Assign a player to every row before applying — {unassignedPlayers.length} player
              {unassignedPlayers.length === 1 ? '' : 's'} still need{unassignedPlayers.length === 1 ? 's' : ''} a
              scorecard row (or scan a photo that shows the whole group).
            </p>
          )}

          <div className="flex justify-end gap-2 pt-2">
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            {ocrResult && (
              <Button variant="primary" onClick={handleConfirm} disabled={!canConfirm}>
                Apply Scores
              </Button>
            )}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

interface GroupSetupStepProps {
  roundId: number;
  teeTimeId: number;
  players: TeeTimePlayerScore[];
  skippedMap: Record<number, boolean>;
  advancedStatsMap: Record<number, boolean>;
  onToggleSkipped: (playerId: number, skipped: boolean) => void;
  onToggleAdvancedStats: (playerId: number, enabled: boolean) => void;
  pendingSkipIds: Set<number>;
  onContinue: () => void;
  scorecardOcrEnabled: boolean;
  holes: TeeTimeHoleInfo[];
  onScanApplied: (scores: Record<number, Record<number, number | ''>>) => void;
}

function GroupSetupStep({
  roundId,
  teeTimeId,
  players,
  skippedMap,
  advancedStatsMap,
  onToggleSkipped,
  onToggleAdvancedStats,
  pendingSkipIds,
  onContinue,
  scorecardOcrEnabled,
  holes,
  onScanApplied,
}: GroupSetupStepProps) {
  const activePlayers = players.filter((p) => !p.isWithdrawn);
  const [switchingPlayerId, setSwitchingPlayerId] = useState<number | null>(null);
  const [switchNotice, setSwitchNotice] = useState<string | null>(null);
  const [showScanModal, setShowScanModal] = useState(false);

  return (
    <div className="space-y-6">
      <div className="rounded-lg bg-primary-50 px-4 py-3">
        <p className="text-sm text-primary-700">
          Before entering scores, confirm who is playing and choose which players to track advanced statistics for.
        </p>
      </div>

      {switchNotice && (
        <div className="rounded-lg border border-green-200 bg-green-50 px-4 py-3 text-sm text-green-700">
          {switchNotice}
        </div>
      )}

      {scorecardOcrEnabled && (
        <Button variant="outline" className="w-full" onClick={() => setShowScanModal(true)}>
          <Camera className="h-4 w-4 mr-1.5" />
          Scan Scorecard
        </Button>
      )}

      <div className="space-y-3">
        {activePlayers.map((player) => {
          const isSkipped = skippedMap[player.playerId] ?? player.skippedWeek;
          const trackAdvanced = advancedStatsMap[player.playerId] ?? false;
          const isPending = pendingSkipIds.has(player.playerId);

          return (
            <Card key={player.playerId} className={isSkipped ? 'opacity-60' : ''}>
              <CardContent className="p-4">
                <div className="flex items-start justify-between gap-4">
                  <div className="min-w-0">
                    <p className="font-semibold text-gray-900 truncate">{player.playerName}</p>
                    <p className="text-xs text-gray-500">
                      HCP {formatHandicapPair(player.handicapIndex)} · CH {player.courseHandicap}
                    </p>
                  </div>
                  <div className="flex flex-col items-end gap-2 shrink-0">
                    {/* Skip toggle */}
                    <button
                      type="button"
                      disabled={isPending}
                      onClick={() => onToggleSkipped(player.playerId, !isSkipped)}
                      className={`flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-medium transition-colors ${
                        isSkipped
                          ? 'bg-amber-100 text-amber-800 hover:bg-amber-200'
                          : 'bg-gray-100 text-gray-600 hover:bg-gray-200'
                      } disabled:opacity-50`}
                    >
                      {isPending ? (
                        <Spinner className="h-3 w-3" />
                      ) : (
                        <span>{isSkipped ? '✗ Skipping' : '✓ Playing'}</span>
                      )}
                    </button>
                    {/* Advanced stats toggle — only relevant if not skipping */}
                    {!isSkipped && (
                      <button
                        type="button"
                        onClick={() => onToggleAdvancedStats(player.playerId, !trackAdvanced)}
                        className={`flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-medium transition-colors ${
                          trackAdvanced
                            ? 'bg-blue-100 text-blue-800 hover:bg-blue-200'
                            : 'bg-gray-100 text-gray-500 hover:bg-gray-200'
                        }`}
                      >
                        <BarChart2 className="h-3 w-3" />
                        {trackAdvanced ? 'Advanced stats on' : 'Advanced stats off'}
                      </button>
                    )}
                    <button
                      type="button"
                      onClick={() => setSwitchingPlayerId(player.playerId)}
                      className="flex items-center gap-1.5 rounded-full bg-gray-100 px-3 py-1 text-xs font-medium text-gray-600 transition-colors hover:bg-gray-200"
                    >
                      <Repeat className="h-3 w-3" />
                      Switch player
                    </button>
                  </div>
                </div>
              </CardContent>
            </Card>
          );
        })}
      </div>

      <Button variant="primary" size="lg" className="w-full" onClick={onContinue}>
        Start Entering Scores
        <ChevronRight className="h-4 w-4 ml-1" />
      </Button>

      {switchingPlayerId != null && (
        <SwitchPlayerPicker
          roundId={roundId}
          currentTeeTimeId={teeTimeId}
          onClose={() => setSwitchingPlayerId(null)}
          onSwitched={(otherPlayerName) => {
            setSwitchingPlayerId(null);
            setSwitchNotice(`Switched with ${otherPlayerName}.`);
          }}
        />
      )}

      {showScanModal && (
        <ScorecardScanModal
          teeTimeId={teeTimeId}
          players={activePlayers}
          holes={holes}
          onClose={() => setShowScanModal(false)}
          onConfirm={(scores) => {
            setShowScanModal(false);
            onScanApplied(scores);
          }}
        />
      )}
    </div>
  );
}

interface HoleViewProps {
  hole: TeeTimeHoleInfo;
  players: TeeTimePlayerScore[];
  scores: Record<number, Record<number, number | ''>>;
  holeDataMap: Record<number, Record<number, HoleData>>;
  onScoreChange: (playerId: number, value: number | '') => void;
  onHoleDataChange: (playerId: number, field: keyof HoleData, value: number | '' | boolean | null) => void;
  canEdit: boolean;
  advancedStatsMap: Record<number, boolean>;
}

function PuttInput({ label, value, onChange, disabled, min, max, step, placeholder }: {
  label: string;
  value: number | '';
  onChange: (value: number | '') => void;
  disabled?: boolean;
  min?: number;
  max?: number;
  step?: string;
  placeholder?: string;
}) {
  return (
    <div className="flex flex-col items-center gap-1">
      <label className="text-[10px] font-medium text-gray-400">{label}</label>
      <input
        type="number"
        min={min ?? 0}
        max={max ?? 99}
        step={step}
        value={value === '' ? '' : value}
        disabled={disabled}
        placeholder={placeholder}
        onChange={(e) => {
          const v = e.target.value;
          if (v === '') { onChange(''); return; }
          const n = step ? parseFloat(v) : parseInt(v, 10);
          if (!isNaN(n) && n >= (min ?? 0)) onChange(n);
        }}
        className="h-10 w-14 rounded-lg border border-gray-200 text-center text-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20] disabled:bg-gray-100 disabled:text-gray-400"
      />
    </div>
  );
}

function HoleView({ hole, players, scores, holeDataMap, onScoreChange, onHoleDataChange, canEdit, advancedStatsMap }: HoleViewProps) {
  // Fairway is relevant for par 4 and 5 holes only
  const showFairway = hole.par >= 4;

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
          const holeData = holeDataMap[player.playerId]?.[hole.holeNumber];
          const isSkipped = player.skippedWeek;
          const trackAdvanced = advancedStatsMap[player.playerId] ?? false;

          // GIR: reached green in regulation = (gross - putts) <= par - 2
          const putts = holeData?.putts;
          const gir = trackAdvanced && putts !== '' && putts != null && playerScore !== '' && playerScore !== 0
            ? ((playerScore as number) - (putts as number)) <= hole.par - 2
            : null;

          const handicapStrokes = calculateHandicapStrokes(player.courseHandicap, hole.strokeIndex);

          return (
            <Card key={player.playerId} className={isSkipped ? 'opacity-50' : ''}>
              <CardContent className="p-4">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="font-semibold text-gray-900 flex items-center gap-1.5">
                      {player.playerName}
                      {isSkipped && (
                        <span className="text-xs text-gray-500">(Skipped)</span>
                      )}
                      {!isSkipped && <HandicapDots strokes={handicapStrokes} />}
                    </p>
                    <p className="text-xs text-gray-500">
                      HCP {formatHandicapPair(player.handicapIndex)} · CH {player.courseHandicap}
                    </p>
                  </div>
                  <div className="flex items-end gap-2">
                    <ScoreInput
                      label="Gross"
                      value={isSkipped ? '' : playerScore}
                      onChange={(v) => onScoreChange(player.playerId, v)}
                      disabled={!canEdit || isSkipped}
                    />
                    {trackAdvanced && (
                      <>
                        <PuttInput
                          label="Putts"
                          value={isSkipped ? '' : (holeData?.putts ?? '')}
                          onChange={(v) => onHoleDataChange(player.playerId, 'putts', v)}
                          disabled={!canEdit || isSkipped}
                          min={0}
                          max={9}
                          placeholder="—"
                        />
                        <PuttInput
                          label="1st Putt (ft)"
                          value={isSkipped ? '' : (holeData?.firstPuttDistanceFeet ?? '')}
                          onChange={(v) => onHoleDataChange(player.playerId, 'firstPuttDistanceFeet', v)}
                          disabled={!canEdit || isSkipped}
                          min={0}
                          max={200}
                          step="1"
                          placeholder="—"
                        />
                        {showFairway && (
                          <div className="flex flex-col items-center gap-1">
                            <label className="text-[10px] font-medium text-gray-400">Fairway</label>
                            <button
                              type="button"
                              disabled={!canEdit || isSkipped}
                              onClick={() => onHoleDataChange(player.playerId, 'fairwayHit', !holeData?.fairwayHit)}
                              className={`h-10 w-14 rounded-lg border text-sm font-medium transition-colors ${
                                holeData?.fairwayHit
                                  ? 'border-green-500 bg-green-50 text-green-700'
                                  : 'border-gray-200 bg-white text-gray-400 hover:border-gray-300'
                              } disabled:bg-gray-100 disabled:text-gray-400`}
                            >
                              {holeData?.fairwayHit ? 'Yes' : 'No'}
                            </button>
                          </div>
                        )}
                        <div className="flex flex-col items-center gap-1">
                          <label className="text-[10px] font-medium text-gray-400">GIR</label>
                          <div className={`h-10 w-10 rounded-lg flex items-center justify-center text-sm font-bold ${
                            gir === true
                              ? 'bg-green-100 text-green-700'
                              : gir === false
                              ? 'bg-gray-100 text-gray-500'
                              : 'bg-gray-50 text-gray-300'
                          }`}>
                            {gir === true ? '✓' : gir === false ? '✗' : '—'}
                          </div>
                        </div>
                      </>
                    )}
                  </div>
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
                <>
                  <div className="mb-3 overflow-x-auto">
                    <table className="w-full text-center text-xs">
                      <thead>
                        <tr className="text-gray-500">
                          <th className="pr-2 text-left font-medium">Hole</th>
                          {holes.map((hole) => (
                            <th key={hole.holeNumber} className="px-1 font-medium">{hole.holeNumber}</th>
                          ))}
                        </tr>
                        <tr className="text-gray-400">
                          <th className="pr-2 text-left font-normal">Par</th>
                          {holes.map((hole) => (
                            <th key={hole.holeNumber} className="px-1 font-normal">{hole.par}</th>
                          ))}
                        </tr>
                      </thead>
                      <tbody>
                        <tr>
                          <td className="pr-2 text-left font-medium text-gray-700">Score</td>
                          {holes.map((hole) => {
                            const gross = scores[summary.player.playerId]?.[hole.holeNumber];
                            return (
                              <td key={hole.holeNumber} className="px-1 font-semibold text-gray-900">
                                {gross === '' || gross === undefined || gross === null ? '—' : gross}
                              </td>
                            );
                          })}
                        </tr>
                      </tbody>
                    </table>
                  </div>
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
                </>
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

/**
 * Closest-to-the-pin selection for each par 3 of the round. Scorer/admin
 * only; the winner may be any active participant of the round (not just
 * this tee-time group), or "None".
 */
function ClosestToPinSection({ roundId, canEdit }: { roundId: number; canEdit: boolean }) {
  const ctp = useRoundClosestToPin(roundId);
  const saveCtp = useSetRoundClosestToPin(roundId);
  // holeNumber -> playerId | null; only holds unsaved local edits
  const [edits, setEdits] = useState<Record<number, number | null>>({});

  if (ctp.isPending) {
    return (
      <Card>
        <CardContent className="flex justify-center py-6">
          <Spinner />
        </CardContent>
      </Card>
    );
  }
  if (ctp.isError || !ctp.data || ctp.data.par3Holes.length === 0) return null;

  const { par3Holes, participants } = ctp.data;

  const selectionFor = (holeNumber: number, savedPlayerId: number | null): number | null =>
    holeNumber in edits ? edits[holeNumber] : savedPlayerId;

  const isDirty = par3Holes.some(
    (h) => h.holeNumber in edits && edits[h.holeNumber] !== h.playerId,
  );

  const handleSave = () => {
    const selections = par3Holes.map((h) => ({
      holeNumber: h.holeNumber,
      playerId: selectionFor(h.holeNumber, h.playerId),
    }));
    saveCtp.mutate(selections, { onSuccess: () => setEdits({}) });
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Target className="h-5 w-5 text-[#1B5E20]" />
          Closest to the Pin
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <p className="text-sm text-gray-500">
          Select the player who was closest to the pin on each par 3 for this round.
        </p>
        {par3Holes.map((hole) => (
          <div key={hole.holeNumber} className="flex items-center justify-between gap-4">
            <span className="text-sm font-medium text-gray-900">Hole {hole.holeNumber}</span>
            <select
              value={selectionFor(hole.holeNumber, hole.playerId) ?? ''}
              onChange={(e) =>
                setEdits((prev) => ({
                  ...prev,
                  [hole.holeNumber]: e.target.value === '' ? null : parseInt(e.target.value, 10),
                }))
              }
              disabled={!canEdit || saveCtp.isPending}
              className="w-56 rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20] disabled:opacity-50 disabled:bg-gray-50"
            >
              <option value="">None</option>
              {participants.map((p) => (
                <option key={p.playerId} value={p.playerId}>
                  {p.playerName}
                </option>
              ))}
            </select>
          </div>
        ))}
        {canEdit && (
          <div className="flex items-center gap-3 pt-2">
            <Button size="sm" onClick={handleSave} disabled={!isDirty || saveCtp.isPending}>
              {saveCtp.isPending ? 'Saving…' : 'Save Closest to Pin'}
            </Button>
            {saveCtp.isError && (
              <span className="text-sm text-red-600">Failed to save. Please try again.</span>
            )}
            {saveCtp.isSuccess && !isDirty && !saveCtp.isPending && (
              <span className="text-sm text-green-700">Saved!</span>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

/**
 * Tournament-only closest-to-pin (par 3s) and longest-drive (the round's
 * configured hole) entry, scoped to the current tee-time group. Unlike the
 * regular-round ClosestToPinSection, any group member can record these (not
 * just scorer/admin) and each pick saves immediately on selection — the
 * leaderboard reads live from the same rows.
 */
function TournamentHoleExtrasSection({
  teeTimeId,
  currentHole,
  players,
  ctp,
  longestDriveHoleNumber,
  longestDrive,
  canEdit,
}: {
  teeTimeId: number;
  currentHole: TeeTimeHoleInfo;
  players: TeeTimePlayerScore[];
  ctp: TeeTimeGroupScorecard['tournamentCtp'];
  longestDriveHoleNumber: number | null;
  longestDrive: TeeTimeGroupScorecard['tournamentLongestDrive'];
  canEdit: boolean;
}) {
  const setCtp = useSetTeeTimeTournamentCtp(teeTimeId);
  const setLongestDrive = useSetTeeTimeTournamentLongestDrive(teeTimeId);

  const ctpHole = currentHole.par === 3 ? ctp.find((c) => c.holeNumber === currentHole.holeNumber) : undefined;
  const showLongestDrive = longestDriveHoleNumber === currentHole.holeNumber && longestDrive.length > 0;

  if (!ctpHole && !showLongestDrive) return null;

  const activePlayers = players.filter((p) => !p.isWithdrawn);

  return (
    <Card>
      <CardContent className="space-y-4 py-4">
        {ctpHole && (
          <div className="flex items-center justify-between gap-4">
            <span className="flex items-center gap-1.5 text-sm font-medium text-gray-700">
              <Target className="h-4 w-4 text-[#1B5E20]" />
              Closest to the Pin
            </span>
            <select
              value={ctpHole.winnerPlayerId ?? ''}
              onChange={(e) =>
                setCtp.mutate({
                  holeNumber: currentHole.holeNumber,
                  winnerPlayerId: e.target.value === '' ? null : parseInt(e.target.value, 10),
                })
              }
              disabled={!canEdit || setCtp.isPending}
              className="w-56 rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20] disabled:opacity-50 disabled:bg-gray-50"
            >
              <option value="">None</option>
              {activePlayers.map((p) => (
                <option key={p.playerId} value={p.playerId}>{p.playerName}</option>
              ))}
            </select>
          </div>
        )}
        {showLongestDrive && (
          <div className="space-y-2">
            <span className="flex items-center gap-1.5 text-sm font-medium text-gray-700">
              <Flag className="h-4 w-4 text-amber-500" />
              Longest Drive
            </span>
            {longestDrive.map((flight) => {
              const flightPlayers = activePlayers.filter((p) => p.tournamentFlightId === flight.tournamentFlightId);
              if (flightPlayers.length === 0) return null;
              return (
                <div key={flight.tournamentFlightId} className="flex items-center justify-between gap-4 pl-1">
                  <span className="text-sm text-gray-500">Flight {flight.flightName}</span>
                  <select
                    value={flight.winnerPlayerId ?? ''}
                    onChange={(e) =>
                      setLongestDrive.mutate({
                        tournamentFlightId: flight.tournamentFlightId,
                        winnerPlayerId: e.target.value === '' ? null : parseInt(e.target.value, 10),
                      })
                    }
                    disabled={!canEdit || setLongestDrive.isPending}
                    className="w-56 rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20] disabled:opacity-50 disabled:bg-gray-50"
                  >
                    <option value="">None</option>
                    {flightPlayers.map((p) => (
                      <option key={p.playerId} value={p.playerId}>{p.playerName}</option>
                    ))}
                  </select>
                </div>
              );
            })}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

interface PendingConflictSave {
  context: 'hole' | 'submit';
  conflicts: HoleScoreConflict[];
  holeNumber?: number;
}

interface ConflictDialogProps {
  pending: PendingConflictSave;
  choices: Record<string, 'existing' | 'new'>;
  onChoiceChange: (playerId: number, holeNumber: number, choice: 'existing' | 'new') => void;
  onConfirm: () => void;
  onCancel: () => void;
  isSubmitting: boolean;
}

function conflictKey(playerId: number, holeNumber: number): string {
  return `${playerId}:${holeNumber}`;
}

function ConflictDialog({ pending, choices, onChoiceChange, onConfirm, onCancel, isSubmitting }: ConflictDialogProps) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <Card className="w-full max-w-lg max-h-[90vh] overflow-y-auto">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-amber-800">
            <AlertTriangle className="h-5 w-5" />
            Score Conflict{pending.conflicts.length > 1 ? 's' : ''} Detected
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <p className="text-sm text-gray-600">
            Someone else already entered a different score. Choose which value to keep for each.
          </p>
          <div className="space-y-3">
            {pending.conflicts.map((c) => {
              const key = conflictKey(c.playerId, c.holeNumber);
              const choice = choices[key] ?? 'existing';
              return (
                <div key={key} className="rounded-lg border border-amber-200 bg-amber-50 p-3">
                  <p className="text-sm font-semibold text-gray-900">
                    Hole {c.holeNumber} — {c.playerName}
                  </p>
                  <div className="mt-2 space-y-2">
                    <label className="flex items-center gap-2 text-sm">
                      <input
                        type="radio"
                        name={key}
                        checked={choice === 'existing'}
                        onChange={() => onChoiceChange(c.playerId, c.holeNumber, 'existing')}
                      />
                      Keep {c.existingGrossStrokes}
                      <span className="text-gray-500">
                        (entered by {c.existingEnteredByName ?? 'another player'})
                      </span>
                    </label>
                    <label className="flex items-center gap-2 text-sm">
                      <input
                        type="radio"
                        name={key}
                        checked={choice === 'new'}
                        onChange={() => onChoiceChange(c.playerId, c.holeNumber, 'new')}
                      />
                      Use {c.newGrossStrokes} <span className="text-gray-500">(your entry)</span>
                    </label>
                  </div>
                </div>
              );
            })}
          </div>
          <div className="flex justify-end gap-3 pt-2">
            <Button variant="outline" onClick={onCancel} disabled={isSubmitting}>
              Cancel
            </Button>
            <Button variant="primary" onClick={onConfirm} disabled={isSubmitting}>
              {isSubmitting ? <Spinner className="h-4 w-4" /> : 'Confirm'}
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

export function TeeTimeScoreEntryPage() {
  const { teeTimeId } = useParams<{ teeTimeId: string }>();
  const teeTimeIdNum = parseInt(teeTimeId ?? '0', 10);

  const { data: scorecard, isLoading, error, refetch: refetchScorecard } = useTeeTimeGroupScorecard(teeTimeIdNum);
  const submitScores = useSubmitTeeTimeGroupScores(teeTimeIdNum);
  const saveHoleScores = useSaveTeeTimeHoleScores(teeTimeIdNum);
  const setParticipantSkipped = useSetTeeTimeParticipantSkipped(teeTimeIdNum);

  const user = useAuthStore((s) => s.user);
  const featureFlags = useFeatureFlagStates();
  const ctpEnabled = featureFlags.data?.[FEATURE_FLAG_KEYS.closestToPinEnabled] ?? false;
  const scorecardOcrEnabled = featureFlags.data?.[FEATURE_FLAG_KEYS.scorecardOcrEnabled] ?? false;
  const isScorerOrAdmin =
    (user?.isSuperAdmin ?? false) ||
    (user?.roles?.some((r) => r === 'scorer' || r === 'admin') ?? false);
  const showClosestToPin = ctpEnabled && isScorerOrAdmin;

  // setup step state
  const [setupComplete, setSetupComplete] = useState(false);
  // local overrides for skip (mirrors DB after save); keyed by playerId
  const [skippedOverrides, setSkippedOverrides] = useState<Record<number, boolean>>({});
  // which playerIds are currently being saved (skip API in-flight)
  const [pendingSkipIds, setPendingSkipIds] = useState<Set<number>>(new Set());
  // per-player advanced stats opt-in (local only, not persisted)
  const [advancedStatsMap, setAdvancedStatsMap] = useState<Record<number, boolean>>({});

  const [currentHoleIndex, setCurrentHoleIndex] = useState(0);
  const [scores, setScores] = useState<Record<number, Record<number, number | ''>>>({});
  const [holeDataMap, setHoleDataMap] = useState<Record<number, Record<number, HoleData>>>({});
  const [showSummary, setShowSummary] = useState(false);
  const [submitSuccess, setSubmitSuccess] = useState(false);
  const [isRefetchingHole, setIsRefetchingHole] = useState(false);
  const [pendingConflict, setPendingConflict] = useState<PendingConflictSave | null>(null);
  const [conflictChoices, setConflictChoices] = useState<Record<string, 'existing' | 'new'>>({});

  const rawPlayers = scorecard?.players ?? [];
  // Merge local skip overrides so the UI reacts immediately without waiting for refetch
  const players = rawPlayers.map((p) =>
    p.playerId in skippedOverrides ? { ...p, skippedWeek: skippedOverrides[p.playerId] } : p
  );
  const holes = scorecard?.holes ?? [];
  const canEdit = scorecard?.roundStatus === 'Scheduled' || scorecard?.roundStatus === 'InProgress';
  const currentHole = holes[currentHoleIndex];

  const seededOnceRef = useRef(false);

  const mergeHoleFromScorecard = useCallback((holeNumber: number, fresh: TeeTimeGroupScorecard) => {
    setScores((prev) => {
      const next = { ...prev };
      fresh.players.forEach((player) => {
        const hs = player.holeScores.find((h) => h.holeNumber === holeNumber);
        next[player.playerId] = { ...next[player.playerId] };
        if (hs && hs.grossStrokes != null) {
          next[player.playerId][holeNumber] = hs.grossStrokes;
        } else {
          delete next[player.playerId][holeNumber];
        }
      });
      return next;
    });
    setHoleDataMap((prev) => {
      const next = { ...prev };
      fresh.players.forEach((player) => {
        const hs = player.holeScores.find((h) => h.holeNumber === holeNumber);
        next[player.playerId] = {
          ...next[player.playerId],
          [holeNumber]: {
            putts: hs?.putts ?? '',
            firstPuttDistanceFeet: hs?.firstPuttDistanceFeet ?? '',
            fairwayHit: hs?.fairwayHit ?? null,
          },
        };
      });
      return next;
    });
  }, []);

  // One-time bulk seed of all holes on first load only, so GroupSetupStep /
  // ScoreSummary have data before any navigation happens.
  useEffect(() => {
    if (!scorecard || seededOnceRef.current) return;
    seededOnceRef.current = true;

    const initialScores: Record<number, Record<number, number | ''>> = {};
    const initialHoleData: Record<number, Record<number, HoleData>> = {};
    scorecard.players.forEach((player) => {
      initialScores[player.playerId] = {};
      initialHoleData[player.playerId] = {};
      player.holeScores.forEach((holeScore) => {
        if (holeScore.grossStrokes != null) {
          initialScores[player.playerId][holeScore.holeNumber] = holeScore.grossStrokes;
        }
        initialHoleData[player.playerId][holeScore.holeNumber] = {
          putts: holeScore.putts ?? '',
          firstPuttDistanceFeet: holeScore.firstPuttDistanceFeet ?? '',
          fairwayHit: holeScore.fairwayHit ?? null,
        };
      });
    });
    setScores(initialScores);
    setHoleDataMap(initialHoleData);
  }, [scorecard]);

  // Refetch fresh scores for the current hole whenever the user navigates to
  // it (Next, Prev, or hole-dot jump) — including the initially-displayed
  // hole, since currentHoleIndex starts at 0 and wouldn't otherwise change
  // when the bulk seed completes.
  useEffect(() => {
    if (!scorecard || !currentHole) return;

    let cancelled = false;
    setIsRefetchingHole(true);
    refetchScorecard()
      .then((res) => {
        if (!cancelled && res.data) {
          mergeHoleFromScorecard(currentHole.holeNumber, res.data);
        }
      })
      .finally(() => {
        if (!cancelled) setIsRefetchingHole(false);
      });

    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentHoleIndex, !!scorecard]);

  const handleScoreChange = useCallback((playerId: number, holeNumber: number, value: number | '') => {
    setScores((prev) => ({
      ...prev,
      [playerId]: {
        ...prev[playerId],
        [holeNumber]: value,
      },
    }));
  }, []);

  // Applies a confirmed scorecard scan: merges the per-player hole scores
  // into local state (same shape/path as manual hole-by-hole entry), then
  // jumps straight to the summary screen so the user can do a final review
  // before submitting — scanned scores aren't saved until they hit Submit.
  const handleScanApplied = useCallback((scannedScores: Record<number, Record<number, number | ''>>) => {
    setScores((prev) => {
      const next = { ...prev };
      Object.entries(scannedScores).forEach(([playerIdStr, holeScores]) => {
        const playerId = Number(playerIdStr);
        next[playerId] = { ...next[playerId], ...holeScores };
      });
      return next;
    });
    setShowSummary(true);
  }, []);

  const handleHoleDataChange = useCallback((playerId: number, holeNumber: number, field: keyof HoleData, value: number | '' | boolean | null) => {
    setHoleDataMap((prev) => ({
      ...prev,
      [playerId]: {
        ...prev[playerId],
        [holeNumber]: {
          ...(prev[playerId]?.[holeNumber] ?? { putts: '', firstPuttDistanceFeet: '', fairwayHit: null }),
          [field]: value,
        },
      },
    }));
  }, []);

  const handleToggleSkipped = useCallback(async (playerId: number, skipped: boolean) => {
    setPendingSkipIds((prev) => new Set(prev).add(playerId));
    try {
      await setParticipantSkipped.mutateAsync({ playerId, skipped });
      setSkippedOverrides((prev) => ({ ...prev, [playerId]: skipped }));
    } finally {
      setPendingSkipIds((prev) => { const next = new Set(prev); next.delete(playerId); return next; });
    }
  }, [setParticipantSkipped]);

  const handleToggleAdvancedStats = useCallback((playerId: number, enabled: boolean) => {
    setAdvancedStatsMap((prev) => ({ ...prev, [playerId]: enabled }));
  }, []);

  const buildHoleScoresPayload = useCallback((holeNumber: number): PlayerScoreInput[] => {
    return players
      .filter((p) => !p.skippedWeek)
      .map((player) => {
        const gross = scores[player.playerId]?.[holeNumber];
        const hd = holeDataMap[player.playerId]?.[holeNumber];
        const hole = holes.find((h) => h.holeNumber === holeNumber);
        return {
          playerId: player.playerId,
          holeScores: gross !== undefined && gross !== '' ? [{
            holeNumber,
            grossStrokes: gross as number,
            putts: hd?.putts !== '' && hd?.putts != null ? hd.putts as number : null,
            firstPuttDistanceFeet: hd?.firstPuttDistanceFeet !== '' && hd?.firstPuttDistanceFeet != null ? hd.firstPuttDistanceFeet as number : null,
            fairwayHit: hole && hole.par >= 4 ? hd?.fairwayHit ?? null : null,
          }] : [],
        };
      })
      .filter((p) => p.holeScores.length > 0);
  }, [players, scores, holeDataMap, holes]);

  const advanceHole = useCallback(() => {
    if (currentHoleIndex < holes.length - 1) {
      setCurrentHoleIndex((prev) => prev + 1);
    } else {
      setShowSummary(true);
    }
  }, [currentHoleIndex, holes.length]);

  // Returns null if any non-skipped player is missing a gross score for any
  // hole — callers must not submit a payload with fabricated placeholder
  // scores for holes that were never actually entered.
  const buildSubmitPayload = useCallback((): PlayerScoreInput[] | null => {
    const activePlayers = players.filter((p) => !p.skippedWeek);
    const result: PlayerScoreInput[] = [];

    for (const player of activePlayers) {
      const holeScores = [];
      for (const hole of holes) {
        const gross = scores[player.playerId]?.[hole.holeNumber];
        if (gross === undefined || gross === '') {
          return null;
        }
        const hd = holeDataMap[player.playerId]?.[hole.holeNumber];
        holeScores.push({
          holeNumber: hole.holeNumber,
          grossStrokes: gross,
          putts: hd?.putts !== '' && hd?.putts != null ? hd.putts as number : null,
          firstPuttDistanceFeet: hd?.firstPuttDistanceFeet !== '' && hd?.firstPuttDistanceFeet != null ? hd.firstPuttDistanceFeet as number : null,
          fairwayHit: hole.par >= 4 ? hd?.fairwayHit ?? null : null,
        });
      }
      result.push({ playerId: player.playerId, holeScores });
    }

    return result;
  }, [players, scores, holeDataMap, holes]);

  function extractConflicts(err: unknown): HoleScoreConflict[] | null {
    const axiosErr = err as { response?: { status?: number; data?: { conflicts?: HoleScoreConflict[] } } } | null;
    if (axiosErr?.response?.status === 409 && axiosErr.response.data?.conflicts) {
      return axiosErr.response.data.conflicts;
    }
    return null;
  }

  const handleNext = async () => {
    if (!canEdit || !currentHole) {
      advanceHole();
      return;
    }

    const payload = buildHoleScoresPayload(currentHole.holeNumber);
    if (payload.length > 0) {
      try {
        await saveHoleScores.mutateAsync({ holeNumber: currentHole.holeNumber, playerScores: payload });
      } catch (err) {
        const conflicts = extractConflicts(err);
        if (conflicts && conflicts.length > 0) {
          setPendingConflict({ context: 'hole', conflicts, holeNumber: currentHole.holeNumber });
          return;
        }
        // Non-fatal otherwise — user can still navigate; full submit will catch any issues
      }
    }

    advanceHole();
  };

  const handlePrev = () => {
    if (showSummary) {
      setShowSummary(false);
    } else if (currentHoleIndex > 0) {
      setCurrentHoleIndex((prev) => prev - 1);
    }
  };

  const handleSubmit = async () => {
    const playerScores = buildSubmitPayload();
    if (playerScores === null) {
      // Should be prevented by the ScoreSummary "allComplete" gate, but never
      // submit a payload with holes missing real scores.
      return;
    }

    try {
      await submitScores.mutateAsync({ playerScores });
      setSubmitSuccess(true);
    } catch (err) {
      const conflicts = extractConflicts(err);
      if (conflicts && conflicts.length > 0) {
        setPendingConflict({ context: 'submit', conflicts });
      }
      // Other errors are surfaced via submitScores.isError below
    }
  };

  const handleConflictChoiceChange = useCallback((playerId: number, holeNumber: number, choice: 'existing' | 'new') => {
    setConflictChoices((prev) => ({ ...prev, [conflictKey(playerId, holeNumber)]: choice }));
  }, []);

  const handleConflictCancel = () => {
    setPendingConflict(null);
    setConflictChoices({});
  };

  const handleConflictConfirm = async () => {
    if (!pendingConflict) return;

    const confirmedOverwrites: ConfirmedOverwrite[] = [];
    // Revert local state for any conflicts the user chose to keep the existing value for,
    // so the retried payload matches and won't re-trigger the same conflict.
    for (const c of pendingConflict.conflicts) {
      const key = conflictKey(c.playerId, c.holeNumber);
      const choice = conflictChoices[key] ?? 'existing';
      if (choice === 'new') {
        confirmedOverwrites.push({ playerId: c.playerId, holeNumber: c.holeNumber });
      } else {
        setScores((prev) => ({
          ...prev,
          [c.playerId]: { ...prev[c.playerId], [c.holeNumber]: c.existingGrossStrokes },
        }));
      }
    }

    try {
      if (pendingConflict.context === 'hole' && pendingConflict.holeNumber != null) {
        const holeNumber = pendingConflict.holeNumber;
        const payload = buildHoleScoresPayload(holeNumber).map((p) => {
          const keptExisting = pendingConflict.conflicts.find(
            (c) => c.playerId === p.playerId && (conflictChoices[conflictKey(c.playerId, c.holeNumber)] ?? 'existing') === 'existing'
          );
          if (!keptExisting) return p;
          return {
            ...p,
            holeScores: p.holeScores.map((hs) =>
              hs.holeNumber === holeNumber ? { ...hs, grossStrokes: keptExisting.existingGrossStrokes } : hs
            ),
          };
        });
        await saveHoleScores.mutateAsync({ holeNumber, playerScores: payload, confirmedOverwrites });
        setPendingConflict(null);
        setConflictChoices({});
        advanceHole();
      } else {
        const payload = buildSubmitPayload();
        if (payload === null) {
          setPendingConflict(null);
          setConflictChoices({});
          return;
        }
        await submitScores.mutateAsync({ playerScores: payload, confirmedOverwrites });
        setPendingConflict(null);
        setConflictChoices({});
        setSubmitSuccess(true);
      }
    } catch (err) {
      const conflicts = extractConflicts(err);
      if (conflicts && conflicts.length > 0) {
        // Still conflicting (e.g. a third player also changed it) — show the updated list.
        setPendingConflict({ ...pendingConflict, conflicts });
        setConflictChoices({});
      }
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

      {submitScores.isError && !pendingConflict && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {(() => {
            const err = submitScores.error as { response?: { data?: { error?: string } } } | null;
            return err?.response?.data?.error ?? 'Failed to submit scores. Please try again.';
          })()}
        </div>
      )}

      {/* Setup interstitial — only shown for editable rounds */}
      {!setupComplete && canEdit && (
        <GroupSetupStep
          roundId={scorecard.roundId}
          teeTimeId={scorecard.teeTimeId}
          players={players}
          skippedMap={skippedOverrides}
          advancedStatsMap={advancedStatsMap}
          onToggleSkipped={handleToggleSkipped}
          onToggleAdvancedStats={handleToggleAdvancedStats}
          pendingSkipIds={pendingSkipIds}
          onContinue={() => setSetupComplete(true)}
          scorecardOcrEnabled={scorecardOcrEnabled}
          holes={holes}
          onScanApplied={(scores) => {
            setSetupComplete(true);
            handleScanApplied(scores);
          }}
        />
      )}

      {/* Progress indicator */}
      {(setupComplete || !canEdit) && !showSummary && (
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
            disabled={saveHoleScores.isPending}
          >
            {saveHoleScores.isPending ? (
              <Spinner className="h-4 w-4" />
            ) : (
              <>
                {currentHoleIndex === holes.length - 1 ? 'Review' : 'Next'}
                {currentHoleIndex < holes.length - 1 && <ChevronRight className="h-4 w-4 ml-1" />}
              </>
            )}
          </Button>
        </div>
      )}

      {/* Hole number indicator */}
      {(setupComplete || !canEdit) && !showSummary && (
        <p className="text-center text-sm text-gray-500">
          Hole {currentHoleIndex + 1} of {holes.length}
          {isRefetchingHole && <Spinner className="ml-2 inline-block h-3 w-3 align-middle" />}
        </p>
      )}

      {/* Main content */}
      {(setupComplete || !canEdit) && showSummary ? (
        <>
          {/* Closest to the pin — scorer/admin only, feature-flagged */}
          {showClosestToPin && (
            <ClosestToPinSection roundId={scorecard.roundId} canEdit={canEdit} />
          )}
          <ScoreSummary
            players={players}
            holes={holes}
            scores={scores}
            onSubmit={handleSubmit}
            isSubmitting={submitScores.isPending}
            canEdit={canEdit}
          />
        </>
      ) : currentHole ? (
        <>
          <HoleView
            hole={currentHole}
            players={players}
            scores={scores}
            holeDataMap={holeDataMap}
            onScoreChange={(playerId, value) =>
              handleScoreChange(playerId, currentHole.holeNumber, value)
            }
            onHoleDataChange={(playerId, field, value) =>
              handleHoleDataChange(playerId, currentHole.holeNumber, field, value)
            }
            canEdit={canEdit}
            advancedStatsMap={advancedStatsMap}
          />
          {scorecard?.roundType === 'Tournament' && (
            <TournamentHoleExtrasSection
              teeTimeId={teeTimeIdNum}
              currentHole={currentHole}
              players={players}
              ctp={scorecard.tournamentCtp}
              longestDriveHoleNumber={scorecard.longestDriveHoleNumber}
              longestDrive={scorecard.tournamentLongestDrive}
              canEdit={canEdit}
            />
          )}
        </>
      ) : null}

      {/* Navigation buttons for mobile */}
      {(setupComplete || !canEdit) && !showSummary && (
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
            disabled={saveHoleScores.isPending}
          >
            {saveHoleScores.isPending ? (
              <Spinner className="mr-2 h-4 w-4" />
            ) : currentHoleIndex === holes.length - 1 ? (
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

      {pendingConflict && (
        <ConflictDialog
          pending={pendingConflict}
          choices={conflictChoices}
          onChoiceChange={handleConflictChoiceChange}
          onConfirm={handleConflictConfirm}
          onCancel={handleConflictCancel}
          isSubmitting={saveHoleScores.isPending || submitScores.isPending}
        />
      )}
    </div>
  );
}
