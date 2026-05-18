import { useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/Button';
import { useAuth } from '@/hooks/useAuth';
import { startExternalLogin } from '@/lib/auth';

const schema = z.object({
  email: z.string().email('Valid email required'),
  password: z.string().min(1, 'Password is required'),
});

type FormValues = z.infer<typeof schema>;

export function LoginPage() {
  const { login, isAuthenticated } = useAuth();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const next = params.get('next') ?? '/';
  const [submitError, setSubmitError] = useState<string | null>(null);

  if (isAuthenticated) {
    navigate(next, { replace: true });
  }

  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: '', password: '' },
  });

  async function onSubmit(values: FormValues) {
    setSubmitError(null);
    try {
      await login(values.email, values.password);
    } catch (err) {
      const message =
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error
        ?? 'Sign-in failed. Check your email and password.';
      setSubmitError(message);
    }
  }

  const inputClass =
    'mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20]';

  return (
    <div className="flex min-h-screen items-center justify-center bg-gray-50">
      <div className="w-full max-w-sm rounded-xl border border-gray-200 bg-white p-8 shadow-md">
        <div className="text-center">
          <span className="text-5xl" role="img" aria-label="golf flag">⛳</span>
          <h1 className="mt-4 text-2xl font-bold text-gray-900">Sign in</h1>
        </div>

        <form onSubmit={handleSubmit(onSubmit)} className="mt-6 space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700">Email</label>
            <input type="email" autoComplete="email" {...register('email')} className={inputClass} />
            {errors.email && <p className="mt-1 text-xs text-red-600">{errors.email.message}</p>}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700">Password</label>
            <input type="password" autoComplete="current-password" {...register('password')} className={inputClass} />
            {errors.password && <p className="mt-1 text-xs text-red-600">{errors.password.message}</p>}
          </div>
          {submitError && <p className="text-sm text-red-600">{submitError}</p>}
          <Button type="submit" className="w-full" disabled={isSubmitting}>
            {isSubmitting ? 'Signing in…' : 'Sign in'}
          </Button>
          <p className="text-right text-xs">
            <Link to="/auth/forgot-password" className="text-gray-500 hover:text-primary-900 hover:underline">
              Forgot password?
            </Link>
          </p>
        </form>

        <div className="my-6 flex items-center gap-3 text-xs text-gray-400">
          <div className="h-px flex-1 bg-gray-200" />
          <span>or continue with</span>
          <div className="h-px flex-1 bg-gray-200" />
        </div>

        <div className="space-y-2">
          <Button
            type="button"
            variant="outline"
            className="w-full"
            onClick={() => void startExternalLogin('google')}
          >
            Sign in with Google
          </Button>
        </div>

        <p className="mt-6 text-center text-xs text-gray-400">
          New here? Membership is invite-only — ask an admin to send you a link.
        </p>
      </div>
    </div>
  );
}
