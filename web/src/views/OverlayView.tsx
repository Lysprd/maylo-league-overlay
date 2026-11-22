import React, { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { AnimatePresence, motion } from 'framer-motion';
import {
  Check,
  ChevronRight,
  Coins,
  Compass,
  Download,
  GripHorizontal,
  Minus,
  Plus,
  RotateCcw,
  Search,
  Sparkles,
  Swords,
  Timer,
  Trophy,
  X,
  Zap,
} from 'lucide-react';
import { GlassCard } from '@/components/ui/glass-card';
import { Status } from '@/components/ui/status';
import { SegmentedTabs } from '@/components/ui/segmented-tabs';
import { Button, Keycap, TierBadge } from '@/components/ui/primitives';
import { postAction } from '@/lib/bridge';
import { AugmentGuide, AugmentTier } from '@/components/AugmentGuide';
import { benchmarkMetrics, draftDrawerStyle, OverlayLayoutPreview } from '@/components/OverlayLayoutPreview';
import { useSavedBuilds } from '@/components/HubTools';
import { cn, fmtGold, fmtPct, iconUrl, itemIconUrl, parsePct, spellIconUrl } from '@/lib/utils';
import type { AppState, DraftChampion, LivePlayer, AugmentEntry } from '@/types';

const SKILL_KEYS = ['Q', 'W', 'E', 'R'] as const;
const SKILL_COLORS: Record<string, string> = {
  Q: 'bg-indigo-500 text-white border-indigo-400/70',
  W: 'bg-sky-500 text-white border-sky-400/70',
  E: 'bg-amber-500 text-zinc-950 border-amber-400/70',
  R: 'bg-rose-500 text-white border-rose-400/70',
};

const MINIMAP_PATHS = {
  ORDER: {
    red: [
      { label: '1', name: 'Red', x: 53, y: 73 },
      { label: '2', name: 'Krugs', x: 57, y: 82 },
      { label: '3', name: 'Raptors', x: 47, y: 64 },
      { label: '4', name: 'Wolves', x: 26, y: 56 },
      { label: '5', name: 'Blue', x: 26, y: 46 },
      { label: '6', name: 'Gromp', x: 15, y: 43 },
      { label: 'S', name: 'Scuttle', x: 29, y: 36 },
    ],
    blue: [
      { label: '1', name: 'Blue', x: 26, y: 46 },
      { label: '2', name: 'Gromp', x: 15, y: 43 },
      { label: '3', name: 'Wolves', x: 26, y: 56 },
      { label: '4', name: 'Raptors', x: 47, y: 64 },
      { label: '5', name: 'Red', x: 53, y: 73 },
      { label: '6', name: 'Krugs', x: 57, y: 82 },
      { label: 'S', name: 'Scuttle', x: 71, y: 64 },
    ],
  },
  CHAOS: {
    red: [
      { label: '1', name: 'Red', x: 47, y: 27 },
      { label: '2', name: 'Krugs', x: 43, y: 18 },
      { label: '3', name: 'Raptors', x: 53, y: 36 },
      { label: '4', name: 'Wolves', x: 74, y: 44 },
      { label: '5', name: 'Blue', x: 74, y: 54 },
      { label: '6', name: 'Gromp', x: 85, y: 57 },
      { label: 'S', name: 'Scuttle', x: 71, y: 64 },
    ],
    blue: [
      { label: '1', name: 'Blue', x: 74, y: 54 },
      { label: '2', name: 'Gromp', x: 85, y: 57 },
      { label: '3', name: 'Wolves', x: 74, y: 44 },
      { label: '4', name: 'Raptors', x: 53, y: 36 },
      { label: '5', name: 'Red', x: 47, y: 27 },
      { label: '6', name: 'Krugs', x: 43, y: 18 },
      { label: 'S', name: 'Scuttle', x: 29, y: 36 },
    ],
  },
} as const;

const PANEL_ANCHORS = {
  'top-left': [0, 0],
  'top-center': [0.5, 0],
  'center-bottom': [0.5, 1],
  'bottom-right': [1, 1],
  'bottom-left': [0, 1],
} as const;

const PANEL_INSET = 4;
const EDIT_TOOLBAR_INSET = 48;

function DraggableSlot({
  panelKey,
  title,
  defaultX,
  defaultY,
  anchor = 'top-left',
  editing,
  savedPanels,
  scale,
  passThrough = false,
  movable = true,
  children,
}: {
  panelKey: string;
  title: string;
  defaultX: number;
  defaultY: number;
  anchor?: keyof typeof PANEL_ANCHORS;
  editing: boolean;
  savedPanels?: Record<string, { x: number; y: number }>;
  scale: number;
  passThrough?: boolean;
  movable?: boolean;
  children: React.ReactNode;
}) {
  const saved = savedPanels?.[panelKey];
  const x = saved && Number.isFinite(saved.x) ? saved.x : defaultX;
  const y = saved && Number.isFinite(saved.y) ? saved.y : defaultY;
  const draggable = editing && movable;
  const [ax, ay] = PANEL_ANCHORS[anchor];
  const ref = useRef<HTMLDivElement>(null);
  const [size, setSize] = useState({ width: 0, height: 0 });
  const [dragPosition, setDragPosition] = useState<{ x: number; y: number } | null>(null);
  const dragRef = useRef<{
    pointerId: number;
    startX: number;
    startY: number;
    origPxX: number;
    origPxY: number;
  } | null>(null);

  useLayoutEffect(() => {
    const el = ref.current;
    if (!el) return;
    const measure = () => {
      const { width, height } = el.getBoundingClientRect();
      setSize(previous => previous.width === width && previous.height === height ? previous : { width, height });
    };
    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(el, { box: 'border-box' });
    return () => observer.disconnect();
  }, [scale]);

  const left = PANEL_INSET + ax * size.width;
  const right = PANEL_INSET + (1 - ax) * size.width;
  const top = (editing ? EDIT_TOOLBAR_INSET : PANEL_INSET) + ay * size.height;
  const bottom = PANEL_INSET + (1 - ay) * size.height;

  const resetDrag = () => {
    const drag = dragRef.current;
    dragRef.current = null;
    if (drag) {
      const el = ref.current;
      if (el?.hasPointerCapture(drag.pointerId)) el.releasePointerCapture(drag.pointerId);
      postAction('setDragging', { value: false });
    }
    setDragPosition(null);
  };

  useLayoutEffect(() => {
    if (!draggable) resetDrag();
    else if (!dragRef.current) setDragPosition(null);
  }, [x, y, draggable]);

  useEffect(() => {
    window.addEventListener('blur', resetDrag);
    return () => {
      window.removeEventListener('blur', resetDrag);
      if (dragRef.current) {
        dragRef.current = null;
        postAction('setDragging', { value: false });
      }
    };
  }, []);

  const startDrag = (e: React.PointerEvent<HTMLDivElement>) => {
    if (!draggable || e.button !== 0 || dragRef.current) return;
    const target = e.target as Element;
    if (target.closest('button, input, a, [data-no-drag]')) return;

    const el = e.currentTarget;
    e.preventDefault();
    el.setPointerCapture(e.pointerId);
    const rect = el.getBoundingClientRect();
    dragRef.current = {
      pointerId: e.pointerId,
      startX: e.clientX,
      startY: e.clientY,
      origPxX: rect.left + ax * rect.width,
      origPxY: rect.top + ay * rect.height,
    };
    postAction('setDragging', { value: true });
  };

  const pointerPosition = (e: React.PointerEvent<HTMLDivElement>, drag: NonNullable<typeof dragRef.current>) => ({
    x: Math.max(left, Math.min(window.innerWidth - right, drag.origPxX + e.clientX - drag.startX)) / window.innerWidth,
    y: Math.max(top, Math.min(window.innerHeight - bottom, drag.origPxY + e.clientY - drag.startY)) / window.innerHeight,
  });

  const moveDrag = (e: React.PointerEvent<HTMLDivElement>) => {
    const drag = dragRef.current;
    if (drag?.pointerId === e.pointerId) setDragPosition(pointerPosition(e, drag));
  };

  const finishDrag = (e: React.PointerEvent<HTMLDivElement>) => {
    const drag = dragRef.current;
    if (drag?.pointerId !== e.pointerId) return;
    if (Math.hypot(e.clientX - drag.startX, e.clientY - drag.startY) < 2) {
      resetDrag();
      return;
    }
    const position = pointerPosition(e, drag);
    dragRef.current = null;
    setDragPosition(position);
    postAction('savePanelPos', { key: panelKey, ...position });
    postAction('setDragging', { value: false });
  };

  const cancelPointer = (e: React.PointerEvent<HTMLDivElement>) => {
    if (dragRef.current?.pointerId === e.pointerId) resetDrag();
  };

  return (
    <div
      ref={ref}
      data-overlay-panel={panelKey}
      onPointerDown={startDrag}
      onPointerMove={moveDrag}
      onPointerUp={finishDrag}
      onPointerCancel={cancelPointer}
      onLostPointerCapture={cancelPointer}
      style={{
        position: 'fixed',
        left: `clamp(${left}px, ${(dragPosition?.x ?? x) * 100}%, calc(100% - ${right}px))`,
        top: `clamp(${top}px, ${(dragPosition?.y ?? y) * 100}%, calc(100% - ${bottom}px))`,
        width: 'max-content',
        maxWidth: `calc((100vw - ${PANEL_INSET * 2}px) / ${scale})`,
        transform: `translate(${-ax * 100}%, ${-ay * 100}%) scale(${scale})`,
        transformOrigin: `${ax * 100}% ${ay * 100}%`,
      }}
      className={cn(
        passThrough && !draggable ? 'pointer-events-none select-none' : 'pointer-events-auto select-none',
        draggable && 'touch-none rounded-2xl ring-1 ring-indigo-400/80 shadow-[0_0_24px_rgba(129,140,248,0.32)]'
      )}
    >
      {draggable && (
        <div className="flex min-w-0 cursor-grab items-center gap-1.5 rounded-t-2xl border-b border-indigo-400/30 bg-[#141729]/95 px-2.5 py-1 text-[10px] font-semibold text-indigo-200 active:cursor-grabbing">
          <GripHorizontal className="size-3 shrink-0 text-indigo-400" />
          <span className="truncate">{title}</span>
        </div>
      )}
      {children}
    </div>
  );
}

function synergyPillClass(tone?: string) {
  if (tone === 'rose') return 'border-rose-400/55 bg-rose-500/20 text-rose-200';
  if (tone === 'gold') return 'border-amber-400/55 bg-amber-500/20 text-amber-200';
  if (tone === 'sky') return 'border-sky-400/55 bg-sky-500/20 text-sky-200';
  if (tone === 'teal') return 'border-teal-400/55 bg-teal-500/20 text-teal-200';
  return 'border-indigo-400/55 bg-indigo-500/22 text-indigo-200';
}

function AugmentBadgeCard({ stat, width, scale, french }: { stat: AugmentEntry; width: string; scale: number; french: boolean }) {
  return (
    <GlassCard surface="overlay" style={{ width, zoom: scale }} className="flex items-center gap-2 px-2.5 py-2">
      {stat.icon && <img src={iconUrl(stat.icon)} alt="" className="size-8 shrink-0 rounded" />}
      <div className="min-w-0 flex-1">
        <p className="truncate text-xs font-semibold text-white">{stat.name}</p>
        <p className="text-[10px] text-zinc-400">{(french && ({ Gold: 'Or', Silver: 'Argent', Prismatic: 'Prismatique' } as Record<string, string>)[stat.rarity]) || stat.rarity}</p>
      </div>
      <AugmentTier augment={stat} dark french={french} />
    </GlassCard>
  );
}

function SkillOrderMatrix({
  priority,
  levels,
  activeLevel,
  compact = false,
}: {
  priority: string[];
  levels: string[];
  activeLevel: number;
  compact?: boolean;
}) {
  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between gap-2">
        <div className="flex items-center gap-1.5">
          <span className="text-[10px] font-semibold uppercase tracking-wider text-zinc-400">
            Skill Priority
          </span>
          <div className="flex items-center gap-1">
            {priority.map((k, idx) => (
              <React.Fragment key={k + idx}>
                {idx > 0 && <ChevronRight className="size-3 text-zinc-500" />}
                <span
                  className={cn(
                    'inline-flex size-5 items-center justify-center rounded-md border text-[10px] font-bold',
                    SKILL_COLORS[k] ?? 'bg-zinc-700 text-white border-zinc-500'
                  )}
                >
                  {k}
                </span>
              </React.Fragment>
            ))}
          </div>
        </div>
        {activeLevel > 0 && (
          <span className="text-[11px] font-medium text-indigo-300 tabular-nums">
            Current Lv {activeLevel}
          </span>
        )}
      </div>

      <div className="overflow-x-auto">
        <div className="grid grid-cols-[26px_repeat(18,minmax(0,1fr))] gap-1 min-w-[460px]">
          <div />
          {Array.from({ length: 18 }, (_, i) => {
            const lv = i + 1;
            const isNow = lv === activeLevel;
            return (
              <div
                key={lv}
                className={cn(
                  'text-center text-[9px] font-semibold tabular-nums rounded py-0.5',
                  isNow ? 'bg-indigo-500/30 text-indigo-200' : 'text-zinc-500'
                )}
              >
                {lv}
              </div>
            );
          })}
          {SKILL_KEYS.map((sk) => (
            <React.Fragment key={sk}>
              <div className="flex items-center justify-center text-[10px] font-bold text-zinc-300">
                {sk}
              </div>
              {Array.from({ length: 18 }, (_, idx) => {
                const picked = (levels[idx] || '').toUpperCase() === sk;
                const isNow = idx + 1 === activeLevel;
                return (
                  <div
                    key={idx}
                    className={cn(
                      'flex items-center justify-center rounded border text-[9px] font-bold transition-colors',
                      compact ? 'h-4' : 'h-5',
                      picked
                        ? SKILL_COLORS[sk]
                        : isNow
                          ? 'border-indigo-400/35 bg-indigo-500/10'
                          : 'border-white/[0.06] bg-white/[0.02]'
                    )}
                  >
                    {picked ? sk : ''}
                  </div>
                );
              })}
            </React.Fragment>
          ))}
        </div>
      </div>
    </div>
  );
}

