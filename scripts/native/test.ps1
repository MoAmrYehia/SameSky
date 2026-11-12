# Runs the native app's edit-mode unit tests (native/quest/Assets/SameSky/Tests/EditMode) in Unity batch mode and
# prints each result. They cover logic that needs no headset: data contracts, the bundled sample and its labels,
# configuration rules, recordings, shared-playback timing, the furniture catalogue's rules and the hand skin bundle.
# The Unity Editor must not have native/quest open (one editor per project). Exit code 0 only when every test passed.
. "$PSScriptRoot/common.ps1"
if (Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 -and $_.MainWindowTitle -match '^quest - ' }) {
  throw 'The Unity Editor has native/quest open: close it first (Unity allows one editor per project).'
}
$log = Join-Path $DevLogs 'unity-editmode-tests.log'
$results = Join-Path $DevLogs 'editmode-results.xml'
Remove-Item $log, $results -Force -ErrorAction SilentlyContinue
Write-Step "Edit-mode tests (Unity $UnityVersion, batch mode)"
# -runTests ends Unity by itself; with -quit it would leave before the tests ran.
$unityArgs = @('-batchmode', '-buildTarget', 'Win64', '-projectPath', "`"$Project`"", '-runTests', '-testPlatform', 'EditMode', '-testResults', "`"$results`"", '-logFile', "`"$log`"")
$proc = Start-Process -FilePath $Unity -ArgumentList $unityArgs -PassThru
$proc.WaitForExit()
if (-not (Test-Path $results)) {
  Select-String -Path $log -Pattern 'error CS\d+' -ErrorAction SilentlyContinue | Select-Object -First 10 | ForEach-Object { Write-Host "    $($_.Line)" -ForegroundColor Red }
  throw "No test results were written (exit code $($proc.ExitCode)): the scripts may not compile. Log: $log"
}
[xml]$doc = Get-Content $results -Raw
$run = $doc.'test-run'
foreach ($case in $doc.SelectNodes('//test-case')) {
  $colour = if ($case.result -eq 'Passed') { 'Green' } else { 'Red' }
  Write-Host ('    {0,-8} {1}' -f $case.result, $case.name) -ForegroundColor $colour
  if ($case.result -eq 'Failed') { Write-Host ('             ' + (($case.failure.message.'#cdata-section' -replace '\s+', ' ').Trim())) -ForegroundColor Red }
}
Write-Host "    $($run.passed) of $($run.total) passed, $($run.failed) failed, $($run.skipped) skipped"
if ($proc.ExitCode -ne 0 -or [int]$run.failed -gt 0) { exit 1 }
