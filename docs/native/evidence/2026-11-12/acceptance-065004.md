# Native acceptance run acceptance-065004

12 November 2026 06:50, commit `c2ff6c6` (with uncommitted changes). Unity Play mode (editor) in Meta XR Simulator, profile `acceptance`, API http://127.0.0.1:8000.

Evidence kind: **simulator-tested**. Meta XR Simulator with a simulated Meta Quest 3 and simulated hands. It is not a headset result, and says nothing about frame rate, comfort, spatial audio or real hand tracking on a Quest.

| # | Check | Result | Driven by | What the probe saw |
|---|---|---|---|---|
| 1 | XR session active: simulated Meta Quest 3 with hand input | **pass** | script (metavr) | XR active True, runtime 'Meta XR Simulator 207.1.0', hands L True R True, input hands; metavr device set exit 0, input sync exit 0/0 |
| 2 | Dock button pressed by a simulated hand | **pass** | script | pressed dock:day with input 'hands' (script); then screen day |
| 3 | Cup grabbed by a simulated hand | **pass** | script | grab of object:cup with input 'hands' (script); picked-up True; released True; the cup moved 0.097 m with the hand |
| 4 | Voice note heard within the first minute of a fresh start (sample day) | **pass** | script | voice note playing 25.9 s after a fresh start (limit 60 s); captions shown True; finished True |
| 5 | Reply on the table survives an app restart and an API restart | **pass** | script | reply d4b88319-640b-4df9-a502-605471033bf7 placed (new, typed 'd'); server has it True (placed True); back on the table after app restart True; API went down True and up True; server still has it True; back after API restart True |
| 6 | Recenter, pause and exit | **pass** | script | recenter: pressed True, event True, yaw 35.5 -> 0 deg; pause and resume True; exit: event True, app ended True |
| 7 | Hand tracking lost mid-press and found again: no ghost press | **pass** | script | right hand lost True and back True; presses during and after: 0 (count 0 -> 0); cancels: 0 |
| 8 | MP4 of the simulator for every app session | **pass** | script (FFmpeg window capture) | 5 of 5 recordings saved |

Execution order: Check1, FirstMinute, Check2, RenderTour, Check5, Check6, Check7. The first minute is timed on a fresh profile, so checks 3 and 4 run before check 2.

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
- probe state (check1-xr): xrDeviceActive=True, xrDevice=OpenXR Display, openxrRuntime=Meta XR Simulator 207.1.0, hands={"left":true,"right":true}, activeInput=hands, fps=62.4, screen=launch, head=[0.0,1.18,0.0], handsAt={"left":[-0.3,0.88,0.5],"right":[0.3,0.88,0.5]} [acceptance-065004/state-check1-xr.json]

### 2. Dock button pressed by a simulated hand: pass

- probe event (dock press): `press dock:day`, input hands, t 58.998 s, app session 2
- probe event (what the press did): `screen day`, input hands, t 59.0 s, app session 2

### 3. Cup grabbed by a simulated hand: pass

- probe event (grab): `grab object:cup`, input hands, t 36.369 s, app session 2
- probe event (cup picked up): `cup picked-up`, input hands, t 36.37 s, app session 2
- probe state (check3-held): cupHeld=True, grabs=1, lastGrab=object:cup, activeInput=hands, handsAt={"left":[-0.417,0.834,0.366],"right":[0.183,0.834,0.366]} [acceptance-065004/state-check3-held.json]
- probe event (released): `release object:cup`, input hands, t 48.272 s, app session 2
- note: Taken with a palm grab.

### 4. Voice note heard within the first minute of a fresh start (sample day): pass

- probe event (app start (fresh profile)): `start 0.9.0`, input None, t 10.451 s, app session 2
- probe event (launch:sample pressed): `press launch:sample`, input hands, t 27.157 s, app session 2
- probe event (sample room open): `screen room`, input hands, t 27.231 s, app session 2
- probe event (voice note playing): `voice play`, input hands, t 36.377 s, app session 2
- probe event (what started the voice note): `grab object:cup`, input hands, t 36.369 s, app session 2
- probe state (check4-captions): voicePlaying=True, audio=cup:m-maya-voice, captionsOn=True, captions=Saved you a seat., presence=recorded, paused=False [acceptance-065004/state-check4-captions.json]
- probe event (voice note finished): `voice finished`, input hands, t 45.854 s, app session 2
- probe-timing (start -> voice play (probe clock)): {"secondsFromStartToVoice":25.9,"limit":60}

### 5. Reply on the table survives an app restart and an API restart: pass

