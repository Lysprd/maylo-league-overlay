// Editorial Swiss Geometric Vector Illustrations (Dark & Light harmonized)
import { motion } from 'framer-motion';
import { cn } from '@/lib/utils';

/**
 * Subtle architectural radar & telemetry backdrop for the Overview Hero Banner.
 */
export function SwissRadarBackdrop({ className }: { className?: string }) {
  return (
    <svg
      viewBox="0 0 420 220"
      fill="none"
      aria-hidden="true"
      className={cn('pointer-events-none select-none', className)}
    >
      <defs>
        <radialGradient id="swissRadarGlow" cx="72%" cy="46%" r="55%">
          <stop offset="0%" stopColor="#6366F1" stopOpacity="0.22" />
          <stop offset="65%" stopColor="#818CF8" stopOpacity="0.05" />
          <stop offset="100%" stopColor="#6366F1" stopOpacity="0" />
        </radialGradient>
      </defs>

      <rect width="420" height="220" fill="url(#swissRadarGlow)" />

      {/* Concentric Swiss geometric rings */}
      <g className="stroke-indigo-500/20 dark:stroke-indigo-300/15" strokeWidth="1">
        <circle cx="305" cy="108" r="34" />
        <circle cx="305" cy="108" r="68" strokeDasharray="3 5" />
        <circle cx="305" cy="108" r="102" />
        <circle cx="305" cy="108" r="138" strokeDasharray="2 6" />
      </g>

      {/* Precision crosshairs */}
      <g className="stroke-zinc-400/20 dark:stroke-white/10" strokeWidth="1">
        <line x1="150" y1="108" x2="420" y2="108" />
        <line x1="305" y1="0" x2="305" y2="220" />
        <line x1="215" y1="18" x2="395" y2="198" strokeDasharray="2 6" />
      </g>

      {/* Telemetry data nodes */}
      <g>
        <circle cx="256" cy="64" r="3.5" className="fill-indigo-500/75 dark:fill-indigo-400/80" />
        <circle cx="354" cy="76" r="3" className="fill-teal-500/80 dark:fill-teal-400/85" />
        <circle cx="332" cy="154" r="3.5" className="fill-amber-500/80 dark:fill-amber-300/85" />
        <path
          d="M256 64L305 108L354 76M305 108L332 154"
          className="stroke-indigo-500/35 dark:stroke-indigo-300/30"
          strokeWidth="1.2"
        />
      </g>

      {/* Corner registration marks */}
      <g className="stroke-zinc-400/35 dark:stroke-white/20" strokeWidth="1.2">
        <path d="M394 16H404V26" />
        <path d="M394 204H404V194" />
      </g>
    </svg>
  );
}

/**
 * Animated Swiss Rift Map & Live Telemetry illustration for Live Game Waiting State.
 */
export function SwissRiftLiveIllustration({ className }: { className?: string }) {
  return (
    <div className={cn('relative mx-auto flex size-36 items-center justify-center', className)}>
      <svg viewBox="0 0 160 160" fill="none" className="size-full">
        {/* Outer architectural frame */}
        <rect
          x="16"
          y="16"
          width="128"
          height="128"
          rx="22"
          className="fill-zinc-100/90 dark:fill-white/[0.03] stroke-zinc-300/90 dark:stroke-white/[0.12]"
          strokeWidth="1.25"
        />
        {/* Rift lanes */}
        <path
          d="M34 126V42C34 37.6 37.6 34 42 34H126"
          className="stroke-zinc-300 dark:stroke-white/15"
          strokeWidth="2.4"
          strokeLinecap="round"
        />
        <path
          d="M34 126H118C122.4 126 126 122.4 126 118V34"
          className="stroke-zinc-300 dark:stroke-white/15"
          strokeWidth="2.4"
          strokeLinecap="round"
        />
        <path
          d="M36 124L124 36"
          className="stroke-indigo-500/45 dark:stroke-indigo-400/45"
          strokeWidth="2"
          strokeDasharray="4 4"
        />
        {/* River diagonal */}
        <path
          d="M44 44L116 116"
          className="stroke-sky-500/35 dark:stroke-sky-400/30"
          strokeWidth="1.8"
          strokeDasharray="2 5"
        />

        {/* Dragon & Baron pits */}
        <circle cx="101" cy="101" r="6" className="fill-amber-500/20 stroke-amber-500 dark:stroke-amber-300" strokeWidth="1.5" />
        <circle cx="59" cy="59" r="6" className="fill-purple-500/20 stroke-purple-500 dark:stroke-purple-300" strokeWidth="1.5" />

        {/* Nexus beacons */}
        <circle cx="36" cy="124" r="5" className="fill-indigo-500" />
        <circle cx="124" cy="36" r="5" className="fill-rose-500" />

        {/* Pulsing center radar */}
        <motion.circle
          cx="80"
          cy="80"
          r="14"
          className="stroke-indigo-500/60 dark:stroke-indigo-400/70"
          strokeWidth="1.5"
          initial={{ scale: 0.75, opacity: 0.85 }}
          animate={{ scale: 1.9, opacity: 0 }}
          transition={{ duration: 2.2, repeat: Infinity, ease: 'easeOut' }}
          style={{ transformOrigin: '80px 80px' }}
        />
        <circle cx="80" cy="80" r="6" className="fill-indigo-500/20 stroke-indigo-500 dark:stroke-indigo-300" strokeWidth="1.8" />
        <circle cx="80" cy="80" r="2.2" className="fill-indigo-500 dark:fill-indigo-300" />
      </svg>
    </div>
  );
}

