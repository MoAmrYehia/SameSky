# Devpost submission text (draft)

Drafted on 13 November 2026 for the Meta VR Start Developer Competition form; updated on 17 November 2026, when
the WebXR room went live on GitHub Pages and became the project link. The rules ask for "a text description that
explains your inspiration, how you built it, future plans for improvement and a target launch date". Everything
below is written to be true of the repository as it stands on that date; [REQUIREMENTS_MAP.md](REQUIREMENTS_MAP.md)
has the evidence behind each claim.

## Before you paste

The text is complete. Four things are worth a look first:

1. **The project link is the hosted WebXR room**, so the description and the testing instructions describe what a
   judge sees there. The native Quest app is described as the second client. If you submit the native app's
   release channel instead, use the native testing instructions and the upload steps at the end.
2. **The video shows the native app** in Meta XR Simulator, not the web room. The rules accept simulator footage,
   and its description says which client it shows, but a judge will see differences (the web room has no Market,
   and its replies are chosen phrases rather than typed). A short clip of the web room in Meta's Immersive Web
   Emulator ([EMULATOR.md](EMULATOR.md)) would close that gap.
3. **The target launch date** at the end of *What's next* says March 2027. That date was picked for you; change it if
   you plan otherwise.
4. **How it was built, the last bullet,** says the app was built with Claude Code as the coding agent, directed by
   your brief and visual references. That is what the repository's history shows; word your own part as you see it.

The text also says nothing has run on a headset. Open the link on a Quest before you submit, and if you measure
anything there, change those lines to what you measured.

The video link is the only blank, marked **[in bold brackets]**.

## Form fields

| Field | Text |
| --- | --- |
| Project name | SameSky |
| Tagline (Devpost allows 200 characters; this is 155) | Your day, side by side. A shared room where a cup holds a voice note from someone far away, and your reply waits on the table for them. Hands only, seated. |
| Track | Entertainment |
| Division | New Experience (built from scratch; the repository's first commit is from 6 November 2026) |
| Link to the project (the form: "For WebXR projects provide your hosted URL") | https://moamryehia.github.io/SameSky/room.html |
| Video | **[the YouTube or Vimeo link of the 2:11 demo, "I Saved You a Seat"]** |
| Repository (optional) | https://github.com/MoAmrYehia/SameSky |

## Description

Paste from here to the next horizontal line.

---

## Inspiration

SameSky began with a couple in Dubai, one of them often away for work. A call needs both people free in the same hour, and across time zones that hour is hard to find.
Messages get through, but they scroll away. What goes missing is the feeling of having been in the same place.

I wanted a room that keeps things for you. You sit down and something from the other person is waiting. What you
leave is still there when they sit down.

## What it does

You sit at a round table by a window at golden hour, with Maya across from you. A cup on the table is glowing: it
holds her voice note. Reach out and pinch it, or point at it from your seat and pinch, and the note plays from the
cup, with captions. Choose a reply, and a folded note appears beside the cup for her to find. It is still there
when you come back.

Around that moment:

- **Our day:** a one-to-two-minute story made from the photos, clip and voice note two people chose to share,
  played on the room's screen. Every line says where it came from: their caption, their words, or an AI
  interpretation.
- **Things on the table:** photo frames and a postcard open on the shared screen when you pinch them.
- **Keepsakes:** small objects on a shelf, earned by sharing days, with the next one in progress.
- **A phone companion** at the same address, for sharing a moment with one person or with a family circle (a
  moment goes only to the people you name), and **Together**: both local times, and the next stretch of the day you
  both usually have free.
- **A native Quest app** (Unity) on the same service and the same sample day, which adds typed replies on a
  spatial keyboard and a **Market** to restyle the room you share. Nothing is sold.

Everything is done with your hands. No controller is needed at any point. It is made to be used seated, for a few
minutes at a time, and the dock in reach has captions, sound, recenter, reset and exit.

**What you will run.** The link opens the room's sample day in Quest Browser: press **Enter in VR**. It needs no
account and no install, and nothing you do leaves the browser. On a desktop or phone the same page works with clicks.
The people in it are fictional, its photos and clip are stock, and its voice is synthesized; the room labels each of
them, and labels Maya as simulated. Accounts, sharing from the phone and live two-person sessions are built and were
tested against a local server, not on headsets. They are not in this build because the service isn't hosted yet.

## How I built it

- **WebXR room:** Meta's Immersive Web SDK on three.js, in TypeScript. Built for hands: pinch to select and grab,
  point and pinch from the seat, and a dock you poke. It is a static site on GitHub Pages; the hosted build is the
  sample day, so it needs no server.
- **Headset app:** Unity 6 with Meta's XR Core SDK and Interaction SDK (209) over OpenXR. Built for hands: poke,
  point and pinch, hand grab and distance grab. The tracked hands are drawn with skin in a tone you choose, take the
  room's light and cast their shadow on the table.
- **Service:** FastAPI and SQLAlchemy with a background worker. Every shared moment carries the list of people it
  was shared with, and that list is checked on every read. A time-zone engine works out when two people overlap.
  82 automated tests cover privacy, withdrawal, retries and daylight-saving edges.
- **Companion:** React, on the same service, with the same sample day built in.
- **Testing without a headset:** an automated run drives the web room in Meta's Immersive Web Emulator (an
  emulated Quest 3) with emulated hands and controllers: it enters VR, pokes the dock, points at the cup and pinches,
  drops hand tracking, plays the story with a controller and leaves. Thirteen checks; it found five bugs, one of which
  would have locked both hands on a real Quest. For the native app, an acceptance run in Meta XR Simulator presses
  the dock, picks up the cup, types a reply, restarts the app and the server, and drops hand tracking in the middle
  of a press. A check passes only on what the app itself reports.
