// The person across the table, built from the shared sculpted character kit (src/shared/character.ts) —
// the same model, outfit and materials the companion renders as their portrait.
//
// Compatible with the previous Avatar3D interface (root, head, leftHand/rightHand anchors, setPresence,
// setPose, playGesture, update, dispose), so table-relative networking is unchanged. Presence is always
// labelled: live (tracked head/hands from the network), recorded (a voice note is playing), simulated
// (sample mode) or away. Authored idle motion (breathing, blinks, glances, rest poses, gestures) is kept
// separate from tracking: a tracked head or hand always wins over idle animation and gestures.

import { Euler, Group, type Material, type Mesh, type Object3D, Quaternion, Vector3 } from 'three';
import { type CharacterKit, CharacterModel } from '../shared/character';
import type { AvatarDefinition } from '../shared/contracts';
import { COLORS, FittedPill } from './ui3d';

export type PresenceKind = 'live' | 'recorded' | 'simulated' | 'away';
export type RestPose = 'table' | 'chin';

/** The seat the character's pelvis rests on, in table space (metres, radians; yaw 0 faces +Z, the viewer). */
export interface Seat {
  x: number;
  y: number;
  z: number;
  yaw: number;
}

/** Character units -> metres. The kit is sculpted slightly small; this brings it to adult seated scale. */
export const AVATAR_SCALE = 1.2;
const TABLE_RADIUS = 0.54;
/** A remote participant's head when seated at their own table, mirrored to our far side (table space). */
const TRACKED_ORIGIN = new Vector3(0, 0.43, -0.8);
const UP = new Vector3(0, 1, 0);

const _v1 = new Vector3();
const _v2 = new Vector3();
const _q1 = new Quaternion();
const _e = new Euler(0, 0, 0, 'YXZ');

export class Avatar3D {
  /** Table space, at the seat. Contains the (rotated, scaled) body and the presence badge. */
  readonly root = new Group();
  readonly body = new Group();
  readonly character: CharacterModel;
  readonly head: Object3D;
  readonly leftHand: Object3D;
  readonly rightHand: Object3D;
  readonly label: FittedPill;
  presence: PresenceKind = 'simulated';
  rest: RestPose = 'table';
  /** Which side of the head the presence badge sits on (layout picks the side with room). */
  badgeSide: 'right' | 'left' = 'right';
  /** Table-space point the person attends to when their head is not tracked (the viewer, the screen…). */
  readonly attention = new Vector3(0, 0.45, 0.8);
  private tracked = false;
  private readonly trackedHeadP = new Vector3();
  private readonly trackedHeadQ = new Quaternion();
  private readonly trackedHand = [new Vector3(), new Vector3()];
  private readonly hasTrackedHand = [false, false];
  private readonly restHand = [new Vector3(), new Vector3()];
  private readonly restHandQ = [new Quaternion(), new Quaternion()];
  private readonly restOriented = [false, false];
  private readonly restLean = new Vector3();
  private readonly lean = new Vector3();
  private readonly headQ = new Quaternion();
  private readonly gaze = new Vector3();
  private wave = 0;
  private tableTop = 0;
  private tableEdge = 0.35;
  private opacity = 1;
  private materials: Material[] = [];

  constructor(kit: CharacterKit, def: AvatarDefinition, readonly name: string, seat: Seat, seed = 2) {
    this.character = new CharacterModel(kit, def, seed);
    this.head = this.character.head;
    this.leftHand = this.character.leftWrist;
    this.rightHand = this.character.rightWrist;
    this.root.name = `avatar-${name}`;
    this.root.position.set(seat.x, seat.y, seat.z);
    this.body.rotation.y = seat.yaw;
    this.body.scale.setScalar(AVATAR_SCALE);
    this.body.add(this.character.root);
    this.label = new FittedPill(0.78, 0.085, 20);
    // the badge is interface: always legible, never hidden inside the hair or head
    this.label.mesh.material.depthTest = false;
    this.root.add(this.body, this.label.mesh);
    this.character.root.traverse((o) => {
      const m = (o as Mesh).material as Material | undefined;
      if (m && !this.materials.includes(m)) this.materials.push(m);
    });
    this.measureTable(seat);
    this.setRest('table');
    this.character.gazeTarget = this.gaze;
    this.setPresence('simulated');
  }

  /** Where the table top and its near edge are in character space, for rest poses that touch the table. */
  private measureTable(seat: Seat) {
    this.tableTop = -seat.y / AVATAR_SCALE;
    // first intersection of the character's forward ray with the table's rim (table space, XZ)
    const fx = Math.sin(seat.yaw);
    const fz = Math.cos(seat.yaw);
    const b = 2 * (seat.x * fx + seat.z * fz);
    const c = seat.x * seat.x + seat.z * seat.z - TABLE_RADIUS * TABLE_RADIUS;
    const disc = b * b - 4 * c;
    const t = disc > 0 ? (-b - Math.sqrt(disc)) / 2 : 0.4;
    this.tableEdge = Math.max(0.2, t / AVATAR_SCALE);
  }

