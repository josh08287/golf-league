import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { RootLayout } from '@/components/layout/RootLayout';

import { HomePage } from '@/pages/HomePage';
import { FlightsPage } from '@/pages/FlightsPage';
import { FlightLeaderboardPage } from '@/pages/FlightLeaderboardPage';
import { RoundsPage } from '@/pages/RoundsPage';
import { RoundDetailPage } from '@/pages/RoundDetailPage';
import { PlayerProfilePage } from '@/pages/PlayerProfilePage';
import { LoginPage } from '@/pages/LoginPage';
import { RegisterPage } from '@/pages/RegisterPage';
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
          <Route path="players/:playerId" element={<PlayerProfilePage />} />
          <Route path="login" element={<LoginPage />} />
          <Route path="register" element={<RegisterPage />} />
          {adminRoutes}
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}
