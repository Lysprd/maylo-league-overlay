import type * as React from 'react';
import { motion } from 'framer-motion';
import { cn } from '@/lib/utils';

export function TierBadge({
  tier,
  size = 'md',
  dark = false,
  className,
}: {
  tier?: string;
  size?: 'xs' | 'sm' | 'md';
  dark?: boolean;
  className?: string;
}) {
  const t = (tier || '?').toUpperCase();
  const map: Record<string, string> = dark
    ? {
        'S+': 'bg-gradient-to-b from-amber-400/28 to-amber-600/22 text-amber-200 border-amber-400/80 shadow-[0_0_12px_rgba(251,191,36,0.24)]',
        S: 'bg-[#261E0B] text-amber-300 border-amber-400/55',
        A: 'bg-[#191B33] text-indigo-300 border-indigo-400/55',
        B: 'bg-[#112136] text-sky-300 border-sky-400/50',
        C: 'bg-[#1E1F26] text-zinc-300 border-zinc-500/45',
        D: 'bg-[#2E1318] text-rose-300 border-rose-400/50',
      }
    : {
        'S+': 'bg-gradient-to-b from-amber-100 to-amber-200/85 text-amber-900 border-amber-300 dark:from-amber-400/26 dark:to-amber-600/18 dark:text-amber-200 dark:border-amber-400/65 dark:shadow-[0_0_12px_rgba(251,191,36,0.18)]',
        S: 'bg-amber-100/85 text-amber-800 border-amber-200 dark:bg-amber-500/15 dark:text-amber-300 dark:border-amber-400/35',
        A: 'bg-indigo-100 text-indigo-700 border-indigo-200 dark:bg-indigo-500/18 dark:text-indigo-300 dark:border-indigo-400/35',
        B: 'bg-sky-100 text-sky-700 border-sky-200 dark:bg-sky-500/18 dark:text-sky-300 dark:border-sky-400/35',
        C: 'bg-zinc-100 text-zinc-600 border-zinc-200 dark:bg-white/10 dark:text-zinc-300 dark:border-white/15',
        D: 'bg-rose-100 text-rose-700 border-rose-200 dark:bg-rose-500/18 dark:text-rose-300 dark:border-rose-400/35',
      };
  const colors =
    map[t] ??
    (dark
      ? 'bg-[#1B1D24] text-zinc-300 border-zinc-500/40'
      : 'bg-zinc-100 text-zinc-600 border-zinc-200 dark:bg-white/10 dark:text-zinc-300 dark:border-white/15');

  const sizing =
    size === 'xs'
      ? 'h-5 min-w-[22px] px-1.5 text-[10px] rounded-md'
      : size === 'sm'
        ? 'h-6 min-w-[26px] px-2 text-[11px] rounded-md'
        : 'h-7 min-w-[30px] px-2.5 text-xs rounded-lg';

  return (
    <span
      className={cn(
        'font-display inline-flex items-center justify-center border font-bold tracking-tight leading-none shrink-0',
        colors,
        sizing,
        className
      )}
    >
      {t}
    </span>
  );
}

export function Keycap({ keys, inverted = false }: { keys: string; inverted?: boolean }) {
  return (
    <span className="inline-flex items-center gap-1">
      {keys.split('+').map((k) => (
        <kbd
          key={k}
          className={cn(
            'font-mono-tech inline-flex h-6 min-w-[24px] items-center justify-center rounded-md border px-2 text-[10px] font-semibold leading-none',
            inverted
              ? 'border-white/20 bg-white/12 text-white dark:border-zinc-900/20 dark:bg-zinc-900/12 dark:text-zinc-900'
              : 'border-zinc-200 dark:border-white/12 bg-white dark:bg-white/[0.07] text-zinc-700 dark:text-zinc-200 shadow-[0_1px_0_rgba(0,0,0,0.06)]'
          )}
        >
          {k}
        </kbd>
      ))}
    </span>
  );
}

export function Switch({
  checked,
  onChange,
  label,
  description,
}: {
  checked: boolean;
  onChange: (next: boolean) => void;
  label: string;
  description?: string;
}) {
  return (
    <button
      type="button"
      onClick={() => onChange(!checked)}
      className="group flex w-full items-center justify-between gap-4 rounded-xl px-2.5 py-2 text-left transition-colors duration-150 hover:bg-zinc-100/85 dark:hover:bg-white/[0.045] active:scale-[0.995] cursor-pointer"
    >
      <div className="min-w-0">
        <div className="text-[13px] font-medium text-zinc-900 dark:text-zinc-100">{label}</div>
        {description && <div className="text-[11px] text-zinc-500 dark:text-zinc-400 mt-0.5 leading-snug">{description}</div>}
      </div>
      <span
        className={cn(
          'relative inline-flex h-5 w-9 shrink-0 items-center rounded-full p-0.5 transition-all duration-200',
          checked
            ? 'justify-end bg-indigo-500 shadow-[0_0_12px_rgba(99,102,241,0.4)]'
            : 'justify-start bg-zinc-300 dark:bg-zinc-700'
        )}
      >
        <motion.span
          layout
          transition={{ type: 'spring', stiffness: 560, damping: 32, mass: 0.65 }}
          className="inline-block size-4 rounded-full bg-white shadow-sm"
        />
      </span>
    </button>
  );
}

export function Button({
  variant = 'secondary',
  size = 'md',
  className,
  children,
  ...props
}: React.ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: 'primary' | 'secondary' | 'ghost' | 'accent' | 'danger';
  size?: 'xs' | 'sm' | 'md';
}) {
  const styles: Record<string, string> = {
    primary: 'bg-zinc-900 text-white hover:bg-zinc-800 dark:bg-white dark:text-zinc-950 dark:hover:bg-zinc-200 border-transparent shadow-[inset_0_1px_0_rgba(255,255,255,0.14),0_1px_2px_rgba(0,0,0,0.08)]',
    accent: 'bg-indigo-600 text-white hover:bg-indigo-500 border-transparent shadow-[inset_0_1px_0_rgba(255,255,255,0.2),0_4px_14px_-4px_rgba(99,102,241,0.5)]',
    secondary: 'bg-white dark:bg-white/[0.05] text-zinc-800 dark:text-zinc-100 border-zinc-200 dark:border-white/[0.11] hover:bg-zinc-50 hover:border-zinc-300 dark:hover:bg-white/[0.09] dark:hover:border-white/[0.18]',
    ghost: 'bg-transparent text-zinc-600 dark:text-zinc-300 border-transparent hover:bg-zinc-100 dark:hover:bg-white/[0.07]',
    danger: 'bg-rose-50 dark:bg-rose-500/15 text-rose-700 dark:text-rose-300 border-rose-200 dark:border-rose-500/30 hover:bg-rose-100 dark:hover:bg-rose-500/25',
  };
  const sizes: Record<string, string> = {
    xs: 'px-2.5 py-1 text-[11px] rounded-lg gap-1',
    sm: 'px-2.5 py-1.5 text-xs rounded-lg gap-1.5',
    md: 'px-3.5 py-2 text-[13px] rounded-xl gap-2',
  };
  return (
    <button
      type="button"
      className={cn(
        'inline-flex items-center justify-center border font-medium transition-all duration-150 active:scale-[0.975] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500/50 cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed disabled:active:scale-100',
        styles[variant],
        sizes[size],
        className
      )}
      {...props}
    >
      {children}
    </button>
  );
}
