import { AlertCircle } from 'lucide-react';
import { cn } from '@/lib/utils';

interface ErrorMessageProps {
  title?: string;
  message?: string;
  className?: string;
}

export function ErrorMessage({
  title = 'Something went wrong',
  message = 'An error occurred while loading the data. Please try again.',
  className,
}: ErrorMessageProps) {
  return (
    <div
      role="alert"
      className={cn(
        'flex items-start gap-3 rounded-lg border border-red-200 bg-red-50 p-4 text-red-800',
        className,
      )}
    >
      <AlertCircle className="mt-0.5 h-5 w-5 flex-shrink-0 text-red-500" />
      <div>
        <p className="font-semibold">{title}</p>
        <p className="mt-1 text-sm">{message}</p>
      </div>
    </div>
  );
}
