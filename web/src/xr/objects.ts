// Memory objects that sit on the shared table. Each carries a shared moment (or a reply) and is placed in
// table-relative coordinates from its durable RoomObject record, so every participant sees the same layout
// and replacing the visuals never changes serialized positions. Each object has a simple invisible hit
// proxy so detailed meshes stay cheap to select with a hand ray or a fingertip.

import {
  BoxGeometry, CanvasTexture, CircleGeometry, CylinderGeometry, DoubleSide, Group, LatheGeometry, Mesh, MeshBasicMaterial,
  Matrix4, MeshPhysicalMaterial, MeshStandardMaterial, PlaneGeometry, Quaternion, RingGeometry, SRGBColorSpace, TextureLoader, TorusGeometry, Vector2,
  Vector3,
} from 'three';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';
import type { Moment, Reply, RoomObject } from '../shared/contracts';
import { tex, walnutMaterial } from '../shared/keepsakes3d';
import { contactShadow } from './env';
import { CanvasPanel, COLORS, FONT, roundRect, wrapText } from './ui3d';

const loader = new TextureLoader();

export function loadTexture(url: string) {
  const t = loader.load(url);
  t.colorSpace = SRGBColorSpace;
  t.anisotropy = 4;
  return t;
}

export interface MemoryObject {
  record: RoomObject;
  group: Group;
  glow?: Mesh;
  moment?: Moment;
  reply?: Reply;
  labelAnchorY: number;
  dispose(): void;
}

function speckleTexture(base: string, dots: string, count: number) {
  const c = document.createElement('canvas');
  c.width = 512;
  c.height = 256;
  const ctx = c.getContext('2d')!;
  ctx.fillStyle = base;
  ctx.fillRect(0, 0, 512, 256);
  let seed = 7;
  const rand = () => ((seed = (seed * 16807) % 2147483647) / 2147483647);
  for (let i = 0; i < count; i++) {
    ctx.fillStyle = dots.replace('A', (0.15 + rand() * 0.5).toFixed(2));
    ctx.beginPath();
    ctx.arc(rand() * 512, rand() * 256, 0.4 + rand() * 1.4, 0, Math.PI * 2);
    ctx.fill();
  }
  const t = new CanvasTexture(c);
  t.colorSpace = SRGBColorSpace;
  return t;
}

let mugTexture: CanvasTexture | null = null;
let corkTexture: CanvasTexture | null = null;

function mug(): Group {
  const g = new Group();
  mugTexture ??= speckleTexture('#efe6d6', 'rgba(92,72,52,A)', 900);
  corkTexture ??= speckleTexture('#b48a5e', 'rgba(70,46,24,A)', 2600);
  const profile = [
    new Vector2(0, 0), new Vector2(0.036, 0), new Vector2(0.041, 0.004), new Vector2(0.0435, 0.02), new Vector2(0.0445, 0.07), new Vector2(0.0445, 0.088),
    new Vector2(0.0415, 0.091), new Vector2(0.0385, 0.088), new Vector2(0.038, 0.012), new Vector2(0, 0.01),
  ];
  const glaze = new MeshPhysicalMaterial({ map: mugTexture, roughness: 0.35, clearcoat: 0.9, clearcoatRoughness: 0.12 });
  // body and handle share one glaze: merged into one mesh (one draw call)
  const bodyGeo = new LatheGeometry(profile, 48).translate(0, 0.007, 0);
  const handleGeo = new TorusGeometry(0.022, 0.0065, 12, 24, Math.PI * 1.25).rotateZ(-Math.PI * 0.62).translate(0.047, 0.053, 0);
  const body = new Mesh(mergeGeometries([bodyGeo, handleGeo])!, glaze);
  bodyGeo.dispose();
  handleGeo.dispose();
  const coffee = new Mesh(new CircleGeometry(0.037, 32), new MeshPhysicalMaterial({ color: '#3d2416', roughness: 0.15, clearcoat: 1 }));
  coffee.rotation.x = -Math.PI / 2;
  coffee.position.y = 0.079;
  const coaster = new Mesh(new CylinderGeometry(0.058, 0.058, 0.007, 48), new MeshStandardMaterial({ map: corkTexture, roughness: 0.95 }));
  coaster.position.y = 0.0035;
  for (const m of [body, coaster]) {
    m.castShadow = true;
    m.receiveShadow = true;
  }
  g.add(coaster, body, coffee);
  return g;
}

function glowRing(radius: number): Mesh {
  const ring = new Mesh(new RingGeometry(radius, radius + 0.014, 64), new MeshBasicMaterial({ color: 0xffd78a, transparent: true, opacity: 0.8, side: DoubleSide, depthWrite: false, toneMapped: false }));
  ring.rotation.x = -Math.PI / 2;
  ring.position.y = 0.0045;
  return ring;
}

