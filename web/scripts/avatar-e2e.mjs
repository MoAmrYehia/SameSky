// End-to-end check of the owner-approved avatar path through the real UI and API (live mode, local stack):
// two new accounts share a direct room; one imports the TEST MANNEQUIN (tests/fixtures/test-mannequin.glb —
// not a person, not photo-derived) via Settings → Your avatar → Import, reviews it and approves it; the other
// then sees it in companion portraits, seated in the room, and live across the table when both are in the
// room (two desktop browsers; no headset). "Stop sharing" then swaps the open room back to the labelled demo
// avatar without a reload. Also captures the "generation not set up" consent state. Screenshots of the setup
// and approval screens are taken at 1672×941, 1440×900 and 390×844. No real person's photo or likeness is used.
//
//   node scripts/avatar-e2e.mjs --base http://localhost:5173 --out ../docs/visual-qa/after
import { mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { chromium } from 'playwright-core';

const arg = (n, d) => (process.argv.includes(`--${n}`) ? process.argv[process.argv.indexOf(`--${n}`) + 1] : d);
const base = arg('base', 'http://localhost:5173');
const out = resolve(arg('out', '../docs/visual-qa/after'));
mkdirSync(out, { recursive: true });
const SIZES = [[1672, 941], [1440, 900], [390, 844]];
const stamp = Date.now().toString(36);
const api = async (path, body, token, method = 'POST') => {
  const r = await fetch(`${base}${path}`, { method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) }, body: body ? JSON.stringify(body) : undefined });
  if (!r.ok) throw new Error(`${path} ${r.status} ${await r.text()}`);
  return r.json();
};
const signup = async (name, zone) => {
  const r = await api('/v1/auth/signup', { email: `${name.toLowerCase()}-${stamp}@example.com`, password: 'correct horse battery', display_name: name, time_zone: zone });
  return { id: r.user.id, token: r.token, name };
};
const adam = await signup('Adam', 'Asia/Dubai');
const maya = await signup('Maya', 'Asia/Shanghai');
const room = await api('/v1/rooms', { kind: 'direct', name: 'Adam + Maya', reporting_zone: 'Asia/Dubai' }, adam.token);
const invite = await api(`/v1/rooms/${room.id}/invites`, {}, adam.token);
await api('/v1/invites/redeem', { token: invite.token }, maya.token);

// the machine's GPU on Windows (ANGLE/D3D11); SwiftShader elsewhere
const gl = process.platform === 'win32' ? ['--use-angle=d3d11', '--enable-gpu'] : ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'];
const browser = await chromium.launch({ channel: 'chrome', headless: true, args: [...gl, '--ignore-gpu-blocklist'] });
const results = [];
const asUser = async (user, viewport = { width: 1440, height: 900 }) => {
  const ctx = await browser.newContext({ viewport, deviceScaleFactor: 1 });
  await ctx.addInitScript(({ token, roomId }) => {
    localStorage.setItem('samesky.mode', 'live');
    localStorage.setItem('samesky.token', token);
    localStorage.setItem('samesky.room.live', roomId);
    localStorage.setItem('samesky.theme', 'light');
    localStorage.setItem('samesky.onboarding.live', 'done');
  }, { token: user.token, roomId: room.id });
  const page = await ctx.newPage();
  page.on('pageerror', (e) => results.push(`page error (${user.name}): ${e.message}`));
  return { ctx, page };
};
const settle = (page) => page.waitForFunction(() => !document.querySelector('.avatar-loading, .keepsake-render.loading'), null, { timeout: 120_000 }).catch(() => results.push('renders still loading'));

/** The same state at each capture size; the page must fit the viewport (no sideways scroll) at every one. */
async function shots(page, name, { anchor = null, extra = null } = {}) {
  for (const [w, h] of SIZES) {
    await page.setViewportSize({ width: w, height: h });
    await page.waitForTimeout(700);
    if (anchor) await page.evaluate((sel) => document.querySelector(sel)?.scrollIntoView({ block: 'start' }), anchor);
    await page.waitForTimeout(300);
    const wide = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    if (wide > 1) results.push(`FAIL ${name} ${w}x${h}: page is ${wide}px wider than the viewport`);
    await page.screenshot({ path: `${out}/${name}-${w}x${h}.jpg`, type: 'jpeg', quality: 84 });
    if (extra && w < 600) {
      await page.evaluate((sel) => document.querySelector(sel)?.scrollIntoView({ block: 'center' }), extra);
      await page.waitForTimeout(300);
      await page.screenshot({ path: `${out}/${name}-actions-${w}x${h}.jpg`, type: 'jpeg', quality: 84 });
    }
  }
  await page.setViewportSize({ width: 1440, height: 900 });
}

