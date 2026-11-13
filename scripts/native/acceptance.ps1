# Acceptance run of the native SameSky app in Meta XR Simulator (Windows). It drives the app the way a person
# does, with simulated hands, and records what the app's development probe saw.
#   powershell -ExecutionPolicy Bypass -File scripts/native/acceptance.ps1
#   -Assist                  you make each hand gesture in the simulator when asked; the script still checks the probe
#   -NonInteractive          never ask; a gesture the script can't complete is recorded as failed
#   -Target WindowsBuild     run native/quest/Builds/Windows/SameSky.exe instead of Unity Play mode
#   -SkipRecording           no MP4s (check 8 is then skipped)
#   -FingertipOffset "x,y,z" metres from the right-hand anchor to the index fingertip in the poke pose (world axes)
#   -Only 5,7                run only these checks (check 1 always runs: it calibrates the hand controls)
#   -SampleOnly              run the app as a build with no server (build.ps1 -SampleOnly, the build for the
#                            competition's judges): no API is started, check 1 also confirms that the welcome panel
#                            offers the sample day and no account buttons, and checks 5 and 9, which need the
#                            service, are skipped
#   -Renders                 also save fixed seated views of the other screens (Our day, Share, Together, Keepsakes,
#                            Settings, the story, the family circle, the marketplace and the room in two of its
#                            ready-made looks), opened through the development probe rather
#                            than by hand: pictures for comparing with the visual references, not checks
#
# Checks (results: docs/native/evidence/<date>/acceptance-<time>.json and .md; files in acceptance-<time>/):
#   1 XR session: metavr sets the simulated Meta Quest 3 and hand input; the probe shows an active OpenXR session,
#     both hands tracked and hands as the active input
#   2 a dock button pressed by a simulated hand
#   3 the cup grabbed by a simulated hand
#   4 the voice note waiting in the cup heard within the first minute of a fresh start (sample day)
#   5 a reply left on the table is still there after an app restart and after an API restart (stop.ps1, start.ps1)
#   6 recenter, pause (and resume) and exit
#   7 right-hand tracking lost mid-press and found again, with no ghost press
#   8 an MP4 of the simulator for every app session that runs checks: metavr xrsim record start/stop, or, when the
#     runtime has no recorder (207.1 hasn't), FFmpeg capturing the simulator window
#   9 the furniture marketplace: a chair fabric chosen and kept with a simulated hand (dock, tab, fabric, Keep) is
#     stored by the server and shown again after an app restart
# The first minute is timed on a fresh profile with nothing in between, so checks 3 and 4 run before check 2. The
# report lists the checks in the order above.
#
# Driving: only the simulator's own keyboard controls (its Input bindings panel; see $SimKeys). In Meta XR Simulator
# 207 the movement keys move the simulated headset and the hands follow it, so the script moves the right hand by
# moving the whole simulated person, and shapes it with the hand-action keys (poke, pinch, grab). Keys reach the
# simulator only while a mouse button (here a side button) is held in its 3D view (sim.ps1, Invoke-SimKeys).
# Input reaches the app as OpenXR hand tracking -> Meta Interaction SDK, as on a headset. Aiming uses the probe
# (probe/state.json: targets with id, position and normal; head, headForward, handsAt, pokeAt), and every step is
# confirmed in probe/events.jsonl. A check passes only with probe evidence; what can't be automated is listed as a
# manual step. The script drives the simulator window with the keyboard and mouse: don't use either while it runs.
# Live mode (checks 5 and 9) signs in Nora, a development account from dev-seed.json: the script gets her token from the
# public API and writes it to the acceptance profile's session.json before the app starts.
[CmdletBinding()]
param(
  [ValidateSet('PlayMode', 'WindowsBuild')][string]$Target = 'PlayMode',
  [string]$AppProfile = 'acceptance',
  [switch]$Assist,
  [switch]$NonInteractive,
  [switch]$SkipRecording,
  [string]$FingertipOffset = '',
  [string]$Only = '',   # e.g. 5 or 2,3,4 (a string: powershell -File passes it as one)
  [switch]$SampleOnly,
  [switch]$Renders,
  [int]$UnityStartTimeoutSec = 600,
  [int]$FirstMinuteSec = 60,
  [switch]$Library      # define everything and stop before the run: scripts/demo/film.ps1 dot-sources this file for its hand control
)

. "$PSScriptRoot/sim.ps1"
# Windows PowerShell 5.1 would otherwise write arrays as {"value": [...], "Count": n}
if ($PSVersionTable.PSVersion.Major -lt 6) { Remove-TypeData System.Array -ErrorAction SilentlyContinue }

# Meta XR Simulator 207 keys (its Input bindings panel, keyboard and mouse), as System.Windows.Forms.Keys names.
$SimKeys = [ordered]@{
  Forward = 'W'; Back = 'S'; Left = 'A'; Right = 'D'; Up = 'R'; Down = 'F'      # move the headset (head-relative); the hands follow it
  YawLeft = 'Left'; YawRight = 'Right'; PitchUp = 'Up'; PitchDown = 'Down'      # arrow keys turn the headset
  TiltLeft = 'Q'; TiltRight = 'E'                                               # tilt
  PoseDefault = 'D1'; PosePoke = 'D2'; PosePinch = 'D3'; PoseGrab = 'D4'        # hand actions 1 2 3 4: open, poke, pinch, grab (each stays until the next)
  ResetPoses = 'Space'                                                          # reset controller poses
}
$Moves = @('Forward', 'Back', 'Right', 'Left', 'Up', 'Down')
$MinHoldMs = 15
$MaxHoldMs = 1500
$SimSpeed = 0.3          # movement speed multiplier (metavr xrsim input speed): 0.3 m/s, about 5 mm for the shortest hold
$TurnDegPerSec = 80.0    # the arrow keys' turn rate (measured: 81 deg/s); loops below correct for the difference

