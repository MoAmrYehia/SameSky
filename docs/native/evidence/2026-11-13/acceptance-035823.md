# Native acceptance run acceptance-035823

13 November 2026 03:58, commit `8c4d854` (with uncommitted changes). Unity Play mode (editor) in Meta XR Simulator, profile `acceptance`, API http://127.0.0.1:8000.

Evidence kind: **simulator-tested**. Meta XR Simulator with a simulated Meta Quest 3 and simulated hands. It is not a headset result, and says nothing about frame rate, comfort, spatial audio or real hand tracking on a Quest.

| # | Check | Result | Driven by | What the probe saw |
|---|---|---|---|---|
| 1 | XR session active: simulated Meta Quest 3 with hand input | **pass** | script (metavr) | XR active True, runtime 'Meta XR Simulator 207.1.0', hands L True R True, input hands; metavr device set exit 0, input sync exit 0/0 |
| 2 | Dock button pressed by a simulated hand | **pass** | script | pressed dock:day with input 'hands' (script); then screen day |
| 3 | Cup grabbed by a simulated hand | **pass** | script | grab of object:cup with input 'hands' (script); picked-up True; released True; the cup moved 0.05 m with the hand |
| 4 | Voice note heard within the first minute of a fresh start (sample day) | **pass** | script | voice note playing 33.1 s after a fresh start (limit 60 s); captions shown True; finished True |
| 5 | Reply on the table survives an app restart and an API restart | **pass** | script | reply 8248113e-8b7c-4388-bfd8-3a25f355e64f placed (new, typed 'e'); server has it True (placed True); back on the table after app restart True; API went down True and up True; server still has it True; back after API restart True |
| 6 | Recenter, pause and exit | **pass** | script | recenter: pressed True, event True, yaw -35.5 -> 0 deg; pause and resume True; exit: event True, app ended True |
| 7 | Hand tracking lost mid-press and found again: no ghost press | **pass** | script | right hand lost True and back True; presses during and after: 0 (count 0 -> 0); cancels: 1 |
| 8 | MP4 of the simulator for every app session | **pass** | script (FFmpeg window capture) | 6 of 6 recordings saved |
| 9 | Furniture chosen in the marketplace by a simulated hand is kept across an app restart | **pass** | script | chairs changed from 'charcoal' to 'sage' by hand in the marketplace and kept; server version 6 -> 7; after an app restart the room shows: chairs=tub/sage floor=parquet/natural lamp=drum/brass plant=leafy rug=round/oatmeal table=pedestal/walnut walls=plaster/ivory |

Execution order: Check1, FirstMinute, Check2, Check5, Check9, Check6, Check7. The first minute is timed on a fresh profile, so checks 3 and 4 run before check 2.

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
- probe state (check1-xr): xrDeviceActive=True, xrDevice=OpenXR Display, openxrRuntime=Meta XR Simulator 207.1.0, hands={"left":true,"right":true}, activeInput=hands, fps=57.6, screen=launch, head=[0.0,1.18,0.0], handsAt={"left":[-0.295,0.88,0.503],"right":[0.305,0.88,0.497]} [acceptance-035823/state-check1-xr.json]
- log (Unity log: OpenXR runtime used by Play mode): [SameSky:sim] play mode; XR_RUNTIME_JSON=%USERPROFILE%\SameSkyTools\MetaXRSimulator\PFiles\MetaXRSimulator\v207.1\meta_openxr_simulator.json

### 2. Dock button pressed by a simulated hand: pass

- probe event (dock press): `press dock:day`, input hands, t 66.786 s, app session 2
- probe event (what the press did): `screen day`, input hands, t 66.787 s, app session 2

### 3. Cup grabbed by a simulated hand: pass

