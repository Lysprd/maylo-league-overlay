# Changelog

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
