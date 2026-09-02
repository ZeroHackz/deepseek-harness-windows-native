<#
.SYNOPSIS
    Fetches the official DeepSeek icons and generates the app's .ico assets.

.DESCRIPTION
    Sources (official websites):
      - light/color variant: favicon.ico from https://deepseek.com
          -> the blue whale on transparency (their light-mode favicon)
      - dark variant:       favicon.svg from https://platform.deepseek.com
          -> the same whale silhouette; the site ships it fill="#000"
             (black), so for legibility on dark chrome a white-filled copy is
             rasterized. The untouched black SVG is kept under assets/source.

    Outputs (committed, used by the .NET build):
      - src\DeepSeekHarness\assets\icon-light.ico  (multi-size, incl. 256)
      - src\DeepSeekHarness\assets\icon-dark.ico
      - src\DeepSeekHarness\assets\app.ico         (= icon-light, exe icon)

    Rasterization of the SVG uses headless Edge/Chrome (Chromium
    --headless --screenshot). Pass -SkipDownload to regenerate the .ico files
    from the committed sources only (offline).

    Usage:
      pwsh -File tools\update-icons.ps1            # fetch + regenerate
      pwsh -File tools\update-icons.ps1 -SkipDownload
.EXAMPLE
    pwsh -File tools\update-icons.ps1
