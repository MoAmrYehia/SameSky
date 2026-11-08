// Builds the skinned TEST MANNEQUIN used to verify the photo-avatar rendering path without anyone's likeness.
//
//   node scripts/build-test-rig.mjs   ->  tests/fixtures/test-mannequin.glb
//
// It is deliberately an artist's mannequin (ball joints, neutral matte finish), NOT a person and NOT
// photo-derived. What it shares with real provider exports is the contract the room and portrait renderer
// rely on: a single skinned mesh in metres, a standard humanoid skeleton (Mixamo-style names, as Avaturn /
// Ready-Player-Me style exports use), a standing A-pose bind pose facing +Z, and ARKit-named eye-blink
// morph targets. Original geometry, generated here; no third-party assets.

import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { Document, NodeIO } from '@gltf-transform/core';

const OUT = resolve(dirname(fileURLToPath(import.meta.url)), '../tests/fixtures/test-mannequin.glb');

// --- skeleton (world positions in metres, standing, arms 45 degrees down) -----------------------------
const A = Math.SQRT1_2;
const arm = (side) => {
  const s = side === 'Left' ? 1 : -1;
  const shoulder = [s * 0.19, 1.42, -0.01];
  const elbow = [shoulder[0] + s * A * 0.29, shoulder[1] - A * 0.29, -0.01];
  const wrist = [elbow[0] + s * A * 0.26, elbow[1] - A * 0.26, -0.01];
  return [
    { name: `${side}Shoulder`, parent: 'Spine2', pos: [s * 0.05, 1.42, -0.01] },
    { name: `${side}Arm`, parent: `${side}Shoulder`, pos: shoulder },
    { name: `${side}ForeArm`, parent: `${side}Arm`, pos: elbow },
    { name: `${side}Hand`, parent: `${side}ForeArm`, pos: wrist },
  ];
};
const leg = (side) => {
  const x = side === 'Left' ? 0.09 : -0.09;
  return [
    { name: `${side}UpLeg`, parent: 'Hips', pos: [x, 0.93, 0] },
    { name: `${side}Leg`, parent: `${side}UpLeg`, pos: [x, 0.51, 0.01] },
    { name: `${side}Foot`, parent: `${side}Leg`, pos: [x, 0.08, -0.01] },
    { name: `${side}ToeBase`, parent: `${side}Foot`, pos: [x, 0.02, 0.12] },
  ];
};
const JOINTS = [
  { name: 'Hips', parent: null, pos: [0, 0.98, 0] },
  { name: 'Spine', parent: 'Hips', pos: [0, 1.08, 0] },
  { name: 'Spine1', parent: 'Spine', pos: [0, 1.2, 0] },
  { name: 'Spine2', parent: 'Spine1', pos: [0, 1.32, 0] },
  { name: 'Neck', parent: 'Spine2', pos: [0, 1.47, 0] },
  { name: 'Head', parent: 'Neck', pos: [0, 1.56, 0.01] },
  { name: 'LeftEye', parent: 'Head', pos: [0.032, 1.628, 0.086] },
  { name: 'RightEye', parent: 'Head', pos: [-0.032, 1.628, 0.086] },
  ...arm('Left'), ...arm('Right'), ...leg('Left'), ...leg('Right'),
];
const J = Object.fromEntries(JOINTS.map((j, i) => [j.name, i]));
const P = (name) => JOINTS[J[name]].pos;

// --- geometry helpers -------------------------------------------------------------------------------
class Part {
  constructor() { this.pos = []; this.idx = []; this.joints = []; this.weights = []; this.color = []; }
  vertex(p, joint, joint2 = null, w2 = 0, color = null) {
    this.pos.push(...p);
    this.joints.push(J[joint], joint2 ? J[joint2] : 0, 0, 0);
    this.weights.push(1 - w2, w2, 0, 0);
    if (color) this.color.push(...color, 1);
    return this.pos.length / 3 - 1;
  }
}

