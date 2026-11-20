import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { AnimatePresence, motion } from 'framer-motion';
import { isOverlayRoute, useAppState } from '@/lib/bridge';
import { consumeThemeTransitionOrigin } from '@/lib/theme-transition';
import { HubView } from '@/views/HubView';
import { OverlayView } from '@/views/OverlayView';
import { AnimatedMayloEmblem, MayloWordmark } from '@/components/ui/brand';

const BOOT_STEPS = [
  { key: 'engine', label: 'Engine', threshold: 18 },
  { key: 'meta', label: 'Meta', threshold: 45 },
  { key: 'client', label: 'Client', threshold: 72 },
  { key: 'ready', label: 'Ready', threshold: 96 },
] as const;

function stageCaptionFor(displayPct: number, rawStage: string, targetPct: number): string {
  if (displayPct >= 98) return 'Ready';
  if (targetPct - displayPct > 18) {
    if (displayPct < 30) return 'Initializing workspace…';
    if (displayPct < 58) return 'Loading Riot Data Dragon & Meta…';
    if (displayPct < 82) return 'Connecting League of Legends client…';
    return 'Syncing Tier List & Patch Notes…';
  }
  return rawStage || 'Initializing workspace…';
}

function SplashScreen({
  displayPct,
  stage,
  light,
}: {
  displayPct: number;
  stage: string;
  light: boolean;
}) {
  const pct = Math.max(0, Math.min(100, Math.round(displayPct)));
  return (
    <motion.div
      key="maylo-splash"
      initial={{ opacity: 1 }}
      exit={{ opacity: 0, scale: 1.015 }}
      transition={{ duration: 0.24, ease: [0.22, 1, 0.36, 1] }}
      className={
        light
          ? 'fixed inset-0 z-50 flex h-screen w-screen flex-col items-center justify-center overflow-hidden bg-[#F7F8FA] text-zinc-900 select-none'
          : 'fixed inset-0 z-50 flex h-screen w-screen flex-col items-center justify-center overflow-hidden bg-[#0B0C10] text-zinc-100 select-none'
      }
    >
      {/* Subtle Swiss architectural dot-grid */}
      <div
        className="pointer-events-none absolute inset-0 swiss-grid-bg opacity-65"
        style={{
          maskImage: 'radial-gradient(ellipse 56% 52% at 50% 50%, black 20%, transparent 100%)',
          WebkitMaskImage: 'radial-gradient(ellipse 56% 52% at 50% 50%, black 20%, transparent 100%)',
        }}
      />

      {/* Calm breathing ambient glow */}
      <div
        className="pointer-events-none absolute size-[440px] rounded-full blur-[120px] animate-pulse"
        style={{
          background: light
            ? 'radial-gradient(circle, rgba(99,102,241,0.16) 0%, rgba(129,140,248,0.06) 55%, transparent 72%)'
            : 'radial-gradient(circle, rgba(99,102,241,0.28) 0%, rgba(129,140,248,0.09) 55%, transparent 72%)',
        }}
      />

      {/* Centered Animated Flat Companion & Lowercase Swiss Wordmark */}
      <motion.div
        initial={{ opacity: 0, y: 8, scale: 0.985 }}
        animate={{ opacity: 1, y: 0, scale: 1 }}
        transition={{ duration: 0.3, ease: [0.22, 1, 0.36, 1] }}
        className="relative flex flex-col items-center"
      >
        <AnimatedMayloEmblem light={light} />

        <div className="mt-4">
          <MayloWordmark size="lg" light={light} />
        </div>

        <div
          className={
            light
              ? 'mt-1.5 font-mono-tech text-[10px] tracking-[0.14em] text-zinc-400'
              : 'mt-1.5 font-mono-tech text-[10px] tracking-[0.14em] text-zinc-500'
          }
        >
          league &amp; mayhem companion
        </div>

        {/* Smooth Interpolated Progress Bar */}
        <div className="mt-7 w-[288px]">
          <div
            className={
              light
                ? 'relative h-1.5 w-full overflow-hidden rounded-full bg-zinc-200/90'
                : 'relative h-1.5 w-full overflow-hidden rounded-full bg-white/[0.08]'
            }
          >
            <div
              className="relative h-full overflow-hidden rounded-full bg-gradient-to-r from-indigo-600 via-indigo-500 to-indigo-400"
              style={{ width: `${Math.max(2, displayPct)}%` }}
            >
              <div className="splash-shimmer absolute inset-y-0 w-1/2 bg-gradient-to-r from-transparent via-white/45 to-transparent" />
            </div>
          </div>

          <div className="mt-2.5 flex items-center justify-between text-[11px]">
            <span className={light ? 'text-zinc-500 truncate' : 'text-zinc-400 truncate'}>
              {stage}
            </span>
            <span
              className={
                light
                  ? 'font-mono-tech font-semibold tabular-nums text-indigo-600 shrink-0 ml-3'
                  : 'font-mono-tech font-semibold tabular-nums text-indigo-300 shrink-0 ml-3'
              }
            >
              {pct}%
            </span>
          </div>

          {/* 4-Step Swiss Sequence Indicator */}
          <div className="mt-4 flex items-center justify-between gap-2">
            {BOOT_STEPS.map((step) => {
              const done = displayPct >= step.threshold;
              return (
                <div key={step.key} className="flex items-center gap-1.5">
                  <span
                    className={
                      done
                        ? 'size-1.5 rounded-full bg-indigo-500 transition-colors duration-200'
                        : light
                          ? 'size-1.5 rounded-full bg-zinc-300 transition-colors duration-200'
                          : 'size-1.5 rounded-full bg-white/15 transition-colors duration-200'
                    }
                  />
                  <span
                    className={
                      done
                        ? light
                          ? 'font-mono-tech text-[9px] font-semibold uppercase tracking-wider text-zinc-700 transition-colors duration-200'
                          : 'font-mono-tech text-[9px] font-semibold uppercase tracking-wider text-zinc-200 transition-colors duration-200'
                        : light
                          ? 'font-mono-tech text-[9px] font-medium uppercase tracking-wider text-zinc-400 transition-colors duration-200'
                          : 'font-mono-tech text-[9px] font-medium uppercase tracking-wider text-zinc-600 transition-colors duration-200'
                    }
                  >
                    {step.label}
                  </span>
                </div>
              );
            })}
          </div>
        </div>
      </motion.div>
    </motion.div>
  );
}

