import { useState } from 'react';
import { Check, Search } from 'lucide-react';
import { cn, fmtGold } from '@/lib/utils';
import { Button } from '@/components/ui/primitives';
import type { SituationalItem } from '@/types';

const groups = [
  { fr: 'Attaque', en: 'Offense', filters: [
    { id: 'armor-pen', fr: 'Contre l’armure', en: 'Against armor', hintFr: 'Pénétration ou réduction d’armure pour les dégâts physiques.', hintEn: 'Armor penetration or reduction for physical damage.' },
    { id: 'magic-pen', fr: 'Contre la résistance magique', en: 'Against magic resist', hintFr: 'Pénétration magique ou réduction de résistance magique.', hintEn: 'Magic penetration or magic resist reduction.' },
    { id: 'anti-heal', fr: 'Contre les soins', en: 'Against healing', hintFr: 'Regarde comment chaque objet applique Hémorragie.', hintEn: 'Check how each item applies Grievous Wounds.' },
    { id: 'anti-shield', fr: 'Contre les boucliers', en: 'Against shields', hintFr: 'Effets qui réduisent les boucliers ennemis ou les endommagent.', hintEn: 'Effects that reduce or damage enemy shields.' },
    { id: 'health-damage', fr: 'Contre les gros PV', en: 'Against high health', hintFr: 'Des dégâts qui dépendent des PV de la cible.', hintEn: 'Damage that scales with the target’s health.' },
    { id: 'lethality', fr: 'Létalité', en: 'Lethality', hintFr: 'La létalité fournit de la pénétration d’armure fixe.', hintEn: 'Lethality provides flat armor penetration.' },
  ] },
  { fr: 'Défense', en: 'Defense', filters: [
    { id: 'armor', fr: 'Dégâts physiques', en: 'Physical damage', hintFr: 'Objets qui te donnent de l’armure.', hintEn: 'Items that grant armor.' },
    { id: 'magic-resist', fr: 'Dégâts magiques', en: 'Magic damage', hintFr: 'Objets qui te donnent de la résistance magique.', hintEn: 'Items that grant magic resistance.' },
    { id: 'crowd-control', fr: 'Contrôles', en: 'Crowd control', hintFr: 'Ténacité et effets de dissipation. Vérifie quels contrôles l’effet couvre.', hintEn: 'Tenacity and cleanse effects. Check which effects each item covers.' },
    { id: 'anti-attacks', fr: 'Attaques et critiques', en: 'Attacks and crits', hintFr: 'Effets défensifs contre les attaques, critiques ou la vitesse d’attaque.', hintEn: 'Defensive effects against attacks, crits, or attack speed.' },
    { id: 'burst', fr: 'Survie au burst', en: 'Burst protection', hintFr: 'Stase, boucliers de secours, résurrection et boucliers antisorts.', hintEn: 'Stasis, emergency shields, revives, and spell shields.' },
  ] },
  { fr: 'Utilité', en: 'Utility', filters: [
    { id: 'team-protection', fr: 'Protection alliée', en: 'Ally protection', hintFr: 'Objets dont l’effet soigne ou protège des alliés.', hintEn: 'Items with effects that heal or shield allies.' },
    { id: 'slows', fr: 'Ralentissements', en: 'Slows', hintFr: 'Effets qui ralentissent les cibles pour les garder à portée ou se replier.', hintEn: 'Slows help keep targets in range or create distance.' },
    { id: 'mana', fr: 'Mana', en: 'Mana', hintFr: 'Objets qui donnent du mana ou de la régénération de mana.', hintEn: 'Items that grant mana or mana regeneration.' },
    { id: 'sustain', fr: 'Récupération', en: 'Sustain', hintFr: 'Vol de vie et omnivampirisme pour se soigner en infligeant des dégâts.', hintEn: 'Life steal and omnivamp for healing through damage.' },
    { id: 'mobility', fr: 'Mobilité', en: 'Mobility', hintFr: 'Bottes, vitesse de déplacement et déplacements actifs.', hintEn: 'Boots, movement speed, and active dashes.' },
  ] },
];

const normalize = (text: string) => text.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase();

