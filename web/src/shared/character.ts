// Runtime for SameSky's sculpted character kit (public/assets/models/avatars/characters.glb).
// One implementation is shared by the XR room (src/xr/avatar3d.ts) and the companion's portrait
// renderer (src/shared/portrait.ts), so a person's portrait is always the same 3D character they appear
// as at the table.
//
// Rig: pelvis (root, sits on the seat) > chest > neck pivot > head; chest > shoulder > upper arm > elbow >
// forearm > wrist > hand. Arms use analytic two-bone IK toward hand targets, so hands never detach.

import {
  BufferAttribute, BufferGeometry, CanvasTexture, Color, Group, LoadingManager, Matrix4, Mesh, MeshBasicMaterial, MeshPhysicalMaterial,
  MeshStandardMaterial, Object3D, Quaternion, RepeatWrapping, SphereGeometry, SRGBColorSpace, Texture, TextureLoader,
  TorusGeometry, Vector3, CylinderGeometry,
} from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { MeshoptDecoder } from 'three/examples/jsm/libs/meshopt_decoder.module.js';
import type { AvatarDefinition } from './contracts';

export const KIT_URL = '/assets/models/avatars/characters.glb';
export const KIT_VERSION = 'kit-3';

export interface KitExtras {
  neckPivot: [number, number, number];
  shoulders: [number, number, number][];
  upperArm: number;
  forearm: number;
  eyes: Record<string, { centers: [number, number, number][]; radius: number }>;
  earrings: [number, number, number][];
  glasses: { y: number; z: number; lens: number };
  labelHeight: number;
  seatToEye: number;
}

export interface CharacterKit {
  geometries: Map<string, BufferGeometry>;
  extras: KitExtras;
  knitNormal: Texture;
}

let kitPromise: Promise<CharacterKit> | null = null;

export function loadCharacterKit(manager?: LoadingManager): Promise<CharacterKit> {
  if (!kitPromise) {
    kitPromise = (async () => {
      const loader = new GLTFLoader(manager);
      loader.setMeshoptDecoder(MeshoptDecoder);
      const gltf = await loader.loadAsync(KIT_URL);
      const geometries = new Map<string, BufferGeometry>();
      gltf.scene.updateMatrixWorld(true);
      gltf.scene.traverse((o) => {
        const m = o as Mesh;
        if (m.isMesh) geometries.set(m.name || m.parent?.name || '', bakeToFloat(m));
      });
      const extras = (gltf.parser.json.asset?.extras ?? {}) as KitExtras;
      const knitNormal = await new TextureLoader(manager).loadAsync('/assets/textures/knit/normal.webp');
      knitNormal.wrapS = knitNormal.wrapT = RepeatWrapping;
      knitNormal.repeat.set(3, 3);
      return { geometries, extras, knitNormal };
    })();
    kitPromise.catch(() => {
      kitPromise = null; // allow a retry after a network error
    });
  }
  return kitPromise;
}

/**
 * meshopt compression stores quantized (normalized integer) attributes and moves the de-quantization into
 * the node transform. Bake that transform into a plain Float32 geometry so parts can be re-parented freely.
 */
function bakeToFloat(mesh: Mesh): BufferGeometry {
  const src = mesh.geometry;
  const out = new BufferGeometry();
  for (const name of ['position', 'normal', 'uv', 'color'] as const) {
    const attr = src.getAttribute(name);
    if (!attr) continue;
    const size = attr.itemSize;
    const arr = new Float32Array(attr.count * size);
    for (let i = 0; i < attr.count; i++) {
      arr[i * size] = attr.getX(i);
      if (size > 1) arr[i * size + 1] = attr.getY(i);
      if (size > 2) arr[i * size + 2] = attr.getZ(i);
      if (size > 3) arr[i * size + 3] = attr.getW(i);
    }
    out.setAttribute(name, new BufferAttribute(arr, size));
  }
  if (src.index) out.setIndex(new BufferAttribute(Uint32Array.from(src.index.array as ArrayLike<number>), 1));
  out.applyMatrix4(mesh.matrixWorld);
  out.computeBoundingSphere();
  return out;
}

