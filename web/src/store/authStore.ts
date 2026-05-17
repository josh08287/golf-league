import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export type UserRole = 'admin' | 'scorer' | 'player';

export interface AuthUser {
  name: string;
  email: string;
  roles: UserRole[];
  playerId: string | null;
  isSuperAdmin: boolean;
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

/** Convenience: returns true if the user holds the given role. */
export const hasRole = (user: AuthUser | null, role: UserRole): boolean =>
  !!user?.roles?.includes(role);
