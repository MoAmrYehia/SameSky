// Memory objects that sit on the shared table. Each one carries a shared moment (or a reply) and is
// positioned in table-relative coordinates, so every participant sees the same layout from their seat.

import {
  BoxGeometry, CanvasTexture, CircleGeometry, Color, CylinderGeometry, DoubleSide, Group, LatheGeometry, Mesh, MeshBasicMaterial,
  MeshStandardMaterial, type Object3D, PlaneGeometry, RingGeometry, SphereGeometry, SRGBColorSpace, TextureLoader, TorusGeometry, Vector2,
} from 'three';
import type { Award, Moment, Reply, RoomObject } from '../shared/contracts';
import { CanvasPanel, COLORS, FONT, roundRect, wrapText } from './ui3d';

const loader = new TextureLoader();

export function loadTexture(url: string) {
  const t = loader.load(url);
  t.colorSpace = SRGBColorSpace;
  return t;
}

export interface MemoryObject {
  record: RoomObject;
  group: Group;
  glow?: Mesh;
  moment?: Moment;
  reply?: Reply;
  dispose(): void;
}

function speckledMug(): Group {
  const g = new Group();
  const pts = [new Vector2(0, 0), new Vector2(0.038, 0), new Vector2(0.042, 0.01), new Vector2(0.044, 0.085), new Vector2(0.041, 0.09), new Vector2(0.036, 0.088), new Vector2(0.036, 0.02), new Vector2(0, 0.02)];
  const c = document.createElement('canvas');
  c.width = 256;
  c.height = 128;
  const ctx = c.getContext('2d')!;
  ctx.fillStyle = '#efe6d6';
  ctx.fillRect(0, 0, 256, 128);
  for (let i = 0; i < 260; i++) {
    ctx.fillStyle = `rgba(90,70,50,${Math.random() * 0.5})`;
    ctx.beginPath();
    ctx.arc(Math.random() * 256, Math.random() * 128, Math.random() * 1.6, 0, Math.PI * 2);
    ctx.fill();
  }
  const map = new CanvasTexture(c);
  map.colorSpace = SRGBColorSpace;
  const mug = new Mesh(new LatheGeometry(pts, 32), new MeshStandardMaterial({ map, roughness: 0.5 }));
  const handle = new Mesh(new TorusGeometry(0.022, 0.006, 8, 16, Math.PI * 1.3), new MeshStandardMaterial({ color: 0xefe6d6, roughness: 0.5 }));
  handle.position.set(0.046, 0.048, 0);
  handle.rotation.z = -Math.PI * 0.65;
  const coffee = new Mesh(new CircleGeometry(0.035, 24), new MeshStandardMaterial({ color: 0x4a2c1a, roughness: 0.2 }));
  coffee.rotation.x = -Math.PI / 2;
  coffee.position.y = 0.075;
  const coaster = new Mesh(new CylinderGeometry(0.06, 0.06, 0.006, 32), new MeshStandardMaterial({ color: 0xb08a5c, roughness: 1 }));
  coaster.position.y = 0.003;
  g.add(coaster, mug, handle, coffee);
  mug.position.y = 0.006;
  return g;
}

function glowRing(radius: number): Mesh {
  const ring = new Mesh(new RingGeometry(radius, radius + 0.018, 48), new MeshBasicMaterial({ color: 0xffd78a, transparent: true, opacity: 0.8, side: DoubleSide, depthWrite: false }));
  ring.rotation.x = -Math.PI / 2;
  ring.position.y = 0.004;
  return ring;
}

function photoFrame(url: string | undefined, landscape = true): Group {
  const g = new Group();
  const w = landscape ? 0.2 : 0.14;
  const h = landscape ? 0.14 : 0.2;
  const wood = new MeshStandardMaterial({ color: 0xa8743f, roughness: 0.6 });
  const back = new Mesh(new BoxGeometry(w + 0.025, h + 0.025, 0.012), wood);
  const mat = new Mesh(new PlaneGeometry(w, h), new MeshStandardMaterial({ color: 0xf6efe2 }));
  mat.position.z = 0.0065;
  const photo = new Mesh(new PlaneGeometry(w - 0.02, h - 0.02), new MeshBasicMaterial({ map: url ? loadTexture(url) : null, color: url ? 0xffffff : 0xd8c8b0, toneMapped: false }));
  photo.position.z = 0.007;
  const frame = new Group();
  frame.add(back, mat, photo);
  frame.position.y = h / 2 + 0.012;
  frame.rotation.x = -0.18;
  const stand = new Mesh(new BoxGeometry(0.03, h * 0.8, 0.008), wood);
  stand.position.set(0, h * 0.4, -0.04);
  stand.rotation.x = 0.4;
  g.add(frame, stand);
  return g;
}

function postcard(url: string | undefined): Group {
  const g = new Group();
  const card = new Mesh(new BoxGeometry(0.18, 0.004, 0.12), [
    new MeshStandardMaterial({ color: 0xf2eadc }), new MeshStandardMaterial({ color: 0xf2eadc }),
    new MeshBasicMaterial({ map: url ? loadTexture(url) : null, color: url ? 0xffffff : 0xc9b9a2, toneMapped: false }),
    new MeshStandardMaterial({ color: 0xf2eadc }), new MeshStandardMaterial({ color: 0xf2eadc }), new MeshStandardMaterial({ color: 0xf2eadc }),
  ]);
  card.position.y = 0.006;
  card.rotation.y = 0.15;
  // a small "play" disc tells people it is a clip
  const disc = new Mesh(new CircleGeometry(0.022, 24), new MeshBasicMaterial({ color: 0x1d2725, transparent: true, opacity: 0.7 }));
  disc.rotation.x = -Math.PI / 2;
  disc.position.set(0, 0.0085, 0);
  const tri = new Mesh(new CircleGeometry(0.011, 3), new MeshBasicMaterial({ color: 0xffffff }));
  tri.rotation.set(-Math.PI / 2, 0, -Math.PI / 2);
  tri.position.set(0.002, 0.009, 0);
  g.add(card, disc, tri);
  return g;
}

