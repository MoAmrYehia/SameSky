# Reads a Quest APK back and holds it against Meta's manifest rules and against what the build for judges has to be.
# Dot-source it, then call Test-QuestApk:
#
#   . scripts/native/check_apk.ps1
#   $r = Test-QuestApk -Apk <path> [-RequireRealKey] [-SampleOnly]
#
#   $r.facts      what the APK says about itself (aapt2, apksigner)
#   $r.problems   what is wrong with it, in words; empty when nothing is
#
# What is checked: package, API levels (minimum 32, target 34), 64-bit only, Quest 3 and 3S as the supported devices, not
# debuggable, a signature that verifies (with -RequireRealKey: not Android's debug key either); with -SampleOnly, that
# the configuration baked into it is "no server, sample day only"; and that none of its files holds a path under the
# Windows user folder of the PC that made it, nor one of that PC's network addresses (the two things that got into
# the first release builds; see Editor/ReleasePrivacy.cs and BuildScript.HomePathMap). Every file is searched, as plain
# text and as UTF-16.

. "$PSScriptRoot/common.ps1"

function Get-AndroidTools {
  $android = Join-Path (Split-Path $Unity) 'Data/PlaybackEngines/AndroidPlayer'
  $buildTools = Get-ChildItem (Join-Path $android 'SDK/build-tools') -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1
  if (-not $buildTools) { throw "Unity's Android build tools were not found under $android" }
  return [pscustomobject]@{ aapt2 = (Join-Path $buildTools.FullName 'aapt2.exe'); apksigner = (Join-Path $buildTools.FullName 'apksigner.bat'); jdk = (Join-Path $android 'OpenJDK') }
}

function Test-QuestApk([string]$Apk, [switch]$RequireRealKey, [switch]$SampleOnly) {
  if (-not (Test-Path $Apk)) { throw "No APK at $Apk" }
  $tools = Get-AndroidTools
  $problems = @()
  $was = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
  $javaHome = $env:JAVA_HOME
  try {
    $badging = (& $tools.aapt2 dump badging $Apk 2>$null) -join "`n"
    $tree = (& $tools.aapt2 dump xmltree --file AndroidManifest.xml $Apk 2>$null) -join "`n"
    $env:JAVA_HOME = $tools.jdk
    $certs = (& $tools.apksigner verify --print-certs $Apk 2>$null)
    $verifies = ($LASTEXITCODE -eq 0)
    $signer = ($certs | Where-Object { $_ -match 'Signer #1 certificate DN' } | Select-Object -First 1) -replace '^.*certificate DN:\s*', ''
  } finally { $env:JAVA_HOME = $javaHome; $ErrorActionPreference = $was }
  $facts = [ordered]@{
    package          = if ($badging -match "package: name='([^']+)'") { $Matches[1] } else { $null }
    versionCode      = if ($badging -match "versionCode='(\d+)'") { [int]$Matches[1] } else { $null }
    minSdk           = if ($badging -match "minSdkVersion:'(\d+)'") { [int]$Matches[1] } else { $null }
    targetSdk        = if ($badging -match "targetSdkVersion:'(\d+)'") { [int]$Matches[1] } else { $null }
    architectures    = if ($badging -match "native-code: (.+)") { ($Matches[1] -replace "'", '').Trim() } else { $null }
    debuggable       = [bool]($tree -match 'android:debuggable\([^)]*\)=true')
    supportedDevices = if ($tree -match 'Raw: "com\.oculus\.supportedDevices"\)\s*\n\s*A: \S*android:value\([^)]*\)="([^"]*)"') { $Matches[1] } else { $null }
    signatureVerifies = $verifies
    signer           = $signer
    sizeBytes        = (Get-Item $Apk).Length
    sha256           = (Get-FileHash -Algorithm SHA256 $Apk).Hash.ToLowerInvariant()
  }
  if ($facts.package -ne 'com.samesky.quest') { $problems += "the package is '$($facts.package)', not com.samesky.quest" }
  if ($facts.targetSdk -ne 34) { $problems += "the target API level is $($facts.targetSdk); Meta's manifest rules ask for 34" }
  if ($facts.minSdk -ne 32) { $problems += "the minimum API level is $($facts.minSdk), not 32" }
  if ($facts.supportedDevices -ne 'quest3|quest3s') { $problems += "the supported devices are '$($facts.supportedDevices)', not quest3|quest3s" }
  if ($facts.architectures -ne 'arm64-v8a') { $problems += "the native code is '$($facts.architectures)', not arm64-v8a alone" }
  if ($facts.debuggable) { $problems += 'the build is debuggable' }
  if (-not $verifies) { $problems += 'the signature does not verify (or the APK is not signed)' }
  if ($RequireRealKey -and (-not $signer -or $signer -match 'Android Debug')) { $problems += "the APK is not signed with your own key (signer: '$signer')" }

  # every file, for what must not be in it and for the sample-only configuration
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  $needles = [ordered]@{}
  if ($env:USERNAME) {
    $needles["Users/$($env:USERNAME)/"] = 'a path under the Windows user folder of the PC that built it'
    $needles["Users\$($env:USERNAME)\"] = 'a path under the Windows user folder of the PC that built it'
  }
  try {
    Get-NetIPAddress -AddressFamily IPv4 -ErrorAction Stop | Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } | ForEach-Object { $needles[$_.IPAddress] = "a network address of the PC that built it" }
  } catch { }
  $latin1 = [Text.Encoding]::GetEncoding(28591)   # one character per byte, so a binary file can be searched as text
  $found = @{}
  $configured = $false
  $count = 0
  $zip = [IO.Compression.ZipFile]::OpenRead($Apk)
  try {
    $count = $zip.Entries.Count
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
      # the app's configuration, as BuildScript wrote it: no API address, environment "sample-only"
      if ($entry.FullName -like 'assets/bin/Data/*' -and $entry.Length -lt 8MB -and $text.Contains('"environment": "sample-only"') -and $text.Contains('"apiUrl": ""')) { $configured = $true }
      $text = $null
    }
  } finally { $zip.Dispose() }
  foreach ($what in $found.Keys) { $problems += "$what is inside the APK ($(@($found[$what] | Select-Object -First 3) -join ', '))" }
  if ($SampleOnly -and -not $configured) { $problems += "the APK does not carry the sample-only configuration (no server): it isn't the build for judges" }
  $facts.sampleOnlyConfig = $configured
  $facts.filesSearched = $count
  return [pscustomobject]@{ facts = [pscustomobject]$facts; problems = $problems }
}