function photoFrame(url: string | undefined, landscape = true): Group {
  const g = new Group();
  const w = landscape ? 0.2 : 0.14;
  const h = landscape ? 0.14 : 0.2;
  const border = 0.016;
  // the frame leans back on its easel stand; every walnut part is baked into one geometry (one draw call)
  const lean = new Matrix4().compose(new Vector3(0, h / 2 + 0.006, 0), new Quaternion().setFromAxisAngle(new Vector3(1, 0, 0), -0.16), new Vector3(1, 1, 1));
  const parts: BoxGeometry[] = [];
  for (const [x, y, bw, bh] of [[0, h / 2 - border / 2, w, border], [0, -h / 2 + border / 2, w, border], [-w / 2 + border / 2, 0, border, h], [w / 2 - border / 2, 0, border, h]]) {
    parts.push(new BoxGeometry(bw, bh, 0.016).translate(x, y, 0).applyMatrix4(lean) as BoxGeometry);
  }
  parts.push(new BoxGeometry(0.024, h * 0.82, 0.006).rotateX(0.42).translate(0, h * 0.4, -0.042) as BoxGeometry);
  const wood = new Mesh(mergeGeometries(parts)!, walnutMaterial());
  parts.forEach((p) => p.dispose());
  wood.castShadow = true;
  const back = new Mesh(new PlaneGeometry(w - 0.004, h - 0.004).translate(0, 0, -0.0075).applyMatrix4(lean), new MeshStandardMaterial({ color: '#3a2a1f', roughness: 0.9, side: DoubleSide }));
  const passe = new Mesh(new PlaneGeometry(w - border * 2, h - border * 2).translate(0, 0, 0.001).applyMatrix4(lean), new MeshStandardMaterial({ color: '#f3ebdd', roughness: 0.95 }));
  const print = new Mesh(new PlaneGeometry(w - border * 2 - 0.014, h - border * 2 - 0.014).translate(0, 0, 0.002).applyMatrix4(lean), new MeshStandardMaterial({ map: url ? tex(url) : null, color: url ? '#ffffff' : '#d8c8b0', roughness: 0.42 }));
  g.add(wood, back, passe, print);
  return g;
}

function postcard(url: string | undefined): Group {
  const g = new Group();
  const paper = new MeshStandardMaterial({ color: '#f2eadc', roughness: 0.9 });
  const card = new Mesh(new BoxGeometry(0.17, 0.003, 0.115), [paper, paper, new MeshStandardMaterial({ map: url ? tex(url) : null, color: url ? '#ffffff' : '#c9b9a2', roughness: 0.5 }), paper, paper, paper]);
  card.position.y = 0.0025;
  card.rotation.y = 0.12;
  card.castShadow = true;
  card.receiveShadow = true;
  const disc = new Mesh(new CircleGeometry(0.02, 32), new MeshBasicMaterial({ color: 0x1d2725, transparent: true, opacity: 0.72, toneMapped: false }));
  disc.rotation.x = -Math.PI / 2;
  disc.position.set(0, 0.0045, 0);
  const tri = new Mesh(new CircleGeometry(0.0095, 3), new MeshBasicMaterial({ color: 0xffffff, toneMapped: false }));
  tri.rotation.set(-Math.PI / 2, 0, -Math.PI / 2);
  tri.position.set(0.0015, 0.005, 0);
  g.add(card, disc, tri);
  return g;
}

function stone(): Group {
  const g = new Group();
  const s = new Mesh(new CylinderGeometry(0.045, 0.05, 0.03, 32), new MeshPhysicalMaterial({ color: '#9fb8a6', roughness: 0.3, clearcoat: 0.6 }));
  s.scale.set(1.15, 0.8, 0.9);
  s.position.y = 0.012;
  s.castShadow = true;
  g.add(s);
  return g;
}

