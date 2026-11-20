using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace MayloRelease {
 internal sealed class ReleaseVersion : IComparable<ReleaseVersion> {
  public readonly string Text;
  readonly Version number;
  readonly int? beta;

  ReleaseVersion(string text, Version number, int? beta) {
   Text = text; this.number = number; this.beta = beta;
  }

  public static ReleaseVersion Parse(string text) {
   var match = Regex.Match(text ?? "", @"\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-beta\.([1-9][0-9]*))?\z");
   int major, minor, patch, beta = 0;
   if (!match.Success || !Int32.TryParse(match.Groups[1].Value, out major) ||
       !Int32.TryParse(match.Groups[2].Value, out minor) || !Int32.TryParse(match.Groups[3].Value, out patch) ||
       (match.Groups[4].Success && !Int32.TryParse(match.Groups[4].Value, out beta))) return null;
   return new ReleaseVersion(text, new Version(major, minor, patch), beta > 0 ? (int?)beta : null);
  }

  public int CompareTo(ReleaseVersion other) {
   int version = number.CompareTo(other.number);
   if (version != 0) return version;
   if (!beta.HasValue) return other.beta.HasValue ? 1 : 0;
   return other.beta.HasValue ? beta.Value.CompareTo(other.beta.Value) : -1;
  }
 }

 internal static class InstallStore {
  public const string Launcher = "MAYLO.exe";
  public const string App = "MAYLO-Hub.exe";
  public const string Payload = "MAYLO-win-x64.zip";
  const long SizeLimit = 64 * 1024 * 1024;

  [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
  static extern bool DeleteFile(string lpFileName);

  /// <summary>
  /// Removes NTFS Mark of the Web (:Zone.Identifier) so Windows Startup and shortcuts
  /// never block automatic launch with "Open File - Security Warning".
  /// </summary>
  public static void UnblockFile(string path) {
   if (String.IsNullOrWhiteSpace(path)) return;
   try { DeleteFile(path + ":Zone.Identifier"); } catch { }
  }

  /// <summary>
  /// Copies only the primary file data stream (never NTFS Alternate Data Streams such as
  /// :Zone.Identifier) and atomically replaces the destination file.
  /// </summary>
  public static void CopyCleanFile(string source, string destination) {
   string fullSrc = Path.GetFullPath(source);
   string fullDst = Path.GetFullPath(destination);
   if (String.Equals(fullSrc, fullDst, StringComparison.OrdinalIgnoreCase)) {
    UnblockFile(fullDst);
    return;
   }
   Directory.CreateDirectory(Path.GetDirectoryName(fullDst));
   string temp = fullDst + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
   try {
    using (var input = new FileStream(fullSrc, FileMode.Open, FileAccess.Read, FileShare.Read))
    using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
     input.CopyTo(output);
    }
    UnblockFile(temp);
    if (File.Exists(fullDst)) {
     try { File.Replace(temp, fullDst, null); }
     catch {
      File.Delete(fullDst);
      File.Move(temp, fullDst);
     }
    } else {
     File.Move(temp, fullDst);
    }
    UnblockFile(fullDst);
   } finally {
    try { if (File.Exists(temp)) File.Delete(temp); } catch { }
   }
  }

  public static string Versions(string root) { return Path.Combine(root, "versions"); }
  public static string InstalledRoot() {
   var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
   var versions = directory.Parent;
   if (versions == null || versions.Name != "versions" || versions.Parent == null ||
       ReleaseVersion.Parse(directory.Name) == null) return null;
   return File.Exists(Path.Combine(versions.Parent.FullName, Launcher)) ? versions.Parent.FullName : null;
  }
  public static string AppPath(string root, ReleaseVersion version) {
   return Path.Combine(Versions(root), version.Text, App);
  }

  static void CheckDirectory(string dir) {
   if (Directory.Exists(dir) && (File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0)
    throw new IOException("MAYLO cannot use a redirected install directory.");
  }

  static ReleaseVersion ReadVersion(string path) {
   if (!File.Exists(path) || new FileInfo(path).Length > 128) return null;
   return ReleaseVersion.Parse(File.ReadAllText(path).Trim());
  }

  public static ReleaseVersion Current(string root, bool previous = false) {
   var version = ReadVersion(Path.Combine(root, previous ? "previous.txt" : "current.txt"));
   if (version == null || !File.Exists(AppPath(root, version))) return null;
   var actual = ReadVersion(Path.Combine(Versions(root), version.Text, "version.txt"));
   return actual != null && actual.Text == version.Text ? version : null;
  }

  public static FileStream Lock(string root) {
   CheckDirectory(root);
   Directory.CreateDirectory(root);
   CheckDirectory(Versions(root));
   return new FileStream(Path.Combine(root, ".install.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
  }

  public static void Activate(string root, ReleaseVersion release) {
   var current = Path.Combine(root, "current.txt");
   var pending = Path.Combine(root, Guid.NewGuid().ToString("N") + ".tmp");
   File.WriteAllText(pending, release.Text);
   try {
    if (File.Exists(current)) File.Replace(pending, current, Path.Combine(root, "previous.txt"));
    else File.Move(pending, current);
   } finally {
    if (File.Exists(pending)) File.Delete(pending);
   }
  }

  /// <summary>
  /// Keeps only the active version and the immediate rollback version in versions/.
  /// </summary>
  public static void PruneOldVersions(string root) {
   try {
    string versionsDir = Versions(root);
    if (!Directory.Exists(versionsDir)) return;
    var keep = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var cur = Current(root, false); if (cur != null) keep.Add(cur.Text);
    var prev = Current(root, true); if (prev != null) keep.Add(prev.Text);
    string runningDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory).Name;
    if (!String.IsNullOrEmpty(runningDir)) keep.Add(runningDir);
    foreach (var dir in Directory.GetDirectories(versionsDir)) {
     string name = Path.GetFileName(dir);
     if (ReleaseVersion.Parse(name) == null || keep.Contains(name)) continue;
     if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) continue;
     try { Directory.Delete(dir, true); } catch { }
    }
   } catch { }
  }

  public static void Extract(Stream payload, string target, Action<int, string> progress = null) {
   Directory.CreateDirectory(target);
   var root = Path.GetFullPath(target).TrimEnd('\\') + "\\";
   using (var zip = new ZipArchive(payload, ZipArchiveMode.Read, true)) {
    if (zip.Entries.Count > 500) throw new InvalidDataException("Unexpected update archive.");
    long totalSize = 0;
    int done = 0;
    foreach (var entry in zip.Entries) {
     if (String.IsNullOrEmpty(entry.Name)) continue;
     var relative = entry.FullName.Replace('\\', '/');
     var segments = relative.Split('/');
     if (Array.Exists(segments, x => x == "" || x == "." || x == ".." || x.TrimEnd(' ', '.') != x ||
       x.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)) throw new InvalidDataException("Invalid update path.");
     var dest = Path.GetFullPath(Path.Combine(root, relative));
     if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid update path.");
     totalSize += entry.Length;
     if (totalSize > SizeLimit) throw new InvalidDataException("Update archive is too large.");
     Directory.CreateDirectory(Path.GetDirectoryName(dest));
     using (var input = entry.Open())
     using (var output = new FileStream(dest, FileMode.CreateNew)) input.CopyTo(output);
     UnblockFile(dest);
     done++;
     if (progress != null) progress(15 + (done * 70 / zip.Entries.Count), "Installing " + entry.Name + "…");
    }
   }
  }

  public static void Stage(string root, ReleaseVersion release, Stream payload, Action<int, string> progress = null) {
   var versions = Versions(root);
   Directory.CreateDirectory(versions);
   var final = Path.Combine(versions, release.Text);
   if (Directory.Exists(final)) {
    CheckDirectory(final);
    ValidatePayload(final, release);
    UnblockFile(Path.Combine(final, App));
    return;
   }

   var stage = Path.Combine(versions, ".stage-" + Guid.NewGuid().ToString("N"));
   try {
    Extract(payload, stage, progress);
    ValidatePayload(stage, release);
    Directory.Move(stage, final);
   } finally {
    if (Directory.Exists(stage)) Directory.Delete(stage, true);
   }
  }

  static void ValidatePayload(string directory, ReleaseVersion release) {
   foreach (var file in new[] { App, App + ".config", "version.txt", "ui-dist\\index.html",
     "Microsoft.Web.WebView2.Core.dll", "Microsoft.Web.WebView2.Wpf.dll", "WebView2Loader.dll" })
    if (!File.Exists(Path.Combine(directory, file))) throw new InvalidDataException("Incomplete MAYLO package.");
   var actual = ReadVersion(Path.Combine(directory, "version.txt"));
   if (actual == null || actual.Text != release.Text) throw new InvalidDataException("MAYLO package and release versions differ.");
  }

  static string BuildForwardedArgs(string[] args) {
   if (args == null || args.Length == 0) return "";
   var safe = args.Where(a => a == "--autostart" || a == "--minimized").ToArray();
   return String.Join(" ", safe);
  }

  public static Process Launch(string root, ReleaseVersion release, string[] args = null) {
   var exe = AppPath(root, release);
   UnblockFile(exe);
   var psi = new ProcessStartInfo(exe) {
    WorkingDirectory = Path.GetDirectoryName(exe),
    UseShellExecute = false
   };
   string fwd = BuildForwardedArgs(args);
   if (!String.IsNullOrEmpty(fwd)) psi.Arguments = fwd;
   return Process.Start(psi);
  }
 }
}
