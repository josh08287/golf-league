import * as React from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/utils';

const badgeVariants = cva(
  'inline-flex items-center rounded-sm px-2 py-0.5 text-[11px] font-semibold uppercase tracking-wider transition-colors focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2',
  {
    variants: {
      variant: {
        default:
          'bg-primary-900 text-white',
        secondary:
          'bg-gray-100 text-gray-800 border border-gray-200/80',
        destructive:
          'bg-red-100 text-red-800',
        outline:
          'border border-primary-900 text-primary-900',
        gold:
          'bg-[#F7E3AC] text-[#6B4806] border border-[#E6C775]',
        silver:
          'bg-gray-200 text-gray-800 border border-gray-300',
        bronze:
          'bg-[#EFE0CE] text-[#784315] border border-[#DFC4A6]',
        green:
          'bg-primary-50 text-primary-900 border border-primary-200/70',
        amber:
          'bg-[#FAF0D7] text-[#8A5A10] border border-[#EFE0B9]',
        blue:
          'bg-sky-50 text-sky-900 border border-sky-200/70',
        /** Admin page aliases */
        success:
          'bg-primary-50 text-primary-900 border border-primary-200/70',
        warning:
          'bg-[#FAF0D7] text-[#8A5A10] border border-[#EFE0B9]',
        info:
          'bg-sky-50 text-sky-900 border border-sky-200/70',
        neutral:
          'bg-gray-100 text-gray-600 border border-gray-200/70',
        red:
          'bg-red-50 text-red-800 border border-red-200/70',
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
