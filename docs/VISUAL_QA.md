# Visual QA — concept fidelity pass (6 November 2026)

This records what the visual enhancement changed, how it was checked, and what still differs from the four
concept images in `docs/references/`. Captures are real renders of the running app (sample mode), not mock-ups.
Every text and control in them is live.

- **Before:** `docs/visual-qa/before/` (the app before this pass).
- **After:** `docs/visual-qa/after/`.
- **Side-by-side:** `docs/visual-qa/compare/` (concept | before | after).
- **Provenance** of every shipped asset: [ASSET_MANIFEST.md](ASSET_MANIFEST.md) and [asset-provenance.json](asset-provenance.json).

## How the captures were made

```bash
cd web && npm run dev     # http://localhost:5173 (5174 was used here)
node scripts/capture.mjs --base http://localhost:5174 --out ../docs/visual-qa/after --sizes 1672x941 \
  --shots room,room-watch,room-xr,room-loading,room-error,room-signin,day,day-circle,day-loading,share,share-populated,together,keepsakes,settings,welcome --report
node scripts/capture.mjs --base http://localhost:5174 --out ../docs/visual-qa/after --sizes 1672x941 --themes dark --shots day,together,keepsakes,settings
node scripts/capture.mjs --base http://localhost:5174 --out ../docs/visual-qa/after --sizes 1440x900,1024x768,390x844 --shots room,day,share-populated,together --report
api/.venv/Scripts/python -I scripts/visual_qa_compare.py
node scripts/room-interactions.mjs --base http://localhost:5174   # pointer smoke test, see below
```

Capture setup:

- **Browser and clock:** headless Chrome (playwright-core with the system Chrome, SwiftShader WebGL), with the clock frozen at Friday 6 November 2026, 17:05 Dubai / 21:05 Beijing.
- **Sample state:** fresh sample mode, onboarding at its first step.
- **Fonts and renders:** each shot waits for fonts and for every 3D portrait and keepsake render.
- **Room:** `?capture=1` fixes idle animation time and disables blinking. The shot waits for `window.__sameskyReady`, then runs `window.__sameskySafeArea()`, which fails the capture if any key element is clipped or covered.
- **Populated Share:** the capture loads the sample clip and selects *Private circle*. It never presses *Share*, so nothing is published.

## Side-by-side comparisons and discrepancy lists

### Shared room — `compare/room.jpg`

Now matching the concept:

- **Composition:**
  - The glass media screen sits toward the left and is fully in view. Its header, transport, captions and primary *Play our day* action are all inside the panel.
  - The seated partner is on the right, large enough to read expression and clothing, resting her chin on her hand as in the concept.
  - The cup sits on a cork coaster in the foreground with a concise glass label (*Saved you a seat* · Maya · Voice note · 0:09 · ▶).
  - The postcard and memory tree are on the table, and one compact unified dock rests at the table edge.
- **Room:**
  - A wide window frames a real panorama as a projected cylindrical plate at about 9 m, softly filtered.
  - Upholstered armchair, walnut table, herringbone parquet, knitted cream rug, curved plaster wall, sill plants, a lit lamp on a side table.
- **Presence and header:**
  - The presence badge is at the head's upper right, as in the concept (*Maya · Simulated (sample)*; it switches to *Here now* only for a live, tracked participant).
  - The 2D header has the brand at left, *Our room* in the centre and *Private · Adam + Maya* at the right.
- **Light:** PMREM image-based light from a CC0 interior HDR, a warm low sun through the window with one soft shadow light, contact shading under furniture and objects, and Neutral tone mapping. Whites stay creamy: the sweater, mug and vase are not clipped.

Remaining differences:

1. **Character fidelity.** The partner is an original procedurally sculpted character: modelled nose, lids, brows, lips, solid hair, a knit sweater and fingered hands. It reads as a friendly stylized doll, not the concept's feature-film character.
   - It has no smile, cheek or brow expression yet, and no cloth folds or hair strands.
   - Body parts are rigid pieces driven by two-bone IK (no skinning).