- probe event (grab): `grab object:cup`, input hands, t 43.176 s, app session 2
- probe event (cup picked up): `cup picked-up`, input hands, t 43.177 s, app session 2
- probe state (check3-held): cupHeld=True, grabs=1, lastGrab=object:cup, activeInput=hands, handsAt={"left":[-0.426,0.834,0.37],"right":[0.174,0.834,0.365]} [acceptance-035823/state-check3-held.json]
- probe event (released): `release object:cup`, input hands, t 55.105 s, app session 2
- note: Taken with a palm grab.

### 4. Voice note heard within the first minute of a fresh start (sample day): pass

- probe event (app start (fresh profile)): `start 0.9.0`, input None, t 10.067 s, app session 2
- probe event (launch:sample pressed): `press launch:sample`, input hands, t 33.047 s, app session 2
- probe event (sample room open): `screen room`, input hands, t 33.165 s, app session 2
- probe event (voice note playing): `voice play`, input hands, t 43.188 s, app session 2
- probe event (what started the voice note): `grab object:cup`, input hands, t 43.176 s, app session 2
- probe state (check4-captions): voicePlaying=True, audio=cup:m-maya-voice, captionsOn=True, captions=The bench by the big oak, same as always., presence=recorded, paused=False [acceptance-035823/state-check4-captions.json]
- probe event (voice note finished): `voice finished`, input hands, t 52.689 s, app session 2
- probe-timing (start -> voice play (probe clock)): {"secondsFromStartToVoice":33.1,"limit":60}

### 5. Reply on the table survives an app restart and an API restart: pass

- probe event (launch:continue (first visit)): `press launch:continue`, input hands, t 20.912 s, app session 3
- probe event (live mode (first visit)): `mode live`, input hands, t 20.998 s, app session 3
- probe event (room open (first visit)): `screen room`, input hands, t 21.001 s, app session 3
- probe event (notes on the table before replying): `reply restored:3b49b61f-77d1-4a8e-96ef-9127f70c4eda,39a8de9c-bb02-488f-a09c-7933120e9283,2b77d9c8-0946-4728-86ed-100037f9cf4c,b45a4769-e2c5-46e0-a1a8-848a3d868724,5cf7d8f1-42f3-4b57-bec0-be627acf4396,d4b88319-640b-4df9-a502-605471033bf7,b8ee6dd1-795e-4cfa-9f98-38bc6e31749e,8b98c4d0-7a11-47b6-a28c-fbeffd167629,fe3bb850-7501-4159-ad1c-52759d859259,501eebfe-8c18-4f90-8609-d1f6b8ad00b8,3cf37832-0491-4af0-abd4-d6af79952544`, input hands, t 21.193 s, app session 3
- probe event (cup:reply pressed): `press cup:reply`, input hands, t 32.578 s, app session 3
- probe event (a character typed): `press key:e`, input hands, t 44.621 s, app session 3
- probe event (Done pressed): `press key:done`, input hands, t 69.099 s, app session 3
- probe event (reply placed on the table): `reply placed:8248113e-8b7c-4388-bfd8-3a25f355e64f`, input hands, t 69.226 s, app session 3
- reply (the reply placed in this run): {"replyId":"8248113e-8b7c-4388-bfd8-3a25f355e64f","typed":"e","newThisRun":true,"noraRepliesBefore":11}
- api (server after placing): {"replyId":"8248113e-8b7c-4388-bfd8-3a25f355e64f","replyStored":true,"byNora":true,"placementStored":true,"placement":{"x":-0.42,"y":0.25,"z":0.35,"yaw":0.0}}
- probe event (launch:continue (after an app restart)): `press launch:continue`, input hands, t 30.234 s, app session 4
- probe event (live mode (after an app restart)): `mode live`, input hands, t 30.304 s, app session 4
- probe event (room open (after an app restart)): `screen room`, input hands, t 30.309 s, app session 4
- probe event (notes restored after an app restart): `reply restored:3b49b61f-77d1-4a8e-96ef-9127f70c4eda,39a8de9c-bb02-488f-a09c-7933120e9283,2b77d9c8-0946-4728-86ed-100037f9cf4c,b45a4769-e2c5-46e0-a1a8-848a3d868724,5cf7d8f1-42f3-4b57-bec0-be627acf4396,d4b88319-640b-4df9-a502-605471033bf7,b8ee6dd1-795e-4cfa-9f98-38bc6e31749e,8b98c4d0-7a11-47b6-a28c-fbeffd167629,fe3bb850-7501-4159-ad1c-52759d859259,501eebfe-8c18-4f90-8609-d1f6b8ad00b8,3cf37832-0491-4af0-abd4-d6af79952544,8248113e-8b7c-4388-bfd8-3a25f355e64f`, input hands, t 30.49 s, app session 4
- service (API and worker restarted (stop.ps1, start.ps1)): {"apiDownAfterStop":true,"apiUpAfterStart":true,"apiPidBefore":86896,"apiPidAfter":70864,"workerPidBefore":91624,"workerPidAfter":53912}
- api (server after its restart): {"replyId":"8248113e-8b7c-4388-bfd8-3a25f355e64f","replyStored":true,"byNora":true,"placementStored":true,"placement":{"x":-0.42,"y":0.25,"z":0.35,"yaw":0.0}}
- probe event (launch:continue (after an API restart)): `press launch:continue`, input hands, t 24.147 s, app session 5
- probe event (live mode (after an API restart)): `mode live`, input hands, t 24.23 s, app session 5
- probe event (room open (after an API restart)): `screen room`, input hands, t 24.232 s, app session 5
- probe event (notes restored after an API restart): `reply restored:3b49b61f-77d1-4a8e-96ef-9127f70c4eda,39a8de9c-bb02-488f-a09c-7933120e9283,2b77d9c8-0946-4728-86ed-100037f9cf4c,b45a4769-e2c5-46e0-a1a8-848a3d868724,5cf7d8f1-42f3-4b57-bec0-be627acf4396,d4b88319-640b-4df9-a502-605471033bf7,b8ee6dd1-795e-4cfa-9f98-38bc6e31749e,8b98c4d0-7a11-47b6-a28c-fbeffd167629,fe3bb850-7501-4159-ad1c-52759d859259,501eebfe-8c18-4f90-8609-d1f6b8ad00b8,3cf37832-0491-4af0-abd4-d6af79952544,8248113e-8b7c-4388-bfd8-3a25f355e64f`, input hands, t 24.427 s, app session 5
- note: Nora is signed in by the session file the script wrote (typing the email and password on the spatial keyboard is not automated).

