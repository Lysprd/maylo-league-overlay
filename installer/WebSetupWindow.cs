using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace MayloSetup {
 internal static class SetupExperience {
  const string RuntimePage = "https://developer.microsoft.com/microsoft-edge/webview2/";

  [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
  static extern IntPtr LoadLibrary(string file);

  [DllImport("kernel32.dll")]
  static extern bool FreeLibrary(IntPtr module);

  public static int Run(string capture, bool record) {
   if (!InstallerEngine.IsWebView2Installed()) {
    if (capture != null) return 1;
    var answer = MessageBox.Show("maylo needs Microsoft Edge WebView2. Open the official download page?",
     "maylo setup", MessageBoxButton.YesNo, MessageBoxImage.Information);
    if (answer == MessageBoxResult.Yes)
     using (Process.Start(new ProcessStartInfo(RuntimePage) { UseShellExecute = true })) { }
    return 1;
   }

   string root = Path.Combine(Path.GetTempPath(), "maylo-setup-" + Guid.NewGuid().ToString("N"));
   IntPtr loader = IntPtr.Zero;
   ResolveEventHandler resolve = (sender, args) => {
    string name = new AssemblyName(args.Name).Name;
    if (name != "Microsoft.Web.WebView2.Core" && name != "Microsoft.Web.WebView2.Wpf") return null;
    return Assembly.Load(File.ReadAllBytes(Path.Combine(root, name + ".dll")));
   };

   try {
    using (var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("maylo-payload.zip")) {
     if (payload == null) throw new IOException("maylo setup payload is missing.");
     MayloRelease.InstallStore.Extract(payload, root);
    }
    loader = LoadLibrary(Path.Combine(root, "WebView2Loader.dll"));
    if (loader == IntPtr.Zero) throw new IOException("WebView2 could not load.");
    AppDomain.CurrentDomain.AssemblyResolve += resolve;
    return RunWindow(root, capture, record);
   } catch (Exception ex) {
    if (capture == null) MessageBox.Show(ex.Message, "maylo setup", MessageBoxButton.OK, MessageBoxImage.Error);
    else File.WriteAllText(capture + ".error.txt", ex.ToString());
    return 1;
   } finally {
    AppDomain.CurrentDomain.AssemblyResolve -= resolve;
    if (loader != IntPtr.Zero) FreeLibrary(loader);
    if (Directory.Exists(root)) {
     try { Directory.Delete(root, true); }
     catch (IOException) { }
     catch (UnauthorizedAccessException) { }
    }
   }
  }

  [MethodImpl(MethodImplOptions.NoInlining)]
  static int RunWindow(string root, string capture, bool record) {
   var app = new Application();
   var window = new WebSetupWindow(root, capture, record);
   app.Run(window);
   return window.Result;
  }
 }

 internal sealed class WebSetupWindow : Window {
  const string Page = "https://setup.maylo.local/index.html#setup";
  const string CapturePath = @"%LocalAppData%\maylo";
  readonly WebView2 web = new WebView2();
  readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 32768 };
  readonly TaskCompletionSource<bool> ready = new TaskCompletionSource<bool>();
  readonly string root, capture;
  readonly bool record;
  bool installing, closed;
  string installedExe;
  public int Result { get; private set; }

  public WebSetupWindow(string root, string capture, bool record) {
   this.root = root;
   this.capture = capture;
   this.record = record;
   Title = "maylo setup";
   Width = Math.Min(914, SystemParameters.WorkArea.Width);
   Height = Math.Min(590, SystemParameters.WorkArea.Height);
   WindowStartupLocation = WindowStartupLocation.CenterScreen;
   ResizeMode = ResizeMode.CanMinimize;
   Background = UiAssets.Brush(11, 12, 16);
   Icon = UiAssets.LoadEmbeddedLogo();
   web.DefaultBackgroundColor = System.Drawing.Color.FromArgb(11, 12, 16);
   Content = web;
   SourceInitialized += (s, e) => Dwm.ApplyDarkTitleBar(this);
   Loaded += async (s, e) => await Start();
   Closing += (s, e) => { if (installing) e.Cancel = true; };
   Closed += (s, e) => { closed = true; web.Dispose(); };
  }

  async Task Start() {
   try {
    var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(root, "webview"));
    if (closed) return;
    await web.EnsureCoreWebView2Async(environment);
    if (closed) return;
    var core = web.CoreWebView2;
    core.Settings.AreDevToolsEnabled = false;
    core.Settings.AreDefaultContextMenusEnabled = false;
    core.Settings.AreBrowserAcceleratorKeysEnabled = false;
    core.Settings.IsStatusBarEnabled = false;
    core.SetVirtualHostNameToFolderMapping("setup.maylo.local", Path.Combine(root, "ui-dist"), CoreWebView2HostResourceAccessKind.DenyCors);
    core.NavigationStarting += (s, e) => { e.Cancel = e.Uri != Page; };
    core.NewWindowRequested += (s, e) => { e.Handled = true; };
    core.DownloadStarting += (s, e) => { e.Cancel = true; };
    core.WebMessageReceived += async (s, e) => { if (e.Source == Page) await Receive(e.WebMessageAsJson); };
    core.Navigate(Page);
    if (await Task.WhenAny(ready.Task, Task.Delay(12000)) != ready.Task) throw new TimeoutException("The setup interface did not load.");
    if (capture != null && !closed) {
     await Capture();
     Close();
    }
   } catch (Exception ex) {
    if (closed) return;
    Result = 1;
    if (capture != null) File.WriteAllText(capture + ".error.txt", ex.ToString());
    else MessageBox.Show(this, ex.Message, "maylo setup", MessageBoxButton.OK, MessageBoxImage.Error);
    Close();
   }
  }

  void Send(object message) {
   if (!closed) web.CoreWebView2.PostWebMessageAsJson(json.Serialize(message));
  }

  async Task Receive(string raw) {
   Dictionary<string, object> message;
   try { message = json.Deserialize<Dictionary<string, object>>(raw); }
   catch (ArgumentException) { return; }
   if (message == null) return;
   switch (Text(message, "type")) {
    case "setupReady":
     if (ready.TrySetResult(true))
      Send(new { type = "setupPath", path = capture == null ? InstallerEngine.GetInstalledDir() : CapturePath });
     break;
    case "setupClose":
     if (!installing && capture == null) Close();
     break;
    case "setupBrowse":
     if (!installing && installedExe == null && capture == null) Browse(Text(message, "path"));
     break;
    case "setupInstall":
     if (!installing && installedExe == null && capture == null) await Install(message);
     break;
    case "setupLaunch":
     if (!installing && installedExe != null) Launch();
     break;
   }
  }

  static string Text(Dictionary<string, object> message, string key) {
   object value;
   return message.TryGetValue(key, out value) ? value as string : null;
  }

  static bool Enabled(Dictionary<string, object> message, string key) {
   object value;
   return message.TryGetValue(key, out value) && value is bool && (bool)value;
  }

  void Browse(string path) {
   using (var dialog = new System.Windows.Forms.FolderBrowserDialog {
    Description = "Choose the maylo install folder",
    SelectedPath = Directory.Exists(path) ? path : InstallerEngine.GetInstalledDir()
   }) {
    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK && !String.IsNullOrWhiteSpace(dialog.SelectedPath)) {
     string chosen = dialog.SelectedPath.TrimEnd('\\');
     bool hasOtherFiles = Directory.Exists(chosen) && Directory.GetFileSystemEntries(chosen).Length > 0
      && !File.Exists(Path.Combine(chosen, MayloRelease.InstallStore.Launcher));
     if (hasOtherFiles && !chosen.EndsWith("\\MAYLO", StringComparison.OrdinalIgnoreCase)) {
      chosen = Path.Combine(chosen, "MAYLO");
     }
     Send(new { type = "setupPath", path = chosen });
    }
   }
  }

  async Task Install(Dictionary<string, object> message) {
   installing = true;
   Progress(8, "Preparing files…");
   try {
    installedExe = await Task.Run(() => InstallerEngine.InstallForUser(Text(message, "path"),
     Enabled(message, "desktop"), Enabled(message, "startMenu"), Enabled(message, "startup"),
     (percent, label) => Dispatcher.Invoke(() => Progress(percent, label))));
    Progress(100, "Installation complete");
    Send(new { type = "setupDone", path = Path.GetDirectoryName(installedExe) });
   } catch (Exception ex) {
    Send(new { type = "setupError", message = ex.Message });
   } finally { installing = false; }

   if (installedExe != null && Enabled(message, "openAfter")) {
    await Task.Delay(600);
    if (!closed) Launch();
   }
  }

  void Progress(int percent, string label) {
   Send(new { type = "setupProgress", percent = percent, label = label });
  }

  void Launch() {
   try {
    MayloRelease.InstallStore.UnblockFile(installedExe);
    using (Process.Start(new ProcessStartInfo(installedExe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(installedExe) })) { }
    Close();
   } catch (Exception ex) {
    MessageBox.Show(this, ex.Message, "Open maylo", MessageBoxButton.OK, MessageBoxImage.Error);
   }
  }

  async Task Capture() {
   if (!record) {
    await Task.Delay(650);
    await Snapshot(capture);
    return;
   }
   Directory.CreateDirectory(capture);
   for (int frame = 0; frame < 36; frame++) {
    if (frame == 12) Progress(12, "Preparing files…");
    if (frame == 17) Progress(54, "Installing maylo…");
    if (frame == 21) Progress(90, "Creating Windows shortcuts…");
    if (frame == 26) Send(new { type = "setupDone", path = CapturePath });
    await Task.Delay(115);
    await Snapshot(Path.Combine(capture, frame.ToString("D2") + ".png"));
   }
  }

  async Task Snapshot(string path) {
   Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
   using (var stream = File.Create(path))
    await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
  }
 }
}
