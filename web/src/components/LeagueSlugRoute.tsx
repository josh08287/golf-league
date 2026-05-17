import { useEffect, useState } from 'react';
import { Outlet, useParams } from 'react-router-dom';
import { LeagueContext, type LeagueInfo } from '@/context/LeagueContext';
import { apiClient } from '@/lib/api';

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

  useEffect(() => {
    if (!leagueSlug) {
      setNotFound(true);
      setLoading(false);
      return;
    }

    setLoading(true);
    setNotFound(false);

    apiClient
      .get<{ data: LeagueResponse }>(`/leagues/${leagueSlug}`)
      .then((res) => {
        const l = res.data.data;
        setLeague({ leagueId: l.id, slug: l.slug, name: l.name });
      })
      .catch((err) => {
        if (err?.response?.status === 404) {
          setNotFound(true);
        }
      })
      .finally(() => setLoading(false));
  }, [leagueSlug]);

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
