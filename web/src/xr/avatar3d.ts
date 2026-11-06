// Lightweight, stylized avatar built from the same preset definition as the companion portrait.
// Head + upper body + hands only (P0). Head and hands follow real XR poses when available; anything
// else is labelled as app animation, never presented as measured body motion.

import {
  CapsuleGeometry, Color, Euler, Group, Mesh, MeshStandardMaterial, Quaternion, SphereGeometry, TorusGeometry, Vector3, CylinderGeometry,
} from 'three';
import type { AvatarDefinition } from '../shared/contracts';
import { CanvasPanel, COLORS, FONT, roundRect } from './ui3d';

const mat = (color: string | number, rough = 0.85) => new MeshStandardMaterial({ color: new Color(color), roughness: rough });

export type PresenceKind = 'live' | 'recorded' | 'simulated' | 'away';

export class Avatar3D {
  readonly root = new Group();
  readonly head = new Group();
  readonly body = new Group();
  readonly leftHand: Mesh;
  readonly rightHand: Mesh;
  readonly label: CanvasPanel;
  private targetHead = { p: new Vector3(0, 1.12, 0), q: new Quaternion() };
  private targetLeft = new Vector3(-0.2, 0.75, 0.3);
  private targetRight = new Vector3(0.2, 0.75, 0.3);
  private tracked = false;
  private wave = 0;

  constructor(def: AvatarDefinition, readonly name: string) {
    const skin = mat(def.skin, 0.7);
    const top = mat(def.top, 0.95);
    const hair = mat(def.hairColor, 0.8);

    // Body
    const torso = new Mesh(new CapsuleGeometry(0.17, 0.26, 6, 16), top);
    torso.scale.set(1.15, 1, 0.75);
    torso.position.y = -0.36;
    const neck = new Mesh(new CylinderGeometry(0.045, 0.05, 0.08, 12), skin);
    neck.position.y = -0.14;
    this.body.add(torso, neck);
    if (def.accessory === 'scarf') {
      const scarf = new Mesh(new TorusGeometry(0.075, 0.03, 8, 20), mat('#c98f3a'));
      scarf.rotation.x = Math.PI / 2;
      scarf.position.y = -0.17;
      this.body.add(scarf);
    }

    // Head
    const skull = new Mesh(new SphereGeometry(0.1, 24, 18), skin);
    skull.scale.set(0.95, 1.12, 0.98);
    this.head.add(skull);
    const eyeMat = mat('#2b2222', 0.4);
    for (const x of [-0.035, 0.035]) {
      const eye = new Mesh(new SphereGeometry(0.012, 10, 8), eyeMat);
      eye.position.set(x, 0.012, 0.092);
      eye.scale.set(1, 1.2, 0.6);
      this.head.add(eye);
    }
    const smile = new Mesh(new TorusGeometry(0.026, 0.004, 6, 16, Math.PI), mat('#8a4a3a'));
    smile.position.set(0, -0.04, 0.094);
    smile.rotation.z = Math.PI;
    this.head.add(smile);
    for (const x of [-0.06, 0.06]) {
      const cheek = new Mesh(new SphereGeometry(0.016, 8, 6), new MeshStandardMaterial({ color: '#e58f7a', transparent: true, opacity: 0.35 }));
      cheek.position.set(x, -0.02, 0.083);
      cheek.scale.z = 0.3;
      this.head.add(cheek);
    }
    this.addHair(def, hair);
    this.addAccessory(def);

    // Hands
    this.leftHand = new Mesh(new SphereGeometry(0.04, 14, 10), skin);
    this.rightHand = new Mesh(new SphereGeometry(0.04, 14, 10), skin);
    this.leftHand.scale.set(1, 0.6, 1.3);
    this.rightHand.scale.set(1, 0.6, 1.3);

    this.root.add(this.body, this.head, this.leftHand, this.rightHand);

    this.label = new CanvasPanel(0.42, 0.075);
    this.label.mesh.position.y = 0.26;
    this.head.add(this.label.mesh);
    this.setPresence('simulated');
    this.applyImmediately();
  }

  private addHair(def: AvatarDefinition, hair: MeshStandardMaterial) {
    const cap = (scaleY: number, y: number) => {
      const m = new Mesh(new SphereGeometry(0.106, 24, 14, 0, Math.PI * 2, 0, Math.PI * 0.55), hair);
      m.scale.set(1, scaleY, 1.02);
      m.position.y = y;
      m.rotation.x = -0.25;
      return m;
    };
    switch (def.hair) {
      case 'short':
      case 'buzz':
        this.head.add(cap(def.hair === 'buzz' ? 0.95 : 1.05, 0.02));
        break;
      case 'wavy':
      case 'long': {
        this.head.add(cap(1.1, 0.02));
        const back = new Mesh(new CapsuleGeometry(0.1, def.hair === 'long' ? 0.18 : 0.08, 6, 12), hair);
        back.position.set(0, def.hair === 'long' ? -0.1 : -0.05, -0.04);
        back.scale.set(1.15, 1, 0.7);
        this.head.add(back);
        break;
      }
      case 'bun': {
        this.head.add(cap(1.02, 0.02));
        const bun = new Mesh(new SphereGeometry(0.045, 14, 10), hair);
        bun.position.set(0, 0.11, -0.05);
        this.head.add(bun);
        break;
      }
      case 'curly':
        for (let i = 0; i < 14; i++) {
          const a = (i / 14) * Math.PI * 2;
          const puff = new Mesh(new SphereGeometry(0.04, 10, 8), hair);
          puff.position.set(Math.cos(a) * 0.085, 0.06 + Math.sin(i * 1.7) * 0.02, Math.sin(a) * 0.075 - 0.01);
          this.head.add(puff);
        }
        break;
      default:
        break;
    }
  }

