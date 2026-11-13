# Status — implemented vs. verified

Updated: 13 November 2026 (test counts re-run; the last larger revision was 8 November, for photo-derived avatars
and visual QA pass 2; first written 6 November). This file keeps
the brief's distinction between *implemented*, *tested on a headset*, *tested in an emulator/desktop*, and *still
unverified*. Nothing below is claimed beyond what was run.

**Native Quest app:** this page covers the web room, the companion and the API. The native Unity client in
`native/quest/` has its own page, [NATIVE_STATUS.md](NATIVE_STATUS.md), which separates implemented, compiled,
simulator-tested, real-service-tested, APK-built and headset-tested evidence. A browser or emulator (IWER) result
on this page says nothing about the native client.

## Photo-derived avatars — results

Each row stands on its own evidence; none is proved by another (details in `docs/AVATARS.md`).

| Capability | Result |
|---|---|
| Automated generation (photo → MetaPerson → GLB) | **Blocked / unverified**: adapter implemented and tested against a fake provider only; no Avatar SDK credentials or Pro plan here, so the real service was never called. The app says generation isn’t set up and offers import |
| Manual import | **Verified** in the real UI and API with a non-photo test mannequin: validation → four neutral review views → approval → portraits and room for the other person (`web/scripts/avatar-e2e.mjs`) |
| Owner-approved likeness | **Unverified**: no consenting real-photo subjects; no real person’s photo was used |
| Live co-presence | **Verified on desktop** (two browsers, one live session, no headset): partner seen live as the approved model; *stop sharing* swaps the open room to the labelled demo avatar without a reload, and sharing again restores it |
| Headset (Quest 3/3S) | **Unverified**: no hardware |

## Verification performed

| Check | Result | Where |
|---|---|---|
| API automated tests (82, re-run 13 November) | **pass** — short invite codes, the Ogg copy of a voice note for the native app, the furniture catalogue and each room's furniture choices (version check, realtime notice), privacy/authorization, withdrawal, removal, idempotency, plan validation, awards, availability/DST, Free now, call suggestions, restart persistence, media pipeline, realtime sync, avatar face preset validation, and for photo-derived avatars: the full job flow, cancellation blocking a late provider result, a new generation not replacing the approved one, revocation / member removal / deletion, invalid inputs and provider states (402, 429, bad credentials, not configured → 503), manual import validation, consent withdrawal and source retention, GLB inspector bounds, and the live `avatar_changed` notice (no likeness leaked, not sent to non-participants) | `api/tests`, local Windows 11, Python 3.13 |
| Migrations | `alembic upgrade head` + `alembic check` (no drift) on SQLite; the avatar migration `1c9e35884ad2` also checked downgrade → re-upgrade, and upgraded the existing development database | local |
| Web type check / unit tests / production build | **pass** (TS engine tests: 4; re-run 13 November) | local, Node 26 |
| End-to-end smoke against the running stack (API + worker + Vite proxy) | **pass**: signup ×3, direct room + invite, phone photo upload → worker processing → explicit share confirmation, outsider 404, recap drafted → both approve → shared, reply + recap notifications in inbox (generic text), First Shared Day award, live session: play command reached the second participant in ~10 ms (same machine) and pose relay worked | `scripts/smoke_live.py` |
| Companion, sample mode | Our day, story player (source chips, interpretation labels, chapter progress), Share, Together rendered and exercised | desktop Chromium (in-app browser), 1440×900 and 800 px |
| Companion, live mode | Our day and Together against the real API (real overlap computed server-side) | desktop Chromium |
| XR room, desktop (non-immersive) | Scene renders; cup → spatial voice note + captions + "Recorded message" avatar state; reply sheet → persistent note on the table survives reload; dock; keepsakes; 56–65 fps, ~100 draw calls | desktop Chromium with IWER (Meta Quest 3 emulation) injected; **pointer input exercised via synthetic events**, not a real headset |
| Avatar end-to-end (`web/scripts/avatar-e2e.mjs`) | **pass, 12 checks**: onboarding prompt; consent step honestly says generation isn’t configured; import → review (front, three-quarter, side, seated) → approve; partner portrait and room; live co-presence, also inside an immersive session in Meta's emulator; stop sharing / share again in an open room; no sideways overflow on the avatar screens at 1672×941, 1440×900, 390×844 | headless Chrome (SwiftShader) against the local API + Vite |
| XR room in **Meta's emulator** (IWER 2.0.1, emulated Meta Quest 3), immersive session | **pass, 13 checks** (`web/scripts/xr-emulator-test.mjs`): Enter in VR starts a real `immersive-vr` session; seated 1.2 m eye height; poke Recenter / Captions / Reply / Not now / Leave with the index fingertip; point at the cup and pinch (voice note plays, badge → *Recorded message*); hand tracking lost and re-acquired (nothing fires); controller ray + trigger on *Play our day*; seated-view layout clear; no page errors. It found and fixed five XR bugs — see `docs/EMULATOR.md` | headless Chrome on this PC's GPU (Intel, ANGLE/D3D11), about 110 fps *desktop emulation* — **not** Quest performance |
| New-user journey (`web/scripts/onboarding-e2e.mjs`) | **pass, 18 checks at 1440×900 and 390×844**: account → first-run setup (you, room, invite link with copy/share, avatar choice, usual times) → "waiting for someone" states on Our day, Together and Share → per-room settings → the invitee opens the link, creates an account and lands in the room → the owner sees them in Together; no page scrolls sideways | headless Chrome against the local API |
| Visual QA pass 2 (8 Nov) | Neutral white balance, satin wood, cooler window plate, quieter dock/panels, demo avatars with human-sized irises and matte skin; honest sample-media labels (*Simulated camera pan*, *Synthesized voice note*, *Sample photo*); Settings fits 1440 px. See `docs/VISUAL_QA.md` | headless Chrome (SwiftShader) via `web/scripts/capture.mjs` |
| Visual enhancement pass (room + companion) | Room safe area (screen header, primary action, presence badge, cup label, onboarding hint, dock fully visible and not overlapping) **passes at 1672×941, 1440×900, 1024×768 and 390×844**; 94–101 draw calls and 122k–136k triangles including the shadow pass; loading, asset-error and sign-in states; companion light/dark themes; populated Share with a circle selected (not published); pointer smoke test of the cup, dock Reply, Captions and Our day passes (`web/scripts/room-interactions.mjs`). See `docs/VISUAL_QA.md` | headless Chrome (SwiftShader) via `web/scripts/capture.mjs`; frame rates there are not meaningful |
| Sample media (12 Nov) | The sample day's four photos and its clip are now real stock photographs and footage from Wikimedia Commons (CC0, public domain, CC BY), credited in the apps; captions and the recap's "looks like" lines rewritten to match what they show; labels *Sample photo* and *Sample clip* | type check, unit tests and production build pass; the room and companion captures were **not** re-taken |

