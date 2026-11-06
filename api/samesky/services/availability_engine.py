"""Deterministic, time-zone-aware availability overlap engine (no I/O, fully unit-testable).

Policy for wall-clock times that do not map to exactly one instant (documented in docs/AVAILABILITY.md):

* Skipped wall-clock times (DST gap, e.g. 02:30 on a spring-forward night) resolve to the first instant
  whose local wall-clock time is at or after the requested time — i.e. the moment the clock jumps.
  A window lying entirely inside a gap therefore covers no time.
* Repeated wall-clock times (DST fold, e.g. 01:30 twice on a fall-back night) resolve to the *first*
  occurrence for a window start and the *second* occurrence for a window end, so a declared window
  never silently shrinks.

Recurring windows are expanded in each person's own IANA zone, converted to UTC, then busy and quiet
intervals are removed, every selected participant is intersected, and short overlaps are dropped.
"""

from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass, field
from datetime import UTC, date, datetime, time, timedelta
from zoneinfo import ZoneInfo

Interval = tuple[datetime, datetime]


def parse_hhmm(value: str) -> time:
    try:
        hours, minutes = value.split(":")
        h, m = int(hours), int(minutes)
    except (ValueError, AttributeError):
        raise ValueError(f"invalid local time {value!r}; expected HH:MM") from None
    if not (0 <= h <= 23 and 0 <= m <= 59):
        raise ValueError(f"invalid local time {value!r}")
    return time(h, m)


def _wall(instant: datetime, zone: ZoneInfo) -> datetime:
    return instant.astimezone(zone).replace(tzinfo=None)


def resolve_wall_time(local: datetime, zone: ZoneInfo, edge: str) -> datetime:
    """Map a naive local wall-clock time to a UTC instant using the documented gap/fold policy."""
    assert local.tzinfo is None
    exact = []
    for fold in (0, 1):
        candidate = local.replace(tzinfo=zone, fold=fold).astimezone(UTC)
        if _wall(candidate, zone) == local:
            exact.append(candidate)
    if exact:
        return min(exact) if edge == "start" else max(exact)
    # DST gap: binary-search the first instant whose wall time >= local (the transition instant).
    a = local.replace(tzinfo=zone, fold=0).astimezone(UTC)
    b = local.replace(tzinfo=zone, fold=1).astimezone(UTC)
    lo, hi = (a, b) if a < b else (b, a)
    lo_s, hi_s = int(lo.timestamp()), int(hi.timestamp())
    while hi_s - lo_s > 1:
        mid = (lo_s + hi_s) // 2
        if _wall(datetime.fromtimestamp(mid, UTC), zone) >= local:
            hi_s = mid
        else:
            lo_s = mid
    return datetime.fromtimestamp(hi_s, UTC)


def merge(intervals: list[Interval]) -> list[Interval]:
    result: list[Interval] = []
    for start, end in sorted(i for i in intervals if i[1] > i[0]):
        if result and start <= result[-1][1]:
            result[-1] = (result[-1][0], max(result[-1][1], end))
        else:
            result.append((start, end))
    return result


def subtract(base: list[Interval], removals: list[Interval]) -> list[Interval]:
    result = []
    removals = merge(removals)
    for start, end in merge(base):
        cursor = start
        for r_start, r_end in removals:
            if r_end <= cursor or r_start >= end:
                continue
            if r_start > cursor:
                result.append((cursor, r_start))
            cursor = max(cursor, r_end)
            if cursor >= end:
                break
        if cursor < end:
            result.append((cursor, end))
    return result


def intersect(a: list[Interval], b: list[Interval]) -> list[Interval]:
    a, b = merge(a), merge(b)
    i = j = 0
    out = []
    while i < len(a) and j < len(b):
        start = max(a[i][0], b[j][0])
        end = min(a[i][1], b[j][1])
        if end > start:
            out.append((start, end))
        if a[i][1] < b[j][1]:
            i += 1
        else:
            j += 1
    return out


@dataclass(frozen=True)
class WeeklyRule:
    weekday: int  # 0 = Monday, in the person's local zone
    start: str
    end: str  # end <= start => overnight (end == start => a full 24 hours)


