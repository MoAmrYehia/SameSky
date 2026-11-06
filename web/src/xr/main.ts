// SameSky's hands-first room (Meta Quest 3/3S via IWSDK, with desktop/phone fallback in the same page).
//
// Defining interaction: an object with a message waiting inside it. Pick up the glowing cup and the
// recorded voice note plays from the cup itself (spatial audio) with captions; place a reply beside it
// and it waits for the next visit. Frames and postcards open their photo/clip on the shared screen.

import {
  AudioSource, AudioUtils, createSystem, DistanceGrabbable, type Entity, Grabbed, Hovered, MovementMode, OneHandGrabbable,
  ReferenceSpaceType, SessionMode, World,
} from '@iwsdk/core';
import {
  AudioListener, CanvasTexture, Color, Euler, Group, Mesh, MeshBasicMaterial, PlaneGeometry, Quaternion, RingGeometry, SRGBColorSpace,
  type Texture, Vector3, VideoTexture,
} from 'three';
import { avatarDataUrl } from '../shared/avatar';
import { cueAt, loadCaptions, type Cue } from '../shared/captions';
import type { Award, Me, Moment, Recap, Reply, Room, RoomObject } from '../shared/contracts';
import { currentMode, dataSource, SampleSource, storedToken } from '../shared/data';
import { SAMPLE_PEOPLE } from '../shared/sample';
import { cityOf } from '../shared/tz';
import { Avatar3D } from './avatar3d';
import { buildEnvironment } from './env';
import { LiveSession, type PoseMessage } from './live';
import { buildKeepsake, buildMemoryObject, infoCard, loadTexture, type MemoryObject } from './objects';
import { Button, CanvasPanel, COLORS, FONT, guardInput, makePressable, pill, pressables, registerPressable, roundRect, wrapText } from './ui3d';

const params = new URLSearchParams(location.search);
const mode: 'sample' | 'live' = params.get('mode') === 'sample' ? 'sample' : params.get('mode') === 'live' || (currentMode() === 'live' && storedToken()) ? 'live' : 'sample';
const ds = dataSource(mode);
const debug = params.has('debug');
const $ = (id: string) => document.getElementById(id)!;
const status = (text: string) => { $('status').textContent = text; };

// ---------------------------------------------------------------------------
// State
// ---------------------------------------------------------------------------

let world: World;
let me: Me;
let room: Room;
let moments: Moment[] = [];
let recap: Recap | null = null;
let awards: Award[] = [];
const memory = new Map<string, MemoryObject & { entity: Entity }>();
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

// ---------------------------------------------------------------------------
// Boot
// ---------------------------------------------------------------------------

