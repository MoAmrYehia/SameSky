# "I Saved You a Seat": the demo video

Two minutes and eleven seconds of the app itself. Adam sits down in the shared lounge and finds Maya's chair empty and
a cup waiting. He picks the cup up with his hand and her recorded message plays. He steps into the day she shared,
sends a heart, types an answer that stays on the table, shares a clip of his own from his phone, and comes back to a
room that kept all of it.

**Everything on screen is the running product.** The lounge footage is the native Quest app (Unity, `native/quest`)
in Meta XR Simulator; the phone insert is the companion web app. Nothing in the app's picture is generated, staged in
another tool or retouched. The animated film with the same title is a different thing: see
[docs/trailer](../trailer/README.md).

| | |
| --- | --- |
| Film | `demo-build/SameSky_I_Saved_You_A_Seat_Demo.mp4` (built by `scripts/demo/demo.ps1`; not in git, about 59 MB) |
| Length and format | 2:10.9, 1920×1080, 30 fps, H.264 + AAC stereo, English subtitles burned in; −14.8 LUFS, true peak −1 dBFS |
| Subtitles | [SameSky_I_Saved_You_A_Seat_Demo.srt](SameSky_I_Saved_You_A_Seat_Demo.srt) |
| Narration, recorded dialogue, on-screen text | [SCRIPT.md](SCRIPT.md) |
| Shot list: each shot, its source take, what it shows, sample or live | [SHOT_LIST.md](SHOT_LIST.md) |
| The cut | [`scripts/demo/demo_cut.json`](../../scripts/demo/demo_cut.json): every clip with its source take and the seconds used |

## What you are looking at

- **Capture environment: Meta XR Simulator, not a headset.** The app runs in Unity Play mode with the simulator as
  its OpenXR runtime, the same way the acceptance run does. It was not recorded on a Quest.
- **Hands.** No controllers. The hands are the simulator's hand-tracking input, driven by a script
  (`scripts/demo/film.ps1`, on top of `scripts/native/acceptance.ps1`), so every press, grab and key is the app
  responding to tracked-hand input, at the speed the simulator moves a hand. The film says so on screen the first time
  a hand appears.
- **The camera.** A capture-only camera in the Editor (`Assets/SameSky/Scripts/App/FilmRig.cs`) draws the same scene
  in the same state with the same renderer as the headset view. In the shots with hands it sits where the headset is
  and turns with it (eased, so the simulated head's steps don't jolt the picture, and tipped down toward the hands, as
  a person looks at theirs). In the steady shots it is placed near the seat and moves slowly. It adds no lighting,
  grading or effects, and none of it is compiled into a build.
- **Sample content, offline.** The app is in its sample day: Adam and Maya are fictional, the photos and the clip are
  stock, the recap is precomputed, and Maya's voice note is a synthesized voice. Maya appearing in her chair after the
  note is the sample's **simulated presence, not a live connection**, and the film labels it at that moment and again
  at the end. The app's own labels say the same things and are left readable ("recorded message, not live",
  "simulated presence (fictional sample)", "Precomputed sample recap (no AI call)", "Stock photo (sample)").
- **The companion insert.** The web companion at phone size (430×932) in sample mode, recorded by
  `web/scripts/demo-companion.mjs`: real page, real taps (a ring shows where each lands), no account, nothing uploaded.
  It is framed as a phone by the edit and plays 1.3 times faster than it was recorded.

What the edit does to the footage, and nothing more: it trims; it cuts the waits out of the typing take (the
simulator needs about 25 seconds to move the hand to each key, so the film shows every key press at its real speed
and drops the time between them, and says so); it enlarges part of the frame in five shots so a panel can be read;
it holds the last frame, softened, under the end card.

The film's own additions are kept visually apart from the app's panels: subtitles in dark boxes, captions in plain
white type, notes in a small box top left, the standing line "Meta XR Simulator · native Quest app · offline sample"
on every lounge shot, and the end card.

## Sound

- **The app's sound** is taken from the takes themselves (each clip is recorded with what the app played): the press
  clicks and Maya's voice note. The voice note is raised 10.6 dB to a speaking level and its first sentence is also
  laid over the opening shot, where the subtitle marks it as a recorded message.
- **Narration**: seven lines (`scripts/demo/demo_lines.json`), Windows' built-in text-to-speech voice. Synthesized,
  and said so on the end card.
- **Score**: the trailer's soft piano and pad, every note synthesized in `scripts/demo/trailer_audio.py`, arranged to
  this film in `scripts/demo/demo_audio.py`. It stands back under every voice (12 to 17 dB clear, measured).
- **Room tone**: a quiet synthesized bed. The app itself is silent between its sounds, so this is the edit's.

## Rebuilding it

```powershell
powershell -File scripts/demo/demo.ps1
```

About two minutes. Needs Python with `numpy`, `scipy` and `Pillow`, and FFmpeg (the API's Python environment brings
one). It renders the narration, mixes the sound and cuts the film from the takes in `demo-build/capture`. To change
the film, edit `scripts/demo/demo_cut.json` and run it again (`-Skip voices -Only cup,payoff` recuts two clips).

The takes are not in git (4 GB) and `demo.ps1` does not film them. To film them again:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/demo/film.ps1 -Serve
```

starts the app in the simulator and runs the commands dropped into `demo-build/console` (opening the sample, pressing
a target, starting and stopping a take); the top of `film.ps1` lists them. Leave the keyboard and mouse alone while it
runs, and close the Unity Editor first. The companion is recorded with the web dev server running:

```powershell
cd web; node scripts/demo-companion.mjs
```

## What the film leaves out

- **A live session.** Two people present at once (two accounts, the realtime service) was not filmed. The film ends on
  the asynchronous experience and labels Maya's presence as simulated.
- **An AI-drafted recap.** The sample's story is precomputed and the app labels it that way; drafting a recap with the
  API and approving it is not shown.
- **A headset.** No footage from a Quest. The APK builds (see [NATIVE_STATUS.md](../NATIVE_STATUS.md)); a run on a
  device is not recorded here.
- **A real upload.** The companion is in sample mode, with the stock clip. Signing in and uploading a file of your
  own (phone, camera, drone or AI-glasses export) is not shown; the insert says how such footage comes in.
- A voice reply (the reply is typed), the avatar made from a photo, settings, the family circle, and the
  Marketplace's single pieces (only two whole looks are tried).

## Found while filming

Two faults in the story screen showed up in the first takes of the "Our day" beat. Both are fixed in the app, and that
beat was filmed again afterwards, so the film shows the app as it is now:

- A voice note inside a story did not play: the screen said "This voice note couldn't play." The screen was being
  cleared with a null picture, which Unity refuses (`Ui.SetImage`). A voice note now plays over its chapter's picture,
  with its captions.
- The story panel's text ran into its progress bar and buttons, because room was kept for one line only
  (`SameSkyApp.Story.cs`). It now keeps room for three and ends a longer line with an ellipsis.

## Limits

- The voices are text-to-speech and sound like it.
- The sound mix was balanced by measurement, not by ear.
- The simulator's hand moves slowly and deliberately; a person's hand in a headset is quicker and less exact.
- One small thing in the app that the film does not hide: the voice note card cuts its second line short
  ("Synthesized v...").
