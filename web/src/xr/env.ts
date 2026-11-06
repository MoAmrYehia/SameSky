// The shared room: a warm, seated space around one round table. Everything is procedural or uses the
// sample's own generated imagery, so it loads fast and stays light enough for Quest's frame budget.

import {
  BackSide, BoxGeometry, CanvasTexture, CircleGeometry, Color, ConeGeometry, CylinderGeometry, DirectionalLight, DoubleSide, Group,
  HemisphereLight, Mesh, MeshBasicMaterial, MeshStandardMaterial, PointLight, RepeatWrapping, SphereGeometry, SRGBColorSpace,
  TextureLoader, TorusGeometry,
} from 'three';

export const TABLE = { x: 0, y: 0.72, z: -0.78, radius: 0.55 };

function canvasTexture(w: number, h: number, draw: (ctx: CanvasRenderingContext2D) => void) {
  const c = document.createElement('canvas');
  c.width = w;
  c.height = h;
  draw(c.getContext('2d')!);
  const t = new CanvasTexture(c);
  t.colorSpace = SRGBColorSpace;
  return t;
}

function woodTexture(rings: boolean) {
  return canvasTexture(512, 512, (ctx) => {
    ctx.fillStyle = '#8a5a34';
    ctx.fillRect(0, 0, 512, 512);
    if (rings) {
      for (let r = 250; r > 0; r -= 9) {
        ctx.strokeStyle = `rgba(${90 + (r % 27)}, ${54 + (r % 13)}, 30, ${0.18 + ((r * 7) % 10) / 60})`;
        ctx.lineWidth = 3 + (r % 5);
        ctx.beginPath();
        ctx.ellipse(256, 256, r, r * 0.97, 0, 0, Math.PI * 2);
        ctx.stroke();
      }
    } else {
      for (let x = 0; x < 512; x += 64) {
        ctx.fillStyle = x % 128 ? '#7d5130' : '#86583a';
        ctx.fillRect(x, 0, 62, 512);
        ctx.fillStyle = 'rgba(0,0,0,.25)';
        ctx.fillRect(x + 62, 0, 2, 512);
      }
      for (let i = 0; i < 400; i++) {
        ctx.strokeStyle = `rgba(60,36,20,${Math.random() * 0.15})`;
        ctx.beginPath();
        const x = Math.random() * 512;
        ctx.moveTo(x, Math.random() * 512);
        ctx.lineTo(x + Math.random() * 4 - 2, Math.random() * 512);
        ctx.stroke();
      }
    }
  });
}

export interface RoomEnvironment {
  root: Group;
  table: Group;
  windowView: Mesh;
  lamp: PointLight;
}

