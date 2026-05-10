import { useEffect } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMsal, useIsAuthenticated } from '@azure/msal-react';
import { InteractionStatus } from '@azure/msal-browser';
import { msalInstance, loginRequest } from '@/lib/msalConfig';
import { useInviteByToken, useAcceptInvite } from '@/hooks/useAcceptInvite';
import { Spinner } from '@/components/ui/Spinner';
import { Button } from '@/components/ui/Button';

const schema = z.object({
  firstName: z.string().min(1, 'First name is required'),
  lastName: z.string().min(1, 'Last name is required'),
  email: z.string().email('Valid email required'),
  phone: z.string().optional(),
});

type FormValues = z.infer<typeof schema>;

function claimStr(claims: Record<string, unknown>, ...keys: string[]): string {
  for (const key of keys) {
    const v = claims[key];
    if (typeof v === 'string' && v.trim()) return v.trim();
  }
  return '';
}

export function AcceptInvitePage() {
  const [params] = useSearchParams();
  const token = params.get('token') ?? '';
  const navigate = useNavigate();
  const isAuthenticated = useIsAuthenticated();
  const { accounts, inProgress } = useMsal();

  const { data: invite, isLoading: inviteLoading, error: inviteError } = useInviteByToken(token || null);
  const accept = useAcceptInvite(token);

  const account = accounts[0];
  const claims = (account?.idTokenClaims ?? {}) as Record<string, unknown>;

  const prefillFirstName = claimStr(claims, 'given_name') || account?.name?.split(' ')[0] || '';
  const prefillLastName = claimStr(claims, 'family_name') || account?.name?.split(' ').slice(1).join(' ') || '';
  const prefillEmail = claimStr(claims, 'email', 'preferred_username') || account?.username || invite?.email || '';
  const prefillPhone = claimStr(claims, 'phone_number', 'mobile');

  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<FormValues>({
    resolver: zodResolver(schema),
    values: {
      firstName: prefillFirstName,
      lastName: prefillLastName,
      email: prefillEmail,
      phone: prefillPhone,
    },
  });

  useEffect(() => {
    if (accept.isSuccess) navigate('/', { replace: true });
  }, [accept.isSuccess, navigate]);

  if (!token) {
    return <InvalidInvite message="No invite token found in this link." />;
  }

  if (inviteLoading) {
    return <div className="flex min-h-[60vh] items-center justify-center"><Spinner /></div>;
  }

  if (inviteError || !invite) {
    return <InvalidInvite message="This invite link is invalid or has expired." />;
  }

  if (invite.status === 'Accepted') {
    return <InvalidInvite message="This invite has already been used." />;
  }

  if (invite.status === 'Revoked') {
    return <InvalidInvite message="This invite has been revoked. Please contact the league admin." />;
  }

  if (new Date(invite.expiresAt) < new Date()) {
    return <InvalidInvite message="This invite has expired. Please contact the league admin for a new one." />;
  }

  // Wait for MSAL to finish processing (e.g., after redirect from Google sign-in)
  if (inProgress !== InteractionStatus.None) {
    return (
      <div className="flex min-h-[70vh] items-center justify-center">
        <Spinner />
      </div>
    );
  }

  // Not signed in yet — prompt them to sign in first
  if (!isAuthenticated) {
    // Pre-fill the invited email in the login/create forms
    const signInRequest = {
      ...loginRequest,
      loginHint: invite.email,
    };
    const createAccountRequest = {
      ...loginRequest,
      loginHint: invite.email,
      prompt: 'create' as const,
    };

    return (
      <div className="flex min-h-[70vh] items-center justify-center">
        <div className="w-full max-w-sm rounded-xl border border-gray-200 bg-white p-8 shadow-md text-center">
          <span className="text-5xl" role="img" aria-label="golf">⛳</span>
          <h1 className="mt-4 text-xl font-bold text-gray-900">You've been invited!</h1>
          <p className="mt-2 text-sm text-gray-500">
            Sign in or create an account to join the league.
          </p>
          <p className="mt-1 text-xs text-gray-400">Invite for: <strong>{invite.email}</strong></p>
          <Button
            className="mt-6 w-full"
            size="lg"
            onClick={() => void msalInstance.loginRedirect(signInRequest)}
          >
            Sign in
          </Button>
          <Button
            className="mt-3 w-full"
            size="lg"
            variant="outline"
            onClick={() => void msalInstance.loginRedirect(createAccountRequest)}
          >
            Create account
          </Button>
          <p className="mt-4 text-xs text-gray-400">
            You can use email, Google, or Apple
          </p>
        </div>
      </div>
    );
  }

  async function onSubmit(values: FormValues) {
    await accept.mutateAsync({
      firstName: values.firstName,
      lastName: values.lastName,
      email: values.email,
      phone: values.phone || undefined,
    });
  }

  const inputClass =
    'mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20]';

  return (
    <div className="flex min-h-[70vh] items-center justify-center">
      <div className="w-full max-w-md rounded-xl border border-gray-200 bg-white p-8 shadow-md">
        <span className="text-4xl" role="img" aria-label="golf">⛳</span>
        <h1 className="mt-3 text-2xl font-bold text-gray-900">Join Golf League</h1>
        <p className="mt-1 text-sm text-gray-500">
          Confirm your details to complete your registration.
        </p>

        <form onSubmit={handleSubmit(onSubmit)} className="mt-6 space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700">First Name <span className="text-red-500">*</span></label>
              <input {...register('firstName')} className={inputClass} />
              {errors.firstName && <p className="mt-1 text-xs text-red-600">{errors.firstName.message}</p>}
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700">Last Name <span className="text-red-500">*</span></label>
              <input {...register('lastName')} className={inputClass} />
              {errors.lastName && <p className="mt-1 text-xs text-red-600">{errors.lastName.message}</p>}
            </div>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700">Email <span className="text-red-500">*</span></label>
            <input {...register('email')} type="email" className={inputClass} />
            {errors.email && <p className="mt-1 text-xs text-red-600">{errors.email.message}</p>}
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700">
              Phone <span className="text-gray-400 font-normal">(optional)</span>
            </label>
            <input {...register('phone')} type="tel" placeholder="+1 555 000 0000" className={inputClass} />
          </div>

          {accept.isError && (
            <p className="text-sm text-red-600">
              {(accept.error as { response?: { data?: { error?: string } } })?.response?.data?.error ??
                'Failed to accept invite. Please try again.'}
            </p>
          )}

          <Button type="submit" variant="primary" className="w-full" disabled={isSubmitting || accept.isPending}>
            {accept.isPending ? 'Joining…' : 'Join the League'}
          </Button>
        </form>
      </div>
    </div>
  );
}

function InvalidInvite({ message }: { message: string }) {
  return (
    <div className="flex min-h-[70vh] items-center justify-center">
      <div className="w-full max-w-sm rounded-xl border border-gray-200 bg-white p-8 shadow-md text-center">
        <span className="text-5xl" role="img" aria-label="warning">⚠️</span>
        <h1 className="mt-4 text-xl font-bold text-gray-900">Invalid Invite</h1>
        <p className="mt-3 text-sm text-gray-500">{message}</p>
      </div>
    </div>
  );
}
