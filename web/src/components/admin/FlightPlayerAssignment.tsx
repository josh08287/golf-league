import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Save, Undo2 } from 'lucide-react';
import { apiClient } from '../../lib/api';
import { usePlayers, playerKeys } from '../../hooks/usePlayers';
import { useRecalculateRounds } from '../../hooks/admin/useRecalculateRounds';
import { Spinner } from '../ui/Spinner';
import { Button } from '../ui/Button';
import { ConfirmDialog } from './ConfirmDialog';
import { formatHandicapPair, HANDICAP_PAIR_TOOLTIP } from '../../lib/utils';
import type { Flight, Player } from '../../types/api';

interface FlightPlayerAssignmentProps {
  halfId: number;
  flights: Flight[];
}

export function FlightPlayerAssignment({ halfId, flights }: FlightPlayerAssignmentProps) {
  const qc = useQueryClient();
  // Fetch the full roster in one page so drag-and-drop sees every player,
  // not just the first 20. Large size handles any realistic league size.
  const { data: playersPage, isLoading } = usePlayers(1, undefined, 1000);
  const players = playersPage?.data ?? [];

  const [draggingId, setDraggingId] = useState<number | null>(null);
  const [overFlightId, setOverFlightId] = useState<number | 'unassigned' | null>(null);
  // Pending, unsaved moves: playerId -> new flightId (null = unassigned).
  const [pendingMoves, setPendingMoves] = useState<Map<number, number | null>>(new Map());
  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [showRecalculatePrompt, setShowRecalculatePrompt] = useState(false);

  const recalculateRounds = useRecalculateRounds();

  const dirtyCount = pendingMoves.size;

  if (isLoading) return <Spinner />;

  // Resolve each player's flight for THIS half from the per-half memberships array
  function committedFlightIdForHalf(player: Player): number | null {
    return player.flightMemberships?.find((m) => m.halfId === halfId)?.flightId ?? null;
  }

  function flightIdForHalf(player: Player): number | null {
    return pendingMoves.has(player.id) ? pendingMoves.get(player.id)! : committedFlightIdForHalf(player);
  }

  function playersInFlight(flightId: number | null) {
    return players.filter((p) => flightIdForHalf(p) === flightId);
  }

  function handleDropToFlight(flightId: number | null) {
    if (!draggingId) return;
    const player = players.find((p) => p.id === draggingId);
    setDraggingId(null);
    setOverFlightId(null);
    if (!player || flightIdForHalf(player) === flightId) return;

    setPendingMoves((prev) => {
      const next = new Map(prev);
      if (committedFlightIdForHalf(player) === flightId) {
        // Back to the original assignment — no longer a pending change.
        next.delete(player.id);
      } else {
        next.set(player.id, flightId);
      }
      return next;
    });
  }

  function handleDiscard() {
    setPendingMoves(new Map());
    setSaveError(null);
  }

  async function handleSave() {
    setIsSaving(true);
    setSaveError(null);
    try {
      await Promise.all(
        Array.from(pendingMoves.entries()).map(([playerId, flightId]) =>
          apiClient.patch(`/players/${playerId}`, { flightId: flightId === null ? '' : String(flightId) }),
        ),
      );
      setPendingMoves(new Map());
      await qc.invalidateQueries({ queryKey: playerKeys.all });
      setShowRecalculatePrompt(true);
    } catch {
      setSaveError('Failed to save flight assignments. Try again.');
    } finally {
      setIsSaving(false);
    }
  }

  async function handleConfirmRecalculate() {
    await recalculateRounds.mutateAsync();
    setShowRecalculatePrompt(false);
  }

  return (
    <div className="space-y-3">
      {dirtyCount > 0 && (
        <div className="flex items-center justify-between rounded-lg border border-amber-200 bg-amber-50 px-4 py-3">
          <p className="text-sm text-amber-800">
            {dirtyCount} unsaved flight {dirtyCount === 1 ? 'change' : 'changes'}
          </p>
          <div className="flex gap-2">
            <Button variant="ghost" size="sm" onClick={handleDiscard} disabled={isSaving}>
              <Undo2 className="mr-1.5 h-3.5 w-3.5" />
              Discard
            </Button>
            <Button variant="primary" size="sm" onClick={() => void handleSave()} disabled={isSaving}>
              <Save className="mr-1.5 h-3.5 w-3.5" />
              {isSaving ? 'Saving...' : 'Save Changes'}
            </Button>
          </div>
        </div>
      )}

      {saveError && <p className="text-sm text-red-600">{saveError}</p>}

      <div className="grid gap-4 lg:grid-cols-3">
        <FlightColumn
          label="Unassigned"
          players={playersInFlight(null)}
          draggingId={draggingId}
          isOver={overFlightId === 'unassigned'}
          onDragStart={(id) => setDraggingId(id)}
          onDragOver={() => setOverFlightId('unassigned')}
          onDrop={() => handleDropToFlight(null)}
          onDragLeave={() => setOverFlightId(null)}
          isPending={(id) => pendingMoves.has(id)}
        />
        {flights.map((f) => (
          <FlightColumn
            key={f.id}
            label={f.name}
            players={playersInFlight(f.id)}
            draggingId={draggingId}
            isOver={overFlightId === f.id}
            onDragStart={(id) => setDraggingId(id)}
            onDragOver={() => setOverFlightId(f.id)}
            onDrop={() => handleDropToFlight(f.id)}
            onDragLeave={() => setOverFlightId(null)}
            isPending={(id) => pendingMoves.has(id)}
          />
        ))}
      </div>

      <ConfirmDialog
        open={showRecalculatePrompt}
        title="Recalculate All Rounds?"
        description="Flight assignments were saved. Since flights affect standings and skins for past rounds, it's recommended to recalculate all rounds now so results reflect the updated assignments."
        confirmLabel={recalculateRounds.isPending ? 'Recalculating...' : 'Recalculate All Rounds'}
        cancelLabel="Skip for Now"
        isLoading={recalculateRounds.isPending}
        onConfirm={() => void handleConfirmRecalculate()}
        onCancel={() => setShowRecalculatePrompt(false)}
      />
    </div>
  );
}

