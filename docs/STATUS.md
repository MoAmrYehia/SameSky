# Status — implemented vs. verified

Date: 6 November 2026. This file keeps the brief's distinction between *implemented*, *tested on a headset*,
*tested in an emulator/desktop*, and *still unverified*. Nothing below is claimed beyond what was run.

## Verification performed

| Check | Result | Where |
|---|---|---|
| API automated tests (55) | **pass** — privacy/authorization, withdrawal, removal, idempotency, plan validation, awards, availability/DST, Free now, call suggestions, restart persistence, media pipeline, realtime sync, avatar face preset validation | `api/tests`, local Windows 11, Python 3.13 |
| Migrations | `alembic upgrade head` + `alembic check` (no drift) on SQLite | local |
| Web type check / unit tests / production build | **pass** (TS engine tests: 4) | local, Node 26 |
| End-to-end smoke against the running stack (API + worker + Vite proxy) | **pass**: signup ×3, direct room + invite, phone photo upload → worker processing → explicit share confirmation, outsider 404, recap drafted → both approve → shared, reply + recap notifications in inbox (generic text), First Shared Day award, live session: play command reached the second participant in ~10 ms (same machine) and pose relay worked | `scripts/smoke_live.py` |
| Companion, sample mode | Our day, story player (source chips, interpretation labels, chapter progress), Share, Together rendered and exercised | desktop Chromium (in-app browser), 1440×900 and 800 px |
| Companion, live mode | Our day and Together against the real API (real overlap computed server-side) | desktop Chromium |
| XR room, desktop (non-immersive) | Scene renders; cup → spatial voice note + captions + "Recorded message" avatar state; reply sheet → persistent note on the table survives reload; dock; keepsakes; 56–65 fps, ~100 draw calls | desktop Chromium with IWER (Meta Quest 3 emulation) injected; **pointer input exercised via synthetic events**, not a real headset |
| Visual enhancement pass (room + companion) | Room safe area (screen header, primary action, presence badge, cup label, onboarding hint, dock fully visible and not overlapping) **passes at 1672×941, 1440×900, 1024×768 and 390×844**; 94–101 draw calls and 122k–136k triangles including the shadow pass; loading, asset-error and sign-in states; companion light/dark themes; populated Share with a circle selected (not published); pointer smoke test of the cup, dock Reply, Captions and Our day passes (`web/scripts/room-interactions.mjs`). See `docs/VISUAL_QA.md` | headless Chrome (SwiftShader) via `web/scripts/capture.mjs`; frame rates there are not meaningful |

## Not yet verified (needs hardware, credentials or deployment)

- **Quest 3/3S hardware**: immersive session, hand tracking (pinch select / pinch grab), poke on the dock,
  tracking-loss behaviour, recenter, spatial audio placement, and the 60 fps minimum — now also with the PMREM
  image-based light, Neutral tone mapping, the one soft shadow light, the sculpted characters and the headset
  (table-space) layout of the screen and dock, which was only previewed with `room.html?preview=xr`.
  IWER sessions do not start in headless Chrome, so the emulated immersive path was not re-captured. Use `?debug=1` in the
  room to show fps and draw calls in the HUD; record device, browser version and scene for the submission.
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
- Characters are original procedural sculpts (no DCC tool was available): rigid parts with two-bone IK arms,
  blinks, breathing and rest poses, but no skinning or facial expressions yet. A Blender retopology/rig pass is
  the clearest next visual improvement (`docs/VISUAL_QA.md`).
