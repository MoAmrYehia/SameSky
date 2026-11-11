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
2. **Meta XR Simulator.** Install the standalone simulator. Either download it from Meta's developer downloads
   (Meta XR Simulator for Windows), or run `metavr tools install xrsim --elevate`. The MSI needs administrator
   approval. Don't add the deprecated `com.meta.xr.simulator` Unity package.
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

Record a clip of the simulator's view (MP4):

```bash
metavr xrsim record start -o docs/native/evidence/run.mp4
```

```bash
metavr xrsim record stop
```

### Acceptance run

`scripts/native/acceptance.ps1` runs the eight acceptance checks in the simulator. It opens Unity itself (close
it first), uses its own `acceptance` profile, and starts and seeds the local services. It drives the app only
through the simulator's own hand controls, and keeps a check as passed only when the app's probe recorded it.
Leave the keyboard and mouse alone while it runs. If it can't make a gesture, it asks you to make it in the
simulator and watches the probe for it.

```bash
powershell -ExecutionPolicy Bypass -File scripts/native/acceptance.ps1
```

- **Results:** `docs/native/evidence/<date>/acceptance-<time>.md` and `.json`. Probe events, state snapshots,
  screenshots and MP4s go in the `acceptance-<time>/` folder next to them.
- **Options:** `-Assist` makes every gesture yours; `-NonInteractive` never asks; `-SkipRecording` skips the MP4s.
- **Before committing:** the MP4s can be large. Check the files before you commit the folder.

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
