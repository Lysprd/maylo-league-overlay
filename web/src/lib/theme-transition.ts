// 21st.dev-inspired View Transitions API circular reveal origin tracker
let lastOrigin: { x: number; y: number } | null = null;

export function setThemeTransitionOrigin(x: number, y: number) {
  lastOrigin = { x, y };
}

export function consumeThemeTransitionOrigin(): { x: number; y: number } {
  const origin = lastOrigin ?? { x: window.innerWidth - 150, y: 24 };
  lastOrigin = null;
  return origin;
}
