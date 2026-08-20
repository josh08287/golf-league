import { useMemo, useState, useCallback, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ArrowUpDown, Trophy, X } from 'lucide-react';
import { useSeasons } from '../../hooks/useSeasons';
import { usePlayers, useSubstitutes } from '../../hooks/usePlayers';
import { useCreateTournamentRound } from '../../hooks/admin/useRoundMutations';
import type { MatchupInput } from '../../hooks/admin/useRoundMutations';
import { useCourseDetail } from '../../hooks/admin/useCourseMutations';
import { api } from '../../lib/api';
import { Button } from '../ui/Button';
import { FormField, inputClass, selectClass } from './FormField';
import type { Course, Player } from '../../types/api';

interface CreateTournamentRoundFormProps {
  onSuccess: (roundId: number) => void;
  onCancel: () => void;
}

interface SelectedPlayer {
  id: number;
  fullName: string;
  currentHandicap: number | null;
}

export function CreateTournamentRoundForm({ onSuccess, onCancel }: CreateTournamentRoundFormProps) {
  const createTournament = useCreateTournamentRound();
  const { data: seasons } = useSeasons();
  const activeSeason = useMemo(() => seasons?.find((s) => s.isActive) ?? null, [seasons]);

  const { data: playersPage, isLoading: playersLoading } = usePlayers(1, undefined, 200);
  const { data: substitutes } = useSubstitutes();
  // Tournament rounds aren't half-scoped, so every active regular player is
  // eligible, plus every substitute (active or not — deactivated subs remain
  // sub-eligible).
  const allPlayers: Player[] = [
    ...(playersPage?.data?.filter((p) => p.isActive) ?? []),
    ...(substitutes ?? []),
  ];

  const { data: coursesPage } = useQuery<{ data: Course[] }>({
    queryKey: ['courses'],
    queryFn: () => api.get('/courses').then((r) => r.data),
  });
  const courses = coursesPage?.data ?? [];

  const [courseId, setCourseId] = useState('');
  const [roundDate, setRoundDate] = useState('');
  const [notes, setNotes] = useState('');
  const [longestDriveHoleNumber, setLongestDriveHoleNumber] = useState('');
  const [grossSkinsPool, setGrossSkinsPool] = useState('');
  const [netSkinsPool, setNetSkinsPool] = useState('');
  const [selectedPlayers, setSelectedPlayers] = useState<SelectedPlayer[]>([]);
  const [matchups, setMatchups] = useState<MatchupInput[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [playersInitialized, setPlayersInitialized] = useState(false);

  const { data: selectedCourse } = useCourseDetail(courseId || undefined);
  const nonPar3Holes = (selectedCourse?.holeDetails ?? [])
    .filter((h) => h.par !== 3)
    .sort((a, b) => a.holeNumber - b.holeNumber);

  // Auto-generate default matchups whenever selected players change
  const regenerateMatchups = useCallback((players: SelectedPlayer[]) => {
    const sorted = [...players].sort(
      (a, b) => (a.currentHandicap ?? 99) - (b.currentHandicap ?? 99),
    );
    const pairs: MatchupInput[] = [];
    for (let i = 0; i + 1 < sorted.length; i += 2) {
      pairs.push({ player1Id: sorted[i].id, player2Id: sorted[i + 1].id });
    }
    setMatchups(pairs);
  }, []);

  // Pre-populate regular active players once the list loads. Substitutes are
  // never auto-selected — the admin adds them explicitly via the "Add
  // player" dropdown below, same as any removed/inactive player.
  const regularActivePlayers = playersPage?.data?.filter((p) => p.isActive) ?? [];
  useEffect(() => {
    if (playersInitialized || regularActivePlayers.length === 0) return;
    const initial = regularActivePlayers.map((p) => ({
      id: p.id,
      fullName: p.fullName,
      currentHandicap: p.currentHandicap,
    }));
    setSelectedPlayers(initial);
    regenerateMatchups(initial);
    setPlayersInitialized(true);
  }, [regularActivePlayers, playersInitialized, regenerateMatchups]);

  function addPlayer(playerId: number) {
    const player = allPlayers.find((p) => p.id === playerId);
    if (!player || selectedPlayers.some((sp) => sp.id === playerId)) return;
    const next = [
      ...selectedPlayers,
      { id: player.id, fullName: player.fullName, currentHandicap: player.currentHandicap },
    ];
    setSelectedPlayers(next);
    regenerateMatchups(next);
  }

  function removePlayer(playerId: number) {
    const next = selectedPlayers.filter((p) => p.id !== playerId);
    setSelectedPlayers(next);
    regenerateMatchups(next);
  }

  function swapMatchupPlayers(matchupIndex: number) {
    setMatchups((prev) =>
      prev.map((m, i) =>
        i === matchupIndex ? { player1Id: m.player2Id, player2Id: m.player1Id } : m,
      ),
    );
  }

  function setMatchupPlayer(matchupIndex: number, slot: 1 | 2, playerId: number) {
    setMatchups((prev) =>
      prev.map((m, i) => {
        if (i !== matchupIndex) return m;
        return slot === 1 ? { ...m, player1Id: playerId } : { ...m, player2Id: playerId };
      }),
    );
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);

    if (!courseId || !roundDate) {
      setError('Course and date are required.');
      return;
    }
    if (selectedPlayers.length < 2) {
      setError('Select at least 2 players.');
      return;
    }
    if ((grossSkinsPool && Number(grossSkinsPool) < 0) || (netSkinsPool && Number(netSkinsPool) < 0)) {
      setError('Skins pool amounts cannot be negative.');
      return;
    }

    const season = activeSeason;
    if (!season) {
      setError('No active season found.');
      return;
    }

    try {
      const result = await createTournament.mutateAsync({
        seasonId: season.id,
        courseId: Number(courseId),
        roundDate,
        playerIds: selectedPlayers.map((p) => p.id),
        matchups: matchups.length > 0 ? matchups : undefined,
        notes: notes || undefined,
        longestDriveHoleNumber: longestDriveHoleNumber ? Number(longestDriveHoleNumber) : undefined,
        grossSkinsPool: grossSkinsPool ? Number(grossSkinsPool) : undefined,
        netSkinsPool: netSkinsPool ? Number(netSkinsPool) : undefined,
      });
      onSuccess(result.round?.id ?? 0);
    } catch {
      setError('Failed to create tournament round.');
    }
  }

  const availablePlayers = allPlayers.filter((p) => !selectedPlayers.some((sp) => sp.id === p.id));

  return (
    <form onSubmit={handleSubmit} className="space-y-5">
      {/* Season */}
      <FormField label="Season">
        {seasons !== undefined && !activeSeason ? (
          <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
            No active season found.{' '}
            <Link to="/admin/seasons" className="font-medium underline hover:text-amber-900">
              Go to Seasons
            </Link>{' '}
            to create or activate one.
          </div>
        ) : (
          <input
            readOnly
            value={activeSeason ? String(activeSeason.year) : 'Loading…'}
            className={inputClass + ' bg-gray-50 text-gray-500'}
          />
        )}
      </FormField>

      {/* Date */}
      <FormField label="Tournament Date" required>
        <input
          type="date"
          value={roundDate}
          onChange={(e) => setRoundDate(e.target.value)}
          className={inputClass}
        />
      </FormField>

      {/* Course */}
      <FormField label="Course" required>
        <select value={courseId} onChange={(e) => setCourseId(e.target.value)} className={selectClass}>
          <option value="">— Select course —</option>
          {courses.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
      </FormField>

      {/* Notes */}
      <FormField label="Notes">
        <input
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
          className={inputClass}
          placeholder="Optional"
        />
      </FormField>

      {/* Longest Drive Hole */}
      <FormField label="Longest Drive Hole">
        <select
          value={longestDriveHoleNumber}
          onChange={(e) => setLongestDriveHoleNumber(e.target.value)}
          className={selectClass}
          disabled={!courseId}
        >
          <option value="">— None —</option>
          {nonPar3Holes.map((h) => (
            <option key={h.holeNumber} value={h.holeNumber}>
              Hole {h.holeNumber} (Par {h.par})
            </option>
          ))}
        </select>
        <p className="mt-1 text-xs text-gray-400">
          Players record the longest-drive winner for their flight during score entry on this hole.
        </p>
      </FormField>

      {/* Skins Pools */}
      <div className="grid grid-cols-2 gap-3">
        <FormField label="Gross Skins Pool ($)">
          <input
            type="number"
            min="0"
            step="1"
            value={grossSkinsPool}
            onChange={(e) => setGrossSkinsPool(e.target.value)}
            className={inputClass}
            placeholder="Optional"
          />
        </FormField>
        <FormField label="Net Skins Pool ($)">
          <input
            type="number"
            min="0"
            step="1"
            value={netSkinsPool}
            onChange={(e) => setNetSkinsPool(e.target.value)}
            className={inputClass}
            placeholder="Optional"
          />
        </FormField>
      </div>

      {/* Player Selection */}
      <div>
        <div className="mb-1 flex items-center justify-between">
          <label className="block text-sm font-medium text-gray-700">
            Players <span className="text-red-500">*</span>
            {playersLoading && (
              <span className="ml-2 text-xs font-normal text-gray-400">Loading…</span>
            )}
          </label>
          <span className="text-xs text-gray-400">{selectedPlayers.length} selected</span>
        </div>
        {availablePlayers.length > 0 && (
          <div className="flex gap-2">
            <select
              className={selectClass}
              defaultValue=""
              onChange={(e) => {
                const val = Number(e.target.value);
                if (val) addPlayer(val);
                e.target.value = '';
              }}
            >
              <option value="">+ Add player or substitute…</option>
              {availablePlayers.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.fullName}
                  {p.isSubstitute ? ' (Sub)' : ''} (HCP {p.currentHandicap?.toFixed(1) ?? '—'})
                </option>
              ))}
            </select>
          </div>
        )}
        {selectedPlayers.length > 0 && (
          <ul className="mt-2 max-h-48 space-y-1 overflow-y-auto rounded-md border border-gray-100 p-1">
            {selectedPlayers
              .slice()
              .sort((a, b) => (a.currentHandicap ?? 99) - (b.currentHandicap ?? 99))
              .map((p) => (
                <li
                  key={p.id}
                  className="flex items-center justify-between rounded-md border border-gray-200 bg-gray-50 px-3 py-1.5 text-sm"
                >
                  <span>
                    {p.fullName}{' '}
                    <span className="text-gray-500">(HCP {p.currentHandicap?.toFixed(1) ?? '—'})</span>
                  </span>
                  <button
                    type="button"
                    onClick={() => removePlayer(p.id)}
                    className="text-gray-400 hover:text-red-500"
                  >
                    <X className="h-4 w-4" />
                  </button>
                </li>
              ))}
          </ul>
        )}
      </div>

      {/* Matchup Editor */}
      {matchups.length > 0 && (
        <div>
          <div className="mb-2 flex items-center gap-2">
            <Trophy className="h-4 w-4 text-amber-500" />
            <span className="text-sm font-medium text-gray-700">Matchups</span>
            <span className="text-xs text-gray-400">(drag players between slots to re-pair)</span>
          </div>
          <div className="max-h-48 space-y-2 overflow-y-auto rounded-md border border-gray-100 p-1">
            {matchups.map((m, idx) => (
              <div
                key={idx}
                className="flex items-center gap-2 rounded-md border border-gray-200 bg-white p-2"
              >
                <span className="w-5 text-center text-xs font-semibold text-gray-400">
                  {idx + 1}
                </span>
                <select
                  value={m.player1Id}
                  onChange={(e) => setMatchupPlayer(idx, 1, Number(e.target.value))}
                  className="flex-1 rounded border border-gray-300 px-2 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-green-600"
                >
                  {selectedPlayers.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.fullName}
                    </option>
                  ))}
                </select>
                <span className="text-xs font-semibold text-gray-500">vs</span>
                <select
                  value={m.player2Id}
                  onChange={(e) => setMatchupPlayer(idx, 2, Number(e.target.value))}
                  className="flex-1 rounded border border-gray-300 px-2 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-green-600"
                >
                  {selectedPlayers.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.fullName}
                    </option>
                  ))}
                </select>
                <button
                  type="button"
                  title="Swap players"
                  onClick={() => swapMatchupPlayers(idx)}
                  className="rounded p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-700"
                >
                  <ArrowUpDown className="h-4 w-4" />
                </button>
              </div>
            ))}
            {selectedPlayers.length % 2 !== 0 && (
              <p className="text-xs text-amber-600">
                Odd number of players — one player will not have a matchup.
              </p>
            )}
          </div>
        </div>
      )}

      {error && <p className="text-sm text-red-600">{error}</p>}

      <div className="flex justify-end gap-3 pt-2">
        <Button type="button" variant="ghost" onClick={onCancel}>
          Cancel
        </Button>
        <Button
          type="submit"
          variant="primary"
          disabled={createTournament.isPending}
        >
          {createTournament.isPending ? 'Creating…' : 'Create Tournament Round'}
        </Button>
      </div>
    </form>
  );
}
