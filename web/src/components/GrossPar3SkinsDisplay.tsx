import { Link } from 'react-router-dom';
import { useLeaguePrefix } from '@/context/LeagueContext';
import { Trophy, Crosshair } from 'lucide-react';
import { cn } from '@/lib/utils';
import { useRoundClosestToPin } from '@/hooks/useClosestToPin';
import { useFeatureFlagStates } from '@/hooks/admin/useFeatureFlags';
import { FEATURE_FLAG_KEYS } from '@/types/api';
import type { GrossPar3SkinsSummary } from '@/types/api';

interface GrossPar3SkinsDisplayProps {
  grossPar3Skins: GrossPar3SkinsSummary;
  roundId: number;
}

function ClosestToPinPanel({ roundId }: { roundId: number }) {
  const prefix = useLeaguePrefix();
  const featureFlags = useFeatureFlagStates();
  const ctpEnabled = featureFlags.data?.[FEATURE_FLAG_KEYS.closestToPinEnabled] ?? false;
  const ctp = useRoundClosestToPin(roundId, ctpEnabled);

  if (!ctpEnabled) return null;

  const wonHoles = ctp.data?.par3Holes.filter((h) => h.playerId != null) ?? [];
  if (wonHoles.length === 0) return null;

  return (
    <div className="sm:flex-1 sm:min-w-0 sm:border-l sm:border-gray-200 sm:pl-5">
      <div className="flex items-center gap-2 mb-3">
        <Crosshair className="h-4 w-4 text-[#8A5A10]" />
        <span className="font-sans text-base font-bold tracking-tight text-gray-900">Closest to the Pin</span>
      </div>
      <div className="divide-y divide-gray-100 border-t border-gray-100">
        {wonHoles.map((hole) => (
          <div
            key={hole.holeNumber}
            className="flex items-center justify-between py-2 text-sm"
          >
            <span className="font-mono text-xs font-semibold uppercase tracking-wider text-gray-500">
              Hole {hole.holeNumber}
            </span>
            <Link
              to={`${prefix}/players/${hole.playerId}`}
              className="font-semibold text-gray-900 hover:text-primary-900 hover:underline"
            >
              {hole.playerName}
            </Link>
          </div>
        ))}
      </div>
    </div>
  );
}

