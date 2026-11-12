# Captures the footage for the demo film "I Saved You a Seat" (docs/demo) from the running native app.
#
# The app runs in Unity Play mode inside Meta XR Simulator, exactly as in the acceptance run, and every hand action is
# made through the simulator's own controls (this script dot-sources scripts/native/acceptance.ps1 for that). What is
# added is a capture-only camera in the Editor (Assets/SameSky/Scripts/App/FilmRig.cs): it draws the same scene, in
# the same state, from the seated eye or along a slow move, and writes the frames and the app's own sound to
# demo-build/capture/<shot>.mp4 and .wav, with <shot>.json (frame count, clock) and <shot>.events.json (what the
# probe saw during the shot: presses, grabs, the voice note starting) for the edit.
#
#   powershell -ExecutionPolicy Bypass -File scripts/demo/film.ps1                 every shot, in story order
#   powershell -ExecutionPolicy Bypass -File scripts/demo/film.ps1 -Shots cup,reply
#   powershell -ExecutionPolicy Bypass -File scripts/demo/film.ps1 -Serve          a session that runs the commands
#                                                                                   dropped into demo-build/console
# Leave the keyboard and mouse alone while it runs: the simulator's hands are driven with them.
param([string]$Shots = 'all', [switch]$Serve, [switch]$KeepApp)

. "$PSScriptRoot/../native/acceptance.ps1" -Library -NonInteractive -SkipRecording

$FilmRoot = Join-Path $Repo 'demo-build'
$CaptureDir = Join-Path $FilmRoot 'capture'
$ConsoleDir = Join-Path $FilmRoot 'console'
# the acceptance functions write their files to the run folder: keep them out of docs/native/evidence
$EvidenceDir = $FilmRoot
$RunName = 'capture'
$RunDir = $CaptureDir
$UnityLog = Join-Path $DevLogs 'unity-film.log'
New-Item -ItemType Directory -Force $CaptureDir, $ConsoleDir | Out-Null

# ---------------------------------------------------------------- the capture camera
# A camera key: seat space, metres from the seated eye (x right, y up, z toward the table). Either -Look (a point to
# look at) or -Yaw and -Pitch in degrees.
function K([double]$T, [double[]]$At = @(0, 0, 0), [double[]]$Look = $null, [double]$Yaw = 0, [double]$Pitch = -12, [double]$Fov = 56) {
  $k = [ordered]@{ t = $T; at = $At; yaw = $Yaw; pitch = $Pitch; fov = $Fov }
  if ($Look) { $k.look = $Look }
  return $k
}

# -Wide records 2560 x 1440: the edit then frames a 1920 x 1080 window inside it (a slow tilt from the dock to a panel,
# say) without enlarging a pixel.
# -Follow is the first-person view: the camera sits where the simulated headset is and turns with it (eased), looking
# down by the keys' pitch. Without it the camera stays where the keys put it, in seat space.
# -World takes the keys in world coordinates (see Get-PanelView).
function Start-Film([string]$Name, [object[]]$Keys, [double]$Seconds = 0, [bool]$Audio = $true, [switch]$Wide, [switch]$Follow, [double]$Ease = 0.5, [switch]$World) {
  $mark = New-Mark
  $size = if ($Wide) { @(2560, 1440) } else { @(1920, 1080) }
  $req = [ordered]@{ action = 'start'; name = $Name; dir = $CaptureDir; ffmpeg = (Get-FfmpegExe); seconds = $Seconds; fps = 30; width = $size[0]; height = $size[1]; crf = 12; audio = $Audio; keys = @($Keys) }
  if ($Follow) { $req.follow = 'head'; $req.ease = $Ease }
  if ($World) { $req.space = 'world' }
  Write-Utf8 (Join-Path $ProbeDirectory 'film-request.json') ($req | ConvertTo-Json -Depth 6)
  $e = Wait-EventSince $mark 'film' ('^start ' + [regex]::Escape($Name) + '$') 15
  if (-not $e) { throw "the film camera didn't start for '$Name' (is this the Editor, with FilmRig compiled?)" }
  Write-Host "    filming $Name" -ForegroundColor DarkGray
  return [pscustomobject]@{ name = $Name; mark = $mark; seconds = $Seconds }
}

