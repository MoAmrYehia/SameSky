# API

Base path `/v1`. JSON in and out; `Authorization: Bearer <token>` on every room operation — the actor is
always derived from the verified token, never from a supplied user id. The full machine-readable contract is
[`contracts/openapi.json`](../contracts/openapi.json) (regenerate with `scripts/export_contracts.py`); the AI
recap contract is [`contracts/recap-plan.schema.json`](../contracts/recap-plan.schema.json). TypeScript types
for the same shapes live in `web/src/shared/contracts.ts`.

## Errors

```json
{ "error": { "code": "confirmation_required", "message": "Confirm the exact recipients before sharing.", "retryable": false } }
```

`404 not_found` is used both for "doesn't exist" and "not yours to see". Validation errors list field paths
only (never echo submitted content). Retry-safe mutations use idempotency keys or unique constraints
(replies accept `client_key`; invite redemption, recap requests, notification delivery and awards are idempotent).
Concurrent object moves use optimistic versioning (`expected_version` → `409 version_conflict`).

## Endpoints

| Area | Endpoints |
|---|---|
| Identity | `POST /auth/signup`, `POST /auth/login`, `GET/PATCH /me`, `PUT /me/avatar` (demo avatar definition), `GET /avatar-options` |
| Photo-derived avatars (owner-only; see `docs/AVATARS.md`) | `GET /me/avatar-studio`, `POST /me/avatar-consents`, `POST /me/avatar-consents/{id}/withdraw`, `POST /me/avatar-sources` (multipart: file, view, consent_id), `DELETE /me/avatar-sources/{id}`, `POST /me/avatar-jobs` (source_ids, consent_id, options, idempotency_key), `GET /me/avatar-jobs/{id}`, `POST /me/avatar-jobs/{id}/cancel`, `POST /me/avatar-jobs/{id}/retry`, `POST /me/avatar-assets` (multipart GLB import: file, consent_id, attested), `POST /me/avatar-assets/{id}/approve` (visibility, confirmed), `POST /me/avatar-assets/{id}/reject`, `POST /me/avatar-assets/{id}/activate`, `PATCH /me/avatar-assets/{id}` (visibility), `DELETE /me/avatar-assets/{id}`, `POST /me/avatar/demo`, `DELETE /me/avatar-data` |
| Seeing someone’s avatar | `GET /users/{id}/avatar` (as this viewer may see it: the demo definition, or the approved version with a viewer-bound signed `modelUrl`), `GET /avatar-assets/{id}/model?u&e&s` (signed, re-authorized on every request), `GET /avatar-sources/{id}/image?u&e&s` (owner only, `no-store`) |
| Rooms | `GET/POST /rooms`, `GET/PATCH /rooms/{id}` (mute, label, daily draft time), `POST /rooms/{id}/invites`, `DELETE /rooms/{id}/invites/{inviteId}`, `POST /invites/redeem`, `DELETE /rooms/{id}/members/{userId or "me"}` |
| Media imports | `POST /media` (multipart: file, kind, source_device, projection, capture_at, trim_start_ms, trim_end_ms), `GET /media/{id}` (owner status), `POST /media/{id}/corrections` (rotation, capture time, trim), `GET /media/{id}/{display\|thumb}?u&e&s` (short-lived signed URL + live access check) |
| Moments & audiences | `POST/GET /rooms/{id}/moments`, `GET/PATCH/DELETE /moments/{id}`, `POST /moments/{id}/audience/resolve`, `POST /moments/{id}/share`, `POST /moments/{id}/withdraw` (`all_copies`), `POST /moments/{id}/republish` |
| Social | `GET/POST /moments/{id}/replies`, `POST /moments/{id}/reactions`, `GET/POST /rooms/{id}/objects`, `PATCH /objects/{id}` |
| Recaps | `POST /rooms/{id}/recaps` (`room_date`, optional `recipient_ids`), `GET /rooms/{id}/recaps` (archive), `GET /rooms/{id}/recaps/pending`, `GET /recaps/{id}`, `POST /recaps/{id}/approve` |
| Awards | `GET /rooms/{id}/awards` |
| Availability | `GET /rooms/{id}/availability`, `PUT /rooms/{id}/availability/rules`, `POST /availability/exceptions`, `DELETE /availability/exceptions/{id}`, `POST/DELETE /rooms/{id}/free-now`, `POST /rooms/{id}/opportunities` (selected `participant_ids`), `POST /rooms/{id}/opportunities/suggest`, `POST /opportunities/{id}/respond` (`connect`/`later`/`not_now`/`pause`) |
| Notifications | `GET /notifications`, `POST /notifications/{id}/read`, `GET /push/vapid-public-key`, `POST /push/subscriptions`, `POST /push/subscriptions/delete` |
| Live viewing | `POST /rooms/{id}/sessions` (two participants who can both see the moment), `GET /rooms/{id}/sessions`, `GET /sessions/{id}`, `POST /sessions/{id}/end`, `WS /realtime` |
| Ops | `GET /health` |

## Realtime (`/v1/realtime`)

1. Connect, then send `{"type":"auth","token":"…"}` and `{"type":"join","sessionId":"…"}` (tokens are never
   put in URLs).
2. Receive `welcome` with the authoritative playback state and both participants' avatars, each as this viewer
   may see it. `peer_joined` and `avatar_changed` (sent when someone approves, replaces, stops sharing, deletes
   or edits their avatar) carry no likeness information; clients re-fetch `GET /users/{id}/avatar`.
3. Send `pose` at ~15 Hz (table-relative metres/quaternions; out-of-range or excess updates are dropped),
   `playback` commands, `ping` for clock sync, `reaction`, `leave`.
4. Every playback command re-checks membership and source visibility; removal or withdrawal closes the socket
   (`4403`/`4410`). Presence expires after 10 s without messages. Reconnecting clients get fresh state.
