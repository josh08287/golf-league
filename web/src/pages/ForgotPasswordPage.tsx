import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/Button';
import { clearAuth, requestPasswordReset } from '@/lib/auth';

const schema = z.object({
  email: z.string().email('Valid email required'),
});

type FormValues = z.infer<typeof schema>;

export function ForgotPasswordPage() {
  const [submitted, setSubmitted] = useState(false);

  // Same reasoning as ResetPasswordPage: user is here precisely because they
  // can't sign in; pre-emptively drop any stale tokens so nothing else on
  // the page accidentally redirects them away.
  useEffect(() => { clearAuth(); }, []);

  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: '' },
  });

  async function onSubmit(values: FormValues) {
    // The API always returns 200, so we don't differentiate between "email
    // exists" and "no such account" — same confirmation screen either way.
    await requestPasswordReset(values.email).catch(() => undefined);
    setSubmitted(true);
  }

  const inputClass =
    'mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20]';

  return (
    <div className="flex min-h-[70vh] items-center justify-center">
      <div className="w-full max-w-sm rounded-xl border border-gray-200 bg-white p-8 shadow-md">
        <div className="text-center">
          <span className="text-5xl" role="img" aria-label="golf flag">⛳</span>
          <h1 className="mt-4 text-2xl font-bold text-gray-900">Reset password</h1>
        </div>

        {submitted ? (
          <div className="mt-6 space-y-3 text-sm text-gray-600">
            <p>
              If an account exists for that email, we&apos;ve sent a link to reset the password.
              Check your inbox (and the spam folder).
            </p>
            <p>
              <Link to="../login" className="text-primary-900 font-medium hover:underline">
                Back to sign in
              </Link>
            </p>
          </div>
        ) : (
          <>
            <p className="mt-2 text-sm text-gray-500">
              Enter your account email and we&apos;ll send a reset link.
            </p>
            <form onSubmit={handleSubmit(onSubmit)} className="mt-6 space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700">Email</label>
                <input type="email" autoComplete="email" {...register('email')} className={inputClass} />
                {errors.email && <p className="mt-1 text-xs text-red-600">{errors.email.message}</p>}
              </div>
              <Button type="submit" className="w-full" disabled={isSubmitting}>
                {isSubmitting ? 'Sending…' : 'Send reset link'}
              </Button>
            </form>
            <p className="mt-6 text-center text-sm text-gray-500">
              <Link to="../login" className="text-primary-900 font-medium hover:underline">
                Back to sign in
              </Link>
            </p>
          </>
        )}
      </div>
    </div>
  );
}
