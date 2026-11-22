using System;
using System.Linq;
using System.Collections.Generic;
namespace MayhemCompanion {
 public class LivePlayer {
  public string Name, Champion, Team, Position = "", Spell1 = "", Spell2 = "", SpellKey1 = "", SpellKey2 = "", Keystone = "", RuneTree = "", SecondaryRuneTree = "";
  public double Respawn; public int RuneId, PrimaryTreeId, SecondaryTreeId, Level, Kills, Deaths, Assists, Cs; public bool Dead, Me;
  public List<int> Items = new List<int>();
  public bool InventoryReported;
  public string Kda { get { return Kills + " / " + Deaths + " / " + Assists; } }
 }
 public class ObjectiveTimer {
  public string Key, Name, Status;
  public double SecondsLeft;
  public bool Alive;
 }
 public class LiveSummary {
  public double Health, MaxHealth, AttackDamage, AbilityPower, Armor, MagicResist, AttackSpeed, MoveSpeed;
  public int AbilityQ, AbilityW, AbilityE, AbilityR, ActiveLevel, BlueDragons, RedDragons;
  public string LastDragonType = "";
  public List<LivePlayer> Players = new List<LivePlayer>();
  public List<string> Events = new List<string>();
  public List<MatchMoment> ReviewEvents = new List<MatchMoment>();
  public List<ObjectiveTimer> Objectives = new List<ObjectiveTimer>();
  public List<ObjectiveTimer> Inhibitors = new List<ObjectiveTimer>();

