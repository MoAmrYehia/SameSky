# Asset manifest

Every image, model, texture, sound and font that SameSky ships, where it came from, its licence, how it was
produced, and what it costs to load. Nothing in the demo is a screenshot of the app or a generated picture
of a person; all people in the sample are fictional.

- **Original** assets are produced by scripts in this repository and can be regenerated.
- **Third-party** assets are CC0 (public domain) from [Poly Haven](https://polyhaven.com), downloaded through the Poly Haven API by
  `scripts/assets/fetch_polyhaven.py`. Authors, source URLs and the SHA-256 of every downloaded source file are
  recorded in [`asset-provenance.json`](asset-provenance.json), retrieved on 2026-11-06.
- Source downloads live in `.asset-cache/` (git-ignored, about 190 MB). Only the optimized outputs below are committed.

## Runtime assets (`web/public/assets/`)

| Path | What it is | Origin and licence | Size |
|---|---|---|---|
| `models/avatars/characters.glb` | Sculpted stylized character kit: two face presets (soft, defined), seven hair options (short, wavy, long, bun, curly, buzz, none), torso, neck, seated legs, upper arms, forearms and fingered hands, plus scarf and cap accessories | **Original.** `scripts/assets/sculpt_characters.py` (signed-distance sculpt with marching cubes; UVs and baked shading cues in vertex colours), then `web/scripts/optimize-assets.mjs characters` (per-part simplification budgets, meshopt) | 620 KB, 88,897 triangles for *all* variants (one seated character is about 45k) |
| `models/props/table.glb` | The shared round table | CC0, Poly Haven *Round Wooden Table 01* (Ulan Cabanilla) | 217 KB, 8,640 tris |
| `models/props/armchair.glb` | The partner's upholstered armchair | CC0, Poly Haven *Arm Chair 01* (Kirill Sannikov) | 481 KB, 5,626 tris |
| `models/props/side-table.glb` | Side table for the lamp and keepsakes | CC0, Poly Haven *Round Wooden Table 02* (Ulan Cabanilla) | 269 KB, 4,254 tris |
| `models/props/plant.glb` | Window-sill plants (one model, two instances) | CC0, Poly Haven *Potted Plant 04* (James Ray Cock) | 447 KB, 7,493 tris |
| `models/props/vase.glb` | Side-table vase | CC0, Poly Haven *Ceramic Vase 04* (James Ray Cock) | 196 KB, 3,613 tris |
| `textures/parquet/{color,normal,ao}.webp` | Herringbone floor | CC0, Poly Haven *Herringbone Parquet* | 197 KB |
| `textures/boucle/{normal,ao}.webp` | Rug and cushion relief; the albedo is a flat tint | CC0, Poly Haven *Wool Boucle* | 452 KB |
| `textures/plaster/{color,normal,rough,ao}.webp` | Curved plaster wall and window bands | CC0, Poly Haven *Clay Plaster* | 225 KB |
| `textures/walnut/{color,normal,rough,ao}.webp` | Frames, lamp base and keepsake plinths. The scan is a grey-washed veneer, so materials tint it warm (`WALNUT_TINT`) | CC0, Poly Haven *American Walnut Veneer* | 179 KB |
| `textures/knit/normal.webp` | Knit relief on the characters' sweaters | CC0, Poly Haven *Knitted Fleece* | 476 KB |
| `env/lounge_1k.hdr` | Interior light probe, prefiltered at runtime with PMREM (IWSDK `IBLTexture`) and reused by the portrait renderer | CC0, Poly Haven HDRI *Lythwood Lounge* (Greg Zaal), 1k, unmodified | 1.54 MB |
| `sample/window-skyline.jpg` | Distant skyline seen through the window: a 200° cylindrical projection (straight verticals, correct heights), sunset grade, slight softening, 2048×512 | Derived from CC0 HDRI *Dusseldorf Bridge* (Greg Zaal) by `scripts/assets/build_sample_media.py`. In the room it is multiplied by a slightly cool white (`#e8eef7`) to temper the sunset grade. **No geographic claim:** the room is a *sample lounge* (its welcome card says the view is a CC0 photograph, not a live view of anyone’s city); it is not Dubai or any user’s location | 123 KB |
| `backdrops/lounge.jpg`, `lounge-dark.jpg` | Blurred companion backdrops (light and dark). No UI text, no people, no private content | Derived from CC0 HDRI *Lythwood Lounge* | 48 KB, 30 KB |
| `sample/maya-park.jpg` (+`-thumb`) | Maya's afternoon walk photo | Perspective crop of CC0 HDRI *Ballawley Park* (Savva Zakharov), graded | 408 KB + 77 KB |
| `sample/adam-waterfront.jpg` (+`-thumb`) | Adam's harbour walk photo | Perspective crop of CC0 HDRI *Golden Bay* (Greg Zaal) | 291 KB + 55 KB |
| `sample/adam-desert.jpg` (+`-thumb`) | Adam's desert-hills photo (family circle) | Perspective crop of CC0 HDRI *Rogland Sunset* (Greg Zaal) | 358 KB + 62 KB |
| `sample/lina-courtyard.jpg` (+`-thumb`) | Lina's courtyard evening photo (family circle) | Perspective crop of CC0 HDRI *Courtyard Night* (Greg Zaal) | 227 KB + 45 KB |
| `sample/adam-clip.mp4`, `-poster.jpg`, `-thumb.jpg` | 8-second 1280×720 handheld-style camera pan along the promenade, H.264 High plus AAC (quiet brown-noise ambience), `+faststart` | Rendered frame by frame from CC0 HDRI *Golden Bay* along a simulated camera path. **Not a camera recording**: its sample data carries `origin: 'simulated'`, so the companion labels it *Simulated camera pan* and the room *Adam’s simulated camera pan*. Real device imports keep their own source labels (*Phone*, *Camera*, *Action camera* …) | 3.85 MB (loaded only when played) |
| `sample/maya-voice.m4a`, `.vtt` | Maya's 9-second voice note and its captions | **Original.** Synthesized with the operating system's text-to-speech voice by `scripts/fixtures/make_illustrated_media.py` (`voice()`), AAC 96 kb/s. A synthesized sample voice, not a recording of a person; labelled *Synthesized voice note* in the companion and *Synthesized voice* on the cup | 79 KB |
| Figtree Variable font | Companion and in-room interface type | SIL Open Font License 1.1, self-hosted from the `@fontsource-variable/figtree` npm package and bundled by Vite | 20 KB (variable woff2, latin; latin-ext 10 KB only if needed) |

The sample's people, captions and recaps are fictional. The photos are crops of public-domain panoramas
with no identifiable people. They stand in for what a user would share, and are labelled *Sample photo*
(`origin: 'stock'`) rather than as a phone or camera photo.

## Native app copies (`native/quest/Assets/SameSky/`)

The Unity app ships its own copies of the assets above. Unity can't import the web room's WebP-in-glTF props
or decode AAC audio clips, so it gets the original formats instead. Nothing new comes in:
`scripts/native/build_native_assets.py` copies or converts from the same sources, and the characters come from
the same kit.

| Path | What it is | Origin and licence | Size |
|---|---|---|---|
| `Art/Models/{table,armchair,side-table,plant,vase}/` | The five room props as Poly Haven's 1k glTF: `.gltf`, `.bin` and 1024 px JPG maps (diffuse, normal and ARM; the vase has roughness instead of ARM) | CC0, the same Poly Haven assets as the web props above; authors, URLs and SHA-256 in [`asset-provenance.json`](asset-provenance.json). Copied unchanged from `.asset-cache/` (`scripts/assets/fetch_polyhaven.py`) | 5.68 MB, 25 files |
| `Art/Textures/{herringbone_parquet,clay_plaster,american_walnut_veneer,wool_boucle,knitted_fleece}/` | Tiling sets for the floor, walls, wood, rug and knit: diffuse, normal, roughness and AO JPGs, 1024 px wide | CC0 Poly Haven, as above, unchanged | 14.13 MB, 20 files |
| `Art/Env/lythwood_lounge_1k.hdr` | Interior light for the skybox and reflections | CC0 Poly Haven HDRI *Lythwood Lounge* (Greg Zaal), unchanged | 1.54 MB |
| `Art/Textures/window-skyline.jpg` | The window plate | Copy of `sample/window-skyline.jpg` above (same derivation, same "no geographic claim") | 123 KB |
| `Resources/Characters/{maya,adam,lina}.glb` | The sample people's demo characters, one per fictional person, seated across the table | **Original.** Exported from the companion's character kit (`models/avatars/characters.glb` above) by `web/scripts/export-characters.mjs` through `web/dev/export-characters.html` (needs `npm run dev`). Procedural sculpts: no scan, no photo, no likeness | 3.49–3.55 MB each |
| `Resources/Characters/demo-figure.glb` | The labelled demo figure in live rooms, for someone without an approved avatar | **Original.** Byte-identical copy of the test mannequin `web/tests/fixtures/test-mannequin.glb` (see *Test fixtures*); not a likeness | 905 KB |
| `Resources/Sample/Media/*.jpg`, `adam-clip.mp4` | The sample day's photos, the clip, its poster and the thumbnails | Byte-identical copies of `web/public/assets/sample/`. The native app labels them *Stock photo (sample)* and *Simulated camera pan (sample)* (`OriginLabel` in `SameSkyApp.Room.cs`) | 5.36 MB, 11 files |
| `Resources/Sample/Media/maya-voice.ogg` | Maya's voice note in the cup | **Original.** Converted from the synthesized `sample/maya-voice.m4a` to Ogg Vorbis (mono, 44.1 kHz, quality 5) by `build_native_assets.py`. Still the operating system's text-to-speech voice, not a recording; labelled *Synthesized voice (sample)* on the cup | 55 KB |
| `Resources/Sample/sample-day.json` | The fictional sample day, captions included as timed cues | Exported from `web/src/shared/sample.ts` by `scripts/native/export_sample.mjs` | 34 KB |

## Derived renders (not files)

- **Companion portraits** are rendered at runtime from `characters.glb` by `src/shared/portrait.ts`. It uses the same
  model, outfit, materials and light rig as the room, so a person's portrait is the character that sits across the table.
- **Keepsake cards** are runtime renders of the three keepsake models (`src/shared/keepsakes3d.ts`), using the same
  models that stand in the room.
- Small canvas textures (mug speckle, cork, iris, interface panels) are drawn procedurally at runtime.

## Test fixtures (not shipped)

| Path | What it is | Origin and licence | Size |
|---|---|---|---|
| `web/tests/fixtures/test-mannequin.glb` | Skinned test figure for the photo-derived avatar path: an artist's-mannequin body with a Mixamo-style 24-joint skeleton, eye-blink morph targets, A-pose, metres, facing +Z | **Original.** Generated procedurally by `web/scripts/build-test-rig.mjs` (no scan, no photo, no person). Used only by `web/scripts/avatar-e2e.mjs`, the dev rig page and API validation tests; it never ships in `web/public` and is not a likeness | 905 KB, 17,584 triangles |

No real person's photo, face scan or provider-generated avatar is committed. Photo-derived avatars are private
per-user data stored by the API (`SAMESKY_MEDIA_ROOT/avatar-private/`), never repository assets.


`scripts/fixtures/make_illustrated_media.py` (formerly `scripts/make_sample_media.py`) still produces the
earlier Pillow illustrations for tests and fallbacks. Its output goes to `scripts/fixtures/out/` (git-ignored) and
never overwrites the shipped art. Regenerate it explicitly with:

```bash
python scripts/fixtures/make_illustrated_media.py
```

## Regenerating the shipped art

```bash
api/.venv/Scripts/python -I scripts/assets/fetch_polyhaven.py      # CC0 sources + provenance (cached)
python scripts/assets/sculpt_characters.py                          # original character parts (numpy, scikit-image)
python scripts/assets/build_sample_media.py                          # photos, clip, window plate, backdrops (numpy, scipy, Pillow, FFmpeg)
cd web && node scripts/optimize-assets.mjs characters models textures env
```

## Budgets (default room, desktop sample)

Measured with `node scripts/capture.mjs --report` in headless Chrome (SwiftShader). Frame rate in that
renderer is meaningless; Quest measurements are still to do (see `VISUAL_QA.md`).

| Metric | Target (brief) | Measured |
|---|---|---|
| Triangles per frame | ~150–200k | 123k–136k (includes the shadow pass) |
| Draw calls per frame | ≤100–120 | 94–101 in the default room (105 in the headset preview); includes the shadow pass |
| Texture sizes | 1–2K | ≤2048 px; tiling sets 1024/512; window plate 2048×512; sample photos 1600×1000 |
| Critical room art | ~15 MB | about 6.3 MB: HDR 1.54 + character kit 0.62 + props 1.61 + textures 1.53 + window 0.12 + hero photo and table prints 0.75 + font 0.02. The 3.85 MB clip loads on demand |
