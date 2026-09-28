import { useState } from 'react';
import { Trophy, X } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { Card, CardContent } from '@/components/ui/Card';
import { Badge } from '@/components/ui/Badge';
import { Spinner } from '@/components/ui/Spinner';
import { useTeeTimeSideGames, useOptInSideGame, useOptOutSideGame } from '@/hooks/useTeeTimeSideGames';
import type { NassauFormat, ScoringBasis, SideGameType, TeeTimeSideGame } from '@/types/api';

const GAME_LABELS: Record<SideGameType, string> = {
  Nassau: 'Nassau',
  TwoVTwoBestBall: '2v2 Best Ball',
};

interface EligiblePlayer {
  participantId: number;
  playerName: string;
}

function SideGameStatusCard({ game, onOptOut, optOutPending }: {
  game: TeeTimeSideGame;
  onOptOut?: (id: number) => void;
  optOutPending?: boolean;
}) {
  return (
    <Card>
      <CardContent className="p-3">
        <div className="flex items-start justify-between gap-2">
          <div className="min-w-0">
            <p className="text-sm font-medium text-gray-900">
              {GAME_LABELS[game.gameType]}
              {game.gameType === 'Nassau' && (
                <span className="text-gray-500 font-normal">
                  {' '}
                  ({game.scoringBasis}{game.nassauFormat === 'TeamVsTeam' ? ', 2v2' : ', individual'})
                </span>
              )}
            </p>
            <p className="text-xs text-gray-500">
              {game.holesEntered} of {game.totalHoles} holes entered
            </p>
          </div>
          {onOptOut && (
            <button
              type="button"
              onClick={() => onOptOut(game.id)}
              disabled={optOutPending}
              className="shrink-0 text-gray-400 hover:text-red-600 disabled:opacity-50"
              aria-label={`Opt out of ${GAME_LABELS[game.gameType]}`}
            >
              <X className="h-4 w-4" />
            </button>
          )}
        </div>

        {game.nassau && (
          <div className="mt-2 space-y-1">
            {game.nassau.matches.map((m, i) => (
              <div key={i} className="rounded bg-gray-50 px-2 py-1.5 text-xs">
                <p className="font-medium text-gray-700">{m.sideAName} vs {m.sideBName}</p>
                <p className="text-gray-500">
                  Front: {m.frontStatus} · Back: {m.backStatus} · Overall: {m.overallStatus}
                </p>
              </div>
            ))}
          </div>
        )}

        {game.bestBall && (
          <div className="mt-2 rounded bg-gray-50 px-2 py-1.5 text-xs">
            <p className="font-medium text-gray-700">
              {game.bestBall.teamAName} vs {game.bestBall.teamBName}
            </p>
            <p className="text-gray-500">{game.bestBall.status}</p>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

/**
 * Read-only, always-visible side games status — used during hole-by-hole
 * entry and on the review screen. Renders nothing if the group hasn't
 * opted into any games (or the flag is off / still loading), so it's safe
 * to drop in unconditionally.
 */
export function SideGamesStatusBar({ teeTimeId, enabled }: { teeTimeId: number; enabled: boolean }) {
  const { data } = useTeeTimeSideGames(teeTimeId, enabled);

  if (!enabled || !data || data.configuredGames.length === 0) return null;

  return (
    <div className="space-y-2">
      {data.configuredGames.map((game) => (
        <SideGameStatusCard key={game.id} game={game} />
      ))}
    </div>
  );
}

interface SideGamesPanelProps {
  teeTimeId: number;
  players: EligiblePlayer[];
}

/**
 * Lets any player in a foursome opt the group into optional side games
 * (Nassau, 2v2 best ball) and shows each opted-into game's live status.
 * Every game is scored purely from each player's own hole scores already
 * being entered elsewhere on this page — opting in never changes that.
 */
export function SideGamesPanel({ teeTimeId, players }: SideGamesPanelProps) {
  const { data, isLoading } = useTeeTimeSideGames(teeTimeId);
  const optIn = useOptInSideGame(teeTimeId);
  const optOut = useOptOutSideGame(teeTimeId);
  const [openGameType, setOpenGameType] = useState<SideGameType | null>(null);

  if (isLoading || !data) {
    return (
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Spinner className="h-4 w-4" /> Loading side games…
      </div>
    );
  }

  const configuredTypes = new Set(data.configuredGames.map((g) => g.gameType));
  const availableToAdd = data.eligibleGameTypes.filter((t) => !configuredTypes.has(t));

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-1.5">
        <Trophy className="h-4 w-4 text-primary-700" />
        <p className="text-sm font-semibold text-gray-900">Side Games</p>
      </div>

      {data.configuredGames.map((game) => (
        <SideGameStatusCard
          key={game.id}
          game={game}
          onOptOut={(id) => optOut.mutate(id)}
          optOutPending={optOut.isPending}
        />
      ))}

      {availableToAdd.length > 0 && (
        <div className="flex flex-wrap gap-2">
          {availableToAdd.map((gameType) => (
            <Button
              key={gameType}
              type="button"
              variant="outline"
              size="sm"
              onClick={() => setOpenGameType(gameType)}
            >
              + {GAME_LABELS[gameType]}
            </Button>
          ))}
        </div>
      )}

      {openGameType && (
        <OptInDialog
          gameType={openGameType}
          players={players}
          isPending={optIn.isPending}
          error={optIn.isError ? 'Failed to opt in. Please try again.' : null}
          onCancel={() => setOpenGameType(null)}
          onConfirm={(input) => {
            optIn.mutate(input, {
              onSuccess: () => setOpenGameType(null),
            });
          }}
        />
      )}
    </div>
  );
}

interface OptInDialogProps {
  gameType: SideGameType;
  players: EligiblePlayer[];
  isPending: boolean;
  error: string | null;
  onCancel: () => void;
  onConfirm: (input: {
    gameType: SideGameType;
    scoringBasis?: ScoringBasis;
    nassauFormat?: NassauFormat;
    teams?: { participantId: number; teamNumber: number }[];
  }) => void;
}

function OptInDialog({ gameType, players, isPending, error, onCancel, onConfirm }: OptInDialogProps) {
  const [scoringBasis, setScoringBasis] = useState<ScoringBasis>('Net');
  const [nassauFormat, setNassauFormat] = useState<NassauFormat>('Individual');
  const [teamAssignments, setTeamAssignments] = useState<Record<number, 1 | 2>>(() => {
    const initial: Record<number, 1 | 2> = {};
    players.forEach((p, i) => {
      initial[p.participantId] = i % 2 === 0 ? 1 : 2;
    });
    return initial;
  });

  const needsTeams = gameType === 'TwoVTwoBestBall' || (gameType === 'Nassau' && nassauFormat === 'TeamVsTeam');

  function toggleTeam(participantId: number) {
    setTeamAssignments((prev) => ({
      ...prev,
      [participantId]: prev[participantId] === 1 ? 2 : 1,
    }));
  }

  function handleConfirm() {
    const teams = needsTeams
      ? players.map((p) => ({ participantId: p.participantId, teamNumber: teamAssignments[p.participantId] }))
      : undefined;

    onConfirm({
      gameType,
      scoringBasis: gameType === 'Nassau' ? scoringBasis : undefined,
      nassauFormat: gameType === 'Nassau' ? nassauFormat : undefined,
      teams,
    });
  }

  const teamACount = Object.values(teamAssignments).filter((t) => t === 1).length;
  const teamBCount = Object.values(teamAssignments).filter((t) => t === 2).length;
  const teamsValid = !needsTeams || (teamACount > 0 && teamBCount > 0);

  return (
    <Card className="border-primary-200">
      <CardContent className="p-4 space-y-4">
        <p className="text-sm font-semibold text-gray-900">Opt into {GAME_LABELS[gameType]}</p>

        {gameType === 'Nassau' && (
          <>
            <div>
              <p className="mb-1.5 text-xs font-medium text-gray-600">Format</p>
              <div className="flex gap-2">
                {(['Individual', 'TeamVsTeam'] as NassauFormat[]).map((f) => (
                  <button
                    key={f}
                    type="button"
                    onClick={() => setNassauFormat(f)}
                    className={`rounded-full px-3 py-1 text-xs font-medium ${
                      nassauFormat === f ? 'bg-primary-900 text-white' : 'bg-gray-100 text-gray-600'
                    }`}
                  >
                    {f === 'Individual' ? 'Every player 1v1' : '2v2 teams'}
                  </button>
                ))}
              </div>
            </div>

            <div>
              <p className="mb-1.5 text-xs font-medium text-gray-600">Scoring basis</p>
              <div className="flex gap-2">
                {(['Net', 'Gross'] as ScoringBasis[]).map((b) => (
                  <button
                    key={b}
                    type="button"
                    onClick={() => setScoringBasis(b)}
                    className={`rounded-full px-3 py-1 text-xs font-medium ${
                      scoringBasis === b ? 'bg-primary-900 text-white' : 'bg-gray-100 text-gray-600'
                    }`}
                  >
                    {b}
                  </button>
                ))}
              </div>
            </div>
          </>
        )}

        {needsTeams && (
          <div>
            <p className="mb-1.5 text-xs font-medium text-gray-600">
              Tap a player to move them between teams
            </p>
            <div className="flex flex-wrap gap-2">
              {players.map((p) => {
                const team = teamAssignments[p.participantId];
                return (
                  <button
                    key={p.participantId}
                    type="button"
                    onClick={() => toggleTeam(p.participantId)}
                    className="rounded-full"
                  >
                    <Badge variant={team === 1 ? 'blue' : 'amber'}>
                      Team {team}: {p.playerName}
                    </Badge>
                  </button>
                );
              })}
            </div>
          </div>
        )}

        {error && <p className="text-xs text-red-600">{error}</p>}

        <div className="flex gap-2">
          <Button type="button" size="sm" disabled={isPending || !teamsValid} onClick={handleConfirm}>
            {isPending ? 'Saving…' : 'Confirm'}
          </Button>
          <Button type="button" size="sm" variant="ghost" onClick={onCancel} disabled={isPending}>
            Cancel
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
