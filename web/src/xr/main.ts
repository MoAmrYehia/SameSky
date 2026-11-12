// SameSky's hands-first room (Meta Quest 3/3S via IWSDK, with a desktop/phone fallback in the same page).
//
// Defining interaction: an object with a message waiting inside it. Pick up the glowing cup and the
// recorded voice note plays from the cup itself (spatial audio) with captions; place a reply beside it
// and it waits for the next visit. Frames and postcards open their photo/clip on the shared screen.
//
// Composition: the shared screen floats toward the left, the person across sits in the armchair to the
// right, the cup and its waiting-message label sit in the foreground, and one compact dock rests at the
// table edge. In a headset these are anchored in table space at comfortable reach — the user's head pose is
// never moved to force a composition. On a desktop or phone the same anchors are fitted to the viewport and
// checked against a safe area (window.__sameskySafeArea, used by scripts/capture.mjs).
//
// Rendering: PMREM image-based light from a CC0 interior HDR (IWSDK IBLTexture), Neutral tone mapping,
// one bounded soft shadow light, unlit (toneMapped:false) interface panels, sRGB-tagged colour textures.
// Per-frame code is allocation-free; canvases are redrawn only when their content changes.

import '@fontsource-variable/figtree';
import {
  AudioSource, AudioUtils, createSystem, type Entity, Grabbed, Hovered, IBLTexture,
  OneHandGrabbable, ReferenceSpaceType, SessionMode, World,
} from '@iwsdk/core';
import {
  AudioListener, Box3, BoxGeometry, CanvasTexture, Color, Euler, Group, LoadingManager, Matrix4, Mesh, MeshBasicMaterial, NeutralToneMapping, type Object3D,
  PCFSoftShadowMap, PlaneGeometry, Quaternion, SRGBColorSpace, type Texture, TextureLoader, Vector3, VideoTexture,
} from 'three';
import { cueAt, loadCaptions, type Cue } from '../shared/captions';
import { acquireAvatarModel, SkinnedAvatar } from '../shared/avatarRig';
import { type CharacterKit, CharacterModel, loadCharacterKit } from '../shared/character';
import type { AvatarView, Award, Me, Moment, Recap, Reply, Room, RoomObject } from '../shared/contracts';
import { currentMode, dataSource, SampleSource, storedToken } from '../shared/data';
import { buildKeepsake, texturesReady } from '../shared/keepsakes3d';
import { renderAvatarPortrait, setPortraitViewer } from '../shared/portrait';
import { SAMPLE_PEOPLE } from '../shared/sample';
import { cityOf } from '../shared/tz';
import { Avatar3D } from './avatar3d';
import { emulatorActive, prepareEmulatedSession } from './emulator';
import { buildEnvironment, contactShadow, type RoomEnvironment, TABLE } from './env';
import { LiveSession, type PoseMessage } from './live';
import { buildMemoryObject, infoCard, type MemoryObject } from './objects';
import {
  Button, CanvasPanel, COLORS, drawIcon, FittedCard, FittedPill, FONT, glass, guardInput, hitBox, loadUiFont, makePressable, type Pressable,
  pressables, registerPressable, roundRect, wrapText,
} from './ui3d';

declare global {
  interface Window {
    __sameskyReady?: boolean;
    __sameskySafeArea?: () => SafeAreaReport;
    /** Automated checks: who is across the table, and as what figure. */
    __sameskyPresence?: () => { presence: string; figure: string | null; avatarKey: string };
    /** Automated checks (e.g. the emulator test): named interactive targets with world positions. */
    __sameskyTargets?: () => { label: string; position: [number, number, number]; size: [number, number, number]; normal: [number, number, number]; enabled: boolean; hovered: boolean }[];
    /** Automated checks: the room's observable state. */
    __sameskyState?: () => RoomState;
    /** Emulator only: each input's measured target ray and index fingertip (world space). */
    __sameskyInputs?: () => InputSnapshot[];
  }
}

const params = new URLSearchParams(location.search);
const mode: 'sample' | 'live' = params.get('mode') === 'sample' ? 'sample' : params.get('mode') === 'live' || (currentMode() === 'live' && storedToken()) ? 'live' : 'sample';
const ds = dataSource(mode);
const debug = params.has('debug');
/** Deterministic captures: idle animation runs at a fixed time and nobody blinks mid-shot. */
const capture = params.has('capture');
/**
 * ?preview=xr — documentation aid without a headset: applies the headset (table-space) layout and renders
 * from a seated head position with a Quest-like field of view. Not a substitute for on-device testing.
 */
let xrPreview = false;
const CAPTURE_T = 4.2;
const touch = matchMedia('(pointer: coarse)').matches;
const $ = (id: string) => document.getElementById(id)!;
const status = (text: string) => { $('status').textContent = text; };
const verb = () => (world?.renderer.xr.isPresenting ? 'Pinch' : touch ? 'Tap' : 'Click');
const fmt = (s: number) => `${Math.floor(s / 60)}:${String(Math.floor(s % 60)).padStart(2, '0')}`;

// ---------------------------------------------------------------------------
// State
// ---------------------------------------------------------------------------

let world: World;
let env: RoomEnvironment;
let kit: CharacterKit;
let me: Me;
let room: Room;
let moments: Moment[] = [];
let recap: Recap | null = null;
let awards: Award[] = [];
const memory = new Map<string, MemoryObject & { entity: Entity; far: Mesh }>();
let tableEntity: Entity;
let roomEntity: Entity;
let captionsOn = true;
let muted = false;
let live: LiveSession | null = null;
let partner: Avatar3D | null = null;
let partnerLastSeen = 0;
let partnerKind: 'live' | 'simulated' | 'recorded' | 'away' = 'simulated';
const onboardingKey = `samesky.onboarding.${mode}`;
let onboarding = (() => { try { return localStorage.getItem(onboardingKey) ?? 'cup'; } catch { return 'cup'; } })();
const load = { loaded: 0, total: 0, errors: [] as string[] };

// ---------------------------------------------------------------------------
// Boot: data and heavy assets load in parallel, with visible progress and a recoverable error state
// ---------------------------------------------------------------------------

function setProgress(fraction: number) {
  const pct = Math.max(0, Math.min(100, Math.round(fraction * 100)));
  ($('load-bar') as HTMLElement).style.width = `${pct}%`;
  $('load').setAttribute('aria-valuenow', String(pct));
}

function showLoadError(message: string) {
  $('overlay').hidden = false;
  $('overlay-tag').textContent = 'The room didn’t load';
  $('overlay-text').textContent = message;
  $('overlay-text').classList.add('error-text');
  $('load').hidden = true;
  ($('enter-2d') as HTMLButtonElement).hidden = true;
  ($('enter-vr') as HTMLButtonElement).hidden = true;
  const retry = $('retry') as HTMLButtonElement;
  retry.hidden = false;
  retry.onclick = () => location.reload();
  $('overlay-hint').textContent = 'Your moments are safe — only the room’s 3D files failed to download. The companion still works.';
}

async function boot() {
  if (mode === 'sample') $('mode-tag').innerHTML = 'Sample · fictional people<span class="tag-long"> · nothing leaves this browser</span>';
  else $('mode-tag').textContent = 'Signed in';
  const enter2d = $('enter-2d') as HTMLButtonElement;
  if (mode === 'live' && !storedToken()) {
    $('overlay-tag').textContent = 'Sign-in needed';
    $('overlay-text').textContent = 'Open the companion to sign in, or try the sample room.';
    $('load').hidden = true;
    enter2d.disabled = false;
    enter2d.textContent = 'Try the sample room';
    enter2d.onclick = () => { location.href = '/room.html?mode=sample'; };
    return;
  }

  // Heavy downloads start immediately, alongside the data requests.
  const manager = new LoadingManager();
  manager.onStart = (_u, loaded, total) => { load.loaded = loaded; load.total = total; };
  manager.onProgress = (_u, loaded, total) => { load.loaded = loaded; load.total = total; setProgress(loaded / Math.max(1, total)); };
  manager.onError = (url) => load.errors.push(url);
  const assets = Promise.all([buildEnvironment(manager), loadCharacterKit(manager), loadUiFont()]);
  assets.catch(() => undefined); // awaited (and reported) below

  let rooms: Room[];
  try {
    [me, rooms] = await Promise.all([ds.me(), ds.rooms()]);
  } catch (e) {
    showLoadError(e instanceof Error ? e.message : 'Could not load your room.');
    return;
  }
  const picked = rooms.find((r) => r.id === params.get('room')) ?? rooms.find((r) => r.kind === 'direct') ?? rooms[0];
  if (!picked) {
    $('overlay-text').textContent = 'You don’t have a room yet. Create one in the companion first.';
    $('load').hidden = true;
    return;
  }
  room = picked;
  setPortraitViewer(mode === 'live' ? me.id : null);
  const names = room.members.map((m) => m.displayName).join(' + ');
  $('overlay-tag').textContent = mode === 'sample' ? 'Sample day · fictional people' : room.name;
  $('welcome-title').textContent = room.kind === 'direct' ? names : room.name;
  $('hud-audience-text').textContent = room.kind === 'direct' ? `Private · ${names}` : `Circle · ${room.name}`;

  const [archive, objects, aw] = await Promise.all([ds.archive(room.id), ds.objects(room.id), ds.awards(room.id)]);
  recap = archive[0] ?? null;
  awards = aw;
  moments = await ds.moments(room.id, recap?.roomDate);
  if (!moments.length) moments = await ds.moments(room.id);
  moments = moments.filter((m) => m.sharingState === 'shared');

  world = await World.create($('scene'), {
    xr: { sessionMode: SessionMode.ImmersiveVR, referenceSpace: ReferenceSpaceType.LocalFloor, features: { handTracking: true }, offer: 'none' },
    features: { grabbing: { useHandPinchForGrab: true }, locomotion: false },
    input: { canvasPointerEvents: true },
    render: { fov: 58, near: 0.02, far: 40, camera: { position: [0, 1.24, 0.16], lookAt: [0, 0.9, -0.9] } },
  });
  const renderer = world.renderer;
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
  // IWSDK sets the output colour space only; tone mapping and shadows are tuned for this room.
  renderer.toneMapping = NeutralToneMapping;
  renderer.toneMappingExposure = 0.95;
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = PCFSoftShadowMap;
  world.scene.background = new Color(0x24221f);
  // Image-based light: a CC0 interior HDR, prefiltered through PMREM by IWSDK's EnvironmentSystem.
  world.activeLevel.value.addComponent(IBLTexture, { src: '/assets/env/lounge_1k.hdr', intensity: 0.6 });

  try {
    [env, kit] = await assets;
  } catch (e) {
    console.error(e);
    showLoadError('Part of the room (furniture or the avatar model) could not be downloaded. Check the connection and try again.');
    return;
  }

  roomEntity = world.createTransformEntity(new Group(), { persistent: true });
  roomEntity.object3D!.add(env.root);
  env.root.remove(env.table);
  tableEntity = world.createTransformEntity(env.table, { parent: roomEntity });

  world.registerSystem(RoomSystem);
  buildScreen();
  buildDock();
  buildKeepsakes();
  await placeObjects(objects);
  await setupPartner();
  buildLabels();
  placeDesktopCamera();
  window.addEventListener('resize', () => requestAnimationFrame(placeDesktopCamera));
  // the 2D header can change height (fonts, wrapping): keep the safe area current
  let hudH = 0;
  new ResizeObserver(() => {
    const h = Math.round($('hud').getBoundingClientRect().height);
    if (h !== hudH) { hudH = h; requestAnimationFrame(placeDesktopCamera); }
  }).observe($('hud'));
  window.__sameskySafeArea = safeArea;
  window.__sameskyPresence = () => ({ presence: partnerKind, figure: partner?.character.kind ?? null, avatarKey: partnerAvatarKey.split('|')[0] });
  window.__sameskyTargets = testTargets;
  window.__sameskyState = roomState;
  window.__sameskyInputs = () => inputSnapshot; // stays empty unless the emulator is active
  if (debug) (window as unknown as Record<string, unknown>).__samesky = { world, env, pressables, memory, activate, partner, safeArea, placeDesktopCamera, screen, dock };

  // XR availability + entry.
  const xrOk = await navigator.xr?.isSessionSupported('immersive-vr').catch(() => false);
  const enterVr = $('enter-vr') as HTMLButtonElement;
  enterVr.onclick = () => {
    unlockAudio();
    $('overlay').hidden = true;
    prepareEmulatedSession(); // dev only: lets Meta's emulator (IWER) run the session in desktop Chrome
    world.launchXR();
  };
  enter2d.onclick = () => {
    unlockAudio();
    $('overlay').hidden = true;
  };
  if (!xrOk) $('overlay-hint').textContent = `This browser has no VR headset available, so the room opens on this screen. ${touch ? 'Tap' : 'Click'} things to open them.`;

  world.renderer.xr.addEventListener('sessionstart', () => {
    guardInput(1200);
    const session = world.renderer.xr.getSession();
    session?.addEventListener('inputsourceschange', () => guardInput(600)); // tracking loss/regain never fires a selection
    session?.addEventListener('visibilitychange', () => guardInput(800));
    applyXrLayout();
    refreshVerbs();
    window.setTimeout(recenter, 300);
  });
  world.renderer.xr.addEventListener('sessionend', () => {
    $('overlay').hidden = false;
    roomEntity.object3D!.position.set(0, 0, 0);
    roomEntity.object3D!.rotation.set(0, 0, 0);
    refreshVerbs();
    window.setTimeout(placeDesktopCamera, 50);
  });

  if (params.get('session') && mode === 'live') void joinLive(params.get('session')!);
  if (params.get('watch') === '1') {
    const clip = moments.find((m) => m.kind === 'clip');
    if (clip) window.setTimeout(() => showMoment(clip, { together: true }), 400);
  }

  // Ready: every queued download done (or failed), textures decoded, IBL prefiltered, a couple of frames drawn.
  const started = performance.now();
  while (performance.now() - started < 90_000) {
    await texturesReady();
    if (load.loaded >= load.total && world.scene.environment) break;
    await new Promise((r) => setTimeout(r, 120));
  }
  await new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r)));
  placeDesktopCamera();
  $('load').hidden = true;
  enterVr.hidden = !xrOk;
  enter2d.disabled = false;
  enter2d.textContent = 'Stay on this screen';
  if (load.errors.length) $('overlay-hint').textContent = `Some textures didn’t load (${load.errors.length}); the room still works. Reload to try again.`;
  if (params.get('preview') === 'xr') enterXrPreview();
  window.__sameskyReady = true;
}

