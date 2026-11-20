// 21st.dev-inspired Spotlight & Glass Card (Linear / Raycast / Vercel aesthetic)
import React, { useState } from 'react';
import { cn } from '@/lib/utils';

export interface GlassCardProps extends React.HTMLAttributes<HTMLDivElement> {
  highlight?: 'none' | 'accent' | 'gold' | 'purple';
  surface?: 'overlay' | 'hub';
  spotlight?: boolean;
}

export function GlassCard({
  className,
  highlight = 'none',
  surface = 'overlay',
  spotlight = false,
  children,
  onMouseMove,
  onMouseLeave,
  ...props
}: GlassCardProps) {
  const [spot, setSpot] = useState<{ x: number; y: number }>({ x: 0, y: 0 });
  const [hovering, setHovering] = useState(false);
  const isOverlay = surface === 'overlay';

  const borderClass =
    highlight === 'accent'
      ? 'border-indigo-400/70 shadow-[0_0_24px_-6px_rgba(129,140,248,0.42)]'
      : highlight === 'gold'
        ? 'border-amber-400/70 shadow-[0_0_24px_-6px_rgba(251,191,36,0.35)]'
        : highlight === 'purple'
          ? 'border-purple-400/65 shadow-[0_0_24px_-6px_rgba(192,132,252,0.38)]'
          : isOverlay
            ? 'border-white/[0.12] shadow-[0_16px_38px_rgba(0,0,0,0.58)]'
            : 'border-zinc-200/85 dark:border-white/[0.08] shadow-[0_1px_2px_rgba(15,23,42,0.04),0_8px_24px_-14px_rgba(15,23,42,0.07)] dark:shadow-[0_2px_10px_rgba(0,0,0,0.28)]';

  const bgClass = isOverlay
    ? highlight === 'gold'
      ? 'bg-[#141418]/94 backdrop-blur-xl text-zinc-50'
      : 'bg-[#0E1017]/93 backdrop-blur-xl text-zinc-50'
    : highlight === 'accent'
      ? 'bg-indigo-50/50 dark:bg-[#171926]/95'
      : highlight === 'gold'
        ? 'bg-amber-50/50 dark:bg-[#1A1814]/95'
        : 'bg-white dark:bg-[#13141B]';

  const handleMove = (e: React.MouseEvent<HTMLDivElement>) => {
    if (spotlight) {
      const r = e.currentTarget.getBoundingClientRect();
      setSpot({ x: e.clientX - r.left, y: e.clientY - r.top });
      if (!hovering) setHovering(true);
    }
    onMouseMove?.(e);
  };

  return (
    <div
      onMouseMove={handleMove}
      onMouseLeave={(e) => {
        if (spotlight) setHovering(false);
        onMouseLeave?.(e);
      }}
      className={cn(
        'relative overflow-hidden rounded-2xl border transition-all duration-200',
        borderClass,
        bgClass,
        className
      )}
      {...props}
    >
      {/* Subtle top specular rim */}
      {isOverlay ? (
        <div className="pointer-events-none absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-white/25 to-transparent" />
      ) : (
        <div className="pointer-events-none absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-zinc-900/[0.04] dark:via-white/[0.08] to-transparent" />
      )}
      {/* Interactive 21st.dev radial spotlight (silky opacity fade) */}
      {spotlight && (
        <div
          className={cn(
            'pointer-events-none absolute -inset-px transition-opacity duration-300',
            hovering ? 'opacity-100' : 'opacity-0'
          )}
          style={{
            background: `radial-gradient(260px circle at ${spot.x}px ${spot.y}px, rgba(129, 140, 248, 0.11), transparent 72%)`,
          }}
        />
      )}
      {children}
    </div>
  );
}
