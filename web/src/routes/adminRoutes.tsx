import { lazy } from 'react';
import { Route } from 'react-router-dom';
import { RequireAdmin } from '../components/admin/RequireAdmin';
import { RequireSuperAdmin } from '../components/admin/RequireSuperAdmin';
import { AdminLayout } from '../components/layout/AdminLayout';

const AdminDashboardPage = lazy(() => import('../pages/admin/AdminDashboardPage').then((m) => ({ default: m.AdminDashboardPage })));
const PlayersPage = lazy(() => import('../pages/admin/PlayersPage').then((m) => ({ default: m.PlayersPage })));
const PlayerDetailPage = lazy(() => import('../pages/admin/PlayerDetailPage').then((m) => ({ default: m.PlayerDetailPage })));
const FlightsPage = lazy(() => import('../pages/admin/FlightsPage').then((m) => ({ default: m.FlightsPage })));
const RoundsPage = lazy(() => import('../pages/admin/RoundsPage').then((m) => ({ default: m.RoundsPage })));
const ScoreEntryPage = lazy(() => import('../pages/admin/ScoreEntryPage').then((m) => ({ default: m.ScoreEntryPage })));
const TournamentScoreEntryPage = lazy(() => import('../pages/admin/TournamentScoreEntryPage').then((m) => ({ default: m.TournamentScoreEntryPage })));
const TeeTimesAdminPage = lazy(() => import('../pages/admin/TeeTimesAdminPage').then((m) => ({ default: m.TeeTimesAdminPage })));
const CoursesPage = lazy(() => import('../pages/admin/CoursesPage').then((m) => ({ default: m.CoursesPage })));
const SeasonsPage = lazy(() => import('../pages/admin/SeasonsPage').then((m) => ({ default: m.SeasonsPage })));
const InvitesPage = lazy(() => import('../pages/admin/InvitesPage').then((m) => ({ default: m.InvitesPage })));
const AuditLogPage = lazy(() => import('../pages/admin/AuditLogPage').then((m) => ({ default: m.AuditLogPage })));
const SettingsPage = lazy(() => import('../pages/admin/SettingsPage').then((m) => ({ default: m.SettingsPage })));
const MessagesPage = lazy(() => import('../pages/admin/MessagesPage').then((m) => ({ default: m.MessagesPage })));

export const adminRoutes = (
  <Route element={<RequireAdmin />}>
    <Route path="admin" element={<AdminLayout />}>
      <Route index element={<AdminDashboardPage />} />
      <Route path="players" element={<PlayersPage />} />
      <Route path="players/:id" element={<PlayerDetailPage />} />
      <Route path="flights" element={<FlightsPage />} />
      <Route path="rounds" element={<RoundsPage />} />
      <Route path="rounds/:id/scores" element={<ScoreEntryPage />} />
      <Route path="rounds/:id/tournament-scores" element={<TournamentScoreEntryPage />} />
      <Route path="tee-times" element={<TeeTimesAdminPage />} />
      <Route path="courses" element={<CoursesPage />} />
      <Route path="seasons" element={<SeasonsPage />} />
      <Route path="invites" element={<InvitesPage />} />
      <Route path="settings" element={<SettingsPage />} />
      <Route path="messages" element={<MessagesPage />} />
      <Route element={<RequireSuperAdmin />}>
        <Route path="audit-log" element={<AuditLogPage />} />
      </Route>
    </Route>
  </Route>
);