function enterXrPreview() {
  xrPreview = true;
  const cam = world.camera;
  cam.fov = 78;
  cam.updateProjectionMatrix();
  const table = tableEntity.object3D!;
  table.updateMatrixWorld(true);
  cam.position.copy(table.localToWorld(SEATED_HEAD.clone()));
  cam.lookAt(0, 0.98, -0.85);
  cam.updateMatrixWorld(true);
  applyXrLayout();
  $('mode-tag').textContent = 'Headset layout preview · seated eye position · not an on-device capture';
}

/** Hints say "pinch" in a headset and "click"/"tap" on a screen: redraw them when the mode changes. */
function refreshVerbs() {
  setOnboarding(onboarding);
  if (!screen.current && !story && !voice) setScreenIdle();
}

function unlockAudio() {
  // Browsers require a user gesture before audio can play; resume IWSDK's listener context now.
  const listener = world.camera.children.find((c) => c instanceof AudioListener) as AudioListener | undefined;
  void listener?.context.resume();
}

// ---------------------------------------------------------------------------
// The shared screen: one glass panel with its header, media, captions and transport inside it
// ---------------------------------------------------------------------------

const SCREEN_W = 0.84;
const SCREEN_H = 0.6;
const MEDIA_W = 0.78;
const MEDIA_H = (MEDIA_W * 9) / 16;

interface Screen {
  group: Group;
  entity: Entity;
  bg: CanvasPanel;
  media: Mesh<PlaneGeometry, MeshBasicMaterial>;
  header: CanvasPanel;
  captions: CanvasPanel;
  transport: CanvasPanel;
  play: Button;
  video: HTMLVideoElement;
  texture: Texture | null;
  current: Moment | null;
  together: boolean;
  driftMs: number | null;
}
let screen: Screen;
const mediaLoader = new TextureLoader();
let maskTexture: CanvasTexture | null = null;

/** Rounded-corner alpha mask for the media (alphaTest, so the media stays opaque and unsorted). */
function roundedMask() {
  if (maskTexture) return maskTexture;
  const c = document.createElement('canvas');
  c.width = 320;
  c.height = 180;
  const ctx = c.getContext('2d')!;
  ctx.fillStyle = '#000';
  ctx.fillRect(0, 0, 320, 180);
  ctx.fillStyle = '#fff';
  roundRect(ctx, 0, 0, 320, 180, 12);
  ctx.fill();
  maskTexture = new CanvasTexture(c);
  return maskTexture;
}

function buildScreen() {
  const group = new Group();
  group.name = 'shared-screen';
  const entity = world.createTransformEntity(group, { parent: tableEntity });
  const bg = new CanvasPanel(SCREEN_W, SCREEN_H, { pxPerM: 700, renderOrder: 1 });
  bg.draw((ctx, W, H) => glass(ctx, 2, 2, W - 4, H - 4, H * 0.075, { tint: 'rgba(30, 31, 30, 0.34)', highlight: 0.07, border: 'rgba(255,255,255,0.26)' }));
  bg.mesh.position.z = -0.004;
  const media = new Mesh(new PlaneGeometry(MEDIA_W, MEDIA_H), new MeshBasicMaterial({ color: 0xffffff, toneMapped: false, alphaMap: roundedMask(), alphaTest: 0.5 }));
  const header = new CanvasPanel(MEDIA_W, 0.068, { renderOrder: 2 });
  header.mesh.position.set(0, 0.258, 0.001);
  const captions = new CanvasPanel(MEDIA_W - 0.05, 0.078, { renderOrder: 3 });
  captions.mesh.position.set(0, -0.162, 0.004);
  const transport = new CanvasPanel(0.5, 0.05, { renderOrder: 2 });
  transport.mesh.position.set(0.14, -0.26, 0.002);
  group.add(bg.mesh, media, header.mesh, captions.mesh, transport.mesh);
  const video = document.createElement('video');
  video.playsInline = true;
  video.crossOrigin = 'anonymous';
  video.preload = 'auto';
  screen = { group, entity, bg, media, header, captions, transport, play: null as unknown as Button, video, texture: null, current: null, together: false, driftMs: null };
  screen.play = new Button(world, entity, { label: 'Play our day', icon: 'play', width: 0.25, height: 0.056, primary: true }, () => togglePlay());
  screen.play.panel.mesh.position.set(-0.255, -0.26, 0.004);
  setScreenIdle();
  video.addEventListener('ended', () => { if (story) nextStep(); else drawProgress(); });
}

let headerKey = '';
function drawHeader(title: string, right: string, rightIcon: string | null = null, rightColor: string = COLORS.onGlassMuted) {
  const key = `${title}|${right}|${rightIcon}|${rightColor}`;
  if (key === headerKey) return;
  headerKey = key;
  screen.header.draw((ctx, W, H) => {
    ctx.textBaseline = 'middle';
    ctx.textAlign = 'left';
    drawIcon(ctx, 'screen', H * 0.32, H * 0.5, H * 0.62, '#ffffff');
    ctx.font = `600 ${Math.round(H * 0.34)}px ${FONT}`;
    const rightW = right ? ctx.measureText(right).width + (rightIcon ? H * 0.62 : 0) : 0;
    const maxTitle = W - H * 0.85 - rightW - H * 0.5;
    let size = Math.round(H * 0.46);
    ctx.font = `700 ${size}px ${FONT}`;
    while (ctx.measureText(title).width > maxTitle && size > H * 0.3) ctx.font = `700 ${--size}px ${FONT}`;
    ctx.fillStyle = '#ffffff';
    ctx.fillText(title, H * 0.82, H * 0.52);
    if (right) {
      ctx.font = `600 ${Math.round(H * 0.34)}px ${FONT}`;
      ctx.fillStyle = rightColor;
      ctx.textAlign = 'right';
      ctx.fillText(right, W - 2, H * 0.53);
      if (rightIcon) drawIcon(ctx, rightIcon, W - 2 - ctx.measureText(right).width - H * 0.36, H * 0.5, H * 0.46, rightColor);
    }
  });
}

let captionKey = '\u0000';
/** Memoized: the canvas is only redrawn (and re-uploaded) when the visible caption actually changes. */
function drawCaption(text: string) {
  const visible = captionsOn && !!text;
  const key = visible ? text : '';
  if (key === captionKey) return;
  captionKey = key;
  screen.captions.mesh.visible = visible;
  if (!visible) return;
  screen.captions.draw((ctx, W, H) => {
    let size = Math.round(H * 0.34);
    ctx.font = `600 ${size}px ${FONT}`;
    let lines = wrapText(ctx, text, W * 0.92);
    while (lines.length > 2 && size > H * 0.24) {
      ctx.font = `600 ${--size}px ${FONT}`;
      lines = wrapText(ctx, text, W * 0.92);
    }
    lines = lines.slice(0, 2);
    let w = 0;
    for (const l of lines) w = Math.max(w, ctx.measureText(l).width);
    w += H * 0.6;
    ctx.fillStyle = 'rgba(12,12,11,0.78)';
    roundRect(ctx, (W - w) / 2, 0, w, H, H * 0.24);
    ctx.fill();
    ctx.fillStyle = '#ffffff';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    lines.forEach((l, i) => ctx.fillText(l, W / 2, H * (lines.length === 1 ? 0.52 : 0.3 + i * 0.42)));
  });
}

let transportKey = '';
function drawTransport(frac: number, text: string) {
  const key = `${Math.round(frac * 500)}|${text}|${debug && screen.driftMs != null ? Math.round(screen.driftMs) : ''}`;
  if (key === transportKey) return;
  transportKey = key;
  screen.transport.draw((ctx, W, H) => {
    ctx.font = `600 ${Math.round(H * 0.4)}px ${FONT}`;
    const tw = text ? ctx.measureText(text).width + H * 0.5 : 0;
    const x0 = H * 0.3;
    const barW = W - x0 - tw - H * 0.2;
    ctx.fillStyle = 'rgba(255,255,255,0.45)';
    roundRect(ctx, x0, H * 0.44, barW, H * 0.12, H * 0.06);
    ctx.fill();
    const fx = Math.max(H * 0.1, barW * Math.max(0, Math.min(1, frac)));
    ctx.fillStyle = COLORS.greenBright;
    roundRect(ctx, x0, H * 0.44, fx, H * 0.12, H * 0.06);
    ctx.fill();
    ctx.fillStyle = '#ffffff';
    ctx.beginPath();
    ctx.arc(x0 + fx, H * 0.5, H * 0.16, 0, Math.PI * 2);
    ctx.fill();
    if (text) {
      ctx.textAlign = 'right';
      ctx.textBaseline = 'middle';
      ctx.fillText(text, W - H * 0.1, H * 0.53);
    }
    if (debug && screen.driftMs != null) {
      ctx.font = `${Math.round(H * 0.3)}px ${FONT}`;
      ctx.textAlign = 'left';
      ctx.fillText(`drift ${screen.driftMs.toFixed(0)} ms`, x0, H * 0.2);
    }
  });
}

