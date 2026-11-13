# Visual QA

Two passes are recorded here. Captures are real renders of the running app, not mock-ups; every text and control
in them is live.

## Pass 2 — realistic scenes and photo-derived avatars (8 November 2026)

This pass follows the instructions of 7 November: replace doll avatars with private, owner-approved,
photo-derived avatars (see [AVATARS.md](AVATARS.md)); make the room less amber with fewer glossy highlights and a
quieter dock; make no geographic claim for the window plate; and label the simulated sample clip as simulated.

- **Pass 1 captures:** `docs/visual-qa/pass1/`, copied from the captures committed after pass 1.
- **Pass 2 captures:** `docs/visual-qa/after/`, same scripts, same frozen clock.
- **Side-by-side:** `docs/visual-qa/compare/pass2-*.jpg` (pass 1 | pass 2).

### What changed in the room

| Item | Pass 1 | Pass 2 | Why |
|---|---|---|---|
| Window key light | `#ffdcb8`, 1.55 | `#fff3e8`, 1.45 | A restrained warm key instead of an orange cast on skin, hair and table |
| Hemisphere fill | `#fff4ea` / ground `#4a3a2c`, 0.28 | `#f3f5f7` / `#57514b`, 0.30 | Neutral fill; brown ground bounce removed |
| Lamp | `#ffc27a`, 1.6 | `#ffd6a6`, 1.05; shade emissive `#ffe0b8` at 1.1 | Still reads as a lamp, no longer tints the table |
| Image-based light | 0.70 | 0.60 | Less warm interior reflection |
| Exposure / background | 0.92 / `#2a211b` | 0.95 / `#24221f` | Neutral; highlights stay below clipping |
| Table and side table (CC0 scans) | Model roughness map as authored: a wet-varnish look with broad mirror highlights | `satin()`: roughness × 1.6 (clamped at 1), reflections × 0.65, albedo × `#e4dfdc` | Satin oiled walnut with the grain kept |
| Walnut tint (frames, plinths, lamp) | `#e8a066` (orange stain), clearcoat 0.22 | `#d6a07a`, clearcoat 0.12 | Natural walnut brown |
| Floor, walls, ceiling, sills, rug | Warm multipliers (`#d6bfa6`, `#f0e3d2` …) | Neutral or slightly cool (`#bcc0c8`, `#ebe6df`, `#e2ddd6`, `#e5dfd7`, `#e6e1d9`) | Takes the orange stain out of the oak and plaster |
| Window plate | Raw sunset grade | × `#e8eef7` | Tempers the sunset without greying the photograph |
| Dock | Brown glass at 62 % alpha, bright top highlight | Neutral glass at 50 %, faint highlight, thinner border | Quieter. White labels still measure about 5:1 against the lightest floor behind them |
| Screen frame | Brown glass at 52 %, strong white rim | Neutral glass at 34 %, soft rim | The media, not the frame, carries the panel |
| Labels (presence, cup, captions) | `rgba(24,24,22,.72)` | `rgba(22,23,22,.66)` | Lighter, same contrast class |
| Demo avatars | Large irises (0.70 rad), glossy eyes (clearcoat 1), pink-orange skin sheen | Human-sized irises (0.56 rad), clearcoat 0.6, matte skin with a faint neutral sheen | Less doll-like; still clearly labelled demo avatars |

Honest labels:

- The window card now says *A sample lounge. The view outside is a CC0 photograph, not a live view of anyone’s city.*
  No Dubai claim anywhere: Dubai appears only as Adam’s time zone.
- The sample clip is labelled *Simulated camera pan* in the companion and the composer, and *Adam’s simulated
  camera pan* on the room screen. The voice is labelled *Synthesized voice note* (*Synthesized voice* on the cup),
  and the photos *Sample photo*.
- *Since 12 November* the sample photos and the clip are real stock photographs and footage with credits (see
  `ASSET_MANIFEST.md`), and the clip's label is *Sample clip*. The captures on this page were taken before that.

### Companion

- **Settings overflow (reported at 1440 px: 1523 px page).** The grid uses `minmax(min(340px,100%),1fr)` with
  `min-width:0` children. The availability rows wrap through a container query. `web/scripts/overflow-check.mjs`
  checks the page and every card’s controls at 1672, 1440, 1024 and 390 px, and all fit.
