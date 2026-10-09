import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import axios from 'axios';
import { useAuthStore } from '@/store/authStore';
import { useActiveLeagueStore } from '@/store/activeLeagueStore';
import {
  clearAuth,
  getCurrentUser,
  hasStoredSession,
  isAuthenticated as isAuthed,
  login as loginApi,
  logout as logoutApi,
  type AuthResponse,
} from '@/lib/auth';

/** Delay between bootstrap retries after a transient (non-401) failure, e.g.
 * a cold Function App / SQL instance not yet warm. Covers ~32s of delay
 * across 6 attempts, which alongside request timeouts comfortably bridges
 * Azure SQL Serverless auto-pause resumes (typically 30-50s). */
const BOOTSTRAP_RETRY_DELAYS_MS = [1500, 2500, 4000, 6000, 8000, 10000];

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export function useAuth() {
  const user = useAuthStore((s) => s.user);
  const setUser = useAuthStore((s) => s.setUser);
  const clearUser = useAuthStore((s) => s.clearUser);
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [bootstrapping, setBootstrapping] = useState(() => (isAuthed() || hasStoredSession()) && !user);
  const [connectionError, setConnectionError] = useState(false);
  const [retryTrigger, setRetryTrigger] = useState(0);

  const retry = useCallback(() => {
    setConnectionError(false);
    setBootstrapping(true);
    setRetryTrigger((prev) => prev + 1);
  }, []);

  useEffect(() => {
    if (!isAuthed() && !hasStoredSession()) {
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
          setConnectionError(false);
          break;
        } catch (err) {
          if (cancelled) return;

          // getCurrentUser() attempts a token refresh if the access token was
          // rejected with 401. If it STILL threw 401 and hasStoredSession() is false,
          // the server actually rejected the refresh token — genuinely logged out.
          if (axios.isAxiosError(err) && err.response?.status === 401 && !hasStoredSession()) {
            clearAuth();
            clearUser();
            break;
          }

          if (attempt >= BOOTSTRAP_RETRY_DELAYS_MS.length) {
            // Exhausted retries due to cold start or network error. Leave stored
            // tokens in place so the session isn't wiped, but stop blocking
            // the UI and indicate a connection error.
            setConnectionError(true);
            break;
          }

          await sleep(BOOTSTRAP_RETRY_DELAYS_MS[attempt]);
        }
      }
      if (!cancelled) setBootstrapping(false);
    })();
    return () => { cancelled = true; };
  }, [user, setUser, retryTrigger]);

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
    connectionError,
    retry,
    login,
    logout,
    onLoginSuccess: handleLoginSuccess,
  };
}
