# Native acceptance run acceptance-125054

12 November 2026 12:50, commit `6e510b8` (with uncommitted changes). Unity Play mode (editor) in Meta XR Simulator, profile `acceptance`, API http://127.0.0.1:8000.

Evidence kind: **simulator-tested**. Meta XR Simulator with a simulated Meta Quest 3 and simulated hands. It is not a headset result, and says nothing about frame rate, comfort, spatial audio or real hand tracking on a Quest.

| # | Check | Result | Driven by | What the probe saw |
|---|---|---|---|---|
| 1 | XR session active: simulated Meta Quest 3 with hand input | **pass** | script (metavr) | XR active True, runtime 'Meta XR Simulator 207.1.0', hands L True R True, input hands; metavr device set exit 0, input sync exit 0/0 |
| 2 | Dock button pressed by a simulated hand | **pass** | script | pressed dock:day with input 'hands' (script); then screen day |
| 3 | Cup grabbed by a simulated hand | **pass** | script | grab of object:cup with input 'hands' (script); picked-up True; released True; the cup moved 0.071 m with the hand |
| 4 | Voice note heard within the first minute of a fresh start (sample day) | **pass** | script | voice note playing 35.9 s after a fresh start (limit 60 s); captions shown True; finished True |
| 5 | Reply on the table survives an app restart and an API restart | **pass** | script | reply 501eebfe-8c18-4f90-8609-d1f6b8ad00b8 placed (new, typed 't'); server has it True (placed True); back on the table after app restart True; API went down True and up True; server still has it True; back after API restart True |
| 6 | Recenter, pause and exit | **pass** | script | recenter: pressed True, event True, yaw -35.5 -> 0 deg; pause and resume True; exit: event True, app ended True |
| 7 | Hand tracking lost mid-press and found again: no ghost press | **pass** | script | right hand lost True and back True; presses during and after: 0 (count 0 -> 0); cancels: 1 |
| 8 | MP4 of the simulator for every app session | **pass** | script (FFmpeg window capture) | 6 of 6 recordings saved |
| 9 | Furniture chosen in the marketplace by a simulated hand is kept across an app restart | **pass** | script | chairs changed from 'charcoal' to 'sage' by hand in the marketplace and kept; server version 4 -> 5; after an app restart the room shows: chairs=tub/sage floor=parquet/natural lamp=drum/brass plant=leafy rug=round/oatmeal table=pedestal/walnut walls=plaster/ivory |

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
- probe state (check1-xr): xrDeviceActive=True, xrDevice=OpenXR Display, openxrRuntime=Meta XR Simulator 207.1.0, hands={"left":true,"right":true}, activeInput=hands, fps=63.0, screen=launch, head=[0.0,1.18,0.0], handsAt={"left":[-0.305,0.872,0.492],"right":[0.295,0.872,0.498]} [acceptance-125054/state-check1-xr.json]

### 2. Dock button pressed by a simulated hand: pass

- probe event (dock press): `press dock:day`, input hands, t 68.651 s, app session 2
- probe event (what the press did): `screen day`, input hands, t 68.654 s, app session 2

### 3. Cup grabbed by a simulated hand: pass

- probe event (grab): `grab object:cup`, input hands, t 45.122 s, app session 2
- probe event (cup picked up): `cup picked-up`, input hands, t 45.123 s, app session 2
- probe state (check3-held): cupHeld=True, grabs=1, lastGrab=object:cup, activeInput=hands, handsAt={"left":[-0.42,0.831,0.358],"right":[0.18,0.831,0.364]} [acceptance-125054/state-check3-held.json]
- probe event (released): `release object:cup`, input hands, t 57.009 s, app session 2
- note: Taken with a palm grab.

### 4. Voice note heard within the first minute of a fresh start (sample day): pass

