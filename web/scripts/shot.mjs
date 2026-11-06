// One-off screenshot helper: node scripts/shot.mjs <url> <out.jpg> [width] [height]
import { chromium } from 'playwright-core';
const [url, out, w = '1600', h = '700'] = process.argv.slice(2);
const browser = await chromium.launch({ channel: 'chrome', headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const page = await browser.newPage({ viewport: { width: +w, height: +h } });
const errors = [];
page.on('pageerror', (e) => errors.push(e.message));
page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
await page.goto(url, { waitUntil: 'load' });
await page.waitForFunction(() => window.__ready === true || window.__sameskyReady === true, null, { timeout: 120000 }).catch(() => errors.push('not ready'));
await page.waitForTimeout(1500);
await page.screenshot({ path: out, type: 'jpeg', quality: 88 });
await browser.close();
if (errors.length) console.log('ERRORS:\n' + errors.join('\n'));
console.log('saved', out);
