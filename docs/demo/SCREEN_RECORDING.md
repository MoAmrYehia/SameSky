# The screen-recording demo

`demo-build/SameSky_Simulator_Screen_Recording_Demo.mp4` (not in git): 2:14, 1920×1080, 30 fps, H.264 + AAC,
−16 LUFS. The native Quest app as a screen recording of **Meta XR Simulator's own window**, with a column of text
beside it. It sits next to the demo [I Saved You a Seat](README.md), which shows the same app through a capture
camera.

## What is in the picture

The window is exactly what FFmpeg recorded of the simulator (its title bar, its Inputs panel showing *Meta Quest 3*
and *Hand* for both hands, and the headset view it draws). The edit trims, speeds up, and writes text beside it; it
changes nothing inside the window. Five chapters, and a tag on every stretch says what kind of footage it is:

| Tag | Meaning |
| --- | --- |
| REAL TIME | recorded speed |
| TIME-LAPSE ×N | sped up N times: the simulator moves a simulated hand slowly (10 to 30 s a press), so travel between targets is shown fast and every press at real speed |
| TEST HOOK · NOT BY HAND | screens that the acceptance run's render tour opens through the app's development probe, so each can be seen; no hand pressed them |
| LIVE MODE · LOCAL SERVER | the same app signed in to a local server with development accounts, for the two hand interactions the sample-only run doesn't have (typing a reply, choosing furniture). **Not in the build for judges** |

Everything else is the sample-only build: the sample day, no server, no account buttons on the welcome panel.

## Where the footage comes from

Not filmed today. Windows Smart App Control, which stays as it is, began blocking Unity's OpenXR plugin
(`UnityOpenXR.dll`, unsigned) on 16 November, so the app can no longer start in the simulator on this PC. The
recordings are the ones the acceptance runs of 13 November made, already in `docs/native/evidence/2026-11-13`:

- `acceptance-033533`: the sample-only build (`session2-sample.mp4`, `session3-tracking.mp4`), 15 frames a second;
- `acceptance-035823`: live mode against the local server (`session3-live.mp4`, `session5-live-api-restarted.mp4`).

Both runs are of the code that is now on `main`.

## Sound

The recording has none. The voice note and the press click are the app's own sound, taken from the captures of the
first demo (what the app's listener heard: `demo-build/capture/s04-cup.wav`, `d20-day.wav`) and placed where the app
played them; the voice sits 14.6 dB above the music. The soft piano and pad and the quiet room tone are synthesized
by `scripts/demo/trailer_audio.py`, as in the other films. Nothing is narrated.

## Limits

- 15 frames a second and a compressed capture: the headset view is about 870 pixels square on screen.
- Simulated hands in a simulator, not a headset: frame rate, comfort and real hand tracking are not shown.
- The reply is one typed letter, as in the test; the sample day's reply on a headset is the same keyboard.
- The recordings' timing was matched to the app's own event log by the visual changes in them (the recorder starts
  about 0.8 s after the log's clock reading); the voice note lines up with its card to about a tenth of a second.

## Rebuilding it

```powershell
python scripts/demo/screenrec_edit.py            # about two minutes; --keep reuses the pieces already cut
```

Needs Python with `numpy` and `scipy`, FFmpeg (the API's Python environment brings one), and the first demo's
captured sound in `demo-build/capture` (`scripts/demo/film.ps1`; git-ignored).
