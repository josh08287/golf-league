import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { X } from 'lucide-react';
import { api } from '../../lib/api';
import { usePlayers, useSubstitutes } from '../../hooks/usePlayers';
import {
  useAddTournamentParticipants,
  useRemoveTournamentParticipant,
} from '../../hooks/admin/useRoundMutations';
import { Modal } from './Modal';
import { Button } from '../ui/Button';
import { Spinner } from '../ui/Spinner';
import { selectClass } from './FormField';
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

  if (!round) return null;

  const currentPlayerIds = new Set((participants ?? []).map((p) => p.playerId));
  const availablePlayers = allPlayers.filter((p) => !currentPlayerIds.has(p.id));

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