/** Ellipsoid (UV sphere) at c with radii r; `weigh(p)` returns [joint, joint2, w2]. Optional filter keeps a cap. */
function ellipsoid(part, c, r, seg, weigh, keep = null, color = null) {
  const [sw, sh] = seg;
  const grid = [];
  for (let i = 0; i <= sh; i++) {
    const v = i / sh;
    const th = v * Math.PI;
    const row = [];
    for (let j = 0; j <= sw; j++) {
      const ph = (j / sw) * Math.PI * 2;
      const p = [c[0] + r[0] * Math.sin(th) * Math.sin(ph), c[1] + r[1] * Math.cos(th), c[2] + r[2] * Math.sin(th) * Math.cos(ph)];
      if (keep && !keep(p)) { row.push(-1); continue; }
      const [a, b, w] = weigh(p);
      row.push(part.vertex(p, a, b, w, typeof color === 'function' ? color(p) : color));
    }
    grid.push(row);
  }
  for (let i = 0; i < sh; i++) {
    for (let j = 0; j < sw; j++) {
      const a = grid[i][j], b = grid[i + 1][j], c2 = grid[i + 1][j + 1], d = grid[i][j + 1];
      if (a < 0 || b < 0 || c2 < 0 || d < 0) continue;
      part.idx.push(a, b, d, b, c2, d);
    }
  }
}

/** Capsule between points a and b with radii ra/rb (ball ends, like a mannequin's limbs). */
function capsule(part, a, b, ra, rb, joint, seg = [20, 12]) {
  const d = b.map((v, i) => v - a[i]);
  const len = Math.hypot(...d);
  const y = d.map((v) => v / len);
  const ref = Math.abs(y[1]) < 0.9 ? [0, 1, 0] : [1, 0, 0];
  const x = norm(cross(ref, y));
  const z = cross(y, x);
  const rows = [];
  const [sw, sh] = seg;
  const steps = [];
  for (let i = 0; i <= sh; i++) steps.push(['capA', (i / sh) * (Math.PI / 2)]);
  for (let i = 0; i <= sh; i++) steps.push(['capB', Math.PI / 2 + (i / sh) * (Math.PI / 2)]);
  for (const [cap, t] of steps) {
    const center = cap === 'capA' ? a : b;
    const r = cap === 'capA' ? ra : rb;
    const along = -Math.cos(t) * r;
    const ring = Math.sin(t) * r;
    const row = [];
    for (let j = 0; j <= sw; j++) {
      const ph = (j / sw) * Math.PI * 2;
      const p = [0, 1, 2].map((k) => center[k] + y[k] * along + (x[k] * Math.cos(ph) + z[k] * Math.sin(ph)) * ring);
      row.push(part.vertex(p, joint));
    }
    rows.push(row);
  }
  for (let i = 0; i < rows.length - 1; i++) for (let j = 0; j < sw; j++) part.idx.push(rows[i][j], rows[i + 1][j], rows[i][j + 1], rows[i + 1][j], rows[i + 1][j + 1], rows[i][j + 1]);
}
const cross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
const norm = (v) => { const l = Math.hypot(...v); return v.map((x) => x / l); };

function normals(pos, idx) {
  const n = new Float32Array(pos.length);
  for (let i = 0; i < idx.length; i += 3) {
    const [a, b, c] = [idx[i] * 3, idx[i + 1] * 3, idx[i + 2] * 3];
    const e1 = [pos[b] - pos[a], pos[b + 1] - pos[a + 1], pos[b + 2] - pos[a + 2]];
    const e2 = [pos[c] - pos[a], pos[c + 1] - pos[a + 1], pos[c + 2] - pos[a + 2]];
    const f = cross(e1, e2);
    for (const v of [a, b, c]) { n[v] += f[0]; n[v + 1] += f[1]; n[v + 2] += f[2]; }
  }
  for (let i = 0; i < n.length; i += 3) {
    const l = Math.hypot(n[i], n[i + 1], n[i + 2]) || 1;
    n[i] /= l; n[i + 1] /= l; n[i + 2] /= l;
  }
  return n;
}

