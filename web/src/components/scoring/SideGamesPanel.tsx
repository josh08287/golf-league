import { useState } from 'react';
import { Trophy, X } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { Card, CardContent } from '@/components/ui/Card';
import { Badge } from '@/components/ui/Badge';
import { Spinner } from '@/components/ui/Spinner';
import {
  useTeeTimeSideGames,
  useOptInSideGame,
  useOptOutSideGame,
  useSetSideGameHolePick,
  useSetWolfHolePick,
} from '@/hooks/useTeeTimeSideGames';
import type { BbbHonor, NassauFormat, ScoringBasis, SideGameType, TeeTimeSideGame } from '@/types/api';

const BBB_HONOR_LABELS: Record<BbbHonor, string> = {
  FirstOnGreen: 'First on the green',
  ClosestOnceOn: 'Closest once all on',
  FirstInHole: 'First in the hole',
};

const GAME_INSTRUCTIONS: Record<SideGameType, string[]> = {
  Nassau: [
    'Three separate 1-point bets in one: front 9, back 9, and overall 18 — each scored as its own match.',
    'Every hole, whoever has the lower score (by the basis you pick below) wins that hole 1-up. A tie halves the hole.',
    'Whoever is more holes "up" than down when a segment ends wins that bet. If a segment ends all square, it\'s a push.',
    'Choose Individual to play every possible 1v1 matchup in the group at once, or 2v2 Team to split into two pairs and compare each side\'s best ball per hole.',
    'Pick gross or net scoring below — net uses each player\'s handicap strokes, gross does not.',
  ],
  TwoVTwoBestBall: [
    'Split the foursome into two 2-player teams.',
    'On every hole, each team\'s score is its better (lower) of the two players\' strokes — the other player\'s score doesn\'t matter.',
    'Whichever team has the lower best-ball score wins the hole. A tie halves it.',
    'Pick gross or net scoring below — net uses each player\'s handicap strokes, gross does not.',
    'Running tally is holes won by each team, shown live as scores come in.',
  ],
  BingoBangoBongo: [
    'Three individual honors are up for grabs on every hole, each worth 1 point — no connection to your actual score.',
    '"Bingo" — first ball on the green.',
    '"Bango" — closest to the pin once everyone\'s ball is on the green.',
    '"Bongo" — first ball in the hole.',
    'Whoever is entering scores also picks the honor winners for that hole (or leaves it blank if there\'s no clear winner — no point is awarded that hole).',
    'Works great for players of any skill level since it rewards different parts of the game, not just the lowest score.',
  ],
  Wolf: [
    'Needs at least 4 players. Set the tee-off order below — that order repeats hole after hole and decides whose turn it is to be the "Wolf".',
    'On the Wolf\'s hole, they watch the other 3 tee off first, then choose: partner up with one of them for that hole, go it alone as the "Lone Wolf", or declare "Blind Wolf" before anyone has hit.',
    'With a partner: the Wolf + partner\'s best ball is compared against the other two players\' best ball. Winning side splits 1 point per player.',
    'Lone Wolf (called after watching the others tee off): the Wolf plays alone against the other 3\'s best ball. Win and the Wolf takes 2 points solo; lose and the other 3 split 2 points.',
    'Blind Wolf (the bold move — declared before anyone tees off): win and the Wolf takes 4 points alone; lose and each of the other 3 gets 1 point (3 total, not split) — the biggest risk, biggest reward call in the game.',
    'A tied hole (best ball vs. best ball) is halved — no points awarded, regardless of which call was made.',
    'Pick gross or net scoring below — net uses each player\'s handicap strokes, gross does not.',
    'The partner/lone-wolf/blind-wolf call is made from the hole-entry screen — blind wolf must be declared before that hole\'s scores are entered.',
  ],
};