/** Supported options. Older saved definitions map onto these (see normalizeDefinition). */
export const CHARACTER_OPTIONS = {
  face: ['soft', 'defined'] as const,
  hair: ['short', 'wavy', 'long', 'bun', 'curly', 'buzz', 'none'] as const,
  accessory: ['none', 'glasses', 'earrings', 'scarf', 'cap'] as const,
};

export type FacePreset = (typeof CHARACTER_OPTIONS.face)[number];

export function normalizeDefinition(def: AvatarDefinition): Required<AvatarDefinition> {
  const hair = (CHARACTER_OPTIONS.hair as readonly string[]).includes(def.hair) ? def.hair : 'short';
  const accessory = (CHARACTER_OPTIONS.accessory as readonly string[]).includes(def.accessory) ? def.accessory : 'none';
  const face = def.face === 'defined' || def.face === 'soft' ? def.face : 'soft';
  return { skin: def.skin || '#e8b896', hair, hairColor: def.hairColor || '#4a2f1f', top: def.top || '#3f6b52', accessory, face };
}

export function definitionKey(def: AvatarDefinition): string {
  const d = normalizeDefinition(def);
  return [KIT_VERSION, d.face, d.skin, d.hair, d.hairColor, d.top, d.accessory].join('|');
}

const DOWN = new Vector3(0, -1, 0);
export const HEAD_SCALE = 1.18;
const IRIS = 0.7; // iris cap half-angle (radians): large, friendly irises
const _v1 = new Vector3();
const _v2 = new Vector3();
const _v3 = new Vector3();
const _v4 = new Vector3();
const _v5 = new Vector3();
const _v6 = new Vector3();
const _v7 = new Vector3();
const _x = new Vector3();
const _y = new Vector3();
const _z = new Vector3();
const _q1 = new Quaternion();
const _q2 = new Quaternion();
const _q3 = new Quaternion();
const _rootQ = new Quaternion();
const _m = new Matrix4();

function mix(a: string, b: string, t: number) {
  return new Color(a).lerp(new Color(b), t);
}

function irisTexture(): Texture {
  const c = document.createElement('canvas');
  c.width = c.height = 128;
  const ctx = c.getContext('2d')!;
  const g = ctx.createRadialGradient(64, 64, 6, 64, 64, 64);
  g.addColorStop(0, '#0d0907');
  g.addColorStop(0.32, '#0d0907');
  g.addColorStop(0.36, '#5a3a22');
  g.addColorStop(0.7, '#3b2414');
  g.addColorStop(0.92, '#23140b');
  g.addColorStop(1, '#120a06');
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, 128, 128);
  for (let i = 0; i < 48; i++) {
    const a = (i / 48) * Math.PI * 2;
    ctx.strokeStyle = `rgba(160, 110, 60, ${0.18 + (i % 3) * 0.06})`;
    ctx.beginPath();
    ctx.moveTo(64 + Math.cos(a) * 24, 64 + Math.sin(a) * 24);
    ctx.lineTo(64 + Math.cos(a) * 52, 64 + Math.sin(a) * 52);
    ctx.stroke();
  }
  const t = new CanvasTexture(c);
  t.colorSpace = SRGBColorSpace;
  return t;
}

let sharedIris: Texture | null = null;

export class CharacterModel {
  readonly root = new Group(); // pelvis on the seat; +Z is the character's forward
  readonly chest = new Group();
  readonly neck = new Group();
  readonly head = new Group();
  readonly leftWrist = new Group();
  readonly rightWrist = new Group();
  readonly labelAnchor = new Object3D();
  private readonly shoulders: Group[] = [];
  private readonly upperArms: Group[] = [];
  private readonly forearms: Group[] = [];
  private readonly hands: Group[] = [];
  private readonly lids: Group[] = [];
  private readonly eyeGroups: Group[] = [];
  private readonly materials: (MeshStandardMaterial | MeshPhysicalMaterial | MeshBasicMaterial)[] = [];
  private readonly ownedGeometries: BufferGeometry[] = [];
  private def: Required<AvatarDefinition>;
  private headTarget = new Quaternion();
  private headLean = new Vector3();
  readonly handTargets = [new Vector3(-0.16, 0.24, 0.36), new Vector3(0.16, 0.24, 0.36)];
  private readonly handOrientation = [new Quaternion(), new Quaternion()];
  private readonly handOriented = [false, false];
  /** Optional elbow pole directions (character space); null uses the relaxed outward/down/back default. */
  readonly poles: (Vector3 | null)[] = [null, null];
  private nextBlink = 2.2;
  private blinkT = -1;
  gazeTarget: Vector3 | null = null; // in root space
  breathing = true;
  blinking = true;