// --- body ---------------------------------------------------------------------------------------------
const skin = new Part();
const top = new Part();
const trousers = new Part();
const shoes = new Part();
const hair = new Part();
const eyes = new Part();
const rigid = (joint) => () => [joint, null, 0];
const torsoWeights = (p) => {
  const yy = p[1];
  if (yy < 1.1) return ['Spine', 'Hips', Math.max(0, Math.min(1, (1.1 - yy) / 0.12))];
  if (yy < 1.26) return ['Spine1', 'Spine', Math.max(0, Math.min(1, (1.2 - yy) / 0.1))];
  return ['Spine2', 'Spine1', Math.max(0, Math.min(1, (1.32 - yy) / 0.08))];
};

ellipsoid(skin, [0, 1.625, 0.012], [0.078, 0.104, 0.094], [36, 24], rigid('Head'));
ellipsoid(skin, [0, 1.6, 0.098], [0.013, 0.022, 0.016], [10, 8], rigid('Head')); // nose
for (const s of [1, -1]) ellipsoid(skin, [s * 0.079, 1.62, 0.005], [0.012, 0.024, 0.016], [10, 8], rigid('Head')); // ears
ellipsoid(hair, [0, 1.64, -0.004], [0.084, 0.11, 0.1], [36, 24], rigid('Head'), (p) => p[1] > 1.6 - (p[2] < -0.02 ? 0.06 : 0) && !(p[2] > 0.06 && p[1] < 1.705));
capsule(skin, P('Neck'), [0, 1.57, 0.005], 0.047, 0.045, 'Neck');
ellipsoid(top, [0, 1.25, -0.005], [0.165, 0.24, 0.11], [36, 28], torsoWeights);
ellipsoid(trousers, [0, 0.96, 0], [0.155, 0.1, 0.11], [32, 16], rigid('Hips'));
for (const side of ['Left', 'Right']) {
  capsule(top, P(`${side}Arm`), P(`${side}ForeArm`), 0.052, 0.045, `${side}Arm`);
  capsule(top, P(`${side}ForeArm`), P(`${side}Hand`), 0.043, 0.036, `${side}ForeArm`);
  const w = P(`${side}Hand`);
  const s = side === 'Left' ? 1 : -1;
  ellipsoid(skin, [w[0] + s * A * 0.08, w[1] - A * 0.08, w[2]], [0.03, 0.08, 0.045], [16, 12], rigid(`${side}Hand`));
  capsule(trousers, P(`${side}UpLeg`), P(`${side}Leg`), 0.074, 0.058, `${side}UpLeg`);
  capsule(trousers, P(`${side}Leg`), P(`${side}Foot`), 0.056, 0.044, `${side}Leg`);
  const f = P(`${side}Foot`);
  ellipsoid(shoes, [f[0], 0.045, f[2] + 0.05], [0.046, 0.045, 0.12], [16, 12], rigid(`${side}Foot`));
}
const eyeColor = (c) => (p) => {
  const fwd = (p[2] - c[2]) / 0.0125;
  return fwd > 0.86 ? [0.04, 0.03, 0.03] : fwd > 0.6 ? [0.32, 0.22, 0.14] : [0.93, 0.92, 0.9];
};
const eyeStart = [];
for (const name of ['LeftEye', 'RightEye']) {
  eyeStart.push(eyes.pos.length / 3);
  const c = P(name);
  ellipsoid(eyes, c, [0.0125, 0.0125, 0.0125], [16, 12], rigid('Head'), null, eyeColor(c));
}

// --- document -----------------------------------------------------------------------------------------
const doc = new Document();
doc.getRoot().getAsset().generator = 'SameSky test mannequin (scripts/build-test-rig.mjs)';
const buffer = doc.createBuffer();
const scene = doc.createScene('Scene');
const nodes = JOINTS.map((j) => doc.createNode(j.name));
JOINTS.forEach((j, i) => {
  const parent = j.parent ? P(j.parent) : [0, 0, 0];
  nodes[i].setTranslation(j.pos.map((v, k) => v - parent[k]));
  if (j.parent) nodes[J[j.parent]].addChild(nodes[i]);
});
const armature = doc.createNode('Armature').addChild(nodes[0]);
const ibm = new Float32Array(JOINTS.length * 16);
JOINTS.forEach((j, i) => {
  const m = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, -j.pos[0], -j.pos[1], -j.pos[2], 1];
  ibm.set(m, i * 16);
});
const skinNode = doc.createSkin('MannequinSkin').setInverseBindMatrices(doc.createAccessor().setType('MAT4').setArray(ibm).setBuffer(buffer));
nodes.forEach((n) => skinNode.addJoint(n));
skinNode.setSkeleton(nodes[0]);

