# Native acceptance run acceptance-033533

13 November 2026 03:35, commit `8c4d854` (with uncommitted changes). Unity Play mode (editor) in Meta XR Simulator, profile `acceptance-sampleonly`, run as a build with no server (`-SampleOnly`): the sample day only, no API.

Evidence kind: **simulator-tested**. Meta XR Simulator with a simulated Meta Quest 3 and simulated hands. It is not a headset result, and says nothing about frame rate, comfort, spatial audio or real hand tracking on a Quest.

| # | Check | Result | Driven by | What the probe saw |
|---|---|---|---|---|
| 1 | XR session active: simulated Meta Quest 3 with hand input | **pass** | script (metavr) | XR active True, runtime 'Meta XR Simulator 207.1.0', hands L True R True, input hands; metavr device set exit 0, input sync exit 0/0; build with no server: sample day offered True, account buttons 0 |
| 2 | Dock button pressed by a simulated hand | **pass** | script | pressed dock:day with input 'hands' (script); then screen day |
| 3 | Cup grabbed by a simulated hand | **pass** | script | grab of object:cup with input 'hands' (script); picked-up True; released True; the cup moved 0.083 m with the hand |
| 4 | Voice note heard within the first minute of a fresh start (sample day) | **pass** | script | voice note playing 32.2 s after a fresh start (limit 60 s); captions shown True; finished True |
| 5 | Reply on the table survives an app restart and an API restart | **skipped** |  | needs the SameSky service: not part of a build with no server (-SampleOnly) |
| 6 | Recenter, pause and exit | **pass** | script | recenter: pressed True, event True, yaw -32.8 -> 0 deg; pause and resume True; exit: event True, app ended True |
| 7 | Hand tracking lost mid-press and found again: no ghost press | **pass** | script | right hand lost True and back True; presses during and after: 0 (count 0 -> 0); cancels: 1 |
| 8 | MP4 of the simulator for every app session | **pass** | script (FFmpeg window capture) | 2 of 2 recordings saved |
| 9 | Furniture chosen in the marketplace by a simulated hand is kept across an app restart | **skipped** |  | needs the SameSky service: not part of a build with no server (-SampleOnly) |

Execution order: Check1, FirstMinute, Check2, RenderTour, Check6, Check7. The first minute is timed on a fresh profile, so checks 3 and 4 run before check 2.

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
- probe state (check1-xr): xrDeviceActive=True, xrDevice=OpenXR Display, openxrRuntime=Meta XR Simulator 207.1.0, hands={"left":true,"right":true}, activeInput=hands, fps=63.0, screen=launch, head=[0.0,1.18,0.0], handsAt={"left":[-0.295,0.88,0.503],"right":[0.305,0.88,0.497]} [acceptance-033533/state-check1-xr.json]
- log (Unity log: OpenXR runtime used by Play mode): [SameSky:sim] play mode; XR_RUNTIME_JSON=%USERPROFILE%\SameSkyTools\MetaXRSimulator\PFiles\MetaXRSimulator\v207.1\meta_openxr_simulator.json
- probe-welcome (what can be pressed on the welcome panel of a build with no server): {"sampleOnly":true,"pressable":["launch:captions","launch:exit","launch:recenter","launch:sample","launch:sound"],"accountButtons":[]}

### 2. Dock button pressed by a simulated hand: pass

- probe event (dock press): `press dock:day`, input hands, t 64.932 s, app session 2
- probe event (what the press did): `screen day`, input hands, t 64.934 s, app session 2

### 3. Cup grabbed by a simulated hand: pass

- probe event (grab): `grab object:cup`, input hands, t 41.557 s, app session 2
- probe event (cup picked up): `cup picked-up`, input hands, t 41.558 s, app session 2
- probe state (check3-held): cupHeld=True, grabs=1, lastGrab=object:cup, activeInput=hands, handsAt={"left":[-0.422,0.83,0.374],"right":[0.179,0.83,0.368]} [acceptance-033533/state-check3-held.json]
- probe event (released): `release object:cup`, input hands, t 53.388 s, app session 2
- note: Taken with a palm grab.

### 4. Voice note heard within the first minute of a fresh start (sample day): pass

