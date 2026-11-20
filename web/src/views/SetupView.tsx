import { useEffect, useState } from 'react';
import { AnimatePresence, motion, useReducedMotion } from 'framer-motion';
import { ArrowRight, Check, FolderOpen, RefreshCw, ShieldCheck } from 'lucide-react';
import { AnimatedMayloEmblem, MayloEmblem, MayloWordmark } from '@/components/ui/brand';
import { GlassCard } from '@/components/ui/glass-card';
import { BorderBeam, MotionPage, SWISS_EASE } from '@/components/ui/motion-primitives';
import { Button, Switch } from '@/components/ui/primitives';
import { Status } from '@/components/ui/status';
import { postAction } from '@/lib/bridge';

type SetupMessage =
  | { type: 'setupPath'; path: string; startup?: boolean }
  | { type: 'setupProgress'; percent: number; label: string }
  | { type: 'setupDone'; path: string }
  | { type: 'setupError'; message: string };

const options = [
  { key: 'desktop', label: 'Desktop shortcut', description: 'Open maylo from your desktop.' },
  { key: 'startMenu', label: 'Start menu shortcuts', description: 'Add shortcuts for maylo and its uninstaller.' },
  { key: 'startup', label: 'Launch with Windows', description: 'Open maylo when you sign in.' },
  { key: 'openAfter', label: 'Open after installation', description: 'Start maylo as soon as setup finishes.' },
] as const;

