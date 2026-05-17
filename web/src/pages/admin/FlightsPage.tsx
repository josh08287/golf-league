import { useMemo, useState } from 'react';
import { Lock, RefreshCw, Trash2, Users } from 'lucide-react';
import { useFlights } from '../../hooks/useFlights';
import { usePlayers } from '../../hooks/usePlayers';
import { useSeasons } from '../../hooks/useSeasons';
import { useDeleteFlight, useInitializeHalfFlights } from '../../hooks/admin/useFlightMutations';
import { PageHeader } from '../../components/ui/PageHeader';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { Spinner } from '../../components/ui/Spinner';
import { ErrorMessage } from '../../components/ui/ErrorMessage';
import { ConfirmDialog } from '../../components/admin/ConfirmDialog';
import { FlightPlayerAssignment } from '../../components/admin/FlightPlayerAssignment';
import type { Flight, Player, SeasonHalf } from '../../types/api';

interface FlightCardProps {
  flight: Flight;
  playerCount: number;
  locked: boolean;
  onDelete: (flight: Flight) => void;
}

function FlightCard({ flight, playerCount, locked, onDelete }: FlightCardProps) {
  return (
    <Card className="p-5">
      <div className="flex items-start justify-between">
        <div className="flex-1">
          <h3 className="font-semibold text-gray-900">{flight.name}</h3>
        </div>
        <div className="flex items-center gap-2">
          <div className="flex items-center gap-1 rounded-full bg-green-50 px-2.5 py-1 text-xs font-medium text-[#1B5E20]">
            <Users className="h-3.5 w-3.5" />
            {playerCount}
          </div>
          {!locked && (
            <Button
              variant="ghost"
              size="sm"
              className="text-red-600 hover:bg-red-50 hover:text-red-700"
              onClick={(e) => {
                e.stopPropagation();
                onDelete(flight);
              }}
              title="Delete flight"
            >
              <Trash2 className="h-4 w-4" />
            </Button>
          )}
        </div>
      </div>
    </Card>
  );
}

interface HalfSectionProps {
  half: SeasonHalf;
  flights: Flight[];
  players: Player[];
  locked: boolean;
  onDelete: (flight: Flight) => void;
  onInitialize: (halfId: number) => void;
  initializing: boolean;
}

function HalfSection({ half, flights, players, locked, onDelete, onInitialize, initializing }: HalfSectionProps) {
  function playerCountForFlight(flightId: number) {
    return players.filter((p) =>
      (p.flightMemberships ?? []).some((m) => m.halfId === half.id && m.flightId === flightId),
    ).length;
  }

  return (
    <section className="space-y-4">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <h2 className="text-sm font-semibold uppercase tracking-wider text-gray-500">
            {half.name}
          </h2>
          {locked && (
            <span className="flex items-center gap-1 rounded-full bg-amber-50 px-2 py-0.5 text-xs font-medium text-amber-700">
              <Lock className="h-3 w-3" />
              Locked
            </span>
          )}
        </div>
        {!locked && (
          <Button
            variant="secondary"
            size="sm"
            onClick={() => onInitialize(half.id)}
            disabled={initializing}
            title="Auto-assign players to flights by handicap (lowest → A flight, max 8 per flight)"
          >
            <RefreshCw className={`mr-1.5 h-3.5 w-3.5 ${initializing ? 'animate-spin' : ''}`} />
            {flights.length === 0 ? 'Set Up Flights' : 'Re-initialize Flights'}
          </Button>
        )}
      </div>

      {locked && (
        <p className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
          Flight assignments are locked because rounds have started for this half. Assignments cannot be changed until a new half is created.
        </p>
      )}

      {flights.length === 0 && !locked && (
        <p className="text-sm text-gray-400">
          No flights yet. Click <strong>Set Up Flights</strong> to auto-assign players by handicap.
        </p>
      )}

      {flights.length > 0 && (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {flights.map((f) => (
            <FlightCard
              key={f.id}
              flight={f}
              playerCount={playerCountForFlight(f.id)}
              locked={locked}
              onDelete={onDelete}
            />
          ))}
        </div>
      )}

      {flights.length > 0 && !locked && (
        <div>
          <p className="mb-3 text-sm text-gray-500">
            Drag players between columns to adjust flight assignments for {half.name}. Assignments lock once the first round starts.
          </p>
          <FlightPlayerAssignment halfId={half.id} flights={flights} />
        </div>
      )}

      {flights.length > 0 && locked && (
        <div>
          <p className="mb-3 text-sm font-medium text-gray-600">Current assignments:</p>
          <div className="grid gap-4 lg:grid-cols-3">
            {flights.map((f) => {
              const members = players.filter((p) =>
                (p.flightMemberships ?? []).some(
                  (m) => m.halfId === half.id && m.flightId === f.id,
                ),
              );
              return (
                <div key={f.id} className="rounded-xl border border-gray-200 bg-gray-50 p-3">
                  <p className="mb-2 text-xs font-semibold uppercase tracking-wider text-gray-500">
                    {f.name} ({members.length})
                  </p>
                  <ul className="space-y-1">
                    {members.map((p) => (
                      <li
                        key={p.id}
                        className="rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm text-gray-700"
                      >
                        {p.fullName}
                        <span className="ml-2 text-xs text-gray-400">
                          ({p.currentHandicap?.toFixed(1) ?? '—'})
                        </span>
                      </li>
                    ))}
                    {members.length === 0 && (
                      <li className="text-xs text-gray-400 italic">No players assigned</li>
                    )}
                  </ul>
                </div>
              );
            })}
          </div>
        </div>
      )}
    </section>
  );
}