2. **Skyline.** The window shows a real CC0 Düsseldorf panorama at dusk rather than the concept's invented skyline. The sky is less pink.
3. **Light and colour.** The table top reads darker and more saturated than the concept's polished walnut, and the overall image is slightly more orange.
4. **Framing.** The concept's camera sits closer to the table with the viewer's hands in frame; ours sits a little further back so the screen, partner, cup label and dock all fit and pass the safe-area check at four aspect ratios. The screen is a little smaller than in the concept (about 34% of the width instead of about 37%) so the cup never occludes its transport.
5. **Dock.** Its actions differ from the concept's tabs: *Our day, Reply, Love* plus Captions, Sound, Recenter, Reset and Leave. Those are the room's real actions; companion navigation is not duplicated in the headset.

### Our day — `compare/our-day.jpg`

Now matching the concept:

- **Chapters:** photographic chapters dominate, with a portrait rendered from each person's 3D character overlapping each chapter.
- **Controls:** a single control strip holds play, the *Our day · 1:25* label, a progress bar, *Play our day* and *View sources*. A *Precomputed sample* chip makes clear no AI ran.
- **Waiting for you:** a voice-note card with a portrait, waveform, quote, *Reply*, and *Recorded message · 20:55 in Beijing*.
- **Sources:** the *Moments behind the story* row shows its first sources in view, including a working waveform tile.
- **Audience:** shown at the top right (*Only Adam + Maya*). The self-hosted Figtree font is used throughout.

Remaining differences:

- The portraits are our sculpted characters, simpler and less expressive than the concept's illustrated faces.
- The concept lists *Phone photo, Camera clip, Voice note* first under the sources. Ours lists the recap's sources in capture order: phone photo, voice note, phone photo, then *All 4*.
- Durations differ because they come from the sample data: 1:25 and 0:09 here, against the concept's 1:42 and 0:22.
- The concept's photos are warmer sunset scenes.

### Share a moment — `compare/share.jpg`

Now matching the concept:

- **Composer:** dark translucent composer with tabs, file name, and a large photographic clip preview with a play overlay.
- **Clip details:** metadata chips (*Camera import · 00:08 · Ready*) and a trim filmstrip built from the clip's real frames, with a highlighted window, drag and keyboard handles, and a *00:03 – 00:08* readout.
- **Audience panel:** 1:1 and 1:many option cards (✓ on the selected one), the circle chosen with an icon, recipients with 3D portraits and checkboxes, *Only selected people can view this moment*, the AI-recap toggle, *Review recap before sharing*, a mint *Share with family* button and *Save privately*.

Remaining differences:

- The caption field sits just below the trimmer, under the fold at 1672×941.
- The sample family circle has two recipients (Maya, Lina), not three.
- Source and capture time sit in a collapsible *Source and time* panel.

### Together and keepsakes — `compare/together.jpg`, `compare/keepsakes.jpg`

Now matching the concept:

- **Clocks:** matched portrait clock cards (Adam · Dubai 17:05; Maya · Beijing 21:05) with long dates.
- **Overlap card:** one overlap/connect card holds the windows in both cities, the *Quiet hours respected* and *Sample schedules* chips, and the actions:
  - *Open our room*
  - *Not now*
  - *I'm free now · 30 min*
  - a compact *Watch together (simulated)*
  - *More openings*
- **Keepsakes:** three cards rendered from the actual 3D keepsake models, with the same light rig and camera and walnut plinths:
  - a framed first shared day
  - two linked ceramic rings
  - a memory tree that grows blossoms with shared days, never shrinking

Remaining differences:

- There is no *Both marked free now* badge in this state, because the sample schedules aren't both free at 17:05. The badge appears only when it is true.
- The concept's linked sculpture is an organic intertwined form; ours is two glazed tori.
- The concept's framed print carries a postage stamp.
- The concept shows a desktop toast (*A shared moment is waiting*); ours keeps notifications in the bell inbox.
- Our connect row carries more actions than the concept's two (*Open our room*, *Not now*), kept to one row.

## Room safe-area results

`window.__sameskySafeArea()` projects these items and fails if any is clipped by the viewport or the 2D header:

- the screen header and the whole screen panel
- the primary action
- the presence badge
- the cup label
- the onboarding hint
- the dock

It also fails if the badge or hint overlaps the screen, if the badge overlaps the cup label or hint, or if the cup label overlaps the dock or screen.