- probe event (app start (fresh profile)): `start 0.9.0`, input None, t 9.272 s, app session 2
- probe event (launch:sample pressed): `press launch:sample`, input hands, t 34.58 s, app session 2
- probe event (sample room open): `screen room`, input hands, t 34.66 s, app session 2
- probe event (voice note playing): `voice play`, input hands, t 45.139 s, app session 2
- probe event (what started the voice note): `grab object:cup`, input hands, t 45.122 s, app session 2
- probe state (check4-captions): voicePlaying=True, audio=cup:m-maya-voice, captionsOn=True, captions=The bench by the big oak, same as always., presence=recorded, paused=False [acceptance-125054/state-check4-captions.json]
- probe event (voice note finished): `voice finished`, input hands, t 54.552 s, app session 2
- probe-timing (start -> voice play (probe clock)): {"secondsFromStartToVoice":35.9,"limit":60}

### 5. Reply on the table survives an app restart and an API restart: pass

- probe event (launch:continue (first visit)): `press launch:continue`, input hands, t 24.752 s, app session 3
- probe event (live mode (first visit)): `mode live`, input hands, t 24.84 s, app session 3
- probe event (room open (first visit)): `screen room`, input hands, t 24.842 s, app session 3
- probe event (notes on the table before replying): `reply restored:3b49b61f-77d1-4a8e-96ef-9127f70c4eda,39a8de9c-bb02-488f-a09c-7933120e9283,2b77d9c8-0946-4728-86ed-100037f9cf4c,b45a4769-e2c5-46e0-a1a8-848a3d868724,5cf7d8f1-42f3-4b57-bec0-be627acf4396,d4b88319-640b-4df9-a502-605471033bf7,b8ee6dd1-795e-4cfa-9f98-38bc6e31749e,8b98c4d0-7a11-47b6-a28c-fbeffd167629,fe3bb850-7501-4159-ad1c-52759d859259`, input hands, t 25.019 s, app session 3
- probe event (cup:reply pressed): `press cup:reply`, input hands, t 35.946 s, app session 3
- probe event (a character typed): `press key:t`, input hands, t 49.029 s, app session 3
- probe event (Done pressed): `press key:done`, input hands, t 73.517 s, app session 3
- probe event (reply placed on the table): `reply placed:501eebfe-8c18-4f90-8609-d1f6b8ad00b8`, input hands, t 73.709 s, app session 3
- reply (the reply placed in this run): {"replyId":"501eebfe-8c18-4f90-8609-d1f6b8ad00b8","typed":"t","newThisRun":true,"noraRepliesBefore":9}
- api (server after placing): {"replyId":"501eebfe-8c18-4f90-8609-d1f6b8ad00b8","replyStored":true,"byNora":true,"placementStored":true,"placement":{"x":-0.376,"y":0.25,"z":0.35,"yaw":0.0}}
- probe event (launch:continue (after an app restart)): `press launch:continue`, input hands, t 24.112 s, app session 4
- probe event (live mode (after an app restart)): `mode live`, input hands, t 24.174 s, app session 4
- probe event (room open (after an app restart)): `screen room`, input hands, t 24.176 s, app session 4
- probe event (notes restored after an app restart): `reply restored:3b49b61f-77d1-4a8e-96ef-9127f70c4eda,39a8de9c-bb02-488f-a09c-7933120e9283,2b77d9c8-0946-4728-86ed-100037f9cf4c,b45a4769-e2c5-46e0-a1a8-848a3d868724,5cf7d8f1-42f3-4b57-bec0-be627acf4396,d4b88319-640b-4df9-a502-605471033bf7,b8ee6dd1-795e-4cfa-9f98-38bc6e31749e,8b98c4d0-7a11-47b6-a28c-fbeffd167629,fe3bb850-7501-4159-ad1c-52759d859259,501eebfe-8c18-4f90-8609-d1f6b8ad00b8`, input hands, t 24.366 s, app session 4
- service (API and worker restarted (stop.ps1, start.ps1)): {"apiDownAfterStop":true,"apiUpAfterStart":true,"apiPidBefore":57444,"apiPidAfter":62368,"workerPidBefore":61424,"workerPidAfter":52164}
- api (server after its restart): {"replyId":"501eebfe-8c18-4f90-8609-d1f6b8ad00b8","replyStored":true,"byNora":true,"placementStored":true,"placement":{"x":-0.376,"y":0.25,"z":0.35,"yaw":0.0}}
- probe event (launch:continue (after an API restart)): `press launch:continue`, input hands, t 23.697 s, app session 5
- probe event (live mode (after an API restart)): `mode live`, input hands, t 23.78 s, app session 5
- probe event (room open (after an API restart)): `screen room`, input hands, t 23.782 s, app session 5
- probe event (notes restored after an API restart): `reply restored:3b49b61f-77d1-4a8e-96ef-9127f70c4eda,39a8de9c-bb02-488f-a09c-7933120e9283,2b77d9c8-0946-4728-86ed-100037f9cf4c,b45a4769-e2c5-46e0-a1a8-848a3d868724,5cf7d8f1-42f3-4b57-bec0-be627acf4396,d4b88319-640b-4df9-a502-605471033bf7,b8ee6dd1-795e-4cfa-9f98-38bc6e31749e,8b98c4d0-7a11-47b6-a28c-fbeffd167629,fe3bb850-7501-4159-ad1c-52759d859259,501eebfe-8c18-4f90-8609-d1f6b8ad00b8`, input hands, t 23.98 s, app session 5
- note: Nora is signed in by the session file the script wrote (typing the email and password on the spatial keyboard is not automated).

