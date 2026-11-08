# Testing the room in Meta's emulator

Date: 8 November 2026. SameSky's room is tested inside **Meta's Immersive Web Emulator (IWER)**, which
`@iwsdk/vite-plugin-iwer` injects on `localhost` as an emulated **Meta Quest 3**. A real `immersive-vr` WebXR
session runs in desktop Chrome, with emulated hand tracking and Touch controllers.

The emulator checks session setup, input wiring, layout and app state. It **cannot** measure Quest frame time,
comfort, hand-tracking quality or spatial audio on device. Those still need a headset (`docs/STATUS.md`).

## Try it yourself

```bash
cd web && npm run dev
```

1. Open `http://localhost:5173/room.html?mode=sample` in desktop Chrome. The emulator activates only on
   `localhost` and never in Quest Browser.
2. Press **Enter in VR**. Meta's DevUI appears with the headset pose, hand or controller panels, and draggable
   gizmos.
3. Switch between hands and controllers with the DevUI's input-mode button.
4. Drag the hand gizmo onto a dock button to poke it, or aim it at the cup and use the DevUI's pinch control.
   Shift+Arrow keys raise or lower the head. For a seated check, use about 1.2 m and press the dock's
   **Recenter**.

## Automated test

```bash
node scripts/xr-emulator-test.mjs --base http://localhost:5173 --out ../docs/visual-qa/emulator
```

The script starts the session, seats the headset at 1.2 m eye height, and drives the emulator's DevUI rig the
way dragging its gizmos would. It aims by measurement: in the emulator only, the room reports each input's
actual target ray and index fingertip every frame (`window.__sameskyInputs`), and the script corrects the pose
until it is on target. Every step checks the room's own state (`window.__sameskyState`) and saves a screenshot.
On Windows it uses the machine's GPU (ANGLE/D3D11); elsewhere it falls back to SwiftShader.

| Check | Input |
|---|---|
| Emulated Meta Quest 3 available; **Enter in VR** starts an immersive session | — |
| Poking **Recenter** re-seats the room for the seated eye height | hand, index-fingertip poke |
| Seated view: screen, presence badge, cup label, hint and dock are in view and clear of each other | — |
| Point at the cup and pinch: Maya's voice note plays and her badge switches to *Recorded message* | hand ray + pinch |
| Poking **Captions** toggles captions | hand poke |
| Poking **Reply** opens the reply sheet; **Not now** closes it | hand poke |
| Losing and re-acquiring hand tracking keeps the session and triggers nothing | tracking loss |
| Controller ray + trigger on **Play our day** starts the story | controller |
| Poking **Leave** ends the session; the welcome card returns | hand poke |
| No page errors | — |

Captures: `docs/visual-qa/emulator/` (seated view with hands, cup point-and-pinch, reply sheet, controller
aim, story playing, after leaving).

## What the emulator found (now fixed)

The pass-1 room had been checked only with desktop clicks, which use a different pointer path from a headset.
In the emulator, these broke:

1. **The session died on start.** Desktop Chrome now ships the WebXR Layers API, so three.js tried to build a
   native `XRWebGLBinding` around the emulator's session and the session ended immediately; this was the
   "IWER cancelAnimationFrame error" from pass 1. The IWER build bundled in the plugin (2.0.1) doesn't replace
   that global. `src/xr/emulator.ts` swaps in a binding without projection layers, but only while emulating,
   so three.js uses `XRWebGLLayer`. It never runs on a headset.
2. **Hands were stuck "touching" a phantom object.** `@pmndrs/pointer-events` also tests an `InstancedMesh` as
   a plain mesh with its *base* geometry. The memory tree's leaves were a unit sphere scaled down per instance,
   which made a 1 m invisible touch volume. Both hands sat inside it, IWSDK's touch pointer locked into
   "select", and no other input could reach anything. The leaves are now instanced at real size, and
   decorative instanced meshes opt out of touch tests (`keepsakes3d.ts`). **This would have broken hand input
   on a real Quest.**
3. **Table objects could not be selected from the seat.** IWSDK binds one grab handle per entity, and the
   one-hand handle denies ray events, so `DistanceGrabbable` never initialised and no ray reached the cup,
   frames or postcard. Each object now has an invisible far-select proxy that only XR rays may target
   (`pointerEventsType: { allow: 'ray' }`). Near pinch-grab and desktop click/drag are unchanged (`main.ts`).
4. **Overlapping labels above the dock.** A flash ("Recentered.") landed on the onboarding hint when the hint
   moved onto the dock. The flash now moves out of the hint's way.
5. **The reply sheet covered the other person's face** at eye level, and the cup card drew through it. In a
   headset the sheet now opens lower and closer, facing the person, below the line to the other person's face.
   The cup card hides while the sheet is open.

## Notes on the emulator

- Its DevUI owns the poses: every frame it copies its camera rig into the headset and its transform handles
  into the hands or controllers. Scripts must move those rig objects, not the device directly.
- A poke clicks only if the fingertip leaves the button within 800 ms (IWSDK's XR click threshold). At
  software-rendered frame rates a "slow" poke never clicks. That is an emulator artifact, not a Quest problem;
  the script taps quickly, as a real finger does.
- IWSDK gives a near touch target (within 15 cm) priority over a far ray. A hand resting next to the dock
  pokes the dock rather than pointing past it, so the script raises the hand to point, as a person does.
