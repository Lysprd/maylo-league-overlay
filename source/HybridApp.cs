using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace MayhemCompanion {
 public static class ChromeHelper {
  public static void ApplyRoundedChrome(Window w, bool resizable, bool isDark = true) {
   w.WindowStyle = WindowStyle.SingleBorderWindow;
   w.AllowsTransparency = false;
   w.ResizeMode = resizable ? ResizeMode.CanResize : ResizeMode.NoResize;
   WindowChrome.SetWindowChrome(w, new WindowChrome {
    CaptionHeight = 0,
    ResizeBorderThickness = resizable ? new Thickness(6) : new Thickness(0),
    GlassFrameThickness = new Thickness(-1),
    CornerRadius = new CornerRadius(0),
    UseAeroCaptionButtons = false
   });
   w.SourceInitialized += (s, e) => {
    var hwnd = new WindowInteropHelper(w).Handle;
    // Set WS_CLIPCHILDREN so parent WPF repaints never flash square corners over the WebView2 HWND during live resize!
    int style = Native.GetWindowLong(hwnd, -16);
    Native.SetWindowLong(hwnd, -16, style | 0x02000000);
    bool nativeRounded = Native.RoundWindow(hwnd, isDark);
    if (!nativeRounded) {
     Action updateRegion = () => {
      if (w.WindowState == WindowState.Maximized) { Native.SetWindowRgn(hwnd, IntPtr.Zero, true); return; }
      var src = PresentationSource.FromVisual(w);
      double dx = src != null && src.CompositionTarget != null ? src.CompositionTarget.TransformToDevice.M11 : 1.0;
      double dy = src != null && src.CompositionTarget != null ? src.CompositionTarget.TransformToDevice.M22 : 1.0;
      int pw = (int)Math.Round(w.ActualWidth * dx), ph = (int)Math.Round(w.ActualHeight * dy), rad = (int)Math.Round(16 * dx);
      if (pw > 20 && ph > 20) {
       IntPtr hrgn = Native.CreateRoundRectRgn(0, 0, pw + 1, ph + 1, rad, rad);
       if (hrgn != IntPtr.Zero && Native.SetWindowRgn(hwnd, hrgn, true) == 0) Native.DeleteObject(hrgn);
      }
     };
     w.SizeChanged += (ss, ee) => updateRegion();
     w.StateChanged += (ss, ee) => updateRegion();
     updateRegion();
    }
   };
   w.StateChanged += (s, e) => {
    var b = w.Content as Border;
    if (b != null) b.Padding = w.WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
   };
  }
 }

 public class HybridOverlayWindow : Window {
  readonly HybridHubWindow app;
  readonly WebView2 web;
  IntPtr handle, target;
  string lastRect = "";
  bool lastInteractive, lastActivate, webReady;
  DateTime focusGraceUntil = DateTime.MinValue, offTargetSince = DateTime.MinValue;
  public bool CaptureExcluded;
  public IntPtr Handle { get { return handle; } }

  public HybridOverlayWindow(HybridHubWindow owner) {
   app = owner;
   Title = "maylo · Overlay";
   WindowStyle = WindowStyle.None;
   AllowsTransparency = true;
   Background = Brushes.Transparent;
   ResizeMode = ResizeMode.NoResize;
   ShowInTaskbar = false;
   ShowActivated = false;
   Topmost = true;
   Width = 1280; Height = 720;
   web = new WebView2 {
    DefaultBackgroundColor = System.Drawing.Color.Transparent,
    HorizontalAlignment = HorizontalAlignment.Stretch,
    VerticalAlignment = VerticalAlignment.Stretch,
    IsHitTestVisible = false
   };
   Content = web;
   SourceInitialized += (s, e) => {
    handle = new WindowInteropHelper(this).Handle;
    SetInteraction(false, false);
    CaptureExcluded = Native.SetWindowDisplayAffinity(handle, 0x11);
   };
   Loaded += async (s, e) => await InitWeb();
   Closing += (s, e) => { try { web.Dispose(); } catch { } };
  }

  async Task InitWeb() {
   try {
    var env = await app.GetWebEnv();
    await web.EnsureCoreWebView2Async(env);
    HybridHubWindow.ConfigureAppWebView(web.CoreWebView2, app.UiDistDir, app.HandleWebMessage);
    web.CoreWebView2.NavigationCompleted += (s, e) => {
     webReady = e.IsSuccess && HybridHubWindow.IsAppPage(web.CoreWebView2.Source);
     if (webReady) app.PushState();
    };
    web.CoreWebView2.Navigate("https://maylo.local/index.html#overlay");
   } catch { }
  }

  public void PostState(string json) {
   if (!webReady || web.CoreWebView2 == null) return;
   try { web.CoreWebView2.PostWebMessageAsJson(json); } catch { }
  }

  public void SetInteraction(bool interactive, bool allowActivate) {
   if (handle == IntPtr.Zero) return;
   // CRITICAL Windows WS_EX_LAYERED fix: alpha=0 (Brushes.Transparent) causes DWM to drop clicks right through to League even without WS_EX_TRANSPARENT!
   // Setting alpha=1/255 when interactive makes WebView2 100% clickable & draggable while remaining visually invisible!
   Background = interactive ? new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)) : Brushes.Transparent;
   web.IsHitTestVisible = interactive;
   int style = Native.GetWindowLong(handle, -20);
   // WS_EX_TOOLWINDOW=0x80, WS_EX_TRANSPARENT=0x20, WS_EX_NOACTIVATE=0x08000000
   int next = style | 0x00000080;
   if (interactive) {
    next &= ~0x00000020; // Clickable!
    if (allowActivate) next &= ~0x08000000; else next |= 0x08000000;
   } else {
    next |= 0x08000020; // Click-through + NoActivate!
   }
   Native.SetWindowLong(handle, -20, next);
   uint swpFlags = interactive && allowActivate
    ? (uint)(0x0001 | 0x0002 | 0x0020 | 0x0040)
    : (uint)(0x0001 | 0x0002 | 0x0010 | 0x0020);
   Native.SetWindowPos(handle, new IntPtr(-1), 0, 0, 0, 0, swpFlags);
   bool wasInteractive = lastInteractive;
   bool wasActivated = lastActivate;
   lastInteractive = interactive;
   lastActivate = allowActivate;
   if (interactive && allowActivate) {
    Activate();
    Native.ForceForeground(handle, true);
    web.Focus();
    System.Windows.Input.Keyboard.Focus(web);
   } else if (wasInteractive && !interactive) {
    focusGraceUntil = DateTime.UtcNow.AddMilliseconds(900);
    if (wasActivated && target != IntPtr.Zero && Native.IsWindow(target)) {
     Native.ForceForeground(target, false);
    }
    // Re-assert overlay strictly ABOVE League in Z-order (without activating)!
    Native.SetWindowPos(handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x0040);
   }
  }

  public void Sync() {
   if (app.HubActive && !app.Preview) { if (IsVisible) Hide(); return; }
   target = app.Preview && !app.Game.InGame && !app.Game.InSelection ? IntPtr.Zero : app.Game.Window;
   if (target == IntPtr.Zero && !app.Preview && (app.IsOverlayEditing || app.InGameSheet)) target = Native.Target(app.Game.InGame);
   bool modeAllowed = app.Game.InSelection || app.Game.Mayhem || app.Config.ShowBuildOverlay || app.Config.OcrAllModes || app.Config.ShowPerfStrip || app.Config.ShowJungleWidget || app.Config.ShowBenchBar;
   bool active = app.OverlayVisible && (app.Preview || (app.Config.Overlay && (app.IsOverlayEditing || app.InGameSheet || ((app.Game.InGame || app.Game.InSelection) && app.Game.ModeKey != "" && modeAllowed))));
   bool desktopPreview = app.Preview && (target == IntPtr.Zero || !Native.IsWindow(target) || !Native.IsWindowVisible(target) || Native.IsIconic(target));
   if (!desktopPreview && (target == IntPtr.Zero || !Native.IsWindow(target) || Native.IsIconic(target))) active = false;
   var fg = Native.GetForegroundWindow();
   var own = new WindowInteropHelper(app).Handle;
   bool inGrace = DateTime.UtcNow < focusGraceUntil;
   // Hide overlay when Desktop Hub is foreground (unless preview/editing/sheet) so Hub stays cleanly in front of LoL!
   if (fg == own && !app.Preview && !app.InGameSheet && !app.Editing) active = false;
   // Do NOT blink/hide when another overlay/toast (Discord, GeForce, Xbox Game Bar) briefly appears!
   bool fgIsExternalOverlay = fg != IntPtr.Zero && ((Native.GetWindowLong(fg, -20) & 0x00000080) != 0); // WS_EX_TOOLWINDOW
   bool onTarget = desktopPreview || inGrace || fgIsExternalOverlay || Native.SameApplication(fg, target) || fg == own || Native.OurWindow(fg);
   if (!onTarget) {
    if (offTargetSince == DateTime.MinValue) offTargetSince = DateTime.UtcNow;
    if ((DateTime.UtcNow - offTargetSince).TotalMilliseconds > 350) active = false;
   } else {
    offTargetSince = DateTime.MinValue;
   }
   if (!active) { if (IsVisible) Hide(); return; }

   // Always Show() BEFORE reading PresentationSource DPI so src is GUARANTEED non-null!
   bool justShown = false;
   if (!IsVisible) { Show(); justShown = true; }
   if (handle == IntPtr.Zero) handle = new WindowInteropHelper(this).Handle;
   if (justShown && handle != IntPtr.Zero) {
    // Assert topmost ONCE on show (never in 40ms tick loop so Discord/GeForce overlays never blink!)
    Native.SetWindowPos(handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
   }
   Native.Rect r;
   if (desktopPreview) {
    var screen = System.Windows.Forms.Screen.FromHandle(own).Bounds;
    r = new Native.Rect { Left = screen.Left, Top = screen.Top, Right = screen.Right, Bottom = screen.Bottom };
   } else r = Native.Bounds(target);
   if (r.Width < 200 || r.Height < 200) { if (IsVisible) Hide(); return; }

   string key = r.Left + "," + r.Top + "," + r.Width + "," + r.Height;
   if (lastRect != key) {
    // Move HWND in physical coordinates FIRST so PerMonitorV2 assigns the exact target monitor's DPI,
    // then sync WPF DIP Width/Height so WebView2 fills 100% of League's exact window on ANY monitor/DPI!
    if (handle != IntPtr.Zero) Native.SetWindowPos(handle, IntPtr.Zero, r.Left, r.Top, r.Width, r.Height, 0x0004 | 0x0010);
    var src = PresentationSource.FromVisual(this);
    double sx = src != null && src.CompositionTarget != null ? src.CompositionTarget.TransformFromDevice.M11 : 1.0;
    double sy = src != null && src.CompositionTarget != null ? src.CompositionTarget.TransformFromDevice.M22 : 1.0;
    Width = r.Width * sx;
    Height = r.Height * sy;
    lastRect = key;
   }
   bool wantInteractive = app.IsOverlayEditing || app.IsTabInteractive || app.IsChampSelectInteractive || app.InGameSheet || app.OverlayDragging;
   // CRITICAL: On Tab hold (or collapsed Champ Select pill hover), keep WS_EX_NOACTIVATE (wantActivate = false) so League keeps keyboard focus;
   // activate only for Alt+G sheet, persistent Edit mode, or when hovering the OPEN Champ Select Tier List drawer (so Search typing works!).
   bool wantActivate = app.InGameSheet || app.Editing || (app.IsChampSelectInteractive && app.ChampTierOpen);
   if (lastInteractive != wantInteractive || lastActivate != wantActivate) {
    SetInteraction(wantInteractive, wantActivate);
   }
  }
 }

 public class HybridHubWindow : Window {
  readonly WebView2 web;
  Border splashCover;
  Task<CoreWebView2Environment> webEnvTask;
  bool webReady, splashDismissed, closing, pollBusy, ocrBusy, selectionBusy, tabHeld, tabInteractive, champSelectInteractive, tabSuppressed, manualModeOverride;
  public bool HubActive, Preview, Editing, OverlayVisible = true, PrivateMode, InGameSheet, OverlayDragging, ChampTierOpen;
  static readonly string[] BenchmarkMetricKeys = { "cs", "kda", "kp", "clock" };
  public string InGameSheetTab = "build", PreviewScene = "";
  public bool IsOverlayEditing { get { return Editing || tabHeld; } }
  public bool IsTabInteractive { get { return tabInteractive; } }
  public bool IsChampSelectInteractive { get { return champSelectInteractive; } }
  public bool IsAugmentMode { get { return ActiveMode == "MAYHEM" || ActiveMode == "ARENA"; } }
  public string UiDistDir { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui-dist"); } }

  public readonly GameBridge Bridge = new GameBridge();
  public readonly ProfileService Profile = new ProfileService();
  public V2Settings Config = new V2Settings();
  public GameSnapshot Game = new GameSnapshot();
  public List<Champion> Champions = new List<Champion>();
  public List<TierListEntry> TierList = new List<TierListEntry>();
  public Champion Selected;
  public ModeBuildData ModeData = new ModeBuildData();
  public string ActiveMode = "MAYHEM", DetectedMode = "MAYHEM", ActiveRole = "", DetectedRole = "", View = "Dashboard";
  string draftCounterKey = "", draftCounterStatus = "";
  List<DraftCounter> draftCounters = new List<DraftCounter>();
  int draftCounterRequest;
  public bool BootReady = false;
  public int BootProgress = 12;
  public string BootStage = "Loading…";
  public string ConnectionText = "Connecting to League client…", StatusText = "maylo · Ready", OcrStatus = "Auto-scan ready · Alt+A", ImportStatus = "";
  public double OcrLastMs;
  public List<string> OcrLastLines = new List<string>();
  byte[] lastDebugPng;
  ulong lastOcrSig, lastChampCardSig;
  DateTime nextOcrRecovery = DateTime.MinValue;
  DateTime offerAppearedAt = DateTime.MinValue, champCardCooldownUntil = DateTime.MinValue;
  int offerScanGeneration, lastRawHitCount, champCardMisses;
  int previewAugmentCount = 3;
  bool manualRoleOverride, leftMouseWasDown, realQuit, wasInGame, hadGameWindow, loadoutBusy, champCardBusy;
  List<ChampionCardHit> champSelectCards = new List<ChampionCardHit>();
  readonly Dictionary<string, List<TierListEntry>> tierListByMode = new Dictionary<string, List<TierListEntry>>(StringComparer.OrdinalIgnoreCase);
  public readonly OfferSession OfferSessionRef;
  readonly OfferSession offerSession = new OfferSession();
  readonly HashSet<int> plannedItems = new HashSet<int>();
  HashSet<int> chosenAugments = new HashSet<int>();
  readonly Dictionary<string, HashSet<int>> trackedByChampion = new Dictionary<string, HashSet<int>>();
  readonly Dictionary<string, bool> hotkeyRegistered = new Dictionary<string, bool>();
  readonly Dictionary<int, bool> keyWasDown = new Dictionary<int, bool>();
  readonly Dictionary<string, DateTime> lastActionFire = new Dictionary<string, DateTime>();
  readonly HashSet<string> refreshedThisLaunch = new HashSet<string>();
  List<PortraitHit> portraitPositions = new List<PortraitHit>();
  readonly Dictionary<string, BitmapImage> benchPortraitImages = new Dictionary<string, BitmapImage>();
  readonly System.Net.Http.HttpClient benchPortraitClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
  bool benchScanBusy;
  ulong benchSignature;
  string benchScanScope = "";
  DateTime nextBenchScan, lastGoodBenchScan;
  int inspectedItemId = -1, inspectedAugmentId = -1, generation, tierGeneration;
  string fastChampion = "", displayedClientChampion = "";
  DateTime lastHubToggle = DateTime.MinValue, hubOpened = DateTime.MinValue, lastGoodGame = DateTime.MinValue, lastFastSelection = DateTime.MinValue, loadoutAttempt = DateTime.MinValue;
  IntPtr handle, hubTarget;
  Rect desktopBounds;
  HybridOverlayWindow overlay;
  System.Windows.Forms.NotifyIcon tray;
  DispatcherTimer pollTimer, followTimer, ocrTimer, selectionTimer, profileTimer;

  void SanitizeConfig() {
   if (Config == null) Config = new V2Settings();
   if (Config.Panels == null) Config.Panels = new Dictionary<string, OverlayPanelPosition>();
   if (Config.MatchNotes == null) Config.MatchNotes = new Dictionary<string, string>();
   if (Config.MatchClips == null) Config.MatchClips = new Dictionary<string, string>();
   if (Config.MatchTimelines == null) Config.MatchTimelines = new Dictionary<string, List<MatchMoment>>();
   if (Config.ModePanels == null) Config.ModePanels = new Dictionary<string, Dictionary<string, OverlayPanelPosition>>();
   if (Config.ModePanelVisibility == null) Config.ModePanelVisibility = new Dictionary<string, Dictionary<string, bool>>();
   Config.BenchmarkMetrics = BenchmarkMetricKeys.Where(key => Config.BenchmarkMetrics != null && Config.BenchmarkMetrics.Contains(key)).ToList();
   if (Config.BenchmarkMetrics.Count == 0) Config.BenchmarkMetrics.AddRange(BenchmarkMetricKeys);
   if (Config.ModePanelScale == null) Config.ModePanelScale = new Dictionary<string, double>();
   if (Config.ModeWidgetScale == null) Config.ModeWidgetScale = new Dictionary<string, Dictionary<string, double>>();
   if (Config.TrackedAugments == null) Config.TrackedAugments = new List<int>();
   if (Config.Layout == null) Config.Layout = new Placement();
   if (Config.AugmentPreferences == null) Config.AugmentPreferences = new Dictionary<string, int>();
   if (Config.Notes == null) Config.Notes = new Dictionary<string, string>();
   if (Config.RankedBuildVariants == null) Config.RankedBuildVariants = new Dictionary<string, string>();
   if (Config.Theme != "light" && Config.Theme != "dark") Config.Theme = "dark";
   Config.FlashKey = String.Equals(Config.FlashKey, "D", StringComparison.OrdinalIgnoreCase) ? "D" : "F";
   if (String.IsNullOrWhiteSpace(Config.TierElo)) Config.TierElo = "emerald_plus";
   if (String.IsNullOrWhiteSpace(Config.TierRole)) Config.TierRole = "ALL";
   if (String.IsNullOrWhiteSpace(Config.HotkeySheet) || Config.HotkeySheet.Trim().Length != 1) Config.HotkeySheet = "G";
   if (String.IsNullOrWhiteSpace(Config.HotkeyOverlay) || Config.HotkeyOverlay.Trim().Length != 1) Config.HotkeyOverlay = "M";
   if (String.IsNullOrWhiteSpace(Config.HotkeyEdit) || Config.HotkeyEdit.Trim().Length != 1) Config.HotkeyEdit = "O";
   if (String.IsNullOrWhiteSpace(Config.HotkeyScan) || Config.HotkeyScan.Trim().Length != 1) Config.HotkeyScan = "A";
   if (Config.Layout.Scale < 0.75 || Config.Layout.Scale > 1.35) Config.Layout.Scale = 1.0;
   if (Config.MinimapScale < 0.55 || Config.MinimapScale > 1.85) Config.MinimapScale = 1.0;
   if (Config.BadgeOffsetY < -0.15 || Config.BadgeOffsetY > 0.15) Config.BadgeOffsetY = 0.0;
   if (Config.HudScale < 0.6 || Config.HudScale > 1.5) Config.HudScale = 1.0;
   Bridge.Lockfile = Config.Lockfile ?? "";
  }

  public void PrepareQuit() { realQuit = true; }

  public HybridHubWindow(bool diagnostics = false, bool startMinimized = false) {
   Directory.CreateDirectory(WebCache.Root);
   try {
    var loaded = Json.Serializer().Deserialize<V2Settings>(File.ReadAllText(Path.Combine(WebCache.Root, "settings-v3.json")));
    if (loaded != null) Config = loaded;
   } catch { }
   SanitizeConfig();
   if (!diagnostics) Config.LaunchWithWindows = MayloRelease.InstallStore.StartupEnabled(StartupExecutable());
   // Reset stale pre-v14 overlay panel coordinates once (flush edge-to-edge layout!)
   if (Config.LayoutVersion < 14) {
    Config.Panels.Clear();
    Config.Layout = new Placement();
    Config.BadgeOffsetY = 0;
    Config.LayoutVersion = 14;
    Save();
   }

   Title = "maylo · League & Mayhem Companion";
   MinWidth = 960; MinHeight = 640;
   Width = Math.Max(MinWidth, Math.Min(1220, SystemParameters.WorkArea.Width - 40));
   Height = Math.Max(MinHeight, Math.Min(790, SystemParameters.WorkArea.Height - 45));
   // Preserve multi-monitor placement if saved titlebar lies on any connected screen; otherwise clamp to primary!
   bool onAnyScreen = false;
   try {
    var probe = new System.Drawing.Rectangle((int)Config.Left + 80, (int)Config.Top + 24, 100, 32);
    onAnyScreen = System.Windows.Forms.Screen.AllScreens.Any(sc => sc.WorkingArea.IntersectsWith(probe));
   } catch { }
   if (onAnyScreen) { Left = Config.Left; Top = Config.Top; }
   else {
    Left = Math.Max(0, Math.Min(Config.Left, SystemParameters.WorkArea.Right - Width));
    Top = Math.Max(0, Math.Min(Config.Top, SystemParameters.WorkArea.Bottom - Height));
   }
   bool isDark = !String.Equals(Config.Theme, "light", StringComparison.OrdinalIgnoreCase);
   ChromeHelper.ApplyRoundedChrome(this, true, isDark);
   try { Icon = new BitmapImage(new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "maylo.png"))); } catch { }

   web = new WebView2();
   var rootGrid = new Grid();
   rootGrid.Children.Add(web);
   if (!diagnostics) {
    // Clean solid-theme backdrop hides WebView2's white Win32 surface for ~200ms while Chromium boots,
    // then cleanly reveals the animated React SplashScreen with ZERO color jump or double-logo flicker!
    web.Visibility = Visibility.Hidden;
    splashCover = new Border();
    rootGrid.Children.Add(splashCover);
   }
   Content = new Border { Child = rootGrid };
   ApplyWindowTheme(isDark);
   OfferSessionRef = offerSession;
   if (diagnostics) { BootReady = true; BootProgress = 100; BootStage = "Ready"; return; }

   SourceInitialized += (s, e) => {
    handle = new WindowInteropHelper(this).Handle;
    var hs = HwndSource.FromHwnd(handle);
    if (hs != null) hs.AddHook(WndHook);
    RegisterAppHotkeys();
   };
   Activated += (s, e) => { if (!closing) GitHubUpdater.CheckOnActivate(); };

    Loaded += async (s, e) => {
    GitHubUpdater.StatusChanged += () => {
     if (!closing) Dispatcher.BeginInvoke((Action)(() => { if (!closing) PushState(); }));
    };
    try {
     BootProgress = 12; BootStage = "Initializing workspace…";
     await InitWeb();
     BootProgress = 26; BootStage = "Preparing overlay engine…"; PushState();
     overlay = new HybridOverlayWindow(this);
     overlay.Show();
     SetupTray();
     if (startMinimized && Config.MinimizeToTray && tray != null) Hide();
     StartTimers();
     BootProgress = 44; BootStage = "Loading Riot Data Dragon & Meta…"; PushState();
     await LoadHome(true);
     BootProgress = 68; BootStage = "Connecting League of Legends client…"; PushState();
     await Poll();
     BootProgress = 86; BootStage = "Syncing Tier List & Patch Notes…"; PushState();
     await RefreshTierList(false);
     // Quietly pre-warm Arena, ARAM, and SR Tier Lists so Champ Select Tier List & winrates are available at 0ms when entering any queue
     foreach (var warmMode in new[] { "ARENA", "ARAM", "SR" }) {
      if (warmMode == ActiveMode) continue;
      string wm = warmMode;
      var _w = MultiModeParser.LoadTierList(wm, "ALL", Config.TierElo, Champions, false).ContinueWith(t => {
       if (!closing && t.Status == TaskStatus.RanToCompletion && t.Result != null && t.Result.Count > 0)
        Dispatcher.BeginInvoke((Action)(() => { tierListByMode[wm] = t.Result; }));
      });
     }
     var _ = RefreshProfile(true);
     var _pn = MultiModeParser.LoadPatchNotes(false).ContinueWith(t => {
      if (!closing && t.Status == TaskStatus.RanToCompletion && t.Result != null && t.Result.Entries.Count > 0)
       Dispatcher.BeginInvoke((Action)(() => { if (!closing) PushState(); }));
     });
     BootProgress = 100; BootStage = "Ready"; BootReady = true; PushState();
    } catch (Exception ex) {
     RevealWebSurface();
     StatusText = "Startup error: " + ex.Message;
     BootProgress = 100; BootReady = true; PushState();
    }
   };

   Closing += (s, e) => {
    if (!realQuit && HubActive) {
     e.Cancel = true;
     DismissHub(true);
     return;
    }
    if (!realQuit && Config.MinimizeToTray && tray != null) {
     e.Cancel = true;
     Hide();
     return;
    }
    closing = true;
    if (pollTimer != null) pollTimer.Stop();
    if (followTimer != null) followTimer.Stop();
    if (ocrTimer != null) ocrTimer.Stop();
    if (selectionTimer != null) selectionTimer.Stop();
    if (profileTimer != null) profileTimer.Stop();
    if (tray != null) {
     tray.Visible = false;
     try { if (tray.Icon != null) tray.Icon.Dispose(); } catch { }
     try { if (tray.ContextMenuStrip != null) tray.ContextMenuStrip.Dispose(); } catch { }
     tray.Dispose();
     tray = null;
    }
    if (overlay != null) { try { overlay.Close(); } catch { } overlay = null; }
    try { if (handle != IntPtr.Zero) { var hs = HwndSource.FromHwnd(handle); if (hs != null) hs.RemoveHook(WndHook); } } catch { }
    var saveBounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
    Config.Left = HubActive ? desktopBounds.Left : saveBounds.Left;
    Config.Top = HubActive ? desktopBounds.Top : saveBounds.Top;
    Save();
    foreach (int id in new[] { 1, 2, 3, 4, 5, 6 }) Native.UnregisterHotKey(handle, id);
    try { web.Dispose(); } catch { }
   };
  }

  public void ApplyWindowTheme(bool dark) {
   var brush = dark
    ? new SolidColorBrush(Color.FromRgb(11, 12, 16))
    : new SolidColorBrush(Color.FromRgb(247, 248, 250));
   Background = brush;
   var b = Content as Border;
   if (b != null) b.Background = brush;
   if (splashCover != null) splashCover.Background = brush;
   if (web != null) {
    web.DefaultBackgroundColor = dark
     ? System.Drawing.Color.FromArgb(255, 11, 12, 16)
     : System.Drawing.Color.FromArgb(255, 247, 248, 250);
   }
   if (handle != IntPtr.Zero) Native.SetBorderTheme(handle, dark);
  }

  void RevealWebSurface() {
   if (splashDismissed) return;
   splashDismissed = true;
   web.Visibility = Visibility.Visible;
   if (splashCover != null) splashCover.Visibility = Visibility.Collapsed;
  }

  static int KeyToVk(string key, int fallback) {
   string t = (key ?? "").Trim();
   if (t.Length > 0) {
    char c = char.ToUpperInvariant(t[0]);
    if (c >= 'A' && c <= 'Z') return (int)c;
   }
   return fallback;
  }

  void RegisterAppHotkeys() {
   if (handle == IntPtr.Zero) return;
   foreach (int id in new[] { 1, 2, 3, 4, 5, 6 }) Native.UnregisterHotKey(handle, id);
   hotkeyRegistered.Clear();
   var map = new[] {
    Tuple.Create(1, KeyToVk(Config.HotkeyOverlay, 0x4D), "Alt+" + Config.HotkeyOverlay),
    Tuple.Create(2, KeyToVk(Config.HotkeyEdit, 0x4F), "Alt+" + Config.HotkeyEdit),
    Tuple.Create(4, KeyToVk(Config.HotkeySheet, 0x47), "Alt+" + Config.HotkeySheet),
    Tuple.Create(5, 0x48, "Alt+H"),
    Tuple.Create(6, 0x4C, "Alt+L")
   };
   foreach (var entry in map) hotkeyRegistered[entry.Item3] = Native.RegisterHotKey(handle, entry.Item1, 0x4001, (uint)entry.Item2);
  }

  static string StartupExecutable() {
   var root = MayloRelease.InstallStore.InstalledRoot();
   return root == null ? System.Reflection.Assembly.GetExecutingAssembly().Location
    : Path.Combine(root, MayloRelease.InstallStore.Launcher);
  }

  public static void SetStartupRegistry(bool enabled) {
   try {
    using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)) {
     if (key == null) return;
     if (enabled) {
      key.SetValue("MAYLO", "\"" + StartupExecutable() + "\" --autostart");
     } else if (key.GetValue("MAYLO") != null) key.DeleteValue("MAYLO", false);
    }
   } catch { }
  }

  public Task<CoreWebView2Environment> GetWebEnv() {
   if (webEnvTask == null) {
    string dataDir = Path.Combine(WebCache.Root, "WebView2-v12");
    Directory.CreateDirectory(dataDir);
    webEnvTask = CoreWebView2Environment.CreateAsync(null, dataDir);
   }
   return webEnvTask;
  }

  public WebView2 WebControl { get { return web; } }
  public async Task InitWebForTest(string hash) {
   if (web.CoreWebView2 == null) {
    string testDir = Path.Combine(WebCache.Root, "WebView2-Test-" + System.Diagnostics.Process.GetCurrentProcess().Id);
    Directory.CreateDirectory(testDir);
    var env = await CoreWebView2Environment.CreateAsync(null, testDir);
    await web.EnsureCoreWebView2Async(env);
    web.CoreWebView2.SetVirtualHostNameToFolderMapping("maylo.local", UiDistDir, CoreWebView2HostResourceAccessKind.Allow);
   }
   var tcs = new TaskCompletionSource<bool>();
   EventHandler<CoreWebView2NavigationCompletedEventArgs> onDone = null;
   onDone = (s, e) => { web.CoreWebView2.NavigationCompleted -= onDone; webReady = true; tcs.TrySetResult(true); };
   web.CoreWebView2.NavigationCompleted += onDone;
   string q = hash.TrimStart('#');
   string th = String.Equals(Config.Theme, "light", StringComparison.OrdinalIgnoreCase) ? "light" : "dark";
   web.CoreWebView2.Navigate("https://maylo.local/index.html?surface=" + q + "&theme=" + th + hash);
   await Task.WhenAny(tcs.Task, Task.Delay(5000));
   PushState();
  }

  async Task InitWeb() {
   var env = await GetWebEnv();
   await web.EnsureCoreWebView2Async(env);
   ConfigureAppWebView(web.CoreWebView2, UiDistDir, HandleWebMessage);
   web.CoreWebView2.NavigationCompleted += (s, e) => {
    webReady = e.IsSuccess && IsAppPage(web.CoreWebView2.Source);
    if (webReady) PushState();
   };
   string th = String.Equals(Config.Theme, "light", StringComparison.OrdinalIgnoreCase) ? "light" : "dark";
   web.CoreWebView2.Navigate("https://maylo.local/index.html?theme=" + th + "#hub");
  }

  internal static bool IsAppPage(string url) {
   Uri page;
   return Uri.TryCreate(url, UriKind.Absolute, out page) && page.Scheme == Uri.UriSchemeHttps &&
    page.Host == "maylo.local" && page.IsDefaultPort && page.UserInfo == "" && page.AbsolutePath == "/index.html";
  }

  internal static void ConfigureAppWebView(CoreWebView2 core, string uiDist, Action<string> onMessage) {
   core.Settings.AreDefaultContextMenusEnabled = false;
   core.Settings.IsStatusBarEnabled = false;
   core.Settings.AreDevToolsEnabled = Environment.GetEnvironmentVariable("MAYLO_DEVTOOLS") == "1";
   core.SetVirtualHostNameToFolderMapping("maylo.local", uiDist, CoreWebView2HostResourceAccessKind.Allow);
   core.NavigationStarting += (s, e) => { if (!IsAppPage(e.Uri)) e.Cancel = true; };
   core.NewWindowRequested += (s, e) => { e.Handled = true; };
   core.WebMessageReceived += (s, e) => {
    if (IsAppPage(e.Source)) onMessage(e.WebMessageAsJson);
   };
  }

  void SetupTray() {
   try {
    tray = new System.Windows.Forms.NotifyIcon {
     Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location),
     Text = "maylo · Alt+" + Config.HotkeySheet + " / F9 in game",
     Visible = true
    };
    var menu = new System.Windows.Forms.ContextMenuStrip();
    menu.Items.Add("Open maylo", null, (s, e) => Dispatcher.Invoke(() => { ShowInTaskbar = true; Show(); WindowState = WindowState.Normal; Native.ForceForeground(handle); }));
    menu.Items.Add("Quit maylo", null, (s, e) => Dispatcher.Invoke(() => { realQuit = true; Close(); }));
    tray.ContextMenuStrip = menu;
    tray.DoubleClick += (s, e) => Dispatcher.Invoke(() => { ShowInTaskbar = true; Show(); WindowState = WindowState.Normal; Native.ForceForeground(handle); });
   } catch { }
  }

  void OnGameEnded() {
   matchTierScope = ""; augmentTierSource = null;
   wasInGame = false;
   hadGameWindow = false;
   Game.InGame = false;
   Game.Window = IntPtr.Zero;
   ResetOfferSession();
   InGameSheet = false;
   OverlayDragging = false;
   chosenAugments.Clear();
   trackedByChampion.Clear();
   plannedItems.Clear();
   Config.TrackedAugments.Clear();
   Bridge.ResetSpellSelection();
   Save();
   if (overlay != null) overlay.Sync();
   PushState();
   var _ = RefreshProfile(true);
  }

  void StartTimers() {
   followTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
   followTimer.Tick += (s, e) => {
    // Instant 40ms Exit-Game detection: only trigger once the League window actually appeared and then closed!
    if (Game.InGame && Game.Window != IntPtr.Zero && Native.IsWindow(Game.Window) && Native.IsWindowVisible(Game.Window)) {
     hadGameWindow = true;
    } else if (Game.InGame && hadGameWindow) {
     OnGameEnded();
    }
    UpdateHotkeys(); MaintainHub(); UpdateTabEditing(); TrackAugmentClick(); if (overlay != null) overlay.Sync();
   };
   followTimer.Start();

   pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1300) };
   pollTimer.Tick += async (s, e) => {
    bool fastPhase = Game.InSelection || Game.Phase == "ReadyCheck" || Game.Phase == "Matchmaking";
    int wantMs = fastPhase ? 450 : 1300;
    if ((int)pollTimer.Interval.TotalMilliseconds != wantMs) pollTimer.Interval = TimeSpan.FromMilliseconds(wantMs);
    await Poll();
   };
   pollTimer.Start();

   selectionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
   selectionTimer.Tick += async (s, e) => await FollowSelection();
   selectionTimer.Start();

   ocrTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
   ocrTimer.Tick += async (s, e) => {
    if (Game.InSelection && !Game.InGame) {
     ocrTimer.Interval = TimeSpan.FromMilliseconds(150);
     bool allowCards = Config.Overlay && OverlayVisible && Config.Connected && !HubActive && !InGameSheet && !IsOverlayEditing
      && (Game.ModeKey == "MAYHEM" || Game.ModeKey == "ARAM");
     if (allowCards) {
      if (ModePanelVisible("bench", Config.ShowBenchBar)) {
       var _ = ReadBenchPortraits();
      }
      await ReadChampSelectCards();
     }
     return;
    }
    if (portraitPositions.Count > 0) {
     portraitPositions.Clear();
     benchSignature = 0;
     benchScanScope = "";
    }
    if (champSelectCards.Count > 0) {
     champSelectCards = new List<ChampionCardHit>();
     lastChampCardSig = 0; champCardMisses = 0;
     PushState();
    }
    ocrTimer.Interval = TimeSpan.FromMilliseconds(offerSession.Visible || lastRawHitCount >= 1 ? 55 : 150);
    if (Game.InGame && Config.Ocr && Config.Overlay && OverlayVisible && Config.Connected && !HubActive && !InGameSheet &&
        !IsOverlayEditing && IsAugmentMode && Game.ModeKey == ActiveMode)
     await ReadAugments(false);
   };
   ocrTimer.Start();

   profileTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(18) };
   profileTimer.Tick += async (s, e) => await RefreshProfile(false);
   profileTimer.Start();
  }

  IntPtr WndHook(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled) {
   // Suppress Win32 background erase during live drag-resize so square unclipped corners NEVER flash in the angles!
   if (msg == 0x0014) {
    handled = true;
    return (IntPtr)1;
   }
   if (msg == 0x312) {
    handled = true;
    switch (w.ToInt32()) {
     case 1: FireHotkey("Overlay", true); break;
     case 2: FireHotkey("Edit", true); break;
      case 4: FireHotkey("Build", true); break;
     case 5: FireHotkey("History", true); break;
     case 6: FireHotkey("Live", true); break;
    }
   }
   return IntPtr.Zero;
  }

  bool KeyEdge(int vk) {
   bool down = (Native.GetAsyncKeyState(vk) & 0x8000) != 0;
   bool prev; keyWasDown.TryGetValue(vk, out prev);
   keyWasDown[vk] = down;
   return down && !prev;
  }

  void UpdateHotkeys() {
   bool alt = (Native.GetAsyncKeyState(0x12) & 0x8000) != 0;
   var fg = Native.GetForegroundWindow();
   bool inLeagueOrApp = Native.OurWindow(fg) || Native.SameApplication(fg, Game.Window) || Native.SameApplication(fg, Native.Target(true)) || Native.SameApplication(fg, Native.Target(false));
   bool edgeSheet = KeyEdge(KeyToVk(Config.HotkeySheet, 0x47)), edgeL = KeyEdge(0x4C), edgeH = KeyEdge(0x48), edgeOverlay = KeyEdge(KeyToVk(Config.HotkeyOverlay, 0x4D)), edgeEdit = KeyEdge(KeyToVk(Config.HotkeyEdit, 0x4F)), edgeF9 = KeyEdge(0x78), edgeEsc = KeyEdge(0x1B);
   if (edgeEsc && inLeagueOrApp) {
    if (InGameSheet) { CloseInGameSheet(true); return; }
    if (ChampTierOpen) { ChampTierOpen = false; if (overlay != null) overlay.Sync(); PushState(); return; }
    if (HubActive && Native.OurWindow(fg)) { DismissHub(true); return; }
   }
   if (edgeF9 && inLeagueOrApp) { FireHotkey("Build", false); return; }
   if (!alt || !inLeagueOrApp) return;
   if (edgeSheet) FireHotkey("Build", false);
   else if (edgeL) FireHotkey("Live", false);
   else if (edgeH) FireHotkey("History", false);

   else if (edgeOverlay) FireHotkey("Overlay", false);
   else if (edgeEdit) FireHotkey("Edit", false);
  }

  void FireHotkey(string action, bool fromGlobal) {
   DateTime prev;
   if (lastActionFire.TryGetValue(action, out prev) && (DateTime.UtcNow - prev).TotalMilliseconds < 320) return;
   lastActionFire[action] = DateTime.UtcNow;
   switch (action) {
    case "Overlay":
     OverlayVisible = !OverlayVisible;
     if (!OverlayVisible) {
      ResetOfferSession();
      champSelectCards.Clear();
      lastChampCardSig = 0;
     }
     if (overlay != null) overlay.Sync();
     PushState();
     break;
    case "Edit": Editing = !Editing; OverlayVisible = true; if (!Editing) { Preview = false; PreviewScene = ""; OverlayDragging = false; Save(); } if (overlay != null) overlay.Sync(); PushState(); break;
    case "Build": HotkeyDestination("Build", fromGlobal); break;
    case "History": HotkeyDestination("History", fromGlobal); break;
    case "Live": HotkeyDestination("Live", fromGlobal); break;
   }
  }

  void TrackAugmentClick() {
   bool down = (Native.GetAsyncKeyState(0x01) & 0x8000) != 0;
   bool edge = down && !leftMouseWasDown;
   leftMouseWasDown = down;
   if (!edge || HubActive || InGameSheet || IsOverlayEditing) return;

   // 1. Instant dismissal when clicking a Champion Card in ARAM / ARAM Mayhem Champ Select (<40ms!)
   if (Game.InSelection && !Game.InGame && champSelectCards.Count >= 2) {
    IntPtr clientWin = Game.Window != IntPtr.Zero ? Game.Window : Native.Target(false);
    if (clientWin != IntPtr.Zero && Native.SameApplication(Native.GetForegroundWindow(), clientWin)) {
     Native.Point cpt;
     if (Native.GetCursorPos(out cpt)) {
      var cr = Native.Bounds(clientWin);
      if (cr.Width >= 400 && cr.Height >= 250) {
       double crx = (cpt.X - cr.Left) / (double)cr.Width;
       double cry = (cpt.Y - cr.Top) / (double)cr.Height;
       bool hitCard = cry >= 0.16 && cry <= 0.88 && champSelectCards.Any(c => Math.Abs(c.X - crx) <= 0.14);
       if (hitCard) {
        champSelectCards = new List<ChampionCardHit>();
        lastChampCardSig = 0;
        champCardMisses = 0;
        champCardCooldownUntil = DateTime.UtcNow.AddMilliseconds(1100);
        PushState();
       }
      }
     }
    }
    return;
   }

   // 2. Fast pick / reroll tracking when clicking an In-Game Augment Card
   if (!offerSession.Visible || !Game.InGame) return;
   if (!Native.SameApplication(Native.GetForegroundWindow(), Game.Window)) return;
   Native.Point pt;
   if (!Native.GetCursorPos(out pt)) return;
   var r = Native.Bounds(Game.Window);
   if (r.Width < 300 || r.Height < 200) return;
   double rx = (pt.X - r.Left) / (double)r.Width;
   double ry = (pt.Y - r.Top) / (double)r.Height;
   if (offerSession.TrackClick(rx, ry, DateTime.UtcNow)) {
    // A capture started before the click may still show the old card.
    lastOcrSig = 0;
    nextOcrRecovery = DateTime.MinValue;
    offerScanGeneration++;
    if (offerSession.PendingPick && ocrTimer != null) ocrTimer.Interval = TimeSpan.FromMilliseconds(45);
    if (offerSession.PendingRerollId != 0) PushState();
   }
  }

  void UpdateTabEditing() {
   bool ctrl = (Native.GetAsyncKeyState(0x11) & 0x8000) != 0;
   bool tabKey = (Native.GetAsyncKeyState(9) & 0x8000) != 0;
   bool editDown = ctrl && tabKey;
   if (!editDown) tabSuppressed = false;
   bool allowed = !HubActive && !InGameSheet && (Game.InGame || Game.InSelection || Preview) && Config.Overlay && OverlayVisible
    && (Native.SameApplication(Native.GetForegroundWindow(), Game.Window) || Native.OurWindow(Native.GetForegroundWindow()));
   bool nextEdit = editDown && !tabSuppressed && allowed;

   // Bare Tab (without Ctrl): allow clicking overlay buttons (RED/BLUE jungle clear, Build HUD)
   // when hovering their corner, while keeping the center League Tab scoreboard 100% click-through!
   bool nextTabClick = false;
   if (tabKey && !ctrl && allowed && Game.InGame && !Editing && Game.Window != IntPtr.Zero) {
    Native.Point pt;
    if (Native.GetCursorPos(out pt)) {
     var r = Native.Bounds(Game.Window);
     if (r.Width >= 400 && r.Height >= 250) {
      double rx = (pt.X - r.Left) / (double)r.Width;
      double ry = (pt.Y - r.Top) / (double)r.Height;
      bool overMinimap = ry >= 0.50 && (Config.MinimapLeft ? rx <= 0.36 : rx >= 0.64);
      bool overLeftHud = rx <= 0.24 && ry >= 0.06 && ry <= 0.78;
      nextTabClick = overMinimap || overLeftHud;
     }
    }
   }

   // Champ Select Tier List Drawer: cursor-aware interactive hit-zone on the right edge of Champ Select!
   // Moving cursor onto the pill or open drawer makes it clickable/searchable; moving back onto League's champ grid restores 100% click-through in <40ms!
   bool inChampSelectSurface = (Game.InSelection && !Game.InGame && Game.ModeKey != "") || (Preview && PreviewScene == "bench");
   if (!inChampSelectSurface && ChampTierOpen) { ChampTierOpen = false; PushState(); }
   bool nextChampClick = false;
   if (inChampSelectSurface && allowed && !tabHeld && (!Editing || PreviewScene == "bench")) {
    Native.Point pt;
    if (Native.GetCursorPos(out pt)) {
     Native.Rect r;
     IntPtr win = Game.Window != IntPtr.Zero && Native.IsWindow(Game.Window) ? Game.Window : Native.Target(false);
     if (win != IntPtr.Zero && Native.IsWindowVisible(win) && !Native.IsIconic(win)) {
      r = Native.Bounds(win);
     } else if (Preview) {
      var screen = System.Windows.Forms.Screen.FromHandle(handle).Bounds;
      r = new Native.Rect { Left = screen.Left, Top = screen.Top, Right = screen.Right, Bottom = screen.Bottom };
     } else {
      r = new Native.Rect();
     }
     if (r.Width >= 400 && r.Height >= 250) {
      double rx = (pt.X - r.Left) / (double)r.Width;
      double ry = (pt.Y - r.Top) / (double)r.Height;
      nextChampClick = ChampSelectOverlayLayout.ContainsTierListPointer(rx, ry, r.Width, r.Height, ChampTierOpen);
     }
    }
   }

   bool changed = false;
   if (nextEdit != tabHeld && (nextEdit || !OverlayDragging)) {
    tabHeld = nextEdit;
    changed = true;
   }
   if (nextTabClick != tabInteractive) {
    tabInteractive = nextTabClick;
    changed = true;
   }
   if (nextChampClick != champSelectInteractive) {
    champSelectInteractive = nextChampClick;
    changed = true;
   }
   if (!changed) return;
   if (overlay != null) overlay.Sync();
   if (nextEdit == tabHeld) PushState();
  }

  void ToggleInGameSheet(string tab) {
   if (InGameSheet && InGameSheetTab == tab) { CloseInGameSheet(true); return; }
   InGameSheet = true;
   InGameSheetTab = tab;
   OverlayVisible = true;
   if (overlay != null) {
    overlay.Sync();
    overlay.SetInteraction(true, true);
    Native.ForceForeground(overlay.Handle, true);
   }
   PushState();
  }

  void CloseInGameSheet(bool focusGame) {
   InGameSheet = false;
   if (PreviewScene == "altg") { Preview = false; PreviewScene = ""; }
   if (overlay != null) {
    overlay.SetInteraction(IsOverlayEditing, Editing);
    overlay.Sync();
   }
   if (focusGame && Game.Window != IntPtr.Zero && Native.IsWindow(Game.Window)) Native.ForceForeground(Game.Window, false);
   PushState();
  }

  void HotkeyDestination(string destination, bool fromGlobal) {
   // In-game: ZERO-LAG centered overlay sheet instead of spawning the heavy desktop Hub!
   if (Game.InGame && Game.ModeKey != "" && overlay != null && Config.Overlay) {
    string tab = destination == "Live" ? "live" : (destination == "Build" ? "build" : "build");
    ToggleInGameSheet(tab);
    return;
   }
   var fg = Native.GetForegroundWindow();
   if (fromGlobal || Native.OurWindow(fg) || Native.SameApplication(fg, Game.Window) || Native.SameApplication(fg, Native.Target(true)) || Native.SameApplication(fg, Native.Target(false)))
    ToggleHub(destination);
  }

  async void ToggleHub(string destination) {
   if (DateTime.UtcNow - lastHubToggle < TimeSpan.FromMilliseconds(260)) return;
   lastHubToggle = DateTime.UtcNow;
   IntPtr target = Native.Target(true); if (target == IntPtr.Zero) target = Native.Target(false);
   if (HubActive && View == destination) { DismissHub(true); return; }
   if (!HubActive) {
    desktopBounds = new Rect(Left, Top, Width, Height); hubTarget = target; HubActive = true; WindowState = WindowState.Normal;
    if (target != IntPtr.Zero) {
     var b = Native.Bounds(target);
     if (b.Width > 400) {
      var src = PresentationSource.FromVisual(this);
      double scaleX = src != null && src.CompositionTarget != null ? src.CompositionTarget.TransformFromDevice.M11 : 1.0;
      double scaleY = src != null && src.CompositionTarget != null ? src.CompositionTarget.TransformFromDevice.M22 : 1.0;
      Width = Math.Min(1280, Math.Max(MinWidth, b.Width * scaleX * .90));
      Height = Math.Min(840, Math.Max(MinHeight, b.Height * scaleY * .89));
      Left = b.Left * scaleX + (b.Width * scaleX - Width) / 2;
      Top = b.Top * scaleY + (b.Height * scaleY - Height) / 2;
     }
    }
   }
   hubOpened = DateTime.UtcNow; View = destination; Topmost = true; Show(); Activate();
   if (handle != IntPtr.Zero) Native.RoundWindow(handle, !String.Equals(Config.Theme, "light", StringComparison.OrdinalIgnoreCase));
   Native.ForceForeground(handle);
   if (overlay != null) overlay.Sync();
   PushState();
   if (destination == "History") await RefreshProfile(true);
  }

  void DismissHub(bool focusGame) {
   HubActive = false; Topmost = false; Hide();
   if (desktopBounds.Width > 0) { Left = desktopBounds.Left; Top = desktopBounds.Top; Width = desktopBounds.Width; Height = desktopBounds.Height; }
   if (focusGame && hubTarget != IntPtr.Zero && Native.IsWindow(hubTarget)) Native.ForceForeground(hubTarget);
   if (overlay != null) overlay.Sync();
   PushState();
  }

  void MaintainHub() {
   if (!HubActive || DateTime.UtcNow - hubOpened < TimeSpan.FromMilliseconds(1400)) return;
   var fg = Native.GetForegroundWindow();
   if (fg == IntPtr.Zero) return;
   if (!Native.SameApplication(fg, hubTarget) && !Native.OurWindow(fg)) DismissHub(false);
  }

  public void Save() {
   try { Config.Lockfile = Bridge.Lockfile; File.WriteAllText(Path.Combine(WebCache.Root, "settings-v3.json"), Json.Serializer().Serialize(Config), Encoding.UTF8); } catch { }
  }

  public Champion FromId(int id) {
   string alias; if (!Bridge.ChampionNames.TryGetValue(id, out alias)) return null;
   return Champions.FirstOrDefault(c => GameBridge.Norm(c.AssetKey) == GameBridge.Norm(alias) || GameBridge.Norm(c.Name) == GameBridge.Norm(alias));
  }

  int ChampionIdOf(Champion c) {
   if (c == null) return 0;
   foreach (var kv in Bridge.ChampionNames)
    if (GameBridge.Norm(kv.Value) == GameBridge.Norm(c.AssetKey) || GameBridge.Norm(kv.Value) == GameBridge.Norm(c.Name)) return kv.Key;
   return 0;
  }

  void EnforceModeView() {
   if (!IsAugmentMode) {
    if (View == "Augments") View = "Library";
    if (InGameSheetTab == "augments") InGameSheetTab = "build";
   }
  }

  public async Task RefreshTierList(bool force = false) {
   int ticket = ++tierGeneration;
   string modeSnap = ActiveMode;
   try {
    var list = await MultiModeParser.LoadTierList(modeSnap, "ALL", Config.TierElo, Champions, force);
    if (list != null && list.Count > 0) tierListByMode[modeSnap] = list;
    if (closing || ticket != tierGeneration) return;
    TierList = list;
    PushState();
   } catch { }
  }

  async Task LoadHome(bool force) {
   try {
    StatusText = "Updating champion meta…"; PushState();
    string html = await WebCache.Page("champions", "https://mayhemmeta.com/", force, h => Parser.Champions(h).Count > 100);
    Champions = Parser.Champions(html);
    await MultiModeParser.EnsureDDragon(Champions);
    if (Selected != null) {
     var existing = Champions.FirstOrDefault(x => x.Slug == Selected.Slug);
     if (existing != null) await SelectChampion(existing, force);
    }
    StatusText = Champions.Count + " champions · updated " + DateTime.Now.ToString("HH:mm");
    PushState();
   } catch (Exception ex) { StatusText = "Meta unavailable: " + ex.Message; PushState(); }
  }

  async Task SelectChampion(Champion c, bool force = false, string roleOverride = null) {
   if (c == null) return;
   int ticket = ++generation;
   if (Selected == null || Selected.Slug != c.Slug) {
    plannedItems.Clear(); inspectedAugmentId = -1; inspectedItemId = -1;
    if (Selected != null) trackedByChampion[Selected.Slug] = chosenAugments;
    HashSet<int> existing; chosenAugments = trackedByChampion.TryGetValue(c.Slug, out existing) ? existing : new HashSet<int>();
    ResetOfferSession();
    if (roleOverride == null && !manualRoleOverride) ActiveRole = DetectedRole;
   }
   if (roleOverride != null) ActiveRole = MultiModeParser.NormalizeRole(roleOverride);
   var prevModeData = ModeData;
   Selected = c; ImportStatus = "";
   string targetRole = ActiveMode == "SR" ? (!String.IsNullOrEmpty(ActiveRole) ? ActiveRole : DetectedRole) : "";
   // Clear stale ModeData immediately when switching champion/mode so SyncLoadout NEVER applies the previous champion's runes/spells!
   if (ModeData == null || ModeData.ChampionSlug != c.Slug || ModeData.ModeKey != ActiveMode ||
       (ActiveMode == "SR" && ((targetRole != "" && ModeData.ActiveRole != targetRole) || ModeData.ActiveElo != Config.TierElo))) {
    ModeData = new ModeBuildData { ChampionSlug = "", ModeKey = ActiveMode, SourceLabel = "" };
   }
   PushState();
   try {
    var data = await MultiModeParser.LoadChampionMode(c, ActiveMode, targetRole, Config.TierElo, force || refreshedThisLaunch.Add(ActiveMode + ":" + c.Slug + ":" + targetRole + ":" + Config.TierElo), Bridge.Items, Champions);
    if (closing || ticket != generation) return;
    if (!IsAugmentMode && data.Augments.Count == 0 && prevModeData != null && prevModeData.ChampionSlug == c.Slug && prevModeData.Augments.Count > 0) data.Augments = prevModeData.Augments;
    data.ChampionSlug = c.Slug;
    string preferred;
    if (data.RankedVariants.Count > 0 && Config.RankedBuildVariants.TryGetValue(RankedVariantScope(data), out preferred)) {
     var variant = data.RankedVariants.FirstOrDefault(build => build.Key == preferred);
     if (variant != null) variant.Apply(data);
    }
    ModeData = data;
    if (ActiveMode == "SR" && !String.IsNullOrEmpty(data.ActiveRole)) ActiveRole = data.ActiveRole;
    EnsureAugmentCatalog();
    StatusText = c.Name + " · " + ModeData.SourceLabel;
    PushState();
    await SyncLoadout();
    if (data.ModeKey == "SR" && data.ActiveRole != "" && ticket == generation) {
     var context = await MultiModeParser.LoadRankedContext(c, data.ActiveRole, data.ActiveElo);
     if (context != null && !closing && ticket == generation) {
      data.Teammates = context.Teammates;
      data.Matchups = context.Matchups;
      data.GameLengths = context.GameLengths;
      PushState();
     }
    }
   } catch (Exception ex) {
    if (ticket == generation) { StatusText = "Build unavailable: " + ex.Message; PushState(); }
   }
  }

  static string RankedVariantScope(ModeBuildData data) {
   return data.ChampionSlug + ":" + data.ActiveRole + ":" + data.ActiveElo;
  }

  List<CollectionSkin> collection = new List<CollectionSkin>();
  bool collectionBusy, collectionFailed;
  string collectionAccount = "";
  MatchReviewCapture reviewCapture;
  string viewedMatchKey = "";
  DateTime reviewSavedAt;

  void UpdateMatchReview() {
   if (!Game.InGame) {
    if (reviewCapture != null) { Save(); reviewCapture = null; }
    return;
   }
   string key = MatchReviewKey(Game.MatchId);
   if (key == "" || Game.Live.Seconds <= 0) return;
   if (reviewCapture == null || reviewCapture.Key != key) {
    List<MatchMoment> existing;
    Config.MatchTimelines.TryGetValue(key, out existing);
    reviewCapture = new MatchReviewCapture(key, existing ?? new List<MatchMoment>());
    reviewSavedAt = DateTime.MinValue;
   }
   if (!reviewCapture.Observe(Game.Live)) return;
   Config.MatchTimelines[key] = reviewCapture.Moments;
   foreach (var old in Config.MatchTimelines.Keys.Where(k => k != key).Take(Math.Max(0, Config.MatchTimelines.Count - 80)).ToList())
    Config.MatchTimelines.Remove(old);
   if ((DateTime.UtcNow - reviewSavedAt).TotalSeconds >= 30) { Save(); reviewSavedAt = DateTime.UtcNow; }
  }

  Dictionary<string, OverlayPanelPosition> PanelsForMode() {
   Dictionary<string, OverlayPanelPosition> panels;
   if (!Config.ModePanels.TryGetValue(ActiveMode, out panels) || panels == null) {
    panels = new Dictionary<string, OverlayPanelPosition>(Config.Panels);
    Config.ModePanels[ActiveMode] = panels;
   }
   return panels;
  }

  bool ModePanelVisible(string panel, bool fallback) {
   Dictionary<string, bool> values;
   bool visible;
   return Config.ModePanelVisibility.TryGetValue(ActiveMode, out values) && values != null && values.TryGetValue(panel, out visible)
    ? visible : fallback;
  }

  void SetModePanelVisible(string panel, bool value) {
   Dictionary<string, bool> values;
   if (!Config.ModePanelVisibility.TryGetValue(ActiveMode, out values) || values == null) {
    values = new Dictionary<string, bool>();
    Config.ModePanelVisibility[ActiveMode] = values;
   }
   values[panel] = value;
  }

  string MatchReviewKey(string matchId) {
   return Profile.Account == null || String.IsNullOrEmpty(matchId) ? "" : Profile.Account.Key + ":" + matchId;
  }

  void EnsureAugmentCatalog() {
   if (!IsAugmentMode || ModeData.ModeKey != ActiveMode) return;
   foreach (var stat in ModeData.Augments.Where(augment => TierLogic.RarityKey(augment.Rarity) == "")) {
    string rarity;
    if (!Bridge.AugmentRarities.TryGetValue(stat.Id, out rarity) || TierLogic.RarityKey(rarity) == "")
     MultiModeParser.AugmentRarityById.TryGetValue(stat.Id, out rarity);
    string resolved = TierLogic.RarityKey(rarity);
    if (resolved != "") {
     stat.Rarity = resolved;
     augmentTierSource = null;
    }
   }
   var seenIds = new HashSet<int>(ModeData.Augments.Select(x => x.Id));
   var catalogIds = ActiveMode == "MAYHEM"
    ? Bridge.MayhemAugmentIds.Concat(MultiModeParser.MayhemAugmentIds)
    : MultiModeParser.ArenaAugmentIds.Except(MultiModeParser.MayhemAugmentIds);
   foreach (int id in catalogIds.Distinct()) {
    if (!seenIds.Add(id)) continue;
    string name, rarity, icon;
    if (!MultiModeParser.AugmentNameById.TryGetValue(id, out name) &&
        !Bridge.AugmentNames.TryGetValue(id, out name)) {
     List<string> aliases;
     if (!MultiModeParser.AugmentAliasesById.TryGetValue(id, out aliases) || aliases.Count == 0) continue;
     name = aliases[aliases.Count - 1];
    }
    if (String.IsNullOrWhiteSpace(name)) continue;
    if (!MultiModeParser.AugmentRarityById.TryGetValue(id, out rarity))
     Bridge.AugmentRarities.TryGetValue(id, out rarity);
    MultiModeParser.AugmentIconById.TryGetValue(id, out icon);
    ModeData.Augments.Add(new Stat { Id = id, Name = name, Rarity = TierLogic.RarityKey(rarity),
     Icon = !String.IsNullOrEmpty(icon) ? icon : "/icons/augments/" + id + ".png", Pick = "—" });
   }
  }

  void ResetOfferSession() {
   offerSession.Reset();
   lastOcrSig = 0;
   lastRawHitCount = 0;
   offerAppearedAt = DateTime.MinValue;
   nextOcrRecovery = DateTime.MinValue;
   offerScanGeneration++;
  }

  async Task FollowSelection() {
   if (selectionBusy || closing || !Config.Connected || !Config.AutoPage) return;
   selectionBusy = true;
   try {
    string alias = await Bridge.ReadSelectedChampion();
    if (alias == "" || closing || Game.InGame) return;
    fastChampion = alias; lastFastSelection = DateTime.UtcNow;
    if (!Game.InSelection) displayedClientChampion = "";
    Game.Champion = alias; Game.InSelection = true; Game.Window = Native.Target(false);
    if (Game.ModeKey != "") FollowPage(alias);
   } catch { } finally { selectionBusy = false; }
  }

  void FollowPage(string alias) {
   if (!Config.AutoPage || alias == "" || GameBridge.Norm(alias) == GameBridge.Norm(displayedClientChampion)) return;
   var c = Champions.FirstOrDefault(x => GameBridge.Norm(x.AssetKey) == GameBridge.Norm(alias) || GameBridge.Norm(x.Name) == GameBridge.Norm(alias));
   if (c == null) return;
   displayedClientChampion = alias;
   if (!HubActive || (View != "History" && View != "Live")) View = "Build";
   if (Selected == null || Selected.Slug != c.Slug) { var _ = SelectChampion(c); } else PushState();
  }

  async Task Poll() {
   if (pollBusy || closing || !Config.Connected) return;
   pollBusy = true;
   var oldGame = Game;
   try {
    var fresh = await Bridge.Read();
    if (closing || !Config.Connected) return;
    Game = fresh;
    UpdateMatchReview();
    plannedItems.RemoveWhere(id => Game.Owned.Contains(id));
    lastGoodGame = DateTime.UtcNow;
    bool matchPhase = Game.InSelection || Game.InGame;
    ConnectionText = matchPhase
     ? (Game.InGame ? "Live · " : "Champ Select · ") + Game.ModeLabel +
       (Game.ModeKey == "" && Game.ModeLabel != "Unsupported mode" ? " · Unsupported mode" : "")
     : "Client connected · " + Game.Phase + (String.IsNullOrEmpty(Game.Mode) ? "" : " · " + Game.ModeLabel);

    bool enteringMatch = (!oldGame.InSelection && Game.InSelection) || (!wasInGame && Game.InGame);
    if (enteringMatch) {
     draftCounterRequest++;
     draftCounterKey = draftCounterStatus = "";
     draftCounters.Clear();
     // Entering a real Champ Select or Game ALWAYS adapts to the actual match mode & role!
     manualModeOverride = false;
     manualRoleOverride = false;
     displayedClientChampion = "";
    }

    bool inQueue = Game.Phase == "Matchmaking" || Game.Phase == "ReadyCheck";
    bool hasModeSignal = Game.InSelection || Game.InGame || (Game.Phase == "Lobby" && (!String.IsNullOrEmpty(Game.Mode) || Game.QueueId > 0 || Game.Mayhem));
    if (hasModeSignal) {
     string detected = Game.ModeKey;
     DetectedMode = detected;
     if (!manualModeOverride && detected != "" && ActiveMode != detected) {
      ActiveMode = detected;
      if (ActiveMode != "SR") { ActiveRole = ""; DetectedRole = ""; }
      EnforceModeView();
      displayedClientChampion = "";
      List<TierListEntry> cachedTier;
      TierList = tierListByMode.TryGetValue(detected, out cachedTier) && cachedTier != null
       ? cachedTier : new List<TierListEntry>();
      var _t = RefreshTierList(false);
      if (Selected != null && (Game.Champion == "" || ChampionMatches(Selected, Game.Champion))) {
       var _ = SelectChampion(Selected, false);
      }
     }
     if (Game.InSelection || Game.InGame) {
      string roleDet = MultiModeParser.NormalizeRole(Game.AssignedRole);
      if (roleDet != "") {
       DetectedRole = roleDet;
       if (!manualRoleOverride && detected == "SR" && ActiveMode == "SR" && ActiveRole != roleDet) {
        ActiveRole = roleDet;
        if (Selected != null) { var _ = SelectChampion(Selected, false, roleDet); }
       }
      }
     }
    } else if (!inQueue) {
     manualModeOverride = false;
     manualRoleOverride = false;
     if (InGameSheet && !Preview) CloseInGameSheet(false);
    }

    if (wasInGame && !Game.InGame) OnGameEnded();
    else if (Game.InGame) {
     wasInGame = true;
     if (Game.Window != IntPtr.Zero && Native.IsWindow(Game.Window) && Native.IsWindowVisible(Game.Window)) hadGameWindow = true;
    }
    if ((!Game.InGame || Game.ModeKey == "") && offerSession.Visible) ResetOfferSession();
    if (matchPhase && Game.ModeKey == "" && InGameSheet && !Preview) CloseInGameSheet(false);
    if (Game.InSelection && String.IsNullOrEmpty(Game.Champion) && DateTime.UtcNow - lastFastSelection < TimeSpan.FromSeconds(2)) Game.Champion = fastChampion;
    if (!oldGame.InGame && Game.InGame) displayedClientChampion = "";
    if (Game.InSelection && champSelectCards.Count > 0 && ((String.IsNullOrEmpty(oldGame.Champion) && !String.IsNullOrEmpty(Game.Champion)) || Game.Bench.Count > oldGame.Bench.Count)) {
     champSelectCards = new List<ChampionCardHit>();
     lastChampCardSig = 0;
     champCardMisses = 0;
     champCardCooldownUntil = DateTime.UtcNow.AddMilliseconds(1100);
    }
    if (Game.ModeKey != "") FollowPage(Game.Champion);
    await SyncLoadout();
    if (overlay != null) overlay.Sync();
    PushState();
   } catch {
    ConnectionText = "Client unavailable · manual mode";
    if (DateTime.UtcNow - lastGoodGame > TimeSpan.FromSeconds(5)) {
     if (wasInGame) OnGameEnded();
     Game = new GameSnapshot(); if (overlay != null) overlay.Sync();
    }
    PushState();
   } finally { pollBusy = false; }
  }

  static bool ChampionMatches(Champion c, string nameOrAlias) {
   if (c == null || String.IsNullOrEmpty(nameOrAlias)) return false;
   string n = GameBridge.Norm(nameOrAlias);
   return GameBridge.Norm(c.AssetKey) == n || GameBridge.Norm(c.Name) == n;
  }

  void UseMatchMode(string mode) {
   if (mode == ActiveMode) return;
   ActiveMode = mode;
   manualModeOverride = false;
   manualRoleOverride = false;
   ActiveRole = DetectedRole = mode == "SR" ? MultiModeParser.NormalizeRole(Game.AssignedRole) : "";
   EnforceModeView();
   List<TierListEntry> cached;
   TierList = tierListByMode.TryGetValue(mode, out cached) && cached != null ? cached : new List<TierListEntry>();
   var _ = RefreshTierList(false);
  }

  async Task SyncLoadout() {
   if (loadoutBusy) return;
   if (!Game.InSelection && !Game.InGame) { Bridge.ResetSpellSelection(); return; }
   if (Game.ModeKey != ActiveMode || Selected == null || !ChampionMatches(Selected, Game.Champion)) return;
   // CRITICAL: Ensure ModeData has actually finished loading for THIS champion and THIS mode before pushing runes/spells/shop!
   if (ModeData == null || ModeData.ChampionSlug != Selected.Slug || ModeData.ModeKey != ActiveMode) return;
   if (DateTime.UtcNow - loadoutAttempt < TimeSpan.FromSeconds(0.9)) return;
   loadoutBusy = true;
   loadoutAttempt = DateTime.UtcNow;
   try {
    if (Game.InSelection) {
     if (Config.AutoSpells && ActiveMode != "ARENA" && ModeData.Spells.Count > 0) {
      try { await Bridge.ApplySpells(Selected.AssetKey, SpellPlan.Pair(ModeData.Spells[0].Name), Config.FlashKey); } catch { Bridge.SpellStatus = "Spell update unavailable"; }
     }
     if (Config.AutoRunes && !IsAugmentMode && ModeData.Runes != null) {
      try { await Bridge.ApplyRunesAuto(Selected.AssetKey, ActiveMode, ActiveRole, ModeData.Runes); } catch { Bridge.RuneStatus = "Rune update unavailable"; }
     }
     // Auto-Export Shop Item Set during Champ Select (League loads custom item sets at game start, not mid-game!)
     if (Config.AutoExportShop) {
      try {
       var coreIds = CurrentBuildPath(true).Where(x => ModeData.RankedVariant != null ? Price(x.Stat.Id) > 0 :
        !x.IsBoots && x.Role != "STARTER").Select(x => x.Stat.Id).ToList();
       var starterOrPrism = ActiveMode == "ARENA" && ModeData.PrismaticItems.Count > 0
        ? ModeData.PrismaticItems.Select(x => x.Id).ToList()
        : ModeData.Starters.Select(x => x.Id).ToList();
       await Bridge.ApplyShopAuto(
        Selected.Name, ChampionIdOf(Selected), ActiveMode, ActiveMode == "SR" ? ActiveRole : "",
        starterOrPrism,
        coreIds,
        ModeData.RankedVariant != null ? new List<int>() : ModeData.Boots.Select(x => x.Id).ToList(),
        ModeData.BestItems.Where(x => ModeData.RankedVariant == null || Price(x.Id) > 0).Select(x => x.Id).ToList()
       );
      } catch { }
     }
    }
   } finally { loadoutBusy = false; }
  }

  async Task RefreshProfile(bool force) {
   if (!Config.Connected) return;
   if (await Profile.Refresh(Bridge, force) && !closing) PushState();
  }

  async Task ReadBenchPortraits() {
   if (benchScanBusy || Game.Bench.Count == 0) return;
   string patch = MultiModeParser.DDragonVersion;
   IntPtr target = Game.Window;
   if (target == IntPtr.Zero ||
       !Native.IsWindowVisible(target) || Native.IsIconic(target) ||
       !Native.SameApplication(Native.GetForegroundWindow(), target)) return;
   var bounds = Native.Bounds(target);
   if (bounds.Width < 400 || bounds.Height < 250) return;

   int[] bench = Game.Bench.ToArray();
   var champions = bench.Select(FromId).Where(c => c != null &&
    Regex.IsMatch(c.AssetKey ?? "", @"\A[a-zA-Z0-9]+\z")).ToList();
   champions = champions.GroupBy(c => c.Slug).Select(group => group.First()).ToList();
   if (champions.Count == 0) return;
   string scope = patch + ":" + Game.ModeKey + ":" + bounds.Width + ":" + bounds.Height + ":" + String.Join(",", bench);
   if (scope == benchScanScope && DateTime.UtcNow < nextBenchScan) return;
   if (scope != benchScanScope) {
    benchScanScope = scope;
    benchSignature = 0;
    lastGoodBenchScan = DateTime.MinValue;
    portraitPositions.Clear();
   }
   benchScanBusy = true;
   nextBenchScan = DateTime.UtcNow.AddMilliseconds(400);
   try {
    var missing = champions.Where(c => !benchPortraitImages.ContainsKey(patch + ":" + c.Slug)).ToArray();
    await Task.WhenAll(missing.Select(async c => {
     try {
      byte[] png = null;
      try { png = await Bridge.ReadChampionPortrait(ChampionIdOf(c)); } catch { }
      if (png == null && !String.IsNullOrEmpty(patch)) {
       string url = "https://ddragon.leagueoflegends.com/cdn/" + patch + "/img/champion/" + c.AssetKey + ".png";
       png = await benchPortraitClient.GetByteArrayAsync(url);
      }
      if (png == null) return;
      using (var stream = new MemoryStream(png)) {
       var icon = new BitmapImage();
       icon.BeginInit(); icon.CacheOption = BitmapCacheOption.OnLoad; icon.StreamSource = stream; icon.EndInit(); icon.Freeze();
       benchPortraitImages[patch + ":" + c.Slug] = icon;
      }
     } catch { }
    }));
    var images = new Dictionary<string, BitmapImage>();
    foreach (var c in champions) {
     BitmapImage icon;
     if (benchPortraitImages.TryGetValue(patch + ":" + c.Slug, out icon)) images[c.Slug] = icon;
    }
    if (images.Count == 0 || closing || !Game.InSelection || Game.InGame || !bench.SequenceEqual(Game.Bench)) return;
    int bandHeight = (int)(bounds.Height * .25);
    ulong previous = portraitPositions.Count == champions.Count ? benchSignature : 0;
    var scan = await Task.Run(() => {
     using (var frame = new System.Drawing.Bitmap(bounds.Width, bandHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb)) {
      using (var graphics = System.Drawing.Graphics.FromImage(frame))
       graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, frame.Size);
      ulong signature = AugmentOcr.BandSignature(frame);
      if (signature == previous) return Tuple.Create(signature, (List<PortraitHit>)null);
      var hits = Vision.BenchPortraits(frame, champions, images);
      foreach (var hit in hits) hit.Y *= bandHeight / (double)bounds.Height;
      return Tuple.Create(signature, hits);
     }
    });
    if (closing || !Game.InSelection || Game.InGame || !OverlayVisible || !Config.Overlay || HubActive ||
        Game.Window != target || !Native.SameApplication(Native.GetForegroundWindow(), target) ||
        !bench.SequenceEqual(Game.Bench)) return;
    var currentBounds = Native.Bounds(target);
    if (bounds.Width != currentBounds.Width || bounds.Height != currentBounds.Height ||
        bounds.Left != currentBounds.Left || bounds.Top != currentBounds.Top) return;
    if (scan.Item2 == null) return;
    if (scan.Item2.Count == 0) {
     benchSignature = 0;
     if (portraitPositions.Count == 0 || DateTime.UtcNow - lastGoodBenchScan < TimeSpan.FromMilliseconds(1200)) return;
    } else {
     benchSignature = scan.Item1;
     lastGoodBenchScan = DateTime.UtcNow;
    }
    portraitPositions = scan.Item2;
    PushState();
   } catch { }
   finally { benchScanBusy = false; }
  }

  async Task ReadChampSelectCards() {
   if (champCardBusy || closing || !Game.InSelection || Game.InGame || Champions.Count == 0) return;
   if (DateTime.UtcNow < champCardCooldownUntil) return;
   IntPtr target = Game.Window != IntPtr.Zero ? Game.Window : Native.Target(false);
   if (target == IntPtr.Zero || !Native.IsWindow(target) || !Native.IsWindowVisible(target) || Native.IsIconic(target)) return;
   if (!Native.SameApplication(Native.GetForegroundWindow(), target)) return;
   var rect = Native.Bounds(target);
   if (rect.Width < 400 || rect.Height < 250) return;
   champCardBusy = true;
   try {
    bool preferUserLang = Bridge.ChampionLocalNames.Values.Any(v => !String.IsNullOrEmpty(v) && v.Any(ch => ch > 127))
     || !String.Equals(System.Globalization.CultureInfo.InstalledUICulture.TwoLetterISOLanguageName, "en", StringComparison.OrdinalIgnoreCase);
    var champSnap = Champions.ToList();
    var aliasSnap = new Dictionary<int, string>(Bridge.ChampionNames);
    var localSnap = new Dictionary<int, string>(Bridge.ChampionLocalNames);
    ulong prevSig = champSelectCards.Count >= 2 ? lastChampCardSig : 0UL;
    var scan = await Task.Run(() => {
     var map = AugmentOcr.BuildChampionOcrMap(champSnap, aliasSnap, localSnap);
     return AugmentOcr.ScanChampSelectCards(rect, champSnap, map, preferUserLang, prevSig);
    });
    if (closing || !OverlayVisible || !Config.Overlay || HubActive || !Game.InSelection || Game.InGame ||
        DateTime.UtcNow < champCardCooldownUntil || !Native.SameApplication(Native.GetForegroundWindow(), target)) return;
    if (scan.SkippedUnchanged) return;
    if (scan.Hits.Count >= 2) {
     lastChampCardSig = scan.Signature;
     champCardMisses = 0;
     var stabilized = new List<ChampionCardHit>();
     foreach (var h in scan.Hits) {
      var prev = champSelectCards.FirstOrDefault(p => p.Champion != null && p.Champion.Slug == h.Champion.Slug);
      double x = prev != null && Math.Abs(prev.X - h.X) < 0.015 ? prev.X : h.X;
      double y = prev != null && Math.Abs(prev.Y - h.Y) < 0.015 ? prev.Y : h.Y;
      stabilized.Add(new ChampionCardHit { Champion = h.Champion, X = x, Y = y });
     }
     bool changed = stabilized.Count != champSelectCards.Count
      || stabilized.Where((c, i) => champSelectCards[i].Champion.Slug != c.Champion.Slug || Math.Abs(champSelectCards[i].X - c.X) > 0.012 || Math.Abs(champSelectCards[i].Y - c.Y) > 0.012).Any();
     champSelectCards = stabilized;
     if (changed) {
      PushState();
     }
    } else if (champSelectCards.Count > 0) {
     champCardMisses++;
     int needMisses = !String.IsNullOrEmpty(Game.Champion) ? 1 : 2;
     if (champCardMisses >= needMisses) {
      champSelectCards = new List<ChampionCardHit>();
      lastChampCardSig = 0;
      champCardMisses = 0;
      PushState();
     }
    }
   } catch { } finally { champCardBusy = false; }
  }

  async Task ReadAugments(bool requested) {
   if (ocrBusy || !Config.Connected || !IsAugmentMode || (Game.InGame && Game.ModeKey != ActiveMode)) return;
   EnsureAugmentCatalog();
   if (Game.InGame && (Selected == null || !ChampionMatches(Selected, Game.Champion))) {
    var played = Champions.FirstOrDefault(c => ChampionMatches(c, Game.Champion));
    if (played != null) { await SelectChampion(played); return; }
   }
   if (Selected == null || !ChampionMatches(Selected, Game.Champion) ||
       ModeData.ChampionSlug != Selected.Slug || ModeData.ModeKey != ActiveMode || ModeData.Augments.Count == 0)
    return;
   ocrBusy = true;
   try {
    IntPtr target = Game.InGame ? Game.Window : Native.Target(true);
    if (target == IntPtr.Zero || !Native.IsWindow(target) || !Native.IsWindowVisible(target) || Native.IsIconic(target)) { if (requested) OcrStatus = "Start a League game to scan augments"; return; }
    if (requested && HubActive && !Native.SameApplication(Native.GetForegroundWindow(), target)) { DismissHub(true); await Task.Delay(180); }
    if (!Native.SameApplication(Native.GetForegroundWindow(), target)) { if (requested) OcrStatus = "Focus League and press Alt+A"; return; }
    var rect = Native.Bounds(target);
    if (rect.Width < 300 || rect.Height < 200) return;
    bool preferUserLang = Bridge.AugmentNames.Values.Any(v => !String.IsNullOrEmpty(v) && v.Any(ch => ch > 127))
     || !String.Equals(System.Globalization.CultureInfo.InstalledUICulture.TwoLetterISOLanguageName, "en", StringComparison.OrdinalIgnoreCase);
    var aliasMap = MultiModeParser.BuildAugmentAliasMap(Bridge.AugmentNames);
    var augSnapshot = ModeData.Augments.ToList();
    bool flippingWindow = (offerSession.Visible && (DateTime.UtcNow - offerAppearedAt).TotalMilliseconds < 2500)
     || (offerSession.Visible && offerSession.Choices.Count(x => x != null) < 3)
     || lastRawHitCount == 1;
    bool allowWide = requested || flippingWindow || DateTime.UtcNow >= nextOcrRecovery;
    if (allowWide) nextOcrRecovery = DateTime.UtcNow.AddMilliseconds(flippingWindow ? 120 : 900);
    ulong prevSig = offerSession.Visible && !offerSession.Refreshing && offerSession.PendingRerollId == 0 && !flippingWindow ? lastOcrSig : 0UL;
    int scanGeneration = offerScanGeneration;
    var scanSource = ModeData;
    // Run GDI capture + bitmap scaling + 600-augment fuzzy matching OFF the WPF UI thread!
    var scan = await Task.Run(() => AugmentOcr.ScanGameRect(rect, augSnapshot, aliasMap, preferUserLang, null, allowWide, requested, prevSig));
    if (scanGeneration != offerScanGeneration || scanSource != ModeData || !Config.Ocr || !OverlayVisible || !Game.InGame || closing || Game.Window != target ||
        !Native.IsWindow(target) || !Native.IsWindowVisible(target) || !Native.SameApplication(Native.GetForegroundWindow(), target)) return;
    if (scan.SkippedUnchanged) return;
    lastRawHitCount = scan.Hits.Count;
    lastOcrSig = scan.Hits.Count > 0 ? scan.Signature : 0;
    OcrLastMs = scan.Milliseconds;
    OcrLastLines = scan.Lines;
    if (scan.DebugPng != null) lastDebugPng = scan.DebugPng;
    bool wasVis = offerSession.Visible;
    bool beforeRef = offerSession.Refreshing;
    int beforeReroll = offerSession.PendingRerollId;
    bool changed = offerSession.Observe(scan.Hits, DateTime.UtcNow, requested);
    if (!wasVis && offerSession.Visible) offerAppearedAt = DateTime.UtcNow;
    if (!offerSession.Visible) { lastOcrSig = 0; offerAppearedAt = DateTime.MinValue; }
    var picked = offerSession.ConfirmedPick;
    if (picked != null) {
     if (chosenAugments.Add(picked.Id)) {
      Config.TrackedAugments = chosenAugments.ToList();
      Save();
     }
     OcrStatus = "Picked: " + picked.Name;
    }
    if (!OcrStatus.StartsWith("Picked:") || offerSession.Visible) {
     OcrStatus = offerSession.Visible
      ? offerSession.Choices.Count(x => x != null) + " augments detected" + (scan.UsedTitleBand ? " · fast band (" + Math.Round(scan.Milliseconds) + "ms)" : " · full")
      : (scan.Lines.Count > 0 ? "Scanning (" + scan.Lines.Count + " lines seen)" : "Waiting for augment cards");
    }
    if (changed || beforeRef != offerSession.Refreshing || beforeReroll != offerSession.PendingRerollId || requested) PushState();
   } catch (Exception ex) { OcrStatus = ex.Message; if (requested) PushState(); }
   finally { ocrBusy = false; }
  }

  void SaveLastScanDebug() {
   try {
    Directory.CreateDirectory(WebCache.Root);
    if (lastDebugPng != null) File.WriteAllBytes(Path.Combine(WebCache.Root, "last-scan.png"), lastDebugPng);
    var sb = new StringBuilder();
    sb.AppendLine("maylo " + MayloRelease.BuildInfo.Version + " OCR Diagnostic · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
    sb.AppendLine("Status: " + OcrStatus);
    sb.AppendLine("Last scan ms: " + Math.Round(OcrLastMs));
    sb.AppendLine("Game: inGame=" + Game.InGame + " mode=" + Game.Mode + " mayhem=" + Game.Mayhem + " champ=" + Game.Champion);
    sb.AppendLine("Detected lines (" + OcrLastLines.Count + "):");
    foreach (string l in OcrLastLines) sb.AppendLine("  - " + l);
    File.WriteAllText(Path.Combine(WebCache.Root, "last-scan.txt"), sb.ToString(), Encoding.UTF8);
    StatusText = "Saved OCR debug to " + WebCache.Root + "\\last-scan.txt";
    PushState();
   } catch (Exception ex) { StatusText = "Could not save scan: " + ex.Message; PushState(); }
  }

  Dictionary<int, Dictionary<string, object>> ActiveItemsMap() {
   return Bridge.Items.Count > 0 ? Bridge.Items : MultiModeParser.DDragonItems;
  }

  string[] ItemCategories(int id) {
   Dictionary<string, object> item;
   var map = ActiveItemsMap();
   if (!map.TryGetValue(id, out item)) map.TryGetValue(BuildPlanner.Canonical(id), out item);
   return Json.A(item, "categories").Select(Convert.ToString).ToArray();
  }

  int Price(int id) { Dictionary<string, object> d; var map = ActiveItemsMap(); return map.TryGetValue(id, out d) ? Json.I(d, "priceTotal") : 0; }
  string ItemName(int id) { return MultiModeParser.ItemNameOf(id, Bridge.Items); }

  EnemyThreatProfile CurrentEnemyThreats() {
   return EnemyCompAnalyzer.Analyze(Game.Live, ItemCategories);
  }

  public List<BuildStep> CurrentBuildPath(bool referenceOnly = false) {
   if (Selected == null || ModeData.ChampionSlug != Selected.Slug || ModeData.ModeKey != ActiveMode ||
       (Game.InGame && !Preview && (Game.ModeKey != ActiveMode || !ChampionMatches(Selected, Game.Champion))))
    return new List<BuildStep>();

   if (ModeData.RankedVariant != null)
    return ModeData.RankedVariant.Path.Select(item => new BuildStep {
     Stat = item, Role = "BUILD ITEM", Reason = "U.GG", ShortTip = "",
     IsBoots = MultiModeParser.IsRankedBoot(item.Id), Owned = !referenceOnly && Game.Owned.Contains(item.Id)
    }).ToList();

   var map = ActiveItemsMap();
   var bootIds = new HashSet<int>(ModeData.Boots.Select(x => BuildPlanner.Canonical(x.Id)));
   var nonShopPrismatics = new HashSet<int>(ModeData.PrismaticItems.Select(x => x.Id));
   var statItems = ModeData.Items.Concat(ModeData.BestItems).Concat(ModeData.Boots);
   var candidates = statItems.Where(x => x.Id > 0 && (ActiveMode == "ARENA" || BuildPlanner.Canonical(x.Id) == x.Id) &&
    !nonShopPrismatics.Contains(x.Id) && !MultiModeParser.StarterIds.Contains(BuildPlanner.Canonical(x.Id)) &&
    Price(x.Id) > 0 && (bootIds.Contains(BuildPlanner.Canonical(x.Id)) || Price(x.Id) >= 1500) &&
    (x.Games > 0 || Parser.Number(x.Pick) > 0)).ToList();
   var candidateIds = new HashSet<int>(candidates.Select(x => BuildPlanner.Canonical(x.Id)));
   var complete = new List<int>();
   foreach (int id in referenceOnly ? Enumerable.Empty<int>() : Game.Owned) {
    int canonical = BuildPlanner.Canonical(id);
    bool boots = canonical != 1001 && (bootIds.Contains(canonical) || ItemCategories(id).Contains("Boots", StringComparer.OrdinalIgnoreCase));
    if (boots) { bootIds.Add(canonical); complete.Add(id); continue; }
    Dictionary<string, object> item;
    if (MultiModeParser.StarterIds.Contains(canonical) || !map.TryGetValue(id, out item)) continue;
    int price = Json.I(item, "priceTotal");
    if (candidateIds.Contains(canonical) || (price >= 1500 && Json.A(item, "to").Length == 0))
     complete.Add(id);
   }
   var themes = new List<string>();
   foreach (int aid in !referenceOnly && Game.InGame && IsAugmentMode ? (IEnumerable<int>)chosenAugments : Enumerable.Empty<int>()) {
    List<string> tags;
    if (Bridge.AugmentTags.TryGetValue(aid, out tags) && tags != null) themes.AddRange(tags);
    var augStat = ModeData.Augments.FirstOrDefault(x => x.Id == aid);
    string aname = augStat != null ? augStat.Name : "";
    string adesc = MultiModeParser.GetAugmentDescription(aid, aname, Bridge.AugmentDescriptions);
    string blob = (aname + " " + adesc).ToLowerInvariant();
    if (blob.Contains("crit") || blob.Contains("jeweled") || blob.Contains("vulnerability")) themes.Add("kCrit");
    if (blob.Contains("attack speed") || blob.Contains("vitesse d'attaque") || blob.Contains("on-hit") || blob.Contains("à l'impact") || blob.Contains("dual wield") || blob.Contains("twice thrice")) themes.Add("kAttackSpeed");
    if (blob.Contains("ability power") || blob.Contains("puissance") || blob.Contains("magic damage") || blob.Contains("dégâts magiques") || blob.Contains("eureka") || blob.Contains("phenomenal")) themes.Add("kAP");
    if (blob.Contains("ability haste") || blob.Contains("accélération") || blob.Contains("cooldown") || blob.Contains("récupération") || blob.Contains("bread and") || blob.Contains("recursion")) themes.Add("kHaste");
    if (blob.Contains("heal") || blob.Contains("shield") || blob.Contains("soin") || blob.Contains("bouclier") || blob.Contains("omnivamp")) themes.Add("kHealShield");
    if (blob.Contains("armor") || blob.Contains("armure") || blob.Contains("magic resist") || blob.Contains("résistance magique") || blob.Contains("max health") || blob.Contains("pv max") || blob.Contains("tank")) themes.Add("kResilience");
    if (blob.Contains("move speed") || blob.Contains("vitesse de déplacement")) themes.Add("kSpeed");
   }
   var threats = !referenceOnly && Game.InGame ? CurrentEnemyThreats() : null;
   var list = BuildPlanner.Plan(candidates, complete, bootIds, SelectedModeBaseline(), ItemCategories,
    Config.BuildFocus, themes, ModeData.CorePaths, threats, ItemName, Game.InGame && referenceOnly ? null : plannedItems,
     !referenceOnly && Game.InGame ? (Func<int, double>)(id => (Price(id) - BuildPlanner.RemainingCost(id, Game.Owned, map)) / (double)Price(id)) : null,
     !referenceOnly || ActiveMode != "ARENA");
   // Integrate recommended Starter item at game start (step 0) until any starter is bought, 8 min elapses, or first core is completed!
   if (complete.Count == 0 && ModeData.Starters.Count > 0 && (referenceOnly || !Game.InGame || Game.Live.Seconds < 480)) {
    bool ownsStarter = !referenceOnly && (Game.Owned.Any(id => MultiModeParser.StarterIds.Contains(id))
     || ModeData.Starters.Any(s => s.Id > 0 && Game.Owned.Contains(s.Id)));
    var topStarter = ModeData.Starters.Where(s => s.Id > 0 && Price(s.Id) > 0)
     .OrderByDescending(s => Parser.Number(s.Pick)).ThenByDescending(s => s.Rate).FirstOrDefault();
    if (!ownsStarter && topStarter != null) {
     double sp = 0;
     double.TryParse((topStarter.Pick ?? "").Replace("%", "").Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out sp);
     list.Insert(0, new BuildStep {
      Stat = topStarter,
      Role = "STARTER",
      ShortTip = "Opening buy",
      Reason = "Recommended starter item for " + (Selected != null ? Selected.Name : "this champion") + ".",
      PickRate = sp,
      SynergyTag = "Opening",
      SynergyTone = "gold",
      IsBoots = false
     });
    }
   }
   return list;
  }

  int Preference(Stat s) { if (Selected == null || s == null) return 0; int v; return Config.AugmentPreferences.TryGetValue(Selected.Slug + ":" + s.Id, out v) ? v : 0; }

  List<TierListEntry> TierRowsFor(string mode) {
   List<TierListEntry> cached;
   if (mode == ActiveMode && TierList.Count > 0) return TierList;
   if (tierListByMode.TryGetValue(mode, out cached) && cached != null && cached.Count > 0) return cached;
   return mode == "MAYHEM" ? MultiModeParser.MayhemTierList(Champions) : new List<TierListEntry>();
  }

  static TierListEntry ChampionTierFor(IEnumerable<TierListEntry> rows, Champion champion, string mode, string role) {
   if (champion == null) return null;
   return rows.FirstOrDefault(row => row.Slug == champion.Slug &&
    (mode != "SR" || String.IsNullOrEmpty(role) || MultiModeParser.NormalizeRole(row.Role) == role));
  }

  double SelectedModeBaseline() {
   if (Selected == null) return 50;
   var entry = TierList.FirstOrDefault(t => t.Slug == Selected.Slug &&
    (ActiveMode != "SR" || String.Equals(t.Role, ActiveRole, StringComparison.OrdinalIgnoreCase)));
   return entry != null && entry.WinRate > 0 ? entry.WinRate : ActiveMode == "MAYHEM" && Selected.Win > 0 ? Selected.Win : 50;
  }

  object SerializeStat(Stat s, bool hideOutcome = false) {
   return new {
    id = s.Id, name = s.Name, pick = s.Pick ?? "", win = hideOutcome ? "" : s.Win ?? "",
    rate = hideOutcome ? 0 : s.Rate, icon = s.Icon ?? "", games = s.Games,
    rarity = TierLogic.RarityKey(s.Rarity), price = Price(s.Id)
   };
  }

  ModeBuildData augmentTierSource;
  string matchTierScope = "";
  Dictionary<int, string> augmentTiers = new Dictionary<int, string>();

  int MinAugmentGames { get { return ActiveMode == "ARENA" ? 30 : 100; } }

  void UpdateAugmentTiers(Stat[] catalog, bool matchActive) {
   string scope = matchActive && Selected != null ? ActiveMode + ":" + Selected.Slug : "";
   if (augmentTierSource == ModeData && scope == matchTierScope) return;
   var next = Recommendations.AugmentTiers(catalog, MinAugmentGames);
   if (scope != "" && scope == matchTierScope) {
    foreach (var pair in next)
     if (!augmentTiers.ContainsKey(pair.Key)) augmentTiers.Add(pair.Key, pair.Value);
   } else augmentTiers = next;
   augmentTierSource = ModeData;
   matchTierScope = scope;
  }

  object SerializeAugment(Stat s, bool includeDescription = true) {
   string tier;
   augmentTiers.TryGetValue(s.Id, out tier);
   return new {
    id = s.Id, name = s.Name, icon = s.Icon ?? "", rarity = TierLogic.RarityKey(s.Rarity),
    tier = tier, unratedReason = tier == null ? Recommendations.UnratedAugmentReason(s, MinAugmentGames) : null,
    pick = s.Pick ?? "", games = s.Games,
    description = includeDescription ? MultiModeParser.GetAugmentDescription(s.Id, s.Name, Bridge.AugmentDescriptions) : ""
   };
  }

  string situationalItemScope = "";
  object[] situationalItemCatalog = new object[0];

  object[] SituationalItemCatalog() {
   var items = MultiModeParser.DDragonItems;
   var scope = ActiveMode + ":" + MultiModeParser.DDragonVersion + ":" + items.Count + ":" +
    Bridge.Items.Count + ":" + (Selected == null ? "" : Selected.AssetKey);
   if (scope == situationalItemScope) return situationalItemCatalog;
   string champion = Selected == null ? "" : Selected.AssetKey;
   situationalItemCatalog = items.Where(pair => SituationalItems.Available(pair.Value, ActiveMode, champion))
    .Select(pair => new { id = pair.Key, item = pair.Value, categories = SituationalItems.Categories(pair.Value).ToArray() })
    .Where(entry => entry.categories.Length > 0)
    .OrderBy(entry => Json.A(entry.item, "to").Length > 0)
    .ThenBy(entry => ItemName(entry.id))
    .Select(entry => {
     Dictionary<string, object> localItem;
     Bridge.Items.TryGetValue(entry.id, out localItem);
     string description = Json.S(localItem, "description");
     if (description == "") description = Json.S(entry.item, "description");
     var copy = ItemCopy.Parse(description);
     return (object)new {
      id = entry.id, name = ItemName(entry.id), icon = MultiModeParser.ItemIconOf(entry.id),
      total = Json.I(entry.item, "priceTotal"), categories = entry.categories,
      stats = copy.Stats, effects = copy.Paragraphs,
      components = BuildPlanner.Components(entry.id, new int[0], items).Select(component => {
       Dictionary<string, object> definition;
       items.TryGetValue(component.Id, out definition);
       return new { id = component.Id, name = ItemName(component.Id), price = Json.I(definition, "priceTotal"), icon = MultiModeParser.ItemIconOf(component.Id) };
      }).ToArray()
     };
    }).ToArray();
   situationalItemScope = scope;
   return situationalItemCatalog;
  }

  object GuideItem(int id, string name, string icon, Dictionary<int, Dictionary<string, object>> items, int[] inventory) {
   Dictionary<string, object> definition;
   items.TryGetValue(id, out definition);
   int total = Json.I(definition, "priceTotal");
   return new {
    id = id, name = name, icon = icon, total = total,
    remainingGold = inventory == null || total <= 0 ? (int?)null : BuildPlanner.RemainingCost(id, inventory, items),
    components = BuildPlanner.Components(id, inventory ?? new int[0], items).Select(component => {
     Dictionary<string, object> child;
     items.TryGetValue(component.Id, out child);
     return new { id = component.Id, name = ItemName(component.Id), price = Json.I(child, "priceTotal"),
      icon = MultiModeParser.ItemIconOf(component.Id), owned = component.Owned };
    }).ToArray()
   };
  }

  public string BuildStateJson() {
   var path = CurrentBuildPath(true);
   var guideItemsMap = ActiveItemsMap();
   var localPlayer = Game.Live.Players.FirstOrDefault(player => player.Me && player.InventoryReported);
   var inventory = Game.InGame && !Preview && localPlayer != null ? localPlayer.Items.Where(id => id > 0).ToArray() : null;
   bool isArena = ActiveMode == "ARENA";
   double modeScale;
   if (!Config.ModePanelScale.TryGetValue(ActiveMode, out modeScale)) modeScale = Config.Layout.Scale;
   Dictionary<string, double> widgetScale;
   Config.ModeWidgetScale.TryGetValue(ActiveMode, out widgetScale);
   const string emptyBuild = "No build data available";
   string summary = path.Count == 0 ? emptyBuild : ModeData.RankedVariant != null ? ModeData.SourceLabel : Config.BuildFocus + " build guide";
   bool hasReference = Selected != null && ModeData.ModeKey == ActiveMode && ModeData.ChampionSlug == Selected.Slug;
   var rankedPair = hasReference ? ModeData.SummonerSpellIds.ToArray() : new int[0];
   var rankedSpells = rankedPair.Length == 2
    ? SpellPlan.OrderForFlash(rankedPair, Config.FlashKey, Game.Spell1Id, Game.Spell2Id)
    : rankedPair;
   var rankedBoot = ModeData.RankedVariant != null ? ModeData.Boots.FirstOrDefault() : null;
   int rankedBootStep = rankedBoot == null ? -1 : path.FindIndex(step => step.Stat.Id == rankedBoot.Id);
   var jungleStarter = ModeData.RankedVariant != null ? ModeData.Starters.FirstOrDefault(item => MultiModeParser.IsJungleStarter(item.Id)) : null;
   var catalog = IsAugmentMode && hasReference ? ModeData.Augments.OrderBy(x => x.Name).ToArray() : new Stat[0];

   bool inAugmentMatch = hasReference && IsAugmentMode && Game.InGame && Game.ModeKey == ActiveMode && ChampionMatches(Selected, Game.Champion);
   UpdateAugmentTiers(catalog, inAugmentMatch);
   bool showPreviewAugments = Preview && PreviewScene == "augments";
   bool showLiveAugments = inAugmentMatch && Config.Ocr && offerSession.Visible;
   var offeredList = showPreviewAugments
    ? catalog.OrderByDescending(stat => augmentTiers.ContainsKey(stat.Id)).Take(previewAugmentCount)
      .Select((stat, i) => new { slot = i, stat = SerializeAugment(stat, false), x = 0.0, y = 0.0 }).ToArray()
    : showLiveAugments
      ? offerSession.Positions.Select((pos, i) => {
        var stat = pos != null && pos.Stat.Id != offerSession.PendingRerollId
         ? catalog.FirstOrDefault(augment => augment.Id == pos.Stat.Id) : null;
        return new { slot = i, stat = stat != null ? SerializeAugment(stat, false) : null,
         x = pos != null ? pos.X : 0, y = pos != null ? pos.Y : 0 };
       }).ToArray()
      : null;

   object inspectedItemObj = null;
   if (inspectedItemId > 0) {
    Dictionary<string, object> d; ActiveItemsMap().TryGetValue(inspectedItemId, out d);
    var st = ModeData.Items.Concat(ModeData.PrismaticItems).Concat(ModeData.BestItems).Concat(ModeData.Boots).FirstOrDefault(x => x.Id == inspectedItemId);
    var copy = d != null ? ItemCopy.Parse(Json.S(d, "description")) : new ItemCopy();
    var comps = d != null ? Json.A(d, "from").Select(Json.ToInt).Where(x => x > 0).Select(cid => new { id = cid, name = ItemName(cid), price = Price(cid) }).ToArray() : new object[0];
    inspectedItemObj = new {
     id = inspectedItemId, name = d != null ? Json.S(d, "name") : (st != null ? st.Name : "Item " + inspectedItemId),
     priceTotal = Price(inspectedItemId), owned = Game.Owned.Contains(inspectedItemId), planned = plannedItems.Contains(inspectedItemId),
     stats = copy.Stats, paragraphs = copy.Paragraphs, components = comps,
     winRate = st != null && !isArena ? st.Rate : 0, pickRate = st != null ? st.Pick : ""
    };
   }

   object inspectedAugObj = null;
   if (inspectedAugmentId > 0) {
    var aug = catalog.FirstOrDefault(x => x.Id == inspectedAugmentId);
    if (aug != null) {
     inspectedAugObj = SerializeAugment(aug);
    }
   }

   Func<int, object> runeMetaObj = id => {
    RuneMeta rm;
    if (MultiModeParser.DDragonRunes.TryGetValue(id, out rm)) return new { id = rm.Id, name = rm.Name, icon = rm.Icon };
    return new { id = id, name = "Rune " + id, icon = "" };
   };
   string draftMode = Game.InSelection && !Game.InGame && !Preview && Game.ModeKey != "" ? Game.ModeKey : ActiveMode;
   var draftTiers = TierRowsFor(draftMode);
   string displayedTierRole = ActiveMode == "SR" ? MultiModeParser.NormalizeRole(Config.TierRole) : "";
   var playedTiers = Game.InGame && Game.ModeKey != draftMode ? TierRowsFor(Game.ModeKey) : draftTiers;
   var selMode = ChampionTierFor(TierRowsFor(ActiveMode), Selected, ActiveMode, MultiModeParser.NormalizeRole(ActiveRole));
   Func<string, string> patchTagOf = sl => {
    if (String.IsNullOrEmpty(sl) || MultiModeParser.PatchNotes == null) return "";
    Dictionary<string, string> modeTags;
    string pt;
    if (MultiModeParser.PatchNotes.ChampionTagByMode != null
        && MultiModeParser.PatchNotes.ChampionTagByMode.TryGetValue(ActiveMode ?? "SR", out modeTags)
        && modeTags.TryGetValue(sl, out pt)) return pt;
    return "";
   };
   Func<TierListEntry, object> tierRow = t => new {
    id = t.Id, slug = t.Slug, name = t.Name, icon = t.Icon, role = t.Role, tier = t.Tier, rank = t.Rank, rankDelta = t.RankDelta,
    patchTag = draftMode == ActiveMode ? patchTagOf(t.Slug) : "",
    winRate = t.WinRate, pickRate = t.PickRate, banRate = t.BanRate, kda = t.Kda, games = t.Games, counters = t.Counters.ToArray()
   };
   bool previewSelection = Preview && PreviewScene == "bench";
   bool previewDraft = previewSelection && (draftMode == "MAYHEM" || draftMode == "ARAM");
   var rawCards = previewDraft
    ? Champions.Take(3).Select((champion, index) => new ChampionCardHit { Champion = champion, X = .28 + .22 * index, Y = .54 }).ToList()
    : Game.InSelection && !Game.InGame ? champSelectCards : new List<ChampionCardHit>();
   var rawBench = (previewDraft ? Champions.Skip(3).Take(5) : Game.Bench.Select(FromId).Where(c => c != null))
    .GroupBy(c => c.Slug).Select(group => group.First()).ToList();
   var actualPick = previewSelection ? Selected : Champions.FirstOrDefault(champion => ChampionMatches(champion, Game.Champion));
   bool showDraftNotes = !PrivateMode && (previewSelection || (Game.InSelection && !Game.InGame));
   Func<Champion, string> draftNote = champion => {
    string note;
    if (!showDraftNotes || !Config.Notes.TryGetValue(champion.Slug, out note) || String.IsNullOrWhiteSpace(note)) return null;
    note = System.Text.RegularExpressions.Regex.Replace(note.Trim(), @"\s+", " ");
    if (note.Length <= 90) return note;
    return note.Substring(0, Char.IsHighSurrogate(note[89]) ? 89 : 90) + "…";
   };
   Func<Champion, object> draftChampion = champion => {
    var tier = ChampionTierFor(draftTiers, champion, draftMode, MultiModeParser.NormalizeRole(Game.AssignedRole));
    return new {
     name = champion.Name, slug = champion.Slug, tier = tier != null ? tier.Tier : "",
     icon = MultiModeParser.ChampIconOf(champion), note = draftNote(champion)
    };
   };
   var champCardsPayload = rawCards.Where(hit => hit != null && hit.Champion != null).Select(hit => {
    var tier = ChampionTierFor(draftTiers, hit.Champion, draftMode, "");
    return new { name = hit.Champion.Name, slug = hit.Champion.Slug, tier = tier != null ? tier.Tier : "", x = hit.X, y = hit.Y, note = draftNote(hit.Champion) };
   }).ToArray();
   var pn = MultiModeParser.PatchNotes ?? new PatchNotesBundle();

   var updateStatus = GitHubUpdater.GetStatus();
   var payload = new {
    version = MayloRelease.BuildInfo.Version,
    ddVersion = MultiModeParser.DDragonVersion,
    pinned = Topmost,
    bootReady = BootReady,
    bootProgress = BootProgress,
    bootStage = BootStage,
    view = View,
    activeMode = ActiveMode,
    detectedMode = DetectedMode,
    activeRole = ActiveRole,
    detectedRole = DetectedRole,
    availableRoles = ModeData.AvailableRoles.ToArray(),
    modeSource = ModeData.SourceLabel,
    rankedBuild = hasReference && ModeData.RankedVariant != null ? new {
     active = ModeData.RankedVariant.Key,
     variants = ModeData.RankedVariants.Select(variant => new { key = variant.Key, games = variant.Games }).ToArray(),
     options = ModeData.RankedVariant.Options.Select((items, i) => new {
      step = ModeData.RankedVariant.CoreCount + i + 1,
      items = items.Select(item => new { id = item.Id, name = item.Name, games = item.Games }).ToArray()
     }).ToArray()
    } : null,
    privateMode = PrivateMode,
    hubActive = HubActive,
    inGameSheet = InGameSheet,
    inGameSheetTab = InGameSheetTab,
    preview = Preview,
    previewScene = PreviewScene, previewAugmentCount = previewAugmentCount,
    editing = IsOverlayEditing,
    overlayVisible = OverlayVisible,
    champTierOpen = ChampTierOpen,
    draftCounters = draftMode == "SR" && (previewSelection || (Game.InSelection && !Game.InGame)) ? new {
     key = draftCounterKey, status = draftCounterStatus,
     patch = String.Join(".", MultiModeParser.DDragonVersion.Split('.').Take(2)),
     enemies = Game.DraftEnemies.Select(FromId).Where(c => c != null)
      .Select(c => new { slug = c.Slug, name = c.Name, icon = MultiModeParser.ChampIconOf(c) }).ToArray(),
     rows = draftCounters.Where(row => !Game.DraftUnavailable.Contains(row.Id)).Take(12).Select(row => {
      var champion = FromId(row.Id) ?? row.Champion;
      return new { slug = champion.Slug, name = champion.Name, icon = MultiModeParser.ChampIconOf(champion),
       games = row.Games, winRate = row.WinRate, goldAt15 = row.GoldAt15 };
     }).ToArray()
    } : null,
    augmentsVisible = showPreviewAugments || showLiveAugments,
    refreshingOffers = offerSession.Refreshing,
    connectionText = ConnectionText,
    statusText = StatusText,
    ocrStatus = OcrStatus,
    ocrLastMs = OcrLastMs,
    ocrLastLines = OcrLastLines.Take(10).ToArray(),
    spellStatus = Bridge.SpellStatus,
    runeStatus = Bridge.RuneStatus,
    shopStatus = Bridge.ShopStatus,
    importStatus = ImportStatus,
    account = Profile.Account == null ? null : new {
     riotId = Profile.Account.RiotId,
     level = Profile.Account.Level,
     region = Profile.Account.Region,
     status = Profile.Status,
     profileIconId = Profile.Account.ProfileIconId,
     profileIconUrl = Profile.Account.ProfileIconId > 0 ? ("https://ddragon.leagueoflegends.com/cdn/" + MultiModeParser.DDragonVersion + "/img/profileicon/" + Profile.Account.ProfileIconId + ".png") : ""
    },
    champions = Champions.Select(c => new { name = c.Name, slug = c.Slug, tier = c.Tier, win = c.Win, pick = c.Pick, icon = c.Icon, assetKey = c.AssetKey, patchTag = patchTagOf(c.Slug) }).ToArray(),
    tierList = TierList.Where(t => displayedTierRole == "" || MultiModeParser.NormalizeRole(t.Role) == displayedTierRole)
     .Take(320).Select(tierRow).ToArray(),
    champSelectMode = draftMode,
    champSelectTierList = (Game.InSelection && !Game.InGame || Preview && PreviewScene == "bench") && (draftTiers != TierList || draftMode == "SR")
     ? draftTiers.Select(tierRow).ToArray() : null,
    selected = Selected == null ? null : new { name = Selected.Name, slug = Selected.Slug,
     tier = selMode != null ? selMode.Tier : ActiveMode == "MAYHEM" ? Selected.Tier : "",
     win = selMode != null ? selMode.WinRate : ActiveMode == "MAYHEM" ? Selected.Win : 0,
     pick = selMode != null ? selMode.PickRate : ActiveMode == "MAYHEM" ? Selected.Pick : 0,
     icon = Selected.Icon, assetKey = Selected.AssetKey, patchTag = patchTagOf(Selected.Slug) },
    game = new {
     phase = Game.Phase, mode = Game.Mode, modeKey = Game.ModeKey, modeLabel = Game.ModeLabel, champion = Game.Champion, inGame = Game.InGame, inSelection = Game.InSelection, mayhem = Game.Mayhem, gold = Game.Gold,
     matchId = Game.MatchId, owned = Game.Owned.ToArray(), planned = plannedItems.ToArray(),
     bench = rawBench.Select(draftChampion).ToArray(),
     pickedChampion = actualPick != null ? draftChampion(actualPick) : null,
     champCards = champCardsPayload,
     benchPortraits = previewDraft
      ? rawBench.Select((c, i) => new { slug = c.Slug, x = .32 + .075 * i, y = .075, size = .045 }).ToArray()
      : (Game.InSelection && !Game.InGame ? portraitPositions : new List<PortraitHit>())
       .Where(hit => rawBench.Any(c => c.Slug == hit.Champion.Slug))
       .Select(hit => new { slug = hit.Champion.Slug, x = hit.X, y = hit.Y, size = hit.Size }).ToArray(),
     team = Game.Team.Select(FromId).Where(c => c != null).Select(c => new { name = c.Name, slug = c.Slug, tier = c.Tier, win = c.Win, pick = c.Pick, icon = MultiModeParser.ChampIconOf(c), assetKey = c.AssetKey }).ToArray(),
     live = new {
      clock = Game.Live.Clock, seconds = Game.Live.Seconds, health = Game.Live.Health, maxHealth = Game.Live.MaxHealth,
      ad = Game.Live.AttackDamage, ap = Game.Live.AbilityPower, armor = Game.Live.Armor, mr = Game.Live.MagicResist,
      attackSpeed = Game.Live.AttackSpeed, moveSpeed = Game.Live.MoveSpeed,
      activeLevel = Game.Live.ActiveLevel, q = Game.Live.AbilityQ, w = Game.Live.AbilityW, e = Game.Live.AbilityE, r = Game.Live.AbilityR,
      blueDragons = Game.Live.BlueDragons, redDragons = Game.Live.RedDragons, lastDragonType = Game.Live.LastDragonType,
      events = Game.Live.Events.ToArray(),
      inventoryValue = (object)null,
      inhibitors = new object[0],
      objectives = Game.Live.Objectives.Select(o => new { key = o.Key, name = o.Name, status = o.Status, alive = o.Alive, secondsLeft = o.SecondsLeft }).ToArray(),
      players = Game.Live.Players.Select(p => {
       var pc = Champions.FirstOrDefault(c => GameBridge.Norm(c.AssetKey) == GameBridge.Norm(p.Champion) || GameBridge.Norm(c.Name) == GameBridge.Norm(p.Champion));
       string pRole = MultiModeParser.NormalizeRole(p.Position);
       var tier = p.Me ? ChampionTierFor(playedTiers, pc, Game.ModeKey,
        pRole == "" ? MultiModeParser.NormalizeRole(Game.AssignedRole) : pRole) : null;
       return new {
        key = p.Team + "|" + p.Name + "|" + p.Champion, name = p.Name, champion = p.Champion,
        championName = pc != null ? pc.Name : p.Champion, championIcon = pc != null ? MultiModeParser.ChampIconOf(pc) : "",
        championTier = tier != null ? tier.Tier : "",
        team = p.Team, position = pRole, level = p.Level, kills = p.Kills, deaths = p.Deaths, assists = p.Assists, cs = p.Cs,
        dead = p.Dead, respawn = p.Respawn, me = p.Me, items = p.Items.ToArray(),
        spell1 = p.Spell1, spell2 = p.Spell2, spellKey1 = p.SpellKey1, spellKey2 = p.SpellKey2,
        keystone = p.Keystone, runeTree = p.RuneTree, tags = new string[0], isLaneOpponent = false, matchupWr = 0
       };
      }).ToArray()
     }
    },
    buildSummary = summary,
    rankedLoadout = hasReference && ModeData.RankedVariant != null ? new {
     spells = rankedSpells.Select((id, index) => new { id, name = MultiModeParser.SummonerName(id),
      iconKey = MultiModeParser.SummonerIconKey(id), slot = index == 0 ? "D" : "F" }).ToArray(),
     boot = rankedBoot != null ? new { id = rankedBoot.Id, name = rankedBoot.Name, step = rankedBootStep + 1,
      matched = ModeData.RankedVariant.BootFromMatchingPath } : null,
     pet = jungleStarter != null ? new { id = jungleStarter.Id, name = jungleStarter.Name } : null
    } : null,
    enemyThreats = new string[0],
    situationalItems = InGameSheet && InGameSheetTab == "situational" ? SituationalItemCatalog() : null,
    referenceItems = (hasReference ? ModeData.Items.Concat(ModeData.Boots).Concat(ModeData.Starters).Concat(ModeData.BestItems) : Enumerable.Empty<Stat>())
     .Where(item => item.Id > 0).GroupBy(item => item.Id).Select(group => group.First())
     .Select(item => GuideItem(item.Id, item.Name, item.Icon ?? "", guideItemsMap, InGameSheet ? inventory : null)).ToArray(),
    ownedBuildItems = inventory == null ? new object[0] : inventory.Distinct().Where(id => {
     Dictionary<string, object> item;
     if (!guideItemsMap.TryGetValue(id, out item)) return false;
     var categories = Json.A(item, "categories").Select(Convert.ToString);
     return !MultiModeParser.StarterIds.Contains(BuildPlanner.Canonical(id)) &&
      Json.I(item, "priceTotal") >= 1000 && !categories.Any(category => category == "Consumable" || category == "Trinket") &&
      Json.A(item, "to").Length == 0 && !ItemName(id).StartsWith("Item ", StringComparison.Ordinal);
    }).Select(id => GuideItem(id, ItemName(id), MultiModeParser.ItemIconOf(id), guideItemsMap, InGameSheet ? inventory : null)).ToArray(),
    buildPath = path.Select((step, idx) => {
     int total = Price(step.Stat.Id);
     var comps = BuildPlanner.Components(step.Stat.Id, new int[0], ActiveItemsMap())
      .Select(cs => new { id = cs.Id, name = ItemName(cs.Id), owned = false, price = Price(cs.Id), icon = MultiModeParser.ItemIconOf(cs.Id) }).ToArray();
     string role = step.Role == "STARTER" ? "STARTER" : step.IsBoots ? "BOOTS" : "BUILD ITEM";
     return new { stat = SerializeStat(step.Stat, isArena), role = role, reason = step.Reason, shortTip = step.ShortTip,
      synergyTag = "", synergyTone = "", pickRate = step.PickRate, isBoots = step.IsBoots,
      totalGold = total, remainingGold = inventory != null && total > 0 ? BuildPlanner.RemainingCost(step.Stat.Id, inventory, guideItemsMap) : total, affordable = false, components = comps };
    }).ToArray(),
    starters = ModeData.Starters.Select(s => SerializeStat(s, isArena)).ToArray(),
    prismaticItems = ModeData.PrismaticItems.Select(s => SerializeStat(s, true)).ToArray(),
    boots = ModeData.Boots.Select(s => SerializeStat(s, isArena)).ToArray(),
    spells = ModeData.Spells.Select(s => SerializeStat(s)).ToArray(),
    bestItems = ModeData.BestItems.Where(s => !ModeData.Items.Take(6).Any(i => i.Name == s.Name)).Take(4).Select(s => SerializeStat(s, isArena)).ToArray(),
    teammates = ModeData.Teammates.Select(s => SerializeStat(s)).ToArray(),
    matchups = ModeData.Matchups.Select(s => SerializeStat(s)).ToArray(),
    gameLengths = ModeData.GameLengths.Select(g => new { minutes = g.Minutes, winRate = g.WinRate }).ToArray(),
    patchNotes = new {
     version = pn.PatchVersion,
     officialUrl = pn.OfficialUrl,
     entries = pn.Entries.Select(e => new {
      id = e.Id, category = e.Category, mode = e.Mode, slug = e.Slug, title = e.Title, icon = e.Icon,
      changeType = e.ChangeType, summary = e.Summary, playerNote = e.PlayerNote,
      details = e.Details.Select(dl => new { text = dl.Text, oldValue = dl.OldValue, newValue = dl.NewValue, type = dl.Type }).ToArray()
     }).ToArray()
    },
    runes = ModeData.Runes == null ? null : new {
     primaryStyle = ModeData.Runes.PrimaryStyle, secondaryStyle = ModeData.Runes.SecondaryStyle,
     primaryMeta = runeMetaObj(ModeData.Runes.PrimaryStyle), secondaryMeta = runeMetaObj(ModeData.Runes.SecondaryStyle),
     perks = ModeData.Runes.Perks.ToArray(),
     perkDetails = ModeData.Runes.Perks.Select(id => runeMetaObj(id)).ToArray(),
     name = ModeData.Runes.Name, source = ModeData.Runes.Source
    },
    runeAlternatives = ModeData.Runes == null ? new object[0] : ModeData.Runes.Perks.Select(id => {
     RuneMeta selectedRune;
     if (!MultiModeParser.DDragonRunes.TryGetValue(id, out selectedRune) || selectedRune.Style == 0) return new object[0];
     return MultiModeParser.DDragonRunes.Values.Where(rune => rune.Style == selectedRune.Style &&
      rune.Slot == selectedRune.Slot).OrderBy(rune => rune.Name).Select(rune => runeMetaObj(rune.Id)).ToArray();
    }).Cast<object>().ToArray(),
    skills = ModeData.Skills == null ? null : new { priority = ModeData.Skills.Priority.ToArray(), levels = ModeData.Skills.Levels.ToArray(), source = ModeData.Skills.Source },
    offered = offeredList != null ? offeredList.Cast<object>().ToArray() : new object[0],
    allAugments = catalog.Select(stat => SerializeAugment(stat)).ToArray(),
    portraitPositions = portraitPositions.Select(p => new { champion = new { name = p.Champion.Name, slug = p.Champion.Slug, tier = p.Champion.Tier, win = p.Champion.Win, pick = p.Champion.Pick, icon = p.Champion.Icon, assetKey = p.Champion.AssetKey }, x = p.X, y = p.Y }).ToArray(),
    collection = View == "Collection" && Profile.Account != null && collectionAccount == Profile.Account.Key
     ? collection.Select(skin => new { id = skin.Id, name = skin.Name, champion = skin.Champion, tile = skin.Tile, owned = skin.Owned }).Cast<object>().ToArray()
     : new object[0],
    collectionBusy = collectionBusy,
    collectionFailed = collectionFailed,
    rankHistory = Profile.RankHistory.Take(30).Select(rank => new { created = rank.Created, tier = rank.Tier, division = rank.Division, lp = rank.Lp }).ToArray(),
    history = Profile.History.Take(60).Select(m => {
     var mc = FromId(m.ChampionId);
     var augDet = (m.Augments ?? new List<int>()).Where(id => id > 0).Select(id => {
      string an; Bridge.AugmentNames.TryGetValue(id, out an);
      var st = ModeData.Augments.FirstOrDefault(x => x.Id == id);
      return new { id = id, name = !String.IsNullOrEmpty(an) ? an : (st != null ? st.Name : ("Augment " + id)), icon = st != null && !String.IsNullOrEmpty(st.Icon) ? st.Icon : ("/icons/augments/" + id + ".png") };
     }).ToArray();
     string matchKey = MatchReviewKey(m.Id);
     string matchNote, clipPath;
     Config.MatchNotes.TryGetValue(matchKey, out matchNote);
     Config.MatchClips.TryGetValue(matchKey, out clipPath);
     List<MatchMoment> moments;
     moments = null;
     if (viewedMatchKey == matchKey) Config.MatchTimelines.TryGetValue(matchKey, out moments);
     return new { id = m.Id, championId = m.ChampionId, championName = mc != null ? mc.Name : ("Champion " + m.ChampionId), championIcon = mc != null ? MultiModeParser.ChampIconOf(mc) : "", queue = m.Queue, queueName = m.QueueName, mode = m.Mode, modeKey = GameBridge.DetectModeKey(m.Mode, m.Queue, m.Mayhem), mayhem = m.Mayhem, kills = m.Kills, deaths = m.Deaths, assists = m.Assists, won = m.Won, remake = m.Remake, duration = m.Duration, time = m.Time, dateText = m.Date.ToString("dd MMM HH:mm"), damage = m.Damage, gold = m.Gold, cs = m.Cs, items = (m.Items ?? new List<int>()).ToArray(), augments = (m.Augments ?? new List<int>()).ToArray(), augmentDetails = augDet,
      runes = (m.Runes ?? new List<int>()).Select(id => runeMetaObj(id)).ToArray(), created = m.Created,
      note = matchNote ?? "", clipName = String.IsNullOrEmpty(clipPath) ? "" : Path.GetFileName(clipPath),
      timeline = viewedMatchKey != matchKey ? null : (moments ?? new List<MatchMoment>()).Select(moment => new {
       seconds = moment.Seconds, kind = moment.Kind, detail = moment.Detail ?? "",
       itemName = moment.Item > 0 ? ItemName(moment.Item) : ""
      }).ToArray() };
    }).ToArray(),
    note = Selected != null && Config.Notes.ContainsKey(Selected.Slug) ? Config.Notes[Selected.Slug] : "",
    hotkeys = hotkeyRegistered,
    settings = new {
     language = Config.Language == "fr" || Config.Language == "en" ? Config.Language :
      (System.Globalization.CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "fr" ? "fr" : "en"),
     connected = Config.Connected, overlay = Config.Overlay, showBuildOverlay = ModePanelVisible("build", Config.ShowBuildOverlay), showRerollBar = Config.ShowRerollBar,
     showSkillAdvisor = Config.ShowSkillAdvisor, showPerfStrip = ModePanelVisible("performance", Config.ShowPerfStrip), showJungleWidget = ModePanelVisible("minimap", Config.ShowJungleWidget),
     benchmarkMetrics = Config.BenchmarkMetrics.ToArray(),
     showBenchBar = ModePanelVisible("bench", Config.ShowBenchBar), minimalHud = Config.MinimalHud,
     ocr = Config.Ocr, ocrAllModes = Config.OcrAllModes, autoLayout = Config.AutoLayout, autoPage = Config.AutoPage,
     autoSpells = Config.AutoSpells, autoRunes = Config.AutoRunes, autoExportShop = Config.AutoExportShop,
     launchWithWindows = Config.LaunchWithWindows, minimizeToTray = Config.MinimizeToTray,
     hotkeySheet = Config.HotkeySheet, hotkeyOverlay = Config.HotkeyOverlay, hotkeyEdit = Config.HotkeyEdit, hotkeyScan = Config.HotkeyScan,
     flashKey = Config.FlashKey, tierElo = Config.TierElo, tierRole = Config.TierRole,
     buildFocus = Config.BuildFocus, theme = Config.Theme, scale = modeScale, widgetScale = widgetScale ?? new Dictionary<string, double>(), badgeOffsetY = Config.BadgeOffsetY,
     minimapScale = Config.MinimapScale, minimapLeft = Config.MinimapLeft, hudScale = Config.HudScale,
     onboardingSeen = Config.OnboardingSeen,
     panels = PanelsForMode().ToDictionary(kv => kv.Key, kv => new { x = kv.Value.X, y = kv.Value.Y })
    },
    inspectedItem = inspectedItemObj,
    inspectedAugment = inspectedAugObj,
    update = new {
     state = updateStatus.State,
     version = updateStatus.Version,
     message = updateStatus.CanRestart && (Game.InGame || Game.InSelection)
      ? "Update ready. You can restart after leaving the game." : updateStatus.Message,
     canRestart = updateStatus.CanRestart && !Game.InGame && !Game.InSelection
    }
   };
   return Json.Serializer().Serialize(payload);
  }

  public void PushState() {
   string json = BuildStateJson();
   if (webReady && web.CoreWebView2 != null) { try { web.CoreWebView2.PostWebMessageAsJson(json); } catch { } }
   if (overlay != null) overlay.PostState(json);
  }

  public async void HandleWebMessage(string rawJson) {
   try {
    var msg = Json.Serializer().Deserialize<Dictionary<string, object>>(rawJson);
    if (msg == null) return;
    string type = Json.S(msg, "type");
    switch (type) {
     case "ready":
      if (Json.S(msg, "surface") != "overlay") RevealWebSurface();
      PushState();
      break;
     case "dragWindow":
      if (handle != IntPtr.Zero) {
       if (WindowState == WindowState.Maximized) WindowState = WindowState.Normal;
       Native.ReleaseCapture();
       Native.SendMessage(handle, 0x00A1, (IntPtr)2, IntPtr.Zero);
      }
      break;
     case "togglePin":
      Topmost = !Topmost;
      if (Topmost && handle != IntPtr.Zero) Native.ForceForeground(handle);
      PushState();
      break;
     case "minimizeWindow": WindowState = WindowState.Minimized; break;
     case "maximizeWindow": if (!HubActive) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; break;
     case "closeWindow": if (HubActive) DismissHub(true); else Close(); break;
     case "setView": View = Json.S(msg, "view"); PushState(); break;
     case "toggleHub": HotkeyDestination(Json.S(msg, "view") == "" ? "Build" : Json.S(msg, "view"), true); break;
     case "closeInGameSheet": CloseInGameSheet(true); break;
     case "setInGameSheetTab": InGameSheetTab = Json.S(msg, "tab"); PushState(); break;
     case "setDragging":
      OverlayDragging = msg.ContainsKey("value") && Convert.ToBoolean(msg["value"]);
      if (overlay != null) overlay.Sync();
      break;
     case "togglePrivate": PrivateMode = !PrivateMode; PushState(); break;
     case "setChampTierOpen":
      ChampTierOpen = msg.ContainsKey("value") && Convert.ToBoolean(msg["value"]);
      if (overlay != null) overlay.Sync();
      PushState();
      break;
     case "setMode":
      string m = Json.S(msg, "mode");
      if (m == "MAYHEM" || m == "ARENA" || m == "ARAM" || m == "SR") {
       ActiveMode = m; manualModeOverride = true; EnforceModeView();
       List<TierListEntry> cachedTl;
       TierList = tierListByMode.TryGetValue(m, out cachedTl) && cachedTl != null ? cachedTl : new List<TierListEntry>();
       var _tl = RefreshTierList(false);
       if (Selected != null) await SelectChampion(Selected, false); else PushState();
      }
      break;
     case "setRole":
      string nr = MultiModeParser.NormalizeRole(Json.S(msg, "role"));
      if (nr != "") { ActiveRole = nr; manualRoleOverride = true; if (Selected != null) await SelectChampion(Selected, false, nr); else PushState(); }
      break;
     case "setTierFilter":
      string tr = Json.S(msg, "role"); string te = Json.S(msg, "elo");
      if (tr != "") Config.TierRole = tr.ToUpperInvariant();
      if (te != "") Config.TierElo = te.ToLowerInvariant();
      Save();
      await RefreshTierList(false);
      if (te != "" && ActiveMode == "SR" && Selected != null) await SelectChampion(Selected, false, ActiveRole);
      break;
     case "setFlashKey":
      string fk = Json.S(msg, "key").ToUpperInvariant();
      if (fk == "D" || fk == "F") { Config.FlashKey = fk; Save(); Bridge.ResetSpellSelection(); PushState(); await SyncLoadout(); }
      break;
     case "openDraftBuild":
      if (!Game.InSelection || Game.InGame || Game.ModeKey != ActiveMode) break;
      var draftPick = Champions.FirstOrDefault(c => c.Slug == Json.S(msg, "slug") && ChampionMatches(c, Game.Champion));
      if (draftPick == null) break;
      View = "Build";
      var draftRole = ActiveMode == "SR" && !manualRoleOverride ? MultiModeParser.NormalizeRole(Game.AssignedRole) : null;
      if (Selected != null && Selected.Slug == draftPick.Slug && (String.IsNullOrEmpty(draftRole) || ActiveRole == draftRole))
       PushState();
      else
       await SelectChampion(draftPick, false, !String.IsNullOrEmpty(draftRole) ? draftRole : null);
      break;
     case "loadDraftCounters": {
      if (!((Game.InSelection && !Game.InGame && Game.ModeKey == "SR") ||
          (Preview && PreviewScene == "bench" && ActiveMode == "SR"))) break;
      var opponent = Champions.FirstOrDefault(c => c.Slug == Json.S(msg, "slug"));
      int opponentId = opponent == null ? 0 : ChampionIdOf(opponent);
      string lane = MultiModeParser.NormalizeRole(Json.S(msg, "role"));
      if (opponentId <= 0 || lane == "") break;
      string elo = Config.TierElo;
      string key = opponent.Slug + ":" + lane + ":" + elo;
      if (key == draftCounterKey && draftCounterStatus != "error") break;
      int request = ++draftCounterRequest;
      draftCounterKey = key;
      draftCounterStatus = "loading";
      draftCounters.Clear();
      PushState();
      try {
       var result = await MultiModeParser.LoadDraftCounters(opponentId, lane, elo);
       if (request != draftCounterRequest || closing) break;
       draftCounters = result;
       draftCounterStatus = "ready";
      } catch (Exception ex) {
       var webException = ex as System.Net.WebException;
       if (webException != null && webException.Response != null) webException.Response.Dispose();
       if (request != draftCounterRequest || closing) break;
       draftCounterStatus = "error";
      }
      PushState();
      break;
     }
     case "browseDraftBuild":
      if (Game.InGame || (!Game.InSelection && (!Preview || PreviewScene != "bench"))) break;
      string browseMode = Preview ? ActiveMode : Game.ModeKey;
      var browsePick = Champions.FirstOrDefault(c => c.Slug == Json.S(msg, "slug"));
      if (browseMode == "" || browsePick == null) break;
      UseMatchMode(browseMode);
      ChampTierOpen = false;
      if (Preview) { Preview = false; PreviewScene = ""; Editing = false; }
      if (!HubActive || View != "Build") ToggleHub("Build");
      View = "Build";
      var browseRole = ActiveMode == "SR" ? MultiModeParser.NormalizeRole(Json.S(msg, "role")) : "";
      await SelectChampion(browsePick, false, browseRole == "" ? null : browseRole);
      break;
     case "useLiveBuild":
      if (!Game.InGame || Game.ModeKey == "") break;
      var livePick = Champions.FirstOrDefault(c => ChampionMatches(c, Game.Champion));
      if (livePick == null) break;
      UseMatchMode(Game.ModeKey);
      await SelectChampion(livePick);
      break;
     case "selectChampion":
      string slug = Json.S(msg, "slug"); string nextView = Json.S(msg, "view"); string pickRole = Json.S(msg, "role");
      var ch = Champions.FirstOrDefault(x => x.Slug == slug || GameBridge.Norm(x.Name) == GameBridge.Norm(slug));
      if (ch != null) {
       if (nextView != "") View = nextView;
       await SelectChampion(ch, false, pickRole != "" ? pickRole : null);
      }
      break;
     case "setBuildFocus": Config.BuildFocus = Json.S(msg, "focus"); Save(); PushState(); break;
     case "setRankedBuildVariant": {
      if (ActiveMode != "SR" || Selected == null || ModeData.ChampionSlug != Selected.Slug || ModeData.ActiveRole != ActiveRole ||
          ModeData.ActiveElo != Config.TierElo || Json.S(msg, "champion") != Selected.Slug ||
          Json.S(msg, "role") != ActiveRole || Json.S(msg, "elo") != Config.TierElo) break;
      var variant = ModeData.RankedVariants.FirstOrDefault(build => build.Key == Json.S(msg, "variant"));
      if (variant == null || variant == ModeData.RankedVariant) break;
      variant.Apply(ModeData);
      Config.RankedBuildVariants[RankedVariantScope(ModeData)] = variant.Key;
      ImportStatus = "";
      Save(); PushState();
      await SyncLoadout();
      break;
     }
     case "inspectItem": inspectedAugmentId = -1; inspectedItemId = Json.I(msg, "id"); PushState(); break;
     case "closeItem": inspectedItemId = -1; PushState(); break;
     case "inspectAugment": inspectedItemId = -1; inspectedAugmentId = Json.I(msg, "id"); PushState(); break;
     case "closeAugment": inspectedAugmentId = -1; PushState(); break;
     case "togglePlanItem":
      int pid = Json.I(msg, "id"); if (!plannedItems.Add(pid)) plannedItems.Remove(pid); PushState(); break;
     case "clearPlan": plannedItems.Clear(); PushState(); break;
     case "refreshProfile": await RefreshProfile(true); break;
     case "refreshDatabases":
      situationalItemScope = "";
      StatusText = "Syncing Riot Data Dragon, CommunityDragon & OP.GG…"; PushState();
      await MultiModeParser.EnsureDDragon(Champions, true);
      await LoadHome(true);
      await RefreshTierList(true);
      await MultiModeParser.LoadPatchNotes(true);
      StatusText = "Databases synced · Patch " + MultiModeParser.DDragonVersion;
      PushState();
      break;
     case "saveNote":
      string nSlug = Json.S(msg, "slug"), nText = Json.S(msg, "text");
      if (nSlug != "") { Config.Notes[nSlug] = nText; Save(); StatusText = "Note saved."; PushState(); }
      break;
     case "savePanelPos":
      string pKey = Json.S(msg, "key");
      double px = Convert.ToDouble(msg["x"], System.Globalization.CultureInfo.InvariantCulture);
      double py = Convert.ToDouble(msg["y"], System.Globalization.CultureInfo.InvariantCulture);
      if (pKey == "" || Double.IsNaN(px) || Double.IsNaN(py)) break;
      px = Math.Max(0, Math.Min(1, px));
      py = Math.Max(0, Math.Min(1, py));
      PanelsForMode()[pKey] = new OverlayPanelPosition { X = px, Y = py }; Save(); PushState(); break;
     case "resetPositions": PanelsForMode().Clear(); Config.ModePanelScale[ActiveMode] = 1.0; Config.ModeWidgetScale.Remove(ActiveMode); Save(); StatusText = "Mode overlay layout reset."; PushState(); break;
     case "setWidgetScale":
      {
       string widget = Json.S(msg, "panel");
       double size;
       if (!new[] { "build_hud", "perf_strip_center" }.Contains(widget) ||
           !Double.TryParse(Json.S(msg, "scale"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out size) ||
           Double.IsNaN(size) || Double.IsInfinity(size)) break;
       Dictionary<string, double> map;
       if (!Config.ModeWidgetScale.TryGetValue(ActiveMode, out map) || map == null)
        Config.ModeWidgetScale[ActiveMode] = map = new Dictionary<string, double>();
       map[widget] = Math.Max(0.75, Math.Min(1.35, size));
       Save(); PushState(); break;
      }
     case "setBenchmarkMetrics":
      var chosenMetrics = new HashSet<string>(Json.A(msg, "metrics").OfType<string>());
      var validMetrics = BenchmarkMetricKeys.Where(chosenMetrics.Contains).ToList();
      if (validMetrics.Count > 0) { Config.BenchmarkMetrics = validMetrics; Save(); PushState(); }
      break;
     case "useMinimalOverlay":
      SetModePanelVisible("build", true);
      SetModePanelVisible("performance", false);
      SetModePanelVisible("minimap", false);
      SetModePanelVisible("bench", false);
      Save(); PushState(); break;
     case "syncLeagueMinimap": SyncLeagueMinimapConfig(); PushState(); break;
     case "togglePreview":
      bool pv = msg.ContainsKey("value") && Convert.ToBoolean(msg["value"]);
      if (pv && Selected == null && Champions.Count > 0) {
       var sample = Champions.FirstOrDefault(x => x.Slug == "briar") ?? Champions.FirstOrDefault(x => x.Slug == "gwen") ?? Champions[0];
       await SelectChampion(sample, false);
      }
      Preview = pv; Editing = pv; PreviewScene = pv ? (PreviewScene == "" ? "sr_hud" : PreviewScene) : "";
      if (!pv) InGameSheet = false;
      OverlayVisible = true; if (overlay != null) overlay.Sync(); PushState(); break;
     case "finishEdit":
      Editing = false; Preview = false; PreviewScene = ""; InGameSheet = false; OverlayDragging = false;
      if (tabHeld) { tabHeld = false; tabSuppressed = true; }
      OverlayVisible = true; Save(); if (overlay != null) overlay.Sync(); PushState(); break;
     case "setPreviewScene":
      string psc = Json.S(msg, "scene");
      if (psc != "" && psc != "off" && Selected == null && Champions.Count > 0) {
       var sample = Champions.FirstOrDefault(x => x.Slug == "briar") ?? Champions.FirstOrDefault(x => x.Slug == "gwen") ?? Champions[0];
       await SelectChampion(sample, false);
      }
      if (psc == "" || psc == "off") { Editing = false; Preview = false; PreviewScene = ""; InGameSheet = false; OverlayDragging = false; if (tabHeld) { tabHeld = false; tabSuppressed = true; } Save(); }
      else if (psc == "altg") { Preview = true; Editing = false; PreviewScene = "altg"; InGameSheet = true; InGameSheetTab = "build"; }
      else { Preview = true; Editing = true; PreviewScene = psc; InGameSheet = false; }
      if (psc == "augments") {
       previewAugmentCount = Math.Max(3, Math.Min(OfferSession.MaxSlots, Json.I(msg, "count")));
       EnsureAugmentCatalog();
      }
      OverlayVisible = true; if (overlay != null) overlay.Sync(); PushState(); break;
     case "exportConfig":
      try {
       var sfd = new Microsoft.Win32.SaveFileDialog { Title = "Export maylo settings", Filter = "maylo config (*.json)|*.json", FileName = "maylo-settings.json" };
       if (sfd.ShowDialog(this) == true) {
        var serializer = Json.Serializer();
        var exported = serializer.Deserialize<Dictionary<string, object>>(serializer.Serialize(Config));
        exported.Remove("MatchNotes"); exported.Remove("MatchClips"); exported.Remove("MatchTimelines");
        File.WriteAllText(sfd.FileName, serializer.Serialize(exported), Encoding.UTF8);
        StatusText = "Config exported to " + Path.GetFileName(sfd.FileName); PushState();
       }
      } catch (Exception ex) { StatusText = "Export failed: " + ex.Message; PushState(); }
      break;
     case "importConfig":
      try {
       var ofd = new Microsoft.Win32.OpenFileDialog { Title = "Import maylo settings", Filter = "maylo config (*.json)|*.json" };
       if (ofd.ShowDialog(this) == true) {
        var loaded = Json.Serializer().Deserialize<V2Settings>(File.ReadAllText(ofd.FileName, Encoding.UTF8));
        if (loaded != null) {
         loaded.MatchNotes = Config.MatchNotes;
         loaded.MatchClips = Config.MatchClips;
         loaded.MatchTimelines = Config.MatchTimelines;
         Config = loaded;
         SanitizeConfig();
         Save(); RegisterAppHotkeys(); SetStartupRegistry(Config.LaunchWithWindows);
         ApplyWindowTheme(!String.Equals(Config.Theme, "light", StringComparison.OrdinalIgnoreCase));
         StatusText = "Config imported from " + Path.GetFileName(ofd.FileName);
         if (overlay != null) overlay.Sync();
         PushState();
        }
       }
      } catch (Exception ex) { StatusText = "Import failed: " + ex.Message; PushState(); }
      break;
     case "copyText":
      string clip = Json.S(msg, "text");
      if (clip != "") { try { Clipboard.SetText(clip); StatusText = "Copied: " + clip; PushState(); } catch { } }
      break;
     case "setHotkey":
      string hkField = Json.S(msg, "field"), hkKey = Json.S(msg, "key").Trim().ToUpperInvariant();
      if (hkKey.Length == 1 && hkKey[0] >= 'A' && hkKey[0] <= 'Z') {
       if (hkKey == "H" || hkKey == "L") { StatusText = "Alt+" + hkKey + " is reserved by maylo."; PushState(); break; }
       var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
       if (hkField != "sheet") used.Add(Config.HotkeySheet);
       if (hkField != "overlay") used.Add(Config.HotkeyOverlay);
       if (hkField != "edit") used.Add(Config.HotkeyEdit);
       if (hkField != "scan") used.Add(Config.HotkeyScan);
       if (used.Contains(hkKey)) { StatusText = "Alt+" + hkKey + " is already assigned to another shortcut."; PushState(); break; }
       if (hkField == "sheet") Config.HotkeySheet = hkKey;
       else if (hkField == "overlay") Config.HotkeyOverlay = hkKey;
       else if (hkField == "edit") Config.HotkeyEdit = hkKey;
       else if (hkField == "scan") Config.HotkeyScan = hkKey;
       if (tray != null) tray.Text = "maylo · Alt+" + Config.HotkeySheet + " / F9 in game";
       Save(); RegisterAppHotkeys(); PushState();
      }
      break;
     case "updateSetting":
      string sk = Json.S(msg, "key"); object sv = msg.ContainsKey("value") ? msg["value"] : null;
      bool resyncLoadout = false;
      if (sk == "connected") Config.Connected = Convert.ToBoolean(sv);
      else if (sk == "overlay") Config.Overlay = Convert.ToBoolean(sv);
      else if (sk == "showBuildOverlay") SetModePanelVisible("build", Convert.ToBoolean(sv));
      else if (sk == "language") Config.Language = Convert.ToString(sv) == "fr" ? "fr" : "en";
      else if (sk == "showRerollBar") Config.ShowRerollBar = Convert.ToBoolean(sv);
      else if (sk == "showSkillAdvisor") Config.ShowSkillAdvisor = Convert.ToBoolean(sv);
      else if (sk == "showPerfStrip") SetModePanelVisible("performance", Convert.ToBoolean(sv));
      else if (sk == "showJungleWidget") SetModePanelVisible("minimap", Convert.ToBoolean(sv));
      else if (sk == "showBenchBar") SetModePanelVisible("bench", Convert.ToBoolean(sv));
      else if (sk == "minimalHud") Config.MinimalHud = Convert.ToBoolean(sv);
      else if (sk == "launchWithWindows") { Config.LaunchWithWindows = Convert.ToBoolean(sv); SetStartupRegistry(Config.LaunchWithWindows); }
      else if (sk == "minimizeToTray") Config.MinimizeToTray = Convert.ToBoolean(sv);
      else if (sk == "ocr") { Config.Ocr = Convert.ToBoolean(sv); if (!Config.Ocr) ResetOfferSession(); }
      else if (sk == "ocrAllModes") Config.OcrAllModes = Convert.ToBoolean(sv);
      else if (sk == "autoSpells") { Config.AutoSpells = Convert.ToBoolean(sv); if (Config.AutoSpells) { Bridge.ResetSpellSelection(); resyncLoadout = true; } }
      else if (sk == "autoRunes") { Config.AutoRunes = Convert.ToBoolean(sv); if (Config.AutoRunes) { Bridge.ResetSpellSelection(); resyncLoadout = true; } }
      else if (sk == "autoExportShop") { Config.AutoExportShop = Convert.ToBoolean(sv); if (Config.AutoExportShop) { Bridge.ResetSpellSelection(); resyncLoadout = true; } }
      else if (sk == "autoPage") Config.AutoPage = Convert.ToBoolean(sv);
      else if (sk == "flashKey") { Config.FlashKey = Convert.ToString(sv).ToUpperInvariant() == "D" ? "D" : "F"; Bridge.ResetSpellSelection(); resyncLoadout = true; }
      else if (sk == "theme") { Config.Theme = Convert.ToString(sv) == "light" ? "light" : "dark"; ApplyWindowTheme(!String.Equals(Config.Theme, "light", StringComparison.OrdinalIgnoreCase)); }
      else if (sk == "scale" && sv != null) Config.ModePanelScale[ActiveMode] = Math.Max(0.75, Math.Min(1.35, Convert.ToDouble(sv, System.Globalization.CultureInfo.InvariantCulture)));
      else if (sk == "badgeOffsetY" && sv != null) Config.BadgeOffsetY = Math.Max(-0.15, Math.Min(0.15, Convert.ToDouble(sv, System.Globalization.CultureInfo.InvariantCulture)));
      else if (sk == "minimapScale" && sv != null) Config.MinimapScale = Math.Max(0.55, Math.Min(1.85, Convert.ToDouble(sv, System.Globalization.CultureInfo.InvariantCulture)));
      else if (sk == "hudScale" && sv != null) Config.HudScale = Math.Max(0.6, Math.Min(1.5, Convert.ToDouble(sv, System.Globalization.CultureInfo.InvariantCulture)));
      else if (sk == "minimapLeft") { Config.MinimapLeft = Convert.ToBoolean(sv); PanelsForMode().Remove("minimap_overlay"); }
      else if (sk == "onboardingSeen") Config.OnboardingSeen = Convert.ToBoolean(sv);
      Save(); if (overlay != null) overlay.Sync(); PushState();
      if (resyncLoadout) await SyncLoadout();
      break;
     case "checkUpdates":
      await GitHubUpdater.CheckNowAsync();
      PushState();
      break;
     case "applyUpdateRestart":
      var upRoot = MayloRelease.InstallStore.InstalledRoot();
      if (upRoot != null && !Game.InGame && !Game.InSelection && GitHubUpdater.GetStatus().CanRestart) {
       Program.PendingRestartLauncher = Path.Combine(upRoot, MayloRelease.InstallStore.Launcher);
       realQuit = true;
       Close();
      }
      break;
     case "loadCollection":
      if (collectionBusy || Game.InGame || Profile.Account == null) break;
      collectionBusy = true; collectionFailed = false; PushState();
      var accountForCollection = Profile.Account;
      try {
       var refreshed = await Bridge.ReadSkinCollection(accountForCollection);
       if (Profile.Account != null && Profile.Account.Key == accountForCollection.Key) {
        collection = refreshed;
        collectionAccount = accountForCollection.Key;
        collectionFailed = collection.Count == 0;
       }
      } catch { collectionFailed = true; }
      finally { collectionBusy = false; PushState(); }
      break;
     case "viewMatchReview":
      string reviewId = Json.S(msg, "matchId");
      viewedMatchKey = Profile.History.Any(record => record.Id == reviewId) ? MatchReviewKey(reviewId) : "";
      PushState(); break;
     case "saveMatchNote":
     case "attachMatchClip":
     case "openMatchClip":
     case "removeMatchClip":
      {
       string matchId = Json.S(msg, "matchId");
       string key = MatchReviewKey(matchId);
       if (Game.InGame || key == "" || !Profile.History.Any(record => record.Id == matchId)) break;
       if (type == "saveMatchNote") {
        string note = Json.S(msg, "text").Trim();
        if (note.Length == 0) Config.MatchNotes.Remove(key);
        else Config.MatchNotes[key] = note.Substring(0, Math.Min(3000, note.Length));
       } else if (type == "attachMatchClip") {
        var picker = new Microsoft.Win32.OpenFileDialog { Title = "Select a match clip",
         Filter = "Video files|*.mp4;*.webm;*.mkv;*.mov", CheckFileExists = true };
        if (picker.ShowDialog(this) != true) break;
        Config.MatchClips[key] = picker.FileName;
       } else if (type == "removeMatchClip") {
        Config.MatchClips.Remove(key);
       } else {
        string video;
        if (!Config.MatchClips.TryGetValue(key, out video) || !File.Exists(video)) {
         StatusText = "Clip file unavailable. Attach it again.";
         PushState();
         break;
        }
        string[] extensions = { ".mp4", ".webm", ".mkv", ".mov" };
        if (extensions.Contains(Path.GetExtension(video), StringComparer.OrdinalIgnoreCase))
         System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(video) { UseShellExecute = true });
        break;
       }
       Save(); PushState(); break;
      }
     case "exportItemSet":
      if (Game.InGame) { ImportStatus = "Export is available before the game."; PushState(); break; }
      if (Selected == null || ModeData.ModeKey != ActiveMode || ModeData.ChampionSlug != Selected.Slug) {
       ImportStatus = "Load the selected champion’s build first."; PushState(); break;
      }
      try {
       var bp = CurrentBuildPath(true);
       var requested = Json.A(msg, "guideItems").Select(Json.ToInt).Where(id => id > 0).Distinct().Take(7).ToList();
       var allowed = new HashSet<int>(ModeData.Items.Concat(ModeData.BestItems).Concat(ModeData.Boots)
        .Concat(ModeData.Starters).Select(item => item.Id));
       if (requested.Count > 0 && requested.Any(id => !allowed.Contains(id))) {
        ImportStatus = "Some saved items are unavailable for this champion and mode.";
        PushState(); break;
       }
       bool ranked = ModeData.RankedVariant != null;
       var exportedCore = ranked ? (requested.Count > 0 ? requested : bp.Select(step => step.Stat.Id)).Where(id => Price(id) > 0).ToList()
        : requested.Count > 0 ? requested.Where(id => !ModeData.Starters.Any(x => x.Id == id) &&
        !ModeData.Boots.Any(x => x.Id == id)).ToList() : bp.Where(x => !x.IsBoots && x.Role != "STARTER").Select(x => x.Stat.Id).ToList();
       ImportStatus = await Bridge.ExportItemSet(
        Selected.Name, ChampionIdOf(Selected), ActiveMode, ActiveMode == "SR" ? ActiveRole : "",
        ModeData.Starters.Select(x => x.Id).ToList(),
        exportedCore,
        ranked ? new List<int>() : ModeData.Boots.Select(x => x.Id).ToList(),
        ModeData.BestItems.Where(x => !ranked || Price(x.Id) > 0).Select(x => x.Id).ToList()
       );
      } catch (Exception ex) { ImportStatus = "Shop export failed: " + ex.Message; }
      PushState(); break;
     case "importRunes":
      if (Game.InGame) { ImportStatus = "Rune import is available before the game."; PushState(); break; }
      try {
       if (Selected == null || ModeData.ModeKey != ActiveMode || ModeData.ChampionSlug != Selected.Slug)
        throw new InvalidOperationException("Load the selected champion’s build first.");
       var page = ModeData.Runes;
       var edited = Json.A(msg, "perks").Select(Json.ToInt).ToList();
       if (edited.Count > 0) {
        if (page == null || edited.Count != page.Perks.Count || edited.Distinct().Count() != edited.Count)
         throw new InvalidOperationException("The edited page does not match the active build.");
        for (int i = 0; i < edited.Count; i++) {
         if (edited[i] == page.Perks[i]) continue;
         RuneMeta original, replacement;
         if (!MultiModeParser.DDragonRunes.TryGetValue(page.Perks[i], out original) ||
             !MultiModeParser.DDragonRunes.TryGetValue(edited[i], out replacement) ||
             original.Style == 0 || original.Style != replacement.Style || original.Slot != replacement.Slot)
          throw new InvalidOperationException("Choose a rune in the same tree and slot.");
        }
        page = new RunePage { PrimaryStyle = page.PrimaryStyle, SecondaryStyle = page.SecondaryStyle, Perks = edited, Name = page.Name };
       }
       ImportStatus = await Bridge.ImportRunes(page);
      } catch (Exception ex) { ImportStatus = "Rune import failed: " + ex.Message; }
      PushState(); break;
     case "prepareBorderless": PrepareBorderless(); PushState(); break;
     case "chooseLockfile":
      var d = new Microsoft.Win32.OpenFileDialog { Title = "Select League client lockfile", Filter = "Lockfile|lockfile|All files|*.*" };
      if (d.ShowDialog(this) == true) { Bridge.Lockfile = d.FileName; Save(); }
      break;
    }
   } catch { }
  }

  public static string BorderlessConfig(string text) {
   string section = ""; bool found = false;
   var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
   for (int i = 0; i < lines.Count; i++) {
    if (lines[i].TrimStart().StartsWith("[")) section = lines[i].Trim();
    if (section.Equals("[General]", StringComparison.OrdinalIgnoreCase) && Regex.IsMatch(lines[i], @"^\s*WindowMode\s*=")) { lines[i] = "WindowMode=2"; found = true; }
   }
   if (!found) {
    int general = lines.FindIndex(x => x.Trim().Equals("[General]", StringComparison.OrdinalIgnoreCase));
    if (general >= 0) lines.Insert(general + 1, "WindowMode=2"); else { lines.Add("[General]"); lines.Add("WindowMode=2"); }
   }
   return String.Join("\r\n", lines);
  }

  void SyncLeagueMinimapConfig() {
   try {
    if (String.IsNullOrEmpty(Bridge.Lockfile) || !File.Exists(Bridge.Lockfile)) { StatusText = "Connect League client to read PersistedSettings.json."; return; }
    string file = Path.Combine(Path.GetDirectoryName(Bridge.Lockfile), "Config", "PersistedSettings.json");
    if (!File.Exists(file)) { StatusText = "PersistedSettings.json not found."; return; }
    string txt = File.ReadAllText(file, Encoding.UTF8);
    var mScale = Regex.Match(txt, "\"name\"\\s*:\\s*\"MinimapScale\"\\s*,\\s*\"value\"\\s*:\\s*\"([0-9\\.]+)\"", RegexOptions.IgnoreCase);
    if (mScale.Success) {
     double v;
     if (double.TryParse(mScale.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out v)) {
      // Calibrated from real League PersistedSettings: MinimapScale 1.0 -> 1.00 (24vh), 0.0 -> 0.77, 2.0 -> 1.23, 3.0 -> 1.46
      Config.MinimapScale = Math.Max(0.55, Math.Min(1.85, 0.77 + v * 0.23));
     }
    }
    var mFlip = Regex.Match(txt, "\"name\"\\s*:\\s*\"FlipMiniMap\"\\s*,\\s*\"value\"\\s*:\\s*\"([01])\"", RegexOptions.IgnoreCase);
    if (mFlip.Success) Config.MinimapLeft = mFlip.Groups[1].Value == "1";
    PanelsForMode().Remove("minimap_overlay");
    Save();
    StatusText = "Minimap synced from League (" + Math.Round(Config.MinimapScale * 100) + "%, " + (Config.MinimapLeft ? "Left" : "Right") + ")";
   } catch (Exception ex) { StatusText = "Minimap sync failed: " + ex.Message; }
  }

  void PrepareBorderless() {
   try {
    if (Game.InGame) throw new Exception("Finish the current game before changing video settings.");
    if (String.IsNullOrEmpty(Bridge.Lockfile) || !File.Exists(Bridge.Lockfile)) throw new Exception("Connect League client to locate game.cfg.");
    string file = Path.Combine(Path.GetDirectoryName(Bridge.Lockfile), "Config", "game.cfg");
    if (!File.Exists(file)) throw new Exception("game.cfg not found. Set Borderless in League video settings.");
    string backup = file + ".maylo-backup"; if (!File.Exists(backup)) File.Copy(file, backup);
    File.WriteAllText(file, BorderlessConfig(File.ReadAllText(file)));
    StatusText = "Borderless mode set for the next game.";
   } catch (Exception ex) { StatusText = ex.Message; }
  }
 }

}
