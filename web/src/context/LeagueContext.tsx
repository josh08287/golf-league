import { createContext, useContext } from 'react';

export interface LeagueInfo {
  leagueId: number;
  slug: string;
  name: string;
}

interface LeagueContextValue {
  league: LeagueInfo | null;
  /** true while the league is being resolved from the API */
  loading: boolean;
  /** true when the slug in the URL does not match any known league */
  notFound: boolean;
}

export const LeagueContext = createContext<LeagueContextValue>({
  league: null,
  loading: true,
  notFound: false,
});

export function useLeague(): LeagueContextValue {
  return useContext(LeagueContext);
}

/** Returns just the league name, falling back to the slug while loading. */
export function useLeagueName(slug?: string): string {
  const { league } = useLeague();
  return league?.name ?? slug ?? '';
}
