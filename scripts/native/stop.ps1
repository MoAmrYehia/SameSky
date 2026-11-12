# Stops the API and worker started by start.ps1 (whole process trees). Data stays in api/data/native-dev.
. "$PSScriptRoot/common.ps1"
if (-not (Test-Path $PidFile)) { Write-Host 'Nothing to stop.'; exit 0 }
$pids = Get-Content $PidFile -Raw | ConvertFrom-Json
foreach ($name in 'worker', 'api') {
  $id = $pids.$name
  if (Get-Process -Id $id -ErrorAction SilentlyContinue) { & taskkill.exe /PID $id /T /F | Out-Null; Write-Host "stopped $name" }
  else { Write-Host "$name was not running" }
}
Remove-Item $PidFile
for ($i = 0; $i -lt 20 -and (Test-Api); $i++) { Start-Sleep -Milliseconds 250 }