function stone(): Group {
  const g = new Group();
  const s = new Mesh(new SphereGeometry(0.045, 24, 16), new MeshStandardMaterial({ color: 0x9fb8a6, roughness: 0.35 }));
  s.scale.set(1.2, 0.5, 0.9);
  s.position.y = 0.022;
  g.add(s);
  return g;
}

/** A folded paper note carrying a reply ("Recorded message"). */
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
  panel.mesh.rotation.x = -Math.PI / 2 + 0.55;
  panel.mesh.position.y = 0.04;
  const backFold = new Mesh(new PlaneGeometry(0.16, 0.1), new MeshStandardMaterial({ color: 0xf1e8d8, side: DoubleSide }));
  backFold.rotation.x = -Math.PI / 2 - 0.55;
  backFold.position.set(0, 0.04, -0.04);
  g.add(panel.mesh, backFold);
  return { group: g, panel };
}

export function buildMemoryObject(record: RoomObject, moment?: Moment, reply?: Reply): MemoryObject {
  let group: Group;
  let glow: Mesh | undefined;
  let notePanel: CanvasPanel | undefined;
  switch (record.objectType) {
    case 'cup':
      group = speckledMug();
      glow = glowRing(0.075);
      group.add(glow);
      break;
    case 'frame':
      group = photoFrame(moment?.media?.thumbUrl ?? moment?.media?.url, (moment?.media?.width ?? 16) >= (moment?.media?.height ?? 10));
      break;
    case 'postcard':
      group = postcard(moment?.media?.thumbUrl);
      break;
    case 'audioStone':
      group = stone();
      break;
    default: {
      const n = note(reply);
      group = n.group;
      notePanel = n.panel;
    }
  }
  group.name = `memory-${record.id}`;
  group.position.set(record.transform.x, record.transform.y, record.transform.z);
  group.rotation.y = (record.transform.yaw * Math.PI) / 180;
  return {
    record, group, glow, moment, reply,
    dispose() {
      notePanel?.dispose();
      group.traverse((o) => {
        const m = o as Mesh;
        m.geometry?.dispose?.();
      });
    },
  };
}

// ---------------------------------------------------------------------------
// Keepsakes shelf
// ---------------------------------------------------------------------------

export function buildKeepsake(award: Award): Object3D {
  const g = new Group();
  const plinth = new Mesh(new CylinderGeometry(0.07, 0.075, 0.02, 24), new MeshStandardMaterial({ color: 0x3c2a1e }));
  plinth.position.y = 0.01;
  g.add(plinth);
  const dim = !award.earned;
  if (award.keepsake === 'frame') {
    const f = photoFrame('/sample/adam-waterfront.jpg');
    f.scale.setScalar(0.75);
    f.position.y = 0.02;
    g.add(f);
  } else if (award.keepsake === 'linked_sculptures') {
    const m1 = new MeshStandardMaterial({ color: 0xf0e7d8, roughness: 0.35 });
    const a = new Mesh(new TorusGeometry(0.045, 0.016, 16, 32), m1);
    const b = new Mesh(new TorusGeometry(0.045, 0.016, 16, 32), new MeshStandardMaterial({ color: 0xd9cdb8, roughness: 0.35 }));
    a.position.set(-0.022, 0.08, 0);
    b.position.set(0.022, 0.08, 0);
    b.rotation.y = Math.PI / 2;
    g.add(a, b);
  } else {
    const pot = new Mesh(new CylinderGeometry(0.04, 0.03, 0.04, 20), new MeshStandardMaterial({ color: 0xefe6d6 }));
    pot.position.y = 0.04;
    const trunk = new Mesh(new CylinderGeometry(0.006, 0.009, 0.08, 8), new MeshStandardMaterial({ color: 0x6b4a2b }));
    trunk.position.y = 0.1;
    g.add(pot, trunk);
    const blossoms = Math.max(1, Math.round((award.progress / award.target) * 9));
    for (let i = 0; i < 9; i++) {
      const a = (i / 9) * Math.PI * 2;
      const leaf = new Mesh(new SphereGeometry(0.018, 10, 8), new MeshStandardMaterial({ color: 0x6f9a5c }));
      leaf.position.set(Math.cos(a) * 0.035, 0.15 + Math.sin(i * 2.1) * 0.012, Math.sin(a) * 0.03);
      g.add(leaf);
      if (i < blossoms) {
        const b = new Mesh(new SphereGeometry(0.008, 8, 6), new MeshStandardMaterial({ color: 0xf6e4d0, emissive: new Color(0x5a4030) }));
        b.position.copy(leaf.position).add({ x: 0.006, y: 0.012, z: 0.01 } as never);
        g.add(b);
      }
    }
  }
  if (dim) g.traverse((o) => {
    const m = (o as Mesh).material as MeshStandardMaterial | undefined;
    if (m && !Array.isArray(m)) {
      m.transparent = true;
      m.opacity = 0.55;
    }
  });
  return g;
}

export function infoCard(widthM: number, heightM: number, title: string, lines: string[], footer?: string): CanvasPanel {
  const panel = new CanvasPanel(widthM, heightM);
  panel.draw((ctx, W, H) => {
    ctx.fillStyle = 'rgba(247,242,234,0.97)';
    roundRect(ctx, 0, 0, W, H, 48);
    ctx.fill();
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