export function SituationalItemGuide({ items, owned, french, onUse, canUse }: {
  items?: SituationalItem[] | null;
  owned: Set<number>;
  french: boolean;
  onUse?: (item: SituationalItem) => void;
  canUse?: (item: SituationalItem) => boolean;
}) {
  const [category, setCategory] = useState(groups[0].filters[0]);
  const [query, setQuery] = useState('');
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const search = normalize(query.trim());
  const visible = (items ?? []).filter((item) => item.categories.includes(category.id) && normalize(item.name).includes(search));
  const selected = visible.find((item) => item.id === selectedId) ?? visible[0];

  return (
    <section className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-sm font-semibold text-white">{french ? 'Objets situationnels' : 'Situational items'}</h2>
        <label className="flex items-center gap-1.5 rounded-lg border border-white/15 px-2 py-1 text-xs">
          <Search className="size-3.5 text-zinc-400" aria-hidden />
          <input type="search" value={query} onChange={(event) => setQuery(event.target.value)}
            className="w-36 bg-transparent text-white" aria-label={french ? 'Rechercher un objet' : 'Search items'}
            placeholder={french ? 'Rechercher un objet…' : 'Search items…'} />
        </label>
      </div>
      <div className="grid gap-2 md:grid-cols-3">
        {groups.map((group) => (
          <fieldset key={group.en} className="rounded-lg border border-white/10 p-2">
            <legend className="px-1 text-xs text-zinc-300">{french ? group.fr : group.en}</legend>
            <div className="flex flex-wrap gap-1.5">
              {group.filters.map((filter) => (
                <button key={filter.id} type="button" aria-pressed={category.id === filter.id}
                  onClick={() => { setCategory(filter); setSelectedId(null); }}
                  className={cn('rounded px-2 py-1 text-xs', category.id === filter.id
                    ? 'bg-indigo-500/30 text-indigo-100 ring-1 ring-indigo-400/60' : 'bg-white/5 text-zinc-300 hover:bg-white/10')}>
                  {french ? filter.fr : filter.en}
                </button>
              ))}
            </div>
          </fieldset>
        ))}
      </div>
      <p className="text-xs text-zinc-400">{french ? category.hintFr : category.hintEn}</p>
      {selected ? (
        <div className="grid items-start gap-3 sm:grid-cols-[minmax(0,.85fr)_minmax(0,1fr)]">
          <ul className="space-y-1">
            {visible.map((item) => (
              <li key={item.id}>
                <button type="button" aria-pressed={item.id === selected.id} onClick={() => setSelectedId(item.id)}
                  className={cn('flex w-full items-center gap-2 rounded-lg border p-1.5 text-left text-xs', item.id === selected.id
                    ? 'border-indigo-400/60 bg-indigo-500/15' : 'border-white/10 hover:border-white/25')}>
                  <img src={item.icon} alt="" className="size-7 shrink-0 rounded" />
                  <span className="min-w-0 flex-1 text-zinc-100">{item.name}</span>
                  {owned.has(item.id) && <Check className="size-3 text-emerald-300" aria-label={french ? 'Acheté' : 'Owned'} />}
                  <span className="shrink-0 tabular-nums text-zinc-400">{fmtGold(item.total)}</span>
                </button>
              </li>
            ))}
          </ul>
          <div className="space-y-2 rounded-lg border border-white/10 bg-black/20 p-3 text-xs text-zinc-300">
            <h3 className="font-semibold text-white">{selected.name}</h3>
            {onUse && canUse?.(selected) && (
              <Button size="xs" variant="secondary" onClick={() => onUse(selected)}>
                {french ? 'Remplacer l’étape actuelle' : 'Replace current step'}
              </Button>
            )}
            {selected.stats.length > 0 && <ul className="space-y-0.5 text-indigo-200">{selected.stats.map((stat, index) => <li key={index}>{stat}</li>)}</ul>}
            {selected.effects.map((effect, index) => <p key={index}>{effect.startsWith('# ') ? <strong className="text-zinc-100">{effect.slice(2)}</strong> : effect}</p>)}
            {!!selected.components.length && (
              <div className="border-t border-white/10 pt-2">
                <h4 className="mb-1 font-semibold">{french ? 'Recette' : 'Recipe'}</h4>
                <ul className="space-y-1">
                  {selected.components.map((component, index) => (
                    <li key={`${component.id}-${index}`} className="flex items-center gap-1.5">
                      <img src={component.icon} alt="" className="size-5 rounded" />
                      <span className="grow">{component.name}</span><span className="tabular-nums">{component.price > 0 ? fmtGold(component.price) : '—'}</span>
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </div>
        </div>
      ) : (
        <p role="status" className="rounded-lg border border-white/10 p-3 text-xs text-zinc-400">
          {items == null ? (french ? 'Chargement des objets…' : 'Loading items…')
            : !items.length ? (french ? 'Catalogue indisponible pour ce mode.' : 'Item catalog unavailable for this mode.')
              : (french ? 'Aucun objet trouvé pour ce filtre.' : 'No items match this filter.')}
        </p>
      )}
    </section>
  );
}
