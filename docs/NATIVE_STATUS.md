# Native Quest app: status and evidence

Updated 13 November 2026. This page covers the native Unity client in `native/quest/`, built for the brief
`SameSky_Native_Quest_Local_Simulator_AI_Instructions.md` and, for its look, the visual reference pack
(`SameSky_Visual_Reference_Guide.md`). Since the merge of that work it has gained hands drawn with skin, real
photographs and footage in the sample day, and a furniture marketplace: see
[the section on that round](#hands-real-sample-media-and-a-furniture-marketplace-12-november).

**Rule:** every row names its own evidence. The kinds of evidence never stand in for each other:

- **Implemented**: the code exists.
- **Compiled**: Unity built it.
- **Simulator-tested**: run in Meta XR Simulator, checked by the app's probe.
- **Real-service-tested**: run against the real local API and worker.
- **APK-built**, **headset-installed**, **hardware-measured**: as they say.

A browser or IWER result says nothing about the native client.

## Environment used

| Item | Value |
|---|---|
| Host | Windows 11 Home 10.0.26300 x64; Intel Core Ultra 9 275HX (24 cores); 15.4 GB RAM; NVIDIA GeForce RTX 5060 Laptop GPU and Intel Graphics |
| Unity | 6000.3.28f1 (Unity 6.3 LTS), Personal licence. Installed per user (`%USERPROFILE%\Unity\Hub\Editor\6000.3.28f1`) from Unity's MD5-verified installers, because Hub's own install needed an admin approval that couldn't be given at the time |
| Android tools | Inside Unity's Android module: OpenJDK 17.0.18, SDK platforms 34–37, build-tools 36.0.0, platform-tools, command-line tools 16.0, NDK r27c (27.2.12479018), CMake 3.22.1 (Unity 6's Android build requires it; installed with `sdkmanager`). Unpacked by `scripts/native/install_unity_user.ps1`. The first Gradle run downloads its dependencies; one download failed with a TLS "tag mismatch" and succeeded on retry |
| Meta XR SDK | Core, Interaction and Interaction OVR 209.0.0, SHA-1 verified (`native/quest/packages.lock.txt`) |
| Other packages | OpenXR 1.19.0, XR Hands 1.10.0, glTFast 6.21.0, URP 17.3.0, Input System 1.20.1 (`Packages/packages-lock.json`) |
| Meta XR Simulator | 207.1 (runtime 207.1.0.43.123), signature-checked (Meta Platforms, Inc.). Unpacked per user with `scripts/native/install_xrsim_user.ps1`. Apps select it per process with `XR_RUNTIME_JSON`; the system OpenXR runtime is unchanged |
| Meta VR CLI | `metavr` 1.10.0 |
| Recording | FFmpeg 9.0.2 (the copy the API already uses, from `imageio-ffmpeg`): simulator 207.1's runtime has no MP4 recorder |
| Local services | API and worker from `scripts/native/start.ps1` on the isolated `api/data/native-dev` data |

## Acceptance run in Meta XR Simulator

`scripts/native/acceptance.ps1 -NonInteractive -Renders`, Unity Play mode, simulated **Meta Quest 3** with
**simulated hands**. Every gesture was made by the script through the simulator's own keyboard controls. Each one
reached the app as OpenXR hand tracking through Meta's Interaction SDK, and each check passed only on the app's
probe events.

**Latest run, on the code as it is committed:**
[`docs/native/evidence/2026-11-13/acceptance-035823.md`](native/evidence/2026-11-13/acceptance-035823.md) and `.json`,
13 November 2026, **9 of 9 passed**, with probe events, state snapshots, simulator screenshots and one MP4 per app
session in the folder of the same name. It was run after the app learned to run as a build with no server (further
down), to show that the app with a server still behaves as before. Earlier the same day, after the two story-screen
fixes found while filming the demo ([demo/README.md](demo/README.md)):
[`acceptance-022557`](native/evidence/2026-11-13/acceptance-022557.md), 9 of 9, with the render tour (the fixed
seated renders of the app as it is now are in its folder).
The run before those, after the hands, real media and marketplace round:
[`acceptance-125054`](native/evidence/2026-11-12/acceptance-125054.md) (12 November, 9 of 9; that round's renders
and contact sheet come from it). Earlier full passes of the first eight checks:
[`acceptance-065004`](native/evidence/2026-11-12/acceptance-065004.md) (12 November, after the visual pass) and
[`acceptance-210509`](native/evidence/2026-11-11/acceptance-210509.md) (11 November, before it).

