# Deployment

Three pieces: an HTTPS static site (the `web/dist` build), the API (`uvicorn samesky.main:app`), and one
persistent worker (`python -m samesky.worker`). They share one database and one media volume.

## 1. Database

- Development: SQLite at `api/data/samesky.db` (created automatically).
- Production: Postgres (Supabase Postgres works). Install a driver (`pip install "psycopg[binary]"`), set
  `DATABASE_URL=postgresql+psycopg://samesky_api:…@host:5432/postgres`, then:

```bash
cd api && alembic upgrade head
```

```bash
psql "$SUPERUSER_URL" -f api/migrations/sql/postgres_rls_lockdown.sql
```

  The script enables and forces RLS on every table and grants nothing to `anon`/`authenticated`; only the
  `samesky_api` role (used by the API and worker) has a policy. Create that role first
  (`create role samesky_api login password '…';` and grant it ownership/usage of the schema).

## 2. API

```bash
cd api && pip install -r requirements.txt && uvicorn samesky.main:app --host 0.0.0.0 --port 8000 --proxy-headers
```

Required environment: `SAMESKY_SECRET_KEY` (long random), `DATABASE_URL`, `SAMESKY_MEDIA_ROOT` (persistent
volume), `SAMESKY_CORS_ORIGINS` (your web origin). Optional: `ANTHROPIC_API_KEY` (+ `SAMESKY_AI_MODEL`),
`VAPID_PUBLIC_KEY`/`VAPID_PRIVATE_KEY`/`VAPID_SUBJECT`, `SUPABASE_JWT_SECRET`, `SAMESKY_PUBLIC_API_URL` (only if
the web app and API are on different origins), and for photo-derived avatars `METAPERSON_CLIENT_ID`/
`METAPERSON_CLIENT_SECRET` (+ the `SAMESKY_AVATAR_*` settings in `.env.example`; set them on the worker too, see
`docs/AVATARS.md`). Private avatar source photos and models live under `SAMESKY_MEDIA_ROOT/avatar-private/`,
outside every static path; back that volume up like media and include it in deletion requests. WebSockets must be allowed through the proxy for `/v1/realtime`;
run a single API instance (the realtime hub is in-process) or add sticky sessions.

## 3. Worker (durable, restartable)

```bash
cd api && python -m samesky.worker
```

- Run exactly as a long-lived service (systemd, a container with `restart: always`, Fly/Render worker…).
- Restart at any time: running jobs hold a 60 s lease; after a crash the lease expires and the job is
  reclaimed. Every handler re-validates its preconditions (membership, grants, consent, versions, expiry), so a
  retry never duplicates a notification, award or recap and never delivers a stale call suggestion.
- Failed jobs retry with exponential backoff (5 s … 5 min) up to their attempt limit, then stay `failed` with a
  safe error code. Inspect with `select kind, state, attempts, last_error from jobs order by updated_at desc`.
- `python -m samesky.worker --once` processes everything runnable and exits (useful for cron-style hosting).

## 4. Web (static HTTPS)

```bash
cd web && npm ci && VITE_API_URL=https://api.example.com npm run build
```

Serve `web/dist` over HTTPS (WebXR, camera/microphone and push all require a secure context). If the API is
served from the same origin under `/v1`, leave `VITE_API_URL` empty. `room.html` is the WebXR entry;
`index.html` is the companion. Keep the judged build at a fixed URL and do not change it during judging.

## 5. Web Push

```bash
api/.venv/Scripts/python scripts/gen_vapid.py
```

Put the two keys and `VAPID_SUBJECT=mailto:…` in the API/worker environment. Users enable push only by pressing
**Enable notifications** in Settings; payloads are generic. Verify one real round trip on an identified browser
(e.g. desktop Chrome): enable, then have another account reply to their moment while the tab is closed.

## 6. Local headset testing

`npm run dev -- --mode https` serves the Vite dev server with a self-signed certificate on your LAN; open
`https://<pc-ip>:5173/room.html?mode=sample` in Quest Browser. Add `&debug=1` to show fps/draw calls.
