using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Shell;
using Microsoft.Win32;
using MayloRelease;
using IOPath = System.IO.Path;

[assembly: AssemblyTitle("MAYLO Setup")]
[assembly: AssemblyDescription("Windows Installer for maylo — League of Legends & ARAM Mayhem Companion")]
[assembly: AssemblyProduct("MAYLO")]
[assembly: AssemblyCompany("maylo")]

namespace MayloSetup {
 static class Dwm {
  [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

  public static void ApplyModernChrome(Window w) {
   WindowChrome.SetWindowChrome(w, new WindowChrome {
    CaptionHeight = 0,
    CornerRadius = new CornerRadius(12),
    GlassFrameThickness = new Thickness(-1),
    ResizeBorderThickness = new Thickness(0),
    UseAeroCaptionButtons = false
   });
   w.SourceInitialized += (s, e) => {
    try {
     var h = new WindowInteropHelper(w).Handle;
     int round = 2; DwmSetWindowAttribute(h, 33, ref round, 4);
     int dark = 1; DwmSetWindowAttribute(h, 20, ref dark, 4);
     int border = 0x002B2725; DwmSetWindowAttribute(h, 34, ref border, 4);
    } catch { }
   };
  }
 }

 static class UiAssets {
  public static ImageSource LoadEmbeddedLogo() {
   try {
    var asm = Assembly.GetExecutingAssembly();
    using (var s = asm.GetManifestResourceStream("maylo.png")) {
     if (s == null) return null;
     var bmp = new BitmapImage();
     bmp.BeginInit();
     bmp.CacheOption = BitmapCacheOption.OnLoad;
     bmp.StreamSource = s;
     bmp.EndInit();
     bmp.Freeze();
     return bmp;
    }
   } catch { return null; }
  }

  public static SolidColorBrush Brush(byte r, byte g, byte b, byte a = 255) {
   var br = new SolidColorBrush(Color.FromArgb(a, r, g, b));
   br.Freeze();
   return br;
  }

  public static ControlTemplate RoundedButtonTemplate(CornerRadius radius, SolidColorBrush hoverBg, SolidColorBrush pressBg) {
   var tpl = new ControlTemplate(typeof(Button));
   var bd = new FrameworkElementFactory(typeof(Border));
   bd.Name = "bd";
   bd.SetValue(Border.CornerRadiusProperty, radius);
   bd.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
   bd.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
   bd.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
   bd.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

   var cp = new FrameworkElementFactory(typeof(ContentPresenter));
   cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
   cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
   bd.AppendChild(cp);
   tpl.VisualTree = bd;

   var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
   hover.Setters.Add(new Setter(Border.BackgroundProperty, hoverBg, "bd"));
   tpl.Triggers.Add(hover);

   var press = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
   press.Setters.Add(new Setter(Border.BackgroundProperty, pressBg, "bd"));
   tpl.Triggers.Add(press);

   var dis = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
   dis.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45, "bd"));
   tpl.Triggers.Add(dis);

   return tpl;
  }

