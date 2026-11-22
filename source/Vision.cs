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
  class Pixels { public int W, H; public byte[] P; public Pixels(Bitmap b) { W = b.Width; H = b.Height; var data = b.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb); P = new byte[W * H * 4]; for (int y = 0; y < H; y++) System.Runtime.InteropServices.Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), P, y * W * 4, W * 4); b.UnlockBits(data); } }
  class Template { public double[] V; public int[] U, Y; public double Mean, Norm; }
  static Template Make(BitmapImage image) {
   if (image == null) return null;
   var fmt = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0); int w = fmt.PixelWidth, h = fmt.PixelHeight; var p = new byte[w * h * 4]; fmt.CopyPixels(p, w * 4, 0);
   var values = new List<double>(); var us = new List<int>(); var ys = new List<int>();
   for (int y = 0; y < 6; y++) for (int x = 0; x < 6; x++) { if ((x == 0 || x == 5) && (y == 0 || y == 5)) continue; int xx = (int)(w * (.12 + x * .152)), yy = (int)(h * (.12 + y * .152)); for (int c = 0; c < 3; c++) values.Add(p[(yy * w + xx) * 4 + c]); us.Add(x); ys.Add(y); }
   var v = values.ToArray(); double mean = v.Average(); double norm = Math.Sqrt(v.Sum(n => (n - mean) * (n - mean))); return norm < 50 ? null : new Template { V = v, Mean = mean, Norm = norm, U = us.ToArray(), Y = ys.ToArray() };
  }
  static double Score(Pixels frame, Template t, int x, int y, int size) {
   double sum = 0, sq = 0, dot = 0;
   for (int i = 0; i < t.U.Length; i++) { int xx = x + (int)(size * (.12 + t.U[i] * .152)), yy = y + (int)(size * (.12 + t.Y[i] * .152)); int p = (yy * frame.W + xx) * 4; for (int c = 0; c < 3; c++) { double n = frame.P[p + c]; sum += n; sq += n * n; dot += n * t.V[i * 3 + c]; } }
   double denom = Math.Sqrt(Math.Max(1, sq - sum * sum / t.V.Length)) * t.Norm; return (dot - sum * t.Mean) / denom;
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
     foreach (int size in new[] { 12, 15, 18, 21, 24, 27, 30, 34, 39, 45, 52, 60 }) {
      int maxX = frame.W - size, maxY = frame.H - size;
      for (int y = 0; y <= maxY; y += 3) for (int x = 0; x <= maxX; x += 3) {
       double score = Score(frame, template, x, y, size); if (score > best) { best = score; bx = x; by = y; bs = size; }
      }
     }
     if (bs == 0) continue;
     int startX = bx, startY = by, startSize = bs;
     for (int size = Math.Max(10, startSize - 2); size <= startSize + 2; size++) for (int y = Math.Max(0, startY - 3); y <= Math.Min(frame.H - size - 1, startY + 3); y++) for (int x = Math.Max(0, startX - 3); x <= Math.Min(frame.W - size - 1, startX + 3); x++) { double score = Score(frame, template, x, y, size); if (score > best) { best = score; bx = x; by = y; bs = size; } }
     if (best < .90) continue;
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
   return confirmed;
  }
 }
}
