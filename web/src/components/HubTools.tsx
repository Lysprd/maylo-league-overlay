import { useEffect, useMemo, useState, type RefObject } from 'react';
import { Search } from 'lucide-react';
import { Button } from '@/components/ui/primitives';
import { itemIconUrl } from '@/lib/utils';
import { postAction } from '@/lib/bridge';
import type { AppState, MatchRecord } from '@/types';

const normalize = (text: string) =>
  text.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase();

export function SettingsSearch({ root, french }: { root: RefObject<HTMLDivElement>; french: boolean }) {
  const [query, setQuery] = useState('');
  const [matches, setMatches] = useState<{ label: string; element: HTMLElement }[]>([]);
  useEffect(() => {
    const keywords: Record<string, string> = {
      raccourci: 'shortcut hotkey', raccourcis: 'shortcut hotkey', overlay: 'overlay hud', langue: 'language',
      mise: 'update', rune: 'rune', ecran: 'borderless layout screen', demarrage: 'launch startup',
    };
    const terms = normalize(query.trim()).replace(/mise a jour/g, 'update').split(/\s+/).filter(Boolean);
    if (!root.current || !terms.length) {
      setMatches([]);
      return;
    }
    const controls = root.current.querySelectorAll<HTMLElement>('button, select, input[type=range]');
    setMatches(Array.from(controls).flatMap((element) => {
      const label = element.getAttribute('aria-label') || element.textContent?.trim() || '';
      const text = normalize(label);
      const match = label && terms.every((term) => text.includes(term) ||
        keywords[term]?.split(' ').some((word) => text.includes(word)));
      return match ? [{ label, element }] : [];
    }).slice(0, 8));
  }, [query, root]);

  return (
    <div className="border-b border-zinc-500/20 px-5 py-3">
      <label className="flex items-center gap-2 rounded-lg border border-zinc-500/30 px-3 py-2">
        <Search aria-hidden className="size-4 text-zinc-400" />
        <input value={query} onChange={(event) => setQuery(event.target.value)}
          aria-label={french ? 'Rechercher un réglage' : 'Find a setting'}
          placeholder={french ? 'Rechercher un réglage…' : 'Find a setting…'}
          className="w-full bg-transparent text-sm outline-none" />
      </label>
      {!!query.trim() && (
        <div className="max-h-36 overflow-y-auto pt-1">
          {matches.length === 0 && <p className="py-2 text-xs text-zinc-500">{french ? 'Aucun résultat.' : 'No matching settings.'}</p>}
          {matches.map(({ label, element }, index) => (
            <button key={label + index} type="button" className="block w-full truncate rounded px-2 py-1.5 text-left text-xs hover:bg-indigo-500/10"
              onClick={() => { element.scrollIntoView({ block: 'center' }); element.focus({ preventScroll: true }); setQuery(''); }}>
              {label}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

export function RecentProgress({ matches, french, farm }: { matches: MatchRecord[]; french: boolean; farm: boolean }) {
  const [champion, setChampion] = useState('all');
  const valid = matches.filter((match) => !match.remake);
  const champions = Array.from(new Map(valid.map((match) => [match.championId, match.championName]))).sort((a, b) => a[1].localeCompare(b[1]));
  const selected = valid.filter((match) => champion === 'all' || String(match.championId) === champion);
  const groups = [selected.slice(0, 10), selected.slice(10, 20)];
  const average = (games: MatchRecord[], value: (match: MatchRecord) => number) =>
    games.length ? (games.reduce((sum, match) => sum + value(match), 0) / games.length).toFixed(1) : '—';
  const metrics = [
    { name: french ? 'Morts / partie' : 'Deaths / match', value: (match: MatchRecord) => match.deaths },
    { name: french ? 'Durée (min)' : 'Duration (min)', value: (match: MatchRecord) => match.duration / 60 },
    ...(farm ? [{ name: 'CS/min', value: (match: MatchRecord) => match.cs * 60 / match.duration }] : []),
  ];

  return (
    <section className="space-y-3 rounded-xl border border-zinc-500/25 p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-sm font-semibold">{french ? 'Progression récente' : 'Recent progress'}</h2>
        <select value={champion} onChange={(event) => setChampion(event.target.value)} aria-label={french ? 'Champion' : 'Champion'}
          className="rounded border border-zinc-500/25 bg-white p-1.5 text-xs dark:bg-zinc-900">
          <option value="all">{french ? 'Tous les champions' : 'All champions'}</option>
          {champions.map(([id, name]) => <option key={id} value={id}>{name}</option>)}
        </select>
      </div>
      <table className="w-full text-left text-xs tabular-nums">
        <thead><tr>
          <th scope="col"><span className="sr-only">{french ? 'Indicateur' : 'Metric'}</span></th>
          <th scope="col">{french ? 'Récentes' : 'Recent'} ({groups[0].length})</th>
          <th scope="col">{french ? 'Précédentes' : 'Previous'} ({groups[1].length})</th>
        </tr></thead>
        <tbody>{metrics.map(({ name, value }) => (
          <tr key={name} className="border-t border-zinc-500/20">
            <th className="py-1.5 font-medium" scope="row">{name}</th>
            {groups.map((group, index) => <td key={index}>{average(group.filter((match) => match.duration > 0), value)}</td>)}
          </tr>
        ))}</tbody>
      </table>
    </section>
  );
}

export function RankedProgress({ history, french }: { history: AppState['rankHistory']; french: boolean }) {
  if (!history?.length) return null;
  const date = new Intl.DateTimeFormat(french ? 'fr-FR' : 'en-US', { dateStyle: 'medium', timeStyle: 'short' });
  return (
    <section className="space-y-2 rounded-xl border border-zinc-500/25 p-4">
      <h2 className="text-sm font-semibold">{french ? 'LP observés · Solo/Duo' : 'Observed LP · Solo/Duo'}</h2>
      <ol className="space-y-1 text-xs tabular-nums">
        {history.slice(0, 8).map((rank) => (
          <li key={rank.created} className="flex flex-wrap justify-between gap-2 border-t border-zinc-500/20 py-1.5">
            <time className="text-zinc-500">{date.format(rank.created)}</time>
            <strong>{rank.tier.toLowerCase().replace(/^./, (letter) => letter.toUpperCase())} {rank.division} · {rank.lp} LP</strong>
          </li>
        ))}
      </ol>
    </section>
  );
}

type SavedBuild = { id: string; name: string; items: { id: number; name: string }[] };

export function useSavedBuilds(state: AppState, inOverlay = false) {
  const match = inOverlay && state.game.inGame && !state.preview;
  const champion = match
    ? (state.game.modeKey === state.activeMode ? state.game.pickedChampion?.slug : null)
    : state.selected?.slug;
  const storageKey = champion ? `maylo:builds:${state.activeMode}:${state.activeRole}:${champion}` : '';
  const guideKey = `${storageKey}:guide`;
  const read = () => {
    if (!storageKey) return { key: storageKey, saved: [], guideId: '' };
    try {
      const list = JSON.parse(localStorage.getItem(storageKey) || '[]');
      const saved = Array.isArray(list) ? list.filter((build): build is SavedBuild =>
        build && typeof build.id === 'string' && typeof build.name === 'string' &&
        Array.isArray(build.items) && build.items.length > 0 &&
        build.items.every((item: SavedBuild['items'][number]) =>
          item && Number.isInteger(item.id) && item.id > 0 && typeof item.name === 'string')) : [];
      return { key: storageKey, saved, guideId: localStorage.getItem(guideKey) || '' };
    } catch {
      return { key: storageKey, saved: [], guideId: '' };
    }
  };
  const [snapshot, setSnapshot] = useState(read);
  const { saved, guideId } = snapshot.key === storageKey ? snapshot : read();
  const guide = saved.find(({ id }) => id === guideId);

  useEffect(() => {
    setSnapshot(read());
    const sync = (event: StorageEvent) => {
      if (event.key === storageKey || event.key === guideKey || event.key === null) setSnapshot(read());
    };
    window.addEventListener('storage', sync);
    return () => window.removeEventListener('storage', sync);
  }, [storageKey, guideKey]);

  const selectGuide = (id: string) => {
    if (!storageKey) return;
    if (id) localStorage.setItem(guideKey, id);
    else localStorage.removeItem(guideKey);
    setSnapshot(read());
  };
  const commit = (entries: SavedBuild[]) => {
    if (!storageKey) return;
    localStorage.setItem(storageKey, JSON.stringify(entries));
    if (!entries.some(({ id }) => id === guideId)) localStorage.removeItem(guideKey);
    setSnapshot(read());
  };
  return { saved, commit, guide, selectGuide };
}

export function BuildLibrary({ state, french }: { state: AppState; french: boolean }) {
  const [name, setName] = useState('');
  const [comparison, setComparison] = useState('');
  const [recipeId, setRecipeId] = useState<number | null>(null);
  const { saved, commit, guide, selectGuide } = useSavedBuilds(state);
  const current = state.buildPath.map(({ stat }) => ({ id: stat.id, name: stat.name }));
  const matching = useMemo(() => new Set(current.map(({ id }) => id)), [current]);
  const alternate = saved.find((build) => build.id === comparison);
  const activeItems = guide?.items ?? current;
  const recipe = activeItems.some(({ id }) => id === recipeId)
    ? state.referenceItems?.find(({ id }) => id === recipeId)
    : undefined;
  const canImport = state.settings.connected && !state.game.inGame;

  return (
    <section className="space-y-3 rounded-xl border border-zinc-500/25 p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-sm font-semibold">{french ? 'Build actif' : 'Active build'}</h2>
        {saved.length > 0 && (
          <label className="flex items-center gap-2 text-xs text-zinc-500">
            Alt+{state.settings.hotkeySheet || 'G'}
            <select aria-label={french ? 'Variante active' : 'Active variant'} className="max-w-52 rounded-lg border border-zinc-500/25 bg-transparent px-2 py-1 text-zinc-800 dark:bg-zinc-900 dark:text-zinc-100"
              value={guide?.id ?? ''} onChange={(event) => selectGuide(event.target.value)}>
              <option value="">{french ? 'Guide standard' : 'Default guide'}</option>
              {saved.map(({ id, name }) => <option key={id} value={id}>{name}</option>)}
            </select>
          </label>
        )}
      </div>
      {activeItems.length > 0 ? (
        <ul className="flex flex-wrap gap-2">
          {activeItems.map(({ id, name }) => (
            <li key={id}>
              <button type="button" aria-pressed={id === recipeId} onClick={() => setRecipeId(recipeId === id ? null : id)}
                className="flex items-center gap-2 rounded-lg border border-zinc-500/25 p-1.5 pr-2.5 text-left text-xs hover:border-indigo-400/60 aria-pressed:border-indigo-400 aria-pressed:bg-indigo-500/10">
                <img src={itemIconUrl(id, state.ddVersion)} alt="" className="size-7 rounded" />
                {name}
              </button>
            </li>
          ))}
        </ul>
      ) : <p className="text-xs text-zinc-500">{french ? 'Guide indisponible.' : 'Build guide unavailable.'}</p>}
      {recipeId != null && activeItems.some(({ id }) => id === recipeId) && (
        <div className="rounded-lg bg-zinc-500/10 p-2.5 text-xs">
          {recipe ? <>
            <strong>{recipe.name}{recipe.total > 0 ? ` · ${recipe.total}g` : ''}</strong>
            <ul className="mt-1 flex flex-wrap gap-x-4 gap-y-1">
              {recipe.components.map((component, index) => <li key={`${component.id}-${index}`}>{component.name} · {component.price}g</li>)}
            </ul>
          </> : <span>{french ? 'Recette indisponible.' : 'Recipe unavailable.'}</span>}
        </div>
      )}
      <div className="flex flex-wrap items-center gap-2">
        <Button size="sm" disabled={!canImport || !activeItems.length}
          onClick={() => postAction('exportItemSet', guide ? { guideItems: guide.items.map(({ id }) => id) } : undefined)}>
          {french ? 'Exporter vers la boutique' : 'Export to shop'}
        </Button>
        {state.runes && <Button size="sm" variant="secondary" disabled={!canImport} onClick={() => postAction('importRunes')}>
          {french ? 'Importer les runes' : 'Import runes'}
        </Button>}
        {state.importStatus && <span role="status" className="text-xs text-zinc-500">{state.importStatus}</span>}
      </div>
      <details className="space-y-3">
        <summary className="cursor-pointer text-xs font-semibold">{french ? 'Gérer les variantes' : 'Manage variants'}</summary>
        <form className="flex flex-wrap gap-2" onSubmit={(event) => {
          event.preventDefault();
          if (!name.trim() || !current.length) return;
          commit([{ id: String(Date.now()), name: name.trim(), items: current }, ...saved].slice(0, 20));
          setName('');
        }}>
          <input aria-label={french ? 'Nom du build' : 'Build name'} placeholder={french ? 'Nom du build' : 'Build name'}
            className="min-w-40 flex-1 rounded border border-zinc-500/25 bg-transparent px-2 py-1 text-sm" value={name} maxLength={60}
            onChange={(event) => setName(event.target.value)} />
          <Button type="submit" size="sm" disabled={!name.trim() || !current.length}>{french ? 'Enregistrer le guide' : 'Save current guide'}</Button>
        </form>
        {saved.map((build) => (
          <div key={build.id} className="flex flex-wrap items-center gap-2 border-t border-zinc-500/20 pt-2">
            <strong className="mr-auto text-xs">{build.name}</strong>
            {build.items.map((item) => <img key={item.id} src={itemIconUrl(item.id, state.ddVersion)} alt={item.name} title={item.name} className="size-6 rounded" />)}
            <Button size="xs" variant="secondary" onClick={() => setComparison(comparison === build.id ? '' : build.id)}>{french ? 'Comparer' : 'Compare'}</Button>
            <Button size="xs" variant="secondary" disabled={!canImport} onClick={() => postAction('exportItemSet', { guideItems: build.items.map(({ id }) => id) })}>
              {french ? 'Exporter' : 'Export'}
            </Button>
            <Button size="xs" variant="ghost" onClick={() => commit(saved.filter((entry) => entry.id !== build.id))}>{french ? 'Retirer' : 'Remove'}</Button>
          </div>
        ))}
        {alternate && (
          <div className="grid gap-2 text-xs sm:grid-cols-2">
            <div><strong>{french ? 'Guide actuel' : 'Current guide'}</strong><p>{current.map(({ name }) => name).join(' → ') || '—'}</p></div>
            <div><strong>{alternate.name}</strong>
              <ul className="mt-1 list-inside list-disc">{alternate.items.map((item) => (
                <li key={item.id} className={matching.has(item.id) ? '' : 'font-semibold text-indigo-500'}>{item.name}</li>
              ))}</ul>
            </div>
          </div>
        )}
      </details>
    </section>
  );
}

export function SkinCollection({ state, french }: { state: AppState; french: boolean }) {
  const [query, setQuery] = useState('');
  const [filter, setFilter] = useState('owned');
  const [limit, setLimit] = useState(48);
  const skins = state.collection ?? [];
  const owned = skins.filter((skin) => skin.owned).length;
  const candidates = skins.filter((skin) => (filter === 'all' || skin.owned === (filter === 'owned')) &&
    normalize(skin.name + ' ' + skin.champion).includes(normalize(query.trim())));
  return (
    <section className="space-y-3 rounded-xl border border-zinc-500/25 p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h2 className="text-sm font-semibold">{french ? 'Collection de skins' : 'Skin collection'}</h2>
          {skins.length > 0 && <p className="text-xs text-zinc-500">{owned} / {skins.length} {french ? 'skins dans la collection' : 'skins owned'}</p>}
        </div>
        <Button size="sm" disabled={state.collectionBusy || state.game.inGame || !state.account} onClick={() => postAction('loadCollection')}>
          {state.collectionBusy ? (french ? 'Chargement…' : 'Loading…') : (french ? 'Lire ma collection' : 'Read my collection')}
        </Button>
      </div>
      {state.collectionFailed && <p className="text-xs text-zinc-500">{french ? 'Inventaire indisponible. Vérifie que le client League est ouvert.' : 'Inventory unavailable. Check that the League client is open.'}</p>}
      {skins.length > 0 && <>
        <div className="flex gap-2">
          <input aria-label={french ? 'Rechercher un skin' : 'Search skins'}
            className="min-w-0 flex-1 rounded border border-zinc-500/25 bg-transparent p-2 text-xs"
            value={query} onChange={(event) => { setQuery(event.target.value); setLimit(48); }} />
          <select aria-label={french ? 'Filtrer les skins' : 'Filter skins'}
            className="rounded border border-zinc-500/25 bg-white p-2 text-xs dark:bg-zinc-900"
            value={filter} onChange={(event) => { setFilter(event.target.value); setLimit(48); }}>
            <option value="owned">{french ? 'Possédés' : 'Owned'}</option>
            <option value="missing">{french ? 'Manquants' : 'Missing'}</option>
            <option value="all">{french ? 'Tous' : 'All'}</option>
          </select>
        </div>
        <ul className="grid gap-2 sm:grid-cols-3 lg:grid-cols-4">
          {candidates.slice(0, limit).map((skin) => <li key={skin.id} className="flex items-center gap-2 rounded border border-zinc-500/20 p-2">
            {skin.tile && <img src={skin.tile} loading="lazy" alt="" className="size-9 rounded object-cover" />}
            <span className="min-w-0 text-xs"><strong className="block">{skin.name}</strong><span className="text-zinc-500">{skin.champion}</span></span>
          </li>)}
        </ul>
        {candidates.length > limit && <Button size="sm" onClick={() => setLimit(limit + 48)}>{french ? 'Afficher plus' : 'Show more'}</Button>}
        {!candidates.length && <p className="text-xs text-zinc-500">{french ? 'Aucun résultat.' : 'No matching skins.'}</p>}
      </>}
    </section>
  );
}

export function RuneEditor({ state, french }: { state: AppState; french: boolean }) {
  const page = state.runes;
  const [edits, setEdits] = useState<Record<number, number>>({});
  if (!page || !state.runeAlternatives?.some((row) => row.length > 1)) return null;
  return (
    <details className="rounded-xl border border-zinc-500/25 p-4">
      <summary className="cursor-pointer text-sm font-semibold">{french ? 'Personnaliser les runes' : 'Customize runes'}</summary>
      <div className="mt-3 grid gap-2 sm:grid-cols-2">
        {state.runeAlternatives.map((row, index) => row.length > 1 && (
          <label key={index} className="flex flex-col gap-1 text-xs">
            <span>{page.perkDetails?.[index]?.name || `Rune ${index + 1}`}</span>
            <select value={edits[index] ?? page.perks[index]}
              className="rounded border border-zinc-500/25 bg-white p-1.5 dark:bg-zinc-900"
              onChange={(event) => setEdits({ ...edits, [index]: Number(event.target.value) })}>
              {row.map((rune) => <option key={rune.id} value={rune.id}>{rune.name}</option>)}
            </select>
          </label>
        ))}
      </div>
      <div className="mt-3 flex flex-wrap gap-2">
        <Button size="sm" disabled={state.game.inGame || !state.settings.connected} onClick={() => postAction('importRunes', {
          perks: page.perks.map((id, index) => edits[index] ?? id),
        })}>{french ? 'Importer cette page' : 'Import this page'}</Button>
        <Button size="sm" variant="ghost" onClick={() => setEdits({})}>{french ? 'Rétablir les runes' : 'Reset runes'}</Button>
        {state.importStatus && <p role="status" className="self-center text-xs">{state.importStatus}</p>}
      </div>
    </details>
  );
}
