# Native acceptance run acceptance-065944

12 November 2026 06:59, commit `c2ff6c6` (with uncommitted changes). Unity Play mode (editor) in Meta XR Simulator, profile `acceptance`, API http://127.0.0.1:8000.

Evidence kind: **simulator-tested**. Meta XR Simulator with a simulated Meta Quest 3 and simulated hands. It is not a headset result, and says nothing about frame rate, comfort, spatial audio or real hand tracking on a Quest.

| # | Check | Result | Driven by | What the probe saw |
|---|---|---|---|---|
| 1 | XR session active: simulated Meta Quest 3 with hand input | **pass** | script (metavr) | XR active True, runtime 'Meta XR Simulator 207.1.0', hands L True R True, input hands; metavr device set exit 0, input sync exit 0/0 |
| 2 | Dock button pressed by a simulated hand | **skipped** |  | not selected (-Only) |
| 3 | Cup grabbed by a simulated hand | **skipped** |  | not selected (-Only) |
| 4 | Voice note heard within the first minute of a fresh start (sample day) | **skipped** |  | not selected (-Only) |
| 5 | Reply on the table survives an app restart and an API restart | **skipped** |  | not selected (-Only) |
| 6 | Recenter, pause and exit | **skipped** |  | not selected (-Only) |
| 7 | Hand tracking lost mid-press and found again: no ghost press | **pass** | script | right hand lost True and back True; presses during and after: 0 (count 0 -> 0); cancels: 1 |
| 8 | MP4 of the simulator for every app session | **pass** | script (FFmpeg window capture) | 2 of 2 recordings saved |

Execution order: Check1, Check7. The first minute is timed on a fresh profile, so checks 3 and 4 run before check 2.

## Manual steps still to do

- [ ] Check 1: **Confirm the simulated headset is Meta Quest 3**. The probe cannot see the headset model: check Device in the simulator Inputs panel (screenshot sim-check1-session.png).
- [ ] Check 8: **Watch the recordings**. Check each MP4 shows the steps above (the script only checks the files exist and have a length).

## Details

### 1. XR session active: simulated Meta Quest 3 with hand input: pass

- command (simulated device): `metavr.cmd xrsim device set --name "Meta Quest 3" --wait`, exit 0: DEVICE       ------------ Meta Quest 3
- command (hand input): `metavr.cmd xrsim input sync --source hand`, exit 0: HANDS        SOURCE -----------  ------ left, right  hand
- command (MP4 recorder in this runtime): `metavr.cmd xrsim record status`, exit 1: Error: this XR Simulator runtime does not support native MP4 recording: its build doesn't include the recording service, so `record start`, `record stop` and `record status` keep failing against it; record the simulator window with your OS screen recorder instead
- command (hand input after the restart): `metavr.cmd xrsim input sync --source hand`, exit 0: HANDS        SOURCE -----------  ------ left, right  hand
- probe state (check1-xr): xrDeviceActive=True, xrDevice=OpenXR Display, openxrRuntime=Meta XR Simulator 207.1.0, hands={"left":true,"right":true}, activeInput=hands, fps=62.5, screen=launch, head=[0.0,1.18,0.0], handsAt={"left":[-0.3,0.88,0.5],"right":[0.3,0.88,0.5]} [acceptance-065944/state-check1-xr.json]

### 2. Dock button pressed by a simulated hand: skipped


### 3. Cup grabbed by a simulated hand: skipped


### 4. Voice note heard within the first minute of a fresh start (sample day): skipped


### 5. Reply on the table survives an app restart and an API restart: skipped


### 6. Recenter, pause and exit: skipped


### 7. Hand tracking lost mid-press and found again: no ghost press: pass

- probe state (check7-before): hands={"left":true,"right":true}, activeInput=hands, presses=0, lastPress=, handsAt={"left":[-0.629,0.892,0.266],"right":[-0.029,0.892,0.266]} [acceptance-065944/state-check7-before.json]
- probe-target (was the button held down when tracking was dropped): {"target":"launch:recenter","downBeforeTrackingLoss":true}
- command (right-hand tracking off): `metavr.cmd xrsim input source --hand right --source none`, exit 0: HAND   SOURCE -----  ------ right  none
- probe state (check7-lost): hands={"left":false,"right":false}, activeInput=None, presses=0, lastPress= [acceptance-065944/state-check7-lost.json]
- command (hand tracking back): `metavr.cmd xrsim input sync --source hand`, exit 0: HANDS        SOURCE -----------  ------ left, right  hand
- probe state (check7-after): hands={"left":true,"right":true}, activeInput=hands, presses=0, lastPress= [acceptance-065944/state-check7-after.json]
- probe event (press cancelled by the tracking loss): `cancel launch:recenter`, input hands, t 23.098 s, app session 3
- probe-count (presses counted by the probe): {"pressesBefore":0,"pressesAfter":0,"pressEvents":0,"cancelEvents":1}