  public static CheckBox CreateToggleSwitch(bool initial) {
   var cb = new CheckBox {
    IsChecked = initial,
    Cursor = Cursors.Hand,
    VerticalAlignment = VerticalAlignment.Center
   };
   var tpl = new ControlTemplate(typeof(CheckBox));
   var track = new FrameworkElementFactory(typeof(Border));
   track.Name = "track";
   track.SetValue(Border.WidthProperty, 38.0);
   track.SetValue(Border.HeightProperty, 22.0);
   track.SetValue(Border.CornerRadiusProperty, new CornerRadius(11));
   track.SetValue(Border.BackgroundProperty, Brush(39, 41, 52));
   track.SetValue(Border.BorderBrushProperty, Brush(255, 255, 255, 24));
   track.SetValue(Border.BorderThicknessProperty, new Thickness(1));

   var grid = new FrameworkElementFactory(typeof(Grid));
   var thumb = new FrameworkElementFactory(typeof(Border));
   thumb.Name = "thumb";
   thumb.SetValue(Border.WidthProperty, 16.0);
   thumb.SetValue(Border.HeightProperty, 16.0);
   thumb.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
   thumb.SetValue(Border.BackgroundProperty, Brush(212, 212, 216));
   thumb.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Left);
   thumb.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Center);
   thumb.SetValue(Border.MarginProperty, new Thickness(2, 0, 2, 0));
   grid.AppendChild(thumb);
   track.AppendChild(grid);
   tpl.VisualTree = track;

   var on = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
   on.Setters.Add(new Setter(Border.BackgroundProperty, Brush(99, 102, 241), "track"));
   on.Setters.Add(new Setter(Border.BorderBrushProperty, Brush(129, 140, 248, 160), "track"));
   on.Setters.Add(new Setter(Border.HorizontalAlignmentProperty, HorizontalAlignment.Right, "thumb"));
   on.Setters.Add(new Setter(Border.BackgroundProperty, Brushes.White, "thumb"));
   tpl.Triggers.Add(on);

   var dis = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
   dis.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45, "track"));
   tpl.Triggers.Add(dis);

   cb.Template = tpl;
   return cb;
  }
 }

 public static class InstallerEngine {
  public const string Version = BuildInfo.Version;
  const string UninstallRegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\MAYLO";

  public static string DefaultInstallDir {
   get { return IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MAYLO"); }
  }

  public static string GetInstalledDir() {
   try {
    using (var key = Registry.CurrentUser.OpenSubKey(UninstallRegKey)) {
     if (key != null) {
      var val = key.GetValue("InstallLocation") as string;
      if (!String.IsNullOrWhiteSpace(val)) return val;
     }
    }
   } catch { }
   return DefaultInstallDir;
  }

  public static bool IsWebView2Installed() {
   string[] paths = {
    @"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
    @"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"
   };
   foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser }) {
    foreach (var p in paths) {
     try {
      using (var k = root.OpenSubKey(p)) {
       System.Version version;
       if (k != null && System.Version.TryParse(k.GetValue("pv") as string, out version) && version > new System.Version(0, 0, 0, 0)) return true;
      }
     } catch { }
    }
   }
   return false;
  }

  public static long GetPayloadUncompressedBytes() {
   try {
    var asm = Assembly.GetExecutingAssembly();
    using (var res = asm.GetManifestResourceStream("maylo-payload.zip")) {
     if (res == null) return 0;
     using (var zip = new ZipArchive(res, ZipArchiveMode.Read)) {
      long sum = 0;
      foreach (var e in zip.Entries) sum += e.Length;
      return sum;
     }
    }
   } catch { return 0; }
  }

  public static void Install(string targetDir, Action<int, string> progress = null) {
   var asm = Assembly.GetExecutingAssembly();
   var release = ReleaseVersion.Parse(Version);
   using (InstallStore.Lock(targetDir))
   using (var res = asm.GetManifestResourceStream("maylo-payload.zip")) {
    if (res == null) throw new Exception("Embedded MAYLO payload not found.");
    var launcher = IOPath.Combine(targetDir, InstallStore.Launcher);
    var current = InstallStore.Current(targetDir);
    if (File.Exists(launcher) && current == null) throw new IOException("Choose a folder without another MAYLO.exe.");
    if (current == null || current.CompareTo(release) < 0) {
     InstallStore.Stage(targetDir, release, res, progress);
     InstallStore.Activate(targetDir, release);
    }
    if (!File.Exists(launcher)) File.Copy(asm.Location, launcher);
   }
  }

  public static void LaunchCurrent(string root) {
   using (InstallStore.Lock(root)) {
    var current = InstallStore.Current(root);
    var previous = InstallStore.Current(root, true);
    var release = current ?? previous;
    if (release == null) throw new IOException("No valid MAYLO version is installed.");
    try {
     using (var app = InstallStore.Launch(root, release)) {
      if (app.WaitForExit(2500) && app.ExitCode != 0) throw new IOException("MAYLO could not start.");
     }
    } catch (Exception) {
     if (previous == null || previous.Text == release.Text) throw;
     using (InstallStore.Launch(root, previous)) { }
     release = previous;
    }
    if (current == null || current.Text != release.Text) InstallStore.Activate(root, release);
    using (var key = Registry.CurrentUser.OpenSubKey(UninstallRegKey, true)) {
     if (key != null && String.Equals(key.GetValue("InstallLocation") as string, root, StringComparison.OrdinalIgnoreCase))
      key.SetValue("DisplayVersion", release.Text);
    }
   }
  }

  public static void CreateShortcut(string shortcutPath, string targetExe, string workDir, string description, string arguments = "") {
   try {
    Directory.CreateDirectory(IOPath.GetDirectoryName(shortcutPath));
    Type shellType = Type.GetTypeFromProgID("WScript.Shell");
    dynamic shell = Activator.CreateInstance(shellType);
    dynamic sc = shell.CreateShortcut(shortcutPath);
    sc.TargetPath = targetExe;
    sc.WorkingDirectory = workDir;
    sc.Description = description;
    if (!String.IsNullOrEmpty(arguments)) sc.Arguments = arguments;
    sc.IconLocation = targetExe + ",0";
    sc.Save();
   } catch { }
  }

  static bool InstallationIsRunning(string root) {
   string prefix = IOPath.GetFullPath(InstallStore.Versions(root)).TrimEnd('\\') + "\\";
   foreach (var p in Process.GetProcessesByName("MAYLO-Hub")) {
    try { if (p.MainModule.FileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true; }
    catch (InvalidOperationException) { }
    finally { p.Dispose(); }
   }
   return false;
  }

  public static void RegisterUninstall(string installDir, string exePath, string uninstallerPath, int estimatedSizeKb) {
   try {
    using (var key = Registry.CurrentUser.CreateSubKey(UninstallRegKey)) {
     if (key == null) return;
     key.SetValue("DisplayName", "maylo · League & ARAM Mayhem Companion");
     var installed = InstallStore.Current(installDir);
     key.SetValue("DisplayVersion", installed == null ? Version : installed.Text);
     key.SetValue("Publisher", "maylo");
     key.SetValue("InstallLocation", installDir);
     key.SetValue("DisplayIcon", exePath + ",0");
     key.SetValue("UninstallString", "\"" + uninstallerPath + "\" --uninstall");
     key.SetValue("EstimatedSize", Math.Max(1024, estimatedSizeKb), RegistryValueKind.DWord);
     key.SetValue("NoModify", 1, RegistryValueKind.DWord);
     key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }
   } catch { }
  }

  public static void SetStartup(string exePath, bool enabled) {
   try {
    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)) {
     if (key == null) return;
     if (enabled && !String.IsNullOrEmpty(exePath)) key.SetValue("MAYLO", "\"" + exePath + "\"");
     else if (key.GetValue("MAYLO") != null) key.DeleteValue("MAYLO", false);
    }
   } catch { }
  }

  public static void Uninstall(string installDir, bool removeUserData) {
   if (!File.Exists(IOPath.Combine(installDir, InstallStore.Launcher)) || InstallStore.Current(installDir) == null)
    throw new IOException("MAYLO installation not found.");
   if (InstallationIsRunning(installDir)) throw new IOException("Quit MAYLO from its tray icon, then try again.");
   using (InstallStore.Lock(installDir)) {
    foreach (var dir in Directory.GetDirectories(InstallStore.Versions(installDir))) {
     if (ReleaseVersion.Parse(IOPath.GetFileName(dir)) != null && (File.GetAttributes(dir) & FileAttributes.ReparsePoint) == 0)
      Directory.Delete(dir, true);
    }
    foreach (var file in new[] { InstallStore.Launcher, "current.txt", "previous.txt" })
     File.Delete(IOPath.Combine(installDir, file));
   }
   File.Delete(IOPath.Combine(installDir, ".install.lock"));
   if (Directory.GetFileSystemEntries(InstallStore.Versions(installDir)).Length == 0) Directory.Delete(InstallStore.Versions(installDir));
   if (Directory.GetFileSystemEntries(installDir).Length == 0) Directory.Delete(installDir);
   SetStartup("", false);
   try { Registry.CurrentUser.DeleteSubKeyTree(UninstallRegKey, false); } catch { }
   string desktopLink = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "MAYLO.lnk");
   string startDir = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "MAYLO");
   try { if (File.Exists(desktopLink)) File.Delete(desktopLink); } catch { }
   try { if (Directory.Exists(startDir)) Directory.Delete(startDir, true); } catch { }
   if (removeUserData) {
    string cacheDir = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MayhemCompanion");
    try { if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true); } catch { }
   }
  }
 }

 public class SetupWindow : Window {
  readonly TextBox txtDir;
  readonly Button btnBrowse, btnInstall, btnCancel;
  readonly CheckBox chkDesktop, chkStartMenu, chkStartup, chkLaunch;
  readonly Border progressFill, progressTrack, optionsCard, doneCard;
  readonly TextBlock lblStatus, lblPct, lblDonePath;
  string installedExePath = "";
  bool finished = false;
  bool installing;

  public SetupWindow(bool diagnostics = false) {
   Title = "MAYLO — Installation";
   Width = 560;
   Height = 616;
   MaxHeight = Math.Max(300, SystemParameters.WorkArea.Height - 16);
   WindowStyle = WindowStyle.None;
   ResizeMode = ResizeMode.NoResize;
   WindowStartupLocation = WindowStartupLocation.CenterScreen;
   Background = UiAssets.Brush(11, 12, 16);
   Foreground = Brushes.White;
   FontFamily = new FontFamily("Segoe UI");
   Dwm.ApplyModernChrome(this);
   Closing += (s, e) => { if (installing) e.Cancel = true; };

   var outer = new Border {
    Background = UiAssets.Brush(11, 12, 16),
    BorderBrush = UiAssets.Brush(255, 255, 255, 20),
    BorderThickness = new Thickness(1),
    CornerRadius = new CornerRadius(12),
    ClipToBounds = true
   };

   var mainGrid = new Grid();
   mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3) });    // Top accent bar
   mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(46) });   // Draggable title bar
   mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Body
   mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });      // Footer

   // 1. Subtle Indigo Top Hairline Accent
   var topAccent = new Border {
    Background = new LinearGradientBrush(
     Color.FromRgb(99, 102, 241),
     Color.FromRgb(56, 189, 248),
     new Point(0, 0), new Point(1, 0))
   };
   Grid.SetRow(topAccent, 0);
   mainGrid.Children.Add(topAccent);

   // 2. Draggable Title Bar
   var titleBar = new Grid { Background = UiAssets.Brush(11, 12, 16, 1) };
   titleBar.MouseLeftButtonDown += (s, e) => {
    if (e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { } }
   };
   var titleLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24, 0, 0, 0) };
   titleLeft.Children.Add(new TextBlock {
    Text = "MAYLO POUR WINDOWS",
    FontFamily = new FontFamily("Consolas"),
    FontSize = 10,
    FontWeight = FontWeights.SemiBold,
    Foreground = UiAssets.Brush(129, 140, 248)
   });
   titleBar.Children.Add(titleLeft);

   var winBtns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
   var btnMin = MakeCaptionButton("—", UiAssets.Brush(255, 255, 255, 18), UiAssets.Brush(255, 255, 255, 32));
   btnMin.Click += (s, e) => WindowState = WindowState.Minimized;
   var btnX = MakeCaptionButton("✕", UiAssets.Brush(239, 68, 68, 200), UiAssets.Brush(220, 38, 38, 230));
   btnX.Click += (s, e) => Close();
   winBtns.Children.Add(btnMin);
   winBtns.Children.Add(btnX);
   titleBar.Children.Add(winBtns);
   Grid.SetRow(titleBar, 1);
   mainGrid.Children.Add(titleBar);

   // 3. Body Container
   var body = new StackPanel { Margin = new Thickness(26, 4, 26, 12) };

   // Brand Hero Row
   var hero = new Grid { Margin = new Thickness(14, 12, 14, 12) };
   hero.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
   hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

   var logoTile = new Border {
    Width = 68, Height = 68,
    CornerRadius = new CornerRadius(18),
    Margin = new Thickness(0, 0, 16, 0)
   };
   var logoImg = UiAssets.LoadEmbeddedLogo();
   if (logoImg != null) {
    logoTile.Child = new Image { Source = logoImg, Stretch = Stretch.Uniform };
    var floatTransform = new TranslateTransform();
    logoTile.RenderTransform = floatTransform;
    if (SystemParameters.ClientAreaAnimation) {
     var floatAnimation = new DoubleAnimation(3, -5, TimeSpan.FromMilliseconds(1350)) {
      AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase()
     };
     Loaded += (s, e) => floatTransform.BeginAnimation(TranslateTransform.YProperty, floatAnimation);
    }
   } else {
    logoTile.Child = new TextBlock { Text = "m", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
   }
   Grid.SetColumn(logoTile, 0);
   hero.Children.Add(logoTile);

   var heroText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
   var brandRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
   brandRow.Children.Add(new TextBlock { Text = "maylo", FontSize = 25, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
   brandRow.Children.Add(new Border { Width = 1, Height = 16, Background = UiAssets.Brush(255, 255, 255, 35), Margin = new Thickness(11, 2, 11, 0), VerticalAlignment = VerticalAlignment.Center });
   var verPill = new Border {
    Background = UiAssets.Brush(99, 102, 241, 38),
    BorderBrush = UiAssets.Brush(129, 140, 248, 115),
    BorderThickness = new Thickness(1),
    CornerRadius = new CornerRadius(6),
    Padding = new Thickness(7, 2, 7, 2),
    VerticalAlignment = VerticalAlignment.Center
   };
   verPill.Child = new TextBlock { Text = "v" + InstallerEngine.Version, FontFamily = new FontFamily("Consolas"), FontSize = 11, FontWeight = FontWeights.Bold, Foreground = UiAssets.Brush(165, 180, 252) };
   brandRow.Children.Add(verPill);
   heroText.Children.Add(brandRow);
   heroText.Children.Add(new TextBlock {
    Text = "Ton compagnon pour League of Legends",
    FontSize = 12,
    Foreground = UiAssets.Brush(161, 161, 170),
    Margin = new Thickness(0, 3, 0, 0)
   });
   Grid.SetColumn(heroText, 1);
   hero.Children.Add(heroText);
   body.Children.Add(new Border {
    Child = hero, CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1),
    BorderBrush = UiAssets.Brush(129, 140, 248, 60), Margin = new Thickness(0, 0, 0, 12),
    Background = new LinearGradientBrush(Color.FromRgb(33, 31, 65), Color.FromRgb(17, 19, 29), 14)
   });

   // Readiness Status Pills
   bool wv2 = InstallerEngine.IsWebView2Installed();
   long bytes = InstallerEngine.GetPayloadUncompressedBytes();
   string sizeMb = bytes > 0 ? String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.0} MB", bytes / (1024.0 * 1024.0)) : "~5.0 MB";
   var pills = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
   var runtime = MakeStatusPill(wv2 ? "WebView2 prêt" : "Télécharger WebView2", wv2 ? Color.FromRgb(52, 211, 153) : Color.FromRgb(251, 191, 36));
   if (!wv2) {
    runtime.Cursor = Cursors.Hand;
    runtime.MouseLeftButtonUp += (s, e) => Process.Start(new ProcessStartInfo("https://developer.microsoft.com/microsoft-edge/webview2/") { UseShellExecute = true });
   }
   pills.Children.Add(runtime);
   pills.Children.Add(MakeStatusPill("Mises à jour automatiques", Color.FromRgb(129, 140, 248)));
   pills.Children.Add(MakeStatusPill(sizeMb, Color.FromRgb(161, 161, 170)));
   body.Children.Add(pills);

   // Install Path Card
   var dirCard = new Border {
    Background = UiAssets.Brush(18, 20, 27),
    BorderBrush = UiAssets.Brush(255, 255, 255, 16),
    BorderThickness = new Thickness(1),
    CornerRadius = new CornerRadius(12),
    Padding = new Thickness(14, 10, 14, 12),
    Margin = new Thickness(0, 0, 0, 12)
   };
   var dirStack = new StackPanel();
   dirStack.Children.Add(new TextBlock {
    Text = "DOSSIER D’INSTALLATION",
    FontFamily = new FontFamily("Consolas"),
    FontSize = 10,
    FontWeight = FontWeights.SemiBold,
    Foreground = UiAssets.Brush(113, 113, 122),
    Margin = new Thickness(0, 0, 0, 6)
   });
   var dirRow = new Grid();
   dirRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
   dirRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

   txtDir = new TextBox {
    Text = diagnostics ? @"%LocalAppData%\MAYLO" : InstallerEngine.GetInstalledDir(),
    Background = UiAssets.Brush(12, 13, 18),
    Foreground = UiAssets.Brush(228, 228, 231),
    BorderBrush = UiAssets.Brush(255, 255, 255, 20),
    BorderThickness = new Thickness(1),
    Padding = new Thickness(10, 6, 10, 6),
    FontFamily = new FontFamily("Consolas"),
    FontSize = 11.5,
    VerticalContentAlignment = VerticalAlignment.Center
   };
   Grid.SetColumn(txtDir, 0);
   dirRow.Children.Add(txtDir);

   btnBrowse = new Button {
    Content = "Parcourir…",
    Height = 30,
    Padding = new Thickness(12, 0, 12, 0),
    Margin = new Thickness(8, 0, 0, 0),
    Background = UiAssets.Brush(30, 32, 43),
    Foreground = UiAssets.Brush(228, 228, 231),
    BorderBrush = UiAssets.Brush(255, 255, 255, 26),
    BorderThickness = new Thickness(1),
    FontSize = 11.5,
    FontWeight = FontWeights.Medium,
    Cursor = Cursors.Hand,
    Template = UiAssets.RoundedButtonTemplate(new CornerRadius(7), UiAssets.Brush(42, 45, 59), UiAssets.Brush(52, 56, 72))
   };
   btnBrowse.Click += (s, e) => BrowseFolder();
   Grid.SetColumn(btnBrowse, 1);
   dirRow.Children.Add(btnBrowse);
   dirStack.Children.Add(dirRow);
   dirCard.Child = dirStack;
   body.Children.Add(dirCard);

   // Options Card
   optionsCard = new Border {
    Background = UiAssets.Brush(18, 20, 27),
    BorderBrush = UiAssets.Brush(255, 255, 255, 16),
    BorderThickness = new Thickness(1),
    CornerRadius = new CornerRadius(12),
    Padding = new Thickness(16, 12, 16, 12)
   };
   var opts = new StackPanel();
   chkDesktop = UiAssets.CreateToggleSwitch(true);
   chkStartMenu = UiAssets.CreateToggleSwitch(true);
   chkStartup = UiAssets.CreateToggleSwitch(false);
   chkLaunch = UiAssets.CreateToggleSwitch(true);
   opts.Children.Add(MakeOptionRow("Raccourci sur le Bureau", "Accéder à MAYLO en un clic", chkDesktop, true));
   opts.Children.Add(MakeOptionRow("Menu Démarrer", "Ajouter MAYLO et son raccourci de désinstallation", chkStartMenu, true));
   opts.Children.Add(MakeOptionRow("Lancer avec Windows", "Ouvrir MAYLO à l’ouverture de ta session", chkStartup, true));
   opts.Children.Add(MakeOptionRow("Ouvrir après l’installation", "Lancer MAYLO dès que tout est prêt", chkLaunch, false));
   optionsCard.Child = opts;
   body.Children.Add(optionsCard);

   // Completion Card (hidden until finished)
   doneCard = new Border {
    Visibility = Visibility.Collapsed,
    Background = UiAssets.Brush(16, 28, 24),
    BorderBrush = UiAssets.Brush(52, 211, 153, 85),
    BorderThickness = new Thickness(1),
    CornerRadius = new CornerRadius(12),
    Padding = new Thickness(18, 16, 18, 16)
   };
   var doneStack = new StackPanel();
   var doneHead = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
   var okDot = new Border {
    Width = 24, Height = 24, CornerRadius = new CornerRadius(12),
    Background = UiAssets.Brush(16, 185, 129, 45),
    BorderBrush = UiAssets.Brush(52, 211, 153, 160),
    BorderThickness = new Thickness(1),
    Margin = new Thickness(0, 0, 10, 0)
   };
   okDot.Child = new TextBlock { Text = "✓", Foreground = UiAssets.Brush(52, 211, 153), FontWeight = FontWeights.Bold, FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
   doneHead.Children.Add(okDot);
   doneHead.Children.Add(new TextBlock { Text = "MAYLO est prêt à jouer", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
   doneStack.Children.Add(doneHead);
   lblDonePath = new TextBlock { Text = "", FontFamily = new FontFamily("Consolas"), FontSize = 11, Foreground = UiAssets.Brush(167, 243, 208), Margin = new Thickness(34, 6, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
   doneStack.Children.Add(lblDonePath);
   doneStack.Children.Add(new TextBlock {
    Text = "Dans League, maintiens Ctrl+Tab ou utilise Alt+O pour placer tes widgets.",
    FontSize = 11.5, Foreground = UiAssets.Brush(209, 213, 219), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(34, 8, 0, 0)
   });
   doneCard.Child = doneStack;
   body.Children.Add(doneCard);

   // Progress Strip
   var progWrap = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
   var progLabels = new Grid { Margin = new Thickness(0, 0, 0, 6) };
   lblStatus = new TextBlock { Text = "Prêt à installer", FontSize = 11.5, Foreground = UiAssets.Brush(161, 161, 170), TextTrimming = TextTrimming.CharacterEllipsis };
   lblPct = new TextBlock { Text = "0%", FontFamily = new FontFamily("Consolas"), FontSize = 11, FontWeight = FontWeights.Bold, Foreground = UiAssets.Brush(165, 180, 252), HorizontalAlignment = HorizontalAlignment.Right };
   progLabels.Children.Add(lblStatus);
   progLabels.Children.Add(lblPct);
   progWrap.Children.Add(progLabels);

   progressTrack = new Border {
    Height = 6,
    CornerRadius = new CornerRadius(3),
    Background = UiAssets.Brush(32, 34, 44),
    ClipToBounds = true
   };
   progressFill = new Border {
    Width = 0,
    Height = 6,
    CornerRadius = new CornerRadius(3),
    HorizontalAlignment = HorizontalAlignment.Left,
    Background = new LinearGradientBrush(Color.FromRgb(99, 102, 241), Color.FromRgb(56, 189, 248), new Point(0, 0), new Point(1, 0))
   };
   progressTrack.Child = progressFill;
   progWrap.Children.Add(progressTrack);
   body.Children.Add(progWrap);

   var bodyScroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
   Grid.SetRow(bodyScroll, 2);
   mainGrid.Children.Add(bodyScroll);

   // 4. Footer
   var footerBar = new Border {
    Background = UiAssets.Brush(14, 15, 21),
    BorderBrush = UiAssets.Brush(255, 255, 255, 14),
    BorderThickness = new Thickness(0, 1, 0, 0),
    Padding = new Thickness(26, 14, 26, 16)
   };
   var footerGrid = new Grid();
   footerGrid.Children.Add(new TextBlock {
    Text = "Installation pour cet utilisateur Windows",
    FontFamily = new FontFamily("Consolas"),
    FontSize = 10.5,
    Foreground = UiAssets.Brush(113, 113, 122),
    VerticalAlignment = VerticalAlignment.Center
   });

   var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
   btnCancel = new Button {
    Content = "Annuler",
    MinWidth = 88, Height = 36,
    Padding = new Thickness(16, 0, 16, 0),
    Margin = new Thickness(0, 0, 10, 0),
    Background = UiAssets.Brush(27, 29, 38),
    Foreground = UiAssets.Brush(212, 212, 216),
    BorderBrush = UiAssets.Brush(255, 255, 255, 24),
    BorderThickness = new Thickness(1),
    FontSize = 12.5,
    FontWeight = FontWeights.Medium,
    Cursor = Cursors.Hand,
    Template = UiAssets.RoundedButtonTemplate(new CornerRadius(9), UiAssets.Brush(38, 41, 54), UiAssets.Brush(48, 52, 68))
   };
   btnCancel.Click += (s, e) => Close();

   btnInstall = new Button {
    Content = "Installer MAYLO",
    MinWidth = 148, Height = 36,
    Padding = new Thickness(18, 0, 18, 0),
    Background = UiAssets.Brush(99, 102, 241),
    Foreground = Brushes.White,
    BorderBrush = UiAssets.Brush(129, 140, 248, 140),
    BorderThickness = new Thickness(1),
    FontSize = 12.5,
    FontWeight = FontWeights.SemiBold,
    Cursor = Cursors.Hand,
    Template = UiAssets.RoundedButtonTemplate(new CornerRadius(9), UiAssets.Brush(79, 70, 229), UiAssets.Brush(67, 56, 202))
   };
   btnInstall.Click += async (s, e) => {
    if (finished) {
     LaunchInstalledAndClose();
    } else {
     await RunInstall();
    }
   };
   btns.Children.Add(btnCancel);
   btns.Children.Add(btnInstall);
   footerGrid.Children.Add(btns);
   footerBar.Child = footerGrid;
   Grid.SetRow(footerBar, 3);
   mainGrid.Children.Add(footerBar);

   outer.Child = mainGrid;
   Content = outer;
  }

  Button MakeCaptionButton(string glyph, SolidColorBrush hoverBg, SolidColorBrush pressBg) {
   return new Button {
    Content = glyph,
    Width = 30, Height = 26,
    Margin = new Thickness(4, 0, 0, 0),
    Background = Brushes.Transparent,
    Foreground = UiAssets.Brush(161, 161, 170),
    BorderThickness = new Thickness(0),
    FontSize = 11,
    Cursor = Cursors.Hand,
    Template = UiAssets.RoundedButtonTemplate(new CornerRadius(6), hoverBg, pressBg)
   };
  }

  Border MakeStatusPill(string text, Color dotColor) {
   var p = new Border {
    Background = UiAssets.Brush(20, 22, 30),
    BorderBrush = UiAssets.Brush(255, 255, 255, 16),
    BorderThickness = new Thickness(1),
    CornerRadius = new CornerRadius(99),
    Padding = new Thickness(9, 4, 10, 4),
    Margin = new Thickness(0, 0, 7, 0)
   };
   var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
   sp.Children.Add(new Ellipse { Width = 6, Height = 6, Fill = new SolidColorBrush(dotColor), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
   sp.Children.Add(new TextBlock { Text = text, FontSize = 10.5, FontWeight = FontWeights.Medium, Foreground = UiAssets.Brush(212, 212, 216) });
   p.Child = sp;
   return p;
  }

  Border MakeOptionRow(string title, string subtitle, CheckBox toggle, bool bottomBorder) {
   var row = new Border {
    BorderBrush = UiAssets.Brush(255, 255, 255, bottomBorder ? (byte)12 : (byte)0),
    BorderThickness = new Thickness(0, 0, 0, bottomBorder ? 1 : 0),
    Padding = new Thickness(0, 7, 0, bottomBorder ? 8 : 2)
   };
   var g = new Grid();
   g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
   g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

   var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
   text.Children.Add(new TextBlock { Text = title, FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = UiAssets.Brush(244, 244, 245) });
   text.Children.Add(new TextBlock { Text = subtitle, FontSize = 11, Foreground = UiAssets.Brush(138, 138, 150), Margin = new Thickness(0, 1, 0, 0) });
   Grid.SetColumn(text, 0);
   g.Children.Add(text);

   Grid.SetColumn(toggle, 1);
   g.Children.Add(toggle);
   row.Child = g;
   return row;
  }

  void BrowseFolder() {
   using (var dlg = new System.Windows.Forms.FolderBrowserDialog()) {
    dlg.Description = "Choisir le dossier d’installation de MAYLO";
    dlg.SelectedPath = Directory.Exists(txtDir.Text) ? txtDir.Text : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK && !String.IsNullOrWhiteSpace(dlg.SelectedPath)) {
     string chosen = dlg.SelectedPath;
     if (!chosen.EndsWith("MAYLO", StringComparison.OrdinalIgnoreCase)) chosen = IOPath.Combine(chosen, "MAYLO");
     txtDir.Text = chosen;
    }
   }
  }

  void SetProgress(int pct, string text) {
   int clamped = Math.Max(0, Math.Min(100, pct));
   lblStatus.Text = text;
   lblPct.Text = clamped + "%";
   double trackW = progressTrack.ActualWidth > 10 ? progressTrack.ActualWidth : 506;
   double targetWidth = trackW * (clamped / 100.0);
   if (SystemParameters.ClientAreaAnimation) {
    var slide = new DoubleAnimation(progressFill.Width, targetWidth, TimeSpan.FromMilliseconds(160)) {
     EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
     FillBehavior = FillBehavior.Stop
    };
    progressFill.Width = targetWidth;
    progressFill.BeginAnimation(FrameworkElement.WidthProperty, slide);
   } else progressFill.Width = targetWidth;
  }

  void LaunchInstalledAndClose() {
   try {
    if (!String.IsNullOrEmpty(installedExePath) && File.Exists(installedExePath)) {
     var started = Process.Start(new ProcessStartInfo(installedExePath) {
      WorkingDirectory = IOPath.GetDirectoryName(installedExePath),
      UseShellExecute = true
     });
     if (started != null) started.Dispose();
    }
   } catch { }
   Close();
  }

  async Task RunInstall() {
   string dir = (txtDir.Text ?? "").Trim();
   if (String.IsNullOrWhiteSpace(dir)) dir = InstallerEngine.DefaultInstallDir;
   bool desktop = chkDesktop.IsChecked == true;
   bool startMenu = chkStartMenu.IsChecked == true;
   bool startup = chkStartup.IsChecked == true;
   bool autoLaunch = chkLaunch.IsChecked == true;

   btnInstall.IsEnabled = false;
   btnBrowse.IsEnabled = false;
   txtDir.IsEnabled = false;
   chkDesktop.IsEnabled = chkStartMenu.IsEnabled = chkStartup.IsEnabled = chkLaunch.IsEnabled = false;

   try {
    installing = true;
    btnCancel.IsEnabled = false;
    dir = IOPath.GetFullPath(dir);
    string exePath = IOPath.Combine(dir, InstallStore.Launcher);
    SetProgress(8, "Préparation des fichiers…");
    long bytes = InstallerEngine.GetPayloadUncompressedBytes();
    int estKb = (int)Math.Max(1024, bytes / 1024);

    await Task.Run(() => {
     InstallerEngine.Install(dir, (pct, msg) => Dispatcher.Invoke(() => SetProgress(pct, msg)));
     Dispatcher.Invoke(() => SetProgress(90, "Création des raccourcis Windows…"));
     if (desktop) {
      InstallerEngine.CreateShortcut(
       IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "MAYLO.lnk"),
       exePath, dir, "maylo · League of Legends & ARAM Mayhem Companion");
     }
     if (startMenu) {
      string sm = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "MAYLO");
      InstallerEngine.CreateShortcut(IOPath.Combine(sm, "MAYLO.lnk"), exePath, dir, "maylo · League of Legends & ARAM Mayhem Companion");
      InstallerEngine.CreateShortcut(IOPath.Combine(sm, "Uninstall MAYLO.lnk"), exePath, dir, "Désinstaller MAYLO", "--uninstall");
     }
     InstallerEngine.RegisterUninstall(dir, exePath, exePath, estKb);
     InstallerEngine.SetStartup(exePath, startup);
    });

    installedExePath = exePath;
    finished = true;
    installing = false;
    btnCancel.IsEnabled = true;
    SetProgress(100, "Installation terminée");
    optionsCard.Visibility = Visibility.Collapsed;
    lblDonePath.Text = dir;
    doneCard.Visibility = Visibility.Visible;
    btnCancel.Content = "Fermer";
    btnInstall.Content = "Ouvrir MAYLO";
    btnInstall.IsEnabled = true;

    if (autoLaunch) {
     await Task.Delay(550);
     LaunchInstalledAndClose();
    }
   } catch (Exception ex) {
    installing = false;
    btnCancel.IsEnabled = true;
    SetProgress(0, "Installation interrompue. " + ex.Message);
    btnInstall.IsEnabled = true;
    btnBrowse.IsEnabled = true;
    txtDir.IsEnabled = true;
    chkDesktop.IsEnabled = chkStartMenu.IsEnabled = chkStartup.IsEnabled = chkLaunch.IsEnabled = true;
   }
  }
 }

 public class UninstallWindow : Window {
  readonly CheckBox chkPurge;
  readonly TextBlock statusText;
  readonly Button btnRemove, btnCancel;
  readonly string installDir;

  public UninstallWindow(string dir) {
   installDir = dir;
   Title = "Uninstall maylo";
   Width = 460;
   Height = 285;
   WindowStyle = WindowStyle.None;
   ResizeMode = ResizeMode.NoResize;
   WindowStartupLocation = WindowStartupLocation.CenterScreen;
   Background = UiAssets.Brush(11, 12, 16);
   Foreground = Brushes.White;
   FontFamily = new FontFamily("Segoe UI");
   Dwm.ApplyModernChrome(this);

   var outer = new Border {
    Background = UiAssets.Brush(11, 12, 16),
    BorderBrush = UiAssets.Brush(255, 255, 255, 22),
    BorderThickness = new Thickness(1),
    CornerRadius = new CornerRadius(12),
    ClipToBounds = true
   };

   var grid = new Grid();
   grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3) });
   grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
   grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

   var top = new Border { Background = UiAssets.Brush(239, 68, 68) };
   Grid.SetRow(top, 0);
   grid.Children.Add(top);

   var body = new StackPanel { Margin = new Thickness(24, 20, 24, 16) };
   body.MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { } } };

   var head = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
   var logo = UiAssets.LoadEmbeddedLogo();
   if (logo != null) {
    var tile = new Border { Width = 38, Height = 38, CornerRadius = new CornerRadius(10), Background = UiAssets.Brush(20, 22, 31), BorderBrush = UiAssets.Brush(255, 255, 255, 20), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 12, 0) };
    tile.Child = new Image { Source = logo, Width = 26, Height = 26 };
    head.Children.Add(tile);
   }
   var titles = new StackPanel();
   titles.Children.Add(new TextBlock { Text = "Désinstaller MAYLO", FontSize = 17, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
   titles.Children.Add(new TextBlock { Text = installDir, FontFamily = new FontFamily("Consolas"), FontSize = 10.5, Foreground = UiAssets.Brush(138, 138, 150), TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 340 });
   head.Children.Add(titles);
   body.Children.Add(head);

   statusText = new TextBlock {
    Text = "Cette action retire MAYLO, ses raccourcis et son lancement au démarrage de Windows.",
    FontSize = 12, Foreground = UiAssets.Brush(190, 190, 200), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 16, 0, 14)
   };
   body.Children.Add(statusText);

   var opt = new Border {
    Background = UiAssets.Brush(18, 20, 27), BorderBrush = UiAssets.Brush(255, 255, 255, 16), BorderThickness = new Thickness(1),
    CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 10, 12, 10)
   };
   var row = new Grid();
   row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
   row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
   var txt = new StackPanel();
   txt.Children.Add(new TextBlock { Text = "Supprimer aussi les réglages et le cache", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = UiAssets.Brush(235, 235, 240) });
   txt.Children.Add(new TextBlock { Text = "Efface les préférences, les notes et les icônes en cache", FontSize = 10.5, Foreground = UiAssets.Brush(138, 138, 150) });
   Grid.SetColumn(txt, 0); row.Children.Add(txt);
   chkPurge = UiAssets.CreateToggleSwitch(false);
   Grid.SetColumn(chkPurge, 1); row.Children.Add(chkPurge);
   opt.Child = row;
   body.Children.Add(opt);

   Grid.SetRow(body, 1);
   grid.Children.Add(body);

   var footer = new Border {
    Background = UiAssets.Brush(14, 15, 21), BorderBrush = UiAssets.Brush(255, 255, 255, 14), BorderThickness = new Thickness(0, 1, 0, 0),
    Padding = new Thickness(24, 12, 24, 14)
   };
   var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
   btnCancel = new Button {
    Content = "Annuler", MinWidth = 86, Height = 34, Margin = new Thickness(0, 0, 10, 0),
    Background = UiAssets.Brush(27, 29, 38), Foreground = UiAssets.Brush(212, 212, 216),
    BorderBrush = UiAssets.Brush(255, 255, 255, 24), BorderThickness = new Thickness(1), FontSize = 12, Cursor = Cursors.Hand,
    Template = UiAssets.RoundedButtonTemplate(new CornerRadius(8), UiAssets.Brush(38, 41, 54), UiAssets.Brush(48, 52, 68))
   };
   btnCancel.Click += (s, e) => Close();

   btnRemove = new Button {
    Content = "Désinstaller", MinWidth = 112, Height = 34,
    Background = UiAssets.Brush(220, 38, 38), Foreground = Brushes.White,
    BorderBrush = UiAssets.Brush(248, 113, 113, 120), BorderThickness = new Thickness(1), FontSize = 12, FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand,
    Template = UiAssets.RoundedButtonTemplate(new CornerRadius(8), UiAssets.Brush(185, 28, 28), UiAssets.Brush(153, 27, 27))
   };
   btnRemove.Click += (s, e) => {
    btnRemove.IsEnabled = false;
    try {
     InstallerEngine.Uninstall(installDir, chkPurge.IsChecked == true);
     statusText.Text = "MAYLO a été désinstallé.";
     btnCancel.Content = "Fermer";
    } catch (Exception ex) {
     statusText.Text = ex.Message;
     btnRemove.IsEnabled = true;
    }
   };
   btns.Children.Add(btnCancel);
   btns.Children.Add(btnRemove);
   footer.Child = btns;
   Grid.SetRow(footer, 2);
   grid.Children.Add(footer);

   outer.Child = grid;
   Content = outer;
  }
 }

 public static class Program {
  [STAThread]
  public static int Main(string[] args) {
   if (args.Length > 1 && args[0] == "--test-install") {
    try {
     string dir = args[1];
     InstallerEngine.Install(dir);
     if (InstallStore.Current(dir) == null || !File.Exists(IOPath.Combine(dir, InstallStore.Launcher))) return 2;
     return 0;
    } catch { return 1; }
   }

   if (args.Length > 1 && (args[0] == "--verify-ui" || args[0] == "--verify-motion")) {
    bool record = args[0] == "--verify-motion";
    int code = 0;
    var app = new Application();
   var w = new SetupWindow(true);
    w.Loaded += async (s, e) => {
     try {
      await Task.Delay(350);
      for (int i = 0; i < (record ? 27 : 1); i++) {
       string outPng = record ? IOPath.Combine(args[1], i.ToString("D2") + ".png") : args[1];
       w.UpdateLayout();
       var rtb = new RenderTargetBitmap((int)Math.Ceiling(w.ActualWidth), (int)Math.Ceiling(w.ActualHeight), 96, 96, PixelFormats.Pbgra32);
       rtb.Render(w);
       var enc = new PngBitmapEncoder();
       enc.Frames.Add(BitmapFrame.Create(rtb));
       Directory.CreateDirectory(IOPath.GetDirectoryName(IOPath.GetFullPath(outPng)));
       using (var fs = File.Create(outPng)) enc.Save(fs);
       if (record) await Task.Delay(85);
      }
     } catch { code = 1; }
     finally { w.Close(); app.Shutdown(); }
    };
    app.Run(w);
    return code;
   }

   if (args.Length > 0 && args[0] == "--uninstall") {
    try {
     string temp = IOPath.Combine(IOPath.GetTempPath(), "MAYLO-uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
     File.Copy(Assembly.GetExecutingAssembly().Location, temp);
     using (Process.Start(new ProcessStartInfo(temp, "--remove-installed") { UseShellExecute = false })) { }
     return 0;
    } catch (Exception ex) {
     MessageBox.Show(ex.Message, "MAYLO");
     return 1;
    }
   }

   if (args.Length > 0 && args[0] == "--remove-installed") {
    var app = new Application();
    app.Run(new UninstallWindow(InstallerEngine.GetInstalledDir()));
    return 0;
   }

   if (String.Equals(IOPath.GetFileName(Assembly.GetExecutingAssembly().Location), InstallStore.Launcher, StringComparison.OrdinalIgnoreCase)) {
    try { InstallerEngine.LaunchCurrent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\')); }
    catch (Exception ex) { MessageBox.Show(ex.Message, "MAYLO"); return 1; }
    return 0;
   }

   var setupApp = new Application();
   setupApp.Run(new SetupWindow());
   return 0;
  }
 }
}
