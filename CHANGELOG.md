# Changelog

## v0.1.0-beta.6

- Keep update status accurate during background checks, show retryable errors, and continue looking for newer releases while a restart is pending.
- Apply staged updates from the hub once champion select or the current match has ended.
- Preserve the Windows sign-in preference when running setup again and honor the minimized startup setting.
- Preserve Windows file provenance and keep unrelated folders intact during update cleanup.
- Replace the launcher atomically during setup and handle unavailable or non-empty installation folders more clearly.