  constructor(private kit: CharacterKit, definition: AvatarDefinition, private seed = 1) {
    this.def = normalizeDefinition(definition);
    this.root.name = 'character';
    this.build();
  }

  private material<T extends MeshStandardMaterial | MeshPhysicalMaterial | MeshBasicMaterial>(m: T): T {
    this.materials.push(m);
    return m;
  }

  private part(name: string, mat: MeshStandardMaterial | MeshPhysicalMaterial, parent: Object3D, offset?: Vector3, mirror = false) {
    const geo = this.kit.geometries.get(name);
    if (!geo) return null;
    const mesh = new Mesh(geo, mat);
    mesh.name = name;
    if (offset) mesh.position.copy(offset).negate();
    if (mirror) mesh.scale.x = -1;
    mesh.castShadow = true;
    parent.add(mesh);
    return mesh;
  }

  private build() {
    const d = this.def;
    const ex = this.kit.extras;
    // Head skin carries sculpted shading cues (blush, socket depth) as vertex colours; other skin parts don't.
    const skinFace = this.material(new MeshPhysicalMaterial({ color: d.skin, roughness: 0.56, sheen: 0.35, sheenRoughness: 0.6, sheenColor: new Color('#ffb59e'), vertexColors: true }));
    const skin = this.material(new MeshPhysicalMaterial({ color: d.skin, roughness: 0.58, sheen: 0.3, sheenRoughness: 0.6, sheenColor: new Color('#ffb59e') }));
    const lips = this.material(new MeshPhysicalMaterial({ color: mix(d.skin, '#a8433f', 0.55), roughness: 0.4, sheen: 0.2 }));
    const brows = this.material(new MeshStandardMaterial({ color: new Color(d.hairColor).multiplyScalar(0.85), roughness: 0.8 }));
    const hair = this.material(new MeshPhysicalMaterial({ color: d.hairColor, roughness: 0.64, sheen: 0.5, sheenRoughness: 0.45, sheenColor: new Color(d.hairColor).lerp(new Color('#ffe2c0'), 0.5) }));
    const top = this.material(new MeshStandardMaterial({ color: d.top, roughness: 0.92, normalMap: this.kit.knitNormal }));
    top.normalScale.set(0.7, 0.7);
    const pants = this.material(new MeshStandardMaterial({ color: '#3a3f47', roughness: 0.86 }));
    const accent = this.material(new MeshStandardMaterial({ color: d.top === '#c98f3a' ? '#e9e0cf' : '#c98f3a', roughness: 0.85, normalMap: this.kit.knitNormal }));

    // pelvis / torso / legs
    this.root.add(this.chest);
    this.part('torso', top, this.chest);
    this.part('neck', skin, this.chest);
    this.part('legs', pants, this.root);
    if (d.accessory === 'scarf') this.part('scarf', accent, this.chest);

    // head on a neck pivot (the kit meshes are authored in head-local coordinates)
    const pivot = new Vector3(...ex.neckPivot);
    this.neck.position.copy(pivot);
    this.chest.add(this.neck);
    this.neck.add(this.head);
    this.head.scale.setScalar(HEAD_SCALE); // friendly stylized proportions (large head)
    this.part(`head_${d.face}`, skinFace, this.head);
    this.part(`lips_${d.face}`, lips, this.head)!.castShadow = false; // tiny parts inside the head's own shadow
    this.part(`brows_${d.face}`, brows, this.head)!.castShadow = false;
    if (d.hair !== 'none') this.part(`hair_${d.hair}`, hair, this.head);
    if (d.accessory === 'cap') this.part('cap', accent, this.head);
    this.buildEyes(skin);
    if (d.accessory === 'glasses') this.buildGlasses();
    if (d.accessory === 'earrings') this.buildEarrings();
    this.labelAnchor.position.set(0, ex.labelHeight, 0);
    this.head.add(this.labelAnchor);

    // arms
    ex.shoulders.forEach((s, i) => {
      const mirror = i === 0;
      const shoulder = new Group();
      shoulder.position.set(...s);
      this.chest.add(shoulder);
      const upper = new Group();
      shoulder.add(upper);
      this.part('upper_arm', top, upper, undefined, mirror);
      const fore = new Group();
      fore.position.set(0, -ex.upperArm, 0);
      upper.add(fore);
      this.part('forearm', top, fore, undefined, mirror);
      const wrist = i === 0 ? this.leftWrist : this.rightWrist;
      wrist.position.set(0, -ex.forearm, 0);
      fore.add(wrist);
      const hand = new Group();
      wrist.add(hand);
      this.part('hand', skin, hand, undefined, mirror);
      this.shoulders.push(shoulder);
      this.upperArms.push(upper);
      this.forearms.push(fore);
      this.hands.push(hand);
    });
  }