export function App() {
  const overlay = isOverlayRoute();
  const state = useAppState();
  const urlTheme = new URLSearchParams(window.location.search).get('theme');
  const isLight = state ? state.settings.theme === 'light' : urlTheme === 'light';
  const prevLightRef = useRef<boolean | null>(null);

  // Smoothly interpolate splash progress from 0% -> target so the bar never snaps straight to 70%
  const bootReady = Boolean(state?.bootReady);
  const targetPct = !state ? 18 : bootReady ? 100 : Math.max(8, Math.min(96, state.bootProgress || 12));
  const [displayPct, setDisplayPct] = useState(0);
  const [splashDismissed, setSplashDismissed] = useState(false);
  const targetRef = useRef(targetPct);
  targetRef.current = targetPct;
  const bootReadyRef = useRef(bootReady);
  bootReadyRef.current = bootReady;

  // If bootReady flips back to false (e.g. diagnostics / reload), re-arm the splash cleanly
  useEffect(() => {
    if (!bootReady && splashDismissed) {
      setSplashDismissed(false);
      setDisplayPct(0);
    }
  }, [bootReady, splashDismissed]);

  useEffect(() => {
    if (overlay || splashDismissed) return;
    let rafId = 0;
    let lastTs = performance.now();
    let holdTimer: number | null = null;

    const tick = (now: number) => {
      const dt = Math.min(0.05, Math.max(0.004, (now - lastTs) / 1000));
      lastTs = now;
      let shouldContinue = true;

      setDisplayPct((prev) => {
        const tgt = targetRef.current;
        if (tgt <= prev) {
          if (bootReadyRef.current && prev >= 99.5) {
            shouldContinue = false;
          }
          return prev;
        }
        const diff = tgt - prev;
        // Capped smooth glide: always sweeps gracefully from 0 -> 100 in ~450-600ms without ever jumping straight to 70%
        const speed = bootReadyRef.current ? 195 : 135;
        const step = Math.min(diff, Math.max(18 * dt, Math.min(speed * dt, diff * 6.5 * dt)));
        const next = prev + step;
        if (bootReadyRef.current && next >= 99.6) {
          shouldContinue = false;
          return 100;
        }
        return next;
      });

      if (shouldContinue) {
        rafId = requestAnimationFrame(tick);
      } else if (holdTimer === null) {
        holdTimer = window.setTimeout(() => setSplashDismissed(true), 120);
      }
    };

    rafId = requestAnimationFrame(tick);
    return () => {
      cancelAnimationFrame(rafId);
      if (holdTimer !== null) window.clearTimeout(holdTimer);
    };
  }, [overlay, splashDismissed]);

  useLayoutEffect(() => {
    if (overlay) {
      document.documentElement.classList.add('overlay-mode', 'dark');
      document.documentElement.style.background = 'transparent';
      return;
    }
    document.documentElement.classList.remove('overlay-mode');
    const changed = state !== null && prevLightRef.current !== null && prevLightRef.current !== isLight;
    if (state !== null) prevLightRef.current = isLight;

    const applyTheme = () => {
      document.documentElement.classList.toggle('dark', !isLight);
      document.documentElement.style.background = isLight ? '#F7F8FA' : '#0B0C10';
    };

    const reducedMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
    const docAny = document as Document & {
      startViewTransition?: (cb: () => void) => { ready: Promise<void> };
    };

    if (changed && !reducedMotion && typeof docAny.startViewTransition === 'function') {
      const { x, y } = consumeThemeTransitionOrigin();
      const maxRadius = Math.hypot(
        Math.max(x, window.innerWidth - x),
        Math.max(y, window.innerHeight - y)
      );
      const transition = docAny.startViewTransition(() => {
        applyTheme();
      });
      transition.ready
        .then(() => {
          document.documentElement.animate(
            {
              clipPath: [
                `circle(0px at ${x}px ${y}px)`,
                `circle(${maxRadius}px at ${x}px ${y}px)`,
              ],
            },
            {
              duration: 420,
              easing: 'cubic-bezier(0.22, 1, 0.36, 1)',
              pseudoElement: '::view-transition-new(root)',
            }
          );
        })
        .catch(() => {
          // Fallback already applied
        });
    } else {
      applyTheme();
    }
  }, [overlay, isLight, state !== null]);

  if (overlay) {
    return state ? <OverlayView state={state} /> : null;
  }

  const showSplash = !state || !bootReady || !splashDismissed;
  const caption = stageCaptionFor(displayPct, state?.bootStage || '', targetPct);

  return (
    <>
      {state && bootReady && <HubView state={state} />}
      <AnimatePresence>
        {showSplash && (
          <SplashScreen
            displayPct={displayPct}
            stage={caption}
            light={isLight}
          />
        )}
      </AnimatePresence>
    </>
  );
}

export default App;
