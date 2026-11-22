import { useId, useState } from 'react';
import type { AppState, BenchmarkMetric } from '@/types';

export const BENCHMARK_METRICS: { id: BenchmarkMetric; fr: string; en: string }[] = [
  { id: 'cs', fr: 'CS/min', en: 'CS/min' },
  { id: 'kda', fr: 'KDA', en: 'KDA' },
  { id: 'kp', fr: 'Participation aux kills', en: 'Kill participation' },
  { id: 'clock', fr: 'Temps de partie', en: 'Game time' },
];

export function benchmarkMetrics(settings: AppState['settings']) {
  return settings.benchmarkMetrics?.length ? settings.benchmarkMetrics : BENCHMARK_METRICS.map(({ id }) => id);
}

// Keep these percentages aligned with ChampSelectOverlayLayout.ContainsTierListPointer.
const draftDrawer = { right: 1, top: 12, open: { bottom: 10, vw: 28, vh: 32 }, closed: { bottom: 83, vw: 18, vh: 20 } };
export function draftDrawerStyle(open: boolean) {
  const size = open ? draftDrawer.open : draftDrawer.closed;
  return { right: `${draftDrawer.right}vw`, top: `${draftDrawer.top}vh`, bottom: `${size.bottom}vh`, width: `min(${size.vw}vw,${size.vh}vh)` };
}

const resolutions = [[1366, 768], [1920, 1080], [2560, 1440], [3440, 1440]];

export function OverlayLayoutPreview({ state, french, dark = false }: { state: AppState; french: boolean; dark?: boolean }) {
  const label = useId();
  const [resolution, setResolution] = useState(1);
  const [draft, setDraft] = useState(state.previewScene === 'bench' || state.game.inSelection);
  const [drawerOpen, setDrawerOpen] = useState(true);
  const [width, height] = resolutions[resolution];
  const size = drawerOpen ? draftDrawer.open : draftDrawer.closed;
  const ratios = state.settings.panels || {};
  const widgets = [
    ...(draft
      ? state.settings.showBenchBar && ['MAYHEM','ARAM'].includes(state.activeMode)
        ? [{ key: 'champ_select_ribbon', x: .5, y: .008, label: french ? 'Banc' : 'Bench' }]
        : []
      : [
          ...(state.settings.showBuildOverlay ? [{ key: 'build_hud', x: .012, y: .018, label: 'Build' }] : []),
          ...(state.activeMode === 'SR' && state.settings.showPerfStrip ? [{ key: 'perf_strip_center', x: .5, y: .042, label: 'Benchmark' }] : []),
          ...(state.activeMode === 'SR' && state.settings.showJungleWidget ? [{
            key: 'minimap_overlay', x: state.settings.minimapLeft ? .0045 : .9955, y: .989, label: french ? 'Minicarte' : 'Minimap',
          }] : []),
        ]),
  ];
  return (
    <div className={`space-y-2 text-xs ${dark ? 'text-zinc-100' : ''}`}>
      <div className="flex flex-wrap items-center gap-2">
        <label htmlFor={label} className="font-semibold">{french ? 'Résolution' : 'Resolution'}</label>
        <select id={label} value={resolution} onChange={(event) => setResolution(Number(event.target.value))}
          className="rounded border border-zinc-500/40 bg-white px-2 py-1 dark:bg-zinc-900" style={dark ? { colorScheme: 'dark', background: '#18181b' } : undefined}>
          {resolutions.map(([w,h], index) => <option key={w} value={index}>{w} × {h}</option>)}
        </select>
        <button type="button" aria-pressed={!draft} onClick={() => setDraft(false)} className="rounded border border-zinc-500/40 px-2 py-1 aria-pressed:bg-indigo-500/20">
          {french ? 'En partie' : 'In game'}
        </button>
        <button type="button" aria-pressed={draft} onClick={() => setDraft(true)} className="rounded border border-zinc-500/40 px-2 py-1 aria-pressed:bg-indigo-500/20">
          {french ? 'Sélection' : 'Champ select'}
        </button>
        {draft && <label className="flex items-center gap-1"><input type="checkbox" checked={drawerOpen} onChange={(event) => setDrawerOpen(event.target.checked)} />
          {french ? 'Tier list ouverte' : 'Tier list open'}</label>}
      </div>
      <div className="relative overflow-hidden rounded-lg border border-indigo-400/40 bg-[#0d1020] text-white"
        style={{ aspectRatio: `${width} / ${height}` }} aria-label={french ? 'Plan des panneaux de l’overlay' : 'Overlay panel map'}>
        {widgets.map((widget) => {
          const { x, y } = ratios[widget.key] ?? widget;
          return <span key={widget.key} className="absolute z-10 whitespace-nowrap rounded border border-white/30 bg-[#202944] px-1.5 py-0.5 text-[10px]"
            style={{ left: `${Math.max(1, Math.min(99, x * 100))}%`, top: `${Math.max(1, Math.min(99, y * 100))}%`,
              transform: `translate(${x >= .85 ? '-100%' : x >= .25 ? '-50%' : '0'},${y >= .85 ? '-100%' : '0'})` }}>
            {widget.label}
          </span>;
        })}
        {draft && <div data-preview-click-zone className="absolute rounded-sm border border-amber-300/80 bg-amber-400/22"
          style={{ right: `${draftDrawer.right}%`, top: `${draftDrawer.top}%`, bottom: `${size.bottom}%`, width: `${Math.min(size.vw, size.vh * height / width)}%` }}>
          <span className="sr-only">{french ? 'Zone interactive de la tier list' : 'Tier list interactive area'}</span>
        </div>}
      </div>
      <p className={dark ? 'text-zinc-300' : 'text-zinc-500'}>
        {draft
          ? (french ? 'En orange, la zone qui reçoit les clics en sélection.' : 'Orange marks the click area in champion select.')
          : (french ? 'Les repères indiquent les positions enregistrées. Déplace les panneaux dans Alt+O.' : 'Markers show saved positions. Move the panels in Alt+O.')}
      </p>
    </div>
  );
}
