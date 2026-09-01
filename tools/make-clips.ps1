<#
.SYNOPSIS
    Turn a recorded frame sequence into an mp4 and a gif.

.DESCRIPTION
    The clip scripts in Docs\scripts\clip-*.rbs write numbered PNGs; this makes something you
    can post out of them.

    **Both formats, because they are for different places.** Reddit and itch.io take mp4 and
    autoplay it, at a fraction of the size and without the colour banding a 256-entry palette
    forces. A GitHub README cannot embed video in markdown, so it needs the gif. Posting the
    gif where the mp4 would work is the common mistake and it costs both quality and load time.

    The gif is built through a generated palette rather than the default one. ffmpeg's stock
    palette turns this game's masonry into three shades of mud; palettegen reads the clip and
    picks 256 colours that are actually in it. It costs one extra pass and is the difference
    between usable and not.

.PARAMETER Name
    The clip folder under captures\, without the path. Defaults to every clip-* folder.

.PARAMETER Fps
    Frames per second the sequence was recorded at. Must match the record command.

.PARAMETER GifWidth
    How wide the gif is scaled to. 720 keeps a README readable without a 12 MB download; the
    mp4 stays at full size.
#>
[CmdletBinding()]
param(
    [string]$Name = "",
    [int]$Fps = 30,
    [int]$GifWidth = 720
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$captures = Join-Path $root "captures"
$out = Join-Path $root "Docs\clips"

if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue)) {
    throw "ffmpeg is not on PATH. It is the only thing this script needs."
}

New-Item -ItemType Directory -Force -Path $out | Out-Null

$folders = if ($Name) {
    @(Join-Path $captures $Name)
} else {
    Get-ChildItem -Path $captures -Directory -Filter "clip-*" | ForEach-Object { $_.FullName }
}

if (-not $folders) { throw "No clip folders found under $captures." }

foreach ($folder in $folders) {
    $clip = Split-Path $folder -Leaf
    $frames = Get-ChildItem -Path $folder -Filter "frame_*.png" -ErrorAction SilentlyContinue

    if (-not $frames -or $frames.Count -eq 0) {
        Write-Host "  $clip : no frames, skipped" -ForegroundColor Yellow
        continue
    }

    $pattern = Join-Path $folder "frame_%04d.png"
    $mp4 = Join-Path $out "$clip.mp4"
    $gif = Join-Path $out "$clip.gif"
    $palette = Join-Path $env:TEMP "$clip-palette.png"

    Write-Host "==> $clip ($($frames.Count) frames)" -ForegroundColor Cyan

    # yuv420p and an even width, or the file plays as a black rectangle in about half the
    # players it will meet -- Reddit's among them.
    ffmpeg -y -loglevel error -framerate $Fps -i $pattern `
        -vf "scale=trunc(iw/2)*2:trunc(ih/2)*2" `
        -c:v libx264 -pix_fmt yuv420p -crf 20 -movflags +faststart $mp4
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed writing $mp4" }

    ffmpeg -y -loglevel error -framerate $Fps -i $pattern `
        -vf "fps=$Fps,scale=${GifWidth}:-1:flags=lanczos,palettegen=stats_mode=diff" $palette
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed writing the palette for $clip" }

    ffmpeg -y -loglevel error -framerate $Fps -i $pattern -i $palette `
        -lavfi "fps=$Fps,scale=${GifWidth}:-1:flags=lanczos[x];[x][1:v]paletteuse=dither=bayer:bayer_scale=3" `
        -loop 0 $gif
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed writing $gif" }

    Remove-Item $palette -Force -ErrorAction SilentlyContinue

    $mp4Size = "{0:N1} MB" -f ((Get-Item $mp4).Length / 1MB)
    $gifSize = "{0:N1} MB" -f ((Get-Item $gif).Length / 1MB)
    Write-Host "    mp4  $mp4Size   $mp4"
    Write-Host "    gif  $gifSize   $gif"
}

Write-Host ""
Write-Host "Clips are in Docs\clips. Post the mp4; the gif is for the README." -ForegroundColor Green