const ROLE_LIST = [
  { key: 'top', label: 'Top' },
  { key: 'jungle', label: 'Jungle' },
  { key: 'mid', label: 'Mid' },
  { key: 'adc', label: 'ADC' },
  { key: 'support', label: 'Support' },
] as const;

function InGameCommandSheet({ state, builds }: { state: AppState; builds: ReturnType<typeof useSavedBuilds> }) {
  const game = state.game;
  const inMatch = game.inGame && !state.preview;
  const guideReady = !inMatch || (game.modeKey === state.activeMode &&
    game.pickedChampion?.slug != null && game.pickedChampion.slug === state.selected?.slug);
  const champ = guideReady ? state.selected : game.pickedChampion;
  const modeForSheet = inMatch ? game.modeKey : state.activeMode;
  const isMayhem = modeForSheet === 'MAYHEM';
  const isArena = modeForSheet === 'ARENA';
  const hasAugments = isMayhem || isArena;
  const isRift = (state.game.modeKey ?? state.activeMode) === 'SR';
  const rawTab = state.inGameSheetTab || 'build';
  const tab = !hasAugments && rawTab === 'augments' ? 'build' : rawTab;
  const [itemId, setItemId] = useState<number | null>(null);
  const { saved, guide: variant, selectGuide } = builds;
  const dialog = useRef<HTMLDivElement>(null);
  useEffect(() => dialog.current?.focus({ preventScroll: true }), []);
  const trapTab = (event: React.KeyboardEvent<HTMLDivElement>) => {
    if (event.key !== 'Tab') return;
    const controls = dialog.current?.querySelectorAll<HTMLElement>('button:not(:disabled), input:not(:disabled), select:not(:disabled), a[href]');
    if (!controls?.length) { event.preventDefault(); return; }
    const first = controls[0];
    const last = controls[controls.length - 1];
    const active = document.activeElement;
    if (active === dialog.current || (event.shiftKey ? active === first : active === last)) {
      event.preventDefault();
      (event.shiftKey ? last : first).focus();
    }
  };
  const opening = state.buildPath.map((step) => ({
    id: step.stat.id, name: step.stat.name, icon: step.stat.icon, total: step.totalGold, components: step.components,
  }));
  const referenceItems = variant
    ? variant.items.map((item) => state.referenceItems?.find((entry) => entry.id === item.id) || { ...item, icon: '', total: 0, components: [] })
    : opening;
  const guideItem = referenceItems.find((item) => item.id === itemId);
  const french = state.settings.language === 'fr';
  const live = state.game.live;
  const me = useMemo(() => live.players.find((p) => p.me) ?? null, [live.players]);
  const minutes = Math.max(1 / 60, (live.seconds || 0) / 60);
  const csPerMin = me && live.seconds > 20 ? me.cs / minutes : 0;
  const focuses = ['Adaptive', 'Ability power', 'On-hit', 'Critical strike', 'Survival'];

  const modeLabel = (!state.preview && state.game.modeLabel) || (
    state.activeMode === 'MAYHEM'
      ? 'ARAM Mayhem'
      : state.activeMode === 'ARENA'
        ? 'Arena 2v2'
        : state.activeMode === 'ARAM'
          ? 'ARAM'
          : "Summoner's Rift");

  const blueTeam = live.players.filter((p) => p.team === 'ORDER');
  const redTeam = live.players.filter((p) => p.team === 'CHAOS');

  const renderPlayerRow = (p: LivePlayer) => {
    const pCsMin = live.seconds > 20 ? (p.cs / minutes).toFixed(1) : '0.0';
    const renderSpellIcon = (slot: 1 | 2, spName: string, spKey: string) => {
      if (!spKey) return null;
      return (
        <div
          key={slot}
          title={spName}
          className="relative flex size-6 items-center justify-center rounded border border-white/15 bg-black/50 overflow-hidden shrink-0"
        >
          <img
            src={spellIconUrl(spKey, state.ddVersion)}
            alt={spName}
            className="size-full object-cover"
          />
        </div>
      );
    };

    return (
      <div
        key={p.key}
        className={cn(
          'rounded-xl border px-2.5 py-2 space-y-1.5',
          p.me
            ? 'border-indigo-400/50 bg-indigo-500/12'
            : p.isLaneOpponent
              ? 'border-amber-400/45 bg-amber-500/[0.08]'
              : 'border-white/[0.07] bg-white/[0.03]'
        )}
      >
        <div className="grid grid-cols-[minmax(140px,1.25fr)_58px_68px_1fr] items-center gap-2">
          <div className="flex items-center gap-2 min-w-0">
            {p.championIcon ? (
              <div className="size-7 rounded-lg border border-white/15 overflow-hidden bg-black/50 shrink-0">
                <img
                  src={iconUrl(p.championIcon)}
                  alt={p.championName}
                  className="size-full object-cover scale-[1.14]"
                />
              </div>
            ) : (
              <div className="size-7 rounded-lg bg-white/10 shrink-0" />
            )}
            <div className="min-w-0">
              <div className="flex items-center gap-1 text-xs font-semibold text-zinc-100 leading-tight">
                <span className="truncate">{p.championName}</span>
                <span className="text-[10px] font-normal text-zinc-400 shrink-0">Lv{p.level}</span>
                {p.isLaneOpponent && (
                  <span className="rounded bg-amber-400/20 px-1 text-[8px] font-bold uppercase text-amber-200 shrink-0">
                    VS
                  </span>
                )}
              </div>
              <div className="flex items-center gap-1.5 truncate text-[10px] text-zinc-400">
                <span className="truncate">{p.keystone || (state.privateMode ? 'Hidden' : p.name)}</span>
                {(p.tags || []).slice(0, 1).map((t) => (
                  <span
                    key={t}
                    className={cn(
                      'rounded px-1 text-[8px] font-bold uppercase shrink-0',
                      t === 'Fed'
                        ? 'bg-rose-500/20 text-rose-200'
                        : t === 'CS Lead'
                          ? 'bg-teal-500/20 text-teal-200'
                          : 'bg-zinc-500/20 text-zinc-300'
                    )}
                  >
                    {t}
                  </span>
                ))}
              </div>
            </div>
          </div>
          <div className="text-center text-xs font-bold tabular-nums text-zinc-100">
            {p.kills}/{p.deaths}/{p.assists}
          </div>
          <div className="text-center text-[11px] tabular-nums text-zinc-300">
            {p.cs}{isRift && <span className="text-zinc-500"> ({pCsMin})</span>}
          </div>
          <div className="flex items-center justify-end gap-1">
            {renderSpellIcon(1, p.spell1, p.spellKey1)}
            {renderSpellIcon(2, p.spell2, p.spellKey2)}
            <div className="h-4 w-px bg-white/10 mx-0.5" />
            {p.items.slice(0, 5).map((id, i) => (
              <img
                key={`${id}-${i}`}
                src={itemIconUrl(id, state.ddVersion)}
                alt=""
                className="size-6 rounded border border-white/12 bg-black/50"
              />
            ))}
          </div>
        </div>
      </div>
    );
  };

  return (
    <motion.div
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      transition={{ duration: 0.14 }}
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/58 p-4 pointer-events-auto"
      onClick={() => postAction('closeInGameSheet')}
    >
      <motion.div
        initial={{ opacity: 0, scale: 0.97, y: 6 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        transition={{ duration: 0.18, ease: [0.22, 1, 0.36, 1] }}
        ref={dialog}
        role="dialog"
        aria-modal="true"
        aria-label={french ? 'Guide en partie' : 'In-game guide'}
        tabIndex={-1}
        onKeyDown={trapTab}
        onClick={(e) => e.stopPropagation()}
        style={{ clipPath: 'inset(0 round 16px)' }}
        className="rounded-2xl overflow-hidden shadow-[0_28px_80px_rgba(0,0,0,0.75)]"
      >
      <GlassCard
        surface="overlay"
        style={{ clipPath: 'inset(0 round 16px)' }}
        className="w-[min(980px,94vw)] h-[min(640px,88vh)] flex flex-col rounded-2xl overflow-hidden backdrop-blur-none border-white/[0.15] bg-[#0E1016]/97 shadow-none"
      >
        {/* Top Command Bar */}
        <div className="flex shrink-0 flex-wrap items-center justify-between gap-x-4 gap-y-2 border-b border-white/[0.08] px-3 py-2.5 sm:px-5 sm:py-3.5">
          <div className="flex items-center gap-3 min-w-0">
            {champ?.icon && (
              <div className="size-10 rounded-xl border border-white/15 overflow-hidden bg-black/50 shrink-0">
                <img
                  src={iconUrl(champ.icon)}
                  alt={champ.name}
                  className="size-full object-cover scale-[1.14]"
                />
              </div>
            )}
            <div className="min-w-0">
              <div className="flex items-center gap-2">
                <span className="truncate text-base font-bold text-white">
                  {champ?.name ?? 'Champion'}
                </span>
                {champ && <TierBadge tier={champ.tier} size="sm" dark />}
                {state.selected?.slug === champ?.slug && guideReady && state.selected?.patchTag && (
                  <span
                    className={cn(
                      'rounded-md border px-1.5 py-0.5 text-[9px] font-bold uppercase tracking-wider',
                      state.selected.patchTag === 'buff'
                        ? 'border-emerald-400/45 bg-emerald-500/18 text-emerald-300'
                        : state.selected.patchTag === 'nerf'
                          ? 'border-rose-400/45 bg-rose-500/18 text-rose-300'
                          : 'border-amber-400/45 bg-amber-500/18 text-amber-200'
                    )}
                  >
                    {state.selected.patchTag === 'adjust' ? 'ADJ' : state.selected.patchTag}
                  </span>
                )}
                <span className="rounded-md border border-indigo-400/30 bg-indigo-500/15 px-2 py-0.5 text-[10px] font-semibold text-indigo-200">
                  {modeLabel}
                </span>
              </div>
              <div className="mt-0.5 flex items-center gap-3 text-[11px] text-zinc-400 tabular-nums">
                {me && (
                  <span>
                    KDA <strong className="text-zinc-100">{me.kills}/{me.deaths}/{me.assists}</strong>
                  </span>
                )}
                {isRift && csPerMin > 0 && (
                  <span>
                    CS <strong className="text-zinc-100">{me?.cs}</strong> ({csPerMin.toFixed(1)}/m)
                  </span>
                )}
                {isRift && state.game.gold >= 0 && (
                  <span className="inline-flex items-center gap-1 text-amber-300 font-semibold">
                    <Coins className="size-3" />
                    {fmtGold(state.game.gold)}
                  </span>
                )}
                {live.clock && <span>{live.clock}</span>}
              </div>
            </div>
          </div>

          <div className="flex min-w-0 flex-wrap items-center gap-2">
            <SegmentedTabs
              size="sm"
              value={tab}
              onChange={(v) => postAction('setInGameSheetTab', { tab: v })}
              className="border-white/[0.1] bg-white/[0.04]"
              options={
                hasAugments
                  ? [
                      { value: 'build', label: french ? 'Build et compétences' : 'Build & Skills', icon: <Swords className="size-3" /> },
                      { value: 'augments', label: 'Augments', icon: <Sparkles className="size-3" /> },
                      { value: 'live', label: french ? 'Partie en cours' : 'Live Match', icon: <Timer className="size-3" /> },
                    ]
                  : [
                      { value: 'build', label: french ? 'Build et compétences' : 'Build & Skills', icon: <Swords className="size-3" /> },
                      { value: 'live', label: french ? 'Partie en cours' : 'Live Match', icon: <Timer className="size-3" /> },
                    ]
              }
            />
            <button
              type="button"
              onClick={() => postAction('closeInGameSheet')}
              aria-label={french ? 'Fermer le guide' : 'Close guide'}
              className="inline-flex items-center gap-1.5 rounded-lg border border-white/12 bg-white/[0.06] px-2.5 py-1.5 text-[11px] font-medium text-zinc-200 hover:bg-white/[0.14] hover:text-white cursor-pointer"
            >
              <Keycap keys={`Alt+${state.settings.hotkeySheet || 'G'}`} />
              <X className="size-3.5" />
            </button>
          </div>
        </div>

        {/* Scrollable Content */}
        <div className="min-h-0 flex-1 space-y-4 overflow-y-auto overscroll-contain px-3 py-3 sm:px-5 sm:py-4">
          {(tab === 'build' || tab === 'augments') && !guideReady && (
            <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-white/10 p-3 text-sm text-zinc-300">
              <p role="status">{french ? 'Le guide du champion joué est indisponible.' : 'The guide for the champion in this match is unavailable.'}</p>
              {game.pickedChampion && <Button size="xs" onClick={() => postAction('useLiveBuild')}>
                {french ? 'Afficher le guide de la partie' : 'Show guide for this match'}
              </Button>}
            </div>
          )}
          {tab === 'build' && guideReady && (
            <>
              {/* Role Bar (SR) + Focus Bar + Actions */}
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div className="flex flex-wrap items-center gap-3">
                  {state.activeMode === 'SR' && (
                    <div className="flex items-center gap-1 rounded-xl border border-white/[0.08] bg-white/[0.03] p-1">
                      {ROLE_LIST.map((r) => {
                        const active = state.activeRole === r.key;
                        const viable = state.availableRoles.includes(r.key);
                        const detected = state.detectedRole === r.key;
                        return (
                          <button
                            key={r.key}
                            type="button"
                            onClick={() => postAction('setRole', { role: r.key })}
                            className={cn(
                              'rounded-lg px-2 py-1 text-[10px] font-semibold transition-colors cursor-pointer',
                              active
                                ? 'bg-indigo-500 text-white'
                                : viable
                                  ? 'text-zinc-200 hover:bg-white/[0.08]'
                                  : 'text-zinc-500 hover:text-zinc-300 hover:bg-white/[0.05]'
                            )}
                          >
                            {r.label}
                            {detected ? ' · Auto' : ''}
                          </button>
                        );
                      })}
                    </div>
                  )}
                  <div className="flex flex-wrap items-center gap-1.5">
                    <span className="text-[11px] font-medium text-zinc-400 mr-0.5">Focus</span>
                    {focuses.map((f) => {
                      const active = state.settings.buildFocus === f;
                      return (
                        <button
                          key={f}
                          type="button"
                          onClick={() => postAction('setBuildFocus', { focus: f })}
                          className={cn(
                            'rounded-lg border px-2.5 py-1 text-[11px] font-semibold transition-colors cursor-pointer',
                            active
                              ? 'border-indigo-400/60 bg-indigo-500/22 text-indigo-100'
                              : 'border-white/[0.08] bg-white/[0.03] text-zinc-400 hover:text-zinc-100 hover:bg-white/[0.07]'
                          )}
                        >
                          {f}
                        </button>
                      );
                    })}
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  {state.importStatus && (
                    <span className="text-[10px] font-medium text-teal-300 truncate max-w-[200px]">
                      {state.importStatus}
                    </span>
                  )}
                  <Button
                    size="xs"
                    variant="secondary"
                    disabled={!champ || state.game.inGame}
                    onClick={() => postAction('exportItemSet', variant ? { guideItems: variant.items.map(({ id }) => id) } : undefined)}
                    className="border-white/12 bg-white/[0.06] text-zinc-100 hover:bg-white/[0.12]"
                  >
                    <Download className="size-3" />
                    Export to Shop
                  </Button>
                </div>
              </div>

              <section className="space-y-2 rounded-xl border border-white/10 bg-white/[0.03] p-3">
                <div className="flex justify-between gap-2 text-xs text-zinc-400">
                  <span>{french ? 'Guide de build' : 'Build reference'}</span><span>{state.modeSource}</span>
                </div>
                {!!saved.length && (
                  <select value={variant?.id ?? ''} onChange={(event) => { selectGuide(event.target.value); setItemId(null); }}
                    aria-label={french ? 'Choisir un build' : 'Choose a build'} className="rounded bg-zinc-900 p-1.5 text-xs text-white">
                    <option value="">{french ? 'Guide par défaut' : 'Default guide'}</option>
                    {saved.map((build) => <option key={build.id} value={build.id}>{build.name}</option>)}
                  </select>
                )}
                <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
                  {referenceItems.map((item) => (
                    <button key={item.id} type="button" aria-pressed={itemId === item.id}
                      onClick={() => setItemId(item.id)}
                      className={cn('flex items-center gap-2 rounded-lg border p-2 text-left text-xs hover:border-indigo-400/50',
                        itemId === item.id ? 'border-indigo-400/60 bg-indigo-500/15' : 'border-white/10')}>
                      <img src={item.icon ? iconUrl(item.icon) : itemIconUrl(item.id, state.ddVersion)} alt="" className="size-7 rounded" />
                      <span className="flex-1 font-semibold text-white">{item.name}</span>
                      {item.total > 0 && <span className="text-zinc-400">{fmtGold(item.total)}</span>}
                    </button>
                  ))}
                </div>
                {guideItem && (
                  <div className="border-t border-white/10 pt-2 text-xs text-zinc-300">
                    <strong className="text-white">{guideItem.name}</strong>
                    {guideItem.components.length > 0 && (
                      <ul className="mt-2 flex flex-wrap gap-2">
                        {guideItem.components.map((component, index) => (
                          <li key={`${component.id}-${index}`} className="flex items-center gap-1.5">
                            <img src={component.icon ? iconUrl(component.icon) : itemIconUrl(component.id, state.ddVersion)} alt="" className="size-4 rounded" />
                            {component.name} · {fmtGold(component.price)}
                          </li>
                        ))}
                      </ul>
                    )}
                  </div>
                )}
                {!referenceItems.length && <p className="text-xs text-zinc-400">{french ? 'Guide indisponible.' : 'Build guide unavailable.'}</p>}
              </section>

              {state.skills && state.skills.levels.length > 0 && (
                <div className="rounded-xl border border-white/[0.08] bg-white/[0.03] p-3.5">
                  <SkillOrderMatrix
                    priority={state.skills.priority}
                    levels={state.skills.levels}
                    activeLevel={0}
                    compact
                  />
                </div>
              )}

              {/* Visual Runes Compact Strip */}
              {state.runes?.perkDetails && state.runes.perkDetails.length >= 6 && (
                <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-white/[0.08] bg-white/[0.03] px-3.5 py-2.5">
                  <div className="flex items-center gap-2">
                    {state.runes.primaryMeta?.icon && (
                      <img src={state.runes.primaryMeta.icon} alt="" className="size-5 shrink-0" />
                    )}
                    <span className="text-[10px] font-bold uppercase tracking-wider text-zinc-400">
                      Runes
                    </span>
                  </div>
                  <div className="flex flex-wrap items-center gap-1.5">
                    {state.runes.perkDetails.map((rk, i) => (
                      <div
                        key={`${rk.id}-${i}`}
                        title={rk.name}
                        className={cn(
                          'flex items-center justify-center rounded-full border bg-black/55',
                          i === 0
                            ? 'size-8 border-amber-400/70 p-0.5'
                            : i < 6
                              ? 'size-6 border-white/15 p-0.5'
                              : 'size-5 border-indigo-400/40 p-0.5'
                        )}
                      >
                        {rk.icon && <img src={rk.icon} alt={rk.name} className="size-full rounded-full object-contain" />}
                      </div>
                    ))}
                  </div>
                  <span className="text-[10px] text-zinc-400">{state.runes.primaryMeta?.name} + {state.runes.secondaryMeta?.name}</span>
                </div>
              )}

              {/* Starters / Prismatic / Boots / Spells Compact Strip */}
              <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                {(isArena
                  ? [
                      { title: 'Prismatic Items', items: (state.prismaticItems || []).slice(0, 4) },
                      { title: 'Boots', items: state.boots.slice(0, 3) },
                      { title: 'Situational', items: state.bestItems.slice(0, 3) },
                    ]
                  : [
                      { title: 'Starters', items: state.starters.slice(0, 3) },
                      { title: 'Boots', items: state.boots.slice(0, 3) },
                      { title: 'Summoner Spells', items: state.spells.slice(0, 3) },
                    ]
                ).map((group) => (
                  <div
                    key={group.title}
                    className="rounded-xl border border-white/[0.08] bg-white/[0.03] p-3"
                  >
                    <div className="text-[10px] font-bold uppercase tracking-wider text-zinc-400 mb-2">
                      {group.title}
                    </div>
                    <div className="space-y-1.5">
                      {group.items.length === 0 ? (
                        <div className="text-[11px] text-zinc-500">No data</div>
                      ) : (
                        group.items.map((st) => (
                          <div key={st.name} className="flex items-center justify-between gap-2 text-xs">
                            <div className="flex items-center gap-2 min-w-0">
                              {st.icon && (
                                <img
                                  src={iconUrl(st.icon)}
                                  alt=""
                                  className="size-5 rounded border border-white/12 shrink-0"
                                />
                              )}
                              <span className="truncate text-zinc-200">{st.name}</span>
                            </div>
                            <span className="text-[11px] font-semibold text-indigo-300 tabular-nums shrink-0">
                              {isArena ? (parsePct(st.pick) ? `${parsePct(st.pick)} PR` : '—') : fmtPct(st.rate)}
                            </span>
                          </div>
                        ))
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </>
          )}

          {tab === 'augments' && guideReady && (
            <AugmentGuide key={`${state.activeMode}:${champ?.slug}`} augments={state.allAugments} dark french={french} />
          )}

          {tab === 'live' && (
            <div className="space-y-4">
              {live.objectives.length > 0 && (
                <div className="grid grid-cols-2 gap-2.5 sm:grid-cols-4">
                  {live.objectives.map((objective) => (
                    <div key={objective.key} className="flex justify-between gap-2 rounded-xl border border-white/10 px-3 py-2 text-xs">
                      <span>{objective.name}</span><span>{objective.status}</span>
                    </div>
                  ))}
                </div>
              )}

              {live.players.length === 0 ? (
                <div className="rounded-xl border border-white/[0.08] bg-white/[0.03] p-8 text-center text-sm text-zinc-400">
                  Waiting for live match scoreboard from Riot Live Client API.
                </div>
              ) : (
                <div className="grid grid-cols-1 lg:grid-cols-2 gap-3">
                  <div className="space-y-1.5">
                    <div className="text-[10px] font-bold uppercase tracking-wider text-sky-300 px-1">
                      Blue Team
                    </div>
                    {blueTeam.map(renderPlayerRow)}
                  </div>
                  <div className="space-y-1.5">
                    <div className="text-[10px] font-bold uppercase tracking-wider text-rose-300 px-1">
                      Red Team
                    </div>
                    {redTeam.map(renderPlayerRow)}
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      </GlassCard>
      </motion.div>
    </motion.div>
  );
}

function fmtSecs(secs: number): string {
  const s = Math.max(0, Math.ceil(secs));
  return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;
}

const CS_ROLES = [
  { key: 'ALL', label: 'All' },
  { key: 'TOP', label: 'Top' },
  { key: 'JUNGLE', label: 'Jg' },
  { key: 'MID', label: 'Mid' },
  { key: 'ADC', label: 'ADC' },
  { key: 'SUPPORT', label: 'Sup' },
] as const;

function DraftChampionBadge({ champion, portrait = false, french = false }: {
  champion: Omit<DraftChampion, 'icon'> & { icon?: string };
  portrait?: boolean;
  french?: boolean;
}) {
  const tierLabel = champion.tier ? `Tier ${champion.tier}` : (french ? 'Tier indisponible' : 'Tier unavailable');
  return (
    <div className="inline-flex max-w-full items-center gap-1.5 rounded-lg border border-white/12 bg-[#10131c]/95 px-1.5 py-1 text-white">
      {portrait && champion.icon && <img src={iconUrl(champion.icon)} alt="" className="size-6 shrink-0 rounded-md object-cover" />}
      <span className="min-w-0 truncate text-[11px] font-semibold leading-tight">{champion.name}</span>
      <span className="shrink-0" title={tierLabel} aria-label={tierLabel}><TierBadge tier={champion.tier} size="xs" dark /></span>
    </div>
  );
}

function ChampSelectTierDrawer({ state }: { state: AppState }) {
  const [search, setSearch] = useState('');
  const [tierFilter, setTierFilter] = useState<'ALL' | 'S+' | 'S' | 'A'>('ALL');
  const open = Boolean(state.champTierOpen);
  const french = state.settings.language === 'fr';
  const mode = state.champSelectMode ?? state.activeMode;
  const isSR = mode === 'SR';
  const isArena = mode === 'ARENA';
  const listing = state.champSelectTierList ?? state.tierList;
  const modeLabel = isSR ? 'Faille' : isArena ? 'Arena' : mode === 'MAYHEM' ? 'ARAM Mayhem' : 'ARAM';
  const title = `Tier list · ${isSR && !french ? 'Rift' : modeLabel}`;
  const role = (state.settings.tierRole || 'ALL').toUpperCase();
  const rows = useMemo(() => {
    const normalize = (s: string) => s.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
    const q = normalize(search.trim());
    return (listing || []).filter((row) =>
      (!isSR || role === 'ALL' || row.role?.toUpperCase() === role) &&
      (tierFilter === 'ALL' || row.tier?.toUpperCase() === tierFilter) &&
      (!q || normalize(row.name).includes(q) || normalize(row.slug).includes(q)));
  }, [listing, search, tierFilter, isSR, role]);

  return (
    <AnimatePresence mode="wait">
      {!open ? (
        <motion.div
          key="cs-tier-pill"
          initial={{ opacity: 0, x: 14, scale: 0.95 }}
          animate={{ opacity: 1, x: 0, scale: 1 }}
          exit={{ opacity: 0, x: 12, scale: 0.95 }}
          transition={{ duration: 0.16, ease: [0.22, 1, 0.36, 1] }}
          className="pointer-events-auto fixed z-40" style={draftDrawerStyle(false)}
        >
          <button
            type="button"
            onClick={() => postAction('setChampTierOpen', { value: true })}
            aria-label={title}
            className="group inline-flex h-full w-full items-center gap-1.5 rounded-xl border border-indigo-400/50 bg-[#0D101B]/94 px-3 py-1.5 text-[11px] font-bold text-white shadow-[0_12px_30px_rgba(0,0,0,0.68),0_0_18px_rgba(99,102,241,0.24)] backdrop-blur-md transition-all duration-150 hover:border-indigo-300/85 hover:bg-[#141A2E]/96 active:scale-95 cursor-pointer"
          >
            <Trophy className="size-3.5 text-amber-300 shrink-0 transition-transform duration-150 group-hover:scale-110" />
            <span className="min-w-0 flex-1 truncate text-left">{title}</span>
            <ChevronRight className="size-3 text-indigo-300 transition-transform duration-150 group-hover:translate-x-0.5" />
          </button>
        </motion.div>
      ) : (
        <motion.div
          key="cs-tier-drawer"
          initial={{ opacity: 0, x: 26, scale: 0.97 }}
          animate={{ opacity: 1, x: 0, scale: 1 }}
          exit={{ opacity: 0, x: 22, scale: 0.97 }}
          transition={{ type: 'spring', stiffness: 420, damping: 32, mass: 0.7 }}
          className="pointer-events-auto fixed z-40 flex flex-col" style={draftDrawerStyle(true)}
        >
          <GlassCard
            surface="overlay"
            highlight={isArena ? 'gold' : 'accent'}
            className="flex h-full flex-col overflow-hidden bg-[#0B0E18]/95 shadow-[0_22px_54px_rgba(0,0,0,0.82)]"
          >
        <div className="flex items-center justify-between gap-2 border-b border-white/[0.08] px-3 py-2.5 shrink-0">
          <div className="flex items-center gap-2 min-w-0">
            <Trophy className="size-3.5 text-amber-300 shrink-0" />
            <div className="min-w-0">
              <div className="truncate text-xs font-bold text-white leading-tight">{title}</div>
              <div className="text-[9px] text-zinc-400 leading-tight">
                {isArena ? 'Top 4 · Pick · Ban' : french ? 'Cliquer pour consulter le build' : 'Click a champion to see the build'}
              </div>
            </div>
          </div>
          <button
            type="button"
            onClick={() => postAction('setChampTierOpen', { value: false })}
            className="flex size-6 items-center justify-center rounded-lg border border-white/10 bg-white/[0.05] text-zinc-300 hover:bg-white/[0.14] hover:text-white cursor-pointer shrink-0"
            aria-label={french ? 'Réduire la tier list' : 'Collapse tier list'}
          >
            <X className="size-3.5" />
          </button>
        </div>

        <div className="space-y-2 border-b border-white/[0.07] px-3 py-2 shrink-0">
          <div className="relative">
            <Search className="pointer-events-none absolute left-2.5 top-1/2 size-3 -translate-y-1/2 text-zinc-400" />
            <input
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              aria-label={french ? 'Rechercher un champion' : 'Search champion'}
              placeholder={french ? 'Chercher…' : 'Search…'}
              className="w-full rounded-lg border border-white/12 bg-black/45 py-1.5 pl-7 pr-2.5 text-[11px] text-white placeholder:text-zinc-500 focus:border-indigo-400 focus:outline-none"
            />
          </div>

          <div className="flex items-center justify-between gap-1.5">
            <div className="flex items-center gap-1">
              {(['ALL', 'S+', 'S', 'A'] as const).map((tf) => (
                <button
                  key={tf}
                  type="button"
                  onClick={() => setTierFilter(tf)}
                  className={cn(
                    'rounded-md px-2 py-0.5 text-[10px] font-bold transition-colors cursor-pointer',
                    tierFilter === tf
                      ? 'bg-indigo-500 text-white'
                      : 'bg-white/[0.05] text-zinc-400 hover:text-white'
                  )}
                >
                  {tf === 'ALL' ? (french ? 'Tous' : 'All') : tf}
                </button>
              ))}
            </div>
            <span className="font-mono-tech text-[9px] text-zinc-400 tabular-nums">
              {rows.length}
            </span>
          </div>

          {isSR && (
            <div className="grid grid-cols-6 gap-1">
              {CS_ROLES.map((r) => {
                const active = (state.settings.tierRole || 'ALL').toUpperCase() === r.key;
                return (
                  <button
                    key={r.key}
                    type="button"
                    onClick={() => postAction('setTierFilter', { role: r.key })}
                    className={cn(
                      'rounded-md py-1 text-[9px] font-bold uppercase transition-colors cursor-pointer',
                      active
                        ? 'bg-indigo-500/90 text-white'
                        : 'bg-white/[0.04] text-zinc-400 hover:text-white'
                    )}
                  >
                    {r.label}
                  </button>
                );
              })}
            </div>
          )}
        </div>

        <div className="flex-1 overflow-y-auto divide-y divide-white/[0.05] px-1.5 py-1">
          {rows.length === 0 ? (
            <div className="py-10 text-center text-[11px] text-zinc-400">
              {listing.length === 0 ? (french ? 'Tier list indisponible' : 'Tier list unavailable') : (french ? 'Aucun champion trouvé' : 'No matching champions')}
            </div>
          ) : (
            rows.map((row, idx) => {
              const isSelected = state.selected?.slug === row.slug;
              return (
                <button
                  key={`${row.slug}-${row.role}-${idx}`}
                  type="button"
                  onClick={() =>
                    postAction('browseDraftBuild', {
                      slug: row.slug,
                      role: isSR ? row.role : '',
                    })
                  }
                  className={cn(
                    'flex w-full items-center justify-between gap-2 rounded-lg px-2 py-1.5 text-left transition-colors cursor-pointer',
                    isSelected
                      ? 'bg-indigo-500/20 ring-1 ring-inset ring-indigo-400/45'
                      : 'hover:bg-white/[0.06]'
                  )}
                >
                  <div className="flex items-center gap-2 min-w-0">
                    <span className="w-5 text-center font-mono-tech text-[10px] font-bold text-zinc-500 tabular-nums shrink-0">
                      {row.rank || idx + 1}
                    </span>
                    {row.icon && (
                      <div className="size-6 rounded-md border border-white/15 overflow-hidden bg-black/50 shrink-0">
                        <img
                          src={iconUrl(row.icon)}
                          alt={row.name}
                          className="size-full object-cover scale-[1.14]"
                        />
                      </div>
                    )}
                    <div className="min-w-0">
                      <div className="flex items-center gap-1 min-w-0">
                        <span className="truncate text-[11px] font-semibold text-white">{row.name}</span>
                        {row.patchTag && (
                          <span
                            className={cn(
                              'rounded px-1 py-0.25 text-[7px] font-extrabold uppercase leading-none shrink-0',
                              row.patchTag === 'buff'
                                ? 'bg-emerald-500/25 text-emerald-300'
                                : row.patchTag === 'nerf'
                                  ? 'bg-rose-500/25 text-rose-300'
                                  : 'bg-amber-500/25 text-amber-200'
                            )}
                          >
                            {row.patchTag === 'buff' ? '▲' : row.patchTag === 'nerf' ? '▼' : '~'}
                          </span>
                        )}
                      </div>
                      <div className="text-[9px] text-zinc-400 tabular-nums leading-tight">
                        {isSR && row.role ? `${row.role} · ` : ''}
                        PR {fmtPct(row.pickRate)}
                        {(isArena || isSR) && row.banRate > 0 ? ` · Ban ${fmtPct(row.banRate)}` : ''}
                      </div>
                    </div>
                  </div>
                  <div className="flex items-center gap-1.5 shrink-0">
                    <TierBadge tier={row.tier} size="xs" dark />
                    <span
                      className={cn(
                        'w-10 text-right font-mono-tech text-[10px] font-bold tabular-nums',
                        row.winRate >= 52
                          ? 'text-indigo-300'
                          : row.winRate < 48.5
                            ? 'text-rose-300'
                            : 'text-zinc-200'
                      )}
                    >
                      {fmtPct(row.winRate)}
                    </span>
                  </div>
                </button>
              );
            })
          )}
        </div>
      </GlassCard>
        </motion.div>
      )}
    </AnimatePresence>
  );
}

export function OverlayView({ state }: { state: AppState }) {
  const builds = useSavedBuilds(state, true);
  const [jungleSide, setJungleSide] = useState<'red' | 'blue'>('red');
  const [tickDelta, setTickDelta] = useState(0);
  const [liveMapScale, setLiveMapScale] = useState<number | null>(null);
  const mapResizeRef = useRef<{
    pointerId: number;
    startX: number;
    startY: number;
    startScale: number;
    basePx: number;
    leftSide: boolean;
  } | null>(null);

  // Smooth 1-second local countdown interpolation between 1.3s Riot API polls
  useEffect(() => {
    setTickDelta(0);
    if (!state.game.inGame) return;
    const id = window.setInterval(() => setTickDelta((d) => (d < 5 ? d + 1 : d)), 1000);
    return () => window.clearInterval(id);
  }, [state.game.inGame, state.game.live.seconds]);

  // Guarantee minimap resize never leaves OverlayDragging stuck if editing ends, blur occurs, or overlay unmounts
  useEffect(() => {
    if (!state.editing && mapResizeRef.current) {
      mapResizeRef.current = null;
      setLiveMapScale(null);
      postAction('setDragging', { value: false });
    }
  }, [state.editing]);

  useEffect(() => {
    if (!mapResizeRef.current) setLiveMapScale(null);
  }, [state.settings.minimapScale]);

  useEffect(() => {
    const cancelResize = () => {
      if (mapResizeRef.current) {
        mapResizeRef.current = null;
        setLiveMapScale(null);
        postAction('setDragging', { value: false });
      }
    };
    window.addEventListener('blur', cancelResize);
    return () => {
      window.removeEventListener('blur', cancelResize);
      cancelResize();
    };
  }, []);

  // Prevent Chromium Tab focus-scrolling completely & close focused Alt+G Sheet on Escape
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Tab' && !state.inGameSheet) {
        e.preventDefault();
        e.stopPropagation();
      } else if (e.key === 'Escape' && state.inGameSheet) {
        e.preventDefault();
        postAction('closeInGameSheet');
      }
    };
    window.addEventListener('keydown', onKey, true);
    return () => window.removeEventListener('keydown', onKey, true);
  }, [state.inGameSheet]);

  if ((!state.settings.overlay && !state.preview) || !state.overlayVisible ||
      (!state.preview && state.game.modeKey === '')) {
    return null;
  }

  // Zero-lag centered Alt+G / F9 In-Game Command Overlay
  if (state.inGameSheet) {
    return <InGameCommandSheet state={state} builds={builds} />;
  }

  const editing = state.editing;
  const scale = state.settings.scale || 1;
  const saved = state.settings.panels || {};
  const widgetScale = (id: string) => state.settings.widgetScale?.[id] ?? scale;
  const live = state.game.live;
  const liveSeconds = (live.seconds || 0) + (state.game.inGame ? tickDelta : 0);
  const liveClock = liveSeconds > 0 ? fmtSecs(liveSeconds) : live.clock;
  const smoothObjectives = (live.objectives || []).map((o) => {
    if (o.alive || o.status === 'DONE' || o.secondsLeft <= 0) return o;
    const rem = Math.max(0, o.secondsLeft - tickDelta);
    return { ...o, secondsLeft: rem, alive: rem <= 0, status: rem <= 0 ? 'UP' : fmtSecs(rem) };
  });
  const me = live.players.find((p) => p.me) ?? null;
  const pScene = state.preview ? state.previewScene || '' : '';

  const augmentPreview = pScene === 'augments';
  const previewCount = state.offered.filter(offer => offer.stat).length;
  const previewGap = Math.min(0.21, 1 / (previewCount + 1));
  const offeredWithStat = state.offered.flatMap((offer) => {
    if (!offer.stat) return [];
    const x = augmentPreview ? 0.5 + (offer.slot - (previewCount - 1) / 2) * previewGap : offer.x;
    return x > 0 && x < 1 ? [{ ...offer, stat: offer.stat, x }] : [];
  });

  const showChampSelect =
    pScene === 'bench' || (!state.preview && state.game.inSelection && !state.game.inGame);

  const showAugments =
    (augmentPreview || (state.game.inGame && state.augmentsVisible && state.settings.ocr)) &&
    offeredWithStat.length > 0;

  const titleYs = offeredWithStat.map((o) => o.y).filter((y) => y > 0).sort((a, b) => a - b);
  const rowY = titleYs.length > 0
    ? (titleYs[Math.floor((titleYs.length - 1) / 2)] + titleYs[Math.floor(titleYs.length / 2)]) / 2
    : 0.40;
  const badgeOffset = state.settings.badgeOffsetY || 0;
  const baseAugY = Math.max(0.11, Math.min(0.46, rowY - 0.19 + badgeOffset));
  const moveAugments = (delta: number) => postAction('updateSetting', {
    key: 'badgeOffsetY',
    value: Math.max(-0.15, Math.min(0.15, Math.round((badgeOffset + delta) * 100) / 100)),
  });

  const showInGameHud =
    (state.game.inGame ||
      (state.preview && (pScene === 'sr_hud' || (pScene === '' && !state.game.inSelection)))) &&
    pScene !== 'augments' &&
    pScene !== 'bench';
  const hudItems = builds.guide
    ? builds.guide.items.map((item) => ({
        ...item, icon: state.referenceItems?.find((reference) => reference.id === item.id)?.icon,
      }))
    : state.buildPath.map(({ stat }) => stat);
  const showBuildHud = showInGameHud && state.settings.showBuildOverlay && hudItems.length > 0 &&
    (!state.game.inGame || state.preview || state.activeMode === state.game.modeKey);
  const isSR = pScene === 'sr_hud' || (state.game.inGame
    ? (state.game.modeKey ?? (['CLASSIC', 'SWIFTPLAY'].includes(state.game.mode) ? 'SR' : '')) === 'SR'
    : state.activeMode === 'SR');
  const metrics = benchmarkMetrics(state.settings);
  const showPerfStrip = isSR && showInGameHud && state.settings.showPerfStrip && (me !== null || state.preview);
  const showJungleWidget =
    showInGameHud &&
    state.settings.showJungleWidget &&
    isSR &&
    (smoothObjectives.length > 0 || state.preview);

  // Compute live performance metrics
  const minutes = Math.max(1 / 60, liveSeconds / 60);
  const csPerMin = me && liveSeconds > 20 ? me.cs / minutes : 0;
  const myTeamKills = me
    ? live.players
        .filter((p) => p.team === me.team)
        .reduce((acc, p) => acc + p.kills, 0)
    : 0;
  const kp =
    me && myTeamKills > 0
      ? Math.min(100, Math.round(((me.kills + me.assists) / myTeamKills) * 100))
      : 0;

  const teamSide: 'ORDER' | 'CHAOS' = me?.team === 'CHAOS' ? 'CHAOS' : 'ORDER';
  const mapCamps = MINIMAP_PATHS[teamSide][jungleSide];
  const minimapScale = liveMapScale ?? (state.settings.minimapScale || 1);
  const minimapLeft = !!state.settings.minimapLeft;
  const minimapVh = 24.0 * minimapScale;
  const isJungler =
    state.activeRole === 'jungle' ||
    me?.spellKey1 === 'SummonerSmite' ||
    me?.spellKey2 === 'SummonerSmite' ||
    state.preview;
  const showEarlyClearPath = isJungler && (liveSeconds < 245 || editing || state.preview);

  const previewObjs =
    smoothObjectives.length > 0
      ? smoothObjectives
      : state.preview
        ? [
            { key: 'dragon', name: 'Dragon', status: '1:42', alive: false, secondsLeft: 102 },
            { key: 'grubs', name: 'Voidgrubs', status: 'UP', alive: true, secondsLeft: 0 },
          ]
        : [];
  const dragonObj = previewObjs.find((o) => o.key === 'dragon') ?? null;
  const topPitCandidates = previewObjs.filter((o) => o.key !== 'dragon' && o.status !== 'DONE');
  const topPitObj =
    topPitCandidates.find((o) => o.alive) ??
    [...topPitCandidates].sort((a, b) => a.secondsLeft - b.secondsLeft)[0] ??
    null;

  const shortObjLabel = (key: string) =>
    key === 'dragon' ? 'DRK' : key === 'grubs' ? 'GRUB' : key === 'herald' ? 'HER' : 'BAR';

  return (
    <div className="fixed inset-0 pointer-events-none overflow-hidden">
      {/* Top Editing Unlock Banner */}
      {editing && (
        <div className="pointer-events-auto fixed top-2 left-1/2 z-50 w-max max-w-[calc(100vw-16px)] -translate-x-1/2">
          <div className="flex items-center gap-2 whitespace-nowrap rounded-full border border-indigo-400/50 bg-[#101322]/95 px-2.5 py-1.5 text-xs font-medium text-indigo-100 shadow-2xl backdrop-blur-xl">
            <Status variant="success" pulse>
              Overlay Unlocked
            </Status>
            {showAugments ? (
              <div className="flex items-center gap-1" aria-label="Augment badge height">
                <button type="button" aria-label="Move augment badges up" disabled={badgeOffset <= -0.15} onClick={() => moveAugments(-0.02)}
                  className="grid size-5 cursor-pointer place-items-center rounded bg-white/10 hover:bg-white/20 disabled:opacity-40">
                  <Minus className="size-3" />
                </button>
                <button type="button" aria-label="Move augment badges down" disabled={badgeOffset >= 0.15} onClick={() => moveAugments(0.02)}
                  className="grid size-5 cursor-pointer place-items-center rounded bg-white/10 hover:bg-white/20 disabled:opacity-40">
                  <Plus className="size-3" />
                </button>
              </div>
            ) : (
              <span className="hidden truncate text-zinc-300 min-[680px]:inline">
                Drag widgets · Ctrl+Tab or Alt+{state.settings.hotkeyEdit || 'O'} to lock
              </span>
            )}
            <details className="relative">
              <summary className="cursor-pointer rounded-full border border-white/15 px-2.5 py-0.5 text-[11px]">
                {state.settings.language === 'fr' ? 'Aperçu' : 'Preview'}
              </summary>
              <div className="absolute left-1/2 top-full mt-3 w-[min(420px,calc(100vw-20px))] -translate-x-1/2 whitespace-normal rounded-xl border border-indigo-400/40 bg-[#101322] p-3 shadow-2xl">
                <OverlayLayoutPreview state={state} french={state.settings.language === 'fr'} dark />
              </div>
            </details>
            <button
              type="button"
              onClick={() => postAction('resetPositions')}
              className="inline-flex items-center gap-1 rounded-full border border-white/15 bg-white/[0.07] px-2.5 py-0.5 text-[11px] font-semibold text-white hover:bg-white/[0.15] cursor-pointer"
            >
              <RotateCcw className="size-3" />
              Reset
            </button>
            <button
              type="button"
              onClick={() => postAction('finishEdit')}
              className="inline-flex items-center gap-1 rounded-full border border-indigo-400/50 bg-indigo-500/30 px-2.5 py-1 text-[11px] font-semibold leading-none text-white hover:bg-indigo-500/50 cursor-pointer"
            >
              <Check className="size-3" />
              Done
            </button>
          </div>
        </div>
      )}

      {showChampSelect && (
        <>
          {state.game.champCards?.map((card) => {
            const neighbours = state.game.champCards!.filter((other) => other.slug !== card.slug);
            const gap = Math.min(card.x * 2, (1 - card.x) * 2, ...neighbours.map((other) => Math.abs(other.x - card.x)));
            return (
              <motion.div key={card.slug} data-champion-card-tier={card.slug}
                initial={{ opacity: 0, y: 4 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.14 }}
                className="pointer-events-none fixed z-30 -translate-x-1/2"
                style={{ left: `${card.x * 100}vw`, top: `${Math.min(92, (card.y + 0.041) * 100)}vh` }}>
                <div style={{ maxWidth: `min(138px, calc(${gap * 100}vw - 8px))` }}>
                  <DraftChampionBadge champion={card} french={state.settings.language === 'fr'} />
                </div>
              </motion.div>
            );
          })}
          {(() => {
            const picked = state.game.pickedChampion;
            const cardSlugs = new Set(state.game.champCards?.map((card) => card.slug));
            const bench = state.settings.showBenchBar
              ? state.game.bench.filter((champion) => !cardSlugs.has(champion.slug) && champion.slug !== picked?.slug) : [];
            const french = state.settings.language === 'fr';
            if (!picked && !bench.length) return null;
            return (
              <DraggableSlot panelKey="champ_select_ribbon" title={french ? 'Sélection de champion' : 'Champion select'}
                defaultX={0.5} defaultY={0.008} anchor="top-center" editing={editing} savedPanels={saved}
                scale={widgetScale('champ_select_ribbon')} passThrough>
                <GlassCard surface="overlay" className="flex max-w-[78vw] flex-wrap items-center gap-2 p-1.5">
                  {picked && <div data-draft-current className="flex items-center gap-1.5">
                    <span className="text-[10px] text-zinc-300">{french ? 'Sélection' : 'Selected'}</span>
                    <DraftChampionBadge champion={picked} portrait french={french} />
                  </div>}
                  {!!bench.length && <div data-draft-bench className="flex flex-wrap items-center gap-1">
                    <span className="px-1 text-[10px] text-zinc-300">{french ? 'Banc' : 'Bench'}</span>
                    {bench.map((champion) => <DraftChampionBadge key={champion.slug} champion={champion} portrait french={french} />)}
                  </div>}
                </GlassCard>
              </DraggableSlot>
            );
          })()}
          {(!editing || pScene === 'bench') && <ChampSelectTierDrawer state={state} />}
        </>
      )}

      {showAugments && (
        <>
          <DraggableSlot
            panelKey="augment_row"
            title="Augments"
            defaultX={0.5}
            defaultY={baseAugY}
            anchor="center-bottom"
            editing={editing}
            scale={1}
            movable={false}
            passThrough
          >
            <div className="grid w-[calc(100vw-8px)] grid-cols-[minmax(0,1fr)] items-end">
              {offeredWithStat.map((slot) => {
                const nearestGap = Math.min(
                  slot.x * 2, (1 - slot.x) * 2,
                  ...(augmentPreview ? offeredWithStat : state.offered).filter((other) => other.x > 0 && other.slot !== slot.slot)
                    .map((other) => Math.abs(other.x - slot.x)),
                );
                const width = `min(228px, calc((${nearestGap * 100}vw - 8px) / ${scale}))`;
                return (
                  <div key={slot.slot} data-augment-badge={slot.slot}
                    className="col-start-1 row-start-1 w-max -translate-x-1/2 justify-self-start"
                    style={{ marginLeft: `calc(${slot.x * 100}vw - ${PANEL_INSET}px)` }}
                  >
                    <AugmentBadgeCard stat={slot.stat} width={width} scale={scale} french={state.settings.language === 'fr'} />
                  </div>
                );
              })}
            </div>
          </DraggableSlot>

        </>
      )}

      {showBuildHud && (
        <DraggableSlot panelKey="build_hud" title="Build Guide" defaultX={0.012} defaultY={0.018}
          editing={editing} savedPanels={saved} scale={widgetScale("build_hud")} passThrough>
          <GlassCard surface="overlay" className="px-2.5 py-2">
            <p className="max-w-40 truncate text-[10px] font-semibold text-zinc-300">
              {builds.guide?.name ?? 'Build'} · Alt+{state.settings.hotkeySheet || 'G'}
            </p>
            <div className="mt-1.5 flex gap-1">
              {hudItems.slice(0, 4).map((item) => (
                <img key={item.id} src={item.icon ? iconUrl(item.icon) : itemIconUrl(item.id, state.ddVersion)}
                  alt={item.name} className="size-6 rounded" />
              ))}
            </div>
          </GlassCard>
        </DraggableSlot>
      )}

      {showPerfStrip && (
        <DraggableSlot panelKey="perf_strip_center" title="Performance" defaultX={0.5} defaultY={0.042}
          anchor="top-center" editing={editing} savedPanels={saved} scale={widgetScale('perf_strip_center')} passThrough>
          <GlassCard surface="overlay" className="flex max-w-[65vw] flex-wrap items-center gap-2 px-2 py-1.5">
            {me && <div data-performance-champion>
              <DraftChampionBadge champion={{ name: me.championName, slug: me.champion, tier: me.championTier ?? '', icon: me.championIcon }}
                portrait french={state.settings.language === 'fr'} />
            </div>}
            <dl className="flex items-center gap-2.5 whitespace-nowrap text-[11px] tabular-nums">
              {metrics.includes('cs') && <div className="flex items-center gap-1"><dt className="text-zinc-400">CS/min</dt><dd className="font-semibold">{me && liveSeconds > 20 ? csPerMin.toFixed(1) : '—'}</dd></div>}
              {metrics.includes('kda') && <div className="flex items-center gap-1"><dt className="text-zinc-400">KDA</dt><dd className="font-semibold">{me ? `${me.kills}/${me.deaths}/${me.assists}` : '—'}</dd></div>}
              {metrics.includes('kp') && <div className="flex items-center gap-1"><dt className="text-zinc-400">KP</dt><dd className="font-semibold">{me && myTeamKills > 0 ? `${kp}%` : '—'}</dd></div>}
              {metrics.includes('clock') && <div><dt className="sr-only">{state.settings.language === 'fr' ? 'Temps de partie' : 'Game time'}</dt><dd className="text-zinc-300">{liveClock || '—'}</dd></div>}
            </dl>
          </GlassCard>
        </DraggableSlot>
      )}

      {/* DIRECT ON-MINIMAP OBJECTIVE TIMERS & JUNGLE PATH (Flush corner + Live Resize Handle in Alt+O!) */}
      {showJungleWidget && (() => {
        const startMapResize = (e: React.PointerEvent<HTMLDivElement>) => {
          if (!editing || e.button !== 0) return;
          e.preventDefault();
          e.stopPropagation();
          e.currentTarget.setPointerCapture(e.pointerId);
          postAction('setDragging', { value: true });
          mapResizeRef.current = {
            pointerId: e.pointerId,
            startX: e.clientX,
            startY: e.clientY,
            startScale: minimapScale,
            basePx: (window.innerHeight || 1080) * 0.24,
            leftSide: minimapLeft,
          };
        };
        const moveMapResize = (e: React.PointerEvent<HTMLDivElement>) => {
          const r = mapResizeRef.current;
          if (!r || r.pointerId !== e.pointerId) return;
          e.stopPropagation();
          const dx = r.leftSide ? e.clientX - r.startX : r.startX - e.clientX;
          const dy = r.startY - e.clientY;
          const delta = (dx + dy) / 2;
          const next = Math.max(0.55, Math.min(1.85, r.startScale + delta / Math.max(80, r.basePx)));
          setLiveMapScale(Math.round(next * 100) / 100);
        };
        const endMapResize = (e: React.PointerEvent<HTMLDivElement>) => {
          const r = mapResizeRef.current;
          if (!r || r.pointerId !== e.pointerId) return;
          e.stopPropagation();
          mapResizeRef.current = null;
          try {
            e.currentTarget.releasePointerCapture(e.pointerId);
          } catch {
            // ignore
          }
          const dx = r.leftSide ? e.clientX - r.startX : r.startX - e.clientX;
          const dy = r.startY - e.clientY;
          const delta = (dx + dy) / 2;
          const finalScale = Math.round(Math.max(0.55, Math.min(1.85, r.startScale + delta / Math.max(80, r.basePx))) * 100) / 100;
          setLiveMapScale(finalScale);
          postAction('updateSetting', { key: 'minimapScale', value: finalScale });
          postAction('setDragging', { value: false });
        };
        const cancelMapResize = (e: React.PointerEvent<HTMLDivElement>) => {
          const r = mapResizeRef.current;
          if (!r || r.pointerId !== e.pointerId) return;
          e.stopPropagation();
          mapResizeRef.current = null;
          try {
            e.currentTarget.releasePointerCapture(e.pointerId);
          } catch {
            // ignore
          }
          setLiveMapScale(null);
          postAction('setDragging', { value: false });
        };

        return (
        <DraggableSlot
          panelKey="minimap_overlay"
          title="Minimap Timers"
          defaultX={minimapLeft ? 0.0045 : 0.9955}
          defaultY={0.989}
          anchor={minimapLeft ? 'bottom-left' : 'bottom-right'}
          editing={editing}
          savedPanels={saved}
          scale={1}
          passThrough
        >
          <div
            className={cn('flex max-w-full flex-col', minimapLeft ? 'items-start' : 'items-end')}
            style={{ width: editing || showEarlyClearPath ? `max(156px, ${minimapVh}vh)` : `${minimapVh}vh` }}
          >
              {/* Subtle micro-bar above minimap top edge (during early clear or edit) */}
              {(showEarlyClearPath || editing) && (
                <div
                  data-no-drag
                  className="pointer-events-auto mb-1.5 flex w-full flex-wrap items-center justify-between gap-1 rounded-lg border border-white/12 bg-[#0B0C10]/92 px-1.5 py-1 text-[10px] backdrop-blur-md shadow-md"
                >
                  {editing ? (
                    <>
                      <div className="flex items-center gap-1">
                        <span className="font-semibold text-indigo-200 tabular-nums">
                          {Math.round(minimapScale * 100)}%
                        </span>
                        {showEarlyClearPath && (
                          <div className="flex items-center gap-0.5 ml-1 border-l border-white/12 pl-1">
                            {(['red', 'blue'] as const).map((side) => (
                              <button
                                key={side}
                                type="button"
                                onClick={() => setJungleSide(side)}
                                className={cn(
                                  'rounded px-1.5 py-0.5 text-[8px] font-bold uppercase cursor-pointer transition-colors leading-none',
                                  jungleSide === side
                                    ? 'bg-indigo-500 text-white'
                                    : 'text-zinc-400 hover:text-white'
                                )}
                              >
                                {side}
                              </button>
                            ))}
                          </div>
                        )}
                      </div>
                      <div className="flex items-center gap-1">
                        <button
                          type="button"
                          onClick={() =>
                            postAction('updateSetting', {
                              key: 'minimapScale',
                              value: Math.max(0.55, Math.round((minimapScale - 0.05) * 100) / 100),
                            })
                          }
                          aria-label="Decrease minimap size"
                          className="flex size-5 items-center justify-center rounded bg-white/10 text-white hover:bg-white/20 cursor-pointer"
                        >
                          <Minus className="size-3" />
                        </button>
                        <button
                          type="button"
                          onClick={() =>
                            postAction('updateSetting', {
                              key: 'minimapScale',
                              value: Math.min(1.85, Math.round((minimapScale + 0.05) * 100) / 100),
                            })
                          }
                          aria-label="Increase minimap size"
                          className="flex size-5 items-center justify-center rounded bg-white/10 text-white hover:bg-white/20 cursor-pointer"
                        >
                          <Plus className="size-3" />
                        </button>
                        <button
                          type="button"
                          onClick={() => postAction('syncLeagueMinimap')}
                          className="rounded bg-indigo-500/30 border border-indigo-400/50 px-2 py-0.5 text-[9px] font-bold text-indigo-100 hover:bg-indigo-500/50 cursor-pointer"
                        >
                          Auto LoL
                        </button>
                      </div>
                    </>
                  ) : (
                    <>
                      <span className="inline-flex items-center gap-1 font-semibold text-zinc-300">
                        <Compass className="size-2.5 text-indigo-400" />
                        Clear 3:20
                      </span>
                      <div className="flex items-center gap-1">
                        {(['red', 'blue'] as const).map((side) => (
                          <button
                            key={side}
                            type="button"
                            onClick={() => setJungleSide(side)}
                            className={cn(
                              'rounded px-2 py-0.5 text-[9px] font-bold uppercase cursor-pointer transition-colors leading-none',
                              jungleSide === side
                                ? 'bg-indigo-500 text-white'
                                : 'text-zinc-400 hover:text-white'
                            )}
                          >
                            {side}
                          </button>
                        ))}
                      </div>
                    </>
                  )}
                </div>
              )}

            <div
              data-minimap-frame
              style={{ width: `${minimapVh}vh` }}
              className={cn(
                'relative aspect-square max-w-full',
                editing &&
                  'border-2 border-dashed border-indigo-400/80 bg-indigo-500/[0.08]'
              )}
            >

              {/* Live Interactive Corner Resize Handle (visible in Alt+O Edit Mode!) */}
              {editing && (
                <div
                  data-no-drag
                  title="Drag corner to resize Minimap frame"
                  onPointerDown={startMapResize}
                  onPointerMove={moveMapResize}
                  onPointerUp={endMapResize}
                  onPointerCancel={cancelMapResize}
                  onLostPointerCapture={cancelMapResize}
                  className={cn(
                    'pointer-events-auto absolute -top-2 z-20 flex size-5 items-center justify-center rounded-full border-2 border-white bg-indigo-500 shadow-lg',
                    minimapLeft ? '-right-1 cursor-nesw-resize' : '-left-1 cursor-nwse-resize'
                  )}
                >
                  <div className="size-1.5 rounded-full bg-white" />
                </div>
              )}

            {/* Early-Game Numbered Jungle Path SVG + Camp Pins directly on Minimap */}
            {showEarlyClearPath && (
              <>
                <svg
                  viewBox="0 0 100 100"
                  className="pointer-events-none absolute inset-0 size-full overflow-visible"
                >
                  <polyline
                    fill="none"
                    stroke="rgba(129, 140, 248, 0.72)"
                    strokeWidth="1.25"
                    strokeDasharray="2.5 2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    points={mapCamps.map((c) => `${c.x},${c.y}`).join(' ')}
                  />
                </svg>
                {mapCamps.map((camp, idx) => (
                  <div
                    key={camp.name}
                    style={{ left: `${camp.x}%`, top: `${camp.y}%` }}
                    className={cn(
                      'pointer-events-none absolute -translate-x-1/2 -translate-y-1/2 flex size-4 items-center justify-center rounded-full border text-[8px] font-black shadow-[0_2px_6px_rgba(0,0,0,0.85)]',
                      idx === 0
                        ? 'border-amber-300 bg-amber-500 text-zinc-950'
                        : camp.label === 'S'
                          ? 'border-teal-300 bg-teal-500 text-zinc-950'
                          : 'border-indigo-300/90 bg-[#121426]/92 text-indigo-100'
                    )}
                  >
                    {camp.label}
                  </div>
                ))}
              </>
            )}

            {/* Top River Pit Pin: Voidgrubs / Herald / Baron (x: 33%, y: 30%) */}
            {topPitObj && (
              <div
                style={{ left: '33%', top: '30%' }}
                className={cn(
                  'pointer-events-none absolute -translate-x-1/2 -translate-y-1/2 inline-flex items-center gap-1 rounded-md border px-2 py-1 text-[9px] font-extrabold tracking-tight leading-none tabular-nums whitespace-nowrap shadow-[0_2px_10px_rgba(0,0,0,0.9)]',
                  topPitObj.alive
                    ? 'border-amber-400/85 bg-[#1B1608]/94 text-amber-300'
                    : 'border-purple-400/60 bg-[#0D0E16]/92 text-purple-200'
                )}
              >
                <span className="opacity-80">{shortObjLabel(topPitObj.key)}</span>
                <span>{topPitObj.status}</span>
              </div>
            )}

            {/* Bottom River Pit Pin: Dragon (x: 67%, y: 70%) */}
            {dragonObj && (
              <div
                style={{ left: '67%', top: '70%' }}
                className={cn(
                  'pointer-events-none absolute -translate-x-1/2 -translate-y-1/2 inline-flex items-center gap-1 rounded-md border px-2 py-1 text-[9px] font-extrabold tracking-tight leading-none tabular-nums whitespace-nowrap shadow-[0_2px_10px_rgba(0,0,0,0.9)]',
                  dragonObj.alive
                    ? 'border-amber-400/85 bg-[#1B1608]/94 text-amber-300'
                    : 'border-amber-400/50 bg-[#0D0E16]/92 text-amber-200'
                )}
              >
                <span className="opacity-80">DRK</span>
                <span>{dragonObj.status}</span>
              </div>
            )}

            {/* Helper label visible ONLY while aligning in Alt+O Edit mode */}
            {editing && (
              <div className="pointer-events-none absolute inset-x-0 bottom-1.5 text-center text-[9px] font-semibold text-indigo-200">
                Drag corner dot to match League Minimap
              </div>
            )}
          </div>
          </div>
        </DraggableSlot>
        );
      })()}
    </div>
  );
}