### 8. MP4 of the simulator for every app session: pass

- recording (session2-sample): {"label":"session2-sample","file":"docs/native/evidence/2026-11-12/acceptance-065944/session2-sample.mp4","method":"window","bytes":78384,"durationSec":2.6,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session2-sample.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session2-sample.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-12T07:00:46.7059428+04:00","stoppedAt":"2026-11-12T07:00:50.5359687+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":15.51},"probeAtStop":{"xrDeviceActive":true,"screen":"launch","mode":"none","activeInput":"hands","time":18.21}}
- probe state (app state when session2-sample started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=15.51
- recording (session3-tracking): {"label":"session3-tracking","file":"docs/native/evidence/2026-11-12/acceptance-065944/session3-tracking.mp4","method":"window","bytes":553466,"durationSec":20.1,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session3-tracking.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session3-tracking.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-12T07:01:10.7334208+04:00","stoppedAt":"2026-11-12T07:01:31.7517875+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":14.96},"probeAtStop":{"xrDeviceActive":true,"screen":"launch","mode":"none","activeInput":"hands","time":34.86}}
- probe state (app state when session3-tracking started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=14.96
- note: The simulator runtime has no MP4 recorder (metavr xrsim record says its build doesn't include the recording service), so FFmpeg recorded the simulator window: the 3D view as the simulator showed it, plus its side panel.

## Setup

- API and worker running (scripts/native/start.ps1): ok
- Development accounts and rooms, and a voice note from Sam to Nora (seed_dev.py --voice-note): ok
- Signed in as Nora (nora.native@example.com, development account) through POST /v1/auth/login; the token is never written to the evidence: ok
- Meta XR Simulator window found: ok
- Simulator movement speed 0.3 (metavr xrsim input speed): ok
- Hand control: calibrated; device 'body'; fingertip offset [-0.054,0.008,0.167] (the app's right poke interactor (probe pokeAt), in the poke pose)

## Notes

- metavr xrsim record: Error: this XR Simulator runtime does not support native MP4 recording: its build doesn't include the recording service, so `record start`, `record stop` and `record status` keep failing against it; record the simulator window with your OS screen recorder instead The recordings are FFmpeg captures of the simulator window instead.

## Files

- `acceptance-065944/seed.json`: seeded ids (development data), 560 bytes, sha256 `0efe88d686dd008d...`
- `acceptance-065944/events-session1.jsonl`: probe events, app session 1, 302 bytes, sha256 `4c8b4e8ae2ab1310...`
- `acceptance-065944/state-check1-xr.json`: probe state: check1-xr, 3049 bytes, sha256 `f4bf0529654b9e94...`
- `acceptance-065944/sim-check1-session.png`: simulator view: check1-session, 968318 bytes, sha256 `24aefb51bedf2bad...`
- `acceptance-065944/seat-launch.png`: seated view (fixed camera): the launch screen in the room (reference 10), 1104577 bytes, sha256 `dc98bdc15386467d...`
- `acceptance-065944/session2-sample.mp4`: simulator window recording (FFmpeg): session2-sample, 78384 bytes, sha256 `43b57d16e7a69570...`
- `acceptance-065944/events-session2.jsonl`: probe events, app session 2, 365 bytes, sha256 `b3f07eabb3752f3d...`
- `acceptance-065944/state-check7-before.json`: probe state: check7-before, 3083 bytes, sha256 `3df754cdaea1c102...`
- `acceptance-065944/state-check7-lost.json`: probe state: check7-lost, 2985 bytes, sha256 `176fca1197cc3e79...`
- `acceptance-065944/state-check7-after.json`: probe state: check7-after, 3084 bytes, sha256 `582dc0f7ce92913f...`
- `acceptance-065944/sim-check7-after.png`: simulator view: check7-after, 962414 bytes, sha256 `e95f51821bd1fbf1...`
- `acceptance-065944/session3-tracking.mp4`: simulator window recording (FFmpeg): session3-tracking, 553466 bytes, sha256 `b6597f83e3ced4cb...`
- `acceptance-065944/events-session3.jsonl`: probe events, app session 3, 377 bytes, sha256 `64d4451a82eefa53...`
