export type GameModeKey = 'MAYHEM' | 'ARAM' | 'SR';

export interface Champion {
  name: string;
  slug: string;
  tier: string;
  win: number;
  effectiveWin?: number;
  synergyBonus?: number;
  synergyLabel?: string;
  synergyTone?: string;
  isCard?: boolean;
  upgradeDelta?: number;
  bestPick?: boolean;
  pick: number;
  icon: string;
  assetKey: string;
  favorite?: boolean;
  patchTag?: string;
}

export interface StatItem {
  id: number;
  name: string;
  pick: string;
  win: string;
  rate: number;
  icon: string;
  games: number;
  rarity: string;
  tier?: string;
  price?: number;
  preference?: number;
  tracked?: boolean;
  recommended?: boolean;
  synergy?: { partner: string; rate: number } | null;
  synergyBonus?: number;
  topCombo?: { partner: string; rate: number } | null;
}

export interface ComponentStatus {
  id: number;
  name: string;
  owned: boolean;
  price: number;
  icon: string;
}

export interface BuildStep {
  stat: StatItem;
  role: string;
  reason: string;
  shortTip: string;
  synergyTag?: string;
  synergyTone?: string;
  pickRate: number;
  isBoots: boolean;
  totalGold: number;
  remainingGold: number;
  affordable: boolean;
  components: ComponentStatus[];
}

export interface OfferedSlot {
  slot: number;
  stat: StatItem | null;
  x: number;
  y: number;
}

export interface RuneMeta {
  id: number;
  name: string;
  icon: string;
}

export interface RunePage {
  primaryStyle: number;
  secondaryStyle: number;
  primaryMeta?: RuneMeta;
  secondaryMeta?: RuneMeta;
  perks: number[];
  perkDetails?: RuneMeta[];
  name: string;
  source: string;
}

export interface SkillOrder {
  priority: string[];
  levels: string[];
  source: string;
}

export interface ObjectiveTimer {
  key: string;
  name: string;
  status: string;
  alive: boolean;
  secondsLeft: number;
}

export interface TierListEntry {
  id: number;
  slug: string;
  name: string;
  icon: string;
  role: string;
  tier: string;
  rank: number;
  rankDelta?: number;
  patchTag?: string;
  winRate: number;
  pickRate: number;
  banRate: number;
  kda: number;
  games: number;
  counters: string[];
}

export interface PatchDetailLine {
  text: string;
  oldValue: string;
  newValue: string;
  type: string;
}

export interface PatchEntry {
  id: number;
  category: 'champion' | 'augment' | 'item' | 'system' | string;
  mode: 'SR' | 'ARAM' | 'MAYHEM' | string;
  slug: string;
  title: string;
  icon: string;
  changeType: 'buff' | 'nerf' | 'adjust' | 'added' | 'removed' | 'system' | string;
  summary: string;
  playerNote: string;
  details: PatchDetailLine[];
}

export interface PatchNotesData {
  version: string;
  officialUrl: string;
  entries: PatchEntry[];
}

export interface LivePlayer {
  key: string;
  name: string;
  champion: string;
  championName: string;
  championIcon: string;
  team: string;
  position?: string;
  level: number;
  kills: number;
  deaths: number;
  assists: number;
  cs: number;
  dead: boolean;
  respawn: number;
  me: boolean;
  items: number[];
  spell1: string;
  spell2: string;
  spellKey1: string;
  spellKey2: string;
  keystone: string;
  runeTree: string;
  tags?: string[];
  isLaneOpponent?: boolean;
  matchupWr?: number;
}

export interface LiveSummary {
  clock: string;
  seconds: number;
  health: number;
  maxHealth: number;
  ad: number;
  ap: number;
  armor: number;
  mr: number;
  attackSpeed: number;
  moveSpeed: number;
  activeLevel: number;
  q: number;
  w: number;
  e: number;
  r: number;
  blueDragons?: number;
  redDragons?: number;
  lastDragonType?: string;
  players: LivePlayer[];
  events: string[];
  objectives: ObjectiveTimer[];
  inhibitors?: ObjectiveTimer[];
  inventoryValue?: { blue: number; red: number; partial: boolean } | null;
}

export interface MatchRecord {
  id: string;
  championId: number;
  championName: string;
  championIcon: string;
  queue: number;
  queueName: string;
  mode: string;
  mayhem: boolean;
  kills: number;
  deaths: number;
  assists: number;
  won: boolean;
  remake: boolean;
  duration: number;
  time: string;
  dateText: string;
  damage: number;
  gold: number;
  cs: number;
  items: number[];
  augments: number[];
  augmentDetails?: { id: number; name: string; icon: string }[];
}

