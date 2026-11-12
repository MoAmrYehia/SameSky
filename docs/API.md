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
| Rooms | `GET/POST /rooms`, `GET/PATCH /rooms/{id}` (mute, label, daily draft time), `POST /rooms/{id}/invites` (link token and short code, see [Invitations](#invitations)), `DELETE /rooms/{id}/invites/{inviteId}`, `POST /invites/redeem` (`token`), `POST /invites/redeem-code` (`code`), `DELETE /rooms/{id}/members/{userId or "me"}` |
| Media imports | `POST /media` (multipart: file, kind, source_device, projection, capture_at, trim_start_ms, trim_end_ms), `GET /media/{id}` (owner status), `POST /media/{id}/corrections` (rotation, capture time, trim), `GET /media/{id}/{display\|thumb\|ogg}?u&e&s` (short-lived signed URL + live access check; `ogg` see [Voice notes on native clients](#voice-notes-on-native-clients)). `source_device` is one of `phone`, `tablet`, `webcam`, `laptop`, `camera`, `action_camera`, `drone`, `glasses`, `headset` (recorded in the Quest app) or `other`; it is a label the person chooses |
| Moments & audiences | `POST/GET /rooms/{id}/moments`, `GET/PATCH/DELETE /moments/{id}`, `POST /moments/{id}/audience/resolve`, `POST /moments/{id}/share`, `POST /moments/{id}/withdraw` (`all_copies`), `POST /moments/{id}/republish` |
| Social | `GET/POST /moments/{id}/replies`, `POST /moments/{id}/reactions`, `GET/POST /rooms/{id}/objects`, `PATCH /objects/{id}` |
| Recaps | `POST /rooms/{id}/recaps` (`room_date`, optional `recipient_ids`), `GET /rooms/{id}/recaps` (archive), `GET /rooms/{id}/recaps/pending`, `GET /recaps/{id}`, `POST /recaps/{id}/approve` |
| Awards | `GET /rooms/{id}/awards` |
| Furniture marketplace | `GET /furniture/catalog` (slots, pieces, finishes and ready-made looks; every piece is included, nothing is priced), `GET /rooms/{id}/decor`, `PUT /rooms/{id}/decor` (`choices`: slot → `{item, finish}`, optional `expected_version`). See [Furniture](#furniture) |
| Availability | `GET /rooms/{id}/availability`, `PUT /rooms/{id}/availability/rules`, `POST /availability/exceptions`, `DELETE /availability/exceptions/{id}`, `POST/DELETE /rooms/{id}/free-now`, `POST /rooms/{id}/opportunities` (selected `participant_ids`), `POST /rooms/{id}/opportunities/suggest`, `POST /opportunities/{id}/respond` (`connect`/`later`/`not_now`/`pause`) |
| Notifications | `GET /notifications`, `POST /notifications/{id}/read`, `GET /push/vapid-public-key`, `POST /push/subscriptions`, `POST /push/subscriptions/delete` |
| Live viewing | `POST /rooms/{id}/sessions` (two participants who can both see the moment), `GET /rooms/{id}/sessions`, `GET /sessions/{id}`, `POST /sessions/{id}/end`, `WS /realtime` |
| Ops | `GET /health` |

## Furniture

The marketplace lets the people who share a room furnish it. The catalogue is one file
(`api/samesky/furniture_catalog.json`, bundled unchanged with the headset app): seven slots (table, chairs, rug,
lamp, plant, walls, floor), the pieces that can fill each one, the finishes a piece comes in, and three
ready-made looks. Nothing is priced, sold or owned: there are no purchases, balances or entitlements.

- `GET /rooms/{id}/decor` (members only) returns the room's choice for every slot, with the defaults filled in:

  ```json
  { "roomId": "…", "version": 3, "choices": { "table": { "item": "tripod", "finish": "oak" }, "rug": { "item": "none", "finish": null }, "…": {} },
    "updatedBy": "…", "updatedAt": "…" }
  ```

  A room nobody has furnished is at `version` 0 with the catalogue's defaults.
- `PUT /rooms/{id}/decor` (any active member) changes the slots it names and leaves the others. A finish left out
  means the piece's first finish. Unknown slots, pieces or finishes are refused with 422 (`unknown_slot`,
  `unknown_item`, `unknown_finish`) and nothing changes. With `expected_version`, a stale version gets 409
  `version_conflict`: load the room again and re-apply.
- People in a live viewing session in that room receive `{ "type": "decor_changed", "roomId": "…" }` on the
  WebSocket and ask for the decor again with their own authorization.
- A piece or finish that is later removed from the catalogue falls back to the slot's default when read.

## Invitations

`POST /rooms/{id}/invites` (room owner only) returns both ways to join, each shown once:

```json
{ "id": "…", "token": "…", "code": "K7PQ-4XMR", "expiresAt": "…", "path": "/join/…" }
```

- **`token`** goes in the invite link. Redeem it with `POST /invites/redeem` `{ "token": "…" }`.
- **`code`** is for typing by hand, for example on the headset's keypad: 8 characters from an alphabet without
  look-alikes (no I, L, O, 0 or 1), shown as `XXXX-XXXX`. Redeem it with `POST /invites/redeem-code`
  `{ "code": "K7PQ-4XMR" }`. Case, spaces and dashes don't matter. An optional `label` (40 characters) is also
  accepted.

Both need a signed-in account, return the room, and redeem the same single-use invitation: whichever is used
first wins. A retry by the person who redeemed it succeeds again; anyone else gets an error. Errors are `409 invite_used`,
`invite_revoked`, `invite_expired`, `already_member` and `room_full`. Codes also have:

- `404 invite_code_unknown` when the code matches nothing;
- `429 too_many_attempts` after 8 codes that matched nothing in 15 minutes for that account.

Only hashes are stored: SHA-256 for tokens, and an HMAC keyed with the server secret for codes.

## Voice notes on native clients

Unity's runtime can't decode AAC audio clips. Processed voice notes therefore also get an Ogg Vorbis copy, and a
moment's `media` then carries **`oggUrl`** next to `url`. It is a signed URL for the **`ogg`** variant of
`GET /media/{id}/{variant}`, bound to the viewer and authorized on every request like the original: withdrawal,
removal and forged links fail the same way. The copy is best effort. If the server's FFmpeg lacks libvorbis,
`oggUrl` is absent and the voice note still plays everywhere else.

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
