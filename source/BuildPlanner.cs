using System;
using System.Linq;
using System.Collections.Generic;
namespace MayhemCompanion {
 public class BuildStep {
  public Stat Stat;
  public string Reason, ShortTip, Role;
  public string SynergyTag = "", SynergyTone = ""; // e.g. "CORE SYNERGY", "ANTI-HEAL", "ANTI-TANK", "VS HEAVY AP", "AUGMENT FIT"
  public double Score, PickRate;
  public bool Owned, IsBoots;
 }

 public class ComponentStatus { public int Id; public bool Owned; }

 public class EnemyThreatProfile {
  public bool HeavyAD, HeavyAP, NeedsAntiHeal, NeedsAntiTank;
  public int AdCount, ApCount, TankCount, HealerCount, ArmoredCount;
  public List<string> Tags = new List<string>();
 }

 public static class EnemyCompAnalyzer {
  static readonly HashSet<string> Healers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
   "aatrox", "soraka", "drmundo", "vladimir", "briar", "warwick", "sylas", "swain", "yuumi",
   "zac", "kayn", "fiora", "irelia", "olaf", "sona", "nami", "milio", "illaoi", "renekton",
   "volibear", "maokai", "belveth", "samira", "nilah", "senna", "seraphine", "taric"
  };
  static readonly HashSet<string> Tanks = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
   "ornn", "sion", "malphite", "chogath", "drmundo", "zac", "amumu", "sejuani", "maokai",
   "shen", "ksante", "tahmkench", "skarner", "rammus", "nautilus", "leona", "braum", "alistar",
   "poppy", "galio", "volibear", "mordekaiser", "urgot", "nasus", "rell", "thresh"
  };
  static readonly HashSet<string> ApChamps = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
   "ahri", "akali", "anivia", "annie", "aurelionsol", "aurora", "azir", "brand", "cassiopeia",
   "diana", "ekko", "elise", "evelynn", "fiddlesticks", "fizz", "gwen", "heimerdinger", "hwei",
   "karthus", "kassadin", "katarina", "kennen", "leblanc", "lillia", "lissandra", "lux",
   "malzahar", "mel", "mordekaiser", "morgana", "neeko", "nidalee", "orianna", "rumble", "ryze",
   "seraphine", "singed", "swain", "sylas", "syndra", "taliyah", "teemo", "twistedfate",
   "veigar", "velkoz", "vex", "viktor", "vladimir", "xerath", "ziggs", "zoe", "zyra"
  };
  static readonly HashSet<string> AdChamps = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
   "aatrox", "ambessa", "aphelios", "ashe", "belveth", "briar", "caitlyn", "camille", "corki",
   "darius", "draven", "ezreal", "fiora", "gangplank", "garen", "gnar", "graves", "hecarim",
   "illaoi", "irelia", "jarvaniv", "jax", "jayce", "jhin", "jinx", "kaisa", "kalista", "kayn",
   "khazix", "kindred", "kled", "leesin", "lucian", "masteryi", "missfortune", "naafiri",
   "nilah", "nocturne", "olaf", "pantheon", "pyke", "qiyana", "quinn", "reksai", "renekton",
   "rengar", "riven", "samira", "senna", "sett", "shaco", "sivir", "smolder", "talon",
   "tristana", "trundle", "tryndamere", "twitch", "urgot", "varus", "vayne", "vi", "viego",
   "monkeyking", "wukong", "xayah", "xinzhao", "yasuo", "yone", "yorick", "yunara", "zed", "zeri"
  };

  public static EnemyThreatProfile Analyze(LiveSummary live, Func<int, string[]> categories) {
   var p = new EnemyThreatProfile();
   if (live == null || live.Players == null || live.Players.Count == 0) return p;
   var me = live.Players.FirstOrDefault(x => x.Me);
   string myTeam = me != null ? me.Team : "ORDER";
   var enemies = live.Players.Where(x => !String.IsNullOrEmpty(x.Team) && x.Team != myTeam).ToList();
   if (enemies.Count == 0) return p;

   foreach (var e in enemies) {
    string norm = GameBridge.Norm(e.Champion);
    if (Healers.Contains(norm)) p.HealerCount++;
    bool isTank = Tanks.Contains(norm);
    bool isAp = ApChamps.Contains(norm);
    bool isAd = AdChamps.Contains(norm);

    int defensiveItems = 0, armorItems = 0, apItems = 0, adItems = 0;
    if (categories != null && e.Items != null) {
     foreach (int id in e.Items) {
      var cats = categories(id);
      if (cats.Contains("Armor") || cats.Contains("Health")) defensiveItems++;
      if (cats.Contains("Armor")) armorItems++;
      if (cats.Contains("SpellDamage")) apItems++;
      if (cats.Contains("Damage") || cats.Contains("CriticalStrike")) adItems++;
     }
    }
    if (isTank || defensiveItems >= 2) p.TankCount++;
    if (armorItems >= 2) p.ArmoredCount++;
    if (isAp || apItems >= 2) p.ApCount++;
    else if (isAd || adItems >= 2) p.AdCount++;
   }

   p.HeavyAD = p.AdCount >= 3 && p.AdCount > p.ApCount;
   p.HeavyAP = p.ApCount >= 3 && p.ApCount > p.AdCount;
   p.NeedsAntiHeal = p.HealerCount >= 1;
   p.NeedsAntiTank = p.TankCount >= 2;
   if (p.HeavyAD) p.Tags.Add("Heavy AD");
   if (p.HeavyAP) p.Tags.Add("Heavy AP");
   if (p.NeedsAntiHeal) p.Tags.Add("Anti-Heal");
   if (p.NeedsAntiTank) p.Tags.Add("Anti-Tank");
   return p;
  }
 }

 public static class Recommendations {
  public static double Score(Stat stat, double baseline) {
   return baseline + (stat.Rate - baseline) * stat.Games / (stat.Games + 250.0);
  }

  public static Dictionary<int, string> AugmentTiers(IEnumerable<Stat> catalog, int minGames) {
   var result = new Dictionary<int, string>();
   var groups = catalog.Where(stat => stat.Id > 0 && stat.Games >= minGames && stat.Rate > 0 && stat.Rate <= 100)
    .GroupBy(stat => TierLogic.RarityKey(stat.Rarity));
   foreach (var group in groups) {
    var cards = group.GroupBy(stat => stat.Id).Select(duplicates => duplicates.OrderByDescending(stat => stat.Games).First())
     .OrderBy(stat => stat.Rate).ToList();
    // A rarity needs enough comparable augments to establish a reference.
    if (group.Key == "" || cards.Count < 5) continue;
    int middle = cards.Count / 2;
    double median = (cards[(cards.Count - 1) / 2].Rate + cards[middle].Rate) / 2;
    foreach (var card in cards) {
     double delta = Score(card, median) - median;
     result[card.Id] = delta >= 5 ? "S+" : delta >= 3 ? "S" : delta >= 1 ? "A" :
      delta >= -1 ? "B" : delta >= -3 ? "C" : "D";
    }
   }
   return result;
  }

  public static Stat Best(IEnumerable<Stat> offered, double baseline, int minGames, out string reason) {
   var cards = offered.Where(x => x != null).ToList();
   var ranked = cards.Where(x => x.Games >= minGames && x.Rate > 0 && x.Rate <= 100)
    .OrderByDescending(x => Score(x, baseline)).ThenByDescending(x => x.Games).ToList();
   reason = "";
   if (cards.Count < 2) return null;
   // Unknown cards and high rates from very small samples can make the choice inconclusive.
   if (ranked.Count < 2 || cards.Any(x => x.Rate <= 0 || x.Rate > 100 ||
       (x.Games < minGames && x.Rate >= ranked[0].Rate))) {
    reason = "Limited data";
    return null;
   }

   var first = ranked[0];
   if (Score(first, baseline) - Score(ranked[1], baseline) < 1.0) {
    reason = "Close choices";
    return null;
   }
   reason = ranked.Any(x => x.Rate > first.Rate) ? "Sample-adjusted" : "";
   return first;
  }
 }

 public static class BuildPlanner {
  static readonly HashSet<int> AntiHealIds = new HashSet<int> { 3033, 3165, 3075, 6609, 3123, 3916, 3076 };
  static readonly HashSet<int> AntiTankIds = new HashSet<int> { 3036, 3033, 3071, 3153, 3135, 3137, 6653, 3124, 6694, 4633 };

  public static int Canonical(int id) { return id >= 220000 && id < 230000 ? id % 10000 : id; }

  // Mayhem item tables provide pick rate without a sample size; use a conservative proxy.
  static double EvidenceScore(double rate, double pick, int games, double baseline) {
   double confidence = games > 0 ? games / (games + 250.0) : 0.65 * pick / (pick + 12.0);
   double delta = rate > 0 && rate <= 100 ? rate - baseline : 0;
   return Math.Max(-15, Math.Min(15, delta)) * confidence + Math.Log(1 + pick) * 0.65;
  }

  public static List<BuildStep> Plan(
   IEnumerable<Stat> source,
   IEnumerable<int> inventory,
   IEnumerable<int> boots,
   double baseline,
   Func<int, string[]> categories,
   string focus,
   IEnumerable<string> themes,
   IEnumerable<CoreBuildPath> corePaths = null,
   EnemyThreatProfile threats = null,
   Func<int, string> itemNameLookup = null,
   IEnumerable<int> pinnedItems = null,
   Func<int, double> componentProgress = null, bool useItemOutcomes = true) {

   var owned = new HashSet<int>(inventory.Select(Canonical));
   var bootSet = new HashSet<int>(boots.Select(Canonical));
   bool ownsBoots = owned.Any(bootSet.Contains);
   var pinned = new HashSet<int>((pinnedItems ?? Enumerable.Empty<int>()).Select(Canonical));
   var themesSet = new HashSet<string>(themes ?? new string[0]);
   var focusCategories = new Dictionary<string, string[]> {
    { "On-hit", new[] { "AttackSpeed", "OnHit" } },
    { "Critical strike", new[] { "CriticalStrike", "Damage" } },
    { "Ability power", new[] { "SpellDamage", "AbilityHaste" } },
    { "Survival", new[] { "Health", "Armor", "SpellBlock", "MagicResist" } }
   };

   // 1. Dynamic Core Branch Locking: if the player already owns 1+ core items, lock onto the strongest matching OP.GG 3-item core path!
   CoreBuildPath matchedBranch = null;
   int matchedAnchorId = 0;
   if (corePaths != null) {
    var ownedNonBoots = owned.Where(id => !bootSet.Contains(id)).ToList();
    if (ownedNonBoots.Count > 0) {
     matchedBranch = corePaths
      .Where(p => p.Ids != null && p.Ids.Count >= 2 && ownedNonBoots.Contains(Canonical(p.Ids[0])))
      .OrderByDescending(p => p.Ids.TakeWhile(id => ownedNonBoots.Contains(Canonical(id))).Count())
      .ThenByDescending(p => EvidenceScore(p.WinRate, Math.Max(0, p.PickRate), p.Games, baseline))
      .FirstOrDefault();
     if (matchedBranch != null) matchedAnchorId = matchedBranch.Ids.FirstOrDefault(id => ownedNonBoots.Contains(Canonical(id)));
    }
   }
   string anchorName = matchedAnchorId > 0 && itemNameLookup != null ? itemNameLookup(matchedAnchorId) : "";

   var result = new List<BuildStep>();
   int nonBootOwned = owned.Count(id => !bootSet.Contains(id));
   var candidates = source.Where(x => x.Id > 0).GroupBy(x => Canonical(x.Id))
    .Select(g => g.OrderBy(x => x.CoreSlot == 0 ? 4 : x.CoreSlot)
     .ThenByDescending(x => x.Games).ThenByDescending(x => Parser.Number(x.Pick)).First())
    .Where(x => !owned.Contains(Canonical(x.Id))).ToList();
   bool observedArmor = threats != null && threats.ArmoredCount > 0;
   bool delayDominik = nonBootOwned < 2 && !observedArmor && candidates.Any(x =>
    Canonical(x.Id) != 3036 && !bootSet.Contains(Canonical(x.Id)) && x.CoreSlot < 4);
   bool hasOpeningItem = nonBootOwned == 0 && candidates.Any(x => x.CoreSlot == 1 &&
    (!delayDominik || Canonical(x.Id) != 3036) && !bootSet.Contains(Canonical(x.Id)));
   foreach (var stat in candidates) {
    int cid = Canonical(stat.Id);
    if (bootSet.Contains(cid) && ownsBoots) continue;
    if (delayDominik && cid == 3036 && !pinned.Contains(cid)) continue;
    double pick = Math.Max(0, Parser.Number(stat.Pick));
    double score = EvidenceScore(useItemOutcomes ? stat.Rate : 0, pick, stat.Games, baseline);
    if (stat.CoreSlot > 0 && stat.CoreSlot <= 3)
     score += 5 - 2 * Math.Abs(stat.CoreSlot - Math.Min(3, nonBootOwned + 1));
    if (nonBootOwned == 0 && stat.CoreSlot == 0 && !bootSet.Contains(cid))
     score += Math.Log(1 + pick) * 1.5;
    double evidenceRank = score;

    var cats = categories(stat.Id);
    var reasons = new List<string>();
    string shortTip = "", synTag = "", synTone = "";

    // Dynamic Core Path Synergy!
    if (matchedBranch != null && matchedBranch.Ids.Any(id => Canonical(id) == cid)) {
     score += 2.5;
     synTag = "CORE SYNERGY";
     synTone = "indigo";
     reasons.Add(anchorName != "" ? ("Core synergy w/ " + anchorName) : "Matches your active core path");
     shortTip = "Core path synergy";
    }

    // Enemy Comp Live Adaptation!
    if (threats != null) {
     if (threats.NeedsAntiHeal && AntiHealIds.Contains(cid)) {
      score += 5.5;
      if (synTag == "") { synTag = "ANTI-HEAL"; synTone = "rose"; }
      reasons.Add("Counters enemy healing");
     }
     if (AntiTankIds.Contains(cid) && (cid == 3036 ? observedArmor : threats.NeedsAntiTank)) {
      score += 5.0;
      if (synTag == "") { synTag = "ANTI-TANK"; synTone = "gold"; }
      reasons.Add(cid == 3036 ? "Enemy armor confirmed" : "Shreds enemy tanks (" + threats.TankCount + " tanks)");
     }
     if (threats.HeavyAD && (cid == 3047 || cats.Contains("Armor"))) {
      score += (cid == 3047 ? 6.0 : 3.5);
      if (synTag == "") { synTag = "VS HEAVY AD"; synTone = "gold"; }
      reasons.Add("Strong vs heavy AD (" + threats.AdCount + " AD)");
     }
     if (threats.HeavyAP && (cid == 3111 || cats.Contains("SpellBlock") || cats.Contains("MagicResist"))) {
      score += (cid == 3111 ? 6.0 : 3.5);
      if (synTag == "") { synTag = "VS HEAVY AP"; synTone = "sky"; }
      reasons.Add("Strong vs heavy AP (" + threats.ApCount + " AP)");
     }
    }

    if (focusCategories.ContainsKey(focus) && cats.Intersect(focusCategories[focus]).Any()) { score += 4; reasons.Add(focus + " focus"); shortTip = focus + " core"; }
    if (themesSet.Contains("kCrit") && cats.Contains("CriticalStrike")) {
     score += 3.2; reasons.Add("fits crit augment");
     if (synTag == "") { synTag = "AUGMENT FIT"; synTone = "teal"; }
     if (shortTip == "") shortTip = "Fits Crit augment";
    }
    if (themesSet.Contains("kAttackSpeed") && cats.Any(x => x == "AttackSpeed" || x == "OnHit")) {
     score += 3.0; reasons.Add("fits on-hit/AS augment");
     if (synTag == "") { synTag = "AUGMENT FIT"; synTone = "teal"; }
     if (shortTip == "") shortTip = "Fits AS / On-hit augment";
    }
    if (themesSet.Contains("kAP") && cats.Any(x => x == "SpellDamage" || x == "MagicPenetration")) {
     score += 2.6; reasons.Add("fits AP/spell augment");
     if (synTag == "") { synTag = "AUGMENT FIT"; synTone = "teal"; }
     if (shortTip == "") shortTip = "Fits AP augment";
    }
    if (themesSet.Contains("kHaste") && cats.Any(x => x == "AbilityHaste" || x == "CooldownReduction")) {
     score += 2.4; reasons.Add("fits haste augment");
     if (synTag == "") { synTag = "AUGMENT FIT"; synTone = "teal"; }
     if (shortTip == "") shortTip = "Fits Haste augment";
    }
    if (themesSet.Contains("kHealShield") && cats.Any(x => x == "LifeSteal" || x == "SpellVamp" || x == "HealthRegen")) {
     score += 2.6; reasons.Add("fits heal/shield augment");
     if (synTag == "") { synTag = "AUGMENT FIT"; synTone = "teal"; }
     if (shortTip == "") shortTip = "Fits Heal/Shield augment";
    }
    if (themesSet.Contains("kSpeed") && cats.Any(x => x == "AttackSpeed" || x == "NonbootsMovement")) {
     score += 2.2; reasons.Add("fits speed augment");
     if (synTag == "") { synTag = "AUGMENT FIT"; synTone = "teal"; }
     if (shortTip == "") shortTip = "Fits Speed augment";
    }
    if (themesSet.Contains("kResilience") && cats.Any(x => x == "Health" || x == "Armor" || x == "SpellBlock" || x == "MagicResist")) {
     score += 2.4; reasons.Add("fits resilience augment");
     if (synTag == "") { synTag = "AUGMENT FIT"; synTone = "teal"; }
     if (shortTip == "") shortTip = "Fits Tank augment";
    }
    score = evidenceRank + Math.Min(3.5, score - evidenceRank);
    double progress = componentProgress == null ? 0 : componentProgress(stat.Id);
    if (progress > 0) {
     score += 4 * Math.Min(1, progress);
     reasons.Add("Builds from owned components");
     if (shortTip == "") shortTip = "Components owned";
    }

    if (pinned.Contains(cid)) {
     reasons.Insert(0, "Pinned by you");
     shortTip = "Pinned purchase";
    }
    if (reasons.Count == 0) {
     if (stat.CoreSlot == 1) { reasons.Add("Core 1st item rush"); shortTip = "Core 1st rush"; }
     else if (stat.CoreSlot == 2) { reasons.Add("Core 2nd item power spike"); shortTip = "2nd core spike"; }
     else if (stat.CoreSlot == 3) { reasons.Add("Core 3rd item finisher"); shortTip = "3rd core spike"; }
     else if (stat.CoreSlot >= 4) { reasons.Add("Late-game situational spike"); shortTip = "Late situational"; }
     else if (useItemOutcomes && pick >= 35 && stat.Rate >= baseline + 1.5) { reasons.Add("Core staple · high win & pick"); shortTip = "Core staple"; }
     else if (pick >= 25) { reasons.Add("Most popular core item"); shortTip = "Popular core"; }
     else { reasons.Add(useItemOutcomes ? "Reported item results" : "Observed item selections"); shortTip = "Item stats"; }
    }

    bool isBoot = bootSet.Contains(cid);
    result.Add(new BuildStep {
     Stat = stat, Score = score, PickRate = pick, IsBoots = isBoot,
     Reason = String.Join(" · ", reasons),
     ShortTip = shortTip != "" ? shortTip : "Core pick",
     SynergyTag = synTag, SynergyTone = synTone
    });
   }

   var ranked = result.OrderByDescending(x => pinned.Contains(Canonical(x.Stat.Id)))
    .ThenBy(x => nonBootOwned < 3 && x.Stat.CoreSlot >= 4)
    .ThenBy(x => hasOpeningItem && !x.IsBoots && x.Stat.CoreSlot != 1)
    .ThenByDescending(x => x.Score).ThenByDescending(x => x.Stat.Games).ThenByDescending(x => x.PickRate).ToList();
   int slotsLeft = Math.Max(0, 6 - owned.Count);
   var chosen = ranked.Where(x => !x.IsBoots &&
    (nonBootOwned >= 3 || x.Stat.CoreSlot < 4 || pinned.Contains(Canonical(x.Stat.Id)))).Take(slotsLeft).ToList();
   if (!ownsBoots && slotsLeft > 0) {
    // Prefer enemy-adapted boots (Steelcaps vs Heavy AD / Mercury's vs Heavy AP) if present!
    var boot = ranked.Where(x => x.IsBoots)
     .FirstOrDefault();
    if (boot != null) {
     if (boot.SynergyTag == "") {
      boot.Reason = "Popular boots · adapt vs heavy AD/AP or CC";
      boot.ShortTip = "Popular boots · adapt";
     }
     int at = pinned.Contains(Canonical(boot.Stat.Id)) ? 0 : Math.Min(chosen.Count, Math.Max(0, 2 - nonBootOwned));
     chosen.Insert(at, boot);
    }
   }
   var finalPath = chosen.Take(slotsLeft).ToList();
   for (int i = 0; i < finalPath.Count; i++) {
    if (i == 0) finalPath[i].Role = finalPath[i].IsBoots ? "NEXT · BOOTS" : "NEXT BUY";
    else if (finalPath[i].IsBoots) finalPath[i].Role = "BOOTS";
    else if (i <= 2) finalPath[i].Role = "CORE " + (i + 1);
    else finalPath[i].Role = "STEP " + (i + 1);
   }
   return finalPath;
  }

  public static int RemainingCost(int target, IEnumerable<int> inventory, Dictionary<int, Dictionary<string, object>> items) { Dictionary<string, object> item; if (!items.TryGetValue(target, out item)) return 0; var counts = inventory.Select(Canonical).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count()); int credit = 0; foreach (int child in Json.A(item, "from").Select(Json.ToInt).Where(x => x > 0)) credit += ComponentCredit(child, counts, items, new HashSet<int>()); return Math.Max(0, Json.I(item, "priceTotal") - credit); }
  static int ComponentCredit(int id, Dictionary<int, int> counts, Dictionary<int, Dictionary<string, object>> items, HashSet<int> path) { Dictionary<string, object> item; if (!items.TryGetValue(id, out item) || path.Contains(id)) return 0; if (counts.ContainsKey(Canonical(id)) && counts[Canonical(id)] > 0) { counts[Canonical(id)]--; return Json.I(item, "priceTotal"); } var next = new HashSet<int>(path) { id }; return Json.A(item, "from").Select(Json.ToInt).Where(x => x > 0).Sum(child => ComponentCredit(child, counts, items, next)); }
  public static List<ComponentStatus> Components(int target, IEnumerable<int> inventory, Dictionary<int, Dictionary<string, object>> items) {
   var list = new List<ComponentStatus>(); Dictionary<string, object> item; if (!items.TryGetValue(target, out item)) return list;
   var counts = inventory.Select(Canonical).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
   foreach (int child in Json.A(item, "from").Select(Json.ToInt).Where(x => x > 0)) {
    int canon = Canonical(child); bool has = counts.ContainsKey(canon) && counts[canon] > 0;
    if (has) counts[canon]--;
    list.Add(new ComponentStatus { Id = child, Owned = has });
   }
   return list;
  }
 }
}