$OnlyIds = @($Only -split '[,\s]+' | Where-Object { $_ } | ForEach-Object { [int]$_ })
# a profile of its own, so a sample-only run never attaches to an editor that was started with a server (or the reverse)
if ($SampleOnly -and -not $PSBoundParameters.ContainsKey('AppProfile')) { $AppProfile = 'acceptance-sampleonly' }
if ($SampleOnly -and $Target -ne 'PlayMode') { throw '-SampleOnly runs in Unity Play mode.' }
$ProbeDirectory = Get-ProbeDir $AppProfile
$ProfileRoot = if ($AppProfile) { Join-Path $PlayerData "profile-$AppProfile" } else { $PlayerData }
$RunStamp = Get-Date
$EvidenceDir = Join-Path $Repo ('docs/native/evidence/' + $RunStamp.ToString('yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture))
$RunName = 'acceptance-' + $RunStamp.ToString('HHmmss', [Globalization.CultureInfo]::InvariantCulture)
$RunDir = Join-Path $EvidenceDir $RunName
$UnityLog = Join-Path $DevLogs "unity-$RunName.log"
$MetavrCmd = $Metavr
if ($MetavrCmd -and $MetavrCmd -match '\.ps1$' -and (Test-Path ($MetavrCmd -replace '\.ps1$', '.cmd'))) { $MetavrCmd = $MetavrCmd -replace '\.ps1$', '.cmd' }

$script:AllEvents = New-Object System.Collections.ArrayList
$script:EventCursor = 0
$script:Session = 0
$script:SessionMark = 0
$script:SessionStartEvent = $null
$script:AppProcess = $null
$script:Checks = [ordered]@{}
$script:Setup = New-Object System.Collections.ArrayList
$script:Manual = New-Object System.Collections.ArrayList
$script:Artifacts = New-Object System.Collections.ArrayList
$script:Notes = New-Object System.Collections.ArrayList
$script:Order = New-Object System.Collections.ArrayList
$script:Segments = New-Object System.Collections.ArrayList
$script:Recording = $null
$script:Seed = $null
$script:NoraToken = $null
$script:NoraId = $null
$script:NoraEmail = $null
$script:Calib = [ordered]@{
  ok = $false; device = ''; keyVelocity = $null; axes = $null; speedsMetresPerSec = $null; resolution = 0.008
  fingertip = $null; fingertipSource = ''; fingertipLog = (New-Object System.Collections.ArrayList); headReset = $null; note = ''
}
$script:Seat = $null            # the head position after the app's start-up recenter: where the simulated person sits
$script:RecorderMode = 'metavr' # 'window' once the runtime turns out to have no MP4 recorder
$script:Ffmpeg = $null

# ---------------------------------------------------------------- vectors (probe positions are Unity world metres)
function V3($a) { if ($null -eq $a -or @($a).Count -lt 3) { return $null }; return ,([double[]]@([double]$a[0], [double]$a[1], [double]$a[2])) }
function VAdd([double[]]$a, [double[]]$b) { return ,([double[]]@(($a[0] + $b[0]), ($a[1] + $b[1]), ($a[2] + $b[2]))) }
function VSub([double[]]$a, [double[]]$b) { return ,([double[]]@(($a[0] - $b[0]), ($a[1] - $b[1]), ($a[2] - $b[2]))) }
function VScale([double[]]$a, [double]$k) { return ,([double[]]@(($a[0] * $k), ($a[1] * $k), ($a[2] * $k))) }
function VDot([double[]]$a, [double[]]$b) { return ($a[0] * $b[0] + $a[1] * $b[1] + $a[2] * $b[2]) }
function VCross([double[]]$a, [double[]]$b) { return ,([double[]]@(($a[1] * $b[2] - $a[2] * $b[1]), ($a[2] * $b[0] - $a[0] * $b[2]), ($a[0] * $b[1] - $a[1] * $b[0]))) }
function VLen([double[]]$a) { if ($null -eq $a) { return [double]::NaN }; return [math]::Sqrt((VDot $a $a)) }
function VNorm([double[]]$a) { $l = VLen $a; if (-not ($l -gt 1e-9)) { return ,([double[]]@(0, 0, 0)) }; return (VScale $a (1.0 / $l)) }
function VDiff($a, $b) { $x = V3 $a; $y = V3 $b; if ($null -eq $x -or $null -eq $y) { return $null }; return (VSub $x $y) }
function Round3($a) { if ($null -eq $a) { return $null }; return ,([double[]]@([math]::Round($a[0], 3), [math]::Round($a[1], 3), [math]::Round($a[2], 3))) }
function YawDeg([double[]]$f) { return ([math]::Atan2($f[0], $f[2]) * 180.0 / [math]::PI) }
function AngleDiff([double]$a, [double]$b) { $d = ($a - $b) % 360.0; if ($d -gt 180) { $d -= 360 } elseif ($d -lt -180) { $d += 360 }; return $d }
# Solves cols[0]*x + cols[1]*y + cols[2]*z = e (Cramer's rule); $null when the columns don't span 3D.
function Solve3([object[]]$Cols, [double[]]$e) {
  $d = VDot $Cols[0] (VCross $Cols[1] $Cols[2])
  if ([math]::Abs($d) -lt 1e-300) { return $null }
  $x = (VDot $e (VCross $Cols[1] $Cols[2])) / $d
  $y = (VDot $Cols[0] (VCross $e $Cols[2])) / $d
  $z = (VDot $Cols[0] (VCross $Cols[1] $e)) / $d
  return ,([double[]]@($x, $y, $z))
}

# ---------------------------------------------------------------- text, files and native commands
# No local paths in the evidence: the repository and user folders are replaced by placeholders.
function Protect-Text([string]$Text) {
  if (-not $Text) { return $Text }
  $t = $Text
  foreach ($p in @(@($Repo, '<repo>'), @($env:TEMP, '%TEMP%'), @($env:LOCALAPPDATA, '%LOCALAPPDATA%'), @($env:USERPROFILE, '%USERPROFILE%'))) {
    if (-not $p[0]) { continue }
    foreach ($form in @($p[0], ($p[0] -replace '\\', '/'))) { $t = [regex]::Replace($t, [regex]::Escape($form), $p[1], 'IgnoreCase') }
  }
  return $t
}

function Get-RelPath([string]$Path) {
  $full = [System.IO.Path]::GetFullPath($Path)
  $root = [System.IO.Path]::GetFullPath($Repo).TrimEnd('\', '/') + '\'
  if ($full.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { return ($full.Substring($root.Length) -replace '\\', '/') }
  return (Protect-Text $full)
}

function Write-Utf8([string]$Path, [string]$Text) { [System.IO.File]::WriteAllText($Path, $Text, (New-Object System.Text.UTF8Encoding $false)) }

# Reads a file the app may be writing at the same moment.
function Read-SharedText([string]$Path) {
  $fs = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]'ReadWrite, Delete')
  try { return (New-Object System.IO.StreamReader($fs, [System.Text.Encoding]::UTF8)).ReadToEnd() } finally { $fs.Dispose() }
}

function Format-Arg([string]$a) { if ($a -match '[\s"]') { return '"' + ($a -replace '"', '\"') + '"' }; return $a }

# Runs a program with a time limit and captures its output (stderr never stops the script).
function Invoke-Native([string]$Exe, [string[]]$ArgList, [int]$TimeoutSec = 120, [switch]$NoWait, [switch]$Raw) {
  $id = [guid]::NewGuid().ToString('N').Substring(0, 8)
  $out = Join-Path $env:TEMP "samesky-$id.out"
  $err = Join-Path $env:TEMP "samesky-$id.err"
  $line = (@($ArgList) | ForEach-Object { Format-Arg $_ }) -join ' '
  if ($Exe -match '\.(cmd|bat)$') { $file = $env:ComSpec; $argLine = '/d /s /c "' + (Format-Arg $Exe) + ' ' + $line + '"' }
  elseif ($Exe -match '\.ps1$') { $file = (Get-Process -Id $PID).Path; $argLine = '-NoProfile -ExecutionPolicy Bypass -File ' + (Format-Arg $Exe) + ' ' + $line }
  else { $file = $Exe; $argLine = $line }
  $sp = @{ FilePath = $file; RedirectStandardOutput = $out; RedirectStandardError = $err; NoNewWindow = $true; PassThru = $true }
  if ($argLine) { $sp.ArgumentList = $argLine }
  $p = Start-Process @sp
  $null = $p.Handle   # keeps ExitCode readable after the process ends (Windows PowerShell)
  $run = [pscustomobject]@{ command = (Protect-Text ((Split-Path $Exe -Leaf) + ' ' + $line)); process = $p; out = $out; err = $err; raw = [bool]$Raw }
  if ($NoWait) { return $run }
  return (Complete-Native $run $TimeoutSec)
}

function Complete-Native($Run, [int]$TimeoutSec = 60) {
  $done = $Run.process.WaitForExit($TimeoutSec * 1000)
  if (-not $done) {
    # the whole tree: a .cmd shim runs node under cmd.exe
    Start-Process -FilePath 'taskkill.exe' -ArgumentList "/PID $($Run.process.Id) /T /F" -NoNewWindow -Wait -ErrorAction SilentlyContinue
    [void]$Run.process.WaitForExit(5000)
  }
  $text = ''
  foreach ($f in @($Run.out, $Run.err)) {
    if (Test-Path $f) { $text += [string](Get-Content $f -Raw -ErrorAction SilentlyContinue); Remove-Item $f -Force -ErrorAction SilentlyContinue }
  }
  $text = $text.Trim()
  if (-not $Run.raw) { $text = Protect-Text $text; if ($text.Length -gt 3000) { $text = '...' + $text.Substring($text.Length - 3000) } }
  $exit = -1
  if ($done) { $exit = $Run.process.ExitCode }
  return [ordered]@{ command = $Run.command; exit = $exit; timedOut = (-not $done); output = $text }
}

# The FFmpeg the API already uses (imageio-ffmpeg in api/.venv).
function Get-FfmpegExe {
  if (-not $script:Ffmpeg) {
    try { $script:Ffmpeg = (Invoke-Native $Python @('-c', 'import imageio_ffmpeg; print(imageio_ffmpeg.get_ffmpeg_exe())') 60 -Raw).output.Trim() } catch { }
  }
  if ($script:Ffmpeg -and (Test-Path $script:Ffmpeg)) { return $script:Ffmpeg }
  return $null
}

function Get-MediaDuration([string]$File) {
  try {
    $ff = Get-FfmpegExe
    if (-not $ff) { return $null }
    $info = (Invoke-Native $ff @('-hide_banner', '-i', $File) 60 -Raw).output
    $m = [regex]::Match($info, 'Duration:\s*(\d+):(\d+):([\d.]+)')
    if (-not $m.Success) { return $null }
    return [math]::Round([int]$m.Groups[1].Value * 3600 + [int]$m.Groups[2].Value * 60 + [double]::Parse($m.Groups[3].Value, [Globalization.CultureInfo]::InvariantCulture), 1)
  } catch { return $null }
}

# ---------------------------------------------------------------- the app's probe
function Read-ProbeState {
  $f = Join-Path $ProbeDirectory 'state.json'
  for ($i = 0; $i -lt 8; $i++) {
    if (-not (Test-Path $f)) { return $null }
    try {
      $w1 = [System.IO.File]::GetLastWriteTimeUtc($f)
      $raw = Read-SharedText $f
      $w2 = [System.IO.File]::GetLastWriteTimeUtc($f)
      if ($w1 -eq $w2 -and $raw.TrimEnd().EndsWith('}')) {
        $s = $raw | ConvertFrom-Json
        Add-Member -InputObject $s -NotePropertyName _writtenUtc -NotePropertyValue $w1 -Force
        Add-Member -InputObject $s -NotePropertyName _raw -NotePropertyValue $raw -Force
        return $s
      }
    } catch { }
    Start-Sleep -Milliseconds 40
  }
  return $null
}

# The next snapshot written after now (the probe writes five times a second).
function Wait-NextProbe([int]$TimeoutSec = 4) {
  $after = [DateTime]::UtcNow
  $deadline = $after.AddSeconds($TimeoutSec)
  do {
    $s = Read-ProbeState
    if ($s -and $s._writtenUtc -gt $after) { return $s }
    Start-Sleep -Milliseconds 50
  } while ([DateTime]::UtcNow -lt $deadline)
  throw "The app's probe didn't update for $TimeoutSec s: the app isn't running."
}

function Wait-ProbeState([scriptblock]$Condition, [int]$TimeoutSec = 10) {
  $wpsDeadline = (Get-Date).AddSeconds($TimeoutSec)
  $wpsLast = $null
  do {
    $wpsState = Read-ProbeState
    if ($wpsState) {
      $wpsLast = $wpsState
      if (& $Condition $wpsState) { return [pscustomobject]@{ ok = $true; state = $wpsState } }
    }
    Start-Sleep -Milliseconds 200
  } while ((Get-Date) -lt $wpsDeadline)
  return [pscustomobject]@{ ok = $false; state = $wpsLast }
}

function Test-AppRunning([double]$FreshSec = 2.5) {
  $f = Join-Path $ProbeDirectory 'state.json'
  if (-not (Test-Path $f)) { return $false }
  return (([DateTime]::UtcNow - [System.IO.File]::GetLastWriteTimeUtc($f)).TotalSeconds -lt $FreshSec)
}

function Find-Target($State, [string]$Id) {
  if ($null -eq $State -or $null -eq $State.targets) { return $null }
  foreach ($t in $State.targets) { if ($t.id -eq $Id) { return $t } }
  return $null
}

function Wait-Target([string]$TargetId, [int]$TimeoutSec = 15) {
  return (Wait-ProbeState { param($st) [bool](Find-Target $st $TargetId) } $TimeoutSec).ok
}

function Select-State($State, [string[]]$Fields) {
  $o = [ordered]@{}
  if ($null -eq $State) { return $o }
  foreach ($f in $Fields) { if ($State.PSObject.Properties[$f]) { $o[$f] = $State.$f } }
  return $o
}

# events.jsonl is recreated at every app start; complete lines are read once and kept in $script:AllEvents.
function Update-Events {
  $f = Join-Path $ProbeDirectory 'events.jsonl'
  if (-not (Test-Path $f)) { return }
  try { $text = Read-SharedText $f } catch { return }
  $lines = $text -split "`n"
  $complete = $lines.Count - 1   # the last piece is '' or a line still being written
  for ($i = $script:EventCursor; $i -lt $complete; $i++) {
    $script:EventCursor = $i + 1
    $line = $lines[$i].Trim()
    if (-not $line) { continue }
    try { $e = $line | ConvertFrom-Json } catch { continue }
    Add-Member -InputObject $e -NotePropertyName seen -NotePropertyValue (Get-Date).ToString('o') -Force
    Add-Member -InputObject $e -NotePropertyName session -NotePropertyValue $script:Session -Force
    [void]$script:AllEvents.Add($e)
  }
}

function New-Mark { Update-Events; return $script:AllEvents.Count }

function Find-EventSince([int]$Mark, [string]$Kind, [string]$DetailPattern = '') {
  for ($i = $Mark; $i -lt $script:AllEvents.Count; $i++) {
    $e = $script:AllEvents[$i]
    if ($e.kind -eq $Kind -and ($DetailPattern -eq '' -or "$($e.detail)" -match $DetailPattern)) { return $e }
  }
  return $null
}

function Wait-EventSince([int]$Mark, [string]$Kind, [string]$DetailPattern = '', [double]$TimeoutSec = 10) {
  $deadline = (Get-Date).AddSeconds($TimeoutSec)
  do {
    Update-Events
    $e = Find-EventSince $Mark $Kind $DetailPattern
    if ($e) { return $e }
    Start-Sleep -Milliseconds 150
  } while ((Get-Date) -lt $deadline)
  return $null
}

function Get-EventsSince([int]$Mark) {
  if ($Mark -ge $script:AllEvents.Count) { return @() }
  return @($script:AllEvents.GetRange($Mark, $script:AllEvents.Count - $Mark))
}

# ---------------------------------------------------------------- results and evidence
function New-Check([int]$Id, [string]$Name) {
  $c = [ordered]@{
    id = $Id; name = $Name; status = 'running'; driver = ''; summary = ''
    startedAt = (Get-Date).ToString('o'); finishedAt = $null
    evidence = (New-Object System.Collections.ArrayList); notes = (New-Object System.Collections.ArrayList); manual = (New-Object System.Collections.ArrayList)
  }
  $script:Checks["$Id"] = $c
  Write-Step "Check $Id - $Name"
  return $c
}

function Add-Evidence($Check, [string]$Kind, $Data, [string]$Label = '') {
  [void]$Check.evidence.Add([ordered]@{ kind = $Kind; label = $Label; at = (Get-Date).ToString('o'); data = $Data })
}

function Select-Event($e) { return [ordered]@{ t = $e.t; kind = $e.kind; detail = $e.detail; input = $e.input; session = $e.session; seen = $e.seen } }

function Add-EventEvidence($Check, $Ev, [string]$Label) {
  if ($Ev) { Add-Evidence $Check 'probe-event' (Select-Event $Ev) $Label }
  else { Add-Evidence $Check 'missing' $null $Label }
}

function Add-Artifact([string]$Path, [string]$What) {
  if (-not (Test-Path $Path)) { return $null }
  $rel = Get-RelPath $Path
  foreach ($old in @($script:Artifacts | Where-Object { $_.file -eq $rel })) { $script:Artifacts.Remove($old) }
  [void]$script:Artifacts.Add([ordered]@{ file = $rel; what = $What; bytes = (Get-Item $Path).Length; sha256 = (Get-FileHash -Algorithm SHA256 $Path).Hash.ToLowerInvariant() })
  return $rel
}

function Save-StateFile($State, [string]$Name) {
  if ($null -eq $State) { return $null }
  $path = Join-Path $RunDir "state-$Name.json"
  Write-Utf8 $path $State._raw
  return (Add-Artifact $path "probe state: $Name")
}

function Add-StateEvidence($Check, $State, [string]$Name, [string[]]$Fields) {
  if ($null -eq $State) { Add-Evidence $Check 'missing' $null "probe state: $Name"; return }
  $file = Save-StateFile $State $Name
  Add-Evidence $Check 'probe-state' ([ordered]@{ fields = (Select-State $State $Fields); file = $file }) $Name
}

function Add-Note($Check, [string]$Text) { [void]$Check.notes.Add((Protect-Text $Text)) }

function Add-Manual($Check, [string]$Step, [string]$How) {
  $m = [ordered]@{ check = $Check.id; step = $Step; how = $How; status = 'to do' }
  [void]$Check.manual.Add($m)
  [void]$script:Manual.Add($m)
}

function Add-SetupStep([string]$Name, [bool]$Ok, $Detail) {
  [void]$script:Setup.Add([ordered]@{ step = $Name; ok = $Ok; at = (Get-Date).ToString('o'); detail = $Detail })
}

# A check passes only with probe evidence.
function Complete-Check($Check, [string]$Status, [string]$Summary = '') {
  if ($Status -eq 'pass' -and @($Check.evidence | Where-Object { $_.kind -like 'probe-*' }).Count -eq 0) {
    $Status = 'fail'
    Add-Note $Check 'No probe evidence was recorded, so this check cannot pass.'
  }
  $Check.status = $Status
  if ($Summary) { $Check.summary = (Protect-Text $Summary) }
  $Check.finishedAt = (Get-Date).ToString('o')
  $color = 'Red'
  if ($Status -eq 'pass') { $color = 'Green' } elseif ($Status -eq 'manual' -or $Status -eq 'skipped') { $color = 'Yellow' }
  Write-Host ('    check {0}: {1}. {2}' -f $Check.id, $Status.ToUpperInvariant(), $Check.summary) -ForegroundColor $color
}

function Save-Shot([string]$Name) {
  try {
    $p = Join-Path $RunDir "sim-$Name.png"
    [void](Save-SimShot $p)
    return (Add-Artifact $p "simulator view: $Name")
  } catch { [void]$script:Notes.Add("Screenshot '$Name' failed: $(Protect-Text $_.Exception.Message)"); return $null }
}

# A fixed render from the seated eye (the app's probe draws it with its own camera, whatever the simulated headset is
# doing): the same framing every run, to compare with the visual references. Yaw and pitch in degrees from the
# seat's forward; negative pitch looks down.
function Save-SeatView([string]$Name, [double]$Yaw = 0, [double]$Pitch = -12, [string]$What = '', [string]$Open = '', [double]$Wait = 1.4, [double]$Fov = 60, [string]$View = '') {
  try {
    $png = Join-Path $ProbeDirectory "snap-$Name.png"
    Remove-Item -Force -ErrorAction SilentlyContinue $png
    $req = [ordered]@{ name = $Name; yaw = $Yaw; pitch = $Pitch; fov = $Fov; width = 1600; height = 900 }
    # -Open: the probe opens that screen itself first (development shortcut, SameSkyApp.DevOpen), then waits, then draws
    if ($Open) { $req.open = $Open; $req.wait = $Wait }
    # -View hands|palms: a close-up of the tracked hands from where the head is, or from the other side
    if ($View) { $req.view = $View }
    Write-Utf8 (Join-Path $ProbeDirectory 'snap-request.json') ($req | ConvertTo-Json -Compress)
    # in the Editor the first render of a view can wait a while for its shaders to compile (TestProbe.Snap)
    $deadline = (Get-Date).AddSeconds(50 + $(if ($Open) { $Wait } else { 0 }))
    while (-not (Test-Path $png) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 250 }
    if (-not (Test-Path $png)) { [void]$script:Notes.Add("Seated view '$Name' wasn't rendered (no snap-$Name.png from the probe)."); return $null }
    Start-Sleep -Milliseconds 300   # let the app finish writing it
    $dest = Join-Path $RunDir "seat-$Name.png"
    Copy-Item -Force $png $dest
    $label = if ($What) { "seated view (fixed camera): $What" } else { "seated view (fixed camera): $Name" }
    return (Add-Artifact $dest $label)
  } catch { [void]$script:Notes.Add("Seated view '$Name' failed: $(Protect-Text $_.Exception.Message)"); return $null }
}

# -Renders: fixed seated views of the screens the checks don't visit, for comparing with the visual references. The
# probe opens each screen directly (no hands), so these are pictures of what the app draws, not interaction checks.
function Invoke-RenderTour {
  Write-Step 'Render tour - fixed seated views of the other screens (opened through the development probe)'
  $inSample = $false
  try { $st = Wait-NextProbe; $inSample = (Test-AppRunning) -and "$($st.mode)" -eq 'sample' -and "$($st.screen)" -ne 'launch' } catch { }
  if (-not $inSample) {
    [void](Start-App -Record 'renders')
    if (-not (Wait-Target 'launch:sample' 30)) { [void]$script:Notes.Add('Render tour: the launch screen never appeared.'); return }
    [void](Save-SeatView 'tour-launch' 0 -9 'the launch screen')
    [void](Save-SeatView 'tour-room' 0 -14 'the sample room' 'sample' 4)
  }
  $views = @(
    # in the room, panels lie low at the table edge, so these look further down than the room views do
    @{ n = 'day';       open = 'day';       yaw = 0;   pitch = -24; wait = 2.5; what = 'Our day (reference 05)' },
    @{ n = 'share';     open = 'share';     yaw = 0;   pitch = -24; wait = 2.5; what = 'Share: who receives it, and the AI permission (reference 06)' },
    @{ n = 'together';  open = 'together';  yaw = 0;   pitch = -24; wait = 2.5; what = 'Together: places, times and the next overlap (reference 08)' },
    @{ n = 'keepsakes'; open = 'keepsakes'; yaw = 0;   pitch = -24; wait = 2.5; what = 'Keepsakes (reference 09)' },
    @{ n = 'shelf';     open = 'room';      yaw = -72; pitch = -3;  wait = 2.5; what = 'the keepsake shelf in the alcove (reference 09)' },
    @{ n = 'avatar';    open = 'avatar';    yaw = 0;   pitch = -24; wait = 2.5; what = 'the avatar preview, front, three-quarter and side (references 01 and 13)' },
    @{ n = 'settings';  open = 'settings';  yaw = 0;   pitch = -24; wait = 2;   what = 'Settings: captions, text size, sound, recenter (reference 12)' },
    @{ n = 'pause';     open = 'pause';     yaw = 0;   pitch = -24; wait = 2;   what = 'Paused (reference 12)' },
    @{ n = 'story';     open = 'story';     yaw = -14; pitch = -12; wait = 6;   what = 'the story on the screen beside the partner (references 04 and 05)' },
    @{ n = 'rooms';     open = 'rooms';     yaw = 0;   pitch = -24; wait = 2;   what = 'the room selector (reference 07)' },
    @{ n = 'circle';    open = 'circle';    yaw = 0;   pitch = -10; wait = 4;   what = 'the family circle room (reference 07)' },
    @{ n = 'direct';    open = 'direct';    yaw = 0;   pitch = -10; wait = 4;   what = 'back in the direct room (reference 02)' },
    @{ n = 'partner';   open = '';          yaw = 0;   pitch = -1;  wait = 0;   fov = 22; what = "the partner's face, close (natural proportions, eyes, expression: reference 01)" },
    @{ n = 'left';      open = '';          yaw = -48; pitch = -8;  wait = 0;   what = 'the room to the left: window light and shadows on the wall and floor' },
    @{ n = 'right';     open = '';          yaw = 46;  pitch = -8;  wait = 0;   what = 'the room to the right: the lamp and side table' },
    @{ n = 'reply';     open = 'reply';     yaw = 0;   pitch = -26; wait = 2;   what = 'replying on the keyboard at the table edge (reference 03)' },
    # the furniture marketplace: its tabs, then the room in two of its ready-made looks (tried, not kept)
    @{ n = 'market';        open = 'market';              yaw = 0;   pitch = -26; wait = 3;   what = 'Marketplace: ready-made looks' },
    @{ n = 'market-table';  open = 'market:table';        yaw = 0;   pitch = -26; wait = 3;   what = 'Marketplace: tables and their finishes' },
    @{ n = 'market-chairs'; open = 'market:chairs';       yaw = 0;   pitch = -26; wait = 3;   what = 'Marketplace: chair fabrics' },
    @{ n = 'market-lamp';   open = 'market:lamp';         yaw = 0;   pitch = -26; wait = 3;   what = 'Marketplace: lamps' },
    @{ n = 'market-walls';  open = 'market:walls';        yaw = 0;   pitch = -26; wait = 3;   what = 'Marketplace: wall colours' },
    @{ n = 'look-coastal';  open = 'look:coastal-oak';    yaw = 0;   pitch = -10; wait = 2.5; what = 'the room in the Coastal oak look' },
    @{ n = 'look-coastal-left';  open = '';               yaw = -48; pitch = -8;  wait = 0;   what = 'Coastal oak: the wall, rug and plant to the left' },
    @{ n = 'look-coastal-right'; open = '';               yaw = 46;  pitch = -8;  wait = 0;   what = 'Coastal oak: the tripod lamp to the right' },
    @{ n = 'look-evening';  open = 'look:evening-study';  yaw = 0;   pitch = -10; wait = 2.5; what = 'the room in the Evening study look' },
    @{ n = 'look-evening-right'; open = '';               yaw = 46;  pitch = -8;  wait = 0;   what = 'Evening study: the globe lamp to the right' },
    @{ n = 'hands-panel';   open = 'hands';               yaw = 0;   pitch = -24; wait = 2;   what = 'Your hands: the skin tone choice' },
    @{ n = 'direct-again';  open = 'room';                yaw = 0;   pitch = -10; wait = 3;   what = 'back in the room as it is kept' }
  )
  $saved = 0
  foreach ($v in $views) {
    $fov = if ($v.ContainsKey('fov')) { $v.fov } else { 60 }
    if (Save-SeatView ('tour-' + $v.n) $v.yaw $v.pitch $v.what $v.open $v.wait $fov) { $saved++ }
  }
  Write-Host "    render tour: $saved of $($views.Count) views saved (seat-tour-*.png)"
}

function Save-SessionFiles {
  if ($script:Session -lt 1) { return }
  Update-Events
  $ev = Join-Path $ProbeDirectory 'events.jsonl'
  if (-not (Test-Path $ev)) { return }
  $dest = Join-Path $RunDir ('events-session{0}.jsonl' -f $script:Session)
  try { Write-Utf8 $dest (Read-SharedText $ev); [void](Add-Artifact $dest "probe events, app session $($script:Session)") } catch { }
}

# ---------------------------------------------------------------- app sessions (Unity Play mode or the Windows build)
function Get-UnityEditor {
  $want = ($Project -replace '/', '\').TrimEnd('\')
  foreach ($p in @(Get-Process -Name Unity -ErrorAction SilentlyContinue)) {
    if ($p.MainWindowHandle -eq [IntPtr]::Zero) { continue }   # import workers have no window
    $cmd = (Get-CimInstance Win32_Process -Filter "ProcessId=$($p.Id)" -ErrorAction SilentlyContinue).CommandLine
    if (-not $cmd -or ($cmd -replace '/', '\') -notlike "*$want*") { continue }
    $ours = if ($AppProfile) { $cmd -match ('-sameskyProfile=' + [regex]::Escape($AppProfile) + '(\s|"|$)') } else { $cmd -notmatch '-sameskyProfile=' }
    return [pscustomobject]@{ Process = $p; OurProfile = [bool]$ours }
  }
  return $null
}

function Start-UnityEditor {
  Clear-UnitySceneBackups
  $env:XR_RUNTIME_JSON = $XrsimRuntimeJson   # the simulator for this editor process only
  $unityArgs = @('-projectPath', "`"$Project`"", '-executeMethod', 'SameSky.EditorTools.SimRun.Play', '-logFile', "`"$UnityLog`"", "-sameskyApi=$ApiUrl")
  if ($AppProfile) { $unityArgs += "-sameskyProfile=$AppProfile" }
  if ($SampleOnly) { $unityArgs += '-sameskySampleOnly' }   # AppConfig: no API address, as in a build made with -SampleOnly
  Start-Process -FilePath $Unity -ArgumentList $unityArgs | Out-Null
}

# Ctrl+P in the editor enters or leaves Play mode. Only the fallback now: see Request-UnityPlayMode.
function Send-UnityPlayToggle {
  $u = Get-UnityEditor
  if (-not $u) { throw 'The Unity Editor with native/quest is not running.' }
  [void](Focus-Window $u.Process)
  Start-Sleep -Milliseconds 400
  [SimInput]::KeyDown(0x11)
  Start-Sleep -Milliseconds 40
  [SimInput]::Key(0x50, 60)
  Start-Sleep -Milliseconds 40
  [SimInput]::KeyUp(0x11)
}

# Play mode in a running editor is asked for through a file. The editor script (Assets/SameSky/Editor/SimRun.cs) reads
# Temp/SameSkySim/request.txt on its own tick and answers in ack.txt, so it works whichever window has the keyboard.
# Until 12 November this script pressed Ctrl+P instead; that day, with a Preferences window left open in the editor,
# the editor acted on the keys only after the script had ended, and two runs couldn't start their sessions.
$UnityRequestDir = Join-Path $Project 'Temp/SameSkySim'
$UnityAnswers = @('starting', 'queued', 'stopping', 'already', 'unknown')

function Read-UnityAnswer([string]$Id) {
  $ack = Join-Path $UnityRequestDir 'ack.txt'
  if (-not (Test-Path $ack)) { return $null }
  try { $a = @((Read-SharedText $ack).Trim() -split ' ') } catch { return $null }
  if ($a.Count -ge 3 -and $a[0] -eq $Id -and $UnityAnswers -contains $a[2]) { return $a[2] }
  return $null
}

# Returns what the editor did ('starting', 'queued', 'stopping', 'already'), 'taken' when it took the request but its
# answer couldn't be read, or $null when it didn't react within $WaitSec (the request is then withdrawn).
function Send-UnityRequest([string]$Action, [double]$WaitSec = 10) {
  $req = Join-Path $UnityRequestDir 'request.txt'
  $tmp = Join-Path $UnityRequestDir 'request.tmp'
  $id = [guid]::NewGuid().ToString('N')
  try {
    New-Item -ItemType Directory -Force $UnityRequestDir | Out-Null
    Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $UnityRequestDir 'ack.txt'), $req, $tmp
    Write-Utf8 $tmp "$Action $id"
    [System.IO.File]::Move($tmp, $req)   # whole or not at all: the editor never reads half a request
  } catch { return $null }
  $deadline = (Get-Date).AddSeconds($WaitSec)
  do {
    Start-Sleep -Milliseconds 150
    $did = Read-UnityAnswer $id
    if ($did) { return $did }
  } while ((Get-Date) -lt $deadline)
  if (-not (Test-Path $req)) { return 'taken' }
  Remove-Item -Force -ErrorAction SilentlyContinue $req
  Start-Sleep -Milliseconds 400   # the editor may have read it in the same instant
  return (Read-UnityAnswer $id)
}

# 'play' or 'stop'. Returns the editor's answer, or 'keys' when it gave none and Ctrl+P was pressed instead.
function Request-UnityPlayMode([string]$Action, [double]$WaitSec = 10) {
  $u = Get-UnityEditor
  if (-not $u) { throw 'The Unity Editor with native/quest is not running.' }
  [void](Focus-Window $u.Process)   # in front, the editor also imports changed assets and compiles changed scripts
  $did = Send-UnityRequest $Action $WaitSec
  if ($did) { return $did }
  if (-not $script:PlayKeysNoted) {
    $script:PlayKeysNoted = $true
    [void]$script:Notes.Add("The editor didn't answer a Play mode request (Temp/SameSkySim/request.txt) within $WaitSec s; Ctrl+P was pressed instead.")
  }
  $running = Test-AppRunning
  if (($Action -eq 'play' -and -not $running) -or ($Action -eq 'stop' -and $running)) { Send-UnityPlayToggle }
  return 'keys'
}

# No app a minute and a half after Play mode was asked for: ask the editor again. If it answers that it is already
# entering Play mode, it is waiting on the simulator (OpenXR start-up blocks until the runtime answers), so the
# simulator is stopped: the OpenXR runtime inside the editor starts a fresh one when it finds none.
function Invoke-StartNudge([int]$AfterSec) {
  $again = Send-UnityRequest 'play' 10
  $what = if ($again) { "the editor answered '$again'" } else { "the editor didn't answer" }
  if ($again -eq 'already') {
    $sims = @(Get-Process -Name MetaXRSimulator -ErrorAction SilentlyContinue)
    foreach ($p in $sims) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Seconds 3
    $what += ", so it was waiting on the simulator; the simulator was stopped ($($sims.Count) process) for the OpenXR runtime to start a fresh one"
  }
  [void]$script:Notes.Add("App session $($script:Session) hadn't started after $AfterSec s: $what.")
  Write-Host "    no app after $AfterSec s: $what" -ForegroundColor Yellow
}

function Reset-AppProfile {
  foreach ($n in 'prefs.json', 'session.json', 'sample-state.json') { Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $ProfileRoot $n) }
  Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $ProbeDirectory
}

# Live mode: Nora's development account, signed in the way the app stores a session, in the room with Sam's note.
function Set-LiveSession {
  New-Item -ItemType Directory -Force $ProfileRoot | Out-Null
  $session = [ordered]@{ token = $script:NoraToken; userId = $script:NoraId; email = $script:NoraEmail; apiUrl = $ApiUrl.TrimEnd('/'); savedAt = [DateTime]::UtcNow.ToString('o') }
  Write-Utf8 (Join-Path $ProfileRoot 'session.json') ($session | ConvertTo-Json)
  $prefsPath = Join-Path $ProfileRoot 'prefs.json'
  $prefs = New-Object psobject
  if (Test-Path $prefsPath) { $prefs = Get-Content $prefsPath -Raw | ConvertFrom-Json }
  Add-Member -InputObject $prefs -NotePropertyName lastRoomId -NotePropertyValue $script:Seed.rooms.pair -Force
  Add-Member -InputObject $prefs -NotePropertyName setupStep -NotePropertyValue $null -Force
  Write-Utf8 $prefsPath ($prefs | ConvertTo-Json -Depth 5)
}

function Start-App([switch]$FreshProfile, [string]$Record = '') {
  if (Test-AppRunning) { Stop-App }
  if ($FreshProfile) { Reset-AppProfile }
  Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $ProbeDirectory 'events.jsonl'), (Join-Path $ProbeDirectory 'state.json')
  $script:Session = $script:Session + 1
  $script:EventCursor = 0
  $script:SessionMark = $script:AllEvents.Count
  $timeout = 180
  $asked = $null       # how Play mode was asked for in this session (the running editor's answer, or 'launched')
  $nudgeSec = 90       # with no app after this long, the editor is asked again (Invoke-StartNudge)
  $toFront = $false    # a newly launched editor is brought to the front once its window is there
  if ($Target -eq 'WindowsBuild') {
    $script:AppProcess = Start-SameSkyInSim -Api $ApiUrl -Profile $AppProfile
    $timeout = 120
  } else {
    $u = Get-UnityEditor
    if (-not $u) {
      Start-UnityEditor
      $timeout = $UnityStartTimeoutSec
      # Opening the project, compiling and entering Play mode took about 70 s here; four minutes without an app
      # means the launch's own play request (-executeMethod) got lost, and the request file can still make it.
      $asked = 'launched'; $nudgeSec = 240; $toFront = $true
      Write-Host "    Unity is opening native/quest and entering Play mode (up to $timeout s)"
    } else {
      # the first start of a run can find changed assets and scripts: the editor imports and recompiles before it
      # plays (and before it reads the request, when the request reader itself is among the changed scripts)
      if ($script:Session -eq 1) { $timeout = 420 }
      $asked = Request-UnityPlayMode 'play' $(if ($script:Session -eq 1) { 90 } else { 10 })
    }
  }
  $start = $null
  $deadline = (Get-Date).AddSeconds($timeout)
  $nudgeAt = (Get-Date).AddSeconds($nudgeSec)
  while (-not $start -and (Get-Date) -lt $deadline) {
    $start = Wait-EventSince $script:SessionMark 'start' '' 5
    if ($start) { break }
    if ($toFront) {
      # Windows doesn't hand the foreground to a program that a background script started, and a Unity editor that
      # isn't in front leaves some of its work waiting (EditorApplication.delayCall, for one).
      $u = Get-UnityEditor
      if ($u) { $toFront = $false; [void](Focus-Window $u.Process) }
    }
    if ($asked -and (Get-Date) -ge $nudgeAt) { $asked = $null; Invoke-StartNudge $nudgeSec }
  }
  if (-not $start) {
    $script:AppStartFailures = $script:AppStartFailures + 1
    throw "The app didn't start within $timeout s (no 'start' event in the probe). Unity log: $(Get-RelPath $UnityLog)"
  }
  $script:SessionStartEvent = $start
  [void](Wait-ProbeState { param($st) $st.screen -eq 'launch' } 40)
  Set-SimWindowSize   # the simulator shrinks its window a little at every new session
  [void](Focus-Sim)   # in front of the editor: the keys and the window recording need it
  # The app's start-up recenter has just put the head on the seat (and turned the room to face it).
  $st = Wait-NextProbe
  if (-not $script:Seat) { $script:Seat = V3 $st.head }
  # The editor had the foreground: give the simulator's 3D view the keyboard again (sim.ps1, Enable-SimKeys). The
  # simulator also keeps its headset pose between sessions, so level the head and put it on the seat before aiming.
  if (-not $Assist) {
    Enable-SimKeys
    $script:LastHeadReset = Reset-SimHead $script:Seat
  }
  Write-Host ('    app session {0} started' -f $script:Session)
  if ($Record) { Start-Recording ('session{0}-{1}' -f $script:Session, $Record) }
  return $start
}

function Stop-App {
  Stop-Recording
  Save-SessionFiles
  if ($Target -eq 'WindowsBuild') {
    if ($script:AppProcess -and -not $script:AppProcess.HasExited) { Stop-Process -Id $script:AppProcess.Id -Force -ErrorAction SilentlyContinue }
  } elseif (Test-AppRunning) { [void](Request-UnityPlayMode 'stop') }
  $deadline = (Get-Date).AddSeconds(60)
  while ((Test-AppRunning) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 300 }
  if (Test-AppRunning) { throw 'The app did not stop.' }
  Start-Sleep -Milliseconds 500
}

function Get-UnityRuntimeLine {
  if ($Target -ne 'PlayMode' -or -not (Test-Path $UnityLog)) { return $null }
  try { $text = Read-SharedText $UnityLog } catch { return $null }
  $m = [regex]::Matches($text, '\[SameSky:sim\] play mode; XR_RUNTIME_JSON=[^\r\n]*')
  if ($m.Count -eq 0) { return $null }
  return (Protect-Text $m[$m.Count - 1].Value)
}

# ---------------------------------------------------------------- MP4 of the simulator (check 8)
# metavr xrsim record captures the runtime's compositor. A runtime built without the recording service (207.1 is)
# makes metavr say so and suggest a screen recorder: from then on FFmpeg records the simulator window instead
# (gdigrab of its screen area). While a recording runs the simulator window is kept above every other window, so
# nothing can cover the area being recorded. One thing that doesn't help with: when the app leaves, the simulator
# closes its own window, and until the recorder stops the area shows what was behind it. A session that ends with
# Leave (check 6) therefore ends with two or three seconds of the Unity editor (runs 125054 and 130714).
function Start-WindowRecorder([string]$File) {
  $ff = Get-FfmpegExe
  $w = Get-SimWindow
  if (-not $ff -or -not $w) { return $null }
  try { [SimInput]::Topmost($w.MainWindowHandle, $true) } catch { }
  $r = [SimInput]::Frame($w.MainWindowHandle)
  $width = $r.Width - ($r.Width % 2); $height = $r.Height - ($r.Height % 2)   # even sizes for yuv420p
  $psi = New-Object System.Diagnostics.ProcessStartInfo $ff
  $psi.Arguments = "-hide_banner -loglevel error -nostats -f gdigrab -framerate 15 -draw_mouse 0 -offset_x $($r.Left) -offset_y $($r.Top) -video_size ${width}x$height -i desktop -c:v libx264 -preset veryfast -crf 28 -pix_fmt yuv420p -movflags +faststart -y `"$File`""
  $psi.UseShellExecute = $false
  $psi.CreateNoWindow = $true
  $psi.RedirectStandardInput = $true
  $psi.RedirectStandardError = $true
  $p = [System.Diagnostics.Process]::Start($psi)
  return [pscustomobject]@{ process = $p; window = $w.MainWindowHandle; errors = $p.StandardError.ReadToEndAsync(); command = "ffmpeg -f gdigrab (simulator window, ${width}x$height at 15 fps) -c:v libx264 $(Split-Path $File -Leaf)" }
}

function Stop-WindowRecorder($Rec) {
  $p = $Rec.process
  if (-not $p.HasExited) {
    try { $p.StandardInput.Write('q'); $p.StandardInput.Flush() } catch { }   # q: finish the file properly
    if (-not $p.WaitForExit(20000)) { try { $p.Kill() } catch { }; [void]$p.WaitForExit(5000) }
  }
  try { [SimInput]::Topmost($Rec.window, $false) } catch { }   # the window may be gone already: the simulator can leave with the app
  $err = ''
  try { $err = Protect-Text ([string]$Rec.errors.Result).Trim() } catch { }
  return [ordered]@{ command = $Rec.command; exit = $(if ($p.HasExited) { $p.ExitCode } else { -1 }); timedOut = $false; output = $err }
}

function Start-Recording([string]$Label) {
  if ($SkipRecording -or $script:Recording) { return }
  $file = Join-Path $RunDir "$Label.mp4"
  $s = Read-ProbeState
  $rec = [ordered]@{
    label = $Label; file = $file; method = ''; run = $null; window = $null; metavrRefused = $null; startedAt = (Get-Date).ToString('o')
    probeAtStart = (Select-State $s @('xrDeviceActive', 'openxrRuntime', 'screen', 'mode', 'activeInput', 'hands', 'time'))
  }
  if ($script:RecorderMode -eq 'metavr' -and $MetavrCmd) {
    $run = Invoke-Native $MetavrCmd @('xrsim', 'record', 'start', '-o', $file) -NoWait   # may keep running until stop
    [void]$run.process.WaitForExit(8000)   # a runtime without a recorder says so within a few seconds
    if ($run.process.HasExited -and $run.process.ExitCode -ne 0) {
      $r0 = Complete-Native $run 5
      if ($r0.output -match 'does not support native MP4 recording|recording service') {
        $script:RecorderMode = 'window'
        $rec.metavrRefused = $r0
        [void]$script:Notes.Add("metavr xrsim record: $($r0.output) The recordings are FFmpeg captures of the simulator window instead.")
      } else { $rec.method = 'metavr'; $rec.run = [pscustomobject]@{ done = $r0 } }
    } else { $rec.method = 'metavr'; $rec.run = $run }
  }
  if (-not $rec.method) {
    $rec.window = Start-WindowRecorder $file
    if ($rec.window) { $rec.method = 'window' } else { $rec.method = 'none' }
  }
  $script:Recording = $rec
}

function Stop-Recording {
  $r = $script:Recording
  if (-not $r) { return }
  $script:Recording = $null
  $s = Read-ProbeState
  $start = $null; $stop = $null
  switch ($r.method) {
    'metavr' {
      $stop = Invoke-Native $MetavrCmd @('xrsim', 'record', 'stop') 90
      if ($r.run.done) { $start = $r.run.done } else { $start = Complete-Native $r.run 30 }
    }
    'window' { $start = [ordered]@{ command = $r.window.command; exit = 0; timedOut = $false; output = '' }; $stop = Stop-WindowRecorder $r.window }
    default { $start = $r.metavrRefused; $stop = [ordered]@{ command = 'none'; exit = -1; timedOut = $false; output = 'no recorder: metavr has no recording service and FFmpeg or the simulator window was not found' } }
  }
  $size = -1
  for ($i = 0; $i -lt 40; $i++) {   # until the file stops growing
    if (Test-Path $r.file) { $n = (Get-Item $r.file).Length; if ($n -gt 0 -and $n -eq $size) { break }; $size = $n }
    Start-Sleep -Milliseconds 500
  }
  $bytes = 0
  if (Test-Path $r.file) { $bytes = (Get-Item $r.file).Length }
  $duration = $null
  if ($bytes -gt 0) { $duration = Get-MediaDuration $r.file }
  $seg = [ordered]@{
    label = $r.label; file = (Get-RelPath $r.file); method = $r.method; bytes = $bytes; durationSec = $duration
    # a playable file: the recorder finished cleanly and the video has a length (short sessions make small files)
    ok = ($bytes -gt 10KB -and $stop.exit -eq 0 -and $null -ne $duration -and $duration -ge 1); startCommand = $start; stopCommand = $stop; metavrRefused = $r.metavrRefused
    startedAt = $r.startedAt; stoppedAt = (Get-Date).ToString('o'); probeAtStart = $r.probeAtStart
    probeAtStop = (Select-State $s @('xrDeviceActive', 'screen', 'mode', 'activeInput', 'time'))
  }
  $what = 'simulator recording (metavr xrsim record)'
  if ($r.method -eq 'window') { $what = 'simulator window recording (FFmpeg)' }
  if ($bytes -gt 0) { [void](Add-Artifact $r.file "$($what): $($r.label)") }
  [void]$script:Segments.Add($seg)
}

# ---------------------------------------------------------------- simulated hands
function Set-HandPose([string]$Pose) { Send-SimKey $SimKeys[$Pose] 60; Start-Sleep -Milliseconds 250 }

function Send-Moves([System.Collections.IDictionary]$Holds) {
  if ($Holds.Count -eq 0) { return }
  $byKey = [ordered]@{}
  foreach ($m in @($Holds.Keys)) { $byKey[$SimKeys[$m]] = $Holds[$m] }
  Send-SimChord $byKey
}

# How far one key hold moves the head and each hand (world metres).
function Measure-Move([string]$Move, [int]$HoldMs) {
  $s0 = Wait-NextProbe
  Send-SimKey $SimKeys[$Move] $HoldMs
  Start-Sleep -Milliseconds 100
  $s1 = Wait-NextProbe
  return [pscustomobject]@{ head = (VDiff $s1.head $s0.head); right = (VDiff $s1.handsAt.right $s0.handsAt.right); left = (VDiff $s1.handsAt.left $s0.handsAt.left) }
}

function Get-MoveKind($m) {
  if ($null -eq $m -or $null -eq $m.head -or $null -eq $m.right -or $null -eq $m.left) { return 'none' }
  $eps = 0.006
  $h = VLen $m.head; $r = VLen $m.right; $l = VLen $m.left
  if ($h -gt $eps) {
    if ($r -gt $eps -and (VLen (VSub $m.right $m.head)) -lt 0.01) { return 'body' }   # hands follow the head
    return 'head'
  }
  if ($r -gt $eps -and $l -le $eps) { return 'right' }
  if ($l -gt $eps -and $r -le $eps) { return 'left' }
  if ($l -gt $eps -and $r -gt $eps) { return 'hands' }
  return 'none'
}

# What a movement key moves: in simulator 207 the headset, with the hands following it ('body').
function Get-SimMoveKind {
  $m = Measure-Move 'Right' 250
  [void](Measure-Move 'Left' 250)
  $kind = Get-MoveKind $m
  $script:Calib.device = $kind
  return $kind
}

function PitchDeg([double[]]$f) { return ([math]::Asin([math]::Max(-1.0, [math]::Min(1.0, $f[1]))) * 180.0 / [math]::PI) }

function Get-TurnHold([double]$Degrees) { return [int][math]::Max([double]$MinHoldMs, [math]::Min([double]$MaxHoldMs, [math]::Abs($Degrees) / $TurnDegPerSec * 850.0)) }

# Levels the simulated head, turns it to face the table and puts it back on the seat (the head position the app's
# start-up recenter gave it): arrow and movement keys in a closed loop on the probe.
function Reset-SimHead([double[]]$Seat) {
  for ($i = 0; $i -lt 8; $i++) {
    $p = PitchDeg (V3 (Wait-NextProbe).headForward)
    if ([math]::Abs($p) -lt 1.0) { break }
    $key = $SimKeys.PitchDown
    if ($p -lt 0) { $key = $SimKeys.PitchUp }
    Send-SimKey $key (Get-TurnHold $p)
  }
  for ($i = 0; $i -lt 8; $i++) {
    $y = YawDeg (V3 (Wait-NextProbe).headForward)
    if ([math]::Abs($y) -lt 1.0) { break }
    $key = $SimKeys.YawRight
    if ($y -gt 0) { $key = $SimKeys.YawLeft }
    Send-SimKey $key (Get-TurnHold $y)
  }
  if ($Seat) {
    for ($i = 0; $i -lt 12; $i++) {   # facing the table: Right is +x, Up +y, Forward +z
      $e = VSub $Seat (V3 (Wait-NextProbe).head)
      if ((VLen $e) -lt 0.01) { break }
      $holds = [ordered]@{}
      foreach ($ax in @(@(0, 'Right', 'Left'), @(1, 'Up', 'Down'), @(2, 'Forward', 'Back'))) {
        $d = $e[$ax[0]]
        if ([math]::Abs($d) -lt 0.004) { continue }
        $move = $ax[2]
        if ($d -gt 0) { $move = $ax[1] }
        $holds[$move] = [int][math]::Max([double]$MinHoldMs, [math]::Min([double]$MaxHoldMs, [math]::Abs($d) / $SimSpeed * 900.0))
      }
      Send-Moves $holds
    }
  }
  $s = Wait-NextProbe
  $f = V3 $s.headForward
  $err = $null
  if ($Seat) { $err = [math]::Round((VLen (VSub $Seat (V3 $s.head))), 3) }
  return [ordered]@{ yaw = [math]::Round((YawDeg $f), 1); pitch = [math]::Round((PitchDeg $f), 1); seatError = $err }
}

# World velocity of the right hand for each movement key (metres per millisecond of hold).
function Measure-KeyVectors {
  $v = [ordered]@{}
  foreach ($k in $Moves) {
    $m = Measure-Move $k 300
    if ($null -eq $m.right) { return $false }
    $v[$k] = VScale $m.right (1.0 / 300)
  }
  $axes = @((VScale (VSub $v.Forward $v.Back) 0.5), (VScale (VSub $v.Right $v.Left) 0.5), (VScale (VSub $v.Up $v.Down) 0.5))
  $lens = @($axes | ForEach-Object { VLen $_ })
  $det = VDot $axes[0] (VCross $axes[1] $axes[2])
  $spread = [math]::Abs($det) / ($lens[0] * $lens[1] * $lens[2] + 1e-30)
  $fastest = ($lens | Measure-Object -Maximum).Maximum
  $script:Calib.keyVelocity = $v
  $script:Calib.axes = $axes
  $script:Calib.resolution = [math]::Max(0.008, $fastest * $MinHoldMs * 1.2)
  $script:Calib.speedsMetresPerSec = [ordered]@{}
  foreach ($k in $Moves) { $script:Calib.speedsMetresPerSec[$k] = [math]::Round((VLen $v[$k]) * 1000, 3) }
  return ($spread -gt 0.3)
}

# Key holds (ms per movement) that move the right hand by $Delta, from the measured key velocities.
function Get-AxisHolds([double[]]$Delta) {
  $holds = [ordered]@{}
  $c = Solve3 $script:Calib.axes $Delta
  if ($null -eq $c) { return $holds }
  $pairs = @(@('Forward', 'Back'), @('Right', 'Left'), @('Up', 'Down'))
  for ($i = 0; $i -lt 3; $i++) {
    $move = $pairs[$i][1]
    if ($c[$i] -ge 0) { $move = $pairs[$i][0] }
    $ms = [math]::Abs($c[$i])
    $keySpeed = VLen $script:Calib.keyVelocity[$move]
    if ($keySpeed -gt 1e-9) { $ms = $ms * (VLen $script:Calib.axes[$i]) / $keySpeed }
    if ($ms -lt $MinHoldMs) { continue }
    $holds[$move] = [int][math]::Min($MaxHoldMs, [math]::Round($ms))
  }
  return $holds
}

# Closed loop: hold keys, read where the hand went, correct.
function Move-HandTo([double[]]$Goal, [double]$Tol = 0.008, [int]$MaxIter = 8) {
  $tol = [math]::Max($Tol, $script:Calib.resolution)
  $err = [double]::NaN
  for ($i = 0; $i -le $MaxIter; $i++) {
    $s = Wait-NextProbe
    $p = V3 $s.handsAt.right
    if ($null -eq $p -or -not $s.hands.right) { return [pscustomobject]@{ ok = $false; error = $err; note = 'the right hand is not tracked' } }
    $e = VSub $Goal $p
    $err = VLen $e
    if ($err -le $tol) { return [pscustomobject]@{ ok = $true; error = $err } }
    if ($i -eq $MaxIter) { break }
    $holds = Get-AxisHolds (VScale $e 0.9)
    if ($holds.Count -eq 0) { return [pscustomobject]@{ ok = ($err -le 2 * $tol); error = $err } }
    Send-Moves $holds
  }
  return [pscustomobject]@{ ok = $false; error = $err }
}

# Open loop in small diagonal steps (a press pushes straight through the button face, not in a staircase).
function Move-HandBy([double[]]$Delta, [double]$StepMetres = 0.015) {
  $n = [math]::Max(1, [int][math]::Ceiling((VLen $Delta) / $StepMetres))
  $step = VScale $Delta (1.0 / $n)
  $carry = [double[]]@(0, 0, 0)
  for ($i = 0; $i -lt $n; $i++) {
    $want = VAdd $carry $step
    $holds = Get-AxisHolds $want
    $did = [double[]]@(0, 0, 0)
    foreach ($m in @($holds.Keys)) { $did = VAdd $did (VScale $script:Calib.keyVelocity[$m] $holds[$m]) }
    $carry = VSub $want $did
    Send-Moves $holds
    Start-Sleep -Milliseconds 30
  }
}

# A resting point for the right hand: in front of the chest, nearer than the dock and lower than the eyes. Moves
# start from here so the fingertip reaches each button from the person's side instead of sweeping through others.
function Get-HomePoint($State) {
  $h = V3 $State.head
  $f = V3 $State.headForward
  $fwd = VNorm ([double[]]@($f[0], 0, $f[2]))
  $right = VCross ([double[]]@(0, 1, 0)) $fwd
  return (VAdd $h (VAdd (VScale $right 0.15) (VAdd (VScale $fwd 0.18) ([double[]]@(0, -0.25, 0)))))
}

function Get-PlaneBasis([double[]]$Normal) {
  $u = VCross ([double[]]@(0, 1, 0)) $Normal
  if ((VLen $u) -lt 1e-3) { $u = [double[]]@(1, 0, 0) }
  $u = VNorm $u
  return [pscustomobject]@{ u = $u; v = (VNorm (VCross $Normal $u)) }
}

# The right index fingertip relative to the right-hand anchor, from the app's poke interactor (probe pokeAt.right).
function Get-ProbeFingertip($State) {
  if ($null -eq $State -or $null -eq $State.pokeAt) { return $null }
  $poke = V3 $State.pokeAt.right
  $hand = V3 $State.handsAt.right
  if ($null -eq $poke -or $null -eq $hand) { return $null }
  $off = VSub $poke $hand
  if ((VLen $off) -gt 0.3) { return $null }   # not this hand's
  return $off
}

# The offset a press uses: measured now when the probe has it (it turns with the head), else the calibrated one.
function Get-Fingertip($State) {
  if (-not $FingertipOffset) { $live = Get-ProbeFingertip $State; if ($live) { return $live } }
  return $script:Calib.fingertip
}

function Initialize-Fingertip($State) {
  if ($FingertipOffset) {
    $p = $FingertipOffset -split ','
    $script:Calib.fingertip = V3 @([double]::Parse($p[0], [Globalization.CultureInfo]::InvariantCulture), [double]::Parse($p[1], [Globalization.CultureInfo]::InvariantCulture), [double]::Parse($p[2], [Globalization.CultureInfo]::InvariantCulture))
    $script:Calib.fingertipSource = 'parameter'
    return
  }
  $live = Get-ProbeFingertip $State
  if ($live) { $script:Calib.fingertip = $live; $script:Calib.fingertipSource = "the app's right poke interactor (probe pokeAt), in the poke pose"; return }
  # first guess: 15 cm ahead of the hand anchor and 3 cm below it; presses refine it
  $f = V3 $State.headForward
  $flat = VNorm ([double[]]@($f[0], 0, $f[2]))
  $script:Calib.fingertip = VAdd (VScale $flat 0.15) ([double[]]@(0, -0.03, 0))
  $script:Calib.fingertipSource = 'estimate, refined by presses'
}

function Update-Fingertip([double[]]$New, [string]$Why) {
  $old = $script:Calib.fingertip
  if ((VLen (VSub $New $old)) -gt 0.08) { [void]$script:Calib.fingertipLog.Add([ordered]@{ why = "$Why (ignored: more than 8 cm away)"; offset = (Round3 $old) }); return }
  $script:Calib.fingertip = $New
  [void]$script:Calib.fingertipLog.Add([ordered]@{ why = $Why; offset = (Round3 $New) })
}

function Initialize-HandControl {
  if ($Assist) { $script:Calib.note = 'assisted run: the operator makes every gesture'; return }
  Write-Step 'Calibrating the simulated hand controls'
  try {
    $speed = Invoke-Native $MetavrCmd @('xrsim', 'input', 'speed', '--value', ([string]::Format([Globalization.CultureInfo]::InvariantCulture, '{0}', $SimSpeed))) 60
    Add-SetupStep "Simulator movement speed $SimSpeed (metavr xrsim input speed)" ($speed.exit -eq 0) $speed
    Send-SimKey $SimKeys.ResetPoses 80   # hands back to their default place in front of the head
    Start-Sleep -Milliseconds 500
    $script:Calib.headReset = Reset-SimHead $script:Seat
    $mode = Get-SimMoveKind
    if (@('right', 'hands', 'body') -notcontains $mode) { $script:Calib.note = "the movement keys moved '$mode', not the right hand"; return }
    if (-not (Measure-KeyVectors)) { $script:Calib.note = 'the movement keys do not move the right hand in three directions'; return }
    Set-HandPose 'PosePoke'
    Initialize-Fingertip (Wait-NextProbe)
    $script:Calib.headReset = Reset-SimHead $script:Seat   # measuring moved the head about
    $script:Calib.ok = $true
    if ($script:Calib.resolution -gt 0.02) { $script:Calib.note = 'the simulator moves the hand more than 2 cm in the shortest key hold, so pokes may miss' }
    Write-Host ("    keys move: {0}; speeds (m/s): {1}; fingertip offset {2} ({3})" -f $mode, (($script:Calib.speedsMetresPerSec.GetEnumerator() | ForEach-Object { "$($_.Key) $($_.Value)" }) -join ', '), ((Round3 $script:Calib.fingertip) -join ', '), $script:Calib.fingertipSource)
  } catch { $script:Calib.note = "calibration failed: $(Protect-Text $_.Exception.Message)" }
  if (-not $script:Calib.ok) { Write-Host "    hand control not calibrated: $($script:Calib.note)" -ForegroundColor Yellow }
}

# Re-measures the keys after the head turned (they move the person relative to where the head faces).
function Update-HandControl {
  if (-not $script:Calib.ok) { return }
  try {
    if (-not (Measure-KeyVectors)) { $script:Calib.ok = $false; $script:Calib.note = 'lost hand control after turning the head' }
    Set-HandPose 'PosePoke'
  } catch { $script:Calib.ok = $false; $script:Calib.note = "re-calibration failed: $(Protect-Text $_.Exception.Message)" }
}

# In the simulator both hands move with the head, half a metre apart: while the right index presses a button the
# left one comes down too, and can land on another button. With eight items on the dock, a press on Pause at its
# right end had the left index on Our day at its left end, which opened Our day instead (run 120650). A person keeps
# the other hand out of the way. The simulator can't move one hand, and switching one hand's tracking off switches
# both off (run 122623), so the script turns the head a little to the left for that press: the left hand swings
# back toward the person, the right index still reaches the button, and the head turns back afterwards.
# Returns the id of the button the left index would touch during a press on $Target, or $null.
function Get-LeftHandObstacle($State, $Target, [double[]]$Aim, [double[]]$Normal, [double[]]$Fingertip) {
  if ($null -eq $State -or -not $State.hands.left) { return $null }
  $right = V3 $State.handsAt.right
  $left = V3 $State.handsAt.left
  if ($null -eq $right -or $null -eq $left) { return $null }
  # the left index in the same pose as the right one: the right fingertip's offset, mirrored across the line the head faces
  $forward = V3 $State.headForward
  $side = VNorm ([double[]]@($forward[2], 0, (-$forward[0])))   # to the person's right
  $leftTip = VSub $Fingertip (VScale $side (2 * (VDot $Fingertip $side)))
  $between = VSub (VAdd (VSub $left $right) $leftTip) $Fingertip   # from the right fingertip to the left one
  foreach ($o in @($State.targets)) {
    if ("$($o.id)" -eq "$($Target.id)") { continue }
    $on = VNorm (V3 $o.normal)
    $ob = Get-PlaneBasis $on
    foreach ($depth in 0.06, 0.03, 0.0, -0.025) {   # along the press: in front of the face, on it, behind it
      $v = VSub (VAdd (VAdd $Aim (VScale $Normal $depth)) $between) (V3 $o.at)
      $inside = [math]::Abs((VDot $v $ob.u)) -le ([double]$o.size[0] / 2 + 0.02) -and [math]::Abs((VDot $v $ob.v)) -le ([double]$o.size[1] / 2 + 0.02)
      if ($inside -and [math]::Abs((VDot $v $on)) -le 0.035) { return "$($o.id)" }
    }
  }
  return $null
}

# A vector turned about the vertical axis by the angle the head turned (positive: to the right, as YawDeg counts).
function Rotate-Yaw([double[]]$v, [double]$Degrees) {
  $a = $Degrees * [math]::PI / 180.0
  $c = [math]::Cos($a); $q = [math]::Sin($a)
  return ,([double[]]@(($v[0] * $c + $v[2] * $q), $v[1], (-$v[0] * $q + $v[2] * $c)))
}

# Turns the head so the left index stays clear of every button while the right one presses $Id. Left first (the left
# hand comes back toward the person); if that puts it over something else, the other way. Returns $true when the
# head was turned ($script:AsideTurn holds by how much, for turning back).
function Step-LeftHandAside([string]$Id, [string]$Obstacle) {
  foreach ($deg in -20, 40) {
    $turn = Rotate-HeadYaw $deg
    if (-not $turn.ok) { break }
    $script:AsideTurn += [double]$turn.turned
    Update-HandControl
    $s = Wait-NextProbe
    $t = Find-Target $s $Id
    if (-not $t) { break }
    $f = Get-ProbeFingertip $s
    if (-not $f) { $f = Rotate-Yaw $script:Calib.fingertip $script:AsideTurn }
    if (-not (Get-LeftHandObstacle $s $t (V3 $t.at) (VNorm (V3 $t.normal)) $f)) { break }
  }
  if ($script:AsideTurn -eq 0) { return $false }
  $line = "For $Id the head was turned $([math]::Round($script:AsideTurn)) degrees during the press and back afterwards: the simulator moves both hands together, and the left index would have come down on $Obstacle."
  if (-not $script:Notes.Contains($line)) { [void]$script:Notes.Add($line) }
  Write-Host "    head turned $([math]::Round($script:AsideTurn)) degrees for this press (the left index would have come down on $Obstacle)" -ForegroundColor DarkGray
  return $true
}

# Poke a button: in front of it, through its face, back out. A press counts on release (Pressable.cs).
# Misses try nearby points; a wrong button pressed corrects the fingertip offset.
function Invoke-AutoPress([string]$Id, [string]$Accept, [switch]$KeepIn) {
  $script:AsideTurn = 0.0
  try { return (Invoke-AutoPressCore -Id $Id -Accept $Accept -KeepIn:$KeepIn) }
  finally {
    if ($script:AsideTurn -ne 0) {
      $back = -$script:AsideTurn
      $script:AsideTurn = 0.0
      try {
        # a recenter has just turned the room to face the head: there is nothing to turn back to
        if ($Id -notmatch 'recenter') { [void](Rotate-HeadYaw $back) }
        Update-HandControl
      } catch { }
    }
  }
}

function Invoke-AutoPressCore([string]$Id, [string]$Accept, [switch]$KeepIn) {
  if (-not $script:Calib.ok) { return [pscustomobject]@{ ok = $false; note = "no automatic aiming ($($script:Calib.note))" } }
  $pattern = @(@(0, 0), @(0, 0.6), @(0, -0.6), @(0.4, 0), @(-0.4, 0), @(0, 1.2), @(0, -1.2))
  $tries = New-Object System.Collections.ArrayList
  Set-HandPose 'PosePoke'
  $restPoint = Get-HomePoint (Wait-NextProbe)
  [void](Move-HandTo $restPoint 0.03 6)
  for ($a = 0; $a -lt $pattern.Count; $a++) {
    $s = Wait-NextProbe
    $t = Find-Target $s $Id
    if (-not $t) { return [pscustomobject]@{ ok = $false; note = "'$Id' is not on screen"; tries = $tries } }
    $c = V3 $t.at
    $n = VNorm (V3 $t.normal)
    $b = Get-PlaneBasis $n
    $offU = [math]::Max(-0.03, [math]::Min(0.03, $pattern[$a][0] * [double]$t.size[0] / 2))
    $offV = $pattern[$a][1] * [double]$t.size[1] / 2
    $shift = VAdd (VScale $b.u $offU) (VScale $b.v $offV)
    $aim = VAdd $c $shift
    $f = Get-Fingertip $s
    if (-not $KeepIn -and $script:AsideTurn -eq 0 -and -not $script:NoAsideTurn) {
      $obstacle = Get-LeftHandObstacle $s $t $aim $n $f
      if ($obstacle -and (Step-LeftHandAside $Id $obstacle)) {
        # the room hasn't moved, but the hands and the fingertip's offset have turned with the head
        $s = Wait-NextProbe
        $f = Get-ProbeFingertip $s
        if (-not $f) { $f = Rotate-Yaw $script:Calib.fingertip $script:AsideTurn }
      }
    }
    $mark = New-Mark
    [void](Move-HandTo (VSub (VAdd $aim (VScale $n 0.12)) $f) 0.02 6)    # well in front of the button
    $pre = Move-HandTo (VSub (VAdd $aim (VScale $n 0.04)) $f) 0.008 8     # fingertip 4 cm in front
    # Close the loop on the fingertip itself before pushing. The app reports it (probe pokeAt) only while the hand's
    # poke interactor is the active one; pointing across the dock can leave the ray active until the finger is this
    # close, and the stored offset alone was several centimetres out after a recenter (it pressed the next button).
    # When the app still doesn't report the fingertip at 4 cm (the ray is still the active interactor), come closer a
    # centimetre at a time, down to 2 cm, and look again: the poke takes over as the finger nears the button.
    $front = 0.04
    if (-not $FingertipOffset) {
      $live = $null
      for ($k = 0; $k -lt 7 -and -not $live; $k++) {
        $live = Get-ProbeFingertip (Wait-NextProbe)
        if (-not $live -and $k -ge 1 -and $front -gt 0.021) {
          $front -= 0.01
          Move-HandBy (VScale $n (-0.01))
        }
      }
      if ($live -and (VLen (VSub $live $f)) -gt 0.004) { $f = $live; $pre = Move-HandTo (VSub (VAdd $aim (VScale $n $front)) $f) 0.006 6 }
    }
    Move-HandBy (VScale $n (-($front + 0.025)))                           # through the face to 2.5 cm behind it
    if ($KeepIn) {
      # The press has to be really held when the caller drops tracking: the probe says whether the button is down.
      # If it isn't yet, go a little deeper.
      $down = $false
      for ($k = 0; $k -lt 4 -and -not $down; $k++) {
        $held = Find-Target (Wait-NextProbe) $Id
        $down = [bool]($held -and $held.down)
        if (-not $down) { Move-HandBy (VScale $n (-0.01)) }
      }
      return [pscustomobject]@{ ok = $true; held = $true; down = $down; normal = $n; mark = $mark; tries = $tries }
    }
    Move-HandBy (VScale $n 0.09)                                          # back out: the press counts on release
    $hit = Wait-EventSince $mark 'press' $Accept 1.5
    $others = @(Get-EventsSince $mark | Where-Object { $_.kind -eq 'press' -and "$($_.detail)" -notmatch $Accept })
    $pressed = @($others | ForEach-Object { $_.detail })
    if ($hit) { $pressed += $hit.detail }
    [void]$tries.Add([ordered]@{ offset = @([math]::Round($offU, 3), [math]::Round($offV, 3)); approachError = $pre.error; pressed = $pressed })
    if ($hit) {
      if ($offU -ne 0 -or $offV -ne 0) { Update-Fingertip (VSub $f $shift) "pressed $($hit.detail) after aiming off-centre" }
      [void](Move-HandTo (VSub (VAdd $aim (VScale $n 0.12)) $script:Calib.fingertip) 0.03 3)
      [void](Move-HandTo $restPoint 0.03 4)
      return [pscustomobject]@{ ok = $true; event = $hit; tries = $tries; unexpected = @($others | ForEach-Object { $_.detail }) }
    }
    if ($others.Count -gt 0) {
      $o = Find-Target $s $others[0].detail
      # Only a neighbour says something about where the fingertip really is; a button far from the aim was pressed
      # by something else (the other hand), and correcting by that would throw the aim off by the same distance.
      if ($o -and (VLen (VSub (V3 $o.at) $aim)) -lt 0.12) { Update-Fingertip (VAdd $f (VSub (V3 $o.at) $aim)) "pressed $($others[0].detail) instead of $Id" }
      return [pscustomobject]@{ ok = $false; wrong = $others[0].detail; note = "pressed $($others[0].detail) instead of $Id"; tries = $tries }
    }
    [void](Move-HandTo (VSub (VAdd $aim (VScale $n 0.12)) $f) 0.03 3)    # back off and try the next point
  }
  return [pscustomobject]@{ ok = $false; note = "no press after $($pattern.Count) tries"; tries = $tries }
}

# Pick up a grabbable: palm around it and grab; if that doesn't take, fingertips on it and pinch.
# The hand comes in from the right at the object's height: the dock sits between the person and the cup, and the
# cup label's Play and Reply buttons sit above it, so a path from the front or from above could press them.
function Invoke-AutoGrab([string]$Id) {
  if (-not $script:Calib.ok) { return [pscustomobject]@{ ok = $false; note = "no automatic aiming ($($script:Calib.note))" } }
  $s = Wait-NextProbe
  $t = Find-Target $s $Id
  if (-not $t) { return [pscustomobject]@{ ok = $false; note = "'$Id' is not on screen" } }
  $c = V3 $t.at
  $f = $script:Calib.fingertip
  $palm = VScale ([double[]]@($f[0], 0, $f[2])) 0.5
  $pattern = '^' + [regex]::Escape($Id) + '$'
  $hand = V3 $s.handsAt.right
  $side = [math]::Max($c[0], $hand[0]) + 0.3
  $mark = New-Mark
  Set-HandPose 'PoseDefault'
  [void](Move-HandTo ([double[]]@($side, $hand[1], $hand[2])) 0.03 6)                     # out to the right
  [void](Move-HandTo (VSub ([double[]]@(($side + $palm[0]), $c[1], $c[2])) $palm) 0.02 6)    # level with it, to its right
  [void](Move-HandTo (VSub $c $palm) 0.01 8)                                                # palm on it
  Set-HandPose 'PoseGrab'
  $g = Wait-EventSince $mark 'grab' $pattern 2
  if ($g) { return [pscustomobject]@{ ok = $true; event = $g; how = 'palm grab' } }
  Set-HandPose 'PoseDefault'
  [void](Move-HandTo (VSub $c (VScale $f 0.9)) 0.01 8)                                      # fingertips on it
  Set-HandPose 'PosePinch'
  $g = Wait-EventSince $mark 'grab' $pattern 2
  if ($g) { return [pscustomobject]@{ ok = $true; event = $g; how = 'pinch grab' } }
  Set-HandPose 'PoseDefault'
  return [pscustomobject]@{ ok = $false; note = 'neither a palm grab nor a pinch took hold' }
}

# The script tries first; if it can't (or with -Assist), the operator makes the gesture. Either way the gesture
# only counts when the probe records the expected event.
function Invoke-Gesture([string]$Instruction, [string]$AutoCommand, [hashtable]$AutoArgs = @{}, [string]$Kind, [string]$Pattern, [int]$OperatorTimeoutSec = 120) {
  $mark = New-Mark
  $auto = $null
  if (-not $Assist) {
    try { $auto = & $AutoCommand @AutoArgs } catch { $auto = [pscustomobject]@{ ok = $false; note = "automation error: $(Protect-Text $_.Exception.Message)" } }
    $hit = Wait-EventSince $mark $Kind $Pattern 1
    if ($hit) { return [pscustomobject]@{ ok = $true; driver = 'script'; event = $hit; auto = $auto; mark = $mark } }
  }
  $why = ''
  if ($auto -and $auto.note) { $why = $auto.note }
  if (-not $Assist -and $auto) {
    # what the automation tried, for the report: each try's aim offset, approach error and what got pressed instead
    $tries = @($auto.tries | ForEach-Object { "offset $(($_.offset) -join ',') error $([math]::Round([double]$_.approachError, 3)) pressed '$((@($_.pressed)) -join ',')'" })
    $line = "Couldn't $($Instruction.TrimEnd('.')) automatically: $why$(if ($tries.Count) { ' Tries: ' + ($tries -join '; ') + '.' })"
    Write-Host "    $line" -ForegroundColor DarkYellow
    [void]$script:Notes.Add($line)
  }
  if ($NonInteractive) { return [pscustomobject]@{ ok = $false; driver = 'script'; event = $null; auto = $auto; mark = $mark; note = "the script couldn't do it: $why" } }
  $mark2 = New-Mark
  Write-Host ''
  Write-Host ">>> YOUR TURN: $Instruction" -ForegroundColor Yellow
  if ($why) { Write-Host "    (the script couldn't: $why)" -ForegroundColor DarkYellow }
  Write-Host "    Use the simulator's hand controls only. Watching the probe for up to $OperatorTimeoutSec s." -ForegroundColor Yellow
  $hit = Wait-EventSince $mark2 $Kind $Pattern $OperatorTimeoutSec
  if ($hit) { Write-Host '    seen in the probe' -ForegroundColor Green; return [pscustomobject]@{ ok = $true; driver = 'operator'; event = $hit; auto = $auto; mark = $mark } }
  return [pscustomobject]@{ ok = $false; driver = 'operator'; event = $null; auto = $auto; mark = $mark; note = "not seen within $OperatorTimeoutSec s" }
}

function Invoke-HandPress([string]$Id, [string]$Label, [string]$Accept = '') {
  if (-not $Accept) { $Accept = '^' + [regex]::Escape($Id) + '$' }
  $gesture = @{
    Instruction = "press '$Label' with a simulated hand (poke it, or point at it and pinch). If another screen opened, go back first."
    AutoCommand = 'Invoke-AutoPress'; AutoArgs = @{ Id = $Id; Accept = $Accept }; Kind = 'press'; Pattern = $Accept
  }
  $r = Invoke-Gesture @gesture
  # A press that landed on the neighbouring dock button opened that button's screen. Do what a person would: go
  # back and press again (the miss has already corrected the fingertip estimate). The miss stays in the report.
  if (-not $r.ok -and -not $Assist -and $r.auto -and "$($r.auto.wrong)" -match '^dock:' -and $Id -match '^dock:') {
    $back = @((Wait-NextProbe).targets | Where-Object { "$($_.id)" -match ':back$' })
    if ($back.Count -gt 0) {
      $backId = "$($back[0].id)"
      [void]$script:Notes.Add("Pressed $($r.auto.wrong) instead of $Id; went back ($backId) and pressed again.")
      $backPattern = '^' + [regex]::Escape($backId) + '$'
      $b = Invoke-Gesture -Instruction "press Back ($backId)" -AutoCommand 'Invoke-AutoPress' -AutoArgs @{ Id = $backId; Accept = $backPattern } -Kind 'press' -Pattern $backPattern
      if ($b.ok -and (Wait-Target $Id 8)) { $r = Invoke-Gesture @gesture }
    }
  }
  return $r
}

function Add-DriverOf($Check, $Result) {
  if ($null -eq $Result -or -not $Result.driver) { return }
  $drivers = @($Check.driver -split ', ' | Where-Object { $_ })
  if ($drivers -notcontains $Result.driver) { $drivers += $Result.driver }
  $Check.driver = $drivers -join ', '
}

# ---------------------------------------------------------------- services, data and the simulator
function Initialize-Run {
  $script:StartedAt = (Get-Date).ToString('o')
  $q = $ErrorActionPreference
  $ErrorActionPreference = 'Continue'
  try {
    $script:Commit = "$(& git -C $Repo rev-parse --short HEAD 2>$null)".Trim()
    $script:Clean = -not [bool]("$(& git -C $Repo status --porcelain 2>$null)".Trim())
  } finally { $ErrorActionPreference = $q }
  New-Item -ItemType Directory -Force $RunDir | Out-Null
  $os = Get-CimInstance Win32_OperatingSystem
  $version = $null
  if ($MetavrCmd) { $version = (Invoke-Native $MetavrCmd @('--version') 60).output }
  $script:Environment = [ordered]@{
    os = "$($os.Caption) $($os.Version)"
    gpu = ((Get-CimInstance Win32_VideoController | ForEach-Object { $_.Name }) -join '; ')
    unity = $UnityVersion
    simulatorRuntime = (Protect-Text $XrsimRuntimeJson)
    metavr = $version
    powershell = $PSVersionTable.PSVersion.ToString()
  }
}

function Initialize-Services {
  if ($SampleOnly) {
    Add-SetupStep 'No API or worker: the app runs as a build with no server (-SampleOnly)' $true $null
    return
  }
  Write-Step 'Local API, worker and development data'
  if ((Test-Api) -and -not (Test-Path $PidFile)) { throw "An API answers at $ApiUrl, but scripts/native/start.ps1 didn't start it, so check 5 can't restart it. Stop that API and run this again." }
  if (-not (Test-Api)) { & "$PSScriptRoot/start.ps1" }
  if (-not (Test-Api)) { throw 'The local API did not start (see api/data/native-dev/logs/api.log).' }
  Add-SetupStep 'API and worker running (scripts/native/start.ps1)' $true ([ordered]@{ api = $ApiUrl })
  $seedFile = Join-Path $RunDir 'seed.json'
  $seed = Invoke-Native $Python @((Join-Path $PSScriptRoot 'seed_dev.py'), '--api', $ApiUrl, '--seed', (Join-Path $PSScriptRoot 'dev-seed.json'), '--voice-note', '--json-out', $seedFile) 300
  Add-SetupStep 'Development accounts and rooms, and a voice note from Sam to Nora (seed_dev.py --voice-note)' ($seed.exit -eq 0) $seed
  if ($seed.exit -ne 0) { throw "Seeding failed: $($seed.output)" }
  $script:Seed = Get-Content $seedFile -Raw | ConvertFrom-Json
  [void](Add-Artifact $seedFile 'seeded ids (development data)')
  if (-not $script:Seed.voiceNote.oggUrl) { [void]$script:Notes.Add('The seeded voice note has no Ogg copy (FFmpeg without libvorbis?), so the native app cannot play it in live mode.') }
  $accounts = (Get-Content (Join-Path $PSScriptRoot 'dev-seed.json') -Raw | ConvertFrom-Json).accounts
  $nora = @($accounts | Where-Object { $_.key -eq 'nora' })[0]
  $auth = Invoke-RestMethod -Method Post -Uri "$ApiUrl/v1/auth/login" -ContentType 'application/json' -Body (@{ email = $nora.email; password = $nora.password } | ConvertTo-Json) -TimeoutSec 20
  $script:NoraToken = $auth.token
  $script:NoraId = $auth.user.id
  $script:NoraEmail = $auth.user.email
  Add-SetupStep "Signed in as Nora ($($nora.email), development account) through POST /v1/auth/login; the token is never written to the evidence" $true $null
}

function Initialize-Simulator {
  Write-Step 'Meta XR Simulator'
  if (-not $MetavrCmd) { throw 'metavr was not found (npm install metavr@1.10.0; see docs/NATIVE_QUICKSTART.md).' }
  if (-not $XrsimRuntimeJson) { throw 'Meta XR Simulator was not found (scripts/native/install_xrsim_user.ps1, or Meta''s installer).' }
  if ($Target -eq 'PlayMode' -and -not (Test-Path $Unity)) { throw "Unity $UnityVersion was not found." }
  if ($Target -eq 'WindowsBuild' -and -not (Test-Path $WindowsBuild)) { throw 'No Windows build yet (scripts/native/build.ps1 -Target Windows).' }
  if ($Target -eq 'PlayMode') {
    $u = Get-UnityEditor
    if ($u -and -not $u.OurProfile) { throw "Unity already has native/quest open without -sameskyProfile=$AppProfile. Close it: this script opens the editor itself." }
  }
  if (-not (Get-SimWindow)) { Start-Sim }
  Add-SetupStep 'Meta XR Simulator window found' ([bool](Get-SimWindow)) ([ordered]@{ runtime = (Protect-Text $XrsimRuntimeJson) })
}

function Get-ApiList([string]$Path) {
  $r = Invoke-RestMethod -Method Get -Uri "$ApiUrl$Path" -Headers @{ Authorization = "Bearer $($script:NoraToken)" } -TimeoutSec 20
  return @($r | ForEach-Object { $_ })
}

# What the server has for a reply: the reply itself and its place on the table.
function Test-ReplyOnServer([string]$ReplyId) {
  $replies = @(Get-ApiList "/v1/moments/$($script:Seed.voiceNote.momentId)/replies")
  $objects = @(Get-ApiList "/v1/rooms/$($script:Seed.rooms.pair)/objects")
  $reply = @($replies | Where-Object { $_.id -eq $ReplyId })
  $object = @($objects | Where-Object { $_.replyId -eq $ReplyId })
  $placement = $null
  if ($object.Count -gt 0) { $placement = $object[0].transform }
  return [ordered]@{
    replyId = $ReplyId; replyStored = ($reply.Count -gt 0); byNora = ($reply.Count -gt 0 -and $reply[0].authorId -eq $script:NoraId)
    placementStored = ($object.Count -gt 0); placement = $placement
  }
}

function Restart-Services {
  $before = $null
  if (Test-Path $PidFile) { $before = Get-Content $PidFile -Raw | ConvertFrom-Json }
  & "$PSScriptRoot/stop.ps1"
  $down = -not (Test-Api)
  & "$PSScriptRoot/start.ps1"
  $up = Test-Api
  $after = $null
  if (Test-Path $PidFile) { $after = Get-Content $PidFile -Raw | ConvertFrom-Json }
  return [ordered]@{
    apiDownAfterStop = $down; apiUpAfterStart = $up
    apiPidBefore = $(if ($before) { $before.api }); apiPidAfter = $(if ($after) { $after.api })
    workerPidBefore = $(if ($before) { $before.worker }); workerPidAfter = $(if ($after) { $after.worker })
  }
}

function Enter-LiveRoom($Check, [string]$Label) {
  if (-not (Wait-Target 'launch:continue' 30)) { Add-Note $Check "'Continue as ...' wasn't offered on the launch screen ($Label): the stored session doesn't match this API."; return $null }
  $p = Invoke-HandPress 'launch:continue' 'Continue as Nora (launch screen)'
  Add-DriverOf $Check $p
  Add-EventEvidence $Check $p.event "launch:continue ($Label)"
  if (-not $p.ok) { return $null }
  Add-EventEvidence $Check (Wait-EventSince $script:SessionMark 'mode' '^live$' 30) "live mode ($Label)"
  $room = Wait-EventSince $script:SessionMark 'screen' '^room$' 30
  Add-EventEvidence $Check $room "room open ($Label)"
  return $room
}

function Get-RestoredIds($Ev) {
  if (-not $Ev) { return @() }
  $list = "$($Ev.detail)" -replace '^restored:', ''
  return @($list -split ',' | Where-Object { $_ })
}

# ---------------------------------------------------------------- the checks
function Invoke-Check1 {
  $c = New-Check 1 'XR session active: simulated Meta Quest 3 with hand input'
  # The simulator lists devices only while an app is connected, and a device change applies after an app restart.
  [void](Start-App -FreshProfile)
  $dev = Invoke-Native $MetavrCmd @('xrsim', 'device', 'set', '--name', 'Meta Quest 3', '--wait') 120
  Add-Evidence $c 'command' $dev 'simulated device'
  $sync = Invoke-Native $MetavrCmd @('xrsim', 'input', 'sync', '--source', 'hand') 60
  Add-Evidence $c 'command' $sync 'hand input'
  # Does this runtime record MP4s? Asked now, so the timed session that follows doesn't wait to find out.
  if (-not $SkipRecording) {
    $rec = Invoke-Native $MetavrCmd @('xrsim', 'record', 'status') 60
    Add-Evidence $c 'command' $rec 'MP4 recorder in this runtime'
    if ($rec.output -match 'does not support native MP4 recording|recording service') {
      $script:RecorderMode = 'window'
      [void]$script:Notes.Add("metavr xrsim record: $($rec.output) The recordings are FFmpeg captures of the simulator window instead.")
    }
  }
  # before the timed session that follows; the hands can take a moment to come back after the input sync
  [void](Wait-ProbeState { param($st) $st.hands.right -and $null -ne $st.pokeAt -and $null -ne $st.pokeAt.right } 10)
  Initialize-HandControl
  if (-not $Assist -and -not $script:Calib.ok) {
    [void]$script:Notes.Add("First hand-control calibration failed ($($script:Calib.note)); tried once more.")
    Start-Sleep -Seconds 2
    $script:Calib.note = ''
    Initialize-HandControl
  }
  # close-ups of the tracked hands and their skin, in this untimed session (the next one is timed)
  if (-not $Assist) {
    Set-HandPose 'PoseDefault'
    [void](Save-SeatView 'hands' 0 0 'the tracked hands from where the head is: skin tone, nails, knuckles, their shadow' '' 1.4 42 'hands')
    [void](Save-SeatView 'palms' 0 0 'the tracked hands from the other side: palms, creases, finger pads' '' 1.4 42 'palms')
    Set-HandPose 'PosePinch'
    [void](Save-SeatView 'hands-pinch' 0 0 'the tracked hands pinching' '' 1.4 42 'hands')
    Set-HandPose 'PosePoke'
  }
  Stop-App
  [void](Start-App -FreshProfile -Record 'sample')
  $sync2 = Invoke-Native $MetavrCmd @('xrsim', 'input', 'sync', '--source', 'hand') 60
  Add-Evidence $c 'command' $sync2 'hand input after the restart'
  $w = Wait-ProbeState { param($st) $st.xrDeviceActive -and $st.hands.left -and $st.hands.right -and $st.activeInput -eq 'hands' } 20
  Add-StateEvidence $c $w.state 'check1-xr' @('xrDeviceActive', 'xrDevice', 'openxrRuntime', 'hands', 'activeInput', 'fps', 'screen', 'head', 'handsAt')
  $runtimeLine = Get-UnityRuntimeLine
  if ($runtimeLine) { Add-Evidence $c 'log' $runtimeLine 'Unity log: OpenXR runtime used by Play mode' }
  [void](Save-Shot 'check1-session')
  [void](Save-SeatView 'launch' 0 -9 'the launch screen in the room (reference 10)')
  $s = $w.state
  $simRuntime = ("$($s.openxrRuntime)" -match 'simulator') -or ("$runtimeLine" -match 'meta_openxr_simulator\.json')
  $ok = $w.ok -and $simRuntime -and $dev.exit -eq 0 -and $sync.exit -eq 0 -and $sync2.exit -eq 0
  $sampleOnlyText = ''
  if ($SampleOnly) {
    # a build with no server: the welcome panel must offer the sample day, and nothing that needs an account
    $ids = @($s.targets | ForEach-Object { $_.id })
    $account = @($ids | Where-Object { $_ -in 'launch:signin', 'launch:signup', 'launch:continue' })
    $welcome = [ordered]@{ sampleOnly = [bool]$s.sampleOnly; pressable = @($ids | Sort-Object); accountButtons = $account }
    Add-Evidence $c 'probe-welcome' $welcome 'what can be pressed on the welcome panel of a build with no server'
    $sampleOk = [bool]$s.sampleOnly -and ($ids -contains 'launch:sample') -and $account.Count -eq 0
    if (-not $sampleOk) { Add-Note $c "The welcome panel isn't the sample-only one (sampleOnly $($s.sampleOnly); account buttons: $($account -join ', '))." }
    $ok = $ok -and $sampleOk
    $sampleOnlyText = "; build with no server: sample day offered $($ids -contains 'launch:sample'), account buttons $($account.Count)"
  }
  $c.driver = 'script (metavr)'
  Add-Manual $c 'Confirm the simulated headset is Meta Quest 3' 'The probe cannot see the headset model: check Device in the simulator Inputs panel (screenshot sim-check1-session.png).'
  if (-not $simRuntime) { Add-Note $c "The OpenXR runtime isn't shown as the simulator (probe: '$($s.openxrRuntime)')." }
  $summary = "XR active $($s.xrDeviceActive), runtime '$($s.openxrRuntime)', hands L $($s.hands.left) R $($s.hands.right), input $($s.activeInput); metavr device set exit $($dev.exit), input sync exit $($sync.exit)/$($sync2.exit)$sampleOnlyText"
  if ($ok) { Complete-Check $c 'pass' $summary } else { Complete-Check $c 'fail' $summary }
}

function Invoke-FirstMinute {
  # Session 2 has just started on a fresh profile (Invoke-Check1). Checks 4 and 3 share this one timed run.
  $c4 = New-Check 4 'Voice note heard within the first minute of a fresh start (sample day)'
  $c3 = New-Check 3 'Cup grabbed by a simulated hand'
  $start = $script:SessionStartEvent
  Add-EventEvidence $c4 $start 'app start (fresh profile)'
  if (-not (Wait-Target 'launch:sample' 30)) {
    Complete-Check $c4 'fail' "the launch screen's 'Try the sample day' never appeared"
    Complete-Check $c3 'fail' 'not reached'
    return
  }
  $p = Invoke-HandPress 'launch:sample' 'Try the sample day'
  Add-DriverOf $c4 $p
  Add-EventEvidence $c4 $p.event 'launch:sample pressed'
  $room = Wait-EventSince $script:SessionMark 'screen' '^room$' 30
  Add-EventEvidence $c4 $room 'sample room open'
  if (-not $room -or -not (Wait-Target 'object:cup' 20)) {
    Complete-Check $c4 'fail' 'the sample room (with the cup) did not open'
    Complete-Check $c3 'fail' 'not reached'
    return
  }
  [void](Wait-EventSince $script:SessionMark 'cue' '' 4)
  [void](Save-SeatView 'room-cue' 0 -14 'the sample room with the pick-up cue (references 02 and 10)')
  $g = Invoke-Gesture -Instruction 'pick up the cup on the table with a simulated hand (grab or pinch it).' -AutoCommand 'Invoke-AutoGrab' -AutoArgs @{ Id = 'object:cup' } -Kind 'grab' -Pattern '^object:cup$'
  Add-DriverOf $c3 $g
  Add-DriverOf $c4 $g
  Add-EventEvidence $c3 $g.event 'grab'
  if ($g.auto -and $g.auto.how) { Add-Note $c3 "Taken with a $($g.auto.how)." }
  $picked = Wait-EventSince $script:SessionMark 'cup' '^picked-up$' 5
  Add-EventEvidence $c3 $picked 'cup picked up'
  $held = Wait-ProbeState { param($st) $st.cupHeld } 3
  Add-StateEvidence $c3 $held.state 'check3-held' @('cupHeld', 'grabs', 'lastGrab', 'activeInput', 'handsAt')
  $moved = $null
  if ($g.ok -and $g.driver -eq 'script') {
    $before = Find-Target (Wait-NextProbe) 'object:cup'
    Move-HandBy ([double[]]@(0, 0.06, 0))
    $after = Find-Target (Wait-NextProbe) 'object:cup'
    if ($before -and $after) { $moved = [math]::Round((VLen (VSub (V3 $after.at) (V3 $before.at))), 3) }
  }
  [void](Save-Shot 'check3-cup-held')
  $voice = Wait-EventSince $script:SessionMark 'voice' '^play$' 10
  Add-EventEvidence $c4 $voice 'voice note playing'
  if ($voice) {
    # what started it: the grab, or a press of the cup label's Play button
    for ($i = $script:AllEvents.IndexOf($voice) - 1; $i -ge $script:SessionMark; $i--) {
      $e = $script:AllEvents[$i]
      if (($e.kind -eq 'grab' -and $e.detail -eq 'object:cup') -or ($e.kind -eq 'press' -and $e.detail -eq 'cup:play')) { Add-EventEvidence $c4 $e 'what started the voice note'; break }
    }
  }
  $cap = Wait-ProbeState { param($st) $st.voicePlaying -and "$($st.captions)".Length -gt 0 } 8
  Add-StateEvidence $c4 $cap.state 'check4-captions' @('voicePlaying', 'audio', 'captionsOn', 'captions', 'presence', 'paused')
  if ($cap.ok) {
    [void](Save-Shot 'check4-captions')
    [void](Save-SeatView 'message' 0 -20 'the cup lifted, its message card playing with captions (reference 03)')
  }
  $fin = Wait-EventSince $script:SessionMark 'voice' '^finished$' 40
  Add-EventEvidence $c4 $fin 'voice note finished'
  if ($fin) {
    Start-Sleep -Milliseconds 1500   # the sample's simulated presence sits down and turns toward the person
    [void](Save-SeatView 'room-partner' 0 -9 "after the message: the sample's partner as labelled simulated presence (reference 02)")
  }
  $rel = $null
  if ($g.ok) {
    $markRelease = New-Mark
    if ($g.driver -eq 'script') { Set-HandPose 'PoseDefault' }
    else { Write-Host '>>> YOUR TURN: let go of the cup.' -ForegroundColor Yellow }
    $rel = Wait-EventSince $markRelease 'release' '^object:cup$' 20
    Add-EventEvidence $c3 $rel 'released'
    if ($g.driver -eq 'script') { Move-HandBy ([double[]]@(0, 0.08, -0.08)) }
  }
  # check 3
  $ok3 = $g.ok -and "$($g.event.input)" -eq 'hands' -and [bool]$picked -and [bool]$rel
  $s3 = "grab of object:cup with input '$($g.event.input)' ($($g.driver)); picked-up $([bool]$picked); released $([bool]$rel)"
  if ($null -ne $moved) { $s3 += "; the cup moved $moved m with the hand" }
  if ($ok3) { Complete-Check $c3 'pass' $s3 } else { if ($g.note) { Add-Note $c3 $g.note }; Complete-Check $c3 'fail' $s3 }
  # check 4
  $elapsed = $null
  if ($voice -and $start) { $elapsed = [math]::Round([double]$voice.t - [double]$start.t, 1) }
  if ($elapsed -ne $null) { Add-Evidence $c4 'probe-timing' ([ordered]@{ secondsFromStartToVoice = $elapsed; limit = $FirstMinuteSec }) 'start -> voice play (probe clock)' }
  $paused = $cap.state -and $cap.state.paused
  if ($paused) { Add-Note $c4 "The app reported paused while the voice note played (see the 'pause' events in the session's events.jsonl)." }
  if (-not $fin -and $voice) { Add-Note $c4 "Playback didn't finish within 40 s." }
  Add-Manual $c4 'Hear it' 'The probe shows playback state and caption text, not sound: listen in the simulator (or the MP4) for the voice note coming from the cup.'
  $ok4 = $voice -and $elapsed -ne $null -and $elapsed -le $FirstMinuteSec -and $cap.ok -and [bool]$fin
  $s4 = "voice note playing $elapsed s after a fresh start (limit $FirstMinuteSec s); captions shown $($cap.ok); finished $([bool]$fin)"
  if ($ok4) { Complete-Check $c4 'pass' $s4 } else { Complete-Check $c4 'fail' $s4 }
}

function Enter-SampleRoomFresh($Check) {
  [void](Start-App -Record "room-$($Check.id)")
  if (-not (Wait-Target 'launch:sample' 30)) { return $false }
  $p = Invoke-HandPress 'launch:sample' 'Try the sample day'
  Add-DriverOf $Check $p
  return [bool](Wait-EventSince $script:SessionMark 'screen' '^room$' 30)
}

function Invoke-Check2 {
  $c = New-Check 2 'Dock button pressed by a simulated hand'
  $ready = $false
  try { $ready = [bool](Find-Target (Wait-NextProbe) 'dock:day') } catch { }
  if (-not $ready) { $ready = (Enter-SampleRoomFresh $c) -and (Wait-Target 'dock:day' 15) }
  if (-not $ready) { Complete-Check $c 'fail' 'the dock never showed'; return }
  $mark = New-Mark
  $p = Invoke-HandPress 'dock:day' 'Our day (on the dock at the table edge)' '^dock:'
  Add-DriverOf $c $p
  Add-EventEvidence $c $p.event 'dock press'
  $effect = $null
  $deadline = (Get-Date).AddSeconds(6)
  while (-not $effect -and (Get-Date) -lt $deadline) {
    Update-Events
    $effect = Find-EventSince $mark 'screen'
    if (-not $effect) { $effect = Find-EventSince $mark 'recenter' }
    if (-not $effect) { Start-Sleep -Milliseconds 200 }
  }
  Add-EventEvidence $c $effect 'what the press did'
  [void](Save-Shot 'check2-dock')
  $ok = $p.ok -and "$($p.event.input)" -eq 'hands' -and [bool]$effect
  $summary = "pressed $($p.event.detail) with input '$($p.event.input)' ($($p.driver)); then $($effect.kind) $($effect.detail)"
  if ($ok) { Complete-Check $c 'pass' $summary } else { if ($p.note) { Add-Note $c $p.note }; Complete-Check $c 'fail' $summary }
}

function Invoke-TypeReply($Check, [string]$Letter = 'g') {
  for ($attempt = 1; $attempt -le 3; $attempt++) {
    if (-not (Find-Target (Wait-NextProbe) 'key:done')) {
      $open = Invoke-HandPress 'cup:reply' 'Reply (under the cup label)'
      Add-DriverOf $Check $open
      Add-EventEvidence $Check $open.event 'cup:reply pressed'
      if (-not $open.ok -or -not (Wait-Target 'key:done' 8)) { continue }
    }
    $mark = New-Mark
    $typed = Invoke-HandPress "key:$Letter" "the '$Letter' key on the keyboard" '^key:([a-z0-9]|comma|period)$'
    Add-DriverOf $Check $typed
    Add-EventEvidence $Check $typed.event 'a character typed'
    if (-not $typed.ok) { continue }
    $done = Invoke-HandPress 'key:done' 'Done on the keyboard'
    Add-DriverOf $Check $done
    Add-EventEvidence $Check $done.event 'Done pressed'
    if (-not $done.ok) { continue }
    $placed = Wait-EventSince $mark 'reply' '^placed:' 15
    if ($placed) { return $placed }
  }
  return $null
}

function Invoke-Check5 {
  $c = New-Check 5 'Reply on the table survives an app restart and an API restart'
  if (-not $script:Seed -or -not $script:Seed.voiceNote) { Complete-Check $c 'blocked' 'no seeded voice note'; return }
  Stop-App
  Set-LiveSession
  Add-Note $c 'Nora is signed in by the session file the script wrote (typing the email and password on the spatial keyboard is not automated).'
  [void](Start-App -Record 'live')
  if (-not (Enter-LiveRoom $c 'first visit')) { Complete-Check $c 'fail' "couldn't open Nora's room"; return }
  Add-EventEvidence $c (Wait-EventSince $script:SessionMark 'reply' '^restored:' 15) 'notes on the table before replying'
  if (-not (Wait-Target 'cup:reply' 15)) { Complete-Check $c 'fail' "no reply button by the cup (is Sam's voice note waiting?)"; return }
  # The app keys a reply by its moment and text (a resend returns the same reply), so type a letter Nora hasn't
  # replied with yet: the reply this run places is then a new one.
  $earlier = @(Get-ApiList "/v1/moments/$($script:Seed.voiceNote.momentId)/replies" | Where-Object { $_.authorId -eq $script:NoraId })
  $letter = @('g', 'h', 'j', 'k', 'f', 'd', 's', 'a', 'l', 't', 'r', 'e', 'w', 'q', 'y', 'u', 'i', 'o', 'p', 'z', 'x', 'c', 'v', 'b', 'n', 'm' | Where-Object { @($earlier | ForEach-Object { "$($_.text)".Trim() }) -notcontains $_ })[0]
  if (-not $letter) { $letter = 'g'; Add-Note $c 'Nora has already replied with every letter, so this reply repeats an earlier one.' }
  $placed = Invoke-TypeReply $c $letter
  Add-EventEvidence $c $placed 'reply placed on the table'
  if (-not $placed) { Complete-Check $c 'fail' "the reply wasn't placed"; return }
  $replyId = ("$($placed.detail)" -split ':', 2)[1]
  $isNew = @($earlier | ForEach-Object { $_.id }) -notcontains $replyId
  Add-Evidence $c 'reply' ([ordered]@{ replyId = $replyId; typed = $letter; newThisRun = $isNew; noraRepliesBefore = $earlier.Count }) 'the reply placed in this run'
  $api1 = Test-ReplyOnServer $replyId
  Add-Evidence $c 'api' $api1 'server after placing'
  [void](Save-Shot 'check5-placed')
  [void](Save-SeatView 'live-room' 0 -16 'the live room: the reply standing on the table as a folded card (reference 03)')
  # 1. app restart
  Stop-App
  [void](Start-App -Record 'live-restarted')
  $r1 = $null
  if (Enter-LiveRoom $c 'after an app restart') { $r1 = Wait-EventSince $script:SessionMark 'reply' '^restored:' 20 }
  Add-EventEvidence $c $r1 'notes restored after an app restart'
  $afterApp = (Get-RestoredIds $r1) -contains $replyId
  [void](Save-Shot 'check5-after-app-restart')
  # 2. API restart (the app restarts too, so it reads everything again)
  Stop-App
  $svc = Restart-Services
  Add-Evidence $c 'service' $svc 'API and worker restarted (stop.ps1, start.ps1)'
  $api2 = Test-ReplyOnServer $replyId
  Add-Evidence $c 'api' $api2 'server after its restart'
  [void](Start-App -Record 'live-api-restarted')
  $r2 = $null
  if (Enter-LiveRoom $c 'after an API restart') { $r2 = Wait-EventSince $script:SessionMark 'reply' '^restored:' 20 }
  Add-EventEvidence $c $r2 'notes restored after an API restart'
  $afterApi = (Get-RestoredIds $r2) -contains $replyId
  [void](Save-Shot 'check5-after-api-restart')
  $ok = $api1.replyStored -and $api1.placementStored -and $afterApp -and $svc.apiDownAfterStop -and $svc.apiUpAfterStart -and $api2.replyStored -and $api2.placementStored -and $afterApi
  $summary = "reply $replyId placed ($(if ($isNew) { "new, typed '$letter'" } else { 'an earlier reply sent again' })); server has it $($api1.replyStored) (placed $($api1.placementStored)); back on the table after app restart $afterApp; API went down $($svc.apiDownAfterStop) and up $($svc.apiUpAfterStart); server still has it $($api2.replyStored); back after API restart $afterApi"
  if ($ok) { Complete-Check $c 'pass' $summary } else { Complete-Check $c 'fail' $summary }
}

function Get-RoomDecor([string]$RoomId) {
  return Invoke-RestMethod -Method Get -Uri "$ApiUrl/v1/rooms/$RoomId/decor" -Headers @{ Authorization = "Bearer $($script:NoraToken)" } -TimeoutSec 20
}

function Invoke-Check9 {
  $c = New-Check 9 'Furniture chosen in the marketplace by a simulated hand is kept across an app restart'
  if (-not $script:Seed -or -not $script:Seed.rooms -or -not $script:Seed.rooms.pair) { Complete-Check $c 'blocked' 'no seeded room'; return }
  $roomId = $script:Seed.rooms.pair
  # How the room is furnished now, asked of the server as Nora. The hand then picks a chair fabric that isn't
  # the current one, so every run makes a real change.
  $before = Get-RoomDecor $roomId
  $was = "$($before.choices.chairs.finish)"
  $want = @('sage', 'charcoal', 'cognac', 'cream' | Where-Object { $_ -ne $was })[0]
  Add-Evidence $c 'api' ([ordered]@{ version = $before.version; chairs = $before.choices.chairs }) 'the room on the server before'
  $live = $false
  try { $st = Wait-NextProbe; $live = (Test-AppRunning) -and "$($st.mode)" -eq 'live' -and "$($st.screen)" -eq 'room' } catch { }
  if (-not $live) {
    Stop-App
    Set-LiveSession
    [void](Start-App -Record 'live-market')
    if (-not (Enter-LiveRoom $c 'for the marketplace')) { Complete-Check $c 'fail' "couldn't open Nora's room"; return }
  }
  if (-not (Wait-Target 'dock:market' 15)) { Complete-Check $c 'fail' 'the dock has no Market button'; return }
  $mark = New-Mark
  $presses = @(
    @{ id = 'dock:market'; label = 'Market (on the dock at the table edge)'; next = 'market:tab-chairs' },
    @{ id = 'market:tab-chairs'; label = 'the Chairs tab'; next = "market:finish-chairs-$want" },
    @{ id = "market:finish-chairs-$want"; label = "the $want fabric"; next = 'market:keep' },
    @{ id = 'market:keep'; label = 'Keep'; next = '' }
  )
  foreach ($step in $presses) {
    $p = Invoke-HandPress $step.id $step.label
    Add-DriverOf $c $p
    Add-EventEvidence $c $p.event "$($step.id) pressed"
    if (-not $p.ok) { if ($p.note) { Add-Note $c $p.note }; Complete-Check $c 'fail' "couldn't press $($step.label)"; return }
    if ($step.id -eq "market:finish-chairs-$want") { [void](Save-Shot 'check9-trying'); [void](Save-SeatView 'market-trying' 0 -18 "the marketplace open, the chairs tried in $want") }
    if ($step.next -and -not (Wait-Target $step.next 10)) { Complete-Check $c 'fail' "after $($step.label): $($step.next) never showed"; return }
  }
  $tried = Find-EventSince $mark 'decor' "^try chairs=tub/$want"
  Add-EventEvidence $c $tried 'the fabric tried in the room (not kept yet)'
  $kept = Wait-EventSince $mark 'decor' '^kept v' 15
  Add-EventEvidence $c $kept 'kept'
  $after = Get-RoomDecor $roomId
  Add-Evidence $c 'api' ([ordered]@{ version = $after.version; chairs = $after.choices.chairs; updatedByNora = ("$($after.updatedBy)" -eq "$($script:NoraId)") }) 'the room on the server after Keep'
  [void](Wait-EventSince $mark 'screen' '^room$' 10)
  [void](Save-SeatView 'market-kept' 0 -10 "the live room with the chairs in $want, kept from the marketplace")
  # the app restarts and reads the room again
  Stop-App
  [void](Start-App -Record 'live-market-restarted')
  $shown = $null
  $state = $null
  if (Enter-LiveRoom $c 'after an app restart') {
    $shown = Wait-EventSince $script:SessionMark 'decor' '^shown v' 20
    $w = Wait-ProbeState { param($st) "$($st.decor)" -match "chairs=tub/$want" } 10
    $state = $w.state
  }
  Add-EventEvidence $c $shown 'furniture shown after the restart'
  Add-StateEvidence $c $state 'check9-restarted' @('screen', 'mode', 'room', 'decor', 'decorVersion', 'decorUnsaved')
  [void](Save-Shot 'check9-after-app-restart')
  $ok = [bool]$tried -and [bool]$kept -and "$($after.choices.chairs.finish)" -eq $want -and ([int]$after.version -gt [int]$before.version) `
    -and $null -ne $state -and ("$($state.decor)" -match "chairs=tub/$want") -and -not $state.decorUnsaved
  $summary = "chairs changed from '$was' to '$want' by hand in the marketplace and kept; server version $($before.version) -> $($after.version); after an app restart the room shows: $($state.decor)"
  if ($ok) { Complete-Check $c 'pass' $summary } else { Complete-Check $c 'fail' $summary }
}

function Rotate-HeadYaw([double]$Degrees) {
  $s0 = Wait-NextProbe
  $y0 = YawDeg (V3 $s0.headForward)
  $key = $SimKeys.YawLeft
  if ($Degrees -gt 0) { $key = $SimKeys.YawRight }
  Send-SimKey $key 300
  $s1 = Wait-NextProbe
  $turned = [math]::Abs((AngleDiff (YawDeg (V3 $s1.headForward)) $y0))
  if ($turned -lt 1.5) { return [pscustomobject]@{ ok = $false; note = 'the arrow keys did not turn the head'; before = $y0 } }
  $rate = $turned / 300.0
  $rest = [math]::Abs($Degrees) - $turned
  if ($rest -gt 2) { Send-SimKey $key ([int][math]::Min(4000, $rest / $rate)) }
  $s2 = Wait-NextProbe
  $y2 = YawDeg (V3 $s2.headForward)
  return [pscustomobject]@{ ok = $true; before = [math]::Round($y0, 1); after = [math]::Round($y2, 1); turned = [math]::Round((AngleDiff $y2 $y0), 1); state = $s2 }
}

function Invoke-Check6 {
  $c = New-Check 6 'Recenter, pause and exit'
  $inRoom = $false
  try { $inRoom = [bool](Find-Target (Wait-NextProbe) 'dock:recenter') } catch { }
  if (-not $inRoom) { $inRoom = (Enter-SampleRoomFresh $c) -and (Wait-Target 'dock:recenter' 15) }
  if (-not $inRoom) { Complete-Check $c 'fail' 'no room with a dock to work in'; return }
  # recenter: turn the head away from the table first, so the recenter has something to correct
  $turn = $null
  if (-not $Assist -and $script:Calib.ok) {
    $turn = Rotate-HeadYaw -35   # to the left: the left hand comes back toward the person, not out over the table
    if ($turn.ok) {
      Add-StateEvidence $c $turn.state 'check6-turned' @('head', 'headForward')
      Update-HandControl
    } else { Add-Note $c "Head not turned first: $($turn.note)" }
  } elseif (-not $NonInteractive) {
    Write-Host '>>> YOUR TURN: turn the simulated head about 30 degrees to one side (arrow keys with the headset selected).' -ForegroundColor Yellow
    [void](Wait-ProbeState { param($st) [math]::Abs((YawDeg (V3 $st.headForward))) -gt 15 } 60)
  }
  $yawBefore = YawDeg (V3 (Wait-NextProbe).headForward)
  $mark = New-Mark
  $rc = Invoke-HandPress 'dock:recenter' 'Recenter (dock)'
  Add-DriverOf $c $rc
  Add-EventEvidence $c $rc.event 'dock:recenter pressed'
  $re = Wait-EventSince $mark 'recenter' '' 5
  Add-EventEvidence $c $re 'recenter'
  Start-Sleep -Milliseconds 600
  $after = Wait-NextProbe
  Add-StateEvidence $c $after 'check6-recentered' @('head', 'headForward')
  $yawAfter = YawDeg (V3 $after.headForward)
  $recenterOk = $rc.ok -and [bool]$re -and [math]::Abs($yawAfter) -le 10
  # pause and resume
  $mark = New-Mark
  $pausedBefore = [bool](Wait-NextProbe).paused
  if ($pausedBefore) { Add-Note $c "The app was already paused before the pause button was pressed (see the 'pause' events in the session's events.jsonl)." }
  $pp = Invoke-HandPress 'dock:pause' 'Pause (dock)'
  Add-DriverOf $c $pp
  Add-EventEvidence $c $pp.event 'dock:pause pressed'
  Add-EventEvidence $c (Wait-EventSince $mark 'screen' '^pause$' 5) 'pause screen'
  $pst = Wait-ProbeState { param($st) $st.paused } 5
  Add-StateEvidence $c $pst.state 'check6-paused' @('paused', 'screen', 'audio', 'voicePlaying')
  [void](Save-Shot 'check6-paused')
  $mark = New-Mark
  $res = Invoke-HandPress 'pause:resume' 'Resume (pause screen)'
  Add-DriverOf $c $res
  Add-EventEvidence $c $res.event 'pause:resume pressed'
  $resumed = Wait-EventSince $mark 'pause' '^resume$' 5
  Add-EventEvidence $c $resumed 'resumed'
  $back = Wait-EventSince $mark 'screen' '^room$' 10
  Add-EventEvidence $c $back 'back in the room'
  $unp = Wait-ProbeState { param($st) -not $st.paused } 5
  Add-StateEvidence $c $unp.state 'check6-resumed' @('paused', 'screen')
  $pauseOk = $pp.ok -and $pst.ok -and $res.ok -and [bool]$resumed -and [bool]$back -and $unp.ok
  Add-Manual $c 'System pause' "Headset removed or the Meta button: the probe should log 'pause system' or 'pause focus' and sound should stop. Needs a headset, or the simulator's own control for it."
  # exit through the app's own Leave flow
  $mark = New-Mark
  $e1 = Invoke-HandPress 'dock:pause' 'Pause (dock)'
  $e2 = $null; $e3 = $null
  if ($e1.ok) { $e2 = Invoke-HandPress 'pause:exit' 'Leave SameSky (pause screen)' }
  if ($e2 -and $e2.ok) {
    Add-EventEvidence $c (Wait-EventSince $mark 'screen' '^exit$' 5) 'leave confirmation'
    $e3 = Invoke-HandPress 'exit:quit' 'Leave (confirmation)'
  }
  foreach ($r in @($e1, $e2, $e3)) { Add-DriverOf $c $r }
  $quit = Wait-EventSince $mark 'exit' '^quit$' 5
  Add-EventEvidence $c $quit 'exit'
  if ($quit) { Stop-Recording }   # as soon as the script knows the app has left (it was still drawing the hand back)
  $deadline = (Get-Date).AddSeconds(20)
  while ((Test-AppRunning) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 300 }
  $ended = -not (Test-AppRunning)
  Stop-Recording
  Save-SessionFiles
  Add-Evidence $c 'probe-stopped' ([ordered]@{ probeStoppedUpdating = $ended }) 'the app ended (its probe stopped updating)'
  $exitOk = [bool]$quit -and $ended
  $summary = "recenter: pressed $($rc.ok), event $([bool]$re), yaw $([math]::Round($yawBefore, 1)) -> $([math]::Round($yawAfter, 1)) deg; pause and resume $pauseOk; exit: event $([bool]$quit), app ended $ended"
  if ($recenterOk -and $pauseOk -and $exitOk) { Complete-Check $c 'pass' $summary } else { Complete-Check $c 'fail' $summary }
}

function Invoke-Check7 {
  $c = New-Check 7 'Hand tracking lost mid-press and found again: no ghost press'
  [void](Start-App -Record 'tracking')
  if (-not (Wait-Target 'launch:recenter' 30)) { Complete-Check $c 'fail' "the launch screen's Recenter button never appeared"; return }
  $hold = $null
  if (-not $Assist -and $script:Calib.ok) {
    $hold = Invoke-AutoPress -Id 'launch:recenter' -Accept '^launch:recenter$' -KeepIn
    $c.driver = 'script'
  } elseif (-not $NonInteractive) {
    Write-Host ">>> YOUR TURN: push the right index fingertip into 'Recenter' on the launch panel and keep it there (10 s)." -ForegroundColor Yellow
    Start-Sleep -Seconds 10
    $c.driver = 'operator'
  }
  $s0 = Wait-NextProbe
  $presses0 = [int]$s0.presses
  Add-StateEvidence $c $s0 'check7-before' @('hands', 'activeInput', 'presses', 'lastPress', 'handsAt')
  if ($hold -and $hold.held) { Add-Evidence $c 'probe-target' ([ordered]@{ target = 'launch:recenter'; downBeforeTrackingLoss = [bool]$hold.down }) 'was the button held down when tracking was dropped' }
  $mark = New-Mark
  $lost = Invoke-Native $MetavrCmd @('xrsim', 'input', 'source', '--hand', 'right', '--source', 'none') 60
  Add-Evidence $c 'command' $lost 'right-hand tracking off'
  $gone = Wait-ProbeState { param($st) -not $st.hands.right } 10
  Add-StateEvidence $c $gone.state 'check7-lost' @('hands', 'activeInput', 'presses', 'lastPress')
  if ($hold -and $hold.held) { Move-HandBy (VScale $hold.normal 0.10) }   # out of the button while untracked
  Start-Sleep -Seconds 2
  $back = Invoke-Native $MetavrCmd @('xrsim', 'input', 'sync', '--source', 'hand') 60
  Add-Evidence $c 'command' $back 'hand tracking back'
  $found = Wait-ProbeState { param($st) $st.hands.right } 15
  Start-Sleep -Seconds 3
  Update-Events
  $s1 = Wait-NextProbe
  Add-StateEvidence $c $s1 'check7-after' @('hands', 'activeInput', 'presses', 'lastPress')
  $events = @(Get-EventsSince $mark)
  $ghost = @($events | Where-Object { $_.kind -eq 'press' })
  $cancel = @($events | Where-Object { $_.kind -eq 'cancel' })
  foreach ($e in $ghost) { Add-EventEvidence $c $e 'press while tracking was lost or after it came back' }
  foreach ($e in $cancel) { Add-EventEvidence $c $e 'press cancelled by the tracking loss' }
  Add-Evidence $c 'probe-count' ([ordered]@{ pressesBefore = $presses0; pressesAfter = [int]$s1.presses; pressEvents = $ghost.Count; cancelEvents = $cancel.Count }) 'presses counted by the probe'
  if ($cancel.Count -eq 0) { Add-Note $c 'No cancel event: the fingertip may not have been inside the button when tracking dropped, so this shows no ghost press on loss and recovery, but not a cancelled press.' }
  [void](Save-Shot 'check7-after')
  Set-HandPose 'PoseDefault'
  $ok = $lost.exit -eq 0 -and $gone.ok -and $back.exit -eq 0 -and $found.ok -and $ghost.Count -eq 0 -and [int]$s1.presses -eq $presses0
  $summary = "right hand lost $($gone.ok) and back $($found.ok); presses during and after: $($ghost.Count) (count $presses0 -> $([int]$s1.presses)); cancels: $($cancel.Count)"
  if ($ok) { Complete-Check $c 'pass' $summary } else { Complete-Check $c 'fail' $summary }
  Stop-App
}

function Complete-Check8 {
  $c = New-Check 8 'MP4 of the simulator for every app session'
  $methods = @($script:Segments | ForEach-Object { $_.method } | Where-Object { $_ } | Sort-Object -Unique)
  $c.driver = 'script (' + $(if ($methods.Count) { ($methods | ForEach-Object { @{ metavr = 'metavr xrsim record'; window = 'FFmpeg window capture'; none = 'no recorder' }[$_] }) -join ', ' } else { 'metavr' }) + ')'
  if ($SkipRecording) { Complete-Check $c 'skipped' 'recording was turned off (-SkipRecording)'; return }
  if ($script:Segments.Count -eq 0) { Complete-Check $c 'blocked' 'no app session was recorded (the run stopped before one started)'; return }
  foreach ($seg in $script:Segments) {
    Add-Evidence $c 'recording' $seg $seg.label
    if ($seg.probeAtStart -and $seg.probeAtStart.Count -gt 0) { Add-Evidence $c 'probe-state' ([ordered]@{ fields = $seg.probeAtStart }) "app state when $($seg.label) started" }
  }
  if ($methods -contains 'window') { Add-Note $c "The simulator runtime has no MP4 recorder (metavr xrsim record says its build doesn't include the recording service), so FFmpeg recorded the simulator window: the 3D view as the simulator showed it, plus its side panel." }
  $good = @($script:Segments | Where-Object { $_.ok })
  Add-Manual $c 'Watch the recordings' 'Check each MP4 shows the steps above (the script only checks the files exist and have a length).'
  $summary = "$($good.Count) of $($script:Segments.Count) recordings saved"
  if ($script:Segments.Count -gt 0 -and $good.Count -eq $script:Segments.Count) { Complete-Check $c 'pass' $summary } else { Complete-Check $c 'fail' $summary }
}

# ---------------------------------------------------------------- report
function Format-Value($v) {
  if ($null -eq $v) { return 'null' }
  if ($v -is [string] -or $v -is [ValueType]) { return "$v" }
  return ($v | ConvertTo-Json -Compress -Depth 5)
}

function Format-Md([string]$Text) { return (("$Text" -replace '\|', '\|') -replace "[\r\n]+", ' ') }

function Write-ResultsMarkdown($Doc, [string]$Path) {
  $L = New-Object System.Collections.Generic.List[string]
  $L.Add("# Native acceptance run $RunName")
  $L.Add('')
  $clean = 'clean working tree'
  if (-not $Doc.workingTreeClean) { $clean = 'with uncommitted changes' }
  $apiText = "API $ApiUrl"
  if ($SampleOnly) { $apiText = 'run as a build with no server (`-SampleOnly`): the sample day only, no API' }
  $L.Add("$($RunStamp.ToString('d MMMM yyyy HH:mm', [Globalization.CultureInfo]::InvariantCulture)), commit ``$($Doc.commit)`` ($clean). $($Doc.target), profile ``$AppProfile``, $apiText.")
  $L.Add('')
  $L.Add('Evidence kind: **simulator-tested**. Meta XR Simulator with a simulated Meta Quest 3 and simulated hands. It is not a headset result, and says nothing about frame rate, comfort, spatial audio or real hand tracking on a Quest.')
  $L.Add('')
  $L.Add('| # | Check | Result | Driven by | What the probe saw |')
  $L.Add('|---|---|---|---|---|')
  foreach ($c in $Doc.checks) { $L.Add("| $($c.id) | $(Format-Md $c.name) | **$($c.status)** | $(Format-Md $c.driver) | $(Format-Md $c.summary) |") }
  $L.Add('')
  $L.Add('Execution order: ' + (($Doc.executionOrder | ForEach-Object { $_ -replace '^Invoke-', '' }) -join ', ') + '. The first minute is timed on a fresh profile, so checks 3 and 4 run before check 2.')
  $L.Add('')
  if ($script:Manual.Count -gt 0) {
    $L.Add('## Manual steps still to do')
    $L.Add('')
    foreach ($m in $script:Manual) { $L.Add("- [ ] Check $($m.check): **$($m.step)**. $($m.how)") }
    $L.Add('')
  }
  $L.Add('## Details')
  foreach ($c in $Doc.checks) {
    $L.Add('')
    $L.Add("### $($c.id). $($c.name): $($c.status)")
    $L.Add('')
    foreach ($e in $c.evidence) {
      $label = ''
      if ($e.label) { $label = " ($($e.label))" }
      switch ($e.kind) {
        'probe-event' { $L.Add("- probe event$($label): ``$($e.data.kind) $($e.data.detail)``, input $($e.data.input), t $($e.data.t) s, app session $($e.data.session)") }
        'probe-state' {
          $fields = @()
          foreach ($k in @($e.data.fields.Keys)) { $fields += "$k=$(Format-Value $e.data.fields[$k])" }
          $file = ''
          if ($e.data.file) { $file = " [$($e.data.file -replace '^docs/native/evidence/[^/]+/', '')]" }
          $L.Add("- probe state$($label): $(Format-Md ($fields -join ', '))$file")
        }
        'missing' { $L.Add("- **missing**: $($e.label)") }
        'command' { $L.Add("- command$($label): ``$($e.data.command)``, exit $($e.data.exit)$(if ($e.data.output) { ': ' + (Format-Md $e.data.output) })") }
        default { $L.Add("- $($e.kind)$($label): $(Format-Md (Format-Value $e.data))") }
      }
    }
    foreach ($n in $c.notes) { $L.Add("- note: $(Format-Md $n)") }
  }
  $L.Add('')
  $L.Add('## Setup')
  $L.Add('')
  foreach ($s in $Doc.setup) { $L.Add("- $($s.step): $(if ($s.ok) { 'ok' } else { '**failed**' })") }
  $cal = $Doc.calibration
  $L.Add("- Hand control: $(if ($cal.ok) { 'calibrated' } else { 'not automatic' }); device '$($cal.device)'; fingertip offset $(Format-Value $cal.fingertip) ($($cal.fingertipSource))$(if ($cal.note) { '; ' + $cal.note })")
  if ($script:Notes.Count -gt 0) {
    $L.Add('')
    $L.Add('## Notes')
    $L.Add('')
    foreach ($n in $script:Notes) { $L.Add("- $(Format-Md $n)") }
  }
  $L.Add('')
  $L.Add('## Files')
  $L.Add('')
  foreach ($a in $script:Artifacts) { $L.Add("- ``$($a.file -replace '^docs/native/evidence/[^/]+/', '')``: $($a.what), $($a.bytes) bytes, sha256 ``$($a.sha256.Substring(0, 16))...``") }
  Write-Utf8 $Path (($L -join "`n") + "`n")
}

function Save-Results {
  $checks = @(foreach ($k in @($script:Checks.Keys | Sort-Object { [int]$_ })) { $script:Checks[$k] })
  $cal = [ordered]@{}
  foreach ($k in @($script:Calib.Keys)) { if ($k -ne 'axes' -and $k -ne 'keyVelocity') { $cal[$k] = $script:Calib[$k] } }
  if ($cal.fingertip) { $cal.fingertip = Round3 $cal.fingertip }
  $targetText = 'Unity Play mode (editor) in Meta XR Simulator'
  if ($Target -eq 'WindowsBuild') { $targetText = 'Windows desktop build in Meta XR Simulator' }
  $doc = [ordered]@{
    schema = 'samesky-native-acceptance/1'; run = $RunName; evidenceKind = 'simulator-tested'
    startedAt = $script:StartedAt; finishedAt = (Get-Date).ToString('o')
    commit = $script:Commit; workingTreeClean = $script:Clean
    target = $targetText; profile = $AppProfile; api = $(if ($SampleOnly) { '' } else { $ApiUrl }); sampleOnly = [bool]$SampleOnly
    options = [ordered]@{ assist = [bool]$Assist; nonInteractive = [bool]$NonInteractive; recording = (-not $SkipRecording); firstMinuteSec = $FirstMinuteSec }
    environment = $script:Environment
    simulatorKeys = $SimKeys
    setup = $script:Setup
    calibration = $cal
    executionOrder = $script:Order
    checks = $checks
    manual = $script:Manual
    artifacts = $script:Artifacts
    notes = $script:Notes
  }
  New-Item -ItemType Directory -Force $EvidenceDir | Out-Null
  $jsonPath = Join-Path $EvidenceDir "$RunName.json"
  $mdPath = Join-Path $EvidenceDir "$RunName.md"
  Write-Utf8 $jsonPath ($doc | ConvertTo-Json -Depth 12)
  Write-ResultsMarkdown $doc $mdPath
  Write-Host ''
  Write-Host "Results: $(Get-RelPath $mdPath) and .json; files in $(Get-RelPath $RunDir)/" -ForegroundColor Cyan
}

# ---------------------------------------------------------------- run
if ($Library) { return }
$script:StartedAt = (Get-Date).ToString('o')
$script:Commit = ''
$script:Clean = $false
$script:Environment = $null
$script:AppStartFailures = 0
$script:PlayKeysNoted = $false
Write-Step "SameSky native acceptance run ($RunName, $Target)"
try {
  Initialize-Run
  Initialize-Services
  Initialize-Simulator
  $steps = @('Invoke-Check1', 'Invoke-FirstMinute', 'Invoke-Check2', 'Invoke-Check5', 'Invoke-Check9', 'Invoke-Check6', 'Invoke-Check7')
  if ($SampleOnly) { $steps = @($steps | Where-Object { $_ -notin 'Invoke-Check5', 'Invoke-Check9' }) }   # both need the service
  if ($OnlyIds.Count) {
    $covers = @{ 'Invoke-FirstMinute' = @(3, 4); 'Invoke-Check2' = @(2); 'Invoke-Check5' = @(5); 'Invoke-Check9' = @(9); 'Invoke-Check6' = @(6); 'Invoke-Check7' = @(7) }
    $steps = @('Invoke-Check1') + @($steps | Where-Object { $covers.ContainsKey($_) -and @($covers[$_] | Where-Object { $OnlyIds -contains $_ }).Count -gt 0 })
  }
  if ($Renders) {
    # in the sample room that check 2 leaves open when it ran, else in a session of its own after the checks
    $at = [array]::IndexOf($steps, 'Invoke-Check2')
    if ($at -ge 0) { $steps = @($steps[0..$at]) + @('Invoke-RenderTour') + @($steps | Select-Object -Skip ($at + 1)) }
    else { $steps = @($steps) + @('Invoke-RenderTour') }
  }
  foreach ($step in $steps) {
    if ($script:AppStartFailures -ge 2) { [void]$script:Notes.Add('The app failed to start twice, so the remaining checks were not run.'); break }
    [void]$script:Order.Add($step)
    try { & $step }
    catch {
      $msg = Protect-Text $_.Exception.Message
      Write-Host "    $step stopped: $msg" -ForegroundColor Red
      foreach ($c in @($script:Checks.Values | Where-Object { $_.status -eq 'running' })) { Add-Note $c "Stopped by an error: $msg"; Complete-Check $c 'error' $c.summary }
    }
  }
} catch {
  $msg = Protect-Text $_.Exception.Message
  [void]$script:Notes.Add("The run stopped early: $msg")
  Write-Host "Run stopped: $msg" -ForegroundColor Red
} finally {
  try { Stop-Recording } catch { }
  try { if (Test-AppRunning) { Stop-App } } catch { }
  try { Complete-Check8 } catch { }
  $names = @{ 1 = 'XR session active: simulated Meta Quest 3 with hand input'; 2 = 'Dock button pressed by a simulated hand'; 3 = 'Cup grabbed by a simulated hand'; 4 = 'Voice note heard within the first minute of a fresh start (sample day)'; 5 = 'Reply on the table survives an app restart and an API restart'; 6 = 'Recenter, pause and exit'; 7 = 'Hand tracking lost mid-press and found again: no ghost press'; 8 = 'MP4 of the simulator for every app session'; 9 = 'Furniture chosen in the marketplace by a simulated hand is kept across an app restart' }
  foreach ($id in @(1..7) + 9) {
    if (-not $script:Checks.Contains("$id")) {
      $c = New-Check $id $names[$id]
      if ($SampleOnly -and $id -in 5, 9) { Complete-Check $c 'skipped' 'needs the SameSky service: not part of a build with no server (-SampleOnly)' }
      elseif ($OnlyIds.Count -and $OnlyIds -notcontains $id -and $id -ne 1) { Complete-Check $c 'skipped' 'not selected (-Only)' }
      else { Complete-Check $c 'blocked' 'not run: the run stopped before this check' }
    }
  }
  Save-Results
}