  public object InventoryValue(Dictionary<int, Dictionary<string, object>> itemMap) {
   var teams = Players.Where(p => p.Team == "ORDER" || p.Team == "CHAOS").GroupBy(p => p.Team).ToArray();
   if (teams.Length != 2 || itemMap.Count == 0 || teams.Any(group => !group.Any(p => p.InventoryReported))) return null;
   int blue = 0, red = 0;
   bool partial = teams[0].Count() != teams[1].Count();
   foreach (var p in teams.SelectMany(group => group)) {
    if (!p.InventoryReported) partial = true;
    foreach (int id in p.Items) {
     Dictionary<string, object> item;
     int price;
     if (!itemMap.TryGetValue(id, out item) || !Int32.TryParse(Json.S(item, "priceTotal"), out price) || price < 0) {
      partial = true;
      continue;
     }
     if (p.Team == "ORDER") blue += price;
     else red += price;
    }
   }
   return new { blue, red, partial };
  }
  public double Seconds;
  public string Clock { get { return ((int)Seconds) / 60 + ":" + (((int)Seconds) % 60).ToString("00"); } }
 }
 public static class LiveParser {
  static double Number(Dictionary<string, object> d, string key) { double value; return Double.TryParse(Json.S(d, key), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out value) ? value : 0; }
  static int Ability(Dictionary<string, object> d, string key) { return d != null && d.ContainsKey(key) ? Json.I(Json.D(d[key]), "abilityLevel") : 0; }
  static string SpellKey(string raw) { return raw.Replace("GeneratedTip_SummonerSpell_", "").Replace("_DisplayName", ""); }
  public static LiveSummary Parse(Dictionary<string, object> root, string modeKey = null, int queueId = 0) {
   var result = new LiveSummary();
   if (root == null) return result;
   var active = root.ContainsKey("activePlayer") ? Json.D(root["activePlayer"]) : null;
   var data = root.ContainsKey("gameData") ? Json.D(root["gameData"]) : null;
   if (data != null && data.ContainsKey("gameTime")) result.Seconds = Number(data, "gameTime");
   if (active != null) result.ActiveLevel = Json.I(active, "level");
   var stats = active != null && active.ContainsKey("championStats") ? Json.D(active["championStats"]) : null;
   result.Health = Number(stats, "currentHealth"); result.MaxHealth = Number(stats, "maxHealth"); result.AttackDamage = Number(stats, "attackDamage"); result.AbilityPower = Number(stats, "abilityPower"); result.Armor = Number(stats, "armor"); result.MagicResist = Number(stats, "magicResist"); result.AttackSpeed = Number(stats, "attackSpeed"); result.MoveSpeed = Number(stats, "moveSpeed");
   var abilities = active != null && active.ContainsKey("abilities") ? Json.D(active["abilities"]) : null;
   result.AbilityQ = Ability(abilities, "Q"); result.AbilityW = Ability(abilities, "W"); result.AbilityE = Ability(abilities, "E"); result.AbilityR = Ability(abilities, "R");
   foreach (var item in Json.A(root, "allPlayers")) {
    var p = Json.D(item); if (p == null) continue;
    var scores = p.ContainsKey("scores") ? Json.D(p["scores"]) : null;
    var player = new LivePlayer { Name = Json.S(p, "riotId") != "" ? Json.S(p, "riotId") : Json.S(p, "summonerName"), Champion = Json.S(p, "rawChampionName").Replace("game_character_displayname_", ""), Team = Json.S(p, "team"), Level = Json.I(p, "level"), Kills = Json.I(scores, "kills"), Deaths = Json.I(scores, "deaths"), Assists = Json.I(scores, "assists"), Cs = Json.I(scores, "creepScore"), Dead = Json.S(p, "isDead").Equals("true", StringComparison.OrdinalIgnoreCase), Me = new[] { "riotId", "summonerName" }.Any(k => Json.S(p, k) != "" && Json.S(p, k) == Json.S(active, k)) };
    if (player.Champion == "") player.Champion = Json.S(p, "championName");
    if (player.Me && result.ActiveLevel == 0) result.ActiveLevel = player.Level;
    player.InventoryReported = p.ContainsKey("items") && p["items"] != null;
    foreach (var i in Json.A(p, "items")) { int id = Json.I(Json.D(i), "itemID"); if (id > 0) player.Items.Add(id); }
    player.Position = Json.S(p, "position"); player.Respawn = Number(p, "respawnTimer");
    var spells = p.ContainsKey("summonerSpells") ? Json.D(p["summonerSpells"]) : null;
    var first = spells != null && spells.ContainsKey("summonerSpellOne") ? Json.D(spells["summonerSpellOne"]) : null;
    var second = spells != null && spells.ContainsKey("summonerSpellTwo") ? Json.D(spells["summonerSpellTwo"]) : null;
    player.Spell1 = Json.S(first, "displayName"); player.Spell2 = Json.S(second, "displayName");
    player.SpellKey1 = SpellKey(Json.S(first, "rawDisplayName")); player.SpellKey2 = SpellKey(Json.S(second, "rawDisplayName"));
    var runes = p.ContainsKey("runes") ? Json.D(p["runes"]) : null;
    var key = runes != null && runes.ContainsKey("keystone") ? Json.D(runes["keystone"]) : null;
    var tree = runes != null && runes.ContainsKey("primaryRuneTree") ? Json.D(runes["primaryRuneTree"]) : null;
    var subTree = runes != null && runes.ContainsKey("secondaryRuneTree") ? Json.D(runes["secondaryRuneTree"]) : null;
    player.Keystone = Json.S(key, "displayName"); player.RuneId = Json.I(key, "id");
    player.RuneTree = Json.S(tree, "displayName"); player.PrimaryTreeId = Json.I(tree, "id");
    player.SecondaryRuneTree = Json.S(subTree, "displayName"); player.SecondaryTreeId = Json.I(subTree, "id");
    result.Players.Add(player);
   }

   double lastDragon = -1, lastElder = -1, lastBaron = -1, lastHerald = -1;
   int drakesKilled = 0, grubsKilled = 0;
   var events = root.ContainsKey("events") ? Json.D(root["events"]) : null;
   var allEvents = Json.A(events, "Events").Select(Json.D).Where(d => d != null).ToList();
   var playerTeam = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
   foreach (var pl in result.Players) {
    if (!String.IsNullOrEmpty(pl.Name)) playerTeam[pl.Name] = pl.Team;
    string shortName = (pl.Name ?? "").Split('#')[0];
    if (!String.IsNullOrEmpty(shortName)) playerTeam[shortName] = pl.Team;
   }
   foreach (var e in allEvents) {
    string type = Json.S(e, "EventName");
    double et = Number(e, "EventTime");
    if (type == "DragonKill" || type == "BaronKill" || type == "HeraldKill" || type == "HordeKill" ||
        type == "TurretKilled" || type == "InhibKilled") {
     result.ReviewEvents.Add(new MatchMoment {
      Key = "event:" + Json.S(e, "EventID") + ":" + type + ":" + et,
      Seconds = et, Kind = type, Detail = Json.S(e, "DragonType")
     });
    }
    if (type == "DragonKill") {
     lastDragon = et;
     string dt = Json.S(e, "DragonType");
     if (dt != "") result.LastDragonType = dt;
     if (dt.IndexOf("Elder", StringComparison.OrdinalIgnoreCase) >= 0) lastElder = et;
     else {
      drakesKilled++;
      string tm;
      if (playerTeam.TryGetValue(Json.S(e, "KillerName"), out tm)) {
       if (tm == "ORDER") result.BlueDragons++;
       else if (tm == "CHAOS") result.RedDragons++;
      }
     }
    }
    else if (type == "BaronKill") lastBaron = et;
    else if (type == "HeraldKill") lastHerald = et;
    else if (type == "HordeKill") grubsKilled++;
   }
   foreach (var e in allEvents.AsEnumerable().Reverse().Take(8)) {
    string type = Json.S(e, "EventName"), text = "";
    if (type == "ChampionKill") text = Json.S(e, "KillerName") + " → " + Json.S(e, "VictimName");
    else if (type == "TurretKilled") text = "Turret destroyed · " + Json.S(e, "KillerName");
    else if (type == "InhibKilled") text = "Inhibitor destroyed";
    else if (type == "DragonKill") text = "Dragon · " + Json.S(e, "DragonType");
    else if (type == "BaronKill") text = "Baron slain";
    else if (type == "HeraldKill") text = "Rift Herald slain";
    else if (type == "GameStart") text = "Game started";
    if (text != "") result.Events.Add(text);
   }

   string gMode = Json.S(data, "gameMode");
   bool isSr = (modeKey ?? GameBridge.DetectModeKey(gMode, queueId, false)) == "SR";
   bool swiftRules = queueId == 480 || String.Equals(gMode, "SWIFTPLAY", StringComparison.OrdinalIgnoreCase);
   bool botQueue = queueId >= 830 && queueId <= 890;
   if (isSr && (swiftRules || !botQueue)) {
    double t = result.Seconds;
    bool elderActive = result.BlueDragons >= 4 || result.RedDragons >= 4 || lastElder >= 0;
    double dragNext = lastDragon >= 0 ? lastDragon + (elderActive ? 360 : 300) : 300;
    string dragName = elderActive ? "Elder" : ((result.BlueDragons == 3 || result.RedDragons == 3) ? "Soul" : "Dragon");
    if (swiftRules) {
     if (t >= 900 || drakesKilled >= 2 || dragNext >= 900) {
      dragName = "Elder";
      dragNext = lastElder >= 0 ? lastElder + 360 : 900;
     } else {
      dragName = "Dragon";
     }
    } else {
     result.Inhibitors = InhibitorTimers(allEvents, t);
     if (t < 885)
      result.Objectives.Add(grubsKilled >= 3 ? Completed("grubs", "Voidgrubs") : Timer("grubs", "Voidgrubs", 480, t));
     if (t < 1200)
      result.Objectives.Add(lastHerald >= 0 ? Completed("herald", "Herald") : Timer("herald", "Herald", 900, t));
    }
    result.Objectives.Insert(0, Timer("dragon", dragName, dragNext, t));
    result.Objectives.Add(Timer("baron", "Baron", lastBaron >= 0 ? lastBaron + 360 : (swiftRules ? 720 : 1200), t));
   }
   return result;
  }
  static ObjectiveTimer Timer(string key, string name, double deadline, double now) {
   double remaining = Math.Max(0, deadline - now);
   return new ObjectiveTimer { Key = key, Name = name, SecondsLeft = remaining, Alive = remaining == 0,
    Status = remaining == 0 ? "UP" : FormatClock(remaining) };
  }
  static ObjectiveTimer Completed(string key, string name) {
   return new ObjectiveTimer { Key = key, Name = name, Status = "DONE" };
  }
  static List<ObjectiveTimer> InhibitorTimers(IEnumerable<Dictionary<string, object>> events, double seconds) {
   var down = new Dictionary<string, double>();
   foreach (var e in events.OrderBy(e => Number(e, "EventTime")).ThenBy(e => Json.I(e, "EventID"))) {
    string kind = Json.S(e, "EventName");
    if (kind != "InhibKilled" && kind != "InhibRespawned") continue;
    string id = Json.S(e, kind);
    double time = Number(e, "EventTime");
    if (id == "" || time <= 0 || time > seconds) continue;
    if (kind == "InhibRespawned") down.Remove(id);
    else down[id] = time + 300;
   }
   return down.Where(pair => pair.Value > seconds).OrderBy(pair => pair.Value).Select(pair => {
    var match = System.Text.RegularExpressions.Regex.Match(pair.Key, @"^Inhib_T(Order|Chaos)_L([012])_");
    string name = "Inhibitor";
    if (match.Success) {
     string side = match.Groups[1].Value == "Order" ? "Blue" : "Red";
     string lane = new[] { "Bot", "Mid", "Top" }[Int32.Parse(match.Groups[2].Value)];
     name = side + " " + lane + " inhibitor";
    }
    return new ObjectiveTimer {
     Key = pair.Key, Name = name, SecondsLeft = pair.Value - seconds, Status = FormatClock(pair.Value - seconds)
    };
   }).ToList();
  }

  static string FormatClock(double secs) { int s = Math.Max(0, (int)Math.Ceiling(secs)); return (s / 60) + ":" + (s % 60).ToString("00"); }
 }
}
