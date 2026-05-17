import { BrowserRouter, Routes, Route, Navigate, useNavigate } from 'react-router-dom';
import { useLayoutEffect } from 'react';
import { setNavigator } from '@/lib/api';
import { RootLayout } from '@/components/layout/RootLayout';
import { AppRoute } from '@/components/AppRoute';

import { HomePage } from '@/pages/HomePage';
import { FlightsPage } from '@/pages/FlightsPage';
import { FlightLeaderboardPage } from '@/pages/FlightLeaderboardPage';
import { RoundsPage } from '@/pages/RoundsPage';
import { RoundDetailPage } from '@/pages/RoundDetailPage';
import { PlayerProfilePage } from '@/pages/PlayerProfilePage';
import { PlayersPage } from '@/pages/PlayersPage';
import { LoginPage } from '@/pages/LoginPage';
import { RegisterPage } from '@/pages/RegisterPage';
import { AuthCallbackPage } from '@/pages/AuthCallbackPage';
import { MfaPage } from '@/pages/MfaPage';
import { MfaEnrollPage } from '@/pages/MfaEnrollPage';
import { ForgotPasswordPage } from '@/pages/ForgotPasswordPage';
import { ResetPasswordPage } from '@/pages/ResetPasswordPage';
import { AcceptInvitePage } from '@/pages/AcceptInvitePage';
import { TeeTimesNextPage, TeeTimesRoundPage } from '@/pages/TeeTimesPage';
import { TeeTimeScoreEntryPage } from '@/pages/TeeTimeScoreEntryPage';
import { StatisticsPage } from '@/pages/StatisticsPage';
import { TournamentResultsPage } from '@/pages/TournamentResultsPage';
import { adminRoutes } from '@/routes/adminRoutes';

function NavigatorInjector() {
  const navigate = useNavigate();
  // useLayoutEffect fires synchronously after DOM mutations — before any child
  // components' effects run — so _navigate is set before React Query fires
  // its first requests and before any 401 response interceptor could trigger.
  useLayoutEffect(() => { setNavigator(navigate); }, [navigate]);
  return null;
}

export default function App() {
  return (
    <BrowserRouter>
      <NavigatorInjector />
      <Routes>
        {/* Public auth routes — no league context needed */}
        <Route path="login" element={<LoginPage />} />
        <Route path="register" element={<RegisterPage />} />
        <Route path="accept-invite" element={<AcceptInvitePage />} />
        <Route path="auth/callback" element={<AuthCallbackPage />} />
        <Route path="auth/mfa" element={<MfaPage />} />
        <Route path="auth/mfa/enroll" element={<MfaEnrollPage />} />
        <Route path="auth/forgot-password" element={<ForgotPasswordPage />} />
        <Route path="auth/reset-password" element={<ResetPasswordPage />} />

        {/* Protected routes — require auth + league context */}
        <Route element={<AppRoute />}>
          <Route element={<RootLayout />}>
            <Route index element={<HomePage />} />
            <Route path="flights" element={<FlightsPage />} />
            <Route path="flights/:flightId" element={<FlightLeaderboardPage />} />
            <Route path="rounds" element={<RoundsPage />} />
            <Route path="rounds/:roundId" element={<RoundDetailPage />} />
            <Route path="players" element={<PlayersPage />} />
            <Route path="players/:playerId" element={<PlayerProfilePage />} />
            <Route path="tee-times" element={<TeeTimesNextPage />} />
            <Route path="rounds/:roundId/tee-times" element={<TeeTimesRoundPage />} />
            <Route path="tee-times/:teeTimeId/enter-scores" element={<TeeTimeScoreEntryPage />} />
            <Route path="statistics" element={<StatisticsPage />} />
            <Route path="rounds/:roundId/tournament-results" element={<TournamentResultsPage />} />
            {adminRoutes}
            <Route path="*" element={<Navigate to="/" replace />} />
          </Route>
        </Route>
      </Routes>
    </BrowserRouter>
  );
}
