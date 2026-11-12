# Native acceptance run acceptance-022557

13 November 2026 02:25, commit `6e510b8` (with uncommitted changes). Unity Play mode (editor) in Meta XR Simulator, profile `acceptance`, API http://127.0.0.1:8000.

Evidence kind: **simulator-tested**. Meta XR Simulator with a simulated Meta Quest 3 and simulated hands. It is not a headset result, and says nothing about frame rate, comfort, spatial audio or real hand tracking on a Quest.

| # | Check | Result | Driven by | What the probe saw |
|---|---|---|---|---|
| 1 | XR session active: simulated Meta Quest 3 with hand input | **pass** | script (metavr) | XR active True, runtime 'Meta XR Simulator 207.1.0', hands L True R True, input hands; metavr device set exit 0, input sync exit 0/0 |
| 2 | Dock button pressed by a simulated hand | **pass** | script | pressed dock:day with input 'hands' (script); then screen day |
| 3 | Cup grabbed by a simulated hand | **pass** | script | grab of object:cup with input 'hands' (script); picked-up True; released True; the cup moved 0.074 m with the hand |
| 4 | Voice note heard within the first minute of a fresh start (sample day) | **pass** | script | voice note playing 33.1 s after a fresh start (limit 60 s); captions shown True; finished True |
| 5 | Reply on the table survives an app restart and an API restart | **pass** | script | reply 3cf37832-0491-4af0-abd4-d6af79952544 placed (new, typed 'r'); server has it True (placed True); back on the table after app restart True; API went down True and up True; server still has it True; back after API restart True |
| 6 | Recenter, pause and exit | **pass** | script | recenter: pressed True, event True, yaw -35.5 -> 0 deg; pause and resume True; exit: event True, app ended True |
| 7 | Hand tracking lost mid-press and found again: no ghost press | **pass** | script | right hand lost True and back True; presses during and after: 0 (count 0 -> 0); cancels: 1 |
| 8 | MP4 of the simulator for every app session | **pass** | script (FFmpeg window capture) | 6 of 6 recordings saved |
| 9 | Furniture chosen in the marketplace by a simulated hand is kept across an app restart | **pass** | script | chairs changed from 'sage' to 'charcoal' by hand in the marketplace and kept; server version 5 -> 6; after an app restart the room shows: chairs=tub/charcoal floor=parquet/natural lamp=drum/brass plant=leafy rug=round/oatmeal table=pedestal/walnut walls=plaster/ivory |

Execution order: Check1, FirstMinute, Check2, RenderTour, Check5, Check9, Check6, Check7. The first minute is timed on a fresh profile, so checks 3 and 4 run before check 2.

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
- probe state (check1-xr): xrDeviceActive=True, xrDevice=OpenXR Display, openxrRuntime=Meta XR Simulator 207.1.0, hands={"left":true,"right":true}, activeInput=hands, fps=58.5, screen=launch, head=[0.0,1.18,0.0], handsAt={"left":[-0.295,0.88,0.503],"right":[0.305,0.88,0.497]} [acceptance-022557/state-check1-xr.json]
- log (Unity log: OpenXR runtime used by Play mode): [SameSky:sim] play mode; XR_RUNTIME_JSON=%USERPROFILE%\SameSkyTools\MetaXRSimulator\PFiles\MetaXRSimulator\v207.1\meta_openxr_simulator.json

### 2. Dock button pressed by a simulated hand: pass

- probe event (dock press): `press dock:day`, input hands, t 65.923 s, app session 2
- probe event (what the press did): `screen day`, input hands, t 65.925 s, app session 2

### 3. Cup grabbed by a simulated hand: pass

- probe event (grab): `grab object:cup`, input hands, t 42.619 s, app session 2
- probe event (cup picked up): `cup picked-up`, input hands, t 42.62 s, app session 2
- probe state (check3-held): cupHeld=True, grabs=1, lastGrab=object:cup, activeInput=hands, handsAt={"left":[-0.421,0.83,0.374],"right":[0.179,0.83,0.368]} [acceptance-022557/state-check3-held.json]
- probe event (released): `release object:cup`, input hands, t 54.532 s, app session 2
- note: Taken with a palm grab.

