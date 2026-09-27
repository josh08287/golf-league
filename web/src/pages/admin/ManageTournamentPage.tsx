import { useEffect, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ArrowLeft,
  ArrowUpDown,
  Loader2,
  Plus,
  Trash2,
  Trophy,
  Users,
  X,
} from 'lucide-react';
import { api } from '../../lib/api';
import { useAllPlayers, useSubstitutes } from '../../hooks/usePlayers';
import { useRound, useTournamentResults } from '../../hooks/useRounds';
import {
  useAddTournamentParticipants,
  useRemoveTournamentParticipant,
  useRegenerateTournamentMatchups,
  useSetTournamentLongestDriveHole,
  useSetTournamentMatchups,
  useSetTournamentSkinsPool,
} from '../../hooks/admin/useRoundMutations';
import type { MatchupInput } from '../../hooks/admin/useRoundMutations';
import { useCourseDetail } from '../../hooks/admin/useCourseMutations';
import { useLeaguePrefix } from '@/context/LeagueContext';
import { formatDate } from '@/lib/utils';
import { isRoundFinalized, isRoundScheduled } from '../../lib/enumUtils';
import { Button } from '../../components/ui/Button';
import { Spinner } from '../../components/ui/Spinner';
import { FormField, inputClass, selectClass } from '../../components/admin/FormField';
import type { Participant } from '../../types/api';

