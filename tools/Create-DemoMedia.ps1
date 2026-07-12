[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InputPath,

    [Parameter(Mandatory = $true)]
    [TimeSpan]$StartTime,

    [Parameter(Mandatory = $true)]
    [TimeSpan]$EndTime,

    [string]$GifOutputPath = "docs\images\raw-buffer-visualizer-demo.gif",
    [string]$Mp4OutputPath = "docs\video\raw-buffer-visualizer-demo.mp4",
    [string]$FfmpegPath = "ffmpeg",
    [string]$FfprobePath = "ffprobe"
)

$ErrorActionPreference = "Stop"

function Resolve-Executable([string]$path, [string]$displayName) {
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        return (Resolve-Path -LiteralPath $path).Path
    }

    $command = Get-Command $path -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($command) {
        return $command.Source
    }

    throw "$displayName was not found. Install FFmpeg, add its bin directory to PATH, or pass -${displayName}Path with the full executable path."
}

function Resolve-OutputPath([string]$path) {
    if ([System.IO.Path]::IsPathRooted($path)) {
        return [System.IO.Path]::GetFullPath($path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $path))
}

function Invoke-CheckedProcess([string]$filePath, [string[]]$arguments, [string]$operation) {
    $output = & $filePath @arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        $detail = ($output | Out-String).Trim()
        if ([string]::IsNullOrWhiteSpace($detail)) {
            $detail = "No diagnostic output was returned."
        }

        throw "$operation failed with exit code $LASTEXITCODE.`n$detail"
    }

    return $output
}

function Get-MediaInfo([string]$ffprobe, [string]$path) {
    $arguments = @(
        "-v", "error",
        "-count_frames",
        "-select_streams", "v:0",
        "-show_entries", "stream=codec_name,width,height,avg_frame_rate,nb_frames,nb_read_frames:format=duration",
        "-of", "json",
        $path
    )
    $json = Invoke-CheckedProcess $ffprobe $arguments "ffprobe inspection"
    return (($json | Out-String) | ConvertFrom-Json)
}

function Get-AudioStreamCount([string]$ffprobe, [string]$path) {
    $arguments = @(
        "-v", "error",
        "-select_streams", "a",
        "-show_entries", "stream=index",
        "-of", "csv=p=0",
        $path
    )
    $output = Invoke-CheckedProcess $ffprobe $arguments "audio stream inspection"
    return @($output | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }).Count
}

