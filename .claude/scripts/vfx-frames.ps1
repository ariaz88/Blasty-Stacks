<#
.SYNOPSIS
    Pull still frames out of a reference video so they can be read as images.

.DESCRIPTION
    Claude cannot read a video file - only images. This wraps ffmpeg (installed at
    C:\ffmpeg\ffmpeg-7.1.1-full_build\bin) so every frame pull in this project is
    done the same way instead of being re-invented per session.

    Frames are written OUTSIDE Assets/ on purpose. A folder of PNGs under Assets/
    becomes hundreds of Unity texture imports; the script refuses to do that.

.PARAMETER Video
    Path to the source clip. Relative paths resolve against the repo root.

.PARAMETER Start
    Where to start. Accepts 12, 12.4, 0:12.4 or 00:00:12.4. Default: the beginning.

.PARAMETER End
    Where to stop, same formats. Default: the end of the clip.

.PARAMETER Fps
    Frames per second to extract. Default 8 - enough to read an effect's beats
    without producing a hundred near-identical images.

.PARAMETER Width
    Output width in pixels, height auto. Default 720. Pass 0 to keep native size.

.PARAMETER OutDir
    Where the PNGs go. Defaults to a folder under $env:TEMP. Claude passes the
    session scratchpad instead.

.EXAMPLE
    .\.claude\scripts\vfx-frames.ps1 -Video "Reference\hero-death\clip.mp4" -Start 0:12.4 -End 0:14.0

.EXAMPLE
    .\.claude\scripts\vfx-frames.ps1 -Video "Assets\Arts\Reference videos\Roguelite Clip.MP4" -Start 5 -End 7 -Fps 12
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Video,

    [string] $Start,
    [string] $End,
    [int]    $Fps    = 8,
    [int]    $Width  = 720,
    [string] $OutDir
)

$ErrorActionPreference = 'Stop'

function Resolve-Timecode {
    # Accepts 12 | 12.4 | 0:12.4 | 00:00:12.4 and returns seconds as [double].
    param([string] $Value, [string] $Name)

    if ([string]::IsNullOrWhiteSpace($Value)) { return $null }

    $parts = $Value.Split(':')
    if ($parts.Count -gt 3) { throw "$Name '$Value' is not a timecode. Use 12.4, 0:12.4 or 00:00:12.4." }

    $seconds = 0.0
    foreach ($p in $parts) {
        $n = 0.0
        if (-not [double]::TryParse($p, [ref] $n)) {
            throw "$Name '$Value' is not a timecode. Use 12.4, 0:12.4 or 00:00:12.4."
        }
        $seconds = $seconds * 60.0 + $n
    }
    return $seconds
}

# --- locate the tools -------------------------------------------------------
foreach ($exe in @('ffmpeg', 'ffprobe')) {
    if (-not (Get-Command $exe -ErrorAction SilentlyContinue)) {
        throw "$exe is not on PATH. Expected it at C:\ffmpeg\ffmpeg-7.1.1-full_build\bin."
    }
}

# --- resolve the input ------------------------------------------------------
if (-not [System.IO.Path]::IsPathRooted($Video)) {
    $Video = Join-Path (Get-Location).Path $Video
}
if (-not (Test-Path -LiteralPath $Video)) { throw "No such video: $Video" }
$videoItem = Get-Item -LiteralPath $Video

# --- report what the clip actually is ---------------------------------------
# Worth printing every time: a wrong assumption about length or resolution is
# how you end up extracting the wrong two seconds.
$probe = & ffprobe -v error -select_streams v:0 `
    -show_entries "format=duration:stream=width,height,avg_frame_rate" `
    -of default=noprint_wrappers=1 "$($videoItem.FullName)"
Write-Output "Source : $($videoItem.Name)"
foreach ($line in $probe) { Write-Output "         $line" }

# --- resolve the time window ------------------------------------------------
$startSec = Resolve-Timecode -Value $Start -Name 'Start'
$endSec   = Resolve-Timecode -Value $End   -Name 'End'

if ($null -ne $startSec -and $null -ne $endSec -and $endSec -le $startSec) {
    throw "End ($End) must be after Start ($Start)."
}

# --- resolve the output folder ----------------------------------------------
$slug = [System.IO.Path]::GetFileNameWithoutExtension($videoItem.Name) -replace '[^A-Za-z0-9_-]', '-'
if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $OutDir = Join-Path $env:TEMP "claude-vfx-frames\$slug"
}
if (-not [System.IO.Path]::IsPathRooted($OutDir)) {
    $OutDir = Join-Path (Get-Location).Path $OutDir
}

# Hard stop: never let extracted frames land where Unity will import them.
if ($OutDir -match '(?i)[\\/]Assets[\\/]') {
    throw "Refusing to write frames under Assets/ - Unity would import every one of them. Pick a path outside Assets/."
}

if (Test-Path -LiteralPath $OutDir) {
    Get-ChildItem -LiteralPath $OutDir -Filter 'frame_*.png' | Remove-Item -Force
} else {
    New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
}

# --- build the ffmpeg call --------------------------------------------------
# -ss goes BEFORE -i for a fast seek, and the window length is passed as -t
# rather than -to: with an input-side seek, -to has meant different things in
# different ffmpeg versions, while -t is unambiguously a duration.
$ffArgs = @('-hide_banner', '-loglevel', 'error', '-y')
if ($null -ne $startSec) { $ffArgs += @('-ss', $startSec.ToString([cultureinfo]::InvariantCulture)) }
$ffArgs += @('-i', $videoItem.FullName)
if ($null -ne $endSec) {
    # No ?? operator here - this has to run on Windows PowerShell 5.1.
    $from = 0.0
    if ($null -ne $startSec) { $from = $startSec }
    $duration = $endSec - $from
    $ffArgs += @('-t', $duration.ToString([cultureinfo]::InvariantCulture))
}

$filter = "fps=$Fps"
if ($Width -gt 0) { $filter += ",scale=${Width}:-2" }
$ffArgs += @('-vf', $filter, (Join-Path $OutDir 'frame_%03d.png'))

& ffmpeg @ffArgs
if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed with exit code $LASTEXITCODE." }

# --- report -----------------------------------------------------------------
$frames = @(Get-ChildItem -LiteralPath $OutDir -Filter 'frame_*.png' | Sort-Object Name)
$window = if ($null -ne $startSec) { "$startSec s -> $(if ($null -ne $endSec) { "$endSec s" } else { 'end' })" } else { 'whole clip' }

Write-Output ""
Write-Output "Window : $window  @ $Fps fps"
Write-Output "Frames : $($frames.Count) -> $OutDir"
foreach ($f in $frames) { Write-Output "         $($f.Name)" }
