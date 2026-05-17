import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowRight, Flag, Plus, X } from 'lucide-react';
import { apiClient } from '@/lib/api';
import { useAuthStore } from '@/store/authStore';
import { Button } from '@/components/ui/Button';

interface LeagueItem {
  id: number;
  name: string;
  slug: string;
}

function AddLeagueModal({
  onClose,
  onCreated,
}: {
  onClose: () => void;
  onCreated: (league: LeagueItem) => void;
}) {
  const [name, setName] = useState('');
  const [slug, setSlug] = useState('');
  const [slugTouched, setSlugTouched] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const nameRef = useRef<HTMLInputElement>(null);

  useEffect(() => { nameRef.current?.focus(); }, []);

  function toSlug(value: string) {
    return value.toLowerCase().replace(/[^a-z0-9-]/g, '-').replace(/-+/g, '-').replace(/^-|-$/g, '');
  }

  function handleNameChange(value: string) {
    setName(value);
    if (!slugTouched) setSlug(toSlug(value));
  }

  function handleSlugChange(value: string) {
    setSlugTouched(true);
    setSlug(toSlug(value));
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!name.trim() || !slug) return;
    setSaving(true);
    setError(null);
    try {
      const res = await apiClient.post<{ data: LeagueItem }>('/leagues', { name: name.trim(), slug });
      onCreated(res.data.data);
    } catch (err) {
      const msg = (err as { response?: { data?: { error?: string } } })?.response?.data?.error
        ?? 'Failed to create league.';
      setError(msg);
    } finally {
      setSaving(false);
    }
  }

  const inputClass = 'mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-primary-900 focus:outline-none focus:ring-1 focus:ring-primary-900';

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4">
      <div className="w-full max-w-md rounded-xl bg-white p-6 shadow-xl">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-bold text-gray-900">Add New League</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            <X className="h-5 w-5" />
          </button>
        </div>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700">League name</label>
            <input
              ref={nameRef}
              type="text"
              value={name}
              onChange={(e) => handleNameChange(e.target.value)}
              className={inputClass}
              placeholder="Capital Golf League"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700">Slug</label>
            <input
              type="text"
              value={slug}
              onChange={(e) => handleSlugChange(e.target.value)}
              className={inputClass}
              placeholder="capital"
            />
            <p className="mt-1 text-xs text-gray-400">
              URL path: <span className="font-mono">/{slug || '…'}</span>
            </p>
          </div>
          {error && <p className="text-sm text-red-600">{error}</p>}
          <div className="flex justify-end gap-3 pt-2">
            <Button type="button" variant="outline" onClick={onClose} disabled={saving}>
              Cancel
            </Button>
            <Button type="submit" disabled={saving || !name.trim() || !slug}>
              {saving ? 'Creating…' : 'Create league'}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}

export function LeaguePickerPage() {
  const [leagues, setLeagues] = useState<LeagueItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [showModal, setShowModal] = useState(false);
  const user = useAuthStore((s) => s.user);
  const isSuperAdmin = user?.isSuperAdmin ?? false;

  useEffect(() => {
    apiClient
      .get<{ data: LeagueItem[] }>('/leagues')
      .then((res) => setLeagues(res.data.data))
      .catch(() => setError(true))
      .finally(() => setLoading(false));
  }, []);

  function handleCreated(league: LeagueItem) {
    setLeagues((prev) => [...prev, league]);
    setShowModal(false);
  }

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

          {isSuperAdmin && !loading && (
            <div className="mt-6 flex justify-center">
              <Button
                variant="outline"
                onClick={() => setShowModal(true)}
                className="flex items-center gap-2"
              >
                <Plus className="h-4 w-4" />
                Add league
              </Button>
            </div>
          )}
        </div>
      </main>

      <footer className="border-t border-gray-200 bg-white py-6 text-center text-sm text-gray-400">
        &copy; {new Date().getFullYear()} Golf League
      </footer>

      {showModal && (
        <AddLeagueModal
          onClose={() => setShowModal(false)}
          onCreated={handleCreated}
        />
      )}
    </div>
  );
}
