$ErrorActionPreference = 'Stop'
$version = '1.0.2903.40'
$directory = Join-Path $PSScriptRoot '..\lib'
$files = @{
    'lib/net462/Microsoft.Web.WebView2.Core.dll' = 'Microsoft.Web.WebView2.Core.dll'
    'lib/net462/Microsoft.Web.WebView2.Wpf.dll' = 'Microsoft.Web.WebView2.Wpf.dll'
    'runtimes/win-x64/native/WebView2Loader.dll' = 'WebView2Loader.dll'
}
if (@($files.Values | Where-Object { !(Test-Path -LiteralPath (Join-Path $directory $_)) }).Count -eq 0) { return }
Add-Type -AssemblyName System.IO.Compression.FileSystem
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$package = Join-Path ([IO.Path]::GetTempPath()) ('maylo-webview2-' + [Guid]::NewGuid().ToString('N') + '.zip')
try {
    Invoke-WebRequest -UseBasicParsing -Uri "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$version/microsoft.web.webview2.$version.nupkg" -OutFile $package
    $zip = [IO.Compression.ZipFile]::OpenRead($package)
    try {
        foreach ($path in $files.Keys) {
            $entry = $zip.GetEntry($path)
            if (!$entry) { throw "WebView2 SDK asset missing: $path" }
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $directory $files[$path]), $true)
        }
    } finally { $zip.Dispose() }
} finally {
    if (Test-Path -LiteralPath $package) { Remove-Item -LiteralPath $package }
}
