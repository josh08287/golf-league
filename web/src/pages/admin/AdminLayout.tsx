import { Outlet, NavLink, Link } from 'react-router-dom';
import { LayoutDashboard, Users, Trophy, Calendar, Settings, Layers } from 'lucide-react';
import { cn } from '@/lib/utils';

const adminNav = [
  { to: '/admin', label: 'Dashboard', icon: LayoutDashboard },
  { to: '/admin/players', label: 'Players', icon: Users },
  { to: '/admin/seasons', label: 'Seasons', icon: Layers },
  { to: '/admin/flights', label: 'Flights', icon: Trophy },
  { to: '/admin/rounds', label: 'Rounds', icon: Calendar },
  { to: '/admin/settings', label: 'Settings', icon: Settings },
];

export function AdminLayout() {
  const linkClass = ({ isActive }: { isActive: boolean }) =>
    cn(
      'flex items-center gap-2.5 rounded-md px-3 py-2 text-sm font-medium transition-colors',
      isActive
        ? 'bg-primary-900 text-white'
        : 'text-gray-700 hover:bg-primary-50 hover:text-primary-900',
    );

  return (
    <div className="flex gap-8">
      {/* Sidebar */}
      <aside className="hidden lg:flex w-52 shrink-0 flex-col gap-1">
        <p className="px-3 pb-2 text-xs font-semibold uppercase tracking-widest text-gray-400">
          Admin
        </p>
        {adminNav.map(({ to, label, icon: Icon }) => (
          <NavLink key={to} to={to} end={to === '/admin'} className={linkClass}>
            <Icon className="h-4 w-4 shrink-0" />
            {label}
          </NavLink>
        ))}
        <div className="mt-auto pt-6 border-t border-gray-100">
          <Link
            to="/"
            className="flex items-center gap-2 text-xs text-gray-400 hover:text-primary-900 px-3"
          >
            ← Back to site
          </Link>
        </div>
      </aside>

      {/* Mobile top nav */}
      <div className="lg:hidden w-full">
        <nav className="flex gap-1 overflow-x-auto pb-2 mb-4 border-b border-gray-200">
          {adminNav.map(({ to, label, icon: Icon }) => (
            <NavLink
              key={to}
              to={to}
              end={to === '/admin'}
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-1.5 rounded-md px-3 py-1.5 text-sm font-medium whitespace-nowrap transition-colors',
                  isActive
                    ? 'bg-primary-900 text-white'
                    : 'text-gray-700 hover:bg-primary-50',
                )
              }
            >
              <Icon className="h-3.5 w-3.5" />
              {label}
            </NavLink>
          ))}
        </nav>
        <Outlet />
      </div>

      {/* Main content (desktop) */}
      <div className="hidden lg:block flex-1 min-w-0">
        <Outlet />
      </div>
    </div>
  );
}
