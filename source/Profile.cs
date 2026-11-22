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
 public class MatchMoment {
  public string Key, Kind, Detail;
  public double Seconds;
  public int Item;
 }

 public sealed class MatchReviewCapture {
  public string Key { get; private set; }
  public List<MatchMoment> Moments { get; private set; }
  readonly HashSet<string> seenEvents = new HashSet<string>();
  Dictionary<int, int> owned;

  public MatchReviewCapture(string key, IEnumerable<MatchMoment> existing) {
   Key = key;
   Moments = existing.OrderBy(moment => moment.Seconds).ToList();
   foreach (var moment in Moments) if (moment.Kind != "item") seenEvents.Add(moment.Key);
  }

  public bool Observe(LiveSummary live) {
   int previousCount = Moments.Count;
   foreach (var moment in live.ReviewEvents) {
    if (seenEvents.Add(moment.Key)) Moments.Add(moment);
   }
   var me = live.Players.FirstOrDefault(player => player.Me && player.InventoryReported);
   if (me != null) {
    var current = me.Items.Where(id => id > 0).GroupBy(id => id).ToDictionary(group => group.Key, group => group.Count());
    if (owned != null) {
     foreach (var item in current) {
      int before;
      owned.TryGetValue(item.Key, out before);
      for (int i = before; i < item.Value; i++) Moments.Add(new MatchMoment { Kind = "item", Item = item.Key, Seconds = live.Seconds });
     }
    }
    owned = current;
   }
   if (Moments.Count == previousCount) return false;
   Moments = Moments.OrderBy(moment => moment.Seconds).Take(240).ToList();
   return true;
  }
 }

 public class CollectionSkin {
  public int Id;
  public string Name, Champion, Tile;
  public bool Owned;
 }

 public class RankSnapshot {
  public long Created;
  public string Tier, Division;
  public int Lp;
 }

 public static class ProfileParser {
  public static List<CollectionSkin> Skins(object data) {
   var skins = new List<CollectionSkin>();
   foreach (var champion in Json.Array(data).Select(Json.D)) {
    string title = Json.S(champion, "name");
    foreach (var skin in Json.A(champion, "skins").Select(Json.D)) {
     int id = Json.I(skin, "id");
     if (id <= 0 || id % 1000 == 0 || Json.S(skin, "isBase").Equals("true", StringComparison.OrdinalIgnoreCase)) continue;
     Dictionary<string, object> ownership = skin.ContainsKey("ownership") ? Json.D(skin["ownership"]) : null;
     bool owned;
     if (!Boolean.TryParse(Json.S(ownership, "owned"), out owned) &&
         !Boolean.TryParse(Json.S(skin, "owned"), out owned)) continue;
     string name = Json.S(skin, "name"), tile = Json.S(skin, "tilePath");
     if (name == "") continue;
     const string prefix = "/lol-game-data/assets/";
     string icon = tile.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
      ? "https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/" + tile.Substring(prefix.Length).ToLowerInvariant()
      : "";
     skins.Add(new CollectionSkin { Id = id, Name = name, Champion = title, Tile = icon, Owned = owned });
    }
   }
   return skins.GroupBy(skin => skin.Id).Select(group => group.First())
    .OrderBy(skin => skin.Champion).ThenBy(skin => skin.Name).ToList();
  }

  public static RankSnapshot SoloRank(Dictionary<string, object> root, long now) {
   const string solo = "RANKED_SOLO_5x5";
   var queues = root != null && root.ContainsKey("queueMap") ? Json.D(root["queueMap"]) : null;
   var rank = queues != null && queues.ContainsKey(solo) ? Json.D(queues[solo]) :
    Json.A(root, "queues").Select(Json.D).FirstOrDefault(value => Json.S(value, "queueType") == solo);
   string tier = Json.S(rank, "tier");
   int lp;
   if (tier == "" || tier == "NONE" || tier == "UNRANKED" ||
       !Int32.TryParse(Json.S(rank, "leaguePoints"), out lp) || lp < 0) return null;
   return new RankSnapshot { Created = now, Tier = tier, Division = Json.S(rank, "division"), Lp = lp };
  }

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
  public List<RankSnapshot> RankHistory = new List<RankSnapshot>();
  DateTime lastRankCheck;
  string latestRankedMatch = "";
  public static string CachePath(AccountProfile account) { using (var sha = System.Security.Cryptography.SHA256.Create()) { string key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(account.Key))).Replace("-", ""); return Path.Combine(WebCache.Root, "history-" + key + ".json"); } }
  public async Task<bool> Refresh(GameBridge bridge, bool force = false) {
   if (busy || (!force && DateTime.UtcNow < nextRefresh)) return false;
   busy = true; nextRefresh = DateTime.UtcNow.AddSeconds(20);
   try {
    var account = await bridge.ReadAccount(); bool changed = Account == null || Account.Key != account.Key;
    if (changed) {
     Account = account; History = new List<MatchRecord>(); Requested = 25;
     RankHistory = new List<RankSnapshot>(); lastRankCheck = DateTime.MinValue; latestRankedMatch = "";
     try { History = Json.Serializer().Deserialize<List<MatchRecord>>(File.ReadAllText(CachePath(account))) ?? History; } catch { }
     try { RankHistory = Json.Serializer().Deserialize<List<RankSnapshot>>(File.ReadAllText(CachePath(account) + ".rank")) ?? RankHistory; } catch { }
    }
    var history = await bridge.ReadHistory(account, Requested);
    Account = account; History = history.Concat(History).GroupBy(m => m.Id).Select(g => g.First()).OrderByDescending(m => m.Created).Take(500).ToList(); Updated = DateTime.Now;
    Status = History.Count + " matches · synced " + Updated.ToString("HH:mm");
    Directory.CreateDirectory(WebCache.Root); File.WriteAllText(CachePath(account), Json.Serializer().Serialize(History), Encoding.UTF8);
    var latestSolo = History.FirstOrDefault(match => match.Queue == 420 && !match.Remake);
    if (latestSolo != null && (latestRankedMatch != latestSolo.Id ||
        (DateTime.UtcNow - lastRankCheck).TotalMinutes >= 5)) {
     latestRankedMatch = latestSolo.Id; lastRankCheck = DateTime.UtcNow;
     try {
      var rank = await bridge.ReadSoloRank();
      var previous = RankHistory.FirstOrDefault();
      if (rank != null && (previous == null || previous.Tier != rank.Tier || previous.Division != rank.Division || previous.Lp != rank.Lp)) {
       RankHistory.Insert(0, rank); RankHistory = RankHistory.Take(150).ToList();
       File.WriteAllText(CachePath(account) + ".rank", Json.Serializer().Serialize(RankHistory), Encoding.UTF8);
      }
     } catch { }
    }
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
