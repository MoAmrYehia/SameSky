export interface Cue {
  start: number; // seconds
  end: number;
  text: string;
}

function toSeconds(stamp: string): number {
  const parts = stamp.trim().split(':').map(Number);
  return parts.reduce((acc, v) => acc * 60 + v, 0);
}

export function parseVtt(text: string): Cue[] {
  const cues: Cue[] = [];
  for (const block of text.replace(/\r/g, '').split('\n\n')) {
    const lines = block.split('\n').filter(Boolean);
    const timing = lines.findIndex((l) => l.includes('-->'));
    if (timing < 0) continue;
    const [a, b] = lines[timing].split('-->');
    cues.push({ start: toSeconds(a), end: toSeconds(b.trim().split(/\s+/)[0]), text: lines.slice(timing + 1).join(' ') });
  }
  return cues;
}

const cache = new Map<string, Promise<Cue[]>>();
export function loadCaptions(url: string): Promise<Cue[]> {
  if (!cache.has(url)) cache.set(url, fetch(url).then((r) => (r.ok ? r.text() : '')).then(parseVtt).catch(() => []));
  return cache.get(url)!;
}

/** Captions for any recording: a VTT file if present, otherwise the consented transcript, otherwise the caption. */
export function cueAt(cues: Cue[], t: number, fallback: string): string {
  if (!cues.length) return fallback;
  return cues.find((c) => t >= c.start && t < c.end)?.text ?? '';
}