function recapSeconds() {
  return recap?.plan?.chapters.reduce((a, c) => a + (c.durationSeconds || 0), 0) ?? 0;
}

function drawProgress() {
  const v = screen.video;
  if (screen.current?.kind === 'clip' && v.duration) drawTransport(v.currentTime / v.duration, `${fmt(v.currentTime)} / ${fmt(v.duration)}`);
  else if (story) drawTransport(story.index / Math.max(1, story.steps.length - 1), `${Math.max(1, story.index + 1)} of ${story.steps.length}`);
  else if (voice) drawTransport(Math.min(1, (performance.now() - voice.started) / voice.durationMs), fmt(voice.durationMs / 1000));
  else drawTransport(0, recap?.plan ? fmt(recapSeconds()) : '');
}

function setMediaTexture(tex: Texture | null, aspect = 16 / 9) {
  if (screen.texture && screen.texture !== tex) screen.texture.dispose();
  screen.texture = tex;
  screen.media.material.map = tex;
  screen.media.material.color.set(tex ? 0xffffff : 0x2a3431);
  screen.media.material.needsUpdate = true;
  screen.media.scale.set(Math.min(1, (MEDIA_H * aspect) / MEDIA_W), Math.min(1, MEDIA_W / aspect / MEDIA_H), 1);
}

function loadMedia(url: string) {
  const t = mediaLoader.load(url);
  t.colorSpace = SRGBColorSpace;
  t.anisotropy = 4;
  return t;
}

function generatorShort() {
  return recap ? recap.generatorLabel.replace(' (no AI call)', '').replace(' recap', '') : room.name;
}

function setScreenIdle() {
  stopVideo();
  screen.current = null;
  screen.together = false;
  const title = recap?.plan?.title ?? (moments.length ? 'Shared moments' : 'Nothing shared yet');
  drawHeader(title, generatorShort());
  const hero = moments.find((m) => m.kind === 'photo');
  setMediaTexture(hero?.media?.url ? loadMedia(hero.media.url) : null, 16 / 10);
  drawCaption(recap ? `${verb()} Play our day, or pick up something on the table.` : `${verb()} something on the table to open it.`);
  screen.play.set({ label: recap ? 'Play our day' : 'Play', icon: 'play' });
  drawProgress();
}

function stopVideo() {
  screen.video.pause();
  screen.video.removeAttribute('src');
  screen.video.load();
}

function showMoment(m: Moment, opts: { together?: boolean; segment?: string } = {}) {
  screen.current = m;
  screen.together = !!opts.together || !!live;
  const owner = room.members.find((x) => x.id === m.ownerId);
  // sample media names how it was made (a simulated pan, a synthesized voice, a stock photo), never passing as a recording
  const origin = m.media?.origin;
  const kind = m.kind === 'clip' ? (origin === 'simulated' ? 'simulated camera pan' : 'clip')
    : m.kind === 'voice' ? (origin === 'synthesized' ? 'synthesized voice note' : 'voice note')
    : m.kind === 'text' ? 'note' : origin === 'stock' ? 'sample photo' : 'photo';
  if (screen.together) drawHeader(`${owner?.displayName ?? m.ownerName}'s ${kind}`, live ? 'Watching together' : 'Watching together · simulated', 'users', COLORS.greenBright);
  else drawHeader(`${owner?.displayName ?? m.ownerName}'s ${kind}`, `${m.captureLocal.time} in ${cityOf(m.captureLocal.zone)}`);
  if (m.kind === 'clip' && m.media?.url) {
    const v = screen.video;
    v.src = m.media.url;
    v.muted = muted;
    const tex = new VideoTexture(v);
    tex.colorSpace = SRGBColorSpace;
    setMediaTexture(tex, (m.media.width ?? 16) / (m.media.height ?? 9));
    if (capture) {
      // a fixed, representative frame for documentation captures
      v.addEventListener('loadeddata', () => { v.currentTime = 3.2; }, { once: true });
      v.addEventListener('seeked', () => { v.pause(); drawProgress(); }, { once: true });
    } else if (!live) void v.play().catch(() => drawCaption(`${verb()} Play to start the clip.`));
    screen.play.set({ label: 'Pause', icon: 'pause' });
    if (opts.together && mode === 'sample' && partner) partner.setPresence('simulated');
  } else {
    stopVideo();
    if (m.media?.url && m.kind === 'photo') setMediaTexture(loadMedia(m.media.url), (m.media.width ?? 16) / (m.media.height ?? 10));
    else if (m.kind === 'voice' || m.kind === 'text') void portraitCard(m).then((tex) => { if (screen.current?.id === m.id) setMediaTexture(tex, 16 / 9); else tex.dispose(); });
    else setMediaTexture(null);
    screen.play.set({ label: story ? 'Pause' : 'Back', icon: story ? 'pause' : 'x' });
  }
  drawCaption(opts.segment ?? (m.kind === 'voice' ? '' : m.caption));
  drawProgress();
}

/** Voice notes and text moments show the person's own avatar portrait with their words, never a fake video. */
async function portraitCard(m: Moment): Promise<Texture> {
  const owner = room.members.find((x) => x.id === m.ownerId)?.avatar;
  const img = new Image();
  img.src = await renderAvatarPortrait(owner, SAMPLE_PEOPLE['sample-maya'].avatar, 384);
  await img.decode().catch(() => undefined);
  const c = document.createElement('canvas');
  c.width = 1280;
  c.height = 720;
  const ctx = c.getContext('2d')!;
  const g = ctx.createLinearGradient(0, 0, 1280, 720);
  g.addColorStop(0, '#4a3b30');
  g.addColorStop(1, '#221a15');
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, 1280, 720);
  ctx.save();
  ctx.beginPath();
  ctx.arc(260, 340, 170, 0, Math.PI * 2);
  ctx.clip();
  ctx.drawImage(img, 90, 170, 340, 340);
  ctx.restore();
  ctx.fillStyle = '#f7f2ea';
  ctx.font = `700 64px ${FONT}`;
  ctx.fillText(m.kind === 'voice' ? `“${m.caption}”` : m.ownerName, 500, 260, 700);
  ctx.font = `500 34px ${FONT}`;
  ctx.fillStyle = 'rgba(255,255,255,0.78)';
  if (m.kind === 'voice') {
    ctx.fillText(`${m.ownerName} · Recorded message · ${m.captureLocal.time} in ${cityOf(m.captureLocal.zone)}`, 500, 320, 720);
    ctx.fillStyle = COLORS.greenBright;
    for (let i = 0; i < 40; i++) {
      const h = 20 + Math.abs(Math.sin(i * 0.9 + 3) * 90 + Math.sin(i * 2.3) * 30);
      ctx.fillRect(500 + i * 17, 440 - h / 2, 9, h);
    }
  } else {
    wrapText(ctx, m.caption, 700).slice(0, 5).forEach((line, i) => ctx.fillText(line, 500, 330 + i * 48));
  }
  const tex = new CanvasTexture(c);
  tex.colorSpace = SRGBColorSpace;
  return tex;
}

function togglePlay() {
  if (story) {
    story.paused = !story.paused;
    if (screen.current?.kind === 'clip') {
      if (story.paused) screen.video.pause();
      else void screen.video.play();
    }
    if (voice && story.paused) stopVoice();
    screen.play.set({ label: story.paused ? 'Resume' : 'Pause', icon: story.paused ? 'play' : 'pause' });
    return;
  }
  if (screen.current?.kind === 'clip') {
    const v = screen.video;
    if (live) {
      live.command(v.paused ? 'play' : 'pause', v.currentTime * 1000);
      return;
    }
    if (v.paused) { void v.play(); screen.play.set({ label: 'Pause', icon: 'pause' }); }
    else { v.pause(); screen.play.set({ label: 'Play', icon: 'play' }); }
    return;
  }
  if (screen.current) { setScreenIdle(); return; }
  if (recap?.plan) startStory();
}

// ---------------------------------------------------------------------------
// Recap story on the screen
// ---------------------------------------------------------------------------

interface Step { moment: Moment | null; chapter: number; text: string; seconds: number }
let story: { steps: Step[]; index: number; elapsed: number; paused: boolean } | null = null;

function startStory() {
  if (!recap?.plan) return;
  const byId = new Map(recap.playback.map((m) => [m.id, m]));
  const steps: Step[] = [];
  recap.plan.chapters.forEach((ch, chapter) => {
    const media = ch.playbackMomentIds.map((id) => byId.get(id)).filter((m): m is Moment => !!m);
    const texts = ch.segments.map((s) => s.text);
    if (!media.length) steps.push({ moment: null, chapter, text: texts.join(' '), seconds: 8 });
    media.forEach((m, i) => steps.push({ moment: m, chapter, text: texts[i] ?? texts[texts.length - 1] ?? '', seconds: m.kind === 'photo' ? 7 : 0 }));
  });
  steps.push({ moment: null, chapter: -1, text: recap.plan.closingLine || recap.plan.title, seconds: 6 });
  story = { steps, index: -1, elapsed: 0, paused: false };
  drawDock();
  nextStep();
}

function nextStep() {
  if (!story) return;
  stopVoice();
  story.index += 1;
  story.elapsed = 0;
  const step = story.steps[story.index];
  if (!step) {
    story = null;
    drawDock();
    setScreenIdle();
    return;
  }
  if (step.moment) {
    showMoment(step.moment, { segment: step.text });
    if (step.moment.kind === 'voice') playVoice(step.moment, () => nextStep());
  } else {
    stopVideo();
    setMediaTexture(null);
    const name = step.chapter >= 0 ? room.members.find((m) => m.id === recap!.plan!.chapters[step.chapter].contributorId)?.displayName : null;
    drawHeader(name ? `${name}'s chapter` : recap!.plan!.title, generatorShort());
    drawCaption(step.text);
  }
  screen.play.set({ label: 'Pause', icon: 'pause' });
}

// ---------------------------------------------------------------------------
// Voice notes: spatial audio from the object that holds them (IWSDK AudioSource)
// ---------------------------------------------------------------------------

let voice: { moment: Moment; entity: Entity; started: number; cues: Cue[]; durationMs: number; onEnd?: () => void; oneShot: boolean } | null = null;

function playVoice(m: Moment, onEnd?: () => void) {
  stopVoice();
  const holder = [...memory.values()].find((o) => o.record.momentId === m.id);
  let entity: Entity;
  let oneShot = false;
  if (holder) {
    entity = holder.entity;
    if (!entity.hasComponent(AudioSource)) entity.addComponent(AudioSource, { src: m.media!.url!, positional: true, refDistance: 0.6, volume: 1 });
  } else {
    const p = new Vector3();
    screen.group.getWorldPosition(p);
    entity = AudioUtils.createOneShot(world, m.media!.url!, { positional: true, position: { x: p.x, y: p.y, z: p.z } });
    oneShot = true;
  }
  if (!muted) AudioUtils.play(entity, 0.15);
  voice = { moment: m, entity, started: performance.now(), cues: [], durationMs: m.media?.durationMs ?? 10_000, onEnd, oneShot };
  if (m.media?.captionsUrl) void loadCaptions(m.media.captionsUrl).then((c) => { if (voice?.moment.id === m.id) voice.cues = c; });
  if (partner && m.ownerId !== me.id) {
    partner.setPresence('recorded', `${m.captureLocal.time} in ${cityOf(m.captureLocal.zone)}`);
    partner.playGesture(m.recordedGesture);
  }
  drawCupLabel();
}