### 6. Recenter, pause and exit: pass

- probe state (check6-turned): head=[-0.146,1.23,-0.317], headForward=[-0.581,0.0,0.814] [acceptance-035823/state-check6-turned.json]
- probe event (dock:recenter pressed): `press dock:recenter`, input hands, t 45.412 s, app session 6
- probe event (recenter): `recenter 1.18`, input hands, t 45.413 s, app session 6
- probe state (check6-recentered): head=[-0.454,1.28,-0.633], headForward=[0.0,0.0,1.0] [acceptance-035823/state-check6-recentered.json]
- probe event (dock:pause pressed): `press dock:pause`, input hands, t 76.258 s, app session 6
- probe event (pause screen): `screen pause`, input hands, t 76.26 s, app session 6
- probe state (check6-paused): paused=True, screen=pause, audio=, voicePlaying=False [acceptance-035823/state-check6-paused.json]
- probe event (pause:resume pressed): `press pause:resume`, input hands, t 101.026 s, app session 6
- probe event (resumed): `pause resume`, input hands, t 101.027 s, app session 6
- probe event (back in the room): `screen room`, input hands, t 101.028 s, app session 6
- probe state (check6-resumed): paused=False, screen=room [acceptance-035823/state-check6-resumed.json]
- probe event (leave confirmation): `screen exit`, input hands, t 156.133 s, app session 6
- probe event (exit): `exit quit`, input hands, t 179.774 s, app session 6
- probe-stopped (the app ended (its probe stopped updating)): {"probeStoppedUpdating":true}