async function boot() {
  $('mode-tag').textContent = mode === 'sample' ? 'Sample · nothing leaves this browser' : 'Signed in';
  if (mode === 'live' && !storedToken()) {
    $('overlay-tag').textContent = 'Sign-in needed';
    $('overlay-text').textContent = 'Open the companion to sign in, or try the sample room.';
    ($('enter-2d') as HTMLButtonElement).textContent = 'Try the sample room';
    $('enter-2d').onclick = () => { location.href = '/room.html?mode=sample'; };
    return;
  }
  let rooms: Room[];
  try {
    [me, rooms] = await Promise.all([ds.me(), ds.rooms()]);
  } catch (e) {
    $('overlay-text').textContent = e instanceof Error ? e.message : 'Could not load your room.';
    return;
  }
  const picked = rooms.find((r) => r.id === params.get('room')) ?? rooms.find((r) => r.kind === 'direct') ?? rooms[0];
  if (!picked) {
    $('overlay-text').textContent = 'You don’t have a room yet. Create one in the companion first.';
    return;
  }
  room = picked;
  $('overlay-tag').textContent = mode === 'sample' ? 'Sample day · fictional people' : room.name;
  $('welcome-title').textContent = room.kind === 'direct' ? `${room.members.map((m) => m.displayName).join(' + ')}` : room.name;

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
    render: { fov: 62, near: 0.02, far: 40, camera: { position: [0, 1.18, 0.12], lookAt: [0, 0.86, -0.85] } },
  });
  world.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
  placeDesktopCamera();
  world.scene.background = new Color(0x15110f);

  const env = buildEnvironment();
  roomEntity = world.createTransformEntity(new Group(), { persistent: true });
  roomEntity.object3D!.add(env.root);
  env.root.remove(env.table);
  tableEntity = world.createTransformEntity(env.table, { parent: roomEntity });

  world.registerSystem(RoomSystem);
  if (debug) (window as unknown as Record<string, unknown>).__samesky = { world, pressables, memory, activate };
  buildScreen();
  buildDock();
  buildShelf();
  await placeObjects(objects);
  setupPartner();
  buildHints();

  // XR availability + entry.
  const xrOk = await navigator.xr?.isSessionSupported('immersive-vr').catch(() => false);
  const enterVr = $('enter-vr') as HTMLButtonElement;
  enterVr.hidden = !xrOk;
  enterVr.onclick = () => {
    unlockAudio();
    $('overlay').hidden = true;
    world.launchXR();
  };
  $('enter-2d').onclick = () => {
    unlockAudio();
    $('overlay').hidden = true;
    status('Click objects to open them. On a Quest, use “Enter in VR”.');
    window.setTimeout(() => status(''), 6000);
  };
  if (!xrOk) $('overlay-hint').textContent = 'This browser has no VR headset available, so the room opens on this screen. Click to select.';

  world.renderer.xr.addEventListener('sessionstart', () => {
    guardInput(1200);
    const session = world.renderer.xr.getSession();
    session?.addEventListener('inputsourceschange', () => guardInput(600)); // tracking loss/regain never fires a selection
    session?.addEventListener('visibilitychange', () => guardInput(800));
    window.setTimeout(recenter, 300);
  });
  world.renderer.xr.addEventListener('sessionend', () => {
    $('overlay').hidden = false;
    roomEntity.object3D!.position.set(0, 0, 0);
    roomEntity.object3D!.rotation.set(0, 0, 0);
    window.setTimeout(placeDesktopCamera, 50);
  });

  if (params.get('session') && mode === 'live') void joinLive(params.get('session')!);
  if (params.get('watch') === '1') {
    const clip = moments.find((m) => m.kind === 'clip');
    if (clip) window.setTimeout(() => showMoment(clip, { together: true }), 800);
  }
}

/** Seated eye height, looking at the far side of the table: the same framing a phone/desktop participant gets. */
function placeDesktopCamera() {
  if (world.renderer.xr.isPresenting) return;
  const player = world.camera.parent;
  player?.position.set(0, 0, 0);
  player?.rotation.set(0, 0, 0);
  world.camera.position.set(0, 1.22, 0.2);
  world.camera.lookAt(0, 0.95, -0.9);
}

function unlockAudio() {
  // Browsers require a user gesture before audio can play; resume IWSDK's listener context now.
  const listener = world.camera.children.find((c) => c instanceof AudioListener) as AudioListener | undefined;
  void listener?.context.resume();
}

// ---------------------------------------------------------------------------
// The shared screen: photos, clips, recap story, captions
// ---------------------------------------------------------------------------

interface Screen {
  group: Group;
  entity: Entity;
  media: Mesh<PlaneGeometry, MeshBasicMaterial>;
  header: CanvasPanel;
  captions: CanvasPanel;
  progress: CanvasPanel;
  play: Button;
  video: HTMLVideoElement;
  texture: Texture | null;
  current: Moment | null;
  together: boolean;
  driftMs: number | null;
}
let screen: Screen;

