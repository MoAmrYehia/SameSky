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
| Native app: client, sample, room, story, share, Together, settings, avatars | yes | **yes**: 0 compiler errors | no | no | Assemblies: `SameSky.Runtime`, `Assembly-CSharp-Editor`, `SameSky.Tests.EditMode` |
| Project setup (OpenXR + Meta features, URP, player settings, scene, Interaction SDK rig) | yes | ran in batch mode | — | — | Android: Meta Quest, hand tracking, hand interaction, Meta aim, Touch. Windows: the same minus Quest support |
| Windows desktop build (for the simulator) | yes | **built** (335 MB) | **blocked** | — | Windows Smart App Control blocks the unsigned `SameSky.exe`; we don't change security settings. Unity Play mode (signed editor) is the simulator path. |
| Play mode in Meta XR Simulator | `scripts/native/play_in_sim.ps1` | — | **not yet** | — | First attempt surfaced two fixes, listed below |
| APK | `scripts/native/build.ps1` | — | — | — | Needs the Android SDK/NDK unpacked first |
| Headset install and performance | — | — | — | — | No Quest available |

## Open items found so far

1. **glTF props and characters import as plain files.** glTFast registers its `.gltf`/`.glb` importer only as an
   override when another importer might claim them. Fix: add the scripting define
   `GLTFAST_FORCE_DEFAULT_IMPORTER_ON` (Android and Standalone) in `ProjectSetup`, re-import, rebuild the scene.
   Until then, the setup places a labelled placeholder for each model.
2. **Android SDK not found.** Unpack the downloaded JDK, SDK and NDK into the Android module with
   `install_unity_user.ps1`. The command-line tools zip's SHA-384 differs from Unity's record: Google
   re-published `commandlinetools-win-12266719_latest.zip`. That file is optional for builds.
3. **The unsigned desktop build is blocked** by Smart App Control. The simulator run uses Play mode instead.

Nothing on this page claims a simulator run, an APK or a headset test that hasn't happened.