### 4. Voice note heard within the first minute of a fresh start (sample day): pass

- probe event (app start (fresh profile)): `start 0.9.0`, input None, t 9.519 s, app session 2
- probe event (launch:sample pressed): `press launch:sample`, input hands, t 32.004 s, app session 2
- probe event (sample room open): `screen room`, input hands, t 32.138 s, app session 2
- probe event (voice note playing): `voice play`, input hands, t 42.627 s, app session 2
- probe event (what started the voice note): `grab object:cup`, input hands, t 42.619 s, app session 2
- probe state (check4-captions): voicePlaying=True, audio=cup:m-maya-voice, captionsOn=True, captions=The bench by the big oak, same as always., presence=recorded, paused=False [acceptance-022557/state-check4-captions.json]
- probe event (voice note finished): `voice finished`, input hands, t 52.059 s, app session 2
- probe-timing (start -> voice play (probe clock)): {"secondsFromStartToVoice":33.1,"limit":60}

### 5. Reply on the table survives an app restart and an API restart: pass

- probe event (launch:continue (first visit)): `press launch:continue`, input hands, t 24.413 s, app session 3
- probe event (live mode (first visit)): `mode live`, input hands, t 24.557 s, app session 3
- probe event (room open (first visit)): `screen room`, input hands, t 24.56 s, app session 3
- probe event (notes on the table before replying): `reply restored:3b49b61f-77d1-4a8e-96ef-9127f70c4eda,39a8de9c-bb02-488f-a09c-7933120e9283,2b77d9c8-0946-4728-86ed-100037f9cf4c,b45a4769-e2c5-46e0-a1a8-848a3d868724,5cf7d8f1-42f3-4b57-bec0-be627acf4396,d4b88319-640b-4df9-a502-605471033bf7,b8ee6dd1-795e-4cfa-9f98-38bc6e31749e,8b98c4d0-7a11-47b6-a28c-fbeffd167629,fe3bb850-7501-4159-ad1c-52759d859259,501eebfe-8c18-4f90-8609-d1f6b8ad00b8`, input hands, t 24.819 s, app session 3
- probe event (cup:reply pressed): `press cup:reply`, input hands, t 34.912 s, app session 3
- probe event (a character typed): `press key:r`, input hands, t 49.234 s, app session 3
- probe event (Done pressed): `press key:done`, input hands, t 72.345 s, app session 3
- probe event (reply placed on the table): `reply placed:3cf37832-0491-4af0-abd4-d6af79952544`, input hands, t 72.526 s, app session 3
- reply (the reply placed in this run): {"replyId":"3cf37832-0491-4af0-abd4-d6af79952544","typed":"r","newThisRun":true,"noraRepliesBefore":10}
- api (server after placing): {"replyId":"3cf37832-0491-4af0-abd4-d6af79952544","replyStored":true,"byNora":true,"placementStored":true,"placement":{"x":-0.297,"y":0.25,"z":0.35,"yaw":0.0}}
- probe event (launch:continue (after an app restart)): `press launch:continue`, input hands, t 24.772 s, app session 4
- probe event (live mode (after an app restart)): `mode live`, input hands, t 24.856 s, app session 4
- probe event (room open (after an app restart)): `screen room`, input hands, t 24.858 s, app session 4
- probe event (notes restored after an app restart): `reply restored:3b49b61f-77d1-4a8e-96ef-9127f70c4eda,39a8de9c-bb02-488f-a09c-7933120e9283,2b77d9c8-0946-4728-86ed-100037f9cf4c,b45a4769-e2c5-46e0-a1a8-848a3d868724,5cf7d8f1-42f3-4b57-bec0-be627acf4396,d4b88319-640b-4df9-a502-605471033bf7,b8ee6dd1-795e-4cfa-9f98-38bc6e31749e,8b98c4d0-7a11-47b6-a28c-fbeffd167629,fe3bb850-7501-4159-ad1c-52759d859259,501eebfe-8c18-4f90-8609-d1f6b8ad00b8,3cf37832-0491-4af0-abd4-d6af79952544`, input hands, t 25.067 s, app session 4
- service (API and worker restarted (stop.ps1, start.ps1)): {"apiDownAfterStop":true,"apiUpAfterStart":true,"apiPidBefore":62368,"apiPidAfter":52692,"workerPidBefore":52164,"workerPidAfter":83404}
- api (server after its restart): {"replyId":"3cf37832-0491-4af0-abd4-d6af79952544","replyStored":true,"byNora":true,"placementStored":true,"placement":{"x":-0.297,"y":0.25,"z":0.35,"yaw":0.0}}
- probe event (launch:continue (after an API restart)): `press launch:continue`, input hands, t 23.853 s, app session 5
- probe event (live mode (after an API restart)): `mode live`, input hands, t 23.941 s, app session 5
- probe event (room open (after an API restart)): `screen room`, input hands, t 23.944 s, app session 5
- probe event (notes restored after an API restart): `reply restored:3b49b61f-77d1-4a8e-96ef-9127f70c4eda,39a8de9c-bb02-488f-a09c-7933120e9283,2b77d9c8-0946-4728-86ed-100037f9cf4c,b45a4769-e2c5-46e0-a1a8-848a3d868724,5cf7d8f1-42f3-4b57-bec0-be627acf4396,d4b88319-640b-4df9-a502-605471033bf7,b8ee6dd1-795e-4cfa-9f98-38bc6e31749e,8b98c4d0-7a11-47b6-a28c-fbeffd167629,fe3bb850-7501-4159-ad1c-52759d859259,501eebfe-8c18-4f90-8609-d1f6b8ad00b8,3cf37832-0491-4af0-abd4-d6af79952544`, input hands, t 24.153 s, app session 5
- note: Nora is signed in by the session file the script wrote (typing the email and password on the spatial keyboard is not automated).