- **Assets:** in the web room, original characters sculpted by a script in the repository; in the native app,
  characters from Microsoft's Rocketbox library (MIT). Sample photos and footage from Wikimedia Commons (credited in
  the app); textures, the room's light and a few props from Poly Haven (CC0); the Figtree typeface (OFL) and Lucide
  icons (ISC). The room and its furniture are generated in code from those textures.
- **Tools:** I directed the work with a written product brief and visual references, and built the app with Claude
  Code as the coding agent, merging each round after its tests and simulator checks passed.

## Challenges

- **No headset.** Everything was checked in Meta's Immersive Web Emulator and Meta XR Simulator. Nothing has run on
  a device yet, and frame rate on a Quest is unmeasured. The emulators still found real faults: an invisible
  one-metre touch volume that held both hands in "select" so nothing else could be reached, table objects that no
  ray could select from the seat, and in the native app a button that fired when a hand lost tracking in the middle
  of a press.
- **Honest presence.** A room like this could easily fake a person. Every state is labelled instead: not here now,
  recorded message, simulated (in the sample), live.
- **Privacy that doesn't depend on the interface.** A moment shared with one person must never surface in a family
  circle's story. The server enforces that, and tests cover it.

## What I'm proud of

- **The first minute.** The first thing you do in the room is pick up a cup and hear a voice note play from it.
- **A reason to come back.** What you leave on the table is there next time, and so is what was left for you.
- **Hands that forgive.** Losing hand tracking in the middle of a reach or a press triggers nothing.
- **One link.** The room opens from a web address in Quest Browser, with no install and no account.

## What's next

- Run it on a Quest 3 and 3S, measure it, and tune it to a steady 72 Hz.
- Host the service, so the hosted room and the native app can sign in: live two-person sessions, and sharing from
  the phone.
- Mixed reality: the shared table placed on your own table.
- Voice in live sessions, and English and Arabic.
- **Target launch: March 2027, as an early-access release on Meta's store.**

---

## Testing instructions

For the form's testing field. The full version is [JUDGE_GUIDE.md](JUDGE_GUIDE.md).

