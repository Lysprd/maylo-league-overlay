import { useLayoutEffect, useRef } from 'react';
import { motion } from 'framer-motion';
import { isOverlayRoute, useAppState } from '@/lib/bridge';
import { consumeThemeTransitionOrigin } from '@/lib/theme-transition';
import { HubView } from '@/views/HubView';
import { OverlayView } from '@/views/OverlayView';
import { AnimatedMayloEmblem, MayloWordmark } from '@/components/ui/brand';

function SplashScreen({ progress, stage, light }: { progress: number; stage: string; light: boolean }) {
  const pct = Math.max(10, Math.min(100, progress || 16));
  return (
    <div
      className={
        light
          ? 'relative flex h-screen w-screen flex-col items-center justify-center overflow-hidden bg-[#F7F8FA] text-zinc-900 select-none'
          : 'relative flex h-screen w-screen flex-col items-center justify-center overflow-hidden bg-[#0B0C10] text-zinc-100 select-none'
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
        initial={{ opacity: 0, y: 10, scale: 0.98 }}
        animate={{ opacity: 1, y: 0, scale: 1 }}
        transition={{ duration: 0.34, ease: [0.22, 1, 0.36, 1] }}
        className="relative flex flex-col items-center"
      >
        <AnimatedMayloEmblem light={light} />

        <div className="mt-4">
          <MayloWordmark size="lg" light={light} />
        </div>

        <div className={light ? 'mt-1.5 font-mono-tech text-[10px] tracking-[0.14em] text-zinc-400' : 'mt-1.5 font-mono-tech text-[10px] tracking-[0.14em] text-zinc-500'}>
          league &amp; mayhem companion
        </div>

        {/* Animated Shimmer Progress Bar */}
        <div className="mt-7 w-[272px]">
          <div className={light ? 'relative h-1.5 w-full overflow-hidden rounded-full bg-zinc-200/90' : 'relative h-1.5 w-full overflow-hidden rounded-full bg-white/[0.08]'}>
            <div
              className="relative h-full overflow-hidden rounded-full bg-gradient-to-r from-indigo-600 via-indigo-500 to-indigo-400 transition-all duration-500 ease-out"
              style={{ width: `${pct}%` }}
            >
              <div className="splash-shimmer absolute inset-y-0 w-1/2 bg-gradient-to-r from-transparent via-white/45 to-transparent" />
            </div>
          </div>
          <div className="mt-2.5 flex items-center justify-between text-[11px]">
            <span className={light ? 'text-zinc-500 truncate' : 'text-zinc-400 truncate'}>
              {stage || 'Loading…'}
            </span>
            <span className={light ? 'font-mono-tech font-semibold text-indigo-600 shrink-0 ml-3' : 'font-mono-tech font-semibold text-indigo-300 shrink-0 ml-3'}>
              {pct}%
            </span>
          </div>
        </div>
      </motion.div>
    </div>
  );
}

export function App() {
  const overlay = isOverlayRoute();
  const state = useAppState();
  const urlTheme = new URLSearchParams(window.location.search).get('theme');
  const isLight = state ? state.settings.theme === 'light' : urlTheme === 'light';
  const prevLightRef = useRef<boolean | null>(null);

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

  if (!state) {
    if (overlay) return null;
    return <SplashScreen progress={16} stage="Loading…" light={isLight} />;
  }

  if (!overlay && !state.bootReady) {
    return <SplashScreen progress={state.bootProgress} stage={state.bootStage} light={isLight} />;
  }

  return overlay ? <OverlayView state={state} /> : <HubView state={state} />;
}

export default App;