  private buildEyes(skin: MeshPhysicalMaterial) {
    const eyes = this.kit.extras.eyes[this.def.face];
    const r = eyes.radius;
    sharedIris ??= irisTexture();
    const sclera = this.material(new MeshPhysicalMaterial({ color: '#f6f1ea', roughness: 0.18, clearcoat: 1, clearcoatRoughness: 0.08 }));
    const iris = this.material(new MeshPhysicalMaterial({ map: sharedIris, roughness: 0.25, clearcoat: 1, clearcoatRoughness: 0.05 }));
    const lash = this.material(new MeshStandardMaterial({ color: '#1b1411', roughness: 0.7 }));
    const glint = this.material(new MeshBasicMaterial({ color: '#ffffff' }));
    const scleraGeo = new SphereGeometry(r, 28, 18);
    const irisGeo = new SphereGeometry(r * 1.004, 28, 10, 0, Math.PI * 2, 0, IRIS);
    irisGeo.rotateX(Math.PI / 2);
    // remap UVs of the cap to a disc so the radial iris texture lands correctly
    const pos = irisGeo.attributes.position;
    const uv = irisGeo.attributes.uv;
    for (let i = 0; i < pos.count; i++) {
      const x = pos.getX(i) / (r * Math.sin(IRIS));
      const y = pos.getY(i) / (r * Math.sin(IRIS));
      uv.setXY(i, 0.5 + x * 0.5, 0.5 + y * 0.5);
    }
    const lidGeo = new SphereGeometry(r * 1.09, 28, 12, 0, Math.PI * 2, 0, Math.PI / 2);
    const lashGeo = new TorusGeometry(r * 1.095, r * 0.075, 6, 28, Math.PI);
    const glintGeo = new SphereGeometry(r * 0.13, 8, 6);
    this.ownedGeometries.push(scleraGeo, irisGeo, lidGeo, lashGeo, glintGeo);
    for (const c of eyes.centers) {
      const socket = new Group();
      socket.position.set(...c);
      this.head.add(socket);
      const eye = new Group();
      socket.add(eye);
      eye.add(new Mesh(scleraGeo, sclera), new Mesh(irisGeo, iris));
      const g = new Mesh(glintGeo, glint);
      g.position.set(r * 0.32, r * 0.36, r * 0.92);
      eye.add(g);
      this.eyeGroups.push(eye);
      // upper lid: a hemisphere hinged at the eye centre; rotation.x opens/closes it
      const lid = new Group();
      const lidMesh = new Mesh(lidGeo, skin);
      const lashMesh = new Mesh(lashGeo, lash);
      lashMesh.rotation.set(Math.PI / 2, 0, 0); // arc along the lid's front edge
      lid.add(lidMesh, lashMesh);
      socket.add(lid);
      this.lids.push(lid);
    }
    this.setLids(0);
  }

  private setLids(closed: number) {
    // The upper lid is a hemisphere hinged at the eye centre: -0.6 rad rests it over the top of the eye,
    // +1.62 rad rolls it down over the front (closed).
    const angle = -0.64 + closed * 2.26;
    for (const lid of this.lids) lid.rotation.x = angle;
  }

