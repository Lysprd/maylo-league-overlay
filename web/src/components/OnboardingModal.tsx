import { useEffect, useRef, useState } from 'react';
import { motion } from 'framer-motion';
import {
  ArrowLeft,
  ArrowRight,
  Check,
  Flame,
  Keyboard,
  Layers,
  Moon,
  Sparkles,
  Sun,
  Wand2,
  X,
} from 'lucide-react';
import type { AppSettings } from '@/types';
import { postAction } from '@/lib/bridge';
import { setThemeTransitionOrigin } from '@/lib/theme-transition';
import { cn } from '@/lib/utils';
import { Button, Keycap, TierBadge } from '@/components/ui/primitives';
import { MayloEmblem, MayloWordmark } from '@/components/ui/brand';
import { BorderBeam, SWISS_EASE } from '@/components/ui/motion-primitives';

interface OnboardingModalProps {
  settings: AppSettings;
  onFinish: () => void;
}

const SLIDE_META = [
  { code: '01', tag: 'WELCOME' },
  { code: '02', tag: 'AUTO-LOADOUT' },
  { code: '03', tag: 'IN-GAME HUD' },
  { code: '04', tag: 'QUICK SETUP' },
] as const;

const TOTAL_SLIDES = SLIDE_META.length;

export function OnboardingModal({ settings, onFinish }: OnboardingModalProps) {
  const [step, setStep] = useState(0);
  const isLight = settings.theme === 'light';
  const onFinishRef = useRef(onFinish);
  onFinishRef.current = onFinish;

  const next = () => {
    if (step < TOTAL_SLIDES - 1) setStep((s) => s + 1);
    else onFinishRef.current();
  };

  const prev = () => {
    if (step > 0) setStep((s) => s - 1);
  };

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const tag = (document.activeElement?.tagName || '').toUpperCase();
      if (e.key === 'ArrowRight') {
        e.preventDefault();
        setStep((s) => {
          if (s < TOTAL_SLIDES - 1) return s + 1;
          onFinishRef.current();
          return s;
        });
      } else if (e.key === 'Enter' && tag !== 'BUTTON' && tag !== 'INPUT' && tag !== 'TEXTAREA' && tag !== 'SELECT') {
        e.preventDefault();
        setStep((s) => {
          if (s < TOTAL_SLIDES - 1) return s + 1;
          onFinishRef.current();
          return s;
        });
      } else if (e.key === 'ArrowLeft') {
        e.preventDefault();
        setStep((s) => Math.max(0, s - 1));
      } else if (e.key === 'Escape') {
        e.preventDefault();
        onFinishRef.current();
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  const meta = SLIDE_META[step];

  return (
    <motion.div
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.22 }}
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-md p-5 select-none"
      onClick={() => onFinishRef.current()}
    >
      <motion.div
        initial={{ opacity: 0, scale: 0.96, y: 14 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        exit={{ opacity: 0, scale: 0.97, y: 8 }}
        transition={{ duration: 0.32, ease: SWISS_EASE }}
        onClick={(e) => e.stopPropagation()}
        className={cn(
          'relative w-full max-w-[560px] overflow-hidden rounded-2xl border shadow-2xl transition-colors',
          isLight
            ? 'border-zinc-200/90 bg-white text-zinc-900 shadow-[0_28px_80px_rgba(15,23,42,0.2)]'
            : 'border-white/[0.1] bg-[#11131B] text-zinc-100 shadow-[0_32px_90px_rgba(0,0,0,0.78)]'
        )}
      >
        <BorderBeam tone="indigo" />

        {/* Swiss Numbered Header Bar */}
        <div className="flex items-center justify-between px-6 pt-5 pb-2">
          <div className="flex items-center gap-2.5">
            <span className="font-mono-tech inline-flex items-center gap-1.5 rounded-md border border-indigo-400/30 bg-indigo-500/12 px-2 py-0.5 text-[10px] font-semibold tracking-[0.12em] text-indigo-600 dark:text-indigo-300">
              <span>{meta.code}</span>
              <span className="opacity-40">/</span>
              <span>{meta.tag}</span>
            </span>
          </div>
          <button
            type="button"
            onClick={() => onFinishRef.current()}
            className="inline-flex items-center gap-1 rounded-lg px-2.5 py-1 text-xs font-medium text-zinc-500 hover:bg-zinc-100 hover:text-zinc-900 dark:text-zinc-400 dark:hover:bg-white/[0.06] dark:hover:text-white transition-colors cursor-pointer"
          >
            Skip
            <X className="size-3.5" />
          </button>
        </div>

        {/* Horizontal Spring Slider Track */}
        <div className="overflow-hidden">
          <motion.div
            animate={{ x: `-${step * 100}%` }}
            transition={{ type: 'spring', stiffness: 320, damping: 34, mass: 0.85 }}
            className="flex"
          >
            {/* SLIDE 1: WELCOME & AUTO-DETECT */}
            <div className="w-full shrink-0 px-6 pt-3 pb-6">
              <div className="relative flex h-[212px] flex-col items-center justify-center overflow-hidden rounded-xl border border-zinc-200/80 dark:border-white/[0.07] bg-zinc-50/60 dark:bg-[#0D0F16] swiss-grid-bg">
                <div
                  className={cn(
                    'relative flex size-16 items-center justify-center rounded-2xl border shadow-lg',
                    isLight
                      ? 'border-zinc-200 bg-white text-zinc-900'
                      : 'border-white/14 bg-[#161925] text-white'
                  )}
                >
                  <MayloEmblem className="size-9" light={isLight} />
                </div>

                <div className="relative mt-3.5">
                  <MayloWordmark size="md" showVersion light={isLight} />
                </div>

                <div className="relative mt-4 flex flex-wrap items-center justify-center gap-2">
                  {[
                    { label: 'Ranked SR', tone: 'border-indigo-400/35 bg-indigo-500/12 text-indigo-600 dark:text-indigo-300' },
                    { label: 'ARAM', tone: 'border-sky-400/35 bg-sky-500/12 text-sky-600 dark:text-sky-300' },
                    { label: 'ARAM Mayhem', tone: 'border-amber-400/35 bg-amber-500/12 text-amber-700 dark:text-amber-300' },
                  ].map((m) => (
                    <span
                      key={m.label}
                      className={cn(
                        'inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-[11px] font-semibold backdrop-blur-sm',
                        m.tone
                      )}
                    >
                      <span className="size-1.5 rounded-full bg-current" />
                      {m.label}
                    </span>
                  ))}
                </div>
              </div>

              <div className="mt-5 text-center px-3">
                <h2 className="font-display text-[19px] font-bold tracking-tight text-zinc-900 dark:text-white">
                  Built for Instant Clarity
                </h2>
                <p className="mt-1.5 text-[13px] leading-relaxed text-zinc-500 dark:text-zinc-400">
                  Launch League and play normally. maylo detects your game mode, role, and champion automatically the second you enter Champ Select.
                </p>
              </div>
            </div>

            {/* SLIDE 2: AUTO-RUNES, SPELLS & BUILDS */}
            <div className="w-full shrink-0 px-6 pt-3 pb-6">
              <div className="relative flex h-[212px] flex-col items-center justify-center gap-3.5 overflow-hidden rounded-xl border border-zinc-200/80 dark:border-white/[0.07] bg-zinc-50/70 dark:bg-[#0D0F16] px-5 swiss-grid-bg">
                <div className="relative flex flex-wrap items-center justify-center gap-2">
                  {[
                    { label: 'Auto-Runes', icon: Wand2 },
                    { label: 'Auto-Shop', icon: Layers },
                    { label: 'Auto-Spells', icon: Flame },
                  ].map((item) => {
                    const Icon = item.icon;
                    return (
                      <div
                        key={item.label}
                        className="inline-flex items-center gap-1.5 rounded-lg border border-emerald-500/35 bg-emerald-500/10 px-3 py-1.5 text-xs font-semibold text-emerald-700 dark:text-emerald-300 backdrop-blur-sm"
                      >
                        <Icon className="size-3.5" />
                        <span>{item.label}</span>
                        <Check className="size-3.5" />
                      </div>
                    );
                  })}
                </div>

                {/* Architectural connector arrow */}
                <div className="h-3 w-px bg-gradient-to-b from-emerald-400/60 to-indigo-500/60" />

                <div className="relative w-full max-w-[400px] rounded-xl border border-zinc-200/90 dark:border-white/[0.09] bg-white dark:bg-[#161924] px-4 py-3 shadow-sm">
                  <div className="flex items-center justify-between font-mono-tech text-[9.5px] font-semibold uppercase tracking-wider text-zinc-400">
                    <span>ADAPTIVE BUILD PATH</span>
                    <span className="text-indigo-500 dark:text-indigo-300">LIVE SYNC</span>
                  </div>
                  <div className="mt-2.5 flex items-center justify-between gap-2">
                    {['Starter', 'Core 1', 'Boots', 'Core 2', 'Core 3'].map((slot, i) => (
                      <div key={slot} className="flex flex-col items-center gap-1 flex-1">
                        <div
                          className={cn(
                            'font-mono-tech flex h-8 w-full items-center justify-center rounded-lg border text-[10px] font-bold',
                            i === 0
                              ? 'border-amber-400/50 bg-amber-500/12 text-amber-700 dark:text-amber-300'
                              : i === 1
                                ? 'border-indigo-400/50 bg-indigo-500/12 text-indigo-700 dark:text-indigo-300'
                                : 'border-zinc-200 dark:border-white/10 bg-zinc-50 dark:bg-white/[0.04] text-zinc-500 dark:text-zinc-400'
                          )}
                        >
                          {i === 0 ? 'BUY' : `0${i}`}
                        </div>
                        <span className="text-[9.5px] font-medium text-zinc-500 dark:text-zinc-400 truncate">
                          {slot}
                        </span>
                      </div>
                    ))}
                  </div>
                </div>
              </div>

              <div className="mt-5 text-center px-3">
                <h2 className="font-display text-[19px] font-bold tracking-tight text-zinc-900 dark:text-white">
                  Zero-Effort Runes &amp; Builds
                </h2>
                <p className="mt-1.5 text-[13px] leading-relaxed text-zinc-500 dark:text-zinc-400">
                  Runes, summoner spells, and item sets sync automatically. Explore any champion anytime inside the unified <strong className="font-semibold text-zinc-700 dark:text-zinc-200">Champions</strong> tab.
                </p>
              </div>
            </div>

            {/* SLIDE 3: LIVE IN-GAME OVERLAY & AUGMENTS */}
            <div className="w-full shrink-0 px-6 pt-3 pb-6">
              <div className="relative flex h-[212px] flex-col items-center justify-center gap-3 overflow-hidden rounded-xl border border-zinc-200/80 dark:border-white/[0.08] bg-[#0B0D14] px-5 text-zinc-100 swiss-grid-bg">
                {/* Top-center mini perf strip */}
                <div className="font-mono-tech flex items-center gap-2 rounded-full border border-white/12 bg-white/[0.06] px-3.5 py-1 text-[10px] font-semibold text-zinc-200 backdrop-blur-md">
                  <span className="text-emerald-300">7.8 CS/m</span>
                  <span className="text-zinc-600">·</span>
                  <span>8/1/6</span>
                  <span className="text-zinc-600">·</span>
                  <span className="text-amber-300">DRG 1:42</span>
                </div>

                <div className="flex w-full max-w-[404px] items-stretch justify-between gap-2.5">
                  {/* Mini Build HUD Preview */}
                  <div className="flex-1 rounded-xl border border-white/12 bg-[#141722]/95 p-3 shadow-lg">
                    <div className="flex items-center justify-between font-mono-tech text-[9px] font-bold uppercase tracking-wider text-zinc-400">
                      <span className="text-amber-300">STARTER · NEXT</span>
                      <span className="rounded bg-emerald-500/20 px-1.5 py-0.5 text-emerald-300">BUY</span>
                    </div>
                    <div className="mt-1.5 font-display text-xs font-semibold text-white">Compact Build HUD</div>
                    <div className="mt-1 flex items-center gap-1">
                      {[1, 2, 3, 4].map((n) => (
                        <span key={n} className="size-4 rounded border border-white/12 bg-white/[0.06]" />
                      ))}
                      <span className="ml-auto font-mono-tech text-[9px] text-zinc-400">350g</span>
                    </div>
                  </div>

                  {/* Mini Augment Badge Preview */}
                  <div className="flex flex-col items-center justify-center rounded-xl border border-amber-400/45 bg-[#1B1810] px-4 py-3 shadow-lg">
                    <div className="flex items-center gap-1.5">
                      <Sparkles className="size-3.5 text-amber-300" />
                      <TierBadge tier="S" size="xs" dark />
                    </div>
                    <div className="mt-1.5 font-mono-tech text-[11px] font-bold text-amber-200">+4.8% WR</div>
                    <div className="font-mono-tech text-[8.5px] uppercase tracking-wider text-zinc-400">Mayhem OCR</div>
                  </div>
                </div>
              </div>

              <div className="mt-5 text-center px-3">
                <h2 className="font-display text-[19px] font-bold tracking-tight text-zinc-900 dark:text-white">
                  Precision In-Game Overlay
                </h2>
                <p className="mt-1.5 text-[13px] leading-relaxed text-zinc-500 dark:text-zinc-400">
                  Play League in <strong className="font-semibold text-zinc-700 dark:text-zinc-200">Borderless</strong> mode for the compact Build HUD, live minimap objective timers, and instant ARAM Mayhem augment badges.
                </p>
              </div>
            </div>

            {/* SLIDE 4: SHORTCUTS & QUICK SETUP */}
            <div className="w-full shrink-0 px-6 pt-3 pb-6">
              <div className="flex h-[212px] flex-col justify-between rounded-xl border border-zinc-200/80 dark:border-white/[0.07] bg-zinc-50/70 dark:bg-[#0D0F16] p-4 swiss-grid-bg">
                <div className="space-y-2">
                  {[
                    { keys: 'Ctrl+Tab', label: 'Hold to drag overlay widgets (Tab stays free)' },
                    { keys: `Alt+${settings.hotkeySheet || 'G'}`, label: 'Open full Command Overlay in-game' },
                    { keys: `Alt+${settings.hotkeyEdit || 'O'}`, label: 'Edit positions & resize minimap live' },
                  ].map((item) => (
                    <div
                      key={item.keys}
                      className="flex items-center justify-between gap-3 rounded-lg border border-zinc-200/80 dark:border-white/[0.07] bg-white/95 dark:bg-[#161924]/95 px-3 py-2"
                    >
                      <span className="flex items-center gap-2 text-xs text-zinc-600 dark:text-zinc-300 truncate">
                        <Keyboard className="size-3.5 text-indigo-500 dark:text-indigo-400 shrink-0" />
                        <span className="truncate">{item.label}</span>
                      </span>
                      <Keycap keys={item.keys} />
                    </div>
                  ))}
                </div>

                {/* Quick Preferences: Flash Key + Theme */}
                <div className="flex items-center justify-between gap-3 pt-1">
                  <div className="flex items-center gap-2">
                    <span className="font-mono-tech text-[10px] font-semibold uppercase tracking-wider text-zinc-500 dark:text-zinc-400">
                      Flash
                    </span>
                    <div className="inline-flex rounded-lg border border-zinc-200 dark:border-white/10 bg-zinc-100 dark:bg-white/[0.04] p-0.5">
                      {(['D', 'F'] as const).map((k) => (
                        <button
                          key={k}
                          type="button"
                          onClick={() => postAction('setFlashKey', { key: k })}
                          className={cn(
                            'font-mono-tech rounded-md px-2.5 py-0.5 text-[11px] font-bold transition-colors cursor-pointer',
                            (settings.flashKey || 'F') === k
                              ? 'bg-indigo-600 text-white'
                              : 'text-zinc-500 dark:text-zinc-400 hover:text-zinc-900 dark:hover:text-white'
                          )}
                        >
                          {k}
                        </button>
                      ))}
                    </div>
                  </div>

                  <div className="flex items-center gap-2">
                    <span className="font-mono-tech text-[10px] font-semibold uppercase tracking-wider text-zinc-500 dark:text-zinc-400">
                      Theme
                    </span>
                    <div className="inline-flex rounded-lg border border-zinc-200 dark:border-white/10 bg-zinc-100 dark:bg-white/[0.04] p-0.5">
                      <button
                        type="button"
                        onClick={(e) => {
                          setThemeTransitionOrigin(e.clientX, e.clientY);
                          postAction('updateSetting', { key: 'theme', value: 'dark' });
                        }}
                        className={cn(
                          'inline-flex items-center gap-1 rounded-md px-2.5 py-0.5 text-[11px] font-semibold transition-colors cursor-pointer',
                          !isLight ? 'bg-indigo-600 text-white' : 'text-zinc-500 hover:text-zinc-900'
                        )}
                      >
                        <Moon className="size-3" />
                        Dark
                      </button>
                      <button
                        type="button"
                        onClick={(e) => {
                          setThemeTransitionOrigin(e.clientX, e.clientY);
                          postAction('updateSetting', { key: 'theme', value: 'light' });
                        }}
                        className={cn(
                          'inline-flex items-center gap-1 rounded-md px-2.5 py-0.5 text-[11px] font-semibold transition-colors cursor-pointer',
                          isLight ? 'bg-indigo-600 text-white' : 'text-zinc-400 hover:text-white'
                        )}
                      >
                        <Sun className="size-3" />
                        Light
                      </button>
                    </div>
                  </div>
                </div>
              </div>

              <div className="mt-5 text-center px-3">
                <h2 className="font-display text-[19px] font-bold tracking-tight text-zinc-900 dark:text-white">
                  You&apos;re Ready to Play
                </h2>
                <p className="mt-1.5 text-[13px] leading-relaxed text-zinc-500 dark:text-zinc-400">
                  Data Dragon, CommunityDragon, and meta builds stay synced automatically. Replay this guide anytime in <strong className="font-semibold text-zinc-700 dark:text-zinc-200">Settings</strong>.
                </p>
              </div>
            </div>
          </motion.div>
        </div>

        {/* Footer Controls: Back · Step Dots · Next / Get Started */}
        <div className="flex items-center justify-between border-t border-zinc-200/80 dark:border-white/[0.07] px-6 py-4">
          <div className="w-24">
            {step > 0 && (
              <Button size="sm" variant="ghost" onClick={prev}>
                <ArrowLeft className="size-3.5" />
                Back
              </Button>
            )}
          </div>

          {/* Clickable Slider Indicator Pills */}
          <div className="flex items-center gap-1.5">
            {SLIDE_META.map((s, i) => (
              <button
                key={s.code}
                type="button"
                aria-label={`Slide ${i + 1}`}
                onClick={() => setStep(i)}
                className={cn(
                  'h-2 rounded-full transition-all duration-300 cursor-pointer',
                  i === step
                    ? 'w-7 bg-indigo-500'
                    : 'w-2 bg-zinc-300 hover:bg-zinc-400 dark:bg-white/20 dark:hover:bg-white/35'
                )}
              />
            ))}
          </div>

          <div className="flex w-28 justify-end">
            <Button size="sm" variant="accent" onClick={next}>
              {step < TOTAL_SLIDES - 1 ? (
                <>
                  Next
                  <ArrowRight className="size-3.5" />
                </>
              ) : (
                <>
                  Get Started
                  <Check className="size-3.5" />
                </>
              )}
            </Button>
          </div>
        </div>
      </motion.div>
    </motion.div>
  );
}