  private addAccessory(def: AvatarDefinition) {
    if (def.accessory === 'glasses') {
      for (const x of [-0.036, 0.036]) {
        const lens = new Mesh(new TorusGeometry(0.022, 0.0035, 6, 18), mat('#2a2a2a', 0.4));
        lens.position.set(x, 0.012, 0.1);
        this.head.add(lens);
      }
    } else if (def.accessory === 'earrings') {
      for (const x of [-0.098, 0.098]) {
        const ring = new Mesh(new SphereGeometry(0.009, 8, 6), new MeshStandardMaterial({ color: '#d8b24a', metalness: 0.7, roughness: 0.3 }));
        ring.position.set(x, -0.05, 0.01);
        this.head.add(ring);
      }
    } else if (def.accessory === 'cap') {
      const brim = new Mesh(new CylinderGeometry(0.08, 0.08, 0.008, 16, 1, false, 0, Math.PI), mat(def.top));
      brim.position.set(0, 0.07, 0.07);
      brim.rotation.y = Math.PI / 2;
      this.head.add(brim);
    }
  }

  setPresence(kind: PresenceKind, detail?: string) {
    const text = {
      live: `${this.name} · Here now`,
      recorded: `${this.name} · Recorded message${detail ? ` · ${detail}` : ''}`,
      simulated: `${this.name} · Simulated (sample)`,
      away: `${this.name} · Not here right now`,
    }[kind];
    const dot = { live: '#58c27d', recorded: COLORS.gold, simulated: '#9aa5a0', away: '#9aa5a0' }[kind];
    this.label.draw((ctx, W, H) => {
      ctx.font = `600 ${H * 0.46}px ${FONT}`;
      const tw = Math.min(W - 20, ctx.measureText(text).width + H * 1.3);
      const x = (W - tw) / 2;
      ctx.fillStyle = 'rgba(22,28,26,0.86)';
      roundRect(ctx, x, 2, tw, H - 4, (H - 4) / 2);
      ctx.fill();
      ctx.fillStyle = dot;
      ctx.beginPath();
      ctx.arc(x + H * 0.5, H / 2, H * 0.16, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = '#fff';
      ctx.textBaseline = 'middle';
      ctx.fillText(text, x + H * 0.85, H / 2 + 1, tw - H);
    });
    for (const m of [this.body, this.head, this.leftHand, this.rightHand]) m.traverse((o) => {
      const material = (o as Mesh).material as MeshStandardMaterial | undefined;
      if (material && 'opacity' in material && o !== this.label.mesh) {
        material.transparent = kind === 'away';
        material.opacity = kind === 'away' ? 0.35 : 1;
      }
    });
  }

  /** Pose in the avatar's parent space (table-relative). */
  setPose(head: { p: number[]; q: number[] }, left?: { p: number[] } | null, right?: { p: number[] } | null, tracked = true) {
    this.targetHead.p.fromArray(head.p);
    this.targetHead.q.fromArray(head.q);
    if (left) this.targetLeft.fromArray(left.p);
    if (right) this.targetRight.fromArray(right.p);
    this.tracked = tracked;
  }

  playGesture(name: string | null | undefined) {
    if (name === 'wave') this.wave = 2.2;
  }

  private applyImmediately() {
    this.head.position.copy(this.targetHead.p);
    this.head.quaternion.copy(this.targetHead.q);
  }

  /** Interpolate toward the latest network pose (10-20 Hz in, 72-90 Hz out) and add gentle idle motion. */
  update(dt: number, t: number) {
    const k = 1 - Math.exp(-dt * 12);
    this.head.position.lerp(this.targetHead.p, k);
    this.head.quaternion.slerp(this.targetHead.q, k);
    if (!this.tracked) this.head.position.y += Math.sin(t * 1.4) * 0.0015; // breathing animation (app animation)
    const yaw = new Euler().setFromQuaternion(this.head.quaternion, 'YXZ').y;
    this.body.position.set(this.head.position.x, this.head.position.y, this.head.position.z);
    this.body.rotation.y = yaw;
    this.leftHand.position.lerp(this.targetLeft, k);
    this.rightHand.position.lerp(this.targetRight, k);
    if (this.wave > 0) {
      this.wave -= dt;
      const up = new Vector3(0.2 + Math.sin(this.wave * 9) * 0.035, 0.02, 0.1).applyEuler(new Euler(0, yaw, 0));
      this.rightHand.position.copy(this.head.position).add(up);
    }
  }

  dispose() {
    this.root.traverse((o) => {
      const m = o as Mesh;
      m.geometry?.dispose();
      const material = m.material as MeshStandardMaterial | MeshStandardMaterial[] | undefined;
      if (Array.isArray(material)) material.forEach((x) => x.dispose());
      else material?.dispose();
    });
    this.label.dispose();
  }
}
