using System;
using System.Linq;
using System.Collections.Generic;
namespace MayhemCompanion {
 public class LivePlayer {
  public string Name, Champion, Team, Position = "", Spell1 = "", Spell2 = "", SpellKey1 = "", SpellKey2 = "", Keystone = "", RuneTree = "", SecondaryRuneTree = "";
  public double Respawn; public int RuneId, PrimaryTreeId, SecondaryTreeId, Level, Kills, Deaths, Assists, Cs; public bool Dead, Me;
  public List<int> Items = new List<int>();
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
  public List<ObjectiveTimer> Objectives = new List<ObjectiveTimer>();
  public double Seconds;
  public string Clock { get { return ((int)Seconds) / 60 + ":" + (((int)Seconds) % 60).ToString("00"); } }
 }
 public static class LiveParser {
  static double Number(Dictionary<string, object> d, string key) { double value; return Double.TryParse(Json.S(d, key), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out value) ? value : 0; }
  static int Ability(Dictionary<string, object> d, string key) { return d != null && d.ContainsKey(key) ? Json.I(Json.D(d[key]), "abilityLevel") : 0; }
  static string SpellKey(string raw) { return raw.Replace("GeneratedTip_SummonerSpell_", "").Replace("_DisplayName", ""); }
  public static LiveSummary Parse(Dictionary<string, object> root) {
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

   double lastDragon = -1, lastBaron = -1, lastHerald = -1, lastGrubs = -1;
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
    if (type == "DragonKill") {
     lastDragon = et;
     string dt = Json.S(e, "DragonType");
     if (dt != "") result.LastDragonType = dt;
     string killer = Json.S(e, "KillerName");
     string tm; if (playerTeam.TryGetValue(killer, out tm)) { if (tm == "ORDER") result.BlueDragons++; else if (tm == "CHAOS") result.RedDragons++; }
    }
    else if (type == "BaronKill") lastBaron = et;
    else if (type == "HeraldKill") lastHerald = et;
    else if (type == "HordeKill") lastGrubs = et;
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

   // Compute live SR Objective Timers ONLY in Summoner's Rift (CLASSIC / SWIFTPLAY)
   string gMode = Json.S(data, "gameMode");
   bool isSr = String.Equals(gMode, "CLASSIC", StringComparison.OrdinalIgnoreCase) || String.Equals(gMode, "SWIFTPLAY", StringComparison.OrdinalIgnoreCase);
   if (isSr) {
    double t = result.Seconds;
    bool elderActive = result.BlueDragons >= 4 || result.RedDragons >= 4;
    double dragRespawn = elderActive ? 360 : 300;
    double dragNext = lastDragon >= 0 ? lastDragon + dragRespawn : 300;
    string dragName = elderActive ? "Elder" : ((result.BlueDragons == 3 || result.RedDragons == 3) ? "Soul" : "Dragon");
    result.Objectives.Add(new ObjectiveTimer { Key = "dragon", Name = dragName, SecondsLeft = Math.Max(0, dragNext - t), Alive = t >= dragNext, Status = t >= dragNext ? "UP" : FormatClock(dragNext - t) });
    if (t < 960) {
     double grubsNext = lastGrubs >= 0 ? lastGrubs + 240 : 360;
     bool grubsDone = lastGrubs >= 0 && grubsNext >= 900;
     result.Objectives.Add(new ObjectiveTimer { Key = "grubs", Name = "Voidgrubs", SecondsLeft = grubsDone ? 0 : Math.Max(0, grubsNext - t), Alive = !grubsDone && t >= grubsNext, Status = grubsDone ? "DONE" : (t >= grubsNext ? "UP" : FormatClock(grubsNext - t)) });
    } else if (t < 1500) {
     bool heraldDone = lastHerald >= 0;
     result.Objectives.Add(new ObjectiveTimer { Key = "herald", Name = "Herald", SecondsLeft = heraldDone ? 0 : Math.Max(0, 960 - t), Alive = !heraldDone && t >= 960, Status = heraldDone ? "DONE" : (t >= 960 ? "UP" : FormatClock(960 - t)) });
    }
    double baronNext = lastBaron >= 0 ? lastBaron + 360 : 1500;
    result.Objectives.Add(new ObjectiveTimer { Key = "baron", Name = "Baron", SecondsLeft = Math.Max(0, baronNext - t), Alive = t >= baronNext, Status = t >= baronNext ? "UP" : FormatClock(baronNext - t) });
   }

   return result;
  }
  static string FormatClock(double secs) { int s = Math.Max(0, (int)Math.Ceiling(secs)); return (s / 60) + ":" + (s % 60).ToString("00"); }
 }
}
