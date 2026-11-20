param([switch]$SkipNpmInstall)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$version = (Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
$dist = Join-Path $root 'dist'
$stage = Join-Path $dist ('stage-' + [Guid]::NewGuid().ToString('N'))
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $framework 'WPF'
$files = Join-Path $stage 'app'
$assets = Join-Path $root 'assets'
$zip = Join-Path $dist 'MAYLO-win-x64.zip'
$setup = Join-Path $dist "MAYLO-v$version-Setup.exe"
New-Item -ItemType Directory -Path (Join-Path $files 'assets') -Force | Out-Null

try {
    & (Join-Path $PSScriptRoot 'release-info.ps1') -Path (Join-Path $stage 'BuildInfo.cs')
    Push-Location (Join-Path $root 'web')
    try {
        if (!$SkipNpmInstall) {
            $npm = Get-Command npm.cmd -ErrorAction SilentlyContinue
            $npmPath = if ($npm) { $npm.Source } else { 'C:\Program Files\nodejs\npm.cmd' }
            & $npmPath ci --no-audit --no-fund
            if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' }
        }
        node node_modules/typescript/bin/tsc --noEmit
        if ($LASTEXITCODE -ne 0) { throw 'TypeScript check failed.' }
        node node_modules/vite/bin/vite.js build --outDir (Join-Path $files 'ui-dist')
        if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
    } finally { Pop-Location }

    & (Join-Path $root 'source\build.ps1') -Destination (Join-Path $files 'MAYLO-Hub.exe')
    Copy-Item -LiteralPath (Join-Path $assets 'maylo.ico'),(Join-Path $assets 'maylo.png') -Destination (Join-Path $files 'assets')
    Get-ChildItem -LiteralPath (Join-Path $root 'lib') -Filter '*.dll' | Copy-Item -Destination $files
    Copy-Item -LiteralPath (Join-Path $root 'LICENSE'),(Join-Path $root 'QUICKSTART.txt') -Destination $files
    Set-Content -LiteralPath (Join-Path $files 'version.txt') -Value $version -Encoding ASCII -NoNewline
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $stagedZip = Join-Path $stage 'app.zip'
    [IO.Compression.ZipFile]::CreateFromDirectory($files, $stagedZip, [IO.Compression.CompressionLevel]::Optimal, $false)

    $stagedSetup = Join-Path $stage 'setup.exe'
    $compilerArgs = @('/nologo','/target:winexe','/platform:x64','/optimize+',
        "/out:$stagedSetup","/win32icon:$assets\maylo.ico","/win32manifest:$root\source\app.manifest",
        "/resource:$stagedZip,maylo-payload.zip","/resource:$assets\maylo.png,maylo.png",
        '/r:System.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.IO.Compression.dll','/r:System.IO.Compression.FileSystem.dll','/r:System.Xaml.dll','/r:System.Web.Extensions.dll','/r:Microsoft.CSharp.dll',
        "/r:$wpf\WindowsBase.dll","/r:$wpf\PresentationCore.dll","/r:$wpf\PresentationFramework.dll",
        "/r:$root\lib\Microsoft.Web.WebView2.Core.dll","/r:$root\lib\Microsoft.Web.WebView2.Wpf.dll",
        (Join-Path $root 'installer\Setup.cs'),(Join-Path $root 'installer\WebSetupWindow.cs'),(Join-Path $root 'shared\InstallStore.cs'),(Join-Path $stage 'BuildInfo.cs'))
    & (Join-Path $framework 'csc.exe') @compilerArgs
    if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
    Copy-Item -LiteralPath $stagedZip -Destination $zip -Force
    Copy-Item -LiteralPath $stagedSetup -Destination $setup -Force
    Get-Item -LiteralPath $setup, $zip | Select-Object Name, Length
} finally {
    $resolved = (Resolve-Path -LiteralPath $stage).Path
    if ((Split-Path $resolved -Parent) -eq (Resolve-Path -LiteralPath $dist).Path -and (Split-Path $resolved -Leaf) -like 'stage-*') {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
