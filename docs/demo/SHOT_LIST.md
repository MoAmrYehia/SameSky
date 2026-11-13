# "I Saved You a Seat" (demo): shot list

Every shot of the demo video, the take it comes from, and what it shows. The cut itself is
[`scripts/demo/demo_cut.json`](../../scripts/demo/demo_cut.json); the times below are the film's.

**Capture environment, all lounge shots:** the native Quest app (Unity, `native/quest`) in Unity Play mode with Meta
XR Simulator as its OpenXR runtime, on a Windows laptop. Not a headset. Hands are the simulator's hand-tracking input,
driven by `scripts/demo/film.ps1`; no controllers. Takes are 30 fps, filmed on 12 November 2026.

**Content, all shots:** the app's offline sample day. Adam (the person in the headset) and Maya are fictional; photos
and the clip are stock and labelled so by the app; the recap is precomputed; Maya's voice note is a synthesized voice.
**Nothing in the film is live**: no second person, no network call to another user, no AI call.

Camera: *first person* is the capture camera sitting where the headset is and turning with it; *steady* is the capture
camera placed near the seat on a slow move; both draw the app's own scene with its own renderer
(`Assets/SameSky/Scripts/App/FilmRig.cs`). *Enlarged* means the edit shows part of the captured frame.

| Film time | Shot | Take and seconds used | Camera | What the app is doing | Notes |
| --- | --- | --- | --- | --- | --- |
| 0:00–0:07 | The empty seat | `s01-hook` 0.3–7.8 | steady, no hands | The lounge at dusk. Maya's chair is empty, labelled "Maya · not here now · 17:35 for them". On the table: two shared photos in frames, the cup, and the cue "Pick up the cup to hear Maya's message". | The first words of Maya's voice note are laid over this shot from the cup take, subtitled as a recorded message. The question on screen is the film's. |
| 0:07–0:11 | Not here now | `s02-away` 0.8–4.2 | steady | The presence label over her chair, with her local time. | |
| 0:11–0:20 | Together | `s03-together` 7.4–16.4 | first person, enlarged after the press | A hand presses **Together** on the dock (0:13). The panel: You · Dubai 13:43, Maya · Beijing 17:43; "Sample: Maya is fictional and their presence is simulated"; "Today 17:00–18:30 · 90 min you both have"; **I'm free now (1 hour)**; **Sit with Maya**. | Availability is the sample's fixed data. "Sit with Maya" is not pressed: no live session is filmed. |
| 0:20–0:38 | The cup | `s04-cup` 5.5–24.0 | first person, **one take, 18.5 s** | The hand reaches and palm-grabs the cup (0:25.5). Her voice note plays from it for nine seconds with the app's captions and the "Recorded" badge (the card's second line, "... Synthesized voice", is cut short by the card); the chair's label reads "Maya · recorded message, not live". When the note ends (0:34.8) the sample seats a simulated Maya and the label changes to "simulated presence (fictional sample)". | The sound is the take's own. The film adds "Offline sample: Maya's arrival is simulated. It is not a live connection." |
| 0:38–0:42 | Maya | `s12-maya` 1.0–4.2 | steady | Maya's figure in her chair under the app's label "Maya · simulated presence (fictional sample)". | Simulated presence. |
| 0:42–0:47 | Our day | `d20-day` 21.6–26.6 | first person, enlarged | The **Our day** panel: "Just the two of you · Adam + Maya", a picture per chapter, "Two walks under one sky · 2 chapters · 1:25 · made from 2 photos, a clip and a voice note", and the generator label "Precomputed sample recap (no AI call)". The hand presses **Play our day** (0:44.2); the story starts on the room's screen. | The film adds "A sample story, put together in advance: no AI call is made here." |
| 0:47–0:50 | Her photo | `d21-story` 1.0–4.6 | steady | Chapter 1 on the screen: Maya's photo, its caption, "Maya · 18:40 GMT+8 · Phone · Stock photo (sample)". The story panel beside it. | |
| 0:50–0:55 | A heart | `d20-day` 37.4–42.2 | first person, enlarged | The hand presses **Love** (0:51.8); the toast "Sent." | In the sample the reaction is kept on the device; nobody receives it. |
| 0:55–0:59 | His clip | `d21-story` 53.2–57.0 | steady | Chapter 2: Adam's clip plays on the screen, "Adam · 15:10 GMT+4 · Camera · Stock footage (sample)". Ordinary video stays on the flat screen. | |
| 0:59–1:02 | Reply | `d07-reply-open` 14.9–18.6 | first person | The hand presses **Reply** beside the cup (1:00.4); the keyboard "Reply to Maya" opens. | |
| 1:02–1:05 | The first key | `d08-reply-type` 15.6–18.6 | steady, in front of the keyboard | The hand presses S (1:04.7). | Real speed. |
| 1:05–1:12 | Typing | `d08-reply-type`, a third of a second around each of the next 19 presses | same | "Same seat tomorrow?" key by key, with the switch to the symbols layout for the question mark. | **Edited for time:** each press is at its real speed; the waits between presses (about 25 s each, the simulator moving the hand) are cut. The film says so on screen. |
| 1:12–1:16 | Done | `d08-reply-type` 540.6–545.2 | same | The hand presses **Done** (1:14.2); the keyboard closes and the reply stands on the table as a card. | |
| 1:16–1:21 | The card | `d09-card` 0.5–4.7 | steady | "Same seat tomorrow? — Adam" beside the cup. | Kept in the sample on the device. |
| 1:21–1:35 | From the phone | companion recording 1.6–20.6, played 1.3× faster | the companion web app at 430×932, framed as a phone | **Share a moment** in sample mode: "Use a sample clip", preview and trim (0:03–0:08 of the clip), "Who can see this?" → One person · 1:1 → Maya, **Share with Maya**, the confirmation "Only Maya", then the moment listed as shared. | Recorded by `web/scripts/demo-companion.mjs`. No account, nothing uploaded; the clip is stock. The film notes that camera, drone and AI-glasses footage comes in as an exported file. |
| 1:35–1:39 | Keepsakes | `d11-keepsakes` 16.6–20.2 | first person, enlarged after the press | The hand presses **Keepsakes** (1:36.3). The panel: First Shared Day, A Conversation Continued, Seven Days of Us (5 of 7 shared days), past stories, the inbox. | Sample data. |
| 1:39–1:42 | The shelf | `d12-shelf` 2.6–5.9 | steady | The walnut shelf in the alcove with the earned keepsakes. | |
| 1:42–1:45 | Marketplace | `d22-market` 60.5–63.7 | first person | The hand tries the **Evening study** look (1:43.4): chairs, table and rug change at once. "Nothing here is sold and there are no purchases"; "Trying it out · not kept yet". | |
| 1:45–1:48 | And back | `d22-market` 94.5–97.3 | first person | The hand picks **Walnut and ivory** again (1:45.6): the room as it comes. | The look was not kept. |
| 1:48–1:53 | Pause, resume | `d13-pause` 62.4–67.0 | first person, enlarged | The **Paused** panel ("Sound and stories are paused"; captions, sound, recenter, settings, leave). The hand presses **Resume** (1:51.0); the dock returns. | |
| 1:53–2:11 | The seat is taken | `s11-payoff` 2.0–13.6, last frame held | steady, the opening angle | Maya's simulated figure in the chair that was empty, the reply card by the cup. | The film adds "Offline sample: Maya's presence is simulated. It is not a live session." End card from 2:00: title, "Save someone a seat.", and the capture line. |

