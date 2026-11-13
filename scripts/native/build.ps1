# Builds the native app and records what was built (Unity version, package id, size, SHA-256, commit, log).
#   -Target Android  (default) Quest APK, development build unless -Release
#   -Target Windows  desktop OpenXR build for Meta XR Simulator
#   -ApiUrl          the API the build talks to (headset: this PC's LAN address; release: https only)
#   -SampleOnly      a build with no server at all: it offers the sample day and nothing that needs an account
#                    (the build for the competition's judges while the API isn't hosted)
#   -Release         a release build: not debuggable, signed with your keystore. Needs -ApiUrl https://... or
#                    -SampleOnly, and the keystore in environment variables: run scripts/native/release.ps1, which
#                    asks you for the password and sets them for that one build
#   -DebugSigned     with -Release: sign with Android's debug key instead of a keystore. A check build, for looking
#                    at what a release build contains. It can't be uploaded
#   -VersionCode     Android version code. Meta's dashboard wants a higher one with every upload, so a release build
#                    gets one from the date and time (yyMMddHH and the tenth of the hour) unless you give one
# An Android build is then read back (aapt2, apksigner) and compared with Meta's manifest rules: target API level 34,
# Quest 3 and 3S as supported devices, and for a release build not debuggable and not signed with the debug key.
param(
  [ValidateSet('Android', 'Windows')][string]$Target = 'Android',
  [string]$ApiUrl = '',
  [switch]$Release,
  [switch]$SampleOnly,
  [switch]$DebugSigned,
  [int]$VersionCode = 0
)
$requestedApi = $ApiUrl
. "$PSScriptRoot/common.ps1"   # sets $ApiUrl to this PC's local address, so keep what was asked for apart
$localApi = $ApiUrl
$ApiUrl = $requestedApi
if ($DebugSigned -and -not $Release) { throw '-DebugSigned goes with -Release (a development build is always signed with the debug key).' }
if ($SampleOnly -and $Target -ne 'Android') { throw '-SampleOnly is for the Quest APK.' }
if ($SampleOnly) {
  if ($ApiUrl) { throw 'Give -SampleOnly or -ApiUrl, not both.' }
}
elseif (-not $ApiUrl) {
  if ($Target -eq 'Windows') { $ApiUrl = $localApi }
  elseif (-not $Release) {
    $lan = Get-LanAddress
    if (-not $lan) { throw 'No LAN address found for the headset to reach this PC; pass -ApiUrl http://<address>:8000' }
    $ApiUrl = "http://${lan}:$ApiPort"
  }
  else { throw 'Release builds need -ApiUrl https://... or -SampleOnly (a build with no server: the sample day only).' }
}
if ($Release -and -not $DebugSigned) {
  $missing = @('SAMESKY_KEYSTORE', 'SAMESKY_KEYSTORE_PASS', 'SAMESKY_KEY_ALIAS', 'SAMESKY_KEY_PASS') | Where-Object { -not [Environment]::GetEnvironmentVariable($_) }
  if ($missing) { throw "A release build is signed with your keystore, and $($missing -join ', ') isn't set. Run scripts/native/release.ps1: it asks for the password and makes the build." }
  if (-not (Test-Path $env:SAMESKY_KEYSTORE)) { throw 'The keystore file named in SAMESKY_KEYSTORE was not found.' }
}
if ($Release -and $Target -eq 'Android' -and $VersionCode -le 0) {
  $now = (Get-Date).ToUniversalTime()
  $VersionCode = [int]($now.ToString('yyMMddHH')) * 10 + [int][math]::Floor($now.Minute / 6)
}
$env:SAMESKY_COMMIT = (& git -C $Repo rev-parse --short HEAD).Trim()
$stamp = Get-Date -Format 'yyyyMMdd-HHmm'
$kind = if ($Release -and $DebugSigned) { 'releasecheck' } elseif ($Release) { 'release' } else { 'dev' }
if ($SampleOnly) { $kind += '-sampleonly' }
$out = if ($Target -eq 'Android') { Join-Path $Project "Builds/Android/SameSky-$kind-$stamp.apk" } else { Join-Path $Project 'Builds/Windows/SameSky.exe' }
$log = Join-Path $Project "Builds/build-$Target-$stamp.log"
New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null
$unityArgs = @('-batchmode', '-nographics', '-projectPath', "`"$Project`"", '-executeMethod', "SameSky.EditorTools.BuildScript.$Target", "-sameskyApi=$ApiUrl", "-sameskyOut=`"$out`"", '-logFile', "`"$log`"", '-quit')
if ($Release) { $unityArgs += '-sameskyRelease' }
if ($SampleOnly) { $unityArgs += '-sameskySampleOnly' }
if ($DebugSigned) { $unityArgs += '-sameskyDebugSigned' }
if ($VersionCode -gt 0) { $unityArgs += "-sameskyVersionCode=$VersionCode" }
if (Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 -and $_.MainWindowTitle -match '^quest - ' }) { throw 'The Unity Editor has native/quest open: close it first (a batch build needs the project to itself).' }
# The build bakes the API address into Resources/SameSkyConfig.json (for a headset, this PC's LAN address), and a
# release build names its keystore and version code in the player settings. None of that belongs in the repository,
# so both files are put back afterwards.
$restore = @{}
foreach ($f in @('Assets/SameSky/Resources/SameSkyConfig.json', 'ProjectSettings/ProjectSettings.asset')) {
  $path = Join-Path $Project $f
  if (Test-Path $path) { $restore[$path] = [IO.File]::ReadAllBytes($path) }
}

