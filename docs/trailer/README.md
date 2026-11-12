# "I Saved You a Seat": the animated concept trailer

A two-minute animated short about what SameSky is for: Maya sees a sunset, turns to share it with someone who is not
there, and leaves it for him instead: a clip, a photo and a voice message that wait as a cup on the table in their
shared lounge. Adam finds the cup, hears her, answers. When their days finally overlap, the empty chair is filled.

**This is a concept trailer, not a demonstration of the app.** Nothing in it is captured from the running app, a
headset or the simulator. It is 3D animation made for the film. The competition's required demo video is a separate
thing and has to be real footage (see "The official demo" below).

| | |
| --- | --- |
| Film | `demo-build/SameSky_I_Saved_You_A_Seat_Animated_Trailer.mp4` (built by `scripts/demo/trailer.ps1`; not in git, about 70 MB) |
| Length and format | 2:00, 1920×1080, 24 fps, H.264 + AAC stereo, English subtitles burned in |
| Subtitles | [SameSky_I_Saved_You_A_Seat_Animated_Trailer.srt](SameSky_I_Saved_You_A_Seat_Animated_Trailer.srt) |
| Dialogue and narration | [SCRIPT.md](SCRIPT.md) |
| Shot list, and what each shot corresponds to in the app | [SHOT_LIST.md](SHOT_LIST.md) |

## How it was made

Everything is generated from this repository; there is no outside footage, music or artwork in it.

- **Animation and rendering: Unity**, the same project as the native app, in Edit mode. A small film toolkit lives in
  `native/quest/Assets/SameSky/Editor/Film` (editor-only, so none of it ships in the app):
  - `FilmTracks.cs` keyframes with easing; `FilmActor.cs` the character rig: a seated breathing loop underneath, and
    on top of it two-bone arm IK, fingers that close, fingertip touches, an eye-led head turn with human limits, eye
    darts and blinks, face shapes, and lip-sync from the voice synthesizer's mouth-shape timings;
  - `FilmStage.cs` the stage and the film camera (depth of field, bloom, a grade); `FilmSets.cs` and `FilmProps.cs`
    the sets and props; `FilmShots*.cs` every shot, key by key; `FilmRenderer.cs` draws each frame with a film-grade
    copy of the app's render pipeline (HDR, soft shadows, ambient occlusion), at 3840×2160, and pipes it to FFmpeg.
- **Sets.** The lounge is the app's own room, copied out of its scene, with a chair added at the person's place and
  film lighting. Adam's flat, Maya's desk and the waterfront were built for the film from soft-edged boxes, lathed
  shapes and textures generated in code (the sunset sky, the water, the far shore).
- **Characters.** Adam and Maya are the app's two seated figures: Microsoft Rocketbox avatars (MIT licence), which the
  app already uses for its fictional sample people. `scripts/demo/trailer_look.py` recolours copies of their textures
  for the film (her terracotta cardigan and cream top, his navy jacket and stone trousers, skin, hair, eyes). They
  are fictional adults; nobody was scanned, photographed or recorded.
- **What the screens show.** Maya's sunset clip and her two photos are themselves rendered on the film's waterfront
  set (`FilmShots.Media.cs`), so the phone and the lounge screen play the film's own pictures.
- **Voices.** Windows' built-in text-to-speech voices (`scripts/demo/trailer_voices.ps1`). Synthesized, and labelled
  so on the end card.
- **Music and sound.** `scripts/demo/trailer_audio.py` synthesizes an original piano-and-pad score from notes written
  in the script, and all the foley and room tones.
- **Edit.** `scripts/demo/trailer_edit.py` and `scripts/demo/trailer_cut.json` (shot order, line timings, captions,
  sound cues). Text is set in Figtree (SIL Open Font Licence), the app's typeface.

## Rebuilding it

```powershell
powershell -File scripts/demo/trailer.ps1
```

About six minutes on a laptop with a recent GPU. Needs the project's Unity version, Python with `numpy`, `scipy` and
`Pillow`, and FFmpeg (the API's Python environment brings one). Close the Unity Editor first. To change the film, edit
a shot in `FilmShots*.cs` or a time in `trailer_cut.json` and run it again (`-Shots s12 -Skip look,voices,media`
re-renders one shot and re-cuts).

While working on shots, a render console saves starting Unity each time:
`Unity -batchmode -projectPath native/quest -filmConsole` answers requests left in
`demo-build/film/console/request.json` (see the top of `FilmRenderer.cs`).

## What it is and is not

- It follows the brief's story beat for beat, in seven parts, with every scene animated: hands place, reach, grip and
  lift; heads follow eyes; faces change; the camera moves on a purpose.
- **It does not look like a studio animated feature.** The characters are realistic game-style figures, not designed
  cartoon characters; their faces are stiffer than hand animation would give; hair is simple; nobody walks (the rig has
  a seated pose only, so every scene is staged seated or from the waist up). It reads as a real-time engine cinematic.
- **The voices are synthetic** and sound it. Recording three people would improve the film more than anything else.
- The sound mix was balanced by measurement (every line sits 9 to 16 dB clear of the music under it), not by ear.
- Each feature the film shows is mapped to the app in [SHOT_LIST.md](SHOT_LIST.md), which also says where the film
  stages a feature differently from the app and where it shows a concept the app does not have.

## The official demo

The competition asks for a demo video under three minutes that shows the project itself on a Quest or in an XR
simulator. This trailer does not count as that, and should not be presented as gameplay. The demo is a separate
film made from real footage of the app in Meta XR Simulator: [docs/demo](../demo/README.md).