export function buildEnvironment(): RoomEnvironment {
  const root = new Group();
  root.name = 'environment';

  // Lighting: warm evening.
  root.add(new HemisphereLight(0xffe4c4, 0x3a2a20, 1.35));
  const sun = new DirectionalLight(0xffb37a, 1.1);
  sun.position.set(-1.5, 3, -4);
  root.add(sun);
  const lamp = new PointLight(0xffcf8a, 3.2, 5, 1.6);
  lamp.position.set(1.45, 1.35, -1.2);
  root.add(lamp);

  // Shell: a soft dark-warm dome so the edges of the world never look empty.
  const shell = new Mesh(
    new SphereGeometry(9, 32, 16),
    new MeshBasicMaterial({ side: BackSide, map: canvasTexture(16, 256, (ctx) => {
      const g = ctx.createLinearGradient(0, 0, 0, 256);
      g.addColorStop(0, '#3a2c3f');
      g.addColorStop(0.45, '#6b4636');
      g.addColorStop(0.6, '#2c221d');
      g.addColorStop(1, '#15110f');
      ctx.fillStyle = g;
      ctx.fillRect(0, 0, 16, 256);
    }) }),
  );
  root.add(shell);

  // Floor + rug.
  const floorTex = woodTexture(false);
  floorTex.wrapS = floorTex.wrapT = RepeatWrapping;
  floorTex.repeat.set(6, 6);
  const floor = new Mesh(new CircleGeometry(6, 48), new MeshStandardMaterial({ map: floorTex, roughness: 0.85 }));
  floor.rotation.x = -Math.PI / 2;
  root.add(floor);
  const rug = new Mesh(new CircleGeometry(1.5, 48), new MeshStandardMaterial({
    roughness: 1,
    map: canvasTexture(512, 512, (ctx) => {
      ctx.fillStyle = '#d9c7ad';
      ctx.fillRect(0, 0, 512, 512);
      ctx.strokeStyle = '#bfa78a';
      for (let r = 40; r < 256; r += 26) {
        ctx.lineWidth = 6;
        ctx.beginPath();
        ctx.arc(256, 256, r, 0, Math.PI * 2);
        ctx.stroke();
      }
    }),
  }));
  rug.rotation.x = -Math.PI / 2;
  rug.position.set(TABLE.x, 0.005, TABLE.z);
  root.add(rug);

  // Window: a curved panorama of the waterfront at golden hour (the sample's own generated image).
  const viewTex = new TextureLoader().load('/sample/adam-waterfront.jpg');
  viewTex.colorSpace = SRGBColorSpace;
  const windowView = new Mesh(
    new CylinderGeometry(3.6, 3.6, 2.6, 48, 1, true, Math.PI - 1.15, 2.3),
    new MeshBasicMaterial({ map: viewTex, side: BackSide, toneMapped: false }),
  );
  windowView.position.set(0, 1.75, -0.6);
  root.add(windowView);
  const frameMat = new MeshStandardMaterial({ color: 0x2a211c, roughness: 0.6 });
  for (let i = 0; i <= 6; i++) {
    const a = Math.PI - 1.15 + (2.3 * i) / 6;
    const mullion = new Mesh(new BoxGeometry(0.06, 2.7, 0.06), frameMat);
    mullion.position.set(Math.sin(a) * 3.55, 1.75, -0.6 + Math.cos(a) * 3.55);
    root.add(mullion);
  }
  const sill = new Mesh(new TorusGeometry(3.55, 0.05, 6, 48, 2.3), frameMat);
  sill.rotation.set(Math.PI / 2, 0, Math.PI / 2 - 1.15 + Math.PI / 2 + 0.0);
  sill.rotation.z = -Math.PI / 2 - 1.15 + Math.PI;
  sill.position.set(0, 0.45, -0.6);
  root.add(sill);

  // Table: round, warm wood, one pedestal.
  const table = new Group();
  table.position.set(TABLE.x, TABLE.y, TABLE.z);
  const wood = new MeshStandardMaterial({ map: woodTexture(true), roughness: 0.55, metalness: 0.02 });
  const top = new Mesh(new CylinderGeometry(TABLE.radius, TABLE.radius * 0.98, 0.04, 64), wood);
  top.position.y = -0.02;
  table.add(top);
  const leg = new Mesh(new CylinderGeometry(0.06, 0.08, TABLE.y - 0.06, 24), new MeshStandardMaterial({ color: 0x5c3b22, roughness: 0.6 }));
  leg.position.y = -(TABLE.y - 0.06) / 2 - 0.04;
  table.add(leg);
  const base = new Mesh(new CylinderGeometry(0.28, 0.3, 0.03, 32), new MeshStandardMaterial({ color: 0x4a2f1b }));
  base.position.y = -TABLE.y + 0.015;
  table.add(base);
  root.add(table);

  // Armchair across the table (where a seated avatar appears).
  const fabric = new MeshStandardMaterial({ color: 0xb9ab98, roughness: 1 });
  const chair = new Group();
  const seat = new Mesh(new BoxGeometry(0.62, 0.16, 0.56), fabric);
  seat.position.y = 0.42;
  const back = new Mesh(new BoxGeometry(0.66, 0.62, 0.16), fabric);
  back.position.set(0, 0.72, -0.24);
  const armL = new Mesh(new BoxGeometry(0.12, 0.3, 0.56), fabric);
  armL.position.set(-0.36, 0.52, 0);
  const armR = armL.clone();
  armR.position.x = 0.36;
  const cushion = new Mesh(new BoxGeometry(0.3, 0.28, 0.1), new MeshStandardMaterial({ color: 0x55705c, roughness: 1 }));
  cushion.position.set(0.18, 0.62, -0.14);
  cushion.rotation.z = 0.2;
  chair.add(seat, back, armL, armR, cushion);
  chair.position.set(TABLE.x + 0.32, 0, TABLE.z - 0.8);
  chair.rotation.y = -0.2;
  root.add(chair);

  // Lamp on a side table (right) and a plant (left back).
  const side = new Mesh(new CylinderGeometry(0.22, 0.22, 0.62, 24), new MeshStandardMaterial({ color: 0x6b4529 }));
  side.position.set(1.45, 0.31, -1.2);
  const shade = new Mesh(new SphereGeometry(0.17, 24, 16, 0, Math.PI * 2, 0, Math.PI * 0.55), new MeshStandardMaterial({ color: 0xfff1d6, emissive: new Color(0xffc477), emissiveIntensity: 1.4, side: DoubleSide }));
  shade.position.set(1.45, 0.92, -1.2);
  const stem = new Mesh(new CylinderGeometry(0.015, 0.015, 0.3, 8), new MeshStandardMaterial({ color: 0x3a2a1c }));
  stem.position.set(1.45, 0.77, -1.2);
  root.add(side, shade, stem);

  const plant = new Group();
  const pot = new Mesh(new CylinderGeometry(0.2, 0.15, 0.36, 24), new MeshStandardMaterial({ color: 0xe9e0cf, roughness: 0.7 }));
  pot.position.y = 0.18;
  plant.add(pot);
  const leafMat = new MeshStandardMaterial({ color: 0x4e7a45, roughness: 0.8 });
  for (let i = 0; i < 14; i++) {
    const leaf = new Mesh(new ConeGeometry(0.07, 0.42, 6), leafMat);
    const a = (i / 14) * Math.PI * 2;
    leaf.position.set(Math.cos(a) * 0.1, 0.52 + (i % 3) * 0.12, Math.sin(a) * 0.1);
    leaf.rotation.set(Math.sin(a) * 0.7, 0, -Math.cos(a) * 0.7);
    plant.add(leaf);
  }
  plant.position.set(-1.55, 0, -1.6);
  root.add(plant);

  const shelf = new Group();
  const shelfMat = new MeshStandardMaterial({ color: 0x5a3b24, roughness: 0.7 });
  for (let i = 0; i < 4; i++) {
    const board = new Mesh(new BoxGeometry(0.9, 0.03, 0.28), shelfMat);
    board.position.y = 0.3 + i * 0.42;
    shelf.add(board);
  }
  const colors = [0x8a3b3b, 0x2f4a6b, 0xc98f3a, 0xe9e0cf, 0x3f6b52, 0x5b4a7a];
  for (let row = 0; row < 3; row++) {
    for (let b = 0; b < 9; b++) {
      const book = new Mesh(new BoxGeometry(0.06, 0.24 + ((b * 7 + row) % 5) * 0.02, 0.2), new MeshStandardMaterial({ color: colors[(b + row) % colors.length], roughness: 0.9 }));
      book.position.set(-0.36 + b * 0.08, 0.3 + row * 0.42 + 0.14, 0);
      shelf.add(book);
    }
  }
  shelf.position.set(-2.05, 0, -0.6);
  shelf.rotation.y = Math.PI / 2.6;
  root.add(shelf);

  return { root, table, windowView, lamp };
}
