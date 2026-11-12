# Builds the demo video "I Saved You a Seat" from footage of the running app:
#
#   1. scripts/demo/trailer_voices.ps1   the narration (scripts/demo/demo_lines.json), with a Windows text-to-speech voice
#   2. scripts/demo/demo_audio.py        the sound: the app's own sound from the captured clips, narration, score
#   3. scripts/demo/demo_edit.py         the cut (scripts/demo/demo_cut.json), the film's text, the final file
#
# Result: demo-build/SameSky_I_Saved_You_A_Seat_Demo.mp4 (and the .srt in docs/demo).
#
# The footage is filmed beforehand and is not made by this script (docs/demo/README.md says how):
#   demo-build/capture/<clip>.mp4/.wav/.json   the native app in Meta XR Simulator, filmed with scripts/demo/film.ps1
#   demo-build/capture/companion/              the companion web app, recorded with web/scripts/demo-companion.mjs
#
# Needs: Python with numpy, scipy and Pillow, and FFmpeg (the API's Python environment brings one). About two minutes.
#
#   powershell -File scripts/demo/demo.ps1 [-Skip voices,audio] [-Keep] [-Only cup,payoff]

[CmdletBinding()]
param(
  [string[]]$Skip = @(),        # any of: voices, audio, edit
  [switch]$Keep,                # reuse the clips already cut (demo-build/demo/parts)
  [string]$Only = ''            # recut just these clips (ids from demo_cut.json, comma separated), then the final pass
)

. "$PSScriptRoot/../native/common.ps1"

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
function Invoke-Python([string]$Script, [string[]]$Arguments = @()) {
  $was = $ErrorActionPreference
  $ErrorActionPreference = 'Continue'
  try { & $py (Join-Path $PSScriptRoot $Script) @Arguments 2>&1 | ForEach-Object { Write-Host "    $_" } }
  finally { $ErrorActionPreference = $was }
  if ($LASTEXITCODE -ne 0) { throw "$Script failed (exit code $LASTEXITCODE)" }
}

$py = Find-Python
if ($Skip -notcontains 'voices') {
  Write-Step 'Narration (Windows text-to-speech)'
  & (Join-Path $PSScriptRoot 'trailer_voices.ps1') -Lines 'demo_lines.json' -Out 'demo-build/demo/voice'
}
if ($Skip -notcontains 'audio') { Write-Step 'Sound: the app, narration, score'; Invoke-Python 'demo_audio.py' }
if ($Skip -notcontains 'edit') {
  Write-Step 'The cut'
  $editArgs = @()
  if ($Keep) { $editArgs += '--keep' }
  if ($Only) { $editArgs += @('--only', $Only) }
  Invoke-Python 'demo_edit.py' $editArgs
}
Write-Host ''
Write-Host "Done: $(Join-Path $Repo 'demo-build/SameSky_I_Saved_You_A_Seat_Demo.mp4')"
