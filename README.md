<p align="center">
  <img src="assets/maylo.png" width="84" alt="MAYLO mascot" />
</p>

<h1 align="center">MAYLO</h1>

<p align="center">
  <strong>A Windows companion for League of Legends</strong><br />
  Recognize your augments, adapt your build and keep match information in view.
</p>

<p align="center">
  <a href="https://github.com/Lysprd/MAYLO/releases"><strong>Download the Windows beta</strong></a>
  · <a href="#see-maylo-in-action">See it in action</a>
  · <a href="https://github.com/Lysprd/MAYLO/issues">Report an issue</a>
</p>

<p align="center"><code>v0.1.0-beta.2</code> · Windows 10/11 x64 · MIT</p>

## See MAYLO in action

<p align="center">
  <img src="docs/media/setup.gif" width="760" alt="MAYLO setup with the animated mascot, installation options, progress and completion" />
</p>

| The Hub | Champion builds |
| --- | --- |
| ![MAYLO Hub with champion and match information](docs/media/hub.webp) | ![Champion build, runes and suggested purchases](docs/media/build.webp) |

<p align="center">
  <img src="docs/media/augments.gif" width="700" alt="Simulated ARAM Mayhem offer where the fourth badge appears without moving the other three" />
</p>

## In game

- **ARAM Mayhem.** Windows OCR recognizes English and French augment titles. Badges are centered above detected cards in rows of three, four or five.
- **ARAM and Summoner’s Rift.** Get mode-specific builds, item suggestions based on your inventory and opponents, runes and summoner spells.
- **In-game overlay.** Open the match sheet with **Alt+G**, edit widget positions with **Alt+O**, or rescan augments with **Alt+A**.

## Install and update

1. Download **MAYLO-v…-Setup.exe** from [Releases](https://github.com/Lysprd/MAYLO/releases). Setup runs for your Windows user and requires Microsoft Edge WebView2. If it’s missing, setup offers to open the [official runtime download page](https://developer.microsoft.com/microsoft-edge/webview2/).
2. Launch MAYLO from its shortcut. In League, use **Borderless** video mode for the overlay.
3. MAYLO checks GitHub for updates in the background, verifies the package’s SHA-256 digest and applies it the next time you open the app. Your settings carry over.

For portable use, extract the full ZIP and run `MAYLO-Hub.exe`. Portable copies are updated manually. See the [quick start guide](QUICKSTART.txt) for more shortcuts.

## Build and release

Run these commands from PowerShell on Windows with Node.js 24 and .NET Framework 4.8.

```powershell
.\scripts\build-release.ps1
.\scripts\test-release.ps1
```

The installer and portable ZIP are written to `dist/`. The app and installer use the same React components and styles in `web/src/`. [VERSION](VERSION) controls the display version and Windows metadata. Changing it on `main` triggers the [Windows release workflow](.github/workflows/release.yml). Beta installations receive pre-releases and stable releases. Stable installations follow stable releases.

MAYLO is an independent project and is not affiliated with Riot Games. [MIT license](LICENSE).
