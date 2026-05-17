import { useEffect, useState } from 'react';
import { Outlet, useParams } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { LeagueContext, type LeagueInfo } from '@/context/LeagueContext';
import { apiClient, setLeagueSlug } from '@/lib/api';
import { getTokenLeagueId, isAuthenticated, refresh, getCurrentUser } from '@/lib/auth';
import { useAuthStore } from '@/store/authStore';

interface LeagueResponse {
  id: number;
  name: string;
  slug: string;
  isActive: boolean;
}

export function LeagueSlugRoute() {
  const { leagueSlug } = useParams<{ leagueSlug: string }>();
  const [league, setLeague] = useState<LeagueInfo | null>(null);
  const [loading, setLoading] = useState(true);
  const [notFound, setNotFound] = useState(false);
  const setUser = useAuthStore((s) => s.setUser);
  const queryClient = useQueryClient();

  useEffect(() => {
    if (!leagueSlug) {
      setNotFound(true);
      setLoading(false);
      return;
    }

    setLoading(true);
    setNotFound(false);

    void (async () => {
      try {
        const res = await apiClient.get<{ data: LeagueResponse }>(`/leagues/${leagueSlug}`);
        const l = res.data.data;

        // If the user is logged in but their token is scoped to a different
        // league, silently re-issue the token for this league so all API
        // requests filter to the correct dataset.
        const currentLeagueId = getTokenLeagueId();
        if (isAuthenticated() && currentLeagueId !== l.id) {
          const newToken = await refresh(leagueSlug);
          if (newToken) {
            // Only update the store if the new token actually has this league's
            // id — if it's still null the user has no membership here.
            if (getTokenLeagueId() === l.id) {
              const me = await getCurrentUser();
              setUser({
                name: me.email,
                email: me.email,
                roles: me.roles ?? [],
                playerId: me.playerId != null ? String(me.playerId) : null,
                isSuperAdmin: me.isSuperAdmin ?? false,
              });
              queryClient.clear();
            }
          }
        }

        setLeagueSlug(l.slug);
        setLeague({ leagueId: l.id, slug: l.slug, name: l.name });
      } catch (err) {
        if ((err as { response?: { status?: number } })?.response?.status === 404) {
          setNotFound(true);
        }
      } finally {
        setLoading(false);
      }
    })();
    return () => { setLeagueSlug(null); };
  }, [leagueSlug, setUser, queryClient]);

  if (loading) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary-900 border-t-transparent" />
      </div>
    );
  }

  if (notFound) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-4 text-center px-4">
        <span className="text-5xl">⛳</span>
        <h1 className="text-2xl font-bold text-gray-900">League not found</h1>
        <p className="text-gray-500">
          There's no league at <strong>/{leagueSlug}</strong>.
        </p>
      </div>
    );
  }

  return (
    <LeagueContext.Provider value={{ league, loading, notFound }}>
      <Outlet />
    </LeagueContext.Provider>
  );
}