function buildScreen() {
  const group = new Group();
  // Floating to the left of the table (as in the concept art) so the person across stays visible.
  group.position.set(-0.5, 0.56, -0.42);
  group.rotation.set(0.04, 0.5, 0);
  group.scale.setScalar(0.92);
  const entity = world.createTransformEntity(group, { parent: tableEntity });
  const bezel = new Mesh(new PlaneGeometry(0.98, 0.6), new MeshBasicMaterial({ color: 0x141a19, transparent: true, opacity: 0.92 }));
  bezel.position.z = -0.004;
  const media = new Mesh(new PlaneGeometry(0.92, 0.5175), new MeshBasicMaterial({ color: 0xffffff, toneMapped: false }));
  media.position.y = 0.0;
  const header = new CanvasPanel(0.98, 0.08);
  header.mesh.position.set(0, 0.34, 0);
  const captions = new CanvasPanel(0.92, 0.09);
  captions.mesh.position.set(0, -0.2, 0.004);
  const progress = new CanvasPanel(0.6, 0.03);
  progress.mesh.position.set(0.12, -0.34, 0.002);
  group.add(bezel, media, header.mesh, captions.mesh, progress.mesh);
  const video = document.createElement('video');
  video.playsInline = true;
  video.crossOrigin = 'anonymous';
  video.preload = 'auto';
  screen = { group, entity, media, header, captions, progress, play: null as unknown as Button, video, texture: null, current: null, together: false, driftMs: null };
  screen.play = new Button(world, entity, { label: 'Play our day', icon: 'play', width: 0.3, height: 0.085, primary: true }, () => togglePlay());
  screen.play.panel.mesh.position.set(-0.3, -0.35, 0.004);
  setScreenIdle();
  video.addEventListener('ended', () => { if (story) nextStep(); else drawProgress(); });
}

function drawHeader(title: string, sub: string, together = false) {
  screen.header.draw((ctx, W, H) => {
    ctx.fillStyle = 'rgba(20,26,25,0.92)';
    roundRect(ctx, 0, 0, W, H, H * 0.4);
    ctx.fill();
    ctx.fillStyle = '#fff';
    ctx.font = `700 ${H * 0.4}px ${FONT}`;
    ctx.textBaseline = 'middle';
    ctx.fillText(title, H * 0.5, H * 0.5, W * 0.6);
    ctx.fillStyle = '#c9d6cd';
    ctx.font = `500 ${H * 0.3}px ${FONT}`;
    ctx.textAlign = 'right';
    ctx.fillText(sub, W - H * 0.5, H * 0.5, W * 0.38);
    if (together) {
      ctx.textAlign = 'left';
      ctx.fillStyle = '#9fd3ae';
      ctx.fillText('● Watching together', W * 0.52, H * 0.5);
    }
  });
}

function drawCaption(text: string) {
  screen.captions.mesh.visible = captionsOn && !!text;
  if (!screen.captions.mesh.visible) return;
  screen.captions.draw((ctx, W, H) => {
    ctx.font = `600 ${H * 0.34}px ${FONT}`;
    const lines = wrapText(ctx, text, W * 0.9).slice(0, 2);
    const w = Math.max(...lines.map((l) => ctx.measureText(l).width)) + H * 0.6;
    ctx.fillStyle = 'rgba(10,12,12,0.86)';
    roundRect(ctx, (W - w) / 2, 0, w, H, H * 0.22);
    ctx.fill();
    ctx.fillStyle = '#fff';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    lines.forEach((l, i) => ctx.fillText(l, W / 2, H * (lines.length === 1 ? 0.5 : 0.3 + i * 0.4)));
  });
}

function drawProgress() {
  const v = screen.video;
  const frac = screen.current?.kind === 'clip' && v.duration ? v.currentTime / v.duration : story ? story.index / Math.max(1, story.steps.length - 1) : 0;
  screen.progress.draw((ctx, W, H) => {
    ctx.fillStyle = 'rgba(255,255,255,0.25)';
    roundRect(ctx, 0, H * 0.35, W, H * 0.3, H * 0.15);
    ctx.fill();
    ctx.fillStyle = '#7fbf95';
    roundRect(ctx, 0, H * 0.35, Math.max(H * 0.3, W * frac), H * 0.3, H * 0.15);
    ctx.fill();
    if (debug && screen.driftMs != null) {
      ctx.fillStyle = '#fff';
      ctx.font = `${H * 0.6}px ${FONT}`;
      ctx.fillText(`drift ${screen.driftMs.toFixed(0)} ms`, W * 0.75, H * 0.3);
    }
  });
}