@dataclass
class ParticipantSchedule:
    user_id: str
    zone: str
    rules: list[WeeklyRule] = field(default_factory=list)
    busy: list[Interval] = field(default_factory=list)
    extra: list[Interval] = field(default_factory=list)
    quiet_start: str | None = None
    quiet_end: str | None = None
    min_minutes: int = 15
    rule_versions: tuple[int, ...] = ()


def _daterange(first: date, last: date):
    d = first
    while d <= last:
        yield d
        d += timedelta(days=1)


def expand_weekly(rules: list[WeeklyRule], zone_name: str, window: Interval) -> list[Interval]:
    zone = ZoneInfo(zone_name)
    w_start, w_end = window
    first = _wall(w_start, zone).date() - timedelta(days=1)  # catch overnight windows from the previous day
    last = _wall(w_end, zone).date()
    out: list[Interval] = []
    for day in _daterange(first, last):
        for rule in rules:
            if rule.weekday != day.weekday():
                continue
            s_t, e_t = parse_hhmm(rule.start), parse_hhmm(rule.end)
            end_day = day if e_t > s_t else day + timedelta(days=1)
            start = resolve_wall_time(datetime.combine(day, s_t), zone, "start")
            end = resolve_wall_time(datetime.combine(end_day, e_t), zone, "end")
            start, end = max(start, w_start), min(end, w_end)
            if end > start:
                out.append((start, end))
    return merge(out)


def expand_daily(start: str, end: str, zone_name: str, window: Interval) -> list[Interval]:
    return expand_weekly([WeeklyRule(d, start, end) for d in range(7)], zone_name, window)


def free_intervals(p: ParticipantSchedule, window: Interval) -> list[Interval]:
    available = merge(expand_weekly(p.rules, p.zone, window) + [(max(s, window[0]), min(e, window[1])) for s, e in p.extra])
    blocked = list(p.busy)
    if p.quiet_start and p.quiet_end:
        blocked += expand_daily(p.quiet_start, p.quiet_end, p.zone, window)
    return subtract(available, blocked)


def common_overlaps(participants: list[ParticipantSchedule], now: datetime, days: int = 7, min_minutes: int | None = None) -> list[Interval]:
    if len(participants) < 2:
        return []
    window = (now, now + timedelta(days=days))
    required = timedelta(minutes=min_minutes if min_minutes is not None else max(p.min_minutes for p in participants))
    common = free_intervals(participants[0], window)
    for p in participants[1:]:
        common = intersect(common, free_intervals(p, window))
    return [(s, e) for s, e in common if e - s >= required]


def is_blocked_now(p: ParticipantSchedule, now: datetime) -> bool:
    window = (now - timedelta(days=1), now + timedelta(days=1))
    blocked = list(p.busy)
    if p.quiet_start and p.quiet_end:
        blocked += expand_daily(p.quiet_start, p.quiet_end, p.zone, window)
    return any(s <= now < e for s, e in merge(blocked))


def schedule_fingerprint(participants: list[ParticipantSchedule]) -> str:
    canonical = [
        {
            "u": p.user_id,
            "z": p.zone,
            "r": sorted((r.weekday, r.start, r.end) for r in p.rules),
            "b": sorted((s.isoformat(), e.isoformat()) for s, e in p.busy),
            "x": sorted((s.isoformat(), e.isoformat()) for s, e in p.extra),
            "q": [p.quiet_start, p.quiet_end],
            "m": p.min_minutes,
        }
        for p in sorted(participants, key=lambda p: p.user_id)
    ]
    return hashlib.sha256(json.dumps(canonical, sort_keys=True).encode()).hexdigest()


def local_label(instant: datetime, zone_name: str) -> dict:
    local = instant.astimezone(ZoneInfo(zone_name))
    return {
        "zone": zone_name,
        "local": local.isoformat(),
        "date": local.date().isoformat(),
        "time": local.strftime("%H:%M"),
        "weekday": local.strftime("%A"),
        "abbreviation": local.tzname(),
    }