### 6. Recenter, pause and exit: pass

- probe state (check6-turned): head=[-0.158,1.247,-0.317], headForward=[-0.581,0.0,0.814] [acceptance-022557/state-check6-turned.json]
- probe event (dock:recenter pressed): `press dock:recenter`, input hands, t 44.921 s, app session 6
- probe event (recenter): `recenter 1.18`, input hands, t 44.922 s, app session 6
- probe state (check6-recentered): head=[-0.467,1.293,-0.629], headForward=[0.0,0.0,1.0] [acceptance-022557/state-check6-recentered.json]
- probe event (dock:pause pressed): `press dock:pause`, input hands, t 76.24 s, app session 6
- probe event (pause screen): `screen pause`, input hands, t 76.242 s, app session 6
- probe state (check6-paused): paused=True, screen=pause, audio=, voicePlaying=False [acceptance-022557/state-check6-paused.json]
- probe event (pause:resume pressed): `press pause:resume`, input hands, t 101.211 s, app session 6
- probe event (resumed): `pause resume`, input hands, t 101.212 s, app session 6
- probe event (back in the room): `screen room`, input hands, t 101.213 s, app session 6
- probe state (check6-resumed): paused=False, screen=room [acceptance-022557/state-check6-resumed.json]
- probe event (leave confirmation): `screen exit`, input hands, t 156.646 s, app session 6
- probe event (exit): `exit quit`, input hands, t 180.062 s, app session 6
- probe-stopped (the app ended (its probe stopped updating)): {"probeStoppedUpdating":true}

### 7. Hand tracking lost mid-press and found again: no ghost press: pass

