import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { QRCodeSVG } from 'qrcode.react';
import { Button } from '@/components/ui/Button';
import { Spinner } from '@/components/ui/Spinner';
import {
  startTotpEnrollment,
  verifyTotpEnrollment,
  verifyTotpForLogin,
} from '@/lib/auth';
import { useAuth } from '@/hooks/useAuth';

export function MfaEnrollPage() {
  const { onLoginSuccess } = useAuth();
  const navigate = useNavigate();

  const mfaToken = sessionStorage.getItem('golf-league-mfa-token');
  const [secret, setSecret] = useState<string | null>(null);
  const [otpAuthUri, setOtpAuthUri] = useState<string | null>(null);
  const [code, setCode] = useState('');
  const [loadError, setLoadError] = useState<string | null>(null);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const enrollmentStarted = useRef(false);

  useEffect(() => {
    if (!mfaToken) {
      navigate('/login', { replace: true });
      return;
    }
    if (enrollmentStarted.current) return; // StrictMode double-invoke guard
    enrollmentStarted.current = true;

    void (async () => {
      try {
        const result = await startTotpEnrollment(mfaToken);
        setSecret(result.secret);
        setOtpAuthUri(result.otpAuthUri);
      } catch (err) {
        const message =
          (err as { response?: { data?: { error?: string } } })?.response?.data?.error
          ?? 'Could not start TOTP enrollment.';
        setLoadError(message);
      }
    })();
  }, [mfaToken, navigate]);

  if (!mfaToken) return null;

  async function onSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitError(null);
    setSubmitting(true);
    try {
      // Two-step: confirm the secret with the entered code, then exchange
      // the MFA-challenge token for full tokens. Both calls use the
      // challenge token as bearer.
      await verifyTotpEnrollment(code.trim(), mfaToken!);
      const resp = await verifyTotpForLogin(mfaToken!, code.trim());
      sessionStorage.removeItem('golf-league-mfa-token');
      sessionStorage.removeItem('golf-league-mfa-enrollment-required');
      await onLoginSuccess(resp);
      navigate('/', { replace: true });
    } catch (err) {
      const message =
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error
        ?? 'Verification failed. Double-check the code from your authenticator.';
      setSubmitError(message);
    } finally {
      setSubmitting(false);
    }
  }

  if (loadError) {
    return (
      <div className="flex min-h-[70vh] items-center justify-center">
        <div className="w-full max-w-sm rounded-xl border border-gray-200 bg-white p-8 shadow-md text-center">
          <h1 className="text-xl font-bold text-gray-900">Could not start enrollment</h1>
          <p className="mt-3 text-sm text-red-600">{loadError}</p>
          <button
            className="mt-4 text-sm text-primary-900 underline"
            onClick={() => navigate('/login', { replace: true })}
          >
            Back to sign in
          </button>
        </div>
      </div>
    );
  }

  if (!secret || !otpAuthUri) {
    return (
      <div className="flex min-h-[70vh] items-center justify-center">
        <Spinner />
      </div>
    );
  }

  return (
    <div className="flex min-h-[70vh] items-center justify-center">
      <div className="w-full max-w-md rounded-xl border border-gray-200 bg-white p-8 shadow-md">
        <div className="text-center">
          <h1 className="text-2xl font-bold text-gray-900">Set up two-step verification</h1>
          <p className="mt-2 text-sm text-gray-500">
            Admins must use a second factor. Scan the QR code with an
            authenticator app (Google Authenticator, 1Password, Authy), then
            enter the 6-digit code it shows.
          </p>
        </div>

        <div className="mt-6 flex justify-center rounded-md bg-white p-4">
          <QRCodeSVG value={otpAuthUri} size={196} includeMargin />
        </div>

        <details className="mt-3 text-xs text-gray-500">
          <summary className="cursor-pointer">Can&apos;t scan? Enter this secret manually.</summary>
          <code className="mt-2 block break-all rounded bg-gray-50 p-2 font-mono text-[11px]">
            {secret}
          </code>
        </details>

        <form onSubmit={onSubmit} className="mt-6 space-y-3">
          <label className="block text-sm font-medium text-gray-700">Code from authenticator</label>
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
          {submitError && <p className="text-sm text-red-600">{submitError}</p>}
          <Button type="submit" className="w-full" disabled={submitting || code.replace(/\s/g, '').length < 6}>
            {submitting ? 'Verifying…' : 'Finish setup'}
          </Button>
        </form>

        <p className="mt-6 text-center text-xs text-gray-400">
          Save the secret somewhere safe in case you lose access to your device.
        </p>
      </div>
    </div>
  );
}
