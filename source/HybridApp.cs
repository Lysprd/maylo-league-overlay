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
    web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
    web.CoreWebView2.Settings.IsStatusBarEnabled = false;
    web.CoreWebView2.Settings.AreDevToolsEnabled = Environment.GetEnvironmentVariable("MAYLO_DEVTOOLS") == "1";
    web.CoreWebView2.SetVirtualHostNameToFolderMapping("maylo.local", app.UiDistDir, CoreWebView2HostResourceAccessKind.Allow);
    web.CoreWebView2.WebMessageReceived += (s, e) => app.HandleWebMessage(e.WebMessageAsJson);
    web.CoreWebView2.NavigationCompleted += (s, e) => { webReady = true; app.PushState(); };
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
   bool modeAllowed = app.Game.Mayhem || app.Config.ShowBuildOverlay || app.Config.OcrAllModes || app.Config.ShowPerfStrip || app.Config.ShowJungleWidget || app.Config.ShowBenchBar;
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
   bool wantInteractive = app.IsOverlayEditing || app.InGameSheet || app.OverlayDragging;
   // CRITICAL: On Tab hold, keep WS_EX_NOACTIVATE (wantActivate = false) so League keeps keyboard focus (ZERO lag, ZERO Tab scroll)
   // while Background alpha=1 + ~WS_EX_TRANSPARENT still allows instant mouse dragging!
   bool wantActivate = app.InGameSheet || app.Editing;
   if (lastInteractive != wantInteractive || lastActivate != wantActivate) {
    SetInteraction(wantInteractive, wantActivate);
   }
  }
 }

 public class HybridHubWindow : Window {
  readonly WebView2 web;
  Border splashCover;
  Task<CoreWebView2Environment> webEnvTask;
  bool webReady, splashDismissed, closing, pollBusy, ocrBusy, selectionBusy, tabHeld, tabSuppressed, manualModeOverride;
  public bool HubActive, Preview, Editing, OverlayVisible = true, PrivateMode, InGameSheet, OverlayDragging;
  public string InGameSheetTab = "build", PreviewScene = "";
  public bool IsOverlayEditing { get { return Editing || tabHeld; } }
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
  public bool BootReady = false;
  public int BootProgress = 12;
  public string BootStage = "Loading…";
  public string ConnectionText = "Connecting to League client…", StatusText = "maylo · Ready", OcrStatus = "Auto-scan ready · Alt+A", ImportStatus = "";
  public double OcrLastMs;
  public List<string> OcrLastLines = new List<string>();
  byte[] lastDebugPng;
  ulong lastOcrSig;
  DateTime nextOcrRecovery = DateTime.MinValue;
  int offerScanGeneration;
  int previewAugmentCount = 3;
  bool manualRoleOverride, leftMouseWasDown, realQuit, wasInGame, hadGameWindow, loadoutBusy;
  readonly Dictionary<string, double> spellReadyAt = new Dictionary<string, double>();

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
   if (Config.TrackedAugments == null) Config.TrackedAugments = new List<int>();
   if (Config.Layout == null) Config.Layout = new Placement();
   if (Config.AugmentPreferences == null) Config.AugmentPreferences = new Dictionary<string, int>();
   if (Config.Notes == null) Config.Notes = new Dictionary<string, string>();
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

    Loaded += async (s, e) => {
    GitHubUpdater.StatusChanged += () => {
     if (!closing) Dispatcher.BeginInvoke((Action)(() => { if (!closing) PushState(); }));
    };
    try {
     BootProgress = 24; BootStage = "Loading…";
     await InitWeb();
     PushState();
     overlay = new HybridOverlayWindow(this);
     overlay.Show();
     SetupTray();
     if (startMinimized && Config.MinimizeToTray && tray != null) Hide();
     StartTimers();
     BootProgress = 52; BootStage = "Loading Riot Data Dragon & Meta…"; PushState();
     await LoadHome(true);
     BootProgress = 78; BootStage = "Connecting League of Legends client…"; PushState();
     await Poll();
     BootProgress = 94; BootStage = "Syncing Tier List & Match History…"; PushState();
     await RefreshTierList(false);
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
    Tuple.Create(3, KeyToVk(Config.HotkeyScan, 0x41), "Alt+" + Config.HotkeyScan),
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
   web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
   web.CoreWebView2.Settings.IsStatusBarEnabled = false;
   web.CoreWebView2.Settings.AreDevToolsEnabled = Environment.GetEnvironmentVariable("MAYLO_DEVTOOLS") == "1";
   web.CoreWebView2.SetVirtualHostNameToFolderMapping("maylo.local", UiDistDir, CoreWebView2HostResourceAccessKind.Allow);
   web.CoreWebView2.WebMessageReceived += (s, e) => HandleWebMessage(e.WebMessageAsJson);
   web.CoreWebView2.NavigationCompleted += (s, e) => { webReady = true; PushState(); };
   string th = String.Equals(Config.Theme, "light", StringComparison.OrdinalIgnoreCase) ? "light" : "dark";
   web.CoreWebView2.Navigate("https://maylo.local/index.html?theme=" + th + "#hub");
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
   wasInGame = false;
   hadGameWindow = false;
   Game.InGame = false;
   Game.Window = IntPtr.Zero;
   ResetOfferSession();
   InGameSheet = false;
   OverlayDragging = false;
   spellReadyAt.Clear();
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
   pollTimer.Tick += async (s, e) => await Poll();
   pollTimer.Start();

   selectionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
   selectionTimer.Tick += async (s, e) => await FollowSelection();
   selectionTimer.Start();

   ocrTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(110) };
   ocrTimer.Tick += async (s, e) => {
    ocrTimer.Interval = TimeSpan.FromMilliseconds(offerSession.Visible ? 75 : 110);
    bool allowOcr = Game.InGame && Config.Ocr && Config.Connected && !HubActive && !InGameSheet && !IsOverlayEditing && !ocrBusy && (Game.Mayhem || Config.OcrAllModes);
    if (!allowOcr || Game.ModeKey == "") return;
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
     case 3: FireHotkey("Scan", true); break;
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
   bool edgeSheet = KeyEdge(KeyToVk(Config.HotkeySheet, 0x47)), edgeL = KeyEdge(0x4C), edgeH = KeyEdge(0x48), edgeScan = KeyEdge(KeyToVk(Config.HotkeyScan, 0x41)), edgeOverlay = KeyEdge(KeyToVk(Config.HotkeyOverlay, 0x4D)), edgeEdit = KeyEdge(KeyToVk(Config.HotkeyEdit, 0x4F)), edgeF9 = KeyEdge(0x78), edgeEsc = KeyEdge(0x1B);
   if (edgeEsc && inLeagueOrApp) {
    if (InGameSheet) { CloseInGameSheet(true); return; }
    if (HubActive && Native.OurWindow(fg)) { DismissHub(true); return; }
   }
   if (edgeF9 && inLeagueOrApp) { FireHotkey("Build", false); return; }
   if (!alt || !inLeagueOrApp) return;
   if (edgeSheet) FireHotkey("Build", false);
   else if (edgeL) FireHotkey("Live", false);
   else if (edgeH) FireHotkey("History", false);
   else if (edgeScan) FireHotkey("Scan", false);
   else if (edgeOverlay) FireHotkey("Overlay", false);
   else if (edgeEdit) FireHotkey("Edit", false);
  }

  void FireHotkey(string action, bool fromGlobal) {
   DateTime prev;
   if (lastActionFire.TryGetValue(action, out prev) && (DateTime.UtcNow - prev).TotalMilliseconds < 320) return;
   lastActionFire[action] = DateTime.UtcNow;
   switch (action) {
    case "Overlay": OverlayVisible = !OverlayVisible; if (overlay != null) overlay.Sync(); PushState(); break;
    case "Edit": Editing = !Editing; OverlayVisible = true; if (!Editing) { Preview = false; PreviewScene = ""; OverlayDragging = false; Save(); } if (overlay != null) overlay.Sync(); PushState(); break;
    case "Scan": TriggerRead(); break;
    case "Build": HotkeyDestination("Build", fromGlobal); break;
    case "History": HotkeyDestination("History", fromGlobal); break;
    case "Live": HotkeyDestination("Live", fromGlobal); break;
   }
  }

  void TrackAugmentClick() {
   bool down = (Native.GetAsyncKeyState(0x01) & 0x8000) != 0;
   bool edge = down && !leftMouseWasDown;
   leftMouseWasDown = down;
   if (!edge || !offerSession.Visible || !Game.InGame || HubActive || InGameSheet || IsOverlayEditing) return;
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
   }
  }

  void UpdateTabEditing() {
   // Require Ctrl + Tab (instead of bare Tab) so checking the League scoreboard with Tab NEVER interrupts gameplay!
   bool ctrl = (Native.GetAsyncKeyState(0x11) & 0x8000) != 0;
   bool down = ctrl && ((Native.GetAsyncKeyState(9) & 0x8000) != 0);
   if (!down) tabSuppressed = false;
   bool allowed = !tabSuppressed && !HubActive && !InGameSheet && (Game.InGame || Game.InSelection || Preview) && Config.Overlay && OverlayVisible && (Native.SameApplication(Native.GetForegroundWindow(), Game.Window) || Native.OurWindow(Native.GetForegroundWindow()));
   bool next = down && allowed;
   if (!next && OverlayDragging) return;
   if (next == tabHeld) return;
   tabHeld = next;
   if (overlay != null) overlay.Sync();
   PushState();
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
   if (ActiveMode != "MAYHEM") {
    if (View == "Augments") View = "TierList";
    if (InGameSheetTab == "augments") InGameSheetTab = "build";
   }
  }

  public async Task RefreshTierList(bool force = false) {
   int ticket = ++tierGeneration;
   try {
    var list = await MultiModeParser.LoadTierList(ActiveMode, Config.TierRole, Config.TierElo, Champions, force);
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
   // Clear stale ModeData immediately when switching champion/mode so SyncLoadout NEVER applies the previous champion's runes/spells!
   if (ModeData == null || ModeData.ChampionSlug != c.Slug || ModeData.ModeKey != ActiveMode) {
    ModeData = new ModeBuildData { ChampionSlug = "", ModeKey = ActiveMode };
   }
   PushState();
   try {
    string targetRole = ActiveMode == "SR" ? (!String.IsNullOrEmpty(ActiveRole) ? ActiveRole : DetectedRole) : "";
    var data = await MultiModeParser.LoadChampionMode(c, ActiveMode, targetRole, Config.TierElo, force || refreshedThisLaunch.Add(ActiveMode + ":" + c.Slug + ":" + targetRole + ":" + Config.TierElo), Bridge.Items, Champions);
    if (closing || ticket != generation) return;
    if (ActiveMode != "MAYHEM" && data.Augments.Count == 0 && prevModeData != null && prevModeData.ChampionSlug == c.Slug && prevModeData.Augments.Count > 0) data.Augments = prevModeData.Augments;
    data.ChampionSlug = c.Slug;
    ModeData = data;
    if (ActiveMode == "SR" && !String.IsNullOrEmpty(data.ActiveRole)) ActiveRole = data.ActiveRole;
    EnsureAugmentCatalog();
    StatusText = c.Name + " · " + ModeData.SourceLabel;
    PushState();
    await SyncLoadout();
   } catch (Exception ex) {
    if (ticket == generation) { StatusText = "Build unavailable: " + ex.Message; PushState(); }
   }
  }

  void EnsureAugmentCatalog() {
   var names = new HashSet<string>(ModeData.Augments.Select(x => GameBridge.Norm(x.Name)));
   var seenIds = new HashSet<int>(ModeData.Augments.Select(x => x.Id));
   foreach (int id in Bridge.MayhemAugmentIds) {
    string name; if (!Bridge.AugmentNames.TryGetValue(id, out name) || !names.Add(GameBridge.Norm(name))) continue;
    string rarity; Bridge.AugmentRarities.TryGetValue(id, out rarity);
    seenIds.Add(id);
    ModeData.Augments.Add(new Stat { Id = id, Name = name, Icon = "/icons/augments/" + id + ".png", Games = 0, Rate = 0, Rarity = TierLogic.RarityKey(rarity), Pick = "—" });
   }
   // Also include all CommunityDragon cherry-augments (600+ EN/FR ARAM Mayhem & Arena augments) so no augment is ever missed by OCR!
   foreach (var kv in MultiModeParser.AugmentAliasesById) {
    if (seenIds.Contains(kv.Key) || kv.Value == null || kv.Value.Count == 0) continue;
    string bestName = kv.Value[kv.Value.Count - 1];
    if (!names.Add(GameBridge.Norm(bestName))) continue;
    string rar; MultiModeParser.AugmentRarityById.TryGetValue(kv.Key, out rar);
    seenIds.Add(kv.Key);
    ModeData.Augments.Add(new Stat { Id = kv.Key, Name = bestName, Icon = "/icons/augments/" + kv.Key + ".png", Games = 0, Rate = 0, Rarity = TierLogic.RarityKey(rar), Pick = "—" });
   }
  }

  void ResetOfferSession() {
   offerSession.Reset();
   lastOcrSig = 0;
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
    var fresh = await Bridge.Read(Config.AutoAccept);
    if (closing || !Config.Connected) return;
    Game = fresh;
    plannedItems.RemoveWhere(id => Game.Owned.Contains(id));
    lastGoodGame = DateTime.UtcNow;
    bool matchPhase = Game.InSelection || Game.InGame;
    ConnectionText = matchPhase
     ? (Game.InGame ? "Live · " : "Champ Select · ") + Game.ModeLabel +
       (Game.ModeKey == "" && Game.ModeLabel != "Unsupported mode" ? " · Unsupported mode" : "")
     : "Client connected · " + Game.Phase + (String.IsNullOrEmpty(Game.Mode) ? "" : " · " + Game.ModeLabel);

    bool enteringMatch = (!oldGame.InSelection && Game.InSelection) || (!wasInGame && Game.InGame);
    if (enteringMatch) {
     // Entering a real Champ Select or Game ALWAYS adapts to the actual match mode & role!
     manualModeOverride = false;
     manualRoleOverride = false;
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
      TierList = new List<TierListEntry>();
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
     if (Config.AutoSpells && ModeData.Spells.Count > 0) {
      try { await Bridge.ApplySpells(Selected.AssetKey, SpellPlan.Pair(ModeData.Spells[0].Name), Config.FlashKey); } catch { Bridge.SpellStatus = "Spell update unavailable"; }
     }
     if (Config.AutoRunes && ActiveMode != "MAYHEM" && ModeData.Runes != null) {
      try { await Bridge.ApplyRunesAuto(Selected.AssetKey, ActiveMode, ActiveRole, ModeData.Runes); } catch { Bridge.RuneStatus = "Rune update unavailable"; }
     }
     // Auto-Export Shop Item Set during Champ Select (League loads custom item sets at game start, not mid-game!)
     if (Config.AutoExportShop) {
      try {
       var coreIds = ModeData.Items.Where(x => x.Id > 0).Select(x => x.Id).Take(6).ToList();
       await Bridge.ApplyShopAuto(
        Selected.Name, ChampionIdOf(Selected), ActiveMode, ActiveMode == "SR" ? ActiveRole : "",
        ModeData.Starters.Select(x => x.Id).ToList(),
        coreIds,
        ModeData.Boots.Select(x => x.Id).ToList(),
        ModeData.BestItems.Select(x => x.Id).ToList()
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

  async void TriggerRead() { await ReadAugments(true); }

  async Task ReadAugments(bool requested) {
   if (ocrBusy || !Config.Connected || (Game.InGame && Game.ModeKey == "")) return;
   EnsureAugmentCatalog();
   if (Game.InGame && (Selected == null || !ChampionMatches(Selected, Game.Champion))) {
    var played = Champions.FirstOrDefault(c => ChampionMatches(c, Game.Champion));
    if (played != null) { await SelectChampion(played); return; }
   }
   if (ModeData.Augments.Count == 0) return;
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
    bool allowWide = requested || DateTime.UtcNow >= nextOcrRecovery;
    if (allowWide) nextOcrRecovery = DateTime.UtcNow.AddMilliseconds(900);
    ulong prevSig = offerSession.Visible && !offerSession.Refreshing ? lastOcrSig : 0UL;
    int scanGeneration = offerScanGeneration;
    // Run GDI capture + bitmap scaling + 600-augment fuzzy matching OFF the WPF UI thread!
    var scan = await Task.Run(() => AugmentOcr.ScanGameRect(rect, augSnapshot, aliasMap, preferUserLang, null, allowWide, requested, prevSig));
    if (scanGeneration != offerScanGeneration || !Game.InGame || closing || Game.Window != target ||
        !Native.IsWindow(target) || !Native.IsWindowVisible(target) || !Native.SameApplication(Native.GetForegroundWindow(), target)) return;
    if (scan.SkippedUnchanged) return;
    lastOcrSig = scan.Hits.Count > 0 ? scan.Signature : 0;
    OcrLastMs = scan.Milliseconds;
    OcrLastLines = scan.Lines;
    if (scan.DebugPng != null) lastDebugPng = scan.DebugPng;
    bool beforeRef = offerSession.Refreshing;
    bool changed = offerSession.Observe(scan.Hits, DateTime.UtcNow, requested);
    if (!offerSession.Visible) lastOcrSig = 0;
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
    if (changed || beforeRef != offerSession.Refreshing || requested) PushState();
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

  public List<BuildStep> CurrentBuildPath() {
   var map = ActiveItemsMap();
   var source = ModeData.Items.Concat(ModeData.BestItems).Concat(ModeData.Boots);
   var candidates = source.Where(x => x.Id > 0 && x.Id < 220000 && (!map.ContainsKey(x.Id) || Json.I(map[x.Id], "priceTotal") >= 1500 || ModeData.Boots.Any(b => b.Id == x.Id)));
   var complete = Game.Owned.Concat(plannedItems).Where(id => id > 0).Where(id => !map.ContainsKey(id) || (Json.A(map[id], "to").Length == 0 && Json.I(map[id], "priceTotal") >= 1500) || Json.I(map[id], "priceTotal") >= 2400 || ModeData.Boots.Any(b => BuildPlanner.Canonical(b.Id) == BuildPlanner.Canonical(id))).ToList();
   var themes = new List<string>();
   foreach (int aid in chosenAugments) {
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
   var threats = CurrentEnemyThreats();
   var list = BuildPlanner.Plan(candidates, complete, ModeData.Boots.Select(x => x.Id), Selected == null ? 50 : Selected.Win, ItemCategories, Config.BuildFocus, themes, ModeData.CorePaths, threats, ItemName);
   // Integrate recommended Starter item at game start (step 0) until any starter is bought, 8 min elapses, or first core is completed!
   if (complete.Count == 0 && ModeData.Starters.Count > 0 && (!Game.InGame || Game.Live.Seconds < 480)) {
    bool ownsStarter = Game.Owned.Any(id => MultiModeParser.StarterIds.Contains(id))
     || ModeData.Starters.Any(s => s.Id > 0 && (Game.Owned.Contains(s.Id) || plannedItems.Contains(s.Id)));
    var topStarter = ModeData.Starters.FirstOrDefault(s => s.Id > 0);
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

  object SynergyFor(Stat stat) {
   if (stat == null || ModeData.Combos.Count == 0) return null;
   var names = new HashSet<string>(ModeData.Augments.Where(x => chosenAugments.Contains(x.Id) && x.Id != stat.Id).Select(x => x.Name));
   foreach (var pair in ModeData.Combos.OrderByDescending(x => x.Rate)) {
    var parts = pair.Name.Split(new[] { " + " }, StringSplitOptions.None);
    if (parts.Length != 2 || !parts.Contains(stat.Name)) continue;
    string partner = parts.FirstOrDefault(x => x != stat.Name);
    if (names.Contains(partner)) return new { partner = partner, rate = pair.Rate };
   }
   return null;
  }

  double SynergyBonus(Stat stat) {
   if (stat == null || Selected == null) return 0;
   var names = new HashSet<string>(ModeData.Augments.Where(x => chosenAugments.Contains(x.Id) && x.Id != stat.Id).Select(x => x.Name));
   var rates = new List<double>();
   foreach (var pair in ModeData.Combos) {
    var parts = pair.Name.Split(new[] { " + " }, StringSplitOptions.None);
    if (parts.Length == 2 && parts.Contains(stat.Name) && names.Contains(parts.FirstOrDefault(x => x != stat.Name))) rates.Add(pair.Rate);
   }
   return rates.Count == 0 ? 0 : Math.Max(-4, Math.Min(5, rates.Average(r => (r - Selected.Win) * .20)));
  }

  Stat BestOffer() { return Selected == null ? null : Recommendations.Best(offerSession.Choices, Selected.Win, Preference, SynergyBonus); }

  object SerializeStat(Stat s, Stat best = null) {
   double baseline = Selected == null ? 50 : Selected.Win;
   return new {
    id = s.Id, name = s.Name, pick = s.Pick ?? "", win = s.Win ?? "", rate = s.Rate,
    icon = s.Icon ?? "", games = s.Games, rarity = TierLogic.RarityKey(s.Rarity),
    tier = s.Games > 0 ? TierLogic.AugTier(s, baseline) : "",
    price = Price(s.Id),
    preference = Preference(s),
    tracked = chosenAugments.Contains(s.Id),
    recommended = best != null && best.Id == s.Id,
    synergy = SynergyFor(s)
   };
  }

  public string BuildStateJson() {
   var path = CurrentBuildPath();
   var me = Game.Live.Players.FirstOrDefault(p => p.Me);
   var inv = me == null ? Game.Owned.AsEnumerable() : me.Items.AsEnumerable();
   var bestOffer = BestOffer();

   var topMap = new Dictionary<string, object>();
   var rarities = Preview
    ? new List<string> { "Gold", "Prismatic" }
    : offerSession.Choices.Where(x => x != null).Select(x => TierLogic.RarityKey(x.Rarity)).Where(r => r != "").Distinct().ToList();
   foreach (string r in rarities) {
    var list = ModeData.Augments.Where(x => x.Games >= 100 && !chosenAugments.Contains(x.Id) && TierLogic.RarityKey(x.Rarity) == r)
     .OrderByDescending(x => Recommendations.Score(x, Selected == null ? 50 : Selected.Win, Preference(x)) + SynergyBonus(x))
     .ThenByDescending(x => x.Games).Take(5).Select(x => SerializeStat(x, bestOffer)).ToList();
    if (list.Count > 0) topMap[r] = list;
   }

   int completed = Math.Max(0, 6 - path.Count(x => x.Role != "STARTER"));
   string summary = path.Count == 0
    ? (Selected == null ? "Select a champion to inspect build path." : (ModeData.Items.Count == 0 && ModeData.BestItems.Count == 0 ? "No build data available." : Config.BuildFocus + " build complete (6/6 items)."))
    : Config.BuildFocus + " path · " + completed + "/6 slots" + (chosenAugments.Count > 0 ? " · tuned to " + chosenAugments.Count + " augments" : "") + " · Next: " + path[0].Stat.Name;

   var offeredList = new List<object>();
   var previewAugments = Preview && PreviewScene == "augments"
    ? ModeData.Augments.OrderByDescending(x => x.Games >= 100).Take(previewAugmentCount).ToArray()
    : null;
   int slotCount = previewAugments != null ? previewAugments.Length : offerSession.Choices.Length;
   for (int i = 0; i < slotCount; i++) {
    Stat st = previewAugments != null ? previewAugments[i] : offerSession.Choices[i];
    var pos = previewAugments != null ? null : offerSession.Positions[i];
    offeredList.Add(new { slot = i, stat = st == null ? null : SerializeStat(st, previewAugments != null && i == 0 ? st : bestOffer), x = pos == null ? 0 : pos.X, y = pos == null ? 0 : pos.Y });
   }

   object inspectedItemObj = null;
   if (inspectedItemId > 0) {
    Dictionary<string, object> d; ActiveItemsMap().TryGetValue(inspectedItemId, out d);
    var st = ModeData.Items.Concat(ModeData.BestItems).Concat(ModeData.Boots).FirstOrDefault(x => x.Id == inspectedItemId);
    var copy = d != null ? ItemCopy.Parse(Json.S(d, "description")) : new ItemCopy();
    var comps = d != null ? Json.A(d, "from").Select(Json.ToInt).Where(x => x > 0).Select(cid => new { id = cid, name = ItemName(cid), price = Price(cid) }).ToArray() : new object[0];
    inspectedItemObj = new {
     id = inspectedItemId, name = d != null ? Json.S(d, "name") : (st != null ? st.Name : "Item " + inspectedItemId),
     priceTotal = Price(inspectedItemId), owned = Game.Owned.Contains(inspectedItemId), planned = plannedItems.Contains(inspectedItemId),
     stats = copy.Stats, paragraphs = copy.Paragraphs, components = comps,
     winRate = st != null ? st.Rate : 0, pickRate = st != null ? st.Pick : ""
    };
   }

   object inspectedAugObj = null;
   if (inspectedAugmentId > 0) {
    var aug = ModeData.Augments.FirstOrDefault(x => x.Id == inspectedAugmentId);
    if (aug != null) {
     string desc = MultiModeParser.GetAugmentDescription(aug.Id, aug.Name, Bridge.AugmentDescriptions);
     double baseline = Selected == null ? 50 : Selected.Win;
     var pairings = ModeData.Combos
      .Where(c => c.Name.Split(new[] { " + " }, StringSplitOptions.None).Contains(aug.Name))
      .OrderByDescending(c => c.Rate).Take(6)
      .Select(c => {
       string partnerName = c.Name.Split(new[] { " + " }, StringSplitOptions.None).FirstOrDefault(n => n != aug.Name) ?? c.Name;
       var partnerStat = ModeData.Augments.FirstOrDefault(x => x.Name == partnerName);
       return new { id = partnerStat != null ? partnerStat.Id : 0, name = partnerName, icon = partnerStat != null ? partnerStat.Icon : "", rate = c.Rate };
      }).ToArray();
     inspectedAugObj = new {
      id = aug.Id, name = aug.Name, icon = aug.Icon ?? "", rarity = TierLogic.RarityKey(aug.Rarity),
      tier = TierLogic.AugTier(aug, baseline), rate = aug.Rate, pick = aug.Pick ?? "", games = aug.Games,
      delta = aug.Games > 0 ? Math.Round(aug.Rate - baseline, 1) : 0,
      description = desc,
      preference = Preference(aug), tracked = chosenAugments.Contains(aug.Id), pairings = pairings
     };
    }
   }

   Func<int, object> runeMetaObj = id => {
    RuneMeta rm;
    if (MultiModeParser.DDragonRunes.TryGetValue(id, out rm)) return new { id = rm.Id, name = rm.Name, icon = rm.Icon };
    return new { id = id, name = "Rune " + id, icon = "" };
   };
   var meLive = Game.Live.Players.FirstOrDefault(p => p.Me);
   string myTeam = meLive != null ? meLive.Team : "ORDER";
   string myPos = meLive != null ? MultiModeParser.NormalizeRole(meLive.Position) : ActiveRole;
   double mins = Math.Max(1.0 / 60.0, Game.Live.Seconds / 60.0);
   double topCsMin = Game.Live.Players.Count > 0 && Game.Live.Seconds > 90 ? Game.Live.Players.Max(x => x.Cs / mins) : 999;

   var activeCds = new List<object>();
   foreach (var kv in spellReadyAt.ToList()) {
    double rem = kv.Value - Game.Live.Seconds;
    if (rem <= 0) { spellReadyAt.Remove(kv.Key); continue; }
    int rSec = (int)Math.Ceiling(kv.Value);
    activeCds.Add(new { key = kv.Key, readyAt = kv.Value, readyClock = (rSec / 60) + ":" + (rSec % 60).ToString("00"), secondsLeft = (int)Math.Ceiling(rem) });
   }

   Func<Champion, TierListEntry> modeEntryFor = ch => {
    if (ch == null || ActiveMode == "MAYHEM" || TierList == null || TierList.Count == 0) return null;
    if (ActiveMode == "SR" && !String.IsNullOrEmpty(ActiveRole)) {
     var exact = TierList.FirstOrDefault(t => t.Slug == ch.Slug && String.Equals(t.Role, ActiveRole, StringComparison.OrdinalIgnoreCase));
     if (exact != null) return exact;
    }
    return TierList.FirstOrDefault(t => t.Slug == ch.Slug);
   };
   var selMode = modeEntryFor(Selected);
   Func<string, string> patchTagOf = sl => {
    string pt;
    return !String.IsNullOrEmpty(sl) && MultiModeParser.PatchNotes != null && MultiModeParser.PatchNotes.ChampionTagBySlug.TryGetValue(sl, out pt) ? pt : "";
   };
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
    privateMode = PrivateMode,
    hubActive = HubActive,
    inGameSheet = InGameSheet,
    inGameSheetTab = InGameSheetTab,
    preview = Preview,
    previewScene = PreviewScene, previewAugmentCount = previewAugmentCount,
    editing = IsOverlayEditing,
    overlayVisible = OverlayVisible,
    augmentsVisible = offerSession.Visible,
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
    tierList = TierList.Take(320).Select(t => new { id = t.Id, slug = t.Slug, name = t.Name, icon = t.Icon, role = t.Role, tier = t.Tier, rank = t.Rank, rankDelta = t.RankDelta, patchTag = patchTagOf(t.Slug), winRate = t.WinRate, pickRate = t.PickRate, banRate = t.BanRate, kda = t.Kda, games = t.Games, counters = t.Counters.ToArray() }).ToArray(),
    selected = Selected == null ? null : new { name = Selected.Name, slug = Selected.Slug, tier = selMode != null ? selMode.Tier : Selected.Tier, win = selMode != null ? selMode.WinRate : Selected.Win, pick = selMode != null ? selMode.PickRate : Selected.Pick, icon = Selected.Icon, assetKey = Selected.AssetKey, patchTag = patchTagOf(Selected.Slug) },
    game = new {
     phase = Game.Phase, mode = Game.Mode, modeKey = Game.ModeKey, modeLabel = Game.ModeLabel, champion = Game.Champion, inGame = Game.InGame, inSelection = Game.InSelection, mayhem = Game.Mayhem, gold = Game.Gold,
     owned = Game.Owned.ToArray(), planned = plannedItems.ToArray(),
     bench = (Preview ? Champions.Take(6) : Game.Bench.Select(FromId).Where(c => c != null)).Select(c => { var be = modeEntryFor(c); return new { name = c.Name, slug = c.Slug, tier = be != null ? be.Tier : c.Tier, win = be != null ? be.WinRate : c.Win, pick = be != null ? be.PickRate : c.Pick, icon = MultiModeParser.ChampIconOf(c), assetKey = c.AssetKey, patchTag = patchTagOf(c.Slug) }; }).OrderByDescending(c => c.win).ToArray(),
     team = Game.Team.Select(FromId).Where(c => c != null).Select(c => new { name = c.Name, slug = c.Slug, tier = c.Tier, win = c.Win, pick = c.Pick, icon = MultiModeParser.ChampIconOf(c), assetKey = c.AssetKey }).ToArray(),
     live = new {
      clock = Game.Live.Clock, seconds = Game.Live.Seconds, health = Game.Live.Health, maxHealth = Game.Live.MaxHealth,
      ad = Game.Live.AttackDamage, ap = Game.Live.AbilityPower, armor = Game.Live.Armor, mr = Game.Live.MagicResist,
      attackSpeed = Game.Live.AttackSpeed, moveSpeed = Game.Live.MoveSpeed,
      activeLevel = Game.Live.ActiveLevel, q = Game.Live.AbilityQ, w = Game.Live.AbilityW, e = Game.Live.AbilityE, r = Game.Live.AbilityR,
      blueDragons = Game.Live.BlueDragons, redDragons = Game.Live.RedDragons, lastDragonType = Game.Live.LastDragonType,
      events = Game.Live.Events.ToArray(),
      inventoryValue = Game.InGame && Game.ModeKey != "" ? Game.Live.InventoryValue(ActiveItemsMap()) : null,
      inhibitors = Game.Live.Inhibitors.Select(o => new { key = o.Key, name = o.Name, status = o.Status, alive = o.Alive, secondsLeft = o.SecondsLeft }).ToArray(),
      objectives = Game.Live.Objectives.Select(o => new { key = o.Key, name = o.Name, status = o.Status, alive = o.Alive, secondsLeft = o.SecondsLeft }).ToArray(),
      spellCooldowns = activeCds.ToArray(),
      players = Game.Live.Players.Select(p => {
       var pc = Champions.FirstOrDefault(c => GameBridge.Norm(c.AssetKey) == GameBridge.Norm(p.Champion) || GameBridge.Norm(c.Name) == GameBridge.Norm(p.Champion));
       var tags = new List<string>();
       if (p.Kills >= 4 && p.Kills >= p.Deaths + 3) tags.Add("Fed");
       else if (p.Deaths >= 4 && p.Deaths >= p.Kills + 3) tags.Add("Behind");
       if (Game.Live.Seconds > 120 && p.Cs / mins >= Math.Max(7.0, topCsMin - 0.3)) tags.Add("CS Lead");
       string pRole = MultiModeParser.NormalizeRole(p.Position);
       bool isLaneOpp = p.Team != myTeam && myPos != "" && pRole == myPos;
       double matchupWr = 0;
       if (isLaneOpp && pc != null) {
        var mu = ModeData.Matchups.FirstOrDefault(m => GameBridge.Norm(m.Name) == GameBridge.Norm(pc.Name));
        if (mu == null && ActiveMode != "MAYHEM")
         mu = ModeData.Teammates.FirstOrDefault(m => GameBridge.Norm(m.Name) == GameBridge.Norm(pc.Name));
        if (mu != null) matchupWr = mu.Rate;
       }
       return new {
        key = p.Team + "|" + p.Name + "|" + p.Champion, name = p.Name, champion = p.Champion,
        championName = pc != null ? pc.Name : p.Champion, championIcon = pc != null ? MultiModeParser.ChampIconOf(pc) : "",
        team = p.Team, position = pRole, level = p.Level, kills = p.Kills, deaths = p.Deaths, assists = p.Assists, cs = p.Cs,
        dead = p.Dead, respawn = p.Respawn, me = p.Me, items = p.Items.ToArray(),
        spell1 = p.Spell1, spell2 = p.Spell2, spellKey1 = p.SpellKey1, spellKey2 = p.SpellKey2,
        keystone = p.Keystone, runeTree = p.RuneTree, tags = tags.ToArray(), isLaneOpponent = isLaneOpp, matchupWr = matchupWr
       };
      }).ToArray()
     }
    },
    buildSummary = summary,
    enemyThreats = CurrentEnemyThreats().Tags.ToArray(),
    buildPath = path.Select((step, idx) => {
     int total = Price(step.Stat.Id);
     int rem = idx == 0 ? BuildPlanner.RemainingCost(step.Stat.Id, inv, ActiveItemsMap()) : total;
     if (rem <= 0) rem = total;
     var comps = idx == 0 ? BuildPlanner.Components(step.Stat.Id, inv, ActiveItemsMap()).Select(cs => new { id = cs.Id, name = ItemName(cs.Id), owned = cs.Owned, price = Price(cs.Id), icon = MultiModeParser.ItemIconOf(cs.Id) }).ToArray() : new object[0];
     return new { stat = SerializeStat(step.Stat), role = step.Role, reason = step.Reason, shortTip = step.ShortTip, synergyTag = step.SynergyTag, synergyTone = step.SynergyTone, pickRate = step.PickRate, isBoots = step.IsBoots, totalGold = total, remainingGold = rem, affordable = idx == 0 && Game.InGame && Game.Gold >= 0 && rem > 0 && Game.Gold >= rem, components = comps };
    }).ToArray(),
    starters = ModeData.Starters.Select(s => SerializeStat(s)).ToArray(),
    boots = ModeData.Boots.Select(s => SerializeStat(s)).ToArray(),
    spells = ModeData.Spells.Select(s => SerializeStat(s)).ToArray(),
    bestItems = ModeData.BestItems.Where(s => !ModeData.Items.Take(6).Any(i => i.Name == s.Name)).Take(4).Select(s => SerializeStat(s)).ToArray(),
    teammates = ModeData.Teammates.Select(s => SerializeStat(s)).ToArray(),
    matchups = ModeData.Matchups.Select(s => SerializeStat(s)).ToArray(),
    combos = ModeData.Combos.Select(s => SerializeStat(s)).ToArray(),
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
    skills = ModeData.Skills == null ? null : new { priority = ModeData.Skills.Priority.ToArray(), levels = ModeData.Skills.Levels.ToArray(), source = ModeData.Skills.Source },
    offered = offeredList.ToArray(),
    topAugments = topMap,
    trackedAugments = ModeData.Augments.Where(x => chosenAugments.Contains(x.Id)).Select(x => SerializeStat(x, bestOffer)).ToArray(),
    allAugments = ModeData.Augments.Select(x => SerializeStat(x, bestOffer)).ToArray(),
    portraitPositions = portraitPositions.Select(p => new { champion = new { name = p.Champion.Name, slug = p.Champion.Slug, tier = p.Champion.Tier, win = p.Champion.Win, pick = p.Champion.Pick, icon = p.Champion.Icon, assetKey = p.Champion.AssetKey }, x = p.X, y = p.Y }).ToArray(),
    history = Profile.History.Take(60).Select(m => {
     var mc = FromId(m.ChampionId);
     var augDet = (m.Augments ?? new List<int>()).Where(id => id > 0).Select(id => {
      string an; Bridge.AugmentNames.TryGetValue(id, out an);
      var st = ModeData.Augments.FirstOrDefault(x => x.Id == id);
      return new { id = id, name = !String.IsNullOrEmpty(an) ? an : (st != null ? st.Name : ("Augment " + id)), icon = st != null && !String.IsNullOrEmpty(st.Icon) ? st.Icon : ("/icons/augments/" + id + ".png") };
     }).ToArray();
     return new { id = m.Id, championId = m.ChampionId, championName = mc != null ? mc.Name : ("Champion " + m.ChampionId), championIcon = mc != null ? MultiModeParser.ChampIconOf(mc) : "", queue = m.Queue, queueName = m.QueueName, mode = m.Mode, mayhem = m.Mayhem, kills = m.Kills, deaths = m.Deaths, assists = m.Assists, won = m.Won, remake = m.Remake, duration = m.Duration, time = m.Time, dateText = m.Date.ToString("dd MMM HH:mm"), damage = m.Damage, gold = m.Gold, cs = m.Cs, items = m.Items.ToArray(), augments = m.Augments.ToArray(), augmentDetails = augDet };
    }).ToArray(),
    note = Selected != null && Config.Notes.ContainsKey(Selected.Slug) ? Config.Notes[Selected.Slug] : "",
    hotkeys = hotkeyRegistered,
    settings = new {
     connected = Config.Connected, overlay = Config.Overlay, showBuildOverlay = Config.ShowBuildOverlay, showRerollBar = Config.ShowRerollBar,
     showSkillAdvisor = Config.ShowSkillAdvisor, showPerfStrip = Config.ShowPerfStrip, showJungleWidget = Config.ShowJungleWidget,
     showSpellTracker = Config.ShowSpellTracker, showBenchBar = Config.ShowBenchBar, minimalHud = Config.MinimalHud,
     ocr = Config.Ocr, ocrAllModes = Config.OcrAllModes, autoLayout = Config.AutoLayout, autoPage = Config.AutoPage,
     autoSpells = Config.AutoSpells, autoRunes = Config.AutoRunes, autoExportShop = Config.AutoExportShop, autoAccept = Config.AutoAccept,
     launchWithWindows = Config.LaunchWithWindows, minimizeToTray = Config.MinimizeToTray,
     hotkeySheet = Config.HotkeySheet, hotkeyOverlay = Config.HotkeyOverlay, hotkeyEdit = Config.HotkeyEdit, hotkeyScan = Config.HotkeyScan,
     flashKey = Config.FlashKey, tierElo = Config.TierElo, tierRole = Config.TierRole,
     buildFocus = Config.BuildFocus, theme = Config.Theme, scale = Config.Layout.Scale, badgeOffsetY = Config.BadgeOffsetY,
     minimapScale = Config.MinimapScale, minimapLeft = Config.MinimapLeft, hudScale = Config.HudScale,
     onboardingSeen = Config.OnboardingSeen,
     panels = Config.Panels.ToDictionary(kv => kv.Key, kv => new { x = kv.Value.X, y = kv.Value.Y })
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
     case "setMode":
      string m = Json.S(msg, "mode");
      if (m == "MAYHEM" || m == "ARAM" || m == "SR") {
       ActiveMode = m; manualModeOverride = true; EnforceModeView();
       TierList = new List<TierListEntry>();
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
     case "selectChampion":
      string slug = Json.S(msg, "slug"); string nextView = Json.S(msg, "view"); string pickRole = Json.S(msg, "role");
      var ch = Champions.FirstOrDefault(x => x.Slug == slug || GameBridge.Norm(x.Name) == GameBridge.Norm(slug));
      if (ch != null) {
       if (nextView != "") View = nextView;
       await SelectChampion(ch, false, pickRole != "" ? pickRole : null);
      }
      break;
     case "setBuildFocus": Config.BuildFocus = Json.S(msg, "focus"); Save(); PushState(); break;
     case "inspectItem": inspectedAugmentId = -1; inspectedItemId = Json.I(msg, "id"); PushState(); break;
     case "closeItem": inspectedItemId = -1; PushState(); break;
     case "inspectAugment": inspectedItemId = -1; inspectedAugmentId = Json.I(msg, "id"); PushState(); break;
     case "closeAugment": inspectedAugmentId = -1; PushState(); break;
     case "togglePlanItem":
      int pid = Json.I(msg, "id"); if (!plannedItems.Add(pid)) plannedItems.Remove(pid); PushState(); break;
     case "clearPlan": plannedItems.Clear(); PushState(); break;
     case "trackAugment":
      int aid = Json.I(msg, "id");
      if (!chosenAugments.Add(aid)) chosenAugments.Remove(aid);
      Config.TrackedAugments = chosenAugments.ToList(); Save(); PushState(); break;
     case "preferAugment":
      int prefId = Json.I(msg, "id"), prefVal = Json.I(msg, "value");
      if (Selected != null) { Config.AugmentPreferences[Selected.Slug + ":" + prefId] = prefVal; Save(); PushState(); }
      break;
     case "scanAugments": await ReadAugments(true); break;
     case "showAugmentBadges": offerSession.Visible = true; PushState(); break;
     case "clearAugments": ResetOfferSession(); PushState(); break;
     case "saveLastScan": SaveLastScanDebug(); break;
     case "refreshProfile": await RefreshProfile(true); break;
     case "refreshDatabases":
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
      Config.Panels[pKey] = new OverlayPanelPosition { X = px, Y = py }; Save(); PushState(); break;
     case "resetPositions": Config.Panels.Clear(); Config.Layout = new Placement(); Config.BadgeOffsetY = 0; Config.MinimapScale = 1.0; Config.HudScale = 1.0; SyncLeagueMinimapConfig(); Save(); StatusText = "Overlay positions reset."; PushState(); break;
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
       if (sfd.ShowDialog(this) == true) { File.WriteAllText(sfd.FileName, Json.Serializer().Serialize(Config), Encoding.UTF8); StatusText = "Config exported to " + Path.GetFileName(sfd.FileName); PushState(); }
      } catch (Exception ex) { StatusText = "Export failed: " + ex.Message; PushState(); }
      break;
     case "importConfig":
      try {
       var ofd = new Microsoft.Win32.OpenFileDialog { Title = "Import maylo settings", Filter = "maylo config (*.json)|*.json" };
       if (ofd.ShowDialog(this) == true) {
        var loaded = Json.Serializer().Deserialize<V2Settings>(File.ReadAllText(ofd.FileName, Encoding.UTF8));
        if (loaded != null) {
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
     case "startSpellCd":
      string cdKey = Json.S(msg, "key"), spName = Json.S(msg, "spell"), cdPlayerKey = Json.S(msg, "playerKey");
      if (cdKey != "") {
       if (spellReadyAt.ContainsKey(cdKey)) { spellReadyAt.Remove(cdKey); PushState(); break; }
       string normSp = GameBridge.Norm(spName);
       double baseCd = normSp.Contains("flash") ? 300 : normSp.Contains("teleport") ? 360 : normSp.Contains("heal") ? 240 : (normSp.Contains("exhaust") || normSp.Contains("cleanse") || normSp.Contains("ghost")) ? 210 : (normSp.Contains("ignite") || normSp.Contains("barrier")) ? 180 : normSp.Contains("smite") ? 90 : normSp.Contains("mark") || normSp.Contains("snowball") ? 80 : 210;
       var pl = Game.Live.Players.FirstOrDefault(x => (x.Team + "|" + x.Name + "|" + x.Champion) == cdPlayerKey);
       double haste = 0;
       // ARAM & ARAM Mayhem grant +70 innate Summoner Spell Haste!
       if (ActiveMode == "ARAM" || ActiveMode == "MAYHEM" || Game.Mayhem) haste += 70;
       if (pl != null) {
        if (pl.Items.Contains(3158)) haste += 10; // Ionian Boots of Lucidity
        bool hasInsp = pl.PrimaryTreeId == 8300 || pl.SecondaryTreeId == 8300
         || String.Equals(pl.RuneTree, "Inspiration", StringComparison.OrdinalIgnoreCase)
         || String.Equals(pl.SecondaryRuneTree, "Inspiration", StringComparison.OrdinalIgnoreCase);
        if (hasInsp) haste += 18; // Cosmic Insight
       }
       double adjCd = baseCd * (100.0 / (100.0 + haste));
       spellReadyAt[cdKey] = Game.Live.Seconds + adjCd;
       PushState();
      }
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
      else if (sk == "showBuildOverlay") Config.ShowBuildOverlay = Convert.ToBoolean(sv);
      else if (sk == "showRerollBar") Config.ShowRerollBar = Convert.ToBoolean(sv);
      else if (sk == "showSkillAdvisor") Config.ShowSkillAdvisor = Convert.ToBoolean(sv);
      else if (sk == "showPerfStrip") Config.ShowPerfStrip = Convert.ToBoolean(sv);
      else if (sk == "showJungleWidget") Config.ShowJungleWidget = Convert.ToBoolean(sv);
      else if (sk == "showSpellTracker") Config.ShowSpellTracker = Convert.ToBoolean(sv);
      else if (sk == "showBenchBar") Config.ShowBenchBar = Convert.ToBoolean(sv);
      else if (sk == "minimalHud") Config.MinimalHud = Convert.ToBoolean(sv);
      else if (sk == "launchWithWindows") { Config.LaunchWithWindows = Convert.ToBoolean(sv); SetStartupRegistry(Config.LaunchWithWindows); }
      else if (sk == "minimizeToTray") Config.MinimizeToTray = Convert.ToBoolean(sv);
      else if (sk == "ocr") Config.Ocr = Convert.ToBoolean(sv);
      else if (sk == "ocrAllModes") Config.OcrAllModes = Convert.ToBoolean(sv);
      else if (sk == "autoAccept") Config.AutoAccept = Convert.ToBoolean(sv);
      else if (sk == "autoSpells") { Config.AutoSpells = Convert.ToBoolean(sv); if (Config.AutoSpells) { Bridge.ResetSpellSelection(); resyncLoadout = true; } }
      else if (sk == "autoRunes") { Config.AutoRunes = Convert.ToBoolean(sv); if (Config.AutoRunes) { Bridge.ResetSpellSelection(); resyncLoadout = true; } }
      else if (sk == "autoExportShop") { Config.AutoExportShop = Convert.ToBoolean(sv); if (Config.AutoExportShop) { Bridge.ResetSpellSelection(); resyncLoadout = true; } }
      else if (sk == "autoPage") Config.AutoPage = Convert.ToBoolean(sv);
      else if (sk == "flashKey") { Config.FlashKey = Convert.ToString(sv).ToUpperInvariant() == "D" ? "D" : "F"; Bridge.ResetSpellSelection(); resyncLoadout = true; }
      else if (sk == "theme") { Config.Theme = Convert.ToString(sv) == "light" ? "light" : "dark"; ApplyWindowTheme(!String.Equals(Config.Theme, "light", StringComparison.OrdinalIgnoreCase)); }
      else if (sk == "scale" && sv != null) Config.Layout.Scale = Math.Max(0.75, Math.Min(1.35, Convert.ToDouble(sv, System.Globalization.CultureInfo.InvariantCulture)));
      else if (sk == "badgeOffsetY" && sv != null) Config.BadgeOffsetY = Math.Max(-0.15, Math.Min(0.15, Convert.ToDouble(sv, System.Globalization.CultureInfo.InvariantCulture)));
      else if (sk == "minimapScale" && sv != null) Config.MinimapScale = Math.Max(0.55, Math.Min(1.85, Convert.ToDouble(sv, System.Globalization.CultureInfo.InvariantCulture)));
      else if (sk == "hudScale" && sv != null) Config.HudScale = Math.Max(0.6, Math.Min(1.5, Convert.ToDouble(sv, System.Globalization.CultureInfo.InvariantCulture)));
      else if (sk == "minimapLeft") { Config.MinimapLeft = Convert.ToBoolean(sv); Config.Panels.Remove("minimap_overlay"); }
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
     case "exportItemSet":
      if (Selected == null) { ImportStatus = "Select a champion first."; PushState(); break; }
      try {
       var bp = CurrentBuildPath();
       ImportStatus = await Bridge.ExportItemSet(
        Selected.Name, ChampionIdOf(Selected), ActiveMode,
        ModeData.Starters.Select(x => x.Id).ToList(),
        bp.Where(x => !x.IsBoots && x.Role != "STARTER").Select(x => x.Stat.Id).ToList(),
        ModeData.Boots.Select(x => x.Id).ToList(),
        ModeData.BestItems.Select(x => x.Id).ToList()
       );
      } catch (Exception ex) { ImportStatus = "Shop export failed: " + ex.Message; }
      PushState(); break;
     case "importRunes":
      try { ImportStatus = await Bridge.ImportRunes(ModeData.Runes); } catch (Exception ex) { ImportStatus = "Rune import failed: " + ex.Message; }
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
    Config.Panels.Remove("minimap_overlay");
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