function setMediaTexture(tex: Texture | null, aspect = 16 / 9) {
  if (screen.texture && screen.texture !== tex) screen.texture.dispose();
  screen.texture = tex;
  screen.media.material.map = tex;
  screen.media.material.color.set(tex ? 0xffffff : 0x2a3431);
  screen.media.material.needsUpdate = true;
  const h = 0.5175;
  screen.media.scale.set(Math.min(1, (h * aspect) / 0.92), Math.min(1, 0.92 / aspect / h), 1);
}

function setScreenIdle() {
  stopVideo();
  screen.current = null;
  const title = recap?.plan?.title ?? (moments.length ? 'Shared moments' : 'Nothing shared yet');
  drawHeader(title, recap ? `${recap.generatorLabel.replace(' (no AI call)', '')}` : room.name);
  const hero = moments.find((m) => m.kind === 'photo');
  setMediaTexture(hero?.media?.url ? loadTexture(hero.media.url) : null, 16 / 10);
  drawCaption(recap ? 'Pinch “Play our day” to watch the story, or pick up something on the table.' : 'Pick up something on the table to open it.');
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
  screen.together = !!opts.together;
  const owner = room.members.find((x) => x.id === m.ownerId);
  drawHeader(`${owner?.displayName ?? m.ownerName}'s ${m.kind === 'clip' ? 'clip' : m.kind === 'voice' ? 'voice note' : m.kind === 'text' ? 'note' : 'photo'}`, `${m.captureLocal.time} in ${cityOf(m.captureLocal.zone)}`, opts.together || !!live);
  if (m.kind === 'clip' && m.media?.url) {
    const v = screen.video;
    v.src = m.media.url;
    v.muted = muted;
    const tex = new VideoTexture(v);
    tex.colorSpace = SRGBColorSpace;
    setMediaTexture(tex, (m.media.width ?? 16) / (m.media.height ?? 9));
    if (!live) void v.play().catch(() => drawCaption('Pinch Play to start the clip.'));
    screen.play.set({ label: 'Pause', icon: 'pause' });
    if (opts.together && mode === 'sample' && partner) {
      partner.setPresence('simulated', undefined);
      partnerLookAtScreen = true;
    }
  } else {
    stopVideo();
    if (m.media?.url && m.kind === 'photo') setMediaTexture(loadTexture(m.media.url), (m.media.width ?? 16) / (m.media.height ?? 10));
    else if (m.kind === 'voice' || m.kind === 'text') void portraitCard(m).then((tex) => { if (screen.current?.id === m.id) setMediaTexture(tex, 16 / 9); });
    else setMediaTexture(null);
    screen.play.set({ label: story ? 'Pause' : 'Back', icon: story ? 'pause' : 'x' });
  }
  drawCaption(opts.segment ?? (m.kind === 'voice' ? '' : m.caption));
  drawProgress();
}

/** Voice notes and text moments show the person's own avatar portrait with their words, never a fake video. */
async function portraitCard(m: Moment): Promise<Texture> {
  const def = room.members.find((x) => x.id === m.ownerId)?.avatar?.definition ?? SAMPLE_PEOPLE['sample-maya'].avatar;
  const img = new Image();
  img.src = avatarDataUrl(def, { background: '#e9dccb' });
  await img.decode().catch(() => undefined);
  const c = document.createElement('canvas');
  c.width = 1280;
  c.height = 720;
  const ctx = c.getContext('2d')!;
  const g = ctx.createLinearGradient(0, 0, 1280, 720);
  g.addColorStop(0, '#3b4b44');
  g.addColorStop(1, '#1b2321');
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, 1280, 720);
  ctx.drawImage(img, 90, 170, 340, 340);
  ctx.fillStyle = '#f1ede6';
  ctx.font = `700 64px ${FONT}`;
  ctx.fillText(m.kind === 'voice' ? `“${m.caption}”` : m.ownerName, 500, 260, 700);
  ctx.font = `500 34px ${FONT}`;
  ctx.fillStyle = '#c9d6cd';
  if (m.kind === 'voice') {
    ctx.fillText(`${m.ownerName} · Recorded message · ${m.captureLocal.time} in ${cityOf(m.captureLocal.zone)}`, 500, 320, 720);
    ctx.fillStyle = '#79a985';
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
    drawHeader(name ? `${name}'s chapter` : recap!.plan!.title, recap!.generatorLabel.replace(' (no AI call)', ''));
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
}

