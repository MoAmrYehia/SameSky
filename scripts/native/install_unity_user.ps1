# Installs the pinned Unity editor and its Android modules for the current Windows user, without administrator
# rights, from installers and archives already downloaded from Unity and Google (Unity's public release API
# lists every URL, size and checksum used here). Use it when Unity Hub's own install can't get an admin
# approval; Hub can then "Locate" the editor. Nothing system-wide is changed.
#   powershell -ExecutionPolicy Bypass -File scripts/native/install_unity_user.ps1 -Downloads <folder>
param([string]$Downloads = (Join-Path $env:USERPROFILE 'SameSkyTools/downloads/unity'))
. "$PSScriptRoot/common.ps1"
$root = Join-Path $env:USERPROFILE "Unity/Hub/Editor/$UnityVersion"
$player = Join-Path $root 'Editor/Data/PlaybackEngines/AndroidPlayer'

function Assert-Md5($file, $b64) {
  $hex = (Get-FileHash -Algorithm MD5 $file).Hash
  $bytes = for ($i = 0; $i -lt $hex.Length; $i += 2) { [Convert]::ToByte($hex.Substring($i, 2), 16) }
  if ([Convert]::ToBase64String([byte[]]$bytes) -ne $b64) { throw "$file failed its MD5 check" }
}

function Install-Nsis($exe, $md5, $target) {
  Assert-Md5 $exe $md5
  # "highestAvailable" installers would ask for elevation; RunAsInvoker installs as the current user instead.
  $env:__COMPAT_LAYER = 'RunAsInvoker'
  try { $p = Start-Process -FilePath $exe -ArgumentList '/S', "/D=$target" -Wait -PassThru }
  finally { Remove-Item Env:__COMPAT_LAYER -ErrorAction SilentlyContinue }
  if ($p.ExitCode -ne 0) { throw "$exe exited with $($p.ExitCode)" }
}

function Expand-To($zip, $dest, $renameFrom = $null, $renameTo = $null) {
  New-Item -ItemType Directory -Force $dest | Out-Null
  & "$env:SystemRoot\System32\tar.exe" -xf $zip -C $dest   # Windows' bsdtar extracts zip quickly
  if ($LASTEXITCODE -ne 0) { throw "could not extract $zip" }
  if ($renameFrom -and (Test-Path $renameFrom)) {
    if (Test-Path $renameTo) {
      Get-ChildItem $renameFrom -Force | Move-Item -Destination $renameTo -Force
      Remove-Item $renameFrom -Recurse -Force
    } else { Move-Item $renameFrom $renameTo }
  }
}

Write-Step "Unity $UnityVersion editor -> $root"
if (-not (Test-Path (Join-Path $root 'Editor/Unity.exe'))) {
  Install-Nsis (Join-Path $Downloads "UnitySetup64-$UnityVersion.exe") 'qk6SSXDsB0/n2iy3EbP9SQ==' $root
}
Write-Step 'Android Build Support'
if (-not (Test-Path $player)) {
  Install-Nsis (Join-Path $Downloads "UnitySetup-Android-Support-for-Editor-$UnityVersion.exe") 'yHiVUmpdEKwKRraju3JjVA==' $root
}
Write-Step 'OpenJDK 17'
if (-not (Test-Path (Join-Path $player 'OpenJDK/bin/java.exe'))) { Expand-To (Join-Path $Downloads 'jdk17.zip') (Join-Path $player 'OpenJDK') }
Write-Step 'Android SDK (platform-tools, build-tools 36, platforms 34-37, command-line tools)'
$sdk = Join-Path $player 'SDK'
Expand-To (Join-Path $Downloads 'android-sdk-ndk-tools.zip') $sdk
if (-not (Test-Path "$sdk/platform-tools/adb.exe")) { Expand-To (Join-Path $Downloads 'platform-tools_r37.0.1-win.zip') $sdk }
if (-not (Test-Path "$sdk/build-tools/36.0.0")) { Expand-To (Join-Path $Downloads 'build-tools_r36_windows.zip') "$sdk/build-tools" "$sdk/build-tools/android-16" "$sdk/build-tools/36.0.0" }
foreach ($z in 'platform-34-ext7_r02.zip', 'platform-35_r01.zip', 'platform-36_r02.zip', 'platform-37.0_r02.zip') { Expand-To (Join-Path $Downloads $z) "$sdk/platforms" }
if (-not (Test-Path "$sdk/cmdline-tools/16.0")) { Expand-To (Join-Path $Downloads 'commandlinetools-win-12266719_latest.zip') "$sdk/cmdline-tools" "$sdk/cmdline-tools/cmdline-tools" "$sdk/cmdline-tools/16.0" }
Write-Step 'Android NDK r27c'
if (-not (Test-Path (Join-Path $player 'NDK/source.properties'))) { Expand-To (Join-Path $Downloads 'android-ndk-r27c-windows.zip') (Join-Path $player 'NDK') (Join-Path $player 'NDK/android-ndk-r27c') (Join-Path $player 'NDK') }
Write-Step 'CMake 3.22.1 (Unity 6 Android builds need it)'
if (-not (Test-Path "$sdk/cmake/3.22.1/bin/cmake.exe")) {
  # Running this script means you accept Google's Android SDK License Terms (as Unity Hub asks for its Android
  # module); sdkmanager reads that acceptance from licenses/android-sdk-license, the standard CI way to record it.
  New-Item -ItemType Directory -Force "$sdk/licenses" | Out-Null
  [IO.File]::WriteAllText("$sdk/licenses/android-sdk-license", "`n24333f8a63b6825ea9c5514f83c2829b004d1fee`nd56f5187479451eabf01fb78af6dfcb131a6481e", (New-Object System.Text.UTF8Encoding $false))
  $env:JAVA_HOME = Join-Path $player 'OpenJDK'
  cmd /c "`"$sdk/cmdline-tools/16.0/bin/sdkmanager.bat`" --sdk_root=`"$sdk`" --install cmake;3.22.1"
  if (-not (Test-Path "$sdk/cmake/3.22.1/bin/cmake.exe")) { throw 'CMake 3.22.1 did not install (sdkmanager, see above)' }
}
Write-Step 'Registering the editor with Unity Hub'
$hub = "$env:ProgramFiles\Unity Hub\Unity Hub.exe"
if (Test-Path $hub) { & $hub -- --headless editors --add (Join-Path $root 'Editor/Unity.exe') 2>$null | Out-Null }
Write-Host "Unity $UnityVersion installed for $env:USERNAME at $root"
