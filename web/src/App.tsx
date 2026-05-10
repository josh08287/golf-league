import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { RootLayout } from '@/components/layout/RootLayout';

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
import { adminRoutes } from '@/routes/adminRoutes';

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<RootLayout />}>
          <Route index element={<HomePage />} />
          <Route path="flights" element={<FlightsPage />} />
          <Route path="flights/:flightId" element={<FlightLeaderboardPage />} />
          <Route path="rounds" element={<RoundsPage />} />
          <Route path="rounds/:roundId" element={<RoundDetailPage />} />
          <Route path="players" element={<PlayersPage />} />
          <Route path="players/:playerId" element={<PlayerProfilePage />} />
          <Route path="login" element={<LoginPage />} />
          <Route path="register" element={<RegisterPage />} />
          <Route path="auth/callback" element={<AuthCallbackPage />} />
          <Route path="auth/mfa" element={<MfaPage />} />
          <Route path="auth/mfa/enroll" element={<MfaEnrollPage />} />
          <Route path="auth/forgot-password" element={<ForgotPasswordPage />} />
          <Route path="auth/reset-password" element={<ResetPasswordPage />} />
          <Route path="accept-invite" element={<AcceptInvitePage />} />
          {adminRoutes}
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}