# For a build signed with a keystore, Unity writes the keystore's password into the Gradle project it generates
# (Library/Bee/Android/Prj: on this PC only, never committed). This takes it out again, then looks through the
# build's text files for it and says so if it is still somewhere.
function Remove-KeystorePassword {
  $secret = $env:SAMESKY_KEYSTORE_PASS
  if (-not $secret) { return }
  $prj = Join-Path $Project 'Library/Bee/Android/Prj'
  $removed = 0
  if (Test-Path $prj) {
    Get-ChildItem $prj -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.Extension -in '.gradle', '.properties' } | ForEach-Object {
      $text = [IO.File]::ReadAllText($_.FullName)
      # only where a password is assigned: a password can be an ordinary word that also occurs elsewhere
      $clean = [regex]::Replace($text, '(storePassword|keyPassword)(\s*=?\s*)([''"])[^''"\r\n]*\3', '$1$2$3$3')
      if ($clean -ne $text) { [IO.File]::WriteAllText($_.FullName, $clean); $removed++ }
    }
  }
  $left = @()
  if ($secret.Length -ge 6) {
    $textFiles = '.gradle', '.properties', '.json', '.txt', '.log', '.xml', '.cmd', '.bat', '.rsp', '.yaml'
    foreach ($root in @($prj, (Split-Path $log)) | Where-Object { Test-Path $_ }) {
      Get-ChildItem $root -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.Length -lt 5MB -and $_.Extension -in $textFiles } | ForEach-Object {
        try { if ([IO.File]::ReadAllText($_.FullName).Contains($secret)) { $left += $_.FullName } } catch { }
      }
    }
  }
  if ($removed) { Write-Host "    took the keystore password out of $removed generated Gradle file(s)" }
  if ($left) {
    $shown = (@($left | Select-Object -First 5) | ForEach-Object { $_.Replace($Project, 'native/quest') }) -join ', '
    Write-Host "    the text of the keystore password still occurs in $($left.Count) build file(s), for example: $shown. If it is an ordinary word that may be chance; otherwise delete those files." -ForegroundColor Yellow
  }
}

$server = if ($SampleOnly) { 'no server: the sample day only' } else { "against $ApiUrl" }
Write-Step "Building $Target ($kind), $server"
# Wait for Unity itself, not with Start-Process -Wait: that also waits for every process Unity started, and an
# Android build leaves an adb server running, so the script would never return.
try {
  $proc = Start-Process -FilePath $Unity -ArgumentList $unityArgs -PassThru
  $proc.WaitForExit()
}
finally {
  foreach ($path in $restore.Keys) { [IO.File]::WriteAllBytes($path, $restore[$path]) }
  if ($Release -and -not $DebugSigned) { Remove-KeystorePassword }
}
if ($proc.ExitCode -ne 0 -or -not (Test-Path $out)) { Get-Content $log -Tail 60; throw "build failed ($($proc.ExitCode)); log: $log" }
$hash = (Get-FileHash -Algorithm SHA256 $out).Hash.ToLowerInvariant()
$recordPath = [IO.Path]::ChangeExtension($out, '.build.json')
$record = Get-Content $recordPath -Raw | ConvertFrom-Json
$record | Add-Member sha256 $hash -Force
$record | Add-Member commit $env:SAMESKY_COMMIT -Force
$record | Add-Member log $log -Force

