// 21st.dev-inspired Animated Sliding Segmented Tabs (Framer Motion layoutId spring pill)
import type * as React from 'react';
import { useId } from 'react';
import { motion } from 'framer-motion';
import { cn } from '@/lib/utils';

export interface TabOption<T extends string> {
  value: T;
  label: string;
  icon?: React.ReactNode;
}

export function SegmentedTabs<T extends string>({
  options,
  value,
  onChange,
  size = 'md',
  className,
}: {
  options: TabOption<T>[];
  value: T;
  onChange: (v: T) => void;
  size?: 'sm' | 'md';
  className?: string;
}) {
  const groupId = useId();

  return (
    <div
      className={cn(
        'relative inline-flex items-center gap-0.5 rounded-xl border border-zinc-200/85 dark:border-white/[0.08] bg-zinc-100/90 dark:bg-white/[0.04] p-1',
        className
      )}
    >
      {options.map((opt) => {
        const active = opt.value === value;
        return (
          <button
            key={opt.value}
            type="button"
            onClick={() => onChange(opt.value)}
            className={cn(
              'relative z-10 inline-flex items-center gap-1.5 rounded-lg font-medium transition-colors duration-150 cursor-pointer whitespace-nowrap',
              size === 'sm' ? 'px-2.5 py-1 text-[11px]' : 'px-3 py-1.5 text-xs',
              active
                ? 'text-white dark:text-zinc-950'
                : 'text-zinc-600 dark:text-zinc-400 hover:text-zinc-900 dark:hover:text-zinc-100'
            )}
          >
            {active && (
              <motion.span
                layoutId={`seg-pill-${groupId}`}
                transition={{ type: 'spring', stiffness: 460, damping: 34, mass: 0.7 }}
                className="absolute inset-0 -z-10 rounded-lg bg-zinc-900 dark:bg-white shadow-sm"
              />
            )}
            {opt.icon}
            <span>{opt.label}</span>
          </button>
        );
      })}
    </div>
  );
}
