# Native acceptance run acceptance-210509

11 November 2026 21:05, commit `c2ff6c6` (with uncommitted changes). Unity Play mode (editor) in Meta XR Simulator, profile `acceptance`, API http://127.0.0.1:8000.

Evidence kind: **simulator-tested**. Meta XR Simulator with a simulated Meta Quest 3 and simulated hands. It is not a headset result, and says nothing about frame rate, comfort, spatial audio or real hand tracking on a Quest.

| # | Check | Result | Driven by | What the probe saw |
|---|---|---|---|---|
| 1 | XR session active: simulated Meta Quest 3 with hand input | **pass** | script (metavr) | XR active True, runtime 'Meta XR Simulator 207.1.0', hands L True R True, input hands; metavr device set exit 0, input sync exit 0/0 |
| 2 | Dock button pressed by a simulated hand | **pass** | script | pressed dock:day with input 'hands' (script); then screen day |
| 3 | Cup grabbed by a simulated hand | **pass** | script | grab of object:cup with input 'hands' (script); picked-up True; released True; the cup moved 0.071 m with the hand |
| 4 | Voice note heard within the first minute of a fresh start (sample day) | **pass** | script | voice note playing 24.8 s after a fresh start (limit 60 s); captions shown True; finished True |
| 5 | Reply on the table survives an app restart and an API restart | **pass** | script | reply 39a8de9c-bb02-488f-a09c-7933120e9283 placed (new, typed 'h'); server has it True (placed True); back on the table after app restart True; API went down True and up True; server still has it True; back after API restart True |
| 6 | Recenter, pause and exit | **pass** | script | recenter: pressed True, event True, yaw 35.6 -> 0 deg; pause and resume True; exit: event True, app ended True |
| 7 | Hand tracking lost mid-press and found again: no ghost press | **pass** | script | right hand lost True and back True; presses during and after: 0 (count 0 -> 0); cancels: 1 |
| 8 | MP4 of the simulator for every app session | **pass** | script (FFmpeg window capture) | 5 of 5 recordings saved |

Execution order: Check1, FirstMinute, Check2, Check5, Check6, Check7. The first minute is timed on a fresh profile, so checks 3 and 4 run before check 2.

## Manual steps still to do

- [ ] Check 1: **Confirm the simulated headset is Meta Quest 3**. The probe cannot see the headset model: check Device in the simulator Inputs panel (screenshot sim-check1-session.png).
- [ ] Check 4: **Hear it**. The probe shows playback state and caption text, not sound: listen in the simulator (or the MP4) for the voice note coming from the cup.
- [ ] Check 6: **System pause**. Headset removed or the Meta button: the probe should log 'pause system' or 'pause focus' and sound should stop. Needs a headset, or the simulator's own control for it.
- [ ] Check 8: **Watch the recordings**. Check each MP4 shows the steps above (the script only checks the files exist and have a length).

## Details

### 1. XR session active: simulated Meta Quest 3 with hand input: pass

- command (simulated device): `metavr.cmd xrsim device set --name "Meta Quest 3" --wait`, exit 0: DEVICE       ------------ Meta Quest 3
- command (hand input): `metavr.cmd xrsim input sync --source hand`, exit 0: HANDS        SOURCE -----------  ------ left, right  hand
- command (MP4 recorder in this runtime): `metavr.cmd xrsim record status`, exit 1: Error: this XR Simulator runtime does not support native MP4 recording: its build doesn't include the recording service, so `record start`, `record stop` and `record status` keep failing against it; record the simulator window with your OS screen recorder instead
- command (hand input after the restart): `metavr.cmd xrsim input sync --source hand`, exit 0: HANDS        SOURCE -----------  ------ left, right  hand
- probe state (check1-xr): xrDeviceActive=True, xrDevice=OpenXR Display, openxrRuntime=Meta XR Simulator 207.1.0, hands={"left":true,"right":true}, activeInput=hands, fps=57.6, screen=launch, head=[0.0,1.18,0.0], handsAt={"left":[-0.3,0.88,0.5],"right":[0.3,0.88,0.5]} [acceptance-210509/state-check1-xr.json]

