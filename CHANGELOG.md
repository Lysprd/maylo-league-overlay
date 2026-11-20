# Changelog

## v0.2.0-beta.6
- **Deep UI, UX & Animation Polish Pass:**
  - **Eliminated `BorderBeam` double-curve seams everywhere:** Flush `inset-0` 1px perimeter mask with mode-matched luminous tones (**Gold** in Arena, **Sky** in ARAM, **Indigo** in Mayhem & Ranked SR) + subtle mode-aware top ambient wash across the Hub.
  - **Tactile spring micro-interactions:** Upgraded `Switch` to a Framer Motion spring knob with subtle active glow, added `active:scale` press feedback & specular highlights on `Button` and topbar actions (with active indicator on Private Mode), silky hover fade on `GlassCard` radial spotlights, and hover-lift micro-animations on Top Champions & Build cards.
  - **Distinct `S+` Tier Badge & Prismatic Iridescent Rims:** Gave `S+` a prestigious luminous gold gradient distinct from `S`, added iridescent `.prismatic-top-rim` accents on Arena Prismatic Items cards & Prismatic Augment overlay badges, and added rarity-coded top accents (Prismatic / Gold / Silver) on Augment badges.
  - **Overlay Motion Choreography:** Added `AnimatePresence` spring slide-in transitions for the Champ Select Tier List Drawer & pill toggle, snappy pop-in motion for Floating Champion Cards & In-Game Augment badges, and Arena-specific Build/Overview ergonomics (hiding irrelevant Runes/Spells/Flash controls in Arena, showing `Top 4` labels & `Shop + OCR` KPI).

## v0.2.0-beta.5
- **Full Arena 2v2 (`CHERRY` / Queue `1700`) Mode:**
  - Dedicated **Arena** topbar mode tab alongside Mayhem, ARAM, and Ranked SR, with automatic queue detection (`1700`, `1710`, `CHERRY`, Custom Arena) and mode-scoped Overview, Match History, and Arena Patch Notes.
  - Live OP.GG Arena data: **Arena 2v2 Tier List** (Top-4 Win Rate %, Pick Rate %, Ban Rate %, patch rank delta), **Prismatic Items**, Core Arena Builds, Arena Boots, Situational Items, Skill Order, **Best Arena Duo Partners**, and **Arena Augments (Silver / Gold / Prismatic)** with official CommunityDragon icons & EN/FR descriptions.
  - Full **In-Game Augment OCR**, floating Augment Tier badges, Reroll Reference Bar, and `Alt+G` Augments tab work natively in Arena just like ARAM Mayhem.
- **Live Tier List Drawer Directly in Champion Select:**
  - Adds a compact **`Tier List`** toggle pill on the top-right edge of Champion Select (Arena, Ranked SR, ARAM, and Mayhem) that expands into a searchable glass drawer with `All / S+ / S / A` tier filters, SR role filters, patch tags, and Top-4 / Win / Ban rates.
  - **Cursor-aware zero-friction click-through:** moving the mouse into the drawer lets you search, scroll, and click champions to inspect builds; moving the mouse back onto League's champion grid restores 100% click-through in `< 40 ms` without closing the drawer.

## v0.2.0-beta.4
- **Strict Riot Games Third-Party Policy & Vanguard Compliance:**
  - **Removed Auto-Accept Queue Pop** (`POST /lol-matchmaking/v1/ready-check/accept`) so maylo never triggers match-accept actions on your behalf.
  - **Removed Enemy Summoner Spell Cooldown Tracker** (in-game `Enemy Spells` overlay widget, click-to-track cooldown timers, haste calculation, and chat-copy buttons) while keeping clean read-only summoner spell icons in the Live scoreboard.
  - **Removed `AttachThreadInput` Win32 call** so maylo never attaches to `League of Legends.exe`'s UI thread when restoring focus.
  - Audited all Win32 calls & LCU endpoints: zero DLL injection, zero DirectX hooks, zero memory reads (`OpenProcess` / `ReadProcessMemory`), zero simulated input (`SendInput` / `mouse_event` / `keybd_event`).

## v0.2.0-beta.3
- **Hold `Tab` to click `RED` / `BLUE` jungle clear (and overlay controls):** Holding bare `Tab` in-game now makes the Minimap `Clear 3:20 · RED | BLUE` bar and left HUD interactive whenever your cursor hovers them (with `WS_EX_NOACTIVATE` so League never loses focus), while keeping the center Tab scoreboard 100% click-through.
- **Instant Champion Card & faster Augment dismissal:** Clicking a spawned ARAM / Mayhem Champion Card hides its floating badges immediately (< 40 ms with a 1.1 s anti-repop cooldown). In-game Augment card clicks now cover the full card interior and dismiss in ~100 ms (`90 ms` hold + `45 ms` OCR tick) while preserving full 1.5 s reroll protection.
- **Fixed sidebar Update card border artifact:** Removed the inner double-curve `BorderBeam` seam on the sidebar Update Ready card for a crisp glowing border and shimmer button.

