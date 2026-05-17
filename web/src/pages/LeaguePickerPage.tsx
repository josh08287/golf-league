import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowRight, Flag } from 'lucide-react';
import { apiClient } from '@/lib/api';

interface LeagueItem {
  id: number;
  name: string;
  slug: string;
}

export function LeaguePickerPage() {
  const [leagues, setLeagues] = useState<LeagueItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  useEffect(() => {
    apiClient
      .get<{ data: LeagueItem[] }>('/leagues')
      .then((res) => setLeagues(res.data.data))
      .catch(() => setError(true))
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">
      {/* Header */}
      <header className="border-b border-gray-200 bg-white shadow-sm">
        <div className="container mx-auto max-w-screen-xl px-4 h-16 flex items-center gap-2">
          <span className="text-2xl" role="img" aria-label="golf flag">⛳</span>
          <span className="font-bold text-lg text-primary-900">Golf League</span>
        </div>
      </header>

      {/* Body */}
      <main className="flex-1 flex items-center justify-center px-4 py-16">
        <div className="w-full max-w-lg">
          <div className="text-center mb-10">
            <span className="text-5xl" role="img" aria-label="golf">⛳</span>
            <h1 className="mt-4 text-3xl font-extrabold tracking-tight text-gray-900">
              Select your league
            </h1>
            <p className="mt-2 text-gray-500">
              Choose the league you want to visit.
            </p>
          </div>

          {loading && (
            <div className="flex justify-center">
              <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary-900 border-t-transparent" />
            </div>
          )}

          {error && !loading && (
            <p className="text-center text-sm text-red-600">
              Could not load leagues. Please try again.
            </p>
          )}

          {!loading && !error && leagues.length === 0 && (
            <p className="text-center text-sm text-gray-500">
              No leagues are available yet.
            </p>
          )}

          {!loading && !error && leagues.length > 0 && (
            <ul className="space-y-3">
              {leagues.map((league) => (
                <li key={league.id}>
                  <Link
                    to={`/${league.slug}`}
                    className="group flex items-center justify-between rounded-xl border border-gray-200 bg-white px-6 py-5 shadow-sm transition-all hover:border-primary-900 hover:shadow-md"
                  >
                    <div className="flex items-center gap-4">
                      <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-primary-50 text-primary-900 group-hover:bg-primary-900 group-hover:text-white transition-colors">
                        <Flag className="h-5 w-5" />
                      </div>
                      <span className="text-base font-semibold text-gray-900 group-hover:text-primary-900 transition-colors">
                        {league.name}
                      </span>
                    </div>
                    <ArrowRight className="h-5 w-5 text-gray-400 group-hover:text-primary-900 transition-colors" />
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </div>
      </main>

      <footer className="border-t border-gray-200 bg-white py-6 text-center text-sm text-gray-400">
        &copy; {new Date().getFullYear()} Golf League
      </footer>
    </div>
  );
}