function stopVoice() {
  if (!voice) return;
  try { AudioUtils.stop(voice.entity); } catch { /* not loaded yet */ }
  voice = null;
  if (partner) partner.setPresence(partnerKind);
}

// ---------------------------------------------------------------------------
// Objects on the table
// ---------------------------------------------------------------------------

const hoverLabel = new CanvasPanel(0.36, 0.06);

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
  hoverLabel.mesh.visible = false;
  tableEntity.object3D!.add(hoverLabel.mesh);
}

function addObject(record: RoomObject, moment?: Moment, reply?: Reply) {
  const obj = buildMemoryObject(record, moment, reply);
  const entity = world.createTransformEntity(obj.group, { parent: tableEntity });
  entity.addComponent(OneHandGrabbable, { rotate: false, translate: true });
  entity.addComponent(DistanceGrabbable, { rotate: false, translate: true, scale: false, movementMode: MovementMode.MoveFromTarget });
  makePressableObject(entity, obj);
  memory.set(record.id, { ...obj, entity });
}

function makePressableObject(entity: Entity, obj: MemoryObject) {
  // Pinch from a distance or click (desktop) selects; grabbing selects too (see RoomSystem.init).
  obj.group.userData.pressable = registerPressable(entity, obj.group, () => activate(obj));
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
  if (m.kind === 'voice') {
    showMoment(m, { segment: '' });
    playVoice(m);
    if (obj.glow) obj.glow.visible = false;
    if (onboarding === 'cup') setOnboarding('reply');
  } else {
    showMoment(m, { together: !!live });
  }
}