// --- Maya: onboarding prompt, consent (generation not configured), import, review, approve -------------
{
  const { ctx, page } = await asUser(maya);
  await page.goto(`${base}/#/day`);
  await page.evaluate(() => document.fonts.ready);
  await page.waitForSelector('text=Let people recognise you at the table', { timeout: 30_000 });
  await settle(page);
  await shots(page, 'avatar-onboarding');
  results.push('PASS onboarding prompt shown for a live user with a demo avatar');
  await page.click('text=Create from my photos');
  await page.waitForSelector('text=Before you start', { timeout: 30_000 });
  const hashAfter = await page.evaluate(() => location.hash);
  results.push(`${hashAfter === '#/settings' ? 'PASS' : 'FAIL'} onboarding link opens Settings at the consent step (hash now ${hashAfter})`);
  await shots(page, 'avatar-consent', { anchor: '.avatar-studio', extra: '.consent-check' });
  const notSetUp = await page.isVisible('text=isn’t set up on this server');
  results.push(`${notSetUp ? 'PASS' : 'FAIL'} consent step states that generation isn't configured (no provider credentials)`);
  await page.click('text=Import a model instead');
  await page.waitForSelector('text=Import a model made from your photos');
  await page.getByLabel('This model was made from photos of me, with my consent.').check();
  await page.setInputFiles('input[type=file][accept*=".glb"]', resolve('tests/fixtures/test-mannequin.glb'));
  await page.waitForSelector('text=Does this look like you?', { timeout: 60_000 });
  await page.waitForFunction(() => document.querySelectorAll('.review-view img').length === 4, null, { timeout: 120_000 });
  await page.waitForTimeout(1500);
  await shots(page, 'avatar-review', { anchor: '.avatar-studio', extra: '.review-actions' });
  results.push('PASS manual import validated; review shows front, three-quarter, side and seated views');
  await page.click('text=This looks like me — use this avatar');
  await page.waitForSelector('text=Made from your photos', { timeout: 30_000 });
  await settle(page);
  await shots(page, 'avatar-active', { anchor: '.avatar-studio' });
  results.push('PASS approval activates the exact version (Settings shows it in use)');
  await ctx.close();
}

// --- Adam: sees Maya's approved avatar in portraits and in the room ------------------------------------
{
  const view = await api(`/v1/users/${maya.id}/avatar`, null, adam.token, 'GET');
  results.push(`${view.avatar?.kind === 'photo_derived' ? 'PASS' : 'FAIL'} partner sees kind=${view.avatar?.kind} (version ${view.avatar?.assetVersion})`);
  const { ctx, page } = await asUser(adam);
  await page.goto(`${base}/#/together`);
  await page.evaluate(() => document.fonts.ready);
  await page.waitForTimeout(1500);
  await settle(page);
  await page.screenshot({ path: `${out}/avatar-together-1440x900.jpg`, type: 'jpeg', quality: 84 });
  await ctx.close();
}

