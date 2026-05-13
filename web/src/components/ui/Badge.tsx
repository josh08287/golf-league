import * as React from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/utils';

const badgeVariants = cva(
  'inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold transition-colors focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2',
  {
    variants: {
      variant: {
        default:
          'bg-primary-900 text-white',
        secondary:
          'bg-primary-100 text-primary-900',
        destructive:
          'bg-red-100 text-red-700',
        outline:
          'border border-primary-900 text-primary-900',
        gold:
          'bg-yellow-400 text-yellow-900',
        silver:
          'bg-gray-300 text-gray-700',
        bronze:
          'bg-amber-600 text-white',
        green:
          'bg-green-100 text-green-800',
        amber:
          'bg-amber-100 text-amber-800',
        blue:
          'bg-blue-100 text-blue-800',
        /** Admin page aliases */
        success:
          'bg-green-100 text-green-800',
        warning:
          'bg-amber-100 text-amber-800',
        info:
          'bg-blue-100 text-blue-800',
        neutral:
          'bg-gray-100 text-gray-600',
        red:
          'bg-red-100 text-red-800',
      },
    },
    defaultVariants: {
      variant: 'default',
    },
  },
);

export interface BadgeProps
  extends React.HTMLAttributes<HTMLDivElement>,
    VariantProps<typeof badgeVariants> {}

function Badge({ className, variant, ...props }: BadgeProps) {
  return (
    <div className={cn(badgeVariants({ variant }), className)} {...props} />
  );
}

export { Badge, badgeVariants };
