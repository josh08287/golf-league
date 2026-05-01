/**
 * Admin route tree.
 *
 * Usage — add this inside your <Routes> (or router config):
 *
 *   import { adminRoutes } from './routes/adminRoutes';
 *   ...
 *   {adminRoutes}
 *
 * Or with createBrowserRouter / createRoutesFromElements:
 *
 *   import { adminRouteObjects } from './routes/adminRoutes';
 */

import { Route } from 'react-router-dom';
import { RequireAdmin } from '../components/admin/RequireAdmin';
import { AdminLayout } from '../components/layout/AdminLayout';
import {
  AdminDashboardPage,
  PlayersPage,
  PlayerDetailPage,
  FlightsPage,
  RoundsPage,
  ScoreEntryPage,
  CoursesPage,
  AuditLogPage,
  SettingsPage,
} from '../pages/admin';

/**
 * JSX route tree — use inside <Routes>.
 */
export const adminRoutes = (
  <Route element={<RequireAdmin />}>
    <Route path="/admin" element={<AdminLayout />}>
      <Route index element={<AdminDashboardPage />} />
      <Route path="players" element={<PlayersPage />} />
      <Route path="players/:id" element={<PlayerDetailPage />} />
      <Route path="flights" element={<FlightsPage />} />
      <Route path="rounds" element={<RoundsPage />} />
      <Route path="rounds/:id/scores" element={<ScoreEntryPage />} />
      <Route path="courses" element={<CoursesPage />} />
      <Route path="audit-log" element={<AuditLogPage />} />
      <Route path="settings" element={<SettingsPage />} />
    </Route>
  </Route>
);
