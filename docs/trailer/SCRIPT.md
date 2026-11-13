# "I Saved You a Seat": final dialogue and narration

Every word spoken in the trailer, in order, with the time it starts (minutes:seconds from the start of the film). The
same lines are in [the subtitle file](SameSky_I_Saved_You_A_Seat_Animated_Trailer.srt) and are burned into the video.

| Time | Who | Line |
| --- | --- | --- |
| 0:03.6 | Maya (voice over) | I saved you a seat. |
| 0:08.0 | Narrator | When you live far apart, it's the little things you miss. |
| 0:21.4 | Maya | You would've loved this. |
| 0:28.5 | Narrator | With SameSky, leave a little of your day for someone you love. |
| 0:51.4 | Maya (her recorded message, played in the lounge) | I saw this and thought of you. Sit with me for a minute? |
| 0:56.9 | Narrator | A familiar object. A voice waiting just for you. |
| 1:13.0 | Adam | I wish I'd been there. Tell me everything. |
| 1:28.4 | Maya | You made it. |
| 1:33.4 | Adam | You saved me a seat. |
| 1:37.6 | Narrator | And when your days finally meet, share the same space. |
| 1:49.0 | Maya | Same time tomorrow? |
| 1:51.9 | Adam | Same seat. |

On-screen text:

| Time | Text |
| --- | --- |
| 1:02.6 | Share moments. |
| 1:07.2 | Leave a voice. |
| 1:18.4 | Reply when you can. |
| 1:55.7 | SameSky · A place to be close, even when you're far. · Save someone a seat. · An animated concept trailer. Voices are synthesized. |

## The voices

The three voices are the text-to-speech voices that ship with Windows: Maya is *Microsoft Zira*, Adam is *Microsoft
David*, the narrator is *Microsoft Mark*. Nobody was recorded and nobody's voice is imitated. They are the weakest part
of the film: they are clear, but they sound synthesized. The lines live in `scripts/demo/trailer_lines.json`; to use
recorded voices instead, replace the files in `demo-build/film/voice/` (one WAV per line, same names) and run the audio
and edit steps again. Lip movement is keyed from the synthesizer's mouth-shape timings, so a recorded line needs its
`.json` timing file redone or its shot re-rendered without lip-sync data.
