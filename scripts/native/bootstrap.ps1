# One-time (repeatable) setup of the native project on this machine:
#   1. download the pinned Meta XR SDK packages and verify their checksums (native/quest/packages.lock.txt)
#   2. API virtualenv and dependencies
#   3. bundled art and offline sample (scripts/native/build_native_assets.py)
#   4. Unity: resolve packages, configure XR/URP/player settings and build the scene (batch mode)
# Needs Unity installed and licensed (see preflight.ps1). Meta's package server drops slow downloads after a few
# minutes and can't resume, so each package is retried until its checksum matches.
. "$PSScriptRoot/common.ps1"
Write-Step 'Meta XR SDK packages'
New-Item -ItemType Directory -Force (Join-Path $Project '.packages') | Out-Null
foreach ($p in Get-PackageLock) {
  for ($i = 1; $i -le 8 -and -not ((Test-Path $p.File) -and (Get-Sha1 $p.File) -eq $p.Sha1); $i++) {
    Write-Host "  $($p.Name) $($p.Version) (attempt $i)"
    & curl.exe -s -L --speed-limit 20000 --speed-time 60 -o "$($p.File).part" "https://npm.developer.oculus.com/$($p.Name)/-/$($p.Name)-$($p.Version).tgz"
    Move-Item -Force "$($p.File).part" $p.File
  }
  if ((Get-Sha1 $p.File) -ne $p.Sha1) { throw "$($p.Name): checksum mismatch after retries" }
  Write-Host "  ok $($p.Name)"
}
Write-Step 'API virtualenv'
if (-not (Test-Path $Python)) {
  & py -3 -m venv (Join-Path $Repo 'api/.venv')
  & $Python -m pip install -r (Join-Path $Repo 'api/requirements-dev.txt')
}
Write-Step 'Bundled art and offline sample'
& $Python (Join-Path $PSScriptRoot 'build_native_assets.py')
if ($LASTEXITCODE -ne 0) { throw 'asset build failed' }
Write-Step 'Unity project setup (batch mode; the first run resolves packages and can take a while)'
if (-not (Test-Path $Unity)) { throw "Unity $UnityVersion not found at $Unity" }
$log = Join-Path $DevLogs 'unity-setup.log'
$proc = Start-Process -FilePath $Unity -ArgumentList '-batchmode', '-nographics', '-projectPath', "`"$Project`"", '-executeMethod', 'SameSky.EditorTools.ProjectSetup.Run', '-quit', '-logFile', "`"$log`"" -Wait -PassThru
if ($proc.ExitCode -ne 0) { Get-Content $log -Tail 40; throw "Unity setup failed; see $log" }
Write-Host "Done. Log: $log"