function persistMove(entry: MemoryObject & { entity: Entity }) {
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
// Dock (seated reach), reply sheet, info cards
// ---------------------------------------------------------------------------

let ccButton: Button;
let soundButton: Button;
let sheet: { group: Group; entity: Entity; buttons: Button[]; panel: CanvasPanel } | null = null;
let info: { panel: CanvasPanel; timer: number } | null = null;

function buildDock() {
  const group = new Group();
  group.position.set(0, 0.16, 0.5);
  group.rotation.x = -0.75;
  const dock = world.createTransformEntity(group, { parent: tableEntity });
  const items: [string, string, () => void][] = [
    ['Our day', 'calendar', () => { stopVoice(); story = null; startStory(); }],
    ['Reply', 'reply', () => openReplySheet()],
    ['Love', 'heart', () => react('heart')],
    ['Captions', 'cc', () => { captionsOn = !captionsOn; ccButton.setActive(captionsOn); drawCaption(captionsOn ? 'Captions on.' : ''); }],
    ['Sound', 'sound', () => toggleMute()],
    ['Recenter', 'recenter', () => recenter()],
    ['Reset', 'reset', () => resetObjects()],
    ['Exit', 'exit', () => exitRoom()],
  ];
  const w = 0.078;
  items.forEach(([label, icon, fn], i) => {
    const b = new Button(world, dock, { label, icon, width: w, height: w, dark: true }, fn);
    b.panel.mesh.position.set((i - (items.length - 1) / 2) * (w + 0.008), 0, 0);
    if (label === 'Captions') { ccButton = b; b.setActive(true); }
    if (label === 'Sound') soundButton = b;
  });
}

function toggleMute() {
  muted = !muted;
  screen.video.muted = muted;
  const listener = world.camera.children.find((c) => c instanceof AudioListener) as AudioListener | undefined;
  listener?.setMasterVolume(muted ? 0 : 1);
  soundButton.set({ label: muted ? 'Muted' : 'Sound', icon: muted ? 'mute' : 'sound' });
  soundButton.setActive(muted);
}

const QUICK_REPLIES = ['On my way — save me that seat.', 'That light! Tell me everything tomorrow.', 'Thinking of you. ❤️'];

function openReplySheet() {
  closeSheet();
  if (hint) hint.panel.mesh.visible = hint.ring.visible = false;
  const target = [...moments].reverse().find((m) => !m.isMine) ?? moments[moments.length - 1];
  if (!target) return flash('Nothing to reply to yet.');
  const group = new Group();
  group.position.set(0, 0.3, 0.08);
  group.rotation.x = -0.35;
  const entity = world.createTransformEntity(group, { parent: tableEntity });
  const panel = new CanvasPanel(0.5, 0.09);
  panel.draw((ctx, W, H) => {
    ctx.fillStyle = 'rgba(247,242,234,0.97)';
    roundRect(ctx, 0, 0, W, H, H * 0.3);
    ctx.fill();
    ctx.fillStyle = COLORS.ink;
    ctx.font = `700 ${H * 0.32}px ${FONT}`;
    ctx.textBaseline = 'middle';
    ctx.fillText(`Reply to ${target.ownerName}`, H * 0.35, H * 0.36);
    ctx.fillStyle = COLORS.muted;
    ctx.font = `500 ${H * 0.22}px ${FONT}`;
    ctx.fillText(`Only people who can see this ${target.kind === 'voice' ? 'voice note' : target.kind} will see your reply.`, H * 0.35, H * 0.72, W - H * 0.7);
  });
  panel.mesh.position.y = 0.16;
  group.add(panel.mesh);
  const buttons = QUICK_REPLIES.map((text, i) => {
    const b = new Button(world, entity, { label: text, width: 0.5, height: 0.058 }, () => void sendReply(target, text));
    b.panel.mesh.position.set(0, 0.085 - i * 0.066, 0);
    return b;
  });
  const close = new Button(world, entity, { label: 'Not now', icon: 'x', width: 0.16, height: 0.05, dark: true }, () => closeSheet());
  close.panel.mesh.position.set(0, -0.13, 0);
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
  panel.mesh.position.set(0, 0.3, 0.12);
  panel.mesh.rotation.x = -0.3;
  tableEntity.object3D!.add(panel.mesh);
  info = { panel, timer: window.setTimeout(() => { panel.mesh.removeFromParent(); panel.dispose(); info = null; }, 7000) };
}

let flashPanel: CanvasPanel | null = null;
let flashTimer = 0;
function flash(text: string) {
  status(text);
  window.clearTimeout(flashTimer);
  if (!flashPanel) {
    flashPanel = new CanvasPanel(0.6, 0.06);
    flashPanel.mesh.position.set(0, 0.24, 0.42);
    flashPanel.mesh.rotation.x = -0.5;
    tableEntity.object3D!.add(flashPanel.mesh);
  }
  flashPanel.mesh.visible = true;
  flashPanel.draw((ctx, W, H) => pill(ctx, text, W, H, { bg: 'rgba(22,28,26,0.92)' }));
  flashTimer = window.setTimeout(() => { if (flashPanel) flashPanel.mesh.visible = false; status(''); }, 3500);
}

// ---------------------------------------------------------------------------
// Keepsake shelf
// ---------------------------------------------------------------------------

function buildShelf() {
  const group = new Group();
  group.position.set(0.86, 0.16, -0.05);
  group.rotation.y = -0.85;
  const entity = world.createTransformEntity(group, { parent: tableEntity });
  const plank = new Mesh(new PlaneGeometry(0.52, 0.16), new MeshBasicMaterial({ color: 0x3c2a1e }));
  plank.rotation.x = -Math.PI / 2;
  group.add(plank);
  const label = new CanvasPanel(0.4, 0.05);
  label.draw((ctx, W, H) => pill(ctx, 'Our keepsakes', W, H, { bg: 'rgba(22,28,26,0.85)' }));
  label.mesh.position.set(0, 0.27, 0);
  group.add(label.mesh);
  awards.forEach((a, i) => {
    const k = buildKeepsake(a);
    k.position.set((i - 1) * 0.17, 0, 0);
    const p = makePressable(world, k, entity, () => showInfo(a.title, [a.trigger, a.earned ? 'Earned together.' : `${a.progress} of ${a.target} so far — missed days never count against you.`], a.earned ? '✓ Earned' : undefined));
    void p;
  });
}

// ---------------------------------------------------------------------------
// Partner avatar: simulated (sample), recorded, or live
// ---------------------------------------------------------------------------

let partnerLookAtScreen = false;
const ACROSS = { p: [0.3, 0.43, -0.7], q: new Quaternion().setFromEuler(new Euler(0, -0.2, 0)).toArray() as number[] };

function setupPartner() {
  const other = room.members.find((m) => m.id !== me.id);
  if (!other) return;
  const def = other.avatar?.definition ?? SAMPLE_PEOPLE['sample-maya'].avatar;
  partner = new Avatar3D(def, other.displayName);
  partnerKind = mode === 'sample' ? 'simulated' : 'away';
  partner.setPresence(partnerKind);
  partner.setPose(ACROSS, { p: [0.16, 0.025, -0.4] }, { p: [0.42, 0.025, -0.42] }, false);
  tableEntity.object3D!.add(partner.root);
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
      if (other && partner && other.avatar) {
        partner.root.removeFromParent();
        partner.dispose();
        partner = new Avatar3D(other.avatar.definition, other.displayName);
        tableEntity.object3D!.add(partner.root);
        partner.setPose(ACROSS, null, null, false);
      }
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
    onPeerJoined: (_uid, name) => { partnerKind = 'live'; partner?.setPresence('live'); flash(`${name ?? 'They'} joined.`); },
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
  if (!live.state.paused && v.paused) void v.play().catch(() => drawCaption('Pinch Play to join the playback.'));
  const drift = v.currentTime * 1000 - expected;
  screen.driftMs = drift;
  if (force || Math.abs(drift) > 300) v.currentTime = Math.max(0, expected / 1000);
  screen.play.set({ label: live.state.paused ? 'Play' : 'Pause', icon: live.state.paused ? 'play' : 'pause' });
  if (debug) console.info(`[sync] drift=${drift.toFixed(0)}ms rtt=${live.rtt}ms v${live.state.version}`);
}

// ---------------------------------------------------------------------------
// Onboarding: discover the first meaningful moment within a minute
// ---------------------------------------------------------------------------

let hint: { panel: CanvasPanel; ring: Mesh } | null = null;

function buildHints() {
  const panel = new CanvasPanel(0.62, 0.06);
  const ring = new Mesh(new RingGeometry(0.1, 0.112, 48), new MeshBasicMaterial({ color: 0xffd78a, transparent: true, opacity: 0.9, depthWrite: false }));
  ring.rotation.x = -Math.PI / 2;
  tableEntity.object3D!.add(panel.mesh, ring);
  hint = { panel, ring };
  setOnboarding(onboarding);
}

function setOnboarding(stage: string) {
  onboarding = stage;
  try { localStorage.setItem(onboardingKey, stage); } catch { /* ignore */ }
  if (!hint) return;
  const cup = [...memory.values()].find((o) => o.record.objectType === 'cup');
  if (stage === 'cup' && cup) {
    hint.panel.draw((ctx, W, H) => pill(ctx, 'Something is waiting — pinch the glowing cup', W, H, { bg: 'rgba(61,107,79,0.95)' }));
    hint.panel.mesh.position.set(cup.group.position.x, 0.2, cup.group.position.z);
    hint.ring.position.set(cup.group.position.x, 0.004, cup.group.position.z);
    hint.panel.mesh.visible = hint.ring.visible = true;
  } else if (stage === 'reply') {
    hint.panel.draw((ctx, W, H) => pill(ctx, 'Leave something for later: pinch Reply below', W, H, { bg: 'rgba(61,107,79,0.95)' }));
    hint.panel.mesh.position.set(-0.05, 0.26, 0.42);
    hint.panel.mesh.rotation.x = -0.5;
    hint.ring.visible = false;
    hint.panel.mesh.visible = true;
  } else {
    hint.panel.mesh.visible = hint.ring.visible = false;
  }
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
    root.position.set(pos.x, 0, pos.z);
    root.rotation.set(0, yaw, 0);
    // Keep the table edge at a comfortable seated reach regardless of real head height.
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
// Per-frame system: hover feedback, grab-to-activate, release-to-persist, captions, avatars, poses
// ---------------------------------------------------------------------------

class RoomSystem extends createSystem({ grabbed: { required: [Grabbed] } }) {
  private poseTimer = 0;
  private syncTimer = 0;
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
    // Hover feedback for buttons and objects.
    for (const p of pressables.values()) {
      const h = p.entity.hasComponent(Hovered);
      if (h !== p.hovered) {
        p.hovered = h;
        if (p.redraw) p.redraw(h);
        else p.object.scale.setScalar(h ? 1.06 : 1);
        if (h && !p.redraw) showHoverLabel(p.object);
        else if (!h && !p.redraw) hoverLabel.mesh.visible = false;
      }
    }
    // Waiting cups glow until opened.
    for (const o of memory.values()) if (o.glow?.visible) (o.glow.material as MeshBasicMaterial).opacity = 0.45 + Math.sin(time * 3) * 0.35;
    if (hint?.ring.visible) hint.ring.scale.setScalar(1 + Math.sin(time * 3) * 0.06);

    // Story pacing + voice captions.
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
        if (!story) drawCaption('Pinch Reply to leave something for later.');
        end?.();
      }
    }
    if (screen.current?.kind === 'clip' && !story) drawProgressThrottled(time);

    // Avatars.
    if (partner) {
      if (mode === 'sample' || partnerKind !== 'live') {
        const look = partnerLookAtScreen || !!screen.current ? -0.95 : -0.2 + Math.sin(time * 0.4) * 0.22;
        const q = new Quaternion().setFromEuler(new Euler(partnerLookAtScreen ? -0.15 : 0, look, 0));
        partner.setPose({ p: ACROSS.p, q: q.toArray() as number[] }, null, null, false);
      } else if (performance.now() - partnerLastSeen > 10_000 && partnerKind === 'live') {
        partnerKind = 'away';
        partner.setPresence('away');
      }
      partner.update(delta, time);
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
        $('mode-tag').textContent = `${this.frames} fps · ${world.renderer.xr.isPresenting ? 'XR' : '2D'} · ${world.renderer.info.render.calls} draws`;
        this.frames = 0;
        this.fpsTimer = 0;
      }
    }
  }
}

