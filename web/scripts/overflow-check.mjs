// Layout overflow check for companion pages: the page must not scroll sideways, and no control may extend
// beyond the card that contains it.
//
//   node scripts/overflow-check.mjs --base http://localhost:5173 [--routes settings,day,share,together] [--sizes 1440x900,1672x941,390x844]

import { chromium } from 'playwright-core';

const arg = (name, fallback) => (process.argv.includes(`--${name}`) ? process.argv[process.argv.indexOf(`--${name}`) + 1] : fallback);
const base = arg('base', 'http://localhost:5173');
const routes = arg('routes', 'settings,day,share,together,keepsakes').split(',');
const sizes = arg('sizes', '1440x900,1672x941,1024x768,390x844').split(',').map((s) => s.split('x').map(Number));
const browser = await chromium.launch({ channel: 'chrome', headless: true });
let failed = 0;
for (const [w, h] of sizes) {
  for (const route of routes) {
    const page = await browser.newPage({ viewport: { width: w, height: h } });
    await page.addInitScript(() => { localStorage.clear(); localStorage.setItem('samesky.mode', 'sample'); localStorage.setItem('samesky.theme', 'light'); });
    await page.goto(`${base}/#/${route === 'share' ? 'moments' : route}`);
    await page.evaluate(() => document.fonts.ready);
    await page.waitForTimeout(1500);
    const result = await page.evaluate(() => {
      const doc = document.documentElement;
      const spill = [];
      for (const card of document.querySelectorAll('.card, .glass-panel, .clock-card, .keepsake')) {
        const c = card.getBoundingClientRect();
        for (const el of card.querySelectorAll('input, select, button, textarea, a.btn')) {
          const r = el.getBoundingClientRect();
          if (r.width && (r.right > c.right + 1 || r.left < c.left - 1)) spill.push(`${el.tagName.toLowerCase()}${el.getAttribute('aria-label') ? `[${el.getAttribute('aria-label')}]` : ''} ${Math.round(r.left)}-${Math.round(r.right)} outside ${Math.round(c.left)}-${Math.round(c.right)}`);
        }
      }
      return { scrollWidth: doc.scrollWidth, clientWidth: doc.clientWidth, spill: spill.slice(0, 6), spillCount: spill.length };
    });
    const ok = result.scrollWidth <= result.clientWidth && result.spillCount === 0;
    if (!ok) failed++;
    console.log(`${ok ? 'OK  ' : 'FAIL'} ${route} ${w}x${h}: scrollWidth ${result.scrollWidth}/${result.clientWidth}${result.spillCount ? ` · ${result.spillCount} controls outside their card: ${result.spill.join('; ')}` : ''}`);
    await page.close();
  }
}
await browser.close();
process.exitCode = failed ? 1 : 0;
