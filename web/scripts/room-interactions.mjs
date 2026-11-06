// Smoke test for the room's pointer interactions on a flat screen (sample mode, headless Chrome):
// dock buttons (single canvas + invisible per-button hit areas), the cup's waiting-message label, and a table
// object. Clicks go through IWSDK's canvas pointer events at the projected screen position of each target.
//
//   node scripts/room-interactions.mjs [--base http://localhost:5173]

import { chromium } from 'playwright-core';

const base = process.argv.includes('--base') ? process.argv[process.argv.indexOf('--base') + 1] : 'http://localhost:5173';
const browser = await chromium.launch({ channel: 'chrome', headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
const errors = [];
page.on('pageerror', (e) => errors.push(e.message));
await page.addInitScript(() => {
  localStorage.clear();
  localStorage.setItem('samesky.mode', 'sample');
  localStorage.setItem('samesky.onboarding.sample', 'cup');
  indexedDB.deleteDatabase('samesky-sample');
});
await page.goto(`${base}/room.html?mode=sample&debug`);
await page.waitForFunction(() => window.__sameskyReady === true, null, { timeout: 120_000 });
await page.click('#enter-2d');
await page.waitForTimeout(1500);

/** Screen pixel of an object's world position (projected with the room camera). */
const pixelOf = (selector) => page.evaluate((sel) => {
  const s = window.__samesky;
  const cam = s.world.camera;
  let obj = null;
  if (sel.startsWith('dock:')) {
    const id = sel.slice(5);
    const i = s.dock.items.findIndex((it) => it.id === id);
    obj = s.dock.group.children.filter((c) => c.name === 'hit')[i];
  } else if (sel === 'cup') {
    obj = [...s.memory.values()].find((o) => o.record.objectType === 'cup').group;
  }
  const v = obj.getWorldPosition(new obj.position.constructor());
  if (sel === 'cup') v.y += 0.05;
  v.project(cam);
  return { x: Math.round(((v.x + 1) / 2) * innerWidth), y: Math.round(((1 - v.y) / 2) * innerHeight) };
}, selector);

const results = [];
async function check(name, action, probe) {
  const before = await page.evaluate(probe);
  await action();
  await page.waitForTimeout(1200);
  const after = await page.evaluate(probe);
  const ok = JSON.stringify(before) !== JSON.stringify(after);
  results.push(`${ok ? 'PASS' : 'FAIL'} ${name}: ${JSON.stringify(before)} -> ${JSON.stringify(after)}`);
}

const clickAt = async (sel) => {
  const p = await pixelOf(sel);
  await page.mouse.move(p.x, p.y);
  await page.waitForTimeout(150);
  await page.mouse.click(p.x, p.y);
};

await check('cup (table object) plays the voice note and marks Maya as recorded', () => clickAt('cup'), () => window.__samesky.partner.presence);
await page.waitForTimeout(800);
await check('dock: Reply opens the reply sheet', () => clickAt('dock:reply'), () => window.__samesky.pressables.size);
await check('dock: Captions toggles captions', () => clickAt('dock:cc'), () => window.__samesky.screen.captions.mesh.visible);
await check('dock: Our day starts the story', () => clickAt('dock:day'), () => window.__samesky.screen.current?.id ?? null);

console.log(results.join('\n'));
if (errors.length) console.log('page errors:\n' + errors.join('\n'));
await browser.close();
process.exitCode = results.some((r) => r.startsWith('FAIL')) || errors.length ? 1 : 0;
