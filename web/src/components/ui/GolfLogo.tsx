import React from 'react';
import { cn } from '@/lib/utils';

export interface GolfLogoProps extends React.ImgHTMLAttributes<HTMLImageElement> {
  size?: 'xs' | 'sm' | 'md' | 'lg' | 'xl';
  variant?: 'closeup' | 'badge';
}

const SIZE_MAP = {
  xs: 'h-6 w-6',
  sm: 'h-8 w-8',
  md: 'h-10 w-10',
  lg: 'h-14 w-14',
  xl: 'h-20 w-20',
};

export const GolfLogo: React.FC<GolfLogoProps> = ({
  size = 'md',
  variant = 'closeup',
  className,
  alt = 'Golf League Logo',
  ...props
}) => {
  const src = variant === 'badge' ? '/logo-badge.png' : '/logo-192.png';

  return (
    <img
      src={src}
      alt={alt}
      className={cn(
        'inline-block object-contain select-none transition-transform',
        SIZE_MAP[size],
        className,
      )}
      loading="eager"
      {...props}
    />
  );
};
