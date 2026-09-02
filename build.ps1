param(
    [switch]$SelfContained,
    [switch]$RefreshIcons
)
<#
.SYNOPSIS
    Builds the DeepSeek Harness desktop app into dist\DeepSeekHarness.exe.

.DESCRIPTION
    Default: framework-dependent single-file exe (small; requires the .NET 8
    runtime). With -SelfContained: a single file that bundles the runtime
    (larger, ~70-150 MB) and runs on any 64-bit Windows 10/11 machine.

    With -RefreshIcons: first re-fetches the official DeepSeek icons from
    deepseek.com / platform.deepseek.com (tools\update-icons.ps1) and
    regenerates the .ico assets used by the build.
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj = Join-Path $root 'src\DeepSeekHarness\DeepSeekHarness.csproj'
$out = Join-Path $root 'dist'

if ($RefreshIcons) {
    pwsh -NoProfile -File (Join-Path $root 'tools\update-icons.ps1')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

if ($SelfContained) {
    dotnet publish $proj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $out
}
else {
    dotnet publish $proj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o $out
}

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Output ("published: " + (Join-Path $out 'DeepSeekHarness.exe'))