  private buildGlasses() {
    const g = this.kit.extras.glasses;
    const eyes = this.kit.extras.eyes[this.def.face];
    const frame = this.material(new MeshStandardMaterial({ color: '#2a2622', roughness: 0.35, metalness: 0.2 }));
    const ringGeo = new TorusGeometry(g.lens, 0.0022, 8, 32);
    const bridgeGeo = new CylinderGeometry(0.0016, 0.0016, Math.abs(eyes.centers[1][0] - eyes.centers[0][0]) - g.lens * 2, 6);
    const armGeo = new CylinderGeometry(0.0015, 0.0015, 0.1, 6);
    this.ownedGeometries.push(ringGeo, bridgeGeo, armGeo);
    for (const c of eyes.centers) {
      const ring = new Mesh(ringGeo, frame);
      ring.position.set(c[0], g.y, g.z);
      this.head.add(ring);
      const arm = new Mesh(armGeo, frame);
      arm.rotation.x = Math.PI / 2;
      arm.position.set(c[0] + Math.sign(c[0]) * g.lens, g.y + 0.004, g.z - 0.05);
      this.head.add(arm);
    }
    const bridge = new Mesh(bridgeGeo, frame);
    bridge.rotation.z = Math.PI / 2;
    bridge.position.set(0, g.y + 0.004, g.z + 0.001);
    this.head.add(bridge);
  }

  private buildEarrings() {
    const gold = this.material(new MeshPhysicalMaterial({ color: '#d8b24a', metalness: 1, roughness: 0.25 }));
    const geo = new TorusGeometry(0.0065, 0.0016, 8, 18);
    this.ownedGeometries.push(geo);
    for (const p of this.kit.extras.earrings) {
      const ring = new Mesh(geo, gold);
      ring.position.set(...p);
      ring.rotation.y = Math.PI / 2;
      this.head.add(ring);
    }
  }

  /** Head orientation (character-root space). Position offsets lean the chest toward the target. */
  setHead(quaternion: Quaternion, leanOffset?: Vector3) {
    this.headTarget.copy(quaternion);
    if (leanOffset) this.headLean.copy(leanOffset);
  }

  /** Hand target (wrist) in character-root space; optional orientation, else a relaxed palm-down pose. */
  setHand(index: 0 | 1, target: Vector3, orientation?: Quaternion | null) {
    this.handTargets[index].copy(target);
    this.handOriented[index] = !!orientation;
    if (orientation) this.handOrientation[index].copy(orientation);
  }

  /** Elbow pole for one arm (character space), e.g. elbow forward and down for a chin-on-hand rest. */
  setPole(index: 0 | 1, pole: Vector3 | null) {
    if (!pole) this.poles[index] = null;
    else (this.poles[index] ??= new Vector3()).copy(pole);
  }

  /** Per-frame update. Allocation-free: everything below uses module-level scratch objects. */
  update(dt: number, t: number) {
    // breathing (authored idle motion, separate from tracking)
    if (this.breathing) {
      const b = Math.sin(t * 1.6 + this.seed) * 0.5 + 0.5;
      this.chest.scale.set(1 + b * 0.006, 1 + b * 0.008, 1 + b * 0.012);
    }
    // lean: rotate the chest (limited) so the neck moves toward the requested head offset
    const leanX = Math.max(-0.25, Math.min(0.25, this.headLean.z * 1.6));
    const leanZ = Math.max(-0.2, Math.min(0.2, -this.headLean.x * 1.6));
    this.chest.rotation.x += (leanX - this.chest.rotation.x) * Math.min(1, dt * 6);
    this.chest.rotation.z += (leanZ - this.chest.rotation.z) * Math.min(1, dt * 6);
    // head: target is in root space; express it relative to the chest
    this.root.getWorldQuaternion(_rootQ);
    this.chest.getWorldQuaternion(_q1);
    _q2.copy(_rootQ).invert().multiply(_q1).invert().multiply(this.headTarget);
    this.neck.quaternion.slerp(_q2, Math.min(1, dt * 10));
    // blinks
    if (this.blinking) {
      this.nextBlink -= dt;
      if (this.nextBlink <= 0 && this.blinkT < 0) this.blinkT = 0;
      if (this.blinkT >= 0) {
        this.blinkT += dt;
        const k = this.blinkT < 0.07 ? this.blinkT / 0.07 : Math.max(0, 1 - (this.blinkT - 0.07) / 0.09);
        this.setLids(k);
        if (this.blinkT > 0.16) {
          this.blinkT = -1;
          this.nextBlink = 2.5 + ((Math.sin(t * 12.9898 + this.seed) * 43758.5453) % 1 + 1) % 1 * 3.5;
          this.setLids(0);
        }
      }
    } else if (this.blinkT >= 0) {
      this.blinkT = -1;
      this.setLids(0);
    }
    // gaze: eyes rotate slightly toward a target
    if (this.gazeTarget) {
      for (const eye of this.eyeGroups) {
        eye.parent!.updateWorldMatrix(true, false);
        const local2 = eye.parent!.worldToLocal(this.root.localToWorld(_v1.copy(this.gazeTarget)));
        const yaw = Math.max(-0.35, Math.min(0.35, Math.atan2(local2.x, local2.z)));
        const pitch = Math.max(-0.25, Math.min(0.25, -Math.atan2(local2.y, Math.hypot(local2.x, local2.z))));
        eye.rotation.set(pitch, yaw, 0);
      }
    }
    this.solveArms();
  }