export function FlightsPage() {
  const { data: flightsPage, isLoading, error } = useFlights();
  const flights = flightsPage?.data ?? [];
  const { data: playersPage } = usePlayers(1, undefined, 1000);
  const players = playersPage?.data ?? [];
  const { data: seasons } = useSeasons();
  const activeSeason = useMemo(() => seasons?.find((s) => s.isActive) ?? null, [seasons]);
  const halves = activeSeason?.halves ?? [];

  const [deleteTarget, setDeleteTarget] = useState<Flight | null>(null);
  const [initializingHalfId, setInitializingHalfId] = useState<number | null>(null);
  const [initError, setInitError] = useState<string | null>(null);

  const deleteFlight = useDeleteFlight();
  const initializeFlights = useInitializeHalfFlights();

  async function handleDelete() {
    if (!deleteTarget) return;
    await deleteFlight.mutateAsync(String(deleteTarget.id));
    setDeleteTarget(null);
  }

  async function handleInitialize(halfId: number) {
    setInitializingHalfId(halfId);
    setInitError(null);
    try {
      await initializeFlights.mutateAsync({ halfId, maxPlayersPerFlight: 8 });
    } catch {
      setInitError('Failed to initialize flights. Try again.');
    } finally {
      setInitializingHalfId(null);
    }
  }

  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Spinner />
      </div>
    );
  }

  if (error) {
    return <ErrorMessage message="Failed to load flights." />;
  }

  const flightsByHalf = halves.map((h) => {
    const halfFlights = flights.filter((f) => f.halfId === h.id);
    const locked = halfFlights.length > 0 ? halfFlights[0].isLocked : false;
    return { half: h, flights: halfFlights, locked };
  });

  return (
    <div className="space-y-8">
      <PageHeader title="Flights" />

      {halves.length === 0 && (
        <p className="text-sm text-gray-500">
          No active season with halves. Create a season first.
        </p>
      )}

      {initError && <p className="text-sm text-red-600">{initError}</p>}

      {flightsByHalf.map(({ half, flights: halfFlights, locked }) => (
        <HalfSection
          key={half.id}
          half={half}
          flights={halfFlights}
          players={players}
          locked={locked}
          onDelete={setDeleteTarget}
          onInitialize={handleInitialize}
          initializing={initializingHalfId === half.id}
        />
      ))}

      <ConfirmDialog
        open={!!deleteTarget}
        title="Delete Flight"
        description={`Permanently delete ${deleteTarget?.name}? This will unassign all players from this flight and cannot be undone.`}
        confirmLabel="Delete"
        destructive
        onConfirm={handleDelete}
        onCancel={() => setDeleteTarget(null)}
      />
    </div>
  );
}
