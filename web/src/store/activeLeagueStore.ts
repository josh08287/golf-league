import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export interface ActiveLeague {
  leagueId: number;
  name: string;
  slug: string;
}

interface ActiveLeagueState {
  activeLeague: ActiveLeague | null;
  setActiveLeague: (league: ActiveLeague | null) => void;
}

export const useActiveLeagueStore = create<ActiveLeagueState>()(
  persist(
    (set) => ({
      activeLeague: null,
      setActiveLeague: (league) => set({ activeLeague: league }),
    }),
    { name: 'golf-league-active-league' },
  ),
);
