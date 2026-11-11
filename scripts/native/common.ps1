# Shared settings for the native Quest scripts (Windows PowerShell 5.1+). Dot-source it: . "$PSScriptRoot/common.ps1"
$ErrorActionPreference = 'Stop'
$Repo = (Resolve-Path "$PSScriptRoot/../..").Path
$Project = Join-Path $Repo 'native/quest'
$UnityVersion = (Get-Content (Join-Path $Project 'ProjectSettings/ProjectVersion.txt') | Select-String 'm_EditorVersion:' | ForEach-Object { ($_ -split ':')[1].Trim() } | Select-Object -First 1)
# Unity installed by Hub (Program Files) or per user without admin rights (scripts/native/install_unity_user.ps1)
$Unity = @(
  (Join-Path $env:ProgramFiles "Unity/Hub/Editor/$UnityVersion/Editor/Unity.exe"),
  (Join-Path $env:USERPROFILE "Unity/Hub/Editor/$UnityVersion/Editor/Unity.exe")
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $Unity) { $Unity = Join-Path $env:USERPROFILE "Unity/Hub/Editor/$UnityVersion/Editor/Unity.exe" }
# Meta XR Simulator: Meta's MSI install (Program Files) or the per-user unpack (install_xrsim_user.ps1)
$XrsimRoot = @(
  $env:SAMESKY_XRSIM_ROOT,
  (Join-Path $env:ProgramFiles 'MetaXRSimulator'),
  (Join-Path $env:USERPROFILE 'SameSkyTools/MetaXRSimulator')
) | Where-Object { $_ -and (Test-Path $_) } | ForEach-Object {
  Get-ChildItem $_ -Recurse -Filter meta_openxr_simulator.json -ErrorAction SilentlyContinue | Select-Object -First 1 | ForEach-Object DirectoryName
} | Select-Object -First 1
$XrsimRuntimeJson = if ($XrsimRoot) { Join-Path $XrsimRoot 'meta_openxr_simulator.json' } else { $null }
$Python = Join-Path $Repo 'api/.venv/Scripts/python.exe'
$Node = (Get-Command node -ErrorAction SilentlyContinue).Source
if (-not $Node) { $Node = Join-Path $env:ProgramFiles 'nodejs/node.exe' }

# Isolated development data: never the default api/data/samesky.db, never anyone's real data.
$DevRoot = Join-Path $Repo 'api/data/native-dev'
$DevLogs = Join-Path $DevRoot 'logs'
New-Item -ItemType Directory -Force $DevRoot, $DevLogs | Out-Null
$SecretFile = Join-Path $DevRoot 'secret.txt'
if (-not (Test-Path $SecretFile)) {
  $bytes = New-Object byte[] 48; [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
  [Convert]::ToBase64String($bytes) | Out-File -Encoding ascii -NoNewline $SecretFile
}
$DevEnv = @{
  DATABASE_URL           = 'sqlite:///' + ((Join-Path $DevRoot 'samesky.db').Replace([char]92, [char]47))
  SAMESKY_MEDIA_ROOT     = (Join-Path $DevRoot 'media')
  SAMESKY_SECRET_KEY     = (Get-Content $SecretFile -Raw).Trim()
  SAMESKY_PUBLIC_API_URL = ''
  SAMESKY_CORS_ORIGINS   = 'http://localhost:5173'
}
$ApiPort = 8000
$ApiUrl = "http://127.0.0.1:$ApiPort"
$PidFile = Join-Path $DevRoot 'services.json'

function Set-DevEnv { foreach ($k in $DevEnv.Keys) { [Environment]::SetEnvironmentVariable($k, $DevEnv[$k], 'Process') } }

function Write-Step($text) { Write-Host "==> $text" -ForegroundColor Cyan }

function Test-Api {
  try { (Invoke-RestMethod -Uri "$ApiUrl/v1/health" -TimeoutSec 3).ok -eq $true } catch { $false }
}

# The Meta XR SDK packages, pinned in native/quest/packages.lock.txt (name version sha1).
function Get-PackageLock {
  Get-Content (Join-Path $Project 'packages.lock.txt') | Where-Object { $_ -and -not $_.StartsWith('#') } | ForEach-Object {
    $p = $_ -split '\s+'; [pscustomobject]@{ Name = $p[0]; Version = $p[1]; Sha1 = $p[2]; File = Join-Path $Project ".packages/$($p[0])-$($p[1]).tgz" }
  }
}

function Get-Sha1($path) { (Get-FileHash -Algorithm SHA1 $path).Hash.ToLowerInvariant() }
