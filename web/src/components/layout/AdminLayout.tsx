import { NavLink, Outlet, useNavigate, useParams } from 'react-router-dom';
import {
  LayoutDashboard,
  Users,
  Flag,
  CalendarDays,
  MapPin,
  ClipboardList,
  LogOut,
  Trophy,
  Mail,
  Clock,
  Layers,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { useAuthStore } from '@/store/authStore';
import { useLeagueName } from '@/context/LeagueContext';

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
  const { leagueSlug = '' } = useParams<{ leagueSlug: string }>();
  const leagueName = useLeagueName(leagueSlug);
  const base = `/${leagueSlug}`;

  const NAV_ITEMS: NavItem[] = [
    { to: `${base}/admin`, label: 'Dashboard', icon: LayoutDashboard },
    { to: `${base}/admin/players`, label: 'Players', icon: Users },
    { to: `${base}/admin/invites`, label: 'Invites', icon: Mail },
    { to: `${base}/admin/seasons`, label: 'Seasons', icon: Layers },
    { to: `${base}/admin/flights`, label: 'Flights', icon: Trophy },
    { to: `${base}/admin/rounds`, label: 'Rounds', icon: CalendarDays },
    { to: `${base}/admin/tee-times`, label: 'Tee Times', icon: Clock },
    { to: `${base}/admin/courses`, label: 'Courses', icon: MapPin },
    { to: `${base}/admin/audit-log`, label: 'Audit Log', icon: ClipboardList, superAdminOnly: true },
  ];

  function handleLogout() {
    clearUser();
    navigate(`${base}/login`);
  }

  return (
    <div className="-mx-4 -my-8 flex min-h-[calc(100vh-8rem)] bg-gray-100">
      {/* Sidebar */}
      <aside className="flex w-64 flex-shrink-0 flex-col bg-[#1B5E20] text-white">
        {/* Logo / Brand */}
        <div className="flex h-16 items-center gap-2 border-b border-green-700 px-6">
          <Flag className="h-6 w-6 text-green-300" />
          <span className="text-lg font-bold tracking-tight">{leagueName}</span>
        </div>

        {/* Navigation */}
        <nav className="flex-1 overflow-y-auto py-4">
          <ul className="space-y-1 px-3">
            {NAV_ITEMS.filter((item) => !item.superAdminOnly || user?.isSuperAdmin).map(({ to, label, icon: Icon }) => (
              <li key={to}>
                <NavLink
                  to={to}
                  end={to === `${base}/admin`}
                  className={({ isActive }) =>
                    cn(
                      'flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors',
                      isActive
                        ? 'bg-green-700 text-white'
                        : 'text-green-100 hover:bg-green-700/60 hover:text-white',
                    )
                  }
                >
                  <Icon className="h-5 w-5" />
                  {label}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>

        {/* User info + logout */}
        <div className="border-t border-green-700 p-4">
          <div className="mb-3 truncate text-sm font-medium text-green-100">
            {user?.name ?? user?.email ?? 'Admin'}
          </div>
          <button
            onClick={handleLogout}
            className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-sm text-green-200 transition-colors hover:bg-green-700 hover:text-white"
          >
            <LogOut className="h-4 w-4" />
            Log out
          </button>
        </div>
      </aside>

      {/* Main content */}
      <div className="flex flex-1 flex-col">
        <main className="flex-1 p-8">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