/** A folded paper note carrying a reply. Labelled "Recorded message" so it never implies someone is here now. */
function note(reply: Reply | undefined): { group: Group; panel: CanvasPanel } {
  const g = new Group();
  const panel = new CanvasPanel(0.16, 0.1, { transparent: false });
  panel.draw((ctx, W, H) => {
    ctx.fillStyle = '#fbf6ec';
    ctx.fillRect(0, 0, W, H);
    ctx.fillStyle = COLORS.muted;
    ctx.font = `600 ${H * 0.12}px ${FONT}`;
    ctx.fillText(`${reply?.authorName ?? 'Reply'} · Recorded message`, W * 0.06, H * 0.2);
    ctx.fillStyle = COLORS.ink;
    ctx.font = `500 ${H * 0.15}px ${FONT}`;
    wrapText(ctx, reply?.text ?? '', W * 0.88).slice(0, 4).forEach((line, i) => ctx.fillText(line, W * 0.06, H * (0.42 + i * 0.18)));
  });
  (panel.mesh.material as MeshBasicMaterial).toneMapped = false;
  panel.mesh.rotation.x = -Math.PI / 2 + 0.55;
  panel.mesh.position.y = 0.04;
  const backFold = new Mesh(new PlaneGeometry(0.16, 0.1), new MeshStandardMaterial({ color: '#efe5d3', side: DoubleSide, roughness: 0.95 }));
  backFold.rotation.x = -Math.PI / 2 - 0.55;
  backFold.position.set(0, 0.04, -0.04);
  backFold.castShadow = true;
  g.add(panel.mesh, backFold);
  return { group: g, panel };
}

/** Invisible, simple selection volume (rendering skips it; ray and poke tests still hit it). */
function hitProxy(w: number, h: number, d: number, y: number) {
  const m = new Mesh(new BoxGeometry(w, h, d), new MeshBasicMaterial({ visible: false }));
  m.position.y = y;
  m.name = 'hit-proxy';
  return m;
}

export function buildMemoryObject(record: RoomObject, moment?: Moment, reply?: Reply): MemoryObject {
  let group: Group;
  let glow: Mesh | undefined;
  let notePanel: CanvasPanel | undefined;
  let labelAnchorY = 0.16;
  switch (record.objectType) {
    case 'cup':
      group = mug();
      glow = glowRing(0.07);
      group.add(glow, hitProxy(0.13, 0.12, 0.13, 0.06));
      labelAnchorY = 0.16;
      break;
    case 'frame': {
      const landscape = (moment?.media?.width ?? 16) >= (moment?.media?.height ?? 10);
      group = photoFrame(moment?.media?.thumbUrl ?? moment?.media?.url, landscape);
      group.add(hitProxy(landscape ? 0.22 : 0.16, landscape ? 0.17 : 0.22, 0.1, 0.08));
      labelAnchorY = 0.22;
      break;
    }
    case 'postcard':
      group = postcard(moment?.media?.thumbUrl ?? moment?.media?.posterUrl);
      group.add(hitProxy(0.19, 0.04, 0.14, 0.015));
      labelAnchorY = 0.09;
      break;
    case 'audioStone':
      group = stone();
      group.add(hitProxy(0.12, 0.06, 0.12, 0.02));
      break;
    default: {
      const n = note(reply);
      group = n.group;
      notePanel = n.panel;
      group.add(hitProxy(0.18, 0.09, 0.12, 0.04));
      labelAnchorY = 0.13;
    }
  }
  const shadow = contactShadow(0.2, 0.2, 0.38);
  shadow.position.y = 0.0015;
  group.add(shadow);
  group.name = `memory-${record.id}`;
  group.position.set(record.transform.x, record.transform.y, record.transform.z);
  group.rotation.y = (record.transform.yaw * Math.PI) / 180;
  return {
    record, group, glow, moment, reply, labelAnchorY,
    dispose() {
      notePanel?.dispose();
      group.traverse((o) => (o as Mesh).geometry?.dispose?.());
    },
  };
}

export function infoCard(widthM: number, heightM: number, title: string, lines: string[], footer?: string): CanvasPanel {
  const panel = new CanvasPanel(widthM, heightM);
  panel.draw((ctx, W, H) => {
    ctx.fillStyle = 'rgba(247,242,234,0.97)';
    roundRect(ctx, 0, 0, W, H, 48);
    ctx.fill();
    ctx.strokeStyle = 'rgba(255,255,255,0.9)';
    ctx.lineWidth = 4;
    roundRect(ctx, 2, 2, W - 4, H - 4, 46);
    ctx.stroke();
    ctx.fillStyle = COLORS.ink;
    ctx.font = `700 ${Math.round(H * 0.11)}px ${FONT}`;
    ctx.fillText(title, W * 0.06, H * 0.2, W * 0.88);
    ctx.font = `500 ${Math.round(H * 0.075)}px ${FONT}`;
    ctx.fillStyle = COLORS.muted;
    let y = H * 0.36;
    for (const line of lines) {
      for (const l of wrapText(ctx, line, W * 0.88)) {
        ctx.fillText(l, W * 0.06, y);
        y += H * 0.1;
      }
    }
    if (footer) {
      ctx.fillStyle = COLORS.green;
      ctx.font = `600 ${Math.round(H * 0.07)}px ${FONT}`;
      ctx.fillText(footer, W * 0.06, H * 0.9, W * 0.88);
    }
  });
  return panel;
}

