import { useEffect, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/Button';
import { Spinner } from '@/components/ui/Spinner';
import { useAuth } from '@/hooks/useAuth';
import { useInviteByToken } from '@/hooks/useAcceptInvite';
import { clearAuth, register as registerApi, startExternalLogin } from '@/lib/auth';

const schema = z.object({
  firstName: z.string().min(1, 'First name is required'),
  lastName: z.string().min(1, 'Last name is required'),
  password: z.string().min(10, 'Password must be at least 10 characters'),
});

type FormValues = z.infer<typeof schema>;

/**
 * Invite-only registration. The token must be supplied via ?token=… (the link
 * embedded in the invite email points here). Without a token we render a
 * "this is invite-only" page instead of the form.
 */
export function RegisterPage() {
  const { onLoginSuccess } = useAuth();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const token = params.get('token') ?? '';

  const { data: invite, isLoading, error: inviteError } = useInviteByToken(token || null);

  // Defensive: any stale tokens here mean the user is trying to register
  // a new account; the existing session is irrelevant.
  useEffect(() => { clearAuth(); }, []);

  const [submitError, setSubmitError] = useState<string | null>(null);

  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { firstName: '', lastName: '', password: '' },
  });

  if (!token) {
    return <InviteOnlyNotice message="Registration is invite-only. Ask an admin to send you an invite." />;
  }

  if (isLoading) {
    return <div className="flex min-h-[60vh] items-center justify-center"><Spinner /></div>;
  }

  if (inviteError || !invite) {
    return <InviteOnlyNotice message="This invite link is invalid or has expired." />;
  }

  if (invite.status === 'Accepted') {
    return <InviteOnlyNotice message="This invite has already been used. Try signing in instead." />;
  }
  if (invite.status === 'Revoked') {
    return <InviteOnlyNotice message="This invite was revoked. Contact the league admin." />;
  }
  if (new Date(invite.expiresAt) < new Date()) {
    return <InviteOnlyNotice message="This invite has expired. Ask the admin for a fresh one." />;
  }

  async function onSubmit(values: FormValues) {
    setSubmitError(null);
    try {
      const resp = await registerApi({
        email: invite!.email,
        password: values.password,
        firstName: values.firstName,
        lastName: values.lastName,
        inviteToken: token,
      });
      await onLoginSuccess(resp);
      if (!resp.mfaRequired) navigate('/', { replace: true });
    } catch (err) {
      const message =
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error
        ?? 'Registration failed.';
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
          <h1 className="mt-4 text-2xl font-bold text-gray-900">Join Golf League</h1>
          <p className="mt-1 text-sm text-gray-500">Invite for <strong>{invite.email}</strong></p>
        </div>

        <form onSubmit={handleSubmit(onSubmit)} className="mt-6 space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700">First name</label>
              <input {...register('firstName')} className={inputClass} />
              {errors.firstName && <p className="mt-1 text-xs text-red-600">{errors.firstName.message}</p>}
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700">Last name</label>
              <input {...register('lastName')} className={inputClass} />
              {errors.lastName && <p className="mt-1 text-xs text-red-600">{errors.lastName.message}</p>}
            </div>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700">Password</label>
            <input type="password" autoComplete="new-password" {...register('password')} className={inputClass} />
            {errors.password && <p className="mt-1 text-xs text-red-600">{errors.password.message}</p>}
            <p className="mt-1 text-xs text-gray-400">
              At least 10 characters with upper, lower, and a digit.
            </p>
          </div>

          {submitError && <p className="text-sm text-red-600">{submitError}</p>}
          <Button type="submit" className="w-full" disabled={isSubmitting}>
            {isSubmitting ? 'Creating account…' : 'Create account'}
          </Button>
        </form>

        <div className="my-6 flex items-center gap-3 text-xs text-gray-400">
          <div className="h-px flex-1 bg-gray-200" />
          <span>or join with</span>
          <div className="h-px flex-1 bg-gray-200" />
        </div>

        <div className="space-y-2">
          <Button
            type="button"
            variant="outline"
            className="w-full"
            onClick={() => void startExternalLogin('google')}
          >
            Continue with Google
          </Button>
          {/* Facebook sign-in disabled in UI; backend support remains so the
              button can be re-added later without touching server code. */}
        </div>

        <p className="mt-4 text-center text-xs text-gray-400">
          Social sign-in only works if the provider's email matches your invite.
        </p>

        <p className="mt-6 text-center text-sm text-gray-500">
          Already joined?{' '}
          <Link to="/login" className="text-primary-900 font-medium hover:underline">Sign in</Link>
        </p>
      </div>
    </div>
  );
}

function InviteOnlyNotice({ message }: { message: string }) {
  return (
    <div className="flex min-h-[70vh] items-center justify-center">
      <div className="w-full max-w-sm rounded-xl border border-gray-200 bg-white p-8 shadow-md text-center">
        <span className="text-5xl" role="img" aria-label="locked">🔒</span>
        <h1 className="mt-4 text-xl font-bold text-gray-900">Invite required</h1>
        <p className="mt-3 text-sm text-gray-500">{message}</p>
        <p className="mt-4 text-sm">
          <Link to="/login" className="text-primary-900 font-medium hover:underline">
            Back to sign in
          </Link>
        </p>
      </div>
    </div>
  );
}
