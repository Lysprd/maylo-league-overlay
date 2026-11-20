# Changelog

## v0.1.0-beta.13
- **Mode-scoped Overview & Match History:** Both **Overview** (Recent Form, Win Rate, KDA, Trend Graph, Personal Champion Pool) and **Match History** now automatically follow the active mode selected in the top bar (`ARAM Mayhem`, `ARAM`, or `Ranked SR`) with a clean mode badge and zero redundant queue filter bars.
- **Animated glowing Update button:** Upgraded both the topbar `Update · Restart` pill and the sidebar `Restart & Update` card/button with a pulsing indigo-violet glow, shimmer sweep, animated BorderBeam, and live emerald ping dot so available updates stand out immediately.
- **Animated Live Game indicator in sidebar:** Added a pulsing emerald dot right next to **Live game** whenever an active League match is detected (and an amber pulse during Champ Select).

## v0.1.0-beta.12
- **Instant Champ Select Win Rate & Team Synergy (`Cards & Bench`):** Speeds up Champ Select polling (`450ms`), pre-warms mode Tier Lists in memory (zero blank flash), captures ARAM / ARAM Mayhem priority Champion Cards (`isPriority` + pickable subset) and ally intents, and ranks every card & bench option by combined **Win Rate + Team Synergy** (real ally duo WR pre-fetched in background + instant 0ms DDragon comp synergy: `Frontline`, `AP Carry`, `AD DPS`, `Enchanter`). Highlights top upgrade picks with a golden glow and `+X.X%` upgrade delta so you never miss a winning pick.
- **Faster In-Game Augment Card Flip & Synergy Visibility:** Accelerates OCR scanning to `55ms` and keeps wide multi-card capture active during the first 2.5s of card reveal so all 3–4 augment cards lock in the exact frame they flip. Displays active synergy bonus (`+X.X% syn`) right alongside Win Rate plus top combo synergy hints (`Combo +Partner`) even on Augment #1.

## v0.1.0-beta.11
- **Mode-scoped, lighter Patch Notes UI:** Patch notes now automatically follow the active mode in the top bar (`Ranked SR`, `ARAM`, or `ARAM Mayhem`) — removing redundant mode tabs and empty categories. Entries are cleanly grouped into **Buffs**, **Nerfs**, and **New & Adjusted** with compact expandable cards (click to reveal formatted `old → new` stat lines), clickable summary filter pills, and mode-accurate `BUFF`/`NERF` badges across Tier List, Bench, and `Alt+G`.
- **Single-line update buttons:** Fixed the topbar update pill, sidebar `Restart & Update` button, and Settings update button with `whitespace-nowrap` and compact labels so they never wrap onto two lines.
- **Smooth startup loading screen:** Replaced the instant jump to ~70% with a silky `requestAnimationFrame` progress sweep from `0% → 100%`, synchronized 4-step Swiss boot indicators (`Engine · Meta · Client · Ready`), version footer, and a smooth crossfade into the Hub.

## v0.1.0-beta.10

- Detect new GitHub Releases within ~2 minutes (or immediately when focusing maylo) using conditional HTTP `ETag` (`304 Not Modified`) polling with zero extra API rate-limit usage.
- Add an unmistakable **Update Ready** card with a 1-click **Restart & Update** button right in the sidebar footer (alongside the topbar pill and Settings) as soon as an update is downloaded and SHA-256 verified.

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

## v0.1.0-beta.7

- Keep the augment badges in place during reroll animations and update the changed card as soon as OCR reads it.
- Confirm an augment pick after the offer closes, so a reroll is not recorded as a picked augment.
- Preview three-, four-, and five-card layouts in the overlay settings.

## v0.1.0-beta.6

- Keep update status accurate during background checks, show retryable errors, and continue looking for newer releases while a restart is pending.
- Apply staged updates from the hub once champion select or the current match has ended.
- Preserve the Windows sign-in preference when running setup again and honor the minimized startup setting.
- Preserve Windows file provenance and keep unrelated folders intact during update cleanup.
- Replace the launcher atomically during setup and handle unavailable or non-empty installation folders more clearly.