### 7. Hand tracking lost mid-press and found again: no ghost press: pass

- probe state (check7-before): hands={"left":true,"right":true}, activeInput=hands, presses=0, lastPress=, handsAt={"left":[-0.627,0.883,0.271],"right":[-0.027,0.883,0.265]} [acceptance-035823/state-check7-before.json]
- probe-target (was the button held down when tracking was dropped): {"target":"launch:recenter","downBeforeTrackingLoss":true}
- command (right-hand tracking off): `metavr.cmd xrsim input source --hand right --source none`, exit 0: HAND   SOURCE -----  ------ right  none
- probe state (check7-lost): hands={"left":false,"right":false}, activeInput=None, presses=0, lastPress= [acceptance-035823/state-check7-lost.json]
- command (hand tracking back): `metavr.cmd xrsim input sync --source hand`, exit 0: HANDS        SOURCE -----------  ------ left, right  hand
- probe state (check7-after): hands={"left":true,"right":true}, activeInput=hands, presses=0, lastPress= [acceptance-035823/state-check7-after.json]
- probe event (press cancelled by the tracking loss): `cancel launch:recenter`, input hands, t 31.584 s, app session 7
- probe-count (presses counted by the probe): {"pressesBefore":0,"pressesAfter":0,"pressEvents":0,"cancelEvents":1}

### 8. MP4 of the simulator for every app session: pass

