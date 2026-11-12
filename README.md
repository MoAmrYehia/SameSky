# SameSky

**Your day, side by side.** SameSky helps couples and families who live across distance, shifts and time
zones stay close. People share a few chosen moments from their day; an AI-assisted (or clearly labelled
chronological) recap turns the approved moments into a short shared story; and everyone meets around the
same virtual table on Meta Quest — where a voice note waits inside a cup until you pick it up, and your
reply waits beside it for the next visit.

Built for the Meta VR Start Developer Competition 2026 (Entertainment · New Experience): a native Quest app
(Unity, Meta XR SDK), a WebXR room built with Meta's Immersive Web SDK, and a phone/desktop companion, all on one
API. Hands only, seated. Checked in Meta XR Simulator and Meta's Immersive Web Emulator; **not yet run on a
headset** ([docs/NATIVE_STATUS.md](docs/NATIVE_STATUS.md), [docs/STATUS.md](docs/STATUS.md)).

| | |
|---|---|
| **XR room** (`web/room.html`) | Hands-first seated room on Quest 3/3S via Meta's Immersive Web SDK: pinch to pick up the cup (spatial voice note + captions), frames and postcards open on the shared screen, quick replies become persistent notes on the table, keepsake shelf, recenter / reset / captions / mute / exit within seated reach, each person's owner-approved photo-derived 3D avatar (or a labelled demo avatar), two-person live viewing with synchronized playback. Same page works on desktop/phone with click. |
| **Companion** (`web/index.html`) | React phone/desktop app: Our day (side-by-side recap, source-linked story player, waiting voice note, reply), Share a moment (upload / camera / voice / text, trim, explicit 1:1 or 1:many audience with confirmation, AI consent separate), Together (live clocks, deterministic overlap, Free now, quiet hours, call suggestions), Keepsakes + daily archive, Inbox, Settings (time zone; *Your avatar*: create from your photos or import a model, review in neutral light, approve, choose who sees it, stop sharing or delete; availability, push, rooms & invites). |
| **Native Quest app** (`native/quest/`) | Unity 6 with Meta's XR Core and Interaction SDK, on the same API as the companion: the seated lounge with the cup and its voice note, replies that stay on the table, Our day, Share, Together and Keepsakes. Tracked hands are drawn with skin (a tone you choose, nails, creases, their shadow on the table). In the **furniture marketplace** either person tries tables, chairs, rugs, lamps, plants, wall colours and floors on the real room, then keeps the look for both; every piece is included and nothing is sold. Checked in Meta XR Simulator with simulated hands and built as an APK; not yet run on a headset: [docs/NATIVE_STATUS.md](docs/NATIVE_STATUS.md). |
| **API** (`api/`) | FastAPI + SQLAlchemy (SQLite dev / Postgres prod). Explicit recipient grants bound to membership generations, audience snapshots, withdrawal/invalidation, recap generation + per-contributor approval, idempotent awards, time-zone engine, durable job outbox + worker, Web Push, app-owned realtime channel, and each room's furniture choices from one shared catalogue (versioned, so two people changing the room at once can't overwrite each other unnoticed). |
| **Sample day** | No login, no network calls to real rooms, no AI calls, no push. Fictional Adam (Dubai), Maya (Beijing) and Lina (London); real stock photos and footage (credited) and a synthesized voice; isolated local storage with a reset. |

## Quick start (Windows/macOS/Linux)

Prerequisites: Python 3.11+ and Node 20.19+ (FFmpeg is bundled through `imageio-ffmpeg`).

```bash
# API + worker
cd api
python -m venv .venv
.venv/Scripts/python -m pip install -r requirements-dev.txt     # macOS/Linux: .venv/bin/python
.venv/Scripts/python -m uvicorn samesky.main:app --port 8000
```

```bash
# in a second terminal: the durable background worker (media processing, recaps, notifications)
cd api
.venv/Scripts/python -m samesky.worker
```

```bash
# in a third terminal: the web app (companion + XR room), proxies /v1 to the API
cd web
npm install
npm run dev
```

Photo-to-avatar generation runs on the server through Avatar SDK MetaPerson and needs their API credentials
(Pro plan) in `.env` — see [docs/AVATARS.md](docs/AVATARS.md) and `.env.example`. Without them the app says
generation isn't set up, and people can import a rigged GLB made elsewhere or keep the labelled demo avatar.

Open http://localhost:5173 and choose **Try a sample day**, or open http://localhost:5173/room.html?mode=sample
for the room. On a Quest on the same network, run `npm run dev -- --mode https` and open
`https://<your-computer-ip>:5173/room.html?mode=sample` (accept the self-signed certificate), then **Enter in VR**.

Tests:

```bash
cd api && .venv/Scripts/python -m pytest -q
```

```bash
cd web && npm run typecheck && npm test && npm run build
```

```bash
api/.venv/Scripts/python scripts/smoke_live.py http://localhost:5173
```

