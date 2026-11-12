# Builds the native app and records what was built (Unity version, package id, size, SHA-256, commit, log).
#   -Target Android  (default) Quest APK, development build unless -Release
#   -Target Windows  desktop OpenXR build for Meta XR Simulator
#   -ApiUrl          the API the build talks to (headset: this PC's LAN address; release: https only)
param([ValidateSet('Android', 'Windows')][string]$Target = 'Android', [string]$ApiUrl = '', [switch]$Release)
$requestedApi = $ApiUrl
. "$PSScriptRoot/common.ps1"   # sets $ApiUrl to this PC's local address, so keep what was asked for apart
$localApi = $ApiUrl
$ApiUrl = $requestedApi
if (-not $ApiUrl) {
  if ($Target -eq 'Windows') { $ApiUrl = $localApi }
  elseif (-not $Release) {
    $lan = Get-LanAddress
    if (-not $lan) { throw 'No LAN address found for the headset to reach this PC; pass -ApiUrl http://<address>:8000' }
    $ApiUrl = "http://${lan}:$ApiPort"
  }
  else { throw 'Release builds need -ApiUrl https://...' }
}
$env:SAMESKY_COMMIT = (& git -C $Repo rev-parse --short HEAD).Trim()
$stamp = Get-Date -Format 'yyyyMMdd-HHmm'
$kind = if ($Release) { 'release' } else { 'dev' }
$out = if ($Target -eq 'Android') { Join-Path $Project "Builds/Android/SameSky-$kind-$stamp.apk" } else { Join-Path $Project 'Builds/Windows/SameSky.exe' }
$log = Join-Path $Project "Builds/build-$Target-$stamp.log"
New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null
$unityArgs = @('-batchmode', '-nographics', '-projectPath', "`"$Project`"", '-executeMethod', "SameSky.EditorTools.BuildScript.$Target", "-sameskyApi=$ApiUrl", "-sameskyOut=`"$out`"", '-logFile', "`"$log`"", '-quit')
if ($Release) { $unityArgs += '-sameskyRelease' }
if (Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 -and $_.MainWindowTitle -match '^quest - ' }) { throw 'The Unity Editor has native/quest open: close it first (a batch build needs the project to itself).' }
# The build bakes the API address into Resources/SameSkyConfig.json; for a headset that is this PC's LAN address,
# which doesn't belong in the repository, so the committed file is put back afterwards.
$config = Join-Path $Project 'Assets/SameSky/Resources/SameSkyConfig.json'
$configBefore = if (Test-Path $config) { [IO.File]::ReadAllBytes($config) } else { $null }
Write-Step "Building $Target ($kind) against $ApiUrl"
# Wait for Unity itself, not with Start-Process -Wait: that also waits for every process Unity started, and an
# Android build leaves an adb server running, so the script would never return.
try {
  $proc = Start-Process -FilePath $Unity -ArgumentList $unityArgs -PassThru
  $proc.WaitForExit()
}
finally { if ($null -ne $configBefore) { [IO.File]::WriteAllBytes($config, $configBefore) } }
if ($proc.ExitCode -ne 0 -or -not (Test-Path $out)) { Get-Content $log -Tail 60; throw "build failed ($($proc.ExitCode)); log: $log" }
$hash = (Get-FileHash -Algorithm SHA256 $out).Hash.ToLowerInvariant()
$recordPath = [IO.Path]::ChangeExtension($out, '.build.json')
$record = Get-Content $recordPath -Raw | ConvertFrom-Json
$record | Add-Member sha256 $hash -Force
$record | Add-Member commit $env:SAMESKY_COMMIT -Force
$record | Add-Member log $log -Force
$record | ConvertTo-Json | Out-File -Encoding utf8 $recordPath
Write-Host "Built $out"
Write-Host "SHA-256 $hash"
Write-Host "Record $recordPath"
