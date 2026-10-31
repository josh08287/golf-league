import { cn } from '@/lib/utils';

interface PageHeaderProps {
  title: string;
  /** Primary subtitle / description line below the title */
  description?: string;
  /** Alias for description — used by admin pages */
  subtitle?: React.ReactNode;
  className?: string;
  children?: React.ReactNode;
}

export function PageHeader({
  title,
  description,
  subtitle,
  className,
  children,
}: PageHeaderProps) {
  const sub = subtitle ?? description;
  return (
    <div
      className={cn(
        'flex flex-col gap-3 border-b border-gray-200 pb-6 sm:flex-row sm:items-end sm:justify-between',
        className,
      )}
    >
      <div>
        <h1 className="font-sans text-3xl font-extrabold tracking-tight text-gray-900 sm:text-4xl">
          {title}
        </h1>
        {sub && (
          <div className="mt-1.5 text-sm text-gray-600">{sub}</div>
        )}
      </div>
      {children && <div className="flex flex-wrap items-center gap-2.5">{children}</div>}
    </div>
  );
}
