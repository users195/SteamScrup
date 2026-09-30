# Build helper for SteamScrup (portable edition).
#
# Everything needed is vendored into the repository, so these builds work fully
# offline:
#   tools\dotnet          portable .NET 10 SDK (never installed system-wide)
#   tools\runtimepacks    self-contained runtime packages used as a local NuGet feed
#
#   .\tools\build.ps1                 # quick framework-dependent build
#   .\tools\build.ps1 -Portable       # self-contained portable build + ZIP  <-- the deliverable
#   .\tools\build.ps1 -SelfTest       # run the headless verification suite
#
[CmdletBinding()]
param(
    [switch]$Portable,
    [switch]$SelfTest,
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $root 'tools\dotnet\dotnet.exe'
$appProject = Join-Path $root 'src\SteamScrup\SteamScrup.csproj'

if (-not (Test-Path $dotnet)) {
    throw "Portable SDK not found at $dotnet. See README for how to obtain it."
}

# Keep every piece of build state inside the repository so the OS profile is never written to.
$env:DOTNET_CLI_HOME                   = Join-Path $root 'tools\clihome'
$env:DOTNET_CLI_TELEMETRY_OPTOUT       = '1'
$env:DOTNET_NOLOGO                     = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:NUGET_PACKAGES                    = Join-Path $root 'tools\nuget'
$env:APPDATA                           = Join-Path $root 'tools\appdata'
$env:PATH                              = (Join-Path $root 'tools\dotnet') + ';' + $env:PATH
$env:DOTNET_ROOT                       = Join-Path $root 'tools\dotnet'

# NuGet is confined to the vendored local feed; the public source is not used at all.
$nugetAppData = Join-Path $root 'tools\appdata\NuGet'
New-Item -ItemType Directory -Force -Path $nugetAppData | Out-Null
Copy-Item (Join-Path $root 'tools\NuGet.config') (Join-Path $nugetAppData 'NuGet.Config') -Force

function Invoke-Dotnet {
    param([string[]]$Arguments)
    Write-Host "dotnet $($Arguments -join ' ')" -ForegroundColor DarkGray
    & $dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet exited with $LASTEXITCODE" }
}

if ($SelfTest) {
    Invoke-Dotnet @('build', $appProject, '-c', $Configuration, '--nologo')
    $exe = Join-Path $root "src\SteamScrup\bin\$Configuration\net10.0-windows\SteamScrup.exe"
    $report = Join-Path $root 'tools\selftest-report.txt'
    & $exe --selftest $report
    Write-Host "report: $report" -ForegroundColor Cyan
    exit $LASTEXITCODE
}

if (-not $Portable) {
    Invoke-Dotnet @('build', $appProject, '-c', $Configuration, '--nologo')
    return
}

# ---------------------------------------------------------------- portable build

$version = '1.6.3'
$portableDir = Join-Path $root "dist\SteamScrup-$version-portable"
$zipPath = Join-Path $root "dist\SteamScrup-$version-portable.zip"

Write-Host '== portable build (self-contained, no runtime required) ==' -ForegroundColor Cyan
if (Test-Path $portableDir) { Remove-Item $portableDir -Recurse -Force }

Invoke-Dotnet @(
    'publish', $appProject, '-c', $Configuration, '--nologo',
    '-o', $portableDir,
    '-r', 'win-x64',
    '--self-contained', 'true',
    # Single file: the payoff is that a self-contained .NET app otherwise ships ~240
    # loose DLLs next to the executable. Those cannot simply be moved into a subfolder -
    # the native host requires hostfxr/hostpolicy/coreclr beside the apphost - so one
    # bundled executable is the only way to keep the folder readable.
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true',
    '-p:DebugType=none'
)

# Symbols are useless to end users and only pad the download.
Get-ChildItem $portableDir -Filter *.pdb -Recurse | Remove-Item -Force

# A short read-me travels with the portable copy.
$notice = @"
SteamScrup $version - portable edition
=====================================

Run SteamScrup.exe. Nothing is installed: no registry entries, no shortcuts,
no administrator rights, no background services.

Settings and logs are stored in this folder. If the folder is not writable
(for example after copying it into Program Files), the app automatically falls
back to %LOCALAPPDATA%\SteamScrup instead of failing.

Safety guarantees (cannot be turned off):
  - Never deletes automatically. Always preview, select, then confirm.
  - Never deletes .acf manifest files.
  - Never touches userdata saves unless you enable the advanced option.
  - No network access, no accounts, no telemetry.

Verify the build yourself:
  SteamScrup.exe --selftest report.txt

Steam must be fully closed before anything can be cleaned.
"@
Set-Content -Path (Join-Path $portableDir 'READ-ME-FIRST.txt') -Value $notice -Encoding UTF8

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path $portableDir -DestinationPath $zipPath -CompressionLevel Optimal

$files = Get-ChildItem $portableDir -Recurse -File
Write-Host ''
Write-Host ("portable folder : {0}" -f $portableDir)
Write-Host ("files           : {0}" -f $files.Count)
Write-Host ("uncompressed    : {0:N1} MB" -f (($files | Measure-Object Length -Sum).Sum / 1MB))
Write-Host ("ZIP deliverable : {0}  ({1:N1} MB)" -f $zipPath, ((Get-Item $zipPath).Length / 1MB)) -ForegroundColor Green