const material = (name, rgb, rough, vertexColors = false) =>
  doc.createMaterial(name).setBaseColorFactor([...rgb, 1]).setRoughnessFactor(rough).setMetallicFactor(0).setExtras(vertexColors ? { vertexColors: true } : {});
const mesh = doc.createMesh('Mannequin');
const addPrimitive = (part, mat, morphs = []) => {
  const position = new Float32Array(part.pos);
  const prim = doc.createPrimitive()
    .setAttribute('POSITION', doc.createAccessor().setType('VEC3').setArray(position).setBuffer(buffer))
    .setAttribute('NORMAL', doc.createAccessor().setType('VEC3').setArray(normals(part.pos, part.idx)).setBuffer(buffer))
    .setAttribute('JOINTS_0', doc.createAccessor().setType('VEC4').setArray(new Uint16Array(part.joints)).setBuffer(buffer))
    .setAttribute('WEIGHTS_0', doc.createAccessor().setType('VEC4').setArray(new Float32Array(part.weights)).setBuffer(buffer))
    .setIndices(doc.createAccessor().setType('SCALAR').setArray(new Uint32Array(part.idx)).setBuffer(buffer))
    .setMaterial(mat);
  if (part.color.length) prim.setAttribute('COLOR_0', doc.createAccessor().setType('VEC4').setArray(new Float32Array(part.color)).setBuffer(buffer));
  for (const morph of morphs) {
    const target = doc.createPrimitiveTarget(morph.name).setAttribute('POSITION', doc.createAccessor().setType('VEC3').setArray(morph.delta).setBuffer(buffer));
    prim.addTarget(target);
  }
  mesh.addPrimitive(prim);
  return prim;
};
// eye-blink morphs: squash each eyeball vertically toward its centre (a mannequin "blink")
const blink = (start, count, c) => {
  const delta = new Float32Array(eyes.pos.length);
  for (let v = start; v < start + count; v++) delta[v * 3 + 1] = (c[1] - eyes.pos[v * 3 + 1]) * 0.92;
  return delta;
};
const perEye = (eyes.pos.length / 3) / 2;
const blinkL = blink(eyeStart[0], perEye, P('LeftEye'));
const blinkR = blink(eyeStart[1], perEye, P('RightEye'));
const zeros = (part) => new Float32Array(part.pos.length);
const groups = [
  [skin, material('MannequinFinish', [0.78, 0.72, 0.66], 0.72)],
  [top, material('Sweater', [0.36, 0.47, 0.4], 0.92)],
  [trousers, material('Trousers', [0.2, 0.22, 0.26], 0.85)],
  [shoes, material('Shoes', [0.13, 0.1, 0.09], 0.6)],
  [hair, material('Hair', [0.2, 0.15, 0.11], 0.68)],
  [eyes, material('Eyes', [1, 1, 1], 0.25)],
];
for (const [part, mat] of groups) {
  addPrimitive(part, mat, [
    { name: 'eyeBlinkLeft', delta: part === eyes ? blinkL : zeros(part) },
    { name: 'eyeBlinkRight', delta: part === eyes ? blinkR : zeros(part) },
  ]);
}
mesh.setWeights([0, 0]).setExtras({ targetNames: ['eyeBlinkLeft', 'eyeBlinkRight'] });
const body = doc.createNode('MannequinBody').setMesh(mesh).setSkin(skinNode);
scene.addChild(armature).addChild(body);

const io = new NodeIO();
const glb = await io.writeBinary(doc);
mkdirSync(dirname(OUT), { recursive: true });
writeFileSync(OUT, glb);
const tris = [skin, top, trousers, shoes, hair, eyes].reduce((a, p) => a + p.idx.length / 3, 0);
console.log(`test-mannequin.glb  ${(glb.length / 1024).toFixed(0)} KB  ${tris} triangles  ${JOINTS.length} joints`);
