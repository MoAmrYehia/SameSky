# "I Saved You a Seat" (demo): script

Everything that is said or written in the demo video, with the film's times. Subtitles as a file:
[SameSky_I_Saved_You_A_Seat_Demo.srt](SameSky_I_Saved_You_A_Seat_Demo.srt).

## Narration

One narrator, a synthesized voice (Windows text-to-speech, "Microsoft Mark"; `scripts/demo/demo_lines.json`,
rendered by `scripts/demo/trailer_voices.ps1`). It never speaks for a person in the story and never describes
anything as live.

| Time | Line | Over |
| --- | --- | --- |
| 0:07.8 | Different cities. Different routines. Some days, we miss each other by hours. | Her empty chair's label, then the Together panel with both local times |
| 0:20.2 | In SameSky, a familiar object can hold a message for later. | The cup, before the hand reaches for it |
| 0:42.2 | A few moments become a day you can step into. | Our day, as Play is pressed |
| 1:13.0 | Leave an answer, and it waits on the table. | Done is pressed; the reply card appears |
| 1:21.4 | Share the little things, from your phone or your camera's footage, with the people you choose. | The companion's Share a moment |
| 1:36.5 | A place for the moments between visits. | Keepsakes and the shelf |
| 2:00.8 | SameSky. A place to be close, even when you're far. | The end card |

## Recorded dialogue

One voice besides the narrator: **Maya's voice note**, the sample message the app ships with (a synthesized voice,
and the app says so on the card). It is played by the app, in the take, when the cup is picked up:

| Time | Line |
| --- | --- |
| 0:25.5 | Saved you a seat. |
| 0:27.6 | The bench by the big oak, same as always. |
| 0:31.5 | Tell me about your sunset when you get home. |

Its first sentence is also heard once at 0:02.3, over the opening shot of her empty chair, taken from the same
recording. Both times the subtitle carries the tag **MAYA · RECORDED MESSAGE**. Nobody speaks live in the film.

## The film's text

Added by the edit, set in Figtree, and kept apart from the app's own panels and labels.

| Time | Text | Kind |
| --- | --- | --- |
| every lounge shot | Meta XR Simulator · native Quest app · offline sample | standing line, top left |
| 0:04.3 | What if someone could leave you a moment? | the opening question |
| 0:11.2 | Hand tracking here is Meta XR Simulator's, driven by a script. No controllers are used. | note |
| 0:15.6 | Meet when you can. | caption |
| 0:35.1 | Offline sample: Maya's arrival is simulated. It is not a live connection. | note |
| 0:41.9 | A sample story, put together in advance: no AI call is made here. | note |
| 0:59.3 | Leave a moment. | caption |
| 1:05.7 | Each key is pressed by the simulated hand. The waits between keys are cut. | note |
| 1:21.0 | Companion web app, sample mode. The clip is stock footage, and the page labels it. | note |
| 1:21.1 | 1 Choose a photo or clip · 2 Preview it, trim it · 3 Pick who can see it · 4 Confirm | steps, one by one |
| 1:21.8 | Footage from a camera, a drone or AI glasses comes in as an exported file (a photo, or a clip of up to 60 seconds). There is no direct link to a device. | note |
| 1:28.4 | Share your day. | caption |
| 1:42.5 | Make the room yours. | caption |
| 1:53.1 | Offline sample: Maya's presence is simulated. It is not a live session. | note |
| 2:00.8 | SameSky | title |
| 2:05.7 | Save someone a seat. | end line |
| 2:01.5 | Filmed in Meta XR Simulator from the native Quest app. Offline sample: fictional people, stock media, synthesized voices, scripted hand-tracking input. | capture line |

## Sound

No outside audio. The score and the room tone are synthesized by `scripts/demo/demo_audio.py` (the piano and pad of
`scripts/demo/trailer_audio.py`); the press clicks and the voice note are the app's own sound from the takes.