/**
 * Clean geometric illustration for Empty Champion / Augments / Notes / Synergies states.
 */
export function SwissDraftEmptyIllustration({
  variant = 'champions',
  className,
}: {
  variant?: 'champions' | 'augments' | 'notes' | 'synergies';
  className?: string;
}) {
  return (
    <div className={cn('relative mx-auto flex size-28 items-center justify-center', className)}>
      <svg viewBox="0 0 128 128" fill="none" className="size-full">
        {/* Back tilted card */}
        <rect
          x="18"
          y="28"
          width="54"
          height="68"
          rx="12"
          transform="rotate(-9 18 28)"
          className="fill-zinc-100 dark:fill-white/[0.03] stroke-zinc-300 dark:stroke-white/[0.1]"
          strokeWidth="1.25"
        />
        {/* Right tilted card */}
        <rect
          x="58"
          y="22"
          width="54"
          height="68"
          rx="12"
          transform="rotate(9 58 22)"
          className="fill-zinc-100 dark:fill-white/[0.03] stroke-zinc-300 dark:stroke-white/[0.1]"
          strokeWidth="1.25"
        />
        {/* Front hero card */}
        <rect
          x="36"
          y="22"
          width="56"
          height="74"
          rx="13"
          className="fill-white dark:fill-[#171925] stroke-indigo-400/55 dark:stroke-indigo-400/45"
          strokeWidth="1.5"
        />

        {variant === 'augments' ? (
          <>
            <polygon
              points="64,37 74,52 64,67 54,52"
              className="fill-amber-500/20 stroke-amber-500 dark:stroke-amber-300"
              strokeWidth="1.6"
            />
            <circle cx="64" cy="52" r="3" className="fill-amber-500 dark:fill-amber-300" />
            <rect x="48" y="75" width="32" height="4" rx="2" className="fill-zinc-300 dark:fill-white/20" />
            <rect x="54" y="83" width="20" height="3" rx="1.5" className="fill-indigo-400/55" />
          </>
        ) : variant === 'notes' ? (
          <>
            <rect x="46" y="36" width="24" height="4.5" rx="2" className="fill-indigo-500/75" />
            <rect x="46" y="48" width="36" height="3.5" rx="1.75" className="fill-zinc-300 dark:fill-white/20" />
            <rect x="46" y="57" width="30" height="3.5" rx="1.75" className="fill-zinc-200 dark:fill-white/15" />
            <rect x="46" y="66" width="34" height="3.5" rx="1.75" className="fill-zinc-200 dark:fill-white/15" />
            <rect x="46" y="75" width="20" height="3.5" rx="1.75" className="fill-indigo-400/45" />
          </>
        ) : variant === 'synergies' ? (
          <>
            <circle cx="54" cy="50" r="8" className="fill-indigo-500/20 stroke-indigo-500 dark:stroke-indigo-300" strokeWidth="1.5" />
            <circle cx="74" cy="50" r="8" className="fill-teal-500/20 stroke-teal-500 dark:stroke-teal-300" strokeWidth="1.5" />
            <path d="M58 64Q64 72 70 64" className="stroke-indigo-500/70" strokeWidth="1.8" strokeLinecap="round" />
            <rect x="48" y="78" width="32" height="3.5" rx="1.75" className="fill-zinc-300 dark:fill-white/20" />
          </>
        ) : (
          <>
            <circle cx="64" cy="49" r="12" className="fill-indigo-500/15 stroke-indigo-500 dark:stroke-indigo-300" strokeWidth="1.6" />
            <path d="M59 49L63 53L70 45" className="stroke-indigo-500 dark:stroke-indigo-300" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
            <rect x="48" y="71" width="32" height="4" rx="2" className="fill-zinc-300 dark:fill-white/20" />
            <rect x="53" y="80" width="22" height="3" rx="1.5" className="fill-indigo-400/55" />
          </>
        )}
      </svg>
    </div>
  );
}