**If the project link is the hosted WebXR room** (https://moamryehia.github.io/SameSky/room.html):

> Meta Quest 3 or 3S, Quest Browser, seated, hands only. About three minutes. No account is needed.
>
> 1. Open https://moamryehia.github.io/SameSky/room.html and press **Enter in VR**.
> 2. A cup on the table is glowing: reach out and pinch-grab it, or point at it and pinch. Maya's voice note plays
>    from the cup, with captions.
> 3. Pinch **Reply** on the dock and choose a phrase. A folded note appears beside the cup, and is still there the
>    next time you open the room.
> 4. Pinch a photo frame or the postcard to open it on the shared screen; pinch **Our day** for the recap; pinch a
>    keepsake on the shelf.
> 5. On the dock: **Captions**, **Sound**, **Recenter**, **Reset**, **Exit**.
>
> On a desktop or phone the same page works with clicks. Everything in this build is the sample day: fictional
> people, stock photos and footage and a synthesized voice, all labelled in the room.

**If the project link is the native app's release channel:**

> Meta Quest 3 or 3S, seated, hands only. About three minutes. No account or network is needed.
>
> 1. Start SameSky and press **Try a sample day** with a fingertip.
> 2. A cup on the table holds a voice note: close your hand around it, or point at it and pinch. The note plays
>    from the cup, with captions.
> 3. Press **Reply**, type a few words on the keyboard at the table edge, press **Done**. Your reply stands on the
>    table as a folded card, and is still there the next time you open the app.
> 4. On the dock in front of you: **Our day** (press **Play our day**), **Together**, **Keepsakes**, **Market**.
> 5. **Recenter** is the round button at the end of the dock. **Pause**, then **Leave SameSky**, to exit.
>
> If the headset asks for microphone access at first start, either answer works. Everything in this build is the
> sample day: fictional people, stock photos and footage and a synthesized voice, all labelled in the app.

## The video on YouTube

Upload `demo-build/SameSky_I_Saved_You_A_Seat_Demo.mp4` (2:11, 1080p; a 22 MB copy is
`..._Demo_share_1080p.mp4`). The rules want it **public** on YouTube or Vimeo. *Made for kids*: no. The English
subtitles are burned in; [demo/SameSky_I_Saved_You_A_Seat_Demo.srt](demo/SameSky_I_Saved_You_A_Seat_Demo.srt) can be
added as a caption track too. The credits below are required: two of the stock items are CC BY.

**Title**

> SameSky: I Saved You a Seat (Meta VR Start Developer Competition 2026)

**Description**

> SameSky is a shared room on Meta Quest for people who live apart. A cup on the table holds a voice note from
> someone far away; pick it up with your hand to hear it, and leave a reply that waits on the table for them.
>
> This is real footage of the native Quest app running in Meta XR Simulator with simulated hand tracking, in its
> offline sample day, plus a short insert of the phone companion. Adam and Maya are fictional, Maya's voice note and
> the narration are synthesized voices, and her presence in the chair is simulated. Waits between key presses while
> typing are cut.
>
> Code: https://github.com/MoAmrYehia/SameSky
>
> Sample photos and footage (Wikimedia Commons):
> "Autumn path, Dumitru Rascanu park" by Iurie Nistor, CC0 1.0.
> "Deira Dubai Creek" by kallerna, CC0 1.0.
> "Sharah Desert at sunset, UAE" by Lxs, CC BY 4.0.
> "Tower block and courtyard in Dunedin Chinese Garden at night" by Pseudopanax, public domain.
> "Dubai Part I - Aug 10, 2012" by 54t4n0d, CC BY 3.0 (an 8.5-second excerpt, cropped to 16:9).
> Characters: Microsoft Rocketbox (MIT). Room textures and props: Poly Haven (CC0).

## Putting the build in the release channel

Only if you submit the native app instead of the hosted WebXR room.

The command-line route, after `scripts/native/release.ps1 -SampleOnly` has made the APK
(`native/quest/Builds/Android/SameSky-release-sampleonly-<date>.apk`):

1. [Meta Horizon Developer Dashboard](https://developers.meta.com/horizon/manage/): **My Apps**, create the app
   (name SameSky, Meta Quest) if it isn't there.
2. **Distribution → Release Channels → Create Release Channel**, named `Competition`.
3. **Development → API**: copy the **App ID** and the **App Secret**. Treat the secret like a password.
4. Download the Platform Utility for Windows (`ovr-platform-util.exe`, linked from Meta's *Platform Command Line
   Utility* page) and run, in your own terminal:

   ```
   ovr-platform-util upload-quest-build --app-id <App ID> --app-secret <App Secret> --apk <path to the APK> --channel Competition --age-group TEENS_AND_ADULTS
   ```

   (Meta Quest Developer Hub's *Upload build to release channel* does the same without the command line.)
5. Back in **Release Channels**, open `Competition` → **Channel Settings** → **Channel Invite** → **Grant access to
   users by URL**, automatic approval, **Save Changes**. That URL goes in the Devpost form.

## Built with

`unity` `c#` `meta-xr-sdk` `meta-interaction-sdk` `openxr` `hand-tracking` `python` `fastapi` `sqlalchemy` `react`
`typescript` `webxr` `immersive-web-sdk` `three.js` `immersive-web-emulator` `meta-xr-simulator` `github-pages`