function stopVoice() {
  if (!voice) return;
  try { AudioUtils.stop(voice.entity); } catch { /* not loaded yet */ }
  voice = null;
  if (partner) partner.setPresence(partnerKind);
  drawCupLabel();
}

// ---------------------------------------------------------------------------
// Objects on the table (records keep their serialized table-relative transforms)
// ---------------------------------------------------------------------------

async function placeObjects(objects: RoomObject[]) {
  const replyIds = new Set(objects.filter((o) => o.replyId).map((o) => o.replyId!));
  const replies = new Map<string, Reply>();
  if (replyIds.size) {
    const lists = await Promise.all(moments.map((m) => ds.replies(m.id).catch(() => [] as Reply[])));
    lists.flat().forEach((r) => replies.set(r.id, r));
  }
  const byId = new Map(moments.map((m) => [m.id, m]));
  for (const record of objects) {
    const moment = record.momentId ? byId.get(record.momentId) : undefined;
    const reply = record.replyId ? replies.get(record.replyId) : undefined;
    if (record.momentId && !moment) continue; // not visible to this viewer: render nothing, not even its existence
    addObject(record, moment, reply);
  }
  // Moments without an object yet get a default spot on the table (shared layout, persisted on first move).
  const spots = [[-0.3, -0.2], [0.3, -0.2], [-0.1, 0.12], [0.18, 0.12], [-0.38, 0.05], [0.4, 0.04]];
  let i = 0;
  for (const m of moments) {
    if ([...memory.values()].some((o) => o.record.momentId === m.id) || m.kind === 'text') continue;
    const [x, z] = spots[i++ % spots.length];
    const type = m.kind === 'voice' ? 'cup' : m.kind === 'clip' ? 'postcard' : 'frame';
    addObject({ id: `pending-${m.id}`, objectType: type, momentId: m.id, replyId: null, ownerId: m.ownerId, transform: { x, y: 0, z, yaw: x < 0 ? 20 : -20 }, version: 0 }, m);
  }
}

function addObject(record: RoomObject, moment?: Moment, reply?: Reply) {
  const obj = buildMemoryObject(record, moment, reply);
  const entity = world.createTransformEntity(obj.group, { parent: tableEntity });
  const name = `${record.objectType}:${record.momentId ?? record.replyId ?? record.id}`;
  // Near: reach out and pinch-grab it. Grabbing activates (RoomSystem.init); letting go persists the spot.
  entity.addComponent(OneHandGrabbable, { rotate: false, translate: true });
  // Desktop: click (screen pointers) on the object itself; it can also be dragged there.
  obj.group.userData.pressable = registerPressable(entity, obj.group, () => activate(obj), undefined, `object:${name}`);
  // Far: point and pinch from the seat. IWSDK binds one grab handle per entity, and the one-hand handle
  // denies ray events, so in a headset no ray could reach the object (found in Meta's emulator). Far
  // selection lives on an invisible sibling copy of the object's hit proxy that only XR rays may target;
  // screen pointers pass through it to the object, and near grabs never see it.
  const source = obj.group.getObjectByName('hit-proxy') as Mesh | undefined;
  const far = new Mesh(source?.geometry ?? new BoxGeometry(0.14, 0.1, 0.14), new MeshBasicMaterial({ visible: false }));
  far.name = 'far-select';
  (far as unknown as { pointerEventsType: unknown }).pointerEventsType = { allow: 'ray' };
  far.userData.offset = source ? source.position.clone() : new Vector3(0, 0.05, 0);
  const farEntity = world.createTransformEntity(far, { parent: tableEntity });
  const entry = { ...obj, entity, far };
  registerPressable(farEntity, far, () => activate(entry), (h) => {
    obj.group.scale.setScalar(h ? 1.06 : 1);
    if (h) showHoverLabel(obj.group);
    else hoverLabel.mesh.visible = false;
  }, `far:${name}`, { poke: false });
  syncFarProxy(entry);
  memory.set(record.id, entry);
}

/** Keep an object's far-select proxy on the object, in world terms: a grab can re-parent the object. */
const _farWorld = new Matrix4();
const _farOffset = new Matrix4();
const _farParentInv = new Matrix4();
function syncFarProxy(o: { group: Group; far: Mesh }) {
  const parent = o.far.parent;
  if (!parent) return;
  const offset = o.far.userData.offset as Vector3;
  o.group.updateWorldMatrix(true, false);
  parent.updateWorldMatrix(true, false);
  _farWorld.multiplyMatrices(o.group.matrixWorld, _farOffset.makeTranslation(offset.x, offset.y, offset.z));
  _farWorld.premultiply(_farParentInv.copy(parent.matrixWorld).invert());
  _farWorld.decompose(o.far.position, o.far.quaternion, o.far.scale);
}

