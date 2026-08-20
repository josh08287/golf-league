import { useState } from 'react';
import { Link, NavLink, useNavigate } from 'react-router-dom';
import { Menu, X, LogOut, User, ChevronDown, Check, Plus } from 'lucide-react';
import { useAuth } from '@/hooks/useAuth';
import { useInvites } from '@/hooks/admin/useInvites';
import { useLeagueName } from '@/context/LeagueContext';
import { useMyLeagues } from '@/hooks/useMyLeagues';
import { useFeatureFlagStates } from '@/hooks/admin/useFeatureFlags';
import { useActiveRoundLeaderboardPresence } from '@/hooks/useRounds';
import { FEATURE_FLAG_KEYS } from '@/types/api';
import { useActiveLeagueStore } from '@/store/activeLeagueStore';
import { Button } from '@/components/ui/Button';
import { cn } from '@/lib/utils';
import { apiClient } from '@/lib/api';
import { useQueryClient } from '@tanstack/react-query';
import { myLeaguesKeys } from '@/hooks/useMyLeagues';

export function NavBar() {
  const [mobileOpen, setMobileOpen] = useState(false);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [addLeagueOpen, setAddLeagueOpen] = useState(false);
  const [newLeagueName, setNewLeagueName] = useState('');
  const [newLeagueSlug, setNewLeagueSlug] = useState('');
  const [addLeagueError, setAddLeagueError] = useState('');
  const [addLeagueLoading, setAddLeagueLoading] = useState(false);
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const leagueName = useLeagueName();
  const activeLeague = useActiveLeagueStore((s) => s.activeLeague);
  const setActiveLeague = useActiveLeagueStore((s) => s.setActiveLeague);
  const { data: leaguesData } = useMyLeagues(!!user);
  const queryClient = useQueryClient();

  const leagues = leaguesData?.leagues ?? [];
  const isSuperAdmin = leaguesData?.isSuperAdmin ?? user?.isSuperAdmin ?? false;
  const showPicker = leagues.length > 1 || isSuperAdmin;

  function switchLeague(leagueId: number) {
    const found = leagues.find((l) => l.leagueId === leagueId);
    if (!found) return;
    setPickerOpen(false);
    setActiveLeague({ leagueId: found.leagueId, name: found.name, slug: found.slug });
  }

  function openAddLeague() {
    setPickerOpen(false);
    setNewLeagueName('');
    setNewLeagueSlug('');
    setAddLeagueError('');
    setAddLeagueOpen(true);
  }

  function handleNameChange(name: string) {
    setNewLeagueName(name);
    setNewLeagueSlug(name.toLowerCase().replace(/\s+/g, '-').replace(/[^a-z0-9-]/g, ''));
  }

  async function submitAddLeague(e: React.FormEvent) {
    e.preventDefault();
    setAddLeagueError('');
    setAddLeagueLoading(true);
    try {
      await apiClient.post('/leagues', { name: newLeagueName.trim(), slug: newLeagueSlug.trim() });
      await queryClient.invalidateQueries({ queryKey: myLeaguesKeys.all });
      setAddLeagueOpen(false);
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ??
        'Failed to create league.';
      setAddLeagueError(msg);
    } finally {
      setAddLeagueLoading(false);
    }
  }

  const featureFlags = useFeatureFlagStates();
  const leaderboardFlagEnabled = featureFlags.data?.[FEATURE_FLAG_KEYS.activeRoundLeaderboardEnabled] ?? false;
  const activeRoundLeaderboard = useActiveRoundLeaderboardPresence(leaderboardFlagEnabled);
  const showLeaderboardLink = leaderboardFlagEnabled && activeRoundLeaderboard.data != null;

  const publicLinks = [
    { to: '/', label: 'Home' },
    ...(showLeaderboardLink ? [{ to: '/leaderboard', label: 'Leaderboard' }] : []),
    { to: '/flights', label: 'Standings' },
    { to: '/rounds', label: 'Rounds' },
    { to: '/players', label: 'Players' },
    { to: '/statistics', label: 'Statistics' },
  ];

  const authedLinks = [
    { to: '/tee-times', label: 'Tee Times' },
  ];

  const isAdmin = user?.roles?.includes('admin') ?? false;
  const { data: invites } = useInvites();
  const pendingInviteCount = isAdmin
    ? (invites?.filter((i) => i.status === 'Pending' && new Date(i.expiresAt) >= new Date()).length ?? 0)
    : 0;

  const navLinkClass = ({ isActive }: { isActive: boolean }) =>
    cn(
      'text-sm font-medium transition-colors',
      isActive
        ? 'text-primary-900 font-semibold underline underline-offset-4'
        : 'text-gray-600 hover:text-primary-900',
    );

  const links = [
    ...publicLinks,
    ...(user ? authedLinks : []),
    ...(isAdmin ? [{ to: '/admin', label: 'Admin' }] : []),
  ];

  return (
    <>
      <header className="sticky top-0 z-50 border-b border-gray-200 bg-white shadow-sm">
        <div className="container flex h-16 max-w-screen-xl items-center justify-between px-4">
          {/* Logo / League name with optional picker */}
          <div className="relative flex items-center gap-2">
            <Link
              to="/"
              className="flex items-center gap-2 text-primary-900 font-bold text-lg hover:opacity-80 transition-opacity"
            >
              <span className="text-2xl" role="img" aria-label="golf flag">⛳</span>
              <span className="hidden sm:inline">{leagueName || 'Golf League'}</span>
            </Link>
            {showPicker && user && (
              <div className="relative">
                <button
                  onClick={() => setPickerOpen((o) => !o)}
                  className="ml-1 flex items-center gap-0.5 text-gray-400 hover:text-primary-900 transition-colors"
                  aria-label="Switch league"
                >
                  <ChevronDown className="h-4 w-4" />
                </button>
                {pickerOpen && (
                  <>
                    <div className="fixed inset-0 z-10" onClick={() => setPickerOpen(false)} />
                    <div className="absolute left-0 top-7 z-20 min-w-[200px] rounded-lg border border-gray-200 bg-white shadow-lg py-1">
                      {leagues.map((l) => (
                        <button
                          key={l.leagueId}
                          onClick={() => switchLeague(l.leagueId)}
                          className="flex w-full items-center gap-2 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50"
                        >
                          {activeLeague?.leagueId === l.leagueId && (
                            <Check className="h-4 w-4 text-primary-900 shrink-0" />
                          )}
                          <span className={activeLeague?.leagueId === l.leagueId ? 'ml-0' : 'ml-6'}>
                            {l.name}
                          </span>
                        </button>
                      ))}
                      {isSuperAdmin && (
                        <>
                          {leagues.length > 0 && <div className="my-1 border-t border-gray-100" />}
                          <button
                            onClick={openAddLeague}
                            className="flex w-full items-center gap-2 px-4 py-2 text-sm text-primary-700 hover:bg-primary-50"
                          >
                            <Plus className="h-4 w-4 shrink-0" />
                            Add new league
                          </button>
                        </>
                      )}
                    </div>
                  </>
                )}
              </div>
            )}
          </div>

          {/* Desktop nav */}
          <nav className="hidden md:flex items-center gap-6">
            {links.map((link) => (
              <NavLink key={link.to} to={link.to} className={navLinkClass} end={link.to === '/'}>
                <span className="relative">
                  {link.label}
                  {link.to === '/admin' && pendingInviteCount > 0 && (
                    <span className="absolute -right-4 -top-1.5 flex h-4 w-4 items-center justify-center rounded-full bg-amber-500 text-[10px] font-bold text-white">
                      {pendingInviteCount}
                    </span>
                  )}
                </span>
              </NavLink>
            ))}
          </nav>

          {/* Desktop auth */}
          <div className="hidden md:flex items-center gap-3">
            {user ? (
              <div className="flex items-center gap-3">
                {user.playerId ? (
                  <Link
                    to={`/players/${user.playerId}`}
                    className="flex items-center gap-1.5 text-sm text-gray-600 hover:text-primary-900 transition-colors"
                  >
                    <User className="h-4 w-4" />
                    {user.name}
                  </Link>
                ) : (
                  <span className="flex items-center gap-1.5 text-sm text-gray-600">
                    <User className="h-4 w-4" />
                    {user.name}
                  </span>
                )}
                <Button variant="outline" size="sm" onClick={() => void logout()}>
                  <LogOut className="h-4 w-4 mr-1.5" />
                  Sign out
                </Button>
              </div>
            ) : (
              <div className="flex items-center gap-2">
                <Button size="sm" onClick={() => navigate('/login')}>
                  Sign in
                </Button>
              </div>
            )}
          </div>

          {/* Mobile hamburger */}
          <button
            className="md:hidden p-2 rounded-md text-gray-600 hover:bg-gray-100"
            onClick={() => setMobileOpen((o) => !o)}
            aria-label={mobileOpen ? 'Close menu' : 'Open menu'}
          >
            {mobileOpen ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
          </button>
        </div>

        {/* Mobile menu */}
        {mobileOpen && (
          <div className="md:hidden border-t border-gray-200 bg-white px-4 pb-4 pt-2">
            <nav className="flex flex-col gap-1">
              {links.map((link) => (
                <NavLink
                  key={link.to}
                  to={link.to}
                  className={({ isActive }) =>
                    cn(
                      'rounded-md px-3 py-2 text-sm font-medium',
                      isActive
                        ? 'bg-primary-50 text-primary-900'
                        : 'text-gray-700 hover:bg-gray-50',
                    )
                  }
                  end={link.to === '/'}
                  onClick={() => setMobileOpen(false)}
                >
                  {link.label}
                </NavLink>
              ))}
              {showPicker && user && (
                <div className="mt-2 border-t border-gray-100 pt-2">
                  <p className="px-3 text-xs font-medium text-gray-400 uppercase tracking-wide mb-1">Switch league</p>
                  {leagues.map((l) => (
                    <button
                      key={l.leagueId}
                      onClick={() => { setMobileOpen(false); switchLeague(l.leagueId); }}
                      className="flex w-full items-center gap-2 rounded-md px-3 py-2 text-sm text-gray-700 hover:bg-gray-50"
                    >
                      {activeLeague?.leagueId === l.leagueId && <Check className="h-4 w-4 text-primary-900" />}
                      <span className={activeLeague?.leagueId === l.leagueId ? '' : 'ml-6'}>{l.name}</span>
                    </button>
                  ))}
                  {isSuperAdmin && (
                    <button
                      onClick={() => { setMobileOpen(false); openAddLeague(); }}
                      className="flex w-full items-center gap-2 rounded-md px-3 py-2 text-sm text-primary-700 hover:bg-primary-50"
                    >
                      <Plus className="h-4 w-4 shrink-0" />
                      Add new league
                    </button>
                  )}
                </div>
              )}
            </nav>
            <div className="mt-3 border-t border-gray-100 pt-3">
              {user ? (
                <div className="flex flex-col gap-2">
                  {user.playerId ? (
                    <Link
                      to={`/players/${user.playerId}`}
                      className="text-sm text-gray-700 px-3 hover:text-primary-900 transition-colors"
                      onClick={() => setMobileOpen(false)}
                    >
                      {user.name}
                    </Link>
                  ) : (
                    <span className="text-sm text-gray-500 px-3">{user.name}</span>
                  )}
                  <Button
                    variant="outline"
                    size="sm"
                    className="w-full"
                    onClick={() => { setMobileOpen(false); void logout(); }}
                  >
                    <LogOut className="h-4 w-4 mr-1.5" />
                    Sign out
                  </Button>
                </div>
              ) : (
                <Button
                  size="sm"
                  className="w-full"
                  onClick={() => { setMobileOpen(false); navigate('/login'); }}
                >
                  Sign in
                </Button>
              )}
            </div>
          </div>
        )}
      </header>

      {/* Add New League Modal */}
      {addLeagueOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center">
          <div className="fixed inset-0 bg-black/40" onClick={() => setAddLeagueOpen(false)} />
          <div className="relative z-10 w-full max-w-md rounded-xl bg-white shadow-xl p-6 mx-4">
            <h2 className="text-lg font-semibold text-gray-900 mb-4">Add New League</h2>
            <form onSubmit={(e) => void submitAddLeague(e)} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">League name</label>
                <input
                  type="text"
                  required
                  value={newLeagueName}
                  onChange={(e) => handleNameChange(e.target.value)}
                  placeholder="e.g. Capital Golf League"
                  className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Slug</label>
                <input
                  type="text"
                  required
                  value={newLeagueSlug}
                  onChange={(e) => setNewLeagueSlug(e.target.value.toLowerCase().replace(/[^a-z0-9-]/g, ''))}
                  placeholder="e.g. capital"
                  className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-primary-500"
                />
                <p className="mt-1 text-xs text-gray-400">Lowercase letters, numbers, and hyphens only.</p>
              </div>
              {addLeagueError && (
                <p className="text-sm text-red-600">{addLeagueError}</p>
              )}
              <div className="flex justify-end gap-2 pt-2">
                <Button type="button" variant="outline" size="sm" onClick={() => setAddLeagueOpen(false)}>
                  Cancel
                </Button>
                <Button type="submit" size="sm" disabled={addLeagueLoading}>
                  {addLeagueLoading ? 'Creating…' : 'Create league'}
                </Button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}
