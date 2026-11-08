// Offscreen renders of the same 3D assets used in the room: portraits of each person's avatar — their
// owner-approved photo-derived model (avatarRig.ts) or the labelled demo kit (character.ts) — and keepsake
// product shots (keepsakes3d.ts). Loaded lazily by the companion, so the companion's main bundle stays free
// of three.js. One renderer, one light rig, one camera language; a neutral rig for the owner's likeness review.

import {
  Box3, Color, DirectionalLight, Group, HemisphereLight, NeutralToneMapping, PerspectiveCamera, PMREMGenerator, Quaternion,
  Scene, SRGBColorSpace, Vector3, WebGLRenderer, type Texture,
} from 'three';
import { HDRLoader } from 'three/examples/jsm/loaders/HDRLoader.js';
import { acquireAvatarModel, clearAvatarModels, SkinnedAvatar } from './avatarRig';
import type { AvatarDefinition, AvatarView } from './contracts';
import { CharacterModel, definitionKey, loadCharacterKit } from './character';
import { buildKeepsake, texturesReady } from './keepsakes3d';

let renderer: WebGLRenderer | null = null;
let env: Texture | null = null;
let queue: Promise<unknown> = Promise.resolve();
const memo = new Map<string, Promise<string>>();
const STORE = 'samesky.portraits.v4';

function stored(): Record<string, string> {
  try {
    return JSON.parse(sessionStorage.getItem(STORE) ?? '{}');
  } catch {
    return {};
  }
}

function remember(key: string, url: string) {
  try {
    const all = stored();
    all[key] = url;
    sessionStorage.setItem(STORE, JSON.stringify(all));
  } catch {
    /* storage full or blocked: memory cache still works */
  }
}

async function setup() {
  if (!renderer) {
    renderer = new WebGLRenderer({ antialias: true, alpha: true, preserveDrawingBuffer: true });
    renderer.outputColorSpace = SRGBColorSpace;
    renderer.toneMapping = NeutralToneMapping;
    renderer.toneMappingExposure = 1.05;
    renderer.setPixelRatio(1);
  }
  if (!env) {
    const hdr = await new HDRLoader().loadAsync('/assets/env/lounge_1k.hdr');
    const pmrem = new PMREMGenerator(renderer);
    env = pmrem.fromEquirectangular(hdr).texture;
    hdr.dispose();
    pmrem.dispose();
  }
  return renderer;
}

/** Owner review: even, neutral light so skin tones match the photo (no warm room grade). */
function neutralStage() {
  const scene = new Scene();
  scene.environment = env;
  scene.environmentIntensity = 0.55;
  scene.add(new HemisphereLight(0xffffff, 0x8a8a8a, 0.55));
  const key = new DirectionalLight(0xffffff, 1.7);
  key.position.set(0.9, 1.6, 2.4);
  const fill = new DirectionalLight(0xffffff, 0.7);
  fill.position.set(-1.8, 1.0, 1.2);
  scene.add(key, fill);
  return scene;
}

/** The shared light rig: warm key (lamp), soft fill (room), amber rim (window). Objects get a gentler rig. */
function stage(objects = false) {
  const scene = new Scene();
  scene.environment = env;
  scene.environmentIntensity = objects ? 0.5 : 0.75;
  scene.add(new HemisphereLight(0xfff1e0, 0x6b5440, objects ? 0.3 : 0.35));
  const key = new DirectionalLight(0xffe3c2, objects ? 1.55 : 2.1);
  key.position.set(1.2, 1.8, 2.2);
  const rim = new DirectionalLight(0xffb36b, objects ? 1.1 : 1.6);
  rim.position.set(-1.6, 1.4, -1.4);
  scene.add(key, rim);
  return scene;
}

function serial<T>(job: () => Promise<T>): Promise<T> {
  const next = queue.then(job, job);
  queue = next.catch(() => undefined);
  return next;
}

async function capture(scene: Scene, camera: PerspectiveCamera, size: number) {
  const r = await setup();
  r.setSize(size, size, false);
  r.setClearColor(new Color('#000000'), 0);
  r.render(scene, camera);
  return r.domElement.toDataURL('image/webp', 0.92);
}

export function renderPortrait(def: AvatarDefinition, size = 256): Promise<string> {
  const key = `${definitionKey(def)}|${size}`;
  const hit = memo.get(key) ?? (stored()[key] ? Promise.resolve(stored()[key]) : null);
  if (hit) return hit;
  const job = serial(async () => {
    await setup();
    const kit = await loadCharacterKit();
    const scene = stage();
    const model = new CharacterModel(kit, def, 3);
    model.breathing = false;
    model.setHead(new Quaternion().setFromAxisAngle(new Vector3(0, 1, 0), 0.1));
    model.setHand(0, new Vector3(-0.12, 0.12, 0.26));
    model.setHand(1, new Vector3(0.12, 0.12, 0.26));
    scene.add(model.root);
    model.update(1, 2);
    // Head-and-shoulders framing: from the upper chest to just above the hair, slightly off-axis.
    const pivot = model.neck.getWorldPosition(new Vector3());
    const center = new Vector3(pivot.x, pivot.y + 0.1, pivot.z + 0.02);
    const camera = new PerspectiveCamera(24, 1, 0.05, 10);
    camera.position.set(center.x + 0.16, center.y + 0.03, center.z + 1.12);
    camera.lookAt(center);
    model.gazeTarget = camera.position.clone();
    model.update(1, 2);
    model.update(1, 2);
    const url = await capture(scene, camera, size);
    model.dispose();
    remember(key, url);
    return url;
  });
  memo.set(key, job);
  return job;
}

