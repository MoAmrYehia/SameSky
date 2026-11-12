// Photo-derived avatars: loading, caching and posing an owner-approved skinned human model.
//
// - The model is fetched from a short-lived, viewer-bound URL and parsed with GLTFLoader (+ meshopt).
// - Parsed models are cached per viewer + asset version and reference-counted: geometries, materials and
//   textures belong to the cache entry and are disposed only when its last user releases it (never shared
//   without ownership). Each participant gets its own skeleton (SkeletonUtils.clone), so poses are isolated.
// - The rig adapter never bakes or rescales geometry: skinned meshes, skin weights, bind matrices and morph
//   targets stay intact. It normalizes units (cm exports) and facing with a uniform wrapper transform,
//   finds the humanoid bones by name (mirroring api/samesky/services/glb.py), seats the legs, solves the arms
//   with bounded two-bone IK on the real bones, turns the neck/head, and blinks only when the model has
//   eye-blink morph targets. Nothing here invents facial expressions or measured face tracking.

import { type Bone, Group, type Material, type Mesh, type Object3D, Quaternion, type SkinnedMesh, type Texture, Vector3 } from 'three';
import { type GLTF, GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { MeshoptDecoder } from 'three/examples/jsm/libs/meshopt_decoder.module.js';
import { clone as cloneWithSkeleton } from 'three/examples/jsm/utils/SkeletonUtils.js';
import type { Figure } from './figure';

export type Role =
  | 'hips' | 'spine' | 'chest' | 'neck' | 'head' | 'leftEye' | 'rightEye'
  | 'leftShoulder' | 'leftUpperArm' | 'leftLowerArm' | 'leftHand'
  | 'rightShoulder' | 'rightUpperArm' | 'rightLowerArm' | 'rightHand'
  | 'leftUpperLeg' | 'leftLowerLeg' | 'leftFoot' | 'rightUpperLeg' | 'rightLowerLeg' | 'rightFoot';

const PREFIX = /^(mixamorig\d*[:_]?|armature[|:_]?|j_bip_[clr]_|bip0?1[ _]?|def[-_]|cc_base_|rig[:_])/i;
const norm = (name: string) => name.trim().replace(PREFIX, '').replace(/[\s.-]/g, '_').toLowerCase();
const sideOf = (n: string): 'left' | 'right' | null => {
  if (/(^|_)(left|l)(_|$)|^left|left$|_l$|^l_/.test(n)) return 'left';
  if (/(^|_)(right|r)(_|$)|^right|right$|_r$|^r_/.test(n)) return 'right';
  return null;
};
const stripSide = (n: string) => n.replace(/(^|_)(left|right)/g, '$1').replace(/(^|_)[lr](_|$)/g, '$1').replace(/(left|right)$/, '').replace(/^_+|_+$/g, '');

/** Map joint names to the bones the seated rig drives (same rules as the server's validator). */
export function mapHumanoid(names: string[]): Partial<Record<Role, string>> {
  const roles: Partial<Record<Role, string>> = {};
  const spine: string[] = [];
  for (const raw of names) {
    const lowered = raw.trim().toLowerCase();
    const vrm = /^j_bip_l_/.test(lowered) ? 'left' : /^j_bip_r_/.test(lowered) ? 'right' : null;
    const n = norm(raw);
    const side = vrm ?? sideOf(n);
    const core = side ? stripSide(n) : n;
    let role: Role | null = null;
    if (!side) {
      if (['hips', 'pelvis', 'root_hips', 'hip'].includes(core)) role = 'hips';
      else if (/^neck(_?0?1)?$/.test(core)) role = 'neck';
      else if (core === 'head') role = 'head';
      else if (/^(spine(_?0?\d)?|chest|upper_?chest)$/.test(core)) spine.push(raw);
    } else {
      const p = side;
      if (/^(upper_?arm|arm|up_?arm)$/.test(core)) role = `${p}UpperArm` as Role;
      else if (/^(fore_?arm|lower_?arm|elbow)$/.test(core)) role = `${p}LowerArm` as Role;
      else if (/^(hand|wrist)$/.test(core)) role = `${p}Hand` as Role;
      else if (/^(up_?leg|upper_?leg|thigh)$/.test(core)) role = `${p}UpperLeg` as Role;
      else if (/^(leg|lower_?leg|calf|shin|knee)$/.test(core)) role = `${p}LowerLeg` as Role;
      else if (/^(foot|ankle)$/.test(core)) role = `${p}Foot` as Role;
      else if (/^(shoulder|clavicle)$/.test(core)) role = `${p}Shoulder` as Role;
      else if (core === 'eye') role = `${p}Eye` as Role;
    }
    if (role && !roles[role]) roles[role] = raw;
  }
  if (spine.length) {
    roles.spine = spine[0];
    if (spine.length > 1) roles.chest = spine[spine.length - 1];
  }
  return roles;
}

// ---------------------------------------------------------------------------
// Reference-counted model cache
// ---------------------------------------------------------------------------

interface Entry {
  promise: Promise<GLTF>;
  refs: number;
  gltf?: GLTF;
}
const cache = new Map<string, Entry>();

function disposeGltf(gltf: GLTF) {
  const textures = new Set<Texture>();
  const materials = new Set<Material>();
  gltf.scene.traverse((o) => {
    const m = o as Mesh;
    m.geometry?.dispose();
    const list = Array.isArray(m.material) ? m.material : m.material ? [m.material] : [];
    for (const mat of list) {
      materials.add(mat);
      for (const value of Object.values(mat)) if ((value as Texture)?.isTexture) textures.add(value as Texture);
    }
  });
  textures.forEach((t) => t.dispose());
  materials.forEach((m) => m.dispose());
}

/**
 * Load (once) and hold a parsed model. `key` must identify viewer + asset + version so a replaced or
 * revoked version is never served from cache. Call `release()` when done with every clone made from it.
 */
export async function acquireAvatarModel(key: string, url: string): Promise<{ gltf: GLTF; release: () => void }> {
  let entry = cache.get(key);
  if (!entry) {
    const loader = new GLTFLoader();
    loader.setMeshoptDecoder(MeshoptDecoder);
    const promise = (async () => {
      const res = await fetch(url, { credentials: 'omit' });
      if (!res.ok) throw new Error(res.status === 404 ? 'avatar_unavailable' : `avatar_http_${res.status}`);
      const data = await res.arrayBuffer();
      return await loader.parseAsync(data, '');
    })();
    entry = { promise, refs: 0 };
    cache.set(key, entry);
    promise.then((g) => { if (entry) entry.gltf = g; }).catch(() => cache.delete(key));
  }
  entry.refs += 1;
  const gltf = await entry.promise;
  let released = false;
  return {
    gltf,
    release: () => {
      if (released) return;
      released = true;
      const e = cache.get(key);
      if (!e) return;
      e.refs -= 1;
      if (e.refs <= 0) {
        cache.delete(key);
        if (e.gltf) disposeGltf(e.gltf);
      }
    },
  };
}

/** Drop every cached model (sign-out, revocation, switching accounts). In-use clones keep working until released. */
export function clearAvatarModels() {
  for (const [key, entry] of cache) {
    if (entry.refs <= 0 && entry.gltf) disposeGltf(entry.gltf);
    cache.delete(key);
  }
}

// ---------------------------------------------------------------------------
// The seated rig
// ---------------------------------------------------------------------------

const UP = new Vector3(0, 1, 0);
const HIP_ABOVE_SEAT = 0.09; // seated hip-joint height above the cushion (adult, metres)
const _a = new Vector3();
const _b = new Vector3();
const _c = new Vector3();
const _d = new Vector3();
const _q = new Quaternion();
const _pq = new Quaternion();
const _rq = new Quaternion();
const _dq = new Quaternion();
const _lq = new Quaternion();
const _id = new Quaternion();

interface ArmChain {
  upper: Bone;
  lower: Bone;
  hand: Bone;
  upperLen: number;
  lowerLen: number;
  side: -1 | 1; // -1: x < 0 side of character space (index 0), 1: x > 0 (index 1)
}

export class SkinnedAvatar implements Figure {
  readonly kind = 'photo_derived' as const;
  readonly displayScale = 1;
  readonly root = new Group();
  readonly head: Object3D;
  readonly leftWrist: Object3D;
  readonly rightWrist: Object3D;
  readonly eyeHeight: number;
  gazeTarget: Vector3 | null = null;
  breathing = true;
  blinking = true;
  readonly hasBlink: boolean;
  private readonly holder = new Group();
  private readonly model: Object3D;
  private readonly bones: Partial<Record<Role, Bone>> = {};
  private readonly restLocal = new Map<Bone, Quaternion>();
  private readonly childDir = new Map<Bone, Vector3>();
  private readonly arms: [ArmChain, ArmChain];
  private readonly headRestRoot = new Quaternion();
  private readonly neckRestRoot = new Quaternion();
  private readonly eyeFwd = new Map<Bone, Vector3>();
  private readonly headTarget = new Quaternion();
  private readonly lean = new Vector3();
  private readonly handTargets = [new Vector3(), new Vector3()];
  private readonly poles: (Vector3 | null)[] = [null, null];
  private readonly blinkTargets: { mesh: Mesh; left: number; right: number }[] = [];
  private readonly skinned: SkinnedMesh[] = [];
  private readonly headSize: number;
  private nextBlink: number;
  private blinkT = -1;

  constructor(template: GLTF, private seed = 1) {
    this.model = cloneWithSkeleton(template.scene);
    this.root.name = 'photo-avatar';
    this.holder.add(this.model);
    this.root.add(this.holder);
    this.nextBlink = 2 + (seed % 3);
    const all: Bone[] = [];
    this.model.traverse((o) => {
      if ((o as Bone).isBone) all.push(o as Bone);
      const m = o as SkinnedMesh;
      if (m.isMesh) {
        m.castShadow = true;
        m.receiveShadow = true;
        m.frustumCulled = false; // skinned bounds don't follow the pose
        if (m.isSkinnedMesh) this.skinned.push(m);
        const dict = m.morphTargetDictionary;
        if (dict) {
          const left = dict.eyeBlinkLeft ?? dict.eyeBlink_L ?? dict.blink_l;
          const right = dict.eyeBlinkRight ?? dict.eyeBlink_R ?? dict.blink_r;
          if (left !== undefined && right !== undefined) this.blinkTargets.push({ mesh: m, left, right });
        }
      }
    });
    const roles = mapHumanoid(all.map((b) => b.name));
    for (const [role, name] of Object.entries(roles)) this.bones[role as Role] = all.find((b) => b.name === name);
    const need: Role[] = ['hips', 'neck', 'head', 'leftUpperArm', 'leftLowerArm', 'leftHand', 'rightUpperArm', 'rightLowerArm', 'rightHand'];
    if (need.some((r) => !this.bones[r])) throw new Error('not_humanoid');
    this.hasBlink = this.blinkTargets.length > 0;

    // facing and units, from the bind pose in world space (whatever the exporter's armature transform)
    this.root.updateMatrixWorld(true);
    const L = this.bones.leftUpperArm!.getWorldPosition(new Vector3());
    const R = this.bones.rightUpperArm!.getWorldPosition(new Vector3());
    const across = L.clone().sub(R).setY(0);
    const forward = new Vector3().crossVectors(across, UP).normalize();
    this.holder.rotation.y = -Math.atan2(forward.x, forward.z);
    const headY = this.bones.head!.getWorldPosition(new Vector3()).y;
    const feet = [this.bones.leftFoot, this.bones.rightFoot].filter(Boolean).map((f) => f!.getWorldPosition(new Vector3()).y);
    const floor = feet.length ? Math.min(...feet) - 0.08 : this.bones.hips!.getWorldPosition(new Vector3()).y - 0.95;
    const height = headY - floor;
    this.holder.scale.setScalar(height > 10 ? 0.01 : 1); // centimetre exports
    this.root.updateMatrixWorld(true);
    const hips = this.bones.hips!.getWorldPosition(new Vector3());
    this.holder.position.set(-hips.x, HIP_ABOVE_SEAT - hips.y, 0.02 - hips.z);
    this.root.updateMatrixWorld(true);

    for (const bone of all) this.restLocal.set(bone, bone.quaternion.clone());
    const chainChild = (bone: Bone | undefined, child: Bone | undefined) => {
      if (!bone || !child) return;
      this.childDir.set(bone, child.position.clone().normalize());
    };
    const b = this.bones;
    chainChild(b.leftUpperArm, b.leftLowerArm);
    chainChild(b.leftLowerArm, b.leftHand);
    chainChild(b.rightUpperArm, b.rightLowerArm);
    chainChild(b.rightLowerArm, b.rightHand);
    chainChild(b.leftUpperLeg, b.leftLowerLeg);
    chainChild(b.leftLowerLeg, b.leftFoot);
    chainChild(b.rightUpperLeg, b.rightLowerLeg);
    chainChild(b.rightLowerLeg, b.rightFoot);
    const len = (x: Bone, y: Bone) => x.getWorldPosition(new Vector3()).distanceTo(y.getWorldPosition(new Vector3()));
    const chain = (side: 'left' | 'right'): ArmChain => {
      const upper = b[`${side}UpperArm`]!;
      const lower = b[`${side}LowerArm`]!;
      const hand = b[`${side}Hand`]!;
      return { upper, lower, hand, upperLen: len(upper, lower), lowerLen: len(lower, hand), side: side === 'left' ? 1 : -1 };
    };
    // character's left is +X when facing +Z, so index 0 (x < 0) is the right arm
    this.arms = [chain('right'), chain('left')];
    this.head = b.head!;
    this.rightWrist = b.rightHand!;
    this.leftWrist = b.leftHand!;

    this.seatLegs();
    this.root.updateMatrixWorld(true);
    const rootQ = this.root.getWorldQuaternion(new Quaternion()).invert();
    this.headRestRoot.copy(rootQ).multiply(b.head!.getWorldQuaternion(new Quaternion()));
    this.neckRestRoot.copy(rootQ).multiply(b.neck!.getWorldQuaternion(new Quaternion()));
    const headPos = b.head!.getWorldPosition(new Vector3());
    const neckPos = b.neck!.getWorldPosition(new Vector3());
    this.headSize = Math.max(0.06, headPos.distanceTo(neckPos));
    this.eyeHeight = this.root.worldToLocal(headPos.clone()).y + this.headSize * 0.9;
    for (const eye of [b.leftEye, b.rightEye]) {
      if (!eye) continue;
      const fwdWorld = new Vector3(0, 0, 1).applyQuaternion(this.root.getWorldQuaternion(new Quaternion()));
      this.eyeFwd.set(eye, fwdWorld.applyQuaternion(eye.getWorldQuaternion(new Quaternion()).invert()).normalize());
    }
    // a relaxed default: hands near the lap
    this.handTargets[0].set(-0.16, 0.18, 0.3);
    this.handTargets[1].set(0.16, 0.18, 0.3);
  }

  /** Rotate `bone` (from its rest pose) so its rest direction toward its child points along `dirWorld`. */
  private aim(bone: Bone, dirWorld: Vector3) {
    const child = this.childDir.get(bone);
    const rest = this.restLocal.get(bone);
    if (!child || !rest || !bone.parent) return;
    bone.parent.getWorldQuaternion(_pq);
    _rq.copy(_pq).multiply(rest);
    _a.copy(child).applyQuaternion(_rq);
    _dq.setFromUnitVectors(_a.normalize(), _b.copy(dirWorld).normalize());
    _rq.premultiply(_dq);
    bone.quaternion.copy(_pq.invert().multiply(_rq));
    bone.updateWorldMatrix(false, true);
  }

  private seatLegs() {
    this.root.getWorldQuaternion(_lq);
    for (const side of ['left', 'right'] as const) {
      const upper = this.bones[`${side}UpperLeg`];
      const lower = this.bones[`${side}LowerLeg`];
      if (!upper || !lower) continue;
      const s = side === 'left' ? 1 : -1;
      // thighs forward along the seat (slightly apart and down), shins down to the floor
      this.aim(upper, _c.set(s * 0.1, -0.08, 1).applyQuaternion(_lq));
      this.aim(lower, _c.set(s * 0.02, -1, 0.12).applyQuaternion(_lq));
    }
  }

  chinRest(out: Vector3) {
    this.root.updateMatrixWorld(true);
    this.bones.head!.getWorldPosition(out);
    this.root.worldToLocal(out);
    const s = this.headSize / 0.1;
    return out.add(_c.set(-0.035 * s, -0.14 * s, 0.13 * s));
  }

  setHead(quaternion: Quaternion, leanOffset?: Vector3) {
    this.headTarget.copy(quaternion);
    if (leanOffset) this.lean.copy(leanOffset);
  }

  setHand(index: 0 | 1, target: Vector3) {
    this.handTargets[index].copy(target);
  }

  setPole(index: 0 | 1, pole: Vector3 | null) {
    if (!pole) this.poles[index] = null;
    else (this.poles[index] ??= new Vector3()).copy(pole);
  }

  headWorldPosition(target = new Vector3()) {
    this.bones.head!.getWorldPosition(target);
    return target.add(_c.set(0, this.headSize * 0.9, 0));
  }

  /** Per-frame update (allocation-free after construction). */
  update(dt: number, t: number) {
    const b = this.bones;
    this.root.updateMatrixWorld(true);
    this.root.getWorldQuaternion(_lq);
    // upper body: small forward/side lean toward the requested head offset, plus gentle breathing
    const breathe = this.breathing ? Math.sin(t * 1.6 + this.seed) * 0.006 : 0;
    const leanX = Math.max(-0.22, Math.min(0.22, this.lean.z * 1.6)) + breathe;
    const leanZ = Math.max(-0.18, Math.min(0.18, -this.lean.x * 1.6));
    for (const [bone, share] of [[b.spine, 0.5], [b.chest, 0.5]] as const) {
      if (!bone || !bone.parent) continue;
      bone.parent.getWorldQuaternion(_pq);
      _rq.copy(_pq).multiply(this.restLocal.get(bone)!);
      _q.setFromAxisAngle(_a.set(1, 0, 0).applyQuaternion(_lq), leanX * share);
      _dq.setFromAxisAngle(_b.set(0, 0, 1).applyQuaternion(_lq), leanZ * share);
      _rq.premultiply(_q).premultiply(_dq);
      bone.quaternion.copy(_pq.invert().multiply(_rq));
      bone.updateWorldMatrix(false, true);
    }
    // neck and head: an absolute orientation in character space, shared 35/65 between them
    const neck = b.neck!;
    neck.parent!.getWorldQuaternion(_pq);
    _q.copy(_id).slerp(this.headTarget, 0.35);
    _rq.copy(_lq).multiply(_q).multiply(this.neckRestRoot);
    neck.quaternion.copy(_pq.invert().multiply(_rq));
    neck.updateWorldMatrix(false, true);
    const head = b.head!;
    head.parent!.getWorldQuaternion(_pq);
    _rq.copy(_lq).multiply(this.headTarget).multiply(this.headRestRoot);
    head.quaternion.slerp(_pq.invert().multiply(_rq), Math.min(1, dt * 10 || 1));
    head.updateWorldMatrix(false, true);

    // arms: bounded two-bone IK on the real bones, toward character-space wrist targets
    for (let i = 0; i < 2; i++) {
      const arm = this.arms[i];
      const shoulder = arm.upper.getWorldPosition(_a);
      const target = this.root.localToWorld(_b.copy(this.handTargets[i]));
      const L1 = arm.upperLen;
      const L2 = arm.lowerLen;
      const toTarget = _c.copy(target).sub(shoulder);
      let d = toTarget.length();
      d = Math.max(Math.abs(L1 - L2) + 1e-3, Math.min(L1 + L2 - 1e-3, d));
      const dir = toTarget.normalize();
      const a = (L1 * L1 - L2 * L2 + d * d) / (2 * d);
      const h = Math.sqrt(Math.max(0, L1 * L1 - a * a));
      const custom = this.poles[i];
      const pole = custom ? _d.copy(custom) : _d.set(arm.side * 0.55, -0.6, -0.45);
      pole.applyQuaternion(_lq);
      pole.addScaledVector(dir, -pole.dot(dir)).normalize();
      // elbow = shoulder + dir*a + pole*h
      const elbowX = shoulder.x + dir.x * a + pole.x * h;
      const elbowY = shoulder.y + dir.y * a + pole.y * h;
      const elbowZ = shoulder.z + dir.z * a + pole.z * h;
      this.aim(arm.upper, _d.set(elbowX - shoulder.x, elbowY - shoulder.y, elbowZ - shoulder.z));
      const elbow = arm.lower.getWorldPosition(_a);
      this.aim(arm.lower, _d.copy(this.root.localToWorld(_b.copy(this.handTargets[i]))).sub(elbow));
    }

    // eyes: small turn toward the gaze target, only when the model has eye bones
    if (this.gazeTarget) {
      this.root.localToWorld(_b.copy(this.gazeTarget));
      for (const eye of [b.leftEye, b.rightEye]) {
        const fwd = eye ? this.eyeFwd.get(eye) : undefined;
        if (!eye || !fwd || !eye.parent) continue;
        const pos = eye.getWorldPosition(_a);
        const want = _c.copy(_b).sub(pos).normalize();
        eye.parent.getWorldQuaternion(_pq);
        _rq.copy(_pq).multiply(this.restLocal.get(eye)!);
        const restDir = _d.copy(fwd).applyQuaternion(_rq);
        const angle = restDir.angleTo(want);
        _dq.setFromUnitVectors(restDir, want);
        if (angle > 0.35) _dq.slerp(_id, 1 - 0.35 / angle); // clamp the turn
        _rq.premultiply(_dq);
        eye.quaternion.copy(_pq.invert().multiply(_rq));
      }
    }

    // blinks: only through the model's own eye-blink morph targets
    if (this.hasBlink) {
      let k = 0;
      if (this.blinking) {
        this.nextBlink -= dt;
        if (this.nextBlink <= 0 && this.blinkT < 0) this.blinkT = 0;
        if (this.blinkT >= 0) {
          this.blinkT += dt;
          k = this.blinkT < 0.07 ? this.blinkT / 0.07 : Math.max(0, 1 - (this.blinkT - 0.07) / 0.09);
          if (this.blinkT > 0.16) {
            this.blinkT = -1;
            this.nextBlink = 2.5 + ((Math.sin(t * 12.9898 + this.seed) * 43758.5453) % 1 + 1) % 1 * 3.5;
            k = 0;
          }
        }
      }
      for (const target of this.blinkTargets) {
        const inf = target.mesh.morphTargetInfluences;
        if (!inf) continue;
        inf[target.left] = k;
        inf[target.right] = k;
      }
    }
  }

  dispose() {
    for (const m of this.skinned) m.skeleton?.dispose();
    this.root.removeFromParent();
  }
}