## The takes

| Take | Filmed | Frame | In the film |
| --- | --- | --- | --- |
| `s01-hook`, `s02-away`, `s03-together`, `s04-cup`, `s11-payoff`, `s12-maya` | first session | 1920×1080 (`s03`, `s04`: 2560×1440) | the empty seat, Together, the cup, Maya, the end |
| `d07-reply-open`, `d08-reply-type`, `d09-card`, `d11-keepsakes`, `d12-shelf`, `d13-pause` | second session | 1920×1080 (`d07`, `d11`, `d13`: 2560×1440) | the reply, keepsakes, pause |
| `d20-day`, `d21-story`, `d22-market` | third session, after the two story-screen fixes ([README](README.md#found-while-filming)) | 2560×1440 (`d21`: 1920×1080) | Our day, the Marketplace |
| `companion/` | the web companion, sample mode | 860×1864 | the phone insert |

Each session starts from a fresh sample profile, so the room is in the same state at the same point of the story:
in the first, the cup has not been touched; in the others, the note has been heard and Maya's simulated figure is
seated. The reply card reads the same in the first and second sessions ("Same seat tomorrow?").

## Not in the film

| Feature | Why |
| --- | --- |
| A live session with two people present | Not filmed: it needs two signed-in accounts and the realtime service. The film resolves on the asynchronous experience and labels Maya's presence as simulated. |
| An AI-drafted recap, and approving one | The sample's recap is precomputed, and labelled so. |
| A Quest headset | Captured in Meta XR Simulator only. |
| Uploading a file of your own from the companion | Sample mode with the stock clip is shown. |
| A voice reply, the photo-derived avatar, settings, the family circle, single Marketplace pieces | Left out for length. |
