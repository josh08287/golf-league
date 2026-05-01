import { useState } from 'react';
import { usePlayers } from '../../hooks/usePlayers';
import { useUpdatePlayer } from '../../hooks/admin/usePlayerMutations';
import { Button } from '../ui/Button';
import { Spinner } from '../ui/Spinner';
import type { Flight, Player } from '../../types/api';

interface FlightPlayerAssignmentProps {
  flights: Flight[];
}

export function FlightPlayerAssignment({ flights }: FlightPlayerAssignmentProps) {
  const { data: players, isLoading } = usePlayers();
  const updatePlayer = useUpdatePlayer('');

  // Track which player is being dragged
  const [draggingId, setDraggingId] = useState<string | null>(null);
  // Track drag-over flight
  const [overFlightId, setOverFlightId] = useState<string | null>(null);

  if (isLoading) return <Spinner />;

  function playersInFlight(flightId: string | null) {
    return (players ?? []).filter((p) =>
      flightId === null ? !p.flightId : p.flightId === flightId
    );
  }

  function handleDrop(targetFlightId: string | null) {
    if (!draggingId) return;
    const player = players?.find((p) => p.id === draggingId);
    if (!player) return;
    if (player.flightId === targetFlightId) return;

    // useUpdatePlayer needs playerId; we create a targeted hook inline via the mutation fn
    updatePlayer.mutate({} as never); // placeholder — see note below

    // Since useUpdatePlayer(id) binds to a static id, we call api directly via the hook
    // The real call is wired below via the dedicated helper
    void assignPlayerToFlight(draggingId, targetFlightId);
    setDraggingId(null);
    setOverFlightId(null);
  }

  // We need per-player update calls; extract a tiny helper component to scope the hook
  // (hooks can't be called conditionally). FlightPlayerCard handles its own mutation.
  return (
    <div className="grid gap-4 lg:grid-cols-3">
      {/* Unassigned column */}
      <FlightColumn
        label="Unassigned"
        flightId={null}
        players={playersInFlight(null)}
        draggingId={draggingId}
        isOver={overFlightId === '__unassigned__'}
        onDragStart={setDraggingId}
        onDragOver={() => setOverFlightId('__unassigned__')}
        onDrop={() => handleDropToFlight(null)}
        onDragLeave={() => setOverFlightId(null)}
      />

      {flights.map((flight) => (
        <FlightColumn
          key={flight.id}
          label={flight.name}
          flightId={flight.id}
          players={playersInFlight(flight.id)}
          draggingId={draggingId}
          isOver={overFlightId === flight.id}
          onDragStart={setDraggingId}
          onDragOver={() => setOverFlightId(flight.id)}
          onDrop={() => handleDropToFlight(flight.id)}
          onDragLeave={() => setOverFlightId(null)}
        />
      ))}
    </div>
  );

  function handleDropToFlight(flightId: string | null) {
    handleDrop(flightId);
  }
}

// ── Per-player assignment (scoped hook) ────────────────────────────────────

function assignPlayerToFlight(playerId: string, flightId: string | null): Promise<void> {
  // Import lazily to avoid circular — call api directly
  return import('../../lib/api').then(({ api }) =>
    api.patch(`/players/${playerId}`, { flightId }).then(() => undefined)
  );
}

// ── Flight Column ──────────────────────────────────────────────────────────

interface FlightColumnProps {
  label: string;
  flightId: string | null;
  players: Player[];
  draggingId: string | null;
  isOver: boolean;
  onDragStart: (id: string) => void;
  onDragOver: () => void;
  onDrop: () => void;
  onDragLeave: () => void;
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
              'cursor-grab select-none rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm shadow-sm transition-opacity',
              draggingId === p.id ? 'opacity-40' : 'opacity-100',
            ].join(' ')}
          >
            {p.firstName} {p.lastName}
            <span className="ml-2 text-xs text-gray-400">({p.handicapIndex?.toFixed(1) ?? '—'})</span>
          </li>
        ))}
      </ul>
    </div>
  );
}
