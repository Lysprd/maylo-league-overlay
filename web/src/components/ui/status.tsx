// Based on 21st.dev component: diceui/status (id: 25393)
import { cva, type VariantProps } from 'class-variance-authority';
import type * as React from 'react';
import { cn } from '@/lib/utils';

const statusVariants = cva(
  'inline-flex w-fit shrink-0 items-center gap-2 overflow-hidden whitespace-nowrap rounded-full border px-3 py-1 font-medium text-[11px] leading-none transition-colors',
  {
    variants: {
      variant: {
        default: 'border-zinc-500/20 bg-zinc-500/10 text-zinc-600 dark:text-zinc-300',
        success: 'border-indigo-500/25 bg-indigo-500/12 text-indigo-700 dark:text-indigo-300',
        live: 'border-teal-500/25 bg-teal-500/12 text-teal-700 dark:text-teal-300',
        error: 'border-rose-500/25 bg-rose-500/12 text-rose-700 dark:text-rose-400',
        warning: 'border-amber-500/25 bg-amber-500/12 text-amber-700 dark:text-amber-300',
        info: 'border-sky-500/25 bg-sky-500/12 text-sky-700 dark:text-sky-300',
      },
    },
    defaultVariants: {
      variant: 'default',
    },
  }
);

const dotColors: Record<string, string> = {
  default: 'bg-zinc-400',
  success: 'bg-indigo-400',
  live: 'bg-teal-400',
  error: 'bg-rose-500',
  warning: 'bg-amber-400',
  info: 'bg-sky-400',
};

export interface StatusProps extends VariantProps<typeof statusVariants>, React.HTMLAttributes<HTMLDivElement> {
  pulse?: boolean;
}

export function Status({ className, variant = 'default', pulse = true, children, ...props }: StatusProps) {
  const dot = dotColors[variant ?? 'default'] ?? dotColors.default;
  return (
    <div data-slot="status" className={cn(statusVariants({ variant }), className)} {...props}>
      <span className="relative flex size-1.5 shrink-0">
        {pulse && <span className={cn('absolute inset-0 rounded-full opacity-75 animate-ping', dot)} />}
        <span className={cn('relative inline-flex size-1.5 rounded-full', dot)} />
      </span>
      <span className="leading-none">{children}</span>
    </div>
  );
}
