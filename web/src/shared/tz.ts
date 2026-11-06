// Time-zone helpers built on Intl (maintained IANA data in the browser), plus a TypeScript port of the
// server's deterministic overlap engine (api/samesky/services/availability_engine.py). The server is the
// authority in real mode; this port powers sample mode offline and is tested against the same fixtures.

import type { WeeklyRule } from './contracts';

export type Interval = [number, number]; // epoch ms, half-open

interface WallParts {
  year: number;
  month: number;
  day: number;
  hour: number;
  minute: number;
  second: number;
  weekday: number; // 0 = Monday
}

const formatters = new Map<string, Intl.DateTimeFormat>();
function formatter(zone: string): Intl.DateTimeFormat {
  let f = formatters.get(zone);
  if (!f) {
    f = new Intl.DateTimeFormat('en-US', {
      timeZone: zone,
      hourCycle: 'h23',
      year: 'numeric',
      month: 'numeric',
      day: 'numeric',
      hour: 'numeric',
      minute: 'numeric',
      second: 'numeric',
      weekday: 'short',
    });
    formatters.set(zone, f);
  }
  return f;
}

const WEEKDAYS: Record<string, number> = { Mon: 0, Tue: 1, Wed: 2, Thu: 3, Fri: 4, Sat: 5, Sun: 6 };

export function wallParts(instant: number, zone: string): WallParts {
  const out: Record<string, string> = {};
  for (const p of formatter(zone).formatToParts(new Date(instant))) out[p.type] = p.value;
  return {
    year: +out.year,
    month: +out.month,
    day: +out.day,
    hour: +out.hour % 24,
    minute: +out.minute,
    second: +out.second,
    weekday: WEEKDAYS[out.weekday] ?? 0,
  };
}

function wallKey(p: Pick<WallParts, 'year' | 'month' | 'day' | 'hour' | 'minute'>): number {
  return Date.UTC(p.year, p.month - 1, p.day, p.hour, p.minute);
}

export function isValidZone(zone: string): boolean {
  try {
    formatter(zone);
    return true;
  } catch {
    return false;
  }
}

/** Map a local wall-clock time to an instant: gaps -> transition instant; folds -> first (start) / second (end). */
export function resolveWallTime(year: number, month: number, day: number, hour: number, minute: number, zone: string, edge: 'start' | 'end'): number {
  const target = Date.UTC(year, month - 1, day, hour, minute);
  const candidates = new Set<number>();
  for (const probe of [target - 86_400_000, target, target + 86_400_000]) {
    const offset = wallKey(wallParts(probe, zone)) - Math.floor(probe / 60_000) * 60_000;
    const t = target - offset;
    if (wallKey(wallParts(t, zone)) === target) candidates.add(t);
  }
  if (candidates.size) {
    const list = [...candidates];
    return edge === 'start' ? Math.min(...list) : Math.max(...list);
  }
  // DST gap: binary search (whole seconds) for the first instant whose wall time >= target.
  let lo = Math.floor((target - 15 * 3_600_000) / 1000);
  let hi = Math.ceil((target + 15 * 3_600_000) / 1000);
  while (hi - lo > 1) {
    const mid = Math.floor((lo + hi) / 2);
    if (wallKey(wallParts(mid * 1000, zone)) >= target) hi = mid;
    else lo = mid;
  }
  return hi * 1000;
}

export function merge(list: Interval[]): Interval[] {
  const sorted = list.filter(([s, e]) => e > s).sort((a, b) => a[0] - b[0]);
  const out: Interval[] = [];
  for (const [s, e] of sorted) {
    const last = out[out.length - 1];
    if (last && s <= last[1]) last[1] = Math.max(last[1], e);
    else out.push([s, e]);
  }
  return out;
}

export function subtract(base: Interval[], removals: Interval[]): Interval[] {
  const out: Interval[] = [];
  const rem = merge(removals);
  for (const [start, end] of merge(base)) {
    let cursor = start;
    for (const [rs, re] of rem) {
      if (re <= cursor || rs >= end) continue;
      if (rs > cursor) out.push([cursor, rs]);
      cursor = Math.max(cursor, re);
      if (cursor >= end) break;
    }
    if (cursor < end) out.push([cursor, end]);
  }
  return out;
}

export function intersect(a: Interval[], b: Interval[]): Interval[] {
  const A = merge(a);
  const B = merge(b);
  const out: Interval[] = [];
  let i = 0;
  let j = 0;
  while (i < A.length && j < B.length) {
    const s = Math.max(A[i][0], B[j][0]);
    const e = Math.min(A[i][1], B[j][1]);
    if (e > s) out.push([s, e]);
    if (A[i][1] < B[j][1]) i++;
    else j++;
  }
  return out;
}

function hm(value: string): [number, number] {
  const [h, m] = value.split(':').map(Number);
  return [h, m];
}

