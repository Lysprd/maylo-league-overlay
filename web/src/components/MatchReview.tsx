import { useEffect, useRef, useState } from 'react';
import { ArrowLeft, Copy } from 'lucide-react';
import { GlassCard } from '@/components/ui/glass-card';
import { Button } from '@/components/ui/primitives';
import { SegmentedTabs } from '@/components/ui/segmented-tabs';
import { postAction } from '@/lib/bridge';
import { iconUrl, itemIconUrl } from '@/lib/utils';
import type { MatchRecord } from '@/types';

type ReviewTab = 'summary' | 'loadout' | 'timeline' | 'notes';
type TimelineFilter = 'all' | 'items' | 'objectives';

export function MatchReview({ matchDetails, ddVersion, french, inGame, onBack }: {
  matchDetails: MatchRecord;
  ddVersion: string;
  french: boolean;
  inGame: boolean;
  onBack: () => void;
}) {
  const [matchTab, setMatchTab] = useState<ReviewTab>('summary');
  const [matchDraft, setMatchDraft] = useState<string | null>(null);
  const [timelineFilter, setTimelineFilter] = useState<TimelineFilter>('all');
  const heading = useRef<HTMLHeadingElement>(null);
  useEffect(() => heading.current?.focus({ preventScroll: true }), []);
  const result = matchDetails.remake ? 'Remake' : matchDetails.won
    ? (french ? 'Victoire' : 'Victory')
    : (french ? 'Défaite' : 'Defeat');
  const kda = `${matchDetails.kills}/${matchDetails.deaths}/${matchDetails.assists}`;
  const finalItems = matchDetails.items.filter((id) => id > 0).slice(0, 6);
  const moments = (matchDetails.timeline ?? []).filter(({ kind }) =>
    timelineFilter === 'all' || (timelineFilter === 'items' ? kind === 'item' : kind !== 'item'));
  const labels: Record<string, string> = {
    DragonKill: 'Dragon', BaronKill: 'Baron', HeraldKill: french ? 'Héraut' : 'Rift Herald',
    HordeKill: french ? 'Larves du Néant' : 'Void Grubs', TurretKilled: french ? 'Tourelle détruite' : 'Turret destroyed',
    InhibKilled: french ? 'Inhibiteur détruit' : 'Inhibitor destroyed',
  };

  return (
    <div className="space-y-4">
      <Button size="sm" variant="ghost" onClick={onBack}>
        <ArrowLeft className="size-4" /> {french ? 'Retour' : 'Back'}
      </Button>
      <GlassCard surface="hub" className="space-y-4 p-5">
        <div className="flex flex-wrap items-center gap-3">
          {matchDetails.championIcon && <img src={iconUrl(matchDetails.championIcon)} alt="" className="size-12 rounded-xl" />}
          <div className="grow">
            <h1 ref={heading} tabIndex={-1} className="text-lg font-bold focus:outline-none">{matchDetails.championName} · {result}</h1>
            <p className="text-xs text-zinc-500">{matchDetails.queueName} · {matchDetails.dateText} · {matchDetails.time}</p>
          </div>
          <Button size="sm" variant="secondary" onClick={() => postAction('copyText', {
            text: `${matchDetails.championName} · ${matchDetails.queueName} · ${kda} · ${matchDetails.time}`,
          })}><Copy className="size-3.5" />{french ? 'Copier le bilan' : 'Copy match summary'}</Button>
        </div>
        {!!finalItems.length && <div className="flex flex-wrap items-center gap-1.5">
          {finalItems.map((id, index) => (
            <img key={`${id}-${index}`} src={itemIconUrl(id, ddVersion)} alt=""
              className="size-8 rounded-md border border-zinc-500/25 bg-zinc-900" />
          ))}
        </div>}
      </GlassCard>
      <GlassCard surface="hub" className="space-y-4 p-5">
        <SegmentedTabs<ReviewTab> size="sm" className="flex-wrap" value={matchTab} onChange={setMatchTab} options={[
          { value: 'summary', label: french ? 'Résumé' : 'Summary' },
          { value: 'loadout', label: french ? 'Équipement' : 'Loadout' },
          { value: 'timeline', label: french ? 'Chronologie' : 'Timeline' },
          { value: 'notes', label: french ? 'Notes et clip' : 'Notes & clip' },
        ]} />
        {matchTab === 'summary' && (
          <dl className="grid gap-2 sm:grid-cols-3">
            {[
              ['KDA', kda],
              [french ? 'Dégâts' : 'Damage', matchDetails.damage.toLocaleString(french ? 'fr-FR' : 'en-US')],
              [french ? 'Or et sbires' : 'Gold and CS', `${(matchDetails.gold / 1000).toFixed(1)}k · ${matchDetails.cs} CS`],
            ].map(([label, value]) => (
              <div key={label} className="rounded-xl border border-zinc-500/25 p-3">
                <dt className="text-xs text-zinc-500">{label}</dt>
                <dd className="mt-1 text-lg font-bold tabular-nums">{value}</dd>
              </div>
            ))}
          </dl>
        )}
        {matchTab === 'loadout' && (
          <section className="space-y-4">
            {!!finalItems.length && (
              <div>
                <h2 className="mb-2 text-xs font-semibold">{french ? 'Build final' : 'Final build'}</h2>
                <div className="flex flex-wrap gap-2">
                  {finalItems.map((id, index) => (
                    <img key={`${id}-${index}`} src={itemIconUrl(id, ddVersion)} alt=""
                      className="size-9 rounded-lg border border-zinc-500/25 bg-zinc-900" />
                  ))}
                </div>
              </div>
            )}
            {!!matchDetails.augmentDetails?.length && (
              <div>
                <h2 className="mb-2 text-xs font-semibold">{french ? 'Augments joués' : 'Augments played'}</h2>
                <ul className="flex flex-wrap gap-2">
                  {matchDetails.augmentDetails.map((augment) => (
                    <li key={augment.id} className="inline-flex items-center gap-2 rounded-lg border border-indigo-400/30 bg-indigo-500/10 px-2.5 py-1.5 text-xs font-medium">
                      {augment.icon && <img src={iconUrl(augment.icon)} alt="" className="size-5 rounded bg-zinc-900" />}
                      <span>{augment.name}</span>
                    </li>
                  ))}
                </ul>
              </div>
            )}
            {!!matchDetails.runes?.length && (
              <div>
                <h2 className="mb-2 text-xs font-semibold">Runes</h2>
                <ul className="flex flex-wrap gap-2">
                  {matchDetails.runes.map((rune, index) => (
                    <li key={`${rune.id}-${index}`} className="flex items-center gap-1 rounded border border-zinc-500/25 p-1.5 text-xs">
                      {rune.icon && <img src={rune.icon} alt="" className="size-5" />}{rune.name}
                    </li>
                  ))}
                </ul>
              </div>
            )}
            {!finalItems.length && !matchDetails.augmentDetails?.length && !matchDetails.runes?.length && (
              <p className="text-xs text-zinc-500">{french ? 'Équipement indisponible pour cette partie.' : 'Loadout unavailable for this match.'}</p>
            )}
          </section>
        )}
        {matchTab === 'timeline' && (
          <section className="space-y-3 text-sm">
            {!matchDetails.timeline?.length ? (
              <p className="text-zinc-500">{french ? 'Aucun événement enregistré pour cette partie.' : 'No events were recorded for this match.'}</p>
            ) : (
              <>
                <SegmentedTabs<TimelineFilter> size="sm" value={timelineFilter} onChange={setTimelineFilter} options={[
                  { value: 'all', label: french ? 'Tout' : 'All' },
                  { value: 'items', label: french ? 'Objets' : 'Items' },
                  { value: 'objectives', label: french ? 'Objectifs' : 'Objectives' },
                ]} />
                <p className="text-xs text-zinc-500">{french ? 'Objets observés dans ton inventaire et objectifs signalés par le jeu.' : 'Items observed in your inventory and objectives reported by the game.'}</p>
                {moments.length ? (
                  <ol className="divide-y divide-zinc-500/20">
                    {moments.map((moment, index) => {
                      const seconds = Math.max(0, Math.floor(moment.seconds));
                      return (
                        <li key={index} className="flex gap-3 py-2">
                          <time className="w-10 shrink-0 tabular-nums text-zinc-500">
                            {Math.floor(seconds / 60)}:{String(seconds % 60).padStart(2, '0')}
                          </time>
                          {moment.kind === 'item' ? moment.itemName : labels[moment.kind] || moment.kind}
                        </li>
                      );
                    })}
                  </ol>
                ) : (
                  <p className="text-zinc-500">{french ? 'Aucun événement pour ce filtre.' : 'No events match this filter.'}</p>
                )}
              </>
            )}
          </section>
        )}
        {matchTab === 'notes' && (
          <section className="space-y-3">
            <label className="block space-y-1">
              <span className="text-xs font-semibold">{french ? 'À retenir pour la prochaine partie' : 'What to remember next game'}</span>
              <textarea className="w-full rounded border border-zinc-500/30 bg-transparent p-2 text-sm" rows={4} maxLength={3000}
                value={matchDraft ?? matchDetails.note ?? ''} onChange={(event) => setMatchDraft(event.target.value)} />
            </label>
            <Button size="sm" disabled={matchDraft === null || inGame} onClick={() => {
              postAction('saveMatchNote', { matchId: matchDetails.id, text: matchDraft });
              setMatchDraft(null);
            }}>{french ? 'Enregistrer la note' : 'Save note'}</Button>
            <div className="flex flex-wrap items-center gap-2 border-t border-zinc-500/20 pt-3">
              {matchDetails.clipName && <>
                <span className="max-w-full truncate text-xs text-zinc-500">{matchDetails.clipName}</span>
                <Button size="xs" disabled={inGame} onClick={() => postAction('openMatchClip', { matchId: matchDetails.id })}>{french ? 'Lire' : 'Play'}</Button>
                <Button size="xs" variant="ghost" disabled={inGame} onClick={() => postAction('removeMatchClip', { matchId: matchDetails.id })}>{french ? 'Dissocier' : 'Unlink'}</Button>
              </>}
              <Button size="xs" disabled={inGame} onClick={() => postAction('attachMatchClip', { matchId: matchDetails.id })}>
                {matchDetails.clipName ? (french ? 'Changer le clip' : 'Change clip') : (french ? 'Associer un clip local' : 'Attach a local clip')}
              </Button>
            </div>
          </section>
        )}
      </GlassCard>
    </div>
  );
}
