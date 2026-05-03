import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../../lib/api';
import { usePlayers, playerKeys } from '../../hooks/usePlayers';
import { Spinner } from '../ui/Spinner';
import type { Flight, Player } from '../../types/api';

interface FlightPlayerAssignmentProps {
  flights: Flight[];
}

export function FlightPlayerAssignment({ flights }: FlightPlayerAssignmentProps) {
  const qc = useQueryClient();
  const { data: playersPage, isLoading } = usePlayers();
  const players = playersPage?.data ?? [];

  const [draggingId, setDraggingId] = useState<number | null>(null);
  const [overFlightId, setOverFlightId] = useState<number | 'unassigned' | null>(null);

  if (isLoading) return <Spinner />;

  function playersInFlight(flightId: number | null) {
    return players.filter((p) =>
      flightId === null ? !p.flightId : p.flightId === flightId
    );
  }

  async function handleDropToFlight(flightId: number | null) {
    if (!draggingId) return;
    const player = players.find((p) => p.id === draggingId);
    if (!player || player.flightId === flightId) return;

    setDraggingId(null);
    setOverFlightId(null);

    await apiClient.patch(`/players/${draggingId}`, {
      flightId: flightId === null ? '' : String(flightId),
    });
    await qc.invalidateQueries({ queryKey: playerKeys.all });
  }

  return (
    <div className="grid gap-4 lg:grid-cols-3">
      <FlightColumn
        label="Unassigned"
        players={playersInFlight(null)}
        draggingId={draggingId}
        isOver={overFlightId === 'unassigned'}
        onDragStart={(id) => setDraggingId(id)}
        onDragOver={() => setOverFlightId('unassigned')}
        onDrop={() => void handleDropToFlight(null)}
        onDragLeave={() => setOverFlightId(null)}
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
          onDrop={() => void handleDropToFlight(f.id)}
          onDragLeave={() => setOverFlightId(null)}
        />
      ))}
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
            {p.fullName}
            <span className="ml-2 text-xs text-gray-400">({p.currentHandicap?.toFixed(1) ?? '—'})</span>
          </li>
        ))}
      </ul>
    </div>
  );
}