export function renderKeepsake(kind: string, progress: number, target: number, earned: boolean, width = 480, height = 300): Promise<string> {
  const key = `keepsake-v2|${kind}|${progress}|${target}|${earned}|${width}x${height}`;
  const hit = memo.get(key);
  if (hit) return hit;
  const job = serial(async () => {
    const r = await setup();
    const scene = stage(true);
    const object = buildKeepsake(kind, progress, target, earned);
    const holder = new Group();
    holder.add(object);
    holder.rotation.y = kind === 'linked_sculptures' ? -0.25 : -0.4;
    scene.add(holder);
    await texturesReady(); // render only once the walnut, glaze and print textures are in
    const box = new Box3().setFromObject(holder);
    const center = box.getCenter(new Vector3());
    const extent = box.getSize(new Vector3()).length();
    const camera = new PerspectiveCamera(30, width / height, 0.01, 10);
    camera.position.set(center.x + extent * 0.35, center.y + extent * 0.45, center.z + extent * 1.65);
    camera.lookAt(center.x, center.y - extent * 0.04, center.z);
    r.setSize(width, height, false);
    r.setClearColor(new Color('#000000'), 0);
    r.render(scene, camera);
    const url = r.domElement.toDataURL('image/webp', 0.92);
    object.traverse((o) => (o as import('three').Mesh).geometry?.dispose?.());
    return url;
  });
  memo.set(key, job);
  return job;
}

// ---------------------------------------------------------------------------
// Photo-derived avatars (owner-approved models)
// ---------------------------------------------------------------------------

/** The signed-in viewer, so cached renders of other people's likenesses never cross accounts. */
let viewerKey = 'anon';
export function setPortraitViewer(id: string | null) {
  if ((id ?? 'anon') !== viewerKey) purgeAvatarCaches();
  viewerKey = id ?? 'anon';
}

/** Forget every rendered likeness and cached model (sign-out, revocation, replacement). */
export function purgeAvatarCaches() {
  for (const key of [...memo.keys()]) if (key.startsWith('photo|')) memo.delete(key);
  try {
    const all = stored();
    for (const key of Object.keys(all)) if (key.startsWith('photo|')) delete all[key];
    sessionStorage.setItem(STORE, JSON.stringify(all));
  } catch {
    /* ignore */
  }
  clearAvatarModels();
}

const modelKey = (a: AvatarView) => `${viewerKey}|${a.assetId}|${a.assetVersion ?? 0}`;

async function posedFigure(avatar: AvatarView) {
  const held = await acquireAvatarModel(modelKey(avatar), avatar.modelUrl!);
  const fig = new SkinnedAvatar(held.gltf, 3);
  fig.breathing = false;
  fig.blinking = false;
  fig.setHand(0, new Vector3(-0.17, 0.14, 0.3));
  fig.setHand(1, new Vector3(0.17, 0.14, 0.3));
  return { fig, release: held.release };
}

/** A person's portrait from the avatar they approved (or the labelled demo preset when that's what they use). */
export function renderAvatarPortrait(avatar: AvatarView | null | undefined, fallback: AvatarDefinition, size = 256): Promise<string> {
  if (!avatar || avatar.kind !== 'photo_derived' || !avatar.modelUrl || !avatar.assetId) return renderPortrait(avatar?.definition ?? fallback, size);
  const key = `photo|${modelKey(avatar)}|${size}`;
  const hit = memo.get(key) ?? (stored()[key] ? Promise.resolve(stored()[key]) : null);
  if (hit) return hit;
  const job = serial(async () => {
    await setup();
    const scene = stage();
    const { fig, release } = await posedFigure(avatar);
    try {
      scene.add(fig.root);
      fig.setHead(new Quaternion().setFromAxisAngle(new Vector3(0, 1, 0), 0.08));
      fig.update(1, 2);
      const center = fig.headWorldPosition(new Vector3());
      const camera = new PerspectiveCamera(24, 1, 0.05, 20);
      camera.position.set(center.x + 0.2, center.y + 0.02, center.z + 1.45);
      camera.lookAt(center.x, center.y - 0.06, center.z);
      fig.gazeTarget = fig.root.worldToLocal(camera.position.clone());
      fig.update(1, 2);
      const url = await capture(scene, camera, size);
      remember(key, url);
      return url;
    } finally {
      fig.dispose();
      release();
    }
  });
  memo.set(key, job);
  job.catch(() => memo.delete(key));
  return job;
}

export interface ReviewViews {
  front: string;
  threeQuarter: string;
  side: string;
  seated: string;
}

