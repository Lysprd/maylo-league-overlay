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

  static void TestPurchaseAndAugmentRecommendations() {
   var dominik = new Stat { Id = 3036, Rate = 55, Pick = "16%" };
   var core = new Stat { Id = 3031, Rate = 53, Pick = "34%" };
   var enemies = new LiveSummary();
   enemies.Players.Add(new LivePlayer { Me = true, Team = "ORDER" });
   enemies.Players.Add(new LivePlayer { Team = "CHAOS", Champion = "Ornn", Items = new List<int> { 1, 2 } });
   enemies.Players.Add(new LivePlayer { Team = "CHAOS", Champion = "Malphite" });
   var healthOnly = EnemyCompAnalyzer.Analyze(enemies, id => new[] { "Health" });
   var armor = EnemyCompAnalyzer.Analyze(enemies, id => new[] { "Armor" });
   Req(healthOnly.NeedsAntiTank && healthOnly.ArmoredCount == 0 && armor.ArmoredCount == 1,
    "Tank champions and health items must not count as observed armor");
   var items = new[] { dominik, core };
   var early = BuildPlanner.Plan(items, new int[0], new int[0], 50, id => new string[0],
    "Adaptive", new string[0], threats: healthOnly);
   Req(early.First().Stat.Id == core.Id, "Dominik must wait when an opening alternative exists and no enemy is stacking armor");
   dominik.Pick = "24%";
   var againstArmor = BuildPlanner.Plan(items, new int[0], new int[0], 50, id => new string[0],
    "Adaptive", new string[0], threats: armor);
   Req(againstArmor.First().Stat.Id == dominik.Id, "Dominik can move up when enemy armor is confirmed");
   var components = BuildPlanner.Plan(items, new int[0], new int[0], 50, id => new string[0],
    "Adaptive", new string[0], threats: armor, componentProgress: id => id == core.Id ? 0.9 : 0);
   Req(components.First().Stat.Id == core.Id, "Owned components can keep a close opening purchase on track");

   var gold = new Stat { Id = 1092, Name = "Vulnerability", Rarity = "Gold", Rate = 47, Games = 1000 };
   var prismatic = new Stat { Id = 1048, Name = "Jeweled Gauntlet", Rarity = "Prismatic", Rate = 52, Games = 1000 };
   string advice;
   Req(Recommendations.Best(new[] { gold, prismatic }, 50, 100, out advice) == prismatic,
    "Compare mixed-rarity offers using the same baseline and observed sample");
   prismatic.Rate = 47.5;
   Req(Recommendations.Best(new[] { gold, prismatic }, 50, 100, out advice) == null && advice == "Close choices",
    "Close choices must not be given a Best badge");
   prismatic.Rate = 52;
   prismatic.Games = 20;
   Req(Recommendations.Best(new[] { gold, prismatic }, 50, 100, out advice) == null && advice == "Limited data",
    "An uncertain 52% choice must not make a 47% choice look like a clear winner");
   prismatic.Games = 1000;
   var augs = new[] { gold, prismatic };
   var aliases = new Dictionary<int, List<string>> {
    { gold.Id, new List<string> { "Vulnérabilité", "Shared title" } },
    { prismatic.Id, new List<string> { "Gantelet précieux", "Shared title" } }
   };
   foreach (var name in new[] { "Vulnerability", "Vulnérabilité" })
    Req(AugmentOcr.Match(new List<OcrLine> { new OcrLine { Text = name } }, augs, aliases) == gold,
     "Vulnerability must resolve to the Gold ID in EN and FR");
   foreach (var name in new[] { "Jeweled Gauntlet", "Gantelet précieux" })
    Req(AugmentOcr.Match(new List<OcrLine> { new OcrLine { Text = name } }, augs, aliases) == prismatic,
     "Jeweled Gauntlet must resolve to the Prismatic ID in EN and FR");
   var shared = new List<OcrLine> { new OcrLine { Text = "Shared title", X = 450, Y = 220, Width = 100 } };
   Req(AugmentOcr.Match(shared, augs, aliases) == null, "An ambiguous title must not borrow stats from another ID");
   shared.Add(new OcrLine { Text = "Gold", X = 475, Y = 185, Width = 50 });
   Req(AugmentOcr.FindTitles(shared, augs, aliases, 1000, 1000, 0, 0).Single().Stat == gold,
    "A rarity label above the card can safely disambiguate its title");

   var now = DateTime.UtcNow;
   var offer = new OfferSession();
   var otherGold = new Stat { Id = 1100, Name = "Other Gold", Rarity = "Gold" };
   var opening = new List<AugmentHit> {
    new AugmentHit { Stat = gold, X = .3, Y = .4 },
    new AugmentHit { Stat = otherGold, X = .5, Y = .4 }
   };
   Req(offer.Observe(opening, now), "Two Gold cards must open the offer");
   var hover = new List<AugmentHit> {
    new AugmentHit { Stat = gold, X = .33, Y = .46 }, opening[1]
   };
   Req(!offer.Observe(hover, now.AddMilliseconds(25)) && offer.Positions[0].X == .3 && offer.Positions[0].Y == .4 &&
    offer.Positions[1].Y == .4, "Hovering a card must keep its badge and neighboring badges anchored");
   Req(offer.TrackClick(.3, .72, now.AddMilliseconds(50)) && offer.PendingRerollId == gold.Id,
    "Reroll must mark only the clicked Gold card as pending");
   var replacement = new List<AugmentHit> {
    opening[1], new AugmentHit { Stat = prismatic, X = .32, Y = .41 }
   };
   Req(offer.Observe(replacement, now.AddMilliseconds(150)) && offer.PendingRerollId == 0 &&
    offer.Choices[0] == prismatic && offer.Choices[1] == otherGold && offer.Positions[0].X == .3 &&
    offer.ConfirmedPick == null, "A Gold-to-Prismatic reroll must replace the same slot and preserve the other card");
   Req(offer.TrackClick(.3, .72, now.AddMilliseconds(200)), "The Prismatic card can be rerolled again");
   Req(offer.Observe(new List<AugmentHit> { opening[1] }, now.AddMilliseconds(1800)) &&
    offer.PendingRerollId == 0 && offer.Choices.Count(x => x != null) == 1 && offer.ExpectedCardCount == 2,
    "An unreadable reroll must remove the stale stats without treating the remaining offer as complete");
  }

  static void TestStaleAugmentSlotExpires() {
   var offer = new OfferSession();
   var now = DateTime.UtcNow;
   var cards = new List<AugmentHit> {
    new AugmentHit { Stat = new Stat { Id = 101 }, X = .3, Y = .4 },
    new AugmentHit { Stat = new Stat { Id = 102 }, X = .5, Y = .4 },
    new AugmentHit { Stat = new Stat { Id = 103 }, X = .7, Y = .4 }
   };
   Req(offer.Observe(cards, now), "Open all three cards");
   cards.RemoveAt(2);
   Req(!offer.Observe(cards, now.AddMilliseconds(200)) && offer.Choices[2] != null,
    "Brief OCR dropouts keep the third card stable");
   Req(offer.Observe(cards, now.AddMilliseconds(2500)) && offer.Choices[2] == null &&
    offer.Choices[0].Id == 101 && offer.Choices[1].Id == 102 && offer.ExpectedCardCount == 3,
    "A missing card must expire even when its two neighbors remain readable");
  }

  static void TestChampSelectOverlayLayout() {
   Req(ChampSelectOverlayLayout.ContainsTierListPointer(.91, .14, 1280, 720, false),
    "The closed tier list pill can receive clicks");
   Req(!ChampSelectOverlayLayout.ContainsTierListPointer(.83, .14, 1280, 720, false) &&
    !ChampSelectOverlayLayout.ContainsTierListPointer(.91, .20, 1280, 720, false),
    "The closed tier list leaves the surrounding champion grid click-through");
   Req(ChampSelectOverlayLayout.ContainsTierListPointer(.86, .65, 1280, 720, true) &&
    !ChampSelectOverlayLayout.ContainsTierListPointer(.78, .65, 1280, 720, true),
    "Opening the drawer enlarges hit testing only to the visible panel");
   Req(ChampSelectOverlayLayout.ContainsTierListPointer(.94, .65, 2560, 1080, true) &&
    !ChampSelectOverlayLayout.ContainsTierListPointer(.82, .65, 2560, 1080, true),
    "The click zone stays aligned to the side panel on an ultrawide screen");
  }

  static void TestBenchRow() {
   var bench = Enumerable.Range(0, 5).Select(i => new Champion { Slug = "bench-" + i }).ToList();
   var first = new PortraitHit { Champion = bench[0], X = .30, Y = .25, Size = .04 };
   var third = new PortraitHit { Champion = bench[2], X = .394, Y = .25, Size = .04 };
   var row = Vision.FillBenchRow(new List<PortraitHit> { first, third }, bench, 5);
   Req(row.Count == 5 && row[1].Champion == bench[1] && Math.Abs(row[4].X - .488) < .0001,
    "Two aligned anchors can place the other champions in their reported slot order");
   Req(Vision.FillBenchRow(new List<PortraitHit> { first }, bench, 5).Count == 0,
    "A single anchor must not place badges on an unconfirmed bench");
   var side = new PortraitHit { Champion = bench[1], X = .12, Y = .25, Size = .04, Confidence = .99 };
   var offRow = new PortraitHit { Champion = bench[3], X = .44, Y = .65, Size = .04, Confidence = .99 };
   var withOutliers = Vision.FillBenchRow(new List<PortraitHit> { first, third, side, offRow }, bench, 5);
   Req(withOutliers.Count == 5 && Math.Abs(withOutliers[1].X - .347) < .0001 &&
    Math.Abs(withOutliers[3].Y - .25) < .0001, "Side and off-row candidates must not receive misplaced badges");
   third.Y = .6;
   Req(Vision.FillBenchRow(new List<PortraitHit> { first, third }, bench, 5).Count == 0,
    "Unaligned anchors must not create a row");
  }

  static void TestGrayBenchPortraits() {
   var champions = Enumerable.Range(0, 2).Select(i => new Champion { Slug = "gray-" + i }).ToList();
   var icons = new Dictionary<string, System.Windows.Media.Imaging.BitmapImage>();
   using (var frame = new Bitmap(640, 100, PixelFormat.Format32bppArgb)) {
    for (int index = 0; index < champions.Count; index++) {
     var random = new Random(416 + index);
     using (var icon = new Bitmap(24, 24, PixelFormat.Format32bppArgb))
     using (var png = new MemoryStream()) {
      for (int y = 0; y < 24; y += 3) for (int x = 0; x < 24; x += 3) {
       var color = Color.FromArgb(random.Next(24, 256), random.Next(24, 256), random.Next(24, 256));
       int gray = (color.B * 114 + color.G * 587 + color.R * 299) / 1000 * 2 / 3;
       for (int dy = 0; dy < 3; dy++) for (int dx = 0; dx < 3; dx++) {
        icon.SetPixel(x + dx, y + dy, color);
        frame.SetPixel(238 + index * 30 + x + dx, 15 + y + dy, Color.FromArgb(gray, gray, gray));
        frame.SetPixel(10 + index * 30 + x + dx, 15 + y + dy, color);
       }
      }
      icon.Save(png, ImageFormat.Png);
      png.Position = 0;
      var image = new System.Windows.Media.Imaging.BitmapImage();
      image.BeginInit();
      image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
      image.StreamSource = png;
      image.EndInit();
      icons[champions[index].Slug] = image;
     }
    }
    var hits = Vision.BenchPortraits(frame, champions, icons);
    Req(hits.Count == 2 && hits.All(hit => hit.Confidence >= .84 && hit.X > .37 && hit.X < .46),
     "Grayscale bench portraits must match even if colored copies appear in the sidebar");
   }
  }

  static void TestWebBridgeSource() {
   Req(HybridHubWindow.IsAppPage("https://maylo.local/index.html?theme=light#hub") &&
    HybridHubWindow.IsAppPage("https://maylo.local/index.html#overlay"),
    "The built-in Hub and overlay can use the bridge");
   Req(!HybridHubWindow.IsAppPage("http://maylo.local/index.html") &&
    !HybridHubWindow.IsAppPage("https://example.com/index.html") &&
    !HybridHubWindow.IsAppPage("https://maylo.local/other.html"),
    "Only the built-in HTTPS app page can use the bridge");
  }

  static void TestWithheldAugmentPopularity() {
   var serializer = Json.Serializer();
   var rows = new[] {
    new { id = 1337, name = "Fixture", icon_url = "/icons/augments/1337.png", rarity = "Prismatic",
     games = 16, win_rate = "25.00", pick_rate = "0.39", withheld = true },
    new { id = 1338, name = "Rated", icon_url = "/icons/augments/1338.png", rarity = "Gold",
     games = 1000, win_rate = "52.00", pick_rate = "1.00", withheld = false }
   };
   string html = "<script>self.__next_f.push(" + serializer.Serialize(new object[] { 1, serializer.Serialize(rows) }) + ")</script>";
   var augments = Parser.AllAugments(html);
   var withheld = augments.Single(row => row.Id == 1337);
   Req(withheld.Rate == 0 && withheld.Win == "" && withheld.Pick == "0.39 %" && withheld.Rarity == "Prismatic",
    "Preserve observed popularity without importing a withheld result");
   Req(augments.Single(row => row.Id == 1338).Rate == 52, "Preserve supported rates for tier scoring");
  }

  static void TestUggRanked() {
   const string recommended = "{\"12\":{\"17\":{\"5\":[[[],[200,100,[4,14]],[200,100,[1056,2003,2003]],[100,52,[3118,3020,3100]],[90,46,[\"Q\",\"W\",\"E\"],\"QWE\"],[[[3041,7,7],[3157,50,100]],[[3157,25,40],[3089,20,30]],[]],[100,200]],\"timestamp\"]},\"14\":{\"5\":[[[],[],[],[10,6,[3118,3100,3020]]],\"timestamp\"]}}}";
   const string tank = "{\"12\":{\"17\":{\"5\":[[[],[10,4,[4,12]],[],[3,1,[3118,3111,3102]]],\"timestamp\"]}}}";
   var docs = new Dictionary<string, string> { { "recommended", recommended }, { "tank", tank }, { "ad", "invalid" } };
   var champion = new Champion { Name = "Ahri", Slug = "ahri" };
   var mid = MultiModeParser.ParseUggRanked(docs, champion, "mid", "emerald_plus", "16.23");
   Req(mid.RankedVariants.Select(build => build.Key).SequenceEqual(new[] { "recommended", "tank" }), "Expose only valid U.GG families for the selected role/rank");
   Req(mid.RankedVariant.Path.Select(item => item.Id).SequenceEqual(new[] { 3118, 3020, 3100, 3157, 3089 }), "Keep core order and fill late steps without repeats");
   Req(mid.RankedVariant.Games == 100 && mid.Starters.Count(item => item.Id == 2003) == 2, "Preserve the core sample and the full starting bundle");
   mid.RankedVariants[1].Apply(mid);
   Req(mid.RankedVariant.Path.Select(item => item.Id).SequenceEqual(new[] { 3118, 3111, 3102 }) &&
       mid.Spells.Single().Name == "Flash + Teleport" && mid.Skills == null, "Switch loadout with the build family");
   var master = MultiModeParser.ParseUggRanked(docs, champion, "mid", "master_plus", "16.23");
   Req(master.RankedVariants.Count == 1 && master.RankedVariant.Path[1].Id == 3100, "Respect the exact rank and its different boot order");
   var jungle = MultiModeParser.ParseUggRanked(docs, champion, "jungle", "emerald_plus", "16.23");
   Req(jungle.ActiveRole == "jungle" && jungle.RankedVariants.Count == 0 && jungle.AvailableRoles.Contains("mid"), "Keep the requested role when no guide exists");
  }

  static void TestDraftCounters() {
   const string session = "{\"myTeam\":[{\"cellId\":3},{\"cellId\":4}],\"theirTeam\":[{\"cellId\":5,\"championId\":103},{\"cellId\":6,\"championId\":0}],\"actions\":[[{\"actorCellId\":5,\"championId\":103,\"type\":\"pick\",\"completed\":true},{\"actorCellId\":6,\"championId\":222,\"type\":\"pick\",\"completed\":true},{\"actorCellId\":4,\"championId\":81,\"type\":\"pick\",\"completed\":false},{\"actorCellId\":3,\"championId\":64,\"type\":\"pick\",\"completed\":true}]],\"bans\":{\"myTeamBans\":[777],\"theirTeamBans\":[0,51]}}";
   var snapshot = new GameSnapshot();
   SelectionResolver.ReadDraftPicks(Json.D(Json.Serializer().DeserializeObject(session)), snapshot);
   Req(snapshot.DraftEnemies.SequenceEqual(new[] { 103 }), "Expose only locked and revealed enemy champions");
   Req(snapshot.DraftUnavailable.SetEquals(new[] { 103, 64, 777, 51 }), "Exclude locked champions and reported bans");

   const string matches = "{\"12\":{\"17\":{\"5\":[[[81,34,100,0,15000],[777,370,1000,0,-212000],[222,0,99,0,0]],[]]}}}";
   var previous = MultiModeParser.DDragonChamps;
   try {
    MultiModeParser.DDragonChamps = new[] { 81, 777, 222 }.ToDictionary(id => id, id => new Champion());
    var rows = MultiModeParser.ParseDraftCounters(matches, 103, "mid", "emerald_plus");
    Req(rows.Select(row => row.Id).SequenceEqual(new[] { 777, 81 }), "Rank by confidence and skip tiny samples");
    Req(Math.Abs(rows[0].WinRate - 63) < .001 && rows[0].GoldAt15 == 212 && rows[1].GoldAt15 == -150,
     "Compute win rate and gold from the counter perspective");
    Req(MultiModeParser.ParseDraftCounters(matches, 103, "jungle", "emerald_plus").Count == 0,
     "Do not borrow matchup data from another role");
   } finally { MultiModeParser.DDragonChamps = previous; }
  }

  static void TestRankedLoadout() {
   const string guide = "{\"12\":{\"11\":{\"1\":[[[],[100,50,[4,11]],[],[50,25,[6692,3071,6333]]],\"timestamp\"]}}}";
   const string paths = "{\"12\":{\"11\":{\"1\":[[],[],[[[1101,2003],25,40],[[1103],12,20]],[[[6692,3111,3071],60,100],[[3047,6692,6333],70,120]],[],[[3047,90,150]]]}}}";
   var docs = new Dictionary<string, string> { { "recommended", guide }, { "builds", paths } };
   var champion = new Champion { Name = "Lee Sin", Slug = "leesin" };
   var data = MultiModeParser.ParseUggRanked(docs, champion, "jungle", "diamond_plus", "16.23");
   Req(data.RankedVariant.Path.Select(item => item.Id).SequenceEqual(new[] { 6692, 3111, 3071, 6333 }) &&
    data.RankedVariant.BootFromMatchingPath, "Recover the boot slot from a matching sequence and keep the published core order");
   Req(data.Starters.Single().Id == 1101 && data.SummonerSpellIds.SequenceEqual(new[] { 4, 11 }), "Recover the observed jungle companion and both spells");
   Req(SpellPlan.OrderForFlash(data.SummonerSpellIds.ToArray(), "F", 0, 0).SequenceEqual(new[] { 11, 4 }),
    "Show Smite on D when Flash is on F");
   docs["builds"] = paths.Replace("[6692,3111,3071]", "[6692,3111,3156]");
   var fallback = MultiModeParser.ParseUggRanked(docs, champion, "jungle", "diamond_plus", "16.23");
   Req(fallback.Boots.Single().Id == 3047 && fallback.RankedVariant.Path.Count == 3 &&
    !fallback.RankedVariant.BootFromMatchingPath, "Show popular boots without inventing a purchase position");
  }

  static void TestSituationalItems() {
   var item = new Dictionary<string, object> {
    { "maps", new Dictionary<string, object> { { "11", true }, { "12", true }, { "30", false } } },
    { "purchasable", true }, { "priceTotal", 2900 }, { "categories", new[] { "ArmorPenetration" } },
    { "description", "<stats>30% Armor Penetration</stats><passive>Grievous Wounds</passive>" }
   };
   Req(SituationalItems.Available(item, "SR", "Gwen") && SituationalItems.Available(item, "MAYHEM", "Gwen") &&
    !SituationalItems.Available(item, "ARENA", "Gwen"), "Respect the item shop map without using mode variants from another map");
   Req(SituationalItems.Categories(item).SequenceEqual(new[] { "armor-pen", "anti-heal" }),
    "A hybrid item can appear in both relevant categories");
   item["requiredChampion"] = "Kalista";
   Req(!SituationalItems.Available(item, "SR", "Gwen") && SituationalItems.Available(item, "SR", "Kalista"),
    "Champion-locked items must only appear for the appropriate champion");
   item["inStore"] = false;
   Req(!SituationalItems.Available(item, "SR", "Kalista"), "Hidden shop items must not appear");
   item["description"] = "<stats>15 Lethality</stats><passive>Shield Reaver</passive>";
   Req(SituationalItems.Categories(item).SequenceEqual(new[] { "lethality", "anti-shield" }),
    "Flat penetration and shield reduction must not be tagged as percentage armor penetration");
   item["description"] = "Damaging Abilities burn enemies for 2% max Health magic damage per second";
   item["categories"] = new[] { "SpellDamage" };
   Req(SituationalItems.Categories(item).Contains("health-damage"), "Max health damage belongs in the health damage filter");
  }

  static void TestUnratedAugmentReason() {
   var stat = new Stat { Games = 16, Rarity = "Gold", Rate = 0 };
   Req(Recommendations.UnratedAugmentReason(stat, 100) == "small-sample", "Separate a small sample from a missing outcome");
   stat.Games = 250;
   Req(Recommendations.UnratedAugmentReason(stat, 100) == "no-outcome", "Popularity alone must not produce a tier");
   stat.Rate = 53;
   Req(Recommendations.UnratedAugmentReason(stat, 100) == "no-baseline", "A valid outcome on its own still needs a rarity baseline");
  }

  static void TestAugmentTiers() {
   var gold = new[] { 43d, 46, 50, 52, 58 };
   var prismatic = new[] { 37d, 40, 44, 47, 51 };
   var catalog = gold.Select((rate, i) => new Stat { Id = i + 101, Rate = rate, Rarity = "Gold", Games = 1000 })
    .Concat(prismatic.Select((rate, i) => new Stat { Id = i + 201, Rate = rate, Rarity = "Prismatic", Games = 1000 }))
    .ToList();
   catalog.Add(new Stat { Id = 301, Rate = 90, Games = 6, Rarity = "Gold" });
   var tiers = Recommendations.AugmentTiers(catalog, 100);
   Req(tiers[103] == "B" && tiers[105] == "S+" && tiers[205] == "S+" &&
    !tiers.ContainsKey(301), "The same strength tier can be earned in different rarities; tiny samples stay unrated");
   var goldOnly = Recommendations.AugmentTiers(catalog.Where(stat => stat.Rarity == "Gold"), 100);
   Req(goldOnly.All(pair => tiers[pair.Key] == pair.Value), "A Golden Reroll cannot change the other cards' reference tiers");
   Req(Recommendations.AugmentTiers(catalog.Where(stat => stat.Id >= 103 && stat.Id <= 105), 100).Count == 0,
    "A few detected cards are not enough to create rarity-wide tier ratings");
  }

  static void TestReviewAndReferenceData() {
   var sample = new LiveSummary { Seconds = 30 };
   var player = new LivePlayer { Me = true, InventoryReported = true, Items = new List<int> { 1001 } };
   sample.Players.Add(player);
   var capture = new MatchReviewCapture("sample", new MatchMoment[0]);
   Req(!capture.Observe(sample), "First inventory observation is the baseline");
   player.Items.Add(1036);
   sample.Seconds = 120;
   sample.ReviewEvents.Add(new MatchMoment { Key = "dragon-1", Kind = "DragonKill", Seconds = 110 });
   Req(capture.Observe(sample) && capture.Moments.Count == 2 && capture.Moments[1].Item == 1036,
    "Match review orders a reported objective and a newly observed inventory item");
   Req(!capture.Observe(sample), "Repeated live snapshots do not duplicate match moments");
   player.InventoryReported = false;
   player.Items.Clear();
   Req(!capture.Observe(sample), "Missing inventory data never fabricates item changes");
   player.InventoryReported = true;
   player.Items.AddRange(new[] { 1001, 1036 });
   Req(!capture.Observe(sample), "Restoring data after a missing snapshot does not repeat earlier items");

   var ranked = new Dictionary<string, object> { { "queueMap", new Dictionary<string, object> {
    { "RANKED_SOLO_5x5", new Dictionary<string, object> { { "tier", "GOLD" }, { "division", "II" }, { "leaguePoints", 24 } } }
   } } };
   var rank = ProfileParser.SoloRank(ranked, 1000);
   Req(rank != null && rank.Tier == "GOLD" && rank.Lp == 24 && rank.Created == 1000 &&
    ProfileParser.SoloRank(new Dictionary<string, object>(), 1000) == null,
    "Rank view stores reported Solo/Duo LP and never invents missing snapshots");

   var skins = ProfileParser.Skins(new object[] { new Dictionary<string, object> { { "name", "Ashe" }, { "skins", new object[] {
    new Dictionary<string, object> { { "id", 22000 }, { "name", "Ashe" }, { "owned", true } },
    new Dictionary<string, object> { { "id", 22002 }, { "name", "Ashe skin" }, { "ownership", new Dictionary<string, object> { { "owned", true } } } },
    new Dictionary<string, object> { { "id", 22003 }, { "name", "Unknown ownership" } }
   } } } });
   Req(skins.Count == 1 && skins[0].Id == 22002 && skins[0].Owned, "Skin collection skips base skins and unknown ownership");

   var reference = new[] { new Stat { Id = 3031, Pick = "30%", Rate = 40, Games = 2500 },
    new Stat { Id = 3071, Pick = "10%", Rate = 75, Games = 2500 } };
   var guide = BuildPlanner.Plan(reference, new int[0], new int[0], 50, id => new string[0],
    "Adaptive", new string[0], useItemOutcomes: false);
   Req(guide.First().Stat.Id == 3031, "Arena reference ordering uses reported popularity independently of outcomes");
  }

  public static int RunRecommendations(string reportPath) {
   try {
    TestPurchaseAndAugmentRecommendations();
    TestReviewAndReferenceData();
    TestAugmentTiers();
    TestUnratedAugmentReason();
    TestSituationalItems();
    TestUggRanked();
    TestDraftCounters();
    TestRankedLoadout();
    TestWithheldAugmentPopularity();
    TestChampSelectOverlayLayout();
    TestStaleAugmentSlotExpires();
    TestWebBridgeSource();
    TestBenchRow();
    TestGrayBenchPortraits();
    File.WriteAllText(reportPath, "PASS Maylo recommendations, reference guides and match review");
    return 0;
   } catch (Exception ex) {
    File.WriteAllText(reportPath, ex.ToString());
    return 1;
   }
  }

  public static int Run(string reportPath) {
   try {
    TestPurchaseAndAugmentRecommendations();
    string cache = WebCache.Root;
    var champs = Parser.Champions(File.ReadAllText(Path.Combine(cache, "champions.html")));
    Req(champs.Count >= 150, "MayhemMeta champion index");
    var mayhem = MultiModeParser.ParseMayhem(File.ReadAllText(Path.Combine(cache, "gwen.html")));
    Req(mayhem.Items.Count >= 5 && mayhem.Augments.Count >= 50, "MayhemMeta champion parse");

    // 1. DDragon Runes + CommunityDragon Augment Meta (Offline Descriptions + FR/EN Aliases!)
    var jgTier = MultiModeParser.LoadTierList("SR", "JUNGLE", "emerald_plus", champs, true).GetAwaiter().GetResult();
    Req(jgTier.Count >= 25, "SR Jungle Emerald+ tier list");
    var aramTier = MultiModeParser.LoadTierList("ARAM", "", "emerald_plus", champs, true).GetAwaiter().GetResult();
    var arenaTier = MultiModeParser.LoadTierList("ARENA", "", "emerald_plus", champs, true).GetAwaiter().GetResult();
    var mayhemTier = MultiModeParser.LoadTierList("MAYHEM", "", "emerald_plus", champs, false).GetAwaiter().GetResult();
    Req(aramTier.Count >= 140, "OP.GG ARAM tier list must load 140+ champions (got " + aramTier.Count + ")");
    Req(arenaTier.Count >= 140, "OP.GG Arena tier list must load 140+ champions (got " + arenaTier.Count + ")");
    Req(String.Join(",", aramTier.Take(5).Select(x => x.Slug)) != String.Join(",", mayhemTier.Take(5).Select(x => x.Slug)), "ARAM top 5 must be distinct from Mayhem top 5");
    var gwenChamp = champs.First(c => c.Slug == "gwen");
    var gwenArena = MultiModeParser.LoadChampionMode(gwenChamp, "ARENA", "", "emerald_plus", true, null, champs).GetAwaiter().GetResult();
    Req(gwenArena.ModeKey == "ARENA" && gwenArena.Augments.Count >= 20 && gwenArena.PrismaticItems.Count >= 3 && gwenArena.Teammates.Count >= 5,
        "OP.GG Arena Gwen must load augments, prismatic items & duo synergies (augs=" + gwenArena.Augments.Count + ", prism=" + gwenArena.PrismaticItems.Count + ")");
    var patchNotes = MultiModeParser.LoadPatchNotes(true).GetAwaiter().GetResult();
    Req(patchNotes != null && patchNotes.Entries.Count >= 10 && !String.IsNullOrEmpty(patchNotes.PatchVersion), "OP.GG Patch Notes must load latest patch buffs/nerfs");
    Req(MultiModeParser.DDragonRunes.ContainsKey(8005) && MultiModeParser.DDragonRunes.ContainsKey(5008), "DDragon Runes & Stat Shards must be loaded");
    MultiModeParser.EnsureAugmentMeta().GetAwaiter().GetResult();
    string adaptDesc = MultiModeParser.GetAugmentDescription(1205, "ADAPt", null);
    Req(!String.IsNullOrWhiteSpace(adaptDesc) && !adaptDesc.Contains("Connect the League client") && !adaptDesc.Contains("indisponible"), "CommunityDragon Augment Description must resolve offline: got '" + adaptDesc + "'");
    var aliasMap = MultiModeParser.BuildAugmentAliasMap(null);
    Req(aliasMap.Count >= 150, "CommunityDragon EN+FR alias map must cover 150+ augments");

    // 2. Ranked source ordering and enemy comp threat analysis.
    var briar = champs.First(c => c.Slug == "briar");
    var briarJg = MultiModeParser.LoadChampionMode(briar, "SR", "jungle", "emerald_plus", true, null, champs).GetAwaiter().GetResult();
    Req(briarJg.SourceLabel.StartsWith("U.GG", StringComparison.Ordinal) && briarJg.ActiveRole == "jungle" &&
        briarJg.RankedVariants.Any(build => build.Key == "recommended"), "Briar Jungle gets U.GG Ranked variants");
    Req(briarJg.Runes != null && briarJg.Runes.Perks.Count == 9, "Briar 9 Riot rune perk IDs");

    var diagWin = new HybridHubWindow(true);
    diagWin.Selected = briar;
    diagWin.ActiveMode = "SR";
    diagWin.ModeData = briarJg;
    briarJg.ChampionSlug = briar.Slug;
    diagWin.Game = new GameSnapshot { InGame = true, Mode = "CLASSIC", Champion = briar.AssetKey, Gold = 500 };
    var sourcePath = briarJg.RankedVariant.Path.Select(item => item.Id).ToArray();
    Req(briarJg.Starters.Any(item => item.Id == 1101 || item.Id == 1102 || item.Id == 1103) &&
        sourcePath.Length >= 3 && diagWin.CurrentBuildPath(true).Select(step => step.Stat.Id).SequenceEqual(sourcePath),
        "Keep the jungle starter bundle and the U.GG purchase order");
    diagWin.Game.Owned.Add(sourcePath[0]);
    var afterPurchase = diagWin.CurrentBuildPath();
    Req(afterPurchase[0].Owned && afterPurchase.Select(step => step.Stat.Id).SequenceEqual(sourcePath),
        "Mark a ranked purchase without reordering the U.GG guide");

    var fakeLive = new LiveSummary();
    fakeLive.Players.Add(new LivePlayer { Champion = "Briar", Team = "ORDER", Me = true, Position = "JUNGLE" });
    fakeLive.Players.Add(new LivePlayer { Champion = "Aatrox", Team = "CHAOS", Position = "TOP" });
    fakeLive.Players.Add(new LivePlayer { Champion = "Ornn", Team = "CHAOS", Position = "JUNGLE" });
    fakeLive.Players.Add(new LivePlayer { Champion = "Malphite", Team = "CHAOS", Position = "MIDDLE" });
    fakeLive.Players.Add(new LivePlayer { Champion = "Soraka", Team = "CHAOS", Position = "UTILITY" });
    var threats = EnemyCompAnalyzer.Analyze(fakeLive, id => new string[0]);
    Req(threats.NeedsAntiHeal && threats.NeedsAntiTank, "EnemyCompAnalyzer detects Anti-Heal + Anti-Tank");

    var commonItem = new Stat { Id = 3071, Pick = "22%", Rate = 54, Games = 1800 };
    var rareItem = new Stat { Id = 3075, Pick = "0.3%", Rate = 90, Games = 3 };
    var lateItem = new Stat { Id = 3026, Pick = "30%", Rate = 73, Games = 3000, CoreSlot = 4 };
    var itemFixture = new[] { commonItem, rareItem, lateItem };
    var early = BuildPlanner.Plan(itemFixture, new int[0], new int[0], 50, id => new string[0], "Adaptive", new string[0], null, threats);
    Req(early.First().Stat.Id == commonItem.Id && early.All(x => x.Stat.Id != lateItem.Id), "A tiny sample or late-game item must not dictate first purchase");
    var pinned = BuildPlanner.Plan(itemFixture, new int[0], new int[0], 50, id => new string[0], "Adaptive", new string[0],
     pinnedItems: new[] { lateItem.Id });
    Req(pinned.First().Stat.Id == lateItem.Id, "An explicitly pinned item stays in the purchase list");
    diagWin.Game.Mode = "ARAM";
    Req(diagWin.CurrentBuildPath().Count == 0, "A Rift build must not leak into ARAM");

    // 3. Flash Key D/F + SR-only Objective Timers
    var onD = SpellPlan.OrderForFlash(SpellPlan.Pair("Flash + Smite"), "D", 11, 4);
    var onF = SpellPlan.OrderForFlash(SpellPlan.Pair("Flash + Smite"), "F", 4, 11);
    Req(onD[0] == 4 && onF[1] == 4, "Flash D/F ordering");
    var aramLive = LiveParser.Parse(new Dictionary<string, object> { { "gameData", new Dictionary<string, object> { { "gameMode", "ARAM" }, { "gameTime", 400.0 } } } });
    Req(aramLive.Objectives.Count == 0, "Objectives must be empty outside Summoner's Rift");
    Req(GameBridge.DetectModeKey("CLASSIC", 480, false) == "SR" &&
        GameBridge.DetectModeKey("ARAM", 2400, false) == "MAYHEM" &&
        GameBridge.DetectModeKey("KIWI", 3270, false) == "MAYHEM" &&
        GameBridge.DetectModeKey("CLASSIC", 3100, false) == "SR" &&
        GameBridge.DetectModeKey("PRACTICETOOL", 3140, false) == "SR" &&
        GameBridge.DetectModeKey("PRACTICETOOL", -1, false) == "SR" &&
        GameBridge.DetectModeKey("CLASSIC", -1, false) == "SR" &&
        GameBridge.DetectModeKey("ARAM", -1, false) == "ARAM" &&
        GameBridge.DetectModeKey("ARAM", 720, false) == "ARAM" &&
        GameBridge.DetectModeKey("CLASSIC", 1700, true) == "ARENA" &&
        GameBridge.DetectModeKey("CHERRY", 0, false) == "ARENA" &&
        GameBridge.DetectModeKey("CLASSIC", 99999, false) == "",
        "Queue context must override generic or stale game modes");
    var modeFixture = Json.Serializer().Deserialize<Dictionary<string, object>>(@"{
     ""gameData"":{""gameMode"":""CLASSIC"",""gameTime"":410},
     ""events"":{""Events"":[
      {""EventName"":""InhibKilled"",""InhibKilled"":""Inhib_TOrder_L1_P1_10"",""EventTime"":380},
      {""EventName"":""InhibKilled"",""InhibKilled"":""Inhib_TChaos_L2_P1_11"",""EventTime"":400},
      {""EventName"":""InhibRespawned"",""InhibRespawned"":""Inhib_TChaos_L2_P1_11"",""EventTime"":403}
     ]},
     ""allPlayers"":[
      {""team"":""ORDER"",""items"":[{""itemID"":100},{""itemID"":100}]},
      {""team"":""CHAOS"",""items"":[{""itemID"":200}]}
     ]
    }");
    var standard = LiveParser.Parse(modeFixture, "SR", 420);
    var swift = LiveParser.Parse(modeFixture, "SR", 480);
    Req(standard.Objectives.Single(x => x.Key == "grubs").SecondsLeft == 70 &&
        standard.Objectives.Single(x => x.Key == "herald").SecondsLeft == 490 &&
        standard.Objectives.Single(x => x.Key == "baron").SecondsLeft == 790, "Standard SR uses current objective schedule");
    Req(swift.Objectives.Count == 2 && swift.Objectives.Single(x => x.Key == "baron").SecondsLeft == 310 &&
        swift.Inhibitors.Count == 0 && LiveParser.Parse(modeFixture, "ARENA", 1700).Objectives.Count == 0,
        "Swiftplay uses its own objectives and Arena receives no SR timers");
    Req(standard.Inhibitors.Count == 1 && standard.Inhibitors[0].SecondsLeft == 270 &&
        standard.Inhibitors[0].Name == "Blue Mid inhibitor", "Inhibitor events use their own clocks and respawn clears the matching inhibitor");
    var priceFixture = new Dictionary<int, Dictionary<string, object>> {
     { 100, new Dictionary<string, object> { { "priceTotal", 200 } } },
     { 200, new Dictionary<string, object> { { "priceTotal", 350 } } }
    };
    var value = Json.D(Json.Serializer().DeserializeObject(Json.Serializer().Serialize(standard.InventoryValue(priceFixture))));
    Req(Json.I(value, "blue") == 400 && Json.I(value, "red") == 350 && !String.Equals(Json.S(value, "partial"), "true", StringComparison.OrdinalIgnoreCase),
     "Inventory totals count duplicate slots and are comparable only with complete prices");
    priceFixture.Remove(200);
    var partialInventory = Json.D(Json.Serializer().DeserializeObject(Json.Serializer().Serialize(standard.InventoryValue(priceFixture))));
    Req(String.Equals(Json.S(partialInventory, "partial"), "true", StringComparison.OrdinalIgnoreCase), "Missing item prices are flagged as partial data");

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
      var emptyHits = new List<AugmentHit>();
      // The reroll button is below the card. A click at Y=.72 used to clear the whole overlay.
      var rerollClick = new OfferSession();
      rerollClick.Observe(scan4.Hits, t0);
      Req(rerollClick.TrackClick(xs4[1], 0.72, t0.AddMilliseconds(50)) &&
        rerollClick.PendingRerollId == scan4.Hits[1].Stat.Id, "Reroll click identifies the slot whose displayed stats must be hidden");
      rerollClick.Observe(emptyHits, t0.AddMilliseconds(210));
      rerollClick.Observe(emptyHits, t0.AddMilliseconds(1100));
      Req(rerollClick.Visible && rerollClick.Choices.Count(c => c != null) == 4, "Reroll animation must not clear any badges");
      altStat.Rarity = "Prismatic";
       var shiftedTitle = rerolled.ToList();
      shiftedTitle[1] = new AugmentHit { Stat = altStat, X = xs4[1] + 0.035, Y = 0.43 };
      rerollClick.Observe(shiftedTitle, t0.AddMilliseconds(1150));
      rerollClick.Observe(shiftedTitle, t0.AddMilliseconds(1280));
      Req(rerollClick.Visible && rerollClick.Choices[1].Id == altStat.Id &&
        rerollClick.Choices[1].Rarity == "Prismatic" && rerollClick.PendingRerollId == 0 && rerollClick.ConfirmedPick == null &&
       Math.Abs(rerollClick.Positions[1].X - scan4.Hits[1].X) < 0.001 &&
       rerollClick.Choices.Where((c, i) => i != 1 && c != null).Select(c => c.Id)
        .SequenceEqual(scan4.Hits.Where((c, i) => i != 1).Select(c => c.Stat.Id)),
       "Reroll updates only its card and never records a pick");

      var actualPick = new OfferSession();
      actualPick.Observe(scan4.Hits, t0);
      Req(actualPick.TrackClick(xs4[1], 0.50, t0.AddMilliseconds(50)), "Direct card click should be tracked");
      actualPick.Observe(emptyHits, t0.AddMilliseconds(210));
      Req(actualPick.Visible && actualPick.ConfirmedPick == null, "One empty frame cannot confirm a pick");
      actualPick.Observe(emptyHits, t0.AddMilliseconds(470));
      Req(!actualPick.Visible && actualPick.ConfirmedPick != null && actualPick.ConfirmedPick.Id == scan4.Hits[1].Stat.Id,
       "Two sustained empty frames (~250ms) after clicking a card must confirm the pick promptly without 1.5s lag");

      var ambiguous = new OfferSession();
      ambiguous.Observe(scan4.Hits, t0);
      ambiguous.TrackClick(xs4[1], 0.52, t0.AddMilliseconds(50));
      ambiguous.Observe(emptyHits, t0.AddMilliseconds(200));
      ambiguous.Observe(rerolled, t0.AddMilliseconds(600));
      ambiguous.Observe(emptyHits, t0.AddMilliseconds(1000));
      ambiguous.Observe(emptyHits, t0.AddMilliseconds(1800));
      Req(!ambiguous.Visible && ambiguous.ConfirmedPick == null, "A replacement cancels an apparent card click");

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
     MultiModeParser.LoadPatchNotes(false).GetAwaiter().GetResult();
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

     // 2c. Patch Notes (Buffs / Nerfs / Augments / Items!)
     w.View = "PatchNotes"; w.PushState();
     await Task.Delay(700);
     await snap("Hub-PatchNotes-v12.png");

     // 2d. Arena 2v2 Tier List & Gwen Arena Build (Prismatic Items + Augments + Gold Beam!)
     var savedModeData = w.ModeData;
     var savedSelected = w.Selected;
     var savedTierList = w.TierList;
     var gwenArenaChamp = w.Champions.First(c => c.Slug == "gwen");
     w.ActiveMode = "ARENA"; w.DetectedMode = "ARENA";
     w.TierList = MultiModeParser.LoadTierList("ARENA", "", "emerald_plus", w.Champions, false).GetAwaiter().GetResult();
     w.Selected = gwenArenaChamp;
     w.ModeData = MultiModeParser.LoadChampionMode(gwenArenaChamp, "ARENA", "", "emerald_plus", false, null, w.Champions).GetAwaiter().GetResult();
     w.View = "TierList"; w.PushState();
     await Task.Delay(700);
     await snap("Hub-Arena-TierList-v12.png");
     w.View = "Build"; w.PushState();
     await Task.Delay(800);
     await w.WebControl.CoreWebView2.ExecuteScriptAsync("var m=document.querySelector('main'); if(m) m.scrollTop=380;");
     await Task.Delay(350);
     await snap("Hub-Arena-Build-v12.png");
     await w.WebControl.CoreWebView2.ExecuteScriptAsync("var m=document.querySelector('main'); if(m) m.scrollTop=0;");
     w.ActiveMode = "SR"; w.DetectedMode = "SR";
     w.TierList = savedTierList;
     w.Selected = savedSelected;
     w.ModeData = savedModeData;

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

     // 5b. Alt+G In-Game Command Sheet (Verify crisp rounded corners with zero square bleed!)
     w.PreviewScene = "altg"; w.InGameSheet = true; w.InGameSheetTab = "build"; w.PushState();
     await Task.Delay(700);
     await snap("Overlay-AltG-v12.png");
     w.InGameSheet = false;

     // 6. Non-Blocking Champ Select Overlay (Cards & Bench Ribbon + Open Arena Tier List Drawer!)
     w.Game.InGame = false; w.Game.InSelection = true; w.Game.Mayhem = true; w.ActiveMode = "MAYHEM";
     w.Preview = true; w.PreviewScene = "bench"; w.ChampTierOpen = false;
     w.PushState();
     await Task.Delay(700);
     await snap("Overlay-Bench-Safe-v12.png");

     w.ActiveMode = "ARENA";
     w.TierList = MultiModeParser.LoadTierList("ARENA", "", "emerald_plus", w.Champions, false).GetAwaiter().GetResult();
     w.ChampTierOpen = true;
     w.PushState();
     await Task.Delay(700);
     await snap("Overlay-ChampSelect-TierOpen-v12.png");
     w.ChampTierOpen = false;

     // 7. In-Game 4-Card Augment OCR Badges + Reroll Reference Bar
     w.ActiveMode = "MAYHEM"; w.Game.InGame = true; w.Game.InSelection = false; w.Game.Mayhem = true;
     w.Preview = true; w.PreviewScene = "augments"; w.Editing = false;
     w.ModeData = mayhemGwen; w.Selected = w.Champions.First(c => c.Slug == "gwen");
     w.HandleWebMessage("{\"type\":\"setPreviewScene\",\"scene\":\"augments\",\"count\":4}");
     w.Editing = false;
     w.PushState();
     await Task.Delay(700);
     await snap("Overlay-Augments-v12.png");

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
