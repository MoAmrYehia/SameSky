// The shared lounge: an intimate seated room around one round walnut table, a sunset window, upholstered
// seating, a lamp, plants and ceramics. Furniture and surfaces come from CC0 Poly Haven assets (see
// docs/ASSET_MANIFEST.md); a few pieces (lamp, window frame, shelf) are authored here with the same PBR
// textures. Static pieces get soft contact shading; one bounded shadow light handles moving objects.
//
// World layout (metres): the viewer sits at the origin facing -Z. Everything that people place lives in
// the table's own coordinate frame (TABLE), so serialized object positions are independent of visuals.

import {
  BackSide, Box3, BoxGeometry, CanvasTexture, CircleGeometry, Color, CylinderGeometry, DirectionalLight, Group,
  HemisphereLight, LatheGeometry, type LoadingManager, Mesh, MeshBasicMaterial, MeshPhysicalMaterial, MeshStandardMaterial, type Object3D,
  PlaneGeometry, PointLight, Raycaster, RepeatWrapping, RingGeometry, SRGBColorSpace, type Texture, TextureLoader, Vector2, Vector3,
} from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { MeshoptDecoder } from 'three/examples/jsm/libs/meshopt_decoder.module.js';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';
import { WALNUT_TINT } from '../shared/keepsakes3d';

/** Table frame: centre of the table top. y is filled in from the measured model once it loads. */
export const TABLE = { x: 0, y: 0.76, z: -0.8, radius: 0.54 };

/**
 * The partner's armchair, in table-relative coordinates: across and to the right of the viewer, turned to
 * face the table centre, pushed in so its low arms tuck under the table edge. The seat the character sits
 * on is measured from the model at load time (see `seat` in RoomEnvironment).
 */
export const PARTNER_CHAIR = { distance: 0.84, angle: (30 * Math.PI) / 180 };

export interface RoomEnvironment {
  root: Group;
  table: Group;
  partnerChair: Object3D;
  /** Pelvis rest point on the measured seat cushion, table space; yaw 0 faces +Z (the viewer). */
  seat: { x: number; y: number; z: number; yaw: number };
  sideTable: Group;
  /** Height of the side table's top (its group origin is on the floor). */
  sideTop: number;
  lamp: PointLight;
  sun: DirectionalLight;
  dispose(): void;
}

type PbrKey = 'map' | 'normalMap' | 'roughnessMap' | 'aoMap';
const PBR_FILES: Record<PbrKey, [string, boolean]> = { map: ['color.webp', true], normalMap: ['normal.webp', false], roughnessMap: ['rough.webp', false], aoMap: ['ao.webp', false] };

/** Tiling PBR set (CC0, see ASSET_MANIFEST). Only the requested maps are downloaded; colour is sRGB, data maps linear. */
function pbr(loader: TextureLoader, name: string, repeat: number, keys: PbrKey[] = ['map', 'normalMap', 'roughnessMap', 'aoMap']) {
  const out: Partial<Record<PbrKey, Texture>> = {};
  for (const key of keys) {
    const [file, srgb] = PBR_FILES[key];
    const t = loader.load(`/assets/textures/${name}/${file}`);
    t.wrapS = t.wrapT = RepeatWrapping;
    t.repeat.set(repeat, repeat);
    t.anisotropy = 4;
    if (srgb) t.colorSpace = SRGBColorSpace;
    out[key] = t;
  }
  return out;
}

/** Soft radial contact shadow: a cheap stand-in for baked ambient occlusion where things meet the floor or table. */
let contactTexture: Texture | null = null;
export function contactShadow(width: number, depth: number, opacity = 0.55): Mesh {
  if (!contactTexture) {
    const c = document.createElement('canvas');
    c.width = c.height = 128;
    const ctx = c.getContext('2d')!;
    const g = ctx.createRadialGradient(64, 64, 0, 64, 64, 64);
    g.addColorStop(0, 'rgba(0,0,0,0.85)');
    g.addColorStop(0.45, 'rgba(0,0,0,0.45)');
    g.addColorStop(1, 'rgba(0,0,0,0)');
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, 128, 128);
    contactTexture = new CanvasTexture(c);
  }
  const m = new Mesh(new PlaneGeometry(width, depth), new MeshBasicMaterial({ map: contactTexture, transparent: true, opacity, depthWrite: false, toneMapped: false }));
  m.rotation.x = -Math.PI / 2;
  m.renderOrder = 1;
  return m;
}