export function GrossPar3SkinsDisplay({ grossPar3Skins, roundId }: GrossPar3SkinsDisplayProps) {
  const prefix = useLeaguePrefix();
  const incoming = grossPar3Skins.incomingCarryover;

  // Running cumulative carryover entering each hole (starts at incoming from prior rounds).
  let running = incoming;
  const runningCarryByHole = grossPar3Skins.holeResults.map((hole) => {
    if (hole.skinValue > 0) {
      running = 0;
    } else {
      running += 1;
    }
    return running;
  });

  const header = (
    <div className="flex items-start justify-between pb-4 border-b border-gray-100 gap-3">
      <div>
        <div className="flex items-center gap-2 flex-wrap">
          <Trophy className="h-4 w-4 text-[#9A6B13]" />
          <h3 className="font-sans text-lg font-bold tracking-tight text-gray-900">Par 3 Gross Skins</h3>
        </div>
        <p className="text-xs text-gray-500 mt-1">
          {grossPar3Skins.totalHolesWithSkins} of {grossPar3Skins.par3HoleCount} par-3 holes awarded
          {incoming > 0 && (
            <span
              className="ml-2 font-mono text-[11px] font-semibold text-[#8A5A10]"
              title={`${incoming} skin(s) carried over from prior round(s)`}
            >
              &middot; ↻ {incoming} carryover from prior round{incoming === 1 ? '' : 's'}
            </span>
          )}
        </p>
      </div>
      {grossPar3Skins.totalSkinValueAwarded > 0 && (
        <div className="text-right shrink-0">
          <span className="block text-[10px] font-semibold uppercase tracking-widest text-gray-400">
            Total Pot
          </span>
          <span className="font-mono text-lg font-bold text-[#8A5A10]">
            {grossPar3Skins.totalSkinValueAwarded}
          </span>
        </div>
      )}
    </div>
  );

  if (grossPar3Skins.playerSummaries.length === 0) {
    return (
      <div className="rounded-md border border-gray-200 bg-white p-6 shadow-[0_1px_3px_rgba(20,34,24,0.04)] h-full">
        <div className="flex flex-col sm:flex-row sm:justify-between gap-4">
          <div className="flex-1 min-w-0">
            {header}
            <div className="pt-4 text-sm text-gray-500 italic">
              No gross skins awarded on par 3s — all holes were tied.
            </div>
          </div>
          <ClosestToPinPanel roundId={roundId} />
        </div>
      </div>
    );
  }

  return (
    <div className="rounded-md border border-gray-200 bg-white p-6 shadow-[0_1px_3px_rgba(20,34,24,0.04)] h-full flex flex-col justify-between">
      <div className="flex flex-col sm:flex-row sm:justify-between gap-5 h-full">
        <div className="flex-1 min-w-0 flex flex-col justify-between">
          <div>
            {header}

            {/* Winners Ledger Grid */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 my-4">
              {grossPar3Skins.playerSummaries.map((player) => {
                const holesLabel = player.holesWon
                  ?.map((h) => `Hole ${h.holeNumber}`)
                  .join(', ');
                return (
                  <div
                    key={player.playerId}
                    className="flex items-center justify-between border border-[#E5DEC9] border-l-2 border-l-[#C89B3C] bg-[#FAF6EC]/60 rounded-sm px-3.5 py-2.5"
                  >
                    <div className="min-w-0">
                      {holesLabel && (
                        <span className="block font-mono text-[10px] font-semibold uppercase tracking-wider text-[#8A5A10]">
                          {holesLabel}
                        </span>
                      )}
                      <Link
                        to={`${prefix}/players/${player.playerId}`}
                        className="font-semibold text-sm text-gray-900 hover:text-primary-900 hover:underline truncate block mt-0.5"
                      >
                        {player.playerName}
                      </Link>
                    </div>
                    <div className="text-right shrink-0 ml-3 font-mono">
                      <span className="text-base font-bold text-gray-900">
                        {player.totalSkinsWon}
                      </span>
                      <span className="text-xs text-[#8A5A10] ml-1">
                        ({player.totalSkinValue})
                      </span>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>

          {/* Hole-by-hole inline ledger */}
          <div className="pt-3 border-t border-gray-100">
            <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs font-mono">
              {grossPar3Skins.holeResults.map((hole, idx) => {
                const carrying = runningCarryByHole[idx];
                const notOptedInNote = hole.notOptedInPlayerId != null
                  ? `${hole.notOptedInPlayerName} shot ${hole.notOptedInGrossScore} but isn't opted in to par-3 gross skins`
                  : null;
                const baseTitle = hole.skinValue > 0
                  ? `${hole.winnerPlayerName} won with gross ${hole.winningGrossScore}${hole.wasCarryover ? ' (carryover)' : ''}`
                  : `Tie — ${carrying} skin${carrying === 1 ? '' : 's'} carrying`;
                return (
                  <span
                    key={hole.holeNumber}
                    className={cn(
                      'inline-flex items-center gap-1',
                      hole.skinValue > 0
                        ? 'text-[#8A5A10] font-semibold'
                        : 'text-gray-400'
                    )}
                    title={notOptedInNote ? `${baseTitle} — ${notOptedInNote}` : baseTitle}
                  >
                    <span>H{hole.holeNumber}:</span>
                    {hole.skinValue > 0 ? (
                      <span className="text-gray-900 font-bold">{hole.skinValue}</span>
                    ) : (
                      <span>↻{carrying}</span>
                    )}
                    {notOptedInNote && <span className="text-gray-400">*</span>}
                  </span>
                );
              })}
            </div>
            {grossPar3Skins.holeResults.some((h) => h.notOptedInPlayerId != null) && (
              <p className="mt-1.5 text-xs text-gray-400">
                * A player with the best score wasn&apos;t opted in to par-3 gross skins for this half.
              </p>
            )}
          </div>
        </div>
        <ClosestToPinPanel roundId={roundId} />
      </div>
    </div>
  );
}
