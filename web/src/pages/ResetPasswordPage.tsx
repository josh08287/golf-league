import { useEffect, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/Button';
import { clearAuth, confirmPasswordReset } from '@/lib/auth';

const schema = z.object({
  newPassword: z.string().min(10, 'Password must be at least 10 characters'),
});

type FormValues = z.infer<typeof schema>;

export function ResetPasswordPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const email = params.get('email') ?? '';
  const token = params.get('token') ?? '';

  const [submitError, setSubmitError] = useState<string | null>(null);
  const [done, setDone] = useState(false);

  // The user is about to set a new password — any token in storage is by
  // definition stale (they got here because they couldn't sign in). Clearing
  // up front avoids the api.ts 401 interceptor kicking us back to /login
  // when something else on the page fires a background request.
  useEffect(() => { clearAuth(); }, []);

  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { newPassword: '' },
  });

  if (!email || !token) {
    return (
      <div className="flex min-h-[70vh] items-center justify-center">
        <div className="w-full max-w-sm rounded-xl border border-gray-200 bg-white p-8 shadow-md text-center">
          <span className="text-5xl" role="img" aria-label="warning">⚠️</span>
          <h1 className="mt-4 text-xl font-bold text-gray-900">Invalid reset link</h1>
          <p className="mt-3 text-sm text-gray-500">
            This password reset link is missing required information. Request a new one.
          </p>
          <p className="mt-4 text-sm">
            <Link to="/auth/forgot-password" className="text-primary-900 font-medium hover:underline">
              Send a new reset link
            </Link>
          </p>
        </div>
      </div>
    );
  }

  async function onSubmit(values: FormValues) {
    setSubmitError(null);
    try {
      await confirmPasswordReset({ email, token, newPassword: values.newPassword });
      setDone(true);
      setTimeout(() => navigate('/login', { replace: true }), 1500);
    } catch (err) {
      const message =
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error
        ?? 'Could not reset password. The link may be expired — request a new one.';
      setSubmitError(message);
    }
  }

  const inputClass =
    'mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20]';

  return (
    <div className="flex min-h-[70vh] items-center justify-center">
      <div className="w-full max-w-sm rounded-xl border border-gray-200 bg-white p-8 shadow-md">
        <div className="text-center">
          <span className="text-5xl" role="img" aria-label="golf flag">⛳</span>
          <h1 className="mt-4 text-2xl font-bold text-gray-900">Set a new password</h1>
          <p className="mt-2 text-sm text-gray-500">{email}</p>
        </div>

        {done ? (
          <p className="mt-6 text-center text-sm text-gray-600">
            Password updated. Redirecting to sign in…
          </p>
        ) : (
          <form onSubmit={handleSubmit(onSubmit)} className="mt-6 space-y-4">
            <div>
              <label className="block text-sm font-medium text-gray-700">New password</label>
              <input
                type="password"
                autoComplete="new-password"
                {...register('newPassword')}
                className={inputClass}
              />
              {errors.newPassword && (
                <p className="mt-1 text-xs text-red-600">{errors.newPassword.message}</p>
              )}
              <p className="mt-1 text-xs text-gray-400">
                At least 10 characters with upper, lower, and a digit.
              </p>
            </div>
            {submitError && <p className="text-sm text-red-600">{submitError}</p>}
            <Button type="submit" className="w-full" disabled={isSubmitting}>
              {isSubmitting ? 'Updating…' : 'Set password'}
            </Button>
          </form>
        )}
      </div>
    </div>
  );
}
