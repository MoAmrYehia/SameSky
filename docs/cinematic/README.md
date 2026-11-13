# "Wish You Were Here": an animated short

Seventy-five seconds. Adam and Maya live far apart. On the same evening each leaves the rhythm of their own city and
sets off toward someone they miss: he through a harbour city in the blue hour, she along a coast at sunset. Their
journeys are cut against each other until a stairway in his city ends in a gate full of evening light, and they meet
on a rooftop terrace above the water, under a sky that is her sunset on one side and his city's night on the other.
It ends on the title **SAMESKY** and the line "Far apart. Right here."

**This is a film, not a demonstration.** It shows no app, screen, headset or interface, and none of it is taken from
the SameSky application, its rooms or the earlier videos. The only thing it shares with the product is the word in
the title. (The demo of the app itself is a separate film: [docs/demo](../demo/README.md).)

| | |
| --- | --- |
| Film | `demo-build/SameSky_Wish_You_Were_Here_Cinematic.mp4` (built by `scripts/demo/wish.ps1`; not in git, about 63 MB) |
| Length and format | 1:15.0, 1920×1080, 24 fps, H.264 + AAC stereo; −15.0 LUFS, true peak −1 dBFS. Nothing is burned into the picture except the title and the tagline |
| Dialogue as subtitles | [SameSky_Wish_You_Were_Here_Cinematic.srt](SameSky_Wish_You_Were_Here_Cinematic.srt) |
| Dialogue and shot list | [SCRIPT.md](SCRIPT.md) |
| Music, sound, voice and asset credits | [CREDITS.md](CREDITS.md), with file hashes in [asset-provenance.json](asset-provenance.json) |
| The cut | [`scripts/demo/wish_cut.json`](../../scripts/demo/wish_cut.json): every shot, its length, the lines and where they fall |

## How it was made

A reusable 3D pipeline, not generated video: every frame is drawn by Unity from scenes built for this film, so
characters, light and geography stay the same from shot to shot, and any shot can be changed and rendered again.

- **Animation and rendering: Unity 6 (URP), in Edit mode**, with the film toolkit in
  `native/quest/Assets/SameSky/Editor/Film` (editor-only: none of it can reach a build of the app).
  - `FilmWish.cs` and `FilmWish.Hook.cs`, `.Montage.cs`, `.Journey.cs`, `.Reunion.cs`: the 33 shots, each one a set,
    the people on it, and every key of what moves (paths, eyes, faces, arms, the camera).
  - `FilmWishSets.cs`, `FilmWishSets2.cs`, `FilmWishKit.cs`, `FilmWater.shader`: the three places and what they are made
    of. All of it is built in code from simple shapes and textures drawn by the program: the skies, the water that
    mirrors the scene, buildings with lit windows, shops, lamps, cars, a boat, stairs, the stall of cloths, vines, linen.
  - `FilmCast.cs`, `FilmActor.cs`: the people. The figures and their motion clips come from the Microsoft Rocketbox
    library (MIT). A figure plays a clip (a walk, a turn, a laugh, sitting) while the shot moves it along its path;
    on top of the clip the rig adds where the eyes and head go, arms that reach and hold (inverse kinematics), face
    shapes, lip-sync from the voice synthesizer's mouth-shape timings, and wind in the hair.
  - `FilmRenderer.cs` draws every frame at 3840×2160 with a film-grade copy of the render pipeline (HDR, soft
    shadows, ambient occlusion, depth of field, bloom, a grade, many lights) and pipes it to FFmpeg at 1920×1080.
- **Voices**: Windows' built-in text-to-speech (`scripts/demo/trailer_voices.ps1`, `wish_lines.json`).
- **Music and sound**: `scripts/demo/wish_audio.py` writes and synthesizes the score (96 beats a minute; every cut in
  the film is on a beat) and every sound in the film.
- **Edit**: `scripts/demo/wish_edit.py` puts the shots in the order of `wish_cut.json`, adds the title and the mix.

## Rebuilding it

```powershell
powershell -File scripts/demo/wish.ps1
```

About five minutes on a laptop with a recent GPU once the downloads are there (Unity draws all 33 shots in a little over two). Needs the project's Unity version,
Python with `numpy`, `scipy` and `Pillow`, and FFmpeg (the API's Python environment brings one). Close the Unity Editor
first. The first run downloads the motion clips and the four background characters (203 MB, listed in
`scripts/demo/wish_assets.json`) into `.asset-cache`; later runs use what is there.

To change the film, edit a shot in `FilmWish.*.cs` or a time in `wish_cut.json` and run it again
(`-Skip fetch,look,voices -Shots w19` re-renders one shot and recuts). While working on shots, a render console
saves starting Unity each time: `Unity -batchmode -projectPath native/quest -filmConsole` answers requests left in
`demo-build/film/console/request.json` (see the top of `FilmRenderer.cs`).

## Where it falls short of the brief, plainly

- **The look is a real-time engine cinematic, not feature animation.** The people are realistic stock figures, not
  designed, stylized characters; the places are built from simple shapes and read as stylized. The two do not fully
  belong to one design.
- **Adam and Maya are close to their descriptions, not exact.** The figures cannot be re-modelled, only recoloured:
  Adam has short straight dark hair rather than curls, and wears a navy jacket over a shirt rather than a knit top;
  Maya's hair is chin-length rather than shoulder-length. Both are the same people in every shot.
- **Some motion is approximate.** There are no clips for climbing stairs or for an embrace, so stairs are a walk on a
  rising path (framed from the waist up or from a distance) and the embrace is posed with arm targets; hands that
  hold a rail, a hand or a chair are placed, not captured. Cloth is only the hung cloths; clothes do not move, and
  the wind in Maya's hair is slight.
- **Background people have no faces that move** (those figures come without facial rigs) and are kept soft or far.
- **The voices are text-to-speech** and sound like it, so the comic timing of "Traffic." rests on the cut and the
  pause more than on the delivery.
- **The music is synthesized and simple.** It has a pulse, a bass and a four-bar tune and follows the film's
  sections, but it is not a produced track.
- **I could not listen to the result.** The mix was balanced by measurement: every line sits at least 10 dB clear
  of what plays under it.
