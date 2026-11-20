using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Windows;

namespace MayhemCompanion {
 public static class V12Tests {
  static void Req(bool ok, string msg) { if (!ok) throw new Exception(msg); }

  public static int Run(string reportPath) {
   try {
    string cache = WebCache.Root;
    var champs = Parser.Champions(File.ReadAllText(Path.Combine(cache, "champions.html")));
    Req(champs.Count >= 150, "MayhemMeta champion index");
    var mayhem = MultiModeParser.ParseMayhem(File.ReadAllText(Path.Combine(cache, "gwen.html")));
    Req(mayhem.Items.Count >= 5 && mayhem.Augments.Count >= 50, "MayhemMeta champion parse");

    // 1. DDragon Runes + CommunityDragon Augment Meta (Offline Descriptions + FR/EN Aliases!)
    var jgTier = MultiModeParser.LoadTierList("SR", "JUNGLE", "emerald_plus", champs, true).GetAwaiter().GetResult();
    Req(jgTier.Count >= 25, "SR Jungle Emerald+ tier list");
    var aramTier = MultiModeParser.LoadTierList("ARAM", "", "emerald_plus", champs, true).GetAwaiter().GetResult();
    var mayhemTier = MultiModeParser.LoadTierList("MAYHEM", "", "emerald_plus", champs, false).GetAwaiter().GetResult();
    Req(aramTier.Count >= 140, "OP.GG ARAM tier list must load 140+ champions (got " + aramTier.Count + ")");
    Req(String.Join(",", aramTier.Take(5).Select(x => x.Slug)) != String.Join(",", mayhemTier.Take(5).Select(x => x.Slug)), "ARAM top 5 must be distinct from Mayhem top 5");
    Req(MultiModeParser.DDragonRunes.ContainsKey(8005) && MultiModeParser.DDragonRunes.ContainsKey(5008), "DDragon Runes & Stat Shards must be loaded");
    MultiModeParser.EnsureAugmentMeta().GetAwaiter().GetResult();
    string adaptDesc = MultiModeParser.GetAugmentDescription(1205, "ADAPt", null);
    Req(!String.IsNullOrWhiteSpace(adaptDesc) && !adaptDesc.Contains("Connect the League client") && !adaptDesc.Contains("indisponible"), "CommunityDragon Augment Description must resolve offline: got '" + adaptDesc + "'");
    var aliasMap = MultiModeParser.BuildAugmentAliasMap(null);
    Req(aliasMap.Count >= 150, "CommunityDragon EN+FR alias map must cover 150+ augments");

    // 2. Role-specific Briar SR + Starter-first Build Path + Core Synergy + Enemy Comp Threat Analysis
    var briar = champs.First(c => c.Slug == "briar");
    var briarJg = MultiModeParser.LoadChampionMode(briar, "SR", "jungle", "emerald_plus", true, null, champs).GetAwaiter().GetResult();
    var briarPlan = BuildPlanner.Plan(briarJg.Items.Concat(briarJg.BestItems).Concat(briarJg.Boots), new int[0], briarJg.Boots.Select(b => b.Id), 50.0, id => new string[0], "Adaptive", new string[0], briarJg.CorePaths);
    Req(briarPlan.Count >= 4 && briarPlan[0].Stat.Id != 3026, "Briar 1st buy != Guardian Angel");
    Req(briarJg.Runes != null && briarJg.Runes.Perks.Count == 9, "Briar 9 Riot rune perk IDs");

    // Starter integration check via HybridHubWindow.CurrentBuildPath()
    var diagWin = new HybridHubWindow(true);
    diagWin.Selected = briar;
    diagWin.ModeData = briarJg;
    diagWin.Game = new GameSnapshot { InGame = true, Gold = 500 };
    var starterPath = diagWin.CurrentBuildPath();
    Req(starterPath.Count >= 5 && starterPath[0].Role == "STARTER", "Starter item must be step 0 when inventory is empty");
    diagWin.Game.Owned.Add(starterPath[0].Stat.Id);
    var afterStarter = diagWin.CurrentBuildPath();
    Req(afterStarter.Count >= 4 && afterStarter[0].Role != "STARTER", "Build path advances past STARTER once owned");

    var fakeLive = new LiveSummary();
    fakeLive.Players.Add(new LivePlayer { Champion = "Briar", Team = "ORDER", Me = true, Position = "JUNGLE" });
    fakeLive.Players.Add(new LivePlayer { Champion = "Aatrox", Team = "CHAOS", Position = "TOP" });
    fakeLive.Players.Add(new LivePlayer { Champion = "Ornn", Team = "CHAOS", Position = "JUNGLE" });
    fakeLive.Players.Add(new LivePlayer { Champion = "Malphite", Team = "CHAOS", Position = "MIDDLE" });
    fakeLive.Players.Add(new LivePlayer { Champion = "Soraka", Team = "CHAOS", Position = "UTILITY" });
    var threats = EnemyCompAnalyzer.Analyze(fakeLive, id => new string[0]);
    Req(threats.NeedsAntiHeal && threats.NeedsAntiTank, "EnemyCompAnalyzer detects Anti-Heal + Anti-Tank");
    var adapted = BuildPlanner.Plan(briarJg.Items.Concat(briarJg.BestItems).Concat(briarJg.Boots), new[] { 3748 }, briarJg.Boots.Select(b => b.Id), 50.0, id => new string[0], "Adaptive", new string[0], briarJg.CorePaths, threats, id => MultiModeParser.ItemNameOf(id));
    Req(adapted.Any(s => s.SynergyTag == "CORE SYNERGY"), "Core branch synergy active");

    // 3. Flash Key D/F + SR-only Objective Timers
    var onD = SpellPlan.OrderForFlash(SpellPlan.Pair("Flash + Smite"), "D", 11, 4);
    var onF = SpellPlan.OrderForFlash(SpellPlan.Pair("Flash + Smite"), "F", 4, 11);
    Req(onD[0] == 4 && onF[1] == 4, "Flash D/F ordering");
    var aramLive = LiveParser.Parse(new Dictionary<string, object> { { "gameData", new Dictionary<string, object> { { "gameMode", "ARAM" }, { "gameTime", 400.0 } } } });
    Req(aramLive.Objectives.Count == 0, "Objectives must be empty outside Summoner's Rift");

    // 4. Multi-Language (FR + EN) Title-Band OCR + BandSignature skip + Single-card fallback
    int w = 1920, h = 1080;
    using (var frame = new Bitmap(w, h, PixelFormat.Format32bppArgb)) {
     using (var g = Graphics.FromImage(frame)) {
      g.Clear(Color.FromArgb(20, 28, 35));
      string[] titles = { "BEES!!!", "Adaptation", "Dual Wield" };
      using (var titleFont = new Font("Segoe UI", 26, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel))
      using (var cardBrush = new SolidBrush(Color.FromArgb(36, 44, 52)))
      using (var fmt = new StringFormat { Alignment = StringAlignment.Center }) {
       for (int i = 0; i < 3; i++) {
        float cx = w * (0.29f + i * 0.21f);
        g.FillRectangle(cardBrush, cx - 140, h * 0.28f, 280, 340);
        g.DrawString(titles[i], titleFont, Brushes.Gold, new RectangleF(cx - 130, h * 0.40f, 260, 80), fmt);
       }
      }
     }
     Func<Native.Rect, Bitmap> cap = r => frame.Clone(new Rectangle(r.Left, r.Top, r.Width, r.Height), PixelFormat.Format32bppArgb);
     var rect = new Native.Rect { Left = 0, Top = 0, Right = w, Bottom = h };
     var scan1 = AugmentOcr.ScanGameRect(rect, mayhem.Augments, aliasMap, false, cap, false, false, 0).GetAwaiter().GetResult();
     var scan2 = AugmentOcr.ScanGameRect(rect, mayhem.Augments, aliasMap, false, cap, false, false, scan1.Signature).GetAwaiter().GetResult();
     Req(scan1.Hits.Count >= 2 && scan2.SkippedUnchanged, "Multi-lang FR/EN OCR >=2/3 + BandSignature skip (got " + scan1.Hits.Count + ")");

     // Verify OfferSession blocks accidental 1-card popups (e.g. post-game screen) but allows 2+ cards or manual scan!
     var sess = new OfferSession();
     sess.Observe(new List<AugmentHit> { scan1.Hits[0] }, DateTime.UtcNow, false);
     Req(!sess.Visible, "OfferSession must NOT falsely pop open on 1 random hit");
     sess.Observe(new List<AugmentHit> { scan1.Hits[0] }, DateTime.UtcNow, true);
     Req(sess.Visible, "OfferSession must accept 1 card on explicit manual Alt+A scan");

     // Verify 4-card tight-row OCR (Ambessa / Vladimir rounds): ALL 4 cards split cleanly (no merged OcrLines) & exact centered X!
     using (var frame4 = new Bitmap(w, h, PixelFormat.Format32bppArgb)) {
      double[] xs4 = { 0.20, 0.40, 0.60, 0.80 };
      string[] titles4 = { "BEES!!!", "Adaptation", "Dual Wield", "Eureka" };
      using (var g = Graphics.FromImage(frame4)) {
       g.Clear(Color.FromArgb(20, 28, 35));
       using (var titleFont = new Font("Segoe UI", 26, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel))
       using (var cardBrush = new SolidBrush(Color.FromArgb(36, 44, 52)))
       using (var fmt = new StringFormat { Alignment = StringAlignment.Center }) {
        for (int i = 0; i < 4; i++) {
         float cx = (float)(w * xs4[i]);
         g.FillRectangle(cardBrush, cx - 135, h * 0.28f, 270, 340);
         g.DrawString(titles4[i], titleFont, Brushes.Gold, new RectangleF(cx - 125, h * 0.40f, 250, 80), fmt);
        }
       }
      }
      Func<Native.Rect, Bitmap> cap4 = r => frame4.Clone(new Rectangle(r.Left, r.Top, r.Width, r.Height), PixelFormat.Format32bppArgb);
      var scan4 = AugmentOcr.ScanGameRect(rect, mayhem.Augments, aliasMap, false, cap4, false, false, 0).GetAwaiter().GetResult();
      Req(scan4.Hits.Count == 4, "4-card tight-row OCR must detect ALL 4 augments (got " + scan4.Hits.Count + ")");
      for (int i = 0; i < 4; i++) {
       double dx = Math.Abs(scan4.Hits[i].X - xs4[i]);
       Req(dx < 0.03, "4-card slot " + i + " X center drifted by " + dx.ToString("0.000"));
      }
      var t0 = DateTime.UtcNow;
      var sess4 = new OfferSession();
      sess4.Observe(scan4.Hits, t0, false);
      Req(sess4.Visible && sess4.Choices.Count(c => c != null) == 4, "OfferSession tracks all 4 cards");
      // Anti-blink: transient 2-card partial dropout + empty frame within HoldMilliseconds must NOT drop cards 3 & 4 or blink!
      sess4.Observe(scan4.Hits.Take(2).ToList(), t0.AddMilliseconds(180), false);
      Req(sess4.Visible && sess4.Choices.Count(c => c != null) == 4, "Anti-blink must preserve missing slots during transient hover/shimmer");
      sess4.Observe(new List<AugmentHit>(), t0.AddMilliseconds(420), false);
      Req(sess4.Visible, "Anti-blink must not hide during <650ms empty frame");
      sess4.Observe(scan4.Hits.Skip(2).ToList(), t0.AddMilliseconds(3000), false);
      Req(sess4.Visible && sess4.Choices.Count(c => c != null) == 4 &&
       Math.Abs(sess4.Positions[0].X - xs4[0]) < 0.03, "Repeated partial reads retain the same four cards at their original X positions");
      // Reroll replacement at slot 1's X must immediately update slot 1!
      var altStat = mayhem.Augments.First(a => !scan4.Hits.Any(hit4 => hit4.Stat.Id == a.Id));
      var rerolled = scan4.Hits.ToList();
      rerolled[1] = new AugmentHit { Stat = altStat, X = xs4[1], Y = 0.40 };
      sess4.Observe(rerolled, t0.AddMilliseconds(3100), false);
      Req(sess4.Choices[1] != null && sess4.Choices[1].Id == altStat.Id, "Reroll at slot 1 X must immediately replace slot 1");
      sess4.Observe(new List<AugmentHit>(), t0.AddSeconds(6), false);
      Req(sess4.Visible, "A single missed scan after a pause must not hide the offer");
      sess4.Observe(new List<AugmentHit>(), t0.AddSeconds(7), false);
      Req(!sess4.Visible, "A disappeared offer must clear after the hold interval");

      using (var g = Graphics.FromImage(frame4))
      using (var titleFont = new Font("Segoe UI", 26, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel))
      using (var fmt = new StringFormat { Alignment = StringAlignment.Center }) {
       g.Clear(Color.FromArgb(20, 28, 35));
       for (int i = 0; i < 4; i++) {
        float y = h * (i == 3 ? 0.572f : 0.528f);
        g.DrawString(titles4[i], titleFont, Brushes.Gold, new RectangleF((float)(w * xs4[i]) - 125, y, 250, 50), fmt);
       }
      }
      var partial = AugmentOcr.ScanGameRect(rect, mayhem.Augments, aliasMap, false, cap4, false).GetAwaiter().GetResult();
      Req(partial.Hits.Count == 3, "The first crop must see three cards in the recovery fixture");
      var recovered4 = AugmentOcr.ScanGameRect(rect, mayhem.Augments, aliasMap, false, cap4, true, false, partial.Signature).GetAwaiter().GetResult();
      Req(!recovered4.SkippedUnchanged && recovered4.Hits.Count == 4 && Math.Abs(recovered4.Hits[3].X - xs4[3]) < 0.03,
       "An unchanged three-title crop must still recover the fourth card beyond the crop");
     }

     File.WriteAllText(reportPath,
      "PASS MAYLO v12: DDragon " + MultiModeParser.DDragonVersion + " (" + MultiModeParser.DDragonRunes.Count + " runes); CDragon Augments=" + MultiModeParser.AugmentDescById.Count + " descs (" + aliasMap.Count + " EN/FR alias sets); Starter-first BuildPath OK; 4/4 Tight-Row OCR + Anti-Blink + Reroll OK; Multi-lang OCR " + scan1.Hits.Count + "/3 in " + Math.Round(scan1.Milliseconds) + "ms + skip " + scan2.Milliseconds.ToString("0.0") + "ms.");
    }
    return 0;
   } catch (Exception ex) {
    File.WriteAllText(reportPath, "FAIL: " + ex);
    return 1;
   }
  }