| Viewport | Violations | Draw calls | Triangles |
|---|---|---|---|
| 1672×941 | none | 101 | 136k |
| 1440×900 | none | 97 | 123k |
| 1024×768 | none | 101 | 136k |
| 390×844 (portrait phone) | none | 94 | 122k |

Layout adapts per aspect ratio:

- **Landscape:** the screen is anchored to the top-left below the header and the dock to the bottom centre on the table edge.
- **Portrait:** stacked — the screen on top, the partner in the middle with the badge moved to her left, and the onboarding instruction folded into the cup card.
- **Labels on flat screens** keep a minimum legible text size and lie parallel to the image plane, so they never keystone. In a headset they keep real-world sizes and face the head.

## Room interaction smoke test

`web/scripts/room-interactions.mjs` clicks through IWSDK's canvas pointer events at the projected screen
position of each target. It confirms that the new single-canvas dock with invisible hit areas and the
re-modelled table objects still work. Result:

| Action | Check | Result |
|---|---|---|
| Click the cup | Voice note plays; Maya's presence switches from simulated to recorded | PASS |
| Click *Reply* on the dock | Reply sheet opens (pressables 17 → 21) | PASS |
| Click *Captions* on the dock | Captions toggle | PASS |
| Click *Our day* on the dock | The story starts on the screen | PASS |

Hand-tracked pinch and poke were not exercised; that needs a headset.

## Captures in `after/`

| Shot | What it shows |
|---|---|
| `room-1672x941.jpg` (+1440×900, 1024×768, 390×844) | Default room (sample, first visit) |
| `room-watch-1672x941.jpg` | *Watch together · simulated*: the clip on the shared screen |
| `room-xr-1672x941.jpg` | `?preview=xr`: the headset (table-space) layout from a seated eye position. An approximation, not an on-device capture |
| `room-loading-1672x941.jpg` | First-load progress (the avatar download is held open) |
| `room-error-1672x941.jpg` | Recoverable asset error with *Try again* (the avatar download fails) |
| `room-signin-1672x941.jpg` | Live mode without sign-in |
| `day-1672x941.jpg` (+ dark, 1440×900, 1024×768, 390×844) | Our day |
| `day-circle-1672x941.jpg` | Our day in the three-person family circle |
| `day-loading-1672x941.jpg` | First paint while portraits render (placeholders) |
| `share-1672x941.jpg` | Share, empty |
| `share-populated-1672x941.jpg` (+1440×900, 1024×768, 390×844) | Share with the sample clip loaded and *Private circle* selected (not published) |
| `together-1672x941.jpg` (+ dark, 1440×900, 1024×768, 390×844) | Together |
| `keepsakes-1672x941.jpg` (+ dark) | Keepsakes |
| `settings-1672x941.jpg` (+ dark) | Settings with the avatar editor (face preset, live 3D portrait) |
| `welcome-1672x941.jpg` | First run, signed out |

## Rendering pipeline checks

- **Single Three.js runtime.** The one-three package override (super-three 0.181 alias) is kept. IWSDK's defaults were read first: it sets only the output colour space, so the room sets Neutral tone mapping, exposure 0.92, and PCF soft shadows.
- **Colour management.**
  - sRGB: colour maps, photos, video, canvas UI and the window plate.
  - Linear: normal, roughness and AO maps (`pbr()` in `env.ts`, `tex()` in `keepsakes3d.ts`).
  - The grey-washed walnut scan is tinted, not re-coloured in the file.
- **Image-based light.** The HDR goes through IWSDK `IBLTexture` on the level root, which PMREM-prefilters it in its EnvironmentSystem. The portrait renderer PMREMs the same HDR.
- **Unlit interface.** All panels, labels, the media screen and the window plate use `toneMapped: false`, so interface colours aren't tone mapped twice.
- **Materials.**
  - The CC0 props keep their UVs and PBR maps.
  - The characters carry baked shading cues in vertex colours and a knit normal map.
  - Contact shadows sit under the table, chair, side table, objects and keepsakes.
  - One bounded directional shadow covers the table area. Only the table, character and handled objects cast; static decor doesn't.
