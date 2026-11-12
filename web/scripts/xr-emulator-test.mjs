// SameSky in Meta's Immersive Web Emulator (IWER, injected on localhost by @iwsdk/vite-plugin-iwer as a
// Meta Quest 3).
//
// Starts a real immersive-vr session in Chrome against the emulated headset, seats the headset at a seated
// eye height, and drives the emulated hands and controllers through IWER's device object (window.IWER_DEVICE):
//   - poke dock buttons with the index fingertip (Recenter, Captions, Reply, Not now, Leave)
//   - point at the cup and pinch (the defining moment: Maya's voice note plays from the cup)
//   - drop hand tracking and re-acquire it (nothing may fire)
//   - switch to controllers, aim at the screen's "Play our day" and pull the trigger
// Aiming is by measurement: the room reports each input's actual target ray and fingertip every frame
// (window.__sameskyInputs, emulator only), and the script corrects the pose until it is on target.
// Every step asserts on the room's own state (window.__sameskyState) and screenshots the emulator view.
//
// Emulation in desktop Chrome (SwiftShader here) checks session setup, input wiring, layout and state. It
// cannot measure Quest frame time, comfort or real hand-tracking quality.
//
//   node scripts/xr-emulator-test.mjs --base http://localhost:5173 --out ../docs/visual-qa/emulator
import { mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { chromium } from 'playwright-core';

const arg = (n, d) => (process.argv.includes(`--${n}`) ? process.argv[process.argv.indexOf(`--${n}`) + 1] : d);
const base = arg('base', 'http://localhost:5173');
const out = resolve(arg('out', '../docs/visual-qa/emulator'));
mkdirSync(out, { recursive: true });

// The machine's GPU through ANGLE/D3D11 on Windows (real frame rates); SwiftShader elsewhere (e.g. CI).
const renderer = arg('renderer', process.platform === 'win32' ? 'd3d11' : 'swiftshader');
const gl = renderer === 'swiftshader' ? ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] : [`--use-angle=${renderer}`, '--enable-gpu'];
const browser = await chromium.launch({ channel: 'chrome', headless: arg('headed', 'false') !== 'true', args: [...gl, '--ignore-gpu-blocklist', '--autoplay-policy=no-user-gesture-required'] });
const ctx = await browser.newContext({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 1 });
const page = await ctx.newPage();
const results = [];
const errors = [];
page.on('pageerror', (e) => errors.push(e.message));
page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
await page.addInitScript(() => {
  localStorage.clear();
  localStorage.setItem('samesky.mode', 'sample');
  localStorage.setItem('samesky.theme', 'light');
  localStorage.setItem('samesky.onboarding.sample', 'cup');
});

// ---- small vector / quaternion helpers (x, y, z, w) -------------------------------------------------------------
const v = (a) => (Array.isArray(a) ? { x: a[0], y: a[1], z: a[2] } : a);
const arr = (a) => [a.x, a.y, a.z];
const add = (a, b) => ({ x: a.x + b.x, y: a.y + b.y, z: a.z + b.z });
const sub = (a, b) => ({ x: a.x - b.x, y: a.y - b.y, z: a.z - b.z });
const scale = (a, s) => ({ x: a.x * s, y: a.y * s, z: a.z * s });
const len = (a) => Math.hypot(a.x, a.y, a.z);
const norm = (a) => scale(a, 1 / (len(a) || 1));
const dot = (a, b) => a.x * b.x + a.y * b.y + a.z * b.z;
const cross = (a, b) => ({ x: a.y * b.z - a.z * b.y, y: a.z * b.x - a.x * b.z, z: a.x * b.y - a.y * b.x });
const qmul = (a, b) => [
  a[3] * b[0] + a[0] * b[3] + a[1] * b[2] - a[2] * b[1],
  a[3] * b[1] - a[0] * b[2] + a[1] * b[3] + a[2] * b[0],
  a[3] * b[2] + a[0] * b[1] - a[1] * b[0] + a[2] * b[3],
  a[3] * b[3] - a[0] * b[0] - a[1] * b[1] - a[2] * b[2],
];
const qnorm = (q) => { const l = Math.hypot(...q) || 1; return q.map((x) => x / l); };
/** Rotation taking unit vector a onto unit vector b. */
const qFromTo = (a, b) => {
  const c = cross(a, b);
  return qnorm([c.x, c.y, c.z, 1 + dot(a, b)]);
};
const qPitch = (deg) => [Math.sin((deg * Math.PI) / 360), 0, 0, Math.cos((deg * Math.PI) / 360)];

