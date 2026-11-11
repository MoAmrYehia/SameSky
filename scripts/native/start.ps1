# Starts the real local SameSky services for the native app: the API (all interfaces, port 8000, so a headset on
# the same Wi-Fi can reach it) and the persistent worker, on the isolated native-dev database and media folder.
#   powershell -ExecutionPolicy Bypass -File scripts/native/start.ps1
# The services run detached (they keep running after this window closes); stop them with stop.ps1.
. "$PSScriptRoot/common.ps1"
if (Test-Api) { Write-Host "API already running at $ApiUrl"; exit 0 }
Set-DevEnv
Write-Step 'Applying database migrations (native-dev database)'
Push-Location (Join-Path $Repo 'api')
& $Python -m alembic upgrade head
if ($LASTEXITCODE -ne 0) { throw 'migrations failed' }
Pop-Location

function Start-Detached($name, $workdir, $command) {
  # A small launcher keeps the dev environment and log redirection together; WMI starts it fully detached,
  # so no console or pipe handles are inherited from whoever called this script.
  $lines = @('@echo off')
  foreach ($k in $DevEnv.Keys) { $lines += "set `"$k=$($DevEnv[$k])`"" }
  $lines += "cd /d `"$workdir`""
  $lines += "$command > `"$DevLogs\$name.log`" 2>&1"
  $launcher = Join-Path $DevRoot "run-$name.cmd"
  $lines | Out-File -Encoding ascii $launcher
  # Own hidden console + own process group: Ctrl+C or a closing terminal elsewhere never reaches the services.
  $startup = New-CimInstance -ClassName Win32_ProcessStartup -ClientOnly -Property @{ CreateFlags = [uint32]0x210; ShowWindow = [uint16]0 }
  $r = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{ CommandLine = "cmd.exe /c `"$launcher`""; CurrentDirectory = $workdir; ProcessStartupInformation = $startup }
  if ($r.ReturnValue -ne 0) { throw "could not start $name ($($r.ReturnValue))" }
  return $r.ProcessId
}

Write-Step "Starting API on 0.0.0.0:$ApiPort"
$apiPid = Start-Detached 'api' $Repo "`"$Python`" -m uvicorn samesky.main:app --app-dir api --host 0.0.0.0 --port $ApiPort"
Write-Step 'Starting worker (media processing, recaps, avatar jobs, retention)'
$workerPid = Start-Detached 'worker' (Join-Path $Repo 'api') "`"$Python`" -m samesky.worker --interval 2"
@{ api = $apiPid; worker = $workerPid; started = (Get-Date).ToString('o') } | ConvertTo-Json | Out-File -Encoding utf8 $PidFile
for ($i = 0; $i -lt 60 -and -not (Test-Api); $i++) { Start-Sleep -Milliseconds 500 }
if (-not (Test-Api)) { throw "API did not come up; see $DevLogs\api.log" }
$lan = [System.Net.Dns]::GetHostAddresses([System.Net.Dns]::GetHostName()) | Where-Object { $_.AddressFamily -eq 'InterNetwork' -and -not $_.ToString().StartsWith('127.') -and -not $_.ToString().StartsWith('169.254') } | Select-Object -First 1
Write-Host "API ready: $ApiUrl (Editor and Meta XR Simulator)"
if ($lan) { Write-Host "Headset on this Wi-Fi: http://${lan}:$ApiPort (development builds only; allow it through Windows Firewall)" }
Write-Host "Logs: $DevLogs"
