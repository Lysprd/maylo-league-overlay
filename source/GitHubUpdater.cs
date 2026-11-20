using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using MayloRelease;

namespace MayhemCompanion {
 internal static class GitHubUpdater {
  const long RepositoryId = 1464758229;
  const string RepositoryApi = "https://api.github.com/repositories/1464758229";
  const int DownloadLimit = 32 * 1024 * 1024;

  public sealed class Release {
   public string tag_name { get; set; }
   public bool draft { get; set; }
   public bool prerelease { get; set; }
   public Asset[] assets { get; set; }
  }

  public sealed class Asset {
   public string name { get; set; }
   public string digest { get; set; }
   public string state { get; set; }
   public int size { get; set; }
   public string browser_download_url { get; set; }
  }

  public sealed class RepositoryIdentity {
   public long id { get; set; }
   public string full_name { get; set; }
  }

  public static void Start(CancellationToken cancellation) {
   string root = InstallStore.InstalledRoot();
   if (root == null) return;
   var _ = Task.Run(() => Run(root, cancellation), cancellation);
  }

  static async Task Run(string root, CancellationToken cancellation) {
   using (var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })) {
    client.Timeout = TimeSpan.FromSeconds(30);
    client.MaxResponseContentBufferSize = 4 * 1024 * 1024;
    client.DefaultRequestHeaders.UserAgent.ParseAdd("maylo/" + BuildInfo.Version);
    var delay = TimeSpan.FromSeconds(8);
    while (!cancellation.IsCancellationRequested) {
     try {
      await Task.Delay(delay, cancellation);
      await Check(root, client, cancellation);
     } catch (OperationCanceledException) {
      if (cancellation.IsCancellationRequested) return;
     } catch (Exception ex) {
      System.Diagnostics.Trace.WriteLine("maylo update: " + ex.Message);
     }
     delay = TimeSpan.FromMinutes(30);
    }
   }
  }

  static Tuple<ReleaseVersion, Asset> SelectRelease(Release[] releases, ReleaseVersion current, string repository) {
   return releases.Where(x => x != null && !x.draft && (!x.prerelease || current.Text.Contains("-beta.")) &&
     x.tag_name != null && x.tag_name.StartsWith("v", StringComparison.Ordinal))
    .Select(x => new { Version = ReleaseVersion.Parse(x.tag_name.Substring(1)), Release = x })
    .Where(x => x.Version != null && x.Version.CompareTo(current) > 0)
    .OrderByDescending(x => x.Version)
    .Select(x => Tuple.Create(x.Version, (x.Release.assets ?? new Asset[0]).FirstOrDefault(asset => ValidAsset(asset, x.Version, repository))))
    .FirstOrDefault(x => x.Item2 != null);
  }

  static bool ValidAsset(Asset asset, ReleaseVersion release, string repository) {
   if (asset == null || asset.name != InstallStore.Payload || asset.state != "uploaded" ||
       asset.size < 1 || asset.size > DownloadLimit || !Regex.IsMatch(asset.digest ?? "", @"\Asha256:[0-9a-fA-F]{64}\z")) return false;
   var expected = "https://github.com/" + repository + "/releases/download/v" + release.Text + "/" + InstallStore.Payload;
   return String.Equals(expected, asset.browser_download_url, StringComparison.Ordinal);
  }

  static async Task<T> GetJson<T>(HttpClient client, string url, CancellationToken cancellation) {
   using (var response = await client.GetAsync(url, cancellation)) {
    response.EnsureSuccessStatusCode();
    string json = await response.Content.ReadAsStringAsync();
    return new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 }.Deserialize<T>(json);
   }
  }

  static async Task Check(string root, HttpClient client, CancellationToken cancellation) {
   var current = InstallStore.Current(root);
   if (current == null) return;
   // Resolve the current name through the immutable repository ID before trusting any download URL.
   var repo = await GetJson<RepositoryIdentity>(client, RepositoryApi, cancellation);
   if (repo == null || repo.id != RepositoryId || !Regex.IsMatch(repo.full_name ?? "", @"\A[A-Za-z0-9-]+/[A-Za-z0-9_.-]+\z"))
    throw new IOException("Unexpected GitHub repository identity.");
   var releases = await GetJson<Release[]>(client, RepositoryApi + "/releases?per_page=30", cancellation);
   var target = SelectRelease(releases, current, repo.full_name);
   if (target == null) return;

   using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation)) {
    deadline.CancelAfter(TimeSpan.FromMinutes(2));
    using (var zip = await Download(client, target.Item2, deadline.Token))
    using (InstallStore.Lock(root)) {
     current = InstallStore.Current(root);
     if (current == null || current.CompareTo(target.Item1) >= 0) return;
     InstallStore.Stage(root, target.Item1, zip);
     InstallStore.Activate(root, target.Item1);
    }
   }
  }

  static async Task<MemoryStream> Download(HttpClient client, Asset asset, CancellationToken cancellation) {
   Uri url = new Uri(asset.browser_download_url);
   for (int redirects = 0; redirects <= 3; redirects++) {
    using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellation)) {
     int status = (int)response.StatusCode;
     if (status >= 300 && status < 400) {
      var destination = response.Headers.Location;
      if (destination == null || !destination.IsAbsoluteUri || destination.Scheme != "https" ||
          !destination.IsDefaultPort || destination.UserInfo.Length > 0 ||
          (destination.Host != "release-assets.githubusercontent.com" && destination.Host != "objects.githubusercontent.com"))
       throw new IOException("Unexpected download redirect.");
      url = destination;
      continue;
     }

     response.EnsureSuccessStatusCode();
     using (var input = await response.Content.ReadAsStreamAsync()) {
      var output = new MemoryStream(asset.size);
      try {
       byte[] buffer = new byte[64 * 1024];
       int read;
       while ((read = await input.ReadAsync(buffer, 0, buffer.Length, cancellation)) != 0) {
        if (output.Length + read > asset.size) throw new InvalidDataException("Incorrect update size.");
        output.Write(buffer, 0, read);
       }
       Verify(output, asset);
       return output;
      } catch { output.Dispose(); throw; }
     }
    }
   }
   throw new IOException("Too many download redirects.");
  }

  static void Verify(MemoryStream file, Asset asset) {
   if (file.Length != asset.size) throw new InvalidDataException("Incorrect update size.");
   file.Position = 0;
   string digest;
   using (var sha = SHA256.Create()) digest = "sha256:" + BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
   if (!String.Equals(asset.digest, digest, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Incorrect update SHA-256.");
   file.Position = 0;
  }

  public static int SelfTest(string report) {
   try {
    var beta2 = ReleaseVersion.Parse("0.1.0-beta.2");
    var beta10 = ReleaseVersion.Parse("0.1.0-beta.10");
    if (beta2 == null || beta2.CompareTo(beta10) >= 0 || beta10.CompareTo(ReleaseVersion.Parse("0.1.0")) >= 0 ||
        ReleaseVersion.Parse("0.1.0-beta.02") != null || ReleaseVersion.Parse("../0.1.0") != null) throw new Exception("Release order");
    const string originalName = "Lysprd/MAYLO";
    const string renamed = "Lysprd/maylo-league-overlay";
    var asset = new Asset {
     name = InstallStore.Payload, state = "uploaded", size = 3,
     browser_download_url = "https://github.com/" + originalName + "/releases/download/v0.1.0-beta.10/" + InstallStore.Payload,
     digest = "sha256:ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"
    };
    var offered = new Release { tag_name = "v0.1.0-beta.10", prerelease = true, assets = new[] { asset } };
    if (SelectRelease(new[] { offered }, beta2, originalName).Item1.Text != beta10.Text) throw new Exception("Release selection");
    if (SelectRelease(new[] { offered }, beta2, renamed) != null) throw new Exception("The old URL must not match the new name");
    asset.browser_download_url = asset.browser_download_url.Replace(originalName, renamed);
    if (SelectRelease(new[] { offered }, beta2, renamed).Item1.Text != beta10.Text) throw new Exception("Renamed repository selection");
    offered.draft = true;
    if (SelectRelease(new[] { offered }, beta2, renamed) != null) throw new Exception("Draft selection");
    using (var data = new MemoryStream(new byte[] { 97, 98, 99 })) Verify(data, asset);
    try {
     using (var data = new MemoryStream(new byte[] { 97, 98, 100 })) Verify(data, asset);
     throw new Exception("Accepted mismatched digest");
    } catch (InvalidDataException) { }
    VerifyStagedUpdate();
    File.WriteAllText(report, "PASS: beta ordering, release selection, SHA-256, archive paths, and atomic activation/rollback.");
    return 0;
   } catch (Exception ex) {
    File.WriteAllText(report, ex.ToString());
    return 1;
   }
  }

  static void VerifyStagedUpdate() {
   var directory = Path.Combine(Path.GetTempPath(), "maylo-updater-test-" + Guid.NewGuid().ToString("N"));
   var old = ReleaseVersion.Parse("0.1.0-beta.1");
   var next = ReleaseVersion.Parse("0.1.0-beta.2");
   try {
    using (InstallStore.Lock(directory)) {
     using (var zip = TestPackage(old, false)) InstallStore.Stage(directory, old, zip);
     InstallStore.Activate(directory, old);
     try {
      using (var zip = TestPackage(next, true)) InstallStore.Stage(directory, next, zip);
      throw new Exception("Accepted archive traversal");
     } catch (InvalidDataException) { }
     if (InstallStore.Current(directory).Text != old.Text) throw new Exception("Broken package changed the active version");

     using (var zip = TestPackage(next, false)) InstallStore.Stage(directory, next, zip);
     if (InstallStore.Current(directory).Text != old.Text) throw new Exception("Staging changed the active version");
     InstallStore.Activate(directory, next);
     if (InstallStore.Current(directory).Text != next.Text || InstallStore.Current(directory, true).Text != old.Text)
      throw new Exception("Update should retain the previous version");
     InstallStore.Activate(directory, InstallStore.Current(directory, true));
     if (InstallStore.Current(directory).Text != old.Text) throw new Exception("Rollback failed");
    }
   } finally {
    if (Directory.Exists(directory)) Directory.Delete(directory, true);
   }
  }

  static MemoryStream TestPackage(ReleaseVersion version, bool invalidPath) {
   var stream = new MemoryStream();
   using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) {
    var files = new[] { InstallStore.App, InstallStore.App + ".config", "version.txt", "ui-dist/index.html",
     "Microsoft.Web.WebView2.Core.dll", "Microsoft.Web.WebView2.Wpf.dll", "WebView2Loader.dll" };
    foreach (var file in files) {
     using (var entry = new StreamWriter(zip.CreateEntry(file).Open())) entry.Write(version.Text);
    }
    if (invalidPath) zip.CreateEntry("../outside");
   }
   stream.Position = 0;
   return stream;
  }
 }
}
