# Helpers for running and driving SameSky in Meta XR Simulator (dot-source: . scripts/native/sim.ps1).
# The simulator's own keyboard/mouse hand controls drive the simulated hands, so every interaction still
# arrives through OpenXR hand tracking -> Meta Interaction SDK -> the app. Nothing here touches app state.
. "$PSScriptRoot/common.ps1"

$Metavr = @(
  (Get-Command metavr -ErrorAction SilentlyContinue).Source,
  (Join-Path $env:USERPROFILE 'SameSkyTools/metavr/node_modules/.bin/metavr.cmd')
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
$WindowsBuild = Join-Path $Project 'Builds/Windows/SameSky.exe'
$PlayerData = Join-Path $env:USERPROFILE 'AppData/LocalLow/SameSky/SameSky'
function Get-ProbeDir([string]$Profile = '') { if ($Profile) { Join-Path $PlayerData "profile-$Profile/probe" } else { Join-Path $PlayerData 'probe' } }

function Invoke-Xrsim { param([Parameter(ValueFromRemainingArguments)][string[]]$Args) & $Metavr xrsim @Args }

function Find-SimRuntimeJson { return $XrsimRuntimeJson }

function Start-Sim {
  $exe = Join-Path $XrsimRoot 'MetaXRSimulator.exe'
  & $Metavr xrsim app launch --app-path $exe --wait-timeout 60 | Out-Host
}

function Set-SimQuest3Hands {
  # The device list exists only while an app is connected; the device change applies after an app restart.
  & $Metavr xrsim device set --name 'Meta Quest 3' --wait | Out-Host
  & $Metavr xrsim input sync --source hand --wait | Out-Host
}

function Start-SameSkyInSim([string]$Api = $ApiUrl, [string]$Profile = '') {
  $ProbeDir = Get-ProbeDir $Profile
  $runtime = Find-SimRuntimeJson
  if (-not $runtime) { throw 'Meta XR Simulator runtime manifest not found (is the simulator installed?)' }
  if (Test-Path $ProbeDir) { Remove-Item (Join-Path $ProbeDir '*') -Force -ErrorAction SilentlyContinue }
  # XR_RUNTIME_JSON selects the simulator for this process only; the system-wide OpenXR runtime is untouched.
  $psi = New-Object System.Diagnostics.ProcessStartInfo $WindowsBuild
  $profileArg = if ($Profile) { " -sameskyProfile=$Profile" } else { '' }
  $psi.Arguments = "-sameskyApi=$Api$profileArg -screen-width 1280 -screen-height 720 -logFile `"$DevLogs\samesky-player$Profile.log`""
  $psi.UseShellExecute = $false
  $psi.EnvironmentVariables['XR_RUNTIME_JSON'] = $runtime
  return [System.Diagnostics.Process]::Start($psi)
}

function Get-Probe([string]$Profile = '') { $f = Join-Path (Get-ProbeDir $Profile) 'state.json'; if (Test-Path $f) { Get-Content $f -Raw | ConvertFrom-Json } }

function Get-ProbeEvents([string]$Profile = '') { $f = Join-Path (Get-ProbeDir $Profile) 'events.jsonl'; if (Test-Path $f) { Get-Content $f | Where-Object { $_ } | ForEach-Object { $_ | ConvertFrom-Json } } }

function Wait-ProbeEvent([string]$Kind, [string]$Detail = '', [int]$TimeoutSec = 30, [string]$Profile = '') {
  $deadline = (Get-Date).AddSeconds($TimeoutSec)
  while ((Get-Date) -lt $deadline) {
    $hit = Get-ProbeEvents $Profile | Where-Object { $_.kind -eq $Kind -and ($Detail -eq '' -or $_.detail -like $Detail) } | Select-Object -Last 1
    if ($hit) { return $hit }
    Start-Sleep -Milliseconds 250
  }
  return $null
}

# ---------------------------------------------------------------- keyboard and mouse into the simulator window
Add-Type -ReferencedAssemblies System.Drawing, System.Windows.Forms @'
using System;
using System.Drawing;
using System.Runtime.InteropServices;
public static class SimInput {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] static extern void mouse_event(uint f, int dx, int dy, int d, UIntPtr e);
  [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr e);
  [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint type);
  public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  public static Rectangle Focus(IntPtr h) {
    if (IsIconic(h)) ShowWindow(h, 9); // restore if minimised
    if (GetForegroundWindow() != h) {
      // Windows only lets the process that sent the last input take the foreground: a tap of Alt makes that us.
      // Only when needed: in the simulator an Alt tap is a keystroke too.
      keybd_event(0x12, 0, 0, UIntPtr.Zero); keybd_event(0x12, 0, 2, UIntPtr.Zero);
      SetForegroundWindow(h);
    }
    RECT r; GetWindowRect(h, out r); return new Rectangle(r.L, r.T, r.R - r.L, r.B - r.T);
  }
  [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int ht, uint flags);
  public static void Place(IntPtr h, int x, int y, int w, int ht) { SetWindowPos(h, IntPtr.Zero, x, y, w, ht, 0x0014u); } // no z-order change, no activation
  [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
  // The visible frame, without Windows 11's invisible resize borders (the window rectangle if DWM can't say).
  public static Rectangle Frame(IntPtr h) {
    RECT r;
    if (DwmGetWindowAttribute(h, 9, out r, Marshal.SizeOf(typeof(RECT))) != 0) GetWindowRect(h, out r);
    return new Rectangle(r.L, r.T, r.R - r.L, r.B - r.T);
  }
  [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
  public struct POINT { public int X, Y; }
  public static Point Cursor() { POINT p; GetCursorPos(out p); return new Point(p.X, p.Y); }
  public static void Minimize(IntPtr h) { ShowWindow(h, 6); }
  // Captures a window's own content even when other windows cover it (PW_RENDERFULLCONTENT handles GPU-drawn views).
  public static void ShotWindow(IntPtr h, string path) {
    RECT r; GetWindowRect(h, out r);
    using (var bmp = new Bitmap(r.R - r.L, r.B - r.T))
    using (var g = Graphics.FromImage(bmp)) {
      var hdc = g.GetHdc();
      try { PrintWindow(h, hdc, 2); } finally { g.ReleaseHdc(hdc); }
      bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
  }
  // Arrows, Page Up/Down, Home/End, Insert and Delete need KEYEVENTF_EXTENDEDKEY, or they arrive as numpad keys.
  static uint Ext(byte vk) { return ((vk >= 0x21 && vk <= 0x28) || vk == 0x2D || vk == 0x2E) ? 1u : 0u; }
  public static void Key(byte vk, int holdMs) {
    byte sc = (byte)MapVirtualKey(vk, 0);
    keybd_event(vk, sc, Ext(vk), UIntPtr.Zero); System.Threading.Thread.Sleep(holdMs); keybd_event(vk, sc, Ext(vk) | 2u, UIntPtr.Zero);
  }
  public static void KeyDown(byte vk) { keybd_event(vk, (byte)MapVirtualKey(vk, 0), Ext(vk), UIntPtr.Zero); }
  public static void KeyUp(byte vk) { keybd_event(vk, (byte)MapVirtualKey(vk, 0), Ext(vk) | 2u, UIntPtr.Zero); }
  // Holds several keys at once, each for its own time, so a simulated hand moves along a diagonal, not a staircase.
  public static void Chord(byte[] vks, int[] holdMs) {
    int start = Environment.TickCount;
    bool[] up = new bool[vks.Length];
    int left = vks.Length;
    for (int i = 0; i < vks.Length; i++) KeyDown(vks[i]);
    while (left > 0) {
      int t = Environment.TickCount - start;
      for (int i = 0; i < vks.Length; i++) if (!up[i] && t >= holdMs[i]) { KeyUp(vks[i]); up[i] = true; left--; }
      System.Threading.Thread.Sleep(1);
    }
  }
  public static void MoveTo(int x, int y) { SetCursorPos(x, y); }
  public static void MoveBy(int dx, int dy) { mouse_event(0x0001, dx, dy, 0, UIntPtr.Zero); }
  public static void Wheel(int notches) { mouse_event(0x0800u, 0, 0, notches * 120, UIntPtr.Zero); } // negative scrolls down
  public static void Down(bool right) { mouse_event(right ? 0x0008u : 0x0002u, 0, 0, 0, UIntPtr.Zero); }
  public static void Up(bool right) { mouse_event(right ? 0x0010u : 0x0004u, 0, 0, 0, UIntPtr.Zero); }
  public static void MiddleDown() { mouse_event(0x0020u, 0, 0, 0, UIntPtr.Zero); }
  public static void MiddleUp() { mouse_event(0x0040u, 0, 0, 0, UIntPtr.Zero); }
  public static void XDown(int which) { mouse_event(0x0080u, 0, 0, which, UIntPtr.Zero); } // which: 1 = XBUTTON1 (back), 2 = XBUTTON2
  public static void XUp(int which) { mouse_event(0x0100u, 0, 0, which, UIntPtr.Zero); }
  public static void Shot(Rectangle area, string path) {
    using (var bmp = new Bitmap(area.Width, area.Height))
    using (var g = Graphics.FromImage(bmp)) { g.CopyFromScreen(area.Left, area.Top, 0, 0, area.Size); bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png); }
  }
}
'@

[SimInput]::SetProcessDPIAware() | Out-Null   # window rectangles and screen captures in physical pixels
Add-Type -AssemblyName System.Windows.Forms

function Get-SimWindow {
  Get-Process | Where-Object { $_.MainWindowHandle -ne 0 -and ($_.ProcessName -match 'MetaXRSimulator|XRSimulator' -or $_.MainWindowTitle -match 'Simulator') } | Select-Object -First 1
}

# Brings a window to the front so keys reach it. Windows can refuse SetForegroundWindow to a background process;
# WScript.Shell's AppActivate is the fallback. Returns the window rectangle.
function Focus-Window([System.Diagnostics.Process]$Process) {
  $rect = [SimInput]::Focus($Process.MainWindowHandle)
  for ($i = 0; $i -lt 5 -and [SimInput]::GetForegroundWindow() -ne $Process.MainWindowHandle; $i++) {
    try { [void](New-Object -ComObject WScript.Shell).AppActivate($Process.Id) } catch { }
    Start-Sleep -Milliseconds 100
    $rect = [SimInput]::Focus($Process.MainWindowHandle)
  }
  return $rect
}

function Focus-Sim { $w = Get-SimWindow; if (-not $w) { throw 'simulator window not found' }; return Focus-Window $w }

# Simulator 207's window loses about 20 x 12 pixels at every new app session; this puts it back to one size (up to
# 1600 x 1200 physical pixels, within the screen's work area) so the 3D view, screenshots and recordings stay alike.
function Set-SimWindowSize([int]$Width = 1600, [int]$Height = 1200) {
  $w = Get-SimWindow
  if (-not $w) { return }
  $area = [System.Windows.Forms.Screen]::FromHandle($w.MainWindowHandle).WorkingArea
  [SimInput]::Place($w.MainWindowHandle, $area.Left, $area.Top, [math]::Min($Width, $area.Width), [math]::Min($Height, $area.Height))
  Start-Sleep -Milliseconds 300
}

function Save-SimShot([string]$Path) {
  $w = Get-SimWindow
  if (-not $w) { throw 'simulator window not found' }
  [SimInput]::ShotWindow($w.MainWindowHandle, $Path)   # works even if the Unity editor is in front
  return $Path
}

# An editor closed while in Play mode leaves scene backups; the next start then waits on a modal "Recovering Scene
# Backups" dialog. The scene is regenerated by ProjectSetup and never edited by hand, so the backups are disposable.
function Clear-UnitySceneBackups {
  if (Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 }) { return }
  Remove-Item (Join-Path $Project 'Temp/__Backupscenes') -Recurse -Force -ErrorAction SilentlyContinue
}

# The Unity editor grabs the foreground when Play mode starts; minimising it keeps keys going to the simulator.
function Hide-UnityEditor { Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | ForEach-Object { [SimInput]::Minimize($_.MainWindowHandle) } }

# Meta XR Simulator 207 reads its keys (movement, rotation, hand poses: its Input bindings panel) only while a mouse
# button is held in its 3D view, like an editor viewport's fly mode; without one the keys go to whichever sidebar
# control has focus (Space then opens the About page). The main buttons each do something of their own: the left
# one is the Action input (the right hand pinches), the middle one pans and the right one looks around, and those
# two turn or move the view by however far the cursor is from where the simulator last saw it (which it forgets at
# times, such as a new app session). A side button (X2, "forward") does nothing but let the keys in, once the 3D view
# has the keyboard: a right-button click on it gives it that, and is needed again whenever another window had the
# foreground. Every right-button click lands on the same point, so it rarely turns the view; level the head after
# one anyway (acceptance.ps1 does, at every app start).
function Get-SimViewPoint([System.Drawing.Rectangle]$Rect) {
  # the 3D view fills the window right of the Inputs panel and above the console tray
  return New-Object System.Drawing.Point ($Rect.Left + [int]($Rect.Width * 0.68)), ($Rect.Top + [int]($Rect.Height * 0.55))
}

$script:SimKeyClicks = 0   # right-button clicks made to give the 3D view the keyboard

function Enable-SimKeys {
  $pt = Get-SimViewPoint (Focus-Sim)
  [SimInput]::MoveTo($pt.X, $pt.Y)
  Start-Sleep -Milliseconds 80
  [SimInput]::Down($true)
  Start-Sleep -Milliseconds 100
  [SimInput]::Up($true)
  Start-Sleep -Milliseconds 400   # a turn, if the click caused one, shows in the app before anyone measures
  $script:SimKeyClicks++
}

function Invoke-SimKeys([scriptblock]$Keys) {
  $w = Get-SimWindow
  if (-not $w) { throw 'simulator window not found' }
  if ($script:SimKeyClicks -eq 0 -or [SimInput]::GetForegroundWindow() -ne $w.MainWindowHandle) { Enable-SimKeys }
  $pt = Get-SimViewPoint (Focus-Window $w)
  [SimInput]::MoveTo($pt.X, $pt.Y)
  Start-Sleep -Milliseconds 60
  [SimInput]::XDown(2)
  Start-Sleep -Milliseconds 60
  try { & $Keys } finally { Start-Sleep -Milliseconds 30; [SimInput]::XUp(2) }
}

function Send-SimKey([string]$Key, [int]$HoldMs = 120) {
  $vk = [byte][System.Windows.Forms.Keys]$Key
  Invoke-SimKeys { [SimInput]::Key($vk, $HoldMs) }
}

# Holds several keys together, e.g. @{ W = 120; D = 60 }: a straighter diagonal move than one key after another.
function Send-SimChord([System.Collections.IDictionary]$KeyHolds) {
  if ($KeyHolds.Count -eq 0) { return }
  $vks = [byte[]]@($KeyHolds.Keys | ForEach-Object { [byte][System.Windows.Forms.Keys]$_ })
  $ms = [int[]]@($KeyHolds.Values | ForEach-Object { [int]$_ })
  Invoke-SimKeys { [SimInput]::Chord($vks, $ms) }
}
