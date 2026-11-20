param([string]$Destination)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (!$Destination) { $Destination = Join-Path $root 'MAYLO-Hub.exe' }
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $framework 'WPF'
$winMetadata = Join-Path $env:WINDIR 'System32\WinMetadata'
& (Join-Path $root 'scripts\restore-webview.ps1')

$references = @('PresentationFramework','PresentationCore','WindowsBase') | ForEach-Object { "/reference:$wpf\$_.dll" }
$references += @('System.Xaml','System.Web.Extensions','System.Drawing','System.Windows.Forms','System.Runtime','System.Runtime.WindowsRuntime','System.Runtime.InteropServices.WindowsRuntime','System.Net.Http','System.IO.Compression','System.IO.Compression.FileSystem') | ForEach-Object { "/reference:$framework\$_.dll" }
if (Test-Path (Join-Path $winMetadata 'Windows.Media.winmd')) {
    $references += @('Windows.Foundation','Windows.Media','Windows.Graphics','Windows.Storage','Windows.Globalization') | ForEach-Object { "/reference:$winMetadata\$_.winmd" }
} else {
    $metadata = Get-ChildItem (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\UnionMetadata') -Filter Windows.winmd -Recurse | Sort-Object FullName -Descending | Select-Object -First 1
    if (!$metadata) { throw 'Install Windows SDK metadata for Windows OCR.' }
    $references += "/reference:$($metadata.FullName)"
}
$references += "/reference:$root\lib\Microsoft.Web.WebView2.Core.dll"
$references += "/reference:$root\lib\Microsoft.Web.WebView2.Wpf.dll"

$buildInfo = Join-Path ([IO.Path]::GetTempPath()) ('maylo-buildinfo-' + [Guid]::NewGuid().ToString('N') + '.cs')
try {
    & (Join-Path $root 'scripts\release-info.ps1') -Path $buildInfo
    $sources = @(Get-ChildItem -LiteralPath $PSScriptRoot, (Join-Path $root 'shared') -Filter '*.cs' -File | Select-Object -ExpandProperty FullName)
    & (Join-Path $framework 'csc.exe') /nologo /target:winexe /platform:x64 /optimize+ "/win32icon:$root\assets\maylo.ico" "/win32manifest:$PSScriptRoot\app.manifest" "/out:$Destination" $references $sources $buildInfo
    if ($LASTEXITCODE -ne 0) { throw 'Native MAYLO compilation failed.' }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'app.config') -Destination "$Destination.config" -Force
} finally {
    if (Test-Path -LiteralPath $buildInfo) { Remove-Item -LiteralPath $buildInfo }
}
