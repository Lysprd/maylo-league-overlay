import { motion } from 'framer-motion';
import { cn } from '@/lib/utils';

declare const __MAYLO_VERSION__: string;

/**
 * Playful Minimalist Flat Swiss Companion Icon ("maylo")
 * Pure geometric flat shapes on a 32x32 grid: crisp rounded head, bold flat indigo side fins,
 * and clean expressive pill eyes + smile. Reads sharply from 16px to 256px.
 */
export function MayloEmblem({
  className = 'size-6',
  light,
  interactive = false,
}: {
  className?: string;
  light?: boolean;
  interactive?: boolean;
}) {
  const accentFill = light === true ? '#4F46E5' : light === false ? '#818CF8' : undefined;
  const faceCutout = light === true ? '#FFFFFF' : light === false ? '#11131B' : undefined;

  const svg = (
    <svg viewBox="0 0 32 32" className={cn('shrink-0', className)} fill="none" aria-hidden="true">
      {/* Left flat geometric ear-fins */}
      <rect
        x="2.2"
        y="10.6"
        width="6.2"
        height="3.1"
        rx="1.55"
        transform="rotate(20 5.3 12.15)"
        className={!accentFill ? 'fill-indigo-600 dark:fill-indigo-400' : undefined}
        fill={accentFill}
      />
      <rect
        x="2.2"
        y="17.8"
        width="6.2"
        height="3.1"
        rx="1.55"
        transform="rotate(-20 5.3 19.35)"
        className={!accentFill ? 'fill-indigo-600 dark:fill-indigo-400' : undefined}
        fill={accentFill}
      />

      {/* Right flat geometric ear-fins */}
      <rect
        x="23.6"
        y="10.6"
        width="6.2"
        height="3.1"
        rx="1.55"
        transform="rotate(-20 26.7 12.15)"
        className={!accentFill ? 'fill-indigo-600 dark:fill-indigo-400' : undefined}
        fill={accentFill}
      />
      <rect
        x="23.6"
        y="17.8"
        width="6.2"
        height="3.1"
        rx="1.55"
        transform="rotate(20 26.7 19.35)"
        className={!accentFill ? 'fill-indigo-600 dark:fill-indigo-400' : undefined}
        fill={accentFill}
      />

      {/* Main flat geometric character head */}
      <rect x="6.5" y="7.5" width="19" height="17" rx="8.5" fill="currentColor" />

      {/* Playful minimalist pill eyes */}
      <rect
        x="11.4"
        y="13.2"
        width="2.5"
        height="3.8"
        rx="1.25"
        className={!faceCutout ? 'fill-white dark:fill-[#11131B]' : undefined}
        fill={faceCutout}
      />
      <rect
        x="18.1"
        y="13.2"
        width="2.5"
        height="3.8"
        rx="1.25"
        className={!faceCutout ? 'fill-white dark:fill-[#11131B]' : undefined}
        fill={faceCutout}
      />

      {/* Minimalist happy flat smile */}
      <path
        d="M13.6 19.3C14.3 20.5 17.7 20.5 18.4 19.3"
        className={!faceCutout ? 'stroke-white dark:stroke-[#11131B]' : undefined}
        stroke={faceCutout}
        strokeWidth="1.9"
        strokeLinecap="round"
      />
    </svg>
  );

  if (!interactive) return svg;
  return (
    <motion.div
      whileHover={{ rotate: [-3, 4, 0], scale: 1.08 }}
      whileTap={{ scale: 0.94 }}
      transition={{ type: 'spring', stiffness: 440, damping: 18 }}
      className="inline-flex items-center justify-center"
    >
      {svg}
    </motion.div>
  );
}

/**
 * Playful Animated Splash Companion — bouncy float, fluttering indigo ear-fins,
 * periodic eye blinks, subtle breathing floor shadow, and gentle orbiting sparks.
 */