| # | Check | Result | What the probe recorded (run 035823) |
|---|---|---|---|
| 1 | XR session: simulated Meta Quest 3, hand input | **pass** | OpenXR runtime "Meta XR Simulator 207.1.0", both hands tracked, active input hands |
| 2 | A dock button pressed by a simulated hand | **pass** | `press dock:day` (input hands), then the Our day screen |
| 3 | The cup grabbed by a simulated hand | **pass** | `grab object:cup` (input hands), `cup picked-up`, the cup moved 5 cm with the hand, `release` |
| 4 | The waiting voice note heard within the first minute (fresh start, sample day) | **pass** | `voice play` 33.1 s after the app started (limit 60 s), captions on screen, `voice finished` |
| 5 | A reply on the table survives an app restart and an API restart | **pass** | A new reply (typed on the spatial keyboard) was stored and placed on the server; it was back on the table after an app restart, and again after `stop.ps1` + `start.ps1` restarted the API and worker |
| 6 | Recenter, pause and exit | **pass** | Recenter turned the room from 35.5° off to straight ahead; pause and resume; Leave → the app ended |
| 7 | Hand tracking lost mid-press and found again: no ghost press | **pass** | The button was held down when tracking dropped: `cancel launch:recenter`, no press, press count unchanged |
| 8 | An MP4 for every app session | **pass** | 6 of 6 sessions recorded (FFmpeg captures of the simulator window) |
| 9 | Furniture chosen in the marketplace by a simulated hand is kept across an app restart | **pass** | `press dock:market`, `market:tab-chairs`, `market:finish-chairs-sage` (the chairs in the room turned sage at once: `decor try chairs=tub/sage`), `market:keep` (`decor kept v7`). On the server the room went from version 6 with charcoal chairs to version 7 with sage. After an app restart the live room opened with `decor shown v7` and sage chairs |

Reading this run:

- **Three presses were made with the head turned about 25° to the left**, and turned back afterwards: Done on the keyboard (check 5) and Pause on the dock, twice (check 6). In the simulator both hands move together, and the left index would otherwise have come down on another button at the same moment; the report names each press, and the automation notes below say why.
- **Check 7 loses both hands.** `metavr xrsim input source --hand right --source none` takes tracking away from both hands in this simulator (the probe shows left and right gone). The check therefore covers every hand disappearing mid-press.
- **One recording ends with the Unity editor.** When the app leaves at the end of check 6, the simulator closes its window, and the last second of `session6-live-market-restarted.mp4` shows the editor that was behind it. Before a run is added to the repository, a frame every two seconds of its recordings and each of its simulator screenshots are looked at (for this run 199 frames and eleven screenshots; for run 022557, 239 and eleven): nothing but the simulator appears in them, apart from that last second. The same was done for run 125054 on 12 November.

**The build for judges has no server behind it, and the app was run that way too.** `acceptance.ps1 -SampleOnly`
starts the app as a build made with no API address:
[`acceptance-033533`](native/evidence/2026-11-13/acceptance-033533.md), 13 November 2026. Checks 1, 2, 3, 4, 6, 7
and 8 passed; checks 5 and 9 need the service and were skipped. Check 1 also read the welcome panel through the
probe: *Try a sample day*, the captions and sound chips, Recenter and Leave can be pressed, and there is no *Sign
in*, *Create account* or *Continue as* (`seat-launch.png`, with the line that says why). Settings has no *Use my
account* (`seat-tour-settings.png`). The voice note was playing 32.2 s after a fresh start. Its two recordings and
six simulator screenshots were looked through the same way before they were added.

On the way to run 125054, four runs failed and were kept out of the repository; each found something that is now fixed (details under the automation notes): `092244` and `113228` couldn't start their sessions (the Play-mode keystroke), `120650` passed 8 of 9 (the left hand on the dock, check 6), `122623` passed 6 of 9 (switching one hand off switches off both).

