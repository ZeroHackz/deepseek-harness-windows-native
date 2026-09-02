#Requires -Version 7.0
<#
.SYNOPSIS
    Builds the portable single-file DeepSeek Harness executable.

.DESCRIPTION
    Produces a fully portable single .exe - no DLLs next to it and no .NET
    installation needed on the target machine:

      release\DeepSeekHarness-win-x64-<version>.exe
      release\DeepSeekHarness-win-x64-<version>.zip

    The .NET runtime and the native WebView2 loader are bundled inside the
    exe (extracted to %TEMP% on first start). Only OS dependency on the
    target: the WebView2 Runtime (preinstalled on Windows 11 / most
    Windows 10 machines).

    Usage:
      .\build_portable.ps1                # build single-file exe + zip
      .\build_portable.ps1 -RefreshIcons  # re-fetch the official DeepSeek
                                          # icons from the websites first
.EXAMPLE
    pwsh -NoProfile -File .\build_portable.ps1
#>
param(
    [switch]$RefreshIcons
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj = Join-Path $root 'src\DeepSeekHarness\DeepSeekHarness.csproj'
$release = Join-Path $root 'release'

if ($RefreshIcons) {
    pwsh -NoProfile -File (Join-Path $root 'tools\update-icons.ps1')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$xml = Get-Content -LiteralPath $proj -Raw
$m = [regex]::Match($xml, '<Version>([^<]+)</Version>')
$version = if ($m.Success) { $m.Groups[1].Value } else { '0.0.0' }

$name = "DeepSeekHarness-win-x64-$version"
$out = Join-Path $release $name
$exe = Join-Path $out 'DeepSeekHarness.exe'
$finalExe = Join-Path $release ($name + '.exe')
$zip = Join-Path $release ($name + '.zip')

Write-Output ("Building DeepSeek Harness portable single-file executable v{0} ..." -f $version)
New-Item -ItemType Directory -Force -Path $release | Out-Null
if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Recurse -Force }

dotnet publish $proj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -o $out
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# keep the staging folder to just the exe (drop NuGet XML doc files)
Get-ChildItem -LiteralPath $out -File -Filter '*.xml' | Remove-Item -Force

Copy-Item -LiteralPath $exe -Destination $finalExe -Force
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path $finalExe -DestinationPath $zip -CompressionLevel Optimal

Write-Output ''
Write-Output ('Build successful!')
Write-Output ('  Single-file exe : {0}' -f $finalExe)
Write-Output ('  Zip             : {0}' -f $zip)
Write-Output ''
Write-Output 'No DLLs are needed next to the exe, and no .NET installation is'
Write-Output 'required on the target machine - the runtime and native libraries'
Write-Output 'are bundled and extracted to %TEMP% on first start.'
