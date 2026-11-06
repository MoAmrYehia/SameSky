# Media import pipeline

The file's **origin** (phone, camera, action camera, drone, AI-glasses export…) is a label the person chooses;
it never implies SameSky controls that device. Everything enters through upload or browser capture.

| Source | Path in this release |
|---|---|
| Phone / tablet | File upload; browser camera/microphone capture with permission |
| Laptop / webcam | File upload; browser camera/microphone capture with permission |
| Camera, action camera, drone | Export a supported file, then upload from phone or desktop |
| AI glasses | Upload an exported file (no native integration) |
| 360° / stereo | Rejected for now with a clear message (P1) |

## Accepted inputs (validated by magic bytes and FFmpeg, never by filename)

| Kind | Containers | Codecs | Limits |
|---|---|---|---|
| Photo | JPEG, PNG | — | 10 MB |
| Clip | MP4/MOV (ISO-BMFF), WebM/Matroska | H.264, HEVC, VP8, VP9, AV1, MPEG-4 Part 2 (+ AAC/Opus/Vorbis/MP3/PCM audio) | 100 MB, 60 s source |
| Voice note | WebM/Ogg (Opus), MP4/M4A (AAC), WAV, MP3 | listed audio codecs | 5 MB, 30 s |

Anything else fails with an actionable message (e.g. “The mjpeg codec isn't supported. Export as H.264 MP4.”).
Media that is not `ready` cannot be shared.

## Outputs (the tested playback profile)

- Photos → JPEG, EXIF orientation applied, **all metadata (including GPS) dropped**, ≤ 2048 px, 512 px thumb.
- Clips → MP4 / H.264 High / yuv420p / AAC 128 k / `+faststart`, ≤ 1280 px long edge, ≤ 30 fps, rotation baked
  in, `-map_metadata -1`, trimmed highlight ≤ 15 s and ≤ 25 MB, poster thumbnail.
- Voice → M4A / AAC 96 k mono, ≤ 30 s.

Capture time: taken from container metadata when present, otherwise the user's chosen time; the companion
always lets the person correct it. Rotation can be corrected (`POST /v1/media/{id}/corrections`), which
re-processes from the original. The browser also downscales photos before upload where practical.

Processing runs in the worker with time limits (`subprocess` timeouts) and safe error codes. FFmpeg comes from
`imageio-ffmpeg` unless a system `ffmpeg` is on `PATH`.

Quest playback of the output profile still needs verification on hardware (see STATUS.md).