const GAME_LABELS: Record<SideGameType, string> = {
  Nassau: 'Nassau',
  TwoVTwoBestBall: '2v2 Best Ball',
  BingoBangoBongo: 'Bingo Bango Bongo',
  Wolf: 'Wolf',
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
              {(game.gameType === 'TwoVTwoBestBall' || game.gameType === 'Wolf') && (
                <span className="text-gray-500 font-normal"> ({game.scoringBasis})</span>
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

        {game.bbb && (
          <div className="mt-2 rounded bg-gray-50 px-2 py-1.5 text-xs">
            {game.bbb.standings.length === 0 ? (
              <p className="text-gray-500">No honors recorded yet</p>
            ) : (
              <ul className="space-y-0.5">
                {game.bbb.standings.map((s) => (
                  <li key={s.participantId} className="flex justify-between text-gray-700">
                    <span>{s.playerName}</span>
                    <span className="font-medium">{s.points} pt{s.points === 1 ? '' : 's'}</span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}

        {game.wolf && (
          <div className="mt-2 rounded bg-gray-50 px-2 py-1.5 text-xs space-y-1">
            {game.wolf.nextHoleNumber > 0 && (
              <p className="text-gray-700">
                Next up: <span className="font-medium">{game.wolf.nextWolfPlayerName}</span> on hole {game.wolf.nextHoleNumber}
              </p>
            )}
            {game.wolf.standings.length > 0 && (
              <ul className="space-y-0.5">
                {game.wolf.standings.map((s) => (
                  <li key={s.participantId} className="flex justify-between text-gray-700">
                    <span>{s.playerName}</span>
                    <span className="font-medium">{s.points} pt{s.points === 1 ? '' : 's'}</span>
                  </li>
                ))}
              </ul>
            )}
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
export function SideGamesStatusBar({ teeTimeId, enabled, final = false }: { teeTimeId: number; enabled: boolean; final?: boolean }) {
  const { data } = useTeeTimeSideGames(teeTimeId, enabled);

  if (!enabled || !data || data.configuredGames.length === 0) return null;

  if (final) {
    return (
      <section className="rounded-lg border-2 border-amber-300 bg-amber-50 p-4 space-y-3">
        <div className="flex items-center gap-2">
          <Trophy className="h-5 w-5 text-amber-600" />
          <h3 className="text-lg font-bold text-amber-900">Side Game Results</h3>
        </div>
        {/* Bump the cards' small print up a size — the round is over, so these are the results. */}
        <div className="space-y-2 [&_.text-xs]:text-sm">
          {data.configuredGames.map((game) => (
            <SideGameStatusCard key={game.id} game={game} />
          ))}
        </div>
      </section>
    );
  }

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
          error={
            optIn.isError
              ? (optIn.error as { response?: { data?: { error?: string } } } | null)?.response?.data?.error
                ?? 'Failed to opt in. Please try again.'
              : null
          }
          onCancel={() => {
            optIn.reset();
            setOpenGameType(null);
          }}
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
    wolfRotationOrder?: number[];
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
  const [wolfOrder, setWolfOrder] = useState<number[]>(() => players.map((p) => p.participantId));

  const needsTeams = gameType === 'TwoVTwoBestBall' || (gameType === 'Nassau' && nassauFormat === 'TeamVsTeam');
  const isWolf = gameType === 'Wolf';
  const needsScoringBasis = gameType === 'Nassau' || gameType === 'TwoVTwoBestBall' || gameType === 'Wolf';

  function toggleTeam(participantId: number) {
    setTeamAssignments((prev) => ({
      ...prev,
      [participantId]: prev[participantId] === 1 ? 2 : 1,
    }));
  }

  function moveWolfPlayer(participantId: number, direction: -1 | 1) {
    setWolfOrder((prev) => {
      const index = prev.indexOf(participantId);
      const targetIndex = index + direction;
      if (targetIndex < 0 || targetIndex >= prev.length) return prev;
      const next = [...prev];
      [next[index], next[targetIndex]] = [next[targetIndex], next[index]];
      return next;
    });
  }

  function handleConfirm() {
    const teams = needsTeams
      ? players.map((p) => ({ participantId: p.participantId, teamNumber: teamAssignments[p.participantId] }))
      : undefined;

    onConfirm({
      gameType,
      scoringBasis: needsScoringBasis ? scoringBasis : undefined,
      nassauFormat: gameType === 'Nassau' ? nassauFormat : undefined,
      teams,
      wolfRotationOrder: isWolf ? wolfOrder : undefined,
    });
  }

  const teamACount = Object.values(teamAssignments).filter((t) => t === 1).length;
  const teamBCount = Object.values(teamAssignments).filter((t) => t === 2).length;
  const teamsValid = !needsTeams || (teamACount > 0 && teamBCount > 0);
  const wolfValid = !isWolf || wolfOrder.length >= 4;

  return (
    <Card className="border-primary-200">
      <CardContent className="p-4 space-y-4">
        <p className="text-sm font-semibold text-gray-900">Opt into {GAME_LABELS[gameType]}</p>

        <div className="rounded-lg bg-primary-50 px-3 py-2.5">
          <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-primary-700">How it works</p>
          <ul className="list-disc space-y-1 pl-4 text-xs text-primary-900">
            {GAME_INSTRUCTIONS[gameType].map((line, i) => (
              <li key={i}>{line}</li>
            ))}
          </ul>
        </div>

        {gameType === 'Nassau' && (
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
        )}

        {needsScoringBasis && (
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

        {isWolf && (
          <div>
            <p className="mb-1.5 text-xs font-medium text-gray-600">
              Tee-off order (use the arrows to reorder) — needs at least 4 players
            </p>
            <div className="space-y-1">
              {wolfOrder.map((participantId, index) => {
                const player = players.find((p) => p.participantId === participantId);
                if (!player) return null;
                return (
                  <div key={participantId} className="flex items-center justify-between gap-2 rounded bg-gray-50 px-2 py-1 text-sm">
                    <span className="text-gray-700">{index + 1}. {player.playerName}</span>
                    <span className="flex gap-1">
                      <button
                        type="button"
                        onClick={() => moveWolfPlayer(participantId, -1)}
                        disabled={index === 0}
                        className="rounded px-1.5 py-0.5 text-xs text-gray-500 hover:bg-gray-200 disabled:opacity-30"
                        aria-label={`Move ${player.playerName} earlier`}
                      >
                        ↑
                      </button>
                      <button
                        type="button"
                        onClick={() => moveWolfPlayer(participantId, 1)}
                        disabled={index === wolfOrder.length - 1}
                        className="rounded px-1.5 py-0.5 text-xs text-gray-500 hover:bg-gray-200 disabled:opacity-30"
                        aria-label={`Move ${player.playerName} later`}
                      >
                        ↓
                      </button>
                    </span>
                  </div>
                );
              })}
            </div>
            {!wolfValid && <p className="mt-1 text-xs text-red-600">Wolf needs at least 4 active players.</p>}
          </div>
        )}

        {error && <p className="text-xs text-red-600">{error}</p>}

        <div className="flex gap-2">
          <Button type="button" size="sm" disabled={isPending || !teamsValid || !wolfValid} onClick={handleConfirm}>
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

interface SideGamesHolePickersSectionProps {
  teeTimeId: number;
  holeNumber: number;
  players: EligiblePlayer[];
  enabled: boolean;
  canEdit: boolean;
}

/**
 * Per-hole picker controls for Bingo Bango Bongo (3 honors) and Wolf
 * (partner/lone-wolf call), shown alongside normal score entry for whichever
 * hole is currently active. Renders nothing if the group hasn't opted into
 * either game.
 */
export function SideGamesHolePickersSection({ teeTimeId, holeNumber, players, enabled, canEdit }: SideGamesHolePickersSectionProps) {
  const { data } = useTeeTimeSideGames(teeTimeId, enabled);
  const setHolePick = useSetSideGameHolePick(teeTimeId);
  const setWolfPick = useSetWolfHolePick(teeTimeId);

  if (!enabled || !data) return null;

  const bbbGame = data.configuredGames.find((g) => g.gameType === 'BingoBangoBongo');
  const wolfGame = data.configuredGames.find((g) => g.gameType === 'Wolf');

  if (!bbbGame && !wolfGame) return null;

  return (
    <Card>
      <CardContent className="space-y-4 py-4">
        {bbbGame && (
          <div className="space-y-2">
            <span className="flex items-center gap-1.5 text-sm font-medium text-gray-700">
              <Trophy className="h-4 w-4 text-primary-700" />
              Bingo Bango Bongo
            </span>
            {(Object.keys(BBB_HONOR_LABELS) as BbbHonor[]).map((honor) => {
              const pick = bbbGame.bbb?.picks.find((p) => p.holeNumber === holeNumber && p.honor === honor);
              return (
                <div key={honor} className="flex items-center justify-between gap-4 pl-1">
                  <span className="text-xs text-gray-500">{BBB_HONOR_LABELS[honor]}</span>
                  <select
                    value={pick?.winnerParticipantId ?? ''}
                    onChange={(e) =>
                      setHolePick.mutate({
                        sideGameId: bbbGame.id,
                        holeNumber,
                        honor,
                        winnerParticipantId: e.target.value === '' ? null : parseInt(e.target.value, 10),
                      })
                    }
                    disabled={!canEdit || setHolePick.isPending}
                    className="w-48 rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20] disabled:opacity-50 disabled:bg-gray-50"
                  >
                    <option value="">None</option>
                    {players.map((p) => (
                      <option key={p.participantId} value={p.participantId}>{p.playerName}</option>
                    ))}
                  </select>
                </div>
              );
            })}
          </div>
        )}

        {wolfGame?.wolf && (
          <WolfHolePicker
            // Remount per hole and whenever the saved call changes, so the
            // picker never shows the previous hole's call or a stale one.
            key={`${holeNumber}:${wolfPickSignature(wolfGame, holeNumber)}`}
            game={wolfGame}
            holeNumber={holeNumber}
            players={players}
            canEdit={canEdit}
            onSave={(input) => setWolfPick.mutate({ sideGameId: wolfGame.id, holeNumber, ...input })}
            isPending={setWolfPick.isPending}
          />
        )}
      </CardContent>
    </Card>
  );
}

type WolfCallMode = 'partner' | 'lone' | 'blind';

function wolfPickSignature(game: TeeTimeSideGame, holeNumber: number): string {
  const pick = game.wolf?.picks.find((p) => p.holeNumber === holeNumber);
  if (!pick) return 'none';
  return `${pick.isBlindWolf ? 'blind' : pick.isLoneWolf ? 'lone' : 'partner'}:${pick.partnerParticipantId ?? ''}`;
}

function WolfHolePicker({
  game,
  holeNumber,
  players,
  canEdit,
  onSave,
  isPending,
}: {
  game: TeeTimeSideGame;
  holeNumber: number;
  players: EligiblePlayer[];
  canEdit: boolean;
  onSave: (input: { wolfParticipantId: number; isLoneWolf: boolean; isBlindWolf: boolean; partnerParticipantId: number | null }) => void;
  isPending: boolean;
}) {
  const wolf = game.wolf!;
  const existingPick = wolf.picks.find((p) => p.holeNumber === holeNumber);
  const playPosition = wolf.playOrderHoleNumbers.indexOf(holeNumber);
  const wolfParticipantId =
    existingPick?.wolfParticipantId ??
    (playPosition >= 0 ? wolf.rotationParticipantIds[playPosition % wolf.rotationParticipantIds.length] : undefined);
  const wolfName = players.find((p) => p.participantId === wolfParticipantId)?.playerName ?? 'Unknown';

  const initialMode: WolfCallMode = existingPick?.isBlindWolf ? 'blind' : existingPick?.isLoneWolf ? 'lone' : 'partner';
  // Only needed for the in-between state of "Pick a partner" chosen but no
  // partner selected yet; every complete call is saved immediately.
  const [mode, setMode] = useState<WolfCallMode>(initialMode);

  if (wolfParticipantId == null) return null;

  const partnerOptions = players.filter((p) => p.participantId !== wolfParticipantId);

  const saveLoneCall = (blind: boolean) => {
    setMode(blind ? 'blind' : 'lone');
    onSave({ wolfParticipantId, isLoneWolf: true, isBlindWolf: blind, partnerParticipantId: null });
  };

  const savePartner = (partnerId: number) =>
    onSave({ wolfParticipantId, isLoneWolf: false, isBlindWolf: false, partnerParticipantId: partnerId });

  return (
    <div className="space-y-2">
      <span className="flex items-center gap-1.5 text-sm font-medium text-gray-700">
        <Trophy className="h-4 w-4 text-primary-700" />
        Wolf — {wolfName}'s turn
      </span>

      <div className="flex flex-wrap gap-2 pl-1">
        <button
          type="button"
          disabled={!canEdit}
          onClick={() => setMode('partner')}
          className={`rounded-full px-3 py-1 text-xs font-medium disabled:opacity-50 ${
            mode === 'partner' ? 'bg-primary-900 text-white' : 'bg-gray-100 text-gray-600'
          }`}
        >
          Pick a partner
        </button>
        <button
          type="button"
          disabled={!canEdit || isPending}
          onClick={() => saveLoneCall(false)}
          className={`rounded-full px-3 py-1 text-xs font-medium disabled:opacity-50 ${
            mode === 'lone' ? 'bg-primary-900 text-white' : 'bg-gray-100 text-gray-600'
          }`}
        >
          Lone Wolf (2 pts)
        </button>
        <button
          type="button"
          disabled={!canEdit || isPending}
          onClick={() => saveLoneCall(true)}
          className={`rounded-full px-3 py-1 text-xs font-medium disabled:opacity-50 ${
            mode === 'blind' ? 'bg-primary-900 text-white' : 'bg-gray-100 text-gray-600'
          }`}
        >
          Blind Wolf (4 pts / 1 pt each)
        </button>
      </div>

      {mode === 'blind' && (
        <p className="pl-1 text-xs text-amber-700">
          Only declare Blind Wolf before anyone in the group has hit their tee shot.
        </p>
      )}

      {mode === 'partner' && (
        <select
          value={existingPick && !existingPick.isLoneWolf ? existingPick.partnerParticipantId ?? '' : ''}
          onChange={(e) => {
            if (e.target.value !== '') savePartner(parseInt(e.target.value, 10));
          }}
          disabled={!canEdit || isPending}
          className="ml-1 w-48 rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20] disabled:opacity-50 disabled:bg-gray-50"
        >
          <option value="">Select partner…</option>
          {partnerOptions.map((p) => (
            <option key={p.participantId} value={p.participantId}>{p.playerName}</option>
          ))}
        </select>
      )}

      <p className="pl-1 text-xs text-gray-500">
        {isPending
          ? 'Saving…'
          : existingPick
            ? <>
                Saved: {existingPick.isBlindWolf ? 'Blind wolf' : existingPick.isLoneWolf ? 'Lone wolf' : `Partnered with ${existingPick.partnerPlayerName}`}
                {existingPick.outcome && ` — ${existingPick.outcome}`}
              </>
            : 'No call recorded yet for this hole.'}
      </p>
    </div>
  );
}
