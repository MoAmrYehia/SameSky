# Availability and call opportunities

Time zones tell us how to display time; they do not prove that someone is free.

## Inputs (per person, per room)

- Current IANA time zone (detected on the device, confirmed by the user; changed manually when travelling —
  no GPS). Stored timestamps are UTC, so history is unaffected by a zone change.
- Weekly windows in local wall-clock time (`weekday 0=Monday`, `start`, `end`; end ≤ start = overnight,
  end = start = 24 h). Scoped to one room.
- Busy / travel exceptions (UTC intervals; may apply to all rooms) and extra free windows.
- Quiet hours (local, may wrap midnight).
- Preferred call length (minimum 15 minutes).
- Free now: expires 30 minutes after it is set, scoped to one room.
- Whether availability hints may be shared (if off, others cannot match against you).

## Algorithm (`api/samesky/services/availability_engine.py`, mirrored in `web/src/shared/tz.ts`)

1. Choose the authorized participant set (explicitly selected for circles).
2. Expand each person's weekly windows over the next 7 days *in their own zone*, starting one local day early
   to catch overnight windows.
3. Resolve wall-clock times to instants with the documented DST policy (below), i.e. convert to UTC.
4. Remove busy/travel exceptions and quiet hours (also expanded in the person's zone).
5. Intersect every participant's free intervals; keep overlaps ≥ the largest preferred length.
6. Show each candidate in every participant's local time with weekday and city.
7. Recompute on rule, zone, quiet-hour, exception or membership changes; pending deliveries are re-validated
   and cancelled if the overlap disappeared.

Fixture (tested in both languages): 17:00–18:00 Asia/Dubai and 21:00–22:00 Asia/Shanghai are the same
13:00–14:00 UTC on 6 November 2026.

## Daylight-saving policy (explicit, tested)

- **Skipped times** (spring-forward gap, e.g. 02:30 New York on 8 March 2026) resolve to the first instant
  whose wall-clock time is at or after the requested time — the transition instant. A window that lies entirely
  inside a gap covers no time.
- **Repeated times** (fall-back fold, e.g. 01:30 New York on 1 November 2026) resolve to the *first*
  occurrence for a window start and the *second* occurrence for a window end, so a declared window never
  silently shrinks.

## Evidence language (server-generated)

| Evidence | Message |
|---|---|
| Declared schedules overlap | “Your chosen schedules overlap for N minutes.” |
| Every selected participant has an unexpired Free now in this room and is outside busy/quiet time | “You're both marked free now. Want to connect?” / “Maya and Lina and you are all marked free now…” |
| Presence only | Never described as availability |

## Suggestions and delivery

- Direct rooms: the worker proposes the next overlap within 6 hours. Circles: only when a member explicitly
  suggests a computed time to selected participants; only those people are notified.
- Limits: at most 2 suggestions per person per day across rooms, at least 4 hours apart; muted rooms, paused
  suggestions or opt-out skip delivery. Each opportunity is delivered once per recipient and expires when the
  overlap ends — including after a worker crash/restart (tested).
- Actions: Connect (opens the room, or hands off to the user's own validated `https`/`facetime`/`tel`/`whatsapp`
  link — never simulates a call), Later, Not now, Pause suggestions (7 days).
