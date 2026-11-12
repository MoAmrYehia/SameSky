# Judge testing guide

**Device:** Meta Quest 3 or 3S. **Input:** hands only; no controller is needed at any point. **Posture:** seated.
**Time:** about three minutes for steps 1 to 4, ten for all of it. No account, no phone, no network and no AI
credits are needed: everything below runs in the app's **sample day**. If the headset asks for microphone access
when the app first starts, either answer works: the microphone is only used when you record a voice note.

What has and hasn't been checked: every step in part A was run with simulated hands in Meta XR Simulator
([NATIVE_STATUS.md](NATIVE_STATUS.md)), and the web room in part B in Meta's Immersive Web Emulator
([EMULATOR.md](EMULATOR.md)). Neither has been run on a headset yet.

The people in the sample day are fictional. Its photos and clip are stock (credited and labelled), its voice note is
a synthesized voice (labelled), and Maya sitting down opposite you is the sample's **simulated presence**, not a live
person: the app's own labels say so at each of those moments.

## A. The native Quest app (the build in the Competition release channel)

Install SameSky from the release channel link and start it from your library. Put the controllers down.

1. **Welcome.** A small panel in front of you: press **Try a sample day** with a fingertip (or point and pinch).
   Captions and sound are the two chips under it; **Recenter** and **Leave** sit beneath the panel.
2. **The room.** You are seated at a round table in a lounge at dusk. The chair opposite is empty and labelled
   *Maya · not here now*, with her local time. A cup stands on the table with a cue: *Pick up the cup to hear Maya's
   message*.
3. **The defining moment: pick up the cup.** Close your hand around it or pinch it; without leaning forward you can
   also point at it and pinch to pull it to your hand. Maya's voice note plays **from the cup**, with captions and a
   card that shows who is speaking, a *Recorded* tag, **Pause**, **Replay** and **Reply**. Put the cup down on the
   table. When the note ends, Maya is seated opposite you under the label *simulated presence (fictional sample)*.
4. **Leave something for later.** Press **Reply**, type a few words on the keyboard that opens at the table edge,
   press **Done**. Your reply stands on the table as a small folded card, and is still there the next time you open
   the sample day (it is kept on the headset; nobody receives it).
5. **Our day** (dock, at the table edge in front of you). A short story made from the day's shared moments: press
   **Play our day**. It plays on the room's screen with captions; each chapter says whose it is and what it was made
   from, and the generator label reads *Precomputed sample recap (no AI call)*. **Love** leaves a reaction.
6. **Together.** Both people's local time, the next stretch you both usually have, **I'm free now (1 hour)** and
   **Sit with Maya** (in the sample this seats the simulated Maya; with accounts it starts a live session).
7. **Keepsakes.** What the two of you have earned (First Shared Day, A Conversation Continued, Seven Days of Us 5 of
   7), past stories and the inbox; the same keepsakes stand on the shelf in the alcove.
8. **Market.** Try another table, chairs, rug, lamp, plant, wall colour or floor, or a whole look, on the real room.
   **Keep** saves it for the room, **Put back** undoes it. Nothing is priced or sold.
9. **Share.** The sample walks through sharing one of its own photos with one person or the family circle: who
   receives it is named before the last button. Nothing is sent.
10. **Comfort.** **Recenter** (the round button at the end of the dock) puts the table in front of you again.
    **Pause** stops sound and stories; **Settings** has captions, volume, seat height and *Your hands* (six skin
    tones for the tracked hands). The room name above the dock switches to the **Family circle**, where nobody is
    seated as if present. Taking your hands out of view mid-press cancels the press; nothing fires.
11. **Leave:** Pause → **Leave SameSky**, or **Leave** on the welcome panel.

Signing in and creating an account need the SameSky service; the sample day needs nothing and is the judged flow.

## B. The WebXR room and the companion (a hosted URL)

Quest Browser on the headset; the same pages work on a desktop or phone with a click.

1. Open the build URL + `/room.html?mode=sample` (or open the build URL and press **Enter the room in VR**).
2. Press **Enter in VR**. You're seated at a round table by a window at golden hour. Maya (a fictional person)
   sits across from you; her label says **Simulated (sample)** — this is not a live person.
3. **The defining moment:** a cup is glowing and a hint says *Something is waiting — pinch the glowing cup*.
   Reach out and pinch-grab the cup (or point and pinch from where you sit). Maya's voice note (a synthesized
   sample voice, labelled as such) plays **from the cup**, with captions under the screen; her label changes to **Recorded message · 20:55 in Beijing**
   and her avatar waves (app animation). Let go: the cup stays on the table where you put it.
4. **Leave something for later:** pinch **Reply** on the dock in front of you and choose a phrase. A folded note
   appears beside the cup. It stays there after you leave and come back.
5. Pinch a **photo frame** or the **postcard** (Adam's clip: real stock footage of Dubai Creek, labelled
   **sample clip** and credited; nobody in the sample day filmed it) — it opens on the shared screen.
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
