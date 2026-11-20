// 21st.dev-inspired Motion Primitives (Framer Motion spring physics + GPU BorderBeam + NumberTicker)
import React, { useEffect, useState } from 'react';
import { motion, useSpring } from 'framer-motion';
import { cn } from '@/lib/utils';

export const SWISS_EASE = [0.22, 1, 0.36, 1] as const;

/**
 * 21st.dev BorderBeam — subtle traveling luminous comet tracing a card perimeter.
 */
export function BorderBeam({
  className,
  tone = 'indigo',
}: {
  className?: string;
  tone?: 'indigo' | 'gold';
}) {
  const grad =
    tone === 'gold'
      ? 'conic-gradient(from 0deg, transparent 0deg, transparent 285deg, rgba(251,191,36,0.55) 335deg, rgba(245,158,11,0.9) 355deg, transparent 360deg)'
      : 'conic-gradient(from 0deg, transparent 0deg, transparent 285deg, rgba(99,102,241,0.45) 332deg, rgba(129,140,248,0.92) 355deg, transparent 360deg)';

  return (
    <div
      aria-hidden="true"
      className={cn('pointer-events-none absolute inset-0 overflow-hidden rounded-[inherit]', className)}
      style={{
        padding: '1px',
        WebkitMask: 'linear-gradient(#fff 0 0) content-box, linear-gradient(#fff 0 0)',
        WebkitMaskComposite: 'xor',
        maskComposite: 'exclude',
      }}
    >
      <div
        className="border-beam-conic absolute -inset-[100%] opacity-80"
        style={{ background: grad }}
      />
    </div>
  );
}

/**
 * 21st.dev NumberTicker — silky spring count-up for numeric KPIs.
 */
export function NumberTicker({
  value,
  decimals = 1,
  suffix = '',
  prefix = '',
  className,
}: {
  value: number;
  decimals?: number;
  suffix?: string;
  prefix?: string;
  className?: string;
}) {
  const spring = useSpring(0, { stiffness: 120, damping: 22, mass: 0.6 });
  const [display, setDisplay] = useState(value);

  useEffect(() => {
    spring.set(value);
  }, [value, spring]);

  useEffect(() => {
    const unsub = spring.on('change', (latest) => {
      setDisplay(latest);
    });
    return () => unsub();
  }, [spring]);

  return (
    <span className={cn('tabular-nums', className)}>
      {prefix}
      {display.toFixed(decimals)}
      {suffix}
    </span>
  );
}

/**
 * Smooth Swiss view-switch entrance container.
 */
export function MotionPage({
  pageKey,
  children,
  className,
}: {
  pageKey: string;
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <motion.div
      key={pageKey}
      initial={{ opacity: 0, y: 8 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, y: -4 }}
      transition={{ duration: 0.22, ease: SWISS_EASE }}
      className={className}
    >
      {children}
    </motion.div>
  );
}

/**
 * Staggered Bento / Card Grid entrance.
 */
export function StaggerGrid({
  children,
  className,
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <motion.div
      initial="hidden"
      animate="visible"
      variants={{
        hidden: {},
        visible: {
          transition: {
            staggerChildren: 0.04,
            delayChildren: 0.02,
          },
        },
      }}
      className={className}
    >
      {children}
    </motion.div>
  );
}

export function StaggerItem({
  children,
  className,
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <motion.div
      variants={{
        hidden: { opacity: 0, y: 10, scale: 0.985 },
        visible: {
          opacity: 1,
          y: 0,
          scale: 1,
          transition: { duration: 0.28, ease: SWISS_EASE },
        },
      }}
      className={className}
    >
      {children}
    </motion.div>
  );
}

/**
 * Swiss Section Eyebrow (numbered editorial header e.g. "01 / METRICS").
 */
export function SwissEyebrow({
  index,
  label,
  className,
}: {
  index?: string;
  label: string;
  className?: string;
}) {
  return (
    <div className={cn('flex items-center gap-2 font-mono-tech text-[10px] font-semibold uppercase tracking-[0.12em] text-zinc-400 dark:text-zinc-500', className)}>
      {index && <span className="text-indigo-500 dark:text-indigo-400">{index}</span>}
      {index && <span className="text-zinc-300 dark:text-white/15">/</span>}
      <span>{label}</span>
    </div>
  );
}
