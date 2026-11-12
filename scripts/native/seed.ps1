# Creates deterministic development accounts in the isolated native-dev database (never real data).
# Accounts and their dev-only passwords are listed in scripts/native/dev-seed.json.
#   -Reset   delete the native-dev database and media first (development data only)
param([switch]$Reset)
. "$PSScriptRoot/common.ps1"
if ($Reset) {
  & "$PSScriptRoot/stop.ps1"
  Remove-Item -Recurse -Force (Join-Path $DevRoot 'samesky.db'), (Join-Path $DevRoot 'media') -ErrorAction SilentlyContinue
  Write-Host 'native-dev data reset'
}
if (-not (Test-Api)) { & "$PSScriptRoot/start.ps1" }
& $Python (Join-Path $PSScriptRoot 'seed_dev.py') --api $ApiUrl --seed (Join-Path $PSScriptRoot 'dev-seed.json')
