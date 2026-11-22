using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace MayhemCompanion {
 public class PortraitHit { public Champion Champion; public double X, Y, Size, Confidence; }
 public static class Vision {
  const double BenchLeft = .19, BenchRight = .81;

  static byte Luminance(byte b, byte g, byte r) { return (byte)((b * 114 + g * 587 + r * 299) / 1000); }

  class Pixels {
   public int W, H;
   public byte[] Bgra, Gray;
   public Pixels(Bitmap bitmap) {
    W = bitmap.Width;
    H = bitmap.Height;
    Bgra = new byte[W * H * 4];
    Gray = new byte[W * H];
    var data = bitmap.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    try {
     for (int y = 0; y < H; y++)
      System.Runtime.InteropServices.Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), Bgra, y * W * 4, W * 4);
    } finally { bitmap.UnlockBits(data); }
    for (int i = 0; i < Gray.Length; i++) Gray[i] = Luminance(Bgra[i * 4], Bgra[i * 4 + 1], Bgra[i * 4 + 2]);
   }
  }

  class Template {
   public double[] Color, Gray;
   public double ColorNorm, GrayNorm;
   public int[] U, Y;
  }

  static double Center(double[] samples) {
   double mean = samples.Average();
   for (int i = 0; i < samples.Length; i++) samples[i] -= mean;
   return Math.Sqrt(samples.Sum(value => value * value));
  }

  static Template Make(BitmapImage image) {
   if (image == null) return null;
   var fmt = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
   int width = fmt.PixelWidth, height = fmt.PixelHeight;
   var pixels = new byte[width * height * 4];
   fmt.CopyPixels(pixels, width * 4, 0);
   var color = new List<double>();
   var gray = new List<double>();
   var columns = new List<int>();
   var rows = new List<int>();
   for (int y = 0; y < 6; y++) for (int x = 0; x < 6; x++) {
    if ((x == 0 || x == 5) && (y == 0 || y == 5)) continue;
    int pixel = ((int)(height * (.12 + y * .152)) * width + (int)(width * (.12 + x * .152))) * 4;
    for (int channel = 0; channel < 3; channel++) color.Add(pixels[pixel + channel]);
    gray.Add(Luminance(pixels[pixel], pixels[pixel + 1], pixels[pixel + 2]));
    columns.Add(x); rows.Add(y);
   }
   var template = new Template { Color = color.ToArray(), Gray = gray.ToArray(), U = columns.ToArray(), Y = rows.ToArray() };
   template.ColorNorm = Center(template.Color);
   template.GrayNorm = Center(template.Gray);
   return template.ColorNorm < 50 ? null : template;
  }

  static double Score(Pixels frame, Template template, int origin, int[] offsets) {
   double sum = 0, sq = 0, dot = 0, graySum = 0, graySq = 0, grayDot = 0;
   for (int i = 0; i < offsets.Length; i++) {
    int pixel = origin + offsets[i];
    for (int channel = 0; channel < 3; channel++) {
     double value = frame.Bgra[pixel * 4 + channel];
     sum += value; sq += value * value; dot += value * template.Color[i * 3 + channel];
    }
    double gray = frame.Gray[pixel];
    graySum += gray; graySq += gray * gray; grayDot += gray * template.Gray[i];
   }
   double color = dot / (Math.Sqrt(Math.Max(1, sq - sum * sum / template.Color.Length)) * template.ColorNorm);
   if (template.GrayNorm < 30) return color;
   double grayScore = grayDot / (Math.Sqrt(Math.Max(1, graySq - graySum * graySum / offsets.Length)) * template.GrayNorm);
   return Math.Max(color, grayScore - .025);
  }

  public static List<PortraitHit> FillBenchRow(List<PortraitHit> hits, List<Champion> ordered, double widthToHeight) {
   var known = hits.Where(hit => hit.X >= BenchLeft && hit.X <= BenchRight)
    .ToDictionary(hit => hit.Champion.Slug);
   if (ordered.Count == 1) return known.Values.Where(hit => hit.X >= .36 && hit.X <= .64).ToList();

   var anchors = ordered.Select((champion, index) => new { champion, index }).Where(slot => known.ContainsKey(slot.champion.Slug)).ToList();
   List<PortraitHit> row = new List<PortraitHit>();
   double bestScore = 0;
   for (int left = 0; left < anchors.Count; left++) for (int right = left + 1; right < anchors.Count; right++) {
    var first = known[anchors[left].champion.Slug];
    var last = known[anchors[right].champion.Slug];
    double size = (first.Size + last.Size) / 2;
    double y = (first.Y + last.Y) / 2;
    double pitch = (last.X - first.X) / (anchors[right].index - anchors[left].index);
    double start = first.X - anchors[left].index * pitch;
    double end = start + (ordered.Count - 1) * pitch;
    if (pitch < size * .96 || pitch > size * 1.45 || start < BenchLeft || end > BenchRight ||
        (start + end) / 2 < .33 || (start + end) / 2 > .67 ||
        Math.Abs(first.Y - last.Y) > size * widthToHeight * .22 ||
        Math.Abs(first.Size - last.Size) > size * .22) continue;

    var matching = anchors.Where(slot => {
     var hit = known[slot.champion.Slug];
     return Math.Abs(hit.Y - y) <= size * widthToHeight * .22 &&
      Math.Abs(hit.Size - size) <= size * .20 &&
      Math.Abs(hit.X - (start + slot.index * pitch)) <= size * .20;
    }).ToDictionary(slot => slot.index, slot => known[slot.champion.Slug]);
    if (matching.Count < 2) continue;
    double score = matching.Count * 2 + matching.Values.Average(hit => hit.Confidence);
    if (score <= bestScore) continue;
    bestScore = score;
    row = ordered.Select((champion, index) => matching.ContainsKey(index) ? matching[index] :
     new PortraitHit { Champion = champion, X = start + index * pitch, Y = y, Size = size }).ToList();
   }
   return row;
  }

  public static List<PortraitHit> BenchPortraits(Bitmap original, List<Champion> candidates, Dictionary<string, BitmapImage> images) {
   var result = new List<PortraitHit>();
   using (var small = new Bitmap(640, Math.Max(1, (int)(640.0 * original.Height / original.Width)), PixelFormat.Format32bppArgb)) {
    using (var g = Graphics.FromImage(small)) { g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear; g.DrawImage(original, new Rectangle(0, 0, small.Width, small.Height)); }
    var frame = new Pixels(small);
    foreach (var champion in candidates.GroupBy(c => c.Slug).Select(g => g.First())) {
     BitmapImage image; if (!images.TryGetValue(champion.Slug, out image)) continue;
     var template = Make(image); if (template == null) continue;
     double best = .75; int bx = 0, by = 0, bs = 0;
     Func<int, int[]> offsetsForSize = size => template.U.Select((u, i) =>
      (int)(size * (.12 + template.Y[i] * .152)) * frame.W + (int)(size * (.12 + u * .152))).ToArray();
     foreach (int size in new[] { 12, 15, 18, 21, 24, 27, 30, 34, 39, 45, 52, 60 }) {
      var offsets = offsetsForSize(size);
      int minX = Math.Max(0, (int)(frame.W * BenchLeft - size / 2.0));
      int maxX = Math.Min(frame.W - size, (int)(frame.W * BenchRight - size / 2.0));
      int maxY = frame.H - size;
      for (int y = 0; y <= maxY; y += 3) for (int x = minX; x <= maxX; x += 3) {
       double score = Score(frame, template, y * frame.W + x, offsets); if (score > best) { best = score; bx = x; by = y; bs = size; }
      }
     }
     if (bs == 0) continue;
     int startX = bx, startY = by, startSize = bs;
     for (int size = Math.Max(10, startSize - 2); size <= startSize + 2; size++) {
      var offsets = offsetsForSize(size);
      for (int y = Math.Max(0, startY - 3); y <= Math.Min(frame.H - size, startY + 3); y++)
       for (int x = Math.Max(0, startX - 3); x <= Math.Min(frame.W - size, startX + 3); x++) {
        double score = Score(frame, template, y * frame.W + x, offsets);
        if (score > best) { best = score; bx = x; by = y; bs = size; }
       }
     }
     if (best < .84) continue;
     var hit = new PortraitHit { Champion = champion, X = (bx + bs * .5) / frame.W, Y = (double)by / frame.H, Size = (double)bs / frame.W, Confidence = best };
     result.Add(hit);
    }
   }
   var confirmed = new List<PortraitHit>();
   foreach (var hit in result.OrderByDescending(h => h.Confidence)) {
    if (confirmed.Any(h => Math.Abs(h.X - hit.X) < Math.Min(h.Size, hit.Size) * .7 &&
      Math.Abs(h.Y - hit.Y) * original.Height < Math.Min(h.Size, hit.Size) * original.Width * .7)) continue;
    confirmed.Add(hit);
   }
   return FillBenchRow(confirmed, candidates, original.Width / (double)original.Height);
  }
 }
}