- **Your avatar** is the first card in Settings: the current avatar, *Create from my photos*, *Import a model*,
  earlier versions, sharing and removal, and the demo-avatar options.
- **Onboarding:** *Our day* asks live users with a demo avatar to create theirs (dismissible).

### Avatar setup and approval screens

From `web/scripts/avatar-e2e.mjs`: two new local accounts, the non-photo **test mannequin**, no real person’s
photo. Captured at 1672×941, 1440×900 and 390×844 (plus the action rows on the phone):

| Shot | What it shows |
|---|---|
| `avatar-onboarding-*.jpg` | *Our day* prompt: “Let people recognise you at the table” |
| `avatar-consent-*.jpg` | Before you start: what is made, who processes the photo, privacy, scope picker, and the honest “generation isn’t set up on this server” notice |
| `avatar-review-*.jpg` | Does this look like you? Front, three-quarter, side and seated views in neutral light, the turnable model and the approval button |
| `avatar-active-*.jpg` | In use: version, approval date, who can see it, sharing and removal |
| `avatar-together-1440x900.jpg` | The partner’s companion portrait, rendered from the approved model |
| `avatar-room-live-1440x900.jpg` | The partner live across the table as the approved skinned model (two desktop browsers) |
| `avatar-room-stopped-sharing-1440x900.jpg` | Seconds after *stop sharing*: the open room shows the labelled demo avatar instead |
| `avatar-room-emulator-1440x900.jpg` | The same live partner seen from inside an immersive session in Meta's emulator (emulated Quest 3, seated 1.2 m eye height) |

The test mannequin is deliberately crude and is not a likeness. **Likeness quality was not assessed**, because
no consenting real-photo subjects were available.

### Room safe area and cost (pass 2)

The same `window.__sameskySafeArea()` probe as pass 1 (see below), on the sample room:

| Viewport | Violations | Draw calls | Triangles | Textures |
|---|---|---|---|---|
| 1672×941 | none | 101 | 136k | 58 |
| 1440×900 | none | 97 | 123k | 58 |
| 1024×768 | none | 101 | 136k | 58 |
| 390×844 (portrait phone) | none | 94 | 122k | 53 |

The light and material changes add no draw calls or geometry. A new live room (one text moment on the table) with
the partner as a live, skinned photo-path figure (the 17.6k-triangle test mannequin) measured 52 draw calls, 89k
triangles and 62 textures at 1440×900. A real provider export will be heavier than the mannequin; the sample room’s
136k triangles already include a ~45k-triangle demo character, so a 30k–50k face-preserving avatar LOD fits the
150k–200k target. These are software-WebGL counts
from headless Chrome. They say nothing about headset frame time, and file size is not GPU memory.

The live-room captures were also checked with the safe-area probe: no violations with the photo-derived avatar
seated across the table.

### In Meta's emulator (headset view)

`docs/visual-qa/emulator/` holds frames from a real `immersive-vr` session in Meta's Immersive Web Emulator
(emulated Quest 3, seated eye height 1.2 m), taken by `web/scripts/xr-emulator-test.mjs`. They show emulated hands
and controllers and Meta's DevUI overlay, which is the emulator's own UI, not SameSky's:

| Shot | What it shows |
|---|---|
| `01-seated-hands.jpg` | Seated view after poking Recenter: screen left, Maya across the table, cup card, dock at reach |
| `02-cup-point-pinch.jpg` | Point at the cup and pinch: the voice note plays on the screen, Maya waves, badge *Recorded message* |
| `03-reply-sheet.jpg` | Reply sheet opened by poking the dock: below Maya’s face, the cup card hidden behind the modal |
| `04-controller-aim.jpg`, `05-controller-play-our-day.jpg` | Touch controller aimed at *Play our day*, then the story playing |
| `06-after-exit.jpg` | After poking Leave: back to the welcome card |

