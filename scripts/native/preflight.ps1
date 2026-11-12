# Checks this machine for everything the native Quest app needs, and says exactly what is missing.
#   powershell -ExecutionPolicy Bypass -File scripts/native/preflight.ps1      (exit code 1 if a blocker is found)
. "$PSScriptRoot/common.ps1"
$ErrorActionPreference = 'Continue'
$rows = New-Object System.Collections.Generic.List[object]
function Check($area, $ok, $detail, $fix = '', [switch]$Optional) {
  $status = if ($ok) { 'ok' } elseif ($Optional) { 'warn' } else { 'BLOCKED' }
  $rows.Add([pscustomobject]@{ Area = $area; Status = $status; Detail = $detail; Fix = $(if ($ok) { '' } else { $fix }) })
}

$os = Get-CimInstance Win32_OperatingSystem
Check 'Host' ($os.OSArchitecture -match '64') "$($os.Caption) $($os.Version) $($os.OSArchitecture), $env:PROCESSOR_ARCHITECTURE" 'Meta XR Simulator needs 64-bit Windows 10/11 or Apple Silicon macOS'
$gpus = (Get-CimInstance Win32_VideoController | ForEach-Object Name) -join '; '
Check 'GPU' ($gpus -match 'NVIDIA|AMD|Radeon|Intel') $gpus 'A DirectX 11 GPU is required; a dedicated NVIDIA/AMD GPU is recommended'
$ram = [math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB, 1)
Check 'Memory' ($ram -ge 15) "$ram GB" 'Unity + simulator + API run best with 16 GB' -Optional
$free = [math]::Round((Get-PSDrive C).Free / 1GB)
Check 'Disk' ($free -ge 30) "$free GB free on C:" 'Unity, Android SDK/NDK and builds need about 30 GB'

Check 'Unity Hub' (Test-Path "$env:ProgramFiles\Unity Hub\Unity Hub.exe") "$env:ProgramFiles\Unity Hub" 'Install Unity Hub 3.x from unity.com'
Check "Unity $UnityVersion" (Test-Path $Unity) $Unity "Install $UnityVersion with Unity Hub (pinned in native/quest/ProjectSettings/ProjectVersion.txt)"
$android = Join-Path (Split-Path $Unity) 'Data/PlaybackEngines/AndroidPlayer'
Check 'Android Build Support' (Test-Path $android) $android 'Unity Hub > Installs > Add modules > Android Build Support'
Check 'OpenJDK' (Test-Path (Join-Path $android 'OpenJDK')) 'bundled with Android Build Support' 'Add the OpenJDK module'
Check 'Android SDK and NDK' ((Test-Path (Join-Path $android 'SDK')) -and (Test-Path (Join-Path $android 'NDK'))) 'bundled with Android Build Support' "Add the Android SDK and NDK Tools module (accept Google's licence in Hub)"
$licensed = (Test-Path "$env:ProgramData\Unity\Unity_lic.ulf") -or [bool](Get-ChildItem "$env:LOCALAPPDATA\Unity\licenses" -ErrorAction SilentlyContinue | Select-Object -First 1)
Check 'Unity licence' $licensed 'Personal/Pro entitlement on this machine' 'Sign in to Unity Hub and activate a licence (Hub > Preferences > Licenses)'

Check 'Meta XR Simulator' ([bool]$XrsimRoot) $(if ($XrsimRoot) { $XrsimRoot } else { 'not installed' }) 'Install Meta XR Simulator (MSI with admin approval, or per user: scripts/native/install_xrsim_user.ps1)'
$runtimes = try { (Get-Item 'HKLM:\SOFTWARE\Khronos\OpenXR\1\AvailableRuntimes' -ErrorAction Stop).Property -join '; ' } catch { '' }
Check 'OpenXR runtimes' (($runtimes -match 'imulator') -or $XrsimRuntimeJson) $(if ($runtimes) { $runtimes } elseif ($XrsimRuntimeJson) { "none system-wide; per-process manifest $XrsimRuntimeJson" } else { 'none registered' }) 'The MSI registers the simulator runtime; per-user installs use XR_RUNTIME_JSON per process' -Optional

foreach ($p in Get-PackageLock) {
  $present = Test-Path $p.File
  $ok = $present -and ((Get-Sha1 $p.File) -eq $p.Sha1)
  Check $p.Name $ok "$($p.Version) $(if ($present) { 'present' } else { 'missing' })" 'Run scripts/native/bootstrap.ps1 (downloads and verifies it)'
}

Check 'Python venv' (Test-Path $Python) $Python 'python -m venv api/.venv; api/.venv/Scripts/pip install -r api/requirements-dev.txt'
Check 'Node.js' (Test-Path $Node) $Node 'Install Node.js LTS (used to export the sample and characters)'
$listening = Get-NetTCPConnection -LocalPort $ApiPort -State Listen -ErrorAction SilentlyContinue
$apiUp = Test-Api
Check "Port $ApiPort" ((-not $listening) -or $apiUp) $(if ($apiUp) { 'SameSky API is running' } elseif ($listening) { 'in use by another program' } else { 'free' }) "Free port $ApiPort or stop the other program"

$rows | Format-Table -AutoSize -Wrap | Out-String -Width 220 | Write-Host
$blocked = @($rows | Where-Object Status -eq 'BLOCKED')
if ($blocked.Count) { Write-Host "$($blocked.Count) blocker(s)." -ForegroundColor Yellow; exit 1 } else { Write-Host 'Ready.' -ForegroundColor Green }
