import { describe, expect, it } from 'vitest';
import { commonOverlaps, expandWeekly, resolveWallTime } from './tz';

const utc = (s: string) => Date.parse(s);

describe('time-zone overlap engine (TS port, same fixtures as the API)', () => {
  it('17:00-18:00 Dubai and 21:00-22:00 Shanghai are the same UTC hour on 6 Nov 2026', () => {
    const now = utc('2026-11-06T06:00:00Z');
    const result = commonOverlaps(
      [
        { userId: 'adam', zone: 'Asia/Dubai', rules: [{ weekday: 4, start: '17:00', end: '18:00' }] },
        { userId: 'maya', zone: 'Asia/Shanghai', rules: [{ weekday: 4, start: '21:00', end: '22:00' }] },
      ],
      now,
      1,
    );
    expect(result).toEqual([[utc('2026-11-06T13:00:00Z'), utc('2026-11-06T14:00:00Z')]]);
  });

  it('resolves a DST gap to the transition instant', () => {
    expect(resolveWallTime(2026, 3, 8, 2, 30, 'America/New_York', 'start')).toBe(utc('2026-03-08T07:00:00Z'));
  });

  it('keeps the full window across a DST fold', () => {
    expect(resolveWallTime(2026, 11, 1, 1, 30, 'America/New_York', 'start')).toBe(utc('2026-11-01T05:30:00Z'));
    expect(resolveWallTime(2026, 11, 1, 1, 30, 'America/New_York', 'end')).toBe(utc('2026-11-01T06:30:00Z'));
  });

  it('handles overnight windows across local dates', () => {
    const window: [number, number] = [utc('2026-11-05T00:00:00Z'), utc('2026-11-07T00:00:00Z')];
    const out = expandWeekly([{ weekday: 3, start: '22:00', end: '01:00' }], 'America/Los_Angeles', window);
    expect(out).toEqual([[utc('2026-11-06T06:00:00Z'), utc('2026-11-06T09:00:00Z')]]);
  });
});