let lastActivation = 0;
function activate(obj: MemoryObject) {
  const now = performance.now();
  if (now - lastActivation < 600) return;
  lastActivation = now;
  if (obj.reply) {
    showInfo(`${obj.reply.authorName} · Recorded message`, [obj.reply.text], new Date(obj.reply.createdAt).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' }));
    return;
  }
  const m = obj.moment;
  if (!m) return;
  story = null;
  drawDock();
  if (m.kind === 'voice') {
    if (voice?.moment.id === m.id) { stopVoice(); return; }
    showMoment(m, { segment: '' });
    playVoice(m);
    if (obj.glow) obj.glow.visible = false;
    if (onboarding === 'cup') setOnboarding('reply');
  } else {
    showMoment(m, { together: !!live });
  }
}

function persistMove(entry: MemoryObject & { entity: Entity; far: Mesh }) {
  const group = entry.group;
  const table = tableEntity.object3D!;
  if (group.parent !== table) table.attach(group);
  const clamp = (v: number, a: number, b: number) => Math.max(a, Math.min(b, v));
  group.position.set(clamp(group.position.x, -0.5, 0.5), 0, clamp(group.position.z, -0.33, 0.33));
  const yaw = new Euler().setFromQuaternion(group.quaternion, 'YXZ').y;
  group.quaternion.setFromEuler(new Euler(0, yaw, 0));
  const t = { x: group.position.x, y: 0, z: group.position.z, yaw: ((yaw * 180) / Math.PI + 360) % 360 };
  const rec = entry.record;
  const save = rec.version === 0
    ? ds.placeObject(room.id, { object_type: rec.objectType, moment_id: rec.momentId ?? undefined, reply_id: rec.replyId ?? undefined, ...t })
    : ds.moveObject(rec.id, rec.version, t);
  void save.then((saved) => {
    memory.delete(rec.id);
    entry.record = saved;
    memory.set(saved.id, entry);
  }).catch(async () => {
    // Conflict policy: someone else moved it; load the latest position.
    const fresh = (await ds.objects(room.id)).find((o) => o.id === rec.id);
    if (fresh) {
      entry.record = fresh;
      group.position.set(fresh.transform.x, fresh.transform.y, fresh.transform.z);
      group.rotation.set(0, (fresh.transform.yaw * Math.PI) / 180, 0);
    }
    flash('Someone moved this at the same time — showing the latest spot.');
  });
}

function resetObjects() {
  if (ds instanceof SampleSource) ds.resetObjects();
  void ds.objects(room.id).then((objs) => {
    for (const o of objs) {
      const entry = memory.get(o.id);
      if (!entry) continue;
      entry.record = o;
      entry.group.position.set(o.transform.x, o.transform.y, o.transform.z);
      entry.group.rotation.set(0, (o.transform.yaw * Math.PI) / 180, 0);
    }
    flash('Objects are back in their places.');
  });
}

// ---------------------------------------------------------------------------
// Keepsakes: the growing memory tree on the table, the other two by the lamp (same models as the companion)
// ---------------------------------------------------------------------------

function buildKeepsakes() {
  const side = world.createTransformEntity(new Group(), { parent: roomEntity });
  side.object3D!.position.set(env.sideTable.position.x, env.sideTop, env.sideTable.position.z);
  const spots = [new Vector3(0.11, 0, 0.12), new Vector3(-0.1, 0, 0.17)];
  let s = 0;
  for (const a of awards) {
    const k = buildKeepsake(a.keepsake, a.progress, a.target, a.earned);
    const shadow = contactShadow(0.22, 0.22, 0.42);
    shadow.position.y = 0.002;
    k.add(shadow);
    let parent: Entity;
    if (a.keepsake === 'memory_tree') {
      k.position.set(0.04, 0, -0.3);
      k.rotation.y = 0.3;
      parent = tableEntity;
    } else {
      k.position.copy(spots[s++ % spots.length]);
      k.rotation.y = -0.9;
      parent = side;
    }
    makePressable(world, k, parent, () => showInfo(a.title, [a.trigger, a.earned ? 'Earned together.' : `${a.progress} of ${a.target} so far — missed days never count against you.`], a.earned ? '✓ Earned' : undefined), undefined, `keepsake:${a.keepsake}`);
  }
}

// ---------------------------------------------------------------------------
// The dock: one glass bar (one canvas, one draw call) with invisible per-button hit areas
// ---------------------------------------------------------------------------

interface DockItem { id: string; label: string; icon: string; round: boolean; tip: string; action: () => void; x: number; w: number }
const DOCK_H = 0.078;
let dock: { group: Group; entity: Entity; panel: CanvasPanel; items: DockItem[]; hovered: number; width: number; divider: number; tooltip: FittedPill };

function buildDock() {
  const defs: Omit<DockItem, 'x' | 'w'>[] = [
    { id: 'day', label: 'Our day', icon: 'calendar', round: false, tip: 'Play our day', action: () => { stopVoice(); story = null; startStory(); } },
    { id: 'reply', label: 'Reply', icon: 'reply', round: false, tip: 'Leave a reply for later', action: () => openReplySheet() },
    { id: 'love', label: 'Love', icon: 'heart', round: false, tip: 'Send a heart', action: () => react('heart') },
    { id: 'cc', label: 'Captions', icon: 'cc', round: true, tip: 'Captions', action: () => toggleCaptions() },
    { id: 'sound', label: 'Sound', icon: 'sound', round: true, tip: 'Sound', action: () => toggleMute() },
    { id: 'recenter', label: 'Recenter', icon: 'recenter', round: true, tip: 'Recenter the room', action: () => recenter() },
    { id: 'reset', label: 'Reset', icon: 'reset', round: true, tip: 'Put objects back', action: () => resetObjects() },
    { id: 'exit', label: 'Leave', icon: 'exit', round: true, tip: 'Leave the room', action: () => exitRoom() },
  ];
  const pad = 0.012;
  const labelled = 0.098;
  const round = 0.05;
  let x = pad;
  let divider = 0;
  const items: DockItem[] = defs.map((d, i) => {
    if (d.round && !defs[i - 1].round) {
      divider = x + 0.006;
      x += 0.018;
    }
    const w = d.round ? round : labelled;
    const item = { ...d, x, w };
    x += w + (d.round ? 0.009 : 0.004);
    return item;
  });
  const width = x - (items[items.length - 1].round ? 0.009 : 0.004) + pad;
  const group = new Group();
  group.name = 'dock';
  const entity = world.createTransformEntity(group, { parent: tableEntity });
  const panel = new CanvasPanel(width, DOCK_H, { renderOrder: 4 });
  group.add(panel.mesh);
  const tooltip = new FittedPill(0.4, 0.046, 9);
  tooltip.mesh.visible = false;
  group.add(tooltip.mesh);
  dock = { group, entity, panel, items, hovered: -1, width, divider, tooltip };
  items.forEach((it, i) => {
    const hit = hitBox(it.w, DOCK_H - 0.01);
    const p = makePressable(world, hit, entity, it.action, (h) => {
      if (h) dock.hovered = i;
      else if (dock.hovered === i) dock.hovered = -1;
      drawDock();
    }, `dock:${it.id}`);
    p.object.position.set(it.x + it.w / 2 - width / 2, 0, 0.004);
  });
  drawDock();
}

let dockKey = '';
function drawDock() {
  if (!dock) return;
  const active = (id: string) => (id === 'cc' && captionsOn) || (id === 'sound' && muted) || (id === 'day' && !!story);
  const key = `${dock.hovered}|${captionsOn}|${muted}|${!!story}`;
  if (key !== dockKey) {
    dockKey = key;
    dock.panel.draw((ctx, W, H) => {
      const s = W / dock.width;
      glass(ctx, 2, 2, W - 4, H - 4, H * 0.42, { tint: 'rgba(26, 27, 26, 0.5)', highlight: 0.05, border: 'rgba(255,255,255,0.22)' });
      dock.items.forEach((it, i) => {
        const x = it.x * s;
        const w = it.w * s;
        const on = active(it.id);
        const hover = i === dock.hovered;
        if (!it.round) {
          if (on || hover) {
            roundRect(ctx, x, H * 0.12, w, H * 0.76, H * 0.26);
            ctx.fillStyle = on ? 'rgba(61,107,79,0.94)' : 'rgba(255,255,255,0.16)';
            ctx.fill();
            if (hover) {
              ctx.lineWidth = 4;
              ctx.strokeStyle = '#a8e2bb';
              ctx.stroke();
            }
          }
          drawIcon(ctx, it.icon, x + w / 2, H * 0.37, H * 0.33, '#ffffff', it.icon === 'heart' && on);
          ctx.font = `600 ${Math.round(H * 0.2)}px ${FONT}`;
          ctx.textAlign = 'center';
          ctx.textBaseline = 'middle';
          ctx.fillStyle = '#ffffff';
          ctx.fillText(it.label, x + w / 2, H * 0.72);
        } else {
          const cx = x + w / 2;
          const r = Math.min(w, H * 0.78) / 2;
          ctx.beginPath();
          ctx.arc(cx, H / 2, r, 0, Math.PI * 2);
          ctx.fillStyle = on ? 'rgba(61,107,79,0.95)' : hover ? 'rgba(255,255,255,0.24)' : 'rgba(255,255,255,0.1)';
          ctx.fill();
          ctx.lineWidth = hover ? 4 : 2.5;
          ctx.strokeStyle = hover ? '#a8e2bb' : 'rgba(255,255,255,0.42)';
          ctx.stroke();
          drawIcon(ctx, it.id === 'sound' && muted ? 'mute' : it.icon, cx, H / 2, r * 1.0, '#ffffff');
        }
      });
      ctx.fillStyle = 'rgba(255,255,255,0.32)';
      ctx.fillRect(dock.divider * s, H * 0.24, 2, H * 0.52);
    });
  }
  // round buttons have no visible label: show it above the hovered one
  const it = dock.items[dock.hovered];
  if (it?.round) {
    const state = it.id === 'cc' ? (captionsOn ? ' · on' : ' · off') : it.id === 'sound' ? (muted ? ' · muted' : ' · on') : '';
    dock.tooltip.set(`${it.tip}${state}`);
    dock.tooltip.mesh.position.set(it.x + it.w / 2 - dock.width / 2, DOCK_H / 2 + 0.032, 0.002);
    dock.tooltip.mesh.visible = true;
  } else dock.tooltip.mesh.visible = false;
}

function toggleCaptions() {
  captionsOn = !captionsOn;
  const text = captionKey;
  captionKey = '\u0000';
  drawCaption(captionsOn ? (text || 'Captions on.') : '');
  drawDock();
}

function toggleMute() {
  muted = !muted;
  screen.video.muted = muted;
  const listener = world.camera.children.find((c) => c instanceof AudioListener) as AudioListener | undefined;
  listener?.setMasterVolume(muted ? 0 : 1);
  drawDock();
  flash(muted ? 'Sound off.' : 'Sound on.');
}

// ---------------------------------------------------------------------------
// Labels: the cup's waiting message, onboarding hint, flash messages, hover label (all fitted to text)
// ---------------------------------------------------------------------------

let cupLabel: { card: FittedCard; entry: MemoryObject & { entity: Entity; far: Mesh }; pressable: Pressable } | null = null;
let hint: FittedPill;
let flashPill: FittedPill;
const hoverLabel = new FittedPill(0.62, 0.05, 9);

function buildLabels() {
  const table = tableEntity.object3D!;
  hoverLabel.mesh.visible = false;
  table.add(hoverLabel.mesh);
  const cup = [...memory.values()].find((o) => o.record.objectType === 'cup' && o.moment?.kind === 'voice');
  if (cup) {
    const card = new FittedCard(0.4, 0.072, 7);
    const pressable = makePressable(world, card.mesh, tableEntity, () => activate(cup), () => drawCupLabel(), 'cup-card');
    cupLabel = { card, entry: cup, pressable };
    drawCupLabel();
  }
  hint = new FittedPill(0.62, 0.05, 9);
  hint.mesh.visible = false;
  table.add(hint.mesh);
  flashPill = new FittedPill(0.62, 0.05, 10);
  flashPill.mesh.visible = false;
  dock.group.add(flashPill.mesh);
  setOnboarding(onboarding);
}

function drawCupLabel() {
  if (!cupLabel) return;
  const m = cupLabel.entry.moment!;
  const playing = voice?.moment.id === m.id;
  const seconds = (m.media?.durationMs ?? 0) / 1000;
  // narrow screens carry the onboarding instruction inside the card instead of a separate hint
  const sub = hintNarrow && onboarding === 'cup' ? `${verb()} to listen · ${m.ownerName} · ${fmt(seconds)}` : `${m.ownerName} · ${m.media?.origin === 'synthesized' ? 'Synthesized voice' : 'Voice note'} · ${fmt(seconds)}`;
  cupLabel.card.set(m.caption.replace(/\.$/, ''), sub, { icon: 'wave', trailing: playing ? 'pause' : 'play', hovered: cupLabel.pressable.hovered });
}

function setOnboarding(stage: string) {
  onboarding = stage;
  try { localStorage.setItem(onboardingKey, stage); } catch { /* ignore */ }
  if (!hint) return;
  drawCupLabel();
  if (stage === 'cup' && cupLabel) {
    hint.set(`Something is waiting — ${verb().toLowerCase()} the glowing cup`, { tint: 'rgba(47, 89, 64, 0.9)', icon: 'sprout', iconColor: '#d9f0df' });
    hint.mesh.visible = !hintNarrow;
    if (hint.mesh.parent !== tableEntity.object3D) tableEntity.object3D!.add(hint.mesh);
  } else if (stage === 'reply') {
    hint.set(`Leave something for later — ${verb().toLowerCase()} Reply`, { tint: 'rgba(47, 89, 64, 0.9)', icon: 'reply', iconColor: '#d9f0df' });
    const reply = dock.items.find((i) => i.id === 'reply')!;
    dock.group.add(hint.mesh);
    hint.mesh.rotation.set(0, 0, 0);
    hint.mesh.position.set(reply.x + reply.w / 2 - dock.width / 2 + hint.width / 2 - 0.05, DOCK_H / 2 + 0.034, 0.002);
    hint.mesh.visible = true;
  } else {
    hint.mesh.visible = false;
  }
  if (flashPill?.mesh.visible) placeFlash(); // a flash already showing moves out of the hint's way
}

let flashTimer = 0;
/** Above the dock; one row higher while the onboarding hint sits on the dock, so the two never overlap. */
function placeFlash() {
  flashPill.mesh.position.set(0, DOCK_H / 2 + 0.034 + (hint?.mesh.visible && hint.mesh.parent === dock.group ? 0.06 : 0), 0.003);
}
function flash(text: string) {
  status(text);
  window.clearTimeout(flashTimer);
  flashPill.set(text);
  placeFlash();
  flashPill.mesh.visible = true;
  flashTimer = window.setTimeout(() => { flashPill.mesh.visible = false; status(''); }, 3500);
}

function showHoverLabel(obj: Object3D | null) {
  const entry = [...memory.values()].find((o) => o.group === obj);
  if (!entry) return;
  const m = entry.moment;
  const text = entry.reply ? `${entry.reply.authorName}: “${entry.reply.text}”` : m ? `${m.ownerName} · ${m.kind === 'voice' ? `“${m.caption}”` : m.caption}` : '';
  if (!text) return;
  hoverLabel.set(text);
  hoverLabel.mesh.position.set(entry.group.position.x, entry.labelAnchorY + 0.06, entry.group.position.z);
  hoverLabel.mesh.visible = true;
}

// ---------------------------------------------------------------------------
// Reply sheet, info cards
// ---------------------------------------------------------------------------

let sheet: { group: Group; entity: Entity; buttons: Button[]; panel: CanvasPanel } | null = null;
let info: { panel: CanvasPanel; timer: number } | null = null;
const QUICK_REPLIES = ['On my way — save me that seat.', 'That light! Tell me everything tomorrow.', 'Thinking of you. ❤️'];

function openReplySheet() {
  closeSheet();
  if (hint) hint.mesh.visible = false;
  const target = [...moments].reverse().find((m) => !m.isMine) ?? moments[moments.length - 1];
  if (!target) return flash('Nothing to reply to yet.');
  const group = new Group();
  group.position.set(0, 0.3, 0.1);
  group.rotation.x = -0.35;
  const entity = world.createTransformEntity(group, { parent: tableEntity });
  // Headset: closer and lower, facing the person, so the choices never cover the other person's face
  // (found in Meta's emulator: at eye level the sheet hid Maya and the screen's captions).
  if (world.renderer.xr.isPresenting || xrPreview) setFacing(group, XR_SHEET, SEATED_HEAD, 0.8);
  const panel = new CanvasPanel(0.52, 0.1, { renderOrder: 5 });
  panel.draw((ctx, W, H) => {
    glass(ctx, 2, 2, W - 4, H - 4, H * 0.3, { tint: 'rgba(26, 27, 26, 0.7)' });
    ctx.fillStyle = '#ffffff';
    ctx.font = `700 ${Math.round(H * 0.3)}px ${FONT}`;
    ctx.textBaseline = 'middle';
    ctx.fillText(`Reply to ${target.ownerName}`, H * 0.35, H * 0.36);
    ctx.fillStyle = COLORS.onGlassMuted;
    ctx.font = `500 ${Math.round(H * 0.2)}px ${FONT}`;
    ctx.fillText(`Only people who can see this ${target.kind === 'voice' ? 'voice note' : target.kind} will see your reply.`, H * 0.35, H * 0.7, W - H * 0.7);
  });
  panel.mesh.position.y = 0.17;
  group.add(panel.mesh);
  const buttons = QUICK_REPLIES.map((text, i) => {
    const b = new Button(world, entity, { label: text, width: 0.52, height: 0.058 }, () => void sendReply(target, text));
    b.panel.mesh.position.set(0, 0.09 - i * 0.066, 0);
    return b;
  });
  const close = new Button(world, entity, { label: 'Not now', icon: 'x', width: 0.17, height: 0.05, dark: true }, () => closeSheet());
  close.panel.mesh.position.set(0, -0.125, 0);
  buttons.push(close);
  sheet = { group, entity, buttons, panel };
}

function closeSheet() {
  if (!sheet) return;
  for (const b of sheet.buttons) {
    pressables.delete(b.pressable.entity.index);
    b.pressable.entity.destroy();
    b.panel.dispose();
  }
  sheet.panel.dispose();
  sheet.entity.destroy();
  sheet = null;
  setOnboarding(onboarding);
}

async function sendReply(target: Moment, text: string) {
  closeSheet();
  try {
    const reply = await ds.reply(target.id, text);
    const cup = [...memory.values()].find((o) => o.record.momentId === target.id)?.group.position ?? new Vector3(0, 0, 0);
    const t = { x: Math.max(-0.5, Math.min(0.5, cup.x + 0.13)), y: 0, z: Math.max(-0.33, Math.min(0.33, cup.z + 0.1)), yaw: 0 };
    const record = await ds.placeObject(room.id, { object_type: 'note', reply_id: reply.id, ...t });
    addObject(record, undefined, reply);
    flash(`Your reply is waiting for ${target.ownerName}.`);
    if (onboarding !== 'done') setOnboarding('done');
  } catch (e) {
    flash(e instanceof Error ? e.message : 'Could not send the reply.');
  }
}

function react(kind: string) {
  const target = screen.current ?? [...moments].reverse().find((m) => !m.isMine);
  if (!target) return;
  void ds.react(target.id, kind as 'heart').then(() => flash(`❤️ sent to ${target.ownerName}`));
  live?.react(kind);
}

function showInfo(title: string, lines: string[], footer?: string) {
  if (info) { window.clearTimeout(info.timer); info.panel.mesh.removeFromParent(); info.panel.dispose(); }
  const panel = infoCard(0.42, 0.24, title, lines, footer);
  panel.mesh.position.set(0, 0.32, 0.12);
  panel.mesh.rotation.x = -0.3;
  tableEntity.object3D!.add(panel.mesh);
  info = { panel, timer: window.setTimeout(() => { panel.mesh.removeFromParent(); panel.dispose(); info = null; }, 7000) };
}

// ---------------------------------------------------------------------------
// Partner avatar: simulated (sample), recorded, or live
// ---------------------------------------------------------------------------

async function setupPartner() {
  const other = room.members.find((m) => m.id !== me.id);
  if (!other) return;
  partner = await createPartner(other.avatar, other.displayName);
  partnerAvatarKey = avatarKey(other.avatar);
  partnerKind = mode === 'sample' ? 'simulated' : 'away';
  partner.setPresence(partnerKind);
}

/** Which figure a view resolves to: the exact approved version, or the demo definition. */
function avatarKey(avatar: AvatarView | null | undefined) {
  return avatar?.kind === 'photo_derived' && avatar.assetId ? `photo|${avatar.assetId}|${avatar.assetVersion ?? 0}` : `demo|${JSON.stringify(avatar?.definition ?? null)}`;
}

let partnerAvatarKey = '';
let partnerSwap = Promise.resolve();

/** Swap in a partner's newer approved avatar (or their demo after they stop sharing) without a reload. */
function replacePartner(avatar: AvatarView | null | undefined, name: string) {
  const key = avatarKey(avatar);
  if (!partner || key === partnerAvatarKey) return;
  partnerAvatarKey = key;
  partnerSwap = partnerSwap.then(async () => {
    const next = await createPartner(avatar, name);
    if (avatarKey(avatar) !== partnerAvatarKey) { next.dispose(); return; } // superseded while loading
    partner?.dispose();
    partner = next;
    partner.setPresence(partnerKind);
  });
}

/**
 * The person's owner-approved photo-derived model when they share it with us, otherwise the labelled demo
 * avatar. A model that fails to load falls back to the demo avatar and says so — never silently.
 */
async function createPartner(avatar: AvatarView | null | undefined, name: string) {
  let figure: CharacterModel | SkinnedAvatar | null = null;
  let release: (() => void) | null = null;
  if (avatar?.kind === 'photo_derived' && avatar.modelUrl && avatar.assetId) {
    try {
      const held = await acquireAvatarModel(`${me.id}|${avatar.assetId}|${avatar.assetVersion ?? 0}`, avatar.modelUrl);
      release = held.release;
      figure = new SkinnedAvatar(held.gltf, 2);
    } catch (e) {
      console.warn('[avatar] could not load the approved model', e);
      release?.();
      release = null;
      window.setTimeout(() => flash(`Couldn't load ${name}'s avatar, so a demo avatar is shown.`), 1500);
    }
  }
  figure ??= new CharacterModel(kit, avatar?.definition ?? SAMPLE_PEOPLE['sample-maya'].avatar, 2);
  const p = new Avatar3D(figure, name, env.seat);
  p.releaseModel = release;
  p.demoNote = mode === 'live';
  p.character.blinking = !capture;
  p.setRest(mode === 'sample' ? 'chin' : 'table');
  tableEntity.object3D!.add(p.root);
  p.update(1 / 60, CAPTURE_T, SEATED_HEAD);
  return p;
}

function mirrorPose(p: { p: number[]; q: number[] }) {
  // Their table-relative pose, seen from across the table: rotate 180° about the table's vertical axis.
  const q = new Quaternion().setFromAxisAngle(new Vector3(0, 1, 0), Math.PI).multiply(new Quaternion().fromArray(p.q));
  return { p: [-p.p[0], p.p[1], -p.p[2]], q: q.toArray() as number[] };
}

async function joinLive(sessionId: string) {
  const token = storedToken();
  if (!token) return;
  live = new LiveSession(sessionId, token, {
    onWelcome: (_me, state, participants) => {
      const other = participants.find((p) => p.userId !== me.id);
      if (other?.avatar) replacePartner(other.avatar, other.displayName);
      partnerKind = other?.online ? 'live' : 'away';
      partner?.setPresence(partnerKind);
      const m = moments.find((x) => x.id === state.momentId);
      if (m) showMoment(m, { together: true });
      flash(other?.online ? `${other.displayName} is here.` : `Waiting for ${other?.displayName ?? 'them'} to join…`);
    },
    onState: () => syncVideo(true),
    onPose: (_uid, pose) => {
      partnerLastSeen = performance.now();
      if (partnerKind !== 'live') { partnerKind = 'live'; partner?.setPresence('live'); }
      partner?.setPose(mirrorPose(pose.head), pose.left ? mirrorPose(pose.left) : null, pose.right ? mirrorPose(pose.right) : null, pose.tracked);
    },
    onPeerJoined: (uid, name, avatar) => {
      partnerKind = 'live';
      partner?.setPresence('live');
      flash(`${name ?? 'They'} joined.`);
      // The broadcast never says whether a likeness exists; ask with our own authorization.
      if (avatar?.refresh) void ds.userAvatar(uid).then((r) => replacePartner(r.avatar, r.displayName)).catch(() => undefined);
    },
    onAvatarChanged: (uid) => {
      if (uid === me.id) return;
      void ds.userAvatar(uid).then((r) => replacePartner(r.avatar, r.displayName)).catch(() => undefined);
    },
    onPeerLeft: (_uid, reason) => { partnerKind = 'away'; partner?.setPresence('away'); flash(reason === 'presence_expired' ? 'Connection to them was lost.' : 'They left the room.'); },
    onReaction: (_uid, kind) => flash(kind === 'heart' ? '❤️' : '✨'),
    onClosed: (reason, final) => { if (final) { flash(reason); live = null; partnerKind = 'away'; partner?.setPresence('away'); } else status('Reconnecting…'); },
  });
  live.connect();
}

function syncVideo(force = false) {
  if (!live?.state || !screen.current || screen.current.id !== live.state.momentId) return;
  const v = screen.video;
  const expected = live.expectedPositionMs();
  if (expected == null) return;
  if (live.state.paused && !v.paused) v.pause();
  if (!live.state.paused && v.paused) void v.play().catch(() => drawCaption(`${verb()} Play to join the playback.`));
  const drift = v.currentTime * 1000 - expected;
  screen.driftMs = drift;
  if (force || Math.abs(drift) > 300) v.currentTime = Math.max(0, expected / 1000);
  screen.play.set({ label: live.state.paused ? 'Play' : 'Pause', icon: live.state.paused ? 'play' : 'pause' });
  if (debug) console.info(`[sync] drift=${drift.toFixed(0)}ms rtt=${live.rtt}ms v${live.state.version}`);
}

// ---------------------------------------------------------------------------
// Layout: table-space anchors in a headset, viewport-fitted anchors on a screen
// ---------------------------------------------------------------------------

/** A seated viewer's head in table space (eye ~1.2 m, ~0.8 m from the table centre). */
const SEATED_HEAD = new Vector3(0, 0.43, 0.8);
const XR_SCREEN = new Vector3(-0.6, 0.5, -0.32);
const XR_DOCK = new Vector3(0, 0.035, 0.47);
/** Reply sheet in a headset: ~0.56 m from a seated eye, top edge below the eye-to-chin line of the person opposite. */
const XR_SHEET = new Vector3(0, 0.182, 0.3);
const _a = new Vector3();
const _b = new Vector3();
const _c = new Vector3();
const _d = new Vector3();
const _up = new Vector3(0, 1, 0);
const _uiQ = new Quaternion();
const _yawQ = new Quaternion();

function setFacing(obj: Object3D, posTable: Vector3, targetTable: Vector3, scale: number) {
  const table = tableEntity.object3D!;
  table.updateMatrixWorld(true);
  obj.position.copy(posTable);
  obj.scale.setScalar(scale);
  obj.lookAt(table.localToWorld(_a.copy(targetTable)));
}

/** Headset: comfortable seated reach. The screen sits left of the table, the dock at the near edge. */
function applyXrLayout() {
  setFacing(screen.group, XR_SCREEN, SEATED_HEAD, 1);
  setFacing(dock.group, XR_DOCK, SEATED_HEAD, 1);
}

interface Framing {
  fov: number;
  eye: [number, number, number];
  target: [number, number, number];
  /** Screen top-left (or top-centre when screenAlignX is 0) in NDC, its distance and share of the view width. */
  screen: [number, number];
  screenAlignX: -1 | 0;
  screenDist: number;
  screenFrac: number;
  /** Dock bottom-centre in NDC and share of the view width. */
  dockBottom: number;
  dockFrac: number;
}

function framingFor(aspect: number): Framing {
  // portrait phone: stacked — screen on top, the person in the middle, table, cup and dock below
  if (aspect < 0.8) return { fov: 72, eye: [0.1, 1.35, 0.95], target: [0.07, 1.15, -1.05], screen: [0, 0.8], screenAlignX: 0, screenDist: 1.4, screenFrac: 0.88, dockBottom: -0.96, dockFrac: 0.95 };
  if (aspect < 1.45) return { fov: 62, eye: [0, 1.3, 0.34], target: [-0.04, 0.92, -0.9], screen: [-0.94, 0.8], screenAlignX: -1, screenDist: 1.0, screenFrac: 0.38, dockBottom: -0.95, dockFrac: 0.66 };
  return { fov: 56, eye: [0, 1.25, 0.18], target: [-0.06, 0.9, -0.9], screen: [-0.85, 0.78], screenAlignX: -1, screenDist: 1.05, screenFrac: 0.39, dockBottom: -0.94, dockFrac: 0.55 };
}

function hudTopNdc() {
  const hud = document.getElementById('hud');
  const bottom = hud ? hud.getBoundingClientRect().bottom : 0;
  return 1 - (2 * (bottom + 8)) / window.innerHeight;
}

/**
 * Place a camera-facing UI group so that one of its edges/corners lands on an NDC anchor, at a given
 * distance and with a given share of the view width. Iterates a few times to absorb perspective.
 */
function placeAtNdc(obj: Object3D, anchor: [number, number], align: [number, number], dist: number, widthFrac: number, baseW: number, baseH: number, maxScale: number, yaw = 0) {
  const cam = world.camera;
  const camPos = cam.getWorldPosition(_b);
  const table = tableEntity.object3D!;
  // parallel to the image plane (no keystone), optionally turned slightly toward the centre of the view
  cam.getWorldQuaternion(_uiQ).multiply(_yawQ.setFromAxisAngle(_up, yaw));
  // size from the depth along the view axis (not the ray length), so edge-of-view elements are not oversized
  const forward = cam.getWorldDirection(_d);
  const depth = dist * _c.set(anchor[0], anchor[1], 0.5).unproject(cam).sub(camPos).normalize().dot(forward);
  const visibleW = 2 * depth * Math.tan(((cam.fov * Math.PI) / 180) / 2) * cam.aspect;
  const scale = Math.min(maxScale, (widthFrac * visibleW) / baseW);
  const target: [number, number] = [anchor[0], anchor[1]];
  for (let i = 0; i < 4; i++) {
    const dir = _c.set(target[0], target[1], 0.5).unproject(cam).sub(camPos).normalize();
    const world3 = _d.copy(camPos).addScaledVector(dir, dist);
    obj.position.copy(table.worldToLocal(world3));
    obj.scale.setScalar(scale);
    obj.quaternion.copy(_uiQ);
    obj.updateMatrixWorld(true);
    const p = _c.set((align[0] * baseW) / 2, (align[1] * baseH) / 2, 0).applyMatrix4(obj.matrixWorld).project(cam);
    target[0] += anchor[0] - p.x;
    target[1] += anchor[1] - p.y;
  }
  return scale;
}

function layoutDesktopUi(f: Framing) {
  const cam = world.camera;
  const top = Math.min(f.screen[1], hudTopNdc() - 0.03);
  placeAtNdc(screen.group, [f.screen[0], top], [f.screenAlignX, 1], f.screenDist, f.screenFrac, SCREEN_W, SCREEN_H, 1.25, f.screenAlignX ? 0.2 : 0);
  // the dock rests just above the table's near edge: intersect the bottom-centre ray with that plane
  const camPos = cam.getWorldPosition(_b);
  const dir = _c.set(0, f.dockBottom + 0.12, 0.5).unproject(cam).sub(camPos).normalize();
  let dist = 0.6;
  if (dir.y < -0.05) dist = (TABLE.y + 0.06 - camPos.y) / dir.y;
  dist = Math.max(0.42, Math.min(0.85, dist));
  placeAtNdc(dock.group, [0, f.dockBottom], [0, -1], dist, f.dockFrac, dock.width, DOCK_H, 1.15);
}

/** Desktop/phone: a seated eye position and per-aspect framing; dolly back only if scene anchors would clip. */
function placeDesktopCamera() {
  if (!world || world.renderer.xr.isPresenting || xrPreview) return;
  const cam = world.camera;
  const f = framingFor(window.innerWidth / window.innerHeight);
  const player = cam.parent;
  player?.position.set(0, 0, 0);
  player?.rotation.set(0, 0, 0);
  cam.fov = f.fov;
  cam.aspect = window.innerWidth / window.innerHeight;
  cam.updateProjectionMatrix();
  const eye = _a.set(...f.eye);
  const target = new Vector3(...f.target);
  const back = new Vector3().subVectors(eye, target).normalize();
  const start = eye.clone();
  for (let i = 0; i < 10; i++) {
    cam.position.copy(start).addScaledVector(back, i * 0.06);
    cam.lookAt(target);
    cam.updateMatrixWorld(true);
    updateViewer();
    positionCupLabel();
    layoutDesktopUi(f);
    // the presence badge goes on whichever side of the head has room
    for (const side of ['right', 'left'] as const) {
      if (!partner) break;
      partner.badgeSide = side;
      partner.label.setUiScale(legibleScale(partner.label.mesh, 0.034, 13));
      partner.update(1 / 60, CAPTURE_T, _viewer, _camQ);
      if (!safeArea().violations.some((v) => v.startsWith('presence badge'))) break;
    }
    // only clipping of scene-anchored elements is fixed by moving back; UI anchors are fitted by construction
    if (!safeArea().violations.some((v) => /^(presence badge|cup label|onboarding hint) clipped/.test(v))) break;
  }
  if (cupLabel) positionCupLabel();
}

// ---------------------------------------------------------------------------
// Safe area: every key element fully inside the view (and below the 2D header)
// ---------------------------------------------------------------------------

interface SafeAreaReport { violations: string[]; boxes: Record<string, [number, number, number, number]> }
const corners = [new Vector3(), new Vector3(), new Vector3(), new Vector3()];

function planeCorners(mesh: Mesh) {
  const g = mesh.geometry as PlaneGeometry;
  const w = g.parameters.width / 2;
  const h = g.parameters.height / 2;
  mesh.updateWorldMatrix(true, false);
  corners[0].set(-w, -h, 0).applyMatrix4(mesh.matrixWorld);
  corners[1].set(w, -h, 0).applyMatrix4(mesh.matrixWorld);
  corners[2].set(w, h, 0).applyMatrix4(mesh.matrixWorld);
  corners[3].set(-w, h, 0).applyMatrix4(mesh.matrixWorld);
  return corners;
}

function safeArea(): SafeAreaReport {
  const cam = world.camera;
  cam.updateMatrixWorld(true);
  const top = hudTopNdc();
  const W = window.innerWidth;
  const H = window.innerHeight;
  const report: SafeAreaReport = { violations: [], boxes: {} };
  const check = (name: string, pts: Vector3[] | null, visible = true) => {
    if (!pts || !visible) return;
    let x0 = Infinity, y0 = Infinity, x1 = -Infinity, y1 = -Infinity;
    let behind = false;
    for (const p of pts) {
      const q = _c.copy(p).project(cam);
      if (q.z > 1) behind = true;
      x0 = Math.min(x0, q.x); x1 = Math.max(x1, q.x); y0 = Math.min(y0, q.y); y1 = Math.max(y1, q.y);
    }
    report.boxes[name] = [Math.round(((x0 + 1) / 2) * W), Math.round(((1 - y1) / 2) * H), Math.round(((x1 + 1) / 2) * W), Math.round(((1 - y0) / 2) * H)];
    if (behind || x0 < -0.985 || x1 > 0.985 || y0 < -0.985 || y1 > top) report.violations.push(`${name} clipped ${JSON.stringify(report.boxes[name])}`);
  };
  check('screen header', planeCorners(screen.header.mesh));
  check('primary action', planeCorners(screen.play.panel.mesh));
  check('screen', planeCorners(screen.bg.mesh));
  if (partner) check('presence badge', partner.labelCorners(corners));
  if (cupLabel) check('cup label', planeCorners(cupLabel.card.mesh));
  if (hint) check('onboarding hint', planeCorners(hint.mesh), hint.mesh.visible);
  check('dock', planeCorners(dock.panel.mesh));
  // overlaps that would hide information
  const overlap = (a: string, b: string) => {
    const A = report.boxes[a];
    const B = report.boxes[b];
    if (A && B && A[0] < B[2] && B[0] < A[2] && A[1] < B[3] && B[1] < A[3]) report.violations.push(`${a} overlaps ${b}`);
  };
  overlap('presence badge', 'screen');
  overlap('presence badge', 'onboarding hint');
  overlap('presence badge', 'cup label');
  overlap('cup label', 'dock');
  overlap('onboarding hint', 'screen');
  overlap('cup label', 'screen');
  return report;
}

// ---------------------------------------------------------------------------
// Comfort: recenter, exit
// ---------------------------------------------------------------------------

function recenter() {
  const cam = world.camera;
  const pos = new Vector3();
  const quat = new Quaternion();
  cam.getWorldPosition(pos);
  cam.getWorldQuaternion(quat);
  const yaw = new Euler().setFromQuaternion(quat, 'YXZ').y;
  const root = roomEntity.object3D!;
  if (world.renderer.xr.isPresenting) {
    // Move the room to the person (never the person's view): the table edge at a comfortable seated reach.
    root.position.set(pos.x, 0, pos.z);
    root.rotation.set(0, yaw, 0);
    root.position.y = Math.min(0.15, Math.max(-0.25, pos.y - 1.18));
  } else {
    root.position.set(0, 0, 0);
    root.rotation.set(0, 0, 0);
    placeDesktopCamera();
  }
  guardInput(500);
  flash('Recentered.');
}

function exitRoom() {
  stopVoice();
  live?.leave();
  if (world.renderer.xr.isPresenting) world.exitXR();
  else location.href = '/';
}

// ---------------------------------------------------------------------------
// Per-frame system (allocation-free): hover feedback, grab-to-activate, release-to-persist, captions,
// label facing, avatars, poses
// ---------------------------------------------------------------------------

const _viewer = new Vector3();
const _screenPos = new Vector3();
const _camQ = new Quaternion();
const _tableQ = new Quaternion();
let flatView = true;
let frameT = 0;

/** Viewer position and (for flat screens) camera orientation, both in table space. */
function updateViewer() {
  viewerInTable(_viewer);
  flatView = !(world.renderer.xr.isPresenting || xrPreview);
  world.camera.getWorldQuaternion(_camQ);
  tableEntity.object3D!.getWorldQuaternion(_tableQ);
  _camQ.premultiply(_tableQ.invert());
}

function viewerInTable(out: Vector3) {
  world.camera.getWorldPosition(out);
  return tableEntity.object3D!.worldToLocal(out);
}

function updateHover(p: Pressable) {
  const h = p.entity.hasComponent(Hovered);
  if (h === p.hovered) return;
  p.hovered = h;
  if (p.redraw) p.redraw(h);
  else {
    p.object.scale.setScalar(h ? 1.06 : 1);
    if (h) showHoverLabel(p.object);
    else hoverLabel.mesh.visible = false;
  }
}

function pulseGlow(o: MemoryObject) {
  if (o.glow?.visible) (o.glow.material as MeshBasicMaterial).opacity = 0.5 + Math.sin(frameT * 3) * 0.3;
}

/**
 * Turn a table-space label toward the viewer. On a flat screen it lies parallel to the image plane (no keystone);
 * in a headset it faces the head (yaw and pitch).
 */
function faceViewer(obj: Object3D) {
  if (flatView) {
    obj.quaternion.copy(_camQ);
    return;
  }
  const dx = _viewer.x - obj.position.x;
  const dy = _viewer.y - obj.position.y;
  const dz = _viewer.z - obj.position.z;
  obj.rotation.set(-Math.atan2(dy, Math.hypot(dx, dz)), Math.atan2(dx, dz), 0, 'YXZ');
}

/**
 * Flat screens: labels keep a minimum legible text size (pixels) at their current distance. In a headset
 * they keep their real-world size, chosen for seated reading distances.
 */
function legibleScale(obj: Object3D, textM: number, minPx: number) {
  if (!flatView) return 1;
  const d = obj.getWorldPosition(_lp).distanceTo(world.camera.getWorldPosition(_lc));
  const pxPerM = window.innerHeight / (2 * d * Math.tan((world.camera.fov * Math.PI) / 360));
  return Math.max(1, Math.min(3, minPx / (textM * pxPerM)));
}
const _lp = new Vector3();
const _lc = new Vector3();

/**
 * The cup label sits to the viewer's right of the cup with the onboarding hint just above it. On narrow
 * (portrait) screens both are centred over the cup and capped to the view width.
 */
function positionCupLabel() {
  if (!cupLabel) return;
  const cup = cupLabel.entry.group.position;
  const card = cupLabel.card;
  // the reply sheet is modal: the cup card would otherwise draw through it in a headset
  card.mesh.visible = !sheet;
  cupLabel.pressable.enabled = !sheet;
  const narrow = flatView && window.innerWidth / window.innerHeight < 0.8;
  let k = legibleScale(cupLabel.entry.group, 0.072 * 0.22, 12);
  if (flatView) {
    // never wider than ~92% of the view at the cup's distance
    const d = cupLabel.entry.group.getWorldPosition(_lp).distanceTo(world.camera.getWorldPosition(_lc));
    const viewW = 2 * d * Math.tan((world.camera.fov * Math.PI) / 360) * world.camera.aspect;
    k = Math.min(k, Math.max(1, (0.92 * viewW) / (card.width / cardScale)));
  }
  cardScale = k;
  card.setUiScale(k);
  const yaw = Math.atan2(_viewer.x - cup.x, _viewer.z - cup.z);
  const dx = narrow ? 0 : 0.07 + card.width / 2;
  const y = (narrow ? 0.13 : 0.06) + card.height / 2;
  card.mesh.position.set(cup.x + Math.cos(yaw) * dx, y, cup.z - Math.sin(yaw) * dx);
  faceViewer(card.mesh);
  if (narrow !== hintNarrow) {
    hintNarrow = narrow;
    setOnboarding(onboarding);
  }
  if (hint && hint.mesh.visible && hint.mesh.parent === tableEntity.object3D) {
    hint.setUiScale(k);
    const hx = narrow ? 0 : 0.07 + hint.width / 2 - 0.02 * k;
    hint.mesh.position.set(cup.x + Math.cos(yaw) * hx, y + (card.height + hint.height) / 2 + 0.008 * k, cup.z - Math.sin(yaw) * hx);
    faceViewer(hint.mesh);
  }
}
let cardScale = 1;
let hintNarrow = false;

class RoomSystem extends createSystem({ grabbed: { required: [Grabbed] } }) {
  private poseTimer = 0;
  private syncTimer = 0;
  private progressTimer = 0;
  private frames = 0;
  private fpsTimer = 0;

  init() {
    this.queries.grabbed.subscribe('qualify', (entity) => {
      const entry = [...memory.values()].find((o) => o.entity === entity);
      if (entry) activate(entry);
    });
    this.queries.grabbed.subscribe('disqualify', (entity) => {
      const entry = [...memory.values()].find((o) => o.entity === entity);
      if (entry) persistMove(entry);
    });
  }

  update(delta: number, time: number) {
    frameT = capture ? CAPTURE_T : time;
    const dt = capture ? 1 / 60 : delta;
    // the emulator installs itself on window load, which can land after boot: check until it appears
    if (emulated || (emulated = emulatorActive())) snapshotInputs();
    testFrame++;
    updateViewer();
    pressables.forEach(updateHover);
    memory.forEach(pulseGlow);
    memory.forEach(syncFarProxy);

    // Story pacing + voice captions (memoized: redrawn only when the cue changes).
    if (story && !story.paused) {
      const step = story.steps[story.index];
      if (step && step.seconds > 0) {
        story.elapsed += delta;
        if (story.elapsed >= step.seconds) nextStep();
      }
    }
    if (voice) {
      const t = (performance.now() - voice.started) / 1000;
      drawCaption(cueAt(voice.cues, t, voice.moment.transcript ?? voice.moment.caption));
      if (t * 1000 > voice.durationMs + 400) {
        const end = voice.onEnd;
        stopVoice();
        if (!story) drawCaption(`${verb()} Reply to leave something for later.`);
        end?.();
      }
    }
    if (screen.current?.kind === 'clip' || voice) {
      this.progressTimer += delta;
      if (this.progressTimer > 0.25) {
        this.progressTimer = 0;
        drawProgress();
      }
    }

    // Labels turn toward the viewer; the cup label follows the cup if someone moves it.
    positionCupLabel();
    if (hoverLabel.mesh.visible) faceViewer(hoverLabel.mesh);

    // Avatar: tracked input wins; otherwise authored attention toward the viewer (or the screen while it plays).
    if (partner) {
      if (screen.current && !voice) {
        screen.group.getWorldPosition(_screenPos);
        tableEntity.object3D!.worldToLocal(_screenPos);
        partner.attention.copy(_screenPos);
      } else partner.attention.copy(_viewer);
      if (partnerKind === 'live' && performance.now() - partnerLastSeen > 10_000) {
        partnerKind = 'away';
        partner.setPresence('away');
      }
      partner.label.setUiScale(legibleScale(partner.label.mesh, 0.034, 13));
      partner.update(dt, frameT, _viewer, flatView ? _camQ : undefined);
    }

    // Live: send our table-relative head/hand poses at 15 Hz; resync playback at 4 Hz.
    if (live) {
      this.poseTimer += delta;
      if (this.poseTimer > 1 / 15) {
        this.poseTimer = 0;
        live.pose(ownPose());
      }
      this.syncTimer += delta;
      if (this.syncTimer > 0.25) {
        this.syncTimer = 0;
        syncVideo();
      }
    }

    if (debug) {
      this.frames++;
      this.fpsTimer += delta;
      if (this.fpsTimer >= 1) {
        const info = world.renderer.info;
        $('mode-tag').textContent = `${this.frames} fps · ${world.renderer.xr.isPresenting ? 'XR' : '2D'} · ${info.render.calls} draws · ${Math.round(info.render.triangles / 1000)}k tris · ${info.memory.textures} tex`;
        this.frames = 0;
        this.fpsTimer = 0;
      }
    }
  }
}

const _tv = new Vector3();
const _tq = new Quaternion();
const _tq2 = new Quaternion();
function toTable(o: Object3D) {
  const table = tableEntity.object3D!;
  o.getWorldPosition(_tv);
  table.worldToLocal(_tv);
  o.getWorldQuaternion(_tq);
  table.getWorldQuaternion(_tq2);
  const local = _tq2.invert().multiply(_tq);
  return { p: _tv.toArray().map((x) => +x.toFixed(4)), q: local.toArray().map((x) => +x.toFixed(4)) };
}

function ownPose(): PoseMessage {
  const xr = world.renderer.xr.isPresenting;
  const pose: PoseMessage = { tracked: xr, source: xr ? 'xr' : 'pointer', head: toTable(world.camera) };
  if (xr) {
    const grips = world.playerSpaceEntities.gripSpaces;
    if (grips.left.object3D) pose.left = toTable(grips.left.object3D);
    if (grips.right.object3D) pose.right = toTable(grips.right.object3D);
  }
  return pose;
}

boot().catch((e) => {
  console.error(e);
  showLoadError('The room could not start on this device. Try reloading, or use the companion instead.');
});

// ---------------------------------------------------------------------------
// Test hooks (read-only): used by web/scripts/xr-emulator-test.mjs and the capture scripts
// ---------------------------------------------------------------------------

interface RoomState {
  presenting: boolean;
  emulated: boolean;
  presence: string;
  captionsOn: boolean;
  muted: boolean;
  screen: string | null;
  story: boolean;
  voice: string | null;
  replySheet: boolean;
  info: boolean;
  pressables: number;
  roomOffset: [number, number, number];
  head: [number, number, number];
  frame: number;
}

function shown(o: Object3D | null): boolean {
  for (let x: Object3D | null = o; x; x = x.parent) if (!x.visible) return false;
  return true;
}

function testTargets() {
  const out: { label: string; position: [number, number, number]; size: [number, number, number]; normal: [number, number, number]; enabled: boolean; hovered: boolean }[] = [];
  const box = new Box3();
  const c = new Vector3();
  const size = new Vector3();
  const n = new Vector3();
  const q = new Quaternion();
  for (const p of pressables.values()) {
    if (!p.label || !shown(p.object)) continue;
    box.setFromObject(p.object);
    if (box.isEmpty()) continue;
    box.getCenter(c);
    box.getSize(size);
    // panels and hit planes face +Z locally: their world normal is the direction a finger presses against
    n.set(0, 0, 1).applyQuaternion(p.object.getWorldQuaternion(q));
    out.push({ label: p.label, position: [c.x, c.y, c.z], size: [size.x, size.y, size.z], normal: [n.x, n.y, n.z], enabled: p.enabled, hovered: p.hovered });
  }
  return out;
}

// Emulator only: where each input's target ray and index fingertip actually are this frame, in world space, so
// the emulator test can aim by measurement rather than by guessing IWER's internal offsets.
interface InputSnapshot { handedness: string; kind: 'hand' | 'controller'; origin: [number, number, number]; direction: [number, number, number]; tip: [number, number, number] | null; select: boolean }
let emulated = false;
let inputSnapshot: InputSnapshot[] = [];
let testFrame = 0;
const _rayDir = new Vector3();
const _rayQ = new Quaternion();
function snapshotInputs() {
  const xr = world.renderer.xr;
  const frame = xr.getFrame();
  const ref = xr.getReferenceSpace();
  if (!frame || !ref) { inputSnapshot = []; return; }
  const next: InputSnapshot[] = [];
  for (const src of frame.session.inputSources) {
    const ray = frame.getPose(src.targetRaySpace, ref);
    if (!ray) continue;
    const t = ray.transform;
    _rayQ.set(t.orientation.x, t.orientation.y, t.orientation.z, t.orientation.w);
    _rayDir.set(0, 0, -1).applyQuaternion(_rayQ);
    let tip: [number, number, number] | null = null;
    const joint = src.hand?.get('index-finger-tip');
    if (joint && frame.getJointPose) {
      const jp = frame.getJointPose(joint, ref);
      if (jp) tip = [jp.transform.position.x, jp.transform.position.y, jp.transform.position.z];
    }
    next.push({ handedness: src.handedness, kind: src.hand ? 'hand' : 'controller', origin: [t.position.x, t.position.y, t.position.z], direction: [_rayDir.x, _rayDir.y, _rayDir.z], tip, select: false });
  }
  inputSnapshot = next;
}

function roomState(): RoomState {
  const o = roomEntity.object3D!.position;
  return {
    presenting: world.renderer.xr.isPresenting,
    emulated: !!window.IWER_DEVICE,
    presence: partner?.presence ?? partnerKind, // what the badge shows (live, simulated, recorded, away)
    captionsOn,
    muted,
    screen: screen.current?.id ?? null,
    story: !!story,
    voice: voice?.moment.id ?? null,
    replySheet: !!sheet,
    info: !!info,
    pressables: pressables.size,
    roomOffset: [o.x, o.y, o.z],
    head: (() => { const h = world.camera.getWorldPosition(new Vector3()); return [h.x, h.y, h.z] as [number, number, number]; })(),
    frame: testFrame,
  };
}
