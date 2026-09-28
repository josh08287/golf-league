import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import axios from 'axios';
import { useAuthStore } from '@/store/authStore';
import { useActiveLeagueStore } from '@/store/activeLeagueStore';
import {
  clearAuth,
  getCurrentUser,
  isAuthenticated as isAuthed,
  login as loginApi,
  logout as logoutApi,
  type AuthResponse,
} from '@/lib/auth';

/** Delay between bootstrap retries after a transient (non-401) failure, e.g.
 * a cold Function App / SQL instance not yet warm — not exponential, since
 * this only needs to bridge a single cold-start window, not survive a real
 * outage. */
const BOOTSTRAP_RETRY_DELAYS_MS = [1500, 3000, 5000];

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export function useAuth() {
  const user = useAuthStore((s) => s.user);
  const setUser = useAuthStore((s) => s.setUser);
  const clearUser = useAuthStore((s) => s.clearUser);
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [bootstrapping, setBootstrapping] = useState(() => isAuthed() && !user);

  useEffect(() => {
    if (!isAuthed()) {
      setBootstrapping(false);
      return;
    }
    if (user) {
      setBootstrapping(false);
      return;
    }
    let cancelled = false;
    void (async () => {
      // Retries here bridge a cold Function App / SQL instance waking up —
      // the very first request after a period of inactivity is the one most
      // likely to time out or 503. A real auth rejection (401) never
      // retries; it fails fast below.
      for (let attempt = 0; ; attempt++) {
        try {
          const me = await getCurrentUser();
          if (cancelled) return;
          setUser({
            name: me.email,
            email: me.email,
            roles: me.roles ?? [],
            playerId: me.playerId != null ? String(me.playerId) : null,
            isSuperAdmin: me.isSuperAdmin ?? false,
          });
          break;
        } catch (err) {
          if (cancelled) return;

          if (axios.isAxiosError(err) && err.response?.status === 401) {
            // The server actually rejected the access token — genuinely
            // logged out, not a transient cold-start failure.
            clearAuth();
            clearUser();
            break;
          }

          if (attempt >= BOOTSTRAP_RETRY_DELAYS_MS.length) {
            // Exhausted retries. Leave stored tokens in place (this wasn't a
            // rejection) so a manual refresh can still recover once the
            // backend is warm, but stop blocking the UI on it.
            break;
          }

          await sleep(BOOTSTRAP_RETRY_DELAYS_MS[attempt]);
        }
      }
      if (!cancelled) setBootstrapping(false);
    })();
    return () => { cancelled = true; };
  }, [user, setUser]);

  const handleLoginSuccess = useCallback(async (resp: AuthResponse) => {
    if (resp.mfaRequired) {
      sessionStorage.setItem('golf-league-mfa-token', resp.accessToken);
      sessionStorage.setItem(
        'golf-league-mfa-enrollment-required',
        resp.mfaEnrollmentRequired ? '1' : '0',
      );
      navigate(resp.mfaEnrollmentRequired ? '/auth/mfa/enroll' : '/auth/mfa', { replace: true });
      return;
    }
    const me = await getCurrentUser();
    setUser({
      name: me.email,
      email: me.email,
      roles: me.roles ?? [],
      playerId: me.playerId != null ? String(me.playerId) : null,
      isSuperAdmin: me.isSuperAdmin ?? false,
    });
    await queryClient.invalidateQueries();
  }, [navigate, setUser, queryClient]);

  const login = useCallback(async (email: string, password: string, leagueId?: number) => {
    const resp = await loginApi(email, password, leagueId);
    await handleLoginSuccess(resp);
    return resp;
  }, [handleLoginSuccess]);

  const logout = useCallback(async () => {
    await logoutApi();
    clearUser();
    useActiveLeagueStore.getState().setActiveLeague(null);
    queryClient.clear();
    navigate('/login', { replace: true });
  }, [clearUser, navigate, queryClient]);

  return {
    user,
    isAuthenticated: !!user,
    bootstrapping,
    login,
    logout,
    onLoginSuccess: handleLoginSuccess,
  };
}
