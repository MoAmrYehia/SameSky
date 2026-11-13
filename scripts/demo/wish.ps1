# Builds the animated film "Wish You Were Here" (75 seconds):
#
#   1. scripts/demo/wish_fetch.py       the third-party motion clips and background characters (Microsoft Rocketbox, MIT):
#      scripts/demo/wish_cast.py        downloaded once into .asset-cache, then placed where the Unity code finds them
#   2. scripts/demo/trailer_look.py     the two leads' film textures (recoloured copies of their own)
#   3. scripts/demo/trailer_voices.ps1  the five spoken lines (wish_lines.json), with Windows' own text-to-speech voices
#   4. Unity, batch mode                every shot, rendered frame by frame (native/quest/Assets/SameSky/Editor/Film/FilmWish*.cs)
#   5. scripts/demo/wish_audio.py       the score, the places, the mix
#   6. scripts/demo/wish_edit.py        the cut, the title
#
# Result: demo-build/SameSky_Wish_You_Were_Here_Cinematic.mp4 (and the .srt in docs/cinematic).
#
# Needs: Unity (the project's version), Python with numpy, scipy and Pillow, and FFmpeg (the API's Python environment
# brings one). The Unity Editor must not have native/quest open. Step 1 downloads about 200 MB the first time.
# About five minutes on a laptop with a recent GPU, once the downloads are there.
#
#   powershell -File scripts/demo/wish.ps1 [-Skip fetch,look,voices] [-Shots w01,w19] [-Scale 2]

[CmdletBinding()]
param(
  [string[]]$Skip = @(),        # any of: fetch, look, voices, shots, audio, edit
  [string]$Shots = 'wish',      # which shots to render (ids from FilmWish.cs, comma separated; 'wish' is all of them)
  [int]$Scale = 2               # each shot is drawn this many times larger and scaled down (2 = 3840x2160 to 1920x1080)
)

. "$PSScriptRoot/../native/common.ps1"
$wish = Join-Path $Repo 'demo-build/wish'
New-Item -ItemType Directory -Force (Join-Path $wish 'shots') | Out-Null

function Find-Python {
  $candidates = @((Get-Command python -ErrorAction SilentlyContinue).Source, $Python) | Where-Object { $_ -and (Test-Path $_) }
  foreach ($c in $candidates) {
    & $c -c "import numpy, scipy, PIL" 2>$null
    if ($LASTEXITCODE -eq 0) { return $c }
  }
  throw 'No Python with numpy, scipy and Pillow was found (pip install numpy scipy pillow).'
}

# Python steps: their output is shown as it comes; a warning written to stderr must not stop the build (Windows
# PowerShell turns a native program's redirected stderr into an error), only a failing exit code does.
function Invoke-Python([string]$Script, [string[]]$Before = @()) {
  $was = $ErrorActionPreference
  $ErrorActionPreference = 'Continue'
  try { & $py @Before (Join-Path $PSScriptRoot $Script) 2>&1 | ForEach-Object { Write-Host "    $_" } }
  finally { $ErrorActionPreference = $was }
  if ($LASTEXITCODE -ne 0) { throw "$Script failed (exit code $LASTEXITCODE)" }
}

function Invoke-Film([string]$What, [string[]]$FilmArgs) {
  if (Get-Process Unity -ErrorAction SilentlyContinue) {
    throw 'Unity is running: close it first (Unity allows one editor per project).'
  }
  $log = Join-Path $wish "unity-$What.log"
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

$py = Find-Python
if ($Skip -notcontains 'fetch') {
  Write-Step 'Motion clips and background characters (Microsoft Rocketbox, MIT)'
  Invoke-Python 'wish_fetch.py'
  Invoke-Python 'wish_cast.py' @('-I')      # the downloads are only data to it: Python loads nothing from their folders
}
if ($Skip -notcontains 'look') { Write-Step 'Character textures for the film'; Invoke-Python 'trailer_look.py' }
if ($Skip -notcontains 'voices') {
  Write-Step 'Voices (Windows text-to-speech)'
  & (Join-Path $PSScriptRoot 'trailer_voices.ps1') -Lines 'wish_lines.json' -Out 'demo-build/wish/voice'
}
if ($Skip -notcontains 'shots') {
  Write-Step "The shots ($Shots, drawn at ${Scale}x)"
  Invoke-Film 'shots' @('-filmShots', $Shots, '-filmAction', 'render', '-filmScale', "$Scale", '-filmOut', "`"$(Join-Path $wish 'shots')`"")
}
if ($Skip -notcontains 'audio') { Write-Step 'Score, places and mix'; Invoke-Python 'wish_audio.py' }
if ($Skip -notcontains 'edit') { Write-Step 'The cut'; Invoke-Python 'wish_edit.py' }
Write-Host ''
Write-Host "Done: $(Join-Path $Repo 'demo-build/SameSky_Wish_You_Were_Here_Cinematic.mp4')"