Still to do by hand (the probe can't see these): watch the MP4s, listen to the voice note, and confirm the
simulated device reads *Meta Quest 3* in the simulator's panel (it does in `sim-check1-session.png`).

### What the simulator runs found and fixed

The first full runs failed. These are the causes, all fixed and re-tested:

| Found | Cause | Fix |
|---|---|---|
| **The room never filled in**: no cup, photos, replies or keepsakes, in sample and live mode | `HidePanel()` cancelled the screen's cancellation token and didn't make a new one, so `EnterRoom` loaded the room with an already-cancelled token and stopped silently | `HidePanel` starts a fresh token (`SameSkyApp.cs`) |
| "Couldn't load your room" (Editor, Play mode) | `GetComponent<Rigidbody>() ?? AddComponent<Rigidbody>()`: in the Editor a missing component is Unity's "fake null", which `??` treats as an object | `TryGetComponent` (`GrabbableObject.cs`; the same latent pattern in `XrSetup.cs`) |
| **A press fired when a hand lost tracking mid-press** | The Interaction SDK disables a hand's interactors when tracking drops: an *unselect* and an *unhover* in one frame, with no *cancel*. `Pressable` counted the unselect as a press | A release is confirmed one frame later, and only if no unhover came with it; otherwise it's logged as a cancel (`Pressable.cs`) |
| **The spatial keyboard opened on top of the dock**: reaching for Done could press Recenter or Pause | Both sit at the table edge in front of the person | The dock hides while the keyboard is open (`SameSkyApp.OpenKeyboard`) |
| The app was **always paused** in the simulator (no sound, stories stopped) | It paused on `OnApplicationFocus(false)`, which on a PC means the editor window lost focus: always, while the simulator window is in use | On a PC with XR running, pausing follows OpenXR input focus (`OVRManager.InputFocusLost/Acquired`); window focus still counts on the headset. Pause reasons are tracked separately, so leaving the pause menu by any path (Resume, or Leave then Stay) resumes |

### What the automation had to learn about simulator 207

Recorded here because none of it is in Meta's documentation:

- **Keys only reach the simulator while a mouse button is held in its 3D view.** Without one, they go to whichever
  side-panel control has focus (Space opened the About page).
- **Each main button also does something.** The left one pinches the right hand. The middle one pans and the right
  one looks around, by however far the cursor is from where the simulator last saw it, and it sometimes forgets
  that (a new app session). The script clicks the right button once at one fixed point to give the 3D view the
  keyboard, then holds a side button (X2) for the keys, and levels the head after every app start.
- **The movement keys move the headset, and the hands follow it.** There's no key to move one hand, so the script
  moves the whole simulated person to put the right index fingertip on a target. It steers with the probe's
  fingertip position (`pokeAt`, from the app's poke interactor) in a closed loop. The hand-pose keys 1–4 (open,
  poke, pinch, grab) stay set until the next one.
- The script's own Alt key tap, used to bring a window to the front, turned the view when the simulator was already
  in front. It now taps Alt only when needed.
- **No MP4 recorder in runtime 207.1.** `metavr xrsim record` says the build doesn't include the recording service.
  The script asks once, then records the simulator window with FFmpeg.
- **The simulator window shrinks** by about 20 × 12 pixels at every new app session. The script sets it back to one
  size.
- **The app only knows where the fingertip is while the hand's poke interactor is the active one.** Pointing across
  the dock can leave the ray interactor active until the finger is a few centimetres from a button, and the stored
  fingertip offset alone was several centimetres out after a recenter: a press landed on the button next to Pause.
  The script now re-reads the fingertip 4 cm in front of a button and corrects before pushing.
- **Six dock buttons sit closer together than five.** With *Market* on the dock, a press meant for Pause opened
  Settings (run 083720). When the fingertip isn't reported at 4 cm the script now steps in a centimetre at a time,
  and if a dock press opens the neighbouring screen it goes back and presses again, and says so in the report.
- **Play mode is asked for through a file, not a keystroke.** The script used to press Ctrl+P in the editor. On
  12 November two runs couldn't start their sessions: both times the editor entered Play mode only when the
  script's own process ended. A Preferences window had been left open in the editor, which sends the script down a
  different path when it brings the editor forward; the exact cause wasn't reproduced afterwards, so the remedy
  is to not depend on the keyboard at all. A newly started editor showed the same weakness from another side:
  behind the simulator window it never ran its start-up play request, because Unity runs
  `EditorApplication.delayCall` only while it is the foreground application. Both are gone: the editor script
  (`Editor/SimRun.cs`) now works from `EditorApplication.update`, which keeps ticking in the background, and reads
  a request file (`Temp/SameSkySim/request.txt`: `play` or `stop`) that it answers in `ack.txt`. Ctrl+P is only a
  fallback for an editor that doesn't answer. The script also no longer uses `WScript.Shell.AppActivate`, and taps
  Alt only when another application is in front.
- **The simulator can outlive an app session.** Sometimes it closes when the app leaves and the OpenXR runtime
  starts a new one for the next session; sometimes it stays. Both work; if an app hasn't started 90 s after it was
  asked for, the script asks the editor again and, when the editor says it is already entering Play mode, restarts
  the simulator. That recovery hasn't been needed since it was written, so it has never run.
- **Both hands move together, half a metre apart, and neither can be switched off alone.** While the right index
  presses a button, the left index comes down too. With eight items the dock is as wide as that gap: a press on
  Pause at its right end had the left index on *Our day* at its left end, and Our day opened instead (run 120650).
  `metavr xrsim input source --hand left --source none` takes tracking away from both hands (run 122623), so the
  left hand can't simply be set down. When the left index would land on another button, the script turns the head
  about 25° to the left for that press (the left hand swings back toward the person, the right still reaches) and
  turns it back afterwards; the report names each press it did this for. A person has no such problem: their hands
  move separately.
- **FFmpeg records a screen area, not a window.** The script keeps the simulator window above every other window
  while it records, so nothing can cover that area. When the app leaves, though, the simulator closes its own
  window, and the area shows what was behind it until the recorder stops: the recording of a session that ends
  with Leave (check 6) ends with a second or two of the Unity editor.

## Visual pass against the reference pack, and a UI/UX and realism round (11–12 November)

The thirteen concept images and `SameSky_Visual_Reference_Guide.md` are design references: they prove nothing about
the app. What the native app actually draws is recorded as **fixed seated renders**. The app's development probe
draws the room from the seated eye with its own camera (`TestProbe`, `snap-request.json`), so every run frames the
same views whatever the simulated headset is doing; `acceptance.ps1 -Renders` adds a tour of the other screens,
which the probe opens directly (pictures of what is drawn, not interaction checks). Renders:
`docs/native/evidence/2026-11-12/acceptance-065004/seat-*.png`; sixteen of them on one sheet in
[`docs/native/renders/visual-pass-2026-11-12.jpg`](native/renders/visual-pass-2026-11-12.jpg).

| Ref | Native now | Render | Still different, or not shown by a render |
|---|---|---|---|
| 02 shared lounge | One generated room (`Editor/LoungeBuilder.cs`, `Scripts/Room/MeshGen.cs`): curved floor-to-ceiling glazing with a terrace outside (real parallax against the far skyline plate), ivory plaster, an arched walnut alcove, a 1.05 m walnut table, cream bouclé tub chairs, brass floor lamp, plant, rug. **Light:** late-afternoon window light with real soft shadows (the mullions rake across the floor and the left wall; the table, chairs, frames, cup and the person opposite all shade what is behind them), a soft contact shade where things touch, and glossy surfaces reflect the room itself (one reflection drawn at start). **The person:** a rigged adult (Rocketbox, MIT) seated with a breathing loop, lit by a soft key that touches people only. The dock sits at the table edge with the room's name above it | `seat-tour-direct.png`, `seat-tour-left.png`, `seat-tour-right.png` | Maya's hands rest in her lap, not on the table. No glass in the glazing. Scale, comfort and reach need a headset, and so does the cost of the shadow map (one 2048 map, one cascade, the cheapest soft filter) |
| 10 first minute | A compact welcome (Try a sample day, Sign in, Create account, Captions, Sound; Recenter and Leave beneath) with the room visible around it; in the room, one cue card with a leader to the cup | `seat-launch.png`, `seat-room-cue.png` | — |
| 03 waiting message | The chair is empty while a person is away or only their recording plays. Lifting the cup plays the note; a charcoal card shows the speaker's portrait (rendered from the same character model), title, a *Recorded* tag, their local time, the caption as it is spoken, the position in the recording, Pause/Resume, Replay, Reply, and Place cup while it is held. The cup glows a little while a hand is near or holds it. A second grab carries on instead of starting over. A reply is typed on a solid keyboard at the table edge and then stands on the table as a small folded card, in a place of its own; a saved place that no longer fits the table is put back in line | `seat-message.png`, `seat-room-partner.png`, `seat-tour-reply.png`, `seat-live-room.png` | — |
| 01, 13 avatar likeness and approval | **A face that reads as alive** (`Avatars/SeatedFigure.cs`): the eyes move on their own bones, hold your face with small shifts and now and then glance away, leading the head; uneven blinks, usually on a change of gaze; a faint warmth around the mouth and eyes that varies slowly, a little more on arriving; they arrive in a brief soft glow rather than popping in. Skin keeps a soft highlight, knit and hair are matte. All of it is app animation, not tracked from anyone's face, and the presence label says which kind of presence it is. **Preview:** one booth (`Avatars.Booth`) renders head and shoulders from the same model that sits in the room: front, three-quarter, side. The sample shows it for the demo character, labelled as not made from photos. With an account, the review shows the owner's private source photo, the turnable preview, a side view, who will see it, then **Use this avatar**, **Retake photos** or **Cancel**; an avatar still being prepared shows its progress and keeps the labelled demo avatar meanwhile | `seat-tour-partner.png`, `seat-tour-avatar.png` | Blinks, glances and the arrival are motion: a still render shows one moment of them, and none of it has been watched in a headset. The account review is **implemented and compiled, not run**: it needs an avatar awaiting approval in a live account. Likeness itself needs consenting real people and a provider (see `AVATARS.md`); stock characters prove nothing about it. Lina still has the older stylised figure (she appears only as an initial on the circle's board) |
| 04, 05 watching and our day | Our day: an audience chip, one picture per chapter with whose it is, the title, length, what it was made from, the generator label, Play. The story plays on a flat screen beside the partner with larger captions; its controls lie at the table edge | `seat-tour-day.png`, `seat-tour-story.png` | Live shared playback between two accounts isn't simulator-tested (needs two app instances) |
| 06 share | One screen, four things kept apart: what is shared (thumbnail, source), one person or the circle, each recipient named, the AI permission as its own switch, and a final button that repeats who receives it. The sample walks through it with one of its own photos and sends nothing | `seat-tour-share.png` | The account flow shares the same screen and is not simulator-tested |
| 07 family circle | A room selector above the dock. In a circle nobody is seated as if present: a board names the circle, says it is asynchronous, and lists members with place and local time; the screen and table show only that room's material | `seat-tour-rooms.png`, `seat-tour-circle.png` | The circle's shared feed is the photos on the table, not a list on the board |
| 08 availability | Two tiles (portrait, place, local time, weekday), the next stretch you both usually have, worked out from each person's own weekly times and time zone, a *free now* switch that expires, and an explicit *Sit with …* | `seat-tour-together.png` | Sample only in this render; the account version uses the server's opportunities and isn't simulator-tested |
| 09 keepsakes | Rows with what was earned and when, and progress toward the next (5 of 7 shared days); on the alcove shelf, a frame, two linked brass rings and a small planter | `seat-tour-keepsakes.png`, `seat-tour-shelf.png` | The shelf objects are simple shapes |
| 11 device import | Unchanged: importing from a phone or camera is the companion's job; the headset plays what was imported and labels its source | — | No new native screen |
| 12 comfort and recovery | Settings in plain groups (hearing and reading with the volume level stated; your seat; the sample or the account), with no addresses or build internals on screen; Paused; avatar-being-prepared with the demo alternative (above); presence labelled live, simulated, recorded or not here. Back is in the same place under every in-room panel | `seat-tour-settings.png`, `seat-tour-pause.png` | Reconnect and failure states weren't driven in this pass |

Things this pass found and fixed along the way: toasts lost their text after the first one and left an empty pill
on screen (only the text was hidden when a toast expired); folded cards rendered black (shared vertices, averaged
normals); a photo frame drew over panels in front of it (see-through UI doesn't write depth: prints are now opaque,
and solid panel backgrounds write depth); in the Editor, shader variants compiling in the background showed up cyan
in renders (the probe's renders now wait for the compiler; switching background compilation off instead stalled Play mode for seconds at a time once shadows were on, and broke the timed checks). In the second round, on the look itself: the room had no cast shadows and no reflection of itself, so it read as flat; armchairs burned out to white; replies floated above the table as flat panels; the reply keyboard was see-through; brown hair turned grey once the room was reflected in it (hair, knit and skin no longer mirror the room); the lamp made a hot spot on the wall beside it; and Back sat in a different place and colour on each panel.

Not established by any of this: how it looks and feels in a headset, frame rate on a Quest, and likeness for real
people.

## Hands, real sample media and a furniture marketplace (12 November)

Asked for after the merge: hands that look real, actual photographs and video in the demonstration, and a
marketplace for customising the furniture. Renders: `docs/native/evidence/2026-11-12/acceptance-125054/seat-*.png`;
twelve of them on one sheet in
[`docs/native/renders/hands-media-market-2026-11-12.jpg`](native/renders/hands-media-market-2026-11-12.jpg).

| What | Native now | Evidence | Still different, or not shown |
|---|---|---|---|
| **Hands** | The tracked hands are Meta's own hand mesh (Interaction SDK, OpenXR skeleton), which the SDK draws in a dark see-through material. The app dresses the same mesh in skin (`Scripts/Interaction/HandSkin.cs`, `Shaders/HandSkin.shader`): a skin tone, a lighter palm, nails with a pale tip, wrinkles over the knuckles, creases where the fingers bend, three palm lines, a little redness at the knuckles and fingertips, soft wrapped light with a warm edge where light passes through, and a wrist that fades out instead of ending in a cut. The hands take the room's light and shadows and cast their own on the table. The maps are computed from the mesh's geometry and joints (`Editor/HandSkinSetup.cs`): no photo or scan of anyone's hands. **Tone:** Settings → *Your hands* offers six tones, or *Match my avatar*. Tracking, poses and every interaction are untouched: only the material changes, and only when the mesh is the one the maps were made for (otherwise Meta's material stays) | `seat-hands.png`, `seat-palms.png`, `seat-hands-pinch.png` (check 1), `seat-tour-hands-panel.png`; probe state `handSkin`; unit test `Hand_skin_is_bundled_with_its_tones` | These are the simulator's hands in three fixed poses. A real hand's tracking, the look at arm's length and the cost on a Quest (one more lit pass with shadows, two 1024 px maps per hand) need a headset. Nails are drawn in the texture, not modelled, and read as painted from very close; no hair, veins or tendons |
| **Real photographs and footage in the sample day** | The four sample photos and the clip are real: an autumn park path, Dubai Creek at dusk, a desert at sunset, a courtyard garden at night, and 8.5 seconds of the creek with a ferry crossing (Wikimedia Commons; CC0, public domain, CC BY 4.0 and CC BY 3.0; sources and hashes in [ASSET_MANIFEST.md](ASSET_MANIFEST.md)). Each carries a credit that the moment's detail shows, and the labels say *Stock photo (sample)* and *Stock footage (sample)*: nobody in the fictional day took them. Captions and the recap's lines were rewritten to describe what the pictures show. The voice note is still synthesized and labelled | `seat-tour-day.png`, `seat-tour-story.png`, `seat-tour-share.png`, `seat-tour-left.png`; unit tests `Sample_day_loads_offline_with_honest_labels`, `Sample_media_is_bundled_for_offline_use` | The clip is silent (the original's music isn't used). Watching it play is a manual step: a render shows one frame. The web room's and the companion's captures weren't re-taken |
| **Furniture marketplace** | *Market* on the dock opens the marketplace at the table edge: tabs for Table, Chairs, Rug, Lamp, Plant, Walls, Floor and Looks; every piece as a picture rendered from the real model, every finish as a swatch. Choosing changes the real room at once, as a preview only you see; **Keep** saves it for the room and everyone in it; **Put back** returns to what was kept; leaving the marketplace without keeping does the same. 13 pieces in 7 slots, 35 finished parts and 3 ready-made looks, all built by `Editor/LoungeBuilder.cs`. **Nothing is priced or sold:** no purchases, balances or ownership. Server: `GET /v1/furniture/catalog`, `GET`/`PUT /v1/rooms/{id}/decor` with a version check, and a `decor_changed` notice to people in a live session ([API.md](API.md#furniture)). The sample day keeps its choice on the device | check 9 (live room, real local API); `seat-tour-market*.png`, `seat-tour-look-*.png`; `api/tests/test_furniture.py` (9 tests); four unit tests in `FurnitureTests.cs` | A simulated hand chooses a chair fabric and keeps it (check 9); the other tabs and the looks are drawn by the tour, not pressed. Two people changing the room at once is covered by the API tests (409 on a stale version) and the app's handling of it is implemented, but it wasn't run with two app instances. The pieces are generated shapes rather than scanned furniture; the leafy plant is the one scanned model (CC0) |

Things this round found and fixed along the way: a component stored in the scene needs a script file named after
its class (two marketplace components in one file lost their data when the scene loaded: the default lamp stayed
visible beside the chosen one); pictures on a see-through card need their own drawing order or the card washes
them out; the label under the cup showed through panels standing at the table edge.

## Results by area

| Area | Implemented | Compiled | Simulator-tested | Real-service-tested | Notes |
|---|---|---|---|---|---|
| Launch, offline sample day, the room, the cup and its voice note with captions | yes | **yes** (Unity 6000.3.28f1, 0 errors) | **yes**: checks 2–4 | — | Fictional people; the voice is synthesized and labelled |
| Live mode: sign-in kept on the device, Nora's room, reply by the cup on the spatial keyboard, reply placed on the table | yes | **yes** | **yes**: check 5 | **yes**: check 5 against the real local API and worker, with an API restart in the middle | The sign-in itself was the stored session the script wrote; typing an email and password on the spatial keyboard wasn't automated |
| Recenter, pause menu, leaving | yes | **yes** | **yes**: check 6 | — | The system pause (Meta button, headset off) needs a headset |
| Hand-tracking loss | yes | **yes** | **yes**: check 7 | — | |
| Tracked hands drawn with skin; six tones or the avatar's | yes | **yes** | **drawn**: check 1's close-ups, the tones panel in the tour; the probe reports the skin on every hand visual in the rig (`handSkin.dressed`: 6, with the tone) | — | Every check's presses and grabs were made with these hands. The tone buttons weren't pressed by a simulated hand |
| Furniture marketplace: try, keep, put back; looks | yes | **yes** | **yes**: check 9 (a chair fabric chosen and kept by a simulated hand, still there after an app restart); the other tabs and looks **drawn, not driven** | **yes**: check 9 against the real local API (the room's decor version on the server went up by one and came back after the restart); `api/tests/test_furniture.py`, 9 tests | Two people at once: API-tested only |
| Real photographs and footage in the sample day, credited and labelled | yes | **yes** | **drawn**: the tour's Our day, story, Share and room views | — | Web: type check, unit tests and production build pass; its captures weren't re-taken |
| Edit-mode unit tests | `Tests/EditMode` (14 tests) | **yes** | — | — | `scripts/native/test.ps1`: **14 of 14 pass** in Unity batch mode (contracts, sample labels and credits, configuration, the sample-only rule, recordings, playback timing, the furniture rules, the hand skin bundle) |
| Sample-only build: no server, the sample day only (the build for the competition's judges) | `App/AppConfig.cs` (`SampleOnly`: no API address the build may use); the welcome panel, Settings and the end of the Share walk-through then leave out everything that needs an account, and the welcome panel says so; `build.ps1 -SampleOnly` | **yes** | **yes**: run `acceptance-033533` (`acceptance.ps1 -SampleOnly`), checks 1 to 4, 6 and 7 with simulated hands, and the welcome panel read back through the probe | — | Unit test `A_build_without_a_server_is_sample_only`. In this build nothing that needs an account can be reached: sharing with real people, live sessions, the photo-derived avatar review |
| The sample's other screens: Our day and the story screen, Share, Together, Keepsakes and the shelf, the avatar preview, Settings, Pause, the room selector, the family circle | yes | **yes** | **drawn, not driven**: the render tour opens each through the probe and saves a fixed seated view; only Our day is opened by a simulated hand (check 2), and Pause by check 6 | — | Pictures of what the app draws. Pressing their buttons with simulated hands is next to automate |
| First-run setup, invite and join with a short code (keypad), sharing and Together with an account (shared playback), inbox, the photo-derived avatar review | yes | **yes** | **no** | **partly**: invite codes through the API (`test_invite_codes.py`, `seed.ps1`) | Not covered by the acceptance checks; they need the keyboard and more targets in the automation, an avatar awaiting approval, and for Together two app instances |
| Short invite codes (8 characters, hand-typed) | yes (API, companion, native keypad) | yes | keypad no | **yes**: `api/tests/test_invite_codes.py` (6 tests); `seed.ps1` redeems codes through the public API | Single use; 8 misses per 15 min per account; only an HMAC is stored |
| Voice notes playable natively (Ogg copy) | yes | yes | **yes**: Sam's seeded voice note is in Nora's live room (check 5) | **yes**: `test_headset_voice_note_gets_an_ogg_twin_for_native_playback` | Playback of the live note itself wasn't timed; check 4 timed the sample one |
| Local setup: preflight, bootstrap, start/stop/seed, per-user installs | yes | — | — | **yes** | Services run detached in their own process group; `stop.ps1` and `start.ps1` were exercised by check 5 |
| Windows desktop build (for the simulator) | yes | **built** (335 MB) | **blocked** | — | Windows Smart App Control blocks the unsigned `SameSky.exe`; we don't change security settings. Unity Play mode (signed editor) is the simulator path |
| Quest APK | `scripts/native/build.ps1 -Target Android` | **APK-built** on 13 November from the code as it is committed, with the manifest brought in line with Meta's published rules (below): `SameSky-dev-20261113-0239.apk`, 136 MB (142,562,395 bytes), `com.samesky.quest` 0.9.0 (versionCode 9), ARM64 only (`arm64-v8a`), min SDK 32, **target SDK 34**, supported devices **`quest3\|quest3s`**, development build, 0 errors; SHA-256 `8840bcdcc81d230ecb920a94c898fb4ead499040f3674b4ee8544ed4f0a3afe2`. Package facts read from this file with `aapt2 dump badging` and `dump xmltree` | — | — | A **development** build: debuggable, signed with the debug key, and it talks to this PC's LAN address (`http://<lan-ip>:8000`); the sample works offline. It can't go into a release channel, and it shouldn't be passed on at all: a development build carries that address, and the SDK's developer tools add foreground-service and media-projection entries to its manifest. Not installed on a headset, and never started on any device (open item 1). Unused shader variants aren't stripped |
| Release build, sample-only (what goes into the release channel) | `scripts/native/release.ps1 -SampleOnly` (asks for the keystore password, then `build.ps1 -Release -SampleOnly`) | **Built and read back with a test key, not with a real one.** On 13 November the same path was run with Android's standard debug key standing in for the keystore: 114 MB (119,487,498 bytes), not debuggable, version code from the date, target SDK 34, `quest3\|quest3s`, ARM64 only, 0 errors. Its permissions are hand tracking, internet, microphone, audio settings and Bluetooth: the development build's foreground-service and media-projection entries are not in it. The script refused it at the end, as it should ("isn't signed with your keystore"), and it was deleted | — | — | Two things of the build PC were in the first such build and are now kept out, with a search of every file in a release APK that stops the build if either comes back: this PC's local network address (the SDK writes it into `Resources/DevAgentSettings.asset` before every build; `Editor/ReleasePrivacy.cs` empties it for builds that aren't development builds) and the Windows user name (in the file paths of Unity's IL2CPP runtime sources; `BuildScript.HomePathMap` has the compiler write them as `~/...`). The keystore password Unity writes into its generated Gradle project is taken out after the build. The real build needs the entrant's keystore and has not been made; nothing has been uploaded |
| Headset install and performance | — | — | — | — | No Quest available: **headset-installed** and **hardware-measured** are both *no* |

## Open items

1. **No headset run yet, and no build of the app has ever been started.** Frame rate (the competition asks for
   60 fps on Quest hardware), comfort, spatial audio, real hand tracking and the system pause all need a Quest 3/3S.
   See [NATIVE_QUICKSTART.md](NATIVE_QUICKSTART.md), section 5. Every simulator result on this page comes from
   Unity Play mode, which runs the app's code on the Editor's Mono runtime. The APK is an IL2CPP build of the same
   code: it compiles and packages without errors, but it has not been run, so faults that only a built player shows
   (code or shader variants stripped from the build, Android-only behaviour) can't be ruled out from here.
2. **The unsigned desktop build is blocked** by Smart App Control. The simulator runs use Play mode instead.
3. **Flows outside the acceptance checks aren't simulator-tested yet**: first-run setup, invite and join with a
   code, and with an account: sharing, Together, the inbox and the avatar review. They need the keyboard and more
   targets in the automation, and Together needs two app instances. The sample's versions of these screens are
   rendered by the tour but their buttons aren't pressed by simulated hands yet.
4. **Looks are judged from renders only.** Fixed seated renders show composition, materials and legibility; scale,
   comfort, reach, the tilted table-edge panels and the partner's presence need a headset. Maya's hands rest in
   her lap rather than on the table, the glazing has no glass, and the shelf keepsakes are simple shapes. The shadow map and the one-off reflection are new costs on a Quest that only a headset run can measure.
5. **Real likeness is untested.** The sample's people are stock characters. The owner review for a photo-derived
   avatar is implemented but hasn't been run against an avatar awaiting approval.
6. **Release signing and competition distribution** need a keystore and the Meta developer dashboard (quickstart,
   section 6): the competition takes the link to a release channel named "Competition", not an APK file. On
   13 November the project's Android settings were checked against Meta's published manifest rules and two were
   wrong: the target API level was 36 (the rules ask for 34 from apps created since March 2026) and the supported
   devices named Quest 1, Quest 2, Quest Pro and the VR glasses as well as Quest 3 and 3S. Both are corrected and
   confirmed in a new build. The API isn't hosted, so the build for judges is **sample-only** (decided 13 November):
   no server address, the sample day and no account buttons. `scripts/native/release.ps1 -SampleOnly` makes it, and
   that path was run to the end with a test key (the row *Release build, sample-only* above). What is left needs the
   entrant: the keystore and its password, the upload, the channel's link. Nothing has been through the dashboard's
   own upload checks yet.
7. **The skin on the hands is unmeasured on a Quest** and has only been seen on the simulator's hands. If it costs
   too much there, the fallback is the SDK's own hand material (remove the `HandSkin` component).
8. **The marketplace beyond one fabric isn't pressed by simulated hands**, and a room changed by two people at the
   same moment hasn't been run with two app instances.

Nothing on this page claims a headset test, a performance measurement or a store build that hasn't happened.