export interface ItemDetail {
  id: number;
  name: string;
  priceTotal: number;
  owned: boolean;
  planned: boolean;
  stats: string[];
  paragraphs: string[];
  components: { id: number; name: string; price: number }[];
  winRate?: number;
  pickRate?: string;
}

export interface AugmentDetail {
  id: number;
  name: string;
  icon: string;
  rarity: string;
  tier: string;
  rate: number;
  pick: string;
  games: number;
  delta: number;
  description: string;
  preference: number;
  tracked: boolean;
  pairings: { id: number; name: string; icon: string; rate: number }[];
}

export interface AppSettings {
  connected: boolean;
  overlay: boolean;
  showBuildOverlay: boolean;
  showRerollBar: boolean;
  showSkillAdvisor: boolean;
  showPerfStrip: boolean;
  showJungleWidget: boolean;
  showBenchBar: boolean;
  minimalHud: boolean;
  ocr: boolean;
  ocrAllModes: boolean;
  autoLayout: boolean;
  autoPage: boolean;
  autoSpells: boolean;
  autoRunes: boolean;
  autoExportShop: boolean;
  launchWithWindows: boolean;
  minimizeToTray: boolean;
  hotkeySheet: string;
  hotkeyOverlay: string;
  hotkeyEdit: string;
  hotkeyScan: string;
  flashKey: 'D' | 'F';
  tierElo: string;
  tierRole: string;
  buildFocus: string;
  theme: 'light' | 'dark';
  scale: number;
  badgeOffsetY: number;
  minimapScale: number;
  minimapLeft: boolean;
  hudScale?: number;
  onboardingSeen?: boolean;
  panels: Record<string, { x: number; y: number }>;
}

export interface ChampCardOffer {
  name: string;
  slug: string;
  tier: string;
  win: number;
  effectiveWin: number;
  synergyBonus?: number;
  synergyLabel?: string;
  synergyTone?: string;
  patchTag?: string;
  bestPick?: boolean;
  x: number;
  y: number;
}

export interface PortraitHit {
  champion: Champion;
  x: number;
  y: number;
}

export interface AppState {
  version: string;
  ddVersion?: string;
  pinned?: boolean;
  bootReady: boolean;
  bootProgress: number;
  bootStage: string;
  view: string;
  activeMode: GameModeKey;
  detectedMode: GameModeKey | '';
  activeRole: string;
  detectedRole: string;
  availableRoles: string[];
  modeSource: string;
  privateMode: boolean;
  hubActive: boolean;
  inGameSheet: boolean;
  inGameSheetTab: 'build' | 'augments' | 'live';
  preview: boolean;
  previewScene?: string;
  previewAugmentCount?: number;
  editing: boolean;
  overlayVisible: boolean;
  augmentsVisible: boolean;
  refreshingOffers: boolean;
  connectionText: string;
  statusText: string;
  ocrStatus: string;
  ocrLastMs: number;
  ocrLastLines: string[];
  spellStatus: string;
  runeStatus: string;
  shopStatus: string;
  importStatus: string;
  account: { riotId: string; level: number; region: string; status: string; profileIconId?: number; profileIconUrl?: string } | null;
  champions: Champion[];
  tierList: TierListEntry[];
  selected: Champion | null;
  game: {
    phase: string;
    mode: string;
    modeKey?: GameModeKey | '';
    modeLabel?: string;
    champion: string;
    inGame: boolean;
    inSelection: boolean;
    mayhem: boolean;
    gold: number;
    owned: number[];
    planned: number[];
    bench: Champion[];
    champCards?: ChampCardOffer[];
    team: Champion[];
    live: LiveSummary;
  };
  buildSummary: string;
  enemyThreats?: string[];
  buildPath: BuildStep[];
  starters: StatItem[];
  boots: StatItem[];
  spells: StatItem[];
  bestItems: StatItem[];
  teammates: StatItem[];
  matchups: StatItem[];
  combos: StatItem[];
  gameLengths?: { minutes: number; winRate: number }[];
  patchNotes?: PatchNotesData;
  runes: RunePage | null;
  skills: SkillOrder | null;
  offered: OfferedSlot[];
  topAugments: Record<string, StatItem[]>;
  trackedAugments: StatItem[];
  allAugments: StatItem[];
  portraitPositions: PortraitHit[];
  history: MatchRecord[];
  note: string;
  hotkeys: Record<string, boolean>;
  settings: AppSettings;
  inspectedItem: ItemDetail | null;
  inspectedAugment: AugmentDetail | null;
  update?: {
    state: 'idle' | 'checking' | 'downloading' | 'ready' | 'up_to_date' | 'error' | 'portable';
    version: string;
    message: string;
    canRestart: boolean;
  };
}
