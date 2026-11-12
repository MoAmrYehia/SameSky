// Exports the companion's fictional sample day (web/src/shared/sample.ts) as JSON for the native Quest app,
// so both clients tell the same story with the same honest labels (fictional people, stock photos,
// a synthesized voice, a simulated camera pan, precomputed recaps with no AI call).
//
//   node scripts/native/export_sample.mjs <out.json>
//
// Media URLs become Unity Resources paths ("res:Sample/Media/<name>"); WebVTT captions are embedded as cues.

import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { dirname, resolve, basename, extname } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const out = process.argv[2] ?? resolve(root, 'native/quest/Assets/SameSky/Resources/Sample/sample-day.json');
const sample = await import(new URL('../../web/src/shared/sample.ts', import.meta.url).href);

const res = (url) => (url ? `res:Sample/Media/${basename(url, extname(url))}` : undefined);

function vttCues(url) {
  const text = readFileSync(resolve(root, 'web/public', url.replace(/^\//, '')), 'utf8');
  const toMs = (t) => {
    const [h, m, s] = t.split(':');
    return Math.round(((+h) * 3600 + (+m) * 60 + parseFloat(s)) * 1000);
  };
  const cues = [];
  for (const block of text.split(/\r?\n\r?\n/)) {
    const lines = block.trim().split(/\r?\n/);
    const timing = lines.find((l) => l.includes('-->'));
    if (!timing) continue;
    const [a, b] = timing.split('-->').map((s) => s.trim().split(' ')[0]);
    const body = lines.slice(lines.indexOf(timing) + 1).join(' ');
    const m = /^([^:]{1,40}):\s*(.*)$/.exec(body);
    cues.push({ startMs: toMs(a), endMs: toMs(b), speaker: m ? m[1] : null, text: m ? m[2] : body });
  }
  return cues;
}

function mapMoment(m) {
  const media = m.media
    ? {
        ...m.media,
        url: res(m.media.url),
        thumbUrl: res(m.media.thumbUrl),
        posterUrl: res(m.media.posterUrl),
        captionsUrl: undefined,
        captions: m.media.captionsUrl ? vttCues(m.media.captionsUrl) : undefined,
      }
    : null;
  return { ...m, media };
}

const moments = sample.seedMoments();
const recaps = sample.seedRecaps(moments).map((r) => ({ ...r, playback: r.playback.map(mapMoment) }));

const data = {
  generatedFrom: 'web/src/shared/sample.ts',
  note: 'Fictional sample. Photos are stock-derived, the voice is synthesized, the clip is a simulated camera pan, recaps are precomputed (no AI call), presence is simulated.',
  day: sample.SAMPLE_DAY,
  me: sample.ME,
  people: sample.SAMPLE_PEOPLE,
  rooms: sample.SAMPLE_ROOMS,
  moments: moments.map(mapMoment),
  recaps,
  replies: sample.SEED_REPLIES,
  awards: sample.SEED_AWARDS,
  schedules: sample.SAMPLE_SCHEDULES,
  inbox: sample.SEED_INBOX,
};

mkdirSync(dirname(out), { recursive: true });
writeFileSync(out, JSON.stringify(data, null, 2) + '\n', 'utf8');
console.log(`wrote ${out}: ${data.moments.length} moments, ${data.recaps.length} recaps`);