# Ends a shot (or waits for one of fixed length) and saves what the probe saw during it.
function Stop-Film($Film, [double]$Tail = 0) {
  if ($Tail -gt 0) { Start-Sleep -Milliseconds ([int]($Tail * 1000)) }
  if ($Film.seconds -le 0) { Write-Utf8 (Join-Path $ProbeDirectory 'film-request.json') '{ "action": "stop" }' }
  $deadline = (Get-Date).AddSeconds([math]::Max(120, $Film.seconds + 120))
  $e = $null
  do {
    Update-Events
    $e = Find-EventSince $Film.mark 'film' ('^stop ' + [regex]::Escape($Film.name) + ' ')
    if (-not $e) { $e = Find-EventSince $Film.mark 'film-failed' ('^' + [regex]::Escape($Film.name) + ':') }
    if (-not $e) { Start-Sleep -Milliseconds 150 }
  } while (-not $e -and (Get-Date) -lt $deadline)
  if (-not $e) { throw "the film camera didn't finish '$($Film.name)'" }
  if ($e.kind -ne 'film') { throw "filming '$($Film.name)' failed: $($e.detail)" }
  $events = @(Get-EventsSince $Film.mark | Where-Object { $_.kind -notmatch '^film' } | ForEach-Object { [ordered]@{ t = $_.t; kind = $_.kind; detail = $_.detail; input = $_.input } })
  Write-Utf8 (Join-Path $CaptureDir "$($Film.name).events.json") (ConvertTo-Json -InputObject $events -Depth 4)
  Write-Host "    $($Film.name): $($e.detail -replace '^stop \S+ ', '')" -ForegroundColor DarkGray
}

# The seated eye, looking where a person at the table looks: the camera for first-person shots.
function Seat([double]$Pitch = -16, [double]$Yaw = 0, [double]$Fov = 58, [double]$Seconds = 0, [double]$PushTo = 0) {
  if ($PushTo -gt 0 -and $Seconds -gt 0) { return @((K 0 -Yaw $Yaw -Pitch $Pitch -Fov $Fov), (K $Seconds -Yaw $Yaw -Pitch $Pitch -Fov $PushTo)) }
  return @((K 0 -Yaw $Yaw -Pitch $Pitch -Fov $Fov))
}

# A camera set square to a panel. Panels turn to face the person when they open, and the simulated person is rarely
# on the seat at that moment (the simulator moves the whole body to bring a fingertip to a button), so from the seat a
# panel can look skewed. The probe says where the panel's buttons are and which way they face: this puts the camera
# on that axis. Returns world-space keys for Start-Film -World: $Distance metres in front of the middle of the
# buttons matching $Pattern, raised by $Up along the panel, pushing in to $PushTo metres over $Seconds when given.
function Get-PanelView([string]$Pattern, [double]$Distance = 0.55, [double]$Up = 0.0, [double]$Fov = 50, [double]$Seconds = 0, [double]$PushTo = 0) {
  $ts = @((Wait-NextProbe).targets | Where-Object { "$($_.id)" -match $Pattern })
  if ($ts.Count -eq 0) { throw "nothing on screen matches $Pattern" }
  $min = V3 $ts[0].at; $max = V3 $ts[0].at
  foreach ($t in $ts) { $p = V3 $t.at; for ($i = 0; $i -lt 3; $i++) { if ($p[$i] -lt $min[$i]) { $min[$i] = $p[$i] }; if ($p[$i] -gt $max[$i]) { $max[$i] = $p[$i] } } }
  $n = VNorm (V3 $ts[0].normal)
  $b = Get-PlaneBasis $n
  $centre = VAdd (VScale (VAdd $min $max) 0.5) (VScale $b.v $Up)
  $from = VAdd $centre (VScale $n $Distance)
  if ($PushTo -gt 0 -and $Seconds -gt 0) { return @((K 0 -At (Round3 $from) -Look (Round3 $centre) -Fov $Fov), (K $Seconds -At (Round3 (VAdd $centre (VScale $n $PushTo))) -Look (Round3 $centre) -Fov $Fov)) }
  return @((K 0 -At (Round3 $from) -Look (Round3 $centre) -Fov $Fov))
}

# ---------------------------------------------------------------- the simulated person
# In this simulator switching one hand's tracking off switches off both: a way to take the hands out of a shot of
# the room without moving the head (the panels face the head).
function Hide-Hands {
  [void](Invoke-Native $MetavrCmd @('xrsim', 'input', 'source', '--hand', 'right', '--source', 'none') 60)
  [void](Wait-ProbeState { param($st) -not $st.hands.right -and -not $st.hands.left } 8)
}

function Show-Hands([string]$Pose = 'PosePoke') {
  [void](Invoke-Native $MetavrCmd @('xrsim', 'input', 'sync', '--source', 'hand') 60)
  [void](Wait-ProbeState { param($st) $st.hands.right -and $st.hands.left } 12)
  Start-Sleep -Milliseconds 800
  Set-HandPose $Pose
}