- probe state (check7-before): hands={"left":true,"right":true}, activeInput=hands, presses=0, lastPress=, handsAt={"left":[-0.629,0.888,0.258],"right":[-0.029,0.888,0.258]} [acceptance-022557/state-check7-before.json]
- probe-target (was the button held down when tracking was dropped): {"target":"launch:recenter","downBeforeTrackingLoss":true}
- command (right-hand tracking off): `metavr.cmd xrsim input source --hand right --source none`, exit 0: HAND   SOURCE -----  ------ right  none
- probe state (check7-lost): hands={"left":false,"right":false}, activeInput=None, presses=0, lastPress= [acceptance-022557/state-check7-lost.json]
- command (hand tracking back): `metavr.cmd xrsim input sync --source hand`, exit 0: HANDS        SOURCE -----------  ------ left, right  hand
- probe state (check7-after): hands={"left":true,"right":true}, activeInput=hands, presses=0, lastPress= [acceptance-022557/state-check7-after.json]
- probe event (press cancelled by the tracking loss): `cancel launch:recenter`, input None, t 25.056 s, app session 7
- probe-count (presses counted by the probe): {"pressesBefore":0,"pressesAfter":0,"pressEvents":0,"cancelEvents":1}

### 8. MP4 of the simulator for every app session: pass

