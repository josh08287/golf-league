import { useMemo, useState } from 'react';
import { Lock, RefreshCw, Trash2, Users, CalendarRange } from 'lucide-react';
import { useFlights, useFlightMatches } from '../../hooks/useFlights';
import { useAllPlayers } from '../../hooks/usePlayers';
import { useSeasons } from '../../hooks/useSeasons';
import { useDeleteFlight, useInitializeHalfFlights, useGenerateMatchPlaySchedule } from '../../hooks/admin/useFlightMutations';
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

function MatchScheduleSection({ half, flights, locked }: { half: SeasonHalf; flights: Flight[]; locked: boolean }) {
  const { data: matches, isLoading } = useFlightMatches(String(half.id));
  const generateSchedule = useGenerateMatchPlaySchedule();
  const [warnings, setWarnings] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);

  async function handleGenerate() {
    setError(null);
    setWarnings([]);
    try {
      const result = await generateSchedule.mutateAsync({ halfId: half.id });
      setWarnings(result.warnings);
    } catch {
      setError('Failed to generate the match schedule. Try again.');
    }
  }

  const flightNameById = new Map(flights.map((f) => [f.id, f.name]));
  const matchesByWeek = new Map<number, typeof matches>();
  for (const m of matches ?? []) {
    if (!matchesByWeek.has(m.weekNumber)) matchesByWeek.set(m.weekNumber, []);
    matchesByWeek.get(m.weekNumber)!.push(m);
  }
  const weeks = [...matchesByWeek.keys()].sort((a, b) => a - b);

  return (
    <div className="rounded-lg border border-gray-200 bg-white p-4 space-y-3">
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-gray-900 flex items-center gap-1.5">
          <CalendarRange className="h-4 w-4" />
          Match Schedule
        </h3>
        {!locked && flights.length > 0 && (
          <Button
            variant="secondary"
            size="sm"
            onClick={handleGenerate}
            disabled={generateSchedule.isPending}
          >
            <RefreshCw className={`mr-1.5 h-3.5 w-3.5 ${generateSchedule.isPending ? 'animate-spin' : ''}`} />
            {matches && matches.length > 0 ? 'Regenerate Schedule' : 'Generate Schedule'}
          </Button>
        )}
      </div>

      {flights.length === 0 && (
        <p className="text-sm text-gray-400">Set up flights first before generating a match schedule.</p>
      )}

      {error && <p className="text-sm text-red-600">{error}</p>}
      {warnings.length > 0 && (
        <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 space-y-1">
          {warnings.map((w, i) => (
            <p key={i} className="text-xs text-amber-800">{w}</p>
          ))}
        </div>
      )}

      {isLoading && <Spinner />}

      {!isLoading && weeks.length === 0 && flights.length > 0 && (
        <p className="text-sm text-gray-400">No matches scheduled yet.</p>
      )}

      {weeks.length > 0 && (
        <div className="space-y-3">
          {weeks.map((week) => (
            <div key={week}>
              <p className="mb-1 text-xs font-medium uppercase tracking-wide text-gray-500">Week {week}</p>
              <div className="grid gap-1.5 sm:grid-cols-2">
                {matchesByWeek.get(week)!.map((m) => (
                  <div key={m.id} className="rounded border border-gray-100 bg-gray-50 px-3 py-1.5 text-sm">
                    <span className="text-xs text-gray-400 mr-2">{flightNameById.get(m.flightId) ?? ''}</span>
                    {m.player2Id === null ? (
                      <span className="text-gray-700">{m.player1FullName} <span className="text-gray-400 italic">— BYE</span></span>
                    ) : (
                      <span className="text-gray-700">
                        {m.player1FullName} vs {m.player2FullName}
                        {m.player1Points != null && (
                          <span className="ml-2 text-xs font-medium text-gray-500">
                            {m.player1Points}-{m.player2Points}
                          </span>
                        )}
                      </span>
                    )}
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
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
          Rounds have started for this half. You can still reassign players between flights below —
          just remember to recalculate all rounds afterward so standings and skins stay accurate.
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

      {flights.length > 0 && (
        <div>
          <p className="mb-3 text-sm text-gray-500">
            Drag players between columns to adjust flight assignments for {half.name}.
          </p>
          <FlightPlayerAssignment halfId={half.id} flights={flights} />
        </div>
      )}

      {half.scoringFormat === 'matchPlay' && (
        <MatchScheduleSection half={half} flights={flights} locked={locked} />
      )}
    </section>
  );
}

export function FlightsPage() {
  const { data: flightsPage, isLoading, error } = useFlights();
  const flights = flightsPage?.data ?? [];
  const { data: playersPage } = useAllPlayers();
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