try {
    $inputFullPath = (Resolve-Path -LiteralPath $InputPath -ErrorAction Stop).Path
    if (-not (Test-Path -LiteralPath $inputFullPath -PathType Leaf)) {
        throw "Input video is not a file: $inputFullPath"
    }

    if ($StartTime -lt [TimeSpan]::Zero) {
        throw "StartTime cannot be negative."
    }

    if ($EndTime -le $StartTime) {
        throw "EndTime must be later than StartTime."
    }

    $segmentDuration = $EndTime - $StartTime
    if ($segmentDuration.TotalSeconds -lt 1) {
        throw "The selected segment must be at least one second long."
    }

    $ffmpeg = Resolve-Executable $FfmpegPath "FFmpeg"
    $ffprobe = Resolve-Executable $FfprobePath "ffprobe"
    $sourceInfo = Get-MediaInfo $ffprobe $inputFullPath
    if (-not $sourceInfo.streams) {
        throw "The input file does not contain a readable video stream."
    }

    $sourceDuration = [double]$sourceInfo.format.duration
    if ($EndTime.TotalSeconds -gt ($sourceDuration + 0.05)) {
        throw "EndTime $EndTime exceeds source duration $([TimeSpan]::FromSeconds($sourceDuration))."
    }

    $gifFullPath = Resolve-OutputPath $GifOutputPath
    $mp4FullPath = Resolve-OutputPath $Mp4OutputPath
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $gifFullPath) | Out-Null
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $mp4FullPath) | Out-Null

    $start = $StartTime.ToString("c", [Globalization.CultureInfo]::InvariantCulture)
    $duration = $segmentDuration.ToString("c", [Globalization.CultureInfo]::InvariantCulture)

    $gifFilter = "fps=12,scale=960:-1:flags=lanczos,split[s0][s1];[s0]palettegen=max_colors=192:stats_mode=diff[p];[s1][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle[v]"
    $gifArguments = @(
        "-y", "-hide_banner", "-loglevel", "error",
        "-ss", $start, "-t", $duration, "-i", $inputFullPath,
        "-filter_complex", $gifFilter,
        "-map", "[v]", "-an", "-loop", "0",
        $gifFullPath
    )
    Invoke-CheckedProcess $ffmpeg $gifArguments "GIF conversion" | Out-Null

    $mp4Filter = "scale='min(1280\,iw)':'min(720\,ih)':force_original_aspect_ratio=decrease:flags=lanczos,scale=trunc(iw/2)*2:trunc(ih/2)*2"
    $mp4Arguments = @(
        "-y", "-hide_banner", "-loglevel", "error",
        "-ss", $start, "-t", $duration, "-i", $inputFullPath,
        "-vf", $mp4Filter,
        "-an", "-c:v", "libx264", "-preset", "medium", "-crf", "20",
        "-pix_fmt", "yuv420p", "-movflags", "+faststart",
        $mp4FullPath
    )
    Invoke-CheckedProcess $ffmpeg $mp4Arguments "MP4 conversion" | Out-Null

    $gifInfo = Get-MediaInfo $ffprobe $gifFullPath
    $mp4Info = Get-MediaInfo $ffprobe $mp4FullPath
    $gifStream = $gifInfo.streams | Select-Object -First 1
    $mp4Stream = $mp4Info.streams | Select-Object -First 1
    $gifFrames = if ($gifStream.nb_read_frames) { $gifStream.nb_read_frames } else { $gifStream.nb_frames }
    $gifDurationSeconds = [double]$gifInfo.format.duration
    $gifFps = [double]$gifFrames / $gifDurationSeconds

    if ([int]$gifStream.width -ne 960) {
        throw "Generated GIF width is $($gifStream.width), expected 960."
    }

    if ([Math]::Abs($gifFps - 12.0) -gt 0.05) {
        throw "Generated GIF frame rate is $gifFps fps, expected 12 fps."
    }

    if ([int]$mp4Stream.width -gt 1280 -or [int]$mp4Stream.height -gt 720) {
        throw "Generated MP4 exceeds 1280 x 720: $($mp4Stream.width) x $($mp4Stream.height)."
    }

    if ([string]$mp4Stream.codec_name -ne "h264") {
        throw "Generated MP4 codec is $($mp4Stream.codec_name), expected h264."
    }

    if ((Get-AudioStreamCount $ffprobe $gifFullPath) -ne 0 -or (Get-AudioStreamCount $ffprobe $mp4FullPath) -ne 0) {
        throw "Generated media unexpectedly contains an audio stream."
    }

    [pscustomobject]@{
        Segment = "$StartTime - $EndTime"
        GifPath = $gifFullPath
        GifDurationSeconds = [Math]::Round($gifDurationSeconds, 3)
        GifWidth = [int]$gifStream.width
        GifHeight = [int]$gifStream.height
        GifFrames = $gifFrames
        GifFps = [Math]::Round($gifFps, 3)
        GifBytes = (Get-Item -LiteralPath $gifFullPath).Length
        Mp4Path = $mp4FullPath
        Mp4DurationSeconds = [Math]::Round([double]$mp4Info.format.duration, 3)
        Mp4Width = [int]$mp4Stream.width
        Mp4Height = [int]$mp4Stream.height
        Mp4Codec = [string]$mp4Stream.codec_name
        Mp4Bytes = (Get-Item -LiteralPath $mp4FullPath).Length
    } | Format-List
}
catch {
    Write-Error "Create-DemoMedia failed: $($_.Exception.Message)"
    exit 1
}
