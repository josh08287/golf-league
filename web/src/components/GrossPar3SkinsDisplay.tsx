import { Link } from 'react-router-dom';
import { Trophy } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { GrossPar3SkinsSummary } from '@/types/api';

interface GrossPar3SkinsDisplayProps {
  grossPar3Skins: GrossPar3SkinsSummary;
}

export function GrossPar3SkinsDisplay({ grossPar3Skins }: GrossPar3SkinsDisplayProps) {
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
    <div className="flex items-center justify-between mb-3 flex-wrap gap-y-1">
      <div className="flex items-center gap-2 flex-wrap">
        <Trophy className="h-5 w-5 text-amber-600" />
        <span className="font-semibold text-gray-900">Par 3 Gross Skins</span>
        <span className="text-sm text-gray-500">
          ({grossPar3Skins.totalHolesWithSkins}/{grossPar3Skins.par3HoleCount} holes awarded)
        </span>
        {incoming > 0 && (
          <span
            className="inline-flex items-center gap-1 rounded bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-800"
            title={`${incoming} skin(s) carried over from prior round(s)`}
          >
            ↻ {incoming} carryover from prior round{incoming === 1 ? '' : 's'}
          </span>
        )}
      </div>
      {grossPar3Skins.totalSkinValueAwarded > 0 && (
        <span className="text-sm text-amber-700 font-medium">
          Total Value: {grossPar3Skins.totalSkinValueAwarded}
        </span>
      )}
    </div>
  );

  if (grossPar3Skins.playerSummaries.length === 0) {
    return (
      <div className="bg-amber-50 border border-amber-200 rounded-lg p-4">
        {header}
        <div className="text-sm text-gray-600 italic">
          No gross skins awarded on par 3s — all holes were tied.
        </div>
      </div>
    );
  }

  return (
    <div className="bg-amber-50 border border-amber-200 rounded-lg p-4">
      {header}

      {/* Winners Grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3 mb-3">
        {grossPar3Skins.playerSummaries.map((player) => (
          <div
            key={player.playerId}
            className="flex items-center justify-between bg-white border border-amber-200 rounded-lg px-3 py-2"
          >
            <div className="flex items-center gap-2">
              <Trophy className="h-4 w-4 text-amber-500" />
              <Link
                to={`/players/${player.playerId}`}
                className="font-medium text-gray-900 hover:underline"
              >
                {player.playerName}
              </Link>
            </div>
            <div className="text-sm">
              <span className="font-semibold text-amber-700">{player.totalSkinsWon}</span>
              <span className="text-gray-500 ml-1">({player.totalSkinValue})</span>
            </div>
          </div>
        ))}
      </div>

      {/* Hole-by-hole breakdown */}
      <div className="flex flex-wrap gap-2 text-xs">
        {grossPar3Skins.holeResults.map((hole, idx) => {
          const carrying = runningCarryByHole[idx];
          return (
            <span
              key={hole.holeNumber}
              className={cn(
                'inline-flex items-center gap-1 px-2 py-1 rounded',
                hole.skinValue > 0
                  ? 'bg-amber-100 text-amber-800 font-medium'
                  : 'bg-gray-100 text-gray-500'
              )}
              title={
                hole.skinValue > 0
                  ? `${hole.winnerPlayerName} won with gross ${hole.winningGrossScore}${hole.wasCarryover ? ' (carryover)' : ''}`
                  : `Tie — ${carrying} skin${carrying === 1 ? '' : 's'} carrying`
              }
            >
              Hole {hole.holeNumber}
              {hole.skinValue > 0 ? (
                <>
                  : <Trophy className={cn('h-3 w-3', hole.wasCarryover ? 'text-amber-600' : 'text-amber-500')} />
                  {hole.skinValue}
                </>
              ) : (
                <span className="text-gray-400">↻ {carrying}</span>
              )}
            </span>
          );
        })}
      </div>
    </div>
  );
}
