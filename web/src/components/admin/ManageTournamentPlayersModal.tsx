import { useEffect, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowUpDown, Trophy, X } from 'lucide-react';
import { api } from '../../lib/api';
import { usePlayers, useSubstitutes } from '../../hooks/usePlayers';
import { useTournamentResults } from '../../hooks/useRounds';
import {
  useAddTournamentParticipants,
  useRemoveTournamentParticipant,
  useSetTournamentLongestDriveHole,
  useSetTournamentMatchups,
} from '../../hooks/admin/useRoundMutations';
import type { MatchupInput } from '../../hooks/admin/useRoundMutations';
import { useCourseDetail } from '../../hooks/admin/useCourseMutations';
import { Modal } from './Modal';
import { Button } from '../ui/Button';
import { Spinner } from '../ui/Spinner';
import { FormField, selectClass } from './FormField';
import type { Participant, Round } from '../../types/api';

interface ManageTournamentPlayersModalProps {
  round: Round | null;
  onClose: () => void;
}

export function ManageTournamentPlayersModal({ round, onClose }: ManageTournamentPlayersModalProps) {
  const roundId = round ? String(round.id) : '';
  const qc = useQueryClient();
  const [error, setError] = useState<string | null>(null);

  const { data: participants, isLoading } = useQuery<Participant[]>({
    queryKey: ['rounds', roundId, 'participants'],
    queryFn: () => api.get(`/rounds/${roundId}/participants`).then((r) => r.data),
    enabled: !!round,
  });

  const { data: playersPage } = usePlayers(1, undefined, 200);
  const { data: substitutes } = useSubstitutes();
  const allPlayers = [
    ...(playersPage?.data?.filter((p) => p.isActive) ?? []),
    ...(substitutes ?? []),
  ];

  const addParticipants = useAddTournamentParticipants(roundId);
  const removeParticipant = useRemoveTournamentParticipant(roundId);
  const setLongestDriveHole = useSetTournamentLongestDriveHole(roundId);
  const setMatchups = useSetTournamentMatchups(roundId);

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

  if (!round) return null;

  const currentPlayerIds = new Set((participants ?? []).map((p) => p.playerId));
  const availablePlayers = allPlayers.filter((p) => !currentPlayerIds.has(p.id));
  const currentParticipants = participants ?? [];

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
    } catch {
      setError('Failed to remove player.');
    }
  }

  function handleClose() {
    qc.invalidateQueries({ queryKey: ['rounds', roundId, 'participants'] });
    onClose();
  }

  return (
    <Modal open={!!round} title="Manage Tournament Players" onClose={handleClose}>
      <div className="space-y-4">
        {isLoading ? (
          <div className="flex h-32 items-center justify-center">
            <Spinner />
          </div>
        ) : (
          <>
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

            <ul className="max-h-72 space-y-1 overflow-y-auto rounded-md border border-gray-100 p-1">
              {(participants ?? [])
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
              {(participants ?? []).length === 0 && (
                <li className="px-3 py-2 text-sm text-gray-400">No players yet.</li>
              )}
            </ul>

            {matchupDraft.length > 0 && (
              <div>
                <div className="mb-2 flex items-center gap-2">
                  <Trophy className="h-4 w-4 text-amber-500" />
                  <span className="text-sm font-medium text-gray-700">Matchups</span>
                </div>
                <div className="max-h-48 space-y-2 overflow-y-auto rounded-md border border-gray-100 p-1">
                  {matchupDraft.map((m, idx) => (
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
                    </div>
                  ))}
                </div>
                <div className="mt-2 flex items-center justify-end gap-2">
                  {matchupsDirty && (
                    <span className="text-xs text-amber-600">Unsaved changes</span>
                  )}
                  <Button
                    type="button"
                    variant="secondary"
                    size="sm"
                    onClick={saveMatchups}
                    disabled={!matchupsDirty || setMatchups.isPending}
                  >
                    {setMatchups.isPending ? 'Saving…' : 'Save Matchups'}
                  </Button>
                </div>
              </div>
            )}
          </>
        )}

        {error && <p className="text-sm text-red-600">{error}</p>}

        <div className="flex justify-end pt-2">
          <Button type="button" variant="secondary" onClick={handleClose}>
            Done
          </Button>
        </div>
      </div>
    </Modal>
  );
}
