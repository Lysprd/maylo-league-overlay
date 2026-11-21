$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$version = (Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
$setup = Join-Path $root "dist\MAYLO-v$version-Setup.exe"
$sandbox = Join-Path ([IO.Path]::GetTempPath()) ('maylo-test-' + [Guid]::NewGuid().ToString('N'))

try {
    $installer = Start-Process -FilePath $setup -ArgumentList @('--test-install', ('"' + $sandbox + '"')) -WindowStyle Hidden -Wait -PassThru
    if ($installer.ExitCode -ne 0) { throw "Install smoke test failed ($($installer.ExitCode))." }
    $app = Join-Path $sandbox "versions\$version\MAYLO-Hub.exe"
    $report = Join-Path $sandbox 'updater.txt'
    if ((Get-Content -LiteralPath (Join-Path $sandbox 'current.txt') -Raw) -ne $version) { throw 'Incorrect active version.' }
    if (!(Test-Path -LiteralPath (Join-Path $sandbox 'MAYLO.exe'))) { throw 'Launcher is missing.' }
    $native = Start-Process -FilePath $app -ArgumentList @('--test-updater', ('"' + $report + '"')) -WindowStyle Hidden -Wait -PassThru
    Get-Content -LiteralPath $report
    if ($native.ExitCode -ne 0) { throw 'Updater regression test failed.' }
    $report = Join-Path $sandbox 'recommendations.txt'
    $native = Start-Process -FilePath $app -ArgumentList @('--test-recommendations', ('"' + $report + '"')) -WindowStyle Hidden -Wait -PassThru
    Get-Content -LiteralPath $report
    if ($native.ExitCode -ne 0) { throw 'Recommendation regression test failed.' }
} finally {
    if (Test-Path -LiteralPath $sandbox) {
        $resolved = (Resolve-Path -LiteralPath $sandbox).Path
        if ((Split-Path $resolved -Leaf) -like 'maylo-test-*' -and (Split-Path $resolved -Parent) -eq (Resolve-Path ([IO.Path]::GetTempPath())).Path.TrimEnd('\')) {
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
    }
}
