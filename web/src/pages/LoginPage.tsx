import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useIsAuthenticated } from '@azure/msal-react';
import { msalInstance, loginRequest } from '@/lib/msalConfig';
import { Button } from '@/components/ui/Button';

export function LoginPage() {
  const isAuthenticated = useIsAuthenticated();
  const navigate = useNavigate();

  useEffect(() => {
    if (isAuthenticated) navigate('/', { replace: true });
  }, [isAuthenticated, navigate]);

  function handleSignIn() {
    void msalInstance.loginRedirect(loginRequest);
  }

  function handleCreateAccount() {
    void msalInstance.loginRedirect({
      ...loginRequest,
      prompt: 'create', // Show create account option for external users
    });
  }

  return (
    <div className="flex min-h-[70vh] items-center justify-center">
      <div className="w-full max-w-sm rounded-xl border border-gray-200 bg-white p-8 shadow-md text-center">
        {/* Logo */}
        <span className="text-5xl" role="img" aria-label="golf flag">⛳</span>
        <h1 className="mt-4 text-2xl font-bold text-gray-900">Golf League</h1>
        <p className="mt-2 text-sm text-gray-500">
          Sign in to view standings, scorecards, and your player profile.
        </p>

        <Button
          className="mt-8 w-full gap-2"
          size="lg"
          onClick={handleSignIn}
        >
          {/* Microsoft logo SVG */}
          <svg
            xmlns="http://www.w3.org/2000/svg"
            viewBox="0 0 21 21"
            className="h-5 w-5 flex-shrink-0"
            aria-hidden="true"
          >
            <rect x="1" y="1" width="9" height="9" fill="#f25022" />
            <rect x="11" y="1" width="9" height="9" fill="#7fba00" />
            <rect x="1" y="11" width="9" height="9" fill="#00a4ef" />
            <rect x="11" y="11" width="9" height="9" fill="#ffb900" />
          </svg>
          Sign in
        </Button>

        <Button
          className="mt-3 w-full gap-2"
          size="lg"
          variant="outline"
          onClick={handleCreateAccount}
        >
          Create account
        </Button>

        <p className="mt-6 text-xs text-gray-400">
          Powered by Microsoft Entra External ID
        </p>
      </div>
    </div>
  );
}