// ---- page access ------------------------------------------------------------------------------------------------
const pass = (ok, text) => { results.push(`${ok ? 'PASS' : 'FAIL'} ${text}`); return ok; };
const verbose = process.argv.includes('--verbose');
const f3 = (a) => (a ? (Array.isArray(a) ? a : arr(a)).map((n) => n.toFixed(3)).join(',') : '-');
const log = (...a) => { if (verbose) console.log(...a); };
const state = () => page.evaluate(() => window.__sameskyState());
const inputs = () => page.evaluate(() => window.__sameskyInputs?.() ?? []);
const target = async (label) => {
  const all = await page.evaluate(() => window.__sameskyTargets());
  const t = all.find((x) => x.label === label || x.label.startsWith(label));
  if (!t) throw new Error(`no target ${label}; have ${all.map((x) => x.label).join(', ')}`);
  return { ...t, p: v(t.position) };
};
// IWER's DevUI owns the poses: every frame it copies its camera rig into the headset and its two transform
// handles (children of the camera rig) into whichever input mode is primary. So poses are set on those rig
// objects, in world space, exactly as dragging the DevUI's gizmos would.
const emu = (fn, ...args) => page.evaluate(([f, a]) => {
  const d = window.IWER_DEVICE;
  const ui = d.devui.inputLayer;
  const handle = (side) => ui.transformHandles.get(side);
  const input = (kind, side) => (kind === 'hand' ? d.hands[side] : d.controllers[side]);
  const setWorld = (obj, p, q) => {
    if (p) obj.position.copy(obj.parent.worldToLocal(obj.position.clone().set(p[0], p[1], p[2])));
    if (q) {
      const parentQ = obj.parent.getWorldQuaternion(obj.quaternion.clone()).invert();
      obj.quaternion.copy(parentQ.multiply(obj.quaternion.clone().set(q[0], q[1], q[2], q[3])));
    }
    obj.updateMatrixWorld(true);
  };
  const worldOf = (obj) => {
    const p = obj.getWorldPosition(obj.position.clone());
    const q = obj.getWorldQuaternion(obj.quaternion.clone());
    return { p: [p.x, p.y, p.z], q: [q.x, q.y, q.z, q.w] };
  };
  const ops = {
    // Unless the mouse is pointer-locked, the DevUI re-applies its stored head position every frame
    // (cameraRig.y and playerRig.x/z from combinedCameraPosition), so that is what sets the head.
    head: (p, q) => {
      ui.playerRig.quaternion.set(0, 0, 0, 1);
      if (p) {
        ui.combinedCameraPosition.set(p[0], p[1], p[2]);
        ui.cameraRig.position.set(0, p[1], 0);
        ui.playerRig.position.set(p[0], 0, p[2]);
      }
      if (q) ui.cameraRig.quaternion.set(q[0], q[1], q[2], q[3]);
      ui.playerRig.updateMatrixWorld(true);
    },
    headGet: () => worldOf(ui.cameraRig),
    mode: (m) => { d.primaryInputMode = m; },
    get: (_kind, side) => worldOf(handle(side)),
    set: (_kind, side, p, q) => setWorld(handle(side), p, q),
    pinch: (side, value) => { d.hands[side].updatePinchValue(value); return d.hands[side].pinchValue; },
    pinchGet: (side) => d.hands[side].pinchValue,
    trigger: (side, value) => d.controllers[side].updateButtonValue('trigger', value),
    connect: (kind, side, on) => { input(kind, side).connected = on; },
  };
  return ops[f](...a);
}, [fn, args]);
async function frames(n = 2) {
  const start = (await state()).frame;
  const end = Date.now() + 20_000;
  while (Date.now() < end && (await state()).frame < start + n) await page.waitForTimeout(60);
}
async function until(fn, ms = 10_000) {
  const end = Date.now() + ms;
  while (Date.now() < end) {
    if (await fn()) return true;
    await page.waitForTimeout(120);
  }
  return false;
}
const measured = async (kind, side) => {
  await frames(2);
  const m = (await inputs()).find((x) => x.kind === kind && x.handedness === side);
  if (!m) throw new Error(`no ${kind} ${side} input in the session`);
  return m;
};

