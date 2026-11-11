# Builds the native app and records what was built (Unity version, package id, size, SHA-256, commit, log).
#   -Target Android  (default) Quest APK, development build unless -Release
#   -Target Windows  desktop OpenXR build for Meta XR Simulator
#   -ApiUrl          the API the build talks to (headset: this PC's LAN address; release: https only)
param([ValidateSet('Android', 'Windows')][string]$Target = 'Android', [string]$ApiUrl = '', [switch]$Release)
. "$PSScriptRoot/common.ps1"
if (-not $ApiUrl) {
  if ($Target -eq 'Windows') { $ApiUrl = $script:ApiUrl }
  elseif (-not $Release) {
    $lan = [System.Net.Dns]::GetHostAddresses([System.Net.Dns]::GetHostName()) | Where-Object { $_.AddressFamily -eq 'InterNetwork' -and -not $_.ToString().StartsWith('127.') -and -not $_.ToString().StartsWith('169.254') } | Select-Object -First 1
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
Write-Step "Building $Target ($kind) against $ApiUrl"
$proc = Start-Process -FilePath $Unity -ArgumentList $unityArgs -Wait -PassThru
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
