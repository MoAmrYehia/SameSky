# "Wish You Were Here": credits

## Music

**Original score, made for this film.** Written as notes in [`scripts/demo/wish_audio.py`](../../scripts/demo/wish_audio.py)
(D major, 96 beats a minute, a four-bar tune over D, A, B minor and G) and synthesized by that script: plucked strings,
a soft kick, a pad, a bell-like lead, shaker and claps from shaped noise, and the felt piano of
`scripts/demo/trailer_audio.py`. No recording, sample, loop or library track is used, and no licence is needed.

## Sound

**All synthesized by the same script**: the air of the harbour city, the coast, the two stairways and the terrace;
footsteps; the guitar and clapping of the players on the walk; a hand along a rail and along a wall; cloth in the
wind; the hat; the chairs on stone. No recorded sound effects.

## Voices

**Synthesized.** The five lines are spoken by the text-to-speech voices that ship with Windows
("Microsoft Zira Desktop" for Maya, "Microsoft David Desktop" for Adam), rendered by
`scripts/demo/trailer_voices.ps1` from [`scripts/demo/wish_lines.json`](../../scripts/demo/wish_lines.json). No
person's voice is recorded or imitated.

## Characters and motion

| What | Source | Licence |
| --- | --- | --- |
| Adam and Maya: the figures `Male_Adult_07` and `Female_Adult_14`, with their facial rigs | [Microsoft Rocketbox avatar library](https://github.com/microsoft/Microsoft-Rocketbox) | MIT, © 2020 Microsoft Corporation |
| Background people: `Female_Adult_02`, `Female_Adult_09`, `Male_Adult_03`, `Male_Adult_12` (models and colour textures) | the same | MIT |
| Motion clips, 40: walks, turns, sitting, standing idles, laughs, a wave, clapping, two dances | the same, `Assets/Animations` | MIT |

The licence text is in `native/quest/Assets/SameSky/Art/Characters/Rocketbox/LICENSE.txt`. The exact files, with their
sizes and SHA-256 hashes, are listed in [`asset-provenance.json`](asset-provenance.json). The four background
characters and the motion clips are not kept in this repository: `scripts/demo/wish_fetch.py` downloads them
(203 MB) and `scripts/demo/wish_cast.py` places them in an editor-only folder that never reaches a build of the app.

Adam's and Maya's clothes, skin, hair and eyes are recoloured copies of those two figures' own textures
(`scripts/demo/trailer_look.py`). They are fictional adults: nobody was scanned, photographed or recorded for them.

What the film adds to the figures is its own: the walks along paths and up stairs, where the eyes go and the head
follows, the arms that reach, hold and wave (inverse kinematics), the faces, the lip-sync from the voice
synthesizer's mouth-shape timings, the wind in Maya's hair (`native/quest/Assets/SameSky/Editor/Film/FilmActor.cs`).

## Places and things

**Built for this film in code**, from simple shapes and textures drawn by the program: the harbour city (buildings,
their lit windows, shops, lamps, cars, the boat, the stairway and its gate), the coast (the walk, the sea wall, the
houses, the pines, the headland and its steps, the stall of cloths, the bench, the hat, the guitar), the rooftop
terrace (walls, pergola, vines, linen, lanterns, chairs), the skies and the water
(`FilmWishKit.cs`, `FilmWishSets.cs`, `FilmWishSets2.cs`, `FilmWater.shader`). No outside models, photographs or
textures.

## Type

The title and the tagline are set in **Figtree** (Erik Kennedy), SIL Open Font Licence 1.1; the font files and the
licence are in `native/quest/Assets/SameSky/Art/Fonts/Figtree`.

## Tools

Unity 6 (Universal Render Pipeline) for animation and rendering, FFmpeg for encoding and the cut, Python with NumPy
and SciPy for the sound, Pillow for texture conversion.
