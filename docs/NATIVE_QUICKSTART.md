# Native Quest app: quickstart

The native SameSky app is a Unity project in `native/quest/`, written in C#. It uses the Meta XR Core SDK, the
Meta Interaction SDK and Unity's OpenXR plugin. It runs:

- in **Meta XR Simulator** on Windows, in Unity Play mode or as a desktop OpenXR build;
- on **Meta Quest 3/3S** as an Android ARM64 APK.

The Python API, worker, database, media pipeline and authorization are the same ones the companion uses. The
native app has no special endpoints.

Results so far, kept separate by kind of evidence: [NATIVE_STATUS.md](NATIVE_STATUS.md).

## Versions this repository builds with

| Piece | Version | Pinned in |
|---|---|---|
| Unity Editor | 6000.3.28f1 (Unity 6.3 LTS). Meta asks for 6000.0.66f2 or newer. | `native/quest/ProjectSettings/ProjectVersion.txt` |
| Meta XR Core SDK, Interaction SDK, Interaction SDK OVR | 209.0.0 | `native/quest/packages.lock.txt` (sha1) and `Packages/manifest.json` |
| Unity OpenXR, XR Hands, glTFast, Input System, Newtonsoft JSON | see `Packages/packages-lock.json` | Unity Package Manager lockfile |
| Meta XR Simulator (standalone) | 207.1 | installed per machine |
| Meta VR CLI (`metavr`) | 1.10.0 | `npm install metavr@1.10.0` (any folder) |

## 1. One-time setup (Windows 10/11 x64)

1. **Unity Hub and editor.** Install Unity Hub. Then install editor **6000.3.28f1** with **Android Build
   Support**, **OpenJDK** and **Android SDK & NDK Tools**. Hub asks you to accept Google's Android SDK licence.
   Sign in to Hub and activate a licence; Personal is fine if you're eligible.
   - *No admin approval available?* `scripts/native/install_unity_user.ps1` installs the same editor and Android
     tools for your Windows user only, from Unity's and Google's checksummed downloads. Hub can then *Locate* the
     editor.
2. **Meta XR Simulator.** Install the standalone simulator. Either download it from Meta's developer downloads
   (Meta XR Simulator for Windows), or run `metavr tools install xrsim --elevate`. The MSI needs administrator
   approval. Don't add the deprecated `com.meta.xr.simulator` Unity package.
   - *No admin approval available?* `scripts/native/install_xrsim_user.ps1 -Msi <msi>` unpacks Meta's signed MSI
     into a folder you own. It registers the Windows App Runtime for your user only. It doesn't change the
     system's OpenXR runtime: the scripts select the simulator per process.
3. **Bootstrap the project.** This downloads and verifies the pinned Meta packages, builds the bundled art and
   offline sample, and runs Unity in batch mode to configure XR, URP and player settings and build the scene:

   ```bash
   powershell -ExecutionPolicy Bypass -File scripts/native/bootstrap.ps1
   ```

4. **Check everything.** This reports each missing piece and how to fix it. Exit code 1 means something blocks:

   ```bash
   powershell -ExecutionPolicy Bypass -File scripts/native/preflight.ps1
   ```

## 2. Start the local services (live mode)

```bash
powershell -ExecutionPolicy Bypass -File scripts/native/start.ps1
```

```bash
powershell -ExecutionPolicy Bypass -File scripts/native/seed.ps1
```

- **What starts:** the real API on `0.0.0.0:8000` and the persistent worker, on an isolated database and media
  folder (`api/data/native-dev/`). They run detached. Stop them with `scripts/native/stop.ps1`.
- **Seed accounts:** `seed.ps1` creates development-only accounts and rooms through the public API. They are
  listed in `scripts/native/dev-seed.json`.
- **Reset:** `seed.ps1 -Reset` wipes the native-dev data only. It never touches `api/data/samesky.db` or any
  real account.

## 3. Run in Meta XR Simulator (Unity Play mode)

1. Open `native/quest` in Unity Hub.
2. Activate the simulator in either of these ways. Unity sets the OpenXR runtime for the editor process only,
   and the console shows `[Meta XR Simulator is activated]`:
   - **Window → Meta → Meta XR Simulator → Activate**;
   - the simulator icon next to Play.
3. Open `Assets/SameSky/Scenes/SameSky.unity` and press **Play**. The simulator window opens.
4. In the simulator's **Inputs** panel:
   - set **Device info → Device** to **Meta Quest 3**;
   - under **Input settings**, set **Left input** and **Right input** to **hands**.

   The device list fills only once the app is running. Changing the device needs a restart of Play mode.

   From a terminal you can do the same:

   ```bash
   metavr xrsim device set --name "Meta Quest 3" --wait
   ```

   ```bash
   metavr xrsim input sync --source hand
   ```

5. Recentering, seat height, captions and sound are all in the app: use the dock at the table edge, or Pause.

One command does steps 1–3 without Meta's activation menu: it starts the local services if needed, opens the
simulator, and opens Unity with the simulator selected for that editor process only (`XR_RUNTIME_JSON`). The
system's OpenXR runtime stays as it is.

```bash
powershell -ExecutionPolicy Bypass -File scripts/native/play_in_sim.ps1
```

### Driving the simulated hands (simulator 207)

The simulator's **Input bindings** panel lists the keys. Two things it doesn't say:

- **Keys only count while a mouse button is held in the 3D view.** Click the 3D view once with the right mouse
  button, then hold a mouse button while you press keys. Holding the right button and moving the mouse looks
  around.
- **The keys move the headset, and the hands follow it** (Inputs follow: Head). To put a fingertip on a button,
  move your whole simulated self.

