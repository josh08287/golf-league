import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '@/components/ui/Button';
import { verifyTotpForLogin } from '@/lib/auth';
import { useAuth } from '@/hooks/useAuth';

export function MfaPage() {
  const { onLoginSuccess } = useAuth();
  const navigate = useNavigate();
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const mfaToken = sessionStorage.getItem('golf-league-mfa-token');

  if (!mfaToken) {
    navigate('/login', { replace: true });
    return null;
  }

  async function onSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      const resp = await verifyTotpForLogin(mfaToken!, code.trim());
      sessionStorage.removeItem('golf-league-mfa-token');
      await onLoginSuccess(resp);
      navigate('/', { replace: true });
    } catch (err) {
      const message =
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error
        ?? 'Verification failed.';
      setError(message);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="flex min-h-[70vh] items-center justify-center">
      <div className="w-full max-w-sm rounded-xl border border-gray-200 bg-white p-8 shadow-md">
        <div className="text-center">
          <h1 className="text-2xl font-bold text-gray-900">Two-step verification</h1>
          <p className="mt-2 text-sm text-gray-500">
            Enter the 6-digit code from your authenticator app.
          </p>
        </div>

        <form onSubmit={onSubmit} className="mt-6 space-y-4">
          <input
            type="text"
            inputMode="numeric"
            autoComplete="one-time-code"
            maxLength={8}
            value={code}
            onChange={(e) => setCode(e.target.value)}
            placeholder="123 456"
            className="block w-full rounded-md border border-gray-300 px-3 py-2 text-center text-lg tracking-widest shadow-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20]"
          />
          {error && <p className="text-sm text-red-600">{error}</p>}
          <Button type="submit" className="w-full" disabled={submitting || code.length < 6}>
            {submitting ? 'Verifying…' : 'Verify'}
          </Button>
        </form>

        <p className="mt-6 text-center text-xs text-gray-400">
          Lost access to your authenticator? Contact another admin to reset MFA.
        </p>
      </div>
    </div>
  );
}
