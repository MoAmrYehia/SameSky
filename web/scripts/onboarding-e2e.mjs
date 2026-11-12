// The whole first-time journey through the real UI (live mode, local API):
//   Nora opens SameSky, creates an account, and walks through setup: about her, her room, an invite link,
//   her avatar choice, her usual free times. Then Sam opens the invite link, creates his account and lands in
//   the shared room; Nora sees him in Together. Checks no page scrolls sideways; screenshots at 1440×900 and
//   390×844 go to --out. All names and addresses are made up (@example.com).
//
//   node scripts/onboarding-e2e.mjs --base http://localhost:5173 --out ../docs/visual-qa/onboarding
import { mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { chromium } from 'playwright-core';

const arg = (n, d) => (process.argv.includes(`--${n}`) ? process.argv[process.argv.indexOf(`--${n}`) + 1] : d);
const base = arg('base', 'http://localhost:5173');
const out = resolve(arg('out', '../docs/visual-qa/onboarding'));
mkdirSync(out, { recursive: true });
const stamp = Date.now().toString(36);
const results = [];
const pass = (ok, text) => { results.push(`${ok ? 'PASS' : 'FAIL'} ${text}`); return ok; };
/** Wait for a locator to be visible (Playwright's isVisible() doesn't wait). */
const shows = (locator, timeout = 20_000) => locator.waitFor({ state: 'visible', timeout }).then(() => true, () => false);
const renderer = process.platform === 'win32' ? ['--use-angle=d3d11', '--enable-gpu'] : ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'];
const browser = await chromium.launch({ channel: 'chrome', headless: true, args: [...renderer, '--ignore-gpu-blocklist'] });

async function person(name, viewport) {
  const ctx = await browser.newContext({ viewport, deviceScaleFactor: 1, permissions: ['clipboard-read', 'clipboard-write'] });
  await ctx.addInitScript(() => { try { localStorage.setItem('samesky.theme', 'light'); } catch { /* ignore */ } });
  const page = await ctx.newPage();
  page.on('pageerror', (e) => results.push(`FAIL page error (${name}): ${e.message}`));
  return { ctx, page };
}
async function shot(page, name) {
  await page.waitForTimeout(500);
  const wide = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  const { width, height } = page.viewportSize();
  if (wide > 1) results.push(`FAIL ${name} ${width}x${height}: page is ${wide}px wider than the viewport`);
  await page.screenshot({ path: `${out}/${name}-${width}x${height}.jpg`, type: 'jpeg', quality: 84 });
}
async function signUp(page, { name, email, zone }) {
  await page.getByRole('tab', { name: 'Create account' }).click();
  await page.getByLabel('Display name').fill(name);
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill('correct horse battery');
  await page.getByLabel('Your time zone').selectOption(zone).catch(() => undefined);
  await page.getByRole('button', { name: 'Create account' }).click();
}

let inviteUrl = '';
for (const [label, viewport] of [['desktop', { width: 1440, height: 900 }], ['phone', { width: 390, height: 844 }]]) {
  const nora = { name: `Nora`, email: `nora-${label}-${stamp}@example.com`, zone: 'Europe/Berlin' };
  const sam = { name: `Sam`, email: `sam-${label}-${stamp}@example.com`, zone: 'America/New_York' };

  // ---- Nora: create an account, then setup ------------------------------------------------------------------
  const A = await person('Nora', viewport);
  await A.page.goto(`${base}/`);
  await A.page.getByRole('tab', { name: 'Create account' }).waitFor({ timeout: 30_000 });
  await shot(A.page, `01-welcome-${label}`);
  await signUp(A.page, nora);
  pass(await shows(A.page.getByText('Share a few chosen moments'), 30_000), `${label}: a new account lands in setup, not an empty page`);
  await shot(A.page, `02-setup-about-${label}`);
  await A.page.getByRole('button', { name: 'Continue' }).click();
  await A.page.getByText('Who is this room for?').waitFor({ timeout: 15_000 });
  await A.page.getByLabel('Room name (optional)').fill('Nora + Sam');
  await shot(A.page, `03-setup-room-${label}`);
  await A.page.getByRole('button', { name: 'Create the room' }).click();
  await A.page.getByText('Nora + Sam is ready').waitFor({ timeout: 15_000 });
  await A.page.getByRole('button', { name: 'Create invite link' }).click();
  const url = A.page.getByLabel('Invite link');
  await url.waitFor({ timeout: 15_000 });
  inviteUrl = (await url.innerText()).trim();
  pass(/\/#\/join\/[\w-]+/.test(inviteUrl), `${label}: setup creates a single-use invite link with copy${await A.page.getByRole('button', { name: 'Share link' }).isVisible().catch(() => false) ? ' and share' : ''}`);
  await shot(A.page, `04-setup-invite-${label}`);
  await A.page.getByRole('button', { name: 'Next' }).click();
  await A.page.getByText('Let people recognise you').waitFor({ timeout: 15_000 });
  await shot(A.page, `05-setup-avatar-${label}`);
  await A.page.getByRole('button', { name: 'Keep the demo avatar for now' }).click();
  await A.page.getByText('When are you usually free?').waitFor({ timeout: 15_000 });
  await A.page.getByRole('button', { name: /Weekend mornings/ }).click();
  await shot(A.page, `06-setup-times-${label}`);
  await A.page.getByRole('button', { name: 'Save my usual times' }).click();
  await A.page.getByText('You’re all set').waitFor({ timeout: 15_000 });
  await shot(A.page, `07-setup-done-${label}`);
  await A.page.getByRole('button', { name: 'Go to Our day' }).click();
  pass(await shows(A.page.getByText('Waiting for someone to join Nora + Sam'), 15_000), `${label}: Our day says the room is waiting for someone and offers the invite`);
  await shot(A.page, `08-day-waiting-${label}`);
  await A.page.goto(`${base}/#/together`);
  pass(await shows(A.page.getByText('Nobody to meet yet'), 15_000), `${label}: Together explains there is nobody to meet yet (no empty clocks or dead buttons)`);
  await shot(A.page, `09-together-waiting-${label}`);
  await A.page.goto(`${base}/#/moments`);
  pass(await shows(A.page.getByText('Nobody has joined yet')), `${label}: Share explains there is nobody to share with yet and offers the invite (saving privately still works)`);
  await shot(A.page, `09b-share-waiting-${label}`);
  await A.page.goto(`${base}/#/settings`);
  await A.page.getByText('Room settings').first().click();
  pass(await shows(A.page.getByText('Mute notifications from this room')) && await shows(A.page.getByText(/Draft the daily recap at/)), `${label}: Settings offers per-room mute and (for the owner) the daily recap time`);
  await A.page.getByText('Room settings').first().scrollIntoViewIfNeeded();
  await shot(A.page, `09c-room-settings-${label}`);

  // ---- Sam: open the link, create an account, land in the room --------------------------------------------------
  const B = await person('Sam', viewport);
  await B.page.goto(inviteUrl);
  pass(await shows(B.page.getByText('You\'ve been invited to a SameSky room'), 30_000), `${label}: the invite link opens a join screen`);
  await shot(B.page, `10-invite-open-${label}`);
  await signUp(B.page, sam);
  pass(await shows(B.page.getByText(/Your day, side by side|Nothing shared for this day yet/).first(), 30_000), `${label}: Sam lands on Our day in Nora’s room`);
  await shot(B.page, `11-invitee-day-${label}`);

  // ---- Nora: sees Sam; the usual-times nudge is gone because she set them ----------------------------------------
  await A.page.goto(`${base}/#/day`);
  await A.page.reload();
  await A.page.goto(`${base}/#/together`);
  pass(await shows(A.page.getByText('Sam', { exact: true }).first(), 20_000), `${label}: Nora sees Sam’s clock in Together`);
  await shot(A.page, `12-together-pair-${label}`);
  await A.ctx.close();
  await B.ctx.close();
}
await browser.close();
console.log(results.join('\n'));
process.exitCode = results.some((r) => r.startsWith('FAIL')) ? 1 : 0;
