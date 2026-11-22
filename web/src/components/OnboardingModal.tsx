import { useEffect, useState } from 'react';
import { BookOpen, Gamepad2, History, Settings2, X } from 'lucide-react';
import { Button, Keycap } from '@/components/ui/primitives';
import { postAction } from '@/lib/bridge';
import type { AppSettings } from '@/types';

interface OnboardingModalProps {
  settings: AppSettings;
  onFinish: () => void;
}

export function OnboardingModal({ settings, onFinish }: OnboardingModalProps) {
  const [step, setStep] = useState(0);
  const french = settings.language === 'fr';
  const sheetKey = settings.hotkeySheet || 'G';
  const editKey = settings.hotkeyEdit || 'O';
  const pages = french ? [
    { icon: Gamepad2, title: 'Connecter League', text: 'Lance le client League of Legends. Maylo détecte ton mode et ton champion. Pour afficher le guide par-dessus le jeu, utilise le mode Sans bordure.' },
    { icon: BookOpen, title: 'Préparer ton build', text: 'En sélection, ouvre le build du champion détecté depuis l’accueil. Enregistre une variante et choisis-la dans le menu Alt+G pour la retrouver en partie. En Mayhem et en Arena, cherche les augments par nom, effet ou rareté. Les tiers comparent les cartes de même rareté.' },
    { icon: Settings2, title: 'Consulter le guide en partie', text: 'Alt+G ouvre les guides d’objets et d’augments. Choisis un objet pour voir sa recette. Les cartes détectées affichent leur tier. Alt+O sert à placer les panneaux de l’overlay.' },
    { icon: History, title: 'Revoir ta partie', text: 'Ouvre la dernière partie depuis l’accueil pour revoir ton équipement, filtrer la chronologie, ajouter une note ou associer un clip local. Ce guide reste accessible dans les réglages.' },
  ] : [
    { icon: Gamepad2, title: 'Connect League', text: 'Open the League of Legends client. Maylo detects your mode and champion. Use Borderless mode to show the guide over your game.' },
    { icon: BookOpen, title: 'Prepare a build', text: 'During champion select, open the detected champion’s build from Home. Save a variant and select it in the Alt+G menu to use it in game. In Mayhem and Arena, find augments by name, effect or rarity. Tiers compare cards of the same rarity.' },
    { icon: Settings2, title: 'Open the guide in game', text: 'Alt+G opens the item and augment guides. Choose an item to view its recipe. Detected cards show their tier. Use Alt+O to move overlay panels.' },
    { icon: History, title: 'Review your match', text: 'Open your latest match from Home to review your loadout, filter the timeline, add a note or attach a local clip. You can reopen this guide from Settings.' },
  ];
  const page = pages[step];
  const atEnd = step === pages.length - 1;
  const goNext = () => atEnd ? onFinish() : setStep(step + 1);

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.defaultPrevented || event.target instanceof HTMLSelectElement) return;
      if (event.key === 'Escape') onFinish();
      if (event.key === 'ArrowLeft') setStep((previous) => Math.max(0, previous - 1));
      if (event.key === 'ArrowRight') {
        if (atEnd) onFinish();
        else setStep((previous) => Math.min(pages.length - 1, previous + 1));
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [atEnd, onFinish, pages.length]);

  const Icon = page.icon;
  return (
    <div className="fixed inset-0 z-50 grid place-items-center bg-black/65 p-4 backdrop-blur-sm">
      <section role="dialog" aria-modal="true" aria-labelledby="onboarding-title"
        className="w-full max-w-lg space-y-5 rounded-2xl border border-zinc-500/25 bg-white p-5 text-zinc-900 shadow-xl dark:bg-[#14151b] dark:text-zinc-100">
        <header className="flex items-center justify-between gap-3">
          <span className="text-sm font-semibold">Maylo</span>
          <div className="flex items-center gap-2">
            <select aria-label="Language · Langue" value={french ? 'fr' : 'en'}
              onChange={(event) => postAction('updateSetting', { key: 'language', value: event.target.value })}
              className="rounded border border-zinc-500/25 bg-transparent px-2 py-1 text-xs dark:bg-zinc-900">
              <option value="en">English</option><option value="fr">Français</option>
            </select>
            <button type="button" className="rounded p-1" aria-label={french ? 'Fermer' : 'Close'} onClick={onFinish}>
              <X className="size-5" />
            </button>
          </div>
        </header>
        <div className="min-h-36 space-y-2">
          <Icon className="size-7 text-indigo-500" aria-hidden />
          <h1 id="onboarding-title" className="text-xl font-bold">{page.title}</h1>
          <p className="text-sm leading-relaxed text-zinc-600 dark:text-zinc-300">
            {page.text.replace('Alt+G', `Alt+${sheetKey}`).replace('Alt+O', `Alt+${editKey}`)}
          </p>
          {step === 2 && <div className="flex gap-2"><Keycap keys={`Alt+${sheetKey}`} /><Keycap keys={`Alt+${editKey}`} /></div>}
        </div>
        <footer className="flex items-center justify-between border-t border-zinc-500/20 pt-3">
          <span aria-live="polite" className="text-xs tabular-nums text-zinc-500">{step + 1} / {pages.length}</span>
          <div className="flex gap-2">
            <Button size="sm" disabled={step === 0} onClick={() => setStep(step - 1)}>{french ? 'Retour' : 'Back'}</Button>
            <Button size="sm" variant="primary" onClick={goNext}>
              {atEnd ? (french ? 'Ouvrir Maylo' : 'Open Maylo') : (french ? 'Continuer' : 'Continue')}
            </Button>
          </div>
        </footer>
      </section>
    </div>
  );
}
