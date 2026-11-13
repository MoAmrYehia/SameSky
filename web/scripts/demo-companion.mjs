// Records the companion's real "Share a moment" flow for the demo film, at a phone's size.
//
//   node scripts/demo-companion.mjs [--out ../demo-build/capture/companion] [--base http://localhost:5173]
//
// The companion runs in sample mode (fictional people; the clip offered there is stock footage and the page says so).
// The locally installed Chrome is driven with playwright-core, as in scripts/capture.mjs. The page is photographed
// continuously while the steps run, each picture saved with its time, so the film shows the page as it really
// behaved, taps included: a small ring is drawn where each tap lands, the way a phone's "show taps" setting does.
// Writes frames/*.jpg, frames.txt (an FFmpeg concat list with each frame's duration) and steps.json (when each step
// happened).

import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { chromium } from 'playwright-core';

const args = Object.fromEntries(process.argv.slice(2).reduce((acc, a, i, all) => (a.startsWith('--') ? [...acc, [a.slice(2), all[i + 1] && !all[i + 1].startsWith('--') ? all[i + 1] : 'true']] : acc), []));
const base = args.base ?? 'http://localhost:5173';
const out = resolve(args.out ?? '../demo-build/capture/companion');
const [width, height] = (args.size ?? '430x932').split('x').map(Number);
rmSync(out, { recursive: true, force: true });
mkdirSync(`${out}/frames`, { recursive: true });

const browser = await chromium.launch({ channel: args.channel ?? 'chrome', headless: true, args: ['--use-angle=d3d11', '--ignore-gpu-blocklist'] });
const context = await browser.newContext({ viewport: { width, height }, deviceScaleFactor: 2, hasTouch: true, isMobile: true, colorScheme: 'light' });
const page = await context.newPage();
const issues = [];
page.on('pageerror', (e) => issues.push(e.message));
await page.addInitScript(() => {
  try {
    localStorage.clear();
    localStorage.setItem('samesky.mode', 'sample');
    localStorage.setItem('samesky.theme', 'light');
    localStorage.setItem('samesky.onboarding.sample', 'cup');
    indexedDB.deleteDatabase('samesky-sample');
  } catch { /* ignore */ }
  // show taps: a ring where each one lands
  addEventListener('pointerdown', (e) => {
    const ring = document.createElement('div');
    ring.style.cssText = `position:fixed;left:${e.clientX - 22}px;top:${e.clientY - 22}px;width:44px;height:44px;border-radius:50%;border:3px solid rgba(255,255,255,.95);background:rgba(69,102,77,.35);box-shadow:0 0 0 1px rgba(0,0,0,.25);pointer-events:none;z-index:99999;transition:transform .45s ease-out,opacity .45s ease-out`;
    document.documentElement.appendChild(ring);
    requestAnimationFrame(() => { ring.style.transform = 'scale(1.5)'; ring.style.opacity = '0'; });
    setTimeout(() => ring.remove(), 520);
  }, true);
});

await page.goto(`${base}/#/moments`, { waitUntil: 'load' });
await page.evaluate(() => document.fonts.ready);
await page.waitForTimeout(1500);

// the page, photographed as fast as Chrome will hand over full-resolution pictures (about ten a second), each with
// its time: a DevTools screencast would be smoother, but it gives pictures at CSS size, too soft for a film
const frames = [];
let n = 0;
let filming = true;
const camera = (async () => {
  while (filming) {
    const t = Date.now() / 1000;
    const shot = await page.screenshot({ type: 'jpeg', quality: 92 }).catch(() => null);
    if (!shot) continue;
    const name = `frames/${String(n++).padStart(5, '0')}.jpg`;
    writeFileSync(`${out}/${name}`, shot);
    frames.push({ name, t });
  }
})();
const t0 = Date.now() / 1000;
const steps = [];
const step = (what) => { steps.push({ t: Date.now() / 1000 - t0, what }); console.log(steps.at(-1).t.toFixed(1), what); };
// a still page paints nothing: nudge it so a frame is there at each step
const settle = async (ms) => { await page.waitForTimeout(ms); await page.evaluate(() => new Promise((r) => requestAnimationFrame(() => r()))); };
const tap = async (locator, what) => {
  await locator.scrollIntoViewIfNeeded();
  await settle(500);
  const box = await locator.boundingBox();
  if (!box) { issues.push(`not on screen: ${what}`); return; }
  step(what);
  await page.touchscreen.tap(box.x + box.width / 2, box.y + box.height / 2);
};

step('Share a moment');
await settle(1800);
await tap(page.getByRole('button', { name: /sample clip/i }), 'Use a sample clip');
await page.locator('video, .draft-preview, .filmstrip').first().waitFor({ timeout: 20000 }).catch(() => issues.push('no draft preview'));
await settle(2600);
step('Preview and trim');
await page.evaluate(() => window.scrollBy({ top: 260, behavior: 'smooth' }));
await settle(2200);
const audience = page.getByRole('heading', { name: /Who can see this/i });
await audience.scrollIntoViewIfNeeded().catch(() => undefined);
await page.evaluate(() => window.scrollBy({ top: 220, behavior: 'smooth' }));
await settle(2200);
step('Who can see this');
await settle(1500);
const share = page.getByRole('button', { name: /^Share with /i });
await tap(share, 'Share with Maya');
await settle(1800);
// the confirmation: who exactly will see it
const confirm = page.getByRole('button', { name: /^(Share|Confirm|Yes)/i }).last();
step('Confirm');
await settle(1600);
if (await confirm.isVisible().catch(() => false)) await tap(confirm, 'Confirm sharing');
await settle(3200);
step('Shared');
await settle(1500);

filming = false;
await camera;
const body = await page.evaluate(() => document.body.innerText.slice(0, 600));
// an FFmpeg concat list: each frame lasts until the next was painted
const lines = [];
for (let i = 0; i < frames.length; i++) {
  const d = i + 1 < frames.length ? Math.max(0.01, frames[i + 1].t - frames[i].t) : 1.0;
  lines.push(`file '${frames[i].name}'`, `duration ${d.toFixed(4)}`);
}
if (frames.length) lines.push(`file '${frames.at(-1).name}'`);
writeFileSync(`${out}/frames.txt`, lines.join('\n') + '\n');
writeFileSync(`${out}/steps.json`, JSON.stringify({ viewport: { width, height, scale: 2 }, frames: frames.length, seconds: frames.length ? frames.at(-1).t - frames[0].t : 0, steps, issues, endText: body }, null, 2));
console.log(`${frames.length} frames, ${issues.length} issues`, issues);
await browser.close();
