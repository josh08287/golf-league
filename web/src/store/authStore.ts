import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export type UserRole = 'admin' | 'scorer' | 'player';

export interface AuthUser {
  name: string;
  email: string;
  role: UserRole;
  playerId: string | null;
}

interface AuthState {
  user: AuthUser | null;
  setUser: (user: AuthUser) => void;
  clearUser: () => void;
  /** Alias for clearUser — used by AdminLayout */
  logout: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      setUser: (user) => set({ user }),
      clearUser: () => set({ user: null }),
      logout: () => set({ user: null }),
    }),
    {
      name: 'golf-league-auth',
    },
  ),
);
