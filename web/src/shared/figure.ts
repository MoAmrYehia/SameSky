// The contract a seated figure must offer to the room (src/xr/avatar3d.ts) and the portrait renderer
// (src/shared/portrait.ts). Two implementations exist:
//   - CharacterModel (src/shared/character.ts): the labelled DEMO kit, sculpted procedurally;
//   - SkinnedAvatar (src/shared/avatarRig.ts): an owner-approved, photo-derived skinned model.
// Character space: pelvis on the seat at the origin, +Y up, +Z forward (toward whoever they face).

import type { Group, Object3D, Quaternion, Vector3 } from 'three';

export interface Figure {
  readonly kind: 'preset_demo' | 'photo_derived';
  readonly root: Group;
  readonly head: Object3D;
  readonly leftWrist: Object3D;
  readonly rightWrist: Object3D;
  /** Metres per character unit when shown at the table (the demo kit is sculpted small). */
  readonly displayScale: number;
  /** Eye height above the seat, in character units. */
  readonly eyeHeight: number;
  gazeTarget: Vector3 | null;
  breathing: boolean;
  blinking: boolean;
  /** A natural wrist position (character space) for resting the chin on the viewer-left hand. */
  chinRest(out: Vector3): Vector3;
  /** Head orientation in character space; the lean offset tilts the upper body toward it (clamped). */
  setHead(quaternion: Quaternion, leanOffset?: Vector3): void;
  /** Wrist target in character space for hand 0 (x < 0 side) or 1 (x > 0 side). */
  setHand(index: 0 | 1, target: Vector3, orientation?: Quaternion | null): void;
  setPole(index: 0 | 1, pole: Vector3 | null): void;
  update(dt: number, t: number): void;
  /** Approximate centre of the head in world space (portrait framing). */
  headWorldPosition(target?: Vector3): Vector3;
  dispose(): void;
}
