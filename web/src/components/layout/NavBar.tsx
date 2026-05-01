import { useState } from 'react';
import { Link, NavLink, useNavigate } from 'react-router-dom';
import { Menu, X, LogOut, User } from 'lucide-react';
import { useMsal } from '@azure/msal-react';
import { useAuthStore } from '@/store/authStore';
import { Button } from '@/components/ui/Button';
import { cn } from '@/lib/utils';

const publicLinks = [
  { to: '/', label: 'Home' },
  { to: '/flights', label: 'Flights' },
  { to: '/rounds', label: 'Rounds' },
];

export function NavBar() {
  const [mobileOpen, setMobileOpen] = useState(false);
  const { instance } = useMsal();
  const { user, clearUser } = useAuthStore();
  const navigate = useNavigate();

  const navLinkClass = ({ isActive }: { isActive: boolean }) =>
    cn(
      'text-sm font-medium transition-colors',
      isActive
        ? 'text-primary-900 font-semibold underline underline-offset-4'
        : 'text-gray-600 hover:text-primary-900',
    );

  function handleLogin() {
    navigate('/login');
  }

  async function handleLogout() {
    clearUser();
    await instance.logoutRedirect();
  }

  const links = [
    ...publicLinks,
    ...(user?.role === 'admin' ? [{ to: '/admin', label: 'Admin' }] : []),
  ];

  return (
    <header className="sticky top-0 z-50 border-b border-gray-200 bg-white shadow-sm">
      <div className="container flex h-16 max-w-screen-xl items-center justify-between px-4">
        {/* Logo */}
        <Link
          to="/"
          className="flex items-center gap-2 text-primary-900 font-bold text-lg hover:opacity-80 transition-opacity"
        >
          <span className="text-2xl" role="img" aria-label="golf flag">⛳</span>
          <span className="hidden sm:inline">Golf League</span>
        </Link>

        {/* Desktop nav */}
        <nav className="hidden md:flex items-center gap-6">
          {links.map((link) => (
            <NavLink key={link.to} to={link.to} className={navLinkClass} end={link.to === '/'}>
              {link.label}
            </NavLink>
          ))}
        </nav>

        {/* Desktop auth */}
        <div className="hidden md:flex items-center gap-3">
          {user ? (
            <div className="flex items-center gap-3">
              <span className="flex items-center gap-1.5 text-sm text-gray-600">
                <User className="h-4 w-4" />
                {user.name}
              </span>
              <Button variant="outline" size="sm" onClick={() => void handleLogout()}>
                <LogOut className="h-4 w-4 mr-1.5" />
                Sign out
              </Button>
            </div>
          ) : (
            <Button size="sm" onClick={handleLogin}>
              Sign in
            </Button>
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
          </nav>
          <div className="mt-3 border-t border-gray-100 pt-3">
            {user ? (
              <div className="flex flex-col gap-2">
                <span className="text-sm text-gray-500 px-3">{user.name}</span>
                <Button
                  variant="outline"
                  size="sm"
                  className="w-full"
                  onClick={() => { setMobileOpen(false); void handleLogout(); }}
                >
                  <LogOut className="h-4 w-4 mr-1.5" />
                  Sign out
                </Button>
              </div>
            ) : (
              <Button
                size="sm"
                className="w-full"
                onClick={() => { setMobileOpen(false); handleLogin(); }}
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
