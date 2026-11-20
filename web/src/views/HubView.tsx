import React, { useEffect, useMemo, useRef, useState } from 'react';
import { AnimatePresence, motion } from 'framer-motion';
import {
  Activity,
  ArrowLeft,
  Bug,
  Check,
  ChevronDown,
  ChevronRight,
  Coins,
  Compass,
  Crown,
  Download,
  Eye,
  EyeOff,
  FileText,
  Flame,
  History,
  Keyboard,
  LayoutGrid,
  Link2,
  Minus,
  Moon,
  Pin,
  PinOff,
  Plus,
  RefreshCw,
  RotateCcw,
  ScanSearch,
  Search,
  Settings,
  Shield,
  Sliders,
  Sparkles,
  Square,
  Sun,
  Swords,
  ThumbsDown,
  ThumbsUp,
  Timer,
  TrendingUp,
  Trophy,
  Wand2,
  X,
  Zap,
} from 'lucide-react';
import { GlassCard } from '@/components/ui/glass-card';
import { MayloEmblem, MayloWordmark } from '@/components/ui/brand';
import {
  SwissDraftEmptyIllustration,
  SwissRiftLiveIllustration,
} from '@/components/ui/illustrations';
import {
  BorderBeam,
  NumberTicker,
  StaggerGrid,
  StaggerItem,
  SWISS_EASE,
  SwissEyebrow,
} from '@/components/ui/motion-primitives';
import { OnboardingModal } from '@/components/OnboardingModal';
import { Button, Keycap, Switch, TierBadge } from '@/components/ui/primitives';
import { SegmentedTabs } from '@/components/ui/segmented-tabs';
import { Status } from '@/components/ui/status';
import { postAction } from '@/lib/bridge';
import { setThemeTransitionOrigin } from '@/lib/theme-transition';
import { cn, fmtGold, fmtPct, iconUrl, itemIconUrl, parsePct, spellIconUrl } from '@/lib/utils';
import type { AppState, GameModeKey, LivePlayer, MatchRecord, StatItem } from '@/types';

const SR_ROLES = [
  { key: 'ALL', label: 'All' },
  { key: 'TOP', label: 'Top' },
  { key: 'JUNGLE', label: 'Jungle' },
  { key: 'MID', label: 'Mid' },
  { key: 'ADC', label: 'ADC' },
  { key: 'SUPPORT', label: 'Support' },
] as const;

const BUILD_ROLES = [
  { key: 'top', label: 'Top' },
  { key: 'jungle', label: 'Jungle' },
  { key: 'mid', label: 'Mid' },
  { key: 'adc', label: 'ADC' },
  { key: 'support', label: 'Support' },
] as const;

const ELO_OPTIONS = [
  { key: 'emerald_plus', label: 'Emerald+' },
  { key: 'diamond_plus', label: 'Diamond+' },
  { key: 'master_plus', label: 'Master+' },
  { key: 'platinum_plus', label: 'Platinum+' },
  { key: 'gold_plus', label: 'Gold+' },
  { key: 'all', label: 'All Ranks' },
] as const;

const MODE_OPTIONS: { value: GameModeKey; label: string; icon: React.ReactNode }[] = [
  { value: 'MAYHEM', label: 'Mayhem', icon: <Sparkles className="size-3" /> },
  { value: 'ARENA', label: 'Arena', icon: <Crown className="size-3" /> },
  { value: 'ARAM', label: 'ARAM', icon: <Flame className="size-3" /> },
  { value: 'SR', label: 'Ranked SR', icon: <Shield className="size-3" /> },
];

const FOCUS_OPTIONS = [
  { value: 'Adaptive', label: 'Adaptive' },
  { value: 'Ability power', label: 'Ability power' },
  { value: 'On-hit', label: 'On-hit' },
  { value: 'Critical strike', label: 'Critical strike' },
  { value: 'Survival', label: 'Survival' },
];

const SKILL_KEYS = ['Q', 'W', 'E', 'R'] as const;
const SKILL_CELL: Record<string, string> = {
  Q: 'bg-indigo-500 text-white border-indigo-400/70',
  W: 'bg-sky-500 text-white border-sky-400/70',
  E: 'bg-amber-500 text-zinc-950 border-amber-400/70',
  R: 'bg-rose-500 text-white border-rose-400/70',
};

function ChampAvatar({
  src,
  alt,
  className,
}: {
  src?: string;
  alt: string;
  className?: string;
}) {
  if (!src) {
    return <div className={cn('bg-zinc-200 dark:bg-white/10 shrink-0', className)} />;
  }
  return (
    <div className={cn('overflow-hidden bg-zinc-900 shrink-0', className)}>
      <img src={iconUrl(src)} alt={alt} className="size-full object-cover scale-[1.14]" />
    </div>
  );
}

function AugmentIcon({
  src,
  alt,
  className,
  iconClassName = 'size-4',
}: {
  src?: string;
  alt: string;
  className?: string;
  iconClassName?: string;
}) {
  const [broken, setBroken] = useState(false);
  useEffect(() => {
    setBroken(false);
  }, [src]);
  if (!src || broken) {
    return (
      <div
        className={cn(
          'flex items-center justify-center bg-indigo-500/12 text-indigo-500 dark:text-indigo-300 shrink-0',
          className
        )}
      >
        <Sparkles className={iconClassName} />
      </div>
    );
  }
  return (
    <img
      src={iconUrl(src)}
      alt={alt}
      onError={() => setBroken(true)}
      className={cn('bg-zinc-900 object-cover shrink-0', className)}
    />
  );
}