interface FlightColumnProps {
  label: string;
  players: Player[];
  draggingId: number | null;
  isOver: boolean;
  onDragStart: (id: number) => void;
  onDragOver: () => void;
  onDrop: () => void;
  onDragLeave: () => void;
  isPending: (id: number) => boolean;
}

function FlightColumn({
  label,
  players,
  draggingId,
  isOver,
  onDragStart,
  onDragOver,
  onDrop,
  onDragLeave,
  isPending,
}: FlightColumnProps) {
  return (
    <div
      className={[
        'min-h-[200px] rounded-xl border-2 border-dashed p-3 transition-colors',
        isOver ? 'border-[#1B5E20] bg-green-50' : 'border-gray-200 bg-gray-50',
      ].join(' ')}
      onDragOver={(e) => {
        e.preventDefault();
        onDragOver();
      }}
      onDrop={(e) => {
        e.preventDefault();
        onDrop();
      }}
      onDragLeave={onDragLeave}
    >
      <p className="mb-2 text-xs font-semibold uppercase tracking-wider text-gray-500">
        {label} ({players.length})
      </p>
      <ul className="space-y-1">
        {players.map((p) => (
          <li
            key={p.id}
            draggable
            onDragStart={() => onDragStart(p.id)}
            className={[
              'cursor-grab select-none rounded-lg border bg-white px-3 py-2 text-sm shadow-sm transition-opacity',
              isPending(p.id) ? 'border-amber-300 ring-1 ring-amber-300' : 'border-gray-200',
              draggingId === p.id ? 'opacity-40' : 'opacity-100',
            ].join(' ')}
          >
            {p.fullName}
            <span className="ml-2 text-xs text-gray-400" title={HANDICAP_PAIR_TOOLTIP}>
              ({formatHandicapPair(p.currentHandicap)})
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}
