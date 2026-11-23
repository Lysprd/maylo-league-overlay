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
  public List<Stat> PrismaticItems = new List<Stat>();
  public List<Stat> Boots = new List<Stat>();
  public List<Stat> BestItems = new List<Stat>();
  public List<Stat> Spells = new List<Stat>();
  public List<int> SummonerSpellIds = new List<int>();
  public List<Stat> Augments = new List<Stat>();
  public List<Stat> Combos = new List<Stat>();
  public List<Stat> Teammates = new List<Stat>();
  public List<Stat> Matchups = new List<Stat>();
  public List<CoreBuildPath> CorePaths = new List<CoreBuildPath>();
  public List<RankedBuildVariant> RankedVariants = new List<RankedBuildVariant>();
  public RankedBuildVariant RankedVariant;
  public List<GameLengthPoint> GameLengths = new List<GameLengthPoint>();
  public RunePage Runes;
  public SkillOrder Skills;
 }

 public class RankedBuildVariant {
  public string Key;
  public int Games, RoleGames, CoreCount;
  public List<Stat> Starters = new List<Stat>();
  public List<Stat> Path = new List<Stat>();
  public List<List<Stat>> Options = new List<List<Stat>>();
  public List<Stat> Spells = new List<Stat>();
  public List<int> SummonerSpellIds = new List<int>();
  public Stat ObservedBoot;
  public bool BootFromMatchingPath;
  public RunePage Runes;
  public SkillOrder Skills;

  public void Apply(ModeBuildData data) {
   data.RankedVariant = this;
   data.Starters = Starters;
   data.Boots = Path.Where(item => MultiModeParser.IsRankedBoot(item.Id) && item.Id != 1001).ToList();
   if (data.Boots.Count == 0 && ObservedBoot != null) data.Boots.Add(ObservedBoot);
   data.BestItems = Options.SelectMany(items => items).Where(item => !Path.Any(step => step.Id == item.Id))
    .GroupBy(item => item.Id).Select(group => group.First()).ToList();
   data.Spells = Spells;
   data.SummonerSpellIds = SummonerSpellIds;
   data.Runes = Runes;
   data.Skills = Skills;
  }
 }

 public class DraftCounter {
  public Champion Champion;
  public int Id, Games;
  public double WinRate, GoldAt15, LowerBound;
 }

 public static class MultiModeParser {
  public static string DDragonVersion = "";
  public static PatchNotesBundle PatchNotes = new PatchNotesBundle();
  static Task<PatchNotesBundle> patchTask = null;
  public static Dictionary<int, Champion> DDragonChamps = new Dictionary<int, Champion>();
  public static Dictionary<string, int> ChampIdBySlug = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
  public static Dictionary<int, string> DDragonItemNames = new Dictionary<int, string>();
  public static Dictionary<int, int> DDragonItemPrices = new Dictionary<int, int>();
  public static Dictionary<int, Dictionary<string, object>> DDragonItems = new Dictionary<int, Dictionary<string, object>>();
  public static Dictionary<int, RuneMeta> DDragonRunes = new Dictionary<int, RuneMeta>();
  public static Dictionary<int, string> AugmentDescById = new Dictionary<int, string>();
  public static Dictionary<string, string> AugmentDescByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
  public static HashSet<int> ArenaAugmentIds = new HashSet<int>();
  public static HashSet<int> MayhemAugmentIds = new HashSet<int>();
  public static Dictionary<int, List<string>> AugmentAliasesById = new Dictionary<int, List<string>>();
  public static Dictionary<int, string> AugmentRarityById = new Dictionary<int, string>();
  public static Dictionary<int, string> AugmentNameById = new Dictionary<int, string>();
  public static Dictionary<int, string> AugmentIconById = new Dictionary<int, string>();
  static bool ddLoaded = false, augMetaLoaded = false;
  static Task ddTask = null, augMetaTask = null;

  static readonly HashSet<int> BootIds = new HashSet<int> {
   3006, 3009, 3020, 3047, 3111, 3117, 3158, 3010, 3013,
   223006, 223008, 223009, 223020, 223047, 223111, 223117, 223158
  };
  static readonly HashSet<int> GenericArenaIds = new HashSet<int> { 220007, 220000, 220001, 220002 };
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
  static readonly string[] RankedFamilies = { "recommended", "ap", "ad", "tank", "crit", "lethality", "onhit" };
  // U.GG overview axes: world region 12, then rank, then role.
  static readonly Dictionary<string, string> RankedRanks = new Dictionary<string, string> {
   { "all", "8" }, { "gold_plus", "5" }, { "platinum_plus", "10" },
   { "diamond_plus", "11" }, { "master_plus", "14" }, { "emerald_plus", "17" }
  };
  static readonly string[] RankedRoles = { "jungle", "support", "adc", "top", "mid" };

  static object UgAt(object[] row, int index) { return index < row.Length ? row[index] : null; }
  static Dictionary<string, object> UgObject(Dictionary<string, object> root, string key) {
   object value;
   return root != null && root.TryGetValue(key, out value) ? Json.D(value) : null;
  }
  static int UgInt(object[] row, int index) { return Json.ToInt(UgAt(row, index)); }
  static List<int> UgIds(object[] row, int index) {
   return Json.Array(UgAt(row, index)).Select(Json.ToInt).Where(id => id > 0).ToList();
  }
  public static bool IsRankedBoot(int id) {
   return id == 1001 || BootIds.Contains(id) || new[] { 3005, 3008, 3168, 3170, 3171, 3173, 3174, 3175, 3176 }.Contains(id);
  }
  public static bool IsJungleStarter(int id) { return id == 1101 || id == 1102 || id == 1103; }
  static bool NeedsSupplementalBuild(RankedBuildVariant variant) {
   return !variant.Path.Any(item => item.Id != 1001 && IsRankedBoot(item.Id)) ||
    (variant.SummonerSpellIds.Contains(11) && !variant.Starters.Any(item => IsJungleStarter(item.Id)));
  }
  static Stat UgItem(int id, int games, int wins, int slot, Dictionary<int, Dictionary<string, object>> lcuItems) {
   double win = games > 0 ? wins * 100.0 / games : 0;
   return new Stat { Id = id, Name = ItemNameOf(id, lcuItems), Icon = ItemIconOf(id), Games = games,
    Rate = win, Win = games > 0 ? win.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%" : "", CoreSlot = slot };
  }

  static RankedBuildVariant ParseRankedVariant(string key, string champion, string role, object[] row,
    Dictionary<int, Dictionary<string, object>> lcuItems) {
   var core = Json.Array(UgAt(row, 3));
   var ids = UgIds(core, 2).Distinct().ToList();
   if (ids.Count == 0 || UgInt(core, 0) <= 0) return null;
   var variant = new RankedBuildVariant { Key = key, Games = UgInt(core, 0), CoreCount = ids.Count, RoleGames = UgInt(Json.Array(UgAt(row, 6)), 1) };
   var starters = Json.Array(UgAt(row, 2));
   variant.Starters = UgIds(starters, 2).Select(id => UgItem(id, UgInt(starters, 0), UgInt(starters, 1), 0, lcuItems)).ToList();
   variant.Path = ids.Select((id, slot) => UgItem(id, variant.Games, UgInt(core, 1), slot + 1, lcuItems)).ToList();
   var options = Json.Array(UgAt(row, 5));
   for (int index = 0; index < Math.Min(3, options.Length) && ids.Count + index < 6; index++) {
    int slot = ids.Count + index + 1;
    var choices = Json.Array(options[index]).Select(Json.Array)
     .Where(item => UgInt(item, 0) > 0 && UgInt(item, 2) > 0 && !ids.Contains(UgInt(item, 0)))
     .Select(item => UgItem(UgInt(item, 0), UgInt(item, 2), UgInt(item, 1), slot, lcuItems)).ToList();
    variant.Options.Add(choices);
    if (variant.Path.Count != slot - 1) continue;
    var next = choices.OrderByDescending(item => item.Games).FirstOrDefault(item => !variant.Path.Any(step => step.Id == item.Id));
    if (next != null) variant.Path.Add(next);
   }
   var rune = Json.Array(UgAt(row, 0));
   int primary = UgInt(rune, 2), secondary = UgInt(rune, 3);
   var perks = UgIds(rune, 4);
   var shards = UgIds(Json.Array(UgAt(row, 8)), 2);
   if (primary > 0 && secondary > 0 && perks.Count == 6 && shards.Count == 3) {
    // The source can return the six perks interleaved between the two trees.
    var main = perks.Where(id => DDragonRunes.ContainsKey(id) && DDragonRunes[id].Style == primary).OrderBy(id => DDragonRunes[id].Slot).ToList();
    var sub = perks.Where(id => DDragonRunes.ContainsKey(id) && DDragonRunes[id].Style == secondary).OrderBy(id => DDragonRunes[id].Slot).ToList();
    if (main.Count == 4 && sub.Count == 2) variant.Runes = new RunePage {
     PrimaryStyle = primary, SecondaryStyle = secondary, Perks = main.Concat(sub).Concat(shards).ToList(),
     Name = "maylo · " + champion + " " + RoleDisplay(role), Source = "U.GG"
    };
   }
   var spells = Json.Array(UgAt(row, 1));
   var spellIds = UgIds(spells, 2);
   Tuple<string, string> first, second;
   if (spellIds.Count == 2 && SpellById.TryGetValue(spellIds[0], out first) && SpellById.TryGetValue(spellIds[1], out second)) {
    variant.SummonerSpellIds = spellIds;
    variant.Spells.Add(new Stat { Name = first.Item1 + " + " + second.Item1, Games = UgInt(spells, 0),
     Icon = "https://ddragon.leagueoflegends.com/cdn/" + DDragonVersion + "/img/spell/" + first.Item2 + ".png" });
   }
   var skill = Json.Array(UgAt(row, 4));
   var levels = Json.Array(UgAt(skill, 2)).Select(Convert.ToString).ToList();
   var priority = Convert.ToString(UgAt(skill, 3)).Select(c => c.ToString()).ToList();
   if (priority.Count == 3 && priority.Distinct().Count() == 3 && priority.All(s => s == "Q" || s == "W" || s == "E"))
    variant.Skills = new SkillOrder { Priority = priority, Levels = levels, Source = "U.GG" };
   return variant;
  }

  public static ModeBuildData ParseUggRanked(Dictionary<string, string> documents, Champion champion, string requestedRole,
    string elo, string patch, Dictionary<int, Dictionary<string, object>> lcuItems = null) {
   string rank;
   if (!RankedRanks.TryGetValue(elo, out rank)) return null;
   var byRole = RankedRoles.ToDictionary(role => role, role => new List<RankedBuildVariant>());
   foreach (string family in RankedFamilies) {
    string document;
    if (!documents.TryGetValue(family, out document)) continue;
    Dictionary<string, object> roles;
    try {
     var root = Json.D(Json.Serializer().DeserializeObject(document));
     roles = UgObject(UgObject(root, "12"), rank);
    } catch (ArgumentException) { continue; }
    catch (InvalidOperationException) { continue; }
    if (roles == null) continue;
    for (int i = 0; i < RankedRoles.Length; i++) {
     object value;
     if (!roles.TryGetValue((i + 1).ToString(), out value)) continue;
     string role = RankedRoles[i];
     // Each role has a payload and an update timestamp.
     var row = Json.Array(UgAt(Json.Array(value), 0));
     var variant = ParseRankedVariant(family, champion.Name, role, row, lcuItems);
     if (variant != null) byRole[role].Add(variant);
    }
   }
   var available = byRole.Where(entry => entry.Value.Count > 0)
    .OrderByDescending(entry => entry.Value[0].RoleGames).Select(entry => entry.Key).ToList();
   string active = NormalizeRole(requestedRole);
   if (active == "") active = available.FirstOrDefault();
   if (active == null) return null;
   var variants = byRole[active];
   string builds;
   if (documents.TryGetValue("builds", out builds) && variants.Any(NeedsSupplementalBuild))
    AddUggPurchases(variants, builds, rank, active, lcuItems);
   var data = new ModeBuildData {
    ModeKey = "SR", ChampionSlug = champion.Slug, ActiveRole = active, ActiveElo = elo, AvailableRoles = available,
    SourceLabel = "U.GG · " + RoleDisplay(active) + " · " + (elo == "gold_plus" ? "Gold" : EloDisplay(elo)) + " · " + patch,
    RankedVariants = variants,
    Items = variants.SelectMany(v => v.Path.Concat(v.Starters).Concat(v.Options.SelectMany(o => o)))
     .GroupBy(item => item.Id).Select(group => group.First()).ToList()
   };
   if (variants.Count > 0) variants[0].Apply(data);
   return data;
  }

  static void AddUggPurchases(List<RankedBuildVariant> variants, string document, string rank, string role,
    Dictionary<int, Dictionary<string, object>> lcuItems) {
   var root = Json.D(Json.Serializer().DeserializeObject(document));
   var row = Json.A(UgObject(UgObject(root, "12"), rank), (Array.IndexOf(RankedRoles, role) + 1).ToString());
   var paths = Json.Array(UgAt(row, 3)).Select(Json.Array)
    .Select(entry => new { Ids = UgIds(entry, 0), Wins = UgInt(entry, 1), Games = UgInt(entry, 2) }).ToList();
   var pet = Json.Array(UgAt(row, 2)).Select(Json.Array)
    .Select(entry => new { Id = UgIds(entry, 0).FirstOrDefault(IsJungleStarter), Wins = UgInt(entry, 1), Games = UgInt(entry, 2) })
    .Where(entry => entry.Id > 0 && entry.Games > 0).GroupBy(entry => entry.Id)
    .Select(entries => new { Id = entries.Key, Wins = entries.Sum(entry => entry.Wins), Games = entries.Sum(entry => entry.Games) })
    .OrderByDescending(entry => entry.Games).FirstOrDefault();
   var common = Json.Array(UgAt(row, 5)).Select(Json.Array)
    .Where(entry => UgInt(entry, 0) != 1001 && IsRankedBoot(UgInt(entry, 0)) && UgInt(entry, 2) > 0)
    .OrderByDescending(entry => UgInt(entry, 2)).FirstOrDefault();
   foreach (var variant in variants) {
    if (pet != null && variant.SummonerSpellIds.Contains(11) && !variant.Starters.Any(item => IsJungleStarter(item.Id)))
     variant.Starters.Insert(0, UgItem(pet.Id, pet.Games, pet.Wins, 0, lcuItems));
    if (variant.Path.Any(item => item.Id != 1001 && IsRankedBoot(item.Id))) continue;
    var core = variant.Path.Take(variant.CoreCount).Where(item => !IsRankedBoot(item.Id)).Select(item => item.Id).Take(2).ToList();
    if (core.Count == 0) continue;
    var matching = paths.Select(entry => new {
     Entry = entry, BootIndex = entry.Ids.FindIndex(id => id != 1001 && IsRankedBoot(id)),
     Core = entry.Ids.Where(id => !IsRankedBoot(id)).ToList()
    }).Where(entry => entry.BootIndex >= 0 && entry.Entry.Games >= 25 && entry.Core.Count >= core.Count &&
     entry.Core.Take(core.Count).SequenceEqual(core))
     .OrderByDescending(entry => entry.Entry.Games).FirstOrDefault();
    if (matching == null) {
     if (common != null) variant.ObservedBoot = UgItem(UgInt(common, 0), UgInt(common, 2), UgInt(common, 1), 0, lcuItems);
     continue;
    }
    int beforeBoot = matching.Entry.Ids.Take(matching.BootIndex).Count(id => !IsRankedBoot(id));
    int index = 0;
    while (index < variant.CoreCount && (beforeBoot > 0 || IsRankedBoot(variant.Path[index].Id))) {
     if (!IsRankedBoot(variant.Path[index].Id)) beforeBoot--;
     index++;
    }
    variant.ObservedBoot = UgItem(matching.Entry.Ids[matching.BootIndex], matching.Entry.Games, matching.Entry.Wins, index + 1, lcuItems);
    // Keep the published core items in place relative to one another.
    variant.Path.Insert(index, variant.ObservedBoot);
    variant.CoreCount++;
    variant.BootFromMatchingPath = true;
    if (variant.Path.Count > 6) variant.Path.RemoveAt(6);
    if (variant.Options.Count > 6 - variant.CoreCount) variant.Options.RemoveRange(6 - variant.CoreCount, variant.Options.Count - (6 - variant.CoreCount));
   }
  }

  public static string SummonerName(int id) { return SpellById.ContainsKey(id) ? SpellById[id].Item1 : ""; }
  public static string SummonerIconKey(int id) { return SpellById.ContainsKey(id) ? SpellById[id].Item2 : ""; }

  static string UggPatch() {
   var version = DDragonVersion.Split('.');
   return version.Length >= 2 ? version[0] + "_" + version[1] : "";
  }

  static async Task<string> UggSchema(string patch, string dataset) {
   const string manifest = "https://static.bigbrain.gg/assets/lol/riot_patch_update/prod/ugg/ugg-api-versions.json";
   string json = await WebCache.Page("ugg-versions", manifest, false, s => s.Contains("\"overview\""), ".json", 120);
   string schema = Json.S(UgObject(Json.D(Json.Serializer().DeserializeObject(json)), patch), dataset);
   return Regex.IsMatch(schema, @"^1\.5\.\d+$") ? schema : "";
  }

  public static List<DraftCounter> ParseDraftCounters(string document, int targetId, string role, string elo) {
   string rank;
   int roleIndex = Array.IndexOf(RankedRoles, NormalizeRole(role));
   if (roleIndex < 0 || !RankedRanks.TryGetValue(elo, out rank)) return new List<DraftCounter>();
   var root = Json.D(Json.Serializer().DeserializeObject(document));
   var matches = Json.A(UgObject(UgObject(root, "12"), rank), (roleIndex + 1).ToString());
   var result = new List<DraftCounter>();
   foreach (var value in Json.Array(UgAt(matches, 0))) {
    var row = Json.Array(value);
    int id = UgInt(row, 0), games = UgInt(row, 2), wins = UgInt(row, 1);
    Champion champion;
    if (id == targetId || games < 100 || wins < 0 || wins > games || !DDragonChamps.TryGetValue(id, out champion)) continue;
    double observed = 1.0 - wins / (double)games;
    // Wilson lower bound gives fewer games less influence near the top of the list.
    const double z = 1.96;
    double z2 = z * z;
    double lower = (observed + z2 / (2 * games) -
     z * Math.Sqrt((observed * (1 - observed) + z2 / (4 * games)) / games)) / (1 + z2 / games);
    result.Add(new DraftCounter { Id = id, Champion = champion, Games = games, WinRate = observed * 100,
     GoldAt15 = -UgInt(row, 4) / (double)games, LowerBound = lower });
   }
   return result.OrderByDescending(row => row.LowerBound).ThenByDescending(row => row.Games).ToList();
  }

  public static async Task<List<DraftCounter>> LoadDraftCounters(int id, string role, string elo) {
   await EnsureDDragon();
   string patch = UggPatch();
   if (patch == "") throw new InvalidOperationException("No active patch");
   string schema = await UggSchema(patch, "matchups");
   if (schema == "") throw new InvalidOperationException("No matchup data for this patch");
   string url = "https://stats2.u.gg/lol/1.5/matchups/" + patch + "/ranked_solo_5x5/" + id + "/" + schema + ".json";
   string document = await WebCache.Page("ugg-matchups-" + patch + "-" + id, url, false,
    value => value.TrimStart().StartsWith("{", StringComparison.Ordinal), ".json", 60);
   return ParseDraftCounters(document, id, role, elo);
  }

  static async Task<ModeBuildData> LoadUggRanked(Champion champion, int id, string role, string elo,
    Dictionary<int, Dictionary<string, object>> lcuItems) {
   string patch = UggPatch();
   if (id <= 0 || patch == "" || !RankedRanks.ContainsKey(elo)) return null;
   string schema = await UggSchema(patch, "overview");
   if (schema == "") return null;
   var results = await Task.WhenAll(RankedFamilies.Select(async family => {
    string endpoint = family == "recommended" ? "overview" : family + "-overview";
    string url = "https://stats2.u.gg/lol/1.5/" + endpoint + "/" + patch + "/ranked_solo_5x5/" + id + "/" + schema + ".json";
    try {
     string document = await WebCache.Page("ugg-sr-" + patch + "-" + id + "-" + family, url, false,
      s => s.TrimStart().StartsWith("{", StringComparison.Ordinal), ".json", 60);
     return Tuple.Create(family, document);
    } catch (WebException ex) {
     if (ex.Response != null) ex.Response.Dispose();
     return null;
    } catch (Exception) { return null; }
   }));
   var documents = results.Where(result => result != null).ToDictionary(result => result.Item1, result => result.Item2);
   var parsed = ParseUggRanked(documents, champion, role, elo, patch.Replace('_', '.'), lcuItems);
   if (parsed == null || !parsed.RankedVariants.Any(NeedsSupplementalBuild)) return parsed;
   try {
    string buildSchema = await UggSchema(patch, "builds");
    if (buildSchema == "") return parsed;
    string url = "https://stats2.u.gg/lol/1.5/builds/" + patch + "/ranked_solo_5x5/" + id + "/" + buildSchema + ".json";
    documents["builds"] = await WebCache.Page("ugg-build-paths-" + patch + "-" + id, url, false,
     value => value.TrimStart().StartsWith("{", StringComparison.Ordinal), ".json", 60);
    return ParseUggRanked(documents, champion, role, elo, patch.Replace('_', '.'), lcuItems);
   } catch (WebException ex) {
    if (ex.Response != null) ex.Response.Dispose();
    return parsed;
   } catch (Exception) { return parsed; }
  }

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
       { "id", id }, { "name", name }, { "priceTotal", total }, { "from", fromIds }, { "to", Json.A(d, "into") },
       { "description", Json.S(d, "description") }, { "categories", Json.A(d, "tags") },
       { "maps", d.ContainsKey("maps") ? Json.D(d["maps"]) : null }, { "purchasable", Json.S(gold, "purchasable") },
       { "inStore", Json.S(d, "inStore") }, { "hideFromAll", Json.S(d, "hideFromAll") },
       { "requiredChampion", Json.S(d, "requiredChampion") }
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
      int slot = 0;
      foreach (var slotObj in Json.A(td, "slots")) {
       foreach (var rObj in Json.A(Json.D(slotObj), "runes")) {
        var rd = Json.D(rObj); int rid = Json.I(rd, "id");
        if (rid > 0) DDragonRunes[rid] = new RuneMeta { Id = rid, Style = tid, Slot = slot, Name = Json.S(rd, "name"), Icon = "https://ddragon.leagueoflegends.com/cdn/img/" + Json.S(rd, "icon") };
       }
       slot++;
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
       string iconPath = Json.S(d, "augmentSmallIconPath");
       if (id <= 0) continue;
       cherryIds.Add(id);
       if (Json.S(d, "augmentNameId").StartsWith("ARAM_", StringComparison.OrdinalIgnoreCase))
        MayhemAugmentIds.Add(id);
       AddAugAlias(id, name);
       if (!String.IsNullOrEmpty(name) && (lang.Item1 == "en" || !AugmentNameById.ContainsKey(id))) AugmentNameById[id] = name;
       if (!String.IsNullOrEmpty(iconPath) && !AugmentIconById.ContainsKey(id)) {
        string cleanIcon = iconPath.Replace("/lol-game-data/assets/", "").TrimStart('/').ToLowerInvariant();
        if (cleanIcon != "") AugmentIconById[id] = "https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/" + cleanIcon;
       }
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

  public static List<TierListEntry> MayhemTierList(IEnumerable<Champion> champions) {
   int rank = 1;
   var result = new List<TierListEntry>();
   foreach (var champion in (champions ?? Enumerable.Empty<Champion>()).Where(c => c.Win > 0).OrderByDescending(c => c.Win)) {
    int id;
    ChampIdBySlug.TryGetValue(champion.Slug, out id);
    result.Add(new TierListEntry {
     Id = id, Slug = champion.Slug, Name = champion.Name, Icon = ChampIconOf(champion),
     Role = "MAYHEM", Tier = champion.Tier, Rank = rank++, WinRate = champion.Win, PickRate = champion.Pick
    });
   }
   return result;
  }

  public static async Task<List<TierListEntry>> LoadTierList(string modeKey, string roleFilter, string elo, List<Champion> mayhemChamps, bool force = false) {
   await EnsureDDragon(mayhemChamps);
   var result = new List<TierListEntry>();

   if (modeKey == "MAYHEM") return MayhemTierList(mayhemChamps);

   string safeElo = String.IsNullOrEmpty(elo) ? "emerald_plus" : elo.ToLowerInvariant();
   if (modeKey == "ARENA") {
    string raw = await WebCache.Page("opgg-tier-arena", "https://lol-api-champion.op.gg/api/global/champions/arena", force, s => s.Contains("\"data\""), ".json", 45);
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
     int prevPatch = td != null ? Json.I(td, "rank_prev_patch") : 0;
     if (tierNum <= 0) tierNum = 5;
     int play = Json.I(st, "play"), win = Json.I(st, "win");
     double wr = play > 0 ? Math.Round(win * 100.0 / play, 2) : 50.0;
     double deaths = Math.Max(1.0, Dbl(st, "deaths"));
     double kda = Math.Round((Dbl(st, "kills") + Dbl(st, "assists")) / deaths, 2);
     result.Add(new TierListEntry {
      Id = id, Slug = ch.Slug, Name = ch.Name, Icon = ChampIconOf(ch),
      Role = "ARENA", Tier = MapOpGgTier(tierNum), Rank = rankNum > 0 ? rankNum : 999,
      RankDelta = (rankNum > 0 && prevPatch > 0) ? (prevPatch - rankNum) : 0,
      WinRate = wr,
      PickRate = Math.Round(Dbl(st, "pick_rate") * 100.0, 2),
      BanRate = Math.Round(Dbl(st, "ban_rate") * 100.0, 2),
      Kda = kda, Games = play
     });
    }
    return result.OrderBy(x => TierOrder(x.Tier)).ThenBy(x => x.Rank).ThenByDescending(x => x.WinRate).ToList();
   }

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
     int prevPatch = td != null ? Json.I(td, "rank_prev_patch") : 0;
     if (tierNum <= 0) tierNum = 5;
     result.Add(new TierListEntry {
      Id = id, Slug = ch.Slug, Name = ch.Name, Icon = ChampIconOf(ch),
      Role = "ARAM", Tier = MapOpGgTier(tierNum), Rank = rankNum > 0 ? rankNum : 999,
      RankDelta = (rankNum > 0 && prevPatch > 0) ? (prevPatch - rankNum) : 0,
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
    foreach (var po in posArr) {
     var pd = Json.D(po);
     string pName = NormalizeRole(Json.S(pd, "name"));
     if (pName == "") continue;
     if (normFilter != "" && pName != normFilter) continue;
     var st = pd.ContainsKey("stats") ? Json.D(pd["stats"]) : null;
     if (st == null) continue;
     var td = st.ContainsKey("tier_data") ? Json.D(st["tier_data"]) : null;
     int tierNum = td != null ? Json.I(td, "tier") : 5;
     int rankNum = td != null ? Json.I(td, "rank") : 999;
     int prevPatch = td != null ? Json.I(td, "rank_prev_patch") : 0;
     if (tierNum <= 0) tierNum = 5;
     var counterIcons = new List<string>();
     foreach (var co in Json.A(pd, "counters").Take(3)) {
      var cd = Json.D(co);
      Champion cch; if (DDragonChamps.TryGetValue(Json.I(cd, "champion_id"), out cch)) counterIcons.Add(ChampIconOf(cch));
     }
     result.Add(new TierListEntry {
      Id = id, Slug = ch.Slug, Name = ch.Name, Icon = ChampIconOf(ch),
      Role = pName.ToUpperInvariant(), Tier = MapOpGgTier(tierNum), Rank = rankNum > 0 ? rankNum : 999,
      RankDelta = (rankNum > 0 && prevPatch > 0) ? (prevPatch - rankNum) : 0,
      WinRate = Math.Round(Dbl(st, "win_rate") * 100.0, 2),
      PickRate = Math.Round(Dbl(st, "pick_rate") * 100.0, 2),
      BanRate = Math.Round(Dbl(st, "ban_rate") * 100.0, 2),
      Kda = Math.Round(Dbl(st, "kda"), 2), Games = Json.I(st, "play"), Counters = counterIcons
     });
    }
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
      Name = "maylo · " + champName + (modeKey == "SR" ? " " + RoleDisplay(role) : " ARAM"),
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

   // Game-Length Scaling Curve (Early / Mid / Late win rate %!)
   foreach (var gl in Json.A(d, "game_lengths")) {
    var gd = Json.D(gl); if (gd == null) continue;
    int mins = Json.I(gd, "game_length");
    double rate = Math.Round(Dbl(gd, "rate") * 100.0, 1);
    if (rate > 1 && rate < 99) data.GameLengths.Add(new GameLengthPoint { Minutes = mins, WinRate = rate });
   }
   data.GameLengths = data.GameLengths.OrderBy(x => x.Minutes).ToList();

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

  public static ModeBuildData ParseOpGgArenaJson(string jsonText, string champName, Dictionary<int, Dictionary<string, object>> lcuItems = null) {
   var root = Json.D(Json.Serializer().DeserializeObject(jsonText));
   var d = root != null && root.ContainsKey("data") ? Json.D(root["data"]) : null;
   if (d == null) return null;

   var data = new ModeBuildData {
    ModeKey = "ARENA",
    ActiveRole = "",
    ActiveElo = "",
    SourceLabel = "OP.GG · Arena 2v2"
   };

   // Prismatic Items (Arena signature items!)
   foreach (var po in Json.A(d, "prism_items").Take(6)) {
    var pd = Json.D(po);
    var ids = Json.A(pd, "ids").Select(Json.ToInt).Where(x => x > 0).ToList();
    if (ids.Count == 0) continue;
    int id = ids[0], play = Json.I(pd, "play"), win = Json.I(pd, "win");
    double pr = Dbl(pd, "pick_rate") * 100.0;
    double wr = play > 0 ? Math.Round(win * 100.0 / play, 2) : 50.0;
    data.PrismaticItems.Add(new Stat {
     Id = id, Name = ItemNameOf(id, lcuItems), Rate = wr,
     Pick = pr.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%",
     Win = wr.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%",
     Icon = ItemIconOf(id), Games = play, Rarity = "Prismatic"
    });
   }

   // Core Arena item sequences (filter out Boots & generic Prismatic placeholder 220007)
   var itemScores = new Dictionary<int, Stat>();
   var itemSlotWeights = new Dictionary<int, double[]>();
   foreach (var co in Json.A(d, "core_items")) {
    var cd = Json.D(co);
    int play = Json.I(cd, "play"), win = Json.I(cd, "win");
    double pr = Dbl(cd, "pick_rate") * 100.0;
    double wr = play > 0 ? Math.Round(win * 100.0 / play, 2) : 50.0;
    var ids = Json.A(cd, "ids").Select(Json.ToInt).Where(x => x > 0 && !BootIds.Contains(x) && !GenericArenaIds.Contains(x)).ToList();
    if (ids.Count >= 2) {
     data.CorePaths.Add(new CoreBuildPath { Ids = ids.Take(3).ToList(), WinRate = wr, PickRate = pr, Games = play });
    }
    for (int i = 0; i < Math.Min(3, ids.Count); i++) {
     int id = ids[i];
     double w = Math.Max(0.5, pr);
     if (!itemSlotWeights.ContainsKey(id)) itemSlotWeights[id] = new double[3];
     itemSlotWeights[id][i] += w;
     Stat existing;
     if (!itemScores.TryGetValue(id, out existing)) {
      itemScores[id] = new Stat {
       Id = id, Name = ItemNameOf(id, lcuItems), Rate = wr,
       Pick = pr.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%",
       Win = wr.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%",
       Icon = ItemIconOf(id), Games = Math.Max(play, 50), CoreSlot = i + 1
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

   // Arena Boots
   foreach (var bo in Json.A(d, "boots").Take(4)) {
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

   // Situational / Late Arena items
   foreach (var lo in Json.A(d, "last_items")) {
    var ld = Json.D(lo);
    var ids = Json.A(ld, "ids").Select(Json.ToInt).Where(x => x > 0).ToList();
    if (ids.Count == 0) continue;
    int id = ids[0];
    if (BootIds.Contains(id) || StarterIds.Contains(id) || GenericArenaIds.Contains(id) || itemScores.ContainsKey(id)) continue;
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

   // Arena Augments (Silver=1, Gold=4, Prismatic=8)
   var seenAug = new HashSet<int>();
   foreach (var grpObj in Json.A(d, "augment_group")) {
    var gd = Json.D(grpObj); if (gd == null) continue;
    int rarNum = Json.I(gd, "rarity");
    string rarity = rarNum == 1 ? "Silver" : rarNum == 4 ? "Gold" : rarNum == 8 ? "Prismatic" : "";
    foreach (var ao in Json.A(gd, "augments")) {
     var ad = Json.D(ao); if (ad == null) continue;
     int aid = Json.I(ad, "id");
     if (aid <= 0 || !seenAug.Add(aid)) continue;
     int play = Json.I(ad, "play"), win = Json.I(ad, "win");
     double wrVal = Dbl(ad, "win_rate") * 100.0;
     if (wrVal <= 0 && play > 0) wrVal = win * 100.0 / play;
     wrVal = Math.Round(wrVal, 2);
     double pickRate;
     bool hasPick = Double.TryParse(Json.S(ad, "pick_rate"), System.Globalization.NumberStyles.Float,
      System.Globalization.CultureInfo.InvariantCulture, out pickRate) && pickRate >= 0 && pickRate <= 1;
     string aName;
     if (!AugmentNameById.TryGetValue(aid, out aName) || String.IsNullOrEmpty(aName)) {
      List<string> als;
      aName = (AugmentAliasesById.TryGetValue(aid, out als) && als != null && als.Count > 0) ? als[als.Count - 1] : ("Augment " + aid);
     }
     string aIcon;
     if (!AugmentIconById.TryGetValue(aid, out aIcon) || String.IsNullOrEmpty(aIcon)) aIcon = "/icons/augments/" + aid + ".png";
     data.Augments.Add(new Stat {
      Id = aid, Name = aName, Icon = aIcon, Rarity = rarity,
      Rate = wrVal, Games = play,
      Pick = hasPick ? (pickRate * 100).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "%" : "",
      Win = wrVal.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%"
     });
    }
   }
   data.Augments = data.Augments.OrderByDescending(x => x.Games >= 30).ThenByDescending(x => x.Rate).ToList();

   // Arena Best Duo Synergies
   foreach (var so in Json.A(d, "synergies")) {
    var sd = Json.D(so); if (sd == null) continue;
    int cid = Json.I(sd, "champion_id"), play = Json.I(sd, "play"), win = Json.I(sd, "win");
    Champion cch; if (!DDragonChamps.TryGetValue(cid, out cch) || play <= 0) continue;
    double wr = Math.Round(win * 100.0 / play, 1);
    data.Teammates.Add(new Stat { Id = cid, Name = cch.Name, Icon = ChampIconOf(cch), Rate = wr, Pick = play + " games", Games = play });
   }
   data.Teammates = data.Teammates.OrderByDescending(x => x.Games >= 20).ThenByDescending(x => x.Rate).Take(12).ToList();

   // Skill Order
   data.Skills = ParseOpGgJsonSkills(d, data.SourceLabel);
   return data;
  }

  public static async Task<ModeBuildData> LoadChampionMode(Champion champ, string modeKey, string requestedRole = "", string elo = "emerald_plus", bool force = false, Dictionary<int, Dictionary<string, object>> lcuItems = null, List<Champion> mayhemChamps = null) {
   await EnsureDDragon(mayhemChamps);
   string opSlug = champ.Slug.Replace("-", "");
   string safeElo = String.IsNullOrEmpty(elo) ? "emerald_plus" : elo.ToLowerInvariant();

   if (modeKey == "ARENA") {
    try {
     string url = "https://lol-api-champion.op.gg/api/global/champions/arena/" + opSlug;
     string json = await WebCache.Page("opgg-arena-" + champ.Slug, url, force, s => s.Contains("\"augment_group\"") || s.Contains("\"core_items\""), ".json", 60);
     var parsed = ParseOpGgArenaJson(json, champ.Name, lcuItems);
     if (parsed != null && (parsed.Items.Count > 0 || parsed.Augments.Count > 0)) { parsed.ChampionSlug = champ.Slug; return parsed; }
    } catch { }
   }

   if (modeKey == "ARAM") {
    try {
     string url = "https://lol-api-champion.op.gg/api/global/champions/aram/" + opSlug + "/none";
     string json = await WebCache.Page("opgg-aram-" + champ.Slug, url, force, s => s.Contains("\"core_items\""), ".json", 60);
     var parsed = ParseOpGgJson(json, "ARAM", "", safeElo, champ.Name, lcuItems);
     if (parsed != null && parsed.Items.Count > 0) { parsed.ChampionSlug = champ.Slug; return parsed; }
    } catch { }
   }

   if (modeKey == "SR") {
    int cid = 0; ChampIdBySlug.TryGetValue(champ.Slug, out cid);
    if (cid == 0) ChampIdBySlug.TryGetValue(GameBridge.Norm(champ.Name), out cid);
    var ranked = await LoadUggRanked(champ, cid, requestedRole, safeElo, lcuItems);
    if (ranked != null) return ranked;
    return new ModeBuildData {
     ModeKey = "SR", ChampionSlug = champ.Slug, ActiveRole = NormalizeRole(requestedRole), ActiveElo = safeElo,
     SourceLabel = "U.GG · No build available"
    };
   }

   if (modeKey != "MAYHEM") throw new InvalidOperationException("No build data for this mode.");

   string mayhemHtml = await WebCache.Page(champ.Slug, "https://mayhemmeta.com/champions/" + champ.Slug, force, h => Parser.Section(h, "Most Common Items").Count > 0);
   var mayhem = ParseMayhem(mayhemHtml);
   mayhem.ChampionSlug = champ.Slug;
   // Enrich Mayhem with exact OP.GG ARAM JSON skill order & game-length scaling!
   try {
    string aramJson = await WebCache.Page("opgg-aram-" + champ.Slug, "https://lol-api-champion.op.gg/api/global/champions/aram/" + opSlug + "/none", false, s => s.Contains("\"skills\""), ".json", 120);
    var root = Json.D(Json.Serializer().DeserializeObject(aramJson));
    var d = root != null && root.ContainsKey("data") ? Json.D(root["data"]) : null;
    mayhem.Skills = ParseOpGgJsonSkills(d, "OP.GG · ARAM");
    foreach (var gl in Json.A(d, "game_lengths")) {
     var gd = Json.D(gl); if (gd == null) continue;
     int mins = Json.I(gd, "game_length");
     double rate = Math.Round(Dbl(gd, "rate") * 100.0, 1);
     if (rate > 1 && rate < 99) mayhem.GameLengths.Add(new GameLengthPoint { Minutes = mins, WinRate = rate });
    }
    mayhem.GameLengths = mayhem.GameLengths.OrderBy(x => x.Minutes).ToList();
   } catch { }
   return mayhem;
  }

  public static async Task<ModeBuildData> LoadRankedContext(Champion champion, string role, string elo) {
   string slug = champion.Slug.Replace("-", "");
   string url = "https://lol-api-champion.op.gg/api/global/champions/ranked/" + slug + "/" + role + "?tier=" + elo;
   try {
    string json = await WebCache.Page("opgg-sr-" + champion.Slug + "-" + role + "-" + elo, url,
     false, value => value.Contains("\"core_items\""), ".json", 60);
    return ParseOpGgJson(json, "SR", role, elo, champion.Name);
   } catch (WebException ex) {
    if (ex.Response != null) ex.Response.Dispose();
    return null;
   } catch (Exception) { return null; }
  }

  static string ModeFromPatchId(string id) {
   string s = (id ?? "").ToLowerInvariant();
   if (s.Contains("mayhem")) return "MAYHEM";
   if (s.Contains("-aram-")) return "ARAM";
   if (s.Contains("-arena-") || s.Contains("-cherry-")) return "ARENA";
   return "SR";
  }

  static string NormPatchType(string raw) {
   string t = (raw ?? "").Trim().ToLowerInvariant();
   if (t == "buff" || t == "nerf" || t == "added" || t == "removed" || t == "system") return t;
   if (t.Contains("buff")) return "buff";
   if (t.Contains("nerf")) return "nerf";
   return "adjust";
  }

  public static Task<PatchNotesBundle> LoadPatchNotes(bool force = false) {
   if (!force && PatchNotes != null && PatchNotes.Entries.Count > 0) return Task.FromResult(PatchNotes);
   if (!force && patchTask != null && !patchTask.IsCompleted) return patchTask;
   patchTask = LoadPatchNotesCore(force);
   return patchTask;
  }

  static async Task<PatchNotesBundle> LoadPatchNotesCore(bool force) {
   var bundle = new PatchNotesBundle();
   try {
    await EnsureDDragon();
    bool isFr = String.Equals(System.Globalization.CultureInfo.InstalledUICulture.TwoLetterISOLanguageName, "fr", StringComparison.OrdinalIgnoreCase);
    string hl = isFr ? "fr_FR" : "en_US";
    string raw = await WebCache.Page("opgg-patch-notes-" + hl, "https://lol-api-champion.op.gg/api/meta/patch-notes?hl=" + hl, force, s => s.Contains("\"champions\""), ".json", 180);
    var root = Json.D(Json.Serializer().DeserializeObject(raw));
    var meta = root != null && root.ContainsKey("meta") ? Json.D(root["meta"]) : null;
    var data = root != null && root.ContainsKey("data") ? Json.D(root["data"]) : null;
    if (meta != null) {
     bundle.PatchVersion = Json.S(meta, "patch_version");
     bundle.OfficialUrl = Json.S(meta, "official_url");
    }
    if (data == null) return bundle;

    Action<Dictionary<string, object>, string, int, string, string, string> addChanges = (chObj, cat, id, slug, icon, fallbackTitle) => {
     foreach (var co in Json.A(chObj, "changes")) {
      var cd = Json.D(co); if (cd == null) continue;
      string changeId = Json.S(cd, "id");
      string ctype = NormPatchType(Json.S(cd, "type"));
      string title = Json.S(cd, "title");
      if (title == "") title = fallbackTitle;
      string mode = ModeFromPatchId(changeId);
      if (mode == "ARENA") continue; // Keep Patch Notes focused on MAYLO's supported modes (SR, ARAM, ARAM Mayhem)
      var entry = new PatchEntry {
       Id = id, Category = cat, Mode = mode, Slug = slug, Title = title, Icon = icon,
       ChangeType = ctype, Summary = Json.S(cd, "summary"), PlayerNote = Json.S(cd, "player_note")
      };
      foreach (var detObj in Json.A(cd, "details").Take(6)) {
       var dd = Json.D(detObj); if (dd == null) continue;
       string txt = Json.S(dd, "text");
       string oldV = Json.S(dd, "old_value");
       string newV = Json.S(dd, "new_value");
       if (txt != "" || oldV != "" || newV != "") {
        entry.Details.Add(new PatchDetailLine { Text = txt, OldValue = oldV, NewValue = newV, Type = NormPatchType(Json.S(dd, "type")) });
       }
      }
      bundle.Entries.Add(entry);
      if (cat == "champion" && !String.IsNullOrEmpty(slug) && (ctype == "buff" || ctype == "nerf" || ctype == "adjust")) {
       if (!bundle.ChampionTagBySlug.ContainsKey(slug) || mode == "SR") bundle.ChampionTagBySlug[slug] = ctype;
       Dictionary<string, string> modeMap;
       if (!bundle.ChampionTagByMode.TryGetValue(mode, out modeMap)) {
        modeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bundle.ChampionTagByMode[mode] = modeMap;
       }
       modeMap[slug] = ctype;
      }
     }
    };

    foreach (var o in Json.A(data, "champions")) {
     var d = Json.D(o); if (d == null) continue;
     int cid = Json.I(d, "id");
     Champion ch; DDragonChamps.TryGetValue(cid, out ch);
     addChanges(d, "champion", cid, ch != null ? ch.Slug : "", ch != null ? ChampIconOf(ch) : "", ch != null ? ch.Name : ("Champion " + cid));
    }
    foreach (var o in Json.A(data, "augments")) {
     var d = Json.D(o); if (d == null) continue;
     int aid = Json.I(d, "id");
     addChanges(d, "augment", aid, "", "/icons/augments/" + aid + ".png", "Augment " + aid);
    }
    foreach (var o in Json.A(data, "items")) {
     var d = Json.D(o); if (d == null) continue;
     int iid = Json.I(d, "id");
     addChanges(d, "item", iid, "", ItemIconOf(iid), ItemNameOf(iid));
    }
    foreach (var o in Json.A(data, "systems")) {
     var d = Json.D(o); if (d == null) continue;
     var disp = d.ContainsKey("display") ? Json.D(d["display"]) : null;
     addChanges(d, "system", 0, "", "", disp != null ? Json.S(disp, "label") : "System");
    }

    if (bundle.Entries.Count > 0) PatchNotes = bundle;
   } catch { }
   return PatchNotes;
  }
 }
}
