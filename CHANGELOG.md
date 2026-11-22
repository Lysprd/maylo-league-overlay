# Changelog

## v0.2.0-beta.19

- Source Ranked Solo/Duo builds from U.GG for the active patch, role and rank. Choose Recommended, AP, AD, Tank, Crit, Lethality or On-hit where available, with the observed number of core builds.
- Keep the published core order and starting purchases, including consumable quantities. Show late-game options separately and load the selected variant’s runes, spells and skill order.
- Share the variant across the Hub and Alt+G, remember it per champion, role and rank, and export the selected item order to the League shop. Update the first-launch guide.

## v0.2.0-beta.18

- Replace the current build step from Alt+G with an off-build purchase or a situational item. Changes apply to the current match, and the original step can be restored.
- Show owned recipe components and the remaining cost of the selected build item.

## v0.2.0-beta.17

- Put lane, build focus and saved variants at the top of Alt+G. Selecting a focus switches from a saved variant to the corresponding reference guide.
- Show the selection rate alongside each augment tier. Use a question mark for a missing tier and explain the reason in Alt+G.
- Add an Alt+G situational item catalog with offensive, defensive and utility filters, item effects, prices and recipes for the active shop.

## v0.2.0-beta.16

- Refresh augment tiers when rarity data arrives, and use the same champion and rarity tiers from the catalog on live cards.
- Highlight the active build step in the mini-HUD and Alt+G. Show the next item’s name and the number of items already owned.
- Match dimmed bench portraits and limit badges to the confirmed top-center row. Ignore sidebar portraits and keep badges stable across brief scan misses.

## v0.2.0-beta.15

- Show an augment tier only when supported by the data. Keep the name and rarity visible for unranked augments. Preserve available Mayhem pick rates in the guide even when the source withholds results.
- Advance the compact build HUD as items appear in your inventory. Keep the full order in Alt+G and mark owned items.

## v0.2.0-beta.14

- Load bench portrait art from League first, refresh changed benches immediately and use aligned portrait matches to place the remaining badges in the reported bench order.

## v0.2.0-beta.13

### Hub

- Merge Meta and Champions into one mode-specific list with search, tier filters, and direct build access. Keep the role and rank filters for Summoner’s Rift, and retain your search and filters when returning from a build.
- Keep champions without statistics available for browsing and mark their tier as unknown.

### Overlay

- Anchor each augment badge to its own card. Hovering a card or rerolling a slot no longer shifts neighboring badges.
- Number the first four build items in the compact HUD. Alt+G shows the full build in order and opens the first recipe by default. On wider screens, the recipe sits beside the list. Secondary build options can be expanded when needed.
- Replace the champion-select ribbon with compact, click-through tier badges on recognized ARAM and Mayhem bench portraits. A note icon indicates a saved note, which can be read in the tier-list drawer. Revealed champion cards retain their note excerpts.

## v0.2.0-beta.12

- Show saved champion notes on champion-select cards and the top ribbon. Private mode suppresses the notes.
- Clear the update spinner on background download failures and retry without reusing the failed release check. Enforce the download deadline even when the response stream stops sending data.

## v0.2.0-beta.11

- Choose the metrics shown in the Rift benchmark. Preview panel placement and the champion-select click area at four resolutions in Alt+O or Settings.
- Put the active build, item recipes and League imports at the top of the Build page. Keep variants and detailed stats available below.
- Restrict the Hub and overlay WebView native bridge to the bundled Maylo page.
- Keep the chosen build consistent between the compact HUD and Alt+G. Scope in-game item and augment guides to the actual champion and mode.
- Make Alt+G keyboard navigable and open a champion’s build when selected in the champion select tier list.
- Let unreadable augment badges expire independently and pause card scanning while the overlay is hidden.
- Update only the matching League shop set for each champion, mode and role. Preserve other Maylo and League sets.
- Use the same queue detection for the Hub and the live overlay. Show timeline loading separately from an empty timeline and include all final inventory slots.

## v0.2.0-beta.10

- Open the detected champion’s build from Home during champion select. Browse the tier list while waiting for a pick.
- Keep the tier list available in champion select. Give revealed champion cards and bench portraits compact, mode-specific tier badges. Identify the current champion in the performance panel.
- Replace live purchase prompts with item recipes in Alt+G. Show the same champion and rarity tiers on detected augment cards and in the guide, including after a Golden Reroll. Keep unknown tiers marked and remove numeric augment and Arena item outcomes from public views.
- Save and compare build variants by champion, mode and role. Choose a saved variant from the Hub or Alt+G and keep that guide selected for that champion, mode and role. Edit individual runes within their existing slots and update only the Maylo-managed rune page.
- Open the latest match from Home or review a match from History on a dedicated Hub page. Filter locally observed events by items or objectives, then return to the previous screen with its filters intact. Save a private note or link a local video for external playback.
- Compare recent and previous matches by champion and mode. Show recorded Solo/Duo LP snapshots and an on-demand local skin collection.
- Search app settings and save overlay layout, scale and visibility per mode. Add a minimal preset and a revised French or English first-launch guide.

## v0.2.0-beta.9

- Prioritize documented opening items and account for owned components in Next Buy. Delay early Lord Dominik's Regards when another core item is available and enemies have yet to build substantial armor.
- Show the observed augment rate and match count on each card. Leave close or uncertain offers without a Best badge, and keep personal preferences and unverified synergies out of the default ranking.
- Refresh the rerolled card before showing its stats again. Show reroll targets for every rarity in a mixed offer, excluding the augments already on screen.
- Resolve augment statistics by mode and ID. Use readable rarity labels to disambiguate OCR matches and withhold stats when a title still matches multiple augments.

## v0.2.0-beta.8

- Fit the restart action in the title bar without overlapping the mode tabs or window controls. Keep the full action label in the sidebar and update settings.

## v0.2.0-beta.7

- Keep the CS/min and personal-gold HUD on Summoner's Rift. ARAM and Mayhem retain purchase costs and affordability.
- Rank champion cards and bench picks by their observed mode-specific win or top-four rate, with no simulated team-synergy adjustment.
- Rank buyable items using the strength of the available item stats, purchase order, and bounded match context. Keep situational items out of the opening path, separate pins from inventory, and use the same ranked core items in the shop export.
- Preserve Arena item IDs and stop loading a Mayhem build when another mode's data source is unavailable.

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
