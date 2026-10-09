import { useEffect, useState } from 'react';
import { Outlet, useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { LeagueContext } from '@/context/LeagueContext';
import { useActiveLeagueStore } from '@/store/activeLeagueStore';
import { useMyLeagues } from '@/hooks/useMyLeagues';
import { useAuth } from '@/hooks/useAuth';
import { getTokenLeagueId, refresh, getCurrentUser, hasStoredSession, clearAuth } from '@/lib/auth';
import { useAuthStore } from '@/store/authStore';
import { Spinner } from '@/components/ui/Spinner';

export function AppRoute() {
  const { user, bootstrapping, retry } = useAuth();
  const navigate = useNavigate();
  const activeLeague = useActiveLeagueStore((s) => s.activeLeague);
  const setActiveLeague = useActiveLeagueStore((s) => s.setActiveLeague);
  const setUser = useAuthStore((s) => s.setUser);
  const queryClient = useQueryClient();
  // True while we're waiting for the token to be scoped to the active league.
  const [tokenSyncing, setTokenSyncing] = useState(false);

  const { data: leaguesData, isLoading: leaguesLoading } = useMyLeagues(!!user && !bootstrapping);

  // Once league list is loaded, ensure an active league is selected.
  useEffect(() => {
    if (!leaguesData || bootstrapping) return;
    const { leagues } = leaguesData;
    if (leagues.length === 0) return;

    if (activeLeague) {
      const stillMember = leagues.some((l) => l.leagueId === activeLeague.leagueId);
      if (stillMember) return;
    }

    const pick = leagues[0];
    setActiveLeague({ leagueId: pick.leagueId, name: pick.name, slug: pick.slug });
  }, [leaguesData, bootstrapping, activeLeague, setActiveLeague]);

  // If the active league changed and the JWT doesn't match, re-issue the token
  // before rendering protected content.
  useEffect(() => {
    // tokenSyncing is checked inside to bail out early without depending on the
    // state value in the deps array (which would cause an extra re-run).
    if (!activeLeague) return;
    const tokenLeagueId = getTokenLeagueId();
    if (tokenLeagueId === activeLeague.leagueId) return;

    setTokenSyncing(true);
    void (async () => {
      try {
        const newToken = await refresh(activeLeague.leagueId);
        if (newToken && getTokenLeagueId() === activeLeague.leagueId) {
          const me = await getCurrentUser();
          setUser({
            name: me.email,
            email: me.email,
            roles: me.roles ?? [],
            playerId: me.playerId != null ? String(me.playerId) : null,
            isSuperAdmin: me.isSuperAdmin ?? false,
          });
          // Clear cached query data so everything refetches with the new
          // league-scoped token. Use a soft navigate instead of a hard reload
          // so the interceptor in-memory state is preserved.
          queryClient.clear();
          navigate('/', { replace: true });
        }
      } finally {
        setTokenSyncing(false);
      }
    })();
    // Only re-run when the active league changes, not when user/setUser changes
    // (setUser is called inside this same effect, which would cause a loop).
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeLeague?.leagueId]);

  useEffect(() => {
    if (!bootstrapping && !user && !hasStoredSession()) {
      navigate('/login', { replace: true });
    }
  }, [bootstrapping, user, navigate]);

  const leaguesResolved = !!leaguesData;
  const hasNoLeagues = leaguesResolved && leaguesData.leagues.length === 0 && !leaguesData.isSuperAdmin;

  if (bootstrapping || leaguesLoading || tokenSyncing || (user && !activeLeague && !hasNoLeagues)) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <Spinner />
      </div>
    );
  }

  if (!user) {
    if (hasStoredSession()) {
      return (
        <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-gray-50 px-4 text-center">
          <span className="text-5xl" role="img" aria-label="golf flag">⛳</span>
          <h1 className="text-xl font-bold text-gray-900">Connecting to server…</h1>
          <p className="max-w-md text-sm text-gray-500">
            The application is resuming from standby. This can take a moment on cold start.
          </p>
          <div className="mt-2 flex gap-3">
            <button
              onClick={retry}
              className="rounded-lg bg-[#1B5E20] px-4 py-2 text-sm font-medium text-white hover:bg-[#154a19] focus:outline-none focus:ring-2 focus:ring-[#1B5E20] focus:ring-offset-2"
            >
              Try Again
            </button>
            <button
              onClick={() => {
                clearAuth();
                navigate('/login', { replace: true });
              }}
              className="rounded-lg border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[#1B5E20] focus:ring-offset-2"
            >
              Sign In Again
            </button>
          </div>
        </div>
      );
    }
    return null;
  }

  if (hasNoLeagues) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-4 text-center bg-gray-50 px-4">
        <span className="text-5xl">⛳</span>
        <h1 className="text-xl font-bold text-gray-900">You're in — welcome!</h1>
        <p className="text-sm text-gray-500 max-w-xs">
          Your account has been created. The league admin will add you to the roster shortly.
          Try refreshing in a moment.
        </p>
        <button
          className="mt-2 text-sm text-[#1B5E20] underline"
          onClick={() => window.location.reload()}
        >
          Refresh
        </button>
      </div>
    );
  }

  return (
    <LeagueContext.Provider value={{ league: activeLeague, loading: false }}>
      <Outlet />
    </LeagueContext.Provider>
  );
}