  public static int VerifyCorners(string reportPath) {
   int result = 0;
   var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
   app.Startup += async (s, e) => {
    HybridHubWindow w = null;
    try {
     w = new HybridHubWindow(true); w.Width = 980; w.Height = 650; w.Left = 130; w.Top = 95;
     w.Show(); await Task.Delay(120);
     var hwnd = new System.Windows.Interop.WindowInteropHelper(w).Handle;
     int style = Native.GetWindowLong(hwnd, -16);
     bool ok = (style & 0x00C00000) == 0x00C00000 && (style & 0x00040000) == 0x00040000 && Native.RoundWindow(hwnd, true);
     Req(ok, "Native Windows 11 dark rounded chrome verification failed");
     File.WriteAllText(reportPath, "PASS WINDOW CORNERS v12: Native DWMWCP_ROUND + Dark Border Theme verified.");
    } catch (Exception ex) { result = 1; File.WriteAllText(reportPath, "FAIL: " + ex); }
    finally { if (w != null) w.Close(); app.Shutdown(); }
   };
   app.Run();
   return result;
  }

  public static int VerifyWeb(string folder) {
   Directory.CreateDirectory(folder);
   int result = 0;
   var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
   app.Startup += async (s, e) => {
    HybridHubWindow w = null;
    try {
     w = new HybridHubWindow(true);
     w.Width = 1240; w.Height = 820; w.Left = 30; w.Top = 20;
     w.Champions = Parser.Champions(File.ReadAllText(Path.Combine(WebCache.Root, "champions.html")));
     var mayhemGwen = MultiModeParser.ParseMayhem(File.ReadAllText(Path.Combine(WebCache.Root, "gwen.html")));
     MultiModeParser.EnsureAugmentMeta().GetAwaiter().GetResult();
     w.ActiveMode = "SR"; w.DetectedMode = "SR";
     w.Config.TierRole = "JUNGLE"; w.Config.TierElo = "emerald_plus"; w.Config.FlashKey = "F"; w.Config.Theme = "dark"; w.Config.OnboardingSeen = true;
     w.Config.Panels.Clear();
     w.TierList = MultiModeParser.LoadTierList("SR", "JUNGLE", "emerald_plus", w.Champions, false).GetAwaiter().GetResult();
     w.Selected = w.Champions.First(c => c.Slug == "briar");
     w.ActiveRole = "jungle"; w.DetectedRole = "jungle";
     w.ModeData = MultiModeParser.LoadChampionMode(w.Selected, "SR", "jungle", "emerald_plus", false, null, w.Champions).GetAwaiter().GetResult();
     w.ModeData.Augments = mayhemGwen.Augments;
     if (!w.ModeData.AvailableRoles.Contains("top")) w.ModeData.AvailableRoles.Add("top");
     w.Bridge.ShopStatus = "Shop auto-exported (Briar Jungle)";
     w.Bridge.RuneStatus = "Runes applied (SR)";
     w.Bridge.SpellStatus = "Spells (Flash on F)";
     w.Game = new GameSnapshot { InGame = true, Mayhem = false, Mode = "CLASSIC", Champion = w.Selected.AssetKey, AssignedRole = "jungle", Gold = 550 };
     w.Game.Live.Seconds = 210; w.Game.Live.ActiveLevel = 4; w.Game.Live.AbilityQ = 1; w.Game.Live.AbilityW = 1; w.Game.Live.AbilityE = 1; w.Game.Live.AbilityR = 0;
     w.Game.Live.BlueDragons = 1; w.Game.Live.RedDragons = 0; w.Game.Live.LastDragonType = "Infernal";
     w.Game.Live.Objectives = new List<ObjectiveTimer> {
      new ObjectiveTimer { Key = "dragon", Name = "Dragon", Status = "1:42", Alive = false, SecondsLeft = 102 },
      new ObjectiveTimer { Key = "grubs", Name = "Voidgrubs", Status = "UP", Alive = true, SecondsLeft = 0 }
     };
     w.Game.Live.Players.Add(new LivePlayer { Champion = "Briar", Name = "maylo#EUW", Team = "ORDER", Position = "JUNGLE", Me = true, Level = 4, Kills = 2, Deaths = 0, Assists = 2, Cs = 28, Spell1 = "Smite", Spell2 = "Flash", SpellKey1 = "SummonerSmite", SpellKey2 = "SummonerFlash", Keystone = "Press the Attack", RuneTree = "Precision" });
     w.Game.Live.Players.Add(new LivePlayer { Champion = "Aatrox", Name = "EnemyTop", Team = "CHAOS", Position = "TOP", Level = 4, Kills = 1, Deaths = 1, Assists = 0, Cs = 31, Spell1 = "Flash", Spell2 = "Teleport", SpellKey1 = "SummonerFlash", SpellKey2 = "SummonerTeleport", Keystone = "Conqueror", RuneTree = "Precision" });
     w.Game.Live.Players.Add(new LivePlayer { Champion = "Viego", Name = "EnemyJg", Team = "CHAOS", Position = "JUNGLE", Level = 4, Kills = 0, Deaths = 2, Assists = 1, Cs = 24, Spell1 = "Flash", Spell2 = "Smite", SpellKey1 = "SummonerFlash", SpellKey2 = "SummonerSmite", Keystone = "Conqueror", RuneTree = "Inspiration" });
     w.Profile.Account = new AccountProfile { Name = "maylo", Tag = "EUW", Level = 184, Region = "EUW1", ProfileIconId = 5367 };
     var top3AugIds = mayhemGwen.Augments.Take(3).Select(a => a.Id).ToList();
     int[] kArr = { 12, 6, 14, 9, 4, 15, 11, 8, 13, 7 };
     int[] dArr = { 2, 5, 3, 2, 6, 1, 4, 3, 2, 4 };
     int[] aArr = { 8, 7, 11, 9, 6, 10, 8, 12, 9, 6 };
     bool[] wArr = { true, false, true, true, false, true, true, true, false, true };
     for (int i = 0; i < 10; i++) {
      var champForHist = i < 4 ? w.Selected : (i < 7 ? w.Champions.First(c => c.Slug == "gwen") : w.Champions[i + 5]);
      int cid = 0; MultiModeParser.ChampIdBySlug.TryGetValue(champForHist.Slug, out cid);
      if (cid <= 0) cid = i + 1;
      w.Bridge.ChampionNames[cid] = champForHist.AssetKey;
      w.Profile.History.Add(new MatchRecord { Id = "m" + i, ChampionId = cid, Queue = i % 2 == 0 ? 420 : 2400, Mode = i % 2 == 0 ? "CLASSIC" : "KIWI", Created = DateTimeOffset.Now.AddHours(-i * 3).ToUnixTimeMilliseconds(), Won = wArr[i], Kills = kArr[i], Deaths = dArr[i], Assists = aArr[i], Duration = 1520, Damage = 26500 + i * 1200, Gold = 13200, Cs = 178, Items = new List<int> { 3748, 3047, 3071, 6333 }, Augments = i % 2 == 1 ? top3AugIds : new List<int>() });
     }

     w.Show();
     Func<string, Task> snap = async name => {
      using (var fs = File.Create(Path.Combine(folder, name)))
       await w.WebControl.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, fs);
     };

     // 1. Enriched Overview Page (Profile Icon + W/L Streak + SVG Win Rate / KDA Graph + Top Personal Pool!)
     w.View = "Dashboard";
     await w.InitWebForTest("#hub");
     await Task.Delay(1500);
     await snap("Hub-Overview-v12.png");

     // 1b. First-Launch Onboarding Tutorial Slider
     w.Config.OnboardingSeen = false; w.PushState();
     await Task.Delay(600);
     await snap("Hub-Onboarding-v12.png");
     w.Config.OnboardingSeen = true; w.PushState();
     await Task.Delay(250);

     // 2. Champion Build (Cropped Portrait + Quick Auto-Runes/Spells/Shop Toggles + Starter-first Path + Runes!)
     w.View = "Build"; w.PushState();
     await Task.Delay(800);
     await snap("Hub-Build-v12.png");

     // 2b. Enriched Synergies / Matchups tab (Champion Portraits + WR Progress Bars!)
     w.View = "Synergies"; w.PushState();
     await Task.Delay(700);
     await snap("Hub-Matchups-v12.png");

     // 3. Augment Detail Modal (Real CommunityDragon Effect Description!)
     w.ActiveMode = "MAYHEM"; w.View = "Augments";
     var adapt = mayhemGwen.Augments.FirstOrDefault(a => a.Id == 1205) ?? mayhemGwen.Augments[0];
     w.HandleWebMessage("{\"type\":\"inspectAugment\",\"id\":" + adapt.Id + "}");
     await Task.Delay(700);
     await snap("Hub-Augment-Detail-v12.png");
     w.HandleWebMessage("{\"type\":\"closeAugment\"}");
     w.ActiveMode = "SR";

     // 4. Light Mode Startup Loading Screen (White/Porcelain #F7F8FA background, zero dark flash!)
     w.Config.Theme = "light"; w.ApplyWindowTheme(false);
     w.BootReady = false; w.BootProgress = 64; w.BootStage = "Loading Riot Data Dragon & Meta…";
     w.PushState();
     await Task.Delay(500);
     await snap("Hub-Splash-Light-v12.png");
     w.BootReady = true; w.Config.Theme = "dark"; w.ApplyWindowTheme(true);

     // 5. In-Game SR Overlay (Starter buy + Skill Max Order directly on Q·W·E·R spell bar + Minimap Timers!)
     w.Width = 1280; w.Height = 720;
     w.Preview = true; w.PreviewScene = "sr_hud"; w.Editing = false; w.InGameSheet = false;
     await w.InitWebForTest("#overlay");
     await w.WebControl.CoreWebView2.ExecuteScriptAsync("document.documentElement.style.setProperty('background','#12151C','important');document.body.style.setProperty('background','#12151C','important');");
     await Task.Delay(900);
     await snap("Overlay-SR-SkillBar-v12.png");

     // 6. Non-Blocking Champ Select Overlay (Bottom-Center Bench Bar + Bottom-Left Safe HUD!)
     w.Game.InGame = false; w.Game.InSelection = true; w.Game.Mayhem = true; w.ActiveMode = "MAYHEM";
     w.Preview = true; w.PreviewScene = "bench";
     w.PushState();
     await Task.Delay(700);
     await snap("Overlay-Bench-Safe-v12.png");

     w.Game = new GameSnapshot();
     w.Editing = false;
     w.HubActive = true;
     w.Config.Overlay = false;
     var preview = new HybridOverlayWindow(w);
     try {
      preview.Sync();
      var screen = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(w).Handle).Bounds;
      var bounds = Native.Bounds(preview.Handle);
      Req(preview.IsVisible && bounds.Left == screen.Left && bounds.Top == screen.Top &&
       bounds.Width == screen.Width && bounds.Height == screen.Height, "Desktop preview must fill the Hub monitor even with the in-game overlay disabled");
     } finally {
      preview.Close();
      w.HubActive = false;
     }

     File.WriteAllText(Path.Combine(folder, "PASS.txt"), "PASS WebView2 v12 renders captured cleanly.");
    } catch (Exception ex) { result = 1; File.WriteAllText(Path.Combine(folder, "FAIL.txt"), ex.ToString()); }
    finally { if (w != null) w.Close(); app.Shutdown(); }
   };
   app.Run();
   return result;
  }
 }
}