# -Keep carries on with the sample day as the last session left it (the note heard, a reply on the table); without
# it the sample starts over: the chair empty, the message waiting in the cup.
function Start-FilmSession([switch]$Keep) {
  if ($Keep) { [void](Start-App) } else { [void](Start-App -FreshProfile) }
  [void](Invoke-Native $MetavrCmd @('xrsim', 'input', 'sync', '--source', 'hand') 60)
  [void](Wait-ProbeState { param($st) $st.hands.right -and $null -ne $st.pokeAt -and $null -ne $st.pokeAt.right } 12)
  Initialize-HandControl
  if (-not $script:Calib.ok) { Start-Sleep -Seconds 2; $script:Calib.note = ''; Initialize-HandControl }
  if (-not $script:Calib.ok) { throw "no hand control: $($script:Calib.note)" }
  Write-Host "    hands calibrated ($($script:Calib.fingertipSource))" -ForegroundColor DarkGray
}

function Enter-SampleRoom {
  if (-not (Wait-Target 'launch:sample' 30)) { throw "the launch screen's 'Try the sample day' never appeared" }
  $p = Invoke-HandPress 'launch:sample' 'Try the sample day'
  if (-not $p.ok) { throw "couldn't press 'Try the sample day'" }
  if (-not (Wait-EventSince $script:SessionMark 'screen' '^room$' 30)) { throw 'the sample room did not open' }
  if (-not (Wait-Target 'object:cup' 20)) { throw 'no cup in the sample room' }
  [void](Wait-EventSince $script:SessionMark 'cue' '' 4)
}

function Press([string]$Id, [string]$Label = '') {
  if (-not $Label) { $Label = $Id }
  if (-not (Wait-Target $Id 12)) { throw "'$Id' is not on screen" }
  $p = Invoke-HandPress $Id $Label
  if (-not $p.ok) { throw "couldn't press $Id" }
  return $p
}

# Types on the spatial keyboard, one fingertip press per key. The head turn that keeps the left hand clear of other
# buttons (acceptance.ps1) is left out here: on the keyboard the left index comes down beside the keys, not on them,
# and a turning head would swing the first-person camera.
function Type-Text([string]$Text) {
  $script:NoAsideTurn = $true
  try {
    foreach ($ch in $Text.ToCharArray()) {
      if ($ch -eq '?') { [void](Press 'key:symbols' '?123'); [void](Press 'key:?' '?'); continue }
      $id = switch ([string]$ch) { ' ' { 'key:space' } '.' { 'key:period' } ',' { 'key:comma' } default { 'key:' + ([string]$ch).ToLowerInvariant() } }
      [void](Press $id ([string]$ch))
    }
  } finally { $script:NoAsideTurn = $false }
}

# ---------------------------------------------------------------- a console for directing a session by hand
# Runs each file dropped into demo-build/console as in-*.ps1, in this session, and writes what it printed beside it.
function Start-Console {
  Write-Host "console ready: demo-build/console/in-*.ps1" -ForegroundColor Cyan
  Write-Utf8 (Join-Path $ConsoleDir 'ready.txt') (Get-Date).ToString('o')
  while ($true) {
    $next = Get-ChildItem $ConsoleDir -Filter 'in-*.ps1' -ErrorAction SilentlyContinue | Sort-Object Name | Select-Object -First 1
    if (-not $next) { Start-Sleep -Milliseconds 300; continue }
    $outFile = Join-Path $ConsoleDir (($next.BaseName -replace '^in-', 'out-') + '.txt')
    $text = Get-Content $next.FullName -Raw
    Remove-Item $next.FullName -Force
    if ($text -match '^\s*#\s*quit\b') { Write-Utf8 $outFile 'bye'; break }
    $said = ''
    try { $said = (. ([scriptblock]::Create($text)) *>&1 | Out-String) }
    catch { $said += "`nERROR: $(Protect-Text $_.Exception.Message)`n$($_.ScriptStackTrace)" }
    try { Write-Utf8 $outFile $said } catch { }
  }
}

# ---------------------------------------------------------------- run
Write-Step "SameSky demo film capture ($CaptureDir)"
Initialize-Run
Initialize-Services
Initialize-Simulator
try {
  if ($Serve) { Start-Console }
  else {
    . "$PSScriptRoot/shots.ps1"
    Invoke-FilmShots $Shots
  }
} finally {
  if (-not $KeepApp) { try { if (Test-AppRunning) { Stop-App } } catch { } }
}
