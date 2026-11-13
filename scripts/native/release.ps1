# Makes the release build of the native app: the APK that goes into the "Competition" release channel.
# Run it yourself, in your own terminal: it asks you for the keystore password, and nothing else ever sees it.
#
#   powershell -ExecutionPolicy Bypass -File scripts/native/release.ps1 -SampleOnly
#   powershell -ExecutionPolicy Bypass -File scripts/native/release.ps1 -ApiUrl https://api.example.com
#
#   -SampleOnly   no server: the build offers the sample day only (what judges get while the API isn't hosted)
#   -ApiUrl       the hosted API (https) the build talks to, instead of -SampleOnly
#   -Keystore     where your signing keystore is, or is to be made. Default: .samesky\samesky-release.keystore in
#                 your user folder, outside the repository. It is never copied anywhere
#   -Alias        the key's name inside the keystore (default: samesky)
#   -VersionCode  Android version code; by default one made from the date and time, so each build's is higher
#
# The first run makes the keystore (2048-bit RSA, valid for about 27 years) with a password you choose. Keep the
# file and the password: every later upload of this app must be signed with the same key, and nobody can recover
# either for you. Later runs ask for the password and sign with the keystore that is there.
#
# The password is typed at a hidden prompt and handed to keytool and to Unity through environment variables of this
# one PowerShell process, which are removed when the script ends. It never appears on a command line. Unity itself
# writes it into the Gradle project it generates for the build (native/quest/Library, on this PC only and never
# committed); build.ps1 takes it out again after the build and says so if it finds it anywhere else.
# Close the Unity Editor first.
[CmdletBinding()]
param(
  [string]$ApiUrl = '',
  [switch]$SampleOnly,
  [string]$Keystore = '',
  [string]$Alias = 'samesky',
  [int]$VersionCode = 0
)
$requestedApi = $ApiUrl
. "$PSScriptRoot/common.ps1"   # sets $ApiUrl to this PC's local address, which a release build never uses
$ApiUrl = $requestedApi
if ($SampleOnly -and $ApiUrl) { throw 'Give -SampleOnly or -ApiUrl, not both.' }
if (-not $SampleOnly -and $ApiUrl -notmatch '^https://') { throw 'Say which build this is: -SampleOnly (no server, the sample day only) or -ApiUrl https://...' }
if (-not $Keystore) { $Keystore = Join-Path $env:USERPROFILE '.samesky/samesky-release.keystore' }
$Keystore = [IO.Path]::GetFullPath($Keystore)
if ($Keystore.StartsWith($Repo, [StringComparison]::OrdinalIgnoreCase)) { throw 'Keep the keystore outside the repository folder, so it can never be committed.' }
$keytool = Join-Path (Split-Path $Unity) 'Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/keytool.exe'
if (-not (Test-Path $keytool)) { throw "keytool was not found in Unity's Android module ($keytool)." }
if (Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 }) { throw 'Close the Unity Editor first: the build needs the project to itself.' }

function Read-Secret([string]$Prompt) {
  $secure = Read-Host -Prompt $Prompt -AsSecureString
  $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
  try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
  finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
}

$names = @('SAMESKY_KEYSTORE', 'SAMESKY_KEYSTORE_PASS', 'SAMESKY_KEY_ALIAS', 'SAMESKY_KEY_PASS')
try {
  if (Test-Path $Keystore) {
    Write-Step 'Signing with your keystore'
    $password = Read-Secret 'Keystore password'
    if (-not $password) { throw 'No password was typed.' }
  }
  else {
    Write-Step 'Making your signing keystore (once)'
    Write-Host '    Choose a password of at least 6 characters. Keep it, and keep the keystore file: every later upload of'
    Write-Host '    this app must be signed with the same key, and neither can be recovered if lost.'
    $password = Read-Secret 'New keystore password'
    if ($password.Length -lt 6) { throw 'The password needs at least 6 characters.' }
    if ($password -cne (Read-Secret 'The same password again')) { throw 'The two passwords were not the same. Nothing was made.' }
    New-Item -ItemType Directory -Force (Split-Path $Keystore) | Out-Null
    $env:SAMESKY_KEYSTORE_PASS = $password
    # keytool reports on stderr, which Windows PowerShell would otherwise take for a failure
    $was = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    try {
      & $keytool -genkeypair -keystore $Keystore -storetype PKCS12 -alias $Alias -keyalg RSA -keysize 2048 -validity 10000 `
        -dname 'CN=SameSky, O=SameSky' -storepass:env SAMESKY_KEYSTORE_PASS -keypass:env SAMESKY_KEYSTORE_PASS 2>&1 | ForEach-Object { Write-Host "    $_" }
    } finally { $ErrorActionPreference = $was }
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $Keystore)) { throw 'keytool could not make the keystore.' }
    Write-Host '    Made. Back this file up somewhere safe (outside the project).' -ForegroundColor Green
  }
  # the password must open this keystore and this key before a ten-minute build is started on it
  $env:SAMESKY_KEYSTORE_PASS = $password
  $was = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
  try { $listed = (& $keytool -list -keystore $Keystore -alias $Alias -storepass:env SAMESKY_KEYSTORE_PASS 2>&1) -join ' ' }
  finally { $ErrorActionPreference = $was }
  if ($LASTEXITCODE -ne 0) {
    if ($listed -match 'password was incorrect|keystore password') { throw 'That password does not open the keystore.' }
    throw "The keystore has no key named '$Alias' (give its name with -Alias), or could not be read."
  }
  $env:SAMESKY_KEYSTORE = $Keystore
  $env:SAMESKY_KEY_ALIAS = $Alias
  $env:SAMESKY_KEY_PASS = $password
  $password = $null

  $buildArgs = @{ Target = 'Android'; Release = $true }
  if ($SampleOnly) { $buildArgs.SampleOnly = $true } else { $buildArgs.ApiUrl = $ApiUrl }
  if ($VersionCode -gt 0) { $buildArgs.VersionCode = $VersionCode }
  & "$PSScriptRoot/build.ps1" @buildArgs
}
finally {
  $password = $null
  foreach ($n in $names) { Remove-Item "Env:$n" -ErrorAction SilentlyContinue }
}

Write-Host ''
Write-Host 'Next, in the Meta Horizon Developer Dashboard (docs/NATIVE_QUICKSTART.md, section 6):' -ForegroundColor Cyan
Write-Host '  1. My Apps: create the app if it is not there yet.'
Write-Host '  2. Distribution > Release Channels > Create Release Channel, named Competition.'
Write-Host '  3. Upload the APK named above to that channel (Meta Quest Developer Hub, or the dashboard''s upload).'
Write-Host '  4. Channel Settings > Channel Invite > Grant access to users by URL, automatic approval, Save Changes.'
Write-Host '     That URL is the link for the Devpost form.'
Write-Host 'Keep the keystore file and its password: an updated build has to be signed with the same key.'