function OverviewTrendChart({ matches }: { matches: MatchRecord[] }) {
  const [metric, setMetric] = useState<'kda' | 'wr'>('kda');
  const recent = useMemo(
    () => matches.filter((m) => !m.remake).slice(0, 12).reverse(),
    [matches]
  );

  if (recent.length < 2) {
    return (
      <div className="flex h-[176px] items-center justify-center rounded-xl border border-dashed border-zinc-200 dark:border-white/10 text-xs text-zinc-500 dark:text-zinc-400">
        Play or refresh recent matches to unlock your live KDA & Win Rate trend chart
      </div>
    );
  }

  let cumWins = 0;
  const points = recent.map((m, idx) => {
    if (m.won) cumWins++;
    const kda = Math.min(14, (m.kills + m.assists) / Math.max(1, m.deaths));
    const wr = (cumWins / (idx + 1)) * 100;
    return {
      idx,
      champ: m.championName,
      won: m.won,
      kda,
      wr,
      val: metric === 'kda' ? kda : wr,
    };
  });

  const maxVal = metric === 'kda' ? Math.max(6, Math.ceil(Math.max(...points.map((p) => p.val)))) : 100;
  const minVal = 0;
  const W = 520;
  const H = 160;
  const padX = 28;
  const padY = 18;
  const plotW = W - padX * 2;
  const plotH = H - padY * 2;

  const coords = points.map((p, i) => {
    const x = padX + (i / Math.max(1, points.length - 1)) * plotW;
    const y = padY + plotH - ((p.val - minVal) / Math.max(1, maxVal - minVal)) * plotH;
    return { ...p, x, y };
  });

  const linePath = coords
    .map((c, i) => `${i === 0 ? 'M' : 'L'} ${c.x.toFixed(1)} ${c.y.toFixed(1)}`)
    .join(' ');
  const areaPath = `${linePath} L ${coords[coords.length - 1].x.toFixed(1)} ${(H - padY).toFixed(1)} L ${coords[0].x.toFixed(1)} ${(H - padY).toFixed(1)} Z`;

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between gap-2">
        <div>
          <h3 className="text-sm font-semibold text-zinc-900 dark:text-white">
            Recent Form & Performance Trend
          </h3>
          <p className="text-[11px] text-zinc-500 dark:text-zinc-400">
            Last {recent.length} games · Indigo = Victory · Rose = Defeat
          </p>
        </div>
        <div className="inline-flex rounded-lg border border-zinc-200 dark:border-white/10 bg-zinc-100 dark:bg-white/[0.04] p-0.5">
          {[
            { id: 'kda' as const, label: 'KDA' },
            { id: 'wr' as const, label: 'Win Rate %' },
          ].map((opt) => (
            <button
              key={opt.id}
              type="button"
              onClick={() => setMetric(opt.id)}
              className={cn(
                'rounded-md px-2.5 py-1 text-[11px] font-semibold transition-colors cursor-pointer',
                metric === opt.id
                  ? 'bg-indigo-600 text-white shadow-xs'
                  : 'text-zinc-500 dark:text-zinc-400 hover:text-zinc-900 dark:hover:text-white'
              )}
            >
              {opt.label}
            </button>
          ))}
        </div>
      </div>

      <div className="relative w-full overflow-hidden rounded-xl border border-zinc-200/75 dark:border-white/[0.07] bg-zinc-50/70 dark:bg-[#0D0F16] px-2 py-2">
        <svg viewBox={`0 0 ${W} ${H}`} className="w-full h-[156px] overflow-visible">
          <defs>
            <linearGradient id="mayloTrendFill" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="#6366F1" stopOpacity="0.34" />
              <stop offset="100%" stopColor="#6366F1" stopOpacity="0.02" />
            </linearGradient>
          </defs>

          {[0, 0.5, 1].map((t) => {
            const y = padY + plotH * (1 - t);
            const lbl = metric === 'kda' ? `${(maxVal * t).toFixed(0)}` : `${Math.round(100 * t)}%`;
            return (
              <g key={t}>
                <line
                  x1={padX}
                  y1={y}
                  x2={W - padX}
                  y2={y}
                  stroke="currentColor"
                  className="text-zinc-200 dark:text-white/[0.07]"
                  strokeDasharray="3 4"
                />
                <text
                  x={padX - 6}
                  y={y + 3}
                  textAnchor="end"
                  className="fill-zinc-400 dark:fill-zinc-500 text-[9px] font-semibold tabular-nums"
                >
                  {lbl}
                </text>
              </g>
            );
          })}

          <motion.path
            key={`area-${metric}`}
            d={areaPath}
            fill="url(#mayloTrendFill)"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            transition={{ duration: 0.45, ease: SWISS_EASE }}
          />
          <motion.path
            key={`line-${metric}`}
            d={linePath}
            fill="none"
            stroke="#818CF8"
            strokeWidth="2.5"
            strokeLinecap="round"
            strokeLinejoin="round"
            initial={{ pathLength: 0, opacity: 0.4 }}
            animate={{ pathLength: 1, opacity: 1 }}
            transition={{ duration: 0.65, ease: SWISS_EASE }}
          />

          {coords.map((c, idx) => (
            <motion.g
              key={`${metric}-${c.idx}`}
              initial={{ scale: 0, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              transition={{ delay: 0.08 + idx * 0.03, duration: 0.25, ease: SWISS_EASE }}
              style={{ transformOrigin: `${c.x}px ${c.y}px` }}
            >
              <circle
                cx={c.x}
                cy={c.y}
                r="4.5"
                fill={c.won ? '#6366F1' : '#F43F5E'}
                className="stroke-white dark:stroke-[#0F1016]"
                strokeWidth="2"
              >
                <title>
                  {c.champ}: {c.won ? 'Win' : 'Loss'} · {c.kda.toFixed(2)} KDA ({Math.round(c.wr)}% WR)
                </title>
              </circle>
            </motion.g>
          ))}
        </svg>
      </div>
    </div>
  );
}


function rarityPillClass(rarity?: string) {
  const r = (rarity || '').toUpperCase();
  if (r === 'PRISMATIC')
    return 'bg-purple-100 text-purple-700 border-purple-200 dark:bg-purple-500/18 dark:text-purple-300 dark:border-purple-400/35';
  if (r === 'GOLD')
    return 'bg-amber-100 text-amber-800 border-amber-200 dark:bg-amber-500/18 dark:text-amber-300 dark:border-amber-400/35';
  return 'bg-zinc-100 text-zinc-600 border-zinc-200 dark:bg-white/10 dark:text-zinc-300 dark:border-white/15';
}

function SkillOrderCard({
  priority,
  levels,
  source,
  activeLevel,
}: {
  priority: string[];
  levels: string[];
  source: string;
  activeLevel: number;
}) {
  return (
    <GlassCard surface="hub" spotlight className="p-5">
      <div className="flex flex-wrap items-center justify-between gap-3 mb-4">
        <div className="flex items-center gap-3">
          <div className="flex size-8 items-center justify-center rounded-xl bg-indigo-500/12 text-indigo-600 dark:text-indigo-400">
            <TrendingUp className="size-4" />
          </div>
          <div>
            <div className="text-sm font-semibold text-zinc-900 dark:text-zinc-50">
              Skill Level-Up Order
            </div>
            <div className="text-[11px] text-zinc-500 dark:text-zinc-400">
              {source || 'Recommended upgrade sequence'}
            </div>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <span className="text-[11px] font-medium text-zinc-500 dark:text-zinc-400">Max Priority</span>
          <div className="flex items-center gap-1">
            {priority.map((k, idx) => (
              <React.Fragment key={`${k}-${idx}`}>
                {idx > 0 && <ChevronRight className="size-3 text-zinc-400" />}
                <span
                  className={cn(
                    'inline-flex size-6 items-center justify-center rounded-lg border text-xs font-bold shadow-xs',
                    SKILL_CELL[k] ?? 'bg-zinc-700 text-white border-zinc-500'
                  )}
                >
                  {k}
                </span>
              </React.Fragment>
            ))}
          </div>
        </div>
      </div>

      <div className="overflow-x-auto pb-1">
        <div className="grid grid-cols-[30px_repeat(18,minmax(0,1fr))] gap-1.5 min-w-[540px]">
          <div />
          {Array.from({ length: 18 }, (_, i) => {
            const lv = i + 1;
            const current = lv === activeLevel;
            return (
              <div
                key={lv}
                className={cn(
                  'rounded-md py-1 text-center text-[10px] font-semibold tabular-nums',
                  current
                    ? 'bg-indigo-500/20 text-indigo-700 dark:text-indigo-300'
                    : 'text-zinc-400 dark:text-zinc-500'
                )}
              >
                {lv}
              </div>
            );
          })}
          {SKILL_KEYS.map((sk) => (
            <React.Fragment key={sk}>
              <div className="flex items-center justify-center text-xs font-bold text-zinc-700 dark:text-zinc-300">
                {sk}
              </div>
              {Array.from({ length: 18 }, (_, i) => {
                const active = (levels[i] || '').toUpperCase() === sk;
                const isNow = i + 1 === activeLevel;
                return (
                  <div
                    key={i}
                    className={cn(
                      'flex h-6 items-center justify-center rounded-md border text-[10px] font-bold transition-colors',
                      active
                        ? SKILL_CELL[sk]
                        : isNow
                          ? 'border-indigo-400/50 bg-indigo-500/10'
                          : 'border-zinc-200/80 dark:border-white/[0.06] bg-zinc-50 dark:bg-white/[0.02]'
                    )}
                  >
                    {active ? sk : ''}
                  </div>
                );
              })}
            </React.Fragment>
          ))}
        </div>
      </div>
    </GlassCard>
  );
}

function PatchTagBadge({ tag, compact = false }: { tag?: string; compact?: boolean }) {
  if (!tag) return null;
  const t = tag.toLowerCase();
  const label =
    t === 'buff'
      ? compact
        ? '▲'
        : 'BUFF'
      : t === 'nerf'
        ? compact
          ? '▼'
          : 'NERF'
        : t === 'added'
          ? 'NEW'
          : t === 'system'
            ? 'SYS'
            : compact
              ? '~'
              : 'ADJ';
  const cls =
    t === 'buff'
      ? 'border-emerald-500/35 bg-emerald-500/12 text-emerald-600 dark:text-emerald-300'
      : t === 'nerf'
        ? 'border-rose-500/35 bg-rose-500/12 text-rose-600 dark:text-rose-300'
        : t === 'added'
          ? 'border-indigo-500/35 bg-indigo-500/12 text-indigo-600 dark:text-indigo-300'
          : 'border-amber-500/35 bg-amber-500/12 text-amber-600 dark:text-amber-300';
  return (
    <span
      className={cn(
        'inline-flex items-center rounded border px-1.5 py-0.5 font-mono-tech text-[9px] font-bold uppercase tracking-wider leading-none shrink-0',
        cls
      )}
    >
      {label}
    </span>
  );
}

function GameLengthScalingCard({ points }: { points: { minutes: number; winRate: number }[] }) {
  if (!points || points.length < 2) return null;
  const sorted = [...points].sort((a, b) => a.minutes - b.minutes);
  const first = sorted[0].winRate;
  const last = sorted[sorted.length - 1].winRate;
  const peak = sorted.reduce((best, p) => (p.winRate > best.winRate ? p : best), sorted[0]);
  const delta = last - first;
  const profile =
    delta >= 2.2
      ? { label: 'Late Scaling', tone: 'text-indigo-600 dark:text-indigo-300 bg-indigo-500/12 border-indigo-500/30' }
      : delta <= -2.2
        ? { label: 'Early Dominant', tone: 'text-amber-600 dark:text-amber-300 bg-amber-500/12 border-amber-500/30' }
        : peak.minutes >= 25 && peak.minutes <= 35
          ? { label: 'Mid-Game Spike', tone: 'text-teal-600 dark:text-teal-300 bg-teal-500/12 border-teal-500/30' }
          : { label: 'Balanced Curve', tone: 'text-zinc-600 dark:text-zinc-300 bg-zinc-500/10 border-zinc-500/25' };

  const minWr = Math.min(44, ...sorted.map((p) => p.winRate - 1.5));
  const maxWr = Math.max(56, ...sorted.map((p) => p.winRate + 1.5));
  const range = Math.max(4, maxWr - minWr);
  const W = 380;
  const H = 84;
  const padX = 24;
  const padY = 14;
  const coords = sorted.map((p, i) => {
    const x = padX + (i / Math.max(1, sorted.length - 1)) * (W - padX * 2);
    const y = padY + (1 - (p.winRate - minWr) / range) * (H - padY * 2);
    return { ...p, x, y };
  });
  const linePath = coords.map((c, i) => `${i === 0 ? 'M' : 'L'} ${c.x.toFixed(1)} ${c.y.toFixed(1)}`).join(' ');
  const midY = padY + (1 - (50 - minWr) / range) * (H - padY * 2);

  return (
    <GlassCard surface="hub" className="p-4">
      <div className="flex flex-wrap items-center justify-between gap-2 mb-3">
        <div>
          <h3 className="text-xs font-bold uppercase tracking-wider text-zinc-800 dark:text-zinc-100">
            Power Spike by Game Length
          </h3>
          <p className="text-[11px] text-zinc-500 dark:text-zinc-400">
            Win rate scaling across match duration (dashed line = 50% WR)
          </p>
        </div>
        <span className={cn('rounded-full border px-2.5 py-0.5 text-[10px] font-bold uppercase tracking-wider', profile.tone)}>
          {profile.label}
        </span>
      </div>
      <svg viewBox={`0 0 ${W} ${H}`} className="w-full h-[84px] overflow-visible">
        <line
          x1={padX}
          x2={W - padX}
          y1={midY}
          y2={midY}
          stroke="currentColor"
          strokeOpacity="0.18"
          strokeDasharray="3 3"
        />
        <path
          d={linePath}
          fill="none"
          stroke="#6366F1"
          strokeWidth="2.2"
          strokeLinecap="round"
          strokeLinejoin="round"
        />
        {coords.map((c) => (
          <g key={c.minutes}>
            <circle cx={c.x} cy={c.y} r="3.6" fill={c.winRate >= 50 ? '#6366F1' : '#F43F5E'} />
          </g>
        ))}
      </svg>
      <div className="mt-1 grid gap-1 text-center" style={{ gridTemplateColumns: `repeat(${sorted.length}, minmax(0, 1fr))` }}>
        {sorted.map((p) => (
          <div key={p.minutes} className="rounded-lg bg-zinc-50/80 dark:bg-white/[0.025] py-1 px-1">
            <div className="font-mono-tech text-[10px] text-zinc-400">
              {p.minutes === 0 ? '0–25m' : `${p.minutes}m+`}
            </div>
            <div
              className={cn(
                'text-xs font-bold tabular-nums',
                p.winRate >= 51
                  ? 'text-emerald-600 dark:text-emerald-400'
                  : p.winRate < 49
                    ? 'text-rose-600 dark:text-rose-400'
                    : 'text-zinc-800 dark:text-zinc-200'
              )}
            >
              {fmtPct(p.winRate)}
            </div>
          </div>
        ))}
      </div>
    </GlassCard>
  );
}

export function HubView({ state }: { state: AppState }) {
  const [modal, setModal] = useState<'none' | 'settings' | 'shortcuts'>('none');
  const [replayTutorial, setReplayTutorial] = useState(false);
  const [dismissedTutorial, setDismissedTutorial] = useState(false);
  const [champSearch, setChampSearch] = useState('');
  const [tierFilter, setTierFilter] = useState('All');
  const [augSearch, setAugSearch] = useState('');
  const [augRarity, setAugRarity] = useState('All');
  const [selectedMatch, setSelectedMatch] = useState<MatchRecord | null>(null);
  const [liveFilter, setLiveFilter] = useState<'Overview' | 'Your team' | 'Opponents'>('Overview');
  const [tierSearch, setTierSearch] = useState('');
  const [historySearch, setHistorySearch] = useState('');
  const [patchCat, setPatchCat] = useState<'champion' | 'augment' | 'item' | 'system'>('champion');
  const [patchFilter, setPatchFilter] = useState<'all' | 'buff' | 'nerf' | 'other'>('all');
  const [patchSearch, setPatchSearch] = useState('');
  const [expandedPatches, setExpandedPatches] = useState<Record<string, boolean>>({});
  const [noteDraft, setNoteDraft] = useState<string | null>(null);
  const [toastText, setToastText] = useState<string | null>(null);
  const lastStatusRef = useRef<string | null>(null);

  const isDark = state.settings.theme === 'dark';
  const isMayhem = state.activeMode === 'MAYHEM';
  const isArena = state.activeMode === 'ARENA';
  const hasAugments = isMayhem || isArena;
  const isSR = state.activeMode === 'SR';
  const selected = state.selected;
  const game = state.game;
  const unsupportedMatch = (game.inGame || game.inSelection) && game.modeKey === '';
  const rawView = state.view || 'Dashboard';
  const normalizedView = !hasAugments && rawView === 'Augments' ? 'TierList' : rawView;
  // If user enters Build without a selected champion, cleanly show the full Champions grid!
  const view = normalizedView === 'Build' && !selected ? 'Library' : normalizedView;
  const tutorialOpen = replayTutorial || (state.settings.onboardingSeen === false && !dismissedTutorial);

  // Prevent note draft from leaking onto another champion when switching champions
  useEffect(() => {
    setNoteDraft(null);
  }, [selected?.slug]);

  // Close match detail modal automatically if view changes (e.g., Auto-Follow entering Champ Select)
  useEffect(() => {
    setSelectedMatch(null);
  }, [view]);

  // Reset patch filters cleanly when switching game mode in the top bar
  useEffect(() => {
    setPatchFilter('all');
    setPatchSearch('');
    setExpandedPatches({});
  }, [state.activeMode]);

  // Surface statusText feedback (Sync Databases, Sync LoL, Borderless, Export/Import Config, Save Note, Copy) as a subtle auto-fading toast
  useEffect(() => {
    const next = (state.statusText || '').trim();
    if (lastStatusRef.current === null) {
      lastStatusRef.current = next;
      return;
    }
    if (!next || next === lastStatusRef.current) return;
    lastStatusRef.current = next;
    // Skip passive champion-select status noise
    if (selected && next.startsWith(`${selected.name} · `)) return;
    setToastText(next);
    const id = window.setTimeout(() => setToastText(null), 4000);
    return () => window.clearTimeout(id);
  }, [state.statusText, selected]);

  // Close open modals/drawers cleanly on Escape
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== 'Escape' || tutorialOpen) return;
      if (state.inspectedAugment) {
        e.preventDefault();
        postAction('closeAugment');
      } else if (state.inspectedItem) {
        e.preventDefault();
        postAction('closeItem');
      } else if (selectedMatch) {
        e.preventDefault();
        setSelectedMatch(null);
      } else if (modal !== 'none') {
        e.preventDefault();
        setModal('none');
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [tutorialOpen, state.inspectedAugment, state.inspectedItem, selectedMatch, modal]);

  const navItems = useMemo(() => {
    const items = [
      { id: 'Dashboard', label: 'Overview', icon: LayoutGrid },
      { id: 'TierList', label: isSR ? 'Tier List' : isMayhem ? 'Mayhem Meta' : isArena ? 'Arena Meta' : 'ARAM Tier List', icon: Trophy },
      { id: 'Library', label: 'Champions', icon: Swords },
    ];
    if (hasAugments) {
      items.push({ id: 'Augments', label: 'Augments', icon: Sparkles });
    }
    items.push(
      { id: 'PatchNotes', label: state.patchNotes?.version ? `Patch ${state.patchNotes.version}` : 'Patch Notes', icon: Flame },
      { id: 'Live', label: 'Live game', icon: Activity },
      { id: 'History', label: 'Match history', icon: History },
      { id: 'Synergies', label: isArena ? 'Duo Synergies' : isMayhem ? 'Synergies' : 'Matchups', icon: Link2 },
      { id: 'Notes', label: 'Notes', icon: FileText }
    );
    return items;
  }, [isMayhem, isArena, hasAugments, isSR, state.patchNotes?.version]);

  const filteredTierList = useMemo(() => {
    const q = tierSearch.trim().toLowerCase();
    return (state.tierList || []).filter((t) => !q || t.name.toLowerCase().includes(q) || t.slug.includes(q));
  }, [state.tierList, tierSearch]);

  const modeChampions = useMemo(() => {
    const base = state.champions || [];
    if (isMayhem || !state.tierList || state.tierList.length === 0) return base;
    const bySlug = new Map<string, (typeof base)[number]>();
    for (const c of base) bySlug.set(c.slug, c);
    const seen = new Set<string>();
    const merged: typeof base = [];
    for (const t of state.tierList) {
      if (seen.has(t.slug)) continue;
      seen.add(t.slug);
      const ref = bySlug.get(t.slug);
      merged.push({
        name: t.name,
        slug: t.slug,
        tier: t.tier,
        win: t.winRate,
        pick: t.pickRate,
        icon: t.icon || ref?.icon || '',
        assetKey: ref?.assetKey || t.slug,
        patchTag: t.patchTag || ref?.patchTag || '',
      });
    }
    for (const c of base) {
      if (!seen.has(c.slug)) {
        merged.push({ ...c, tier: 'D', win: 0, pick: 0 });
      }
    }
    return merged;
  }, [isMayhem, state.champions, state.tierList]);

  const filteredChampions = useMemo(() => {
    const q = champSearch.trim().toLowerCase();
    return modeChampions.filter(
      (c) =>
        (tierFilter === 'All' || c.tier.toUpperCase().startsWith(tierFilter)) &&
        (!q || c.name.toLowerCase().includes(q) || c.slug.includes(q))
    );
  }, [modeChampions, champSearch, tierFilter]);

  const filteredAugments = useMemo(() => {
    const q = augSearch.trim().toLowerCase();
    return (state.allAugments || []).filter(
      (a) =>
        (augRarity === 'All' || (a.rarity || '').toLowerCase() === augRarity.toLowerCase()) &&
        (!q || a.name.toLowerCase().includes(q))
    );
  }, [state.allAugments, augSearch, augRarity]);

  const activeModeLabel = isMayhem ? 'ARAM Mayhem' : isArena ? 'Arena' : isSR ? 'Ranked SR' : 'ARAM';

  const modeHistory = useMemo(() => {
    return (state.history || []).filter((m) => {
      const modeStr = (m.mode || '').toUpperCase();
      const isMayhemMatch = Boolean(m.mayhem) || m.queue === 2400 || m.queue === 3270 || modeStr === 'KIWI';
      const isAramMatch = !isMayhemMatch && (m.queue === 450 || m.queue === 720 || modeStr === 'ARAM');
      const isArenaMatch = m.queue === 1700 || m.queue === 1710 || modeStr === 'CHERRY';
      if (state.activeMode === 'MAYHEM') return isMayhemMatch;
      if (state.activeMode === 'ARENA') return isArenaMatch;
      if (state.activeMode === 'ARAM') return isAramMatch;
      return !isMayhemMatch && !isAramMatch && !isArenaMatch;
    });
  }, [state.history, state.activeMode]);

  const filteredHistory = useMemo(() => {
    const q = historySearch.trim().toLowerCase();
    return modeHistory.filter((m) => {
      if (q && !m.championName.toLowerCase().includes(q) && !m.queueName.toLowerCase().includes(q)) return false;
      return true;
    });
  }, [modeHistory, historySearch]);

  const computePool = (matches: MatchRecord[]) => {
    const valid = matches.filter((m) => !m.remake);
    const map = new Map<
      string,
      { name: string; icon: string; games: number; wins: number; k: number; d: number; a: number; csMinSum: number }
    >();
    for (const m of valid) {
      const cur = map.get(m.championName) ?? {
        name: m.championName,
        icon: m.championIcon,
        games: 0,
        wins: 0,
        k: 0,
        d: 0,
        a: 0,
        csMinSum: 0,
      };
      cur.games++;
      if (m.won) cur.wins++;
      cur.k += m.kills;
      cur.d += m.deaths;
      cur.a += m.assists;
      cur.csMinSum += m.cs / Math.max(1, m.duration / 60);
      map.set(m.championName, cur);
    }
    return Array.from(map.values())
      .sort((a, b) => b.games - a.games || b.wins / b.games - a.wins / a.games)
      .slice(0, 4);
  };

  const computeStats = (matches: MatchRecord[]) => {
    const valid = matches.filter((m) => !m.remake);
    const total = valid.length;
    if (total === 0) return { games: 0, wr: 0, kda: '0.0', avgCsMin: '0.0' };
    const wins = valid.filter((m) => m.won).length;
    const k = valid.reduce((a, m) => a + m.kills, 0);
    const d = valid.reduce((a, m) => a + m.deaths, 0);
    const as = valid.reduce((a, m) => a + m.assists, 0);
    const csMinSum = valid.reduce((a, m) => a + m.cs / Math.max(1, m.duration / 60), 0);
    return {
      games: total,
      wr: (wins / total) * 100,
      kda: ((k + as) / Math.max(1, d)).toFixed(2),
      avgCsMin: (csMinSum / total).toFixed(1),
    };
  };

  const overviewTopChampions = useMemo(() => computePool(modeHistory), [modeHistory]);
  const overviewStats = useMemo(() => computeStats(modeHistory), [modeHistory]);
  const topPlayedChampions = useMemo(() => computePool(filteredHistory), [filteredHistory]);
  const historyStats = useMemo(() => computeStats(filteredHistory), [filteredHistory]);

  const onTitleMouseDown = (e: React.MouseEvent) => {
    if ((e.target as HTMLElement).closest('button, input, select, [data-nodrag]')) return;
    if (e.button === 0 && e.detail === 1) {
      postAction('dragWindow');
    }
  };

  const onTitleDoubleClick = (e: React.MouseEvent) => {
    if ((e.target as HTMLElement).closest('button, input, select, [data-nodrag]')) return;
    postAction('maximizeWindow');
  };

  const renderAugmentTile = (aug: StatItem) => (
    <button
      key={aug.id}
      type="button"
      onClick={() => postAction('inspectAugment', { id: aug.id })}
      className="group flex items-center justify-between gap-3 rounded-xl border border-zinc-200/80 dark:border-white/[0.08] bg-white dark:bg-[#14151B] px-3.5 py-3 text-left transition-all hover:border-indigo-400/50 hover:bg-zinc-50/70 dark:hover:bg-[#191B24] cursor-pointer"
    >
      <div className="flex items-center gap-3 min-w-0">
        <AugmentIcon
          src={aug.icon}
          alt={aug.name}
          className="size-9 rounded-lg border border-zinc-200 dark:border-white/12"
        />
        <div className="min-w-0">
          <div className="flex items-center gap-1.5">
            <span className="truncate text-[13px] font-semibold text-zinc-900 dark:text-zinc-50 group-hover:text-indigo-600 dark:group-hover:text-indigo-300">
              {aug.name}
            </span>
            {aug.tracked && (
              <span className="rounded bg-indigo-500/15 px-1.5 py-0.5 text-[9px] font-bold uppercase text-indigo-600 dark:text-indigo-300">
                Tracked
              </span>
            )}
          </div>
          <div className="mt-0.5 flex items-center gap-2 text-[11px] text-zinc-500 dark:text-zinc-400 tabular-nums">
            <span className="font-semibold text-indigo-600 dark:text-indigo-300">
              {fmtPct(aug.rate)}
            </span>
            {parsePct(aug.pick) && <span>PR {parsePct(aug.pick)}</span>}
            {aug.rarity && (
                <span
                  className={cn(
                    'inline-flex items-center rounded-md border px-2 py-1 text-[9px] font-semibold uppercase leading-none',
                    rarityPillClass(aug.rarity)
                  )}
                >
                  {aug.rarity}
                </span>
            )}
          </div>
        </div>
      </div>
      <div className="flex items-center gap-2 shrink-0">
        <TierBadge tier={aug.tier} size="sm" />
        <ChevronRight className="size-3.5 text-zinc-400 opacity-0 group-hover:opacity-100 transition-opacity" />
      </div>
    </button>
  );

  const liveMinutes = Math.max(1 / 60, (game.live.seconds || 0) / 60);
  const myTeam = game.live.players.find((p) => p.me)?.team ?? 'ORDER';
  const displayedPlayers: LivePlayer[] = useMemo(() => {
    if (liveFilter === 'Your team') return game.live.players.filter((p) => p.team === myTeam);
    if (liveFilter === 'Opponents') return game.live.players.filter((p) => p.team !== myTeam);
    return game.live.players;
  }, [game.live.players, liveFilter, myTeam]);

  return (
    <div className="flex h-screen w-screen flex-col overflow-hidden bg-[#F7F8FA] dark:bg-[#0B0C10] text-zinc-900 dark:text-zinc-100 select-none">
      {/* TOP BAR */}
      <header
        onMouseDown={onTitleMouseDown}
        onDoubleClick={onTitleDoubleClick}
        className="flex h-12 shrink-0 items-center justify-between gap-4 border-b border-zinc-200/80 dark:border-white/[0.07] bg-white/90 dark:bg-[#0F1016]/95 px-4 backdrop-blur-md"
      >
        <div className="flex items-center gap-3 min-w-0">
          <div className="flex items-center gap-2.5 text-zinc-950 dark:text-white">
            <div className="flex size-7 items-center justify-center rounded-lg border border-zinc-200/90 dark:border-white/[0.12] bg-zinc-50 dark:bg-white/[0.04] shadow-2xs">
              <MayloEmblem className="size-5" light={!isDark} interactive />
            </div>
            <MayloWordmark size="sm" showVersion />
          </div>
          <div className="h-4 w-px bg-zinc-200 dark:bg-white/10" />
          <div data-nodrag className="flex items-center gap-2">
            <SegmentedTabs
              size="sm"
              value={state.activeMode}
              onChange={(mode) => postAction('setMode', { mode })}
              options={MODE_OPTIONS}
            />
            {state.detectedMode && (
              <span className="hidden xl:inline-flex items-center gap-1 rounded-full border border-zinc-200 dark:border-white/[0.08] bg-zinc-50 dark:bg-white/[0.04] px-2.5 py-0.5 text-[10px] font-medium text-zinc-500 dark:text-zinc-400">
                Auto: {state.detectedMode}
              </span>
            )}
          </div>
        </div>

        <div className="flex items-center gap-1.5 shrink-0" data-nodrag>
          {state.update?.canRestart && (
            <button
              type="button"
              onClick={() => postAction('applyUpdateRestart')}
              title={state.update.message || `Restart maylo to apply v${state.update.version}`}
              className="update-btn-glow relative overflow-hidden inline-flex items-center gap-1.5 whitespace-nowrap shrink-0 rounded-full border border-indigo-300/60 bg-gradient-to-r from-indigo-600 via-violet-600 to-indigo-500 px-3 py-1 text-[11px] font-bold leading-none text-white hover:brightness-110 transition-all cursor-pointer mr-1"
            >
              <span className="relative flex size-2 shrink-0">
                <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-300 opacity-80" />
                <span className="relative inline-flex size-2 rounded-full bg-emerald-300" />
              </span>
              <Download className="size-3 shrink-0" />
              <span>Update · Restart</span>
            </button>
          )}
          <Status
            variant={unsupportedMatch ? 'warning' : game.inGame ? 'live' : state.settings.connected ? 'success' : 'default'}
            pulse={game.inGame && !unsupportedMatch}
            title={state.connectionText}
            className="hidden md:inline-flex mr-1"
          >
            {unsupportedMatch ? 'Unsupported mode' : game.inGame
              ? `In Game · ${game.live.clock || 'Live'}`
              : game.inSelection
                ? 'Champ Select'
                : state.connectionText || 'Client Ready'}
          </Status>

          <button
            type="button"
            title={state.pinned ? 'Unpin window (Always on Top ON)' : 'Pin maylo above League window'}
            onClick={() => postAction('togglePin')}
            className={cn(
              'flex size-8 items-center justify-center rounded-lg cursor-pointer transition-colors',
              state.pinned
                ? 'bg-indigo-500/15 text-indigo-600 dark:text-indigo-300'
                : 'text-zinc-500 hover:bg-zinc-100 hover:text-zinc-900 dark:text-zinc-400 dark:hover:bg-white/[0.07] dark:hover:text-white'
            )}
          >
            {state.pinned ? <PinOff className="size-4" /> : <Pin className="size-4" />}
          </button>
          <button
            type="button"
            title="Toggle private mode"
            onClick={() => postAction('togglePrivate')}
            className="flex size-8 items-center justify-center rounded-lg text-zinc-500 hover:bg-zinc-100 hover:text-zinc-900 dark:text-zinc-400 dark:hover:bg-white/[0.07] dark:hover:text-white cursor-pointer"
          >
            {state.privateMode ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
          </button>
          <button
            type="button"
            title="Toggle light/dark theme"
            onClick={(e) => {
              setThemeTransitionOrigin(e.clientX, e.clientY);
              postAction('updateSetting', { key: 'theme', value: isDark ? 'light' : 'dark' });
            }}
            className="flex size-8 items-center justify-center rounded-lg text-zinc-500 hover:bg-zinc-100 hover:text-zinc-900 dark:text-zinc-400 dark:hover:bg-white/[0.07] dark:hover:text-white cursor-pointer"
          >
            {isDark ? <Sun className="size-4" /> : <Moon className="size-4" />}
          </button>
          <button
            type="button"
            title="Keyboard shortcuts"
            onClick={() => setModal('shortcuts')}
            className="flex size-8 items-center justify-center rounded-lg text-zinc-500 hover:bg-zinc-100 hover:text-zinc-900 dark:text-zinc-400 dark:hover:bg-white/[0.07] dark:hover:text-white cursor-pointer"
          >
            <Keyboard className="size-4" />
          </button>
          <button
            type="button"
            title="Settings"
            onClick={() => setModal('settings')}
            className="flex size-8 items-center justify-center rounded-lg text-zinc-500 hover:bg-zinc-100 hover:text-zinc-900 dark:text-zinc-400 dark:hover:bg-white/[0.07] dark:hover:text-white cursor-pointer"
          >
            <Settings className="size-4" />
          </button>

          <div className="mx-1 h-4 w-px bg-zinc-200 dark:bg-white/10" />

          <button
            type="button"
            title="Minimize"
            aria-label="Minimize window"
            onClick={() => postAction('minimizeWindow')}
            className="flex size-8 items-center justify-center rounded-lg text-zinc-500 hover:bg-zinc-100 dark:text-zinc-400 dark:hover:bg-white/[0.07] cursor-pointer"
          >
            <Minus className="size-3.5" />
          </button>
          <button
            type="button"
            title="Maximize"
            aria-label="Maximize window"
            onClick={() => postAction('maximizeWindow')}
            className="flex size-8 items-center justify-center rounded-lg text-zinc-500 hover:bg-zinc-100 dark:text-zinc-400 dark:hover:bg-white/[0.07] cursor-pointer"
          >
            <Square className="size-3" />
          </button>
          <button
            type="button"
            title="Close"
            aria-label="Close window"
            onClick={() => postAction('closeWindow')}
            className="flex size-8 items-center justify-center rounded-lg text-zinc-500 hover:bg-rose-500 hover:text-white dark:text-zinc-400 cursor-pointer"
          >
            <X className="size-3.5" />
          </button>
        </div>
      </header>

      {/* BODY */}
      <div className="flex flex-1 min-h-0 overflow-hidden">
        {/* SIDEBAR (Swiss Navigation with 21st.dev sliding active highlight) */}
        <aside className="flex w-[212px] shrink-0 flex-col justify-between border-r border-zinc-200/80 dark:border-white/[0.07] bg-white dark:bg-[#0E0F15] p-3">
          <div className="space-y-1">
            <div className="font-mono-tech px-2.5 pb-1.5 pt-1 text-[9.5px] font-semibold uppercase tracking-[0.14em] text-zinc-400 dark:text-zinc-500">
              Navigation
            </div>
            {navItems.map((item) => {
              const Icon = item.icon;
              const isChampNav = item.id === 'Library';
              const active = isChampNav ? (view === 'Library' || view === 'Build') : view === item.id;
              const onClickNav = () => {
                if (isChampNav) {
                  if (view === 'Build') postAction('setView', { view: 'Library' });
                  else if (selected && (game.inGame || (game.inSelection && game.champion))) postAction('setView', { view: 'Build' });
                  else postAction('setView', { view: 'Library' });
                } else {
                  postAction('setView', { view: item.id });
                }
              };
              return (
                <button
                  key={item.id}
                  type="button"
                  onClick={onClickNav}
                  className={cn(
                    'relative flex w-full items-center gap-2.5 rounded-xl px-3 py-2 text-[13px] font-medium transition-colors duration-150 cursor-pointer',
                    active
                      ? 'text-white dark:text-zinc-950'
                      : 'text-zinc-600 dark:text-zinc-400 hover:bg-zinc-100/85 dark:hover:bg-white/[0.05] hover:text-zinc-900 dark:hover:text-zinc-100'
                  )}
                >
                  {active && (
                    <motion.span
                      layoutId="hub-sidebar-active"
                      transition={{ type: 'spring', stiffness: 460, damping: 35, mass: 0.7 }}
                      className="absolute inset-0 rounded-xl bg-zinc-900 dark:bg-white shadow-xs"
                    />
                  )}
                  <Icon className="relative z-10 size-4 shrink-0" />
                  <span className="relative z-10 truncate flex-1 text-left">{item.label}</span>
                  {item.id === 'Live' && (game.inGame || game.inSelection) && (
                    <span
                      title={game.inGame ? 'Match live' : 'Champ Select'}
                      className="relative z-10 flex size-2.5 shrink-0 items-center justify-center"
                    >
                      <span
                        className={cn(
                          'absolute inline-flex h-full w-full animate-ping rounded-full opacity-75',
                          game.inGame ? 'bg-emerald-400' : 'bg-amber-400'
                        )}
                      />
                      <span
                        className={cn(
                          'relative inline-flex size-2 rounded-full',
                          game.inGame
                            ? 'bg-emerald-400 shadow-[0_0_8px_rgba(52,211,153,0.9)]'
                            : 'bg-amber-400 shadow-[0_0_8px_rgba(251,191,36,0.85)]'
                        )}
                      />
                    </span>
                  )}
                </button>
              );
            })}
          </div>

          {/* Minimal Account / Live Update / Overlay Preview Footer */}
          <div className="space-y-2 border-t border-zinc-200/80 dark:border-white/[0.07] pt-3">
            {(state.update?.state === 'downloading' || state.update?.state === 'ready') && (
              <div
                className={cn(
                  'rounded-xl border p-2.5 space-y-2 transition-colors',
                  state.update.canRestart
                    ? 'border-indigo-400/70 bg-gradient-to-b from-indigo-500/18 to-indigo-500/8 shadow-[0_0_22px_rgba(99,102,241,0.22)]'
                    : 'border-indigo-400/40 bg-indigo-500/10'
                )}
              >
                <div className="flex items-center justify-between gap-2 min-w-0">
                  <span className="inline-flex items-center gap-1.5 min-w-0 text-[11px] font-bold text-indigo-700 dark:text-indigo-200 whitespace-nowrap">
                    {state.update.state === 'downloading' ? (
                      <RefreshCw className="size-3 animate-spin shrink-0" />
                    ) : (
                      <Download className="size-3 shrink-0" />
                    )}
                    <span className="truncate">
                      {state.update.state === 'downloading'
                        ? `Downloading v${state.update.version}…`
                        : `v${state.update.version} ready`}
                    </span>
                  </span>
                  {state.update.state === 'ready' && (
                    <span className="relative flex size-2.5 shrink-0 items-center justify-center">
                      <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-400 opacity-75" />
                      <span className="relative inline-flex size-2 rounded-full bg-emerald-400" />
                    </span>
                  )}
                </div>
                {state.update.canRestart ? (
                  <button
                    type="button"
                    onClick={() => postAction('applyUpdateRestart')}
                    className="update-btn-glow relative z-10 overflow-hidden flex w-full items-center justify-center gap-1.5 whitespace-nowrap rounded-lg bg-gradient-to-r from-indigo-600 via-violet-600 to-indigo-500 px-2.5 py-1.5 text-[11px] font-bold text-white hover:brightness-110 transition-all cursor-pointer"
                  >
                    <RotateCcw className="size-3 shrink-0" />
                    <span>Restart &amp; Update</span>
                  </button>
                ) : state.update.state === 'ready' ? (
                  <div className="relative z-10 text-[10px] text-zinc-500 dark:text-zinc-400">
                    Ready to apply once match ends
                  </div>
                ) : null}
              </div>
            )}
            {state.account && (
              <div className="flex items-center gap-2.5 rounded-xl border border-zinc-200/75 dark:border-white/[0.07] bg-zinc-50/80 dark:bg-white/[0.03] px-2.5 py-2">
                {state.account.profileIconUrl ? (
                  <img
                    src={state.account.profileIconUrl}
                    alt={state.account.riotId}
                    className="size-8 rounded-lg border border-zinc-200 dark:border-white/15 object-cover shrink-0 bg-zinc-900"
                  />
                ) : (
                  <div className="flex size-8 items-center justify-center rounded-lg border border-zinc-200 dark:border-white/12 bg-indigo-500/12 text-xs font-bold text-indigo-600 dark:text-indigo-300 shrink-0">
                    {(state.account.riotId || 'M')[0].toUpperCase()}
                  </div>
                )}
                <div className="min-w-0 flex-1">
                  <div className="truncate text-xs font-semibold text-zinc-900 dark:text-zinc-100">
                    {state.privateMode ? 'Private Summoner' : state.account.riotId}
                  </div>
                  <div className="mt-0.5 text-[10px] text-zinc-500 dark:text-zinc-400">
                    Lv {state.account.level} · {state.account.region}
                  </div>
                </div>
              </div>
            )}
            <Button
              size="sm"
              variant={state.preview ? 'accent' : 'secondary'}
              className="w-full justify-center"
              onClick={() => postAction('togglePreview', { value: !state.preview })}
            >
              <Sliders className="size-3.5" />
              {state.preview ? 'Stop Overlay Edit' : 'Edit Overlay Layout'}
            </Button>
          </div>
        </aside>

        {/* MAIN CONTENT */}
        <main className="flex-1 overflow-y-auto">
          <motion.div
            key={`${view}-${selected?.slug || 'none'}`}
            initial={{ opacity: 0, y: 7 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.24, ease: SWISS_EASE }}
            className="mx-auto max-w-[1160px] px-6 py-5 space-y-5"
          >
            {/* CHAMPION HERO BANNER (with Back Arrow to All Champions!) */}
            {['Build', 'Augments', 'Synergies', 'Notes'].includes(view) && selected && (
              <GlassCard surface="hub" spotlight className="p-5 swiss-grid-bg">
                <BorderBeam tone="indigo" />
                <div className="flex flex-wrap lg:flex-nowrap items-center justify-between gap-4">
                  <div className="flex items-center gap-3.5 min-w-0 flex-1">
                    {view === 'Build' && (
                      <button
                        type="button"
                        title="Back to All Champions"
                        onClick={() => postAction('setView', { view: 'Library' })}
                        className="flex size-9 items-center justify-center rounded-xl border border-zinc-200 dark:border-white/10 bg-zinc-50 dark:bg-white/[0.04] text-zinc-600 dark:text-zinc-300 hover:border-indigo-400/50 hover:text-zinc-900 dark:hover:text-white transition-colors shrink-0 cursor-pointer"
                      >
                        <ArrowLeft className="size-4" />
                      </button>
                    )}
                    {selected.icon && (
                      <ChampAvatar
                        src={selected.icon}
                        alt={selected.name}
                        className="size-14 rounded-2xl border border-zinc-200 dark:border-white/12 shadow-sm"
                      />
                    )}
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center gap-2">
                        <h1 className="text-xl font-bold tracking-tight text-zinc-900 dark:text-white">
                          {selected.name}
                        </h1>
                        <TierBadge tier={selected.tier} size="md" />
                        <PatchTagBadge tag={selected.patchTag} />
                        <span className="inline-flex items-center rounded-lg border border-indigo-200 dark:border-indigo-400/30 bg-indigo-50 dark:bg-indigo-500/15 px-2.5 py-1 text-[11px] font-semibold leading-none text-indigo-700 dark:text-indigo-300">
                          {state.modeSource || state.activeMode}
                        </span>
                      </div>
                      <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-[11px] text-zinc-500 dark:text-zinc-400 tabular-nums">
                        <span>
                          WR{' '}
                          <strong className="text-zinc-900 dark:text-zinc-100">
                            {fmtPct(selected.win)}
                          </strong>
                        </span>
                        <span>
                          PR{' '}
                          <strong className="text-zinc-900 dark:text-zinc-100">
                            {fmtPct(selected.pick)}
                          </strong>
                        </span>
                        {state.runeStatus && !state.runeStatus.toLowerCase().includes('waiting') && (
                          <span className="inline-flex items-center gap-1 text-teal-600 dark:text-teal-300 font-medium">
                            <Check className="size-3" />
                            {state.runeStatus}
                          </span>
                        )}
                        {state.spellStatus && !state.spellStatus.toLowerCase().includes('waiting') && (
                          <span className="inline-flex items-center gap-1 text-indigo-600 dark:text-indigo-300 font-medium">
                            <Zap className="size-3" />
                            {state.spellStatus}
                          </span>
                        )}
                        {state.shopStatus && (
                          <span className="inline-flex items-center gap-1 text-amber-600 dark:text-amber-300 font-medium">
                            <Download className="size-3" />
                            {state.shopStatus}
                          </span>
                        )}
                      </div>
                    </div>
                  </div>

                  <div className="flex items-center gap-2 shrink-0">
                    {state.runes && (
                      <Button size="sm" variant="secondary" onClick={() => postAction('importRunes')}>
                        <Wand2 className="size-3.5" />
                        Import Runes
                      </Button>
                    )}
                    <Button size="sm" variant="primary" onClick={() => postAction('exportItemSet')}>
                      <Download className="size-3.5" />
                      Export to Shop
                    </Button>
                  </div>
                </div>

                {/* Quick Auto-Import Toggles + SR Role Selector Bar */}
                {view === 'Build' && (
                  <div className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t border-zinc-200/70 dark:border-white/[0.07] pt-3.5">
                    <div className="flex flex-wrap items-center gap-2">
                      {isSR && (
                        <>
                          <span className="text-[11px] font-semibold uppercase tracking-wider text-zinc-400">
                            Role
                          </span>
                          <div className="inline-flex items-center gap-1 rounded-xl border border-zinc-200/80 dark:border-white/[0.08] bg-zinc-100/80 dark:bg-white/[0.03] p-1">
                            {BUILD_ROLES.map((r) => {
                              const active = state.activeRole === r.key;
                              const viable = state.availableRoles.includes(r.key);
                              const detected = state.detectedRole === r.key;
                              return (
                                <button
                                  key={r.key}
                                  type="button"
                                  onClick={() => postAction('setRole', { role: r.key })}
                                  className={cn(
                                    'inline-flex items-center gap-1.5 rounded-lg px-2.5 py-1 text-xs font-medium transition-all cursor-pointer',
                                    active
                                      ? 'bg-zinc-900 text-white dark:bg-white dark:text-zinc-950 shadow-xs'
                                      : viable
                                        ? 'text-zinc-700 dark:text-zinc-200 hover:bg-white/70 dark:hover:bg-white/[0.07]'
                                        : 'text-zinc-400 dark:text-zinc-500 hover:text-zinc-700 dark:hover:text-zinc-300'
                                  )}
                                >
                                  <span>{r.label}</span>
                                  {detected && (
                                    <span
                                      className={cn(
                                        'inline-flex items-center rounded-md px-2 py-1 text-[9px] font-bold uppercase leading-none',
                                        active
                                          ? 'bg-indigo-500 text-white'
                                          : 'bg-indigo-500/15 text-indigo-600 dark:text-indigo-300'
                                      )}
                                    >
                                      Auto
                                    </span>
                                  )}
                                </button>
                              );
                            })}
                          </div>
                        </>
                      )}

                      {/* 1-Click Auto-Import Toggles (Runes / Shop / Spells) */}
                      <div className="flex flex-wrap items-center gap-1.5">
                        {[
                          { key: 'autoRunes', label: 'Auto-Runes', on: state.settings.autoRunes },
                          { key: 'autoExportShop', label: 'Auto-Shop', on: state.settings.autoExportShop },
                          { key: 'autoSpells', label: 'Auto-Spells', on: state.settings.autoSpells },
                        ].map((tg) => (
                          <button
                            key={tg.key}
                            type="button"
                            onClick={() => postAction('updateSetting', { key: tg.key, value: !tg.on })}
                            className={cn(
                              'inline-flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-[11px] font-semibold leading-none transition-all cursor-pointer',
                              tg.on
                                ? 'border-indigo-400/50 bg-indigo-500/14 text-indigo-700 dark:text-indigo-200'
                                : 'border-zinc-200 dark:border-white/10 bg-zinc-100/70 dark:bg-white/[0.03] text-zinc-500 dark:text-zinc-400 hover:text-zinc-800 dark:hover:text-zinc-200'
                            )}
                          >
                            <span
                              className={cn(
                                'size-1.5 rounded-full',
                                tg.on ? 'bg-indigo-500 dark:bg-indigo-400' : 'bg-zinc-400 dark:bg-zinc-600'
                              )}
                            />
                            <span>{tg.label}</span>
                            <span className="text-[10px] opacity-80">{tg.on ? 'ON' : 'OFF'}</span>
                          </button>
                        ))}
                      </div>
                    </div>

                    <div className="flex items-center gap-2 text-xs">
                      <span className="text-zinc-400">Flash Key</span>
                      <div className="inline-flex rounded-lg border border-zinc-200 dark:border-white/10 bg-zinc-100 dark:bg-white/[0.04] p-0.5">
                        {(['D', 'F'] as const).map((k) => (
                          <button
                            key={k}
                            type="button"
                            onClick={() => postAction('setFlashKey', { key: k })}
                            className={cn(
                              'w-7 rounded-md py-0.5 text-xs font-bold transition-colors cursor-pointer',
                              state.settings.flashKey === k
                                ? 'bg-indigo-600 text-white'
                                : 'text-zinc-500 dark:text-zinc-400 hover:text-zinc-900 dark:hover:text-white'
                            )}
                          >
                            {k}
                          </button>
                        ))}
                      </div>
                    </div>
                  </div>
                )}

                {state.importStatus && (
                  <div className="mt-3 rounded-xl border border-indigo-200/80 dark:border-indigo-400/25 bg-indigo-50/70 dark:bg-indigo-500/10 px-3 py-2 text-xs font-medium text-indigo-700 dark:text-indigo-200">
                    {state.importStatus}
                  </div>
                )}
              </GlassCard>
            )}

            {/* VIEW: MODE-ADAPTIVE TIER LIST (Role & Elo in Ranked SR, ARAM, Mayhem) */}
            {view === 'TierList' && (
              <div className="space-y-4">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div>
                    <h2 className="text-base font-bold text-zinc-900 dark:text-white">
                      {isSR
                        ? `Ranked Solo/Duo Tier List`
                        : isMayhem
                          ? 'ARAM Mayhem Tier List'
                          : isArena
                            ? 'Arena 2v2 Tier List'
                            : 'ARAM Tier List'}
                    </h2>
                    <p className="text-xs text-zinc-500 dark:text-zinc-400">
                      {isSR
                        ? `Live OP.GG data · Click any champion to open its role-specific build & skill order`
                        : isArena
                          ? `Live OP.GG Arena data · Top-4 Win Rate, Pick & Ban rates · Click any champion for Augments & Prismatic Items`
                          : `Click any champion to inspect optimal build path & live stats`}
                    </p>
                  </div>
                  <div className="relative min-w-[200px] max-w-xs flex-1">
                    <Search className="pointer-events-none absolute left-3 top-1/2 size-3.5 -translate-y-1/2 text-zinc-400" />
                    <input
                      type="text"
                      value={tierSearch}
                      onChange={(e) => setTierSearch(e.target.value)}
                      placeholder="Filter champion..."
                      className="w-full rounded-xl border border-zinc-200 dark:border-white/10 bg-white dark:bg-[#14151B] py-1.5 pl-8 pr-3 text-xs text-zinc-900 dark:text-white placeholder:text-zinc-400 focus:border-indigo-400 focus:outline-none"
                    />
                  </div>
                </div>

                {/* SR Role + Elo Filter Bar */}
                {isSR && (
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <SegmentedTabs
                      size="sm"
                      value={(state.settings.tierRole || 'ALL').toUpperCase()}
                      onChange={(r) => postAction('setTierFilter', { role: r })}
                      options={SR_ROLES.map((r) => ({ value: r.key, label: r.label }))}
                    />
                    <div className="flex flex-wrap items-center gap-1 rounded-xl border border-zinc-200/80 dark:border-white/[0.08] bg-zinc-100/80 dark:bg-white/[0.03] p-1">
                      {ELO_OPTIONS.map((elo) => {
                        const active = (state.settings.tierElo || 'emerald_plus') === elo.key;
                        return (
                          <button
                            key={elo.key}
                            type="button"
                            onClick={() => postAction('setTierFilter', { elo: elo.key })}
                            className={cn(
                              'rounded-lg px-2.5 py-1 text-[11px] font-semibold transition-colors cursor-pointer',
                              active
                                ? 'bg-indigo-600 text-white shadow-xs'
                                : 'text-zinc-600 dark:text-zinc-400 hover:text-zinc-900 dark:hover:text-white'
                            )}
                          >
                            {elo.label}
                          </button>
                        );
                      })}
                    </div>
                  </div>
                )}

                {/* Strictly Aligned Tier List Grid */}
                <GlassCard surface="hub" className="overflow-hidden">
                  {filteredTierList.length === 0 ? (
                    <div className="p-10 text-center text-sm text-zinc-500 dark:text-zinc-400">
                      {state.tierList.length === 0
                        ? 'Loading live Tier List data…'
                        : 'No champions match your search.'}
                    </div>
                  ) : (
                    <div className="overflow-x-auto">
                      <div className="min-w-[740px] divide-y divide-zinc-200/70 dark:divide-white/[0.06]">
                        <div
                          className={cn(
                            'grid items-center gap-4 px-5 py-2.5 text-[10px] font-semibold uppercase tracking-wider text-zinc-400 bg-zinc-50/80 dark:bg-white/[0.02]',
                            isSR
                              ? 'grid-cols-[44px_minmax(180px,1.5fr)_76px_64px_86px_86px_86px_112px]'
                              : isArena
                                ? 'grid-cols-[44px_minmax(210px,1.8fr)_76px_100px_100px_100px]'
                                : 'grid-cols-[44px_minmax(220px,1.8fr)_76px_110px_110px]'
                          )}
                        >
                          <div className="text-center">#</div>
                          <div>Champion</div>
                          {isSR && <div className="text-center">Role</div>}
                          <div className="text-center">Tier</div>
                          <div className="text-right">{isArena ? 'Top 4 %' : 'Win Rate'}</div>
                          <div className="text-right">Pick Rate</div>
                          {(isSR || isArena) && <div className="text-right">Ban Rate</div>}
                          {isSR && <div className="text-right">Counters</div>}
                        </div>

                        {filteredTierList.slice(0, 180).map((row, idx) => (
                          <button
                            key={`${row.slug}-${row.role}-${idx}`}
                            type="button"
                            onClick={() =>
                              postAction('selectChampion', {
                                slug: row.slug,
                                role: isSR ? row.role : '',
                                view: 'Build',
                              })
                            }
                            className={cn(
                              'grid w-full items-center gap-4 px-5 py-2.5 text-left transition-colors hover:bg-zinc-50 dark:hover:bg-white/[0.035] cursor-pointer',
                              isSR
                                ? 'grid-cols-[44px_minmax(180px,1.5fr)_76px_64px_86px_86px_86px_112px]'
                                : isArena
                                  ? 'grid-cols-[44px_minmax(210px,1.8fr)_76px_100px_100px_100px]'
                                  : 'grid-cols-[44px_minmax(220px,1.8fr)_76px_110px_110px]'
                            )}
                          >
                            <div className="flex flex-col items-center justify-center text-xs font-semibold text-zinc-400 tabular-nums leading-tight">
                              <span>{row.rank || idx + 1}</span>
                              {row.rankDelta ? (
                                <span
                                  className={cn(
                                    'font-mono-tech text-[9px] font-bold',
                                    row.rankDelta > 0
                                      ? 'text-emerald-600 dark:text-emerald-400'
                                      : 'text-rose-500 dark:text-rose-400'
                                  )}
                                >
                                  {row.rankDelta > 0 ? `▲${row.rankDelta}` : `▼${Math.abs(row.rankDelta)}`}
                                </span>
                              ) : null}
                            </div>
                            <div className="flex items-center gap-2.5 min-w-0">
                              {row.icon && (
                                <ChampAvatar
                                  src={row.icon}
                                  alt={row.name}
                                  className="size-8 rounded-lg border border-zinc-200 dark:border-white/12"
                                />
                              )}
                              <span className="truncate text-[13px] font-semibold text-zinc-900 dark:text-white">
                                {row.name}
                              </span>
                              <PatchTagBadge tag={row.patchTag} />
                            </div>
                            {isSR && (
                              <div className="text-center">
                                <span className="rounded-md border border-zinc-200 dark:border-white/10 bg-zinc-100 dark:bg-white/[0.05] px-2 py-0.5 text-[10px] font-bold uppercase text-zinc-600 dark:text-zinc-300">
                                  {row.role}
                                </span>
                              </div>
                            )}
                            <div className="flex justify-center">
                              <TierBadge tier={row.tier} size="sm" />
                            </div>
                            <div
                              className={cn(
                                'text-right text-xs font-bold tabular-nums',
                                row.winRate >= 52
                                  ? 'text-indigo-600 dark:text-indigo-300'
                                  : row.winRate < 48.5
                                    ? 'text-rose-600 dark:text-rose-400'
                                    : 'text-zinc-800 dark:text-zinc-200'
                              )}
                            >
                              {fmtPct(row.winRate)}
                            </div>
                            <div className="text-right text-xs text-zinc-500 dark:text-zinc-400 tabular-nums">
                              {fmtPct(row.pickRate)}
                            </div>
                            {(isSR || isArena) && (
                              <div className="text-right text-xs text-zinc-500 dark:text-zinc-400 tabular-nums">
                                {fmtPct(row.banRate)}
                              </div>
                            )}
                            {isSR && (
                              <div className="flex items-center justify-end gap-1">
                                {(row.counters || []).slice(0, 3).map((ic, i) => (
                                  <ChampAvatar
                                    key={i}
                                    src={ic}
                                    alt=""
                                    className="size-6 rounded-md border border-zinc-200 dark:border-white/12"
                                  />
                                ))}
                              </div>
                            )}
                          </button>
                        ))}
                      </div>
                    </div>
                  )}
                </GlassCard>
              </div>
            )}

            {/* VIEW: DASHBOARD / OVERVIEW (MODE-SCOPED) */}
            {view === 'Dashboard' && (() => {
              const recentForm = modeHistory.filter((m) => !m.remake).slice(0, 10);
              const metaPicks =
                state.tierList.length > 0
                  ? state.tierList.slice(0, 8).map((t) => ({
                      slug: t.slug,
                      name: t.name,
                      icon: t.icon,
                      win: t.winRate,
                      tier: t.tier,
                      role: isSR ? t.role : '',
                    }))
                  : isMayhem
                    ? state.champions.slice(0, 8).map((c) => ({
                        slug: c.slug,
                        name: c.name,
                        icon: c.icon,
                        win: c.win,
                        tier: c.tier,
                        role: '',
                      }))
                    : [];
              return (
              <div className="space-y-5">
                {/* Summoner Profile + Recent W/L Form Banner */}
                <GlassCard surface="hub" spotlight className="relative p-5 swiss-grid-bg">
                  <BorderBeam tone="indigo" />
                  <div className="relative z-10 flex flex-wrap items-center justify-between gap-4">
                    <div className="flex items-center gap-3.5 min-w-0">
                      {state.account?.profileIconUrl ? (
                        <img
                          src={state.account.profileIconUrl}
                          alt={state.account.riotId}
                          className="size-14 rounded-2xl border border-zinc-200 dark:border-white/15 object-cover shadow-md bg-zinc-900 shrink-0"
                        />
                      ) : (
                        <div className="font-display flex size-14 items-center justify-center rounded-2xl border border-indigo-400/35 bg-indigo-500/12 text-lg font-bold text-indigo-600 dark:text-indigo-300 shrink-0">
                          {(state.account?.riotId || 'M')[0].toUpperCase()}
                        </div>
                      )}
                      <div className="min-w-0">
                        <SwissEyebrow index="01" label={`OVERVIEW · ${activeModeLabel.toUpperCase()}`} className="mb-1" />
                        <div className="flex flex-wrap items-center gap-2">
                          <h1 className="font-display truncate text-xl font-bold tracking-tight text-zinc-900 dark:text-white">
                            {state.privateMode
                              ? 'Private Summoner'
                              : state.account?.riotId || 'Summoner Overview'}
                          </h1>
                          <span className="font-mono-tech rounded-md border border-indigo-400/25 bg-indigo-500/10 px-2 py-0.5 text-[10px] font-semibold text-indigo-600 dark:text-indigo-300">
                            {activeModeLabel}
                          </span>
                          {state.account && (
                            <span className="font-mono-tech rounded-md border border-zinc-200 dark:border-white/12 bg-white/80 dark:bg-white/[0.06] px-2 py-0.5 text-[10px] font-semibold text-zinc-600 dark:text-zinc-300 backdrop-blur-xs">
                              LV {state.account.level} · {state.account.region}
                            </span>
                          )}
                        </div>
                        <div className="mt-1.5 flex flex-wrap items-center gap-1.5">
                          <span className="font-mono-tech text-[10px] uppercase tracking-wider font-medium text-zinc-500 dark:text-zinc-400 mr-1">
                            Form
                          </span>
                          {recentForm.length === 0 ? (
                            <span className="text-[11px] text-zinc-400">No recent {activeModeLabel} matches</span>
                          ) : (
                            recentForm.map((m) => (
                              <span
                                key={m.id}
                                title={`${m.championName} (${m.kills}/${m.deaths}/${m.assists})`}
                                className={cn(
                                  'font-mono-tech inline-flex size-5 items-center justify-center rounded-md border text-[10px] font-bold',
                                  m.won
                                    ? 'border-indigo-300 bg-indigo-50 text-indigo-700 dark:border-indigo-400/40 dark:bg-indigo-500/18 dark:text-indigo-300'
                                    : 'border-rose-300 bg-rose-50 text-rose-700 dark:border-rose-400/40 dark:bg-rose-500/18 dark:text-rose-300'
                                )}
                              >
                                {m.won ? 'W' : 'L'}
                              </span>
                            ))
                          )}
                        </div>
                      </div>
                    </div>

                    {selected && (
                      <button
                        type="button"
                        onClick={() => postAction('setView', { view: 'Build' })}
                        className="flex items-center gap-2.5 rounded-xl border border-zinc-200/85 dark:border-white/[0.1] bg-white/85 dark:bg-[#141620]/85 px-3.5 py-2 text-left transition-all hover:border-indigo-400/55 hover:-translate-y-0.5 backdrop-blur-md cursor-pointer"
                      >
                        <ChampAvatar
                          src={selected.icon}
                          alt={selected.name}
                          className="size-8 rounded-lg border border-zinc-200 dark:border-white/12"
                        />
                        <div className="min-w-0">
                          <div className="font-mono-tech text-[9.5px] font-semibold uppercase tracking-wider text-zinc-400">
                            {game.inGame || (game.inSelection && game.champion) ? 'Active Match' : 'Selected'}
                          </div>
                          <div className="font-display text-xs font-bold text-zinc-900 dark:text-white truncate">
                            {selected.name} · {fmtPct(selected.win)}
                          </div>
                        </div>
                        <ChevronRight className="size-3.5 text-zinc-400 shrink-0" />
                      </button>
                    )}
                  </div>
                </GlassCard>

                {/* 4 Key KPIs (21st.dev Staggered Bento + NumberTicker) */}
                <StaggerGrid className="grid grid-cols-2 lg:grid-cols-4 gap-3.5">
                  {[
                    {
                      code: '01',
                      label: 'Active Mode',
                      node: (
                        <span>
                          {state.activeMode === 'MAYHEM'
                            ? 'ARAM Mayhem'
                            : state.activeMode === 'ARENA'
                              ? 'Arena 2v2'
                              : state.activeMode === 'ARAM'
                                ? 'ARAM'
                                : "Summoner's Rift"}
                        </span>
                      ),
                      sub: state.modeSource || 'Auto-detected',
                      icon: Compass,
                    },
                    {
                      code: '02',
                      label: `${activeModeLabel} Win Rate`,
                      node:
                        overviewStats.games > 0 ? (
                          <NumberTicker value={overviewStats.wr} decimals={1} suffix="%" />
                        ) : (
                          <span>—</span>
                        ),
                      sub: `${overviewStats.games} ${activeModeLabel} matches`,
                      icon: TrendingUp,
                    },
                    {
                      code: '03',
                      label: 'Average KDA',
                      node:
                        overviewStats.games > 0 ? (
                          <NumberTicker value={parseFloat(overviewStats.kda)} decimals={2} suffix=":1" />
                        ) : (
                          <span>—</span>
                        ),
                      sub: overviewStats.games > 0 ? `${overviewStats.avgCsMin} CS/min avg` : 'Connect client',
                      icon: Activity,
                    },
                    {
                      code: '04',
                      label: 'Auto-Loadout',
                      node: (
                        <span>
                          {state.settings.autoRunes && state.settings.autoExportShop
                            ? 'Runes + Shop'
                            : state.settings.autoRunes
                              ? 'Runes only'
                              : state.settings.autoExportShop
                                ? 'Shop only'
                                : 'Manual'}
                        </span>
                      ),
                      sub: `Flash on ${state.settings.flashKey || 'F'} · Spells ${state.settings.autoSpells ? 'ON' : 'OFF'}`,
                      icon: Zap,
                    },
                  ].map((kpi) => {
                    const Icon = kpi.icon;
                    return (
                      <StaggerItem key={kpi.label}>
                        <GlassCard surface="hub" spotlight className="p-4 h-full">
                          <div className="flex items-center justify-between text-zinc-400">
                            <span className="font-mono-tech text-[10px] font-semibold uppercase tracking-[0.1em]">
                              <span className="text-indigo-500/80 dark:text-indigo-400/80 mr-1.5">{kpi.code}</span>
                              {kpi.label}
                            </span>
                            <Icon className="size-4 text-indigo-500" />
                          </div>
                          <div className="font-display mt-2.5 text-[22px] font-bold tracking-tight text-zinc-900 dark:text-white truncate tabular-nums">
                            {kpi.node}
                          </div>
                          <div className="mt-1 text-xs text-zinc-500 dark:text-zinc-400 truncate">
                            {kpi.sub}
                          </div>
                        </GlassCard>
                      </StaggerItem>
                    );
                  })}
                </StaggerGrid>

                {/* Performance Trend Graph + Personal Champion Pool */}
                <div className="grid grid-cols-1 lg:grid-cols-[1.55fr_1fr] gap-4">
                  <GlassCard surface="hub" className="p-5">
                    <OverviewTrendChart matches={modeHistory} />
                  </GlassCard>

                  <GlassCard surface="hub" className="p-5 flex flex-col justify-between">
                    <div>
                      <div className="flex items-center justify-between mb-3.5">
                        <div>
                          <h3 className="text-sm font-semibold text-zinc-900 dark:text-white">
                            Your Top Champions
                          </h3>
                          <p className="text-[11px] text-zinc-500 dark:text-zinc-400">
                            Personal {activeModeLabel} pool
                          </p>
                        </div>
                        <Button
                          size="xs"
                          variant="secondary"
                          onClick={() => postAction('setView', { view: 'History' })}
                        >
                          History
                        </Button>
                      </div>
                      {overviewTopChampions.length === 0 ? (
                        <div className="rounded-xl border border-dashed border-zinc-200 dark:border-white/10 p-6 text-center text-xs text-zinc-500 dark:text-zinc-400">
                          No personal champion stats yet. Connect League client and refresh.
                        </div>
                      ) : (
                        <div className="space-y-2.5">
                          {overviewTopChampions.slice(0, 3).map((ch) => {
                            const wr = (ch.wins / Math.max(1, ch.games)) * 100;
                            const kda = ((ch.k + ch.a) / Math.max(1, ch.d)).toFixed(2);
                            const avgCs = (ch.csMinSum / Math.max(1, ch.games)).toFixed(1);
                            return (
                              <button
                                key={ch.name}
                                type="button"
                                onClick={() => postAction('selectChampion', { slug: ch.name, view: 'Build' })}
                                className="flex w-full items-center justify-between gap-3 rounded-xl border border-zinc-200/80 dark:border-white/[0.07] bg-zinc-50/70 dark:bg-white/[0.025] px-3 py-2.5 text-left transition-all hover:border-indigo-400/50 cursor-pointer"
                              >
                                <div className="flex items-center gap-2.5 min-w-0">
                                  <ChampAvatar
                                    src={ch.icon}
                                    alt={ch.name}
                                    className="size-9 rounded-lg border border-zinc-200 dark:border-white/12"
                                  />
                                  <div className="min-w-0">
                                    <div className="truncate text-xs font-bold text-zinc-900 dark:text-white">
                                      {ch.name}
                                    </div>
                                    <div className="text-[11px] text-zinc-500 dark:text-zinc-400 tabular-nums">
                                      {kda} KDA · {avgCs} CS/m
                                    </div>
                                  </div>
                                </div>
                                <div className="text-right tabular-nums shrink-0">
                                  <div
                                    className={cn(
                                      'text-xs font-bold',
                                      wr >= 55 ? 'text-indigo-600 dark:text-indigo-300' : 'text-zinc-800 dark:text-zinc-200'
                                    )}
                                  >
                                    {fmtPct(wr)}
                                  </div>
                                  <div className="text-[10px] text-zinc-400">{ch.games} games</div>
                                </div>
                              </button>
                            );
                          })}
                        </div>
                      )}
                    </div>
                  </GlassCard>
                </div>

                {/* Top Meta Champions */}
                <GlassCard surface="hub" className="p-5">
                  <div className="flex items-center justify-between mb-4">
                    <div>
                      <h2 className="text-sm font-semibold text-zinc-900 dark:text-white">
                        Top Tier Champions · {activeModeLabel}
                      </h2>
                      <p className="text-xs text-zinc-500 dark:text-zinc-400">
                        Click any champion to inspect build path, runes, and skill level-up order
                      </p>
                    </div>
                    <Button
                      size="xs"
                      variant="secondary"
                      onClick={() => postAction('setView', { view: 'TierList' })}
                    >
                      Full Tier List
                    </Button>
                  </div>
                  <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
                    {metaPicks.map((c) => (
                      <button
                        key={`${c.slug}-${c.role}`}
                        type="button"
                        onClick={() => postAction('selectChampion', { slug: c.slug, role: c.role, view: 'Build' })}
                        className="flex items-center justify-between gap-3 rounded-xl border border-zinc-200/80 dark:border-white/[0.07] bg-zinc-50/70 dark:bg-white/[0.025] p-3 text-left transition-all hover:border-indigo-400/50 hover:bg-white dark:hover:bg-white/[0.05] cursor-pointer"
                      >
                        <div className="flex items-center gap-2.5 min-w-0">
                          <ChampAvatar
                            src={c.icon}
                            alt={c.name}
                            className="size-9 rounded-lg border border-zinc-200 dark:border-white/12"
                          />
                          <div className="min-w-0">
                            <div className="truncate text-xs font-semibold text-zinc-900 dark:text-zinc-100">
                              {c.name}
                            </div>
                            <div className="text-[11px] text-zinc-500 dark:text-zinc-400 tabular-nums">
                              {fmtPct(c.win)} WR{c.role ? ` · ${c.role}` : ''}
                            </div>
                          </div>
                        </div>
                        <TierBadge tier={c.tier} size="sm" />
                      </button>
                    ))}
                  </div>
                </GlassCard>
              </div>
              );
            })()}

            {/* VIEW: CHAMPION BUILD & SKILLS */}
            {view === 'Build' && (
              <div className="space-y-5">
                {/* Visual Runes Page Card (Keystone + Primary + Secondary + Stat Shards) */}
                {state.runes?.perkDetails && state.runes.perkDetails.length >= 6 && (
                  <GlassCard surface="hub" spotlight className="p-5">
                    <div className="flex flex-wrap items-center justify-between gap-3 mb-4">
                      <div className="flex items-center gap-3">
                        <div className="flex size-8 items-center justify-center rounded-xl bg-amber-500/12 text-amber-600 dark:text-amber-300">
                          <Wand2 className="size-4" />
                        </div>
                        <div>
                          <div className="text-sm font-semibold text-zinc-900 dark:text-zinc-50">
                            Recommended Runes
                          </div>
                          <div className="text-[11px] text-zinc-500 dark:text-zinc-400">
                            {state.runes.primaryMeta?.name || 'Primary'} +{' '}
                            {state.runes.secondaryMeta?.name || 'Secondary'} · {state.runes.source}
                          </div>
                        </div>
                      </div>
                      <Button size="xs" variant="secondary" onClick={() => postAction('importRunes')}>
                        <Wand2 className="size-3" />
                        Import to League
                      </Button>
                    </div>

                    <div className="grid grid-cols-1 md:grid-cols-[1.35fr_1fr_0.9fr] gap-4">
                      {/* Primary Tree */}
                      <div className="rounded-xl border border-zinc-200/80 dark:border-white/[0.07] bg-zinc-50/70 dark:bg-white/[0.025] p-4">
                        <div className="flex items-center gap-2 mb-3">
                          {state.runes.primaryMeta?.icon && (
                            <img src={state.runes.primaryMeta.icon} alt="" className="size-4" />
                          )}
                          <span className="text-[10px] font-bold uppercase tracking-wider text-zinc-500 dark:text-zinc-400">
                            {state.runes.primaryMeta?.name || 'Primary'}
                          </span>
                        </div>
                        <div className="grid grid-cols-2 gap-2.5">
                          {state.runes.perkDetails.slice(0, 4).map((rk, i) => (
                            <div key={`${rk.id}-${i}`} className="flex items-center gap-2 min-w-0">
                              <div
                                className={cn(
                                  'flex items-center justify-center rounded-full border bg-zinc-900 shrink-0',
                                  i === 0
                                    ? 'size-9 border-amber-400/75 p-0.5 shadow-sm'
                                    : 'size-7 border-zinc-300 dark:border-white/15 p-0.5'
                                )}
                              >
                                {rk.icon && (
                                  <img src={rk.icon} alt={rk.name} className="size-full rounded-full object-contain" />
                                )}
                              </div>
                              <div className="min-w-0">
                                <div className={cn('truncate text-xs', i === 0 ? 'font-bold text-zinc-900 dark:text-white' : 'font-medium text-zinc-700 dark:text-zinc-200')}>
                                  {rk.name}
                                </div>
                                {i === 0 && (
                                  <div className="text-[9px] font-semibold uppercase text-amber-600 dark:text-amber-300">
                                    Keystone
                                  </div>
                                )}
                              </div>
                            </div>
                          ))}
                        </div>
                      </div>

                      {/* Secondary Tree */}
                      <div className="rounded-xl border border-zinc-200/80 dark:border-white/[0.07] bg-zinc-50/70 dark:bg-white/[0.025] p-4">
                        <div className="flex items-center gap-2 mb-3">
                          {state.runes.secondaryMeta?.icon && (
                            <img src={state.runes.secondaryMeta.icon} alt="" className="size-4" />
                          )}
                          <span className="text-[10px] font-bold uppercase tracking-wider text-zinc-500 dark:text-zinc-400">
                            {state.runes.secondaryMeta?.name || 'Secondary'}
                          </span>
                        </div>
                        <div className="space-y-2.5">
                          {state.runes.perkDetails.slice(4, 6).map((rk, i) => (
                            <div key={`${rk.id}-${i}`} className="flex items-center gap-2.5 min-w-0">
                              <div className="flex size-7 items-center justify-center rounded-full border border-zinc-300 dark:border-white/15 bg-zinc-900 p-0.5 shrink-0">
                                {rk.icon && (
                                  <img src={rk.icon} alt={rk.name} className="size-full rounded-full object-contain" />
                                )}
                              </div>
                              <span className="truncate text-xs font-medium text-zinc-700 dark:text-zinc-200">
                                {rk.name}
                              </span>
                            </div>
                          ))}
                        </div>
                      </div>

                      {/* Stat Shards */}
                      <div className="rounded-xl border border-zinc-200/80 dark:border-white/[0.07] bg-zinc-50/70 dark:bg-white/[0.025] p-4">
                        <div className="text-[10px] font-bold uppercase tracking-wider text-zinc-500 dark:text-zinc-400 mb-3">
                          Stat Shards
                        </div>
                        <div className="space-y-2">
                          {state.runes.perkDetails.slice(6, 9).map((rk, i) => (
                            <div key={`${rk.id}-${i}`} className="flex items-center gap-2 min-w-0">
                              <div className="flex size-5 items-center justify-center rounded-full border border-indigo-400/45 bg-zinc-900 p-0.5 shrink-0">
                                {rk.icon && (
                                  <img src={rk.icon} alt={rk.name} className="size-full rounded-full object-contain" />
                                )}
                              </div>
                              <span className="truncate text-[11px] font-medium text-zinc-700 dark:text-zinc-300">
                                {rk.name}
                              </span>
                            </div>
                          ))}
                        </div>
                      </div>
                    </div>
                  </GlassCard>
                )}

                {/* Skill Level-Up Order */}
                {state.skills && state.skills.levels.length > 0 && (
                  <SkillOrderCard
                    priority={state.skills.priority}
                    levels={state.skills.levels}
                    source={state.skills.source}
                    activeLevel={game.live.activeLevel || 0}
                  />
                )}

                {/* Build Path + Focus */}
                <GlassCard surface="hub" spotlight className="p-5 space-y-4">
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div>
                      <h2 className="text-sm font-semibold text-zinc-900 dark:text-white">
                        Recommended Build Path
                      </h2>
                      <p className="text-xs text-zinc-500 dark:text-zinc-400 mt-0.5">
                        {state.buildSummary}
                      </p>
                    </div>
                    <div className="flex flex-wrap items-center gap-1.5">
                      {FOCUS_OPTIONS.map((f) => {
                        const active = state.settings.buildFocus === f.value;
                        return (
                          <button
                            key={f.value}
                            type="button"
                            onClick={() => postAction('setBuildFocus', { focus: f.value })}
                            className={cn(
                              'rounded-lg border px-2.5 py-1 text-xs font-medium transition-colors cursor-pointer',
                              active
                                ? 'border-zinc-900 bg-zinc-900 text-white dark:border-white dark:bg-white dark:text-zinc-950'
                                : 'border-zinc-200 dark:border-white/10 bg-white dark:bg-white/[0.03] text-zinc-600 dark:text-zinc-300 hover:bg-zinc-50 dark:hover:bg-white/[0.07]'
                            )}
                          >
                            {f.label}
                          </button>
                        );
                      })}
                    </div>
                  </div>

                  {/* Build Sequence Grid */}
                  <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
                    {state.buildPath.map((step, idx) => (
                      <button
                        key={`${step.stat.id}-${idx}`}
                        type="button"
                        onClick={() => postAction('inspectItem', { id: step.stat.id })}
                        className={cn(
                          'flex flex-col justify-between rounded-xl border p-4 text-left transition-all cursor-pointer',
                          idx === 0
                            ? 'border-indigo-400/60 bg-indigo-50/50 dark:bg-indigo-500/[0.09] hover:border-indigo-400'
                            : 'border-zinc-200/80 dark:border-white/[0.08] bg-zinc-50/50 dark:bg-white/[0.025] hover:border-zinc-300 dark:hover:border-white/20'
                        )}
                      >
                        <div className="flex items-center justify-between gap-2 mb-2.5">
                          <div className="flex items-center gap-1.5 min-w-0">
                            <span
                              className={cn(
                                'inline-flex items-center rounded-md px-2.5 py-1 text-[10px] font-bold uppercase tracking-wider leading-none shrink-0',
                                step.role === 'STARTER'
                                  ? 'bg-amber-500 text-zinc-950'
                                  : idx === 0
                                    ? 'bg-indigo-600 text-white'
                                    : 'bg-zinc-200/80 dark:bg-white/[0.08] text-zinc-600 dark:text-zinc-300'
                              )}
                            >
                              {step.role}
                            </span>
                            {step.synergyTag && (
                              <span
                                className={cn(
                                  'inline-flex items-center truncate rounded-md border px-2 py-1 text-[9px] font-bold uppercase tracking-wide leading-none',
                                  step.synergyTone === 'rose'
                                    ? 'border-rose-400/45 bg-rose-500/15 text-rose-600 dark:text-rose-300'
                                    : step.synergyTone === 'gold'
                                      ? 'border-amber-400/45 bg-amber-500/15 text-amber-700 dark:text-amber-300'
                                      : step.synergyTone === 'sky'
                                        ? 'border-sky-400/45 bg-sky-500/15 text-sky-700 dark:text-sky-300'
                                        : step.synergyTone === 'teal'
                                          ? 'border-teal-400/45 bg-teal-500/15 text-teal-700 dark:text-teal-300'
                                          : 'border-indigo-400/45 bg-indigo-500/15 text-indigo-700 dark:text-indigo-300'
                                )}
                              >
                                {step.synergyTag}
                              </span>
                            )}
                          </div>
                          <span className="text-[11px] font-semibold text-zinc-500 dark:text-zinc-400 tabular-nums shrink-0">
                            {fmtGold(step.totalGold)}
                          </span>
                        </div>
                        <div className="flex items-center gap-3">
                          <img
                            src={iconUrl(step.stat.icon)}
                            alt={step.stat.name}
                            className="size-11 rounded-xl border border-zinc-200 dark:border-white/12 bg-zinc-900 shrink-0"
                          />
                          <div className="min-w-0 flex-1">
                            <div className="truncate text-[13px] font-bold text-zinc-900 dark:text-white">
                              {step.stat.name}
                            </div>
                            <div className="mt-0.5 flex items-center gap-2 text-xs tabular-nums">
                              <span className="font-bold text-indigo-600 dark:text-indigo-300">
                                {fmtPct(step.stat.rate)} WR
                              </span>
                              {parsePct(step.stat.pick) && (
                                <span className="text-zinc-500 dark:text-zinc-400">
                                  {parsePct(step.stat.pick)} PR
                                </span>
                              )}
                            </div>
                          </div>
                        </div>
                        <div className="mt-2.5 text-[11px] text-zinc-500 dark:text-zinc-400 line-clamp-1">
                          {step.reason}
                        </div>
                      </button>
                    ))}
                  </div>
                </GlassCard>

                {/* Starters / Prismatic Items / Boots / Spells / Situational */}
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                  {(isArena
                    ? [
                        { title: 'Prismatic Items', items: (state.prismaticItems || []).slice(0, 5), inspectable: true },
                        { title: 'Boots', items: state.boots.slice(0, 4), inspectable: true },
                        { title: 'Situational Items', items: state.bestItems.slice(0, 5), inspectable: true },
                      ]
                    : [
                        { title: 'Starters', items: state.starters.slice(0, 4), inspectable: true },
                        { title: 'Boots', items: state.boots.slice(0, 4), inspectable: true },
                        { title: 'Summoner Spells', items: state.spells.slice(0, 4), inspectable: false },
                      ]
                  ).map((group) => (
                    <GlassCard key={group.title} surface="hub" className="p-4">
                      <div className="flex items-center justify-between gap-2 mb-3">
                        <h3 className="text-xs font-semibold uppercase tracking-wider text-zinc-400">
                          {group.title}
                        </h3>
                        {group.title === 'Summoner Spells' && (
                          <div className="inline-flex items-center gap-1 text-[10px]">
                            <span className="text-zinc-400">Flash</span>
                            <div className="inline-flex rounded-md border border-zinc-200 dark:border-white/10 bg-zinc-100 dark:bg-white/[0.04] p-0.5">
                              {(['D', 'F'] as const).map((k) => (
                                <button
                                  key={k}
                                  type="button"
                                  onClick={() => postAction('setFlashKey', { key: k })}
                                  className={cn(
                                    'w-5 rounded py-0.5 text-[10px] font-bold transition-colors cursor-pointer',
                                    state.settings.flashKey === k
                                      ? 'bg-indigo-600 text-white'
                                      : 'text-zinc-500 dark:text-zinc-400 hover:text-zinc-900 dark:hover:text-white'
                                  )}
                                >
                                  {k}
                                </button>
                              ))}
                            </div>
                          </div>
                        )}
                      </div>
                      <div className="space-y-2">
                        {group.items.length === 0 ? (
                          <div className="text-xs text-zinc-400 py-2">No data for this mode</div>
                        ) : (
                          group.items.map((st) => (
                            <div
                              key={st.name}
                              onClick={() => group.inspectable && st.id > 0 && postAction('inspectItem', { id: st.id })}
                              className={cn(
                                'flex items-center justify-between gap-2.5 rounded-xl border border-zinc-200/70 dark:border-white/[0.06] bg-zinc-50/70 dark:bg-white/[0.025] px-3 py-2.5',
                                group.inspectable && st.id > 0 && 'cursor-pointer hover:border-indigo-400/50'
                              )}
                            >
                              <div className="flex items-center gap-2.5 min-w-0">
                                {st.icon && (
                                  <img
                                    src={iconUrl(st.icon)}
                                    alt={st.name}
                                    className="size-7 rounded-lg border border-zinc-200 dark:border-white/12 bg-zinc-900 shrink-0"
                                  />
                                )}
                                <div className="min-w-0">
                                  <div className="truncate text-xs font-semibold text-zinc-900 dark:text-zinc-100">
                                    {st.name}
                                  </div>
                                  {parsePct(st.pick) && (
                                    <div className="text-[10px] text-zinc-500 tabular-nums">
                                      PR {parsePct(st.pick)}
                                    </div>
                                  )}
                                </div>
                              </div>
                              <span className="text-xs font-bold text-indigo-600 dark:text-indigo-300 tabular-nums shrink-0">
                                {fmtPct(st.rate)}
                              </span>
                            </div>
                          ))
                        )}
                      </div>
                    </GlassCard>
                  ))}
                </div>

                {/* Early / Mid / Late Game Scaling Curve */}
                {state.gameLengths && state.gameLengths.length >= 2 && (
                  <GameLengthScalingCard points={state.gameLengths} />
                )}

                {/* Clickable Top Augments Preview (Mayhem & Arena!) */}
                {hasAugments && state.allAugments.length > 0 && (
                  <GlassCard surface="hub" className="p-5">
                    <div className="flex items-center justify-between mb-3.5">
                      <div>
                        <h3 className="text-sm font-semibold text-zinc-900 dark:text-white">
                          Best Augments for {selected?.name}
                        </h3>
                        <p className="text-xs text-zinc-500 dark:text-zinc-400">
                          Click any augment to view full description, combos, and preferences
                        </p>
                      </div>
                      <Button
                        size="xs"
                        variant="secondary"
                        onClick={() => postAction('setView', { view: 'Augments' })}
                      >
                        Browse All ({state.allAugments.length})
                      </Button>
                    </div>
                    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-2.5">
                      {state.allAugments.slice(0, 6).map(renderAugmentTile)}
                    </div>
                  </GlassCard>
                )}
              </div>
            )}

            {/* VIEW: AUGMENTS */}
            {view === 'Augments' && (
              <div className="space-y-5">
                {/* Live OCR Controls */}
                <GlassCard surface="hub" className="p-4">
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div className="flex items-center gap-3">
                      <div className="flex size-9 items-center justify-center rounded-xl bg-indigo-500/12 text-indigo-600 dark:text-indigo-400">
                        <ScanSearch className="size-4" />
                      </div>
                      <div>
                        <div className="text-xs font-semibold text-zinc-900 dark:text-white">
                          Augment OCR Engine{' '}
                          {state.ocrLastMs > 0 && (
                            <span className="ml-1 font-normal text-zinc-400 tabular-nums">
                              ({state.ocrLastMs}ms)
                            </span>
                          )}
                        </div>
                        <div className="text-[11px] text-zinc-500 dark:text-zinc-400">
                          {state.ocrStatus || 'Ready to detect augment cards automatically in-game'}
                        </div>
                      </div>
                    </div>
                    <div className="flex flex-wrap items-center gap-2">
                      <Button size="xs" variant="accent" onClick={() => postAction('scanAugments')}>
                        <ScanSearch className="size-3.5" />
                        Scan Now
                      </Button>
                      <Button size="xs" variant="secondary" onClick={() => postAction('showAugmentBadges')}>
                        Show Badges
                      </Button>
                      <Button size="xs" variant="secondary" onClick={() => postAction('clearAugments')}>
                        Clear
                      </Button>
                      <Button size="xs" variant="ghost" onClick={() => postAction('saveLastScan')}>
                        <Bug className="size-3.5" />
                        Debug
                      </Button>
                    </div>
                  </div>
                </GlassCard>

                {/* Tracked Augments */}
                {state.trackedAugments.length > 0 && (
                  <GlassCard surface="hub" highlight="accent" className="p-4">
                    <div className="text-xs font-semibold uppercase tracking-wider text-indigo-600 dark:text-indigo-300 mb-3">
                      Tracked Augments ({state.trackedAugments.length})
                    </div>
                    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-2.5">
                      {state.trackedAugments.map(renderAugmentTile)}
                    </div>
                  </GlassCard>
                )}

                {/* Search & Rarity Filter */}
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div className="relative flex-1 min-w-[220px] max-w-sm">
                    <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-zinc-400" />
                    <input
                      type="text"
                      value={augSearch}
                      onChange={(e) => setAugSearch(e.target.value)}
                      placeholder="Search augments..."
                      className="w-full rounded-xl border border-zinc-200 dark:border-white/10 bg-white dark:bg-[#14151B] py-2 pl-9 pr-3 text-xs text-zinc-900 dark:text-white placeholder:text-zinc-400 focus:border-indigo-400 focus:outline-none"
                    />
                  </div>
                  <SegmentedTabs
                    size="sm"
                    value={augRarity}
                    onChange={setAugRarity}
                    options={[
                      { value: 'All', label: 'All' },
                      { value: 'Prismatic', label: 'Prismatic' },
                      { value: 'Gold', label: 'Gold' },
                      { value: 'Silver', label: 'Silver' },
                    ]}
                  />
                </div>

                {/* Clickable Augment Catalog */}
                {!selected ? (
                  <GlassCard surface="hub" className="p-10 text-center space-y-3 swiss-grid-bg">
                    <SwissDraftEmptyIllustration variant="augments" />
                    <h3 className="font-display text-base font-bold text-zinc-900 dark:text-white">
                      Choose a Champion First
                    </h3>
                    <p className="mx-auto max-w-sm text-xs leading-relaxed text-zinc-500 dark:text-zinc-400">
                      Select any champion to inspect champion-specific {isArena ? 'Arena 2v2' : 'ARAM Mayhem'} augment win rates, ratings, and best synergies.
                    </p>
                    <Button size="sm" variant="primary" onClick={() => postAction('setView', { view: 'Library' })}>
                      Browse Champions
                    </Button>
                  </GlassCard>
                ) : filteredAugments.length === 0 ? (
                  <GlassCard surface="hub" className="p-10 text-center text-sm text-zinc-500 dark:text-zinc-400">
                    No augments match your current search or rarity filter.
                  </GlassCard>
                ) : (
                  <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-2.5">
                    {filteredAugments.map(renderAugmentTile)}
                  </div>
                )}
              </div>
            )}

            {/* VIEW: LIVE GAME (Pro Scouting) */}
            {view === 'Live' && (() => {
              const laneOpp = game.live.players.find((p) => p.isLaneOpponent) ?? null;
              return (
              <div className="space-y-4">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div>
                    <h2 className="text-base font-bold text-zinc-900 dark:text-white">
                      Live Match Scouting
                    </h2>
                    <p className="text-xs text-zinc-500 dark:text-zinc-400">
                      {game.inGame
                        ? `${game.mode} · ${game.live.clock || '0:00'} · Live KDA, CS/min, items & public objective timers`
                        : 'Waiting for an active League of Legends match'}
                    </p>
                  </div>
                  <SegmentedTabs<'Overview' | 'Your team' | 'Opponents'>
                    size="sm"
                    value={liveFilter}
                    onChange={setLiveFilter}
                    options={[
                      { value: 'Overview', label: 'All Players' },
                      { value: 'Your team', label: 'Your Team' },
                      { value: 'Opponents', label: 'Opponents' },
                    ]}
                  />
                </div>

                {/* Lane Matchup + Dragon Score Banner */}
                {(laneOpp || (game.live.blueDragons || 0) > 0 || (game.live.redDragons || 0) > 0) && (
                  <GlassCard surface="hub" className="px-4 py-3 flex flex-wrap items-center justify-between gap-3 text-xs">
                    <div className="flex items-center gap-2.5">
                      {laneOpp && (
                        <>
                          <span className="rounded-md bg-amber-500/15 px-2 py-0.5 text-[10px] font-bold uppercase text-amber-600 dark:text-amber-300">
                            Lane Matchup
                          </span>
                          <span className="font-semibold text-zinc-900 dark:text-white">
                            {selected?.name} vs {laneOpp.championName}
                          </span>
                          {laneOpp.matchupWr ? (
                            <span
                              className={cn(
                                'rounded-md px-2 py-0.5 text-[11px] font-bold tabular-nums',
                                laneOpp.matchupWr >= 50
                                  ? 'bg-teal-500/15 text-teal-600 dark:text-teal-300'
                                  : 'bg-rose-500/15 text-rose-600 dark:text-rose-300'
                              )}
                            >
                              {fmtPct(laneOpp.matchupWr)} WR
                            </span>
                          ) : null}
                        </>
                      )}
                    </div>
                    {((game.live.blueDragons || 0) > 0 || (game.live.redDragons || 0) > 0) && (
                      <div className="flex items-center gap-2 text-xs tabular-nums">
                        <span className="font-bold text-sky-600 dark:text-sky-300">Blue {game.live.blueDragons || 0}</span>
                        <span className="text-zinc-400">·</span>
                        <span className="font-bold text-rose-600 dark:text-rose-300">Red {game.live.redDragons || 0}</span>
                        {game.live.lastDragonType && (
                          <span className="rounded-md bg-amber-500/15 px-2 py-0.5 text-[10px] font-bold text-amber-600 dark:text-amber-300">
                            {game.live.lastDragonType}
                          </span>
                        )}
                      </div>
                    )}
                  </GlassCard>
                )}

                {game.live.objectives.length > 0 && (
                  <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
                    {game.live.objectives.map((obj) => (
                      <GlassCard
                        key={obj.key}
                        surface="hub"
                        highlight={obj.alive ? 'gold' : 'none'}
                        className="p-3.5 flex items-center justify-between"
                      >
                        <div className="flex items-center gap-2">
                          <Timer className="size-4 text-amber-500" />
                          <span className="text-xs font-semibold">{obj.name}</span>
                        </div>
                        <span className="text-xs font-bold tabular-nums text-indigo-600 dark:text-indigo-300">
                          {obj.status}
                        </span>
                      </GlassCard>
                    ))}
                  </div>
                )}

                <GlassCard surface="hub" className="overflow-hidden">
                  {displayedPlayers.length === 0 ? (
                    <div className="p-10 text-center space-y-3 swiss-grid-bg">
                      <SwissRiftLiveIllustration />
                      <h3 className="font-display text-base font-bold text-zinc-900 dark:text-white">
                        Standing By for Live Match
                      </h3>
                      <p className="mx-auto max-w-md text-xs leading-relaxed text-zinc-500 dark:text-zinc-400">
                        Start any League match to automatically populate live KDA, CS/min benchmarks, player keystones, items, and public objective timers.
                      </p>
                    </div>
                  ) : (
                    <div className="divide-y divide-zinc-200/70 dark:divide-white/[0.06]">
                      <div className="grid grid-cols-[minmax(200px,1.4fr)_82px_92px_104px_230px] items-center gap-3 px-4 py-2.5 text-[10px] font-semibold uppercase tracking-wider text-zinc-400 bg-zinc-50/70 dark:bg-white/[0.02]">
                        <div>Player & Keystone</div>
                        <div className="text-center">Spells</div>
                        <div className="text-center">K / D / A</div>
                        <div className="text-right">CS (CS/m)</div>
                        <div className="text-right">Items</div>
                      </div>
                      {displayedPlayers.map((p) => {
                        return (
                        <div
                          key={p.key}
                          className={cn(
                            'grid grid-cols-[minmax(200px,1.4fr)_82px_92px_104px_230px] items-center gap-3 px-4 py-3',
                            p.me
                              ? 'bg-indigo-50/60 dark:bg-indigo-500/[0.08]'
                              : p.isLaneOpponent
                                ? 'bg-amber-50/50 dark:bg-amber-500/[0.06]'
                                : ''
                          )}
                        >
                          <div className="flex items-center gap-3 min-w-0">
                            {p.championIcon && (
                              <ChampAvatar
                                src={p.championIcon}
                                alt={p.championName}
                                className="size-8 rounded-lg border border-zinc-200 dark:border-white/12"
                              />
                            )}
                            <div className="min-w-0">
                              <div className="flex items-center gap-1.5 text-xs font-semibold text-zinc-900 dark:text-white">
                                <span className="truncate">{p.championName}</span>
                                <span className="text-[10px] font-normal text-zinc-400">Lv{p.level}</span>
                                {p.isLaneOpponent && (
                                  <span className="rounded bg-amber-500/20 px-1.5 py-0.5 text-[9px] font-bold uppercase leading-none text-amber-600 dark:text-amber-300">
                                    VS
                                  </span>
                                )}
                                {(p.tags || []).slice(0, 1).map((t) => (
                                  <span
                                    key={t}
                                    className={cn(
                                      'rounded px-1.5 py-0.5 text-[9px] font-bold uppercase leading-none',
                                      t === 'Fed'
                                        ? 'bg-rose-500/18 text-rose-600 dark:text-rose-300'
                                        : 'bg-teal-500/18 text-teal-600 dark:text-teal-300'
                                    )}
                                  >
                                    {t}
                                  </span>
                                ))}
                              </div>
                              <div className="truncate text-[11px] text-zinc-500 dark:text-zinc-400">
                                {p.keystone ? `${p.keystone} · ` : ''}
                                {state.privateMode && !p.me ? 'Player' : p.name}
                              </div>
                            </div>
                          </div>
                          <div className="flex items-center justify-center gap-1">
                            {[
                              { slot: 1, name: p.spell1, key: p.spellKey1 },
                              { slot: 2, name: p.spell2, key: p.spellKey2 },
                            ].map((sp) =>
                              sp.key ? (
                                <div
                                  key={sp.slot}
                                  title={sp.name}
                                  className="relative flex size-6 items-center justify-center rounded border border-zinc-200 dark:border-white/15 bg-zinc-900 overflow-hidden"
                                >
                                  <img
                                    src={spellIconUrl(sp.key, state.ddVersion)}
                                    alt={sp.name}
                                    className="size-full object-cover"
                                  />
                                </div>
                              ) : null
                            )}
                          </div>
                          <div className="text-center text-xs font-bold tabular-nums">
                            {p.kills} / {p.deaths} / {p.assists}
                          </div>
                          <div className="text-right text-xs tabular-nums text-zinc-600 dark:text-zinc-300">
                            {p.cs}{' '}
                            <span className="text-zinc-400">
                              ({(game.live.seconds || 0) > 20 ? (p.cs / liveMinutes).toFixed(1) : '0.0'})
                            </span>
                          </div>
                          <div className="flex items-center justify-end gap-1">
                            {p.items.slice(0, 6).map((id, i) => (
                              <img
                                key={`${id}-${i}`}
                                src={itemIconUrl(id, state.ddVersion)}
                                alt=""
                                className="size-6 rounded border border-zinc-200 dark:border-white/12 bg-zinc-900"
                              />
                            ))}
                          </div>
                        </div>
                        );
                      })}
                    </div>
                  )}
                </GlassCard>
              </div>
              );
            })()}

            {/* VIEW: MATCH HISTORY (MODE-SCOPED & STRICT CSS GRID ALIGNMENT) */}
            {view === 'History' && (
              <div className="space-y-5">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div className="flex items-center gap-2.5">
                    <h2 className="font-display text-lg font-bold tracking-tight text-zinc-900 dark:text-white">
                      Match History
                    </h2>
                    <span className="rounded-md border border-indigo-400/25 bg-indigo-500/10 px-2 py-0.5 font-mono-tech text-[10px] font-semibold text-indigo-600 dark:text-indigo-300">
                      {activeModeLabel}
                    </span>
                  </div>
                  <div className="flex flex-wrap items-center gap-2">
                    <div className="relative w-48">
                      <Search className="pointer-events-none absolute left-2.5 top-1/2 size-3.5 -translate-y-1/2 text-zinc-400" />
                      <input
                        type="text"
                        value={historySearch}
                        onChange={(e) => setHistorySearch(e.target.value)}
                        placeholder="Filter champion..."
                        className="w-full rounded-xl border border-zinc-200 dark:border-white/10 bg-white dark:bg-[#14151B] py-1.5 pl-8 pr-2.5 text-xs text-zinc-900 dark:text-white placeholder:text-zinc-400 focus:border-indigo-400 focus:outline-none"
                      />
                    </div>
                    <Button size="sm" variant="secondary" onClick={() => postAction('refreshProfile')}>
                      <RefreshCw className="size-3.5" />
                      Refresh
                    </Button>
                  </div>
                </div>

                {/* History Summary KPIs */}
                <div className="grid grid-cols-2 sm:grid-cols-4 gap-3.5">
                  {[
                    { label: 'Matches', value: String(historyStats.games) },
                    { label: 'Win Rate', value: historyStats.games ? fmtPct(historyStats.wr) : '—' },
                    { label: 'Avg KDA', value: historyStats.games ? `${historyStats.kda}:1` : '—' },
                    { label: 'Avg CS / min', value: historyStats.games ? historyStats.avgCsMin : '—' },
                  ].map((s) => (
                    <GlassCard key={s.label} surface="hub" className="px-4 py-3.5 text-center">
                      <div className="text-[10px] font-semibold uppercase tracking-wider text-zinc-400">
                        {s.label}
                      </div>
                      <div className="mt-1 text-lg font-bold tabular-nums text-zinc-900 dark:text-white">
                        {s.value}
                      </div>
                    </GlassCard>
                  ))}
                </div>

                {/* Personal Top Played Champions Pool */}
                {topPlayedChampions.length > 0 && (
                  <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
                    {topPlayedChampions.map((ch) => {
                      const wr = (ch.wins / Math.max(1, ch.games)) * 100;
                      const kda = ((ch.k + ch.a) / Math.max(1, ch.d)).toFixed(2);
                      return (
                        <GlassCard
                          key={ch.name}
                          surface="hub"
                          onClick={() => postAction('selectChampion', { slug: ch.name, view: 'Build' })}
                          className="p-3.5 flex items-center gap-3 cursor-pointer hover:border-indigo-400/50 transition-colors"
                        >
                          {ch.icon && (
                            <ChampAvatar
                              src={ch.icon}
                              alt={ch.name}
                              className="size-10 rounded-xl border border-zinc-200 dark:border-white/12"
                            />
                          )}
                          <div className="min-w-0 flex-1">
                            <div className="flex items-center justify-between gap-1">
                              <span className="truncate text-xs font-bold text-zinc-900 dark:text-white">
                                {ch.name}
                              </span>
                              <span className="text-[10px] text-zinc-400 tabular-nums shrink-0">
                                {ch.games}G
                              </span>
                            </div>
                            <div className="mt-0.5 flex items-center gap-2 text-[11px] tabular-nums">
                              <span
                                className={cn(
                                  'font-bold',
                                  wr >= 55 ? 'text-indigo-600 dark:text-indigo-300' : 'text-zinc-600 dark:text-zinc-300'
                                )}
                              >
                                {fmtPct(wr)}
                              </span>
                              <span className="text-zinc-400">· {kda} KDA</span>
                            </div>
                          </div>
                        </GlassCard>
                      );
                    })}
                  </div>
                )}

                {/* Strictly Aligned Match Table */}
                <GlassCard surface="hub" className="overflow-hidden">
                  {filteredHistory.length === 0 ? (
                    <div className="p-10 text-center text-sm text-zinc-500 dark:text-zinc-400">
                      {historySearch.trim()
                        ? `No ${activeModeLabel} matches matching "${historySearch}".`
                        : `No recent ${activeModeLabel} matches found yet. Play a game or press Refresh.`}
                    </div>
                  ) : (
                    <div className="overflow-x-auto">
                      <div className="min-w-[760px] divide-y divide-zinc-200/70 dark:divide-white/[0.06]">
                        {/* Header Row */}
                        <div className="grid grid-cols-[minmax(195px,1.5fr)_92px_108px_138px_188px] items-center gap-4 px-5 py-2.5 text-[10px] font-semibold uppercase tracking-wider text-zinc-400 bg-zinc-50/80 dark:bg-white/[0.02]">
                          <div>Champion & Queue</div>
                          <div className="text-center">Result</div>
                          <div className="text-center">KDA</div>
                          <div className="text-right">CS · Gold</div>
                          <div className="text-right">Final Build</div>
                        </div>

                        {filteredHistory.map((m) => {
                          const csMin = (m.cs / Math.max(1, m.duration / 60)).toFixed(1);
                          const kdaRatio = ((m.kills + m.assists) / Math.max(1, m.deaths)).toFixed(2);
                          return (
                            <button
                              key={m.id}
                              type="button"
                              onClick={() => setSelectedMatch(m)}
                              className="grid w-full grid-cols-[minmax(195px,1.5fr)_92px_108px_138px_188px] items-center gap-4 px-5 py-3.5 text-left transition-colors hover:bg-zinc-50 dark:hover:bg-white/[0.03] cursor-pointer"
                            >
                              {/* Col 1: Champion */}
                              <div className="flex items-center gap-3 min-w-0">
                                {m.championIcon ? (
                                  <ChampAvatar
                                    src={m.championIcon}
                                    alt={m.championName}
                                    className="size-10 rounded-xl border border-zinc-200 dark:border-white/12"
                                  />
                                ) : (
                                  <div className="size-10 rounded-xl bg-zinc-200 dark:bg-white/10 shrink-0" />
                                )}
                                <div className="min-w-0">
                                  <div className="truncate text-[13px] font-semibold text-zinc-900 dark:text-white leading-tight">
                                    {m.championName}
                                  </div>
                                  <div className="mt-0.5 truncate text-[11px] text-zinc-500 dark:text-zinc-400">
                                    {m.queueName} · {m.dateText}
                                  </div>
                                  {m.augmentDetails && m.augmentDetails.length > 0 && (
                                    <div className="mt-1 flex items-center gap-1">
                                      {m.augmentDetails.slice(0, 4).map((a) => (
                                        <img
                                          key={a.id}
                                          src={iconUrl(a.icon)}
                                          alt={a.name}
                                          title={a.name}
                                          className="size-4 rounded border border-indigo-400/40 bg-zinc-900"
                                        />
                                      ))}
                                    </div>
                                  )}
                                </div>
                              </div>

                              {/* Col 2: Outcome */}
                              <div className="flex flex-col items-center justify-center">
                                <span
                                  className={cn(
                                    'inline-flex items-center justify-center rounded-full border px-2.5 py-0.5 text-[10px] font-bold uppercase tracking-wide',
                                    m.remake
                                      ? 'border-zinc-300 bg-zinc-100 text-zinc-600 dark:border-white/15 dark:bg-white/10 dark:text-zinc-300'
                                      : m.won
                                        ? 'border-indigo-300 bg-indigo-50 text-indigo-700 dark:border-indigo-400/35 dark:bg-indigo-500/18 dark:text-indigo-300'
                                        : 'border-rose-300 bg-rose-50 text-rose-700 dark:border-rose-400/35 dark:bg-rose-500/18 dark:text-rose-300'
                                  )}
                                >
                                  {m.remake ? 'Remake' : m.won ? 'Victory' : 'Defeat'}
                                </span>
                                <span className="mt-1 text-[10px] text-zinc-400 tabular-nums">
                                  {m.time}
                                </span>
                              </div>

                              {/* Col 3: KDA */}
                              <div className="flex flex-col items-center justify-center tabular-nums">
                                <div className="text-[13px] font-bold text-zinc-900 dark:text-white">
                                  {m.kills} / {m.deaths} / {m.assists}
                                </div>
                                <div className="mt-0.5 text-[10px] text-zinc-500 dark:text-zinc-400">
                                  {kdaRatio} KDA
                                </div>
                              </div>

                              {/* Col 4: CS + Gold */}
                              <div className="flex flex-col items-end justify-center tabular-nums">
                                <div className="text-xs font-semibold text-zinc-800 dark:text-zinc-200">
                                  {m.cs} CS <span className="text-zinc-400">({csMin}/m)</span>
                                </div>
                                <div className="mt-0.5 text-[10px] text-zinc-500 dark:text-zinc-400">
                                  {(m.gold / 1000).toFixed(1)}k gold · {(m.damage / 1000).toFixed(1)}k dmg
                                </div>
                              </div>

                              {/* Col 5: Items */}
                              <div className="flex items-center justify-end gap-1">
                                {Array.from({ length: 6 }, (_, i) => {
                                  const id = m.items[i];
                                  return id ? (
                                    <img
                                      key={`${id}-${i}`}
                                      src={itemIconUrl(id, state.ddVersion)}
                                      alt=""
                                      className="size-7 rounded-lg border border-zinc-200 dark:border-white/12 bg-zinc-900"
                                    />
                                  ) : (
                                    <div
                                      key={i}
                                      className="size-7 rounded-lg border border-dashed border-zinc-200 dark:border-white/[0.08] bg-zinc-100/50 dark:bg-white/[0.02]"
                                    />
                                  );
                                })}
                              </div>
                            </button>
                          );
                        })}
                      </div>
                    </div>
                  )}
                </GlassCard>
              </div>
            )}

            {/* VIEW: CHAMPIONS LIBRARY */}
            {view === 'Library' && (
              <div className="space-y-4">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div className="relative flex-1 min-w-[220px] max-w-sm">
                    <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-zinc-400" />
                    <input
                      type="text"
                      value={champSearch}
                      onChange={(e) => setChampSearch(e.target.value)}
                      placeholder="Search champions..."
                      className="w-full rounded-xl border border-zinc-200 dark:border-white/10 bg-white dark:bg-[#14151B] py-2 pl-9 pr-3 text-xs text-zinc-900 dark:text-white placeholder:text-zinc-400 focus:border-indigo-400 focus:outline-none"
                    />
                  </div>
                  <div className="flex flex-wrap items-center gap-2">
                    {selected && (
                      <Button
                        size="sm"
                        variant="secondary"
                        onClick={() => postAction('setView', { view: 'Build' })}
                      >
                        <ChampAvatar src={selected.icon} alt={selected.name} className="size-4 rounded" />
                        <span>{selected.name}</span>
                        <ChevronRight className="size-3.5" />
                      </Button>
                    )}
                    <SegmentedTabs
                      size="sm"
                      value={tierFilter}
                      onChange={setTierFilter}
                      options={[
                        { value: 'All', label: 'All' },
                        { value: 'S', label: 'S' },
                        { value: 'A', label: 'A' },
                        { value: 'B', label: 'B' },
                        { value: 'C', label: 'C' },
                        { value: 'D', label: 'D' },
                      ]}
                    />
                  </div>
                </div>
                {filteredChampions.length === 0 ? (
                  <GlassCard surface="hub" className="p-10 text-center space-y-2.5 swiss-grid-bg">
                    <SwissDraftEmptyIllustration variant="champions" />
                    <p className="text-sm font-medium text-zinc-500 dark:text-zinc-400">
                      No champions match your search or tier filter.
                    </p>
                  </GlassCard>
                ) : (
                  <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
                    {filteredChampions.map((c) => (
                      <button
                        key={c.slug}
                        type="button"
                        onClick={() => postAction('selectChampion', { slug: c.slug, view: 'Build' })}
                        className="flex items-center justify-between gap-3 rounded-xl border border-zinc-200/80 dark:border-white/[0.08] bg-white dark:bg-[#14151B] p-3 text-left transition-all hover:border-indigo-400/50 cursor-pointer"
                      >
                        <div className="flex items-center gap-3 min-w-0">
                          {c.icon && (
                            <ChampAvatar
                              src={c.icon}
                              alt={c.name}
                              className="size-10 rounded-xl border border-zinc-200 dark:border-white/12"
                            />
                          )}
                          <div className="min-w-0">
                            <div className="flex items-center gap-1.5 min-w-0">
                              <span className="truncate text-[13px] font-semibold text-zinc-900 dark:text-white">
                                {c.name}
                              </span>
                              <PatchTagBadge tag={c.patchTag} compact />
                            </div>
                            <div className="text-[11px] text-zinc-500 dark:text-zinc-400 tabular-nums">
                              {fmtPct(c.win)} WR · {fmtPct(c.pick)} PR
                            </div>
                          </div>
                        </div>
                        <TierBadge tier={c.tier} size="sm" />
                      </button>
                    ))}
                  </div>
                )}
              </div>
            )}

            {/* VIEW: SYNERGIES */}
            {view === 'Synergies' && !selected && (
              <GlassCard surface="hub" className="p-10 text-center space-y-3 swiss-grid-bg">
                <SwissDraftEmptyIllustration variant="synergies" />
                <h3 className="font-display text-base font-bold text-zinc-900 dark:text-white">
                  {isArena ? 'Arena Duo Synergies' : isMayhem ? 'Champion Synergies & Combos' : 'Teammates & Matchups'}
                </h3>
                <p className="mx-auto max-w-sm text-xs leading-relaxed text-zinc-500 dark:text-zinc-400">
                  Select a champion first to inspect {isArena ? 'top 2v2 duo partners and Top-4 synergy rates' : isMayhem ? 'synergies, matchups, and augment combos' : 'teammates and tough lane matchups'}.
                </p>
                <Button size="sm" variant="primary" onClick={() => postAction('setView', { view: 'Library' })}>
                  Browse Champions
                </Button>
              </GlassCard>
            )}
            {view === 'Synergies' && selected && (() => {
              const normKey = (s: string) => s.toLowerCase().replace(/[^a-z0-9]/g, '');
              const champLookup = new Map<string, (typeof state.champions)[number]>();
              for (const c of state.champions) {
                champLookup.set(normKey(c.name), c);
                champLookup.set(normKey(c.slug), c);
                if (c.assetKey) champLookup.set(normKey(c.assetKey), c);
              }
              const augLookup = new Map<string, (typeof state.allAugments)[number]>();
              for (const a of state.allAugments) {
                augLookup.set(normKey(a.name), a);
              }
              const cols = isArena
                ? [
                    {
                      key: 'teammates',
                      title: 'Best Arena Duo Partners',
                      subtitle: 'Highest Top-4 win rate 2v2 partners',
                      kind: 'champion' as const,
                      tone: 'good' as const,
                      list: state.teammates,
                    },
                  ]
                : [
                    {
                      key: 'teammates',
                      title: isMayhem ? 'Best Teammates' : 'Strong Against',
                      subtitle: isMayhem ? 'Highest win rate allies' : 'Favorable lane matchups',
                      kind: 'champion' as const,
                      tone: 'good' as const,
                      list: state.teammates,
                    },
                    {
                      key: 'matchups',
                      title: 'Tough Matchups',
                      subtitle: isMayhem ? 'Hardest enemy picks' : 'Counter picks to watch out for',
                      kind: 'champion' as const,
                      tone: 'tough' as const,
                      list: state.matchups,
                    },
                    ...(isMayhem
                      ? [
                          {
                            key: 'combos',
                            title: 'Augment Combos',
                            subtitle: 'Best 2-augment synergies',
                            kind: 'combo' as const,
                            tone: 'combo' as const,
                            list: state.combos,
                          },
                        ]
                      : []),
                  ];
              return (
                <div className={cn('grid grid-cols-1 gap-4', isArena ? 'lg:grid-cols-1 max-w-2xl' : isMayhem ? 'lg:grid-cols-3' : 'lg:grid-cols-2')}>
                  {cols.map((col) => (
                    <GlassCard key={col.key} surface="hub" className="p-4 flex flex-col">
                      <div className="flex items-center justify-between gap-2 mb-3.5 pb-2.5 border-b border-zinc-200/70 dark:border-white/[0.06]">
                        <div className="flex items-center gap-2 min-w-0">
                          <div
                            className={cn(
                              'flex size-7 items-center justify-center rounded-lg border shrink-0',
                              col.tone === 'good' &&
                                'border-emerald-500/25 bg-emerald-500/10 text-emerald-600 dark:text-emerald-400',
                              col.tone === 'tough' &&
                                'border-rose-500/25 bg-rose-500/10 text-rose-600 dark:text-rose-400',
                              col.tone === 'combo' &&
                                'border-indigo-500/25 bg-indigo-500/10 text-indigo-600 dark:text-indigo-400'
                            )}
                          >
                            {col.tone === 'good' ? (
                              <ThumbsUp className="size-3.5" />
                            ) : col.tone === 'tough' ? (
                              <Swords className="size-3.5" />
                            ) : (
                              <Sparkles className="size-3.5" />
                            )}
                          </div>
                          <div className="min-w-0">
                            <h3 className="text-xs font-bold uppercase tracking-wider text-zinc-800 dark:text-zinc-100 truncate">
                              {col.title}
                            </h3>
                            <p className="text-[11px] text-zinc-500 dark:text-zinc-400 truncate">
                              {col.subtitle}
                            </p>
                          </div>
                        </div>
                        {col.list.length > 0 && (
                          <span className="rounded-full border border-zinc-200/80 dark:border-white/10 bg-zinc-100/80 dark:bg-white/[0.04] px-2 py-0.5 font-mono-tech text-[10px] font-semibold text-zinc-500 dark:text-zinc-400 tabular-nums shrink-0">
                            {Math.min(10, col.list.length)}
                          </span>
                        )}
                      </div>

                      <div className="space-y-2 flex-1">
                        {col.list.length === 0 ? (
                          <div className="flex flex-col items-center justify-center rounded-xl border border-dashed border-zinc-200 dark:border-white/10 py-8 text-center">
                            <span className="text-xs font-medium text-zinc-500 dark:text-zinc-400">
                              No data available yet
                            </span>
                            <span className="mt-1 text-[11px] text-zinc-400 dark:text-zinc-500">
                              Try refreshing or switching mode / role
                            </span>
                          </div>
                        ) : (
                          col.list.slice(0, 10).map((st, idx) => {
                            const wr = st.rate || 0;
                            const barPct = Math.max(8, Math.min(100, ((wr - 35) / 35) * 100));
                            const wrTone =
                              wr >= 52
                                ? 'text-emerald-600 dark:text-emerald-400'
                                : wr < 48 && wr > 0
                                  ? 'text-rose-600 dark:text-rose-400'
                                  : 'text-indigo-600 dark:text-indigo-300';
                            const barTone =
                              wr >= 52
                                ? 'bg-emerald-500/80'
                                : wr < 48 && wr > 0
                                  ? 'bg-rose-500/80'
                                  : 'bg-indigo-500/80';

                            if (col.kind === 'combo') {
                              const parts = st.name.split(/\s*\+\s*/);
                              const aug1 = parts[0] ? augLookup.get(normKey(parts[0])) : undefined;
                              const aug2 = parts[1] ? augLookup.get(normKey(parts[1])) : undefined;
                              const clickableId = aug1?.id || aug2?.id || 0;
                              return (
                                <button
                                  key={`${st.name}-${idx}`}
                                  type="button"
                                  onClick={() => {
                                    if (clickableId > 0) postAction('inspectAugment', { id: clickableId });
                                  }}
                                  className={cn(
                                    'group w-full flex items-center gap-3 rounded-xl border border-zinc-200/70 dark:border-white/[0.06] bg-zinc-50/70 dark:bg-white/[0.025] px-3 py-2 text-left transition-all',
                                    clickableId > 0 &&
                                      'hover:border-indigo-400/50 hover:bg-white dark:hover:bg-white/[0.05] cursor-pointer'
                                  )}
                                >
                                  <div className="flex items-center -space-x-2 shrink-0">
                                    <AugmentIcon
                                      src={aug1?.icon}
                                      alt={parts[0] || st.name}
                                      className="size-8 rounded-lg border border-zinc-200 dark:border-white/15 shadow-sm ring-2 ring-white dark:ring-[#14151B]"
                                      iconClassName="size-3.5"
                                    />
                                    <AugmentIcon
                                      src={aug2?.icon}
                                      alt={parts[1] || st.name}
                                      className="size-8 rounded-lg border border-zinc-200 dark:border-white/15 shadow-sm ring-2 ring-white dark:ring-[#14151B]"
                                      iconClassName="size-3.5"
                                    />
                                  </div>
                                  <div className="min-w-0 flex-1">
                                    <div className="truncate text-xs font-semibold text-zinc-900 dark:text-zinc-100">
                                      {parts[0] || st.name}
                                      {parts[1] && (
                                        <span className="font-normal text-zinc-400 dark:text-zinc-500">
                                          {' + '}
                                        </span>
                                      )}
                                      {parts[1]}
                                    </div>
                                    <div className="mt-1.5 flex items-center gap-2">
                                      <div className="h-1 flex-1 overflow-hidden rounded-full bg-zinc-200/80 dark:bg-white/[0.07]">
                                        <div
                                          className={cn('h-full rounded-full transition-all', barTone)}
                                          style={{ width: `${barPct}%` }}
                                        />
                                      </div>
                                      {(st.pick || st.games > 0) && (
                                        <span className="font-mono-tech text-[10px] text-zinc-400 dark:text-zinc-500 tabular-nums shrink-0">
                                          {st.pick || `${st.games}g`}
                                        </span>
                                      )}
                                    </div>
                                  </div>
                                  <div className="text-right shrink-0">
                                    <div className={cn('text-xs font-bold tabular-nums', wrTone)}>
                                      {fmtPct(wr)}
                                    </div>
                                    <div className="font-mono-tech text-[9px] uppercase tracking-wider text-zinc-400">
                                      WR
                                    </div>
                                  </div>
                                </button>
                              );
                            }

                            const matchedChamp = champLookup.get(normKey(st.name));
                            const avatarSrc = st.icon || matchedChamp?.icon || '';
                            const subLabel =
                              st.pick || (st.games > 0 ? `${st.games.toLocaleString()} games` : '');
                            return (
                              <button
                                key={`${st.name}-${idx}`}
                                type="button"
                                onClick={() => {
                                  if (matchedChamp) {
                                    postAction('selectChampion', { slug: matchedChamp.slug, view: 'Build' });
                                  }
                                }}
                                className={cn(
                                  'group w-full flex items-center gap-3 rounded-xl border border-zinc-200/70 dark:border-white/[0.06] bg-zinc-50/70 dark:bg-white/[0.025] px-3 py-2 text-left transition-all',
                                  matchedChamp &&
                                    'hover:border-indigo-400/50 hover:bg-white dark:hover:bg-white/[0.05] cursor-pointer'
                                )}
                              >
                                <ChampAvatar
                                  src={avatarSrc}
                                  alt={st.name}
                                  className="size-9 rounded-lg border border-zinc-200 dark:border-white/12 shadow-sm"
                                />
                                <div className="min-w-0 flex-1">
                                  <div className="flex items-center justify-between gap-2">
                                    <span className="truncate text-[12.5px] font-semibold text-zinc-900 dark:text-zinc-100 group-hover:text-indigo-600 dark:group-hover:text-indigo-300 transition-colors">
                                      {st.name}
                                    </span>
                                    <span className={cn('text-xs font-bold tabular-nums shrink-0', wrTone)}>
                                      {fmtPct(wr)}
                                    </span>
                                  </div>
                                  <div className="mt-1.5 flex items-center gap-2">
                                    <div className="h-1 flex-1 overflow-hidden rounded-full bg-zinc-200/80 dark:bg-white/[0.07]">
                                      <div
                                        className={cn('h-full rounded-full transition-all', barTone)}
                                        style={{ width: `${barPct}%` }}
                                      />
                                    </div>
                                    {subLabel && (
                                      <span className="font-mono-tech text-[10px] text-zinc-400 dark:text-zinc-500 tabular-nums shrink-0">
                                        {subLabel}
                                      </span>
                                    )}
                                  </div>
                                </div>
                              </button>
                            );
                          })
                        )}
                      </div>
                    </GlassCard>
                  ))}
                </div>
              );
            })()}

            {/* VIEW: PATCH NOTES (MODE-SCOPED, COMPACT & SCANNABLE) */}
            {view === 'PatchNotes' && (() => {
              const pn = state.patchNotes;
              const allEntries = pn?.entries || [];
              const modeKey = state.activeMode || 'SR';
              const modeLabel =
                modeKey === 'MAYHEM'
                  ? 'ARAM Mayhem'
                  : modeKey === 'ARENA'
                    ? 'Arena 2v2'
                    : modeKey === 'ARAM'
                      ? 'ARAM'
                      : 'Ranked SR';
              const modeEntries = allEntries.filter((e) => e.mode === modeKey);

              const allCats = [
                { key: 'champion' as const, label: 'Champions' },
                { key: 'augment' as const, label: 'Augments' },
                { key: 'item' as const, label: 'Items' },
                { key: 'system' as const, label: 'Systems' },
              ]
                .map((c) => ({ ...c, count: modeEntries.filter((e) => e.category === c.key).length }))
                .filter((c) => c.count > 0);

              const activeCat = allCats.some((c) => c.key === patchCat)
                ? patchCat
                : (allCats[0]?.key ?? 'champion');

              const catEntries = modeEntries.filter((e) => e.category === activeCat);
              const buffCount = catEntries.filter((e) => e.changeType === 'buff').length;
              const nerfCount = catEntries.filter((e) => e.changeType === 'nerf').length;
              const otherCount = catEntries.length - buffCount - nerfCount;

              const q = patchSearch.trim().toLowerCase();
              const filtered = catEntries.filter((e) => {
                if (patchFilter === 'buff' && e.changeType !== 'buff') return false;
                if (patchFilter === 'nerf' && e.changeType !== 'nerf') return false;
                if (patchFilter === 'other' && (e.changeType === 'buff' || e.changeType === 'nerf')) return false;
                if (q && !e.title.toLowerCase().includes(q) && !e.summary.toLowerCase().includes(q)) return false;
                return true;
              });

              const groups = [
                {
                  key: 'buff',
                  label: 'Buffs',
                  dot: 'bg-emerald-500',
                  text: 'text-emerald-600 dark:text-emerald-400',
                  accent: 'border-l-emerald-500/70',
                  valTone: 'text-emerald-600 dark:text-emerald-300',
                  items: filtered.filter((e) => e.changeType === 'buff'),
                },
                {
                  key: 'nerf',
                  label: 'Nerfs',
                  dot: 'bg-rose-500',
                  text: 'text-rose-600 dark:text-rose-400',
                  accent: 'border-l-rose-500/70',
                  valTone: 'text-rose-600 dark:text-rose-300',
                  items: filtered.filter((e) => e.changeType === 'nerf'),
                },
                {
                  key: 'other',
                  label: activeCat === 'system' ? 'Updates & Systems' : 'New & Adjusted',
                  dot: 'bg-indigo-500',
                  text: 'text-indigo-600 dark:text-indigo-400',
                  accent: 'border-l-indigo-500/70',
                  valTone: 'text-indigo-600 dark:text-indigo-300',
                  items: filtered.filter((e) => e.changeType !== 'buff' && e.changeType !== 'nerf'),
                },
              ].filter((g) => g.items.length > 0);

              const autoExpand = filtered.length <= 3;
              const toggleFilter = (next: 'buff' | 'nerf' | 'other') =>
                setPatchFilter((prev) => (prev === next ? 'all' : next));

              const renderDetailText = (text: string, valTone: string) => {
                const arrowIdx = text.indexOf('→');
                const colonIdx = text.indexOf(':');
                if (arrowIdx > 0) {
                  const beforeArrow = text.slice(0, arrowIdx).trimEnd();
                  const afterArrow = text.slice(arrowIdx + 1).trimStart();
                  if (colonIdx > 0 && colonIdx < arrowIdx) {
                    const label = beforeArrow.slice(0, colonIdx + 1);
                    const oldPart = beforeArrow.slice(colonIdx + 1).trim();
                    return (
                      <>
                        <span className="font-medium text-zinc-700 dark:text-zinc-200">{label}</span>{' '}
                        <span className="text-zinc-400 dark:text-zinc-500">{oldPart}</span>{' '}
                        <span className={cn('font-semibold', valTone)}>→ {afterArrow}</span>
                      </>
                    );
                  }
                  return (
                    <>
                      <span className="text-zinc-500 dark:text-zinc-400">{beforeArrow}</span>{' '}
                      <span className={cn('font-semibold', valTone)}>→ {afterArrow}</span>
                    </>
                  );
                }
                return <span>{text}</span>;
              };

              return (
                <div className="space-y-4">
                  {/* Minimal Mode-Scoped Header */}
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div className="flex items-center gap-2.5">
                      <h2 className="font-display text-lg font-bold tracking-tight text-zinc-900 dark:text-white">
                        Patch {pn?.version || state.ddVersion || 'Latest'}
                      </h2>
                      <span className="rounded-md border border-indigo-400/25 bg-indigo-500/10 px-2 py-0.5 font-mono-tech text-[10px] font-semibold text-indigo-600 dark:text-indigo-300">
                        {modeLabel}
                      </span>
                    </div>
                    {modeEntries.length > 4 && (
                      <div className="relative w-56">
                        <Search className="pointer-events-none absolute left-2.5 top-1/2 -translate-y-1/2 size-3.5 text-zinc-400" />
                        <input
                          value={patchSearch}
                          onChange={(e) => setPatchSearch(e.target.value)}
                          placeholder="Filter changes…"
                          className="w-full rounded-lg border border-zinc-200 dark:border-white/10 bg-white dark:bg-[#14151B] pl-8 pr-3 py-1.5 text-xs text-zinc-900 dark:text-white placeholder:text-zinc-400 focus:border-indigo-400 focus:outline-none"
                        />
                      </div>
                    )}
                  </div>

                  {/* Single Clean Toolbar: Active-Mode Categories + Clickable Balance Filters */}
                  {(allCats.length > 1 || catEntries.length > 1) && (
                    <div className="flex flex-wrap items-center justify-between gap-2 border-b border-zinc-200/70 dark:border-white/[0.06] pb-3">
                      <div className="flex items-center gap-1">
                        {allCats.length > 1 &&
                          allCats.map((cat) => {
                            const active = activeCat === cat.key;
                            return (
                              <button
                                key={cat.key}
                                type="button"
                                onClick={() => {
                                  setPatchCat(cat.key);
                                  setPatchFilter('all');
                                }}
                                className={cn(
                                  'inline-flex items-center gap-1.5 rounded-lg px-2.5 py-1.5 text-xs font-medium transition-colors cursor-pointer',
                                  active
                                    ? 'bg-zinc-900 text-white dark:bg-white dark:text-zinc-950 font-semibold'
                                    : 'text-zinc-500 dark:text-zinc-400 hover:bg-zinc-100 dark:hover:bg-white/[0.05] hover:text-zinc-900 dark:hover:text-white'
                                )}
                              >
                                <span>{cat.label}</span>
                                <span
                                  className={cn(
                                    'font-mono-tech text-[10px]',
                                    active ? 'opacity-70' : 'text-zinc-400 dark:text-zinc-500'
                                  )}
                                >
                                  {cat.count}
                                </span>
                              </button>
                            );
                          })}
                      </div>

                      {catEntries.length > 1 && (
                        <div className="flex items-center gap-1.5">
                          {buffCount > 0 && (
                            <button
                              type="button"
                              onClick={() => toggleFilter('buff')}
                              className={cn(
                                'inline-flex items-center gap-1.5 rounded-lg border px-2.5 py-1 font-mono-tech text-[11px] font-semibold transition-all cursor-pointer',
                                patchFilter === 'buff'
                                  ? 'border-emerald-500/60 bg-emerald-500/18 text-emerald-700 dark:text-emerald-200'
                                  : 'border-zinc-200/80 dark:border-white/[0.08] bg-transparent text-zinc-600 dark:text-zinc-400 hover:border-emerald-500/40 hover:text-emerald-600 dark:hover:text-emerald-300'
                              )}
                            >
                              <span className="size-1.5 rounded-full bg-emerald-500" />
                              {buffCount} Buffs
                            </button>
                          )}
                          {nerfCount > 0 && (
                            <button
                              type="button"
                              onClick={() => toggleFilter('nerf')}
                              className={cn(
                                'inline-flex items-center gap-1.5 rounded-lg border px-2.5 py-1 font-mono-tech text-[11px] font-semibold transition-all cursor-pointer',
                                patchFilter === 'nerf'
                                  ? 'border-rose-500/60 bg-rose-500/18 text-rose-700 dark:text-rose-200'
                                  : 'border-zinc-200/80 dark:border-white/[0.08] bg-transparent text-zinc-600 dark:text-zinc-400 hover:border-rose-500/40 hover:text-rose-600 dark:hover:text-rose-300'
                              )}
                            >
                              <span className="size-1.5 rounded-full bg-rose-500" />
                              {nerfCount} Nerfs
                            </button>
                          )}
                          {otherCount > 0 && (buffCount > 0 || nerfCount > 0) && (
                            <button
                              type="button"
                              onClick={() => toggleFilter('other')}
                              className={cn(
                                'inline-flex items-center gap-1.5 rounded-lg border px-2.5 py-1 font-mono-tech text-[11px] font-semibold transition-all cursor-pointer',
                                patchFilter === 'other'
                                  ? 'border-indigo-500/60 bg-indigo-500/18 text-indigo-700 dark:text-indigo-200'
                                  : 'border-zinc-200/80 dark:border-white/[0.08] bg-transparent text-zinc-600 dark:text-zinc-400 hover:border-indigo-500/40 hover:text-indigo-600 dark:hover:text-indigo-300'
                              )}
                            >
                              <span className="size-1.5 rounded-full bg-indigo-500" />
                              {otherCount} {activeCat === 'champion' ? 'Adjusted' : 'Other'}
                            </button>
                          )}
                        </div>
                      )}
                    </div>
                  )}

                  {allEntries.length === 0 ? (
                    <GlassCard surface="hub" className="p-10 text-center text-sm text-zinc-500 dark:text-zinc-400">
                      Loading latest patch notes…
                    </GlassCard>
                  ) : modeEntries.length === 0 ? (
                    <GlassCard surface="hub" className="p-10 text-center space-y-2">
                      <div className="text-sm font-medium text-zinc-700 dark:text-zinc-200">
                        No {modeLabel}-specific changes in Patch {pn?.version}
                      </div>
                      <p className="text-xs text-zinc-500 dark:text-zinc-400">
                        General champion and item balance changes are available under Ranked SR.
                      </p>
                    </GlassCard>
                  ) : filtered.length === 0 ? (
                    <GlassCard surface="hub" className="p-8 text-center text-xs text-zinc-500 dark:text-zinc-400">
                      No matching changes for &ldquo;{patchSearch}&rdquo;.
                    </GlassCard>
                  ) : (
                    <div className="space-y-5">
                      {groups.map((grp) => (
                        <section key={grp.key} className="space-y-2">
                          {groups.length > 1 && (
                            <div className="flex items-center gap-2 px-0.5">
                              <span className={cn('size-2 rounded-full', grp.dot)} />
                              <span className={cn('text-[11px] font-bold uppercase tracking-wider', grp.text)}>
                                {grp.label}
                              </span>
                              <span className="font-mono-tech text-[10px] text-zinc-400 dark:text-zinc-500">
                                {grp.items.length}
                              </span>
                            </div>
                          )}

                          <div className="grid grid-cols-1 lg:grid-cols-2 items-start gap-2.5">
                            {grp.items.map((entry, idx) => {
                              const entryKey = `${entry.category}-${entry.id}-${entry.title}-${idx}`;
                              const hasDetails = entry.details.length > 0 || Boolean(entry.playerNote);
                              const isExpanded = hasDetails && (autoExpand || Boolean(expandedPatches[entryKey]));
                              return (
                                <GlassCard
                                  key={entryKey}
                                  surface="hub"
                                  className={cn(
                                    'border-l-2 transition-colors',
                                    grp.accent,
                                    hasDetails && 'hover:border-zinc-300 dark:hover:border-white/[0.14]'
                                  )}
                                >
                                  <div
                                    role={hasDetails ? 'button' : undefined}
                                    tabIndex={hasDetails ? 0 : undefined}
                                    onClick={() => {
                                      if (!hasDetails || autoExpand) return;
                                      setExpandedPatches((prev) => ({ ...prev, [entryKey]: !prev[entryKey] }));
                                    }}
                                    onKeyDown={(e) => {
                                      if ((e.key === 'Enter' || e.key === ' ') && hasDetails && !autoExpand) {
                                        e.preventDefault();
                                        setExpandedPatches((prev) => ({ ...prev, [entryKey]: !prev[entryKey] }));
                                      }
                                    }}
                                    className={cn('p-3.5', hasDetails && !autoExpand && 'cursor-pointer')}
                                  >
                                    <div className="flex items-start justify-between gap-2.5">
                                      <div className="flex items-start gap-3 min-w-0">
                                        {entry.category === 'champion' ? (
                                          <ChampAvatar
                                            src={entry.icon}
                                            alt={entry.title}
                                            className="size-9 rounded-lg border border-zinc-200 dark:border-white/12 mt-0.5"
                                          />
                                        ) : entry.category === 'augment' ? (
                                          <AugmentIcon
                                            src={entry.icon}
                                            alt={entry.title}
                                            className="size-9 rounded-lg border border-zinc-200 dark:border-white/12 mt-0.5"
                                          />
                                        ) : entry.category === 'item' && entry.icon ? (
                                          <img
                                            src={iconUrl(entry.icon)}
                                            alt={entry.title}
                                            className="size-9 rounded-lg border border-zinc-200 dark:border-white/12 bg-zinc-900 shrink-0 mt-0.5"
                                          />
                                        ) : (
                                          <div className="flex size-9 items-center justify-center rounded-lg border border-indigo-500/20 bg-indigo-500/10 text-indigo-500 dark:text-indigo-300 shrink-0 mt-0.5">
                                            <Flame className="size-4" />
                                          </div>
                                        )}
                                        <div className="min-w-0">
                                          <div className="flex items-center gap-2">
                                            <span className="truncate text-[13px] font-bold text-zinc-900 dark:text-white">
                                              {entry.title}
                                            </span>
                                            {grp.key === 'other' && <PatchTagBadge tag={entry.changeType} />}
                                          </div>
                                          {entry.summary && (
                                            <p
                                              className={cn(
                                                'mt-0.5 text-xs leading-relaxed text-zinc-500 dark:text-zinc-400',
                                                !isExpanded && 'line-clamp-2'
                                              )}
                                            >
                                              {entry.summary}
                                            </p>
                                          )}
                                        </div>
                                      </div>

                                      <div className="flex items-center gap-1.5 shrink-0">
                                        {entry.category === 'champion' && entry.slug && (
                                          <button
                                            type="button"
                                            onClick={(ev) => {
                                              ev.stopPropagation();
                                              postAction('selectChampion', { slug: entry.slug, view: 'Build' });
                                            }}
                                            className="rounded-md border border-zinc-200 dark:border-white/10 bg-zinc-50 dark:bg-white/[0.04] px-2 py-1 text-[11px] font-medium text-zinc-600 dark:text-zinc-300 hover:border-indigo-400/50 hover:text-indigo-600 dark:hover:text-indigo-300 transition-colors cursor-pointer"
                                          >
                                            Build
                                          </button>
                                        )}
                                        {hasDetails && !autoExpand && (
                                          <span
                                            className={cn(
                                              'inline-flex items-center gap-0.5 rounded-md px-1.5 py-1 text-[10px] font-mono-tech text-zinc-400 transition-colors',
                                              isExpanded && 'text-zinc-700 dark:text-zinc-200'
                                            )}
                                          >
                                            {entry.details.length > 0 && <span>{entry.details.length}</span>}
                                            <ChevronDown
                                              className={cn(
                                                'size-3.5 transition-transform duration-150',
                                                isExpanded && 'rotate-180'
                                              )}
                                            />
                                          </span>
                                        )}
                                      </div>
                                    </div>

                                    {isExpanded && (
                                      <div className="mt-3 pt-2.5 border-t border-zinc-200/60 dark:border-white/[0.06] space-y-1.5">
                                        {entry.details.map((dl, di) => (
                                          <div
                                            key={di}
                                            className="flex items-start gap-2 text-[11px] leading-relaxed text-zinc-600 dark:text-zinc-300"
                                          >
                                            <span className={cn('mt-1.5 size-1 rounded-full shrink-0 opacity-75', grp.dot)} />
                                            <div className="min-w-0">
                                              {dl.text ? (
                                                renderDetailText(dl.text, grp.valTone)
                                              ) : (
                                                <span>
                                                  <span className="text-zinc-400 line-through mr-1.5">{dl.oldValue}</span>
                                                  <span className={cn('font-semibold', grp.valTone)}>
                                                    → {dl.newValue}
                                                  </span>
                                                </span>
                                              )}
                                            </div>
                                          </div>
                                        ))}
                                        {entry.playerNote && (
                                          <div className="pt-1 text-[11px] italic text-zinc-500 dark:text-zinc-400">
                                            {entry.playerNote}
                                          </div>
                                        )}
                                      </div>
                                    )}
                                  </div>
                                </GlassCard>
                              );
                            })}
                          </div>
                        </section>
                      ))}
                    </div>
                  )}

                  {modeKey === 'ARAM' && (
                    <div className="flex items-center justify-between rounded-xl border border-zinc-200/70 dark:border-white/[0.06] bg-zinc-50/60 dark:bg-white/[0.02] px-3.5 py-2.5 text-xs text-zinc-500 dark:text-zinc-400">
                      <span>Showing ARAM-specific tuning only. General champion &amp; item balance lives in Ranked SR.</span>
                      <button
                        type="button"
                        onClick={() => postAction('setMode', { mode: 'SR' })}
                        className="shrink-0 font-semibold text-indigo-600 dark:text-indigo-300 hover:underline cursor-pointer ml-3"
                      >
                        View Ranked SR →
                      </button>
                    </div>
                  )}
                </div>
              );
            })()}

            {/* VIEW: NOTES */}
            {view === 'Notes' && !selected && (
              <GlassCard surface="hub" className="p-10 text-center space-y-3 swiss-grid-bg">
                <SwissDraftEmptyIllustration variant="notes" />
                <h3 className="font-display text-base font-bold text-zinc-900 dark:text-white">
                  Personal Matchup Notes
                </h3>
                <p className="mx-auto max-w-sm text-xs leading-relaxed text-zinc-500 dark:text-zinc-400">
                  Select a champion first to view or edit your personal matchup reminders and augment plans.
                </p>
                <Button size="sm" variant="primary" onClick={() => postAction('setView', { view: 'Library' })}>
                  Browse Champions
                </Button>
              </GlassCard>
            )}
            {view === 'Notes' && selected && (
              <GlassCard surface="hub" className="p-5 space-y-3">
                <div className="flex items-center justify-between">
                  <div>
                    <h3 className="text-sm font-semibold text-zinc-900 dark:text-white">
                      Personal Notes · {selected.name}
                    </h3>
                    <p className="text-xs text-zinc-500 dark:text-zinc-400">
                      Saved locally per champion for matchup reminders and augment plans
                    </p>
                  </div>
                  <Button
                    size="sm"
                    variant="primary"
                    onClick={() => {
                      postAction('saveNote', { slug: selected.slug, text: noteDraft ?? state.note });
                      setNoteDraft(null);
                    }}
                  >
                    Save Note
                  </Button>
                </div>
                <textarea
                  rows={8}
                  value={noteDraft ?? state.note}
                  onChange={(e) => setNoteDraft(e.target.value)}
                  placeholder={`Write notes for ${selected.name}...`}
                  className="w-full rounded-xl border border-zinc-200 dark:border-white/10 bg-zinc-50 dark:bg-[#0D0E14] p-3.5 text-sm text-zinc-900 dark:text-zinc-100 placeholder:text-zinc-400 focus:border-indigo-400 focus:outline-none select-text"
                />
              </GlassCard>
            )}
          </motion.div>
        </main>
      </div>

      {/* AUGMENT DETAIL SLIDE-OVER DRAWER */}
      <AnimatePresence>
      {state.inspectedAugment && (
        <motion.div
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          transition={{ duration: 0.18 }}
          className="fixed inset-0 z-50 flex justify-end bg-black/45 backdrop-blur-[2px]"
          onClick={() => postAction('closeAugment')}
        >
          <motion.div
            initial={{ x: '100%' }}
            animate={{ x: 0 }}
            exit={{ x: '100%' }}
            transition={{ type: 'spring', stiffness: 380, damping: 36 }}
            className="flex h-full w-full max-w-[440px] flex-col border-l border-zinc-200 dark:border-white/[0.1] bg-white dark:bg-[#111218] shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-center justify-between border-b border-zinc-200/80 dark:border-white/[0.08] px-5 py-4">
              <div className="flex items-center gap-3 min-w-0">
                <AugmentIcon
                  src={state.inspectedAugment.icon}
                  alt={state.inspectedAugment.name}
                  className="size-11 rounded-xl border border-zinc-200 dark:border-white/15"
                  iconClassName="size-5"
                />
                <div className="min-w-0">
                  <div className="flex items-center gap-2">
                    <h3 className="truncate text-base font-bold text-zinc-900 dark:text-white">
                      {state.inspectedAugment.name}
                    </h3>
                    <TierBadge tier={state.inspectedAugment.tier} size="sm" />
                  </div>
                  <div className="mt-1 flex items-center gap-1.5">
                    {state.inspectedAugment.rarity && (
                      <span
                        className={cn(
                          'rounded border px-1.5 py-0.5 text-[10px] font-bold uppercase',
                          rarityPillClass(state.inspectedAugment.rarity)
                        )}
                      >
                        {state.inspectedAugment.rarity}
                      </span>
                    )}
                    <span className="text-[11px] text-zinc-500 dark:text-zinc-400 tabular-nums">
                      {state.inspectedAugment.games.toLocaleString('en-US')} games
                    </span>
                  </div>
                </div>
              </div>
              <button
                type="button"
                onClick={() => postAction('closeAugment')}
                className="flex size-8 items-center justify-center rounded-lg text-zinc-400 hover:bg-zinc-100 dark:hover:bg-white/[0.08] cursor-pointer"
              >
                <X className="size-4" />
              </button>
            </div>

            <div className="flex-1 overflow-y-auto p-5 space-y-5">
              {/* KPI Strip */}
              <div className="grid grid-cols-3 gap-2.5">
                <div className="rounded-xl border border-zinc-200/80 dark:border-white/[0.08] bg-zinc-50 dark:bg-white/[0.03] p-3 text-center">
                  <div className="text-[10px] font-semibold uppercase text-zinc-400">Win Rate</div>
                  <div className="mt-1 text-base font-bold text-indigo-600 dark:text-indigo-300 tabular-nums">
                    {fmtPct(state.inspectedAugment.rate)}
                  </div>
                </div>
                <div className="rounded-xl border border-zinc-200/80 dark:border-white/[0.08] bg-zinc-50 dark:bg-white/[0.03] p-3 text-center">
                  <div className="text-[10px] font-semibold uppercase text-zinc-400">vs Avg</div>
                  <div
                    className={cn(
                      'mt-1 text-base font-bold tabular-nums',
                      state.inspectedAugment.games === 0
                        ? 'text-zinc-400'
                        : state.inspectedAugment.delta >= 0
                          ? 'text-teal-600 dark:text-teal-300'
                          : 'text-rose-600 dark:text-rose-400'
                    )}
                  >
                    {state.inspectedAugment.games === 0
                      ? '—'
                      : `${state.inspectedAugment.delta >= 0 ? '+' : ''}${state.inspectedAugment.delta.toFixed(1)}%`}
                  </div>
                </div>
                <div className="rounded-xl border border-zinc-200/80 dark:border-white/[0.08] bg-zinc-50 dark:bg-white/[0.03] p-3 text-center">
                  <div className="text-[10px] font-semibold uppercase text-zinc-400">Pick Rate</div>
                  <div className="mt-1 text-base font-bold text-zinc-900 dark:text-white tabular-nums">
                    {parsePct(state.inspectedAugment.pick) || '—'}
                  </div>
                </div>
              </div>

              {/* Official In-Game Description */}
              <div className="rounded-xl border border-zinc-200/80 dark:border-white/[0.08] bg-zinc-50/70 dark:bg-white/[0.03] p-4">
                <div className="text-[10px] font-semibold uppercase tracking-wider text-zinc-400 mb-1.5">
                  Augment Effect
                </div>
                <p className="text-xs leading-relaxed text-zinc-700 dark:text-zinc-200">
                  {state.inspectedAugment.description}
                </p>
              </div>

              {/* Personal Preference + Track */}
              <div className="space-y-2.5">
                <div className="text-[10px] font-semibold uppercase tracking-wider text-zinc-400">
                  Recommendation Preference
                </div>
                <div className="grid grid-cols-3 gap-2">
                  {[
                    { val: 1, label: 'Prefer', icon: ThumbsUp },
                    { val: 0, label: 'Default', icon: Check },
                    { val: -1, label: 'Avoid', icon: ThumbsDown },
                  ].map((opt) => {
                    const Icon = opt.icon;
                    const active = state.inspectedAugment!.preference === opt.val;
                    return (
                      <Button
                        key={opt.label}
                        size="sm"
                        variant={active ? 'accent' : 'secondary'}
                        onClick={() =>
                          postAction('preferAugment', {
                            id: state.inspectedAugment!.id,
                            value: opt.val,
                          })
                        }
                      >
                        <Icon className="size-3.5" />
                        {opt.label}
                      </Button>
                    );
                  })}
                </div>
                <Button
                  size="md"
                  variant={state.inspectedAugment.tracked ? 'accent' : 'primary'}
                  className="w-full"
                  onClick={() => postAction('trackAugment', { id: state.inspectedAugment!.id })}
                >
                  {state.inspectedAugment.tracked ? (
                    <>
                      <Check className="size-4" />
                      Tracked in Active Build
                    </>
                  ) : (
                    <>
                      <Plus className="size-4" />
                      Track This Augment
                    </>
                  )}
                </Button>
              </div>

              {/* Observed Best Combos */}
              {state.inspectedAugment.pairings.length > 0 && (
                <div className="space-y-2">
                  <div className="text-[10px] font-semibold uppercase tracking-wider text-zinc-400">
                    Observed Best Combos
                  </div>
                  <div className="space-y-2">
                    {state.inspectedAugment.pairings.map((pair) => (
                      <button
                        key={pair.name}
                        type="button"
                        onClick={() => pair.id > 0 && postAction('inspectAugment', { id: pair.id })}
                        className="flex w-full items-center justify-between gap-3 rounded-xl border border-zinc-200/80 dark:border-white/[0.08] bg-zinc-50/70 dark:bg-white/[0.03] px-3 py-2.5 text-left hover:border-indigo-400/50 cursor-pointer"
                      >
                        <div className="flex items-center gap-2.5 min-w-0">
                          <AugmentIcon
                            src={pair.icon}
                            alt={pair.name}
                            className="size-7 rounded-lg"
                            iconClassName="size-3.5"
                          />
                          <span className="truncate text-xs font-semibold text-zinc-900 dark:text-zinc-100">
                            {pair.name}
                          </span>
                        </div>
                        <span className="text-xs font-bold text-indigo-600 dark:text-indigo-300 tabular-nums">
                          {fmtPct(pair.rate)}
                        </span>
                      </button>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </motion.div>
        </motion.div>
      )}
      </AnimatePresence>

      {/* ITEM DETAIL DRAWER */}
      <AnimatePresence>
      {state.inspectedItem && (
        <motion.div
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          transition={{ duration: 0.18 }}
          className="fixed inset-0 z-50 flex justify-end bg-black/45 backdrop-blur-[2px]"
          onClick={() => postAction('closeItem')}
        >
          <motion.div
            initial={{ x: '100%' }}
            animate={{ x: 0 }}
            exit={{ x: '100%' }}
            transition={{ type: 'spring', stiffness: 380, damping: 36 }}
            className="flex h-full w-full max-w-[420px] flex-col border-l border-zinc-200 dark:border-white/[0.1] bg-white dark:bg-[#111218] shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-center justify-between border-b border-zinc-200/80 dark:border-white/[0.08] px-5 py-4">
              <div className="flex items-center gap-3 min-w-0">
                <img
                  src={itemIconUrl(state.inspectedItem.id, state.ddVersion)}
                  alt={state.inspectedItem.name}
                  className="size-11 rounded-xl border border-zinc-200 dark:border-white/15 bg-zinc-900 shrink-0"
                />
                <div className="min-w-0">
                  <h3 className="truncate text-base font-bold text-zinc-900 dark:text-white">
                    {state.inspectedItem.name}
                  </h3>
                  <div className="mt-0.5 flex items-center gap-2 text-xs text-zinc-500 dark:text-zinc-400 tabular-nums">
                    <span className="inline-flex items-center gap-1 font-semibold text-amber-600 dark:text-amber-300">
                      <Coins className="size-3" />
                      {fmtGold(state.inspectedItem.priceTotal)}
                    </span>
                    {state.inspectedItem.winRate ? (
                      <span className="font-semibold text-indigo-600 dark:text-indigo-300">
                        WR {fmtPct(state.inspectedItem.winRate)}
                      </span>
                    ) : null}
                  </div>
                </div>
              </div>
              <button
                type="button"
                onClick={() => postAction('closeItem')}
                className="flex size-8 items-center justify-center rounded-lg text-zinc-400 hover:bg-zinc-100 dark:hover:bg-white/[0.08] cursor-pointer"
              >
                <X className="size-4" />
              </button>
            </div>

            <div className="flex-1 overflow-y-auto p-5 space-y-4">
              {state.inspectedItem.stats.length > 0 && (
                <div className="flex flex-wrap gap-1.5">
                  {state.inspectedItem.stats.map((s) => (
                    <span
                      key={s}
                      className="rounded-lg border border-zinc-200 dark:border-white/10 bg-zinc-100/80 dark:bg-white/[0.05] px-2.5 py-1 text-[11px] font-semibold text-zinc-800 dark:text-zinc-200"
                    >
                      {s}
                    </span>
                  ))}
                </div>
              )}

              {state.inspectedItem.paragraphs.length > 0 && (
                <div className="space-y-2 rounded-xl border border-zinc-200/80 dark:border-white/[0.08] bg-zinc-50/70 dark:bg-white/[0.03] p-4 text-xs leading-relaxed text-zinc-700 dark:text-zinc-300">
                  {state.inspectedItem.paragraphs.map((p, i) => (
                    <p key={i}>{p}</p>
                  ))}
                </div>
              )}

              {state.inspectedItem.components.length > 0 && (
                <div className="space-y-2">
                  <div className="text-[10px] font-semibold uppercase tracking-wider text-zinc-400">
                    Recipe Components
                  </div>
                  <div className="grid grid-cols-2 gap-2">
                    {state.inspectedItem.components.map((c, idx) => (
                      <div
                        key={`${c.id}-${idx}`}
                        className="flex items-center justify-between gap-2 rounded-xl border border-zinc-200/80 dark:border-white/[0.08] bg-zinc-50/70 dark:bg-white/[0.03] px-2.5 py-2 text-xs"
                      >
                        <div className="flex items-center gap-2 min-w-0">
                          <img
                            src={itemIconUrl(c.id, state.ddVersion)}
                            alt=""
                            className="size-6 rounded bg-zinc-900 shrink-0"
                          />
                          <span className="truncate font-medium">{c.name}</span>
                        </div>
                        <span className="text-[11px] text-zinc-500 tabular-nums">{c.price}g</span>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              <Button
                size="md"
                variant={state.inspectedItem.planned ? 'accent' : 'primary'}
                className="w-full"
                onClick={() => postAction('togglePlanItem', { id: state.inspectedItem!.id })}
              >
                {state.inspectedItem.planned ? 'Pinned in Next Buys' : 'Pin to Build Plan'}
              </Button>
            </div>
          </motion.div>
        </motion.div>
      )}
      </AnimatePresence>

      {/* MATCH DETAIL MODAL */}
      <AnimatePresence>
      {selectedMatch && (
        <motion.div
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          transition={{ duration: 0.18 }}
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-[3px] p-4"
          onClick={() => setSelectedMatch(null)}
        >
          <motion.div
            initial={{ opacity: 0, scale: 0.96, y: 10 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.97, y: 6 }}
            transition={{ duration: 0.22, ease: SWISS_EASE }}
            className="w-full max-w-lg"
            onClick={(e) => e.stopPropagation()}
          >
          <GlassCard
            surface="hub"
            className="w-full p-5 space-y-4"
          >
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-3">
                {selectedMatch.championIcon && (
                  <ChampAvatar
                    src={selectedMatch.championIcon}
                    alt={selectedMatch.championName}
                    className="size-11 rounded-xl border border-zinc-200 dark:border-white/12"
                  />
                )}
                <div>
                  <h3 className="text-base font-bold">
                    {selectedMatch.championName} ·{' '}
                    {selectedMatch.remake ? 'Remake' : selectedMatch.won ? 'Victory' : 'Defeat'}
                  </h3>
                  <p className="text-xs text-zinc-500">
                    {selectedMatch.queueName} · {selectedMatch.dateText} · {selectedMatch.time}
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setSelectedMatch(null)}
                className="flex size-8 items-center justify-center rounded-lg text-zinc-400 hover:bg-zinc-100 dark:hover:bg-white/10 cursor-pointer"
              >
                <X className="size-4" />
              </button>
            </div>
            <div className="grid grid-cols-3 gap-2.5 text-center">
              <div className="rounded-xl border border-zinc-200 dark:border-white/[0.08] p-3">
                <div className="text-[10px] uppercase text-zinc-400">KDA</div>
                <div className="mt-1 text-sm font-bold tabular-nums">
                  {selectedMatch.kills}/{selectedMatch.deaths}/{selectedMatch.assists}
                </div>
              </div>
              <div className="rounded-xl border border-zinc-200 dark:border-white/[0.08] p-3">
                <div className="text-[10px] uppercase text-zinc-400">Damage</div>
                <div className="mt-1 text-sm font-bold tabular-nums">
                  {selectedMatch.damage.toLocaleString('en-US')}
                </div>
              </div>
              <div className="rounded-xl border border-zinc-200 dark:border-white/[0.08] p-3">
                <div className="text-[10px] uppercase text-zinc-400">Gold / CS</div>
                <div className="mt-1 text-sm font-bold tabular-nums">
                  {(selectedMatch.gold / 1000).toFixed(1)}k · {selectedMatch.cs} CS
                </div>
              </div>
            </div>
            {/* Match Final Build Items */}
            {selectedMatch.items.length > 0 && (
              <div>
                <div className="text-[10px] font-semibold uppercase tracking-wider text-zinc-400 mb-2">
                  Final Build
                </div>
                <div className="flex flex-wrap items-center gap-2">
                  {selectedMatch.items.slice(0, 6).map((id, i) => (
                    <img
                      key={`${id}-${i}`}
                      src={itemIconUrl(id, state.ddVersion)}
                      alt=""
                      className="size-9 rounded-lg border border-zinc-200 dark:border-white/12 bg-zinc-900"
                    />
                  ))}
                </div>
              </div>
            )}
            {/* Match Mayhem Augments */}
            {selectedMatch.augmentDetails && selectedMatch.augmentDetails.length > 0 && (
              <div>
                <div className="text-[10px] font-semibold uppercase tracking-wider text-zinc-400 mb-2">
                  Augments Played
                </div>
                <div className="flex flex-wrap gap-2">
                  {selectedMatch.augmentDetails.map((a) => (
                    <div
                      key={a.id}
                      className="inline-flex items-center gap-2 rounded-lg border border-indigo-400/30 bg-indigo-500/10 px-2.5 py-1.5 text-xs font-medium"
                    >
                      <img src={iconUrl(a.icon)} alt="" className="size-5 rounded bg-zinc-900" />
                      <span>{a.name}</span>
                    </div>
                  ))}
                </div>
              </div>
            )}
            <div className="flex items-center justify-end gap-2">
              <Button size="sm" variant="secondary" onClick={() => setSelectedMatch(null)}>
                <ArrowLeft className="size-3.5" />
                Back
              </Button>
            </div>
          </GlassCard>
          </motion.div>
        </motion.div>
      )}
      </AnimatePresence>

      {/* SETTINGS MODAL */}
      <AnimatePresence>
      {modal === 'settings' && (
        <motion.div
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          transition={{ duration: 0.18 }}
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-[3px] p-4"
          onClick={() => setModal('none')}
        >
          <motion.div
            initial={{ opacity: 0, scale: 0.96, y: 10 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.97, y: 6 }}
            transition={{ duration: 0.22, ease: SWISS_EASE }}
            className="w-full max-w-2xl"
            onClick={(e) => e.stopPropagation()}
          >
          <GlassCard
            surface="hub"
            className="w-full max-h-[86vh] flex flex-col overflow-hidden"
          >
            <div className="flex items-center justify-between border-b border-zinc-200/80 dark:border-white/[0.08] px-5 py-4 shrink-0">
              <div>
                <h3 className="text-base font-bold text-zinc-900 dark:text-white">
                  maylo settings & overlay modules
                </h3>
                <p className="text-xs text-zinc-500 dark:text-zinc-400">
                  Enable or disable individual overlay widgets and Champ Select automation
                </p>
              </div>
              <button
                type="button"
                onClick={() => setModal('none')}
                className="flex size-8 items-center justify-center rounded-lg text-zinc-400 hover:bg-zinc-100 dark:hover:bg-white/10 cursor-pointer"
              >
                <X className="size-4" />
              </button>
            </div>

            <div className="flex-1 overflow-y-auto p-5 space-y-5">
              {/* Automation */}
              <div className="space-y-1">
                <div className="px-2.5 text-[10px] font-bold uppercase tracking-wider text-zinc-400">
                  Champ Select Automation (All Modes)
                </div>
                <Switch
                  checked={state.settings.autoRunes}
                  onChange={(v) => postAction('updateSetting', { key: 'autoRunes', value: v })}
                  label="Auto-import Runes"
                  description="Automatically applies OP.GG recommended runes + stat shards in Champ Select"
                />
                <Switch
                  checked={state.settings.autoSpells}
                  onChange={(v) => postAction('updateSetting', { key: 'autoSpells', value: v })}
                  label="Auto-import Summoner Spells"
                  description="Automatically equips top-winrate summoner spells for your locked champion & role"
                />
                <div className="flex items-center justify-between gap-4 rounded-xl px-2.5 py-2">
                  <div className="min-w-0">
                    <div className="text-[13px] font-medium text-zinc-900 dark:text-zinc-100">
                      Flash Key Preference
                    </div>
                    <div className="text-[11px] text-zinc-500 dark:text-zinc-400 mt-0.5">
                      Automatically place Flash on your preferred key (D or F) during Champ Select
                    </div>
                  </div>
                  <div className="inline-flex shrink-0 rounded-lg border border-zinc-200 dark:border-white/12 bg-zinc-100 dark:bg-white/[0.05] p-0.5">
                    {(['D', 'F'] as const).map((k) => (
                      <button
                        key={k}
                        type="button"
                        onClick={() => postAction('setFlashKey', { key: k })}
                        className={cn(
                          'w-9 rounded-md py-1 text-xs font-bold transition-colors cursor-pointer',
                          state.settings.flashKey === k
                            ? 'bg-indigo-600 text-white shadow-xs'
                            : 'text-zinc-500 dark:text-zinc-400 hover:text-zinc-900 dark:hover:text-white'
                        )}
                      >
                        {k}
                      </button>
                    ))}
                  </div>
                </div>
                <Switch
                  checked={state.settings.autoExportShop}
                  onChange={(v) => postAction('updateSetting', { key: 'autoExportShop', value: v })}
                  label="Auto-Export Build to In-Game Shop"
                  description="Automatically creates a custom maylo item set in League shop in Champ Select & In-Game"
                />
                <Switch
                  checked={state.settings.autoPage}
                  onChange={(v) => postAction('updateSetting', { key: 'autoPage', value: v })}
                  label="Auto-Follow Champion"
                  description="Automatically switches maylo to your picked champion and detected game mode"
                />
              </div>

              {/* Modular Overlay Widgets */}
              <div className="space-y-1 border-t border-zinc-200/80 dark:border-white/[0.07] pt-4">
                <div className="px-2.5 text-[10px] font-bold uppercase tracking-wider text-zinc-400">
                  In-Game Overlay Modules
                </div>
                <Switch
                  checked={state.settings.overlay}
                  onChange={(v) => postAction('updateSetting', { key: 'overlay', value: v })}
                  label="Master In-Game Overlay"
                  description="Show transparent click-through HUD widgets over League (Borderless)"
                />
                <Switch
                  checked={state.settings.showBuildOverlay}
                  onChange={(v) => postAction('updateSetting', { key: 'showBuildOverlay', value: v })}
                  label="Build Guide HUD"
                  description="Summoner’s Rift, ARAM and Mayhem · next purchase, components and gold remaining"
                />
                <Switch
                  checked={state.settings.showPerfStrip}
                  onChange={(v) => postAction('updateSetting', { key: 'showPerfStrip', value: v })}
                  label="Live CS/min & Performance Strip"
                  description="Summoner’s Rift, ARAM and Mayhem · CS/min, KDA and kill participation"
                />
                <Switch
                  checked={state.settings.showJungleWidget}
                  onChange={(v) => postAction('updateSetting', { key: 'showJungleWidget', value: v })}
                  label="On-Minimap Objective Timers & Jungle Path (SR)"
                  description="Summoner’s Rift, including Swiftplay · objectives and early jungle routes on the minimap"
                />
                {state.settings.showJungleWidget && (
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 rounded-xl border border-zinc-200/70 dark:border-white/[0.06] bg-zinc-50/60 dark:bg-white/[0.02] px-4 py-3 mx-2 my-1">
                    <div>
                      <div className="flex items-center justify-between text-xs font-medium mb-1">
                        <span>Minimap Size Match</span>
                        <span className="tabular-nums text-indigo-600 dark:text-indigo-300 font-semibold">
                          {Math.round((state.settings.minimapScale || 1) * 100)}%
                        </span>
                      </div>
                      <input
                        type="range"
                        min={0.55}
                        max={1.85}
                        step={0.05}
                        value={state.settings.minimapScale || 1}
                        onChange={(e) =>
                          postAction('updateSetting', {
                            key: 'minimapScale',
                            value: parseFloat(e.target.value),
                          })
                        }
                        className="w-full accent-indigo-500 cursor-pointer"
                      />
                    </div>
                    <div className="flex items-center justify-between gap-2">
                      <div className="inline-flex rounded-lg border border-zinc-200 dark:border-white/10 bg-zinc-100 dark:bg-white/[0.04] p-0.5">
                        {[
                          { left: false, label: 'Right' },
                          { left: true, label: 'Left' },
                        ].map((opt) => (
                          <button
                            key={opt.label}
                            type="button"
                            onClick={() =>
                              postAction('updateSetting', { key: 'minimapLeft', value: opt.left })
                            }
                            className={cn(
                              'rounded-md px-2.5 py-1 text-[11px] font-semibold transition-colors cursor-pointer',
                              !!state.settings.minimapLeft === opt.left
                                ? 'bg-indigo-600 text-white'
                                : 'text-zinc-500 dark:text-zinc-400 hover:text-zinc-900 dark:hover:text-white'
                            )}
                          >
                            {opt.label}
                          </button>
                        ))}
                      </div>
                      <Button size="xs" variant="secondary" onClick={() => postAction('syncLeagueMinimap')}>
                        Sync LoL
                      </Button>
                    </div>
                  </div>
                )}
                <Switch
                  checked={state.settings.showRerollBar}
                  onChange={(v) => postAction('updateSetting', { key: 'showRerollBar', value: v })}
                  label="Reroll Reference Bar"
                  description="Mayhem · recommended reroll targets while choosing an augment"
                />
                <Switch
                  checked={state.settings.showBenchBar}
                  onChange={(v) => postAction('updateSetting', { key: 'showBenchBar', value: v })}
                  label="Smart ARAM / Mayhem Bench Bar"
                  description="ARAM and Mayhem · ranks available bench champions during champion select"
                />
                <Switch
                  checked={state.settings.minimalHud}
                  onChange={(v) => postAction('updateSetting', { key: 'minimalHud', value: v })}
                  label="Ultra-Minimalist Build HUD"
                  description="Collapses the in-game Build HUD to a single compact Next Buy pill"
                />
              </div>

              {/* System & Custom Hotkeys */}
              <div className="space-y-2 border-t border-zinc-200/80 dark:border-white/[0.07] pt-4">
                <div className="px-2.5 text-[10px] font-bold uppercase tracking-wider text-zinc-400">
                  System & Custom Hotkeys
                </div>
                <Switch
                  checked={state.settings.launchWithWindows}
                  onChange={(v) => postAction('updateSetting', { key: 'launchWithWindows', value: v })}
                  label="Launch maylo with Windows"
                  description="Automatically starts maylo when you sign in to Windows (no admin needed)"
                />
                <Switch
                  checked={state.settings.minimizeToTray}
                  onChange={(v) => postAction('updateSetting', { key: 'minimizeToTray', value: v })}
                  label="Minimize to System Tray on Close"
                  description="Closing the window keeps maylo running quietly in the taskbar tray"
                />
                <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-zinc-200/80 dark:border-white/[0.08] bg-zinc-50/70 dark:bg-white/[0.025] px-3 py-2.5 mx-2.5">
                  <div className="min-w-0">
                    <div className="flex items-center gap-2 text-xs font-semibold text-zinc-900 dark:text-zinc-100">
                      <span>Automatic Updates</span>
                      <span className="rounded bg-indigo-500/12 px-1.5 py-0.5 font-mono-tech text-[10px] text-indigo-600 dark:text-indigo-300">
                        v{state.version}
                      </span>
                    </div>
                    <div className="mt-0.5 text-[11px] text-zinc-500 dark:text-zinc-400 truncate">
                      {state.update?.message || 'Checks GitHub Releases automatically with SHA-256 verification'}
                    </div>
                  </div>
                  <div className="flex items-center gap-1.5 shrink-0">
                    {state.update?.canRestart ? (
                      <Button
                        size="xs"
                        variant="accent"
                        className="whitespace-nowrap shrink-0"
                        onClick={() => postAction('applyUpdateRestart')}
                      >
                        <Download className="size-3 shrink-0" />
                        <span>Restart &amp; Update</span>
                      </Button>
                    ) : (
                      <Button
                        size="xs"
                        variant="secondary"
                        disabled={state.update?.state === 'checking' || state.update?.state === 'downloading'}
                        onClick={() => postAction('checkUpdates')}
                      >
                        <RefreshCw
                          className={cn(
                            'size-3',
                            (state.update?.state === 'checking' || state.update?.state === 'downloading') &&
                              'animate-spin'
                          )}
                        />
                        {state.update?.state === 'checking'
                          ? 'Checking…'
                          : state.update?.state === 'downloading'
                            ? 'Downloading…'
                            : 'Check for Updates'}
                      </Button>
                    )}
                  </div>
                </div>
                <div className="grid grid-cols-2 sm:grid-cols-4 gap-2.5 px-2.5 pt-2">
                  {[
                    { field: 'sheet', label: 'In-Game Sheet', val: state.settings.hotkeySheet || 'G' },
                    { field: 'overlay', label: 'Toggle HUD', val: state.settings.hotkeyOverlay || 'M' },
                    { field: 'edit', label: 'Edit Layout', val: state.settings.hotkeyEdit || 'O' },
                    { field: 'scan', label: 'Scan Augments', val: state.settings.hotkeyScan || 'A' },
                  ].map((hk) => (
                    <div
                      key={hk.field}
                      className="rounded-xl border border-zinc-200/80 dark:border-white/[0.08] bg-zinc-50/70 dark:bg-white/[0.03] px-2.5 py-2"
                    >
                      <div className="text-[10px] font-medium text-zinc-500 dark:text-zinc-400">
                        {hk.label}
                      </div>
                      <div className="mt-1 flex items-center gap-1">
                        <span className="text-[11px] font-semibold text-zinc-400">Alt +</span>
                        <select
                          value={hk.val}
                          onChange={(e) =>
                            postAction('setHotkey', { field: hk.field, key: e.target.value })
                          }
                          className="rounded-md border border-zinc-200 dark:border-white/15 bg-white dark:bg-[#14151B] px-1.5 py-0.5 text-xs font-bold text-zinc-900 dark:text-white cursor-pointer"
                        >
                          {'ABCDEFGHIJKLMNOPQRSTUVWXYZ'.split('').map((ch) => {
                            const usedOther =
                              ch === 'H' ||
                              ch === 'L' ||
                              (hk.field !== 'sheet' && ch === (state.settings.hotkeySheet || 'G')) ||
                              (hk.field !== 'overlay' && ch === (state.settings.hotkeyOverlay || 'M')) ||
                              (hk.field !== 'edit' && ch === (state.settings.hotkeyEdit || 'O')) ||
                              (hk.field !== 'scan' && ch === (state.settings.hotkeyScan || 'A'));
                            return (
                              <option
                                key={ch}
                                value={ch}
                                disabled={usedOther}
                                className="bg-white dark:bg-[#14151B] text-zinc-900 dark:text-white"
                              >
                                {ch}
                              </option>
                            );
                          })}
                        </select>
                      </div>
                    </div>
                  ))}
                </div>
              </div>

              {/* Augment OCR & Calibration */}
              <div className="space-y-3 border-t border-zinc-200/80 dark:border-white/[0.07] pt-4">
                <div className="px-2.5 text-[10px] font-bold uppercase tracking-wider text-zinc-400">
                  Augment OCR & Positioning
                </div>
                <Switch
                  checked={state.settings.ocr}
                  onChange={(v) => postAction('updateSetting', { key: 'ocr', value: v })}
                  label="Auto-Detect Augments (OCR)"
                  description="Mayhem · reads card titles and centers the tier badges above them"
                />
                <Switch
                  checked={state.settings.ocrAllModes}
                  onChange={(v) => postAction('updateSetting', { key: 'ocrAllModes', value: v })}
                  label="Scan augments outside Mayhem"
                  description="Also scan in supported Summoner’s Rift and ARAM matches"
                />

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 px-2.5 pt-1">
                  <div>
                    <div className="flex items-center justify-between text-xs font-medium mb-1.5">
                      <span>Overlay Scale</span>
                      <span className="tabular-nums text-indigo-600 dark:text-indigo-300 font-semibold">
                        {Math.round((state.settings.scale || 1) * 100)}%
                      </span>
                    </div>
                    <input
                      type="range"
                      min={0.75}
                      max={1.35}
                      step={0.05}
                      value={state.settings.scale || 1}
                      onChange={(e) =>
                        postAction('updateSetting', {
                          key: 'scale',
                          value: parseFloat(e.target.value),
                        })
                      }
                      className="w-full accent-indigo-500 cursor-pointer"
                    />
                  </div>
                  <div>
                    <div className="flex items-center justify-between text-xs font-medium mb-1.5">
                      <span>Augment Badge Vertical Offset</span>
                      <span className="tabular-nums text-indigo-600 dark:text-indigo-300 font-semibold">
                        {(state.settings.badgeOffsetY || 0) >= 0 ? '+' : ''}
                        {Math.round((state.settings.badgeOffsetY || 0) * 100)}%
                      </span>
                    </div>
                    <input
                      type="range"
                      min={-0.15}
                      max={0.15}
                      step={0.01}
                      value={state.settings.badgeOffsetY || 0}
                      onChange={(e) =>
                        postAction('updateSetting', {
                          key: 'badgeOffsetY',
                          value: parseFloat(e.target.value),
                        })
                      }
                      className="w-full accent-indigo-500 cursor-pointer"
                    />
                  </div>
                </div>

                {/* Guided Overlay Preview Scenes (Works on Desktop even without League!) */}
                <div className="rounded-xl border border-zinc-200/80 dark:border-white/[0.08] bg-zinc-50/70 dark:bg-white/[0.025] p-3 mx-2.5 space-y-2">
                  <div className="text-[10px] font-bold uppercase tracking-wider text-zinc-400">
                    Guided Overlay Preview (Calibrate on Desktop)
                  </div>
                  <div className="flex flex-wrap items-center gap-1.5">
                    {[
                      { id: 'sr_hud', label: 'SR HUD + Minimap' },
                      { id: 'bench', label: 'Bench Bar' },
                      { id: 'altg', label: 'Alt+G Sheet' },
                    ].map((sc) => (
                      <Button
                        key={sc.id}
                        size="xs"
                        variant={state.preview && state.previewScene === sc.id ? 'accent' : 'secondary'}
                        onClick={() => postAction('setPreviewScene', { scene: sc.id })}
                      >
                        {sc.label}
                      </Button>
                    ))}
                    {[3, 4, 5].map(count => (
                      <Button
                        key={count}
                        size="xs"
                        variant={state.preview && state.previewScene === 'augments' && (state.previewAugmentCount ?? 3) === count
                          ? 'accent' : 'secondary'}
                        onClick={() => postAction('setPreviewScene', { scene: 'augments', count })}
                      >
                        {count} Augments
                      </Button>
                    ))}
                    {state.preview && (
                      <Button
                        size="xs"
                        variant="danger"
                        onClick={() => postAction('setPreviewScene', { scene: 'off' })}
                      >
                        Stop Preview
                      </Button>
                    )}
                  </div>
                </div>

                <div className="flex flex-wrap items-center gap-2 px-2.5 pt-2">
                  <Button
                    size="xs"
                    variant="secondary"
                    onClick={() => {
                      setModal('none');
                      setReplayTutorial(true);
                    }}
                  >
                    <Sparkles className="size-3" />
                    Replay Tutorial
                  </Button>
                  <Button size="xs" variant="secondary" onClick={() => postAction('refreshDatabases')}>
                    <RefreshCw className="size-3" />
                    Sync Databases{state.ddVersion ? ` (Patch ${state.ddVersion})` : ''}
                  </Button>
                  <Button size="xs" variant="secondary" onClick={() => postAction('resetPositions')}>
                    <RotateCcw className="size-3" />
                    Reset Positions
                  </Button>
                  <Button size="xs" variant="secondary" onClick={() => postAction('exportConfig')}>
                    <Download className="size-3" />
                    Export Config
                  </Button>
                  <Button size="xs" variant="secondary" onClick={() => postAction('importConfig')}>
                    Import Config
                  </Button>
                  <Button size="xs" variant="secondary" onClick={() => postAction('prepareBorderless')}>
                    Set Borderless
                  </Button>
                  <Button size="xs" variant="secondary" onClick={() => postAction('chooseLockfile')}>
                    Locate Lockfile
                  </Button>
                </div>
              </div>
            </div>
          </GlassCard>
          </motion.div>
        </motion.div>
      )}
      </AnimatePresence>

      {/* SHORTCUTS MODAL */}
      <AnimatePresence>
      {modal === 'shortcuts' && (
        <motion.div
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          transition={{ duration: 0.18 }}
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-[3px] p-4"
          onClick={() => setModal('none')}
        >
          <motion.div
            initial={{ opacity: 0, scale: 0.96, y: 10 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.97, y: 6 }}
            transition={{ duration: 0.22, ease: SWISS_EASE }}
            className="w-full max-w-md"
            onClick={(e) => e.stopPropagation()}
          >
          <GlassCard
            surface="hub"
            className="w-full p-5 space-y-4"
          >
            <div className="flex items-center justify-between">
              <h3 className="text-base font-bold text-zinc-900 dark:text-white">
                In-Game Shortcuts
              </h3>
              <button
                type="button"
                onClick={() => setModal('none')}
                className="flex size-8 items-center justify-center rounded-lg text-zinc-400 hover:bg-zinc-100 dark:hover:bg-white/10 cursor-pointer"
              >
                <X className="size-4" />
              </button>
            </div>
            <div className="divide-y divide-zinc-200/70 dark:divide-white/[0.06] text-xs">
              {[
                { key: `Alt+${state.settings.hotkeySheet || 'G'} / F9`, desc: 'Toggle instant centered In-Game Command Overlay' },
                { key: 'Ctrl+Tab (hold)', desc: 'Drag & reposition overlay widgets (Tab stays free)' },
                { key: `Alt+${state.settings.hotkeyEdit || 'O'}`, desc: 'Toggle persistent Overlay Edit & Minimap Resize mode' },
                { key: `Alt+${state.settings.hotkeyOverlay || 'M'}`, desc: 'Show / hide all in-game overlay widgets' },
                { key: `Alt+${state.settings.hotkeyScan || 'A'}`, desc: 'Force immediate Augment OCR scan' },
                { key: 'Alt+L', desc: 'Open Live Match tab inside in-game overlay' },
                { key: 'Esc', desc: 'Close in-game Command Overlay and return to game' },
              ].map((s) => (
                <div key={s.key} className="flex items-center justify-between gap-4 py-2.5">
                  <span className="text-zinc-600 dark:text-zinc-300">{s.desc}</span>
                  <Keycap keys={s.key} />
                </div>
              ))}
            </div>
          </GlassCard>
          </motion.div>
        </motion.div>
      )}
      </AnimatePresence>

      {/* FLOATING STATUS FEEDBACK TOAST */}
      <AnimatePresence>
        {toastText && (
          <motion.div
            initial={{ opacity: 0, y: 14, scale: 0.96 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: 10, scale: 0.96 }}
            transition={{ duration: 0.22, ease: SWISS_EASE }}
            className="fixed bottom-4 right-5 z-[60] flex items-center gap-2.5 rounded-xl border border-zinc-200/90 dark:border-white/12 bg-white/95 dark:bg-[#161822]/95 px-3.5 py-2.5 text-xs font-medium text-zinc-800 dark:text-zinc-100 shadow-lg backdrop-blur-md"
          >
            <span className="size-2 rounded-full bg-indigo-500 shrink-0" />
            <span className="max-w-[340px] truncate">{toastText}</span>
            <button
              type="button"
              aria-label="Dismiss notification"
              onClick={() => setToastText(null)}
              className="text-zinc-400 hover:text-zinc-900 dark:hover:text-white cursor-pointer"
            >
              <X className="size-3.5" />
            </button>
          </motion.div>
        )}
      </AnimatePresence>

      {/* FIRST-LAUNCH ONBOARDING SLIDER */}
      <AnimatePresence>
        {tutorialOpen && (
          <OnboardingModal
            settings={state.settings}
            onFinish={() => {
              setReplayTutorial(false);
              setDismissedTutorial(true);
              if (state.settings.onboardingSeen === false) {
                postAction('updateSetting', { key: 'onboardingSeen', value: true });
              }
            }}
          />
        )}
      </AnimatePresence>
    </div>
  );
}