- probe event (app start (fresh profile)): `start 0.9.0`, input None, t 9.364 s, app session 2
- probe event (launch:sample pressed): `press launch:sample`, input hands, t 32.014 s, app session 2
- probe event (sample room open): `screen room`, input hands, t 32.123 s, app session 2
- probe event (voice note playing): `voice play`, input hands, t 41.576 s, app session 2
- probe event (what started the voice note): `grab object:cup`, input hands, t 41.557 s, app session 2
- probe state (check4-captions): voicePlaying=True, audio=cup:m-maya-voice, captionsOn=True, captions=The bench by the big oak, same as always., presence=recorded, paused=False [acceptance-033533/state-check4-captions.json]
- probe event (voice note finished): `voice finished`, input hands, t 51.025 s, app session 2
- probe-timing (start -> voice play (probe clock)): {"secondsFromStartToVoice":32.2,"limit":60}

### 5. Reply on the table survives an app restart and an API restart: skipped


### 6. Recenter, pause and exit: pass

- probe state (check6-turned): head=[-0.281,1.376,-0.565], headForward=[-0.541,0.0,0.841] [acceptance-033533/state-check6-turned.json]
- probe event (dock:recenter pressed): `press dock:recenter`, input hands, t 167.671 s, app session 2
- probe event (recenter): `recenter 1.18`, input hands, t 167.672 s, app session 2
- probe state (check6-recentered): head=[-0.567,1.426,-0.9], headForward=[0.0,0.0,1.0] [acceptance-033533/state-check6-recentered.json]
- probe event (dock:pause pressed): `press dock:pause`, input hands, t 200.177 s, app session 2
- probe event (pause screen): `screen pause`, input hands, t 200.179 s, app session 2
- probe state (check6-paused): paused=True, screen=pause, audio=, voicePlaying=False [acceptance-033533/state-check6-paused.json]
- probe event (pause:resume pressed): `press pause:resume`, input hands, t 226.455 s, app session 2
- probe event (resumed): `pause resume`, input hands, t 226.455 s, app session 2
- probe event (back in the room): `screen room`, input hands, t 226.456 s, app session 2
- probe state (check6-resumed): paused=False, screen=room [acceptance-033533/state-check6-resumed.json]
- probe event (leave confirmation): `screen exit`, input hands, t 286.133 s, app session 2
- probe event (exit): `exit quit`, input hands, t 311.776 s, app session 2
- probe-stopped (the app ended (its probe stopped updating)): {"probeStoppedUpdating":true}

### 7. Hand tracking lost mid-press and found again: no ghost press: pass

- probe state (check7-before): hands={"left":true,"right":true}, activeInput=hands, presses=0, lastPress=, handsAt={"left":[-0.627,0.884,0.273],"right":[-0.027,0.884,0.267]} [acceptance-033533/state-check7-before.json]
- probe-target (was the button held down when tracking was dropped): {"target":"launch:recenter","downBeforeTrackingLoss":true}
- command (right-hand tracking off): `metavr.cmd xrsim input source --hand right --source none`, exit 0: HAND   SOURCE -----  ------ right  none
- probe state (check7-lost): hands={"left":false,"right":false}, activeInput=None, presses=0, lastPress= [acceptance-033533/state-check7-lost.json]
- command (hand tracking back): `metavr.cmd xrsim input sync --source hand`, exit 0: HANDS        SOURCE -----------  ------ left, right  hand
- probe state (check7-after): hands={"left":true,"right":true}, activeInput=hands, presses=0, lastPress= [acceptance-033533/state-check7-after.json]
- probe event (press cancelled by the tracking loss): `cancel launch:recenter`, input hands, t 28.083 s, app session 3
- probe-count (presses counted by the probe): {"pressesBefore":0,"pressesAfter":0,"pressEvents":0,"cancelEvents":1}

### 8. MP4 of the simulator for every app session: pass