### 2. Dock button pressed by a simulated hand: pass

- probe event (dock press): `press dock:day`, input hands, t 55.421 s, app session 2
- probe event (what the press did): `screen day`, input hands, t 55.422 s, app session 2

### 3. Cup grabbed by a simulated hand: pass

- probe event (grab): `grab object:cup`, input hands, t 34.744 s, app session 2
- probe event (cup picked up): `cup picked-up`, input hands, t 34.745 s, app session 2
- probe state (check3-held): cupHeld=True, grabs=1, lastGrab=object:cup, activeInput=hands, handsAt={"left":[-0.417,0.834,0.367],"right":[0.183,0.834,0.367]} [acceptance-210509/state-check3-held.json]
- probe event (released): `release object:cup`, input hands, t 44.346 s, app session 2
- note: Taken with a palm grab.

### 4. Voice note heard within the first minute of a fresh start (sample day): pass

- probe event (app start (fresh profile)): `start 0.9.0`, input None, t 10.003 s, app session 2
- probe event (launch:sample pressed): `press launch:sample`, input hands, t 26.332 s, app session 2
- probe event (sample room open): `screen room`, input hands, t 26.407 s, app session 2
- probe event (voice note playing): `voice play`, input hands, t 34.753 s, app session 2
- probe event (what started the voice note): `grab object:cup`, input hands, t 34.744 s, app session 2
- probe state (check4-captions): voicePlaying=True, audio=cup:m-maya-voice, captionsOn=True, captions=The bench by the big oak, same as always., presence=recorded, paused=False [acceptance-210509/state-check4-captions.json]
- probe event (voice note finished): `voice finished`, input hands, t 44.043 s, app session 2
- probe-timing (start -> voice play (probe clock)): {"secondsFromStartToVoice":24.8,"limit":60}

### 5. Reply on the table survives an app restart and an API restart: pass

- probe event (launch:continue (first visit)): `press launch:continue`, input hands, t 24.149 s, app session 3
- probe event (live mode (first visit)): `mode live`, input hands, t 24.22 s, app session 3
- probe event (room open (first visit)): `screen room`, input hands, t 24.222 s, app session 3
- probe event (notes on the table before replying): `reply restored:3b49b61f-77d1-4a8e-96ef-9127f70c4eda`, input hands, t 24.346 s, app session 3
- probe event (cup:reply pressed): `press cup:reply`, input hands, t 35.303 s, app session 3
- probe event (a character typed): `press key:h`, input hands, t 47.749 s, app session 3
- probe event (Done pressed): `press key:done`, input hands, t 62.377 s, app session 3
- probe event (reply placed on the table): `reply placed:39a8de9c-bb02-488f-a09c-7933120e9283`, input hands, t 62.493 s, app session 3
- reply (the reply placed in this run): {"replyId":"39a8de9c-bb02-488f-a09c-7933120e9283","typed":"h","newThisRun":true,"noraRepliesBefore":1}
- api (server after placing): {"replyId":"39a8de9c-bb02-488f-a09c-7933120e9283","replyStored":true,"byNora":true,"placementStored":true,"placement":{"x":0.355000019,"y":0.25,"z":0.35,"yaw":0.0}}
- probe event (launch:continue (after an app restart)): `press launch:continue`, input hands, t 22.781 s, app session 4
- probe event (live mode (after an app restart)): `mode live`, input hands, t 22.845 s, app session 4
- probe event (room open (after an app restart)): `screen room`, input hands, t 22.847 s, app session 4
- probe event (notes restored after an app restart): `reply restored:3b49b61f-77d1-4a8e-96ef-9127f70c4eda,39a8de9c-bb02-488f-a09c-7933120e9283`, input hands, t 22.954 s, app session 4
- service (API and worker restarted (stop.ps1, start.ps1)): {"apiDownAfterStop":true,"apiUpAfterStart":true,"apiPidBefore":29392,"apiPidAfter":38224,"workerPidBefore":31920,"workerPidAfter":20176}
- api (server after its restart): {"replyId":"39a8de9c-bb02-488f-a09c-7933120e9283","replyStored":true,"byNora":true,"placementStored":true,"placement":{"x":0.355000019,"y":0.25,"z":0.35,"yaw":0.0}}
- probe event (launch:continue (after an API restart)): `press launch:continue`, input hands, t 21.971 s, app session 5
- probe event (live mode (after an API restart)): `mode live`, input hands, t 22.042 s, app session 5
- probe event (room open (after an API restart)): `screen room`, input hands, t 22.044 s, app session 5
- probe event (notes restored after an API restart): `reply restored:3b49b61f-77d1-4a8e-96ef-9127f70c4eda,39a8de9c-bb02-488f-a09c-7933120e9283`, input hands, t 22.181 s, app session 5
- note: Nora is signed in by the session file the script wrote (typing the email and password on the spatial keyboard is not automated).

