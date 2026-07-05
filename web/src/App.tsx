import { BrowserRouter, Routes, Route, Navigate, useNavigate } from 'react-router-dom';
import { lazy, Suspense, useLayoutEffect } from 'react';
import { setNavigator } from '@/lib/api';
import { RootLayout } from '@/components/layout/RootLayout';
import { AppRoute } from '@/components/AppRoute';
import { Spinner } from '@/components/ui/Spinner';
import { adminRoutes } from '@/routes/adminRoutes';

const HomePage = lazy(() => import('@/pages/HomePage').then((m) => ({ default: m.HomePage })));
const FlightsPage = lazy(() => import('@/pages/FlightsPage').then((m) => ({ default: m.FlightsPage })));
const FlightLeaderboardPage = lazy(() => import('@/pages/FlightLeaderboardPage').then((m) => ({ default: m.FlightLeaderboardPage })));
const RoundsPage = lazy(() => import('@/pages/RoundsPage').then((m) => ({ default: m.RoundsPage })));
const RoundDetailPage = lazy(() => import('@/pages/RoundDetailPage').then((m) => ({ default: m.RoundDetailPage })));
const PlayerProfilePage = lazy(() => import('@/pages/PlayerProfilePage').then((m) => ({ default: m.PlayerProfilePage })));
const PlayersPage = lazy(() => import('@/pages/PlayersPage').then((m) => ({ default: m.PlayersPage })));
const LoginPage = lazy(() => import('@/pages/LoginPage').then((m) => ({ default: m.LoginPage })));
const RegisterPage = lazy(() => import('@/pages/RegisterPage').then((m) => ({ default: m.RegisterPage })));
const AuthCallbackPage = lazy(() => import('@/pages/AuthCallbackPage').then((m) => ({ default: m.AuthCallbackPage })));
const MfaPage = lazy(() => import('@/pages/MfaPage').then((m) => ({ default: m.MfaPage })));
const MfaEnrollPage = lazy(() => import('@/pages/MfaEnrollPage').then((m) => ({ default: m.MfaEnrollPage })));
const ForgotPasswordPage = lazy(() => import('@/pages/ForgotPasswordPage').then((m) => ({ default: m.ForgotPasswordPage })));
const ResetPasswordPage = lazy(() => import('@/pages/ResetPasswordPage').then((m) => ({ default: m.ResetPasswordPage })));
const AcceptInvitePage = lazy(() => import('@/pages/AcceptInvitePage').then((m) => ({ default: m.AcceptInvitePage })));
const TeeTimesNextPage = lazy(() => import('@/pages/TeeTimesPage').then((m) => ({ default: m.TeeTimesNextPage })));
const TeeTimesRoundPage = lazy(() => import('@/pages/TeeTimesPage').then((m) => ({ default: m.TeeTimesRoundPage })));
const TeeTimeScoreEntryPage = lazy(() => import('@/pages/TeeTimeScoreEntryPage').then((m) => ({ default: m.TeeTimeScoreEntryPage })));
const StatisticsPage = lazy(() => import('@/pages/StatisticsPage').then((m) => ({ default: m.StatisticsPage })));
const LeaderboardPage = lazy(() => import('@/pages/LeaderboardPage').then((m) => ({ default: m.LeaderboardPage })));
const TournamentResultsPage = lazy(() => import('@/pages/TournamentResultsPage').then((m) => ({ default: m.TournamentResultsPage })));

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
      <Suspense fallback={<div className="flex min-h-screen items-center justify-center"><Spinner /></div>}>
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
              <Route path="leaderboard" element={<LeaderboardPage />} />
              <Route path="rounds/:roundId/tournament-results" element={<TournamentResultsPage />} />
              {adminRoutes}
              <Route path="*" element={<Navigate to="/" replace />} />
            </Route>
          </Route>
        </Routes>
      </Suspense>
    </BrowserRouter>
  );
}
