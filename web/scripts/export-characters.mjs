// Exports the sample people's demo characters to GLB for the native Quest app.
//   node scripts/export-characters.mjs --base http://localhost:5173 --out ../native/quest/Assets/SameSky/Art/Characters
// Needs the Vite dev server (npm run dev). The characters are original procedural sculpts (no scans, no photos).
import { writeFileSync, mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { chromium } from 'playwright';

const arg = (name, fallback) => {
  const i = process.argv.indexOf(name);
  return i > 0 ? process.argv[i + 1] : fallback;
};
const base = arg('--base', 'http://localhost:5173');
const out = resolve(arg('--out', '../native/quest/Assets/SameSky/Resources/Characters'));
// Maya and Adam are stock Rocketbox adults in the native app (native/quest/Assets/SameSky/Editor/CharacterSetup.cs
// makes their prefabs); only Lina still comes from this kit.
const people = { 'sample-lina': 'lina' };

const browser = await chromium.launch({ channel: 'chrome', headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const page = await browser.newPage();
page.on('pageerror', (e) => console.error('page error:', e.message));
await page.goto(`${base}/dev/export-characters.html`);
await page.waitForFunction(() => window.__exportReady === true, null, { timeout: 120000 });
mkdirSync(out, { recursive: true });
for (const [id, name] of Object.entries(people)) {
  const b64 = await page.evaluate((pid) => window.__exportCharacter(pid), id);
  const bytes = Buffer.from(b64, 'base64');
  writeFileSync(resolve(out, `${name}.glb`), bytes);
  console.log(`${name}.glb ${(bytes.length / 1024).toFixed(0)} KB`);
}
await browser.close();