## v0.2.0-beta.2
- **Floating Tier, Win Rate & Synergy Badges Directly on Spawned Champion Cards:** Detects 2, 3, 4, or 5 ARAM / ARAM Mayhem Champion Cards the instant they appear on the `PICK YOUR CHAMPION` screen using fast localized OCR (~35 ms, zero flicker) and pins a click-through **Tier + Win Rate % + Team Synergy + Golden Best Pick** badge directly on every individual card.

## v0.2.0-beta.1
### Overlay & Champ Select
- **Instant Win Rate & Team Synergy on Cards & Bench:** Speeds up Champ Select polling (`450 ms`), pre-warms ARAM & SR Tier Lists in memory, and ranks Champion Cards & Bench picks by combined **Win Rate + Team Synergy** (real ally duo WR + 0 ms DDragon comp synergy: `Frontline`, `AP Carry`, `AD DPS`, `Enchanter`) with a golden **Best Pick** highlight and `+X.X%` upgrade delta.
- **Full ARAM Mayhem (`3270`), Practice Tool & Custom Game Support:** Recognizes Riot's live ARAM Mayhem queue `3270` (`KIWI`), Practice Tool (`3140` / `PRACTICETOOL`), and Custom SR / ARAM / Mayhem games (`map.gameMode`, `map.id`, `isCustomGame`) so overlays, Champ Select ribbons, and augment OCR work everywhere without `Unsupported mode`.
- **Faster In-Game Augment Card Flip:** Accelerates OCR scanning to `55 ms` with continuous wide multi-card capture during the first 2.5 s of card reveal, plus direct `+X.X% syn` and Augment #1 `Combo +Partner` hints on offered cards.

### Hub & Mode-Scoped Views
- **Lighter Mode-Scoped Patch Notes:** Automatically follows the active topbar mode (`ARAM Mayhem`, `ARAM`, or `Ranked SR`), removes empty categories and redundant mode bars, groups changes into **Buffs / Nerfs / Adjusted** expandable cards with formatted `old → new` stat lines, and applies mode-accurate `BUFF` / `NERF` badges everywhere.
- **Mode-Scoped Overview & Match History:** Overview (Form, Win Rate, KDA, Trend Graph, Top Champions) and Match History now automatically focus on the active mode.
- **Live Game Indicator:** Adds an animated pulsing green dot next to **Live game** in the sidebar whenever a live match is active (and an amber pulse during Champ Select).

### Auto-Updater & UX Polish
- **Fast ETag Auto-Updater + Glowing 1-Click Restart:** Polls GitHub Releases every 2 minutes + immediately on window focus using `ETag` (`304 Not Modified`, zero extra API quota), and highlights available updates with an animated glowing **`Restart & Update`** button (locked single-line) in the sidebar and topbar.
- **Smooth Startup Loading Screen:** Replaces the instant jump to ~70% with a frame-interpolated `0% → 100%` glide, 4-step Swiss boot indicators (`Engine · Meta · Client · Ready`), and a clean crossfade into the Hub.

## v0.1.0-beta.9
- Add a live **Patch Notes (Buff / Nerf)** tab covering Champions, ARAM Mayhem Augments, Items, and Systems (auto-localized FR/EN) plus `BUFF` / `NERF` / `ADJ` badges across Tier List, Champions, Build, Champ Select Bench Bar, and `Alt+G`.
- Add patch rank movement (`▲` / `▼` vs previous patch) to Tier Lists and an Early / Mid / Late game-length scaling curve on the Champion Build page.
- Add Howling Abyss Health Relic timers to the ARAM / Mayhem performance strip and live SR Inhibitor respawn pills above the overlay when an inhibitor is down.
- Fix square corner clipping on the `Alt+G` in-game sheet over transparent WebView2 and refresh rounded DWM corners on out-of-game `Alt+G`.
- Ship a dedicated lightweight `MAYLO.exe` launcher inside the release payload (preserving `Setup.exe` provenance while preventing browser Mark-of-the-Web prompts on Windows auto-start), remove the `--autostart` taskbar flash, speed up augment pick dismissal (~240 ms while keeping 1.5 s reroll protection), and keep the Build HUD visible during in-game augment offers.

## v0.1.0-beta.8
- Identify League queues separately and pause mode-specific overlays and automatic loadouts when the match mode is unsupported.
- Restore the Swiftplay minimap with its own objectives and update the standard Rift objective schedule.
- Compare the gold value of reported inventory items in Alt+G → Live. Incomplete totals are marked as partial.
- Show standard Rift inhibitor respawn timers after confirmed destruction events.
- List supported modes alongside overlay settings.

## v0.1.0-beta.5
- Native Windows installer (`MAYLO-Setup.exe`) and automatic background GitHub Release updater with SHA-256 verification, atomic version switching, and automatic rollback.
- Complete Swiss UI redesign for Hub and transparent WebView2 in-game overlay across Ranked SR, ARAM, and ARAM Mayhem.
