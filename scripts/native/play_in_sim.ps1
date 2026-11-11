# Opens the native project in the Unity Editor and enters Play mode against Meta XR Simulator.
# The simulator is selected for the editor process only (XR_RUNTIME_JSON); the system OpenXR runtime is unchanged.
# Then: in the simulator's Inputs panel choose Meta Quest 3 and hands (or run Set-SimQuest3Hands from sim.ps1).
. "$PSScriptRoot/sim.ps1"
if (-not $XrsimRuntimeJson) { throw 'Meta XR Simulator not found (see preflight.ps1)' }
if (-not (Test-Path $Unity)) { throw "Unity not found at $Unity" }
if (-not (Test-Api)) { & "$PSScriptRoot/start.ps1" }
Start-Sim
$env:XR_RUNTIME_JSON = $XrsimRuntimeJson
$log = Join-Path $DevLogs 'unity-play.log'
Start-Process -FilePath $Unity -ArgumentList '-projectPath', "`"$Project`"", '-executeMethod', 'SameSky.EditorTools.SimRun.Play', '-logFile', "`"$log`""
Write-Host "Unity is opening (log: $log). Play mode starts automatically; the app appears in the simulator window."
