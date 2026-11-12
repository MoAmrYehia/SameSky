# Renders spoken lines with the text-to-speech voices that ship with Windows: by default the animated trailer's
# (scripts/demo/trailer_lines.json); with -Lines demo_lines.json -Out demo-build/demo/voice the demo video's narration.
# Writes for each line:
#   demo-build/film/voice/<id>.wav    the voice, 44.1 kHz mono
#   demo-build/film/voice/<id>.json   its length and the synthesizer's mouth shapes over time (for lip-sync)
# Maya and Adam use the desktop (SAPI) voices, which report mouth shapes. The narrator uses a Windows "OneCore" voice
# when there is one (it reports none, and the narrator is never on screen), and otherwise a desktop voice.
# These are synthesized voices: no person's voice is recorded or imitated.

[CmdletBinding()]
param(
  [string]$Only = '',
  [string]$Lines = 'trailer_lines.json',          # the lines to render (a file beside this script)
  [string]$Out = 'demo-build/film/voice'          # where the WAVs go, from the repository root
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$out = Join-Path $repo $Out
New-Item -ItemType Directory -Force -Path $out | Out-Null
$script = Get-Content (Join-Path $PSScriptRoot $Lines) -Raw -Encoding UTF8 | ConvertFrom-Json

Add-Type -AssemblyName System.Speech

function Get-Ssml([string]$text, [string]$rate, [string]$pitch) {
  $safe = [System.Security.SecurityElement]::Escape($text)
  return "<speak version=`"1.0`" xmlns=`"http://www.w3.org/2001/10/synthesis`" xml:lang=`"en-US`"><prosody rate=`"$rate`" pitch=`"$pitch`">$safe</prosody></speak>"
}

function Invoke-Desktop($line, [string]$voice, [string]$wav) {
  $synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
  try {
    $synth.SelectVoice($voice)
    $format = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(44100, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)
    $synth.SetOutputToWaveFile($wav, $format)
    $visemes = New-Object System.Collections.Generic.List[object]
    $handler = [System.EventHandler[System.Speech.Synthesis.VisemeReachedEventArgs]] {
      param($s, $e)
      $visemes.Add([pscustomobject]@{ t = [math]::Round($e.AudioPosition.TotalSeconds, 3); v = [int]$e.Viseme })
    }
    $synth.add_VisemeReached($handler)
    $say = if ($line.say) { $line.say } else { $line.text }
    $synth.SpeakSsml((Get-Ssml $say $line.rate $line.pitch))
    $synth.SetOutputToNull()
    return $visemes
  } finally { $synth.Dispose() }
}

# Windows.Media.SpeechSynthesis from Windows PowerShell: await its operations through AsTask
function Wait-Async($operation, [type]$resultType) {
  Add-Type -AssemblyName System.Runtime.WindowsRuntime
  $asTask = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
    $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
  } | Select-Object -First 1
  $task = $asTask.MakeGenericMethod($resultType).Invoke($null, @($operation))
  $task.Wait(60000) | Out-Null
  return $task.Result
}

function Invoke-OneCore($line, [string]$voice, [string]$wav) {
  [void][Windows.Media.SpeechSynthesis.SpeechSynthesizer, Windows.Media.SpeechSynthesis, ContentType = WindowsRuntime]
  [void][Windows.Storage.Streams.DataReader, Windows.Storage.Streams, ContentType = WindowsRuntime]
  $synth = New-Object Windows.Media.SpeechSynthesis.SpeechSynthesizer
  $match = [Windows.Media.SpeechSynthesis.SpeechSynthesizer]::AllVoices | Where-Object { $_.DisplayName -eq $voice } | Select-Object -First 1
  if (-not $match) { throw "no OneCore voice called $voice" }
  $synth.Voice = $match
  $say = if ($line.say) { $line.say } else { $line.text }
  $stream = Wait-Async ($synth.SynthesizeSsmlToStreamAsync((Get-Ssml $say $line.rate $line.pitch))) ([Windows.Media.SpeechSynthesis.SpeechSynthesisStream])
  $reader = New-Object Windows.Storage.Streams.DataReader($stream.GetInputStreamAt(0))
  [void](Wait-Async ($reader.LoadAsync([uint32]$stream.Size)) ([uint32]))
  $bytes = New-Object byte[] ([int]$stream.Size)
  $reader.ReadBytes($bytes)
  [System.IO.File]::WriteAllBytes($wav, $bytes)
  $reader.Dispose(); $stream.Dispose(); $synth.Dispose()
}

function Get-WavSeconds([string]$wav) {
  $b = [System.IO.File]::ReadAllBytes($wav)
  $rate = [BitConverter]::ToInt32($b, 24)
  $align = [BitConverter]::ToInt16($b, 32)
  # find the data chunk
  $i = 12
  while ($i + 8 -le $b.Length) {
    $id = [System.Text.Encoding]::ASCII.GetString($b, $i, 4)
    $size = [BitConverter]::ToInt32($b, $i + 4)
    if ($id -eq 'data') {
      if ($size -le 0 -or $size -gt $b.Length - $i - 8) { $size = $b.Length - $i - 8 }
      return [math]::Round($size / [double]($rate * $align), 3)
    }
    $i += 8 + $size + ($size % 2)
  }
  return 0
}

$desktop = (New-Object System.Speech.Synthesis.SpeechSynthesizer).GetInstalledVoices() | ForEach-Object { $_.VoiceInfo.Name }
$report = @()
foreach ($line in $script.lines) {
  if ($Only -and $line.id -ne $Only) { continue }
  $wav = Join-Path $out ($line.id + '.wav')
  $visemes = @()
  $used = $line.voice
  if ($desktop -contains $line.voice) { $visemes = Invoke-Desktop $line $line.voice $wav }
  else {
    try { Invoke-OneCore $line $line.voice $wav }
    catch {
      Write-Warning "$($line.id): $($line.voice) is not available ($($_.Exception.Message)); using $($line.fallback)"
      $used = $line.fallback
      $visemes = Invoke-Desktop $line $line.fallback $wav
    }
  }
  $seconds = Get-WavSeconds $wav
  [pscustomobject]@{ id = $line.id; who = $line.who; voice = $used; text = $line.text; duration = $seconds; visemes = @($visemes) } |
    ConvertTo-Json -Depth 5 | Set-Content -Path (Join-Path $out ($line.id + '.json')) -Encoding UTF8
  $report += [pscustomobject]@{ id = $line.id; who = $line.who; voice = $used; seconds = $seconds; visemes = @($visemes).Count }
}
$report | Format-Table -AutoSize