export function ManageTournamentPage() {
  const { id } = useParams<{ id: string }>();
  const roundId = id ?? '';
  const prefix = useLeaguePrefix();
  const qc = useQueryClient();
  const [error, setError] = useState<string | null>(null);

  const { data: round, isLoading: roundLoading } = useRound(roundId);

  const { data: participants, isLoading: participantsLoading } = useQuery<Participant[]>({
    queryKey: ['rounds', roundId, 'participants'],
    queryFn: () => api.get(`/rounds/${roundId}/participants`).then((r) => r.data),
    enabled: !!roundId,
  });

  const { data: playersPage } = useAllPlayers();
  const { data: substitutes } = useSubstitutes();
  const allPlayers = [
    ...(playersPage?.data?.filter((p) => p.isActive) ?? []),
    ...(substitutes ?? []),
  ];

  const addParticipants = useAddTournamentParticipants(roundId);
  const removeParticipant = useRemoveTournamentParticipant(roundId);
  const setLongestDriveHole = useSetTournamentLongestDriveHole(roundId);
  const setMatchups = useSetTournamentMatchups(roundId);
  const regenerateMatchups = useRegenerateTournamentMatchups(roundId);
  const setSkinsPool = useSetTournamentSkinsPool(roundId);

  const skinsPoolLocked = isRoundFinalized(round?.status);
  const [grossSkinsPool, setGrossSkinsPoolInput] = useState('');
  const [netSkinsPool, setNetSkinsPoolInput] = useState('');
  const [skinsPoolInitialized, setSkinsPoolInitialized] = useState(false);

  const { data: course } = useCourseDetail(round?.courseId);
  const nonPar3Holes = (course?.holeDetails ?? [])
    .filter((h) => h.par !== 3)
    .sort((a, b) => a.holeNumber - b.holeNumber);

  const { data: results } = useTournamentResults(roundId);
  const [matchupDraft, setMatchupDraft] = useState<MatchupInput[]>([]);
  const [matchupsDirty, setMatchupsDirty] = useState(false);

  // Pull the round's current pairings into local editor state whenever the
  // server data changes and the admin hasn't started editing — once dirty,
  // an in-flight 30s poll refetch shouldn't clobber unsaved edits.
  useEffect(() => {
    if (matchupsDirty || !results) return;
    setMatchupDraft(
      results.matchupResults.map((m) => ({ player1Id: m.player1Id, player2Id: m.player2Id })),
    );
  }, [results, matchupsDirty]);

  useEffect(() => {
    if (skinsPoolInitialized || !round) return;
    setGrossSkinsPoolInput(round.grossSkinsPool != null ? String(round.grossSkinsPool) : '');
    setNetSkinsPoolInput(round.netSkinsPool != null ? String(round.netSkinsPool) : '');
    setSkinsPoolInitialized(true);
  }, [round, skinsPoolInitialized]);

  const isLoading = roundLoading || participantsLoading;

  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Spinner />
      </div>
    );
  }

  if (!round) {
    return (
      <div className="flex h-64 flex-col items-center justify-center gap-2 text-gray-500">
        <p>Round not found.</p>
      </div>
    );
  }

  const currentPlayerIds = new Set((participants ?? []).map((p) => p.playerId));
  const availablePlayers = allPlayers.filter((p) => !currentPlayerIds.has(p.id));
  const currentParticipants = participants ?? [];

  function addMatchup() {
    if (currentParticipants.length < 2) return;
    setMatchupsDirty(true);
    setMatchupDraft((prev) => {
      const usedIds = new Set(prev.flatMap((m) => [m.player1Id, m.player2Id]));
      const unused = currentParticipants.filter((p) => !usedIds.has(p.playerId));
      const p1 = unused[0]?.playerId ?? currentParticipants[0].playerId;
      const p2 =
        unused[1]?.playerId ??
        currentParticipants.find((p) => p.playerId !== p1)?.playerId ??
        currentParticipants[0].playerId;
      return [...prev, { player1Id: p1, player2Id: p2 }];
    });
  }

  function removeMatchup(matchupIndex: number) {
    setMatchupsDirty(true);
    setMatchupDraft((prev) => prev.filter((_, i) => i !== matchupIndex));
  }

  function swapMatchupPlayers(matchupIndex: number) {
    setMatchupsDirty(true);
    setMatchupDraft((prev) =>
      prev.map((m, i) =>
        i === matchupIndex ? { player1Id: m.player2Id, player2Id: m.player1Id } : m,
      ),
    );
  }

  function setMatchupPlayer(matchupIndex: number, slot: 1 | 2, playerId: number) {
    setMatchupsDirty(true);
    setMatchupDraft((prev) =>
      prev.map((m, i) => {
        if (i !== matchupIndex) return m;
        return slot === 1 ? { ...m, player1Id: playerId } : { ...m, player2Id: playerId };
      }),
    );
  }

  async function saveMatchups() {
    setError(null);
    try {
      await setMatchups.mutateAsync(matchupDraft);
      setMatchupsDirty(false);
    } catch {
      setError('Failed to save matchups.');
    }
  }

  async function regenerateMatchupsFromHandicaps() {
    setError(null);
    try {
      await regenerateMatchups.mutateAsync();
      setMatchupsDirty(false);
    } catch {
      setError('Failed to regenerate matchups.');
    }
  }

  const skinsPoolDirty =
    grossSkinsPool !== (round.grossSkinsPool != null ? String(round.grossSkinsPool) : '') ||
    netSkinsPool !== (round.netSkinsPool != null ? String(round.netSkinsPool) : '');

  async function saveSkinsPool() {
    setError(null);
    if ((grossSkinsPool && Number(grossSkinsPool) < 0) || (netSkinsPool && Number(netSkinsPool) < 0)) {
      setError('Skins pool amounts cannot be negative.');
      return;
    }
    try {
      await setSkinsPool.mutateAsync({
        grossSkinsPool: grossSkinsPool ? Number(grossSkinsPool) : null,
        netSkinsPool: netSkinsPool ? Number(netSkinsPool) : null,
      });
    } catch {
      setError('Failed to save the skins pool.');
    }
  }

  async function changeLongestDriveHole(value: string) {
    setError(null);
    try {
      await setLongestDriveHole.mutateAsync(value === '' ? null : Number(value));
    } catch {
      setError('Failed to update the longest-drive hole.');
    }
  }

  async function addPlayer(playerId: number) {
    setError(null);
    try {
      await addParticipants.mutateAsync([playerId]);
    } catch {
      setError('Failed to add player.');
    }
  }

  async function removePlayer(playerId: number) {
    setError(null);
    try {
      await removeParticipant.mutateAsync(playerId);
      qc.invalidateQueries({ queryKey: ['rounds', roundId, 'participants'] });
    } catch {
      setError('Failed to remove player.');
    }
  }

  return (
    <div className="space-y-8">
      <div>
        <Link
          to={`${prefix}/admin/rounds`}
          className="mb-2 inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-700"
        >
          <ArrowLeft className="h-4 w-4" />
          Back to Rounds
        </Link>
        <div className="mt-1 flex flex-wrap items-baseline gap-3">
          <h1 className="flex items-center gap-2 text-2xl font-bold text-gray-900">
            <Trophy className="h-6 w-6 text-amber-500" />
            Manage Tournament
          </h1>
          <span className="text-gray-500">
            {round.courseName} · {formatDate(round.scheduledDate)}
          </span>
        </div>
      </div>

      {error && <p className="text-sm text-red-600">{error}</p>}

      <div className="grid gap-8 lg:grid-cols-2">
        {/* Settings */}
        <section className="space-y-4">
          <div className="mb-1 flex items-center gap-2 border-b border-gray-200 pb-2">
            <Trophy className="h-5 w-5 text-green-700" />
            <h2 className="text-lg font-semibold text-gray-800">Tournament Settings</h2>
          </div>

          <FormField label="Longest Drive Hole">
            <select
              value={round.longestDriveHoleNumber ?? ''}
              onChange={(e) => changeLongestDriveHole(e.target.value)}
              className={selectClass}
              disabled={setLongestDriveHole.isPending}
            >
              <option value="">— None —</option>
              {nonPar3Holes.map((h) => (
                <option key={h.holeNumber} value={h.holeNumber}>
                  Hole {h.holeNumber} (Par {h.par})
                </option>
              ))}
            </select>
          </FormField>

          <div className="grid grid-cols-2 gap-3">
            <FormField label="Gross Skins Pool ($)">
              <input
                type="number"
                min="0"
                step="1"
                value={grossSkinsPool}
                onChange={(e) => setGrossSkinsPoolInput(e.target.value)}
                className={inputClass}
                placeholder="Optional"
                disabled={skinsPoolLocked}
              />
            </FormField>
            <FormField label="Net Skins Pool ($)">
              <input
                type="number"
                min="0"
                step="1"
                value={netSkinsPool}
                onChange={(e) => setNetSkinsPoolInput(e.target.value)}
                className={inputClass}
                placeholder="Optional"
                disabled={skinsPoolLocked}
              />
            </FormField>
          </div>
          {skinsPoolLocked ? (
            <p className="text-xs text-gray-400">Skins pools are locked once the round is finalized.</p>
          ) : (
            <div className="flex items-center justify-end gap-2">
              {skinsPoolDirty && <span className="text-xs text-amber-600">Unsaved changes</span>}
              <Button
                type="button"
                variant="secondary"
                size="sm"
                onClick={saveSkinsPool}
                disabled={!skinsPoolDirty || setSkinsPool.isPending}
              >
                {setSkinsPool.isPending ? 'Saving…' : 'Save Skins Pool'}
              </Button>
            </div>
          )}
        </section>

        {/* Players */}
        <section className="space-y-3">
          <div className="mb-1 flex items-center gap-2 border-b border-gray-200 pb-2">
            <Users className="h-5 w-5 text-green-700" />
            <h2 className="text-lg font-semibold text-gray-800">
              Players Signed Up ({currentParticipants.length})
            </h2>
          </div>

          {availablePlayers.length > 0 && (
            <select
              className={selectClass}
              defaultValue=""
              onChange={(e) => {
                const val = Number(e.target.value);
                if (val) addPlayer(val);
                e.target.value = '';
              }}
              disabled={addParticipants.isPending}
            >
              <option value="">+ Add player or substitute…</option>
              {availablePlayers.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.fullName}
                  {p.isSubstitute ? ' (Sub)' : ''} (HCP {p.currentHandicap?.toFixed(1) ?? '—'})
                </option>
              ))}
            </select>
          )}

          <ul className="max-h-96 space-y-1 overflow-y-auto rounded-md border border-gray-100 p-1">
            {currentParticipants
              .slice()
              .sort((a, b) => a.handicapAtTime - b.handicapAtTime)
              .map((p) => (
                <li
                  key={p.id}
                  className="flex items-center justify-between rounded-md border border-gray-200 bg-gray-50 px-3 py-1.5 text-sm"
                >
                  <span>
                    {p.playerName}{' '}
                    <span className="text-gray-500">(HCP {p.handicapAtTime.toFixed(1)})</span>
                  </span>
                  <button
                    type="button"
                    onClick={() => removePlayer(p.playerId)}
                    disabled={removeParticipant.isPending}
                    className="text-gray-400 hover:text-red-500 disabled:opacity-50"
                  >
                    <X className="h-4 w-4" />
                  </button>
                </li>
              ))}
            {currentParticipants.length === 0 && (
              <li className="px-3 py-2 text-sm text-gray-400">No players yet.</li>
            )}
          </ul>
        </section>
      </div>

      {/* Matchups */}
      <section>
        <div className="mb-3 flex flex-wrap items-center justify-between gap-2 border-b border-gray-200 pb-2">
          <div className="flex items-center gap-2">
            <Trophy className="h-5 w-5 text-green-700" />
            <h2 className="text-lg font-semibold text-gray-800">Matchups</h2>
          </div>
          <div className="flex items-center gap-2">
            {isRoundScheduled(round.status) && (
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={regenerateMatchupsFromHandicaps}
                disabled={regenerateMatchups.isPending}
              >
                {regenerateMatchups.isPending ? 'Regenerating…' : 'Regenerate from Handicaps'}
              </Button>
            )}
            <Button
              type="button"
              variant="secondary"
              size="sm"
              onClick={addMatchup}
              disabled={currentParticipants.length < 2}
            >
              <Plus className="mr-1 h-4 w-4" />
              Add Matchup
            </Button>
          </div>
        </div>

        {currentParticipants.length < 2 ? (
          <p className="text-sm text-gray-400 italic">
            Add at least two players before creating matchups.
          </p>
        ) : matchupDraft.length === 0 ? (
          <p className="text-sm text-gray-400 italic">No matchups set.</p>
        ) : (
          <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-3">
            {matchupDraft.map((m, idx) => (
              <div
                key={idx}
                className="flex items-center gap-2 rounded-md border border-gray-200 bg-white p-2 shadow-sm"
              >
                <span className="w-5 text-center text-xs font-semibold text-gray-400">
                  {idx + 1}
                </span>
                <select
                  value={m.player1Id}
                  onChange={(e) => setMatchupPlayer(idx, 1, Number(e.target.value))}
                  className="flex-1 rounded border border-gray-300 px-2 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-green-600"
                >
                  {currentParticipants.map((p) => (
                    <option key={p.playerId} value={p.playerId}>
                      {p.playerName}
                    </option>
                  ))}
                </select>
                <span className="text-xs font-semibold text-gray-500">vs</span>
                <select
                  value={m.player2Id}
                  onChange={(e) => setMatchupPlayer(idx, 2, Number(e.target.value))}
                  className="flex-1 rounded border border-gray-300 px-2 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-green-600"
                >
                  {currentParticipants.map((p) => (
                    <option key={p.playerId} value={p.playerId}>
                      {p.playerName}
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
                <button
                  type="button"
                  title="Remove matchup"
                  onClick={() => removeMatchup(idx)}
                  className="rounded p-1 text-gray-400 hover:bg-red-50 hover:text-red-600"
                >
                  <Trash2 className="h-4 w-4" />
                </button>
              </div>
            ))}
          </div>
        )}

        <div className="mt-3 flex items-center justify-end gap-2">
          {matchupsDirty && <span className="text-xs text-amber-600">Unsaved changes</span>}
          <Button
            type="button"
            variant="primary"
            size="sm"
            onClick={saveMatchups}
            disabled={!matchupsDirty || setMatchups.isPending}
          >
            {setMatchups.isPending ? 'Saving…' : 'Save Matchups'}
          </Button>
        </div>
      </section>

      {(addParticipants.isPending || removeParticipant.isPending) && (
        <div className="fixed bottom-4 right-4 flex items-center gap-2 rounded-md bg-gray-900/90 px-3 py-2 text-sm text-white shadow-lg">
          <Loader2 className="h-4 w-4 animate-spin" />
          Updating…
        </div>
      )}
    </div>
  );
}
