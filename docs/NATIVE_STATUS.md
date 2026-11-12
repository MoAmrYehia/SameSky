# Native Quest app: status and evidence

Updated 12 November 2026. This page covers the native Unity client in `native/quest/`, built for the brief
`SameSky_Native_Quest_Local_Simulator_AI_Instructions.md` and, for its look, the visual reference pack
(`SameSky_Visual_Reference_Guide.md`).

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

`scripts/native/acceptance.ps1 -NonInteractive`, Unity Play mode, simulated **Meta Quest 3** with **simulated
hands**. Every gesture was made by the script through the simulator's own keyboard controls. Each one reached the
app as OpenXR hand tracking through Meta's Interaction SDK, and each check passed only on the app's probe events.
Evidence, after the visual pass and the UI/UX round below:
[`docs/native/evidence/2026-11-12/acceptance-065004.md`](native/evidence/2026-11-12/acceptance-065004.md) and `.json`,
with probe events, state snapshots, simulator screenshots, fixed seated renders and one MP4 per app session in the
folder of the same name. An earlier full pass, from before the visual pass:
[`acceptance-210509`](native/evidence/2026-11-11/acceptance-210509.md) on 11 November (also 8 of 8).

| # | Check | Result | What the probe recorded (run 065004) |
|---|---|---|---|
| 1 | XR session: simulated Meta Quest 3, hand input | **pass** | OpenXR runtime "Meta XR Simulator 207.1.0", both hands tracked, active input hands |
| 2 | A dock button pressed by a simulated hand | **pass** | `press dock:day` (input hands), then the Our day screen |
| 3 | The cup grabbed by a simulated hand | **pass** | `grab object:cup` (input hands), `cup picked-up`, the cup moved with the hand, `release` |
| 4 | The waiting voice note heard within the first minute (fresh start, sample day) | **pass** | `voice play` 25.9 s after the app started, captions on screen, `voice finished` |
| 5 | A reply on the table survives an app restart and an API restart | **pass** | A new reply (typed on the spatial keyboard) was stored and placed on the server; it was back on the table after an app restart, and again after `stop.ps1` + `start.ps1` restarted the API and worker |
| 6 | Recenter, pause and exit | **pass** | Recenter turned the room from 35.5° off to straight ahead; pause and resume; Leave → the app ended |
| 7 | Hand tracking lost mid-press and found again: no ghost press | **pass** | No press, press count unchanged. In this run the finger wasn't registered inside the button when tracking dropped, so there was no cancel to record; the script now checks that the button is down first, and the follow-up run [`acceptance-065944`](native/evidence/2026-11-12/acceptance-065944.md) (check 7 alone) recorded the button held, `cancel launch:recenter`, and no press |
| 8 | An MP4 for every app session | **pass** | 5 of 5 sessions recorded (FFmpeg captures of the simulator window) |

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

## Results by area

| Area | Implemented | Compiled | Simulator-tested | Real-service-tested | Notes |
|---|---|---|---|---|---|
| Launch, offline sample day, the room, the cup and its voice note with captions | yes | **yes** (Unity 6000.3.28f1, 0 errors) | **yes**: checks 2–4 | — | Fictional people; the voice is synthesized and labelled |
| Live mode: sign-in kept on the device, Nora's room, reply by the cup on the spatial keyboard, reply placed on the table | yes | **yes** | **yes**: check 5 | **yes**: check 5 against the real local API and worker, with an API restart in the middle | The sign-in itself was the stored session the script wrote; typing an email and password on the spatial keyboard wasn't automated |
| Recenter, pause menu, leaving | yes | **yes** | **yes**: check 6 | — | The system pause (Meta button, headset off) needs a headset |
| Hand-tracking loss | yes | **yes** | **yes**: check 7 | — | |
| The sample's other screens: Our day and the story screen, Share, Together, Keepsakes and the shelf, the avatar preview, Settings, Pause, the room selector, the family circle | yes | **yes** | **drawn, not driven**: the render tour opens each through the probe and saves a fixed seated view; only Our day is opened by a simulated hand (check 2), and Pause by check 6 | — | Pictures of what the app draws. Pressing their buttons with simulated hands is next to automate |
| First-run setup, invite and join with a short code (keypad), sharing and Together with an account (shared playback), inbox, the photo-derived avatar review | yes | **yes** | **no** | **partly**: invite codes through the API (`test_invite_codes.py`, `seed.ps1`) | Not covered by the acceptance checks; they need the keyboard and more targets in the automation, an avatar awaiting approval, and for Together two app instances |
| Short invite codes (8 characters, hand-typed) | yes (API, companion, native keypad) | yes | keypad no | **yes**: `api/tests/test_invite_codes.py` (6 tests); `seed.ps1` redeems codes through the public API | Single use; 8 misses per 15 min per account; only an HMAC is stored |
| Voice notes playable natively (Ogg copy) | yes | yes | **yes**: Sam's seeded voice note is in Nora's live room (check 5) | **yes**: `test_headset_voice_note_gets_an_ogg_twin_for_native_playback` | Playback of the live note itself wasn't timed; check 4 timed the sample one |
| Local setup: preflight, bootstrap, start/stop/seed, per-user installs | yes | — | — | **yes** | Services run detached in their own process group; `stop.ps1` and `start.ps1` were exercised by check 5 |
| Windows desktop build (for the simulator) | yes | **built** (335 MB) | **blocked** | — | Windows Smart App Control blocks the unsigned `SameSky.exe`; we don't change security settings. Unity Play mode (signed editor) is the simulator path |
| Quest APK | `scripts/native/build.ps1 -Target Android` | **APK-built** after the UI/UX and realism round: `SameSky-dev-20261112-0702.apk`, 156 MB, `com.samesky.quest` 0.9.0 (versionCode 9), ARM64 only (`arm64-v8a`), min SDK 32, target SDK 36, development build, 0 errors; SHA-256 `ebf6b74dae6240356b37d5d6b19357db54224ef6c5f186bcd971ff026e556c21` (checked with `aapt2 dump badging`) | — | — | Built from `c2ff6c6` plus the uncommitted changes of 11–12 November: rebuild after committing. It talks to this PC's LAN address (`http://<lan-ip>:8000`); the sample works offline. Not installed on a headset. It is 28 MB larger than the build before shadows and the room reflection were switched on (128 MB), most likely the extra shader variants: worth stripping before a store build. Also before a store build, review the manifest: it asks for Bluetooth and media-projection permissions the app doesn't use. `build.ps1` used to wait forever after an Android build (Unity leaves an `adb` server running and the script waited for it); it now waits for Unity alone |
| Headset install and performance | — | — | — | — | No Quest available: **headset-installed** and **hardware-measured** are both *no* |

## Open items

1. **No headset run yet.** Frame rate, comfort, spatial audio, real hand tracking and the system pause all need a
   Quest 3/3S. See [NATIVE_QUICKSTART.md](NATIVE_QUICKSTART.md), section 5.
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
   section 6).

Nothing on this page claims a headset test, a performance measurement or a store build that hasn't happened.
