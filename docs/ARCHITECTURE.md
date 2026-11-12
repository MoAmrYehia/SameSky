# Architecture

```
 Quest Browser (WebXR)        Phone / desktop browser
 ┌──────────────────────┐     ┌──────────────────────────┐
 │ room.html (IWSDK)    │     │ index.html (React)       │
 │  src/xr/*            │     │  src/companion/*         │
 └─────────┬────────────┘     └────────────┬─────────────┘
           │ shared contracts + DataSource (src/shared/*)
           │  ├─ SampleSource: local, offline, resettable
           │  └─ LiveSource:   HTTPS /v1/*  +  WSS /v1/realtime
           ▼
 ┌──────────────────────────────────────────────────────────┐
 │ FastAPI (api/samesky)                                    │
 │  routers → services (access, rooms, moments, recaps,     │
 │  awards, availability, notifications, media) → SQLAlchemy │
 │  realtime.py: poses (ephemeral) + playback (authorized)  │
 └───────────────┬──────────────────────────────────────────┘
                 │ same database
 ┌───────────────▼──────────────┐   ┌───────────────────────┐
 │ worker.py (durable jobs)     │──▶│ FFmpeg (bounded)      │
 │ media.process, recap.generate│──▶│ Claude (optional)     │
 │ recap.daily, notify, scans   │──▶│ Web Push (VAPID)      │
 └──────────────────────────────┘   └───────────────────────┘
```

## Decisions and tradeoffs

| Decision | Why | Tradeoff |
|---|---|---|
| **IWSDK** for the room, Three.js via the `super-three` alias pinned to IWSDK's version | Official Meta WebXR stack: hand input, pinch grab, poke/ray interaction tags, spatial audio, IWER emulation | Large bundle (IWSDK entry imports UI kit + physics) |
| **Canvas-texture panels** instead of UIKitML | Crisp text at seated distance, one draw call per panel, no compile step, full control over legibility and hit areas | Hand-rolled layout |
| **Grab = activate** for table objects | The brief's defining interaction: *picking up* the cup plays the voice note. Also selectable by distant pinch / click | Objects snap back onto the table surface on release (clamped, persisted with version check) |
| **API-enforced authorization** (SQLAlchemy, SQLite dev / Postgres prod) instead of direct Supabase client access | Every rule (grant + generation, consent, versions) lives in one tested Python module used by API *and* worker; testable without Docker or cloud credentials | Supabase is optional (JWT verification + Postgres); client roles are locked out with RLS (see `api/migrations/sql/postgres_rls_lockdown.sql`) |
| **App-owned WebSocket** for co-presence | Authenticated per message, membership re-checked on every playback command, kicks on removal/withdrawal, ephemeral poses never persisted | Single-process hub; multi-instance deployment needs sticky sessions or a pub/sub layer |
| **Durable jobs table** + polling worker with leases | Restart-safe, idempotent keys, expiry, retries with backoff, no extra infrastructure | Poll latency (1–2 s) |
| **Media on a private server volume** + signed URLs that re-check access | Withdrawal blocks the *same* still-valid URL immediately | Needs a persistent volume (or move to a private object store behind the API) |
| **Deterministic availability engine** in Python, ported to TypeScript for the offline sample | Server is authoritative; sample works without a backend; both tested against the brief's fixture and DST cases | Two implementations to keep in sync |
| **AI only drafts** (Claude via official SDK, structured JSON) | Model receives server-selected, authorized sources; output is validated; contributors approve | Without a key, recaps are chronological (clearly labelled) |
| **Photo-derived avatars through a server-side provider adapter** (Avatar SDK MetaPerson REST), skinned GLB in the room via three.js | Keys stay on the server; private sources, durable jobs, owner approval of an exact version, viewer-bound signed model URLs re-authorized per download; no native SDK in the WebXR build | Generation needs the provider’s paid API plan; one-photo likeness has limits; bone names are mapped by convention (see `docs/AVATARS.md`) |

## Data model

See `api/samesky/models.py` (and `api/migrations/versions/*`). Key ideas:

- `memberships.generation` increments on every rejoin. `recipient_grants` store the generation at
  publication time. A read requires an unrevoked grant **and** an active membership with the same generation.
- `audience_snapshots` freeze who a moment/recap was shared with (and whether it covered the whole room,
  which awards use).
- `recaps` carry a cache key over room, audience + generations, source ids/versions and model config.
- `jobs` is the outbox: `idempotency_key` unique, `lease_until` for crash recovery, `expires_at`.

## Realtime protocol (summary)

Client → server: `auth`, `join {sessionId}`, `pose {head,left,right,tracked,source}` (≤25/s accepted, range
checked, table-relative), `playback {action, positionMs, baseVersion}`, `ping {t}`, `reaction`, `leave`.
Server → client: `welcome {state, participants+avatars}`, `state {version, playheadMs, paused, stateAt,
serverTime}`, `pose`, `peer_joined/left`, `avatar_changed {userId}` (re-fetch with your own authorization; no
likeness data in the broadcast), `pong` (clock offset), `removed`, `session_ended`.
Clients estimate the server clock from the lowest-RTT pong and correct playback when drift > 300 ms.
