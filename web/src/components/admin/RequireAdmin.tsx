import { Navigate, Outlet } from 'react-router-dom';
import { useIsAuthenticated, useMsal } from '@azure/msal-react';
import { InteractionStatus } from '@azure/msal-browser';
import { useAuthStore } from '@/store/authStore';
import { useMyStatus } from '@/hooks/useMyStatus';
import { Spinner } from '@/components/ui/Spinner';

export function RequireAdmin() {
  const user = useAuthStore((s) => s.user);
  const isAuthenticated = useIsAuthenticated();
  const { inProgress } = useMsal();
  const myStatus = useMyStatus();

  // Wait for MSAL to finish processing
  if (inProgress !== InteractionStatus.None) {
    return (
      <div className="flex h-screen items-center justify-center">
        <Spinner />
      </div>
    );
  }

  // Not authenticated at all
  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  // Wait for the API call to complete to get the role
  if (myStatus.isLoading) {
    return (
      <div className="flex h-screen items-center justify-center">
        <Spinner />
      </div>
    );
  }

  // Auth is ready but no user data means something went wrong
  if (!user) {
    return <Navigate to="/login" replace />;
  }

  // Check admin role
  if (user.role !== 'admin') {
    return <Navigate to="/" replace />;
  }

  return <Outlet />;
}