#>
param(
    [switch]$SkipDownload,
    [string]$BrowserPath = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$sourceDir = Join-Path $root 'assets\source'
$assetDir = Join-Path $root 'src\DeepSeekHarness\assets'
$scratch = Join-Path $env:TEMP 'dsh-icons'
New-Item -ItemType Directory -Force -Path $sourceDir, $assetDir, $scratch | Out-Null

Add-Type -AssemblyName System.Drawing

function Invoke-Download([string]$url, [string]$outFile) {
    Write-Output ("downloading {0}" -f $url)
    Invoke-WebRequest -Uri $url -TimeoutSec 30 -UseBasicParsing -OutFile $outFile
}

function Find-Chromium {
    param([string]$Explicit)
    if ($Explicit -and (Test-Path -LiteralPath $Explicit)) { return $Explicit }
    foreach ($c in @(
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft\Edge\Application\msedge.exe'),
        (Join-Path ${env:ProgramFiles} 'Microsoft\Edge\Application\msedge.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Google\Chrome\Application\chrome.exe'),
        (Join-Path ${env:ProgramFiles} 'Google\Chrome\Application\chrome.exe')
    )) { if (Test-Path -LiteralPath $c) { return $c } }
    return $null
}

function Rasterize-Svg {
    param([string]$SvgPath, [string]$OutPng, [int]$Size = 512)
    $browser = Find-Chromium -Explicit $BrowserPath
    if (-not $browser) { throw 'no Edge/Chrome found to rasterize the SVG; pass -BrowserPath' }
    $html = Join-Path $scratch 'view.svg'
    $svg = Get-Content -LiteralPath $SvgPath -Raw
    $svg = $svg -replace 'width="[\d.]+"', ("width=`"{0}`"" -f $Size) -replace 'height="[\d.]+"', ("height=`"{0}`"" -f $Size)
    [System.IO.File]::WriteAllText($html, $svg)
    $url = 'file:///' + (($html -replace '\\', '/') -replace ' ', '%20')
    & $browser --headless=new --disable-gpu --hide-scrollbars --default-background-color=00000000 --window-size="$Size,$Size" --screenshot="$OutPng" $url 2>$null | Out-Null
    if (-not (Test-Path -LiteralPath $OutPng)) { throw "rasterization failed: $OutPng" }
    Write-Output ("rasterized {0} -> {1}" -f $SvgPath, $OutPng)
}

function Get-PngFrameBytes {
    param([System.Drawing.Bitmap]$Source, [int]$Size)
    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($Source, 0, 0, $Size, $Size)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output -NoEnumerate ([byte[]]$ms.ToArray())
}

function New-IconFile {
    param([string]$SourceImage, [string]$OutFile, [int[]]$Sizes = @(16, 24, 32, 40, 48, 64, 128, 256))
    $src = [System.Drawing.Bitmap]::FromFile($SourceImage)
    $frames = New-Object System.Collections.Generic.List[object]
    foreach ($s in $Sizes) {
        $png = Get-PngFrameBytes -Source $src -Size $s
        $frames.Add([pscustomobject]@{ Size = $s; Png = $png })
    }
    $src.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $header = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($header)
    $bw.Write([uint16]0)                # reserved
    $bw.Write([uint16]1)                # type: icon
    $bw.Write([uint16]$frames.Count)    # image count
    $offset = 6 + (16 * $frames.Count)
    foreach ($f in $frames) {
        $dim = if ($f.Size -ge 256) { 0 } else { $f.Size }
        $bw.Write([byte]$dim)           # width (0 = 256)
        $bw.Write([byte]$dim)           # height
        $bw.Write([byte]0)              # palette
        $bw.Write([byte]0)              # reserved
        $bw.Write([uint16]1)            # planes
        $bw.Write([uint16]32)           # bpp
        $bw.Write([uint32]$f.Png.Length)
        $bw.Write([uint32]$offset)
        $offset += $f.Png.Length
    }
    $bw.Flush()
    $headerBytes = $header.ToArray()
    $ms.Write($headerBytes, 0, $headerBytes.Length)
    foreach ($f in $frames) {
        $png = [byte[]]$f.Png
        $ms.Write($png, 0, $png.Length)
    }
    $bw.Dispose(); $header.Dispose()
    [System.IO.File]::WriteAllBytes($OutFile, $ms.ToArray())
    $ms.Dispose()
    Write-Output ("wrote {0} ({1} frames, {2} bytes)" -f $OutFile, $frames.Count, (Get-Item -LiteralPath $OutFile).Length)
}

# --- fetch sources ----------------------------------------------------------
if ($SkipDownload) {
    if (-not (Test-Path -LiteralPath (Join-Path $sourceDir 'deepseek-light-source.png'))) {
        throw 'light source missing and -SkipDownload is set'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $sourceDir 'platform-dark-source.svg'))) {
        throw 'dark source missing and -SkipDownload is set'
    }
    Write-Output 'offline mode: using committed sources'
}
else {
    # light: the real favicon.ico of deepseek.com (single 225x225 PNG frame inside)
    $ico = Join-Path $scratch 'deepseek-favicon.ico'
    Invoke-Download 'https://deepseek.com/favicon.ico' $ico
    Add-Type -AssemblyName System.Drawing
    $icon = New-Object System.Drawing.Icon($ico)
    $bmp = $icon.ToBitmap()   # 225x225 32bpp
    $bmp.Save((Join-Path $sourceDir 'deepseek-light-source.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose(); $icon.Dispose()
    Write-Output 'extracted deepseek.com light source'

    # dark: platform.deepseek.com favicon.svg - keep the untouched black file,
    # then make the white-filled copy used for the dark-mode icon.
    $svgRaw = Join-Path $sourceDir 'platform-dark-source.svg'
    Invoke-Download 'https://fe-static.deepseek.com/platform/favicon.svg' $svgRaw
    $svgWhite = Join-Path $sourceDir 'platform-dark-white.svg'
    $content = Get-Content -LiteralPath $svgRaw -Raw
    $content = $content -replace 'fill="#000"', 'fill="#ffffff"'
    [System.IO.File]::WriteAllText($svgWhite, $content)
    Write-Output 'fetched platform.deepseek.com dark source (black original + white copy)'
}

# --- rasterize + generate .ico files ----------------------------------------
$lightPng = Join-Path $scratch 'light-render.png'
$darkPng = Join-Path $scratch 'dark-render.png'

$lightSrc = [System.Drawing.Bitmap]::FromFile((Join-Path $sourceDir 'deepseek-light-source.png'))
$lightSrc.Save($lightPng, [System.Drawing.Imaging.ImageFormat]::Png)
$lightSrc.Dispose()

Rasterize-Svg -SvgPath (Join-Path $sourceDir 'platform-dark-white.svg') -OutPng $darkPng

New-IconFile -SourceImage $lightPng -OutFile (Join-Path $assetDir 'icon-light.ico')
New-IconFile -SourceImage $darkPng  -OutFile (Join-Path $assetDir 'icon-dark.ico')
Copy-Item (Join-Path $assetDir 'icon-light.ico') (Join-Path $assetDir 'app.ico') -Force
Write-Output 'done.'
