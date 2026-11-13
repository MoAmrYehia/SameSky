# Builds the animated concept trailer "I Saved You a Seat" from nothing but this repository:
#
#   1. scripts/demo/trailer_look.py     the two characters' film textures (recoloured copies of their own)
#   2. scripts/demo/trailer_voices.ps1  the spoken lines, with Windows' own text-to-speech voices
#   3. Unity, batch mode                the film's own sunset clip and photos (what its screens show), then every shot,
#                                       rendered frame by frame (native/quest/Assets/SameSky/Editor/Film)
#   4. scripts/demo/trailer_audio.py    the score, the foley and the mix
#   5. scripts/demo/trailer_edit.py     the cut, subtitles, captions, end card
#
# Result: demo-build/SameSky_I_Saved_You_A_Seat_Animated_Trailer.mp4 (and the .srt in docs/trailer).
# It is an animated concept trailer, not footage of the app: see docs/trailer/README.md.
#
# Needs: Unity (the project's version, as for the app), Python with numpy, scipy and Pillow, and FFmpeg (the API's
# Python environment brings one). The Unity Editor must not have native/quest open. About six minutes on a laptop with
# a recent GPU.
#
#   powershell -File scripts/demo/trailer.ps1 [-Skip look,voices,media] [-Shots s01,s12] [-Scale 2]

[CmdletBinding()]
param(
  [string[]]$Skip = @(),        # any of: look, voices, media, shots, audio, edit
  [string]$Shots = 'all',       # which shots to render (ids from FilmShots.cs, comma separated)
  [int]$Scale = 2               # each shot is drawn this many times larger and scaled down (2 = 3840x2160 to 1920x1080)
)

. "$PSScriptRoot/../native/common.ps1"
$film = Join-Path $Repo 'demo-build/film'
New-Item -ItemType Directory -Force $film | Out-Null

function Find-Python {
  $candidates = @((Get-Command python -ErrorAction SilentlyContinue).Source, $Python) | Where-Object { $_ -and (Test-Path $_) }
  foreach ($c in $candidates) {
    & $c -c "import numpy, scipy, PIL" 2>$null
    if ($LASTEXITCODE -eq 0) { return $c }
  }
  throw 'No Python with numpy, scipy and Pillow was found (pip install numpy scipy pillow).'
}

function Invoke-Film([string]$What, [string[]]$FilmArgs) {
  if (Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 -and $_.MainWindowTitle -match '^quest - ' }) {
    throw 'The Unity Editor has native/quest open: close it first (Unity allows one editor per project).'
  }
  $log = Join-Path $film "unity-$What.log"
  Remove-Item $log -Force -ErrorAction SilentlyContinue
  $unityArgs = @('-batchmode', '-buildTarget', 'Win64', '-projectPath', "`"$Project`"", '-executeMethod', 'SameSky.Film.FilmRenderer.Run', '-logFile', "`"$log`"") + $FilmArgs
  $proc = Start-Process -FilePath $Unity -ArgumentList $unityArgs -PassThru
  $proc.WaitForExit()
  Select-String -Path $log -Pattern '^\[SameSky:film\] (video|frames|failed)' -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "    $($_.Line -replace [regex]::Escape($Repo), '.')" }
  if ($proc.ExitCode -ne 0) {
    Select-String -Path $log -Pattern 'error CS\d+|\[SameSky:film\] failed' -ErrorAction SilentlyContinue | Select-Object -First 10 | ForEach-Object { Write-Host "    $($_.Line)" -ForegroundColor Red }
    throw "Unity ended with exit code $($proc.ExitCode) while rendering the $What. Log: $log"
  }
}

# Python steps: their output is shown as it comes; a warning written to stderr must not stop the build (Windows
# PowerShell turns a native program's redirected stderr into an error), only a failing exit code does.
function Invoke-Python([string]$Script) {
  $was = $ErrorActionPreference
  $ErrorActionPreference = 'Continue'
  try { & $py (Join-Path $PSScriptRoot $Script) 2>&1 | ForEach-Object { Write-Host "    $_" } }
  finally { $ErrorActionPreference = $was }
  if ($LASTEXITCODE -ne 0) { throw "$Script failed (exit code $LASTEXITCODE)" }
}

$py = Find-Python
if ($Skip -notcontains 'look') { Write-Step 'Character textures for the film'; Invoke-Python 'trailer_look.py' }
if ($Skip -notcontains 'voices') { Write-Step 'Voices (Windows text-to-speech)'; & (Join-Path $PSScriptRoot 'trailer_voices.ps1') }
if ($Skip -notcontains 'media') { Write-Step "What the film's screens show (its own sunset clip and photos)"; Invoke-Film 'media' @('-filmShots', 'media', '-filmAction', 'frames', '-filmSize', '960x540') }
if ($Skip -notcontains 'shots') { Write-Step "The shots ($Shots, drawn at ${Scale}x)"; Invoke-Film 'shots' @('-filmShots', $Shots, '-filmAction', 'render', '-filmScale', "$Scale") }
if ($Skip -notcontains 'audio') { Write-Step 'Score, foley and mix'; Invoke-Python 'trailer_audio.py' }
if ($Skip -notcontains 'edit') { Write-Step 'The cut'; Invoke-Python 'trailer_edit.py' }
Write-Host ''
Write-Host "Done: $(Join-Path $Repo 'demo-build/SameSky_I_Saved_You_A_Seat_Animated_Trailer.mp4')"