- recording (session2-sample): {"label":"session2-sample","file":"docs/native/evidence/2026-11-13/acceptance-035823/session2-sample.mp4","method":"window","bytes":1800363,"durationSec":48.1,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session2-sample.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session2-sample.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-13T04:00:59.2419692+04:00","stoppedAt":"2026-11-13T04:01:48.7041655+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":21.87},"probeAtStop":{"xrDeviceActive":true,"screen":"day","mode":"sample","activeInput":"hands","time":69.93}}
- probe state (app state when session2-sample started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=21.87
- recording (session3-live): {"label":"session3-live","file":"docs/native/evidence/2026-11-13/acceptance-035823/session3-live.mp4","method":"window","bytes":3258045,"durationSec":71.2,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session3-live.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session3-live.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-13T04:02:07.4633714+04:00","stoppedAt":"2026-11-13T04:03:19.6615342+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":12.79},"probeAtStop":{"xrDeviceActive":true,"screen":"room","mode":"live","activeInput":"hands","time":83.74}}
- probe state (app state when session3-live started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=12.79
- recording (session4-live-restarted): {"label":"session4-live-restarted","file":"docs/native/evidence/2026-11-13/acceptance-035823/session4-live-restarted.mp4","method":"window","bytes":605616,"durationSec":11.9,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session4-live-restarted.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session4-live-restarted.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-13T04:03:48.7301139+04:00","stoppedAt":"2026-11-13T04:04:01.6366972+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":22.58},"probeAtStop":{"xrDeviceActive":true,"screen":"room","mode":"live","activeInput":"hands","time":33.98}}
- probe state (app state when session4-live-restarted started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=22.58
- recording (session5-live-api-restarted): {"label":"session5-live-api-restarted","file":"docs/native/evidence/2026-11-13/acceptance-035823/session5-live-api-restarted.mp4","method":"window","bytes":3962191,"durationSec":75.8,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session5-live-api-restarted.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session5-live-api-restarted.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-13T04:04:34.8492214+04:00","stoppedAt":"2026-11-13T04:05:51.6048553+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":15.68},"probeAtStop":{"xrDeviceActive":true,"screen":"room","mode":"live","activeInput":"hands","time":91.07}}
- probe state (app state when session5-live-api-restarted started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=15.68
- recording (session6-live-market-restarted): {"label":"session6-live-market-restarted","file":"docs/native/evidence/2026-11-13/acceptance-035823/session6-live-market-restarted.mp4","method":"window","bytes":7127664,"durationSec":169.2,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session6-live-market-restarted.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session6-live-market-restarted.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-13T04:06:13.2919613+04:00","stoppedAt":"2026-11-13T04:09:03.4652300+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":15.8},"probeAtStop":{"xrDeviceActive":true,"screen":"exit","mode":"live","activeInput":"hands","time":179.69}}
- probe state (app state when session6-live-market-restarted started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=15.8
- recording (session7-tracking): {"label":"session7-tracking","file":"docs/native/evidence/2026-11-13/acceptance-035823/session7-tracking.mp4","method":"window","bytes":575087,"durationSec":20.1,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session7-tracking.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session7-tracking.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-13T04:09:26.6474896+04:00","stoppedAt":"2026-11-13T04:09:47.7742550+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":22.29},"probeAtStop":{"xrDeviceActive":true,"screen":"launch","mode":"none","activeInput":"hands","time":42.11}}
- probe state (app state when session7-tracking started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=22.29
- note: The simulator runtime has no MP4 recorder (metavr xrsim record says its build doesn't include the recording service), so FFmpeg recorded the simulator window: the 3D view as the simulator showed it, plus its side panel.

### 9. Furniture chosen in the marketplace by a simulated hand is kept across an app restart: pass

- api (the room on the server before): {"version":6,"chairs":{"item":"tub","finish":"charcoal"}}
- probe event (dock:market pressed): `press dock:market`, input hands, t 35.2 s, app session 5
- probe event (market:tab-chairs pressed): `press market:tab-chairs`, input hands, t 48.267 s, app session 5
- probe event (market:finish-chairs-sage pressed): `press market:finish-chairs-sage`, input hands, t 64.781 s, app session 5
- probe event (market:keep pressed): `press market:keep`, input hands, t 84.045 s, app session 5
- probe event (the fabric tried in the room (not kept yet)): `decor try chairs=tub/sage`, input hands, t 64.783 s, app session 5
- probe event (kept): `decor kept v7 chairs=tub/sage floor=parquet/natural lamp=drum/brass plant=leafy rug=round/oatmeal table=pedestal/walnut walls=plaster/ivory`, input hands, t 84.097 s, app session 5
- api (the room on the server after Keep): {"version":7,"chairs":{"item":"tub","finish":"sage"},"updatedByNora":true}
- probe event (launch:continue (after an app restart)): `press launch:continue`, input hands, t 23.981 s, app session 6
- probe event (live mode (after an app restart)): `mode live`, input hands, t 24.042 s, app session 6
- probe event (room open (after an app restart)): `screen room`, input hands, t 24.045 s, app session 6
- probe event (furniture shown after the restart): `decor shown v7`, input hands, t 24.076 s, app session 6
- probe state (check9-restarted): screen=room, mode=live, room=Nora + Sam, decor=chairs=tub/sage floor=parquet/natural lamp=drum/brass plant=leafy rug=round/oatmeal table=pedestal/walnut walls=plaster/ivory, decorVersion=7, decorUnsaved=False [acceptance-035823/state-check9-restarted.json]

## Setup

- API and worker running (scripts/native/start.ps1): ok
- Development accounts and rooms, and a voice note from Sam to Nora (seed_dev.py --voice-note): ok
- Signed in as Nora (nora.native@example.com, development account) through POST /v1/auth/login; the token is never written to the evidence: ok
- Meta XR Simulator window found: ok
- Simulator movement speed 0.3 (metavr xrsim input speed): ok
- Hand control: calibrated; device 'body'; fingertip offset [-0.054,0.008,0.168] (the app's right poke interactor (probe pokeAt), in the poke pose)

## Notes

- metavr xrsim record: Error: this XR Simulator runtime does not support native MP4 recording: its build doesn't include the recording service, so `record start`, `record stop` and `record status` keep failing against it; record the simulator window with your OS screen recorder instead The recordings are FFmpeg captures of the simulator window instead.
- For key:done the head was turned -24 degrees during the press and back afterwards: the simulator moves both hands together, and the left index would have come down on key:cancel.
- For dock:pause the head was turned -26 degrees during the press and back afterwards: the simulator moves both hands together, and the left index would have come down on dock:day.
- For dock:pause the head was turned -24 degrees during the press and back afterwards: the simulator moves both hands together, and the left index would have come down on dock:day.

## Files

- `acceptance-035823/seed.json`: seeded ids (development data), 560 bytes, sha256 `0efe88d686dd008d...`
- `acceptance-035823/seat-hands.png`: seated view (fixed camera): the tracked hands from where the head is: skin tone, nails, knuckles, their shadow, 1373112 bytes, sha256 `54b4d890c639ce21...`
- `acceptance-035823/seat-palms.png`: seated view (fixed camera): the tracked hands from the other side: palms, creases, finger pads, 360575 bytes, sha256 `92685ccda0628d82...`
- `acceptance-035823/seat-hands-pinch.png`: seated view (fixed camera): the tracked hands pinching, 1387470 bytes, sha256 `851f86e6bb509838...`
- `acceptance-035823/events-session1.jsonl`: probe events, app session 1, 488 bytes, sha256 `8c4b7cb2f370e576...`
- `acceptance-035823/state-check1-xr.json`: probe state: check1-xr, 3338 bytes, sha256 `6106dcb20c6a235e...`
- `acceptance-035823/sim-check1-session.png`: simulator view: check1-session, 915234 bytes, sha256 `9189062ba093c809...`
- `acceptance-035823/seat-launch.png`: seated view (fixed camera): the launch screen in the room (reference 10), 1029882 bytes, sha256 `6a010a119690515b...`
- `acceptance-035823/seat-room-cue.png`: seated view (fixed camera): the sample room with the pick-up cue (references 02 and 10), 1242416 bytes, sha256 `1080c97cd531e0e9...`
- `acceptance-035823/state-check3-held.json`: probe state: check3-held, 5938 bytes, sha256 `f7ce0db1c22b3b2b...`
- `acceptance-035823/sim-check3-cup-held.png`: simulator view: check3-cup-held, 937904 bytes, sha256 `ab64d503df5268b6...`
- `acceptance-035823/state-check4-captions.json`: probe state: check4-captions, 5966 bytes, sha256 `959b4abe2ccb173f...`
- `acceptance-035823/sim-check4-captions.png`: simulator view: check4-captions, 937912 bytes, sha256 `8b31a8bff0d2473f...`
- `acceptance-035823/seat-message.png`: seated view (fixed camera): the cup lifted, its message card playing with captions (reference 03), 1200200 bytes, sha256 `fce3f3bcde523551...`
- `acceptance-035823/seat-room-partner.png`: seated view (fixed camera): after the message: the sample's partner as labelled simulated presence (reference 02), 1220198 bytes, sha256 `aa14b20c00e4a177...`
- `acceptance-035823/sim-check2-dock.png`: simulator view: check2-dock, 936784 bytes, sha256 `d514e6660ef11701...`
- `acceptance-035823/session2-sample.mp4`: simulator window recording (FFmpeg): session2-sample, 1800363 bytes, sha256 `0801f583c5a5459d...`
- `acceptance-035823/events-session2.jsonl`: probe events, app session 2, 1939 bytes, sha256 `4d49b212b595fbbb...`
- `acceptance-035823/sim-check5-placed.png`: simulator view: check5-placed, 883369 bytes, sha256 `c2bc1b98c1787f22...`
- `acceptance-035823/seat-live-room.png`: seated view (fixed camera): the live room: the reply standing on the table as a folded card (reference 03), 1152843 bytes, sha256 `e7a9021e74ce94e2...`
- `acceptance-035823/session3-live.mp4`: simulator window recording (FFmpeg): session3-live, 3258045 bytes, sha256 `b5cd2ce56cb5818d...`
- `acceptance-035823/events-session3.jsonl`: probe events, app session 3, 1682 bytes, sha256 `d438d25d15704235...`
- `acceptance-035823/sim-check5-after-app-restart.png`: simulator view: check5-after-app-restart, 943432 bytes, sha256 `5de482a72c759373...`
- `acceptance-035823/session4-live-restarted.mp4`: simulator window recording (FFmpeg): session4-live-restarted, 605616 bytes, sha256 `8706573c1ea5d30e...`
- `acceptance-035823/events-session4.jsonl`: probe events, app session 4, 1196 bytes, sha256 `d03a5403d65d2b3b...`
- `acceptance-035823/sim-check5-after-api-restart.png`: simulator view: check5-after-api-restart, 934169 bytes, sha256 `c661c49e8627240f...`
- `acceptance-035823/sim-check9-trying.png`: simulator view: check9-trying, 875634 bytes, sha256 `68a94c7c43f3f31c...`
- `acceptance-035823/seat-market-trying.png`: seated view (fixed camera): the marketplace open, the chairs tried in sage, 899123 bytes, sha256 `04932ffadfca1aa8...`
- `acceptance-035823/seat-market-kept.png`: seated view (fixed camera): the live room with the chairs in sage, kept from the marketplace, 1139594 bytes, sha256 `702630565cf53775...`
- `acceptance-035823/session5-live-api-restarted.mp4`: simulator window recording (FFmpeg): session5-live-api-restarted, 3962191 bytes, sha256 `af2c2b88916938ed...`
- `acceptance-035823/events-session5.jsonl`: probe events, app session 5, 2857 bytes, sha256 `9cc86b01e32f6fd5...`
- `acceptance-035823/state-check9-restarted.json`: probe state: check9-restarted, 4703 bytes, sha256 `268b5475be0d180b...`
- `acceptance-035823/sim-check9-after-app-restart.png`: simulator view: check9-after-app-restart, 943594 bytes, sha256 `45818706c77a080e...`
- `acceptance-035823/state-check6-turned.json`: probe state: check6-turned, 4756 bytes, sha256 `a79f79531dd82fff...`
- `acceptance-035823/state-check6-recentered.json`: probe state: check6-recentered, 4751 bytes, sha256 `d6122fa1a32dfb16...`
- `acceptance-035823/state-check6-paused.json`: probe state: check6-paused, 3844 bytes, sha256 `7092abe7aa7b73f3...`
- `acceptance-035823/sim-check6-paused.png`: simulator view: check6-paused, 908025 bytes, sha256 `4da1473e8474054d...`
- `acceptance-035823/state-check6-resumed.json`: probe state: check6-resumed, 4755 bytes, sha256 `afe1bfb05155c474...`
- `acceptance-035823/session6-live-market-restarted.mp4`: simulator window recording (FFmpeg): session6-live-market-restarted, 7127664 bytes, sha256 `206a95b3ced87c47...`
- `acceptance-035823/events-session6.jsonl`: probe events, app session 6, 3026 bytes, sha256 `52b3c40694530b0c...`
- `acceptance-035823/state-check7-before.json`: probe state: check7-before, 3661 bytes, sha256 `f1cdbb85d96bcacd...`
- `acceptance-035823/state-check7-lost.json`: probe state: check7-lost, 3562 bytes, sha256 `5eee38e9cf75a511...`
- `acceptance-035823/state-check7-after.json`: probe state: check7-after, 3662 bytes, sha256 `3d2cd8fe2896afdb...`
- `acceptance-035823/sim-check7-after.png`: simulator view: check7-after, 939718 bytes, sha256 `f1604bd44afc9318...`
- `acceptance-035823/session7-tracking.mp4`: simulator window recording (FFmpeg): session7-tracking, 575087 bytes, sha256 `46d48f29838b3557...`
- `acceptance-035823/events-session7.jsonl`: probe events, app session 7, 377 bytes, sha256 `15ea3a73d49c703c...`
