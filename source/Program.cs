using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;
using MayloRelease;

namespace MayhemCompanion {
 public static class Program {
  public static string PendingRestartLauncher;

  [STAThread]
  public static int Main(string[] args) {
   Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
   Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
   ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

   if (args.Length > 1) {
    switch (args[0]) {
     case "--test":
     case "--test-v12": return V12Tests.Run(args[1]);
     case "--verify-corners": return V12Tests.VerifyCorners(args[1]);
     case "--verify-web": return V12Tests.VerifyWeb(args[1]);
     case "--test-recommendations": return V12Tests.RunRecommendations(args[1]);
     case "--test-updater": return GitHubUpdater.SelfTest(args[1]);
    }
   }

   bool startMinimized = args.Any(a => a == "--autostart" || a == "--minimized");

   bool first;
   using (var mutex = new Mutex(true, "Local\\MayhemCompanionV2", out first)) {
    if (!first) {
     if (!startMinimized)
      MessageBox.Show("maylo is already running. Open its tray icon or press Alt+G / F9.", "maylo");
     return 0;
    }

    var app = new Application();
    var hub = new HybridHubWindow(false, startMinimized);
    app.SessionEnding += (s, e) => hub.PrepareQuit();
    app.DispatcherUnhandledException += (s, e) => {
     MessageBox.Show(e.Exception.Message, "maylo");
     e.Handled = true;
    };
    using (var cancellation = new CancellationTokenSource()) {
     GitHubUpdater.Start(cancellation.Token);
     try {
      if (startMinimized) {
       hub.WindowState = WindowState.Minimized;
       if (hub.Config.MinimizeToTray) hub.ShowInTaskbar = false;
      }
      app.Run(hub);
     } finally {
      cancellation.Cancel();
     }
    }
   }

   // Mutex is now fully released — launch updated MAYLO.exe cleanly if user clicked Restart to Update!
   if (!String.IsNullOrEmpty(PendingRestartLauncher) && File.Exists(PendingRestartLauncher)) {
    try {
     using (Process.Start(new ProcessStartInfo(PendingRestartLauncher) {
      WorkingDirectory = Path.GetDirectoryName(PendingRestartLauncher),
      UseShellExecute = false
     })) { }
    } catch { }
   }
   return 0;
  }
 }
}
