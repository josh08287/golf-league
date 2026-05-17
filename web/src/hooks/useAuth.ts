import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
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
      } catch {
        if (!cancelled) clearAuth();
      } finally {
        if (!cancelled) setBootstrapping(false);
      }
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