/** Move an input until its measured ray origin is at `want` (works whatever IWER's internal offsets are). */
async function placeAt(kind, side, want) {
  for (let i = 0; i < 4; i++) {
    const m = await measured(kind, side);
    const err = sub(want, v(m.origin));
    log(`placeAt ${kind}-${side} want=${f3(want)} origin=${f3(m.origin)} err=${len(err).toFixed(4)}`);
    if (len(err) < 0.004) return;
    const cur = await emu('get', kind, side);
    await emu('set', kind, side, arr(add(v(cur.p), err)), null);
  }
}
/** Rotate an input until its measured target ray points at `point`. */
async function aimAt(kind, side, point) {
  for (let i = 0; i < 5; i++) {
    const m = await measured(kind, side);
    const want = norm(sub(point, v(m.origin)));
    const have = norm(v(m.direction));
    log(`aimAt ${kind}-${side} point=${f3(point)} origin=${f3(m.origin)} have=${f3(have)} want=${f3(want)} dot=${dot(want, have).toFixed(5)}`);
    if (dot(want, have) > 0.99995) return;
    const cur = await emu('get', kind, side);
    await emu('set', kind, side, null, qnorm(qmul(qFromTo(have, want), cur.q)));
  }
}
/** Move a hand until its measured index fingertip is at `want`. */
async function placeTip(side, want, tries = 4) {
  for (let i = 0; i < tries; i++) {
    const m = await measured('hand', side);
    if (!m.tip) throw new Error('no fingertip joint');
    const err = sub(want, v(m.tip));
    log(`placeTip ${side} want=${f3(want)} tip=${f3(m.tip)} err=${len(err).toFixed(4)}`);
    if (len(err) < 0.003) return;
    const cur = await emu('get', 'hand', side);
    await emu('set', 'hand', side, arr(add(v(cur.p), err)), null);
  }
}
let headPos = { x: 0, y: 1.2, z: 0 };
/**
 * Tap a flat target with the right index fingertip. Line the fingertip up 6 cm in front of the surface along
 * the head-to-target direction, then press 0.8 cm through and come back out in consecutive frames.
 * IWSDK's touch pointer presses within 3 cm of a surface, releases (= click) while the target is still in its
 * 10 cm hover range, and only counts a press-release within 800 ms as a click; software rendering runs the
 * emulated session at a few frames per second, so the tap has to be a quick one, as a real poke is.
 */
async function poke(label) {
  const t = await target(label);
  // press straight into the panel (against its surface normal, turned toward the person), like a finger does
  let n = norm(v(t.normal));
  if (dot(n, sub(headPos, t.p)) < 0) n = scale(n, -1);
  const d = scale(n, -1);
  log(`poke ${label} at ${f3(t.p)} size=${f3(t.size)} head=${f3(headPos)}`);
  await placeTip('right', add(t.p, scale(d, -0.14)));
  await aimAt('hand', 'right', add(t.p, scale(d, 0.3)));
  await placeTip('right', add(t.p, scale(d, -0.06)));
  const m = await measured('hand', 'right');
  const cur = await emu('get', 'hand', 'right');
  const offset = sub(v(m.tip), v(cur.p)); // fingertip relative to the DevUI hand handle, at this orientation
  const handleFor = (depth) => arr(sub(add(t.p, scale(d, depth)), offset));
  await emu('set', 'hand', 'right', handleFor(0.008), null);
  await frames(1);
  await emu('set', 'hand', 'right', handleFor(-0.06), null);
  await frames(2);
  await emu('set', 'hand', 'right', handleFor(-0.16), null);
  await frames(1);
}
/** Point an input at a target from a raised, out-of-reach position, so near touch targets don't take over. */
const RAISED = { x: 0.22, y: 1.16, z: -0.22 };
let shot = 0;
const capture = async (name) => {
  await frames(2);
  await page.screenshot({ path: `${out}/${String(++shot).padStart(2, '0')}-${name}.jpg`, type: 'jpeg', quality: 82, timeout: 180_000 });
};

