// Offscreen renders of the same 3D assets used in the room: character portraits (from the sculpted
// character kit) and keepsake product shots (from keepsakes3d.ts). Loaded lazily by the companion, so the
// companion's main bundle stays free of three.js. One renderer, one light rig, one camera language.

import {
  Box3, Color, DirectionalLight, Group, HemisphereLight, NeutralToneMapping, PerspectiveCamera, PMREMGenerator, Quaternion,
  Scene, SRGBColorSpace, Vector3, WebGLRenderer, type Texture,
} from 'three';
import { HDRLoader } from 'three/examples/jsm/loaders/HDRLoader.js';
import type { AvatarDefinition } from './contracts';
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