  private solveArms() {
    const ex = this.kit.extras;
    this.root.updateWorldMatrix(true, true);
    this.root.getWorldQuaternion(_rootQ);
    const L1 = ex.upperArm;
    const L2 = ex.forearm;
    for (let i = 0; i < 2; i++) {
      const shoulder = this.shoulders[i];
      const upper = this.upperArms[i];
      const fore = this.forearms[i];
      const hand = this.hands[i];
      // target in shoulder space
      const target = shoulder.worldToLocal(this.root.localToWorld(_v1.copy(this.handTargets[i])));
      let d = target.length();
      d = Math.max(Math.abs(L1 - L2) + 1e-3, Math.min(L1 + L2 - 1e-3, d));
      const dir = _v2.copy(target).normalize();
      const a = (L1 * L1 - L2 * L2 + d * d) / (2 * d);
      const h = Math.sqrt(Math.max(0, L1 * L1 - a * a));
      const side = i === 0 ? -1 : 1;
      const custom = this.poles[i];
      const pole = custom ? _v3.copy(custom) : _v3.set(side * 0.55, -0.6, -0.45);
      pole.addScaledVector(dir, -pole.dot(dir)).normalize();
      const elbow = _v4.copy(dir).multiplyScalar(a).addScaledVector(pole, h);
      upper.quaternion.setFromUnitVectors(DOWN, _v5.copy(elbow).normalize());
      // forearm direction expressed in the upper-arm frame
      const foreDir = _v5.copy(dir).multiplyScalar(d).sub(elbow).normalize().applyQuaternion(_q1.copy(upper.quaternion).invert());
      fore.quaternion.setFromUnitVectors(DOWN, foreDir);
      // hand orientation
      upper.updateWorldMatrix(true, true);
      const wristObj = i === 0 ? this.leftWrist : this.rightWrist;
      wristObj.getWorldQuaternion(_q2);
      if (this.handOriented[i]) {
        _q3.copy(_rootQ).multiply(this.handOrientation[i]);
      } else {
        // relaxed: fingers forward (+Z of the character) and slightly inward, palm down
        const fingers = _v6.set(-side * 0.18, -0.12, 1).normalize().applyQuaternion(_rootQ);
        const back = _v7.set(0, 1, 0).applyQuaternion(_rootQ); // back of the hand faces up
        _y.copy(fingers).negate();
        _z.copy(back).addScaledVector(_y, -back.dot(_y)).normalize();
        _x.crossVectors(_y, _z);
        _q3.setFromRotationMatrix(_m.makeBasis(_x, _y, _z));
      }
      hand.quaternion.copy(_q2.invert().multiply(_q3));
    }
  }

  /** Bounding helpers for framing portraits. */
  headWorldPosition(target = new Vector3()) {
    return this.head.getWorldPosition(target).add(new Vector3(0, 0.11, 0).applyQuaternion(this.head.getWorldQuaternion(new Quaternion())));
  }

  dispose() {
    for (const m of this.materials) m.dispose();
    for (const g of this.ownedGeometries) g.dispose();
    this.root.removeFromParent();
  }
}