  /** Seated rest pose: forearms on the table, or chin resting on one hand (the other arm on the table). */
  setRest(pose: RestPose) {
    this.rest = pose;
    const top = this.tableTop + 0.022;
    const edge = this.tableEdge + 0.045;
    if (pose === 'chin') {
      // viewer-left hand (index 0) under the chin, elbow forward and down toward the table
      this.restHand[0].set(-0.022, 0.392, 0.17);
      this.restHandQ[0].copy(handBasis(_v1.set(0.28, 1, -0.32), _v2.set(0.45, 0.1, -1)));
      this.restOriented[0] = true;
      this.character.setPole(0, _v1.set(-0.15, -0.8, 0.75));
      this.restHand[1].set(0.15, top, edge + 0.02);
      this.restOriented[1] = false;
      this.character.setPole(1, null);
      this.restLean.set(-0.012, 0, 0.05);
    } else {
      this.restHand[0].set(-0.13, top, edge);
      this.restHand[1].set(0.14, top, edge + 0.015);
      this.restOriented[0] = this.restOriented[1] = false;
      this.character.setPole(0, null);
      this.character.setPole(1, null);
      this.restLean.set(0, 0, 0.025);
    }
  }

  setPresence(kind: PresenceKind, detail?: string) {
    this.presence = kind;
    const text = {
      live: `${this.name} · Here now`,
      recorded: `${this.name} · Recorded message${detail ? ` · ${detail}` : ''}`,
      simulated: `${this.name} · Simulated (sample)`,
      away: `${this.name} · Not here right now`,
    }[kind];
    const dot = { live: '#58c27d', recorded: COLORS.gold, simulated: '#b8c2bd', away: '#9aa5a0' }[kind];
    this.label.set(text, { dot });
    this.setOpacity(kind === 'away' ? 0.38 : 1);
  }

  private setOpacity(opacity: number) {
    if (opacity === this.opacity) return;
    this.opacity = opacity;
    for (const m of this.materials) {
      m.transparent = opacity < 1;
      m.opacity = opacity;
      m.depthWrite = opacity >= 1;
      m.needsUpdate = true;
    }
  }

  /**
   * Networked pose in table space (already mirrored to our far side). Positions are re-based on this seat so
   * the person stays in the chair: their head offset from a nominal seated head becomes a lean, and their
   * hands keep the same offset from their head. `tracked` false means the pose came from a 2D pointer, not a
   * headset, so it drives nothing but presence.
   */
  setPose(head: { p: number[]; q: number[] }, left?: { p: number[]; q?: number[] } | null, right?: { p: number[]; q?: number[] } | null, tracked = true) {
    this.tracked = tracked;
    if (!tracked) {
      this.hasTrackedHand[0] = this.hasTrackedHand[1] = false;
      return;
    }
    this.trackedHeadP.fromArray(head.p);
    this.trackedHeadQ.fromArray(head.q);
    const hands = [left, right];
    for (let i = 0; i < 2; i++) {
      const h = hands[i];
      this.hasTrackedHand[i] = !!h;
      if (h) {
        this.trackedHand[i].fromArray(h.p);
      }
    }
  }

  playGesture(name: string | null | undefined) {
    if (name === 'wave') this.wave = 2.2;
  }

  /** Table space -> character space (the body's local frame). */
  private toCharacter(p: Vector3, out: Vector3) {
    return out.copy(p).sub(this.root.position).applyAxisAngle(UP, -this.body.rotation.y).divideScalar(AVATAR_SCALE);
  }