- probe event (launch:continue (first visit)): `press launch:continue`, input hands, t 23.813 s, app session 3
- probe event (live mode (first visit)): `mode live`, input hands, t 23.885 s, app session 3
- probe event (room open (first visit)): `screen room`, input hands, t 23.887 s, app session 3
- probe event (notes on the table before replying): `reply restored:3b49b61f-77d1-4a8e-96ef-9127f70c4eda,39a8de9c-bb02-488f-a09c-7933120e9283,2b77d9c8-0946-4728-86ed-100037f9cf4c,b45a4769-e2c5-46e0-a1a8-848a3d868724,5cf7d8f1-42f3-4b57-bec0-be627acf4396`, input hands, t 24.022 s, app session 3
- probe event (cup:reply pressed): `press cup:reply`, input hands, t 35.409 s, app session 3
- probe event (a character typed): `press key:d`, input hands, t 47.539 s, app session 3
- probe event (Done pressed): `press key:done`, input hands, t 63.079 s, app session 3
- probe event (reply placed on the table): `reply placed:d4b88319-640b-4df9-a502-605471033bf7`, input hands, t 63.239 s, app session 3
- reply (the reply placed in this run): {"replyId":"d4b88319-640b-4df9-a502-605471033bf7","typed":"d","newThisRun":true,"noraRepliesBefore":5}
- api (server after placing): {"replyId":"d4b88319-640b-4df9-a502-605471033bf7","replyStored":true,"byNora":true,"placementStored":true,"placement":{"x":-0.42,"y":0.25,"z":0.35,"yaw":0.0}}
- probe event (launch:continue (after an app restart)): `press launch:continue`, input hands, t 23.149 s, app session 4
- probe event (live mode (after an app restart)): `mode live`, input hands, t 23.22 s, app session 4
- probe event (room open (after an app restart)): `screen room`, input hands, t 23.222 s, app session 4
- probe event (notes restored after an app restart): `reply restored:3b49b61f-77d1-4a8e-96ef-9127f70c4eda,39a8de9c-bb02-488f-a09c-7933120e9283,2b77d9c8-0946-4728-86ed-100037f9cf4c,b45a4769-e2c5-46e0-a1a8-848a3d868724,5cf7d8f1-42f3-4b57-bec0-be627acf4396,d4b88319-640b-4df9-a502-605471033bf7`, input hands, t 23.354 s, app session 4
- service (API and worker restarted (stop.ps1, start.ps1)): {"apiDownAfterStop":true,"apiUpAfterStart":true,"apiPidBefore":20900,"apiPidAfter":49020,"workerPidBefore":49812,"workerPidAfter":54412}
- api (server after its restart): {"replyId":"d4b88319-640b-4df9-a502-605471033bf7","replyStored":true,"byNora":true,"placementStored":true,"placement":{"x":-0.42,"y":0.25,"z":0.35,"yaw":0.0}}
- probe event (launch:continue (after an API restart)): `press launch:continue`, input hands, t 23.197 s, app session 5
- probe event (live mode (after an API restart)): `mode live`, input hands, t 23.275 s, app session 5
- probe event (room open (after an API restart)): `screen room`, input hands, t 23.276 s, app session 5
- probe event (notes restored after an API restart): `reply restored:3b49b61f-77d1-4a8e-96ef-9127f70c4eda,39a8de9c-bb02-488f-a09c-7933120e9283,2b77d9c8-0946-4728-86ed-100037f9cf4c,b45a4769-e2c5-46e0-a1a8-848a3d868724,5cf7d8f1-42f3-4b57-bec0-be627acf4396,d4b88319-640b-4df9-a502-605471033bf7`, input hands, t 23.437 s, app session 5
- note: Nora is signed in by the session file the script wrote (typing the email and password on the spatial keyboard is not automated).

### 6. Recenter, pause and exit: pass

