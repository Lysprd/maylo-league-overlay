using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

[assembly: System.Reflection.AssemblyTitle("maylo")]
[assembly: System.Reflection.AssemblyDescription("Multi-mode League & ARAM Mayhem companion")]
[assembly: System.Reflection.AssemblyProduct("maylo")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]

namespace MayhemCompanion {
 public class Champion {
  public string Name { get; set; }
  public string Slug { get; set; }
  public string Tier { get; set; }
  public double Win { get; set; }
  public double Pick { get; set; }
  public bool Favorite { get; set; }
  public string Icon { get; set; }
  public string AssetKey { get { string raw = (Icon ?? Slug ?? "").Split('?', '#')[0]; try { return Path.GetFileNameWithoutExtension(raw); } catch { return Slug ?? ""; } } }
 }

 public class Stat {
  public string Name { get; set; }
  public string Pick { get; set; }
  public string Win { get; set; }
  public double Rate { get; set; }
  public string Icon { get; set; }
  public int Id { get; set; }
  public int Games { get; set; }
  public string Rarity { get; set; }
  public int CoreSlot { get; set; } // 1, 2, 3 = core rush order; 4 = late/situational; 0 = unslotted
 }

 public class RuneMeta {
  public int Id;
  public string Name = "";
  public string Icon = "";
 }

 public class RunePage {
  public int PrimaryStyle;
  public int SecondaryStyle;
  public List<int> Perks = new List<int>();
  public string Name = "maylo Runes";
  public string Source = "OP.GG";
 }

 public class SkillOrder {
  public List<string> Priority = new List<string>(); // e.g. ["W","Q","E"]
  public List<string> Levels = new List<string>();   // 1..18 e.g. ["W","E","Q","W",...]
  public string Source = "OP.GG";
 }

 public class CoreBuildPath {
  public List<int> Ids = new List<int>();
  public double WinRate;
  public double PickRate;
  public int Games;
 }

 public class TierListEntry {
  public int Id;
  public string Slug = "";
  public string Name = "";
  public string Icon = "";
  public string Role = "ALL"; // TOP, JUNGLE, MID, ADC, SUPPORT, ARAM, MAYHEM
  public string Tier = "B";   // S+, S, A, B, C, D
  public int Rank;
  public int RankDelta;
  public double WinRate;
  public double PickRate;
  public double BanRate;
  public double Kda;
  public int Games;
  public List<string> Counters = new List<string>();
 }

 public class GameLengthPoint {
  public int Minutes;
  public double WinRate;
 }

 public class PatchDetailLine {
  public string Text = "";
  public string OldValue = "";
  public string NewValue = "";
  public string Type = "";
 }

 public class PatchEntry {
  public int Id;
  public string Category = "champion"; // champion | augment | item | system
  public string Mode = "SR";           // SR | ARAM | MAYHEM
  public string Slug = "";
  public string Title = "";
  public string Icon = "";
  public string ChangeType = "adjust"; // buff | nerf | adjust | added | system
  public string Summary = "";
  public string PlayerNote = "";
  public List<PatchDetailLine> Details = new List<PatchDetailLine>();
 }

 public class PatchNotesBundle {
  public string PatchVersion = "";
  public string OfficialUrl = "";
  public List<PatchEntry> Entries = new List<PatchEntry>();
  public Dictionary<string, string> ChampionTagBySlug = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
  public Dictionary<string, Dictionary<string, string>> ChampionTagByMode = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
 }

 public class Placement {
  public double BenchX = .23, BenchY = .015, BenchStep = .056, TeamX = .078, TeamY = .23, TeamStep = .118;
  public double AugX = .29, AugY = .22, AugStep = .21, ItemsX = .012, ItemsY = .075, Scale = 1;
 }

 public class OverlayPanelPosition { public double X, Y; }