export function expandWeekly(rules: WeeklyRule[], zone: string, window: Interval): Interval[] {
  const out: Interval[] = [];
  const first = wallParts(window[0], zone);
  const last = wallParts(window[1], zone);
  let day = Date.UTC(first.year, first.month - 1, first.day) - 86_400_000;
  const lastDay = Date.UTC(last.year, last.month - 1, last.day);
  for (; day <= lastDay; day += 86_400_000) {
    const d = new Date(day);
    const weekday = (d.getUTCDay() + 6) % 7;
    for (const rule of rules) {
      if (rule.weekday !== weekday) continue;
      const [sh, sm] = hm(rule.start);
      const [eh, em] = hm(rule.end);
      const overnight = eh * 60 + em <= sh * 60 + sm;
      const endDay = new Date(day + (overnight ? 86_400_000 : 0));
      const s = resolveWallTime(d.getUTCFullYear(), d.getUTCMonth() + 1, d.getUTCDate(), sh, sm, zone, 'start');
      const e = resolveWallTime(endDay.getUTCFullYear(), endDay.getUTCMonth() + 1, endDay.getUTCDate(), eh, em, zone, 'end');
      const cs = Math.max(s, window[0]);
      const ce = Math.min(e, window[1]);
      if (ce > cs) out.push([cs, ce]);
    }
  }
  return merge(out);
}

export interface Schedule {
  userId: string;
  zone: string;
  rules: WeeklyRule[];
  busy?: Interval[];
  quiet?: { start: string; end: string } | null;
  minMinutes?: number;
}

export function freeIntervals(p: Schedule, window: Interval): Interval[] {
  const available = expandWeekly(p.rules, p.zone, window);
  const blocked = [...(p.busy ?? [])];
  if (p.quiet) blocked.push(...expandWeekly([0, 1, 2, 3, 4, 5, 6].map((weekday) => ({ weekday, start: p.quiet!.start, end: p.quiet!.end })), p.zone, window));
  return subtract(available, blocked);
}

export function commonOverlaps(people: Schedule[], now: number, days = 7): Interval[] {
  if (people.length < 2) return [];
  const window: Interval = [now, now + days * 86_400_000];
  const min = Math.max(...people.map((p) => p.minMinutes ?? 15)) * 60_000;
  let common = freeIntervals(people[0], window);
  for (const p of people.slice(1)) common = intersect(common, freeIntervals(p, window));
  return common.filter(([s, e]) => e - s >= min);
}

export function isBlockedNow(p: Schedule, now: number): boolean {
  const window: Interval = [now - 86_400_000, now + 86_400_000];
  const blocked = [...(p.busy ?? [])];
  if (p.quiet) blocked.push(...expandWeekly([0, 1, 2, 3, 4, 5, 6].map((weekday) => ({ weekday, start: p.quiet!.start, end: p.quiet!.end })), p.zone, window));
  return merge(blocked).some(([s, e]) => s <= now && now < e);
}

// ---------------------------------------------------------------------------
// Display helpers
// ---------------------------------------------------------------------------

export function formatTime(instant: number | string | Date, zone: string): string {
  return new Intl.DateTimeFormat(undefined, { timeZone: zone, hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(new Date(instant));
}

export function formatDate(instant: number | string | Date, zone: string, opts: Intl.DateTimeFormatOptions = { weekday: 'long', day: 'numeric', month: 'long' }): string {
  return new Intl.DateTimeFormat('en-GB', { timeZone: zone, ...opts }).format(new Date(instant));
}

export function zoneAbbreviation(instant: number, zone: string): string {
  const part = new Intl.DateTimeFormat('en-US', { timeZone: zone, timeZoneName: 'short' }).formatToParts(new Date(instant)).find((p) => p.type === 'timeZoneName');
  return part?.value ?? zone;
}

export function cityOf(zone: string): string {
  const city = zone.split('/').pop() ?? zone;
  return ({ Shanghai: 'Beijing', Dubai: 'Dubai', London: 'London' } as Record<string, string>)[city] ?? city.replace(/_/g, ' ');
}

export function detectZone(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone || 'Asia/Dubai';
}

export const COMMON_ZONES = [
  'Asia/Dubai', 'Asia/Shanghai', 'Asia/Riyadh', 'Asia/Kolkata', 'Asia/Singapore', 'Asia/Tokyo', 'Asia/Manila',
  'Europe/London', 'Europe/Paris', 'Europe/Berlin', 'Europe/Istanbul', 'Africa/Cairo', 'Africa/Lagos', 'Africa/Nairobi',
  'America/New_York', 'America/Chicago', 'America/Denver', 'America/Los_Angeles', 'America/Sao_Paulo', 'America/Toronto',
  'Australia/Sydney', 'Pacific/Auckland',
];
