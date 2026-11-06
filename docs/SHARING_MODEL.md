# Sharing and recipient model

## Rooms

| Kind | Capacity | Example |
|---|---|---|
| Direct room | 2 | Adam + Maya |
| Private circle | up to 6 including the creator (configurable `SAMESKY_CIRCLE_CAPACITY`) | Family circle |

Invitations are single-use, hashed server-side (`sha256`), expire after 72 h, can be revoked by the creator,
and redemption is idempotent for the same person. Only adults should be invited (stated in the join UI).

## Three separate decisions per moment

1. **Save privately** — creates the moment; only its owner can see it.
2. **Allow AI** (`ai_allowed`) — independent toggle, default off; withdrawing it invalidates dependent recaps.
3. **Share** — two steps:
   - `POST /v1/moments/{id}/audience/resolve` returns the exact recipients and a label
     (“Only Maya” or “Family circle: Lina, Maya”) plus a confirmation token bound to
     moment id + sharing version + sorted recipients.
   - `POST /v1/moments/{id}/share` must present the same recipients and token. A token issued for a
     narrower audience cannot be replayed to widen it.

Each shared moment belongs to one room/audience. Sharing with a different audience is a new, separately
confirmed publication (`/republish` creates a private copy in the target room that references the same asset).

## Access rule

A non-owner can read a moment, its media, replies, recaps that use it, notifications and table objects that
reference it only when **all** hold:

- an unrevoked `recipient_grant` exists for them in the resource's audience snapshot, and
- they have an **active** membership in the room whose `generation` equals the grant's generation.

Consequences (all covered by tests): room membership alone grants nothing historical; newly invited members
see no history; a removed member loses access immediately (including live sessions); rejoining bumps the
generation so revoked grants never return. Non-visible resources return `404 not_found` so existence never
leaks; a shared table never renders an object the viewer cannot see.

## Replies

Replies inherit the parent's audience and can never widen it. Notifications for replies go only to people who
can currently see the parent. “Reply privately” = use the direct room; nothing is copied automatically.

## Recaps

- Eligible sources: moments for the room date that are shared, AI-authorized, processed, and visible to
  **every** member of the target audience. A direct moment can never enter a circle recap.
- Up to six sources, at least one per contributor when the budget permits; omitted ones are disclosed.
- States: `queued → generating → private_preview → awaiting_approval → shared`, or `invalidated` / `failed`.
- Generation re-checks sources at start **and** finish; edits, withdrawal, AI-permission withdrawal or
  membership changes invalidate the recap (its text is deleted, notifications cancelled, award source
  links removed while the generic keepsake stays).
- Approval is per contributor and bound to a fingerprint of their source versions, the audience snapshot and
  the plan. Publication happens when every included contributor has approved; viewers whose content isn't
  included don't approve.
- Late contributions create a new version; a published recap is never silently altered.
- The cache key includes the audience and membership generations, so a partner-only plan can never be reused
  for a family audience.

## Awards

Per room, from server-verified room-wide events with unique keys:
`capsule:{room}:{date}` (one per room date) and `reply:{room}:{author}`. Awards are unique per
(room, milestone). Clients only display them.
