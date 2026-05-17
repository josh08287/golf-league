import { Route } from 'react-router-dom';
import { RequireAdmin } from '../components/admin/RequireAdmin';
import { RequireSuperAdmin } from '../components/admin/RequireSuperAdmin';
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
  SeasonsPage,
  InvitesPage,
  TeeTimesAdminPage,
  TournamentScoreEntryPage,
} from '../pages/admin';

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
      <Route element={<RequireSuperAdmin />}>
        <Route path="audit-log" element={<AuditLogPage />} />
      </Route>
    </Route>
  </Route>
);