export function AnimatedMayloEmblem({
  light = false,
  className,
}: {
  light?: boolean;
  className?: string;
}) {
  const accentFill = light ? '#4F46E5' : '#818CF8';
  const bodyFill = light ? '#11131B' : '#F8FAFC';
  const faceCutout = light ? '#FFFFFF' : '#11131B';

  return (
    <div className={cn('relative flex flex-col items-center justify-center', className)}>
      {/* Playful tiny floating sparks around maylo */}
      <motion.span
        animate={{ y: [-2, -10, -2], opacity: [0.35, 0.95, 0.35], scale: [0.8, 1.15, 0.8] }}
        transition={{ duration: 2.1, repeat: Infinity, ease: 'easeInOut' }}
        className="pointer-events-none absolute -top-2 -right-3 size-2 rounded-full bg-indigo-500 dark:bg-indigo-400"
      />
      <motion.span
        animate={{ y: [2, -7, 2], opacity: [0.25, 0.85, 0.25], scale: [0.7, 1.1, 0.7] }}
        transition={{ duration: 2.5, repeat: Infinity, ease: 'easeInOut', delay: 0.6 }}
        className="pointer-events-none absolute top-3 -left-4 size-1.5 rounded-full bg-sky-500 dark:bg-sky-300"
      />
      <motion.span
        animate={{ y: [0, -6, 0], opacity: [0.3, 0.9, 0.3] }}
        transition={{ duration: 1.9, repeat: Infinity, ease: 'easeInOut', delay: 1.1 }}
        className="pointer-events-none absolute -top-4 left-2 size-1.5 rounded-sm rotate-45 bg-amber-500 dark:bg-amber-300"
      />

      {/* Bouncy floating character with subtle squash & stretch */}
      <motion.div
        animate={{
          y: [0, -10, 0],
          scaleX: [1.03, 0.97, 1.03],
          scaleY: [0.97, 1.04, 0.97],
          rotate: [-2.5, 2.5, -2.5],
        }}
        transition={{
          duration: 1.7,
          repeat: Infinity,
          ease: [0.45, 0.05, 0.55, 0.95],
        }}
        className="relative flex size-20 items-center justify-center"
      >
        <svg viewBox="0 0 32 32" className="size-20 overflow-visible" fill="none" aria-hidden="true">
          {/* Left fluttering ear-fins */}
          <motion.g
            animate={{ rotate: [-7, 9, -7] }}
            transition={{ duration: 0.85, repeat: Infinity, ease: 'easeInOut' }}
            style={{ transformOrigin: '8px 16px' }}
          >
            <rect
              x="2.2"
              y="10.6"
              width="6.2"
              height="3.1"
              rx="1.55"
              transform="rotate(20 5.3 12.15)"
              fill={accentFill}
            />
            <rect
              x="2.2"
              y="17.8"
              width="6.2"
              height="3.1"
              rx="1.55"
              transform="rotate(-20 5.3 19.35)"
              fill={accentFill}
            />
          </motion.g>

          {/* Right fluttering ear-fins (phase-offset) */}
          <motion.g
            animate={{ rotate: [7, -9, 7] }}
            transition={{ duration: 0.85, repeat: Infinity, ease: 'easeInOut' }}
            style={{ transformOrigin: '24px 16px' }}
          >
            <rect
              x="23.6"
              y="10.6"
              width="6.2"
              height="3.1"
              rx="1.55"
              transform="rotate(-20 26.7 12.15)"
              fill={accentFill}
            />
            <rect
              x="23.6"
              y="17.8"
              width="6.2"
              height="3.1"
              rx="1.55"
              transform="rotate(20 26.7 19.35)"
              fill={accentFill}
            />
          </motion.g>

          {/* Main flat geometric character head */}
          <rect x="6.5" y="7.5" width="19" height="17" rx="8.5" fill={bodyFill} />

          {/* Periodic blinking pill eyes */}
          <motion.g
            animate={{ scaleY: [1, 1, 0.12, 1, 1] }}
            transition={{
              duration: 3.0,
              times: [0, 0.86, 0.91, 0.96, 1],
              repeat: Infinity,
              ease: 'easeInOut',
            }}
            style={{ transformOrigin: '16px 15.1px' }}
          >
            <rect x="11.4" y="13.2" width="2.5" height="3.8" rx="1.25" fill={faceCutout} />
            <rect x="18.1" y="13.2" width="2.5" height="3.8" rx="1.25" fill={faceCutout} />
          </motion.g>

          {/* Minimalist happy flat smile */}
          <path
            d="M13.6 19.3C14.3 20.5 17.7 20.5 18.4 19.3"
            stroke={faceCutout}
            strokeWidth="1.9"
            strokeLinecap="round"
          />
        </svg>
      </motion.div>

      {/* Soft ground shadow that shrinks when character bounces upward */}
      <motion.div
        animate={{ scaleX: [1.05, 0.72, 1.05], opacity: [0.24, 0.1, 0.24] }}
        transition={{ duration: 1.7, repeat: Infinity, ease: [0.45, 0.05, 0.55, 0.95] }}
        className={cn(
          'mt-1 h-2 w-12 rounded-full blur-[3px]',
          light ? 'bg-indigo-950' : 'bg-indigo-400'
        )}
      />
    </div>
  );
}

/**
 * Refined Swiss Neo-Grotesque Wordmark ("maylo")
 * Clean lowercase geometric typography + subtle hairline + monospace version index.
 */
export function MayloWordmark({
  size = 'sm',
  showVersion = true,
  light,
  className,
}: {
  size?: 'sm' | 'md' | 'lg';
  showVersion?: boolean;
  light?: boolean;
  className?: string;
}) {
  const textClass =
    size === 'lg'
      ? 'text-[26px] tracking-[-0.035em]'
      : size === 'md'
        ? 'text-[19px] tracking-[-0.03em]'
        : 'text-[15.5px] tracking-[-0.03em]';

  const dividerClass =
    light === true
      ? 'bg-zinc-300'
      : light === false
        ? 'bg-white/15'
        : 'bg-zinc-300 dark:bg-white/15';

  const versionClass =
    light === true
      ? 'text-zinc-400'
      : light === false
        ? 'text-zinc-500'
        : 'text-zinc-400 dark:text-zinc-500';

  return (
    <span className={cn('inline-flex items-center gap-2.5 select-none', className)}>
      <span
        className={cn(
          'font-display font-semibold leading-none text-current',
          textClass
        )}
      >
        maylo
      </span>
      {showVersion && (
        <>
          <span className={cn('h-3 w-px shrink-0', dividerClass)} />
          <span
            className={cn(
              'font-mono-tech text-[10px] font-medium leading-none tracking-[0.06em]',
              versionClass
            )}
          >
            v{__MAYLO_VERSION__}
          </span>
        </>
      )}
    </span>
  );
}