  /**
   * Per-frame update (allocation-free). `viewer` is the viewer's head in table space, used to turn the
   * presence badge toward them; on a flat screen pass `flatQ` (the camera's orientation in table space) so
   * the badge lies parallel to the image plane and its text never keystones.
   */
  update(dt: number, t: number, viewer?: Vector3, flatQ?: Quaternion) {
    const c = this.character;
    const yaw = this.body.rotation.y;
    if (this.tracked) {
      // measured head: orientation relative to the body, position offset becomes a (clamped) lean
      _q1.setFromAxisAngle(UP, -yaw).multiply(this.trackedHeadQ);
      this.headQ.copy(_q1);
      this.toCharacter(_v1.copy(this.trackedHeadP).sub(TRACKED_ORIGIN).add(this.root.position), this.lean);
      this.lean.clampScalar(-0.12, 0.12);
      this.gaze.set(0, 0.6, 2).applyQuaternion(this.headQ);
    } else {
      // authored attention: look toward the attention point with a slow, small idle drift
      this.toCharacter(this.attention, this.gaze);
      const dx = this.gaze.x;
      const dy = this.gaze.y - 0.6;
      const dz = this.gaze.z;
      const look = Math.max(-0.6, Math.min(0.6, Math.atan2(dx, dz) + Math.sin(t * 0.37) * 0.05));
      const pitch = Math.max(-0.3, Math.min(0.35, -Math.atan2(dy, Math.hypot(dx, dz)) + Math.sin(t * 0.23) * 0.025));
      const roll = this.rest === 'chin' ? 0.14 : Math.sin(t * 0.31) * 0.03;
      this.headQ.setFromEuler(_e.set(pitch, look, roll, 'YXZ'));
      this.lean.copy(this.restLean);
    }
    c.setHead(this.headQ, this.lean);

    // hands: tracked hands win; otherwise the rest pose, with the wave gesture on the viewer-right hand
    for (let i = 0; i < 2; i++) {
      const idx = i as 0 | 1;
      if (this.tracked && this.hasTrackedHand[i]) {
        // keep the measured hand-to-head offset, re-based on this avatar's head. Orientation stays relaxed:
        // grip-space axes differ between hand tracking and controllers and are not verified on device yet.
        _v2.copy(this.trackedHand[i]).sub(this.trackedHeadP);
        c.head.getWorldPosition(_v1);
        this.root.parent?.worldToLocal(_v1);
        this.toCharacter(_v1.add(_v2), _v1);
        c.setHand(idx, _v1, null);
      } else if (i === 1 && this.wave > 0 && !(this.tracked && this.hasTrackedHand[1])) {
        this.wave = Math.max(0, this.wave - dt);
        _v1.set(0.2 + Math.sin(this.wave * 9) * 0.035, 0.62, 0.12);
        c.setHand(1, _v1, handBasis(_v2.set(-0.1, 1, 0.1), _q1Vec.set(0, 0, 1)) );
      } else {
        c.setHand(idx, this.restHand[i], this.restOriented[i] ? this.restHandQ[i] : null);
      }
    }
    c.update(dt, t);

    // presence badge: beside the head at eye level, clear of the hair, turned toward the viewer
    c.head.getWorldPosition(_v1);
    this.root.worldToLocal(_v1);
    const side = this.badgeSide === 'right' ? 1 : -1;
    this.label.mesh.position.set(_v1.x + side * (0.2 + this.label.width / 2), _v1.y + 0.16, _v1.z + 0.05);
    if (flatQ) this.label.mesh.quaternion.copy(flatQ);
    else if (viewer) {
      const lx = viewer.x - (this.root.position.x + this.label.mesh.position.x);
      const ly = viewer.y - (this.root.position.y + this.label.mesh.position.y);
      const lz = viewer.z - (this.root.position.z + this.label.mesh.position.z);
      this.label.mesh.rotation.set(-Math.atan2(ly, Math.hypot(lx, lz)), Math.atan2(lx, lz), 0, 'YXZ');
    }
  }

  /** Badge corners in world space (for safe-area checks). */
  labelCorners(out: Vector3[]) {
    const w = this.label.width / 2;
    const h = this.label.height / 2;
    const m = this.label.mesh;
    m.updateWorldMatrix(true, false);
    const s = m.scale;
    out[0].set(-w / s.x, -h / s.y, 0).applyMatrix4(m.matrixWorld);
    out[1].set(w / s.x, -h / s.y, 0).applyMatrix4(m.matrixWorld);
    out[2].set(w / s.x, h / s.y, 0).applyMatrix4(m.matrixWorld);
    out[3].set(-w / s.x, h / s.y, 0).applyMatrix4(m.matrixWorld);
    return out;
  }

  dispose() {
    this.character.dispose();
    this.label.dispose();
    this.root.removeFromParent();
  }
}

const _q1Vec = new Vector3();
const _bx = new Vector3();
const _by = new Vector3();
const _bz = new Vector3();
const _bq = new Quaternion();

/** Hand orientation (character space) from a finger direction and the direction the palm faces. */
function handBasis(fingers: Vector3, palm: Vector3): Quaternion {
  // matches CharacterModel's hand frame: fingers along local -Y, the back of the hand along local +Z
  _by.copy(fingers).normalize().negate();
  _bz.copy(palm).normalize().negate();
  _bz.addScaledVector(_by, -_bz.dot(_by)).normalize();
  _bx.crossVectors(_by, _bz);
  const m00 = _bx.x, m01 = _by.x, m02 = _bz.x;
  const m10 = _bx.y, m11 = _by.y, m12 = _bz.y;
  const m20 = _bx.z, m21 = _by.z, m22 = _bz.z;
  // rotation matrix -> quaternion (same as Quaternion.setFromRotationMatrix, without a Matrix4)
  const trace = m00 + m11 + m22;
  if (trace > 0) {
    const s = 0.5 / Math.sqrt(trace + 1);
    _bq.set((m21 - m12) * s, (m02 - m20) * s, (m10 - m01) * s, 0.25 / s);
  } else if (m00 > m11 && m00 > m22) {
    const s = 2 * Math.sqrt(1 + m00 - m11 - m22);
    _bq.set(0.25 * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s);
  } else if (m11 > m22) {
    const s = 2 * Math.sqrt(1 + m11 - m00 - m22);
    _bq.set((m01 + m10) / s, 0.25 * s, (m12 + m21) / s, (m02 - m20) / s);
  } else {
    const s = 2 * Math.sqrt(1 + m22 - m00 - m11);
    _bq.set((m02 + m20) / s, (m12 + m21) / s, 0.25 * s, (m10 - m01) / s);
  }
  return _bq;
}