- probe state (check6-turned): head=[-0.15,1.23,-0.325], headForward=[0.581,0.0,0.814] [acceptance-065004/state-check6-turned.json]
- probe event (dock:recenter pressed): `press dock:recenter`, input hands, t 43.697 s, app session 5
- probe event (recenter): `recenter 1.18`, input hands, t 43.699 s, app session 5
- probe state (check6-recentered): head=[-0.258,1.28,-0.746], headForward=[0.0,0.0,1.0] [acceptance-065004/state-check6-recentered.json]
- probe event (dock:pause pressed): `press dock:pause`, input hands, t 68.793 s, app session 5
- probe event (pause screen): `screen pause`, input hands, t 68.801 s, app session 5
- probe state (check6-paused): paused=True, screen=pause, audio=, voicePlaying=False [acceptance-065004/state-check6-paused.json]
- probe event (pause:resume pressed): `press pause:resume`, input hands, t 94.862 s, app session 5
- probe event (resumed): `pause resume`, input hands, t 94.863 s, app session 5
- probe event (back in the room): `screen room`, input hands, t 94.864 s, app session 5
- probe state (check6-resumed): paused=False, screen=room [acceptance-065004/state-check6-resumed.json]
- probe event (leave confirmation): `screen exit`, input hands, t 153.814 s, app session 5
- probe event (exit): `exit quit`, input hands, t 185.601 s, app session 5
- probe-stopped (the app ended (its probe stopped updating)): {"probeStoppedUpdating":true}

### 7. Hand tracking lost mid-press and found again: no ghost press: pass

- probe state (check7-before): hands={"left":true,"right":true}, activeInput=hands, presses=0, lastPress=, handsAt={"left":[-0.675,0.884,0.246],"right":[-0.075,0.884,0.246]} [acceptance-065004/state-check7-before.json]
- command (right-hand tracking off): `metavr.cmd xrsim input source --hand right --source none`, exit 0: HAND   SOURCE -----  ------ right  none
- probe state (check7-lost): hands={"left":false,"right":false}, activeInput=None, presses=0, lastPress= [acceptance-065004/state-check7-lost.json]
- command (hand tracking back): `metavr.cmd xrsim input sync --source hand`, exit 0: HANDS        SOURCE -----------  ------ left, right  hand
- probe state (check7-after): hands={"left":true,"right":true}, activeInput=hands, presses=0, lastPress= [acceptance-065004/state-check7-after.json]
- probe-count (presses counted by the probe): {"pressesBefore":0,"pressesAfter":0,"pressEvents":0,"cancelEvents":0}
- note: No cancel event: the fingertip may not have been inside the button when tracking dropped, so this shows no ghost press on loss and recovery, but not a cancelled press.

### 8. MP4 of the simulator for every app session: pass