try {
  // ---- load and enter VR -------------------------------------------------------------------------------------
  await page.goto(`${base}/room.html?mode=sample&capture=1${verbose ? '&debug=1' : ''}`);
  await page.waitForFunction(() => window.__sameskyReady === true && !!window.IWER_DEVICE, null, { timeout: 180_000 });
  if (verbose) {
    // diagnostics: record pointer events reaching each labelled target (needs ?debug=1 for the pressables map)
    await page.evaluate(() => {
      window.__evlog = [];
      for (const p of window.__samesky.pressables.values()) {
        if (!p.label) continue;
        for (const type of ['pointerenter', 'pointerdown', 'pointerup', 'click', 'pointercancel']) {
          p.object.addEventListener(type, (e) => window.__evlog.push(`${p.label}:${type}:${e.pointerType ?? ''}`));
        }
      }
    });
  }
  const device = await page.evaluate(async () => {
    const gl = document.createElement('canvas').getContext('webgl2');
    const ext = gl?.getExtension('WEBGL_debug_renderer_info');
    return { name: window.IWER_DEVICE?.name, version: window.IWER_DEVICE?.version, vr: await navigator.xr?.isSessionSupported('immersive-vr'), gpu: ext ? gl.getParameter(ext.UNMASKED_RENDERER_WEBGL) : 'unknown' };
  });
  pass(device.name === 'Meta Quest 3' && device.vr === true, `emulated headset: ${device.name} (IWER ${device.version}), immersive-vr supported`);
  results.push(`INFO renderer: ${device.gpu}`);
  await page.click('#enter-vr');
  pass(await until(async () => (await state()).presenting, 30_000), 'Enter in VR starts an immersive session in the emulator');
  {
    const a = await state();
    await page.waitForTimeout(3000);
    const b = await state();
    results.push(`INFO emulated session frame rate on this machine: ${((b.frame - a.frame) / 3).toFixed(1)} fps (desktop emulation, not Quest performance)`);
  }

  // ---- seated: eye height 1.2 m looking slightly down, hands on; the dock's Recenter re-seats the table -------
  await emu('head', [0, 1.2, 0], qPitch(-12));
  await emu('mode', 'hand');
  await frames(3);
  headPos = v((await state()).head);
  log(`head (rig) ${f3((await emu('headGet')).p)} head (camera) ${f3(headPos)}`);
  await placeAt('hand', 'right', { x: 0.2, y: 0.95, z: -0.3 });
  await placeAt('hand', 'left', { x: -0.2, y: 0.95, z: -0.3 });
  const before0 = (await state()).roomOffset[1];
  await poke('dock:recenter');
  const s0 = await state();
  const expected = Math.min(0.15, Math.max(-0.25, s0.head[1] - 1.18));
  await until(async () => Math.abs((await state()).roomOffset[1] - expected) < 0.005, 4000);
  const s0b = await state();
  pass(Math.abs(s0b.roomOffset[1] - expected) < 0.005, `poking Recenter with the index fingertip re-seats the room for the seated eye height (${s0.head[1].toFixed(2)} m): offset ${before0.toFixed(3)} -> ${s0b.roomOffset[1].toFixed(3)}`);
  await capture('seated-hands');
  const safe = await page.evaluate(() => window.__sameskySafeArea?.() ?? null);
  pass(!!safe && safe.violations.length === 0, `seated headset view: screen, presence badge, cup label, hint and dock are clear of each other and in view ${safe ? JSON.stringify(safe.violations) : ''}`);

  // ---- hands: point at the cup from the seat and pinch (the defining moment) --------------------------------------
  const cup = await target('far:cup:');
  await placeAt('hand', 'right', RAISED);
  await aimAt('hand', 'right', cup.p);
  log(`cup hovered by the ray: ${(await target('far:cup:')).hovered}`);
  await emu('pinch', 'right', 1);
  await frames(1);
  await emu('pinch', 'right', 0);
  const grabbed = await until(async () => (await state()).voice !== null, 10_000);
  if (verbose) log(`events: ${(await page.evaluate(() => window.__evlog.splice(0))).join(' ')}`);
  await capture('cup-point-pinch');
  const s1 = await state();
  pass(grabbed && s1.presence === 'recorded', `point + pinch on the cup plays Maya's voice note (voice=${s1.voice}) and her label switches to ${s1.presence}`);

  // ---- hands: dock buttons by poke --------------------------------------------------------------------------------
  const cc0 = (await state()).captionsOn;
  await poke('dock:cc');
  pass(await until(async () => (await state()).captionsOn !== cc0, 4000), `poking Captions toggles captions (${cc0} -> ${!cc0})`);
  if (verbose) log(`events: ${(await page.evaluate(() => window.__evlog.splice(0))).join(' ')}`);
  await poke('dock:cc');
  await until(async () => (await state()).captionsOn === cc0, 4000);

  await poke('dock:reply');
  pass(await until(async () => (await state()).replySheet, 4000), 'poking Reply opens the reply sheet');
  await capture('reply-sheet');
  await poke('button:Not now');
  pass(await until(async () => !(await state()).replySheet, 4000), 'poking “Not now” closes the reply sheet');

  // ---- tracking loss: the hand disappears and comes back; nothing fires on re-acquisition --------------------------
  const before = await state();
  await emu('connect', 'hand', 'right', false);
  await frames(3);
  await emu('connect', 'hand', 'right', true);
  await frames(4);
  const after = await state();
  pass(after.presenting && after.replySheet === before.replySheet && after.screen === before.screen && after.story === before.story, 'losing and re-acquiring hand tracking keeps the session and triggers nothing');

  // ---- controllers: aim at the screen's Play button and pull the trigger ----------------------------------------
  await emu('mode', 'controller');
  await frames(3);
  const play = await target('button:Play our day');
  await placeAt('controller', 'right', RAISED);
  await aimAt('controller', 'right', play.p);
  await capture('controller-aim');
  await emu('trigger', 'right', 1);
  await frames(1);
  await emu('trigger', 'right', 0);
  pass(await until(async () => { const s = await state(); return s.story || s.screen !== before.screen; }, 8000), 'a controller ray + trigger on “Play our day” starts the story on the shared screen');
  await capture('controller-play-our-day');

  // ---- leave through the dock (hands again) ---------------------------------------------------------------------------
  await emu('mode', 'hand');
  await frames(3);
  await placeAt('hand', 'right', { x: 0.2, y: 0.95, z: -0.3 });
  await poke('dock:exit');
  const left = await until(async () => !(await state()).presenting, 10_000);
  if (verbose) log(`events: ${(await page.evaluate(() => window.__evlog.splice(0))).join(' ')}`);
  pass(left, 'poking Leave ends the immersive session');
  await page.waitForTimeout(1000);
  pass(await page.evaluate(() => !document.getElementById('overlay')?.hidden), 'the welcome card returns after leaving VR');
  await capture('after-exit');
} catch (e) {
  results.push(`FAIL ${e.message}`);
  await page.screenshot({ path: `${out}/failure.jpg`, type: 'jpeg', quality: 80, timeout: 120_000 }).catch(() => undefined);
}

const relevant = errors.filter((e) => !/favicon|DevTools|GPU stall|WebGL: INVALID|autoplay/i.test(e));
pass(relevant.length === 0, `no page errors${relevant.length ? `: ${relevant.slice(0, 3).join(' | ')}` : ''}`);
await browser.close();
console.log(results.join('\n'));
process.exitCode = results.some((r) => r.startsWith('FAIL')) ? 1 : 0;