// --- live co-presence (two desktop browsers, no headset), then "stop sharing" while both are in the room -
{
  const m = await api(`/v1/rooms/${room.id}/moments`, { kind: 'text', caption: 'Tea by the window' }, adam.token);
  const resolved = await api(`/v1/moments/${m.id}/audience/resolve`, { recipient_ids: [maya.id] }, adam.token);
  await api(`/v1/moments/${m.id}/share`, { recipient_ids: [maya.id], confirmation_token: resolved.confirmationToken }, adam.token);
  const session = await api(`/v1/rooms/${room.id}/sessions`, { moment_id: m.id, participant_ids: [maya.id] }, adam.token);
  const url = `${base}/room.html?mode=live&room=${room.id}&session=${session.id}&capture=1`;
  const M = await asUser(maya);
  const A = await asUser(adam);
  for (const p of [M.page, A.page]) {
    await p.goto(url);
    await p.waitForFunction(() => window.__sameskyReady === true, null, { timeout: 180_000 }).catch(() => results.push('FAIL room not ready'));
    await p.evaluate(() => document.getElementById('enter-2d')?.click());
  }
  const live = await A.page.waitForFunction(() => {
    const s = window.__sameskyPresence?.();
    return s && s.presence === 'live' && s.figure === 'photo_derived' ? s : null;
  }, null, { timeout: 45_000 }).then((h) => h.jsonValue()).catch(() => null);
  results.push(`${live ? 'PASS' : 'FAIL'} live co-presence on desktop: Adam sees Maya live as her approved model (${JSON.stringify(live ?? await A.page.evaluate(() => window.__sameskyPresence?.()))})`);
  await A.page.waitForTimeout(2000);
  const safe = await A.page.evaluate(() => window.__sameskySafeArea?.() ?? null);
  results.push(`${safe && !safe.violations.length ? 'PASS' : 'FAIL'} room safe area with the photo-avatar path ${safe ? JSON.stringify(safe.violations) : ''}`);
  await A.page.screenshot({ path: `${out}/avatar-room-live-1440x900.jpg`, type: 'jpeg', quality: 84 });

  // the same approved model in a headset view: Adam enters VR in Meta's emulator (IWER, emulated Quest 3),
  // seated at 1.2 m eye height; Maya stays live across the table
  if (await A.page.evaluate(() => !!window.IWER_DEVICE)) {
    await A.page.evaluate(() => document.getElementById('enter-vr')?.click());
    const inVr = await A.page.waitForFunction(() => window.__sameskyState?.().presenting === true, null, { timeout: 30_000 }).then(() => true).catch(() => false);
    await A.page.evaluate(() => {
      const ui = window.IWER_DEVICE.devui.inputLayer;
      ui.combinedCameraPosition.set(0, 1.2, 0);
      ui.cameraRig.position.set(0, 1.2, 0);
      ui.cameraRig.quaternion.set(-0.0872, 0, 0, 0.9962); // ~10° down
      window.IWER_DEVICE.primaryInputMode = 'hand';
    });
    await A.page.waitForTimeout(2500);
    const xr = await A.page.evaluate(() => window.__sameskyPresence?.());
    results.push(`${inVr && xr?.figure === 'photo_derived' && xr?.presence === 'live' ? 'PASS' : 'FAIL'} in Meta's emulator (immersive session) Adam sees Maya live as her approved model (${JSON.stringify(xr)})`);
    await A.page.screenshot({ path: `${out}/avatar-room-emulator-1440x900.jpg`, type: 'jpeg', quality: 84 });
    await A.page.evaluate(() => window.IWER_DEVICE.activeSession?.end());
    await A.page.waitForFunction(() => window.__sameskyState?.().presenting === false, null, { timeout: 15_000 }).catch(() => undefined);
    await A.page.evaluate(() => document.getElementById('enter-2d')?.click()); // back to the screen view
    await A.page.waitForTimeout(1500);
  }

  const studio = await api('/v1/me/avatar-studio', null, maya.token, 'GET');
  await api(`/v1/me/avatar-assets/${studio.activeAssetId}`, { visibility: 'private' }, maya.token, 'PATCH');
  const swapped = await A.page.waitForFunction(() => window.__sameskyPresence?.().figure === 'preset_demo', null, { timeout: 45_000 }).then(() => true).catch(() => false);
  results.push(`${swapped ? 'PASS' : 'FAIL'} "stop sharing" reaches the open room: Adam's view swaps to her labelled demo avatar without a reload`);
  await A.page.waitForTimeout(1500);
  await A.page.screenshot({ path: `${out}/avatar-room-stopped-sharing-1440x900.jpg`, type: 'jpeg', quality: 84 });
  const view = await api(`/v1/users/${maya.id}/avatar`, null, adam.token, 'GET');
  results.push(`${view.avatar?.kind === 'preset_demo' && !view.avatar?.modelUrl ? 'PASS' : 'FAIL'} after "stop sharing" the API gives the partner kind=${view.avatar?.kind} and no model link`);

  // sharing again brings the same approved version back to the open room
  await api(`/v1/me/avatar-assets/${studio.activeAssetId}`, { visibility: 'rooms' }, maya.token, 'PATCH');
  const back = await A.page.waitForFunction(() => window.__sameskyPresence?.().figure === 'photo_derived', null, { timeout: 45_000 }).then(() => true).catch(() => false);
  results.push(`${back ? 'PASS' : 'FAIL'} sharing again restores her approved model in the open room`);

  // renderer cost with one live skinned avatar (software WebGL: counts are meaningful, frame times are not)
  await A.page.goto(`${url}&debug=1`);
  await A.page.waitForFunction(() => window.__sameskyReady === true, null, { timeout: 180_000 }).catch(() => undefined);
  await A.page.evaluate(() => document.getElementById('enter-2d')?.click());
  await A.page.waitForFunction(() => window.__sameskyPresence?.().figure === 'photo_derived', null, { timeout: 45_000 }).catch(() => undefined);
  await A.page.waitForTimeout(2500);
  const stats = await A.page.evaluate(() => {
    const r = window.__samesky?.world?.renderer;
    return r ? { calls: r.info.render.calls, triangles: r.info.render.triangles, textures: r.info.memory.textures, geometries: r.info.memory.geometries } : null;
  });
  results.push(`INFO room with one live photo-derived avatar (test mannequin), 1440x900: ${JSON.stringify(stats)}`);
  await M.ctx.close();
  await A.ctx.close();
}
await browser.close();
console.log(results.join('\n'));
process.exitCode = results.some((r) => r.startsWith('FAIL') || r.startsWith('page error')) ? 1 : 0;
