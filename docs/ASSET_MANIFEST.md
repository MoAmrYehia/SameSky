# Asset manifest

Every image, model, texture, sound and font that SameSky ships, where it came from, its licence, how it was
produced, and what it costs to load. Nothing in the demo is a screenshot of the app or a generated picture
of a person; all people in the sample are fictional.

- **Original** assets are produced by scripts in this repository and can be regenerated.
- **Third-party** assets are CC0 (public domain) from [Poly Haven](https://polyhaven.com), downloaded through the Poly Haven API by
  `scripts/assets/fetch_polyhaven.py`. Authors, source URLs and the SHA-256 of every downloaded source file are
  recorded in [`asset-provenance.json`](asset-provenance.json), retrieved on 2026-11-06.
- Source downloads live in `.asset-cache/` (git-ignored, about 190 MB). Only the optimized outputs below are committed.
- The native Quest app adds three more third-party sources, each with its licence text beside it: Microsoft
  Rocketbox characters and animations (MIT), the Figtree typeface (SIL OFL 1.1) and Lucide icons (ISC). See
  *Native app copies*.

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
| `sample/maya-park.jpg` (+`-thumb`) | Maya's walk-after-work photo | **Third-party photograph, CC0 1.0.** *Autumn path, Dumitru Rascanu park* by Iurie Nistor ([Wikimedia Commons](https://commons.wikimedia.org/wiki/File:Autumn_path,_Dumitru_Rascanu_park.jpg)). Cropped to 16:10 and resized by `scripts/assets/build_real_sample_media.py`; no metadata carried over | 460 KB + 85 KB |
| `sample/adam-waterfront.jpg` (+`-thumb`) | Adam's harbour walk photo | **Third-party photograph, CC0 1.0.** *Deira Dubai Creek* by kallerna ([Wikimedia Commons](https://commons.wikimedia.org/wiki/File:Deira_Dubai_Creek.jpg)): the creek at dusk, boats and their lights on the water. Cropped and resized as above | 295 KB + 54 KB |
| `sample/adam-desert.jpg` (+`-thumb`) | Adam's desert-hills photo (family circle) | **Third-party photograph, CC BY 4.0.** *Sharah Desert at sunset, UAE* by Lxs ([Wikimedia Commons](https://commons.wikimedia.org/wiki/File:Sharah_Desert_at_sunset,_UAE.jpg)). Cropped and resized as above; credited in the apps (*Photo: Lxs · CC BY 4.0 · Wikimedia Commons*) | 186 KB + 27 KB |
| `sample/lina-courtyard.jpg` (+`-thumb`) | Lina's courtyard evening photo (family circle) | **Third-party photograph, public domain** (released by its author). *Tower block and courtyard in Dunedin Chinese Garden at night* by Pseudopanax ([Wikimedia Commons](https://commons.wikimedia.org/wiki/File:Tower_block_and_courtyard_in_Dunedin_Chinese_Garden_at_night.jpg)). Cropped and resized as above | 365 KB + 62 KB |
| `sample/adam-clip.mp4`, `-poster.jpg`, `-thumb.jpg` | Adam's clip: 8.5 seconds on Dubai Creek (creek-side buildings, a ferry crossing, a wooden dhow), 1280×720, 30 fps, H.264 High plus a silent AAC track, `+faststart` | **Third-party footage, CC BY 3.0.** Seconds 16.0–24.5 of *Dubai Part I - Aug 10, 2012* by 54t4n0d ([Wikimedia Commons](https://commons.wikimedia.org/wiki/File:Dubai_Part_I_-_Aug_10,_2012.webm)), scaled from 1080p by `build_real_sample_media.py`. The original's music is **not** used. Real footage, but nobody in the sample day filmed it: its sample data carries `origin: 'stock'` and a credit, so the companion labels it *Sample clip* and the headset *Stock footage (sample)*, each with *Footage: 54t4n0d · CC BY 3.0 · Wikimedia Commons* | 3.11 MB + 67 KB + 27 KB |
| `sample/maya-voice.m4a`, `.vtt` | Maya's 9-second voice note and its captions | **Original.** Synthesized with the operating system's text-to-speech voice by `scripts/fixtures/make_illustrated_media.py` (`voice()`), AAC 96 kb/s. A synthesized sample voice, not a recording of a person; labelled *Synthesized voice note* in the companion and *Synthesized voice* on the cup | 79 KB |
| Figtree Variable font | Companion and in-room interface type | SIL Open Font License 1.1, self-hosted from the `@fontsource-variable/figtree` npm package and bundled by Vite | 20 KB (variable woff2, latin; latin-ext 10 KB only if needed) |

The sample's people, captions and recaps are fictional. The photos and the clip are real photographs and footage
by the authors named above. None of those authors is one of the sample's people, and nobody identifiable is in the
pictures (the people on the boats are distant silhouettes). They stand in for what a user would share: each carries
`origin: 'stock'` and a `credit`, so the apps label them *Sample photo* and *Sample clip* and say who made them,
never as the fictional person's own phone or camera recording. The recap's "looks like" lines describe what these
pictures actually show.

- **Sources:** `scripts/assets/sample_media.json` lists each original's page, direct URL, licence, author, size and
  SHA-256 (also in [`asset-provenance.json`](asset-provenance.json), under `sampleMedia`).
- **Fetch:** `scripts/assets/fetch_sample_media.py` downloads them into the git-ignored `.asset-cache/sample-media/`
  (206 MB, most of it the video) and checks the hashes.
- **Build:** `scripts/assets/build_real_sample_media.py` (Pillow and FFmpeg) writes the files above.
- **Before 12 November** these five were crops of CC0 Poly Haven panoramas and a camera pan simulated from one of
  them. `scripts/assets/build_sample_media.py` still builds the window plate and the backdrops from those panoramas;
  it no longer writes the sample photos or the clip.

## Native app copies (`native/quest/Assets/SameSky/`)

The Unity app ships its own copies of the assets above. Unity can't import the web room's WebP-in-glTF props
or decode AAC audio clips, so it gets the original formats instead: `scripts/native/build_native_assets.py`
copies or converts from the same sources. The headset room also has its own type, icons and seated characters
(the rows marked third-party below); the room's walls, glazing, furniture and props are generated by
`Editor/LoungeBuilder.cs` and `Scripts/Room/MeshGen.cs` from the CC0 texture sets.

| Path | What it is | Origin and licence | Size |
|---|---|---|---|
| `Art/Models/{table,armchair,side-table,plant,vase}/` | The five room props as Poly Haven's 1k glTF: `.gltf`, `.bin` and 1024 px JPG maps (diffuse, normal and ARM; the vase has roughness instead of ARM) | CC0, the same Poly Haven assets as the web props above; authors, URLs and SHA-256 in [`asset-provenance.json`](asset-provenance.json). Copied unchanged from `.asset-cache/` (`scripts/assets/fetch_polyhaven.py`) | 5.68 MB, 25 files |
| `Art/Textures/{herringbone_parquet,clay_plaster,american_walnut_veneer,wool_boucle,knitted_fleece}/` | Tiling sets for the floor, walls, wood, rug and knit: diffuse, normal, roughness and AO JPGs, 1024 px wide | CC0 Poly Haven, as above, unchanged | 14.13 MB, 20 files |
| `Art/Env/lythwood_lounge_1k.hdr` | Interior light for the skybox and reflections | CC0 Poly Haven HDRI *Lythwood Lounge* (Greg Zaal), unchanged | 1.54 MB |
| `Art/Textures/window-skyline.jpg` | The window plate | Copy of `sample/window-skyline.jpg` above (same derivation, same "no geographic claim") | 123 KB |
| `Art/Characters/Rocketbox/{Female_Adult_14,Male_Adult_07}/`, `Animations/`, `Resources/Characters/{maya,adam}.prefab` | The sample's Maya and Adam in the native room: stock adult characters with facial rigs (Rocketbox *Female_Adult_14* and *Male_Adult_07*, 81-bone Biped skeletons imported as Humanoid), seated with Rocketbox's `f_sit_table_breathe_01` / `m_sit_table_breathe_01` loops. Textures converted from TGA (head colour 2048 px JPG, other colour maps 1024 px JPG, normal and hair/lash opacity maps PNG); the specular maps are not used. URP materials, animator controllers and prefabs are made by `Editor/CharacterSetup.cs` | **Third-party, MIT** (© 2020 Microsoft), [Microsoft Rocketbox](https://github.com/microsoft/Microsoft-Rocketbox) `Assets/Avatars/Adults/*/Export/*_facial.fbx`, `*/Textures/*.tga` and `Assets/Animations/`; licence text in `Art/Characters/Rocketbox/LICENSE.txt`, source hashes in [`asset-provenance.json`](asset-provenance.json) (`native`). Stock characters standing in for fictional people: not a scan or photo of anyone who uses the app | 17.9 MB, 14 files (models 3.8 MB, animations 5.7 MB, textures 8.4 MB) |
| `Resources/Characters/lina.glb` | Lina (the sample's family circle) | **Original.** Exported from the companion's character kit (`models/avatars/characters.glb` above) by `web/scripts/export-characters.mjs` through `web/dev/export-characters.html` (needs `npm run dev`). Procedural sculpt: no scan, no photo, no likeness | 3.5 MB |
| `Art/Fonts/Figtree/Figtree-{Regular,SemiBold,Bold}.ttf`, `OFL.txt` | The interface type in the headset (TextMesh Pro dynamic font assets in `Resources/Fonts`, made by `Editor/FontSetup.cs`) | **Third-party, SIL Open Font License 1.1**, Figtree by Erik Kennedy ([google/fonts](https://github.com/google/fonts/tree/main/ofl/figtree)); static 400/600/700 instances cut from the variable font with fontTools' instancer, unmodified otherwise | 120 KB |
| `Art/Fonts/Lucide/lucide.ttf`, `LICENSE.txt` | Line icons in buttons and the dock (a fallback font of the interface type; codepoints in `Scripts/UI/Icons.cs`) | **Third-party, ISC**, Lucide (`lucide-static` 0.460.0 from npm), unchanged | 661 KB |
| `Resources/Characters/demo-figure.glb` | The labelled demo figure in live rooms, for someone without an approved avatar | **Original.** Byte-identical copy of the test mannequin `web/tests/fixtures/test-mannequin.glb` (see *Test fixtures*); not a likeness | 905 KB |
| `Resources/Sample/Media/*.jpg`, `adam-clip.mp4` | The sample day's photos, the clip, its poster and the thumbnails | Byte-identical copies of `web/public/assets/sample/` (the third-party photographs and footage above, same licences). The native app labels them *Stock photo (sample)* and *Stock footage (sample)* (`OriginLabel` in `SameSkyApp.Room.cs`) and shows each one's credit | 4.5 MB, 11 files |
| `Resources/Sample/Media/maya-voice.ogg` | Maya's voice note in the cup | **Original.** Converted from the synthesized `sample/maya-voice.m4a` to Ogg Vorbis (mono, 44.1 kHz, quality 5) by `build_native_assets.py`. Still the operating system's text-to-speech voice, not a recording; labelled *Synthesized voice (sample)* on the cup | 55 KB |
| `Resources/Sample/sample-day.json` | The fictional sample day, captions included as timed cues, credits included for the stock media | Exported from `web/src/shared/sample.ts` by `scripts/native/export_sample.mjs` | 34 KB |
| `Art/Generated/Hands/hand-{left,right}-{a,b}.png`, `Resources/Materials/HandSkin{Left,Right}.mat`, `Resources/hand-skin.json` | Skin for the tracked hands: where the nails, the palm, the knuckle wrinkles, the flexion creases and the three palm lines are, a detail normal, and the wrist fade (1024 px) | **Original, generated.** Computed from the geometry and joints of Meta's hand mesh by `Editor/HandSkinSetup.cs`: nothing painted by hand, no photo or scan of anyone's hands. The mesh itself (`OpenXRLeftHand.fbx`, `OpenXRRightHand.fbx`) is Meta's and stays in the Meta XR Interaction SDK package; it is not copied into this repository | 2.1 MB |
| `Art/Generated/Decor/`, the furniture meshes in `Art/Generated/` | The marketplace's pieces and finishes: tables (pedestal, tripod, drum), a flatweave rug, globe and tripod lamps, a snake plant; finish materials for wood, fabric, metal, walls and floor; `marble.png` and `snake-leaf.png` | **Original, generated** by `Editor/LoungeBuilder.cs` (procedural meshes and textures). `veneer-oak.jpg` and `veneer-ash.jpg` are the CC0 *American Walnut Veneer* scan above with its tones remapped. The leafy plant is the CC0 Poly Haven plant above | 1.1 MB plus meshes |
| `Resources/furniture-catalog.json` | The marketplace's catalogue in the headset app | Byte-identical copy of `api/samesky/furniture_catalog.json` (`build_native_assets.py`; `test_native_app_bundles_the_same_catalogue` keeps them the same) | 5 KB |

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
