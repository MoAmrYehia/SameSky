// Deterministic visual captures for docs/VISUAL_QA.md.
//
//   node scripts/capture.mjs --out ../docs/visual-qa/after --sizes 1672x941,1440x900 --shots room,day,share,together,keepsakes
//   --report   also print the room's safe-area boxes and renderer stats (draw calls, triangles, textures)
//
// Uses the locally installed Chrome (playwright-core, no browser download), sample mode, a frozen clock
// (Friday 6 November 2026, 17:05 in Dubai / 21:05 in Beijing), fixed idle-animation time in the room
// (?capture=1), and waits for fonts and the room's ready flag before each screenshot.

import { mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { chromium } from 'playwright-core';

const args = Object.fromEntries(process.argv.slice(2).reduce((acc, a, i, all) => (a.startsWith('--') ? [...acc, [a.slice(2), all[i + 1] && !all[i + 1].startsWith('--') ? all[i + 1] : 'true']] : acc), []));
const base = args.base ?? 'http://localhost:5173';
const out = resolve(args.out ?? '../docs/visual-qa/after');
const sizes = (args.sizes ?? '1672x941').split(',').map((s) => s.split('x').map(Number));
const shots = (args.shots ?? 'room,day,share,together,keepsakes').split(',');
const themes = (args.themes ?? 'light').split(',');
const scheme = args.scheme ?? 'light';
const FROZEN = new Date('2026-11-06T13:05:00Z');

const ROUTES = {
  room: { path: '/room.html?mode=sample&capture=1', room: true },
  'room-watch': { path: '/room.html?mode=sample&capture=1&watch=1', room: true },
  // headset layout preview from a seated eye position (IWER sessions do not start in headless Chrome)
  'room-xr': { path: '/room.html?mode=sample&capture=1&preview=xr', room: true, noSafeArea: true },
  // states: first-load progress (the avatar download is held open), an asset failure, and sign-in needed
  'room-loading': { path: '/room.html?mode=sample&capture=1', state: true, hold: '**/characters.glb' },
  'room-error': { path: '/room.html?mode=sample&capture=1', state: true, abort: '**/characters.glb' },
  'room-signin': { path: '/room.html?mode=live', state: true, live: true },
  day: { path: '/#/day' },
  'day-circle': { path: '/#/day', circle: true },
  share: { path: '/#/moments' },
  'share-populated': { path: '/#/moments', populate: true },
  together: { path: '/#/together' },
  keepsakes: { path: '/#/keepsakes' },
  settings: { path: '/#/settings' },
  // companion first paint: portraits and keepsakes still rendering (shimmer placeholders), no waiting
  'day-loading': { path: '/#/day', early: true },
  welcome: { path: '/', welcome: true },
};

mkdirSync(out, { recursive: true });
const browser = await chromium.launch({ channel: args.channel ?? 'chrome', headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const failures = [];

for (const theme of themes) {
  for (const [w, h] of sizes) {
    for (const shot of shots) {
      const route = ROUTES[shot];
      if (!route) throw new Error(`unknown shot ${shot}`);
      const context = await browser.newContext({ viewport: { width: w, height: h }, deviceScaleFactor: 1, colorScheme: scheme });
      const page = await context.newPage();
      page.on('pageerror', (e) => failures.push(`${shot} ${w}x${h}: ${e.message}`));
      await page.clock.setFixedTime(FROZEN);
      await page.addInitScript(({ theme, welcome, circle }) => {
        try {
          localStorage.clear();
          if (!welcome) localStorage.setItem('samesky.mode', 'sample');
          localStorage.setItem('samesky.theme', theme);
          localStorage.setItem('samesky.onboarding.sample', 'cup');
          if (circle) localStorage.setItem('samesky.room.sample', 'sample-family');
          indexedDB.deleteDatabase('samesky-sample');
        } catch { /* ignore */ }
      }, { theme, welcome: !!route.welcome || !!route.live, circle: !!route.circle });
      if (route.hold) await page.route(route.hold, () => undefined); // never answered during the shot
      if (route.abort) await page.route(route.abort, (r) => r.abort('failed'));
      await page.goto(base + route.path + (route.room && args.report ? '&debug' : ''), { waitUntil: 'load' });
      await page.evaluate(() => document.fonts.ready);
      if (route.state) {
        await page.waitForTimeout(route.hold ? 6000 : 8000);
      } else if (route.room) {
        await page.waitForFunction(() => window.__sameskyReady === true, null, { timeout: 120_000 }).catch(() => failures.push(`${shot} ${w}x${h}: room not ready`));
        await page.evaluate((id) => document.getElementById(id)?.click(), route.enter ?? 'enter-2d');
        await page.waitForTimeout(route.enter ? 5000 : 2500);
        const safe = route.noSafeArea ? null : await page.evaluate(() => window.__sameskySafeArea?.() ?? null);
        if (safe && safe.violations?.length) failures.push(`${shot} ${w}x${h}: safe-area ${JSON.stringify(safe.violations)}`);
        if (args.report) {
          const stats = await page.evaluate(() => {
            const r = window.__samesky?.world?.renderer;
            return r ? { calls: r.info.render.calls, triangles: r.info.render.triangles, textures: r.info.memory.textures, geometries: r.info.memory.geometries, programs: r.info.programs?.length } : null;
          });
          console.log(`report ${shot} ${w}x${h}`, JSON.stringify({ safe, stats }));
        }
      } else if (route.early) {
        await page.waitForTimeout(450);
      } else {
        await page.waitForTimeout(1200);
        if (route.populate) {
          await page.getByRole('button', { name: /sample clip/i }).click().catch(() => failures.push(`${shot}: no sample-clip button`));
          await page.getByRole('button', { name: /Private circle/i }).click().catch(() => undefined);
          await page.waitForTimeout(2500);
        }
        // wait for 3D portraits and keepsake renders (software WebGL in headless Chrome is slow)
        await page.waitForFunction(() => !document.querySelector('.avatar-loading, .keepsake-render.loading'), null, { timeout: 90_000 }).catch(() => failures.push(`${shot} ${w}x${h}: renders still loading`));
        await page.waitForTimeout(600);
      }
      const file = `${out}/${shot}${theme === 'light' ? '' : `-${theme}`}-${w}x${h}.jpg`;
      // software WebGL renders the room at ~2-4 fps, so a single frame can take well over 30 s to capture
      await page.screenshot({ path: file, type: 'jpeg', quality: 84, timeout: 180_000 });
      console.log('captured', file);
      await context.close();
    }
  }
}
await browser.close();
if (failures.length) {
  console.log('\nISSUES:\n' + failures.join('\n'));
  process.exitCode = 1;
}
