import { useParams, Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { useState } from 'react';
import { usePlayer, useHandicapHistory, usePlayerRounds } from '@/hooks/usePlayers';
import { useSetTeeTimePreference } from '@/hooks/useTeeTimes';
import { useAuthStore } from '@/store/authStore';
import { TEE_TIME_SLOTS, TEE_TIME_SLOT_FLAG } from '@/types/api';
import type { TeeTimeSlotName } from '@/types/api';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from '@/components/ui/Card';
import {
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableRow,
} from '@/components/ui/Table';
import { SortableTableHead } from '@/components/ui/SortableTableHead';
import { useSortableTable } from '@/hooks/useSortableTable';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { FullPageSpinner } from '@/components/ui/Spinner';
import { ErrorMessage } from '@/components/ui/ErrorMessage';
import { PageHeader } from '@/components/ui/PageHeader';
import { HandicapChart } from '@/components/HandicapChart';
import { formatShortDate } from '@/lib/utils';
import { normalizeRoundStatus } from '@/lib/enumUtils';

function TeeTimePreferenceSelector({ playerId, currentMask }: { playerId: number; currentMask: number }) {
  const setPreference = useSetTeeTimePreference();
  const [selected, setSelected] = useState<Set<TeeTimeSlotName>>(
    () => new Set(TEE_TIME_SLOTS.filter((s) => (currentMask & TEE_TIME_SLOT_FLAG[s]) !== 0)),
  );

  function toggle(slot: TeeTimeSlotName) {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(slot)) next.delete(slot); else next.add(slot);
      return next;
    });
  }

  function save() {
    setPreference.mutate({ playerId, preferredSlots: [...selected] });
  }

  const isDirty = TEE_TIME_SLOTS.some(
    (s) => selected.has(s) !== ((currentMask & TEE_TIME_SLOT_FLAG[s]) !== 0),
  );

  return (
    <div className="rounded-lg border border-gray-200 bg-gray-50 px-4 py-3 space-y-3">
      <p className="text-sm font-medium text-gray-700">
        Preferred tee-time slots for auto-fill
        <span className="ml-1 text-xs font-normal text-gray-400">(select none, any, or all)</span>
      </p>
      <div className="flex flex-wrap gap-2">
        {TEE_TIME_SLOTS.map((slot) => {
          const active = selected.has(slot);
          return (
            <button
              key={slot}
              type="button"
              onClick={() => toggle(slot)}
              className={[
                'px-3 py-1 rounded-full text-sm font-medium border transition-colors',
                active
                  ? 'bg-[#1B5E20] text-white border-[#1B5E20]'
                  : 'bg-white text-gray-700 border-gray-300 hover:border-[#1B5E20]',
              ].join(' ')}
            >
              {slot}
            </button>
          );
        })}
      </div>
      {isDirty && (
        <div className="flex items-center gap-2">
          <Button size="sm" onClick={save} disabled={setPreference.isPending}>
            Save preference
          </Button>
          {setPreference.isError && (
            <span className="text-xs text-red-600">Failed to save.</span>
          )}
          {setPreference.isSuccess && !setPreference.isPending && (
            <span className="text-xs text-green-700">Saved!</span>
          )}
        </div>
      )}
    </div>
  );
}