# Read the APK back: what it says about itself is what the dashboard will see.
$problems = @()
if ($Target -eq 'Android') {
  $android = Join-Path (Split-Path $Unity) 'Data/PlaybackEngines/AndroidPlayer'
  $buildTools = Get-ChildItem (Join-Path $android 'SDK/build-tools') -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1
  $aapt = if ($buildTools) { Join-Path $buildTools.FullName 'aapt2.exe' } else { $null }
  if ($aapt -and (Test-Path $aapt)) {
    $was = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    try {
      $badging = (& $aapt dump badging $out 2>$null) -join "`n"
      $tree = (& $aapt dump xmltree --file AndroidManifest.xml $out 2>$null) -join "`n"
      $signer = ''
      $apksigner = Join-Path $buildTools.FullName 'apksigner.bat'
      $jdk = Join-Path $android 'OpenJDK'
      if ((Test-Path $apksigner) -and (Test-Path $jdk)) {
        $javaHome = $env:JAVA_HOME; $env:JAVA_HOME = $jdk
        try { $signer = ((& $apksigner verify --print-certs $out 2>$null) | Where-Object { $_ -match 'Signer #1 certificate DN' } | Select-Object -First 1) -replace '^.*certificate DN:\s*', '' }
        finally { $env:JAVA_HOME = $javaHome }
      }
    } finally { $ErrorActionPreference = $was }
    $facts = [ordered]@{
      versionCode      = if ($badging -match "versionCode='(\d+)'") { [int]$Matches[1] } else { $null }
      minSdk           = if ($badging -match "minSdkVersion:'(\d+)'") { [int]$Matches[1] } else { $null }
      targetSdk        = if ($badging -match "targetSdkVersion:'(\d+)'") { [int]$Matches[1] } else { $null }
      architectures    = if ($badging -match "native-code: (.+)") { ($Matches[1] -replace "'", '').Trim() } else { $null }
      debuggable       = [bool]($tree -match 'android:debuggable\([^)]*\)=true')
      supportedDevices = if ($tree -match 'Raw: "com\.oculus\.supportedDevices"\)\s*\n\s*A: \S*android:value\([^)]*\)="([^"]*)"') { $Matches[1] } else { $null }
      signer           = $signer
    }
    $record | Add-Member apk ([pscustomobject]$facts) -Force
    Write-Host "    version code $($facts.versionCode), min API $($facts.minSdk), target API $($facts.targetSdk), $($facts.architectures), devices $($facts.supportedDevices), debuggable $($facts.debuggable)"
    if ($facts.signer) { Write-Host "    signed by: $($facts.signer)" }
    if ($facts.targetSdk -ne 34) { $problems += "target API level is $($facts.targetSdk); Meta's manifest rules ask for 34" }
    if ($facts.supportedDevices -ne 'quest3|quest3s') { $problems += "supported devices are '$($facts.supportedDevices)', not quest3|quest3s" }
    if ($facts.architectures -ne 'arm64-v8a') { $problems += "native code is '$($facts.architectures)', not arm64-v8a alone" }
    if ($Release) {
      if ($facts.debuggable) { $problems += 'the build is debuggable' }
      if (-not $DebugSigned -and (-not $facts.signer -or $facts.signer -match 'Android Debug')) { $problems += "the build isn't signed with your keystore (signer: '$($facts.signer)')" }
    }
  }
  else { Write-Host '    aapt2 was not found in Unity''s Android SDK: the APK was not read back.' -ForegroundColor Yellow }

  # A build for other people must not carry this PC with it. Two things did, before they were taken out
  # (Editor/ReleasePrivacy.cs, BuildScript.HomePathMap): this PC's address on the local network, in a settings file
  # of the SDK's developer tools, and the Windows user name, in the file paths of Unity's IL2CPP runtime sources.
  # Every file in a release APK is searched for both, as plain text and as UTF-16.
  if ($Release) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $needles = [ordered]@{}
    if ($env:USERNAME) {
      $needles["Users/$($env:USERNAME)/"] = 'the Windows user folder'
      $needles["Users\$($env:USERNAME)\"] = 'the Windows user folder'
    }
    try {
      Get-NetIPAddress -AddressFamily IPv4 -ErrorAction Stop | Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } | ForEach-Object { $needles[$_.IPAddress] = "this PC's network address" }
    } catch { }
    $latin1 = [Text.Encoding]::GetEncoding(28591)   # one character per byte, so a binary file can be searched as text
    $found = @{}
    $zip = [IO.Compression.ZipFile]::OpenRead($out)
    $entryCount = 0
    try {
      $entryCount = $zip.Entries.Count
      foreach ($entry in $zip.Entries) {
        if ($entry.Length -eq 0) { continue }
        $stream = $entry.Open()
        try { $memory = New-Object IO.MemoryStream; $stream.CopyTo($memory); $text = $latin1.GetString($memory.ToArray()) }
        finally { $stream.Dispose() }
        foreach ($needle in $needles.Keys) {
          $wide = ($needle.ToCharArray() -join "`0")
          if ($text.IndexOf($needle, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or $text.IndexOf($wide, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            $what = $needles[$needle]
            if (-not $found.ContainsKey($what)) { $found[$what] = @() }
            if ($found[$what] -notcontains $entry.FullName) { $found[$what] += $entry.FullName }
          }
        }
        $text = $null
      }
    } finally { $zip.Dispose() }
    if ($found.Count) {
      foreach ($what in $found.Keys) { $problems += "$what is inside the APK ($(@($found[$what] | Select-Object -First 3) -join ', '))" }
      $record | Add-Member privacy 'failed: see the build output' -Force
    }
    else {
      Write-Host "    searched all $entryCount files in the APK: no path under this PC's user folder and none of its network addresses"
      $record | Add-Member privacy "no user-folder path and no local network address in any of the APK's files" -Force
    }
  }
}
$record | ConvertTo-Json -Depth 5 | Out-File -Encoding utf8 $recordPath
Write-Host "Built $out"
Write-Host "SHA-256 $hash"
Write-Host "Record $recordPath"
if ($Release -and $DebugSigned) { Write-Host 'A check build, signed with the debug key: it shows what a release build contains and cannot be uploaded.' -ForegroundColor Yellow }
if ($problems) { throw "The APK doesn't meet the upload rules: $($problems -join '; ')." }
