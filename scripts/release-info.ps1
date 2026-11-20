param([Parameter(Mandatory)][string]$Path)
$versionText = (Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\VERSION') -Raw).Trim()
if ($versionText -cnotmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-beta\.([1-9][0-9]*))?$') {
    throw 'VERSION must be MAJOR.MINOR.PATCH or MAJOR.MINOR.PATCH-beta.N.'
}
$revision = 0
if ($Matches[4]) { $revision = [int]$Matches[4] }
$numeric = [version]::new([int]$Matches[1], [int]$Matches[2], [int]$Matches[3], $revision)
foreach ($field in @($numeric.Major, $numeric.Minor, $numeric.Build, $numeric.Revision)) {
    if ($field -gt 65534) { throw 'Version components must fit Windows version metadata (0–65534).' }
}
@"
[assembly: System.Reflection.AssemblyVersion("$($numeric.Major).$($numeric.Minor).$($numeric.Build).0")]
[assembly: System.Reflection.AssemblyFileVersion("$numeric")]
[assembly: System.Reflection.AssemblyInformationalVersion("$versionText")]
namespace MayloRelease {
 internal static class BuildInfo {
  public const string Version = "$versionText";
 }
}
"@ | Set-Content -LiteralPath $Path -Encoding UTF8