/** Load a CC0 prop. Static decor is grounded by contact shadows; only `casts` props join the real-time shadow pass. */
async function loadModel(loader: GLTFLoader, url: string, casts = false) {
  const gltf = await loader.loadAsync(url);
  gltf.scene.traverse((o) => {
    const m = o as Mesh;
    if (m.isMesh) {
      m.castShadow = casts;
      m.receiveShadow = true;
    }
  });
  return gltf.scene;
}

/** Scale a model uniformly so its footprint matches `width`, and stand it on y = 0 centred on the origin. */
function fit(model: Object3D, width: number) {
  const box = new Box3().setFromObject(model);
  const size = box.getSize(new Vector3());
  const s = width / Math.max(size.x, size.z);
  model.scale.setScalar(s);
  const box2 = new Box3().setFromObject(model);
  const c = box2.getCenter(new Vector3());
  model.position.set(-c.x, -box2.min.y, -c.z);
  const holder = new Group();
  holder.add(model);
  return { holder, height: box2.max.y - box2.min.y };
}

function mushroomLamp(walnut: ReturnType<typeof pbr>) {
  const g = new Group();
  const base = new Mesh(new LatheGeometry([new Vector2(0, 0), new Vector2(0.075, 0), new Vector2(0.08, 0.012), new Vector2(0.05, 0.03), new Vector2(0.022, 0.05), new Vector2(0.018, 0.3), new Vector2(0, 0.3)], 40),
    new MeshPhysicalMaterial({ ...walnut, color: WALNUT_TINT, roughness: 0.8, clearcoat: 0.3, clearcoatRoughness: 0.35 }));
  const shade = new Mesh(new LatheGeometry([new Vector2(0.0, 0.42), new Vector2(0.06, 0.415), new Vector2(0.12, 0.39), new Vector2(0.155, 0.35), new Vector2(0.165, 0.315), new Vector2(0.16, 0.3), new Vector2(0.0, 0.3)], 48),
    new MeshPhysicalMaterial({ color: '#fff3df', emissive: new Color('#ffcf8f'), emissiveIntensity: 1.35, roughness: 0.6, transmission: 0, sheen: 0.4 }));
  g.add(base, shade);
  return g;
}

