import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMsal } from '@azure/msal-react';
import { useMyStatus } from '@/hooks/useMyStatus';
import { useSubmitRegistration } from '@/hooks/admin/useRegistrations';
import { Button } from '@/components/ui/Button';
import { Spinner } from '@/components/ui/Spinner';

const schema = z.object({
  firstName: z.string().min(1, 'First name is required'),
  lastName: z.string().min(1, 'Last name is required'),
  email: z.string().email('Valid email required'),
  phone: z.string().optional(),
});

type FormValues = z.infer<typeof schema>;

function claimString(claims: Record<string, unknown>, ...keys: string[]): string {
  for (const key of keys) {
    const val = claims[key];
    if (typeof val === 'string' && val.trim()) return val.trim();
  }
  return '';
}

export function RegisterPage() {
  const { accounts } = useMsal();
  const { data: status, isLoading } = useMyStatus();
  const submit = useSubmitRegistration();
  const navigate = useNavigate();

  const account = accounts[0];
  const claims = (account?.idTokenClaims ?? {}) as Record<string, unknown>;

  const prefillFirstName =
    claimString(claims, 'given_name') ||
    (account?.name ? account.name.split(' ')[0] : '');
  const prefillLastName =
    claimString(claims, 'family_name') ||
    (account?.name ? account.name.split(' ').slice(1).join(' ') : '');
  const prefillEmail = claimString(claims, 'email', 'preferred_username') || account?.username || '';
  const prefillPhone = claimString(claims, 'phone_number', 'mobile');

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      firstName: prefillFirstName,
      lastName: prefillLastName,
      email: prefillEmail,
      phone: prefillPhone || '',
    },
  });

  useEffect(() => {
    if (status?.status === 'approved') navigate('/', { replace: true });
  }, [status, navigate]);

  if (isLoading) {
    return (
      <div className="flex min-h-[60vh] items-center justify-center">
        <Spinner />
      </div>
    );
  }

  if (status?.status === 'pending') {
    return <PendingScreen />;
  }

  const isRejected = status?.status === 'rejected';

  async function onSubmit(values: FormValues) {
    await submit.mutateAsync({
      firstName: values.firstName,
      lastName: values.lastName,
      email: values.email,
      phone: values.phone || undefined,
    });
  }

  return (
    <div className="flex min-h-[70vh] items-center justify-center">
      <div className="w-full max-w-md rounded-xl border border-gray-200 bg-white p-8 shadow-md">
        <span className="text-4xl" role="img" aria-label="golf">⛳</span>
        <h1 className="mt-3 text-2xl font-bold text-gray-900">Request to Join</h1>
        <p className="mt-1 text-sm text-gray-500">
          Fill in your details and the league admin will review your request.
        </p>

        {isRejected && (
          <div className="mt-4 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700">
            <strong>Previous request declined.</strong>
            {status.rejectionReason && (
              <span> Reason: {status.rejectionReason}</span>
            )}
            <span> You can re-submit below.</span>
          </div>
        )}

        <form onSubmit={handleSubmit(onSubmit)} className="mt-6 space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700">First Name <span className="text-red-500">*</span></label>
              <input
                {...register('firstName')}
                className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20]"
              />
              {errors.firstName && <p className="mt-1 text-xs text-red-600">{errors.firstName.message}</p>}
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700">Last Name <span className="text-red-500">*</span></label>
              <input
                {...register('lastName')}
                className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20]"
              />
              {errors.lastName && <p className="mt-1 text-xs text-red-600">{errors.lastName.message}</p>}
            </div>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700">Email <span className="text-red-500">*</span></label>
            <input
              {...register('email')}
              type="email"
              className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20]"
            />
            {errors.email && <p className="mt-1 text-xs text-red-600">{errors.email.message}</p>}
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700">Phone <span className="text-gray-400 font-normal">(optional)</span></label>
            <input
              {...register('phone')}
              type="tel"
              placeholder="+1 555 000 0000"
              className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20]"
            />
          </div>

          {submit.isError && (
            <p className="text-sm text-red-600">
              {(submit.error as { response?: { data?: { error?: string } } })?.response?.data?.error ??
                'Failed to submit. Please try again.'}
            </p>
          )}

          <Button
            type="submit"
            variant="primary"
            className="w-full"
            disabled={isSubmitting || submit.isPending}
          >
            Request to Join
          </Button>
        </form>
      </div>
    </div>
  );
}

function PendingScreen() {
  return (
    <div className="flex min-h-[70vh] items-center justify-center">
      <div className="w-full max-w-sm rounded-xl border border-gray-200 bg-white p-8 shadow-md text-center">
        <span className="text-5xl" role="img" aria-label="hourglass">⏳</span>
        <h1 className="mt-4 text-xl font-bold text-gray-900">Request Submitted</h1>
        <p className="mt-3 text-sm text-gray-500">
          Your request to join the league is pending admin review. You'll be able to
          view standings and scorecards once approved.
        </p>
        <p className="mt-4 text-xs text-gray-400">Check back soon — the admin typically reviews within a day.</p>
      </div>
    </div>
  );
}
