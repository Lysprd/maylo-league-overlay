import { useMemo, useState } from 'react';
import { Search } from 'lucide-react';
import { TierBadge } from '@/components/ui/primitives';
import { iconUrl } from '@/lib/utils';
import type { AugmentEntry } from '@/types';

const rarities = ['All', 'Silver', 'Gold', 'Prismatic'] as const;
const frenchRarities = ['Toutes', 'Argent', 'Or', 'Prismatique'];
const tierOrder = ['S+', 'S', 'A', 'B', 'C', 'D'];
const orderOf = (list: readonly string[], value: string) => {
  const index = list.indexOf(value);
  return index < 0 ? list.length : index;
};

export function AugmentTier({ augment, dark = false, french = false }: {
  augment: Pick<AugmentEntry, 'tier'>;
  dark?: boolean;
  french?: boolean;
}) {
  const label = augment.tier
    ? (french ? `Tier ${augment.tier} dans sa rareté` : `Tier ${augment.tier} within its rarity`)
    : (french ? 'Données insuffisantes pour un tier' : 'Insufficient data for a tier');
  return <span className="shrink-0" title={label} aria-label={label}><TierBadge tier={augment.tier ?? undefined} size="sm" dark={dark} /></span>;
}
const normalize = (text: string) => text.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase();

export function AugmentGuide({ augments, dark = false, french = false }: {
  augments: AugmentEntry[];
  dark?: boolean;
  french?: boolean;
}) {
  const [query, setQuery] = useState('');
  const [rarity, setRarity] = useState<string>('All');
  const [sort, setSort] = useState('tier');
  const [expanded, setExpanded] = useState<number | null>(null);
  const byName = useMemo(() => [...augments].sort((a, b) => a.name.localeCompare(b.name)), [augments]);
  const visible = useMemo(() => {
    const search = normalize(query.trim());
    const results = byName.filter((a) => (rarity === 'All' || a.rarity === rarity) &&
      (!search || normalize(a.name + ' ' + (a.description || '')).includes(search)));
    if (sort === 'tier') {
      results.sort((a, b) => orderOf(rarities, a.rarity) - orderOf(rarities, b.rarity) ||
        orderOf(tierOrder, a.tier ?? '') - orderOf(tierOrder, b.tier ?? ''));
    }
    if (sort === 'popularity') {
      const pickRate = (a: AugmentEntry) => Number.parseFloat(a.pick) || 0;
      results.sort((a, b) => pickRate(b) - pickRate(a));
    }
    return results;
  }, [byName, query, rarity, sort]);

  return (
    <section className={dark ? 'space-y-3 text-zinc-100' : 'space-y-3 text-zinc-900 dark:text-zinc-100'}>
      <div className="flex flex-wrap items-center gap-2">
        <label className="flex min-w-48 flex-1 items-center gap-2 rounded-lg border border-zinc-500/30 px-3 py-2">
          <Search aria-hidden className="size-4 text-zinc-400" />
          <input className="w-full bg-transparent text-sm outline-none" value={query}
            aria-label={french ? 'Rechercher un augment' : 'Search augments'}
            placeholder={french ? 'Nom ou effet…' : 'Name or effect…'}
            onChange={(event) => setQuery(event.target.value)} />
        </label>
        <select value={sort} onChange={(event) => setSort(event.target.value)}
          aria-label={french ? 'Trier les augments' : 'Sort augments'}
          className="rounded-lg border border-zinc-500/30 bg-white px-2 py-2 text-sm text-zinc-900 dark:bg-zinc-900 dark:text-white">
          <option value="tier">{french ? 'Tier par rareté' : 'Tier by rarity'}</option>
          <option value="name">{french ? 'Nom' : 'Name'}</option>
          <option value="popularity">{french ? 'Popularité' : 'Popularity'}</option>
        </select>
      </div>
      <div className="flex flex-wrap gap-1.5" aria-label={french ? 'Rareté' : 'Rarity'}>
        {rarities.map((option, index) => (
          <button key={option} type="button" aria-pressed={rarity === option} onClick={() => setRarity(option)}
            className={`rounded-md border px-2.5 py-1 text-xs ${rarity === option ? 'border-indigo-400/60 bg-indigo-500/20 font-semibold' : 'border-zinc-500/25'}`}>
            {french ? frenchRarities[index] : option}
          </button>
        ))}
      </div>
      <p className="text-[11px] text-zinc-500 dark:text-zinc-400">
        {french ? 'Les tiers comparent les augments de même rareté pour ce champion. ? indique un manque de données.' :
          'Tiers compare augments of the same rarity for this champion. ? means there is not enough data.'}
      </p>
      {visible.length === 0 && <p className="text-sm text-zinc-500">{french ? 'Aucun augment trouvé.' : 'No augments found.'}</p>}
      <div className="grid content-start gap-2 sm:grid-cols-2">
        {visible.map((augment) => {
          const pick = Number.parseFloat(augment.pick);
          const hasPick = Number.isFinite(pick) && pick >= 0 && pick <= 100;
          const isOpen = expanded === augment.id;
          const rarityIndex = rarities.findIndex((value) => value === augment.rarity);
          return (
            <div key={augment.id} className="self-start rounded-lg border border-zinc-500/25">
              <button type="button" aria-expanded={isOpen} onClick={() => setExpanded(isOpen ? null : augment.id)}
                className="flex w-full items-center gap-2.5 p-2.5 text-left">
                {augment.icon && <img src={iconUrl(augment.icon)} alt="" loading="lazy" className="size-8 shrink-0 rounded" />}
                <span className="min-w-0 flex-1">
                  <span className="block text-xs font-semibold">{augment.name}</span>
                  <span className="text-[11px] text-zinc-500 dark:text-zinc-400">
                    {french && rarityIndex > 0 ? frenchRarities[rarityIndex] : augment.rarity}
                  </span>
                </span>
                <AugmentTier augment={augment} dark={dark} french={french} />
                {hasPick && <span className="shrink-0 text-xs tabular-nums text-indigo-500 dark:text-indigo-300">
                  {new Intl.NumberFormat(french ? 'fr-FR' : 'en-US', { style: 'percent', maximumFractionDigits: 2 }).format(pick / 100)}{' '}
                  {french ? 'de sélection' : 'pick rate'}
                </span>}
              </button>
              {isOpen && <p className="border-t border-zinc-500/20 px-3 py-2 text-xs leading-relaxed">
                {augment.description || (french ? 'Description indisponible.' : 'Description unavailable.')}
              </p>}
            </div>
          );
        })}
      </div>
    </section>
  );
}