 public class V2Settings {
  public int LayoutVersion = 0;
  public bool OnboardingSeen = false;
  public bool Connected = true, Overlay = true, Ocr = true, OcrAllModes = true, AutoLayout = true, AutoPage = true;
  public bool ShowBuildOverlay = true, ShowRerollBar = true, ShowSkillAdvisor = true, ShowPerfStrip = true, ShowJungleWidget = true;
  public bool ShowSpellTracker = true, ShowBenchBar = true, MinimalHud = false;
  public bool AutoSpells = true, AutoRunes = true, AutoExportShop = true, AutoAccept = false, MinimapLeft = false;
  public bool LaunchWithWindows = false, MinimizeToTray = true;
  public string FlashKey = "F", TierElo = "emerald_plus", TierRole = "ALL";
  public string HotkeySheet = "G", HotkeyOverlay = "M", HotkeyEdit = "O", HotkeyScan = "A";
  public double BadgeOffsetY = 0.0, MinimapScale = 1.0, HudScale = 1.0;
  public string Lockfile = "", BuildFocus = "Adaptive", Theme = "dark";
  public Placement Layout = new Placement();
  public double Left = 80, Top = 40, Opacity = 1;
  public Dictionary<string, OverlayPanelPosition> Panels = new Dictionary<string, OverlayPanelPosition>();
  public long TrackedStart;
  public string TrackedChampion = "";
  public List<int> TrackedAugments = new List<int>();
  public Dictionary<string, int> AugmentPreferences = new Dictionary<string, int>();
  public Dictionary<string, string> Notes = new Dictionary<string, string>();
 }

 public class GameSnapshot {
  public string Phase = "Disconnected", Champion = "", Mode = "", AssignedRole = "";
  public int QueueId = 0;
  public string ModeKey { get { return GameBridge.DetectModeKey(Mode, QueueId, Mayhem); } }
  public string ModeLabel { get { return GameBridge.DescribeMode(Mode, QueueId, ModeKey); } }
  public List<int> Bench = new List<int>(), PriorityCards = new List<int>(), Team = new List<int>();
  public HashSet<int> Owned = new HashSet<int>();
  public double Gold = -1;
  public bool InGame, InSelection, Mayhem;
  public IntPtr Window;
  public LiveSummary Live = new LiveSummary();
 }