export function PlayerProfilePage() {
  const { playerId } = useParams<{ playerId: string }>();
  const user = useAuthStore((s) => s.user);
  const player = usePlayer(playerId ?? '');
  const handicapSort = useSortableTable('playerHandicapHistory');
  const roundsSort = useSortableTable('playerPastRounds');
  const handicapHistory = useHandicapHistory(playerId ?? '', handicapSort.sort);
  const playerRounds = usePlayerRounds(playerId ?? '', roundsSort.sort);

  const playerData = player.data;
  const isOwnProfile = user?.playerId != null && user.playerId === playerId;
  const history = handicapHistory.data ?? [];
  const rounds = playerRounds.data ?? [];

  return (
    <div className="space-y-6">
      <Button variant="ghost" size="sm" asChild className="-ml-2">
        <Link to="/players">
          <ArrowLeft className="h-4 w-4 mr-1" />
          Players
        </Link>
      </Button>

      {player.isPending && <FullPageSpinner />}
      {player.isError && (
        <ErrorMessage message="Could not load player profile. Please try again." />
      )}

      {playerData && (
        <>
          <PageHeader
            title={playerData.fullName}
            description={playerData.flightName ?? undefined}
          >
            <Badge variant={playerData.isActive ? 'green' : 'secondary'}>
              {playerData.isActive ? 'Active' : 'Inactive'}
            </Badge>
          </PageHeader>

          {isOwnProfile && (
            <TeeTimePreferenceSelector
              playerId={parseInt(playerId!, 10)}
              currentMask={playerData.preferredTeeTimeSlots ?? 0}
            />
          )}

          {/* Stats cards */}
          <div className="grid gap-4 sm:grid-cols-3">
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-gray-500">
                  18-Hole Handicap
                </CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-3xl font-bold text-primary-900">
                  {playerData.currentHandicap?.toFixed(1) ?? '—'}
                </p>
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-gray-500">
                  9-Hole Handicap
                </CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-3xl font-bold text-primary-900">
                  {playerData.currentHandicap !== null && playerData.currentHandicap !== undefined
                    ? (playerData.currentHandicap / 2).toFixed(1)
                    : '—'}
                </p>
                <p className="text-xs text-gray-400 mt-1">applied for league rounds</p>
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-gray-500">
                  Flight
                </CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-lg font-semibold text-gray-900">
                  {playerData.flightName ?? '—'}
                </p>
              </CardContent>
            </Card>
          </div>
        </>
      )}

      {/* Handicap chart */}
      <section>
        <h2 className="text-lg font-semibold text-gray-900 mb-4">
          Handicap Trend
        </h2>
        {handicapHistory.isPending && <FullPageSpinner />}
        {handicapHistory.isError && (
          <ErrorMessage message="Could not load handicap history." />
        )}
        {!handicapHistory.isPending && !handicapHistory.isError && (
          <HandicapChart history={history} />
        )}
      </section>

      {/* Handicap history table */}
      <section>
        <h2 className="text-lg font-semibold text-gray-900 mb-4">
          Handicap History
        </h2>

        {history.length > 0 && (
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <Table>
              <TableHeader>
                <TableRow className="bg-gray-50">
                  <SortableTableHead column="date" sort={handicapSort.sort} onSort={handicapSort.cycle}>
                    Date
                  </SortableTableHead>
                  <SortableTableHead column="source" sort={handicapSort.sort} onSort={handicapSort.cycle}>
                    Source
                  </SortableTableHead>
                  <SortableTableHead column="index" sort={handicapSort.sort} onSort={handicapSort.cycle} className="text-right">
                    18-Hole
                  </SortableTableHead>
                  <SortableTableHead column="nineHole" sort={handicapSort.sort} onSort={handicapSort.cycle} className="text-right">
                    9-Hole
                  </SortableTableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {history.map((h, i) => (
                  <TableRow key={`${h.effectiveDate}-${i}`}>
                    <TableCell>{formatShortDate(h.effectiveDate)}</TableCell>
                    <TableCell className="text-gray-500">{h.source}</TableCell>
                    <TableCell className="text-right font-semibold tabular-nums">
                      {h.handicapIndex.toFixed(1)}
                    </TableCell>
                    <TableCell className="text-right tabular-nums text-gray-600">
                      {h.nineHoleHandicapIndex.toFixed(1)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
        {!handicapHistory.isPending && history.length === 0 && (
          <p className="text-gray-500 text-sm">No handicap history recorded yet.</p>
        )}
      </section>

      {/* Past rounds */}
      <section>
        <h2 className="text-lg font-semibold text-gray-900 mb-4">
          Past Rounds
        </h2>

        {playerRounds.isPending && <FullPageSpinner />}
        {playerRounds.isError && (
          <ErrorMessage message="Could not load round history." />
        )}

        {playerRounds.data && rounds.length > 0 && (
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <Table>
              <TableHeader>
                <TableRow className="bg-gray-50">
                  <SortableTableHead column="date" sort={roundsSort.sort} onSort={roundsSort.cycle}>
                    Date
                  </SortableTableHead>
                  <SortableTableHead column="course" sort={roundsSort.sort} onSort={roundsSort.cycle}>
                    Course
                  </SortableTableHead>
                  <SortableTableHead column="week" sort={roundsSort.sort} onSort={roundsSort.cycle} className="text-center">
                    Wk
                  </SortableTableHead>
                  <SortableTableHead column="status" sort={roundsSort.sort} onSort={roundsSort.cycle} className="text-center">
                    Status
                  </SortableTableHead>
                  <SortableTableHead column="gross" sort={roundsSort.sort} onSort={roundsSort.cycle} className="text-right">
                    Gross
                  </SortableTableHead>
                  <SortableTableHead column="net" sort={roundsSort.sort} onSort={roundsSort.cycle} className="text-right">
                    Net
                  </SortableTableHead>
                  <SortableTableHead column="differential" sort={roundsSort.sort} onSort={roundsSort.cycle} className="text-right">
                    18/9 Diff
                  </SortableTableHead>
                  <SortableTableHead column="grossPts" sort={roundsSort.sort} onSort={roundsSort.cycle} className="text-right">
                    Gross Pts
                  </SortableTableHead>
                  <SortableTableHead column="netPts" sort={roundsSort.sort} onSort={roundsSort.cycle} className="text-right">
                    Net Pts
                  </SortableTableHead>
                  
                </TableRow>
              </TableHeader>
              <TableBody>
                {rounds.map((r) => {
                  const statusNormalized = normalizeRoundStatus(r.status);
                  const statusVariant =
                    statusNormalized === 'Finalized'
                      ? 'green'
                      : statusNormalized === 'InProgress'
                        ? 'amber'
                        : statusNormalized === 'Scheduled'
                          ? 'blue'
                          : 'secondary';
                  return (
                    <TableRow key={r.roundId}>
                      <TableCell>
                        <Link
                          to={`/rounds/${r.roundId}`}
                          className="text-primary-900 hover:underline"
                        >
                          {formatShortDate(r.roundDate)}
                        </Link>
                      </TableCell>
                      <TableCell className="text-gray-700">
                        {r.courseName}{' '}
                        <span className="text-gray-400 text-xs">({r.nineHoleSide})</span>
                      </TableCell>
                      <TableCell className="text-center text-gray-500">{r.weekNumber}</TableCell>
                      <TableCell className="text-center">
                        <Badge variant={statusVariant}>{statusNormalized}</Badge>
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {r.totalGrossStrokes ?? '—'}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {r.totalNetStrokes ?? '—'}
                      </TableCell>
                      <TableCell className="text-right tabular-nums text-gray-600">
                        {r.scoreDifferential != null && r.nineHoleScoreDifferential != null
                          ? `${r.scoreDifferential.toFixed(1)}/${r.nineHoleScoreDifferential.toFixed(1)}`
                          : '—'}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {r.totalGrossStablefordPoints ?? '—'}
                      </TableCell>
                      <TableCell className="text-right font-semibold tabular-nums">
                        {r.totalNetStablefordPoints ?? '—'}
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </div>
        )}
        {!playerRounds.isPending && rounds.length === 0 && (
          <p className="text-gray-500 text-sm">No rounds played yet.</p>
        )}
      </section>
    </div>
  );
}
