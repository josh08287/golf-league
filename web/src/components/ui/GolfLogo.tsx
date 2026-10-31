import React from 'react';
import { cn } from '@/lib/utils';

export interface GolfLogoProps extends React.SVGAttributes<SVGSVGElement> {
  size?: 'xs' | 'sm' | 'md' | 'lg' | 'xl';
  variant?: 'closeup' | 'badge';
  alt?: string;
}

const SIZE_MAP = {
  xs: 'h-5 w-5',
  sm: 'h-7 w-7',
  md: 'h-9 w-9',
  lg: 'h-12 w-12',
  xl: 'h-16 w-16',
};

/**
 * Modern vector golf club & league emblem.
 * - `closeup`: minimalist pin, cup, and sweeping fairway contours (ideal inside dark brand containers).
 * - `badge`: full Augusta-pine & burnished-gold architectural crest (ideal standalone on light surfaces).
 */
export const GolfLogo: React.FC<GolfLogoProps> = ({
  size = 'md',
  variant = 'closeup',
  className,
  alt = 'Golf League Emblem',
  ...props
}) => {
  if (variant === 'badge') {
    return (
      <svg
        viewBox="0 0 64 64"
        fill="none"
        xmlns="http://www.w3.org/2000/svg"
        role="img"
        aria-label={alt}
        className={cn('inline-block select-none shrink-0', SIZE_MAP[size], className)}
        {...props}
      >
        {/* Outer architectural crest */}
        <rect x="2" y="2" width="60" height="60" rx="8" fill="#133A25" />
        {/* Inner burnished gold keyline */}
        <rect
          x="5.5"
          y="5.5"
          width="53"
          height="53"
          rx="5"
          stroke="#E5B84B"
          strokeOpacity="0.45"
          strokeWidth="1.25"
        />
        {/* Sweeping fairway horizon arcs */}
        <path
          d="M13 44.5C21.5 39.5 33.5 39.5 51 45"
          stroke="#F7F6F2"
          strokeWidth="2.5"
          strokeLinecap="round"
        />
        <path
          d="M17 50C26.5 46.2 37.5 46.2 47 50"
          stroke="#E5B84B"
          strokeWidth="2"
          strokeLinecap="round"
        />
        {/* Putting cup ring */}
        <ellipse cx="29" cy="44" rx="4.5" ry="1.6" fill="#E5B84B" />
        {/* Slender tapered flagstick */}
        <path
          d="M29 44V14.5"
          stroke="#F7F6F2"
          strokeWidth="2.2"
          strokeLinecap="round"
        />
        {/* Modern geometric pennant */}
        <path
          d="M29 15L45.5 20.5L29 26.5V15Z"
          fill="#E5B84B"
        />
      </svg>
    );
  }

  return (
    <svg
      viewBox="0 0 40 40"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      role="img"
      aria-label={alt}
      className={cn('inline-block select-none shrink-0', SIZE_MAP[size], className)}
      {...props}
    >
      {/* Sweeping fairway contour */}
      <path
        d="M6.5 30.5C13.5 26.5 23.5 26.5 33.5 30.8"
        stroke="#F7F6F2"
        strokeWidth="2.4"
        strokeLinecap="round"
      />
      <path
        d="M10.5 34.8C17.5 32 24.5 32 30.5 34.8"
        stroke="#E5B84B"
        strokeWidth="1.8"
        strokeLinecap="round"
        strokeOpacity="0.9"
      />
      {/* Cup ellipse */}
      <ellipse cx="18.5" cy="30" rx="3.5" ry="1.3" fill="#E5B84B" />
      {/* Flagstick */}
      <path
        d="M18.5 30V7.5"
        stroke="#F7F6F2"
        strokeWidth="2.2"
        strokeLinecap="round"
      />
      {/* Crisp geometric pennant */}
      <path
        d="M18.5 8L32 12.6L18.5 17.4V8Z"
        fill="#E5B84B"
      />
    </svg>
  );
};