- **Disposal and allocation.**
  - Environment, objects, avatar, panels and screen textures have explicit `dispose()`. Old media textures are disposed when they are replaced.
  - Caption, header, transport, dock and label canvases redraw only when their content changes (memo keys).
  - The per-frame loop (`RoomSystem.update`, `Avatar3D.update`, `CharacterModel.update`) uses module-level scratch objects and doesn't allocate in steady state.
- **Draw calls.** Same-material parts are merged: frame walnut parts, mug body and handle, window mullions, tree branches. Blossoms and leaves are instanced, and the dock is one canvas with invisible per-button hit areas.

## Performance

- **Desktop** (SwiftShader software WebGL, which says nothing about headset frame rate): 94–105 draw calls, 122k–136k triangles and 53–58 textures across the default room at four sizes, the watch-together room and the headset preview.
- **Critical room art:** about 6.3 MB. See the budget table in the asset manifest.
- **Room bundle:** 1.53 MB gzipped (IWSDK's entry pulls in UI fonts and physics we don't use).
- **Companion:** 285 KB main chunk; three.js loads lazily only for portraits.

## Device verification

- **No Quest 3/3S hardware was used.** Immersive rendering, hand-tracked pinch and poke on the new dock, frame rate with PMREM IBL and the shadow light, texture memory and render scale are unmeasured on device. This includes `room.html?debug=1`, which shows fps, draw calls, triangles and textures in the header.
- **IWER emulation:** the emulated Quest 3 session doesn't start in headless Chrome (an IWER `cancelAnimationFrame` error), so the headset layout is documented with `?preview=xr` instead.
- **To record on device:** device and browser version, render scale, frame time, draw calls, triangles, texture memory and network payload, in the default room and while a clip plays.

## Changed files

- **Room** (`web/src/xr/`):
  - `env.ts`: CC0 furniture, PBR, window plate, light, measured seat.
  - `objects.ts`: glazed mug, walnut frames, postcard, note, hit proxies.
  - `avatar3d.ts`: character adapter.
  - `main.ts`: loading and error, IBL and tone mapping, composition, dock, labels, safe area, capture and preview modes, allocation-free loop.
  - `ui3d.ts`: glass, `FittedPill`, `FittedCard`, Figtree.
  - `web/room.html`: header, progress, retry.
- **Shared** (`web/src/shared/`):
  - New: `character.ts`, `portrait.ts`, `keepsakes3d.ts`.
  - Changed: `avatar.ts`, `sample.ts`, `contracts.ts`.
- **Companion** (`web/src/companion/`):
  - `styles.css` (rewritten), `ui.tsx`, `App.tsx`, `main.tsx`.
  - Views: `OurDay.tsx`, `Share.tsx`, `Together.tsx`, `Keepsakes.tsx`, `Settings.tsx`, `Welcome.tsx`.
- **API:** `api/samesky/routers/core.py` (`face` avatar preset) and `api/tests/test_realtime.py` (its test).
- **Pipeline:**
  - `scripts/assets/`: `fetch_polyhaven.py`, `sculpt_characters.py`, `build_sample_media.py`.
  - `web/scripts/`: `optimize-assets.mjs`, `capture.mjs`, `room-interactions.mjs`, `shot.mjs`, `measure-props.mjs`.
  - `scripts/visual_qa_compare.py`.
  - `scripts/fixtures/make_illustrated_media.py` (moved).
  - Dev lineup: `web/dev/character.html`, `web/src/dev/preview.ts`.
- **Assets:** `web/public/assets/**` (replaces `web/public/sample/`).
- **Docs:** this file, `ASSET_MANIFEST.md`, `asset-provenance.json`, `REQUIREMENTS_MAP.md`, `STATUS.md`, `README.md`, and `references/`, `visual-qa/`.

## Known limitations of the asset production

- No DCC tool (Blender) was available in this environment. The characters were sculpted from signed-distance fields in Python, which limits fine detail (lips, lashes, hair strands) and rules out skinned deformation and blend-shape expressions. A Blender pass to retopologize, rig and add expressions is the clearest next improvement.
- Sample photos and the clip are crops of CC0 panoramas. They are believable but contain no people, and the clip is a simulated camera path, not a real recording.
- The voice note is a synthesized OS text-to-speech voice and is labelled as such.
