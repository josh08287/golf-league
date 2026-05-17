import { createContext, useContext } from 'react';
import type { ActiveLeague } from '@/store/activeLeagueStore';

export type { ActiveLeague as LeagueInfo };

interface LeagueContextValue {
  league: ActiveLeague | null;
  loading: boolean;
}

export const LeagueContext = createContext<LeagueContextValue>({
  league: null,
  loading: false,
});

export function useLeague(): LeagueContextValue {
  return useContext(LeagueContext);
}

export function useLeagueName(): string {
  const { league } = useLeague();
  return league?.name ?? '';
}

/** No-op prefix — kept for any remaining call sites during migration. */
export function useLeaguePrefix(): string {
  return '';
}