- recording (session2-sample): {"label":"session2-sample","file":"docs/native/evidence/2026-11-13/acceptance-022557/session2-sample.mp4","method":"window","bytes":2724170,"durationSec":129.5,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session2-sample.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session2-sample.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-13T02:28:20.7366237+04:00","stoppedAt":"2026-11-13T02:30:31.6853219+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":20.74},"probeAtStop":{"xrDeviceActive":true,"screen":"room","mode":"sample","activeInput":"hands","time":150.68}}
- probe state (app state when session2-sample started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=20.74
- recording (session3-live): {"label":"session3-live","file":"docs/native/evidence/2026-11-13/acceptance-022557/session3-live.mp4","method":"window","bytes":3289251,"durationSec":72.3,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session3-live.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session3-live.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-13T02:30:53.1637302+04:00","stoppedAt":"2026-11-13T02:32:06.3960756+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":15.77},"probeAtStop":{"xrDeviceActive":true,"screen":"room","mode":"live","activeInput":"hands","time":87.73}}
- probe state (app state when session3-live started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=15.77
- recording (session4-live-restarted): {"label":"session4-live-restarted","file":"docs/native/evidence/2026-11-13/acceptance-022557/session4-live-restarted.mp4","method":"window","bytes":587527,"durationSec":11.9,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session4-live-restarted.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session4-live-restarted.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-13T02:32:28.3680007+04:00","stoppedAt":"2026-11-13T02:32:41.3174920+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":16.14},"probeAtStop":{"xrDeviceActive":true,"screen":"room","mode":"live","activeInput":"hands","time":27.73}}
- probe state (app state when session4-live-restarted started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=16.14
- recording (session5-live-api-restarted): {"label":"session5-live-api-restarted","file":"docs/native/evidence/2026-11-13/acceptance-022557/session5-live-api-restarted.mp4","method":"window","bytes":4079931,"durationSec":76.4,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session5-live-api-restarted.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session5-live-api-restarted.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-13T02:33:20.3736360+04:00","stoppedAt":"2026-11-13T02:34:37.8243518+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":15.39},"probeAtStop":{"xrDeviceActive":true,"screen":"room","mode":"live","activeInput":"hands","time":91.67}}
- probe state (app state when session5-live-api-restarted started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=15.39
- recording (session6-live-market-restarted): {"label":"session6-live-market-restarted","file":"docs/native/evidence/2026-11-13/acceptance-022557/session6-live-market-restarted.mp4","method":"window","bytes":7476693,"durationSec":168.7,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session6-live-market-restarted.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session6-live-market-restarted.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-13T02:34:59.7141278+04:00","stoppedAt":"2026-11-13T02:37:49.4242864+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":16.38},"probeAtStop":{"xrDeviceActive":true,"screen":"exit","mode":"live","activeInput":"hands","time":179.86}}
- probe state (app state when session6-live-market-restarted started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=16.38
- recording (session7-tracking): {"label":"session7-tracking","file":"docs/native/evidence/2026-11-13/acceptance-022557/session7-tracking.mp4","method":"window","bytes":569692,"durationSec":20.1,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session7-tracking.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session7-tracking.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-13T02:38:06.3144422+04:00","stoppedAt":"2026-11-13T02:38:27.5564799+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":16.11},"probeAtStop":{"xrDeviceActive":true,"screen":"launch","mode":"none","activeInput":"hands","time":36.09}}
- probe state (app state when session7-tracking started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=16.11
- note: The simulator runtime has no MP4 recorder (metavr xrsim record says its build doesn't include the recording service), so FFmpeg recorded the simulator window: the 3D view as the simulator showed it, plus its side panel.

### 9. Furniture chosen in the marketplace by a simulated hand is kept across an app restart: pass

- api (the room on the server before): {"version":5,"chairs":{"item":"tub","finish":"sage"}}
- probe event (dock:market pressed): `press dock:market`, input hands, t 35.484 s, app session 5
- probe event (market:tab-chairs pressed): `press market:tab-chairs`, input hands, t 48.173 s, app session 5
- probe event (market:finish-chairs-charcoal pressed): `press market:finish-chairs-charcoal`, input hands, t 65.062 s, app session 5
- probe event (market:keep pressed): `press market:keep`, input hands, t 84.471 s, app session 5
- probe event (the fabric tried in the room (not kept yet)): `decor try chairs=tub/charcoal`, input hands, t 65.064 s, app session 5
- probe event (kept): `decor kept v6 chairs=tub/charcoal floor=parquet/natural lamp=drum/brass plant=leafy rug=round/oatmeal table=pedestal/walnut walls=plaster/ivory`, input hands, t 84.502 s, app session 5
- api (the room on the server after Keep): {"version":6,"chairs":{"item":"tub","finish":"charcoal"},"updatedByNora":true}
- probe event (launch:continue (after an app restart)): `press launch:continue`, input hands, t 24.35 s, app session 6
- probe event (live mode (after an app restart)): `mode live`, input hands, t 24.421 s, app session 6
- probe event (room open (after an app restart)): `screen room`, input hands, t 24.423 s, app session 6
- probe event (furniture shown after the restart): `decor shown v6`, input hands, t 24.459 s, app session 6
- probe state (check9-restarted): screen=room, mode=live, room=Nora + Sam, decor=chairs=tub/charcoal floor=parquet/natural lamp=drum/brass plant=leafy rug=round/oatmeal table=pedestal/walnut walls=plaster/ivory, decorVersion=6, decorUnsaved=False [acceptance-022557/state-check9-restarted.json]

## Setup

- API and worker running (scripts/native/start.ps1): ok
- Development accounts and rooms, and a voice note from Sam to Nora (seed_dev.py --voice-note): ok
- Signed in as Nora (nora.native@example.com, development account) through POST /v1/auth/login; the token is never written to the evidence: ok
- Meta XR Simulator window found: ok
- Simulator movement speed 0.3 (metavr xrsim input speed): ok
- Hand control: calibrated; device 'body'; fingertip offset [-0.054,0.008,0.168] (the app's right poke interactor (probe pokeAt), in the poke pose)

## Notes

- metavr xrsim record: Error: this XR Simulator runtime does not support native MP4 recording: its build doesn't include the recording service, so `record start`, `record stop` and `record status` keep failing against it; record the simulator window with your OS screen recorder instead The recordings are FFmpeg captures of the simulator window instead.
- For key:done the head was turned -26 degrees during the press and back afterwards: the simulator moves both hands together, and the left index would have come down on key:cancel.
- For dock:pause the head was turned -24 degrees during the press and back afterwards: the simulator moves both hands together, and the left index would have come down on dock:day.
- For dock:pause the head was turned -23 degrees during the press and back afterwards: the simulator moves both hands together, and the left index would have come down on dock:day.

## Files

- `acceptance-022557/seed.json`: seeded ids (development data), 560 bytes, sha256 `0efe88d686dd008d...`
- `acceptance-022557/seat-hands.png`: seated view (fixed camera): the tracked hands from where the head is: skin tone, nails, knuckles, their shadow, 1460114 bytes, sha256 `37f6f1c67ab79e77...`
- `acceptance-022557/seat-palms.png`: seated view (fixed camera): the tracked hands from the other side: palms, creases, finger pads, 382495 bytes, sha256 `a9c9a2c8176eeb6e...`
- `acceptance-022557/seat-hands-pinch.png`: seated view (fixed camera): the tracked hands pinching, 1470366 bytes, sha256 `70e8e7ff7a754e07...`
- `acceptance-022557/events-session1.jsonl`: probe events, app session 1, 492 bytes, sha256 `b36fa7405253760d...`
- `acceptance-022557/state-check1-xr.json`: probe state: check1-xr, 3314 bytes, sha256 `401bcf46b0ee55ab...`
- `acceptance-022557/sim-check1-session.png`: simulator view: check1-session, 978027 bytes, sha256 `c32059cc5d63f207...`
- `acceptance-022557/seat-launch.png`: seated view (fixed camera): the launch screen in the room (reference 10), 1108442 bytes, sha256 `f8de1c64fe89cde8...`
- `acceptance-022557/seat-room-cue.png`: seated view (fixed camera): the sample room with the pick-up cue (references 02 and 10), 1344075 bytes, sha256 `5715cdf7e8d98878...`
- `acceptance-022557/state-check3-held.json`: probe state: check3-held, 5911 bytes, sha256 `fa9bd8728cebaf2a...`
- `acceptance-022557/sim-check3-cup-held.png`: simulator view: check3-cup-held, 989246 bytes, sha256 `893174b2a23ceece...`
- `acceptance-022557/state-check4-captions.json`: probe state: check4-captions, 5942 bytes, sha256 `e65d39a6d2fa2e19...`
- `acceptance-022557/sim-check4-captions.png`: simulator view: check4-captions, 989247 bytes, sha256 `2f754b2ca07d3038...`
- `acceptance-022557/seat-message.png`: seated view (fixed camera): the cup lifted, its message card playing with captions (reference 03), 1304563 bytes, sha256 `2ccbf9e5c259233e...`
- `acceptance-022557/seat-room-partner.png`: seated view (fixed camera): after the message: the sample's partner as labelled simulated presence (reference 02), 1313256 bytes, sha256 `b006e23d82a11829...`
- `acceptance-022557/sim-check2-dock.png`: simulator view: check2-dock, 976381 bytes, sha256 `41281112386d310d...`
- `acceptance-022557/seat-tour-day.png`: seated view (fixed camera): Our day (reference 05), 1338054 bytes, sha256 `ba0bc61e761a5ea9...`
- `acceptance-022557/seat-tour-share.png`: seated view (fixed camera): Share: who receives it, and the AI permission (reference 06), 1190972 bytes, sha256 `891d23d3bba70e16...`
- `acceptance-022557/seat-tour-together.png`: seated view (fixed camera): Together: places, times and the next overlap (reference 08), 1217905 bytes, sha256 `9ee409a91d91a99c...`
- `acceptance-022557/seat-tour-keepsakes.png`: seated view (fixed camera): Keepsakes (reference 09), 1177148 bytes, sha256 `e163d07a50ecbd34...`
- `acceptance-022557/seat-tour-shelf.png`: seated view (fixed camera): the keepsake shelf in the alcove (reference 09), 908284 bytes, sha256 `83bad3e80dbb6644...`
- `acceptance-022557/seat-tour-avatar.png`: seated view (fixed camera): the avatar preview, front, three-quarter and side (references 01 and 13), 1212451 bytes, sha256 `9c52918b8facefda...`
- `acceptance-022557/seat-tour-settings.png`: seated view (fixed camera): Settings: captions, text size, sound, recenter (reference 12), 1161132 bytes, sha256 `c191c41062c2efdb...`
- `acceptance-022557/seat-tour-pause.png`: seated view (fixed camera): Paused (reference 12), 1201119 bytes, sha256 `bf949e7ccbb834ed...`
- `acceptance-022557/seat-tour-story.png`: seated view (fixed camera): the story on the screen beside the partner (references 04 and 05), 1215129 bytes, sha256 `9196897b70be3a16...`
- `acceptance-022557/seat-tour-rooms.png`: seated view (fixed camera): the room selector (reference 07), 1345840 bytes, sha256 `d5edc7915100c39d...`
- `acceptance-022557/seat-tour-circle.png`: seated view (fixed camera): the family circle room (reference 07), 1273038 bytes, sha256 `dd9699463d41be01...`
- `acceptance-022557/seat-tour-direct.png`: seated view (fixed camera): back in the direct room (reference 02), 1395966 bytes, sha256 `b68021a4e8f4593e...`
- `acceptance-022557/seat-tour-partner.png`: seated view (fixed camera): the partner's face, close (natural proportions, eyes, expression: reference 01), 1064057 bytes, sha256 `a7980ceb252ed1bf...`
- `acceptance-022557/seat-tour-left.png`: seated view (fixed camera): the room to the left: window light and shadows on the wall and floor, 1314204 bytes, sha256 `9d7f921d20cc2ccb...`
- `acceptance-022557/seat-tour-right.png`: seated view (fixed camera): the room to the right: the lamp and side table, 1153473 bytes, sha256 `1cac380bbdcbdf89...`
- `acceptance-022557/seat-tour-reply.png`: seated view (fixed camera): replying on the keyboard at the table edge (reference 03), 1304636 bytes, sha256 `ab74546b7d40c4ea...`
- `acceptance-022557/seat-tour-market.png`: seated view (fixed camera): Marketplace: ready-made looks, 1086728 bytes, sha256 `c4d7a94ddb168639...`
- `acceptance-022557/seat-tour-market-table.png`: seated view (fixed camera): Marketplace: tables and their finishes, 1119053 bytes, sha256 `d7548ef9fa3aa05c...`
- `acceptance-022557/seat-tour-market-chairs.png`: seated view (fixed camera): Marketplace: chair fabrics, 1118436 bytes, sha256 `63fd1e040eb18613...`
- `acceptance-022557/seat-tour-market-lamp.png`: seated view (fixed camera): Marketplace: lamps, 1097498 bytes, sha256 `0134f2f11bb26097...`
- `acceptance-022557/seat-tour-market-walls.png`: seated view (fixed camera): Marketplace: wall colours, 1071813 bytes, sha256 `60f5b1bb5ad86387...`
- `acceptance-022557/seat-tour-look-coastal.png`: seated view (fixed camera): the room in the Coastal oak look, 1493531 bytes, sha256 `9109aba44d25f981...`
- `acceptance-022557/seat-tour-look-coastal-left.png`: seated view (fixed camera): Coastal oak: the wall, rug and plant to the left, 1370758 bytes, sha256 `83a30ada60d2df00...`
- `acceptance-022557/seat-tour-look-coastal-right.png`: seated view (fixed camera): Coastal oak: the tripod lamp to the right, 1224427 bytes, sha256 `4d48afde704e3307...`
- `acceptance-022557/seat-tour-look-evening.png`: seated view (fixed camera): the room in the Evening study look, 1377075 bytes, sha256 `80ab8826a75ba237...`
- `acceptance-022557/seat-tour-look-evening-right.png`: seated view (fixed camera): Evening study: the globe lamp to the right, 1141028 bytes, sha256 `3d271099e66706ef...`
- `acceptance-022557/seat-tour-hands-panel.png`: seated view (fixed camera): Your hands: the skin tone choice, 1290877 bytes, sha256 `cf0fe12b28afc6d0...`
- `acceptance-022557/seat-tour-direct-again.png`: seated view (fixed camera): back in the room as it is kept, 1395782 bytes, sha256 `855c99198d350c67...`
- `acceptance-022557/session2-sample.mp4`: simulator window recording (FFmpeg): session2-sample, 2724170 bytes, sha256 `f125124c245c110b...`
- `acceptance-022557/events-session2.jsonl`: probe events, app session 2, 7926 bytes, sha256 `964d544032d00fe3...`
- `acceptance-022557/sim-check5-placed.png`: simulator view: check5-placed, 921751 bytes, sha256 `29ac8591e6038ef1...`
- `acceptance-022557/seat-live-room.png`: seated view (fixed camera): the live room: the reply standing on the table as a folded card (reference 03), 1255588 bytes, sha256 `9f8adb2ecf139f26...`
- `acceptance-022557/session3-live.mp4`: simulator window recording (FFmpeg): session3-live, 3289251 bytes, sha256 `453ef4b5cd0c161f...`
- `acceptance-022557/events-session3.jsonl`: probe events, app session 3, 1644 bytes, sha256 `b49806b7a670976f...`
- `acceptance-022557/sim-check5-after-app-restart.png`: simulator view: check5-after-app-restart, 985025 bytes, sha256 `8402c687e111e1fa...`
- `acceptance-022557/session4-live-restarted.mp4`: simulator window recording (FFmpeg): session4-live-restarted, 587527 bytes, sha256 `43bb012b4dd11852...`
- `acceptance-022557/events-session4.jsonl`: probe events, app session 4, 1158 bytes, sha256 `920cfb42768b64e9...`
- `acceptance-022557/sim-check5-after-api-restart.png`: simulator view: check5-after-api-restart, 986467 bytes, sha256 `c588de6d32f17013...`
- `acceptance-022557/sim-check9-trying.png`: simulator view: check9-trying, 911620 bytes, sha256 `d6028802431bd006...`
- `acceptance-022557/seat-market-trying.png`: seated view (fixed camera): the marketplace open, the chairs tried in charcoal, 948684 bytes, sha256 `35635954e08486e3...`
- `acceptance-022557/seat-market-kept.png`: seated view (fixed camera): the live room with the chairs in charcoal, kept from the marketplace, 1201150 bytes, sha256 `f8576b7cc1d6acdd...`
- `acceptance-022557/session5-live-api-restarted.mp4`: simulator window recording (FFmpeg): session5-live-api-restarted, 4079931 bytes, sha256 `f6195d161e359166...`
- `acceptance-022557/events-session5.jsonl`: probe events, app session 5, 2798 bytes, sha256 `7086b80e98a88270...`
- `acceptance-022557/state-check9-restarted.json`: probe state: check9-restarted, 4684 bytes, sha256 `638f26c2f21b3294...`
- `acceptance-022557/sim-check9-after-app-restart.png`: simulator view: check9-after-app-restart, 979623 bytes, sha256 `10ba38248d3eb92e...`
- `acceptance-022557/state-check6-turned.json`: probe state: check6-turned, 4737 bytes, sha256 `221437e3c1e0c11e...`
- `acceptance-022557/state-check6-recentered.json`: probe state: check6-recentered, 4732 bytes, sha256 `e7c1f96e185e980d...`
- `acceptance-022557/state-check6-paused.json`: probe state: check6-paused, 3820 bytes, sha256 `1add5c35dfc9f738...`
- `acceptance-022557/sim-check6-paused.png`: simulator view: check6-paused, 955232 bytes, sha256 `b71072b761831ddb...`
- `acceptance-022557/state-check6-resumed.json`: probe state: check6-resumed, 4734 bytes, sha256 `e8cae961691272b7...`
- `acceptance-022557/session6-live-market-restarted.mp4`: simulator window recording (FFmpeg): session6-live-market-restarted, 7476693 bytes, sha256 `e23f21c86ddfc1bf...`
- `acceptance-022557/events-session6.jsonl`: probe events, app session 6, 2952 bytes, sha256 `cc3695f582146472...`
- `acceptance-022557/state-check7-before.json`: probe state: check7-before, 3633 bytes, sha256 `3124f2ad63e82dfd...`
- `acceptance-022557/state-check7-lost.json`: probe state: check7-lost, 3535 bytes, sha256 `2f32a48715b6a535...`
- `acceptance-022557/state-check7-after.json`: probe state: check7-after, 3631 bytes, sha256 `87187fd6d8d362cf...`
- `acceptance-022557/sim-check7-after.png`: simulator view: check7-after, 957288 bytes, sha256 `66eed4015e0a9c64...`
- `acceptance-022557/session7-tracking.mp4`: simulator window recording (FFmpeg): session7-tracking, 569692 bytes, sha256 `69153ec31f83c13f...`
- `acceptance-022557/events-session7.jsonl`: probe events, app session 7, 377 bytes, sha256 `9b0072ee0471e70e...`