The emulator found five XR bugs that desktop clicks could not (a session that died on start, a phantom touch
volume, table objects unreachable by ray, overlapping labels, a sheet covering the other person's face). They
are fixed and listed in [EMULATOR.md](EMULATOR.md).

### First-run journey

`docs/visual-qa/onboarding/` holds the new-account journey at 1440×900 and 390×844 (`web/scripts/onboarding-e2e.mjs`):

1. welcome;
2. setup: you, your room, invite (copy and share), avatar, usual times, done;
3. "waiting for someone" on Our day, Together and Share;
4. room settings;
5. the invite link opened by the second person, their first Our day, and the pair in Together, each with a
   distinct starter demo avatar.

### Remaining visual gaps

- The demo avatars are still stylized sculpts with rigid parts. People who approve a photo-derived avatar
  replace them; the demo stays labelled.
- The room’s warmth now comes mostly from the sunset photograph itself. A daylight plate would remove the rest.
- Headset rendering (frame time with two skinned avatars, texture memory, the satin table under real IBL on
  device) is unmeasured; see `docs/STATUS.md`.

## Pass 1 — concept fidelity (6 November 2026)

> Pass 1’s own captures are kept in `docs/visual-qa/pass1/` (the comparisons below read them from there); `after/`
> now holds pass 2. Figures and discrepancy lists in this section describe the state after pass 1.

This records what the visual enhancement changed, how it was checked, and what still differs from the four
concept images in `docs/references/`. Captures are real renders of the running app (sample mode), not mock-ups.
Every text and control in them is live.

- **Before:** `docs/visual-qa/before/` (the app before this pass).
- **After:** `docs/visual-qa/after/`.
- **Side-by-side:** `docs/visual-qa/compare/` (concept | before | after).
- **Provenance** of every shipped asset: [ASSET_MANIFEST.md](ASSET_MANIFEST.md) and [asset-provenance.json](asset-provenance.json).

### How the captures were made

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

### Side-by-side comparisons and discrepancy lists

#### Shared room — `compare/room.jpg`

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

#### Our day — `compare/our-day.jpg`

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

#### Share a moment — `compare/share.jpg`

Now matching the concept:

- **Composer:** dark translucent composer with tabs, file name, and a large photographic clip preview with a play overlay.
- **Clip details:** metadata chips (*Camera import · 00:08 · Ready*) and a trim filmstrip built from the clip's real frames, with a highlighted window, drag and keyboard handles, and a *00:03 – 00:08* readout.
- **Audience panel:** 1:1 and 1:many option cards (✓ on the selected one), the circle chosen with an icon, recipients with 3D portraits and checkboxes, *Only selected people can view this moment*, the AI-recap toggle, *Review recap before sharing*, a mint *Share with family* button and *Save privately*.

Remaining differences:

- The caption field sits just below the trimmer, under the fold at 1672×941.
- The sample family circle has two recipients (Maya, Lina), not three.
- Source and capture time sit in a collapsible *Source and time* panel.

#### Together and keepsakes — `compare/together.jpg`, `compare/keepsakes.jpg`

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

### Room safe-area results

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

### Room interaction smoke test

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

### Captures in `after/`

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

### Rendering pipeline checks

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

### Performance

- **Desktop** (SwiftShader software WebGL, which says nothing about headset frame rate): 94–105 draw calls, 122k–136k triangles and 53–58 textures across the default room at four sizes, the watch-together room and the headset preview.
- **Critical room art:** about 6.3 MB. See the budget table in the asset manifest.
- **Room bundle:** 1.53 MB gzipped (IWSDK's entry pulls in UI fonts and physics we don't use).
- **Companion:** 285 KB main chunk; three.js loads lazily only for portraits.

### Device verification

- **No Quest 3/3S hardware was used.** Immersive rendering, hand-tracked pinch and poke on the new dock, frame rate with PMREM IBL and the shadow light, texture memory and render scale are unmeasured on device. This includes `room.html?debug=1`, which shows fps, draw calls, triangles and textures in the header.
- **IWER emulation:** the emulated Quest 3 session doesn't start in headless Chrome (an IWER `cancelAnimationFrame` error), so the headset layout is documented with `?preview=xr` instead.
- **To record on device:** device and browser version, render scale, frame time, draw calls, triangles, texture memory and network payload, in the default room and while a clip plays.

### Changed files

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

### Known limitations of the asset production

- No DCC tool (Blender) was available in this environment. The characters were sculpted from signed-distance fields in Python, which limits fine detail (lips, lashes, hair strands) and rules out skinned deformation and blend-shape expressions. A Blender pass to retopologize, rig and add expressions is the clearest next improvement.
- Sample photos and the clip were crops of CC0 panoramas when these captures were taken (believable, but no people, and the clip a simulated camera path). Since 12 November they are real stock photographs and real footage, credited; the room and companion haven't been re-captured with them.
- The voice note is a synthesized OS text-to-speech voice and is labelled as such.