With `npm run dev` running, these drive the real UI (playwright-core with the system Chrome):

```bash
cd web && node scripts/xr-emulator-test.mjs     # the room in Meta's emulator: hands, controllers, recenter, leave
```

```bash
cd web && node scripts/onboarding-e2e.mjs       # a new account's first run, inviting someone, the invitee joining
```

```bash
cd web && node scripts/avatar-e2e.mjs           # photo-derived avatar import, approval, live co-presence, stop sharing
```

## Documentation

- [docs/STATUS.md](docs/STATUS.md) — what is implemented, what was actually tested where, and what is still unverified
- [docs/NATIVE_QUICKSTART.md](docs/NATIVE_QUICKSTART.md) — the native Quest app (Unity, `native/quest/`): setup, local services, running it in Meta XR Simulator, building the APK
- [docs/NATIVE_STATUS.md](docs/NATIVE_STATUS.md) — the native app's results, kept apart by kind of evidence (compiled, simulator-tested, real-service-tested, APK-built, headset-tested)
- [docs/AVATARS.md](docs/AVATARS.md) — photo-derived avatars: journey, provider setup, privacy, rig contract, results table
- [docs/EMULATOR.md](docs/EMULATOR.md) — testing the room in Meta's Immersive Web Emulator (emulated Quest 3), what it found and fixed
- [docs/JUDGE_GUIDE.md](docs/JUDGE_GUIDE.md) — testing instructions for the judged flow
- [docs/demo/README.md](docs/demo/README.md) — the demo video "I Saved You a Seat" (2:11): real footage of the native app in Meta XR Simulator and the companion, how it was filmed and cut, its script and shot list, what is sample and what it leaves out
- [docs/DEMO_SCRIPT.md](docs/DEMO_SCRIPT.md) — the earlier plan for a recording on headsets with two accounts (not made)
- [docs/trailer/README.md](docs/trailer/README.md) — the animated concept trailer "I Saved You a Seat": how it was made, its script and shot list (a concept film rendered for the purpose, not footage of the app)
- [docs/cinematic/README.md](docs/cinematic/README.md) — "Wish You Were Here", a 75-second animated short that ends on the SameSky title: a film, not a demonstration (no app or interface in it); how it was made, its script, shot list and credits
- [docs/REQUIREMENTS_MAP.md](docs/REQUIREMENTS_MAP.md) — competition and brief requirements → where they are demonstrated
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — components, decisions and tradeoffs
- [docs/SHARING_MODEL.md](docs/SHARING_MODEL.md) — direct rooms, circles, recipient grants, recaps
- [docs/AVAILABILITY.md](docs/AVAILABILITY.md) — overlap algorithm and daylight-saving policy
- [docs/MEDIA.md](docs/MEDIA.md) — accepted input profiles and the processing pipeline
- [docs/API.md](docs/API.md) — endpoints, realtime protocol, errors ([contracts/openapi.json](contracts/openapi.json), [contracts/recap-plan.schema.json](contracts/recap-plan.schema.json))
- [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) — HTTPS hosting, API, worker, Postgres/RLS, push, restart and retry
- [docs/VISUAL_QA.md](docs/VISUAL_QA.md) — before/after captures, side-by-side comparisons with the concept art, remaining differences
- [docs/ASSET_MANIFEST.md](docs/ASSET_MANIFEST.md) — every shipped asset with origin, licence, size and how to regenerate it

## Repository layout

```
api/            FastAPI app (samesky/), worker, Alembic migrations, tests
web/            Vite: companion (src/companion), XR room (src/xr), shared contracts/data (src/shared)
native/quest/   Unity 6 project of the native Quest app (Meta XR Core and Interaction SDK, OpenXR, URP)
contracts/      OpenAPI + RecapPlan JSON schema (generated by scripts/export_contracts.py)
scripts/        asset pipeline (assets/), the native app's setup, tests, simulator run and build (native/),
                the videos (demo/), test fixtures (fixtures/), VAPID keys, contract export, smoke test
docs/           design, status and submission documents
```

All people in the sample are fictional. Sample photos are real photographs from Wikimedia Commons under CC0,
public-domain or CC BY terms (labelled *Sample photo*, with the photographer's credit); the sample clip is real
footage of Dubai Creek under CC BY 3.0 (labelled *Sample clip* and credited, never as the fictional person's own
recording); the voice note uses the operating system's text-to-speech voice
(labelled *Synthesized voice note*). The room's furniture, textures and light probe are CC0 Poly Haven assets,
and the window view is a CC0 photograph of a real river (the room makes no claim about where it is). The demo
characters are original, sculpted by a script in this repository; no real person's photo or likeness is in the
repository. Every file, its origin, licence and size is listed in
[docs/ASSET_MANIFEST.md](docs/ASSET_MANIFEST.md); captures and comparisons with the concept art are in
[docs/VISUAL_QA.md](docs/VISUAL_QA.md).
