import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import {
  LayoutDashboard,
  Users,
  CalendarDays,
  MapPin,
  ClipboardList,
  LogOut,
  Trophy,
  Mail,
  Clock,
  Layers,
  Settings,
  MessageSquare,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { useAuthStore } from '@/store/authStore';
import { useLeagueName } from '@/context/LeagueContext';
import { useActiveLeagueStore } from '@/store/activeLeagueStore';
import { logout as logoutApi } from '@/lib/auth';
import { GolfLogo } from '@/components/ui/GolfLogo';

interface NavItem {
  to: string;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
  superAdminOnly?: boolean;
}

export function AdminLayout() {
  const user = useAuthStore((s) => s.user);
  const clearUser = useAuthStore((s) => s.clearUser);
  const navigate = useNavigate();
  const leagueName = useLeagueName();

  const NAV_ITEMS: NavItem[] = [
    { to: '/admin', label: 'Dashboard', icon: LayoutDashboard },
    { to: '/admin/players', label: 'Players', icon: Users },
    { to: '/admin/invites', label: 'Invites', icon: Mail },
    { to: '/admin/seasons', label: 'Seasons', icon: Layers },
    { to: '/admin/flights', label: 'Flights', icon: Trophy },
    { to: '/admin/rounds', label: 'Rounds', icon: CalendarDays },
    { to: '/admin/tee-times', label: 'Tee Times', icon: Clock },
    { to: '/admin/courses', label: 'Courses', icon: MapPin },
    { to: '/admin/messages', label: 'Messages', icon: MessageSquare },
    { to: '/admin/settings', label: 'Settings', icon: Settings },
    { to: '/admin/audit-log', label: 'Audit Log', icon: ClipboardList, superAdminOnly: true },
  ];

  async function handleLogout() {
    try { await logoutApi(); } catch { /* best-effort */ }
    clearUser();
    useActiveLeagueStore.getState().setActiveLeague(null);
    navigate('/login', { replace: true });
  }

  return (
    <div className="flex min-h-[calc(100vh-9rem)] rounded-md overflow-hidden border border-gray-200 bg-white shadow-sm">
      {/* Sidebar */}
      <aside className="flex w-64 flex-shrink-0 flex-col bg-[#133A25] text-white">
        {/* Logo / Brand */}
        <div className="flex h-16 items-center gap-3 border-b border-white/10 px-6">
          <GolfLogo size="sm" />
          <div>
            <span className="block font-sans text-base font-bold tracking-tight text-white">{leagueName || 'Admin'}</span>
            <span className="block text-[10px] font-semibold uppercase tracking-widest text-[#E5B84B]">Commissioner Suite</span>
          </div>
        </div>

        {/* Navigation */}
        <nav className="flex-1 overflow-y-auto py-4">
          <ul className="space-y-0.5 px-3">
            {NAV_ITEMS.filter((item) => !item.superAdminOnly || user?.isSuperAdmin).map(({ to, label, icon: Icon }) => (
              <li key={to}>
                <NavLink
                  to={to}
                  end={to === '/admin'}
                  className={({ isActive }) =>
                    cn(
                      'flex items-center gap-3 rounded px-3 py-2 text-sm font-medium transition-colors',
                      isActive
                        ? 'bg-[#E5B84B] text-[#112A1B] font-bold'
                        : 'text-emerald-100/80 hover:bg-white/10 hover:text-white',
                    )
                  }
                >
                  <Icon className="h-4 w-4" />
                  {label}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>

        {/* User info + logout */}
        <div className="border-t border-white/10 p-4">
          <div className="mb-3 truncate text-xs font-medium text-emerald-100/80">
            {user?.name ?? user?.email ?? 'Admin'}
          </div>
          <button
            onClick={() => void handleLogout()}
            className="flex w-full items-center gap-2 rounded-xl px-3 py-2 text-xs font-semibold text-emerald-100/80 transition-colors hover:bg-white/10 hover:text-white"
          >
            <LogOut className="h-4 w-4" />
            Log out
          </button>
        </div>
      </aside>

      {/* Main content */}
      <div className="flex flex-1 flex-col bg-[#FAF9F5]">
        <main className="flex-1 p-6 sm:p-8">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
