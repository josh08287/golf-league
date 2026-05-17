import { useEffect, useState } from 'react';
import { Outlet, useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { LeagueContext } from '@/context/LeagueContext';
import { useActiveLeagueStore } from '@/store/activeLeagueStore';
import { useMyLeagues } from '@/hooks/useMyLeagues';
import { useAuth } from '@/hooks/useAuth';
import { getTokenLeagueId, refresh, getCurrentUser } from '@/lib/auth';
import { useAuthStore } from '@/store/authStore';
import { Spinner } from '@/components/ui/Spinner';

export function AppRoute() {
  const { user, bootstrapping } = useAuth();
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
    if (!activeLeague || !user) return;
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
          queryClient.clear();
          navigate('/', { replace: true });
        }
      } finally {
        setTokenSyncing(false);
      }
    })();
  }, [activeLeague, user, setUser, queryClient, navigate]);

  useEffect(() => {
    if (!bootstrapping && !user) {
      navigate('/login', { replace: true });
    }
  }, [bootstrapping, user, navigate]);

  if (bootstrapping || leaguesLoading || tokenSyncing || (user && !activeLeague)) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <Spinner />
      </div>
    );
  }

  if (!user) return null;

  return (
    <LeagueContext.Provider value={{ league: activeLeague, loading: false }}>
      <Outlet />
    </LeagueContext.Provider>
  );
}
