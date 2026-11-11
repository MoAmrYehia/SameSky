# Native Quest app: status and evidence

Updated 11 November 2026. This page covers the native Unity client in `native/quest/`, built for the brief
`SameSky_Native_Quest_Local_Simulator_AI_Instructions.md`.

**Rule:** every row names its own evidence. The kinds of evidence never stand in for each other:

- **Implemented**: the code exists.
- **Compiled**: Unity built it.
- **Simulator-tested**: run in Meta XR Simulator.
- **Real-service-tested**: run against the real local API and worker.
- **APK-built**, **headset-installed**, **hardware-measured**: as they say.

A browser or IWER result says nothing about the native client.

## Environment used

| Item | Value |
|---|---|
| Host | Windows 11 Home 10.0.26300 x64; Intel Core Ultra 9 275HX (24 cores); 15.4 GB RAM; NVIDIA GeForce RTX 5060 Laptop GPU and Intel Graphics |
| Unity | 6000.3.28f1 (Unity 6.3 LTS), Personal licence active. Installed per user (`%USERPROFILE%\Unity\Hub\Editor\6000.3.28f1`) from Unity's MD5-verified installers. Hub's own install needed an admin approval that couldn't be given at the time. |
| Android tools | Android Build Support installed. OpenJDK 17, SDK platforms 34–37, build-tools 36, platform-tools and NDK r27c are **downloaded but not yet unpacked** into the Android module, so Unity still reports "Android SDK not found". `scripts/native/install_unity_user.ps1` does this step. |
| Meta XR SDK | Core, Interaction and Interaction OVR 209.0.0, SHA-1 verified (`native/quest/packages.lock.txt`) |
| Other packages | OpenXR 1.19.0, XR Hands 1.10.0, glTFast 6.21.0, URP 17.3.0, Input System 1.20.1 (`Packages/packages-lock.json`) |
| Meta XR Simulator | 207.1, signature-checked (Meta Platforms, Inc.). Unpacked per user with `scripts/native/install_xrsim_user.ps1` (no admin prompt; Meta's bundled Windows App Runtime 1.5 registered for this user). The simulator window runs. Apps select it per process with `XR_RUNTIME_JSON`; the system OpenXR runtime is unchanged. |
| Meta VR CLI | `metavr` 1.10.0 |
| Local services | API and worker from `scripts/native/start.ps1` on the isolated `api/data/native-dev` data |

## Results

| Area | Implemented | Compiled | Simulator-tested | Real-service-tested | Notes |
|---|---|---|---|---|---|
| Short invite codes (8 characters, hand-typed) | yes (API + companion + native keypad) | yes | no | **yes**: `api/tests/test_invite_codes.py` (6 tests); `seed.ps1` redeems codes through the public API | Single use; 8 misses per 15 min per account; only an HMAC is stored |
| Voice notes playable natively (Ogg twin) | yes | yes | no | **yes**: `test_headset_voice_note_gets_an_ogg_twin_for_native_playback` | Authorized like the original; headset recordings are labelled *Headset* |
| Local start/stop/seed/preflight | yes | — | — | **yes** | Services run detached in their own process group; seed creates 4 accounts and 2 rooms |
| Native app: client, sample, room, story, share, Together, settings, avatars | yes | **yes at `cf2a5a3`**: 0 compiler errors. The C# edits after it (next three rows) are **not compiled yet** | no | no | Assemblies: `SameSky.Runtime`, `Assembly-CSharp-Editor`, `SameSky.Tests.EditMode` |
| Project setup (OpenXR + Meta features, URP, player settings, scene, Interaction SDK rig) | yes | ran in batch mode at `cf2a5a3` | — | — | Android: Meta Quest, hand tracking, hand interaction, Meta aim, Touch. Windows: the same minus Quest support |
| glTF props and characters imported by glTFast (open item 1) | yes: `ProjectSetup.ConfigurePlayer` adds `GLTFAST_FORCE_DEFAULT_IMPORTER_ON` (Android, Standalone) | **no** | no | — | Checked against glTFast's `GltfImporter.cs` and Unity 6000.3.28f1's `PlayerSettings` source, not compiled. The committed `.meta` files already name glTFast's importer, and the committed scene holds the real props; the define makes glTFast the registered default importer |
| Approved avatar loading with glTFast's generic `Load` | yes: `Avatars.SwapToApproved` uses `GltfImport.Load(byte[], Uri)` instead of the obsolete `LoadGltfBinary` | **no** | no | no | Same signature from glTFast 6.11 through the newest public source (6.20.1-pre.1). The GitHub mirror has no 6.21.0 tag, and the 6.21.0 package wasn't readable from the session that made the change |
| Probe events for reply persistence | yes: `reply placed:<id>` and `reply restored:<ids>` (`SameSkyApp.Room.cs`; ids only, no text) | **no** | no | no | Lets the acceptance run match a reply across restarts |
| Acceptance run (`scripts/native/acceptance.ps1`) | yes: 8 checks, results in `docs/native/evidence/<date>/` | — | **not run yet** | — | Written without a PowerShell parser at hand (checked structurally only). Drives the simulator's own hand keys; aims with the probe; passes a check only with probe evidence; falls back to asking the operator |
| Development seed with a waiting voice note (`seed_dev.py --voice-note`) | yes | — | — | **yes, on Linux** (real API and worker on throwaway data): processed, shared Sam → Nora, Ogg copy present, second run idempotent; reply and placement read back after an API restart | Not yet run on this Windows machine |
| Windows desktop build (for the simulator) | yes | **built** (335 MB) | **blocked** | — | Windows Smart App Control blocks the unsigned `SameSky.exe`; we don't change security settings. Unity Play mode (signed editor) is the simulator path. |
| Play mode in Meta XR Simulator | `scripts/native/play_in_sim.ps1` | — | **not yet** | — | First attempt surfaced two fixes, listed below |
| APK | `scripts/native/build.ps1` | — | — | — | Needs the Android SDK/NDK unpacked first |
| Headset install and performance | — | — | — | — | No Quest available |

## Open items found so far

1. **glTF props and characters import as plain files.** glTFast registers its `.gltf`/`.glb` importer only as an
   override when another importer might claim them. Fix: add the scripting define
   `GLTFAST_FORCE_DEFAULT_IMPORTER_ON` (Android and Standalone) in `ProjectSetup`, re-import, rebuild the scene.
   Until then, the setup places a labelled placeholder for each model. **The define is now in `ProjectSetup`
   (not compiled yet).** A define takes effect after the recompile that follows the setup run, so run the setup
   (`bootstrap.ps1`, or *SameSky > Set up project and scene*) a second time; the log says when the define was added.
   Note: in glTFast's public source up to 6.20.1-pre.1, the importer is already the default unless
   `GLTFAST_FORCE_DEFAULT_IMPORTER_OFF` is set. The override-only behaviour was seen with 6.21.0 on this machine,
   and 6.21.0's source hasn't been checked. Either way, the define selects the default registration.
2. **Android SDK not found.** Unpack the downloaded JDK, SDK and NDK into the Android module with
   `install_unity_user.ps1`. The command-line tools zip's SHA-384 differs from Unity's record: Google
   re-published `commandlinetools-win-12266719_latest.zip`. That file is optional for builds.
3. **The unsigned desktop build is blocked** by Smart App Control. The simulator run uses Play mode instead.

To watch in the first simulator run (unverified, from reading the code):

- `SameSkyApp` pauses sound and stories when the app loses focus (`OnApplicationFocus`). Driving the simulator
  focuses its window, which may count as lost focus for Unity. If so, the voice note won't advance. The acceptance
  run records `paused` from the probe and notes it.

Nothing on this page claims a simulator run, an APK or a headset test that hasn't happened.
