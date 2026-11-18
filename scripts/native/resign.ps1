# Signs a release APK that has already been built, with your own key, without starting Unity.
#
# Why this exists: building an APK needs Unity's Android build program, whose helper libraries are not signed, and
# Windows Smart App Control can block them (it did from 16 November 2026: "An Application Control policy has blocked
# this file", NiceIO.dll). The APK itself is not affected: Gradle leaves the one it built, signed with Android's debug
# key, among Unity's intermediate files, and signing is done by Android's apksigner, which runs on Java. This
# script checks that APK against Meta's rules and against what the build for judges has to be (check_apk.ps1: not
# debuggable, API level 34, Quest 3 and 3S, 64-bit, the sample-only configuration, nothing of the build PC inside),
# signs a copy with your keystore, and checks the copy again.
#
# Run scripts/native/release.ps1 -Resign instead of this: it asks you for the keystore password at a hidden prompt and
# sets the environment variables below for this one run. They are:
#   SAMESKY_KEYSTORE, SAMESKY_KEYSTORE_PASS, SAMESKY_KEY_ALIAS, SAMESKY_KEY_PASS
#
#   -Apk            the APK to sign; by default the release APK in Unity's Gradle output (native/quest/Library/...), else
#                   native/quest/Builds/Android/SameSky-release-sampleonly-prebuilt-debugkey.apk
#   -Out            where the signed copy goes; by default native/quest/Builds/Android/SameSky-release-sampleonly-<stamp>.apk
#   -AllowDebugKey  accept a signature made with Android's debug key (to try the script out; such an APK can't be uploaded)
[CmdletBinding()]
param([string]$Apk = '', [string]$Out = '', [switch]$AllowDebugKey)

. "$PSScriptRoot/check_apk.ps1"

$missing = @('SAMESKY_KEYSTORE', 'SAMESKY_KEYSTORE_PASS', 'SAMESKY_KEY_ALIAS', 'SAMESKY_KEY_PASS') | Where-Object { -not [Environment]::GetEnvironmentVariable($_) }
if ($missing) { throw "Signing needs your keystore, and $($missing -join ', ') isn't set. Run scripts/native/release.ps1 -Resign: it asks for the password and does the rest." }
if (-not (Test-Path $env:SAMESKY_KEYSTORE)) { throw 'The keystore file named in SAMESKY_KEYSTORE was not found.' }
if (-not $Apk) {
  # the one Gradle left among Unity's intermediate files, else the copy kept in Builds (git-ignored) so that a later
  # build attempt can't take it away
  foreach ($candidate in @((Join-Path $Project 'Library/Bee/Android/Prj/IL2CPP/Gradle/launcher/build/outputs/apk/release/launcher-release.apk'),
                           (Join-Path $Project 'Builds/Android/SameSky-release-sampleonly-prebuilt-debugkey.apk'))) {
    if (Test-Path $candidate) { $Apk = $candidate; break }
  }
}
if (-not $Apk -or -not (Test-Path $Apk)) { throw "No release APK to sign. Build one first (scripts/native/release.ps1 -SampleOnly), or give its path with -Apk." }
if (-not $Out) { $Out = Join-Path $Project ("Builds/Android/SameSky-release-sampleonly-{0}.apk" -f (Get-Date -Format 'yyyyMMdd-HHmm')) }
New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null

Write-Step "Checking the APK before it is signed ($(Split-Path $Apk -Leaf), $([math]::Round((Get-Item $Apk).Length / 1MB)) MB)"
$before = Test-QuestApk -Apk $Apk -SampleOnly
$f = $before.facts
Write-Host "    $($f.package) version code $($f.versionCode), min API $($f.minSdk), target API $($f.targetSdk), $($f.architectures), devices $($f.supportedDevices), debuggable $($f.debuggable)"
Write-Host "    searched all $($f.filesSearched) files: sample-only configuration $($f.sampleOnlyConfig); nothing of the build PC found unless listed below"
# the signature isn't one of the things that can be wrong yet: it is made next
$blocking = @($before.problems | Where-Object { $_ -notmatch '^the signature does not verify' })
if ($blocking) { throw "This APK can't go in the release channel: $($blocking -join '; ')." }

Write-Step 'Signing with your keystore'
$tools = Get-AndroidTools
$javaHome = $env:JAVA_HOME
$was = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
try {
  $env:JAVA_HOME = $tools.jdk
  # passwords go in through environment variables of this process, never on a command line
  $result = & $tools.apksigner sign --ks $env:SAMESKY_KEYSTORE --ks-key-alias $env:SAMESKY_KEY_ALIAS --ks-pass 'env:SAMESKY_KEYSTORE_PASS' --key-pass 'env:SAMESKY_KEY_PASS' `
    --v1-signing-enabled false --v2-signing-enabled true --v3-signing-enabled true --v4-signing-enabled false --out $Out $Apk 2>&1
  $code = $LASTEXITCODE
} finally { $env:JAVA_HOME = $javaHome; $ErrorActionPreference = $was }
if ($code -ne 0 -or -not (Test-Path $Out)) {
  Remove-Item $Out -Force -ErrorAction SilentlyContinue
  throw "apksigner could not sign the APK (exit code $code): $((@($result) -join ' ') -replace [regex]::Escape($env:SAMESKY_KEYSTORE_PASS), '***')"
}
Remove-Item "$Out.idsig" -Force -ErrorAction SilentlyContinue

Write-Step 'Checking the signed APK'
$after = Test-QuestApk -Apk $Out -SampleOnly -RequireRealKey:(-not $AllowDebugKey)
$g = $after.facts
Write-Host "    signed by: $($g.signer)"
Write-Host "    signature verifies: $($g.signatureVerifies); $($g.filesSearched) files searched"
if ($after.problems) { Remove-Item $Out -Force -ErrorAction SilentlyContinue; throw "The signed APK doesn't meet the rules: $($after.problems -join '; ')." }

$record = [ordered]@{
  output = $Out; sizeBytes = $g.sizeBytes; sha256 = $g.sha256; resignedFrom = (Split-Path $Apk -Leaf); resignedFromSha256 = $f.sha256
  commit = (& git -C $Repo rev-parse --short HEAD).Trim(); signedAt = (Get-Date).ToString('o'); apk = $g
}
$record | ConvertTo-Json -Depth 5 | Out-File -Encoding utf8 ([IO.Path]::ChangeExtension($Out, '.build.json'))
Write-Host "Signed $Out"
Write-Host "SHA-256 $($g.sha256)"
if ($AllowDebugKey) { Write-Host 'Signed with Android''s debug key: only for trying the script out. It cannot be uploaded.' -ForegroundColor Yellow }