### 6. Recenter, pause and exit: pass

- probe state (check6-turned): head=[-0.162,1.251,-0.325], headForward=[0.582,0.0,0.813] [acceptance-210509/state-check6-turned.json]
- probe event (dock:recenter pressed): `press dock:recenter`, input hands, t 41.493 s, app session 5
- probe event (recenter): `recenter 1.18`, input hands, t 41.494 s, app session 5
- probe state (check6-recentered): head=[-0.279,1.297,-0.754], headForward=[0.0,0.0,1.0] [acceptance-210509/state-check6-recentered.json]
- probe event (dock:pause pressed): `press dock:pause`, input hands, t 67.288 s, app session 5
- probe event (pause screen): `screen pause`, input hands, t 67.29 s, app session 5
- probe state (check6-paused): paused=True, screen=pause, audio=, voicePlaying=False [acceptance-210509/state-check6-paused.json]
- probe event (pause:resume pressed): `press pause:resume`, input hands, t 90.508 s, app session 5
- probe event (resumed): `pause resume`, input hands, t 90.508 s, app session 5
- probe event (back in the room): `screen room`, input hands, t 90.509 s, app session 5
- probe state (check6-resumed): paused=False, screen=room [acceptance-210509/state-check6-resumed.json]
- probe event (leave confirmation): `screen exit`, input hands, t 148.778 s, app session 5
- probe event (exit): `exit quit`, input hands, t 185.425 s, app session 5
- probe-stopped (the app ended (its probe stopped updating)): {"probeStoppedUpdating":true}

### 7. Hand tracking lost mid-press and found again: no ghost press: pass

- probe state (check7-before): hands={"left":true,"right":true}, activeInput=hands, presses=0, lastPress=, handsAt={"left":[-0.446,1.188,0.337],"right":[0.154,1.188,0.337]} [acceptance-210509/state-check7-before.json]
- command (right-hand tracking off): `metavr.cmd xrsim input source --hand right --source none`, exit 0: HAND   SOURCE -----  ------ right  none
- probe state (check7-lost): hands={"left":false,"right":false}, activeInput=None, presses=0, lastPress= [acceptance-210509/state-check7-lost.json]
- command (hand tracking back): `metavr.cmd xrsim input sync --source hand`, exit 0: HANDS        SOURCE -----------  ------ left, right  hand
- probe state (check7-after): hands={"left":true,"right":true}, activeInput=hands, presses=0, lastPress= [acceptance-210509/state-check7-after.json]
- probe event (press cancelled by the tracking loss): `cancel launch:recenter`, input hands, t 29.174 s, app session 6
- probe-count (presses counted by the probe): {"pressesBefore":0,"pressesAfter":0,"pressEvents":0,"cancelEvents":1}

### 8. MP4 of the simulator for every app session: pass

