// The three shared-milestone keepsakes as real 3D objects. Used on the room's keepsake shelf and rendered
// (same models, consistent light rig) into the companion's keepsake cards by src/shared/portrait.ts.

import {
  BoxGeometry, type BufferGeometry, CatmullRomCurve3, Color, CylinderGeometry, Group, IcosahedronGeometry, InstancedMesh, LatheGeometry, Matrix4,
  Mesh, MeshPhysicalMaterial, MeshStandardMaterial, PlaneGeometry, Quaternion, RepeatWrapping, SRGBColorSpace, type Texture, TextureLoader,
  TorusGeometry, TubeGeometry, Vector2, Vector3,
} from 'three';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';

export type KeepsakeKind = 'frame' | 'linked_sculptures' | 'memory_tree';

const loader = new TextureLoader();
const cache = new Map<string, Texture>();
const pending = new Set<Promise<unknown>>();

/** Resolves once every texture requested so far has loaded (or failed). */
export async function texturesReady(): Promise<void> {
  while (pending.size) await Promise.allSettled([...pending]);
}

export function tex(url: string, srgb = true, repeat = 1): Texture {
  const key = `${url}|${repeat}`;
  let t = cache.get(key);
  if (!t) {
    let done: () => void = () => undefined;
    const ready = new Promise<void>((res) => (done = res));
    pending.add(ready);
    ready.finally(() => pending.delete(ready));
    t = loader.load(url, () => done(), undefined, () => done());
    if (srgb) t.colorSpace = SRGBColorSpace;
    if (repeat !== 1) {
      t.wrapS = t.wrapT = RepeatWrapping;
      t.repeat.set(repeat, repeat);
    }
    t.anisotropy = 4;
    cache.set(key, t);
  }
  return t;
}

/**
 * The CC0 veneer scan (Poly Haven american_walnut_veneer) is a grey-washed finish; this tint multiplies its
 * grain back to a warm walnut brown (texture ~0.16 linear grey -> ~#66432a).
 */
export const WALNUT_TINT = '#e8a066';

export function walnutMaterial(repeat = 1) {
  return new MeshPhysicalMaterial({
    color: WALNUT_TINT,
    map: tex('/assets/textures/walnut/color.webp', true, repeat),
    normalMap: tex('/assets/textures/walnut/normal.webp', false, repeat),
    roughnessMap: tex('/assets/textures/walnut/rough.webp', false, repeat),
    roughness: 1,
    clearcoat: 0.22,
    clearcoatRoughness: 0.45,
  });
}

function plinth() {
  const g = new Group();
  const base = new Mesh(new CylinderGeometry(0.07, 0.074, 0.022, 48), walnutMaterial());
  base.position.y = 0.011;
  base.castShadow = base.receiveShadow = true;
  g.add(base);
  return g;
}

