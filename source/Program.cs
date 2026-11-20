using System;
using System.Globalization;
using System.Net;
using System.Threading;
using System.Windows;

namespace MayhemCompanion {
 public static class Program {
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
     case "--test-updater": return GitHubUpdater.SelfTest(args[1]);
    }
   }

   bool first;
   using (var mutex = new Mutex(true, "Local\\MayhemCompanionV2", out first)) {
    if (!first) {
     MessageBox.Show("maylo is already running. Open its tray icon or press Alt+G / F9.", "maylo");
     return 0;
    }

    var app = new Application();
    var hub = new HybridHubWindow();
    app.SessionEnding += (s, e) => hub.PrepareQuit();
    app.DispatcherUnhandledException += (s, e) => {
     MessageBox.Show(e.Exception.Message, "maylo");
     e.Handled = true;
    };
    using (var cancellation = new CancellationTokenSource()) {
     GitHubUpdater.Start(cancellation.Token);
     try { app.Run(hub); }
     finally { cancellation.Cancel(); }
    }
    return 0;
   }
  }
 }
}
