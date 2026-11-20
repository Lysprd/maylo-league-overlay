using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MayhemCompanion {
 public class AccountProfile {
  public string Puuid, AccountId, SummonerId, Name, Tag, Region = ""; public int Level, ProfileIconId;
  public string Key { get { return !String.IsNullOrEmpty(Puuid) ? Puuid : AccountId; } }
  public string RiotId { get { return Name + (String.IsNullOrEmpty(Tag) ? "" : "#" + Tag); } }
 }
 public class MatchRecord {
  public string Id { get; set; } public long Created { get; set; } public int ChampionId { get; set; } public int Queue { get; set; } public string Mode { get; set; }
  public int Kills { get; set; } public int Deaths { get; set; } public int Assists { get; set; } public bool Won { get; set; } public bool Remake { get; set; }
  public int Duration { get; set; } public int Damage { get; set; } public int Gold { get; set; } public int Cs { get; set; } public int Heal { get; set; }
  public List<int> Items { get; set; } public List<int> Augments { get; set; } public List<int> Runes { get; set; }
  public bool Mayhem { get { return Queue == 2400 || Queue == 3270 || Mode == "KIWI"; } }
  public string QueueName { get { return Mayhem ? "ARAM Mayhem" : (Queue == 1700 || Queue == 1710 || Mode == "CHERRY") ? "Arena" : Queue == 450 ? "ARAM" : Queue == 420 ? "Ranked Solo" : Queue == 440 ? "Ranked Flex" : Mode == "PRACTICETOOL" ? "Practice" : Mode == "SWIFTPLAY" ? "Quickplay" : Mode == "CLASSIC" ? "Summoner's Rift" : Mode ?? "LoL"; } }
  public string Time { get { return Duration / 60 + ":" + (Duration % 60).ToString("00"); } }
  public DateTime Date { get { return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(Created).ToLocalTime(); } }
 }
 public static class ProfileParser {
  public static AccountProfile Account(Dictionary<string, object> d) { return new AccountProfile { Puuid = Json.S(d, "puuid"), AccountId = Json.S(d, "accountId"), SummonerId = Json.S(d, "summonerId"), Name = Json.S(d, "gameName") != "" ? Json.S(d, "gameName") : Json.S(d, "displayName"), Tag = Json.S(d, "tagLine"), Level = Json.I(d, "summonerLevel"), ProfileIconId = Json.I(d, "profileIconId") }; }
  static bool Same(Dictionary<string, object> player, AccountProfile account) { return player != null && ((account.Puuid != "" && Json.S(player, "puuid") == account.Puuid) || (account.AccountId != "" && (Json.S(player, "accountId") == account.AccountId || Json.S(player, "currentAccountId") == account.AccountId)) || (account.SummonerId != "" && Json.S(player, "summonerId") == account.SummonerId)); }
  public static List<MatchRecord> Matches(Dictionary<string, object> root, AccountProfile account) {
   var result = new List<MatchRecord>();
   if (root == null || account == null) return result;
   var wrapper = root.ContainsKey("games") ? Json.D(root["games"]) : null;
   foreach (var value in Json.A(wrapper, "games")) {
    var game = Json.D(value); if (game == null) continue;
    var participants = Json.A(game, "participants").Select(Json.D).Where(p => p != null).ToList(); int local = -1;
    foreach (var identity in Json.A(game, "participantIdentities").Select(Json.D).Where(id => id != null)) { var p = identity.ContainsKey("player") ? Json.D(identity["player"]) : null; if (Same(p, account)) { local = Json.I(identity, "participantId"); break; } }
    var participant = participants.FirstOrDefault(p => Json.I(p, "participantId") == local); if (participant == null) continue;
    var s = participant.ContainsKey("stats") ? Json.D(participant["stats"]) : null; if (s == null) continue;
    long created; long.TryParse(Json.S(game, "gameCreation"), out created);
    var record = new MatchRecord { Id = Json.S(game, "gameId"), Created = created, ChampionId = Json.I(participant, "championId"), Queue = Json.I(game, "queueId"), Mode = Json.S(game, "gameMode"), Kills = Json.I(s, "kills"), Deaths = Json.I(s, "deaths"), Assists = Json.I(s, "assists"), Won = Json.S(s, "win").Equals("true", StringComparison.OrdinalIgnoreCase), Remake = Json.S(s, "gameEndedInEarlySurrender").Equals("true", StringComparison.OrdinalIgnoreCase), Duration = Json.I(game, "gameDuration"), Damage = Json.I(s, "totalDamageDealtToChampions"), Gold = Json.I(s, "goldEarned"), Cs = Json.I(s, "totalMinionsKilled") + Json.I(s, "neutralMinionsKilled"), Heal = Json.I(s, "totalHeal"), Items = new List<int>(), Augments = new List<int>(), Runes = new List<int>() };
    for (int i = 0; i < 7; i++) { int id = Json.I(s, "item" + i); if (id > 0) record.Items.Add(id); }
    for (int i = 1; i <= 6; i++) { int id = Json.I(s, "playerAugment" + i); if (id > 0) record.Augments.Add(id); }
    for (int i = 0; i < 6; i++) { int id = Json.I(s, "perk" + i); if (id > 0) record.Runes.Add(id); }
    if (record.Id != "") result.Add(record);
   }
   return result.OrderByDescending(r => r.Created).ToList();
  }
 }
 public class ProfileService {
  public AccountProfile Account; public List<MatchRecord> History = new List<MatchRecord>(); public string Status = "Connecting account…"; public DateTime Updated; public int Requested = 25; bool busy; DateTime nextRefresh = DateTime.MinValue;
  public static string CachePath(AccountProfile account) { using (var sha = System.Security.Cryptography.SHA256.Create()) { string key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(account.Key))).Replace("-", ""); return Path.Combine(WebCache.Root, "history-" + key + ".json"); } }
  public async Task<bool> Refresh(GameBridge bridge, bool force = false) {
   if (busy || (!force && DateTime.UtcNow < nextRefresh)) return false;
   busy = true; nextRefresh = DateTime.UtcNow.AddSeconds(20);
   try {
    var account = await bridge.ReadAccount(); bool changed = Account == null || Account.Key != account.Key;
    if (changed) { Account = account; History = new List<MatchRecord>(); Requested = 25; try { History = Json.Serializer().Deserialize<List<MatchRecord>>(File.ReadAllText(CachePath(account))) ?? History; } catch { } }
    var history = await bridge.ReadHistory(account, Requested);
    Account = account; History = history.Concat(History).GroupBy(m => m.Id).Select(g => g.First()).OrderByDescending(m => m.Created).Take(500).ToList(); Updated = DateTime.Now;
    Status = History.Count + " matches · synced " + Updated.ToString("HH:mm");
    Directory.CreateDirectory(WebCache.Root); File.WriteAllText(CachePath(account), Json.Serializer().Serialize(History), Encoding.UTF8);
    return true;
   } catch {
    string nextStatus = Account == null ? "Open League to connect your account" : "Local history · client unavailable";
    bool changed = Status != nextStatus;
    Status = nextStatus;
    return changed;
   }
   finally { busy = false; }
  }
 }
}