export function SetupView() {
  const [path, setPath] = useState('');
  const [choices, setChoices] = useState({ desktop: true, startMenu: true, startup: false, openAfter: true });
  const [phase, setPhase] = useState<'ready' | 'installing' | 'done'>('ready');
  const [progress, setProgress] = useState({ percent: 0, label: 'Preparing installation…' });
  const [error, setError] = useState('');
  const reducedMotion = useReducedMotion();

  useEffect(() => {
    document.documentElement.classList.add('dark');
    const webview = window.chrome?.webview;
    if (!webview) return;
    const receive = (event: { data: unknown }) => {
      const message = event.data as SetupMessage;
      switch (message?.type) {
        case 'setupPath': {
          setPath(message.path);
          const { startup } = message;
          if (typeof startup === 'boolean') setChoices(current => ({ ...current, startup }));
          break;
        }
        case 'setupProgress':
          setPhase('installing');
          setProgress({ percent: message.percent, label: message.label });
          break;
        case 'setupDone': setPath(message.path); setPhase('done'); break;
        case 'setupError': setError(message.message); setPhase('ready'); break;
      }
    };
    webview.addEventListener('message', receive);
    postAction('setupReady');
    return () => webview.removeEventListener('message', receive);
  }, []);

  return (
    <div className="flex min-h-screen flex-col bg-[#0B0C10] text-zinc-100">
      <header className="flex shrink-0 items-center justify-between border-b border-white/[0.08] bg-[#101117] px-5 py-3 sm:px-7">
        <div className="flex items-center gap-2.5">
          <MayloEmblem className="size-6 text-white" light={false} />
          <MayloWordmark size="md" light={false} />
        </div>
        <Status pulse={false}>Windows setup</Status>
      </header>

      <MotionPage pageKey="setup" className="mx-auto grid w-full max-w-[970px] flex-1 items-stretch gap-4 p-5 sm:px-7 md:grid-cols-[0.92fr_1.08fr] md:py-6">
        <GlassCard surface="hub" highlight="accent" className="relative flex min-h-[235px] flex-col items-center justify-center overflow-hidden rounded-2xl px-5 py-6 text-center md:min-h-[390px]">
          <div aria-hidden="true" className="pointer-events-none absolute inset-0 swiss-grid-bg opacity-65"
            style={{ maskImage: 'radial-gradient(ellipse at 50% 46%, black 26%, transparent 77%)' }} />
          <div aria-hidden="true" className="pointer-events-none absolute top-[14%] size-52 rounded-full bg-indigo-500/20 blur-[70px]" />
          {!reducedMotion && <BorderBeam />}
          <div className="relative flex flex-col items-center">
            <AnimatedMayloEmblem light={false} />
            <h1 className="mt-5 font-display text-[28px] font-semibold tracking-tight text-white">
              {phase === 'done' ? 'Ready for the Rift' : 'Install maylo'}
            </h1>
            <p className="mt-1.5 max-w-64 text-[13px] leading-relaxed text-zinc-400">
              Your Windows companion for League of Legends and ARAM Mayhem.
            </p>
            <div className="mt-5 flex flex-wrap justify-center gap-1.5">
              {['Ranked', 'ARAM', 'ARAM Mayhem'].map((mode, i) => (
                <span key={mode} className={['rounded-full border px-2 py-0.5 text-[10px] font-semibold border-indigo-400/35 bg-indigo-500/12 text-indigo-300',
                  'rounded-full border px-2 py-0.5 text-[10px] font-semibold border-sky-400/35 bg-sky-500/12 text-sky-300',
                  'rounded-full border px-2 py-0.5 text-[10px] font-semibold border-amber-400/35 bg-amber-500/12 text-amber-300'][i]}>{mode}</span>
              ))}
            </div>
          </div>
        </GlassCard>

        <GlassCard surface="hub" className="flex min-w-0 flex-col rounded-2xl px-4 py-4 sm:px-5">
          <AnimatePresence mode="wait">
            <motion.div key={phase} className="flex flex-1 flex-col"
              initial={{ opacity: 0, y: 5 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0, y: -4 }}
              transition={{ duration: 0.18, ease: SWISS_EASE }}>
              {phase === 'ready' ? (
                <>
                  <h2 className="font-display text-lg font-semibold text-white">Installation options</h2>
                  <label htmlFor="setup-folder" className="mt-3.5 mb-1.5 text-xs font-medium text-zinc-300">Install folder</label>
                  <div className="mb-2.5 flex gap-2">
                    <input id="setup-folder" value={path} onChange={e => setPath(e.target.value)} spellCheck={false}
                      className="min-w-0 flex-1 rounded-lg border border-white/10 bg-black/25 px-2.5 font-mono-tech text-[11px] text-zinc-200 outline-none focus:border-indigo-400/70" />
                    <Button size="sm" onClick={() => postAction('setupBrowse', { path })}><FolderOpen className="size-3.5" /> Browse</Button>
                  </div>
                  <div className="divide-y divide-white/[0.06]">
                    {options.map(option => <Switch key={option.key} label={option.label} description={option.description}
                      checked={choices[option.key]} onChange={value => setChoices(current => ({ ...current, [option.key]: value }))} />)}
                  </div>
                  {error && <p role="alert" className="mt-2 text-xs text-rose-300">{error}</p>}
                </>
              ) : phase === 'installing' ? (
                <div className="my-auto py-8">
                  <h2 className="font-display text-xl font-semibold text-white">Installing maylo</h2>
                  <div role="progressbar" aria-label="Installation progress" aria-valuenow={progress.percent}
                    className="mt-4 h-1.5 overflow-hidden rounded-full bg-white/[0.08]">
                    <div className="relative h-full overflow-hidden rounded-full bg-gradient-to-r from-indigo-600 via-indigo-500 to-indigo-400 transition-[width] duration-300 motion-reduce:transition-none"
                      style={{ width: progress.percent + '%' }}>
                      {!reducedMotion && <div className="splash-shimmer absolute inset-y-0 w-1/2 bg-gradient-to-r from-transparent via-white/45 to-transparent" />}
                    </div>
                  </div>
                  <div className="mt-2 flex justify-between gap-3 text-xs text-zinc-400">
                    <span className="truncate">{progress.label}</span><span className="font-mono-tech text-indigo-300">{progress.percent}%</span>
                  </div>
                </div>
              ) : (
                <div className="my-auto py-8">
                  <div className="mb-3 inline-flex size-10 items-center justify-center rounded-xl border border-emerald-400/30 bg-emerald-500/15 text-emerald-300"><Check className="size-5" /></div>
                  <h2 className="font-display text-xl font-semibold text-white">maylo is installed</h2>
                  <p className="mt-2 text-[13px] leading-relaxed text-zinc-400">
                    In League, select Borderless video mode for the in-game overlay.
                  </p>
                  <div className="mt-4 break-all rounded-lg border border-white/10 bg-black/20 px-2.5 py-2 font-mono-tech text-[11px] text-zinc-300">{path}</div>
                </div>
              )}
            </motion.div>
          </AnimatePresence>
        </GlassCard>
      </MotionPage>

      <footer className="flex shrink-0 flex-wrap items-center justify-between gap-3 border-t border-white/[0.08] bg-[#101117] px-5 py-3 sm:px-7">
        <div className="flex flex-wrap gap-x-3 gap-y-1 text-[11px] text-zinc-400">
          <span className="inline-flex items-center gap-1.5"><ShieldCheck className="size-3.5 text-indigo-300" /> Per-user install</span>
          <span className="inline-flex items-center gap-1.5"><RefreshCw className="size-3.5 text-indigo-300" /> Automatic updates</span>
        </div>
        <div className="flex gap-2">
          <Button variant="ghost" disabled={phase === 'installing'} onClick={() => postAction('setupClose')}>
            {phase === 'done' ? 'Close' : 'Cancel'}
          </Button>
          <Button variant="accent" disabled={phase === 'installing' || !path.trim()}
            onClick={() => {
              if (phase === 'done') { postAction('setupLaunch'); return; }
              setError('');
              setPhase('installing');
              postAction('setupInstall', { path, ...choices });
            }}>
            {phase === 'done' ? 'Open maylo' : 'Install maylo'} <ArrowRight className="size-4" />
          </Button>
        </div>
      </footer>
    </div>
  );
}