### 6. Recenter, pause and exit: pass

- probe state (check6-turned): head=[-0.146,1.238,-0.321], headForward=[-0.581,0.0,0.814] [acceptance-125054/state-check6-turned.json]
- probe event (dock:recenter pressed): `press dock:recenter`, input hands, t 43.682 s, app session 6
- probe event (recenter): `recenter 1.18`, input hands, t 43.683 s, app session 6
- probe state (check6-recentered): head=[-0.438,1.293,-0.646], headForward=[0.0,0.0,1.0] [acceptance-125054/state-check6-recentered.json]
- probe event (dock:pause pressed): `press dock:pause`, input hands, t 74.126 s, app session 6
- probe event (pause screen): `screen pause`, input hands, t 74.128 s, app session 6
- probe state (check6-paused): paused=True, screen=pause, audio=, voicePlaying=False [acceptance-125054/state-check6-paused.json]
- probe event (pause:resume pressed): `press pause:resume`, input hands, t 99.196 s, app session 6
- probe event (resumed): `pause resume`, input hands, t 99.197 s, app session 6
- probe event (back in the room): `screen room`, input hands, t 99.198 s, app session 6
- probe state (check6-resumed): paused=False, screen=room [acceptance-125054/state-check6-resumed.json]
- probe event (leave confirmation): `screen exit`, input hands, t 155.864 s, app session 6
- probe event (exit): `exit quit`, input hands, t 178.742 s, app session 6
- probe-stopped (the app ended (its probe stopped updating)): {"probeStoppedUpdating":true}

### 7. Hand tracking lost mid-press and found again: no ghost press: pass

- probe state (check7-before): hands={"left":true,"right":true}, activeInput=hands, presses=0, lastPress=, handsAt={"left":[-0.629,0.888,0.254],"right":[-0.029,0.888,0.254]} [acceptance-125054/state-check7-before.json]
- probe-target (was the button held down when tracking was dropped): {"target":"launch:recenter","downBeforeTrackingLoss":true}
- command (right-hand tracking off): `metavr.cmd xrsim input source --hand right --source none`, exit 0: HAND   SOURCE -----  ------ right  none
- probe state (check7-lost): hands={"left":false,"right":false}, activeInput=None, presses=0, lastPress= [acceptance-125054/state-check7-lost.json]
- command (hand tracking back): `metavr.cmd xrsim input sync --source hand`, exit 0: HANDS        SOURCE -----------  ------ left, right  hand
- probe state (check7-after): hands={"left":true,"right":true}, activeInput=hands, presses=0, lastPress= [acceptance-125054/state-check7-after.json]
- probe event (press cancelled by the tracking loss): `cancel launch:recenter`, input hands, t 22.462 s, app session 7
- probe-count (presses counted by the probe): {"pressesBefore":0,"pressesAfter":0,"pressEvents":0,"cancelEvents":1}

