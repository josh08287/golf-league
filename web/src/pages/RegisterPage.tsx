import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/Button';
import { useAuth } from '@/hooks/useAuth';
import { register as registerApi } from '@/lib/auth';

const schema = z.object({
  firstName: z.string().min(1, 'First name is required'),
  lastName: z.string().min(1, 'Last name is required'),
  email: z.string().email('Valid email required'),
  password: z.string().min(10, 'Password must be at least 10 characters'),
});

type FormValues = z.infer<typeof schema>;

export function RegisterPage() {
  const { onLoginSuccess } = useAuth();
  const [submitError, setSubmitError] = useState<string | null>(null);

  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { firstName: '', lastName: '', email: '', password: '' },
  });

  async function onSubmit(values: FormValues) {
    setSubmitError(null);
    try {
      const resp = await registerApi(values);
      await onLoginSuccess(resp);
    } catch (err) {
      const message =
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error
        ?? 'Registration failed. Try a different email or password.';
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
          <h1 className="mt-4 text-2xl font-bold text-gray-900">Create account</h1>
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
            <label className="block text-sm font-medium text-gray-700">Email</label>
            <input type="email" autoComplete="email" {...register('email')} className={inputClass} />
            {errors.email && <p className="mt-1 text-xs text-red-600">{errors.email.message}</p>}
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

        <p className="mt-6 text-center text-sm text-gray-500">
          Already have an account?{' '}
          <Link to="/login" className="text-primary-900 font-medium hover:underline">Sign in</Link>
        </p>
      </div>
    </div>
  );
}
