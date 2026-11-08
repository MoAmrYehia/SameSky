# Judge testing guide

**Device:** Meta Quest 3 or 3S, Quest Browser. **Input:** hands only (controllers optional). **Posture:** seated.
**Time:** about 3 minutes. No account, headset pairing, push permission or AI credits needed.

1. Open the build URL + `/room.html?mode=sample` (or open the build URL and press **Enter the room in VR**).
2. Press **Enter in VR**. You're seated at a round table by a window at golden hour. Maya (a fictional person)
   sits across from you; her label says **Simulated (sample)** — this is not a live person.
3. **The defining moment:** a cup is glowing and a hint says *Something is waiting — pinch the glowing cup*.
   Reach out and pinch-grab the cup (or point and pinch from where you sit). Maya's voice note (a synthesized
   sample voice, labelled as such) plays **from the cup**, with captions under the screen; her label changes to **Recorded message · 20:55 in Beijing**
   and her avatar waves (app animation). Let go: the cup stays on the table where you put it.
4. **Leave something for later:** pinch **Reply** on the dock in front of you and choose a phrase. A folded note
   appears beside the cup. It stays there after you leave and come back.
5. Pinch a **photo frame** or the **postcard** (Adam's clip: a camera pan *simulated* from a CC0 panorama and
   labelled **simulated camera pan**, not a real recording) — it opens on the shared screen.
6. Pinch **Our day** to watch the 1–2 minute recap: chapters per person, every line labelled *From their caption*,
   *In their words* or *Looks like · AI interpretation*. In the sample it is **precomputed** (no AI call).
7. Pinch a **keepsake** on the shelf to the right (First Shared Day, A Conversation Continued, Seven Days of Us 5/7).
8. Dock: **Captions** on/off, **Sound** mute, **Recenter** (re-seats the table in front of you),
   **Reset** (objects back to their places), **Exit**.

Phone/desktop: the same sample runs at the build URL → **Try a sample day** (Our day, Share a moment with the
explicit audience confirmation, Together with live clocks and the Dubai/Beijing overlap, Keepsakes, Inbox).
Settings → **Reset the sample** restores the original day. Sample edits never leave the browser.

No headset at hand: run the app locally (`cd web && npm run dev`) and open `http://localhost:5173/room.html?mode=sample`
in desktop Chrome — Meta's Immersive Web Emulator (an emulated Quest 3 with hands and controllers) is built into the
development server; press **Enter in VR** and use its DevUI. See `docs/EMULATOR.md`. It does not replace a headset.

Photo-derived avatars (signed-in accounts only; the sample's people are fictional and use labelled demo
avatars): Settings → **Your avatar** → *Create from my photos* explains the provider and privacy, then shows the
private review (front, three-quarter, side, seated) and **This looks like me — use this avatar**. Generation needs
the operator's Avatar SDK credentials; without them the app says so and offers *Import a model*. See
`docs/AVATARS.md` for what has and hasn't been verified.

Privacy demonstration: switch the room selector to **Family circle** — the card *Private stays private*
explains that the direct room's moments are absent from the circle's story, feed, keepsakes and notifications.
