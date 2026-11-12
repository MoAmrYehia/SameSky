# Meta XR Simulator for the current user, without administrator rights.
#
# The normal path is Meta's MSI, which installs to Program Files and needs an admin approval (UAC). When nobody
# can approve that (for example on a remote session), this unpacks the same signed MSI into a folder you own
# (an MSI "administrative install": files only, nothing registered system-wide). It then registers the
# Windows App Runtime 1.5 packages the simulator window needs, for this user, with Meta's bundled script.
#
# The system-wide OpenXR runtime is NOT changed. Apps pick the simulator per process with
# XR_RUNTIME_JSON=<root>\meta_openxr_simulator.json (scripts/native/sim.ps1 does this), or, in the Unity
# Editor, via the Meta XR Simulator activation (also per process).
#   powershell -ExecutionPolicy Bypass -File scripts/native/install_xrsim_user.ps1 -Msi <meta_xr_simulator_v207.1.msi>
param(
  [string]$Msi = (Join-Path $env:APPDATA 'metavr/tools/downloads/meta_xr_simulator_v207.1.msi'),
  [string]$Target = (Join-Path $env:USERPROFILE 'SameSkyTools/MetaXRSimulator')
)
$ErrorActionPreference = 'Stop'
$sig = Get-AuthenticodeSignature $Msi
if ($sig.Status -ne 'Valid' -or $sig.SignerCertificate.Subject -notmatch 'Meta Platforms') { throw "$Msi is not validly signed by Meta Platforms ($($sig.Status))" }
$copy = Join-Path $env:TEMP 'samesky-xrsim.msi'   # msiexec dislikes long or busy paths
Copy-Item $Msi $copy -Force
$log = Join-Path $env:TEMP 'samesky-xrsim-extract.log'
$p = Start-Process msiexec.exe -ArgumentList "/a `"$copy`" /qn TARGETDIR=`"$Target`" /l*v `"$log`"" -Wait -PassThru
if ($p.ExitCode -ne 0) { throw "msiexec /a failed ($($p.ExitCode)); see $log" }
$root = Get-ChildItem $Target -Recurse -Filter meta_openxr_simulator.json | Select-Object -First 1 | ForEach-Object DirectoryName
if (-not $root) { throw 'meta_openxr_simulator.json not found after extraction' }
Write-Host "Unpacked to $root"
$wars = Join-Path $root 'WindowsAppRuntime'
Get-ChildItem $wars -Filter *.msix | ForEach-Object {
  $s = Get-AuthenticodeSignature $_.FullName
  if ($s.Status -ne 'Valid' -or $s.SignerCertificate.Subject -notmatch 'Microsoft Corporation') { throw "$($_.Name) is not validly signed by Microsoft" }
}
& cmd.exe /c "`"$wars\install.bat`"" | Out-Null   # per-user Add-AppxPackage; a newer installed version is fine
Write-Host "Runtime manifest: $root\meta_openxr_simulator.json"
Write-Host "Start the simulator window: metavr xrsim app launch --app-path `"$root\MetaXRSimulator.exe`""
