import { useState } from 'react';
import { Link, NavLink, useNavigate } from 'react-router-dom';
import { Menu, X, LogOut, User, ChevronDown, Check } from 'lucide-react';
import { useAuth } from '@/hooks/useAuth';
import { useInvites } from '@/hooks/admin/useInvites';
import { useLeagueName } from '@/context/LeagueContext';
import { useMyLeagues } from '@/hooks/useMyLeagues';
import { useActiveLeagueStore } from '@/store/activeLeagueStore';
import { Button } from '@/components/ui/Button';
import { cn } from '@/lib/utils';

export function NavBar() {
  const [mobileOpen, setMobileOpen] = useState(false);
  const [pickerOpen, setPickerOpen] = useState(false);
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const leagueName = useLeagueName();
  const activeLeague = useActiveLeagueStore((s) => s.activeLeague);
  const setActiveLeague = useActiveLeagueStore((s) => s.setActiveLeague);
  const { data: leaguesData } = useMyLeagues(!!user);

  const leagues = leaguesData?.leagues ?? [];
  const showPicker = leagues.length > 1 || (leaguesData?.isSuperAdmin ?? false);

  function switchLeague(leagueId: number) {
    const found = leagues.find((l) => l.leagueId === leagueId);
    if (!found) return;
    setPickerOpen(false);
    setActiveLeague({ leagueId: found.leagueId, name: found.name, slug: found.slug });
    navigate('/', { replace: true });
  }

  const publicLinks = [
    { to: '/', label: 'Home' },
    { to: '/flights', label: 'Flights' },
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
            {showPicker && user && leagues.length > 1 && (
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
  );
}
