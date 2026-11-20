using System;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MayhemCompanion {
 public class ModeBuildData {
  public string ChampionSlug = "";
  public string ModeKey = "MAYHEM";
  public string SourceLabel = "MayhemMeta · ARAM Mayhem";
  public string ActiveRole = "";
  public string ActiveElo = "emerald_plus";
  public List<string> AvailableRoles = new List<string>();
  public List<Stat> Items = new List<Stat>();
  public List<Stat> Starters = new List<Stat>();
  public List<Stat> Boots = new List<Stat>();
  public List<Stat> BestItems = new List<Stat>();
  public List<Stat> Spells = new List<Stat>();
  public List<Stat> Augments = new List<Stat>();
  public List<Stat> Combos = new List<Stat>();
  public List<Stat> Teammates = new List<Stat>();
  public List<Stat> Matchups = new List<Stat>();
  public List<CoreBuildPath> CorePaths = new List<CoreBuildPath>();
  public RunePage Runes;
  public SkillOrder Skills;
 }

 public static class MultiModeParser {
  public static string DDragonVersion = "";
  public static Dictionary<int, Champion> DDragonChamps = new Dictionary<int, Champion>();
  public static Dictionary<string, int> ChampIdBySlug = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
  public static Dictionary<int, string> DDragonItemNames = new Dictionary<int, string>();
  public static Dictionary<int, int> DDragonItemPrices = new Dictionary<int, int>();
  public static Dictionary<int, Dictionary<string, object>> DDragonItems = new Dictionary<int, Dictionary<string, object>>();
  public static Dictionary<int, RuneMeta> DDragonRunes = new Dictionary<int, RuneMeta>();
  public static Dictionary<int, string> AugmentDescById = new Dictionary<int, string>();
  public static Dictionary<string, string> AugmentDescByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
  public static Dictionary<int, List<string>> AugmentAliasesById = new Dictionary<int, List<string>>();
  public static Dictionary<int, string> AugmentRarityById = new Dictionary<int, string>();
  static Dictionary<int, List<string>> cachedSrRoles = new Dictionary<int, List<string>>();
  static bool ddLoaded = false, augMetaLoaded = false;
  static Task ddTask = null, augMetaTask = null;

  static readonly HashSet<int> BootIds = new HashSet<int> { 3006, 3009, 3020, 3047, 3111, 3117, 3158, 3010, 3013 };
  public static readonly HashSet<int> StarterIds = new HashSet<int> {
   1054, 1055, 1056, 1082, 1083, 2003, 2031, 2051, 3112, 3184, 3865,
   1101, 1102, 1103
  };
  static readonly Dictionary<int, Tuple<string, string>> SpellById = new Dictionary<int, Tuple<string, string>> {
   { 1, Tuple.Create("Cleanse", "SummonerBoost") },
   { 3, Tuple.Create("Exhaust", "SummonerExhaust") },
   { 4, Tuple.Create("Flash", "SummonerFlash") },
   { 6, Tuple.Create("Ghost", "SummonerHaste") },
   { 7, Tuple.Create("Heal", "SummonerHeal") },
   { 11, Tuple.Create("Smite", "SummonerSmite") },
   { 12, Tuple.Create("Teleport", "SummonerTeleport") },
   { 13, Tuple.Create("Clarity", "SummonerMana") },
   { 14, Tuple.Create("Ignite", "SummonerDot") },
   { 21, Tuple.Create("Barrier", "SummonerBarrier") },
   { 32, Tuple.Create("Mark", "SummonerSnowball") }
  };

  public static string NormalizeRole(string raw) {
   string r = (raw ?? "").Trim().ToLowerInvariant();
   if (r == "top") return "top";
   if (r == "jungle" || r == "jng" || r == "jg") return "jungle";
   if (r == "middle" || r == "mid") return "mid";
   if (r == "bottom" || r == "bot" || r == "adc") return "adc";
   if (r == "utility" || r == "support" || r == "sup" || r == "supp") return "support";
   return "";
  }

  public static string RoleDisplay(string role) {
   string r = NormalizeRole(role);
   if (r == "top") return "Top";
   if (r == "jungle") return "Jungle";
   if (r == "mid") return "Mid";
   if (r == "adc") return "ADC";
   if (r == "support") return "Support";
   return "All";
  }

  public static string EloDisplay(string elo) {
   switch ((elo ?? "").ToLowerInvariant()) {
    case "all": return "All Ranks";
    case "gold_plus": return "Gold+";
    case "platinum_plus": return "Platinum+";
    case "diamond_plus": return "Diamond+";
    case "master_plus": return "Master+";
    default: return "Emerald+";
   }
  }

  public static string MapOpGgTier(int t) {
   if (t == 1) return "S+";
   if (t == 2) return "S";
   if (t == 3) return "A";
   if (t == 4) return "B";
   if (t == 5) return "C";
   return "D";
  }

  static double Dbl(Dictionary<string, object> d, string key) {
   if (d == null || !d.ContainsKey(key) || d[key] == null) return 0;
   try { return Convert.ToDouble(d[key], System.Globalization.CultureInfo.InvariantCulture); } catch { return 0; }
  }

  public static string ItemNameOf(int id, Dictionary<int, Dictionary<string, object>> lcuItems = null) {
   Dictionary<string, object> d;
   if (lcuItems != null && lcuItems.TryGetValue(id, out d) && Json.S(d, "name") != "") return Json.S(d, "name");
   string n;
   if (DDragonItemNames.TryGetValue(id, out n) && n != "") return n;
   return "Item " + id;
  }

  public static string ItemIconOf(int id) {
   if (!String.IsNullOrEmpty(DDragonVersion)) return "https://ddragon.leagueoflegends.com/cdn/" + DDragonVersion + "/img/item/" + id + ".png";
   return "/icons/items/" + id + ".png";
  }

  public static string ChampIconOf(Champion c) {
   if (c != null && !String.IsNullOrEmpty(c.Icon)) return c.Icon;
   if (c != null && !String.IsNullOrEmpty(DDragonVersion) && !String.IsNullOrEmpty(c.AssetKey))
    return "https://ddragon.leagueoflegends.com/cdn/" + DDragonVersion + "/img/champion/" + c.AssetKey + ".png";
   return "";
  }

  public static Task EnsureDDragon(List<Champion> mayhemChamps = null, bool force = false) {
   if (!force && ddLoaded && DDragonChamps.Count > 100) return EnsureAugmentMeta(false);
   if (!force && ddTask != null && !ddTask.IsCompleted) return ddTask;
   ddTask = EnsureDDragonCore(mayhemChamps, force);
   return ddTask;
  }

  static async Task EnsureDDragonCore(List<Champion> mayhemChamps, bool force) {
   try {
    string versRaw = await WebCache.Page("dd-versions", "https://ddragon.leagueoflegends.com/api/versions.json", force, s => s.StartsWith("["), ".json", 240);
    var vers = Json.Array(Json.Serializer().DeserializeObject(versRaw));
    if (vers.Length == 0) return;
    DDragonVersion = Convert.ToString(vers[0]);

    string chRaw = await WebCache.Page("dd-champions-" + DDragonVersion, "https://ddragon.leagueoflegends.com/cdn/" + DDragonVersion + "/data/en_US/champion.json", force, s => s.Contains("\"data\""), ".json", 1440);
    var chRoot = Json.D(Json.Serializer().DeserializeObject(chRaw));
    var chData = chRoot != null && chRoot.ContainsKey("data") ? Json.D(chRoot["data"]) : null;
    if (chData != null) {
     foreach (var kv in chData) {
      var d = Json.D(kv.Value);
      int id = Json.I(d, "key");
      string alias = Json.S(d, "id");
      string name = Json.S(d, "name");
      if (id <= 0) continue;
      Champion existing = mayhemChamps != null
       ? mayhemChamps.FirstOrDefault(m => GameBridge.Norm(m.AssetKey) == GameBridge.Norm(alias) || GameBridge.Norm(m.Name) == GameBridge.Norm(name))
       : null;
      string slug = existing != null ? existing.Slug : GameBridge.Norm(alias);
      string icon = existing != null && !String.IsNullOrEmpty(existing.Icon)
       ? existing.Icon
       : ("https://ddragon.leagueoflegends.com/cdn/" + DDragonVersion + "/img/champion/" + alias + ".png");
      DDragonChamps[id] = new Champion { Name = name, Slug = slug, Icon = icon, Tier = existing != null ? existing.Tier : "B", Win = existing != null ? existing.Win : 50, Pick = existing != null ? existing.Pick : 0 };
      ChampIdBySlug[slug] = id;
      ChampIdBySlug[GameBridge.Norm(alias)] = id;
      ChampIdBySlug[GameBridge.Norm(name)] = id;
     }
    }

    string itRaw = await WebCache.Page("dd-items-" + DDragonVersion, "https://ddragon.leagueoflegends.com/cdn/" + DDragonVersion + "/data/en_US/item.json", false, s => s.Contains("\"data\""), ".json", 1440);
    var itRoot = Json.D(Json.Serializer().DeserializeObject(itRaw));
    var itData = itRoot != null && itRoot.ContainsKey("data") ? Json.D(itRoot["data"]) : null;
    if (itData != null) {
     foreach (var kv in itData) {
      int id; if (!int.TryParse(kv.Key, out id) || id <= 0) continue;
      var d = Json.D(kv.Value);
      string name = Json.S(d, "name");
      var gold = d != null && d.ContainsKey("gold") ? Json.D(d["gold"]) : null;
      int total = gold != null ? Json.I(gold, "total") : 0;
      DDragonItemNames[id] = name;
      DDragonItemPrices[id] = total;
      var fromIds = Json.A(d, "from").Select(x => { int n; int.TryParse(Convert.ToString(x), out n); return n; }).Where(n => n > 0).Cast<object>().ToArray();
      DDragonItems[id] = new Dictionary<string, object> {
       { "id", id }, { "name", name }, { "priceTotal", total }, { "from", fromIds }, { "description", Json.S(d, "description") }, { "categories", Json.A(d, "tags") }
      };
     }
    }

    // Load Riot Data Dragon runesReforged.json + Stat Shards for full visual rune icons & names!
    try {
     string rRaw = await WebCache.Page("dd-runes-" + DDragonVersion, "https://ddragon.leagueoflegends.com/cdn/" + DDragonVersion + "/data/en_US/runesReforged.json", false, s => s.StartsWith("["), ".json", 1440);
     foreach (var treeObj in Json.Array(Json.Serializer().DeserializeObject(rRaw))) {
      var td = Json.D(treeObj);
      int tid = Json.I(td, "id");
      if (tid > 0) DDragonRunes[tid] = new RuneMeta { Id = tid, Name = Json.S(td, "name"), Icon = "https://ddragon.leagueoflegends.com/cdn/img/" + Json.S(td, "icon") };
      foreach (var slotObj in Json.A(td, "slots")) {
       foreach (var rObj in Json.A(Json.D(slotObj), "runes")) {
        var rd = Json.D(rObj); int rid = Json.I(rd, "id");
        if (rid > 0) DDragonRunes[rid] = new RuneMeta { Id = rid, Name = Json.S(rd, "name"), Icon = "https://ddragon.leagueoflegends.com/cdn/img/" + Json.S(rd, "icon") };
       }
      }
     }
     var shards = new Dictionary<int, Tuple<string, string>> {
      { 5008, Tuple.Create("Adaptive Force", "perk-images/StatMods/StatModsAdaptiveForceIcon.png") },
      { 5005, Tuple.Create("Attack Speed", "perk-images/StatMods/StatModsAttackSpeedIcon.png") },
      { 5007, Tuple.Create("Ability Haste", "perk-images/StatMods/StatModsCDRScalingIcon.png") },
      { 5010, Tuple.Create("Move Speed", "perk-images/StatMods/StatModsMovementSpeedIcon.png") },
      { 5001, Tuple.Create("Health Scaling", "perk-images/StatMods/StatModsHealthPlusIcon.png") },
      { 5011, Tuple.Create("Health", "perk-images/StatMods/StatModsHealthScalingIcon.png") },
      { 5013, Tuple.Create("Tenacity", "perk-images/StatMods/StatModsTenacityIcon.png") }
     };
     foreach (var kv in shards) DDragonRunes[kv.Key] = new RuneMeta { Id = kv.Key, Name = kv.Value.Item1, Icon = "https://ddragon.leagueoflegends.com/cdn/img/" + kv.Value.Item2 };
    } catch { }
    ddLoaded = true;
   } catch { }
   await EnsureAugmentMeta(force);
  }

  static string ResolveAugmentDesc(string raw, Dictionary<string, object> dataValues) {
   if (String.IsNullOrEmpty(raw)) return "";
   string s = raw;
   if (dataValues != null) {
    s = Regex.Replace(s, @"@([A-Za-z0-9_]+)(?:\*([\d\.]+))?@", m => {
     string key = m.Groups[1].Value;
     double mult = 1.0;
     if (m.Groups[2].Success) double.TryParse(m.Groups[2].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out mult);
     object valObj = null;
     foreach (var kv in dataValues) { if (String.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)) { valObj = kv.Value; break; } }
     if (valObj != null) {
      var arr = Json.Array(valObj);
      double v = 0;
      if (arr.Length > 0) { try { v = Convert.ToDouble(arr[0], System.Globalization.CultureInfo.InvariantCulture); } catch { } }
      else { try { v = Convert.ToDouble(valObj, System.Globalization.CultureInfo.InvariantCulture); } catch { } }
      double res = v * mult;
      return Math.Abs(res - Math.Round(res)) < 0.05 ? ((int)Math.Round(res)).ToString(System.Globalization.CultureInfo.InvariantCulture) : res.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
     }
     return "";
    });
   }
   s = Regex.Replace(s, @"@[^@]+@", "");
   s = Regex.Replace(s, @"%i:[A-Za-z0-9_]+%", "");
   s = Regex.Replace(s, @"<br\s*/?>", " ", RegexOptions.IgnoreCase);
   s = Parser.Clean(s);
   s = Regex.Replace(s, @"\s+([,\.%])", "$1");
   return s.Trim();
  }

  static void AddAugAlias(int id, string name) {
   if (id <= 0 || String.IsNullOrWhiteSpace(name)) return;
   List<string> list;
   if (!AugmentAliasesById.TryGetValue(id, out list)) { list = new List<string>(); AugmentAliasesById[id] = list; }
   if (!list.Contains(name)) list.Add(name);
  }

  public static Task EnsureAugmentMeta(bool force = false) {
   if (!force && augMetaLoaded && AugmentDescById.Count > 50) return Task.FromResult(0);
   if (!force && augMetaTask != null && !augMetaTask.IsCompleted) return augMetaTask;
   augMetaTask = EnsureAugmentMetaCore(force);
   return augMetaTask;
  }

  static async Task EnsureAugmentMetaCore(bool force) {
   try {
    var apiDesc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var arenaRawById = new Dictionary<int, Tuple<string, string>>();
    // 1. Load CommunityDragon Arena/Mayhem descriptions (FR first for French players, EN fallback)
    // NOTE: sanitize "__type" keys because .NET JavaScriptSerializer treats "__type" as a CLR type resolver!
    foreach (var lang in new[] { Tuple.Create("fr", "https://raw.communitydragon.org/latest/cdragon/arena/fr_fr.json"), Tuple.Create("en", "https://raw.communitydragon.org/latest/cdragon/arena/en_us.json") }) {
     try {
      string raw = await WebCache.Page("cd-arena-" + lang.Item1, lang.Item2, force, s => s.Contains("\"augments\""), ".json", 720);
      string safeRaw = raw.Replace("\"__type\"", "\"_cdtype\"");
      var root = Json.D(Json.Serializer().DeserializeObject(safeRaw));
      foreach (var o in Json.A(root, "augments")) {
       var d = Json.D(o); if (d == null) continue;
       int id = Json.I(d, "id");
       string name = Json.S(d, "name");
       string api = Json.S(d, "apiName");
       var dv = d.ContainsKey("dataValues") ? Json.D(d["dataValues"]) : null;
       string desc = ResolveAugmentDesc(Json.S(d, "desc"), dv);
       if (desc == "") desc = ResolveAugmentDesc(Json.S(d, "tooltip"), dv);
       if (id > 0) {
        AddAugAlias(id, name);
        if (desc != "" && !AugmentDescById.ContainsKey(id)) AugmentDescById[id] = desc;
        if (!arenaRawById.ContainsKey(id)) arenaRawById[id] = Tuple.Create(name, desc);
       }
       if (desc != "" && api != "" && !apiDesc.ContainsKey(api)) apiDesc[api] = desc;
       string nn = GameBridge.Norm(name);
       if (desc != "" && nn != "" && !AugmentDescByName.ContainsKey(nn)) AugmentDescByName[nn] = desc;
      }
     } catch { }
    }

    // 2. Load CommunityDragon cherry-augments.json (authoritative ARAM Mayhem IDs, EN + FR names & apiNames!)
    var cherryIds = new HashSet<int>();
    foreach (var lang in new[] { Tuple.Create("fr", "https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/fr_fr/v1/cherry-augments.json"), Tuple.Create("en", "https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/cherry-augments.json") }) {
     try {
      string raw = await WebCache.Page("cd-cherry-" + lang.Item1, lang.Item2, force, s => s.StartsWith("["), ".json", 720);
      foreach (var o in Json.Array(Json.Serializer().DeserializeObject(raw))) {
       var d = Json.D(o); if (d == null) continue;
       int id = Json.I(d, "id");
       string name = Json.S(d, "nameTRA");
       string api = Json.S(d, "augmentNameId").Replace("ARAM_", "");
       string rar = TierLogic.RarityKey(Json.S(d, "rarity"));
       if (id <= 0) continue;
       cherryIds.Add(id);
       AddAugAlias(id, name);
       if (rar != "" && !AugmentRarityById.ContainsKey(id)) AugmentRarityById[id] = rar;
       string desc = "";
       if (!AugmentDescById.TryGetValue(id, out desc)) {
        if (!apiDesc.TryGetValue(api, out desc) && id > 1000) AugmentDescById.TryGetValue(id % 1000, out desc);
        if (String.IsNullOrEmpty(desc)) AugmentDescByName.TryGetValue(GameBridge.Norm(name), out desc);
        if (!String.IsNullOrEmpty(desc)) AugmentDescById[id] = desc;
       }
       if (!String.IsNullOrEmpty(desc) && !String.IsNullOrEmpty(name) && !AugmentDescByName.ContainsKey(GameBridge.Norm(name))) AugmentDescByName[GameBridge.Norm(name)] = desc;
      }
     } catch { }
    }
    // Only apply +1000 fallback for IDs that did NOT have an authoritative cherry-augments entry
    foreach (var kv in arenaRawById) {
     int plus = kv.Key + 1000;
     if (!cherryIds.Contains(plus)) {
      AddAugAlias(plus, kv.Value.Item1);
      if (kv.Value.Item2 != "" && !AugmentDescById.ContainsKey(plus)) AugmentDescById[plus] = kv.Value.Item2;
     }
    }
    augMetaLoaded = true;
   } catch { }
  }

  public static string GetAugmentDescription(int id, string name, Dictionary<int, string> lcuDescs = null) {
   string desc;
   if (AugmentDescById.TryGetValue(id, out desc) && !String.IsNullOrWhiteSpace(desc)) return desc;
   if (id > 1000 && AugmentDescById.TryGetValue(id % 1000, out desc) && !String.IsNullOrWhiteSpace(desc)) return desc;
   if (!String.IsNullOrEmpty(name) && AugmentDescByName.TryGetValue(GameBridge.Norm(name), out desc) && !String.IsNullOrWhiteSpace(desc)) return desc;
   if (lcuDescs != null && lcuDescs.TryGetValue(id, out desc) && !String.IsNullOrWhiteSpace(desc)) return desc;
   return "Augment ARAM Mayhem / Arena. Description détaillée indisponible pour cet augment spécifique.";
  }

  public static Dictionary<int, List<string>> BuildAugmentAliasMap(Dictionary<int, string> lcuNames) {
   var map = new Dictionary<int, List<string>>();
   foreach (var kv in AugmentAliasesById) map[kv.Key] = new List<string>(kv.Value);
   if (lcuNames != null) {
    foreach (var kv in lcuNames) {
     if (String.IsNullOrWhiteSpace(kv.Value)) continue;
     List<string> list;
     if (!map.TryGetValue(kv.Key, out list)) { list = new List<string>(); map[kv.Key] = list; }
     if (!list.Contains(kv.Value)) list.Add(kv.Value);
    }
   }
   return map;
  }

  public static async Task<List<TierListEntry>> LoadTierList(string modeKey, string roleFilter, string elo, List<Champion> mayhemChamps, bool force = false) {
   await EnsureDDragon(mayhemChamps);
   var result = new List<TierListEntry>();

   if (modeKey == "MAYHEM") {
    int rank = 1;
    foreach (var c in (mayhemChamps ?? new List<Champion>()).OrderByDescending(x => x.Win)) {
     int cid = 0; ChampIdBySlug.TryGetValue(c.Slug, out cid);
     result.Add(new TierListEntry {
      Id = cid, Slug = c.Slug, Name = c.Name, Icon = ChampIconOf(c),
      Role = "MAYHEM", Tier = String.IsNullOrEmpty(c.Tier) ? "B" : c.Tier,
      Rank = rank++, WinRate = c.Win, PickRate = c.Pick, BanRate = 0, Kda = 0, Games = 0
     });
    }
    return result;
   }

   string safeElo = String.IsNullOrEmpty(elo) ? "emerald_plus" : elo.ToLowerInvariant();
   if (modeKey == "ARAM") {
    string raw = await WebCache.Page("opgg-tier-aram", "https://lol-api-champion.op.gg/api/global/champions/aram", force, s => s.Contains("\"data\""), ".json", 45);
    var root = Json.D(Json.Serializer().DeserializeObject(raw));
    foreach (var obj in Json.A(root, "data")) {
     var d = Json.D(obj);
     int id = Json.I(d, "id");
     Champion ch; if (!DDragonChamps.TryGetValue(id, out ch)) continue;
     var st = d.ContainsKey("average_stats") ? Json.D(d["average_stats"]) : null;
     if (st == null) continue;
     var td = st.ContainsKey("tier_data") ? Json.D(st["tier_data"]) : null;
     int tierNum = td != null ? Json.I(td, "tier") : Json.I(st, "tier");
     int rankNum = td != null ? Json.I(td, "rank") : Json.I(st, "rank");
     if (tierNum <= 0) tierNum = 5;
     result.Add(new TierListEntry {
      Id = id, Slug = ch.Slug, Name = ch.Name, Icon = ChampIconOf(ch),
      Role = "ARAM", Tier = MapOpGgTier(tierNum), Rank = rankNum > 0 ? rankNum : 999,
      WinRate = Math.Round(Dbl(st, "win_rate") * 100.0, 2),
      PickRate = Math.Round(Dbl(st, "pick_rate") * 100.0, 2),
      BanRate = 0, Kda = Math.Round(Dbl(st, "kda"), 2), Games = Json.I(st, "play")
     });
    }
    return result.OrderBy(x => TierOrder(x.Tier)).ThenBy(x => x.Rank).ThenByDescending(x => x.WinRate).ToList();
   }

   // Ranked SR Tier List by Role + Elo!
   string srRaw = await WebCache.Page("opgg-tier-sr-" + safeElo, "https://lol-api-champion.op.gg/api/global/champions/ranked?tier=" + safeElo, force, s => s.Contains("\"data\""), ".json", 45);
   var srRoot = Json.D(Json.Serializer().DeserializeObject(srRaw));
   string normFilter = NormalizeRole(roleFilter);
   foreach (var obj in Json.A(srRoot, "data")) {
    var d = Json.D(obj);
    int id = Json.I(d, "id");
    Champion ch; if (!DDragonChamps.TryGetValue(id, out ch)) continue;
    var posArr = Json.A(d, "positions");
    var roleList = new List<string>();
    foreach (var po in posArr) {
     var pd = Json.D(po);
     string pName = NormalizeRole(Json.S(pd, "name"));
     if (pName == "") continue;
     roleList.Add(pName);
     if (normFilter != "" && pName != normFilter) continue;
     var st = pd.ContainsKey("stats") ? Json.D(pd["stats"]) : null;
     if (st == null) continue;
     var td = st.ContainsKey("tier_data") ? Json.D(st["tier_data"]) : null;
     int tierNum = td != null ? Json.I(td, "tier") : 5;
     int rankNum = td != null ? Json.I(td, "rank") : 999;
     if (tierNum <= 0) tierNum = 5;
     var counterIcons = new List<string>();
     foreach (var co in Json.A(pd, "counters").Take(3)) {
      var cd = Json.D(co);
      Champion cch; if (DDragonChamps.TryGetValue(Json.I(cd, "champion_id"), out cch)) counterIcons.Add(ChampIconOf(cch));
     }
     result.Add(new TierListEntry {
      Id = id, Slug = ch.Slug, Name = ch.Name, Icon = ChampIconOf(ch),
      Role = pName.ToUpperInvariant(), Tier = MapOpGgTier(tierNum), Rank = rankNum > 0 ? rankNum : 999,
      WinRate = Math.Round(Dbl(st, "win_rate") * 100.0, 2),
      PickRate = Math.Round(Dbl(st, "pick_rate") * 100.0, 2),
      BanRate = Math.Round(Dbl(st, "ban_rate") * 100.0, 2),
      Kda = Math.Round(Dbl(st, "kda"), 2), Games = Json.I(st, "play"), Counters = counterIcons
     });
    }
    if (roleList.Count > 0) cachedSrRoles[id] = roleList;
   }
   return result.OrderBy(x => TierOrder(x.Tier)).ThenBy(x => x.Rank).ThenByDescending(x => x.WinRate).ToList();
  }

  static int TierOrder(string t) {
   if (t == "S+") return 0; if (t == "S") return 1; if (t == "A") return 2; if (t == "B") return 3; if (t == "C") return 4; return 5;
  }

  public static ModeBuildData ParseMayhem(string html) {
   var data = new ModeBuildData { ModeKey = "MAYHEM", SourceLabel = "MayhemMeta · ARAM Mayhem" };
   data.Items = Parser.Section(html, "Most Common Items");
   data.Augments = Parser.AllAugments(html);
   if (data.Augments.Count == 0) data.Augments = Parser.Section(html, "All Augments");
   data.Augments = data.Augments.OrderByDescending(x => x.Games >= 100).ThenByDescending(x => x.Rate).ToList();
   data.BestItems = Parser.Section(html, "Best Items");
   foreach (var b in data.BestItems) b.CoreSlot = 4;
   data.Boots = Parser.Section(html, "Boots");
   data.Spells = Parser.Section(html, "Summoner Spells");
   data.Combos = Parser.Section(html, "Best Combos");
   data.Teammates = Parser.Section(html, "Best Teammates");
   data.Matchups = Parser.Section(html, "Worst Matchups");
   return data;
  }

  public static SkillOrder ParseOpGgJsonSkills(Dictionary<string, object> rootData, string sourceLabel) {
   if (rootData == null) return null;
   var priority = new List<string>();
   foreach (var mo in Json.A(rootData, "skill_masteries").Take(1)) {
    var md = Json.D(mo);
    foreach (var sk in Json.A(md, "ids")) {
     string s = Convert.ToString(sk).ToUpperInvariant();
     if ((s == "Q" || s == "W" || s == "E") && !priority.Contains(s)) priority.Add(s);
    }
   }
   var levels = new List<string>();
   foreach (var so in Json.A(rootData, "skills").Take(1)) {
    var sd = Json.D(so);
    foreach (var lv in Json.A(sd, "order")) {
     string s = Convert.ToString(lv).ToUpperInvariant();
     if (s == "Q" || s == "W" || s == "E" || s == "R") levels.Add(s);
    }
   }
   if (levels.Count < 10) return null;
   if (priority.Count < 3) {
    // Infer priority from first maxed non-R skills
    var orderSeen = new[] { "Q", "W", "E" }.OrderBy(k => {
     int cnt = 0; for (int i = 0; i < levels.Count; i++) { if (levels[i] == k && ++cnt == 5) return i; } return 99;
    }).ToList();
    priority = orderSeen;
   }
   while (levels.Count < 18) {
    int lvl = levels.Count + 1;
    if (lvl == 16 && levels.Count(x => x == "R") < 3) levels.Add("R");
    else {
     string pick = priority.FirstOrDefault(k => levels.Count(x => x == k) < 5) ?? priority[2];
     levels.Add(pick);
    }
   }
   return new SkillOrder { Priority = priority, Levels = levels.Take(18).ToList(), Source = sourceLabel };
  }

  public static ModeBuildData ParseOpGgJson(string jsonText, string modeKey, string role, string elo, string champName, Dictionary<int, Dictionary<string, object>> lcuItems = null) {
   var root = Json.D(Json.Serializer().DeserializeObject(jsonText));
   var d = root != null && root.ContainsKey("data") ? Json.D(root["data"]) : null;
   if (d == null) return null;

   string roleLabel = modeKey == "SR" ? (" · " + RoleDisplay(role) + " · " + EloDisplay(elo)) : "";
   var data = new ModeBuildData {
    ModeKey = modeKey,
    ActiveRole = modeKey == "SR" ? NormalizeRole(role) : "",
    ActiveElo = elo,
    SourceLabel = (modeKey == "ARAM" ? "OP.GG · ARAM" : "OP.GG · Ranked SR") + roleLabel
   };

   // Available roles from summary.positions
   var summary = d.ContainsKey("summary") ? Json.D(d["summary"]) : null;
   if (summary != null) {
    foreach (var po in Json.A(summary, "positions")) {
     string r = NormalizeRole(Json.S(Json.D(po), "name"));
     if (r != "" && !data.AvailableRoles.Contains(r)) data.AvailableRoles.Add(r);
    }
   }
   if (modeKey == "SR" && data.ActiveRole != "" && !data.AvailableRoles.Contains(data.ActiveRole)) data.AvailableRoles.Insert(0, data.ActiveRole);

   // Core 3-item sequences (exact slot weights -> 1st, 2nd, 3rd item rush!)
   var itemScores = new Dictionary<int, Stat>();
   var itemSlotWeights = new Dictionary<int, double[]>();
   foreach (var co in Json.A(d, "core_items")) {
    var cd = Json.D(co);
    int play = Json.I(cd, "play"), win = Json.I(cd, "win");
    double pr = Dbl(cd, "pick_rate") * 100.0;
    double wr = play > 0 ? Math.Round(win * 100.0 / play, 2) : 50.0;
    var ids = Json.A(cd, "ids").Select(Json.ToInt).Where(x => x > 0).ToList();
    if (ids.Count >= 2) {
     data.CorePaths.Add(new CoreBuildPath { Ids = ids.Take(3).ToList(), WinRate = wr, PickRate = pr, Games = play });
    }
    for (int i = 0; i < Math.Min(3, ids.Count); i++) {
     int id = ids[i];
     if (id <= 0 || BootIds.Contains(id) || StarterIds.Contains(id)) continue;
     double w = Math.Max(0.5, pr);
     if (!itemSlotWeights.ContainsKey(id)) itemSlotWeights[id] = new double[3];
     itemSlotWeights[id][i] += w;
     Stat existing;
     if (!itemScores.TryGetValue(id, out existing)) {
      itemScores[id] = new Stat {
       Id = id, Name = ItemNameOf(id, lcuItems), Rate = wr,
       Pick = pr.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%",
       Win = wr.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%",
       Icon = ItemIconOf(id), Games = Math.Max(play, 100), CoreSlot = i + 1
      };
     } else {
      double prevPick = Parser.Number(existing.Pick);
      double nextPick = Math.Min(95, prevPick + pr);
      existing.Pick = nextPick.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";
      existing.Rate = Math.Round((existing.Rate * Math.Max(1, prevPick) + wr * w) / Math.Max(1, prevPick + w), 2);
      existing.Games += play;
     }
    }
   }
   foreach (var kv in itemScores) {
    var sw = itemSlotWeights[kv.Key];
    kv.Value.CoreSlot = sw[0] >= sw[1] && sw[0] >= sw[2] ? 1 : (sw[1] >= sw[2] ? 2 : 3);
   }
   data.Items = itemScores.Values.OrderBy(x => x.CoreSlot).ThenByDescending(x => Parser.Number(x.Pick)).ThenByDescending(x => x.Rate).Take(8).ToList();

   // Starters
   foreach (var so in Json.A(d, "starter_items").Take(4)) {
    var sd = Json.D(so);
    var ids = Json.A(sd, "ids").Select(Json.ToInt).Where(x => x > 0).ToList();
    if (ids.Count == 0) continue;
    int play = Json.I(sd, "play"), win = Json.I(sd, "win");
    double pr = Dbl(sd, "pick_rate") * 100.0;
    double wr = play > 0 ? Math.Round(win * 100.0 / play, 2) : 50.0;
    data.Starters.Add(new Stat {
     Id = ids[0], Name = String.Join(" + ", ids.Distinct().Select(id => ItemNameOf(id, lcuItems))),
     Rate = wr, Pick = pr.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%",
     Win = wr.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%",
     Icon = ItemIconOf(ids[0]), Games = play
    });
   }

   // Boots
   foreach (var bo in Json.A(d, "boots").Take(3)) {
    var bd = Json.D(bo);
    var ids = Json.A(bd, "ids").Select(Json.ToInt).Where(x => x > 0).ToList();
    if (ids.Count == 0) continue;
    int id = ids[0], play = Json.I(bd, "play"), win = Json.I(bd, "win");
    double pr = Dbl(bd, "pick_rate") * 100.0;
    double wr = play > 0 ? Math.Round(win * 100.0 / play, 2) : 50.0;
    data.Boots.Add(new Stat {
     Id = id, Name = ItemNameOf(id, lcuItems), Rate = wr,
     Pick = pr.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%",
     Win = wr.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%",
     Icon = ItemIconOf(id), Games = play
    });
   }

   // Late / 4th-5th situational items (strictly CoreSlot = 4 so Guardian Angel NEVER jumps to 1st!)
   foreach (var lo in Json.A(d, "last_items")) {
    var ld = Json.D(lo);
    var ids = Json.A(ld, "ids").Select(Json.ToInt).Where(x => x > 0).ToList();
    if (ids.Count == 0) continue;
    int id = ids[0];
    if (BootIds.Contains(id) || StarterIds.Contains(id) || itemScores.ContainsKey(id)) continue;
    int play = Json.I(ld, "play"), win = Json.I(ld, "win");
    double pr = Dbl(ld, "pick_rate") * 100.0;
    double wr = play > 0 ? Math.Round(win * 100.0 / play, 2) : 50.0;
    data.BestItems.Add(new Stat {
     Id = id, Name = ItemNameOf(id, lcuItems), Rate = wr,
     Pick = pr.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%",
     Win = wr.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%",
     Icon = ItemIconOf(id), Games = play, CoreSlot = 4
    });
    if (data.BestItems.Count >= 6) break;
   }

   // Summoner Spells (exact Riot Spell IDs!)
   foreach (var spo in Json.A(d, "summoner_spells").Take(3)) {
    var spd = Json.D(spo);
    var ids = Json.A(spd, "ids").Select(Json.ToInt).Where(x => x > 0).ToList();
    if (ids.Count < 2) continue;
    Tuple<string, string> s1, s2;
    if (!SpellById.TryGetValue(ids[0], out s1) || !SpellById.TryGetValue(ids[1], out s2)) continue;
    int play = Json.I(spd, "play"), win = Json.I(spd, "win");
    double pr = Dbl(spd, "pick_rate") * 100.0;
    double wr = play > 0 ? Math.Round(win * 100.0 / play, 2) : 50.0;
    string icon = !String.IsNullOrEmpty(DDragonVersion)
     ? ("https://ddragon.leagueoflegends.com/cdn/" + DDragonVersion + "/img/spell/" + s1.Item2 + ".png")
     : ("/icons/spells/" + s1.Item2 + ".png");
    data.Spells.Add(new Stat {
     Name = s1.Item1 + " + " + s2.Item1, Rate = wr,
     Pick = pr.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%",
     Win = wr.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%",
     Icon = icon, Games = play
    });
   }

   // Exact 9-Perk Runes (4 primary + 2 secondary + 3 stat shards!)
   foreach (var ro in Json.A(d, "runes").Take(1)) {
    var rd = Json.D(ro);
    int pri = Json.I(rd, "primary_page_id"), sec = Json.I(rd, "secondary_page_id");
    var p1 = Json.A(rd, "primary_rune_ids").Select(Json.ToInt).Where(x => x > 0).ToList();
    var p2 = Json.A(rd, "secondary_rune_ids").Select(Json.ToInt).Where(x => x > 0).ToList();
    var sh = Json.A(rd, "stat_mod_ids").Select(Json.ToInt).Where(x => x > 0).ToList();
    if (pri > 0 && sec > 0 && p1.Count == 4 && p2.Count == 2) {
     var page = new RunePage {
      PrimaryStyle = pri, SecondaryStyle = sec,
      Name = "MAYLO · " + champName + (modeKey == "SR" ? " " + RoleDisplay(role) : " ARAM"),
      Source = data.SourceLabel
     };
     page.Perks.AddRange(p1);
     page.Perks.AddRange(p2);
     page.Perks.AddRange(sh.Count == 3 ? sh : new List<int> { 5008, 5008, 5001 });
     data.Runes = page;
    }
   }

   // Exact 1-18 Skill Order + Priority
   data.Skills = ParseOpGgJsonSkills(d, data.SourceLabel);

   // Exact Counters / Matchups: split into Strong Against (Teammates slot in SR/ARAM) & Tough Matchups!
   var allCounters = new List<Stat>();
   foreach (var co in Json.A(d, "counters")) {
    var cd = Json.D(co);
    int cid = Json.I(cd, "champion_id"), play = Json.I(cd, "play"), win = Json.I(cd, "win");
    Champion cch; if (!DDragonChamps.TryGetValue(cid, out cch) || play <= 0) continue;
    double wr = Math.Round(win * 100.0 / play, 1);
    allCounters.Add(new Stat { Id = cid, Name = cch.Name, Icon = ChampIconOf(cch), Rate = wr, Pick = play + " games", Games = play });
   }
   data.Matchups = allCounters.OrderBy(x => x.Rate).Take(10).ToList();
   data.Teammates = allCounters.Where(x => x.Rate >= 50.0).OrderByDescending(x => x.Rate).Take(10).ToList();
   if (data.Teammates.Count == 0 && allCounters.Count > 0) {
    data.Teammates = allCounters.OrderByDescending(x => x.Rate).Take(10).ToList();
   }

   return data;
  }

  public static async Task<ModeBuildData> LoadChampionMode(Champion champ, string modeKey, string requestedRole = "", string elo = "emerald_plus", bool force = false, Dictionary<int, Dictionary<string, object>> lcuItems = null, List<Champion> mayhemChamps = null) {
   await EnsureDDragon(mayhemChamps);
   string opSlug = champ.Slug.Replace("-", "");
   string safeElo = String.IsNullOrEmpty(elo) ? "emerald_plus" : elo.ToLowerInvariant();

   if (modeKey == "ARAM") {
    try {
     string url = "https://lol-api-champion.op.gg/api/global/champions/aram/" + opSlug + "/none";
     string json = await WebCache.Page("opgg-aram-" + champ.Slug, url, force, s => s.Contains("\"core_items\""), ".json", 60);
     var parsed = ParseOpGgJson(json, "ARAM", "", safeElo, champ.Name, lcuItems);
     if (parsed != null && parsed.Items.Count > 0) { parsed.ChampionSlug = champ.Slug; return parsed; }
    } catch { }
   }

   if (modeKey == "SR") {
    // Determine role order: requestedRole first, then cached SR roles for this champ, then common fallback
    int cid = 0; ChampIdBySlug.TryGetValue(champ.Slug, out cid);
    if (cid == 0) ChampIdBySlug.TryGetValue(GameBridge.Norm(champ.Name), out cid);
    var rolesToTry = new List<string>();
    string normReq = NormalizeRole(requestedRole);
    if (normReq != "") rolesToTry.Add(normReq);
    List<string> known;
    if (cid > 0 && cachedSrRoles.TryGetValue(cid, out known)) {
     foreach (var r in known) if (!rolesToTry.Contains(r)) rolesToTry.Add(r);
    }
    foreach (var r in new[] { "top", "jungle", "mid", "adc", "support" }) if (!rolesToTry.Contains(r)) rolesToTry.Add(r);

    foreach (string r in rolesToTry) {
     try {
      string url = "https://lol-api-champion.op.gg/api/global/champions/ranked/" + opSlug + "/" + r + "?tier=" + safeElo;
      string json = await WebCache.Page("opgg-sr-" + champ.Slug + "-" + r + "-" + safeElo, url, force, s => s.Contains("\"core_items\""), ".json", 60);
      var parsed = ParseOpGgJson(json, "SR", r, safeElo, champ.Name, lcuItems);
      if (parsed != null && parsed.Items.Count > 0) {
       parsed.ChampionSlug = champ.Slug;
       // Merge all known viable roles for this champion so the role switcher shows every real role
       if (cid > 0 && cachedSrRoles.TryGetValue(cid, out known)) {
        foreach (var kr in known) if (!parsed.AvailableRoles.Contains(kr)) parsed.AvailableRoles.Add(kr);
       }
       return parsed;
      }
     } catch (TimeoutException) { break; }
     catch (WebException wex) {
      var resp = wex.Response as HttpWebResponse;
      if (resp == null || (int)resp.StatusCode >= 500) break;
     } catch { }
    }
   }

   string mayhemHtml = await WebCache.Page(champ.Slug, "https://mayhemmeta.com/champions/" + champ.Slug, force, h => Parser.Section(h, "Most Common Items").Count > 0);
   var mayhem = ParseMayhem(mayhemHtml);
   mayhem.ChampionSlug = champ.Slug;
   // Enrich Mayhem with exact OP.GG ARAM JSON skill order!
   try {
    string aramJson = await WebCache.Page("opgg-aram-" + champ.Slug, "https://lol-api-champion.op.gg/api/global/champions/aram/" + opSlug + "/none", false, s => s.Contains("\"skills\""), ".json", 120);
    var root = Json.D(Json.Serializer().DeserializeObject(aramJson));
    var d = root != null && root.ContainsKey("data") ? Json.D(root["data"]) : null;
    mayhem.Skills = ParseOpGgJsonSkills(d, "OP.GG · ARAM");
   } catch { }
   return mayhem;
  }
 }
}