- recording (session2-sample): {"label":"session2-sample","file":"docs/native/evidence/2026-11-13/acceptance-033533/session2-sample.mp4","method":"window","bytes":10141565,"durationSec":295.2,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session2-sample.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session2-sample.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-13T03:37:13.8410474+04:00","stoppedAt":"2026-11-13T03:42:10.8984692+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":20.37},"probeAtStop":{"xrDeviceActive":true,"screen":"exit","mode":"sample","activeInput":"hands","time":311.59}}
- probe state (app state when session2-sample started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=20.37
- recording (session3-tracking): {"label":"session3-tracking","file":"docs/native/evidence/2026-11-13/acceptance-033533/session3-tracking.mp4","method":"window","bytes":554937,"durationSec":18.6,"ok":true,"startCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session3-tracking.mp4","exit":0,"timedOut":false,"output":""},"stopCommand":{"command":"ffmpeg -f gdigrab (simulator window, 1582x1190 at 15 fps) -c:v libx264 session3-tracking.mp4","exit":0,"timedOut":false,"output":""},"metavrRefused":null,"startedAt":"2026-11-13T03:42:31.8254918+04:00","stoppedAt":"2026-11-13T03:42:51.3648451+04:00","probeAtStart":{"xrDeviceActive":true,"openxrRuntime":"Meta XR Simulator 207.1.0","screen":"launch","mode":"none","activeInput":"hands","hands":{"left":true,"right":true},"time":19.96},"probeAtStop":{"xrDeviceActive":true,"screen":"launch","mode":"none","activeInput":"hands","time":38.31}}
- probe state (app state when session3-tracking started): xrDeviceActive=True, openxrRuntime=Meta XR Simulator 207.1.0, screen=launch, mode=none, activeInput=hands, hands={"left":true,"right":true}, time=19.96
- note: The simulator runtime has no MP4 recorder (metavr xrsim record says its build doesn't include the recording service), so FFmpeg recorded the simulator window: the 3D view as the simulator showed it, plus its side panel.

### 9. Furniture chosen in the marketplace by a simulated hand is kept across an app restart: skipped


## Setup

- No API or worker: the app runs as a build with no server (-SampleOnly): ok
- Meta XR Simulator window found: ok
- Simulator movement speed 0.3 (metavr xrsim input speed): ok
- Hand control: calibrated; device 'body'; fingertip offset [-0.056,0.008,0.167] (the app's right poke interactor (probe pokeAt), in the poke pose)

## Notes

- metavr xrsim record: Error: this XR Simulator runtime does not support native MP4 recording: its build doesn't include the recording service, so `record start`, `record stop` and `record status` keep failing against it; record the simulator window with your OS screen recorder instead The recordings are FFmpeg captures of the simulator window instead.
- For dock:pause the head was turned -24 degrees during the press and back afterwards: the simulator moves both hands together, and the left index would have come down on dock:day.

## Files

- `acceptance-033533/seat-hands.png`: seated view (fixed camera): the tracked hands from where the head is: skin tone, nails, knuckles, their shadow, 1452641 bytes, sha256 `cb40703f28c43f31...`
- `acceptance-033533/seat-palms.png`: seated view (fixed camera): the tracked hands from the other side: palms, creases, finger pads, 383195 bytes, sha256 `8a7d254c28bfd344...`
- `acceptance-033533/seat-hands-pinch.png`: seated view (fixed camera): the tracked hands pinching, 1462475 bytes, sha256 `5cae6d3fa379fca8...`
- `acceptance-033533/events-session1.jsonl`: probe events, app session 1, 488 bytes, sha256 `1c19d0ad8c3ad26f...`
- `acceptance-033533/state-check1-xr.json`: probe state: check1-xr, 2746 bytes, sha256 `cf2d7f32064560fe...`
- `acceptance-033533/sim-check1-session.png`: simulator view: check1-session, 987195 bytes, sha256 `6e080b36135a959d...`
- `acceptance-033533/seat-launch.png`: seated view (fixed camera): the launch screen in the room (reference 10), 1126917 bytes, sha256 `4aa433bcbfc406a9...`
- `acceptance-033533/seat-room-cue.png`: seated view (fixed camera): the sample room with the pick-up cue (references 02 and 10), 1333563 bytes, sha256 `b5d858cdb0ce5a77...`
- `acceptance-033533/state-check3-held.json`: probe state: check3-held, 5935 bytes, sha256 `7ea06bfa02148b13...`
- `acceptance-033533/sim-check3-cup-held.png`: simulator view: check3-cup-held, 988276 bytes, sha256 `02f6d2cbcb7b3781...`
- `acceptance-033533/state-check4-captions.json`: probe state: check4-captions, 5958 bytes, sha256 `bc82f2e0a74a0af7...`
- `acceptance-033533/sim-check4-captions.png`: simulator view: check4-captions, 988298 bytes, sha256 `930cc45e618093d2...`
- `acceptance-033533/seat-message.png`: seated view (fixed camera): the cup lifted, its message card playing with captions (reference 03), 1306689 bytes, sha256 `09f635ec43d08fd0...`
- `acceptance-033533/seat-room-partner.png`: seated view (fixed camera): after the message: the sample's partner as labelled simulated presence (reference 02), 1314941 bytes, sha256 `2b64f15093142fd7...`
- `acceptance-033533/sim-check2-dock.png`: simulator view: check2-dock, 971720 bytes, sha256 `e0f187c596bc0bb7...`
- `acceptance-033533/seat-tour-day.png`: seated view (fixed camera): Our day (reference 05), 1334148 bytes, sha256 `3549a56a77bb0eee...`
- `acceptance-033533/seat-tour-share.png`: seated view (fixed camera): Share: who receives it, and the AI permission (reference 06), 1186013 bytes, sha256 `a85eea422c37e1d5...`
- `acceptance-033533/seat-tour-together.png`: seated view (fixed camera): Together: places, times and the next overlap (reference 08), 1214557 bytes, sha256 `26e2dbdd54798789...`
- `acceptance-033533/seat-tour-keepsakes.png`: seated view (fixed camera): Keepsakes (reference 09), 1168969 bytes, sha256 `2cdb0d8317e00d0b...`
- `acceptance-033533/seat-tour-shelf.png`: seated view (fixed camera): the keepsake shelf in the alcove (reference 09), 908391 bytes, sha256 `4ca7f19b09180451...`
- `acceptance-033533/seat-tour-avatar.png`: seated view (fixed camera): the avatar preview, front, three-quarter and side (references 01 and 13), 1209513 bytes, sha256 `67924ee9da60fc61...`
- `acceptance-033533/seat-tour-settings.png`: seated view (fixed camera): Settings: captions, text size, sound, recenter (reference 12), 1150779 bytes, sha256 `70fc8a37f32f62ce...`
- `acceptance-033533/seat-tour-pause.png`: seated view (fixed camera): Paused (reference 12), 1205689 bytes, sha256 `952393bf4a41522b...`
- `acceptance-033533/seat-tour-story.png`: seated view (fixed camera): the story on the screen beside the partner (references 04 and 05), 1216626 bytes, sha256 `938c6073884b2f45...`
- `acceptance-033533/seat-tour-rooms.png`: seated view (fixed camera): the room selector (reference 07), 1343217 bytes, sha256 `74dc289c7a01888e...`
- `acceptance-033533/seat-tour-circle.png`: seated view (fixed camera): the family circle room (reference 07), 1273260 bytes, sha256 `ac7038cb95b22568...`
- `acceptance-033533/seat-tour-direct.png`: seated view (fixed camera): back in the direct room (reference 02), 1395887 bytes, sha256 `4b9494c50fad0d80...`
- `acceptance-033533/seat-tour-partner.png`: seated view (fixed camera): the partner's face, close (natural proportions, eyes, expression: reference 01), 1064305 bytes, sha256 `d47ae4b1ffbe6894...`
- `acceptance-033533/seat-tour-left.png`: seated view (fixed camera): the room to the left: window light and shadows on the wall and floor, 1316576 bytes, sha256 `2445b6cc91af1751...`
- `acceptance-033533/seat-tour-right.png`: seated view (fixed camera): the room to the right: the lamp and side table, 1153411 bytes, sha256 `d2851d2713a0065b...`
- `acceptance-033533/seat-tour-reply.png`: seated view (fixed camera): replying on the keyboard at the table edge (reference 03), 1305936 bytes, sha256 `8a39c355b72fb1ef...`
- `acceptance-033533/seat-tour-market.png`: seated view (fixed camera): Marketplace: ready-made looks, 1081693 bytes, sha256 `eb13faf1c0f8d40c...`
- `acceptance-033533/seat-tour-market-table.png`: seated view (fixed camera): Marketplace: tables and their finishes, 1115810 bytes, sha256 `90aa9adf86613522...`
- `acceptance-033533/seat-tour-market-chairs.png`: seated view (fixed camera): Marketplace: chair fabrics, 1116228 bytes, sha256 `0dea424e7b1e7f97...`
- `acceptance-033533/seat-tour-market-lamp.png`: seated view (fixed camera): Marketplace: lamps, 1095315 bytes, sha256 `8242187546510223...`
- `acceptance-033533/seat-tour-market-walls.png`: seated view (fixed camera): Marketplace: wall colours, 1068751 bytes, sha256 `b2aa8b22e81006a3...`
- `acceptance-033533/seat-tour-look-coastal.png`: seated view (fixed camera): the room in the Coastal oak look, 1493241 bytes, sha256 `ae995f6ddf25048d...`
- `acceptance-033533/seat-tour-look-coastal-left.png`: seated view (fixed camera): Coastal oak: the wall, rug and plant to the left, 1372773 bytes, sha256 `3b32db2b1618751c...`
- `acceptance-033533/seat-tour-look-coastal-right.png`: seated view (fixed camera): Coastal oak: the tripod lamp to the right, 1224549 bytes, sha256 `eab46a09dfebeb1b...`
- `acceptance-033533/seat-tour-look-evening.png`: seated view (fixed camera): the room in the Evening study look, 1375579 bytes, sha256 `8f7477244ccb32be...`
- `acceptance-033533/seat-tour-look-evening-right.png`: seated view (fixed camera): Evening study: the globe lamp to the right, 1140772 bytes, sha256 `677210a4d9bba1de...`
- `acceptance-033533/seat-tour-hands-panel.png`: seated view (fixed camera): Your hands: the skin tone choice, 1289317 bytes, sha256 `5993510165c48ace...`
- `acceptance-033533/seat-tour-direct-again.png`: seated view (fixed camera): back in the room as it is kept, 1395519 bytes, sha256 `d75de1b37d731df9...`
- `acceptance-033533/state-check6-turned.json`: probe state: check6-turned, 5368 bytes, sha256 `16716623ec0880fe...`
- `acceptance-033533/state-check6-recentered.json`: probe state: check6-recentered, 5367 bytes, sha256 `ff98f3b0eec30893...`
- `acceptance-033533/state-check6-paused.json`: probe state: check6-paused, 4508 bytes, sha256 `6d870499de345adf...`
- `acceptance-033533/sim-check6-paused.png`: simulator view: check6-paused, 900287 bytes, sha256 `bd676e6dd8601c93...`
- `acceptance-033533/state-check6-resumed.json`: probe state: check6-resumed, 5370 bytes, sha256 `b2faed6213a68523...`
- `acceptance-033533/session2-sample.mp4`: simulator window recording (FFmpeg): session2-sample, 10141565 bytes, sha256 `81cd3fbb4ae21b66...`
- `acceptance-033533/events-session2.jsonl`: probe events, app session 2, 9324 bytes, sha256 `676e1cee67085d82...`
- `acceptance-033533/state-check7-before.json`: probe state: check7-before, 2768 bytes, sha256 `b6cad94f727ed922...`
- `acceptance-033533/state-check7-lost.json`: probe state: check7-lost, 2670 bytes, sha256 `7cf277d2677b9aa0...`
- `acceptance-033533/state-check7-after.json`: probe state: check7-after, 2770 bytes, sha256 `d3147defecba1040...`
- `acceptance-033533/sim-check7-after.png`: simulator view: check7-after, 968108 bytes, sha256 `cbfe578b80b69685...`
- `acceptance-033533/session3-tracking.mp4`: simulator window recording (FFmpeg): session3-tracking, 554937 bytes, sha256 `cd3c19a14eb41724...`
- `acceptance-033533/events-session3.jsonl`: probe events, app session 3, 377 bytes, sha256 `7a4cbf348caca8b4...`
