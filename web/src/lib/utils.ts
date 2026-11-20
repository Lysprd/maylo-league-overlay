import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export function fmtPct(n?: number | null): string {
  if (n === undefined || n === null || Number.isNaN(n) || n <= 0) return '—';
  return `${n.toFixed(1)}%`;
}

export function parsePct(raw?: string | null): string {
  if (!raw || raw === '—') return '';
  const m = String(raw).match(/[\d]+(?:\.[\d]+)?/);
  if (!m) return '';
  const n = parseFloat(m[0]);
  return Number.isNaN(n) || n <= 0 ? '' : `${n.toFixed(1)}%`;
}

export function fmtGold(n?: number | null): string {
  if (n === undefined || n === null || n < 0) return '—';
  return `${Math.round(n).toLocaleString('en-US')}g`;
}

export function iconUrl(path?: string | null): string {
  if (!path) return '';
  if (path.startsWith('http://') || path.startsWith('https://') || path.startsWith('data:')) return path;
  return `https://mayhemmeta.com${path.startsWith('/') ? path : '/' + path}`;
}

export function itemIconUrl(id: number, ddVersion?: string): string {
  if (ddVersion) return `https://ddragon.leagueoflegends.com/cdn/${ddVersion}/img/item/${id}.png`;
  return `https://mayhemmeta.com/icons/items/${id}.png`;
}

export function spellIconUrl(spellKey?: string | null, ddVersion?: string): string {
  if (!spellKey) return '';
  if (ddVersion) return `https://ddragon.leagueoflegends.com/cdn/${ddVersion}/img/spell/${spellKey}.png`;
  return `https://mayhemmeta.com/icons/spells/${spellKey}.png`;
}