### 8. MP4 of the simulator for every app session: pass

- recording (session2-sample): {"label":"session2-sample","file":"docs/native/evidence/2026-11-12/acceptance-125054/session2-sample.mp4","method":"window","bytes":2811520,"durationSec":128.5,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session2-sample.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session2-sample.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-12T12:52:01.7636206+04:00","stoppedAt":"2026-11-12T12:54:13.7648237+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":20.6},"probeAtStop":{"xrDeviceActive":true,"screen":"room","mode":"sample","activeInput":"hands","time":151.73}}
- probe state (app state when session2-sample started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=20.6
- recording (session3-live): {"label":"session3-live","file":"docs/native/evidence/2026-11-12/acceptance-125054/session3-live.mp4","method":"window","bytes":3320832,"durationSec":71.7,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session3-live.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session3-live.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-12T12:54:36.2605844+04:00","stoppedAt":"2026-11-12T12:55:49.0036930+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":15.85},"probeAtStop":{"xrDeviceActive":true,"screen":"room","mode":"live","activeInput":"hands","time":87.6}}
- probe state (app state when session3-live started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=15.85
- recording (session4-live-restarted): {"label":"session4-live-restarted","file":"docs/native/evidence/2026-11-12/acceptance-125054/session4-live-restarted.mp4","method":"window","bytes":607452,"durationSec":12.4,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session4-live-restarted.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session4-live-restarted.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-12T12:56:09.9152920+04:00","stoppedAt":"2026-11-12T12:56:23.2859181+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":15.15},"probeAtStop":{"xrDeviceActive":true,"screen":"room","mode":"live","activeInput":"hands","time":27.32}}
- probe state (app state when session4-live-restarted started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=15.15
- recording (session5-live-api-restarted): {"label":"session5-live-api-restarted","file":"docs/native/evidence/2026-11-12/acceptance-125054/session5-live-api-restarted.mp4","method":"window","bytes":4121322,"durationSec":77.4,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session5-live-api-restarted.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session5-live-api-restarted.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-12T12:57:00.6411631+04:00","stoppedAt":"2026-11-12T12:58:18.9853450+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":15.61},"probeAtStop":{"xrDeviceActive":true,"screen":"room","mode":"live","activeInput":"hands","time":92.66}}
- probe state (app state when session5-live-api-restarted started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=15.61
- recording (session6-live-market-restarted): {"label":"session6-live-market-restarted","file":"docs/native/evidence/2026-11-12/acceptance-125054/session6-live-market-restarted.mp4","method":"window","bytes":7469871,"durationSec":169.2,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session6-live-market-restarted.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session6-live-market-restarted.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-12T12:58:40.9252632+04:00","stoppedAt":"2026-11-12T13:01:31.0919362+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":14.9},"probeAtStop":{"xrDeviceActive":true,"screen":"exit","mode":"live","activeInput":"hands","time":178.69}}
- probe state (app state when session6-live-market-restarted started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=14.9
- recording (session7-tracking): {"label":"session7-tracking","file":"docs/native/evidence/2026-11-12/acceptance-125054/session7-tracking.mp4","method":"window","bytes":580905,"durationSec":20.1,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session7-tracking.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session7-tracking.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-12T13:01:45.6290420+04:00","stoppedAt":"2026-11-12T13:02:06.7311242+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":13.61},"probeAtStop":{"xrDeviceActive":true,"screen":"launch","mode":"none","activeInput":"hands","time":33.82}}
- probe state (app state when session7-tracking started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=13.61
- note: The simulator runtime has no MP4 recorder (metavr xrsim record says its build doesn't include the recording service), so FFmpeg recorded the simulator window: the 3D view as the simulator showed it, plus its side panel.

### 9. Furniture chosen in the marketplace by a simulated hand is kept across an app restart: pass

- api (the room on the server before): {"version":4,"chairs":{"item":"tub","finish":"charcoal"}}
- probe event (dock:market pressed): `press dock:market`, input hands, t 35.762 s, app session 5
- probe event (market:tab-chairs pressed): `press market:tab-chairs`, input hands, t 48.372 s, app session 5
- probe event (market:finish-chairs-sage pressed): `press market:finish-chairs-sage`, input hands, t 64.873 s, app session 5
- probe event (market:keep pressed): `press market:keep`, input hands, t 84.795 s, app session 5
- probe event (the fabric tried in the room (not kept yet)): `decor try chairs=tub/sage`, input hands, t 64.874 s, app session 5
- probe event (kept): `decor kept v5 chairs=tub/sage floor=parquet/natural lamp=drum/brass plant=leafy rug=round/oatmeal table=pedestal/walnut walls=plaster/ivory`, input hands, t 84.828 s, app session 5
- api (the room on the server after Keep): {"version":5,"chairs":{"item":"tub","finish":"sage"},"updatedByNora":true}
- probe event (launch:continue (after an app restart)): `press launch:continue`, input hands, t 23.21 s, app session 6
- probe event (live mode (after an app restart)): `mode live`, input hands, t 23.27 s, app session 6
- probe event (room open (after an app restart)): `screen room`, input hands, t 23.272 s, app session 6
- probe event (furniture shown after the restart): `decor shown v5`, input hands, t 23.305 s, app session 6
- probe state (check9-restarted): screen=room, mode=live, room=Nora + Sam, decor=chairs=tub/sage floor=parquet/natural lamp=drum/brass plant=leafy rug=round/oatmeal table=pedestal/walnut walls=plaster/ivory, decorVersion=5, decorUnsaved=False [acceptance-125054/state-check9-restarted.json]

## Setup

- API and worker running (scripts/native/start.ps1): ok
- Development accounts and rooms, and a voice note from Sam to Nora (seed_dev.py --voice-note): ok
- Signed in as Nora (nora.native@example.com, development account) through POST /v1/auth/login; the token is never written to the evidence: ok
- Meta XR Simulator window found: ok
- Simulator movement speed 0.3 (metavr xrsim input speed): ok
- Hand control: calibrated; device 'body'; fingertip offset [-0.054,0.008,0.167] (the app's right poke interactor (probe pokeAt), in the poke pose)

## Notes

- metavr xrsim record: Error: this XR Simulator runtime does not support native MP4 recording: its build doesn't include the recording service, so `record start`, `record stop` and `record status` keep failing against it; record the simulator window with your OS screen recorder instead The recordings are FFmpeg captures of the simulator window instead.
- For key:done the head was turned -26 degrees during the press and back afterwards: the simulator moves both hands together, and the left index would have come down on key:cancel.
- For dock:pause the head was turned -24 degrees during the press and back afterwards: the simulator moves both hands together, and the left index would have come down on dock:day.

## Files

- `acceptance-125054/seed.json`: seeded ids (development data), 560 bytes, sha256 `0efe88d686dd008d...`
- `acceptance-125054/seat-hands.png`: seated view (fixed camera): the tracked hands from where the head is: skin tone, nails, knuckles, their shadow, 1448835 bytes, sha256 `0b0b06078c3a8ae7...`
- `acceptance-125054/seat-palms.png`: seated view (fixed camera): the tracked hands from the other side: palms, creases, finger pads, 377397 bytes, sha256 `ce7e692d08682850...`
- `acceptance-125054/seat-hands-pinch.png`: seated view (fixed camera): the tracked hands pinching, 1464442 bytes, sha256 `b46d5a21c43099af...`
- `acceptance-125054/events-session1.jsonl`: probe events, app session 1, 487 bytes, sha256 `cc2167ecd581018d...`
- `acceptance-125054/state-check1-xr.json`: probe state: check1-xr, 3319 bytes, sha256 `0e5422810eaf567c...`
- `acceptance-125054/sim-check1-session.png`: simulator view: check1-session, 1004740 bytes, sha256 `6c2af18a56aecde7...`
- `acceptance-125054/seat-launch.png`: seated view (fixed camera): the launch screen in the room (reference 10), 1105609 bytes, sha256 `31ddf4dfd5fb3a82...`
- `acceptance-125054/seat-room-cue.png`: seated view (fixed camera): the sample room with the pick-up cue (references 02 and 10), 1342647 bytes, sha256 `f511bb7200e19544...`
- `acceptance-125054/state-check3-held.json`: probe state: check3-held, 5918 bytes, sha256 `a32059d6f88c0834...`
- `acceptance-125054/sim-check3-cup-held.png`: simulator view: check3-cup-held, 1023053 bytes, sha256 `8e06b2391e3f9251...`
- `acceptance-125054/state-check4-captions.json`: probe state: check4-captions, 5944 bytes, sha256 `9bb25df2e602cda4...`
- `acceptance-125054/sim-check4-captions.png`: simulator view: check4-captions, 1023041 bytes, sha256 `1bb71e30ae8776a6...`
- `acceptance-125054/seat-message.png`: seated view (fixed camera): the cup lifted, its message card playing with captions (reference 03), 1304638 bytes, sha256 `7b52c75e186b1239...`
- `acceptance-125054/seat-room-partner.png`: seated view (fixed camera): after the message: the sample's partner as labelled simulated presence (reference 02), 1305165 bytes, sha256 `0c78b401a5da2272...`
- `acceptance-125054/sim-check2-dock.png`: simulator view: check2-dock, 985315 bytes, sha256 `30ef74e03ccc52e9...`
- `acceptance-125054/seat-tour-day.png`: seated view (fixed camera): Our day (reference 05), 1326561 bytes, sha256 `bd6656462839a9fd...`
- `acceptance-125054/seat-tour-share.png`: seated view (fixed camera): Share: who receives it, and the AI permission (reference 06), 1180035 bytes, sha256 `332e32b06a6f3c06...`
- `acceptance-125054/seat-tour-together.png`: seated view (fixed camera): Together: places, times and the next overlap (reference 08), 1206140 bytes, sha256 `0af8dcad5e90db84...`
- `acceptance-125054/seat-tour-keepsakes.png`: seated view (fixed camera): Keepsakes (reference 09), 1166057 bytes, sha256 `e12da247443dc96a...`
- `acceptance-125054/seat-tour-shelf.png`: seated view (fixed camera): the keepsake shelf in the alcove (reference 09), 907290 bytes, sha256 `b769f37c1caadb56...`
- `acceptance-125054/seat-tour-avatar.png`: seated view (fixed camera): the avatar preview, front, three-quarter and side (references 01 and 13), 1208670 bytes, sha256 `2567ca86c47a4b07...`
- `acceptance-125054/seat-tour-settings.png`: seated view (fixed camera): Settings: captions, text size, sound, recenter (reference 12), 1154304 bytes, sha256 `6b1b251108269497...`
- `acceptance-125054/seat-tour-pause.png`: seated view (fixed camera): Paused (reference 12), 1202907 bytes, sha256 `470411f0b960f89c...`
- `acceptance-125054/seat-tour-story.png`: seated view (fixed camera): the story on the screen beside the partner (references 04 and 05), 1271386 bytes, sha256 `b0a6de0ea184a4f1...`
- `acceptance-125054/seat-tour-rooms.png`: seated view (fixed camera): the room selector (reference 07), 1344294 bytes, sha256 `72e0ff95e517f7ff...`
- `acceptance-125054/seat-tour-circle.png`: seated view (fixed camera): the family circle room (reference 07), 1272630 bytes, sha256 `570035dc89e9668f...`
- `acceptance-125054/seat-tour-direct.png`: seated view (fixed camera): back in the direct room (reference 02), 1395791 bytes, sha256 `3704941930d4ef45...`
- `acceptance-125054/seat-tour-partner.png`: seated view (fixed camera): the partner's face, close (natural proportions, eyes, expression: reference 01), 1063670 bytes, sha256 `3310eee64e67cebb...`
- `acceptance-125054/seat-tour-left.png`: seated view (fixed camera): the room to the left: window light and shadows on the wall and floor, 1313124 bytes, sha256 `b9a811d08b79564f...`
- `acceptance-125054/seat-tour-right.png`: seated view (fixed camera): the room to the right: the lamp and side table, 1153468 bytes, sha256 `87e76778d9760702...`
- `acceptance-125054/seat-tour-reply.png`: seated view (fixed camera): replying on the keyboard at the table edge (reference 03), 1301708 bytes, sha256 `9d7e401a4273ec86...`
- `acceptance-125054/seat-tour-market.png`: seated view (fixed camera): Marketplace: ready-made looks, 1083400 bytes, sha256 `cb4de71851bcc921...`
- `acceptance-125054/seat-tour-market-table.png`: seated view (fixed camera): Marketplace: tables and their finishes, 1115978 bytes, sha256 `5536f71c84091a91...`
- `acceptance-125054/seat-tour-market-chairs.png`: seated view (fixed camera): Marketplace: chair fabrics, 1115595 bytes, sha256 `738403063f792295...`
- `acceptance-125054/seat-tour-market-lamp.png`: seated view (fixed camera): Marketplace: lamps, 1094387 bytes, sha256 `e9db21076662f0d6...`
- `acceptance-125054/seat-tour-market-walls.png`: seated view (fixed camera): Marketplace: wall colours, 1068430 bytes, sha256 `5119618458eefcfe...`
- `acceptance-125054/seat-tour-look-coastal.png`: seated view (fixed camera): the room in the Coastal oak look, 1492797 bytes, sha256 `934b515e4e8cdb62...`
- `acceptance-125054/seat-tour-look-coastal-left.png`: seated view (fixed camera): Coastal oak: the wall, rug and plant to the left, 1367898 bytes, sha256 `affd8ef4e88b7b91...`
- `acceptance-125054/seat-tour-look-coastal-right.png`: seated view (fixed camera): Coastal oak: the tripod lamp to the right, 1225974 bytes, sha256 `8bbff56836de4b26...`
- `acceptance-125054/seat-tour-look-evening.png`: seated view (fixed camera): the room in the Evening study look, 1376230 bytes, sha256 `c10bae7c56d9a9eb...`
- `acceptance-125054/seat-tour-look-evening-right.png`: seated view (fixed camera): Evening study: the globe lamp to the right, 1140702 bytes, sha256 `48d01633b21fb015...`
- `acceptance-125054/seat-tour-hands-panel.png`: seated view (fixed camera): Your hands: the skin tone choice, 1287569 bytes, sha256 `a3ffbd32616b2086...`
- `acceptance-125054/seat-tour-direct-again.png`: seated view (fixed camera): back in the room as it is kept, 1395595 bytes, sha256 `a2c38914a11e8d0a...`
- `acceptance-125054/session2-sample.mp4`: simulator window recording (FFmpeg): session2-sample, 2811520 bytes, sha256 `09afb035070ecf18...`
- `acceptance-125054/events-session2.jsonl`: probe events, app session 2, 7922 bytes, sha256 `0f99adbd8a8a784e...`
- `acceptance-125054/sim-check5-placed.png`: simulator view: check5-placed, 925806 bytes, sha256 `478b5fcd9c8ff5d3...`
- `acceptance-125054/seat-live-room.png`: seated view (fixed camera): the live room: the reply standing on the table as a folded card (reference 03), 1235688 bytes, sha256 `7d174ca1f94ed8cb...`
- `acceptance-125054/session3-live.mp4`: simulator window recording (FFmpeg): session3-live, 3320832 bytes, sha256 `cbad9e19f5275bcc...`
- `acceptance-125054/events-session3.jsonl`: probe events, app session 3, 1607 bytes, sha256 `f2b1f25b0648431a...`
- `acceptance-125054/sim-check5-after-app-restart.png`: simulator view: check5-after-app-restart, 980301 bytes, sha256 `3385f67a3c9c6c94...`
- `acceptance-125054/session4-live-restarted.mp4`: simulator window recording (FFmpeg): session4-live-restarted, 607452 bytes, sha256 `6787ae1f4397a16c...`
- `acceptance-125054/events-session4.jsonl`: probe events, app session 4, 1122 bytes, sha256 `dfd7bb1582d6471a...`
- `acceptance-125054/sim-check5-after-api-restart.png`: simulator view: check5-after-api-restart, 980431 bytes, sha256 `dcb9d3f5b0fcc285...`
- `acceptance-125054/sim-check9-trying.png`: simulator view: check9-trying, 914450 bytes, sha256 `fbe60fad3681bd3a...`
- `acceptance-125054/seat-market-trying.png`: seated view (fixed camera): the marketplace open, the chairs tried in sage, 968277 bytes, sha256 `74372b0105f3a0e5...`
- `acceptance-125054/seat-market-kept.png`: seated view (fixed camera): the live room with the chairs in sage, kept from the marketplace, 1218816 bytes, sha256 `354c3e46d3b4700d...`
- `acceptance-125054/session5-live-api-restarted.mp4`: simulator window recording (FFmpeg): session5-live-api-restarted, 4121322 bytes, sha256 `2afa2fd0989c85f5...`
- `acceptance-125054/events-session5.jsonl`: probe events, app session 5, 2709 bytes, sha256 `f6d00398731d99a8...`
- `acceptance-125054/state-check9-restarted.json`: probe state: check9-restarted, 4680 bytes, sha256 `5a6295d48de77dd2...`
- `acceptance-125054/sim-check9-after-app-restart.png`: simulator view: check9-after-app-restart, 985189 bytes, sha256 `73023166b80c6827...`
- `acceptance-125054/state-check6-turned.json`: probe state: check6-turned, 4733 bytes, sha256 `1904cbba0eae79a3...`
- `acceptance-125054/state-check6-recentered.json`: probe state: check6-recentered, 4729 bytes, sha256 `747b7603ce7a43f9...`
- `acceptance-125054/state-check6-paused.json`: probe state: check6-paused, 3823 bytes, sha256 `30bc0b1001363b7b...`
- `acceptance-125054/sim-check6-paused.png`: simulator view: check6-paused, 955016 bytes, sha256 `58b47a8972b50df8...`
- `acceptance-125054/state-check6-resumed.json`: probe state: check6-resumed, 4730 bytes, sha256 `515154e79603d2ea...`
- `acceptance-125054/session6-live-market-restarted.mp4`: simulator window recording (FFmpeg): session6-live-market-restarted, 7469871 bytes, sha256 `cb53f2d7da900330...`
- `acceptance-125054/events-session6.jsonl`: probe events, app session 6, 2870 bytes, sha256 `ec6d45ce5694fb73...`
- `acceptance-125054/state-check7-before.json`: probe state: check7-before, 3634 bytes, sha256 `81653f72bac5aa3e...`
- `acceptance-125054/state-check7-lost.json`: probe state: check7-lost, 3535 bytes, sha256 `372a07dcac4759ae...`
- `acceptance-125054/state-check7-after.json`: probe state: check7-after, 3635 bytes, sha256 `ffc63d245eee5300...`
- `acceptance-125054/sim-check7-after.png`: simulator view: check7-after, 955698 bytes, sha256 `d45f2f2b55d71ba3...`
- `acceptance-125054/session7-tracking.mp4`: simulator window recording (FFmpeg): session7-tracking, 580905 bytes, sha256 `05718f79e9c97607...`
- `acceptance-125054/events-session7.jsonl`: probe events, app session 7, 377 bytes, sha256 `fe23674cbc6dfa16...`
