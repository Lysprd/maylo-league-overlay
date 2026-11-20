using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Shell;
using Microsoft.Win32;
using MayloRelease;
using IOPath = System.IO.Path;

[assembly: AssemblyTitle("maylo setup")]
[assembly: AssemblyDescription("Windows Installer for maylo — League of Legends & ARAM Mayhem Companion")]
[assembly: AssemblyProduct("maylo")]
[assembly: AssemblyCompany("maylo")]

namespace MayloSetup {
 static class Dwm {
  [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

  public static void ApplyDarkTitleBar(Window window) {
   IntPtr handle = new WindowInteropHelper(window).Handle;
   int dark = 1;
   if (DwmSetWindowAttribute(handle, 20, ref dark, 4) != 0)
    DwmSetWindowAttribute(handle, 19, ref dark, 4);
  }

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
    if (res == null) throw new Exception("Embedded maylo payload not found.");
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

  public static string InstallForUser(string target, bool desktop, bool startMenu, bool startup, Action<int, string> progress) {
   if (String.IsNullOrWhiteSpace(target) || !IOPath.IsPathRooted(target)) throw new IOException("Choose an absolute install folder.");
   string directory = IOPath.GetFullPath(target.Trim());
   Install(directory, progress);
   string exe = IOPath.Combine(directory, InstallStore.Launcher);
   progress(90, "Creating Windows shortcuts…");
   if (desktop) {
    CreateShortcut(IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "MAYLO.lnk"),
     exe, directory, "maylo · League of Legends companion");
   }
   if (startMenu) {
    string menu = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "MAYLO");
    CreateShortcut(IOPath.Combine(menu, "MAYLO.lnk"), exe, directory, "maylo · League of Legends companion");
    CreateShortcut(IOPath.Combine(menu, "Uninstall MAYLO.lnk"), exe, directory, "Uninstall maylo", "--uninstall");
   }
   RegisterUninstall(directory, exe, exe, (int)Math.Max(1024, GetPayloadUncompressedBytes() / 1024));
   SetStartup(exe, startup);
   return exe;
  }

  public static void LaunchCurrent(string root) {
   using (InstallStore.Lock(root)) {
    var current = InstallStore.Current(root);
    var previous = InstallStore.Current(root, true);
    var release = current ?? previous;
    if (release == null) throw new IOException("No valid maylo version is installed.");
    try {
     using (var app = InstallStore.Launch(root, release)) {
      if (app.WaitForExit(2500) && app.ExitCode != 0) throw new IOException("maylo could not start.");
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
    throw new IOException("maylo installation not found.");
   if (InstallationIsRunning(installDir)) throw new IOException("Quit maylo from its tray icon, then try again.");
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
   titles.Children.Add(new TextBlock { Text = "Uninstall maylo", FontSize = 17, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
   titles.Children.Add(new TextBlock { Text = installDir, FontFamily = new FontFamily("Consolas"), FontSize = 10.5, Foreground = UiAssets.Brush(138, 138, 150), TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 340 });
   head.Children.Add(titles);
   body.Children.Add(head);

   statusText = new TextBlock {
    Text = "Remove maylo, its shortcuts and its Windows startup entry.",
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
   txt.Children.Add(new TextBlock { Text = "Also remove settings and cache", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = UiAssets.Brush(235, 235, 240) });
   txt.Children.Add(new TextBlock { Text = "Clear preferences, notes and cached icons", FontSize = 10.5, Foreground = UiAssets.Brush(138, 138, 150) });
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
    Content = "Cancel", MinWidth = 86, Height = 34, Margin = new Thickness(0, 0, 10, 0),
    Background = UiAssets.Brush(27, 29, 38), Foreground = UiAssets.Brush(212, 212, 216),
    BorderBrush = UiAssets.Brush(255, 255, 255, 24), BorderThickness = new Thickness(1), FontSize = 12, Cursor = Cursors.Hand,
    Template = UiAssets.RoundedButtonTemplate(new CornerRadius(8), UiAssets.Brush(38, 41, 54), UiAssets.Brush(48, 52, 68))
   };
   btnCancel.Click += (s, e) => Close();

   btnRemove = new Button {
    Content = "Uninstall", MinWidth = 112, Height = 34,
    Background = UiAssets.Brush(220, 38, 38), Foreground = Brushes.White,
    BorderBrush = UiAssets.Brush(248, 113, 113, 120), BorderThickness = new Thickness(1), FontSize = 12, FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand,
    Template = UiAssets.RoundedButtonTemplate(new CornerRadius(8), UiAssets.Brush(185, 28, 28), UiAssets.Brush(153, 27, 27))
   };
   btnRemove.Click += (s, e) => {
    btnRemove.IsEnabled = false;
    try {
     InstallerEngine.Uninstall(installDir, chkPurge.IsChecked == true);
     statusText.Text = "maylo has been uninstalled.";
     btnCancel.Content = "Close";
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

   if (args.Length > 1 && (args[0] == "--verify-ui" || args[0] == "--verify-motion"))
    return SetupExperience.Run(args[1], args[0] == "--verify-motion");

   if (args.Length > 0 && args[0] == "--uninstall") {
    try {
     string temp = IOPath.Combine(IOPath.GetTempPath(), "MAYLO-uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
     File.Copy(Assembly.GetExecutingAssembly().Location, temp);
     using (Process.Start(new ProcessStartInfo(temp, "--remove-installed") { UseShellExecute = false })) { }
     return 0;
    } catch (Exception ex) {
     MessageBox.Show(ex.Message, "maylo");
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
    catch (Exception ex) { MessageBox.Show(ex.Message, "maylo"); return 1; }
    return 0;
   }

   return SetupExperience.Run(null, false);
  }
 }
}