export function buildKeepsake(kind: KeepsakeKind | string, progress = 1, target = 1, earned = true): Group {
  const g = new Group();
  g.name = `keepsake-${kind}`;
  if (kind === 'frame') {
    // a framed print of the first shared day, standing on its own easel back
    const w = 0.15;
    const h = 0.11;
    const frame = new Group();
    const border = 0.014;
    // all walnut parts (four bars and the easel back) in one geometry: one draw call
    const lean = new Matrix4().compose(new Vector3(0, h / 2 + 0.02, 0), new Quaternion().setFromAxisAngle(new Vector3(1, 0, 0), -0.12), new Vector3(1, 1, 1));
    const parts: BufferGeometry[] = [[0, h / 2 - border / 2, w, border], [0, -h / 2 + border / 2, w, border], [-w / 2 + border / 2, 0, border, h], [w / 2 - border / 2, 0, border, h]]
      .map(([x, y, bw, bh]) => new BoxGeometry(bw, bh, 0.014).translate(x, y, 0).applyMatrix4(lean));
    parts.push(new BoxGeometry(0.02, h * 0.8, 0.006).rotateX(0.45).translate(0, h * 0.42 + 0.02, -0.04));
    const wood = new Mesh(mergeGeometries(parts)!, walnutMaterial());
    parts.forEach((p) => p.dispose());
    wood.castShadow = true;
    const mat = new Mesh(new PlaneGeometry(w - border * 2, h - border * 2), new MeshStandardMaterial({ color: '#f4ede0', roughness: 0.9 }));
    mat.position.z = 0.002;
    const print = new Mesh(new PlaneGeometry(w - border * 2 - 0.016, h - border * 2 - 0.016), new MeshStandardMaterial({ map: tex('/assets/sample/adam-waterfront-thumb.jpg'), roughness: 0.45 }));
    print.position.z = 0.003;
    frame.add(mat, print);
    frame.position.y = h / 2 + 0.02;
    frame.rotation.x = -0.12;
    g.add(plinth(), wood, frame);
  } else if (kind === 'linked_sculptures') {
    // two interlocking glazed ceramic rings
    const glazeA = new MeshPhysicalMaterial({ color: '#f1e8da', roughness: 0.28, clearcoat: 1, clearcoatRoughness: 0.12, sheen: 0.2 });
    const glazeB = new MeshPhysicalMaterial({ color: '#dccdb6', roughness: 0.32, clearcoat: 1, clearcoatRoughness: 0.14 });
    const a = new Mesh(new TorusGeometry(0.042, 0.015, 32, 96), glazeA);
    const b = new Mesh(new TorusGeometry(0.042, 0.015, 32, 96), glazeB);
    // two rings linked at right angles, turned so both read as rings (not edge-on) from a seated view
    a.position.set(-0.021, 0.078, 0);
    a.rotation.set(0.12, -0.55, 0.18);
    b.position.set(0.021, 0.078, 0);
    b.rotation.set(0.12, Math.PI / 2 - 0.55, -0.12);
    a.castShadow = b.castShadow = true;
    g.add(plinth(), a, b);
  } else {
    // a small memory tree that blossoms as shared days accumulate (never shrinks for missed days)
    const pot = new Mesh(
      new LatheGeometry([new Vector2(0.0, 0.0), new Vector2(0.034, 0.0), new Vector2(0.042, 0.012), new Vector2(0.046, 0.036), new Vector2(0.044, 0.042), new Vector2(0.038, 0.04), new Vector2(0.0, 0.038)], 40),
      new MeshPhysicalMaterial({ color: '#eee6d8', roughness: 0.35, clearcoat: 0.6 }),
    );
    pot.position.y = 0.022;
    pot.castShadow = true;
    const bark = new MeshStandardMaterial({ color: '#6b4a2f', roughness: 0.9 });
    const branch = (pts: [number, number, number][], r: number) => new TubeGeometry(new CatmullRomCurve3(pts.map((p) => new Vector3(...p))), 20, r, 8, false);
    const limbs = [
      branch([[0, 0.058, 0], [0.006, 0.09, 0.002], [-0.008, 0.12, 0], [0.002, 0.15, 0.004]], 0.0065),
      branch([[-0.004, 0.11, 0], [-0.03, 0.13, 0.01], [-0.05, 0.14, 0.012]], 0.0035),
      branch([[0.0, 0.125, 0.002], [0.028, 0.148, -0.006], [0.048, 0.155, -0.004]], 0.0032),
    ];
    const wood = new Mesh(mergeGeometries(limbs)!, bark); // trunk and branches: one draw call
    limbs.forEach((l) => l.dispose());
    wood.castShadow = true;
    g.add(pot, wood);
    const leaves = new MeshStandardMaterial({ color: '#5f8a4f', roughness: 0.75 });
    const clusters: [number, number, number, number][] = [[0.002, 0.168, 0.002, 0.034], [-0.048, 0.152, 0.012, 0.026], [0.05, 0.166, -0.004, 0.026], [-0.022, 0.178, -0.012, 0.022], [0.024, 0.182, 0.012, 0.022]];
    const leafGeo = new IcosahedronGeometry(1, 2);
    const count = 34;
    const inst = new InstancedMesh(leafGeo, leaves, clusters.length * count);
    let k = 0;
    const m = new Matrix4();
    let seed = 11;
    const rand = () => ((seed = (seed * 16807) % 2147483647) / 2147483647);
    for (const [x, y, z, r] of clusters) {
      for (let i = 0; i < count; i++) {
        const dir = new Vector3(rand() - 0.5, (rand() - 0.3) * 0.8, rand() - 0.5).normalize();
        const p = new Vector3(x, y, z).addScaledVector(dir, r * (0.45 + rand() * 0.5));
        const s = r * (0.22 + rand() * 0.14);
        m.compose(p, new Quaternion().setFromAxisAngle(dir, rand() * 6), new Vector3(s, s * 0.8, s));
        inst.setMatrixAt(k++, m);
        inst.setColorAt(k - 1, new Color('#5f8a4f').offsetHSL((rand() - 0.5) * 0.04, 0, (rand() - 0.5) * 0.1));
      }
    }
    inst.castShadow = true;
    g.add(inst);
    // blossoms grow with shared days (never shrink for missed days); instanced: one draw call
    const blossoms = Math.max(1, Math.round((Math.min(progress, target) / target) * 12));
    const petal = new MeshPhysicalMaterial({ color: '#f8e7dc', roughness: 0.5, sheen: 0.6, sheenColor: new Color('#ffd6c4') });
    const bloom = new InstancedMesh(new IcosahedronGeometry(0.0062, 1), petal, blossoms);
    for (let i = 0; i < blossoms; i++) {
      const [x, y, z, r] = clusters[i % clusters.length];
      const a = i * 2.39996;
      m.makeTranslation(x + Math.cos(a) * r * 0.75, y + r * 0.55 + Math.sin(i) * 0.004, z + Math.sin(a) * r * 0.75);
      bloom.setMatrixAt(i, m);
    }
    g.add(bloom);
    g.add(plinth());
  }
  if (!earned) {
    g.traverse((o) => {
      const mesh = o as Mesh;
      const mat = mesh.material as MeshStandardMaterial | undefined;
      if (mat && 'opacity' in mat && kind !== 'memory_tree') {
        mat.transparent = true;
        mat.opacity = 0.55;
      }
    });
  }
  return g;
}