- recording (session2-sample): {"label":"session2-sample","file":"docs/native/evidence/2026-11-11/acceptance-210509/session2-sample.mp4","method":"window","bytes":1872913,"durationSec":43.3,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session2-sample.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session2-sample.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-11T21:06:10.2611919+04:00","stoppedAt":"2026-11-11T21:06:54.6779533+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":15.83},"probeAtStop":{"xrDeviceActive":true,"screen":"day","mode":"sample","activeInput":"hands","time":59.06}}
- probe state (app state when session2-sample started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=15.83
- recording (session3-live): {"label":"session3-live","file":"docs/native/evidence/2026-11-11/acceptance-210509/session3-live.mp4","method":"window","bytes":3212409,"durationSec":52.7,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session3-live.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session3-live.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-11T21:07:16.2484115+04:00","stoppedAt":"2026-11-11T21:08:09.7614686+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":15.67},"probeAtStop":{"xrDeviceActive":true,"screen":"room","mode":"live","activeInput":"hands","time":68.02}}
- probe state (app state when session3-live started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=15.67
- recording (session4-live-restarted): {"label":"session4-live-restarted","file":"docs/native/evidence/2026-11-11/acceptance-210509/session4-live-restarted.mp4","method":"window","bytes":543325,"durationSec":10.9,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session4-live-restarted.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session4-live-restarted.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-11T21:08:31.4553696+04:00","stoppedAt":"2026-11-11T21:08:43.2078439+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":15.04},"probeAtStop":{"xrDeviceActive":true,"screen":"room","mode":"live","activeInput":"hands","time":25.55}}
- probe state (app state when session4-live-restarted started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=15.04
- recording (session5-live-api-restarted): {"label":"session5-live-api-restarted","file":"docs/native/evidence/2026-11-11/acceptance-210509/session5-live-api-restarted.mp4","method":"window","bytes":9827226,"durationSec":175.4,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session5-live-api-restarted.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session5-live-api-restarted.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-11T21:09:16.3456010+04:00","stoppedAt":"2026-11-11T21:12:12.7085897+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":15.26},"probeAtStop":{"xrDeviceActive":true,"screen":"exit","mode":"live","activeInput":"hands","time":185.41}}
- probe state (app state when session5-live-api-restarted started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=15.26
- recording (session6-tracking): {"label":"session6-tracking","file":"docs/native/evidence/2026-11-11/acceptance-210509/session6-tracking.mp4","method":"window","bytes":885987,"durationSec":23.8,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session6-tracking.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session6-tracking.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-11T21:12:30.2959200+04:00","stoppedAt":"2026-11-11T21:12:54.9764528+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":16.17},"probeAtStop":{"xrDeviceActive":true,"screen":"launch","mode":"none","activeInput":"hands","time":39.96}}
- probe state (app state when session6-tracking started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=16.17
- note: The simulator runtime has no MP4 recorder (metavr xrsim record says its build doesn't include the recording service), so FFmpeg recorded the simulator window: the 3D view as the simulator showed it, plus its side panel.

## Setup

- API and worker running (scripts/native/start.ps1): ok
- Development accounts and rooms, and a voice note from Sam to Nora (seed_dev.py --voice-note): ok
- Signed in as Nora (nora.native@example.com, development account) through POST /v1/auth/login; the token is never written to the evidence: ok
- Meta XR Simulator window found: ok
- Simulator movement speed 0.3 (metavr xrsim input speed): ok
- Hand control: calibrated; device 'body'; fingertip offset [-0.054,0.008,0.168] (the app's right poke interactor (probe pokeAt), in the poke pose)

## Notes

- metavr xrsim record: Error: this XR Simulator runtime does not support native MP4 recording: its build doesn't include the recording service, so `record start`, `record stop` and `record status` keep failing against it; record the simulator window with your OS screen recorder instead The recordings are FFmpeg captures of the simulator window instead.

## Files

- `acceptance-210509/seed.json`: seeded ids (development data), 560 bytes, sha256 `0efe88d686dd008d...`
- `acceptance-210509/events-session1.jsonl`: probe events, app session 1, 305 bytes, sha256 `f767700b744531dc...`
- `acceptance-210509/state-check1-xr.json`: probe state: check1-xr, 3044 bytes, sha256 `cba104af0a493fec...`
- `acceptance-210509/sim-check1-session.png`: simulator view: check1-session, 1286545 bytes, sha256 `59845336e60504f0...`
- `acceptance-210509/state-check3-held.json`: probe state: check3-held, 4198 bytes, sha256 `aa2647bd33c2bc4b...`
- `acceptance-210509/sim-check3-cup-held.png`: simulator view: check3-cup-held, 1625912 bytes, sha256 `33ee72ad783d7a0e...`
- `acceptance-210509/state-check4-captions.json`: probe state: check4-captions, 4222 bytes, sha256 `4733bf5280c178b1...`
- `acceptance-210509/sim-check4-captions.png`: simulator view: check4-captions, 1626169 bytes, sha256 `fa4425e9db36ee33...`
- `acceptance-210509/sim-check2-dock.png`: simulator view: check2-dock, 1763259 bytes, sha256 `7b2693ed6c388552...`
- `acceptance-210509/session2-sample.mp4`: simulator window recording (FFmpeg): session2-sample, 1872913 bytes, sha256 `34dd28169edfe127...`
- `acceptance-210509/events-session2.jsonl`: probe events, app session 2, 1587 bytes, sha256 `4ea41f526eb3751c...`
- `acceptance-210509/sim-check5-placed.png`: simulator view: check5-placed, 1829154 bytes, sha256 `ed4ecaa0a57a406f...`
- `acceptance-210509/session3-live.mp4`: simulator window recording (FFmpeg): session3-live, 3212409 bytes, sha256 `c035a1c1b7354c4b...`
- `acceptance-210509/events-session3.jsonl`: probe events, app session 3, 1184 bytes, sha256 `c0b2dbd5b98329cd...`
- `acceptance-210509/sim-check5-after-app-restart.png`: simulator view: check5-after-app-restart, 1712413 bytes, sha256 `bec41661792a38de...`
- `acceptance-210509/session4-live-restarted.mp4`: simulator window recording (FFmpeg): session4-live-restarted, 543325 bytes, sha256 `f343108d44f69d21...`
- `acceptance-210509/events-session4.jsonl`: probe events, app session 4, 761 bytes, sha256 `bde8347f217b9a5d...`
- `acceptance-210509/sim-check5-after-api-restart.png`: simulator view: check5-after-api-restart, 1715179 bytes, sha256 `ef9716f53ad62fec...`
- `acceptance-210509/state-check6-turned.json`: probe state: check6-turned, 3891 bytes, sha256 `560fc61d0c3d09e3...`
- `acceptance-210509/state-check6-recentered.json`: probe state: check6-recentered, 3842 bytes, sha256 `e03feda0d374c166...`
- `acceptance-210509/state-check6-paused.json`: probe state: check6-paused, 5663 bytes, sha256 `9e7c22d52226ddf3...`
- `acceptance-210509/sim-check6-paused.png`: simulator view: check6-paused, 1818897 bytes, sha256 `0e84e2e2ed73da98...`
- `acceptance-210509/state-check6-resumed.json`: probe state: check6-resumed, 3888 bytes, sha256 `6918ee2625b34b0a...`
- `acceptance-210509/session5-live-api-restarted.mp4`: simulator window recording (FFmpeg): session5-live-api-restarted, 9827226 bytes, sha256 `f3e5c6fe36bcfd97...`
- `acceptance-210509/events-session5.jsonl`: probe events, app session 5, 2152 bytes, sha256 `762175c7a183a7fd...`
- `acceptance-210509/state-check7-before.json`: probe state: check7-before, 3349 bytes, sha256 `79f6eed8e288ddaa...`
- `acceptance-210509/state-check7-lost.json`: probe state: check7-lost, 3254 bytes, sha256 `dfbd2b2bef3b119c...`
- `acceptance-210509/state-check7-after.json`: probe state: check7-after, 3351 bytes, sha256 `4637e997fa1d5885...`
- `acceptance-210509/sim-check7-after.png`: simulator view: check7-after, 1602197 bytes, sha256 `8be24ed6e889d76f...`
- `acceptance-210509/session6-tracking.mp4`: simulator window recording (FFmpeg): session6-tracking, 885987 bytes, sha256 `1f5a7bfb3aebfa8d...`
- `acceptance-210509/events-session6.jsonl`: probe events, app session 6, 379 bytes, sha256 `775e6a7bc5936f1a...`
