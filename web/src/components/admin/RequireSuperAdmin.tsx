import { Navigate, Outlet, useParams } from 'react-router-dom';
import { Spinner } from '@/components/ui/Spinner';
import { useAuth } from '@/hooks/useAuth';

export function RequireSuperAdmin() {
  const { user, bootstrapping } = useAuth();
  const { leagueSlug = '' } = useParams<{ leagueSlug: string }>();

  if (bootstrapping) {
    return (
      <div className="flex h-screen items-center justify-center">
        <Spinner />
      </div>
    );
  }

  if (!user || !user.isSuperAdmin) {
    return <Navigate to={`/${leagueSlug}/admin`} replace />;
  }

  return <Outlet />;
}