/** Owner likeness review: front, three-quarter, side and seated views under neutral light. Never cached. */
export function renderReviewViews(avatar: AvatarView, size = 360): Promise<ReviewViews> {
  return serial(async () => {
    await setup();
    const scene = neutralStage();
    const { fig, release } = await posedFigure(avatar);
    try {
      scene.add(fig.root);
      fig.update(1, 2);
      const head = fig.headWorldPosition(new Vector3());
      const shot = async (angle: number, dist: number, height: number, look: Vector3) => {
        const camera = new PerspectiveCamera(26, 1, 0.05, 20);
        camera.position.set(head.x + Math.sin(angle) * dist, head.y + height, head.z + Math.cos(angle) * dist);
        camera.lookAt(look);
        fig.gazeTarget = fig.root.worldToLocal(camera.position.clone());
        fig.setHead(new Quaternion());
        fig.update(1, 2);
        return capture(scene, camera, size);
      };
      const face = new Vector3(head.x, head.y - 0.04, head.z);
      const front = await shot(0, 0.95, 0, face);
      const threeQuarter = await shot(0.62, 0.95, 0, face);
      const side = await shot(Math.PI / 2, 0.95, 0, face);
      const body = new Vector3(head.x, head.y - 0.45, head.z + 0.1);
      const seated = await shot(0.35, 2.6, -0.1, body);
      return { front, threeQuarter, side, seated };
    } finally {
      fig.dispose();
      release();
    }
  });
}

/**
 * Interactive likeness viewer for the owner (drag, arrow keys or wheel to turn and zoom). Uses its own small
 * renderer bound to `canvas`; call the returned function to dispose everything.
 */
export async function mountAvatarViewer(canvas: HTMLCanvasElement, avatar: AvatarView): Promise<() => void> {
  await setup();
  const r = new WebGLRenderer({ canvas, antialias: true, alpha: true });
  r.outputColorSpace = SRGBColorSpace;
  r.toneMapping = NeutralToneMapping;
  r.toneMappingExposure = 1.05;
  r.setPixelRatio(Math.min(2, window.devicePixelRatio || 1));
  const scene = neutralStage();
  const { fig, release } = await posedFigure(avatar);
  const turntable = new Group();
  turntable.add(fig.root);
  scene.add(turntable);
  fig.update(1, 2);
  const head = fig.headWorldPosition(new Vector3());
  const camera = new PerspectiveCamera(28, 1, 0.05, 20);
  let yaw = 0;
  let pitch = 0.05;
  let zoom = 1.1;
  let raf = 0;
  const render = () => {
    raf = 0;
    const w = canvas.clientWidth || 320;
    const h = canvas.clientHeight || 320;
    r.setSize(w, h, false);
    camera.aspect = w / h;
    camera.updateProjectionMatrix();
    turntable.rotation.y = yaw;
    camera.position.set(head.x, head.y - 0.1 + Math.sin(pitch) * zoom, head.z + Math.cos(pitch) * zoom);
    camera.lookAt(head.x, head.y - 0.12, head.z);
    fig.update(1 / 60, 2);
    r.render(scene, camera);
  };
  const request = () => { if (!raf) raf = requestAnimationFrame(render); };
  let drag: { x: number; y: number } | null = null;
  const down = (e: PointerEvent) => { drag = { x: e.clientX, y: e.clientY }; canvas.setPointerCapture(e.pointerId); };
  const move = (e: PointerEvent) => {
    if (!drag) return;
    yaw += (e.clientX - drag.x) * 0.01;
    pitch = Math.max(-0.3, Math.min(0.6, pitch + (e.clientY - drag.y) * 0.005));
    drag = { x: e.clientX, y: e.clientY };
    request();
  };
  const up = () => { drag = null; };
  const wheel = (e: WheelEvent) => { e.preventDefault(); zoom = Math.max(0.6, Math.min(3, zoom * (e.deltaY > 0 ? 1.08 : 0.92))); request(); };
  const key = (e: KeyboardEvent) => {
    if (e.key === 'ArrowLeft') yaw -= 0.2;
    else if (e.key === 'ArrowRight') yaw += 0.2;
    else if (e.key === '+' || e.key === '=') zoom = Math.max(0.6, zoom * 0.9);
    else if (e.key === '-') zoom = Math.min(3, zoom * 1.1);
    else return;
    e.preventDefault();
    request();
  };
  canvas.addEventListener('pointerdown', down);
  canvas.addEventListener('pointermove', move);
  canvas.addEventListener('pointerup', up);
  canvas.addEventListener('wheel', wheel, { passive: false });
  canvas.addEventListener('keydown', key);
  const ro = new ResizeObserver(request);
  ro.observe(canvas);
  request();
  return () => {
    if (raf) cancelAnimationFrame(raf);
    ro.disconnect();
    canvas.removeEventListener('pointerdown', down);
    canvas.removeEventListener('pointermove', move);
    canvas.removeEventListener('pointerup', up);
    canvas.removeEventListener('wheel', wheel);
    canvas.removeEventListener('keydown', key);
    fig.dispose();
    release();
    r.dispose();
  };
}