| Key | Does |
|---|---|
| W A S D | move forward, left, back, right (relative to where the headset faces) |
| R F | move up, down |
| arrow keys | turn |
| Q E | tilt |
| 1 2 3 4 | hand pose: open, poke (index out), pinch, grab (fist). The pose stays until you press another. |
| Space | put the hands back in front of you |
| left mouse button | pinch with the right hand (the *Action input*) |

**Press a button:** press 2, then move until the index fingertip goes through the button's face, then back out. A
press counts when the finger comes back out while still over the button. **Pick up the cup:** move the open hand
around it, then press 4 (fist).

**Recording:** simulator 207.1's runtime has no MP4 recorder. `metavr xrsim record start` says its build doesn't
include the recording service. The acceptance run records the simulator window with FFmpeg instead. For a clip of
your own, use a screen recorder, for example the Snipping Tool's video mode.

### Acceptance run

`scripts/native/acceptance.ps1` runs the eight acceptance checks in the simulator:

- **Unity:** it opens Unity itself, with its own `acceptance` profile. Close any editor you opened yourself first.
  It reuses an editor that an earlier run opened.
- **Services:** it starts and seeds the local services.
- **Driving:** it uses only the simulator's own keys (above). It steers the right index fingertip with the probe's
  positions, in a closed loop.
- **Passing:** a check passes only when the app's probe recorded it.

The script uses the keyboard and mouse, so leave both alone while it runs. If it can't make a gesture, it asks you
to make it in the simulator and watches the probe for it.

```bash
powershell -ExecutionPolicy Bypass -File scripts/native/acceptance.ps1
```

- **Results:** `docs/native/evidence/<date>/acceptance-<time>.md` and `.json`. Probe events, state snapshots,
  screenshots and MP4s go in the `acceptance-<time>/` folder next to them. For a 207.1 runtime the MP4s are FFmpeg
  captures of the simulator window, so keep that window visible and unobstructed during the run.
- **Options:** `-Assist` makes every gesture yours; `-NonInteractive` never asks; `-SkipRecording` skips the MP4s;
  `-Only 2,3,4` runs just those checks (check 1 always runs: it calibrates the hands).
- **Before committing:** the MP4s can be large. Check the files before you commit the folder.

#### Fixed seated views (for comparing with the visual references)

The simulator's own screenshots are taken from wherever a check left the simulated head, so they make poor
comparisons. The app's development probe can instead draw the room from the seated eye with its own camera, the
same framing every run:

- Every acceptance run saves a few of these as `seat-*.png` (launch, the pick-up cue, the message card, the partner).
- `-Renders` adds a tour of the other screens: Our day, Share, Together, Keepsakes and the shelf, the avatar preview,
  Settings, Pause, the story screen, the room selector, the family circle, the person opposite in close-up, the room
  to the left and right, and the reply keyboard (`seat-tour-*.png`). Check 5 also saves the live room
  (`seat-live-room.png`).
- In the Editor a shader draws as a cyan stand-in while it compiles in the background; these renders wait for the
  compiler first, so the first one after a lighting change can take a while.

```bash
powershell -ExecutionPolicy Bypass -File scripts/native/acceptance.ps1 -NonInteractive -Only 2,3,4 -Renders
```

The tour opens each screen through the probe (`SameSkyApp.DevOpen`), not with hands: its pictures show what the app
draws, and say nothing about interaction. The checks above are the interaction evidence. To take one view by hand
while the app runs, write `snap-request.json` into the probe folder, for example
`{"name":"room","yaw":0,"pitch":-12,"open":"day"}`; `snap-room.png` appears beside it. Development builds only.

## 4. Build for the headset

```bash
powershell -ExecutionPolicy Bypass -File scripts/native/build.ps1 -Target Android
```

- **Address:** a development build talks to this PC's LAN address (`http://<lan-ip>:8000`). On a headset,
  `localhost` means the headset itself.
- **Cleartext:** plain HTTP is allowed only in development builds.
- **Output:** the APK, a `.build.json` record (Unity version, package id, size, SHA-256, commit) and the build log
  go to `native/quest/Builds/`.

Release build: this needs `https` and a keystore from environment variables. No signing secrets are in the
repository.

```bash
powershell -ExecutionPolicy Bypass -File scripts/native/build.ps1 -Target Android -Release -ApiUrl https://api.example.com
```

## 5. Install on a Quest 3/3S (later, with a device)

1. Enable Developer Mode for the headset in the Meta Horizon app, then connect it with a USB-C data cable and
   allow USB debugging on the headset.
2. Install with **Meta VR Developer Hub** (drag the APK in), or with ADB. ADB is under Unity's Android SDK
   (`.../AndroidPlayer/SDK/platform-tools/adb.exe`):

   ```bash
   adb install -r native/quest/Builds/Android/SameSky-dev-<stamp>.apk
   ```

3. Start the services on the PC (`start.ps1`) and allow port 8000 through Windows Firewall on the private
   network. The app's **Settings → About** line shows which API address it uses.
4. Measure on the device, not in the simulator:
   - frame rate and frame time with `metavr perf` or OVR Metrics Tool (Quest target: a steady 72 Hz or better);
   - hand-tracking quality, spatial audio and seated comfort.

   Record the results in `docs/NATIVE_STATUS.md` under *Headset*.

## 6. Competition distribution

A local simulator run doesn't give judges anything they can open. To submit:

1. Upload a **release** APK to your app in the Meta developer dashboard, on a **Competition** release channel.
2. Add the judges, and confirm the channel's **invite URL** opens on a test account.
3. Keep that build unchanged through judging. The rules for the Start Developer Competition 2026 are on Devpost.
   Re-check them, and confirm Start membership and Developer Access, before submitting.