let lastProgress = 0;
function drawProgressThrottled(time: number) {
  if (time - lastProgress > 0.25) {
    lastProgress = time;
    drawProgress();
  }
}

function showHoverLabel(obj: import('three').Object3D | null) {
  const entry = [...memory.values()].find((o) => o.group === obj);
  if (!entry) return;
  const m = entry.moment;
  const text = entry.reply ? `${entry.reply.authorName}: “${entry.reply.text.slice(0, 40)}”` : m ? `${m.ownerName} · ${m.kind === 'voice' ? `“${m.caption}”` : m.caption.slice(0, 42)}` : '';
  hoverLabel.draw((ctx, W, H) => pill(ctx, text, W, H));
  hoverLabel.mesh.position.set(entry.group.position.x, 0.24, entry.group.position.z);
  hoverLabel.mesh.visible = true;
}

const _v = new Vector3();
const _q = new Quaternion();
function toTable(o: import('three').Object3D) {
  const table = tableEntity.object3D!;
  o.getWorldPosition(_v);
  table.worldToLocal(_v);
  o.getWorldQuaternion(_q);
  const tq = new Quaternion();
  table.getWorldQuaternion(tq);
  const local = tq.invert().multiply(_q);
  return { p: _v.toArray().map((x) => +x.toFixed(4)), q: local.toArray().map((x) => +x.toFixed(4)) };
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
  $('overlay-text').textContent = 'The room could not start on this device. Try the companion instead.';
});