- recording (session2-sample): {"label":"session2-sample","file":"docs/native/evidence/2026-11-12/acceptance-065004/session2-sample.mp4","method":"window","bytes":2329130,"durationSec":93.9,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session2-sample.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session2-sample.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-12T06:51:05.5756203+04:00","stoppedAt":"2026-11-12T06:52:40.9050033+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":15.99},"probeAtStop":{"xrDeviceActive":true,"screen":"room","mode":"sample","activeInput":"hands","time":109.83}}
- probe state (app state when session2-sample started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=15.99
- recording (session3-live): {"label":"session3-live","file":"docs/native/evidence/2026-11-12/acceptance-065004/session3-live.mp4","method":"window","bytes":2928247,"durationSec":54.2,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session3-live.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session3-live.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-12T06:53:02.5155320+04:00","stoppedAt":"2026-11-12T06:53:57.5688732+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":15.56},"probeAtStop":{"xrDeviceActive":true,"screen":"room","mode":"live","activeInput":"hands","time":69.79}}
- probe state (app state when session3-live started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=15.56
- recording (session4-live-restarted): {"label":"session4-live-restarted","file":"docs/native/evidence/2026-11-12/acceptance-065004/session4-live-restarted.mp4","method":"window","bytes":554026,"durationSec":10.9,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session4-live-restarted.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session4-live-restarted.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-12T06:54:20.2730434+04:00","stoppedAt":"2026-11-12T06:54:32.1120603+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":15.96},"probeAtStop":{"xrDeviceActive":true,"screen":"room","mode":"live","activeInput":"hands","time":26.64}}
- probe state (app state when session4-live-restarted started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=15.96
- recording (session5-live-api-restarted): {"label":"session5-live-api-restarted","file":"docs/native/evidence/2026-11-12/acceptance-065004/session5-live-api-restarted.mp4","method":"window","bytes":9066185,"durationSec":174.9,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session5-live-api-restarted.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session5-live-api-restarted.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-12T06:55:08.9635735+04:00","stoppedAt":"2026-11-12T06:58:04.7625029+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":15.12},"probeAtStop":{"xrDeviceActive":true,"screen":"exit","mode":"live","activeInput":"hands","time":185.58}}
- probe state (app state when session5-live-api-restarted started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=15.12
- recording (session6-tracking): {"label":"session6-tracking","file":"docs/native/evidence/2026-11-12/acceptance-065004/session6-tracking.mp4","method":"window","bytes":811967,"durationSec":23.7,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session6-tracking.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session6-tracking.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-12T06:58:20.7914595+04:00","stoppedAt":"2026-11-12T06:58:45.3729793+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":14.86},"probeAtStop":{"xrDeviceActive":true,"screen":"launch","mode":"none","activeInput":"hands","time":38.68}}
- probe state (app state when session6-tracking started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=14.86
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

- `acceptance-065004/seed.json`: seeded ids (development data), 560 bytes, sha256 `0efe88d686dd008d...`
- `acceptance-065004/events-session1.jsonl`: probe events, app session 1, 299 bytes, sha256 `a2686904788a85c1...`
- `acceptance-065004/state-check1-xr.json`: probe state: check1-xr, 3049 bytes, sha256 `8dd23ee5e3381f4d...`
- `acceptance-065004/sim-check1-session.png`: simulator view: check1-session, 968311 bytes, sha256 `488065071a9f84f6...`
- `acceptance-065004/seat-launch.png`: seated view (fixed camera): the launch screen in the room (reference 10), 1104577 bytes, sha256 `dc98bdc15386467d...`
- `acceptance-065004/seat-room-cue.png`: seated view (fixed camera): the sample room with the pick-up cue (references 02 and 10), 1377351 bytes, sha256 `1986163630b61aed...`
- `acceptance-065004/state-check3-held.json`: probe state: check3-held, 5365 bytes, sha256 `d77e269b07e7ba69...`
- `acceptance-065004/sim-check3-cup-held.png`: simulator view: check3-cup-held, 1006763 bytes, sha256 `dcb3becd75d3ce86...`
- `acceptance-065004/state-check4-captions.json`: probe state: check4-captions, 5364 bytes, sha256 `6c25364de72f61e0...`
- `acceptance-065004/sim-check4-captions.png`: simulator view: check4-captions, 1008024 bytes, sha256 `dcf9acc1d29052cd...`
- `acceptance-065004/seat-message.png`: seated view (fixed camera): the cup lifted, its message card playing with captions (reference 03), 1353720 bytes, sha256 `59de838f057454ca...`
- `acceptance-065004/seat-room-partner.png`: seated view (fixed camera): after the message: the sample's partner as labelled simulated presence (reference 02), 1351258 bytes, sha256 `eb2b0057af93a848...`
- `acceptance-065004/sim-check2-dock.png`: simulator view: check2-dock, 971504 bytes, sha256 `0cb02b4a64319f74...`
- `acceptance-065004/seat-tour-day.png`: seated view (fixed camera): Our day (reference 05), 1340160 bytes, sha256 `bcb2ab1d78db7e0f...`
- `acceptance-065004/seat-tour-share.png`: seated view (fixed camera): Share: who receives it, and the AI permission (reference 06), 1207428 bytes, sha256 `f06a1e1e1cabd9ac...`
- `acceptance-065004/seat-tour-together.png`: seated view (fixed camera): Together: places, times and the next overlap (reference 08), 1231037 bytes, sha256 `fd06ddb818284715...`
- `acceptance-065004/seat-tour-keepsakes.png`: seated view (fixed camera): Keepsakes (reference 09), 1188917 bytes, sha256 `b893ef15d5682b55...`
- `acceptance-065004/seat-tour-shelf.png`: seated view (fixed camera): the keepsake shelf in the alcove (reference 09), 1012396 bytes, sha256 `7b703f4d1c966f6a...`
- `acceptance-065004/seat-tour-avatar.png`: seated view (fixed camera): the avatar preview, front, three-quarter and side (references 01 and 13), 1234552 bytes, sha256 `ed7ffdf91bb428d4...`
- `acceptance-065004/seat-tour-settings.png`: seated view (fixed camera): Settings: captions, text size, sound, recenter (reference 12), 1178488 bytes, sha256 `af60b250c713a17b...`
- `acceptance-065004/seat-tour-pause.png`: seated view (fixed camera): Paused (reference 12), 1225007 bytes, sha256 `15c24bd9bcf7da31...`
- `acceptance-065004/seat-tour-story.png`: seated view (fixed camera): the story on the screen beside the partner (references 04 and 05), 1278593 bytes, sha256 `bfca3e94242c84f7...`
- `acceptance-065004/seat-tour-rooms.png`: seated view (fixed camera): the room selector (reference 07), 1369167 bytes, sha256 `1e64fd1fef125401...`
- `acceptance-065004/seat-tour-circle.png`: seated view (fixed camera): the family circle room (reference 07), 1295592 bytes, sha256 `6dc86b3782220da5...`
- `acceptance-065004/seat-tour-direct.png`: seated view (fixed camera): back in the direct room (reference 02), 1428094 bytes, sha256 `8a4a4ae89c223db1...`
- `acceptance-065004/seat-tour-partner.png`: seated view (fixed camera): the partner's face, close (natural proportions, eyes, expression: reference 01), 1068918 bytes, sha256 `c8bc0c866b94ea8d...`
- `acceptance-065004/seat-tour-left.png`: seated view (fixed camera): the room to the left: window light and shadows on the wall and floor, 1330573 bytes, sha256 `9f616cbf4b0b6182...`
- `acceptance-065004/seat-tour-right.png`: seated view (fixed camera): the room to the right: the lamp and side table, 1169736 bytes, sha256 `8f2f6ddda9e44f34...`
- `acceptance-065004/seat-tour-reply.png`: seated view (fixed camera): replying on the keyboard at the table edge (reference 03), 1323936 bytes, sha256 `5cd0cb65f735bdbd...`
- `acceptance-065004/session2-sample.mp4`: simulator window recording (FFmpeg): session2-sample, 2329130 bytes, sha256 `a62b6934c92fdd9d...`
- `acceptance-065004/events-session2.jsonl`: probe events, app session 2, 5405 bytes, sha256 `5cc1fecd51b46e2b...`
- `acceptance-065004/sim-check5-placed.png`: simulator view: check5-placed, 916724 bytes, sha256 `f3eb948cba7fc599...`
- `acceptance-065004/seat-live-room.png`: seated view (fixed camera): the live room: the reply standing on the table as a folded card (reference 03), 1304583 bytes, sha256 `df07263157846f8b...`
- `acceptance-065004/session3-live.mp4`: simulator window recording (FFmpeg): session3-live, 2928247 bytes, sha256 `714bca2289906d6d...`
- `acceptance-065004/events-session3.jsonl`: probe events, app session 3, 1397 bytes, sha256 `5a42b554176e3ac7...`
- `acceptance-065004/sim-check5-after-app-restart.png`: simulator view: check5-after-app-restart, 1002511 bytes, sha256 `4a0b5a2033d8490e...`
- `acceptance-065004/session4-live-restarted.mp4`: simulator window recording (FFmpeg): session4-live-restarted, 554026 bytes, sha256 `7bfd5bacc8cdd306...`
- `acceptance-065004/events-session4.jsonl`: probe events, app session 4, 909 bytes, sha256 `81d149010a04b315...`
- `acceptance-065004/sim-check5-after-api-restart.png`: simulator view: check5-after-api-restart, 1002951 bytes, sha256 `5e2ea658a3e69681...`
- `acceptance-065004/state-check6-turned.json`: probe state: check6-turned, 4136 bytes, sha256 `aa46e53455de5cde...`
- `acceptance-065004/state-check6-recentered.json`: probe state: check6-recentered, 4135 bytes, sha256 `1bc3db28e3cf97c2...`
- `acceptance-065004/state-check6-paused.json`: probe state: check6-paused, 3607 bytes, sha256 `70c337f6c90558e8...`
- `acceptance-065004/sim-check6-paused.png`: simulator view: check6-paused, 951624 bytes, sha256 `e13b02002ec314a3...`
- `acceptance-065004/state-check6-resumed.json`: probe state: check6-resumed, 4182 bytes, sha256 `44da9e0b0774add2...`
- `acceptance-065004/session5-live-api-restarted.mp4`: simulator window recording (FFmpeg): session5-live-api-restarted, 9066185 bytes, sha256 `a84bce4116fcdf28...`
- `acceptance-065004/events-session5.jsonl`: probe events, app session 5, 2447 bytes, sha256 `f122dfe93868b938...`
- `acceptance-065004/state-check7-before.json`: probe state: check7-before, 3383 bytes, sha256 `67f379aa821bc2be...`
- `acceptance-065004/state-check7-lost.json`: probe state: check7-lost, 3284 bytes, sha256 `4511714da773fd54...`
- `acceptance-065004/state-check7-after.json`: probe state: check7-after, 3380 bytes, sha256 `e3d6ded7d4aef129...`
- `acceptance-065004/sim-check7-after.png`: simulator view: check7-after, 963083 bytes, sha256 `1fc314d2aa42cd8e...`
- `acceptance-065004/session6-tracking.mp4`: simulator window recording (FFmpeg): session6-tracking, 811967 bytes, sha256 `c0de3a11fe03ee16...`
- `acceptance-065004/events-session6.jsonl`: probe events, app session 6, 305 bytes, sha256 `6ceae18d9bd4dd4a...`
