import { useEffect, useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Spinner } from '@/components/ui/Spinner';
import { completeExternalLogin } from '@/lib/auth';
import { useAuth } from '@/hooks/useAuth';

export function AuthCallbackPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const { onLoginSuccess } = useAuth();
  const [error, setError] = useState<string | null>(null);
  const ran = useRef(false);

  useEffect(() => {
    if (ran.current) return; // React StrictMode double-invoke guard
    ran.current = true;

    const state = params.get('state');
    const code = params.get('code');
    const providerError = params.get('error');

    if (providerError) {
      setError(`Social sign-in cancelled or failed (${providerError}).`);
      return;
    }
    if (!state || !code) {
      setError('Missing state or code in the callback URL.');
      return;
    }

    void (async () => {
      try {
        const resp = await completeExternalLogin(state, code);
        await onLoginSuccess(resp);
        if (!resp.mfaRequired) navigate('/', { replace: true });
      } catch (err) {
        const message =
          (err as { response?: { data?: { error?: string } } })?.response?.data?.error
          ?? 'Sign-in failed.';
        setError(message);
      }
    })();
  }, [params, navigate, onLoginSuccess]);

  if (error) {
    return (
      <div className="flex min-h-[60vh] flex-col items-center justify-center gap-4 text-center">
        <h1 className="text-xl font-bold text-gray-900">Sign-in error</h1>
        <p className="text-sm text-red-600">{error}</p>
        <button
          className="text-sm text-primary-900 underline"
          onClick={() => navigate('/login', { replace: true })}
        >
          Back to sign in
        </button>
      </div>
    );
  }

  return (
    <div className="flex min-h-[60vh] items-center justify-center">
      <Spinner />
    </div>
  );
}