## Not yet verified (needs hardware, credentials or deployment)

- **Nothing is hosted.** There is no public URL for the web room, the companion or the API, so the hosted route of
  the competition (a link judges open in the Quest Browser) isn't available yet; see
  [REQUIREMENTS_MAP.md](REQUIREMENTS_MAP.md) for where each submission requirement stands.

- **Photo-to-avatar generation** with the real MetaPerson service (needs Avatar SDK client credentials on the Pro
  plan), including MetaPerson’s actual bone and blendshape names in a real export.
- **Likeness acceptance**: two distinct consenting adults, private front/three-quarter/side review, recorded
  approvals. Generic presets and the test mannequin do not count.
- **A genuine phone photo and an independently recorded camera/action-camera clip** in the private end-to-end
  import test (only generated fixtures and the sample clip were used).

- **Quest 3/3S hardware**: immersive session, hand tracking (pinch select / pinch grab), poke on the dock,
  tracking-loss behaviour, recenter, spatial audio placement, and the 60 fps minimum — now also with the PMREM
  image-based light, Neutral tone mapping, the one soft shadow light, the sculpted characters and the headset
  (table-space) layout of the screen and dock. The input wiring and layout now pass in Meta's emulator
  (`docs/EMULATOR.md`), which cannot measure device frame time, comfort, real hand-tracking quality or spatial
  audio. Use `?debug=1` in the room to show fps and draw calls in the HUD; record device, browser version and
  scene for the submission.
- **AI recap with Claude**: the adapter (`api/samesky/services/ai.py`, model `claude-opus-5-5`, structured JSON
  output, server-side refusal fallback) has not been called — no `ANTHROPIC_API_KEY` was configured. The
  chronological fallback path and the validation of model drafts (including a malicious draft) are tested.
- **Web Push round trip**: subscription storage, generic payloads and invalid-endpoint cleanup are implemented,
  but no real background push was delivered yet. It needs VAPID keys (`scripts/gen_vapid.py`), HTTPS, and a
  supported browser (e.g. desktop Chrome/Edge or Android Chrome). Quest Browser push support is not assumed.
- **Two-headset live session**: the realtime protocol is tested with two programmatic clients; playback drift
  between two real headsets over Wi-Fi has not been measured (the room shows drift with `?debug=1`).
- **Postgres / Supabase**: the app runs on SQLAlchemy; Postgres is the intended production database but only
  SQLite was exercised. `api/migrations/sql/postgres_rls_lockdown.sql` is provided but has not been applied.
- **Real camera/microphone capture** in the companion (MediaRecorder) and an independently exported
  action-camera file from a real device; the pipeline was tested with FFmpeg-generated H.264 (rotation
  metadata) and MJPEG (rejected) fixtures plus an EXIF-rotated JPEG with GPS data (stripped).

## P0 scope notes

- In-headset text path is quick-reply phrases + reactions (no in-world keypad yet).
- Daily draft scheduling (`rooms.daily_draft_time`) and automatic direct-room call suggestions run in the
  worker's periodic loop; they are implemented but not covered by automated tests.
- Live camera/audio (WebRTC), TURN, 360°/stereo footage, mixed-reality table placement, localization and
  more than two live avatars are P1 and **not implemented**.
- The room bundle is ~1.5 MB gzipped because IWSDK's entry point pulls in UI fonts and physics we don't use;
  code-splitting is a known optimization. Critical room art is about 6.3 MB (see `docs/ASSET_MANIFEST.md`).
- People appear as their **owner-approved photo-derived avatar** (skinned GLB, per-participant skeleton clone,
  seated legs, two-bone IK on real bones, blinks only when the model has blink morphs) or, otherwise, a labelled
  **demo avatar**. Demo avatars are original procedural sculpts with rigid parts (two-bone IK arms, blinks,
  breathing, rest poses); they are not anyone’s likeness and are labelled “demo avatar” in live rooms.
- Tracked hand *orientation* isn’t applied to photo-derived hands yet (positions are).
