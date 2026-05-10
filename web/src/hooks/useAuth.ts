import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { useAuthStore } from '@/store/authStore';
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

  // On first mount, if we have a stored token but no user object, hydrate from
  // the current-user endpoint. Also runs when the access token changes (e.g.
  // after a refresh) but skips if the store already has the right user.
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
          role: me.role,
          playerId: me.playerId != null ? String(me.playerId) : null,
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
      // Caller must handle the MFA path; we just stash the challenge token
      // for the /auth/mfa page.
      sessionStorage.setItem('golf-league-mfa-token', resp.accessToken);
      navigate('/auth/mfa', { replace: true });
      return;
    }
    const me = await getCurrentUser();
    setUser({
      name: me.email,
      email: me.email,
      role: me.role,
      playerId: me.playerId != null ? String(me.playerId) : null,
    });
    await queryClient.invalidateQueries();
  }, [navigate, setUser, queryClient]);

  const login = useCallback(async (email: string, password: string) => {
    const resp = await loginApi(email, password);
    await handleLoginSuccess(resp);
    return resp;
  }, [handleLoginSuccess]);

  const logout = useCallback(async () => {
    await logoutApi();
    clearUser();
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