export async function buildEnvironment(manager?: LoadingManager): Promise<RoomEnvironment> {
  const root = new Group();
  root.name = 'environment';
  const tex = new TextureLoader(manager);
  const gltf = new GLTFLoader(manager);
  gltf.setMeshoptDecoder(MeshoptDecoder);
  const disposables: { dispose(): void }[] = [];

  // --- light: warm interior IBL (set by main.ts through IWSDK's IBLTexture), sun from the window, lamp ---
  const hemi = new HemisphereLight(0xfff4ea, 0x4a3a2c, 0.28);
  root.add(hemi);
  const sun = new DirectionalLight(0xffdcb8, 1.55);
  sun.position.set(-1.6, 2.6, -3.2);
  sun.target.position.set(TABLE.x, TABLE.y, TABLE.z);
  sun.castShadow = true;
  sun.shadow.mapSize.set(1024, 1024);
  sun.shadow.camera.left = -1.1;
  sun.shadow.camera.right = 1.1;
  sun.shadow.camera.top = 1.1;
  sun.shadow.camera.bottom = -1.1;
  sun.shadow.camera.near = 1;
  sun.shadow.camera.far = 7;
  sun.shadow.bias = -0.0004;
  sun.shadow.normalBias = 0.02;
  sun.shadow.radius = 4;
  root.add(sun, sun.target);
  const lamp = new PointLight(0xffc27a, 1.6, 4.5, 1.8);
  root.add(lamp);

  // --- room shell: one curved plaster wall around the table with a wide window arc behind it ---
  const WALL_R = 3.0;
  const WINDOW_ARC = (2.4 * Math.PI) / 3; // 144 degrees of glazing behind the table
  const parquet = pbr(tex, 'parquet', 3, ['map', 'normalMap', 'aoMap']);
  const floorMat = new MeshStandardMaterial({ ...parquet, roughness: 0.78, color: '#d6bfa6' });
  const floor = new Mesh(new CircleGeometry(WALL_R + 0.05, 64), floorMat);
  floor.rotation.x = -Math.PI / 2;
  floor.position.set(0, 0, TABLE.z);
  floor.receiveShadow = true;
  root.add(floor);
  const boucle = pbr(tex, 'boucle', 9, ['normalMap', 'aoMap']);
  // a cream knitted rug: the boucle relief (normal + AO) without its multicolour albedo
  const rug = new Mesh(new CircleGeometry(1.55, 64), new MeshStandardMaterial({ ...boucle, color: '#e9dfd0', roughness: 1 }));
  rug.rotation.x = -Math.PI / 2;
  rug.position.set(TABLE.x, 0.004, TABLE.z - 0.1);
  rug.receiveShadow = true;
  root.add(rug);
  const plaster = pbr(tex, 'plaster', 3);
  const wallMat = new MeshStandardMaterial({ ...plaster, color: '#f0e3d2', roughness: 1, side: BackSide });
  const walls = new Mesh(new CylinderGeometry(WALL_R, WALL_R, 3.2, 72, 1, true, Math.PI + WINDOW_ARC / 2, Math.PI * 2 - WINDOW_ARC), wallMat);
  walls.position.set(0, 1.6, TABLE.z);
  root.add(walls);
  const ceiling = new Mesh(new CircleGeometry(WALL_R, 48), new MeshStandardMaterial({ color: '#e6d6c3', roughness: 1 }));
  ceiling.rotation.x = Math.PI / 2;
  ceiling.position.set(0, 3.2, TABLE.z);
  root.add(ceiling);

  // --- window: a correctly projected cylindrical skyline plate, framed by a walnut-dark window wall ---
  const plate = tex.load('/assets/sample/window-skyline.jpg');
  plate.colorSpace = SRGBColorSpace;
  const R = 9;
  const latTop = Math.tan((30 * Math.PI) / 180);
  const latBottom = Math.tan((-14 * Math.PI) / 180);
  const eye = 1.2;
  const span = (200 * Math.PI) / 180;
  const view = new Mesh(new CylinderGeometry(R, R, R * (latTop - latBottom), 96, 1, true, Math.PI - span / 2 - 0.35, span), new MeshBasicMaterial({ map: plate, side: BackSide, toneMapped: false }));
  view.position.set(0, eye + (R * (latTop + latBottom)) / 2, TABLE.z);
  view.scale.x = -1; // seen from inside: mirror so the plate reads left-to-right
  root.add(view);
  // window opening: wall below the sill and above the head, mullions in between
  const frameMat = new MeshStandardMaterial({ color: '#2b211b', roughness: 0.55 });
  const sillMat = new MeshStandardMaterial({ ...plaster, color: '#e8d8c4', roughness: 1 });
  const arc = WINDOW_ARC;
  const wallR = WALL_R;
  const lower = new Mesh(new CylinderGeometry(wallR, wallR, 0.62, 48, 1, true, Math.PI - arc / 2, arc), sillMat);
  lower.material.side = BackSide;
  lower.position.set(0, 0.31, TABLE.z);
  const upper = new Mesh(new CylinderGeometry(wallR, wallR, 0.5, 48, 1, true, Math.PI - arc / 2, arc), sillMat);
  upper.material.side = BackSide;
  upper.position.set(0, 2.95, TABLE.z);
  root.add(lower, upper);
  // the sill: a flat ring sector (RingGeometry angle phi = cylinder theta - 90 degrees once laid flat) and its front lip
  const sillTopMat = new MeshStandardMaterial({ color: '#f3eadc', roughness: 0.7 });
  const sill = new Mesh(new RingGeometry(wallR - 0.18, wallR, 64, 1, Math.PI / 2 - arc / 2, arc), sillTopMat);
  sill.rotation.x = -Math.PI / 2;
  sill.position.set(0, 0.64, TABLE.z);
  sill.receiveShadow = true;
  const lip = new Mesh(new CylinderGeometry(wallR - 0.18, wallR - 0.18, 0.04, 64, 1, true, Math.PI - arc / 2, arc), sillTopMat);
  lip.material.side = BackSide;
  lip.position.set(0, 0.62, TABLE.z);
  root.add(sill, lip);
  // seven mullions baked into one geometry (one draw call)
  const mullions: BoxGeometry[] = [];
  for (let i = 0; i <= 6; i++) {
    const a = Math.PI - arc / 2 + (arc * i) / 6;
    mullions.push(new BoxGeometry(0.05, 2.1, 0.07).rotateY(a).translate(Math.sin(a) * (wallR - 0.03), 1.67, TABLE.z + Math.cos(a) * (wallR - 0.03)) as BoxGeometry);
  }
  root.add(new Mesh(mergeGeometries(mullions)!, frameMat));
  mullions.forEach((m) => m.dispose());
  const transom = new Mesh(new CylinderGeometry(wallR - 0.03, wallR - 0.03, 0.05, 48, 1, true, Math.PI - arc / 2, arc), frameMat);
  transom.material = frameMat;
  transom.position.set(0, 2.7, TABLE.z);
  root.add(transom);

  // --- the shared table (CC0 model) ---
  const tableModel = await loadModel(gltf, '/assets/models/props/table.glb', true);
  const tableFit = fit(tableModel, TABLE.radius * 2);
  TABLE.y = tableFit.height;
  const table = new Group();
  table.name = 'table';
  table.position.set(TABLE.x, TABLE.y, TABLE.z);
  tableFit.holder.position.y = -TABLE.y;
  table.add(tableFit.holder);
  const underTable = contactShadow(1.5, 1.5, 0.5);
  underTable.position.set(TABLE.x, 0.007, TABLE.z);
  root.add(underTable, table);

  // --- partner's upholstered armchair (CC0 model, real-world scale), facing the table centre ---
  const chairModel = await loadModel(gltf, '/assets/models/props/armchair.glb');
  const chairFit = fit(chairModel, 0.85);
  const partnerChair = chairFit.holder;
  const cx = Math.sin(PARTNER_CHAIR.angle) * PARTNER_CHAIR.distance;
  const cz = -Math.cos(PARTNER_CHAIR.angle) * PARTNER_CHAIR.distance;
  const chairYaw = Math.atan2(-cx, -cz); // the model's front (+Z) points at the table centre
  partnerChair.position.set(TABLE.x + cx, 0, TABLE.z + cz);
  partnerChair.rotation.y = chairYaw;
  const chairShadow = contactShadow(1.15, 1.15, 0.6);
  chairShadow.position.set(partnerChair.position.x, 0.008, partnerChair.position.z);
  // a sage boucle cushion against the backrest makes the seat read as a soft, lived-in place
  const cushion = new Mesh(new BoxGeometry(0.36, 0.3, 0.1, 6, 6, 2), new MeshStandardMaterial({ ...pbr(tex, 'boucle', 2, ['normalMap', 'aoMap']), color: '#7f9b84', roughness: 1 }));
  roundBox(cushion);
  cushion.position.set(0.2, 0.66, -0.25);
  cushion.rotation.set(-0.2, -0.35, -0.1);
  partnerChair.add(cushion);
  root.add(chairShadow, partnerChair);
  // measure the seat cushion under the pelvis with one downward ray (no hand-tuned seat height)
  root.updateMatrixWorld(true);
  const seatLocal = new Vector3(0, 2, -0.06);
  const probe = partnerChair.localToWorld(seatLocal.clone());
  const hit = new Raycaster(probe, new Vector3(0, -1, 0), 0, 3).intersectObject(chairModel, true)[0];
  const seatY = hit ? Math.min(0.56, Math.max(0.38, hit.point.y)) : 0.48;
  const seat = { x: probe.x - TABLE.x, y: seatY - TABLE.y, z: probe.z - TABLE.z, yaw: chairYaw };

  // --- side table with the lamp, a ceramic and a plant (CC0 models) ---
  const sideModel = await loadModel(gltf, '/assets/models/props/side-table.glb');
  const sideFit = fit(sideModel, 0.52);
  const sideTable = new Group();
  sideTable.position.set(1.12, 0, -1.0);
  sideTable.add(sideFit.holder);
  const sideShadow = contactShadow(0.8, 0.8, 0.5);
  sideShadow.position.set(1.12, 0.008, -1.0);
  const walnut = pbr(tex, 'walnut', 1);
  const lampGroup = mushroomLamp(walnut);
  lampGroup.position.set(0.06, sideFit.height, -0.08);
  sideTable.add(lampGroup);
  lamp.position.set(sideTable.position.x + 0.06, sideFit.height + 0.36, sideTable.position.z - 0.08);
  const vase = await loadModel(gltf, '/assets/models/props/vase.glb');
  const vaseFit = fit(vase, 0.13);
  vaseFit.holder.position.set(-0.15, sideFit.height, -0.06);
  sideTable.add(vaseFit.holder);
  root.add(sideShadow, sideTable);
  // two potted plants standing on the window sill
  const plant = await loadModel(gltf, '/assets/models/props/plant.glb');
  const onSill = (theta: number) => [Math.sin(theta) * (wallR - 0.09), 0.64, TABLE.z + Math.cos(theta) * (wallR - 0.09)] as const;
  const plantFit = fit(plant, 0.3);
  plantFit.holder.position.set(...onSill(Math.PI + 0.42));
  root.add(plantFit.holder);
  const plant2 = plant.clone(true);
  const plant2Fit = fit(plant2, 0.24);
  plant2Fit.holder.position.set(...onSill(Math.PI - 0.55));
  plant2Fit.holder.rotation.y = 1.1;
  root.add(plant2Fit.holder);

  disposables.push(floor.geometry, rug.geometry, walls.geometry, view.geometry, plate);
  return {
    root,
    table,
    partnerChair,
    seat,
    sideTable,
    sideTop: sideFit.height,
    lamp,
    sun,
    dispose() {
      root.traverse((o) => {
        const m = o as Mesh;
        m.geometry?.dispose?.();
        const mat = m.material as MeshStandardMaterial | MeshStandardMaterial[] | undefined;
        (Array.isArray(mat) ? mat : mat ? [mat] : []).forEach((x) => {
          for (const key of ['map', 'normalMap', 'roughnessMap', 'aoMap', 'emissiveMap'] as const) (x[key] as Texture | null)?.dispose();
          x.dispose();
        });
      });
      disposables.forEach((d) => d.dispose());
    },
  };
}

/** Pull a subdivided box's vertices toward a rounded "pillow" shape. */
function roundBox(mesh: Mesh) {
  const pos = mesh.geometry.attributes.position;
  const v = new Vector3();
  for (let i = 0; i < pos.count; i++) {
    v.fromBufferAttribute(pos, i);
    const nx = v.x / 0.18;
    const ny = v.y / 0.15;
    const bulge = (1 - nx * nx) * (1 - ny * ny);
    v.z *= 0.35 + 0.65 * Math.max(0, bulge);
    v.x *= 1 - 0.08 * ny * ny;
    v.y *= 1 - 0.08 * nx * nx;
    pos.setXYZ(i, v.x, v.y, v.z);
  }
  mesh.geometry.computeVertexNormals();
}

