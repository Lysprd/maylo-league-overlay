using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Windows.Media.Ocr;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace MayhemCompanion {
 public class OcrLine { public string Text; public double X, Y, Width, Height; }
 public class AugmentHit { public Stat Stat; public double X, Y; }
 public class ChampionCardHit { public Champion Champion; public double X, Y; }

 public static class AugmentOcr {
  const double TitleBandTop = 0.27;
  const double TitleBandBottom = 0.57;
  const double BandTargetHeight = 360.0;
  const double RowTolerance = 0.065;
  internal const double MinCardSpacing = 0.07;

  static OcrEngine userEngine, enEngine;
  static OcrEngine GetEngine(bool preferUser) {
   if (preferUser) {
    if (userEngine == null) userEngine = OcrEngine.TryCreateFromUserProfileLanguages() ?? OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"));
    if (userEngine != null) return userEngine;
   }
   if (enEngine == null) enEngine = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US")) ?? OcrEngine.TryCreateFromUserProfileLanguages();
   if (enEngine == null) throw new Exception("Windows OCR unavailable. Install an OCR language pack in Windows.");
   return enEngine;
  }

  static async Task<T> Complete<T>(Windows.Foundation.IAsyncOperation<T> op) {
   var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
   op.Completed = (info, status) => {
    try {
     if (status == Windows.Foundation.AsyncStatus.Completed) tcs.TrySetResult(info.GetResults());
     else if (status == Windows.Foundation.AsyncStatus.Canceled) tcs.TrySetCanceled();
     else tcs.TrySetException(info.ErrorCode ?? new Exception("OCR failed"));
    } finally { try { info.Close(); } catch { } }
   };
   if (await Task.WhenAny(tcs.Task, Task.Delay(3500)) != tcs.Task) { try { op.Cancel(); } catch { } throw new TimeoutException("OCR timed out"); }
   return await tcs.Task;
  }

  public static string ShapeFold(string s) {
   if (String.IsNullOrEmpty(s)) return "";
   s = s.Replace("€", "c").Replace("¢", "c").Replace("$", "s").Replace("#", "s");
   string norm = GameBridge.Norm(s);
   var sb = new System.Text.StringBuilder(norm.Length);
   foreach (char ch in norm) {
    switch (ch) {
     case 'l': case '1': case 'j': sb.Append('i'); break;
     case 'm': sb.Append('n'); break;
     case '0': sb.Append('o'); break;
     case '5': sb.Append('s'); break;
     case 'q': sb.Append('c'); break;
     default: sb.Append(ch); break;
    }
   }
   return sb.ToString();
  }

  static int EditDistance(string a, string b, int max) {
   int n = a.Length, m = b.Length;
   if (Math.Abs(n - m) > max) return max + 1;
   if (n == 0) return m; if (m == 0) return n;
   var prev2 = new int[m + 1]; var prev = new int[m + 1]; var curr = new int[m + 1];
   for (int j = 0; j <= m; j++) prev[j] = j;
   for (int i = 1; i <= n; i++) {
    curr[0] = i; int rowMin = curr[0];
    for (int j = 1; j <= m; j++) {
     int cost = a[i - 1] == b[j - 1] ? 0 : 1;
     int v = Math.Min(Math.Min(prev[j] + 1, curr[j - 1] + 1), prev[j - 1] + cost);
     if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1]) v = Math.Min(v, prev2[j - 2] + 1);
     curr[j] = v; if (v < rowMin) rowMin = v;
    }
    if (rowMin > max) return max + 1;
    var tmp = prev2; prev2 = prev; prev = curr; curr = tmp;
   }
   return prev[m];
  }

  static IEnumerable<string> FoldedNames(Stat s, Dictionary<int, List<string>> aliases) {
   var seen = new HashSet<string>();
   string n = ShapeFold(s.Name); if (n.Length > 0 && seen.Add(n)) yield return n;
   List<string> list;
   if (aliases != null && aliases.TryGetValue(s.Id, out list) && list != null) {
    foreach (string raw in list) {
     string a = ShapeFold(raw);
     if (a.Length > 0 && seen.Add(a)) yield return a;
    }
   }
  }

  public static Stat Match(List<OcrLine> lines, IEnumerable<Stat> stats, Dictionary<int, List<string>> aliases = null) {
   var candidates = new HashSet<string>();
   for (int i = 0; i < lines.Count; i++) {
    string one = ShapeFold(lines[i].Text); if (one.Length >= 3) candidates.Add(one);
    if (i + 1 < lines.Count) {
     string two = ShapeFold(lines[i].Text + lines[i + 1].Text); if (two.Length >= 4) candidates.Add(two);
     if (i + 2 < lines.Count) { string three = ShapeFold(lines[i].Text + lines[i + 1].Text + lines[i + 2].Text); if (three.Length >= 6) candidates.Add(three); }
    }
   }
   if (candidates.Count == 0) return null;
   var all = stats.ToList();
   // 1. Exact shape-folded match (prefer exact Name match or highest-Games entry if aliases overlap)
   var exact = all.Where(s => FoldedNames(s, aliases).Any(candidates.Contains)).GroupBy(s => s.Id).Select(g => g.First()).ToList();
   if (exact.Count == 1) return exact[0];
   if (exact.Count > 1) {
    var primary = exact.Where(s => candidates.Contains(ShapeFold(s.Name))).ToList();
    if (primary.Count == 1) return primary[0];
    return exact.OrderByDescending(s => s.Games).First();
   }
   // 2. Clean containment (border noise chars like "| Dual Wield")
   var contained = all.Where(s => FoldedNames(s, aliases).Any(name => name.Length >= 4 && candidates.Any(c => c.Length > name.Length && c.Length <= name.Length + 3 && c.Contains(name)))).GroupBy(s => s.Id).Select(g => g.First()).ToList();
   if (contained.Count == 1) return contained[0];
   // 3. Length-scaled Damerau-Levenshtein requiring strictly unique best match
   Stat bestStat = null; int bestDist = 99, secondDist = 99;
   foreach (var s in all) {
    int d = 99;
    foreach (string name in FoldedNames(s, aliases)) {
     if (name.Length < 4) continue;
     int limit = name.Length <= 6 ? 1 : name.Length <= 13 ? 2 : 3;
     foreach (string cand in candidates) {
      if (cand.Length < 4 || Math.Abs(cand.Length - name.Length) > limit) continue;
      int dist = EditDistance(name, cand, limit);
      if (dist <= limit && dist < d) d = dist;
     }
    }
    if (d < 99) {
     if (bestStat != null && bestStat.Id == s.Id) { if (d < bestDist) bestDist = d; continue; }
     if (d < bestDist) { secondDist = bestDist; bestDist = d; bestStat = s; }
     else if (d < secondDist) secondDist = d;
    }
   }
   return bestStat != null && bestDist < secondDist ? bestStat : null;
  }

  static readonly string[] UiMenuStopWords = {
   "exitgame", "leavegame", "quitterlapartie", "quitterlejeu", "resumegame", "reprendrelapartie",
   "hotkeys", "raccourcis", "surrender", "reddition", "victory", "defeat", "victoire",
   "defaite", "maylo", "recommendedbuild", "augmentocr", "playagain", "rejouer"
  };

  public static List<AugmentHit> FindTitles(List<OcrLine> lines, IEnumerable<Stat> stats, Dictionary<int, List<string>> aliases, int gameW, int gameH, double offsetX, double offsetY) {
   // Reject Pause / Exit Game / Victory / Defeat / MAYLO UI screens immediately so augments NEVER pop on exit!
   foreach (var l in lines) {
    string normLine = GameBridge.Norm(l.Text);
    if (normLine.Length >= 5 && UiMenuStopWords.Any(w => normLine.Contains(w))) return new List<AugmentHit>();
   }
   var hits = new List<AugmentHit>();
   var statsList = stats.ToList();
   for (int i = 0; i < lines.Count; i++) {
    var line = lines[i];
    double left = line.X, right = line.X + line.Width, cx = line.X + line.Width * 0.5;
    var group = new List<OcrLine> { line };
    Stat stat = Match(group, statsList, aliases);
    if (stat == null) {
     var nearby = lines.Where((other, idx) => idx != i && other.Y > line.Y && other.Y - line.Y < Math.Max(95, gameH * .09) && Math.Abs((other.X + other.Width * 0.5) - cx) < gameW * .085).OrderBy(o => o.Y).Take(2).ToList();
     foreach (var next in nearby) {
      group.Add(next); left = Math.Min(left, next.X); right = Math.Max(right, next.X + next.Width);
      stat = Match(group, statsList, aliases);
      if (stat != null) break;
     }
    }
    if (stat == null) continue;
    double x = offsetX + (left + right) * 0.5 / gameW;
    double y = offsetY + line.Y / gameH;
    if (x < .06 || x > .94) continue;
    hits.Add(new AugmentHit { Stat = stat, X = x, Y = y });
   }
   // Count distinct card positions before choosing a row. Descriptions can repeat titles.
   return hits.OrderBy(h => h.Y)
    .Select(h => DistinctCards(hits.Where(x => Math.Abs(x.Y - h.Y) < RowTolerance).OrderBy(x => x.Y)))
    .OrderByDescending(row => row.Count).FirstOrDefault() ?? new List<AugmentHit>();
  }

  static List<AugmentHit> DistinctCards(IEnumerable<AugmentHit> hits) {
   var row = new List<AugmentHit>();
   foreach (var hit in hits) {
    if (row.Count == OfferSession.MaxSlots) break;
    if (row.Any(h => h.Stat.Id == hit.Stat.Id || Math.Abs(h.X - hit.X) < MinCardSpacing)) continue;
    row.Add(hit);
   }
   return row.OrderBy(h => h.X).ToList();
  }

  static List<AugmentHit> RecoverTitles(List<AugmentHit> primary, List<AugmentHit> extra) {
   if (primary.Count == 0) return extra;
   var aligned = extra.Where(hit => primary.Any(h => Math.Abs(h.Y - hit.Y) < RowTolerance));
   var merged = DistinctCards(primary.Concat(aligned));
   if (merged.Count > primary.Count) return merged;
   // A wider crop can locate a title row entirely outside the first crop.
   return primary.Count < 2 && extra.Count > primary.Count ? extra : primary;
  }

  public static ulong BandSignature(Bitmap bmp) {
   int w = bmp.Width, h = bmp.Height;
   if (w < 128 || h < 24) return 0;
   var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
   try {
    byte[] px = new byte[w * h * 4];
    for (int y = 0; y < h; y++) Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), px, y * w * 4, w * 4);
    ulong hash = 14695981039346656037UL;
    int cols = 64, rows = 16;
    for (int gy = 0; gy < rows; gy++) {
     int y0 = h * gy / rows, y1 = h * (gy + 1) / rows;
     for (int gx = 0; gx < cols; gx++) {
      int x0 = w * gx / cols, x1 = w * (gx + 1) / cols;
      uint sum = 0, n = 0;
      for (int y = y0; y < y1; y += 2) {
       int rowOff = y * w * 4;
       for (int x = x0; x < x1; x += 2) {
        int o = rowOff + x * 4;
        sum += (uint)(px[o] + px[o + 1] + px[o + 2]);
        n += 3;
       }
      }
      uint mean = n > 0 ? sum / n : 0;
      hash ^= (mean >> 3);
      hash *= 1099511628211UL;
     }
    }
    return hash;
   } finally { bmp.UnlockBits(data); }
  }

  public static SoftwareBitmap ToSoftwareBitmap(Bitmap source, double scale, out double appliedScale) {
   int maxSide = 3200;
   double s = Math.Min(scale, Math.Min((double)maxSide / Math.Max(1, source.Width), (double)maxSide / Math.Max(1, source.Height)));
   appliedScale = s;
   int sw = Math.Max(8, (int)Math.Round(source.Width * s));
   int sh = Math.Max(8, (int)Math.Round(source.Height * s));
   Bitmap target = source; Bitmap resized = null;
   if (sw != source.Width || sh != source.Height) {
    resized = new Bitmap(sw, sh, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(resized)) {
     g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
     g.DrawImage(source, 0, 0, sw, sh);
    }
    target = resized;
   }
   try {
    var data = target.LockBits(new Rectangle(0, 0, sw, sh), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    try {
     byte[] bytes = new byte[sw * sh * 4];
     for (int y = 0; y < sh; y++) Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), bytes, y * sw * 4, sw * 4);
     using (var writer = new DataWriter()) {
      writer.WriteBytes(bytes);
      return SoftwareBitmap.CreateCopyFromBuffer(writer.DetachBuffer(), BitmapPixelFormat.Bgra8, sw, sh, BitmapAlphaMode.Ignore);
     }
    } finally { target.UnlockBits(data); }
   } finally { if (resized != null) resized.Dispose(); }
  }

  public static async Task<List<OcrLine>> RecognizeBitmap(Bitmap source, double scale, bool preferUserLang) {
   double applied;
   using (var sb = ToSoftwareBitmap(source, scale, out applied)) {
    var engine = GetEngine(preferUserLang);
    var res = await Complete(engine.RecognizeAsync(sb));
    var segments = new List<OcrLine>();
    double srcW = Math.Max(1.0, source.Width);
    // CRITICAL FOR 4-5 CARD ROUNDS: Windows OCR often merges adjacent card titles on the same row into ONE OcrLine!
    // Split every OcrLine into horizontal word clusters whenever there is a card-sized gap between consecutive words.
    foreach (var l in res.Lines) {
     if (l.Words == null || l.Words.Count == 0) continue;
     var words = l.Words.OrderBy(w => w.BoundingRect.X).ToList();
     var cluster = new List<Windows.Media.Ocr.OcrWord> { words[0] };
     Action flush = () => {
      if (cluster.Count == 0) return;
      double minX = cluster.Min(w => w.BoundingRect.X) / applied;
      double maxX = cluster.Max(w => w.BoundingRect.X + w.BoundingRect.Width) / applied;
      double minY = cluster.Min(w => w.BoundingRect.Y) / applied;
      double maxY = cluster.Max(w => w.BoundingRect.Y + w.BoundingRect.Height) / applied;
      segments.Add(new OcrLine {
       Text = String.Join(" ", cluster.Select(w => w.Text)),
       X = minX, Y = minY, Width = Math.Max(1, maxX - minX), Height = Math.Max(1, maxY - minY)
      });
     };
     for (int i = 1; i < words.Count; i++) {
      var prev = words[i - 1].BoundingRect;
      var cur = words[i].BoundingRect;
      double gapSrc = (cur.X - (prev.X + prev.Width)) / applied;
      double hSrc = Math.Max(prev.Height, cur.Height) / applied;
      double splitGap = Math.Max(hSrc, srcW * 0.012);
      if (gapSrc > splitGap) {
       flush();
       cluster = new List<Windows.Media.Ocr.OcrWord>();
      }
      cluster.Add(words[i]);
     }
     flush();
    }
    return segments;
   }
  }

  public static async Task<List<OcrLine>> ReadPng(byte[] png, bool preferUserLang = false) {
   using (var ms = new MemoryStream(png))
   using (var bmp = new Bitmap(ms)) {
    double scale = Math.Min(1.0, 1600.0 / Math.Max(1, bmp.Width));
    return await RecognizeBitmap(bmp, scale, preferUserLang);
   }
  }

  public class ScanOutput {
   public List<AugmentHit> Hits = new List<AugmentHit>();
   public List<string> Lines = new List<string>();
   public byte[] DebugPng;
   public double Milliseconds;
   public bool UsedTitleBand;
   public ulong Signature;
   public bool SkippedUnchanged;
  }

  public static async Task<ScanOutput> ScanGameRect(Native.Rect gameRect, IEnumerable<Stat> augments, Dictionary<int, List<string>> aliases, bool preferUserLang, Func<Native.Rect, Bitmap> capturer = null, bool allowWideFallback = true, bool captureDebugPng = false, ulong prevSignature = 0) {
   var sw = System.Diagnostics.Stopwatch.StartNew();
   var output = new ScanOutput();
   int gw = gameRect.Width, gh = gameRect.Height;
   int cropLeft = gameRect.Left + (int)(gw * 0.05);
   double ox = (cropLeft - gameRect.Left) / (double)gw;
   int cropW = (int)(gw * 0.90);

   int bandTop = gameRect.Top + (int)(gh * TitleBandTop);
   int bandH = Math.Max(16, (int)(gh * (TitleBandBottom - TitleBandTop)));
   var bandRect = new Native.Rect { Left = cropLeft, Top = bandTop, Right = cropLeft + cropW, Bottom = bandTop + bandH };
   Func<Native.Rect, Bitmap> captureRect = r => {
    if (capturer != null) return capturer(r);
    var b = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb);
    try {
     using (var g = Graphics.FromImage(b)) g.CopyFromScreen(r.Left, r.Top, 0, 0, new Size(r.Width, r.Height));
     return b;
    } catch { b.Dispose(); throw; }
   };

   using (var bandBmp = captureRect(bandRect)) {
    ulong sig = BandSignature(bandBmp);
    output.Signature = sig;
    if (!allowWideFallback && prevSignature != 0 && sig == prevSignature) {
     output.SkippedUnchanged = true;
     sw.Stop();
     output.Milliseconds = sw.Elapsed.TotalMilliseconds;
     return output;
    }
    double upScale = Math.Max(1.0, Math.Min(2.2, BandTargetHeight / Math.Max(1, bandBmp.Height)));
    var bandLines = await RecognizeBitmap(bandBmp, upScale, preferUserLang);
    output.Lines = bandLines.Select(l => l.Text).ToList();
    double bandY = (bandTop - gameRect.Top) / (double)gh;
    output.Hits = FindTitles(bandLines, augments, aliases, gw, gh, ox, bandY);
    output.UsedTitleBand = true;
    if (allowWideFallback && output.Hits.Count < OfferSession.MaxSlots) {
     // A different scale changes which tightly spaced words Windows OCR can resolve.
     double retryScale = upScale < 1.5 ? upScale * 1.5 : 1.0;
     var retryLines = await RecognizeBitmap(bandBmp, retryScale, preferUserLang);
     var recovered = RecoverTitles(output.Hits, FindTitles(retryLines, augments, aliases, gw, gh, ox, bandY));
     if (recovered.Count > output.Hits.Count) {
      output.Hits = recovered;
      output.Lines.AddRange(retryLines.Select(l => l.Text));
     }
    }
    if (captureDebugPng || output.Hits.Count >= 2) {
     using (var ms = new MemoryStream()) { bandBmp.Save(ms, ImageFormat.Png); output.DebugPng = ms.ToArray(); }
    }
   }

   if (allowWideFallback && output.Hits.Count < OfferSession.MaxSlots) {
    int wideTop = gameRect.Top + (int)(gh * 0.14);
    int wideH = Math.Max(32, (int)(gh * 0.56));
    var wideRect = new Native.Rect { Left = cropLeft, Top = wideTop, Right = cropLeft + cropW, Bottom = wideTop + wideH };
    using (var wideBmp = captureRect(wideRect)) {
     double s = Math.Min(1.2, 1800.0 / Math.Max(1, wideBmp.Width));
     var wideLines = await RecognizeBitmap(wideBmp, s, preferUserLang);
     var wideHits = FindTitles(wideLines, augments, aliases, gw, gh, ox, (wideTop - gameRect.Top) / (double)gh);
     var recovered = RecoverTitles(output.Hits, wideHits);
     if (recovered.Count > output.Hits.Count) {
      output.Hits = recovered;
      output.UsedTitleBand = false;
      if (wideLines.Count > output.Lines.Count) output.Lines = wideLines.Select(l => l.Text).ToList();
     }
    }
   }

   sw.Stop();
   output.Milliseconds = sw.Elapsed.TotalMilliseconds;
   return output;
  }

  public class ChampCardScanOutput {
   public List<ChampionCardHit> Hits = new List<ChampionCardHit>();
   public ulong Signature;
   public bool SkippedUnchanged;
   public double Milliseconds;
  }

  static Champion MatchChampionName(string rawText, List<Champion> champions, Dictionary<string, Champion> exactMap) {
   string folded = ShapeFold(rawText);
   if (folded.Length < 2) return null;
   Champion exact;
   if (exactMap.TryGetValue(folded, out exact)) return exact;
   if (folded.Length < 4) return null;
   // Allow 1-2 char OCR noise for longer names (e.g. "WARW1CK", "AURELION S0L") requiring unique best match
   Champion best = null;
   int bestDist = 99, secondDist = 99;
   int limit = folded.Length <= 6 ? 1 : 2;
   foreach (var kv in exactMap) {
    string key = kv.Key;
    if (key.Length < 4 || Math.Abs(key.Length - folded.Length) > limit) continue;
    int dist = EditDistance(key, folded, limit);
    if (dist > limit) continue;
    if (best != null && best.Slug == kv.Value.Slug) { if (dist < bestDist) bestDist = dist; continue; }
    if (dist < bestDist) { secondDist = bestDist; bestDist = dist; best = kv.Value; }
    else if (dist < secondDist) secondDist = dist;
   }
   return best != null && bestDist < secondDist ? best : null;
  }

  public static Dictionary<string, Champion> BuildChampionOcrMap(IEnumerable<Champion> champions, Dictionary<int, string> aliasById = null, Dictionary<int, string> localById = null) {
   var map = new Dictionary<string, Champion>(StringComparer.OrdinalIgnoreCase);
   var list = (champions ?? Enumerable.Empty<Champion>()).ToList();
   foreach (var c in list) {
    if (c == null) continue;
    string fn = ShapeFold(c.Name); if (fn.Length >= 2 && !map.ContainsKey(fn)) map[fn] = c;
    string fa = ShapeFold(c.AssetKey); if (fa.Length >= 2 && !map.ContainsKey(fa)) map[fa] = c;
    string fs = ShapeFold(c.Slug); if (fs.Length >= 2 && !map.ContainsKey(fs)) map[fs] = c;
   }
   // Common localized French/client champion display names
   var extras = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
    { "maitreyi", "masteryi" }, { "nunu", "nunu" }, { "nunuwillump", "nunu" }, { "nunuetwillump", "nunu" },
    { "wukong", "monkeyking" }, { "drmundo", "drmundo" }, { "renata", "renata" }, { "renataglasc", "renata" }
   };
   foreach (var kv in extras) {
    Champion target = list.FirstOrDefault(c => GameBridge.Norm(c.AssetKey) == kv.Value || GameBridge.Norm(c.Slug) == kv.Value || GameBridge.Norm(c.Name) == kv.Value);
    if (target != null) { string fk = ShapeFold(kv.Key); if (!map.ContainsKey(fk)) map[fk] = target; }
   }
   if (aliasById != null && localById != null) {
    foreach (var kv in localById) {
     string alias;
     if (!aliasById.TryGetValue(kv.Key, out alias) || String.IsNullOrEmpty(kv.Value)) continue;
     Champion target = list.FirstOrDefault(c => GameBridge.Norm(c.AssetKey) == GameBridge.Norm(alias) || GameBridge.Norm(c.Name) == GameBridge.Norm(alias));
     if (target != null) {
      string fl = ShapeFold(kv.Value);
      if (fl.Length >= 2 && !map.ContainsKey(fl)) map[fl] = target;
     }
    }
   }
   return map;
  }

  public static List<ChampionCardHit> FindChampionCards(List<OcrLine> lines, List<Champion> champions, Dictionary<string, Champion> exactMap, int clientW, int clientH, double offsetX, double offsetY) {
   var hits = new List<ChampionCardHit>();
   if (lines == null || lines.Count == 0 || exactMap == null || exactMap.Count == 0) return hits;
   for (int i = 0; i < lines.Count; i++) {
    var line = lines[i];
    double left = line.X, right = line.X + line.Width, cx = line.X + line.Width * 0.5;
    Champion matched = MatchChampionName(line.Text, champions, exactMap);
    if (matched == null) {
     var nearby = lines.Where((other, idx) => idx != i && other.Y > line.Y && other.Y - line.Y < Math.Max(45, clientH * 0.065) && Math.Abs((other.X + other.Width * 0.5) - cx) < clientW * 0.08).OrderBy(o => o.Y).FirstOrDefault();
     if (nearby != null) {
      matched = MatchChampionName(line.Text + " " + nearby.Text, champions, exactMap);
      if (matched != null) { left = Math.Min(left, nearby.X); right = Math.Max(right, nearby.X + nearby.Width); }
     }
    }
    if (matched == null) continue;
    double x = offsetX + (left + right) * 0.5 / Math.Max(1, clientW);
    double y = offsetY + (line.Y + line.Height * 0.5) / Math.Max(1, clientH);
    if (x < 0.08 || x > 0.92 || y < 0.28 || y > 0.88) continue;
    hits.Add(new ChampionCardHit { Champion = matched, X = x, Y = y });
   }
   if (hits.Count < 2) return new List<ChampionCardHit>();
   // Select the largest horizontally aligned row of distinct champion cards (works for 2, 3, 4, 5+ cards!)
   List<ChampionCardHit> bestRow = new List<ChampionCardHit>();
   foreach (var anchor in hits) {
    var row = new List<ChampionCardHit>();
    foreach (var h in hits.Where(o => Math.Abs(o.Y - anchor.Y) < 0.05).OrderBy(o => o.X)) {
     if (row.Count >= 5) break;
     if (row.Any(r => r.Champion.Slug == h.Champion.Slug || Math.Abs(r.X - h.X) < 0.10)) continue;
     row.Add(h);
    }
    if (row.Count > bestRow.Count) bestRow = row;
   }
   return bestRow.Count >= 2 ? bestRow : new List<ChampionCardHit>();
  }

  static Bitmap MakeHighContrastTextBitmap(Bitmap source) {
   int w = source.Width, h = source.Height;
   var dst = new Bitmap(w, h, PixelFormat.Format32bppArgb);
   var srcData = source.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
   var dstData = dst.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
   try {
    byte[] sBuf = new byte[Math.Abs(srcData.Stride) * h];
    byte[] dBuf = new byte[Math.Abs(dstData.Stride) * h];
    Marshal.Copy(srcData.Scan0, sBuf, 0, sBuf.Length);
    int sStride = Math.Abs(srcData.Stride), dStride = Math.Abs(dstData.Stride);
    for (int y = 0; y < h; y++) {
     int sRow = y * sStride, dRow = y * dStride;
     for (int x = 0; x < w; x++) {
      int so = sRow + x * 4, dOff = dRow + x * 4;
      int lum = (sBuf[so] * 114 + sBuf[so + 1] * 587 + sBuf[so + 2] * 299) / 1000;
      byte v = lum >= 155 ? (byte)0 : (lum >= 115 ? (byte)((155 - lum) * 6) : (byte)255);
      dBuf[dOff] = v; dBuf[dOff + 1] = v; dBuf[dOff + 2] = v; dBuf[dOff + 3] = 255;
     }
    }
    Marshal.Copy(dBuf, 0, dstData.Scan0, dBuf.Length);
   } finally {
    source.UnlockBits(srcData);
    dst.UnlockBits(dstData);
   }
   return dst;
  }

  public static async Task<ChampCardScanOutput> ScanChampSelectCards(Native.Rect clientRect, List<Champion> champions, Dictionary<string, Champion> exactMap, bool preferUserLang, ulong prevSignature = 0, Func<Native.Rect, Bitmap> capturer = null) {
   var sw = System.Diagnostics.Stopwatch.StartNew();
   var output = new ChampCardScanOutput();
   int cw = clientRect.Width, ch = clientRect.Height;
   if (cw < 400 || ch < 250) return output;
   int cropLeft = clientRect.Left + (int)(cw * 0.06);
   int cropTop = clientRect.Top + (int)(ch * 0.30);
   int cropW = Math.Max(64, (int)(cw * 0.88));
   int cropH = Math.Max(64, (int)(ch * 0.56));
   var cropRect = new Native.Rect { Left = cropLeft, Top = cropTop, Right = cropLeft + cropW, Bottom = cropTop + cropH };
   Func<Native.Rect, Bitmap> captureRect = r => {
    if (capturer != null) return capturer(r);
    var b = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb);
    try {
     using (var g = Graphics.FromImage(b)) g.CopyFromScreen(r.Left, r.Top, 0, 0, new Size(r.Width, r.Height));
     return b;
    } catch { b.Dispose(); throw; }
   };
   double ox = (cropLeft - clientRect.Left) / (double)cw;
   double oy = (cropTop - clientRect.Top) / (double)ch;
   using (var bmp = captureRect(cropRect)) {
    ulong sig = BandSignature(bmp);
    output.Signature = sig;
    if (prevSignature != 0 && sig == prevSignature) {
     output.SkippedUnchanged = true;
     sw.Stop();
     output.Milliseconds = sw.Elapsed.TotalMilliseconds;
     return output;
    }
    double scale = Math.Max(1.4, Math.Min(2.0, 620.0 / Math.Max(1, bmp.Height)));
    using (var hc = MakeHighContrastTextBitmap(bmp)) {
     var lines = await RecognizeBitmap(hc, scale, preferUserLang);
     output.Hits = FindChampionCards(lines, champions, exactMap, cw, ch, ox, oy);
    }
    if (output.Hits.Count < 2) {
     var rawLines = await RecognizeBitmap(bmp, Math.Max(1.8, scale), preferUserLang);
     var rawHits = FindChampionCards(rawLines, champions, exactMap, cw, ch, ox, oy);
     if (rawHits.Count > output.Hits.Count) output.Hits = rawHits;
    }
   }
   sw.Stop();
   output.Milliseconds = sw.Elapsed.TotalMilliseconds;
   return output;
  }
 }
}