 public static class Json {
  public static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = 24000000 }; }
  public static string S(Dictionary<string, object> d, string key) { return d != null && d.ContainsKey(key) && d[key] != null ? Convert.ToString(d[key], System.Globalization.CultureInfo.InvariantCulture) : ""; }
  public static int I(Dictionary<string, object> d, string key) { int n; int.TryParse(S(d, key), out n); return n; }
  public static long L(Dictionary<string, object> d, string key) { long n; long.TryParse(S(d, key), out n); return n; }
  public static int ToInt(object value) { int n; return value != null && int.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), out n) ? n : 0; }
  public static Dictionary<string, object> D(object o) { return o as Dictionary<string, object>; }
  public static object[] Array(object value) { var seq = value as System.Collections.IEnumerable; return seq == null || value is string ? new object[0] : seq.Cast<object>().ToArray(); }
  public static object[] A(Dictionary<string, object> d, string key) { return d != null && d.ContainsKey(key) ? Array(d[key]) : new object[0]; }
 }

 public static class TierLogic {
  public static string Tier(double rate, double baseline) { double d = rate - baseline; return d >= 5 ? "S" : d >= 2 ? "A" : d >= -2 ? "B" : d >= -5 ? "C" : "D"; }
  public static string AugTier(Stat stat, double baseline) { return stat.Games >= 100 ? Tier(stat.Rate, baseline) : "?"; }
  public static string RarityKey(string value) { string norm = GameBridge.Norm(value); return norm.Contains("prismatic") ? "Prismatic" : norm.Contains("gold") ? "Gold" : norm.Contains("silver") ? "Silver" : ""; }
 }

 public static class Parser {
  public static string Text(string s) { return WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(s ?? "", "<!--.*?-->", "", RegexOptions.Singleline), "<[^>]*>", " ")).Trim(); }
  public static string Clean(string s) { return Regex.Replace(Text(s), @"\s+", " "); }
  public static double Number(string s) { double d; return double.TryParse(Regex.Match(s ?? "", @"[\d]+(?:\.[\d]+)?").Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out d) ? d : 0; }
  public static List<string> Cells(string row) { return Regex.Matches(row, @"<td\b[^>]*>(.*?)</td>", RegexOptions.Singleline).Cast<Match>().Select(m => m.Groups[1].Value).ToList(); }

  public static List<Champion> Champions(string html) {
   var result = new List<Champion>();
   foreach (Match r in Regex.Matches(html, @"<tr\b[^>]*>(.*?)</tr>", RegexOptions.Singleline)) {
    var c = Cells(r.Value); if (c.Count < 5) continue;
    var slug = Regex.Match(c[1], "href=\"/champions/([a-z0-9-]+)\""); if (!slug.Success) continue;
    result.Add(new Champion { Name = Clean(c[1]), Slug = slug.Groups[1].Value, Tier = Clean(c[2]), Win = Number(Clean(c[3])), Pick = Number(Clean(c[4])), Icon = WebUtility.HtmlDecode(Regex.Match(c[1], "src=\"([^\"]+)\"").Groups[1].Value) });
   }
   if (result.Count < 100) throw new Exception("MayhemMeta data is unavailable or its format has changed.");
   return result.OrderByDescending(c => c.Win).ToList();
  }

  public static List<Stat> AllAugments(string html) {
   var result = new List<Stat>(); var serializer = new JavaScriptSerializer { MaxJsonLength = 16000000 };
   foreach (Match script in Regex.Matches(html, @"self\.__next_f\.push\((.*?)\)</script>", RegexOptions.Singleline)) {
    try {
     var parts = serializer.DeserializeObject(script.Groups[1].Value) as object[];
     if (parts == null || parts.Length < 2 || !(parts[1] is string)) continue;
     foreach (Match m in Regex.Matches((string)parts[1], @"\{[^{}]+\}")) {
      if (!m.Value.Contains("/icons/augments/") || !m.Value.Contains("\"win_rate\"")) continue;
      var d = serializer.Deserialize<Dictionary<string, object>>(m.Value);
      if (!d.ContainsKey("name") || !d.ContainsKey("win_rate") || !d.ContainsKey("pick_rate") || !d.ContainsKey("id") || !d.ContainsKey("icon_url")) continue;
      if (d.ContainsKey("withheld") && Convert.ToBoolean(d["withheld"])) continue;
      string win = Convert.ToString(d["win_rate"], System.Globalization.CultureInfo.InvariantCulture); if (Number(win) <= 0) continue;
      result.Add(new Stat { Name = Convert.ToString(d["name"]), Pick = Convert.ToString(d["pick_rate"], System.Globalization.CultureInfo.InvariantCulture) + " %", Win = win + " %", Rate = Number(win), Id = Json.ToInt(d["id"]), Icon = Convert.ToString(d["icon_url"]), Games = d.ContainsKey("games") ? Json.ToInt(d["games"]) : 0, Rarity = d.ContainsKey("rarity") ? Convert.ToString(d["rarity"]) : "" });
     }
    } catch { }
   }
   return result.GroupBy(x => x.Name).Select(x => x.First()).ToList();
  }

  public static List<Stat> Section(string html, string title) {
   var section = Regex.Matches(html, @"<section\b[^>]*>(.*?)</section>", RegexOptions.Singleline).Cast<Match>().FirstOrDefault(m => Regex.Matches(m.Value, @"<h2\b[^>]*>(.*?)</h2>", RegexOptions.Singleline).Cast<Match>().Any(h => Clean(h.Groups[1].Value) == title));
   var result = new List<Stat>(); if (section == null) return result;
   foreach (Match row in Regex.Matches(section.Value, @"<tr\b[^>]*>(.*?)</tr>", RegexOptions.Singleline)) {
    var c = Cells(row.Value); if (c.Count < 3) continue; int n = title == "All Augments" ? 1 : 0;
    if (c.Count < n + 3) continue; string name = Clean(c[n]);
    if (title.Contains("Combos") && c.Count == 4) { name = Clean(c[0]) + " + " + Clean(c[1]); n = 1; }
    var win = Clean(c[n + 2]); string icon = WebUtility.HtmlDecode(Regex.Match(c[n], "src=\"([^\"]+)\"").Groups[1].Value);
    string iconBase = icon.Split('?', '#')[0];
    int iconId = 0; try { iconId = (int)Number(Path.GetFileNameWithoutExtension(iconBase)); } catch { }
    result.Add(new Stat { Name = name, Pick = Clean(c[n + 1]), Win = win, Rate = Number(win), Icon = icon, Id = iconId });
   }
   return result;
  }
 }

 public class ItemCopy {
  public List<string> Stats = new List<string>(), Paragraphs = new List<string>();
  public static ItemCopy Parse(string html) {
   var result = new ItemCopy(); html = html ?? "";
   var stats = Regex.Match(html, @"<stats>(.*?)</stats>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
   if (stats.Success) {
    result.Stats = Regex.Split(stats.Groups[1].Value, @"<br\s*/?>", RegexOptions.IgnoreCase).Select(Parser.Clean).Where(x => x != "").ToList();
    html = html.Remove(stats.Index, stats.Length);
   }
   html = Regex.Replace(html, @"<(passive|active)>(.*?)</\1>", m => "\n# " + Parser.Clean(m.Groups[2].Value) + "\n", RegexOptions.Singleline | RegexOptions.IgnoreCase);
   html = Regex.Replace(html, @"<br\s*/?>|</p>", "\n", RegexOptions.IgnoreCase);
   html = WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", ""));
   result.Paragraphs = html.Split('\n').Select(x => Regex.Replace(x, @"\s+", " ").Trim()).Where(x => x != "").ToList();
   return result;
  }
 }

 public static class WebCache {
  public static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MayhemCompanion");
  public static Dictionary<string, bool> Offline = new Dictionary<string, bool>();
  static readonly object offlineLock = new object();

  public static async Task<string> DownloadUrl(string url) {
   var req = (HttpWebRequest)WebRequest.Create(url);
   req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 maylo/" + MayloRelease.BuildInfo.Version;
   req.Accept = "application/json, text/html;q=0.9";
   req.Headers["Accept-Language"] = "en-US,en;q=0.9";
   req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
   req.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
   req.Timeout = 12000; req.ReadWriteTimeout = 12000;
   var pending = req.GetResponseAsync();
   if (await Task.WhenAny(pending, Task.Delay(12000)) != pending) { req.Abort(); try { await pending; } catch { } throw new TimeoutException("Request timed out."); }
   using (var response = await pending)
   using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) {
    var readTask = reader.ReadToEndAsync();
    if (await Task.WhenAny(readTask, Task.Delay(15000)) != readTask) { req.Abort(); throw new TimeoutException("Response read timed out."); }
    return await readTask;
   }
  }

  static void WriteAtomic(string path, string content) {
   string tmp = path + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
   try {
    File.WriteAllText(tmp, content, Encoding.UTF8);
    if (File.Exists(path)) {
     try { File.Replace(tmp, path, null); return; } catch { }
     try { File.Delete(path); } catch { }
    }
    File.Move(tmp, path);
   } catch {
    try { File.WriteAllText(path, content, Encoding.UTF8); } catch { }
   } finally {
    try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
   }
  }

  public static async Task<string> Page(string key, string url, bool force = false, Func<string, bool> validator = null, string ext = ".html", int cacheMinutes = 120) {
   Directory.CreateDirectory(Root);
   string f = Path.Combine(Root, key + ext);
   if (!force && File.Exists(f) && DateTime.UtcNow - File.GetLastWriteTimeUtc(f) < TimeSpan.FromMinutes(cacheMinutes)) {
    try {
     string cached = File.ReadAllText(f, Encoding.UTF8);
     if (validator == null || validator(cached)) return cached;
    } catch { }
   }
   try {
    string html = await DownloadUrl(url);
    if (validator != null && !validator(html)) throw new Exception("Data format unavailable");
    WriteAtomic(f, html);
    lock (offlineLock) { Offline[key] = false; }
    return html;
   } catch {
    if (File.Exists(f)) {
     try {
      string cached = File.ReadAllText(f, Encoding.UTF8);
      if (validator == null || validator(cached)) {
       lock (offlineLock) { Offline[key] = true; }
       return cached;
      }
     } catch { }
    }
    throw;
   }
  }
 }
}
