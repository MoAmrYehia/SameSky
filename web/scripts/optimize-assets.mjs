// Turn source assets (.asset-cache, git-ignored) into the optimized runtime assets in public/assets.
//
//   node scripts/optimize-assets.mjs [characters] [models] [textures] [env]
//
// Geometry: weld + meshoptimizer simplification (per-part budgets) + EXT_meshopt_compression.
// Textures: WebP (EXT_texture_webp), max 1024 px; colour maps stay sRGB, data maps stay linear (the loader
// tags them per glTF semantics). Decoders are the ones shipped with three.js (meshopt_decoder.module.js).

import { mkdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import { dedup, meshopt, prune, simplifyPrimitive, textureCompress, weld } from '@gltf-transform/functions';
import { MeshoptEncoder, MeshoptSimplifier } from 'meshoptimizer';
import sharp from 'sharp';

const here = dirname(fileURLToPath(import.meta.url));
const REPO = resolve(here, '../..');
const CACHE = resolve(REPO, '.asset-cache');
const PUBLIC = resolve(here, '../public/assets');
const want = new Set(process.argv.slice(2).length ? process.argv.slice(2) : ['characters', 'models', 'textures', 'env']);

await MeshoptSimplifier.ready;
await MeshoptEncoder.ready;
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS).registerDependencies({ 'meshopt.encoder': MeshoptEncoder, 'meshopt.decoder': MeshoptEncoder });

const report = [];
function size(path) {
  return (statSync(path).size / 1e6).toFixed(2) + ' MB';
}

function triangles(doc) {
  let t = 0;
  for (const mesh of doc.getRoot().listMeshes()) for (const prim of mesh.listPrimitives()) t += (prim.getIndices()?.getCount() ?? prim.getAttribute('POSITION').getCount()) / 3;
  return Math.round(t);
}

// ---------------------------------------------------------------------------
// Characters: per-part triangle budgets (heads keep the most detail).
// ---------------------------------------------------------------------------
const PART_BUDGET = {
  head_soft: 12000, head_defined: 12000, lips_soft: 900, lips_defined: 900, brows_soft: 500, brows_defined: 600,
  hair_short: 6500, hair_wavy: 9000, hair_long: 10000, hair_bun: 6500, hair_curly: 8000, hair_buzz: 3500,
  torso: 5000, neck: 900, legs: 3000, upper_arm: 1200, forearm: 1200, hand: 3500, scarf: 1500, cap: 2000,
};

if (want.has('characters')) {
  const doc = await io.read(resolve(CACHE, 'characters_raw.glb'));
  await doc.transform(weld());
  for (const mesh of doc.getRoot().listMeshes()) {
    const budget = PART_BUDGET[mesh.getName()] ?? 2000;
    for (const prim of mesh.listPrimitives()) {
      const tris = prim.getIndices().getCount() / 3;
      const ratio = Math.min(1, budget / tris);
      if (ratio < 1) simplifyPrimitive(prim, { simplifier: MeshoptSimplifier, ratio, error: 0.004, lockBorder: false });
    }
  }
  await doc.transform(prune(), dedup(), meshopt({ encoder: MeshoptEncoder, level: 'medium' }));
  const out = resolve(PUBLIC, 'models/avatars/characters.glb');
  mkdirSync(dirname(out), { recursive: true });
  await io.write(out, doc);
  report.push(`characters.glb  ${triangles(doc)} tris (all parts, both presets, all hair styles)  ${size(out)}`);
}

// ---------------------------------------------------------------------------
// Poly Haven models (CC0)
// ---------------------------------------------------------------------------
const MODELS = {
  round_wooden_table_01: { out: 'table', maxTris: 9000 },
  ArmChair_01: { out: 'armchair', maxTris: 8000 },
  round_wooden_table_02: { out: 'side-table', maxTris: 4500 },
  potted_plant_04: { out: 'plant', maxTris: 6500 },
  ceramic_vase_04: { out: 'vase', maxTris: 2400 },
};

if (want.has('models')) {
  for (const [id, spec] of Object.entries(MODELS)) {
    const doc = await io.read(resolve(CACHE, 'models', id, `${id}_1k.gltf`));
    await doc.transform(weld());
    const before = triangles(doc);
    if (before > spec.maxTris) {
      const ratio = spec.maxTris / before;
      for (const mesh of doc.getRoot().listMeshes()) for (const prim of mesh.listPrimitives()) simplifyPrimitive(prim, { simplifier: MeshoptSimplifier, ratio, error: 0.002 });
    }
    await doc.transform(
      prune(),
      dedup(),
      textureCompress({ encoder: sharp, targetFormat: 'webp', resize: [1024, 1024], quality: 82 }),
      meshopt({ encoder: MeshoptEncoder, level: 'medium' }),
    );
    const out = resolve(PUBLIC, `models/props/${spec.out}.glb`);
    mkdirSync(dirname(out), { recursive: true });
    await io.write(out, doc);
    report.push(`${spec.out}.glb  ${before} -> ${triangles(doc)} tris  ${size(out)}  (Poly Haven ${id}, CC0)`);
  }
}

// ---------------------------------------------------------------------------
// Tiling PBR textures (CC0) -> WebP
// ---------------------------------------------------------------------------
// Only the maps the runtime actually samples are exported (see pbr() in src/xr/env.ts, walnutMaterial(), character.ts).
const TEXTURES = {
  herringbone_parquet: { name: 'parquet', maps: ['color', 'normal', 'ao'] },
  wool_boucle: { name: 'boucle', maps: ['normal', 'ao'] }, // relief only: the rug and cushion are tinted, not multicoloured
  clay_plaster: { name: 'plaster', maps: ['color', 'normal', 'rough', 'ao'] },
  american_walnut_veneer: { name: 'walnut', maps: ['color', 'normal', 'rough', 'ao'] },
  knitted_fleece: { name: 'knit', maps: ['normal'] }, // sweater knit relief on the characters
};
const MAP_FILES = { color: ['Diffuse.jpg', 'color.webp', 1024], normal: ['nor_gl.jpg', 'normal.webp', 1024], rough: ['Rough.jpg', 'rough.webp', 512], ao: ['AO.jpg', 'ao.webp', 512] };
if (want.has('textures')) {
  for (const [id, { name, maps }] of Object.entries(TEXTURES)) {
    const dir = resolve(PUBLIC, 'textures', name);
    mkdirSync(dir, { recursive: true });
    for (const [src, dst, px] of maps.map((m) => MAP_FILES[m])) {
      const input = resolve(CACHE, 'textures', id, src);
      try {
        statSync(input);
      } catch {
        continue;
      }
      await sharp(input).resize(px, px).webp({ quality: dst === 'normal.webp' ? 90 : 82 }).toFile(resolve(dir, dst));
    }
    report.push(`textures/${name}/*.webp  (Poly Haven ${id}, CC0)`);
  }
}

// ---------------------------------------------------------------------------
// Environment light probe (CC0 HDR, copied as-is: 1k Radiance HDR is ~1.5 MB)
// ---------------------------------------------------------------------------
if (want.has('env')) {
  const out = resolve(PUBLIC, 'env/lounge_1k.hdr');
  mkdirSync(dirname(out), { recursive: true });
  writeFileSync(out, readFileSync(resolve(CACHE, 'hdri', 'lythwood_lounge_1k.hdr')));
  report.push(`env/lounge_1k.hdr  ${size(out)}  (Poly Haven lythwood_lounge, CC0)`);
}

console.log(report.join('\n'));
