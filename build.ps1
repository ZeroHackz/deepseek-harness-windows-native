param(
    [switch]$SelfContained,   # legacy alias of -SingleFile
    [switch]$SingleFile,      # lone self-contained exe (BunkrDownloader-style) + zip
    [switch]$Portable,        # self-contained exe+DLLs folder + zip (CyberleekViewer-style)
    [switch]$RefreshIcons
)
<#
.SYNOPSIS
    Builds the DeepSeek Harness desktop app.

.DESCRIPTION
    Default (no switch): framework-dependent single-file exe into dist\ —
    small, for local development; needs the .NET 8 runtime installed.

    -Portable    Self-contained folder (exe + DLLs, .NET runtime bundled) plus
                 a release zip. Runs on any 64-bit Windows 10/11 without a
                 .NET install. Folder layout keeps startup fast and triggers
                 fewer AV false positives (CyberleekViewer-style).
    -SingleFile  One self-contained exe (runtime + native libs bundled,
                 extracted to %TEMP% on start) plus a release zip
                 (BunkrDownloader-style).
    -RefreshIcons  First re-fetches the official DeepSeek icons
                 (tools\update-icons.ps1) and regenerates the .ico assets.

    Artifacts land in release\ (git-ignored, ready for GitHub Releases):
      release\DeepSeekHarness-portable-win-x64-<ver>\        (folder; -Portable)
      release\DeepSeekHarness-portable-win-x64-<ver>.zip     (-Portable)
      release\DeepSeekHarness-win-x64-<ver>.exe              (-SingleFile)
      release\DeepSeekHarness-win-x64-<ver>.zip              (-SingleFile)

.EXAMPLE
    .\build.ps1                 # dev exe (framework-dependent) into dist\
    .\build.ps1 -Portable       # portable folder + zip
    .\build.ps1 -SingleFile     # lone portable exe + zip
    .\build.ps1 -Portable -RefreshIcons
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj = Join-Path $root 'src\DeepSeekHarness\DeepSeekHarness.csproj'
$dist = Join-Path $root 'dist'
$release = Join-Path $root 'release'

if ($RefreshIcons) {
    pwsh -NoProfile -File (Join-Path $root 'tools\update-icons.ps1')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

function Get-Version {
    $xml = Get-Content -LiteralPath $proj -Raw
    $m = [regex]::Match($xml, '<Version>([^<]+)</Version>')
    if ($m.Success) { return $m.Groups[1].Value }
    return '0.0.0'
}

function Invoke-Publish {
    param([string[]]$ExtraArgs, [string]$OutDir)
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutDir) | Out-Null
    dotnet publish $proj -c Release -r win-x64 --self-contained true @ExtraArgs -o $OutDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

function New-Zip {
    param([string]$SourcePath, [string]$ZipPath)
    if (Test-Path -LiteralPath $ZipPath) { Remove-Item -LiteralPath $ZipPath -Force }
    Compress-Archive -Path $SourcePath -DestinationPath $ZipPath -CompressionLevel Optimal -Force
    Write-Output ("zip: {0} ({1:N1} MB)" -f $ZipPath, ((Get-Item -LiteralPath $ZipPath).Length / 1MB))
}

$version = Get-Version
Write-Output ("building DeepSeek Harness desktop v{0}" -f $version)

if ($SelfContained) { $SingleFile = $true }

if ($Portable -and $SingleFile) {
    Write-Output 'use either -Portable or -SingleFile, not both; defaulting to -Portable'
    $SingleFile = $false
}

# --- default: local dev exe (framework-dependent) ----------------------------
if (-not $Portable -and -not $SingleFile) {
    Write-Output 'publishing dev exe (framework-dependent single file) -> dist\'
    New-Item -ItemType Directory -Force -Path $dist | Out-Null
    dotnet publish $proj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o $dist
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Output ("published: {0}" -f (Join-Path $dist 'DeepSeekHarness.exe'))
    exit 0
}

# --- -Portable: self-contained folder + zip ----------------------------------
if ($Portable) {
    $name = "DeepSeekHarness-portable-win-x64-$version"
    $folder = Join-Path $release $name
    Write-Output "publishing self-contained portable folder -> $folder"
    Invoke-Publish -ExtraArgs @('-p:PublishSingleFile=false', '-p:DebugType=None') -OutDir $folder
    New-Zip -SourcePath (Join-Path $folder '*') -ZipPath (Join-Path $release ($name + '.zip'))
    # stable mirror so a shortcut/run path never breaks across version bumps
    $current = Join-Path $release 'portable-current'
    if (Test-Path -LiteralPath $current) { Remove-Item -LiteralPath $current -Recurse -Force }
    Copy-Item -LiteralPath $folder -Destination $current -Recurse
    Write-Output ("portable exe: {0}" -f (Join-Path $folder 'DeepSeekHarness.exe'))
    Write-Output ("current mirror: {0}" -f (Join-Path $current 'DeepSeekHarness.exe'))
    exit 0
}

# --- -SingleFile: one self-contained exe + zip -------------------------------
if ($SingleFile) {
    $name = "DeepSeekHarness-win-x64-$version"
    $out = Join-Path $release $name
    Write-Output "publishing self-contained single-file exe -> $out"
    Invoke-Publish -ExtraArgs @(
        '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-p:EnableCompressionInSingleFile=true',
        '-p:DebugType=None'
    ) -OutDir $out
    $exe = Join-Path $out 'DeepSeekHarness.exe'
    $finalExe = Join-Path $release ($name + '.exe')
    Copy-Item -LiteralPath $exe -Destination $finalExe -Force
    New-Zip -SourcePath $finalExe -ZipPath (Join-Path $release ($name + '.zip'))
    Write-Output ("portable exe: {0}" -f $finalExe)
    exit 0
}
