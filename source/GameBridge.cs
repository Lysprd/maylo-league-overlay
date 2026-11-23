using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MayhemCompanion {
 public static class SelectionResolver {
  public static void ReadDraftPicks(Dictionary<string, object> session, GameSnapshot snapshot) {
   var revealed = new HashSet<int>(Json.A(session, "theirTeam").Select(Json.D)
    .Select(player => Json.I(player, "championId")).Where(id => id > 0));
   var enemyCells = new HashSet<int>(Json.A(session, "theirTeam").Select(Json.D)
    .Where(player => player != null && player.ContainsKey("cellId")).Select(player => Json.I(player, "cellId")));
   var allyCells = new HashSet<int>(Json.A(session, "myTeam").Select(Json.D)
    .Where(player => player != null && player.ContainsKey("cellId")).Select(player => Json.I(player, "cellId")));
   foreach (var group in Json.A(session, "actions")) foreach (var value in Json.Array(group)) {
    var action = Json.D(value);
    int id = Json.I(action, "championId");
    if (id <= 0 || !String.Equals(Json.S(action, "completed"), "true", StringComparison.OrdinalIgnoreCase)) continue;
    string type = Json.S(action, "type");
    if (type == "pick") {
     int actor = Json.I(action, "actorCellId");
     string side = Json.S(action, "isAllyAction");
     bool enemy = enemyCells.Contains(actor) || String.Equals(side, "false", StringComparison.OrdinalIgnoreCase);
     if (enemy && revealed.Contains(id)) {
      snapshot.DraftUnavailable.Add(id);
      if (!snapshot.DraftEnemies.Contains(id)) snapshot.DraftEnemies.Add(id);
     } else if (!enemy && (allyCells.Contains(actor) || String.Equals(side, "true", StringComparison.OrdinalIgnoreCase))) {
      snapshot.DraftUnavailable.Add(id);
     }
    }
   }
   var bans = session != null && session.ContainsKey("bans") ? Json.D(session["bans"]) : null;
   foreach (var ban in Json.A(bans, "myTeamBans").Concat(Json.A(bans, "theirTeamBans"))) {
    int id = Json.ToInt(ban);
    if (id > 0) snapshot.DraftUnavailable.Add(id);
   }
  }

  public static int Champion(Dictionary<string, object> session, int current) {
   if (current > 0) return current;
   if (session == null || !session.ContainsKey("localPlayerCellId")) return 0;
   int local = Json.I(session, "localPlayerCellId");
   foreach (var group in Json.A(session, "actions")) foreach (var value in Json.Array(group)) {
    var a = Json.D(value); if (Json.I(a, "actorCellId") != local || Json.S(a, "type") != "pick" || Json.I(a, "championId") <= 0) continue;
    if (a.ContainsKey("isInProgress") && Convert.ToBoolean(a["isInProgress"])) return Json.I(a, "championId");
   }
   var me = Json.A(session, "myTeam").Select(Json.D).FirstOrDefault(d => Json.I(d, "cellId") == local);
   int selected = Json.I(me, "championId"); return selected > 0 ? selected : Json.I(me, "championPickIntent");
  }
 }

 public static class SpellPlan {
  static readonly Dictionary<string, int> ids = new Dictionary<string, int> { { "flash", 4 }, { "ghost", 6 }, { "mark", 32 }, { "snowball", 32 }, { "heal", 7 }, { "barrier", 21 }, { "cleanse", 1 }, { "exhaust", 3 }, { "ignite", 14 }, { "clarity", 13 }, { "teleport", 12 }, { "smite", 11 } };
  public static int[] Pair(string name) {
   var parts = (name ?? "").Split(new[] { " + " }, StringSplitOptions.None);
   if (parts.Length != 2) return null;
   int a, b;
   if (!ids.TryGetValue(GameBridge.Norm(parts[0]), out a) || !ids.TryGetValue(GameBridge.Norm(parts[1]), out b) || a == b) return null;
   return new[] { a, b };
  }
  public static int[] KeepSlots(int[] pair, int current1, int current2) {
   if (pair == null) return null;
   if (pair.Contains(current1)) return new[] { current1, pair.First(x => x != current1) };
   if (pair.Contains(current2)) return new[] { pair.First(x => x != current2), current2 };
   return pair;
  }
  public static int[] OrderForFlash(int[] pair, string flashKey, int current1, int current2) {
   if (pair == null || pair.Length != 2) return null;
   if (pair.Contains(4)) {
    int other = pair[0] == 4 ? pair[1] : pair[0];
    if (String.Equals(flashKey, "D", StringComparison.OrdinalIgnoreCase)) return new[] { 4, other };
    if (String.Equals(flashKey, "F", StringComparison.OrdinalIgnoreCase)) return new[] { other, 4 };
   }
   return KeepSlots(pair, current1, current2);
  }
 }

 public partial class GameBridge {
  public string Lockfile = "", SpellStatus = "Waiting for champion select", RuneStatus = "Waiting for champion select", ShopStatus = "";
  string auth = "", localBase = "", spellSelection = "", runeSelection = "", shopSelection = "", lastChampion = "", lastAssignedRole = "", lastChampSelectDebugKey = "";
  DateTime lastFind = DateTime.MinValue, lastMiss = DateTime.MinValue, nextLiveProbe = DateTime.MinValue;
  bool staticReady, lastMayhem;
  Task staticTask;

  public Dictionary<int, string> ChampionNames = new Dictionary<int, string>();
  public Dictionary<int, string> ChampionLocalNames = new Dictionary<int, string>();
  public Dictionary<int, Dictionary<string, object>> Items = new Dictionary<int, Dictionary<string, object>>();
  public Dictionary<int, List<string>> AugmentTags = new Dictionary<int, List<string>>();
  public Dictionary<int, string> AugmentNames = new Dictionary<int, string>();
  public Dictionary<int, string> AugmentDescriptions = new Dictionary<int, string>();
  public Dictionary<int, string> AugmentRarities = new Dictionary<int, string>();
  public HashSet<int> MayhemAugmentIds = new HashSet<int>();

  public static string Norm(string s) {
   if (String.IsNullOrEmpty(s)) return "";
   s = s.Replace("œ", "oe").Replace("Œ", "oe").Replace("æ", "ae").Replace("Æ", "ae").Replace("ß", "ss");
   return Regex.Replace(s.Normalize(NormalizationForm.FormD).ToLowerInvariant(), "[^a-z0-9]", "");
  }

  public static bool IsMayhemMode(string mode, int queueId = 0) {
   return queueId == 2400 || queueId == 3270 || String.Equals(mode, "KIWI", StringComparison.OrdinalIgnoreCase) || String.Equals(mode, "MAYHEM", StringComparison.OrdinalIgnoreCase);
  }

  public static string DetectModeKey(string mode, int queueId, bool mayhem) {
   switch (queueId) {
    case 2400: case 3270: return "MAYHEM";
    case 1700: case 1710: return "ARENA";
    case 450: case 720: return "ARAM";
    case 400: case 420: case 430: case 440: case 480: case 490: case 700:
    case 830: case 840: case 850: case 870: case 880: case 890:
    case 2000: case 2010: case 2020:
    case 3100: case 3110: case 3120: case 3130: case 3140: return "SR";
   }
   // Explicit ARAM Mayhem (KIWI), Arena (CHERRY), and Practice Tool / Tutorial modes work across Riot queue ID rotations.
   if (String.Equals(mode, "KIWI", StringComparison.OrdinalIgnoreCase) ||
       String.Equals(mode, "MAYHEM", StringComparison.OrdinalIgnoreCase)) return "MAYHEM";
   if (String.Equals(mode, "CHERRY", StringComparison.OrdinalIgnoreCase) ||
       String.Equals(mode, "ARENA", StringComparison.OrdinalIgnoreCase)) return "ARENA";
   if (String.Equals(mode, "PRACTICETOOL", StringComparison.OrdinalIgnoreCase) ||
       (mode != null && mode.StartsWith("TUTORIAL", StringComparison.OrdinalIgnoreCase))) return "SR";
   // Published queue IDs take precedence over a generic map mode such as CLASSIC.
   if (queueId > 0) return "";
   if (mayhem || IsMayhemMode(mode)) return "MAYHEM";
   if (String.Equals(mode, "ARAM", StringComparison.OrdinalIgnoreCase)) return "ARAM";
   if (String.Equals(mode, "CLASSIC", StringComparison.OrdinalIgnoreCase) ||
       String.Equals(mode, "SWIFTPLAY", StringComparison.OrdinalIgnoreCase)) return "SR";
   return "";
  }

  public static string DescribeMode(string mode, int queueId, string modeKey) {
   switch (queueId) {
    case 400: return "Normal Draft";
    case 420: return "Ranked Solo/Duo";
    case 430: return "Normal Blind Pick";
    case 440: return "Ranked Flex";
    case 480: return "Swiftplay";
    case 490: return "Quickplay";
    case 700: return "Summoner's Rift Clash";
    case 720: return "ARAM Clash";
    case 830: case 840: case 850: case 870: case 880: case 890: return "Co-op vs. AI";
    case 1700: case 1710: return "Arena";
    case 3140: return "Practice Tool";
   }
   if (modeKey == "MAYHEM") return "ARAM Mayhem";
   if (modeKey == "ARENA") return queueId < 0 ? "Custom Arena" : "Arena";
   if (modeKey == "ARAM") return queueId < 0 ? "Custom ARAM" : "ARAM";
   if (modeKey == "SR") {
    if (String.Equals(mode, "SWIFTPLAY", StringComparison.OrdinalIgnoreCase)) return "Swiftplay";
    if (String.Equals(mode, "PRACTICETOOL", StringComparison.OrdinalIgnoreCase)) return "Practice Tool";
    if (queueId < 0 || (queueId >= 3100 && queueId <= 3130)) return "Custom Game";
    return "Summoner's Rift";
   }
   if (String.Equals(mode, "CHERRY", StringComparison.OrdinalIgnoreCase)) return "Arena";
   if (String.Equals(mode, "JADE", StringComparison.OrdinalIgnoreCase)) return "League Classic";
   return "Unsupported mode";
  }

  static void ApplyFlowSession(GameSnapshot snapshot, Dictionary<string, object> flow, bool preserveExistingMode) {
   if (snapshot == null || flow == null) return;
   var gd = flow.ContainsKey("gameData") ? Json.D(flow["gameData"]) : null;
   long matchId = Json.L(gd, "gameId");
   if (matchId > 0) snapshot.MatchId = matchId.ToString(System.Globalization.CultureInfo.InvariantCulture);
   var q = gd != null && gd.ContainsKey("queue") ? Json.D(gd["queue"]) : null;
   var map = flow.ContainsKey("map") ? Json.D(flow["map"]) : null;
   if (q != null) snapshot.QueueId = Json.I(q, "id");
   string qMode = q != null ? Json.S(q, "gameMode") : "";
   string mapMode = map != null ? Json.S(map, "gameMode") : "";
   string qType = q != null ? Json.S(q, "type") : "";
   bool isCustom = gd != null && Json.S(gd, "isCustomGame").Equals("true", StringComparison.OrdinalIgnoreCase);
   int mapId = map != null ? Json.I(map, "id") : 0;
   string resolved = qMode != "" ? qMode : mapMode;
   if (resolved == "" && qType.IndexOf("PRACTICE", StringComparison.OrdinalIgnoreCase) >= 0) resolved = "PRACTICETOOL";
   if (resolved == "" && (isCustom || snapshot.QueueId <= 0)) {
    if (mapId == 12) resolved = "ARAM";
    else if (mapId == 30) resolved = "CHERRY";
    else if (mapId == 11) resolved = "CLASSIC";
   }
   if (resolved != "" && (!preserveExistingMode || snapshot.Mode == "")) snapshot.Mode = resolved;
   snapshot.Mayhem |= IsMayhemMode(resolved, snapshot.QueueId) || IsMayhemMode(snapshot.Mode, snapshot.QueueId);
  }

  public static async Task<string> Request(string method, string url, string authorization = null, string jsonBody = null) {
   var req = (HttpWebRequest)WebRequest.Create(url);
   req.Method = method; req.Timeout = 2500; req.ReadWriteTimeout = 2500;
   if (authorization != null) req.Headers["Authorization"] = authorization;
   if (new Uri(url).IsLoopback) { req.Proxy = null; req.ServerCertificateValidationCallback = (s, c, ch, e) => true; }
   if (jsonBody != null) {
    req.ContentType = "application/json";
    byte[] bytes = Encoding.UTF8.GetBytes(jsonBody);
    req.ContentLength = bytes.Length;
    var reqStreamTask = req.GetRequestStreamAsync();
    if (await Task.WhenAny(reqStreamTask, Task.Delay(2500)) != reqStreamTask) { req.Abort(); try { await reqStreamTask; } catch { } throw new TimeoutException("Local API unavailable."); }
    using (var stream = await reqStreamTask) await stream.WriteAsync(bytes, 0, bytes.Length);
   }
   var pending = req.GetResponseAsync();
   if (await Task.WhenAny(pending, Task.Delay(2500)) != pending) { req.Abort(); try { await pending; } catch { } throw new TimeoutException("Local API unavailable."); }
   using (var response = await pending)
   using (var r = new StreamReader(response.GetResponseStream())) {
    var readTask = r.ReadToEndAsync();
    if (await Task.WhenAny(readTask, Task.Delay(3500)) != readTask) { req.Abort(); throw new TimeoutException("Local API read timed out."); }
    return await readTask;
   }
  }

  public static Task<string> Get(string url, string authorization = null) { return Request("GET", url, authorization); }

  void InvalidateConnection() { localBase = ""; auth = ""; lastFind = DateTime.MinValue; staticReady = false; }

  async Task<string> LcuRequest(string method, string path, string jsonBody = null) {
   await Connect();
   try { return await Request(method, localBase + path, auth, jsonBody); }
   catch (WebException wex) {
    var resp = wex.Response as HttpWebResponse;
    if (resp == null || resp.StatusCode == HttpStatusCode.Unauthorized || resp.StatusCode == HttpStatusCode.Forbidden) InvalidateConnection();
    throw;
   } catch (TimeoutException) { InvalidateConnection(); throw; }
  }

  public async Task<byte[]> ReadChampionPortrait(int id) {
   if (id <= 0) return null;
   await Connect();
   var url = localBase + "/lol-game-data/assets/v1/champion-icons/" + id + ".png";
   var request = (HttpWebRequest)WebRequest.Create(url);
   request.Proxy = null;
   request.ServerCertificateValidationCallback = (s, c, ch, e) => true;
   request.Headers["Authorization"] = auth;
   request.AllowAutoRedirect = false;
   var pending = request.GetResponseAsync();
   if (await Task.WhenAny(pending, Task.Delay(1200)) != pending) {
    request.Abort();
    try { using (var ignored = await pending) { } } catch { }
    return null;
   }
   using (var response = await pending)
   using (var input = response.GetResponseStream())
   using (var data = new MemoryStream()) {
    var copy = input.CopyToAsync(data);
    if (await Task.WhenAny(copy, Task.Delay(1200)) != copy) {
     request.Abort();
     try { await copy; } catch { }
     return null;
    }
    await copy;
    return data.ToArray();
   }
  }

  Task<string> LcuGet(string path) { return LcuRequest("GET", path); }

  string FindLock() {
   if (File.Exists(Lockfile)) return Lockfile;
   foreach (var p in Process.GetProcessesByName("LeagueClient")) { try { var f = Path.Combine(Path.GetDirectoryName(p.MainModule.FileName), "lockfile"); if (File.Exists(f)) return Lockfile = f; } catch { } finally { p.Dispose(); } }
   try { string registry = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Riot Games\RiotClientInstalls.json"); if (File.Exists(registry)) { var d = Json.Serializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(registry)); var installs = d != null && d.ContainsKey("associated_client") ? Json.D(d["associated_client"]) : null; if (installs != null) foreach (var folder in installs.Keys) { var f = Path.Combine(folder, "lockfile"); if (File.Exists(f)) return Lockfile = f; } } } catch { }
   foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed)) foreach (var suffix in new[] { @"Riot Games\League of Legends\lockfile", @"GAMES\Riot Games\League of Legends\lockfile", @"Games\League of Legends\lockfile" }) { string f = Path.Combine(drive.RootDirectory.FullName, suffix); if (File.Exists(f)) return Lockfile = f; }
   return "";
  }

  Task Connect() {
   if (DateTime.UtcNow - lastFind < TimeSpan.FromSeconds(15) && localBase != "") return Task.FromResult(0);
   if (localBase == "" && DateTime.UtcNow - lastMiss < TimeSpan.FromSeconds(2.5)) throw new Exception("Open League or choose its lockfile in Settings.");
   lastFind = DateTime.UtcNow; string file = FindLock();
   if (file == "") { lastMiss = DateTime.UtcNow; throw new Exception("Open League or choose its lockfile in Settings."); }
   string content; using (var f = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) using (var r = new StreamReader(f)) content = r.ReadToEnd();
   var parts = content.Split(':'); int port; if (parts.Length < 5 || !int.TryParse(parts[2], out port) || port < 1 || port > 65535) { lastMiss = DateTime.UtcNow; throw new Exception("Invalid lockfile."); }
   string next = "https://127.0.0.1:" + port; if (next != localBase) staticReady = false; localBase = next; auth = "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes("riot:" + parts[3])); return Task.FromResult(0);
  }

  public Task LoadStatic() {
   if (staticReady) return Task.FromResult(0);
   if (staticTask != null && !staticTask.IsCompleted) return staticTask;
   staticTask = LoadStaticCore();
   return staticTask;
  }

  async Task LoadStaticCore() {
   await Connect(); if (staticReady) return;
   var champions = Json.Serializer().DeserializeObject(await LcuGet("/lol-game-data/assets/v1/champion-summary.json")) as object[];
   if (champions != null) foreach (var entry in champions) {
    var d = Json.D(entry); if (d == null) continue;
    int cid = Json.I(d, "id");
    if (cid <= 0) continue;
    ChampionNames[cid] = Json.S(d, "alias");
    string loc = Json.S(d, "name");
    if (loc != "") ChampionLocalNames[cid] = loc;
   }
   try { var items = Json.Serializer().DeserializeObject(await LcuGet("/lol-game-data/assets/v1/items.json")) as object[]; foreach (var entry in items ?? new object[0]) { var d = Json.D(entry); if (d != null) Items[Json.I(d, "id")] = d; } } catch { }
   try { var augments = Json.Serializer().DeserializeObject(await LcuGet("/lol-game-data/assets/v1/cherry-augments.json")) as object[]; foreach (var entry in augments ?? new object[0]) { var d = Json.D(entry); if (d == null) continue; int id = Json.I(d, "id"); AugmentNames[id] = Json.S(d, "nameTRA"); AugmentDescriptions[id] = Parser.Clean(Json.S(d, "descTRA")); AugmentTags[id] = Json.A(d, "augmentDisplayTags").Select(Convert.ToString).ToList(); AugmentRarities[id] = Json.S(d, "rarity"); if (Json.S(d, "augmentNameId").StartsWith("ARAM_", StringComparison.OrdinalIgnoreCase)) MayhemAugmentIds.Add(id); } } catch { }
   staticReady = true;
  }

  public async Task<string> ReadSelectedChampion() {
   string phase = Json.Serializer().Deserialize<string>(await LcuGet("/lol-gameflow/v1/gameflow-phase")); if (phase != "ChampSelect") return "";
   if (ChampionNames.Count == 0) await LoadStatic();
   int selected = 0; try { selected = Json.Serializer().Deserialize<int>(await LcuGet("/lol-champ-select/v1/current-champion")); } catch { }
   if (selected <= 0) { var session = Json.Serializer().Deserialize<Dictionary<string, object>>(await LcuGet("/lol-champ-select/v1/session")); selected = SelectionResolver.Champion(session, 0); }
   string alias; return ChampionNames.TryGetValue(selected, out alias) ? alias : "";
  }

  readonly System.Threading.SemaphoreSlim itemSetWrite = new System.Threading.SemaphoreSlim(1, 1);

  public async Task<string> ExportItemSet(string championName, int championId, string modeKey, string role, List<int> starters, List<int> coreIds, List<int> bootIds, List<int> situationalIds) {
   await itemSetWrite.WaitAsync();
   try {
    return await WriteItemSet(championName, championId, modeKey, role, starters, coreIds, bootIds, situationalIds);
   } finally { itemSetWrite.Release(); }
  }

  async Task<string> WriteItemSet(string championName, int championId, string modeKey, string role,
   List<int> starters, List<int> coreIds, List<int> bootIds, List<int> situationalIds) {
   string modeLabel = (modeKey == "SR" ? "Summoner's Rift" : modeKey == "MAYHEM" ? "ARAM Mayhem" : modeKey == "ARENA" ? "Arena" : "ARAM")
    + (String.IsNullOrEmpty(role) ? "" : " · " + MultiModeParser.RoleDisplay(role));
   string uid = "maylo-" + Norm(championName) + "-" + Norm(modeKey) +
    (String.IsNullOrEmpty(role) ? "" : "-" + Norm(role));
   var summoner = Json.Serializer().Deserialize<Dictionary<string, object>>(await LcuGet("/lol-summoner/v1/current-summoner"));
   string summonerId = Json.S(summoner, "summonerId");
   if (String.IsNullOrEmpty(summonerId)) throw new Exception("Summoner ID unavailable");
   var existing = Json.Serializer().Deserialize<Dictionary<string, object>>(await LcuGet("/lol-item-sets/v1/item-sets/" + summonerId + "/sets"));
   var sets = Json.A(existing, "itemSets").Select(Json.D).Where(d => d != null && Json.S(d, "uid") != uid).Cast<object>().ToList();

   Func<string, List<int>, object> makeBlock = (title, list) => new {
    type = title,
    items = list.Where(x => x > 0).GroupBy(id => id)
     .Select(group => new { id = group.Key.ToString(), count = title == "Starters" ? group.Count() : 1 }).ToArray()
   };
   var blocks = new List<object>();
   if (starters.Count > 0) blocks.Add(makeBlock("Starters", starters));
   if (coreIds.Count > 0) blocks.Add(makeBlock("maylo Core Build (" + modeLabel + ")", coreIds));
   if (bootIds.Count > 0) blocks.Add(makeBlock("Boots", bootIds));
   if (situationalIds.Count > 0) blocks.Add(makeBlock("Situational", situationalIds));

   sets.Insert(0, new {
    uid = uid,
    title = "maylo · " + championName + " (" + modeLabel + ")",
    type = "custom",
    map = "any",
    mode = "any",
    associatedChampions = championId > 0 ? new[] { championId } : new int[0],
    associatedMaps = new int[0],
    blocks = blocks.ToArray()
   });
   string payload = Json.Serializer().Serialize(new { accountId = Json.L(existing, "accountId"), timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), itemSets = sets.ToArray() });
   await LcuRequest("PUT", "/lol-item-sets/v1/item-sets/" + summonerId + "/sets", payload);
   return "Exported \"" + championName + "\" build to League in-game shop!";
  }

  public async Task<string> ImportRunes(RunePage page) {
   if (page == null || page.Perks.Count < 6) throw new Exception("No rune page available for this mode.");
   var pages = Json.Serializer().DeserializeObject(await LcuGet("/lol-perks/v1/pages")) as object[];
   var pageDicts = (pages ?? new object[0]).Select(Json.D).Where(d => d != null).ToList();
   const string name = "maylo import";
   var existing = pageDicts.FirstOrDefault(d => Json.S(d, "isEditable").Equals("true", StringComparison.OrdinalIgnoreCase) &&
     (Json.S(d, "name") == name || ((page.Name ?? "").StartsWith("maylo", StringComparison.OrdinalIgnoreCase) && Json.S(d, "name") == page.Name)));
   int id = Json.I(existing, "id");
   var body = Json.D(existing);
   body = body == null ? new Dictionary<string, object>() : new Dictionary<string, object>(body);
   body["name"] = name;
   body["primaryStyleId"] = page.PrimaryStyle;
   body["subStyleId"] = page.SecondaryStyle;
   body["selectedPerkIds"] = page.Perks.ToArray();
   body["current"] = true;
   if (id > 0) {
    await LcuRequest("PUT", "/lol-perks/v1/pages/" + id, Json.Serializer().Serialize(body));
    await LcuRequest("PUT", "/lol-perks/v1/currentpage", Json.Serializer().Serialize(id));
   } else {
    await LcuRequest("POST", "/lol-perks/v1/pages", Json.Serializer().Serialize(body));
   }
   return "Imported the Maylo rune page into League.";

  }

  public async Task ApplySpells(string champion, int[] pair, string flashKey = "F") {
   string phase = Json.Serializer().Deserialize<string>(await LcuGet("/lol-gameflow/v1/gameflow-phase")); if (phase != "ChampSelect") { spellSelection = ""; return; }
   var session = Json.Serializer().Deserialize<Dictionary<string, object>>(await LcuGet("/lol-champ-select/v1/session")); var me = Json.A(session, "myTeam").Select(Json.D).FirstOrDefault(p => p != null && Json.I(p, "cellId") == Json.I(session, "localPlayerCellId"));
   string alias; int chosen = SelectionResolver.Champion(session, 0); if (me == null || !ChampionNames.TryGetValue(chosen, out alias) || Norm(alias) != Norm(champion)) { SpellStatus = "Waiting for your champion"; return; }
   string fk = String.Equals(flashKey, "D", StringComparison.OrdinalIgnoreCase) ? "D" : "F";
   string key = Json.S(session, "gameId") + ":" + Json.S(session, "localPlayerCellId") + ":" + champion + ":" + fk + ":" + (pair == null ? "" : String.Join(",", pair)); if (key == spellSelection) return;
   if (pair == null || pair.Length != 2 || pair.Any(x => x <= 0) || pair[0] == pair[1]) { SpellStatus = "Unknown spell combination"; return; }
   pair = SpellPlan.OrderForFlash(pair, fk, Json.I(me, "spell1Id"), Json.I(me, "spell2Id"));
   if (pair[0] == Json.I(me, "spell1Id") && pair[1] == Json.I(me, "spell2Id")) { spellSelection = key; SpellStatus = "Spells ready (Flash on " + fk + ")"; return; }
   await LcuRequest("PATCH", "/lol-champ-select/v1/session/my-selection", Json.Serializer().Serialize(new { spell1Id = pair[0], spell2Id = pair[1] }));
   spellSelection = key; SpellStatus = "Spells applied (Flash on " + fk + ")";
  }

  public void ResetSpellSelection() { spellSelection = ""; runeSelection = ""; shopSelection = ""; }

  public async Task ApplyShopAuto(string championName, int championId, string modeLabel, string role, List<int> starters, List<int> coreIds, List<int> bootIds, List<int> situationalIds) {
   if (coreIds == null || coreIds.Count == 0) return;
   string key = championName + ":" + modeLabel + ":" + (role ?? "") + ":" +
    String.Join(",", coreIds) + ":" + String.Join(",", starters) + ":" + String.Join(",", bootIds) + ":" + String.Join(",", situationalIds);
   if (key == shopSelection) return;
   await ExportItemSet(championName, championId, modeLabel, role, starters, coreIds, bootIds, situationalIds);
   shopSelection = key;
   ShopStatus = "Shop auto-exported (" + championName + (String.IsNullOrEmpty(role) ? "" : " " + MultiModeParser.RoleDisplay(role)) + ")";
  }

  public async Task ApplyRunesAuto(string champion, string modeKey, string role, RunePage page) {
   if (page == null || page.Perks.Count < 6) { RuneStatus = "No rune page for " + modeKey; return; }
   string phase = Json.Serializer().Deserialize<string>(await LcuGet("/lol-gameflow/v1/gameflow-phase")); if (phase != "ChampSelect") { runeSelection = ""; return; }
   var session = Json.Serializer().Deserialize<Dictionary<string, object>>(await LcuGet("/lol-champ-select/v1/session"));
   string alias; int chosen = SelectionResolver.Champion(session, 0);
   if (!ChampionNames.TryGetValue(chosen, out alias) || Norm(alias) != Norm(champion)) { RuneStatus = "Waiting for your champion"; return; }
   string key = Json.S(session, "gameId") + ":" + Json.S(session, "localPlayerCellId") + ":" + champion + ":" + modeKey + ":" + (role ?? "");
   if (key == runeSelection) return;
   await ImportRunes(page);
   runeSelection = key;
   RuneStatus = "Runes applied (" + modeKey + (String.IsNullOrEmpty(role) ? "" : " " + MultiModeParser.RoleDisplay(role)) + ")";
  }

  public async Task<AccountProfile> ReadAccount() {
   var profile = ProfileParser.Account(Json.Serializer().Deserialize<Dictionary<string, object>>(await LcuGet("/lol-summoner/v1/current-summoner")));
   if (profile.Key == "") throw new Exception("No connected account found.");
   return profile;
  }

  public async Task<List<CollectionSkin>> ReadSkinCollection(AccountProfile account) {
   long summonerId;
   if (account == null || !Int64.TryParse(account.SummonerId, out summonerId) || summonerId <= 0)
    return new List<CollectionSkin>();
   var response = await LcuGet("/lol-champions/v1/inventories/" + summonerId + "/champions");
   return ProfileParser.Skins(Json.Serializer().DeserializeObject(response));
  }

  public async Task<RankSnapshot> ReadSoloRank() {
   var root = Json.D(Json.Serializer().DeserializeObject(await LcuGet("/lol-ranked/v1/current-ranked-stats")));
   return ProfileParser.SoloRank(root, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
  }

  public async Task<List<MatchRecord>> ReadHistory(AccountProfile account, int count) {
   var root = Json.Serializer().Deserialize<Dictionary<string, object>>(await LcuGet("/lol-match-history/v1/products/lol/current-summoner/matches?begIndex=0&endIndex=" + (Math.Max(1, Math.Min(100, count)) - 1)));
   account.Region = Json.S(root, "platformId");
   return ProfileParser.Matches(root, account);
  }

  public async Task<GameSnapshot> Read() {
   var snapshot = new GameSnapshot();
   try {
    if (DateTime.UtcNow < nextLiveProbe) throw new Exception("Client phase cached");
    var game = Json.Serializer().Deserialize<Dictionary<string, object>>(await Get("https://127.0.0.1:2999/liveclientdata/allgamedata"));
    if (game == null) throw new Exception("Empty live game data");
    var active = game.ContainsKey("activePlayer") ? Json.D(game["activePlayer"]) : null;
    var me = Json.A(game, "allPlayers").Select(Json.D).FirstOrDefault(p => p != null && new[] { "riotId", "summonerName" }.Any(k => Json.S(p, k) != "" && Json.S(p, k) == Json.S(active, k)));
    if (me != null) {
     snapshot.InGame = true; snapshot.Phase = "In game";
     snapshot.Champion = Json.S(me, "rawChampionName").Replace("game_character_displayname_", "");
     if (snapshot.Champion == "") snapshot.Champion = Json.S(me, "championName");
     snapshot.Gold = active != null && active.ContainsKey("currentGold") ? Convert.ToDouble(active["currentGold"], System.Globalization.CultureInfo.InvariantCulture) : -1;
     foreach (var i in Json.A(me, "items")) snapshot.Owned.Add(Json.I(Json.D(i), "itemID"));
     var data = game.ContainsKey("gameData") ? Json.D(game["gameData"]) : null;
     snapshot.Mode = Json.S(data, "gameMode");
     if (snapshot.Mode == "" && data != null) {
      int mapNum = Json.I(data, "mapNumber");
      if (mapNum == 12) snapshot.Mode = "ARAM";
      else if (mapNum == 30) snapshot.Mode = "CHERRY";
      else if (mapNum == 11) snapshot.Mode = "CLASSIC";
     }
     snapshot.Mayhem = IsMayhemMode(snapshot.Mode);
     try {
      await LoadStatic();
      var flow = Json.Serializer().Deserialize<Dictionary<string, object>>(await LcuGet("/lol-gameflow/v1/session"));
      ApplyFlowSession(snapshot, flow, true);
     } catch { }
     if (snapshot.QueueId == 0 && (snapshot.Mode == "" || snapshot.Mode == "ARAM")) snapshot.Mayhem |= lastMayhem;
     snapshot.Live = LiveParser.Parse(game, snapshot.ModeKey, snapshot.QueueId);
     var meLive = snapshot.Live.Players.FirstOrDefault(p => p.Me);
     string livePos = meLive != null ? MultiModeParser.NormalizeRole(meLive.Position) : "";
     if (livePos != "") snapshot.AssignedRole = livePos;
     else if (meLive != null && (String.Equals(meLive.SpellKey1, "SummonerSmite", StringComparison.OrdinalIgnoreCase) || String.Equals(meLive.SpellKey2, "SummonerSmite", StringComparison.OrdinalIgnoreCase))) snapshot.AssignedRole = "jungle";
     else if (snapshot.Owned.Any(id => id == 3865 || id == 3866 || id == 3867 || id == 3869 || id == 3870 || id == 3871 || id == 3876 || id == 3877)) snapshot.AssignedRole = "support";
     else snapshot.AssignedRole = lastAssignedRole;
     snapshot.Window = Native.Target(true);
     return snapshot;
    }
   } catch { nextLiveProbe = DateTime.UtcNow.AddSeconds(3); }

   await LoadStatic();
   string phase = Json.Serializer().Deserialize<string>(await LcuGet("/lol-gameflow/v1/gameflow-phase"));
   snapshot.Phase = phase ?? "Connected";
   snapshot.Window = Native.Target(false);
   if (phase == "ChampSelect") {
    var session = Json.Serializer().Deserialize<Dictionary<string, object>>(await LcuGet("/lol-champ-select/v1/session"));
    int cell = session != null && session.ContainsKey("localPlayerCellId") ? Json.I(session, "localPlayerCellId") : -1;
    int selfId = 0;
    Dictionary<string, object> meCell = null;
    foreach (var v in Json.A(session, "myTeam")) {
     var d = Json.D(v); if (d == null) continue;
     int id = Json.I(d, "championId");
     int intent = Json.I(d, "championPickIntent");
     int effectiveId = id > 0 ? id : intent;
     if (effectiveId > 0) snapshot.Team.Add(effectiveId);
     if (Json.I(d, "cellId") == cell) {
      meCell = d;
      selfId = effectiveId;
      snapshot.Spell1Id = Json.I(d, "spell1Id");
      snapshot.Spell2Id = Json.I(d, "spell2Id");
      if (ChampionNames.ContainsKey(effectiveId)) snapshot.Champion = ChampionNames[effectiveId];
      string pos = MultiModeParser.NormalizeRole(Json.S(d, "assignedPosition"));
      if (pos != "") { snapshot.AssignedRole = pos; lastAssignedRole = pos; }
     }
    }
    Action<object, bool> addBenchOrCard = (obj, forceCard) => {
     int bcid = 0;
     bool prio = forceCard;
     var bd = Json.D(obj);
     if (bd != null) {
      bcid = Json.I(bd, "championId");
      if (bcid <= 0) bcid = Json.I(bd, "id");
      if (Json.S(bd, "isPriority").Equals("true", StringComparison.OrdinalIgnoreCase)) prio = true;
     } else {
      bcid = Json.ToInt(obj);
     }
     if (bcid <= 0 || !ChampionNames.ContainsKey(bcid)) return;
     if (!forceCard && !snapshot.Bench.Contains(bcid)) snapshot.Bench.Add(bcid);
     if (prio && !snapshot.PriorityCards.Contains(bcid)) snapshot.PriorityCards.Add(bcid);
    };
    foreach (var v in Json.A(session, "benchChampions")) addBenchOrCard(v, false);
    foreach (var v in Json.A(session, "benchChampionIds")) addBenchOrCard(v, false);
    // Inspect any card/choice/subset fields on session or local player cell (ARAM / ARAM Mayhem Champion Cards)
    Action<Dictionary<string, object>> scanCardKeys = dict => {
     if (dict == null) return;
     foreach (var kv in dict) {
      string kl = (kv.Key ?? "").ToLowerInvariant();
      if (!(kl.Contains("card") || kl.Contains("choice") || kl.Contains("option") || kl.Contains("subset"))) continue;
      foreach (var item in Json.Array(kv.Value)) addBenchOrCard(item, true);
     }
    };
    scanCardKeys(session);
    scanCardKeys(meCell);

    int selected = SelectionResolver.Champion(session, 0);
    if (selected > 0) selfId = selected;
    if (ChampionNames.ContainsKey(selected)) snapshot.Champion = ChampionNames[selected];
    snapshot.InSelection = true;
    try {
     var flow = Json.Serializer().Deserialize<Dictionary<string, object>>(await LcuGet("/lol-gameflow/v1/session"));
     ApplyFlowSession(snapshot, flow, false);
    } catch { }
    if (snapshot.ModeKey == "SR") SelectionResolver.ReadDraftPicks(session, snapshot);
    if (!snapshot.Mayhem && snapshot.Mode == "" && snapshot.Bench.Count > 0) snapshot.Mode = "ARAM";

    // Capture ARAM / ARAM Mayhem Champion Cards from subset/pickable endpoints during the opening flip window
    int subsetCount = -1, pickableCount = -1;
    if (snapshot.Mayhem || snapshot.Mode == "ARAM" || snapshot.QueueId == 2400 || snapshot.QueueId == 3270 || snapshot.QueueId == 450) {
     if (snapshot.PriorityCards.Count == 0) {
      try {
       var subset = Json.Serializer().DeserializeObject(await LcuGet("/lol-champ-select/v1/subset-champion-list")) as object[];
       subsetCount = subset == null ? 0 : subset.Length;
       if (subset != null && subset.Length >= 1 && subset.Length <= 6) {
        foreach (var item in subset) addBenchOrCard(item, true);
       }
      } catch { }
     }
     if (snapshot.PriorityCards.Count == 0) {
      try {
       var pickable = Json.Serializer().DeserializeObject(await LcuGet("/lol-champ-select/v1/pickable-champion-ids")) as object[];
       pickableCount = pickable == null ? 0 : pickable.Length;
       if (pickable != null && pickable.Length >= 1 && pickable.Length <= 6) {
        foreach (var item in pickable) addBenchOrCard(item, true);
       }
      } catch { }
     }
    }

    // Write a tiny non-PII structural snapshot once per champ select to diagnose Riot ARAM card fields
    try {
     string dbgKey = Json.S(session, "gameId") + ":" + snapshot.QueueId + ":" + snapshot.Bench.Count + ":" + snapshot.PriorityCards.Count;
     if (dbgKey != lastChampSelectDebugKey) {
      lastChampSelectDebugKey = dbgKey;
      Directory.CreateDirectory(WebCache.Root);
      var dbgObj = new {
       ts = DateTime.UtcNow.ToString("o"),
       mode = snapshot.Mode,
       queueId = snapshot.QueueId,
       modeKey = snapshot.ModeKey,
       selfId = selfId,
       bench = snapshot.Bench.ToArray(),
       priorityCards = snapshot.PriorityCards.ToArray(),
       subsetCount = subsetCount,
       pickableCount = pickableCount,
       sessionKeys = session != null ? session.Keys.ToArray() : new string[0],
       meKeys = meCell != null ? meCell.Keys.ToArray() : new string[0],
       benchRaw = Json.A(session, "benchChampions").Take(5).ToArray(),
       timerPhase = session != null && session.ContainsKey("timer") ? Json.S(Json.D(session["timer"]), "phase") : ""
      };
      File.WriteAllText(Path.Combine(WebCache.Root, "champselect-debug.json"), Json.Serializer().Serialize(dbgObj), Encoding.UTF8);
     }
    } catch { }
   }
   if (phase == "Lobby") {
    lastChampion = ""; lastAssignedRole = ""; lastMayhem = false;
    try {
     var lobby = Json.Serializer().Deserialize<Dictionary<string, object>>(await LcuGet("/lol-lobby/v2/lobby"));
     var config = lobby != null && lobby.ContainsKey("gameConfig") ? Json.D(lobby["gameConfig"]) : null;
     snapshot.Mode = Json.S(config, "gameMode"); snapshot.QueueId = Json.I(config, "queueId");
     if (snapshot.Mode == "" && config != null) {
      int mid = Json.I(config, "mapId");
      if (mid == 12) snapshot.Mode = "ARAM";
      else if (mid == 11) snapshot.Mode = "CLASSIC";
     }
     snapshot.Mayhem = IsMayhemMode(snapshot.Mode, snapshot.QueueId);
    } catch { }
   }
   if (phase == "None" || phase == "EndOfGame" || phase == "PreEndOfGame" || phase == "WaitingForStats") {
    lastChampion = ""; lastAssignedRole = ""; lastMayhem = false;
   }
   if (phase == "GameStart" || phase == "InProgress" || phase == "Reconnect") {
    nextLiveProbe = DateTime.MinValue;
    snapshot.Window = Native.Target(true); snapshot.InGame = snapshot.Window != IntPtr.Zero; snapshot.Champion = lastChampion;
    try {
     var flow = Json.Serializer().Deserialize<Dictionary<string, object>>(await LcuGet("/lol-gameflow/v1/session"));
     ApplyFlowSession(snapshot, flow, false);
    } catch { snapshot.Mayhem = lastMayhem && snapshot.Mode == ""; }
   }
   if (snapshot.Champion != "") lastChampion = snapshot.Champion;
   if (phase == "Lobby" || phase == "ChampSelect") lastMayhem = snapshot.Mayhem;
   return snapshot;
  }
 }

 public static partial class Native {
  [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; public int Width { get { return Right - Left; } } public int Height { get { return Bottom - Top; } } }
  [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out Rect r);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref Point p);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
  [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr h, int i, int v);
  [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint key);
  [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
  [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
  [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point pt);
  [DllImport("user32.dll")] public static extern bool ReleaseCapture();
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
  [DllImport("user32.dll")] public static extern int GetSystemMetrics(int nIndex);
  [DllImport("user32.dll", SetLastError = true)] public static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);
  [DllImport("gdi32.dll")] public static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int ellipseW, int ellipseH);
  [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr hObject);
  [DllImport("user32.dll")] public static extern int SetWindowRgn(IntPtr hwnd, IntPtr rgn, bool redraw);
  [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
  delegate bool WindowVisitor(IntPtr h, IntPtr data);
  [DllImport("user32.dll")] static extern bool EnumWindows(WindowVisitor visitor, IntPtr data);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);

  public static bool OurWindow(IntPtr h) { uint pid; GetWindowThreadProcessId(h, out pid); return pid == (uint)Process.GetCurrentProcess().Id; }
  public static bool SameApplication(IntPtr a, IntPtr b) { uint pa, pb; if (a == IntPtr.Zero || b == IntPtr.Zero) return false; GetWindowThreadProcessId(a, out pa); GetWindowThreadProcessId(b, out pb); return pa != 0 && pa == pb; }

  public static void SetBorderTheme(IntPtr hwnd, bool dark) {
   if (hwnd == IntPtr.Zero) return;
   try {
    int darkVal = dark ? 1 : 0;
    DwmSetWindowAttribute(hwnd, 20, ref darkVal, 4); // DWMWA_USE_IMMERSIVE_DARK_MODE
    int borderColor = dark ? 0x0026211F : 0x00E4E4E7; // Subtle dark charcoal in dark mode, soft grey in light mode (NO white halo!)
    DwmSetWindowAttribute(hwnd, 34, ref borderColor, 4); // DWMWA_BORDER_COLOR
   } catch { }
  }

  public static bool RoundWindow(IntPtr hwnd, bool dark = true) {
   try {
    int rounded = 2; // DWMWCP_ROUND
    int hr = DwmSetWindowAttribute(hwnd, 33, ref rounded, 4);
    if (hr == 0) {
     SetBorderTheme(hwnd, dark);
     return true;
    }
   } catch { }
   return false;
  }

  public static bool ForceForeground(IntPtr hwnd, bool makeTopmost = false) {
   if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return false;
   if (IsIconic(hwnd)) ShowWindow(hwnd, 9);
   if (GetForegroundWindow() == hwnd) return true;
   // NEVER promote League of Legends to HWND_TOPMOST (-1) or attach to another process's UI thread!
   if (makeTopmost) SetWindowPos(hwnd, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0040);
   else SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0040);
   BringWindowToTop(hwnd);
   return SetForegroundWindow(hwnd);
  }

  public static IntPtr Target(bool game) {
   if (game) {
    // Primary fast lookup (Hexsight pattern): direct RiotWindowClass HWND!
    IntPtr direct = FindWindow("RiotWindowClass", "League of Legends (TM) Client");
    if (direct == IntPtr.Zero) direct = FindWindow("RiotWindowClass", null);
    if (direct != IntPtr.Zero && IsWindowVisible(direct) && !IsIconic(direct)) {
     Rect dr; if (GetClientRect(direct, out dr) && dr.Width >= 200 && dr.Height >= 200) return direct;
    }
   }
   var processes = Process.GetProcessesByName(game ? "League of Legends" : "LeagueClientUx");
   var ids = new HashSet<int>(processes.Select(p => p.Id)); foreach (var p in processes) p.Dispose();
   IntPtr best = IntPtr.Zero; long area = 0;
   EnumWindows((h, d) => {
    uint pid; GetWindowThreadProcessId(h, out pid); Rect r;
    if (ids.Contains((int)pid) && IsWindowVisible(h) && !IsIconic(h) && GetClientRect(h, out r)) {
     long a = (long)r.Width * r.Height; if (a > area) { area = a; best = h; }
    }
    return true;
   }, IntPtr.Zero);
   return best;
  }

  public static Rect Bounds(IntPtr h) {
   Rect r; if (h == IntPtr.Zero || !GetClientRect(h, out r)) return new Rect();
   var p = new Point(); ClientToScreen(h, ref p);
   return new Rect { Left = p.X, Top = p.Y, Right = p.X + r.Width, Bottom = p.Y + r.Height };
  }
 }
}
