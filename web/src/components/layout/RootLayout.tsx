import { useEffect } from 'react';
import { Outlet } from 'react-router-dom';
import { useIsAuthenticated, useMsal } from '@azure/msal-react';
import { InteractionStatus } from '@azure/msal-browser';
import { useAuthStore, type UserRole } from '@/store/authStore';
import { NavBar } from './NavBar';

/**
 * Decode the JWT id-token payload (no verification — MSAL already verified it).
 * We just need the claims for display / role-gating.
 */
function decodeTokenPayload(token: string): Record<string, unknown> {
  try {
    const payload = token.split('.')[1];
    return JSON.parse(atob(payload.replace(/-/g, '+').replace(/_/g, '/'))) as Record<string, unknown>;
  } catch {
    return {};
  }
}

export function RootLayout() {
  const isAuthenticated = useIsAuthenticated();
  const { accounts, inProgress } = useMsal();
  const { setUser, clearUser } = useAuthStore();

  useEffect(() => {
    if (inProgress !== InteractionStatus.None) return;

    if (isAuthenticated && accounts.length > 0) {
      const account = accounts[0];
      const idTokenClaims = account.idTokenClaims as Record<string, unknown> | undefined;

      // Claims may come directly from idTokenClaims or need decoding from idToken
      const claims: Record<string, unknown> =
        idTokenClaims ?? (account.idToken ? decodeTokenPayload(account.idToken) : {});

      const rolesArr = claims['roles'] as string[] | undefined;
      const role = rolesArr?.[0] ?? 'player';
      const playerId = (claims['oid'] as string | undefined) ?? null;

      setUser({
        name: account.name ?? account.username,
        email: account.username,
        role: role as UserRole,
        playerId,
      });
    } else if (!isAuthenticated && inProgress === InteractionStatus.None) {
      clearUser();
    }
  }, [isAuthenticated, accounts, inProgress, setUser, clearUser]);

  return (
    <div className="min-h-screen bg-gray-50">
      <NavBar />
      <main className="container mx-auto max-w-screen-xl px-4 py-8">
        <Outlet />
      </main>
      <footer className="border-t border-gray-200 bg-white mt-auto">
        <div className="container mx-auto max-w-screen-xl px-4 py-6 text-center text-sm text-gray-400">
          &copy; {new Date().getFullYear()} Golf League &mdash; All rights reserved
        </div>
      </footer>
    </div>
  );
}
