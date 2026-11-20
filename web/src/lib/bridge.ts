import { useEffect, useState } from 'react';
import type { AppState } from '@/types';

declare global {
  interface Window {
    chrome?: {
      webview?: {
        postMessage: (msg: unknown) => void;
        addEventListener: (type: string, listener: (ev: { data: unknown }) => void) => void;
        removeEventListener: (type: string, listener: (ev: { data: unknown }) => void) => void;
      };
    };
    __MAYLO_INITIAL_STATE__?: AppState;
  }
}

export function postAction(type: string, payload?: Record<string, unknown>) {
  const msg = { type, ...(payload ?? {}) };
  try {
    window.chrome?.webview?.postMessage(msg);
  } catch {
    // Running outside WebView2 (browser dev preview)
  }
}

export function isOverlayRoute(): boolean {
  const qs = new URLSearchParams(window.location.search);
  return window.location.hash.includes('overlay') || qs.get('surface') === 'overlay';
}

export function useAppState(): AppState | null {
  const [state, setState] = useState<AppState | null>(() => window.__MAYLO_INITIAL_STATE__ ?? null);

  useEffect(() => {
    const wv = window.chrome?.webview;
    if (!wv) return;
    const handler = (ev: { data: unknown }) => {
      try {
        const data = typeof ev.data === 'string' ? JSON.parse(ev.data) : ev.data;
        if (data && typeof data === 'object' && 'version' in (data as Record<string, unknown>)) {
          setState(data as AppState);
        }
      } catch {
        // Ignore malformed host message
      }
    };
    wv.addEventListener('message', handler);
    postAction('ready', { surface: isOverlayRoute() ? 'overlay' : 'hub' });
    return () => wv.removeEventListener('message', handler);
  }, []);

  return state;
}
