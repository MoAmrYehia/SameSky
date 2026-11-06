"""Media import pipeline: signature validation, bounded FFmpeg inspection/transcoding, image derivatives.

Accepted input profiles (see docs/MEDIA.md):
  image : JPEG, PNG                         <= 10 MB      -> JPEG derivative (EXIF orientation applied, all
                                                               metadata incl. GPS stripped), 2048px max, 512px thumb
  video : MP4/MOV (ISO-BMFF) or WebM/Matroska <= 100 MB, <= 60 s; video codecs h264, hevc, vp8, vp9, av1
          -> MP4 / H.264 High / yuv420p / AAC 128k / faststart, <= 1280px long edge, <= 30 fps, trimmed to
             <= 15 s published highlight, rotation baked in, metadata stripped (-map_metadata -1)
  audio : WebM/Ogg (Opus), MP4/M4A (AAC), WAV, MP3 <= 5 MB, <= 30 s -> M4A / AAC 96k mono
The filename extension is never trusted: the container is sniffed from magic bytes and the codecs are
read by FFmpeg. Anything else is rejected with a useful message. Unprocessed media cannot be published.
"""

from __future__ import annotations

import re
import shutil
import subprocess
from dataclasses import dataclass
from datetime import UTC, datetime
from pathlib import Path

from sqlalchemy.orm import Session

from ..config import get_settings
from ..errors import invalid
from ..models import MediaAsset, User

SOURCE_DEVICES = ("phone", "tablet", "webcam", "laptop", "camera", "action_camera", "drone", "glasses", "other")
PROJECTIONS = ("flat", "equirect360", "stereo_sbs")
VIDEO_CODECS = {"h264", "hevc", "vp8", "vp9", "av1", "mpeg4"}
AUDIO_CODECS = {"aac", "opus", "vorbis", "mp3", "pcm_s16le", "pcm_s24le", "pcm_f32le", "flac"}


class MediaRejected(Exception):
    def __init__(self, code: str, message: str):
        super().__init__(code)
        self.code = code
        self.message = message


def sniff(head: bytes) -> tuple[str, str] | None:
    """Return (kind, container) from magic bytes, or None."""
    if head.startswith(b"\xff\xd8\xff"):
        return "image", "jpeg"
    if head.startswith(b"\x89PNG\r\n\x1a\n"):
        return "image", "png"
    if len(head) >= 12 and head[4:8] == b"ftyp":
        brand = head[8:12]
        if brand in (b"M4A ", b"M4B "):
            return "audio", "mp4"
        return "av", "isobmff"  # MP4/MOV: video or audio decided by streams
    if head.startswith(b"\x1a\x45\xdf\xa3"):
        return "av", "matroska"  # WebM/MKV
    if head.startswith(b"OggS"):
        return "audio", "ogg"
    if head.startswith(b"RIFF") and head[8:12] == b"WAVE":
        return "audio", "wav"
    if head.startswith(b"ID3") or head[:2] in (b"\xff\xfb", b"\xff\xf3", b"\xff\xf2"):
        return "audio", "mp3"
    return None


def ffmpeg_path() -> str | None:
    found = shutil.which("ffmpeg")
    if found:
        return found
    try:
        import imageio_ffmpeg

        return imageio_ffmpeg.get_ffmpeg_exe()
    except Exception:  # noqa: BLE001
        return None


@dataclass
class Probe:
    duration_ms: int | None
    video_codec: str | None
    audio_codec: str | None
    width: int | None
    height: int | None
    rotation: int
    fps: float | None
    creation_time: datetime | None


_DURATION = re.compile(r"Duration: (\d+):(\d+):(\d+(?:\.\d+)?)")
_VIDEO = re.compile(r"Stream #\S+.*?: Video: (\w+).*?, (\d{2,5})x(\d{2,5})")
_AUDIO = re.compile(r"Stream #\S+.*?: Audio: (\w+)")
_FPS = re.compile(r"(\d+(?:\.\d+)?) fps")
_ROTATE = re.compile(r"(?:rotate\s*:\s*(-?\d+))|(?:rotation of (-?\d+(?:\.\d+)?) degrees)")
_CREATION = re.compile(r"creation_time\s*:\s*(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})")


def probe(path: Path) -> Probe:
    exe = ffmpeg_path()
    if exe is None:
        raise MediaRejected("transcoder_unavailable", "Video processing isn't available on this server right now.")
    result = subprocess.run([exe, "-hide_banner", "-i", str(path)], capture_output=True, text=True, timeout=30)
    text = result.stderr
    duration = None
    if m := _DURATION.search(text):
        h, mi, s = m.groups()
        duration = int((int(h) * 3600 + int(mi) * 60 + float(s)) * 1000)
    video = _VIDEO.search(text)
    audio = _AUDIO.search(text)
    rotation = 0
    if m := _ROTATE.search(text):
        raw = m.group(1) or m.group(2)
        rotation = int(round(float(raw))) % 360
    fps = float(m.group(1)) if (m := _FPS.search(text)) else None
    created = None
    if m := _CREATION.search(text):
        try:
            created = datetime.fromisoformat(m.group(1)).replace(tzinfo=UTC)
        except ValueError:
            created = None
    return Probe(
        duration_ms=duration,
        video_codec=video.group(1) if video else None,
        audio_codec=audio.group(1) if audio else None,
        width=int(video.group(2)) if video else None,
        height=int(video.group(3)) if video else None,
        rotation=rotation,
        fps=fps,
        creation_time=created,
    )


def asset_dir(asset: MediaAsset) -> Path:
    path = get_settings().media_root / asset.owner_id / asset.id
    path.mkdir(parents=True, exist_ok=True)
    return path


def store_upload(
    db: Session,
    owner: User,
    data: bytes,
    *,
    expected_kind: str,
    source_device: str,
    projection: str = "flat",
    capture_at: datetime | None = None,
    trim_start_ms: int | None = None,
    trim_end_ms: int | None = None,
) -> MediaAsset:
    limits = get_settings().media
    if source_device not in SOURCE_DEVICES:
        raise invalid("invalid_source_device", "Choose where this came from (phone, camera, action camera...).")
    if projection not in PROJECTIONS:
        raise invalid("invalid_projection", "Projection must be flat, equirect360 or stereo_sbs.")
    if projection != "flat":
        raise invalid("projection_not_supported", "360° and stereo footage arrive in a later release. Import it as flat.")
    detected = sniff(data[:16])
    if detected is None:
        raise invalid("unsupported_format", "That file type isn't supported. Use JPEG/PNG photos, MP4/MOV/WebM clips or common audio.")
    kind, container = detected
    if expected_kind == "image":
        if kind != "image":
            raise invalid("kind_mismatch", "That file isn't a JPEG or PNG photo.")
        if len(data) > limits.image_bytes:
            raise invalid("too_large", "Photos can be up to 10 MB.")
    elif expected_kind == "video":
        if kind != "av":
            raise invalid("kind_mismatch", "That file isn't an MP4, MOV or WebM video.")
        if len(data) > limits.video_source_bytes:
            raise invalid("too_large", "Source videos can be up to 100 MB.")
    elif expected_kind == "audio":
        if kind not in ("audio", "av"):
            raise invalid("kind_mismatch", "That file isn't a supported voice recording.")
        if len(data) > limits.voice_bytes:
            raise invalid("too_large", "Voice notes can be up to 5 MB.")
    else:
        raise invalid("invalid_kind", "Media kind must be image, video or audio.")

    asset = MediaAsset(
        owner_id=owner.id,
        kind=expected_kind,
        size_bytes=len(data),
        source_device=source_device,
        projection=projection,
        capture_at=capture_at,
        capture_at_source="user" if capture_at else "unknown",
        trim_start_ms=trim_start_ms,
        trim_end_ms=trim_end_ms,
        input_profile={"container": container},
        processing_state="pending",
    )
    db.add(asset)
    db.flush()
    original = asset_dir(asset) / f"original.{container}"
    original.write_bytes(data)
    asset.original_path = str(original)
    db.commit()
    return asset


def process(db: Session, asset: MediaAsset) -> None:
    """Bounded processing. Sets processing_state to ready or failed with a safe, actionable error."""
    asset.processing_state = "processing"
    db.commit()
    try:
        if asset.kind == "image":
            _process_image(asset)
        elif asset.kind == "video":
            _process_video(asset)
        else:
            _process_audio(asset)
        asset.processing_state = "ready"
        asset.safe_error = None
    except MediaRejected as exc:
        asset.processing_state = "failed"
        asset.safe_error = f"{exc.code}: {exc.message}"[:200]
    except subprocess.TimeoutExpired:
        asset.processing_state = "failed"
        asset.safe_error = "transcode_timeout: Processing took too long. Try a shorter clip."
    except Exception:  # noqa: BLE001 - never leak internals
        asset.processing_state = "failed"
        asset.safe_error = "processing_failed: We couldn't process this file. Try exporting it again as MP4 or JPEG."
    db.commit()


def _process_image(asset: MediaAsset) -> None:
    from PIL import Image, ImageOps

    try:
        img = Image.open(asset.original_path)
        exif = img.getexif()
        img.load()
    except Exception:  # noqa: BLE001
        raise MediaRejected("unreadable_image", "This photo couldn't be read. Try exporting it again.") from None
    raw_dt = exif.get(36867) or exif.get(306)  # DateTimeOriginal / DateTime (no zone in EXIF)
    if raw_dt and asset.capture_at is None:
        asset.input_profile = {**asset.input_profile, "exifCaptureLocal": str(raw_dt)}
    orientation = exif.get(274, 1)
    asset.rotation = {3: 180, 6: 90, 8: 270}.get(orientation, 0)
    img = ImageOps.exif_transpose(img)
    if asset.rotation_override:
        img = img.rotate(-asset.rotation_override, expand=True)
    img = img.convert("RGB")
    img.thumbnail((2048, 2048))
    folder = asset_dir(asset)
    derivative = folder / "display.jpg"
    img.save(derivative, "JPEG", quality=86, optimize=True)  # fresh file: no EXIF/GPS carried over
    thumb = img.copy()
    thumb.thumbnail((512, 512))
    thumb_path = folder / "thumb.jpg"
    thumb.save(thumb_path, "JPEG", quality=80)
    asset.width, asset.height = img.size
    asset.derivative_path, asset.derivative_mime = str(derivative), "image/jpeg"
    asset.thumbnail_path = str(thumb_path)
    asset.input_profile = {**asset.input_profile, "format": "jpeg" if asset.input_profile.get("container") == "jpeg" else "png"}


def _run(args: list[str], timeout: int = 180) -> None:
    result = subprocess.run(args, capture_output=True, text=True, timeout=timeout)
    if result.returncode != 0:
        raise MediaRejected("transcode_failed", "We couldn't convert this file. Try exporting it as MP4 (H.264).")


def _process_video(asset: MediaAsset) -> None:
    limits = get_settings().media
    info = probe(Path(asset.original_path))
    asset.input_profile = {
        **asset.input_profile,
        "videoCodec": info.video_codec,
        "audioCodec": info.audio_codec,
        "width": info.width,
        "height": info.height,
        "rotation": info.rotation,
        "fps": info.fps,
        "durationMs": info.duration_ms,
    }
    if info.video_codec is None:
        raise MediaRejected("no_video_track", "This file has no video track.")
    if info.video_codec not in VIDEO_CODECS:
        raise MediaRejected("unsupported_codec", f"The {info.video_codec} codec isn't supported. Export as H.264 MP4.")
    if info.duration_ms is not None and info.duration_ms > limits.video_source_seconds * 1000 + 500:
        raise MediaRejected("too_long", "Source clips can be up to 60 seconds. Trim it first.")
    if info.creation_time and asset.capture_at is None:
        asset.capture_at, asset.capture_at_source = info.creation_time, "metadata"
    asset.rotation = info.rotation
    start = max(0, asset.trim_start_ms or 0)
    end = asset.trim_end_ms or min(info.duration_ms or 15000, start + int(limits.highlight_seconds * 1000))
    if end - start > limits.highlight_seconds * 1000:
        end = start + int(limits.highlight_seconds * 1000)
    if end <= start:
        raise MediaRejected("invalid_trim", "The trim end must come after the start.")
    folder = asset_dir(asset)
    out = folder / "display.mp4"
    exe = ffmpeg_path()
    assert exe is not None
    vf = "scale='if(gt(iw,ih),min(1280,iw),-2)':'if(gt(iw,ih),-2,min(1280,ih))',fps=30,format=yuv420p"
    if asset.rotation_override is not None:
        vf = {90: "transpose=1,", 180: "transpose=1,transpose=1,", 270: "transpose=2,"}.get(asset.rotation_override % 360, "") + vf
    args = [exe, "-y", "-hide_banner", "-ss", f"{start/1000:.3f}", "-i", asset.original_path, "-t", f"{(end-start)/1000:.3f}",
            "-map", "0:v:0", "-map", "0:a:0?", "-map_metadata", "-1", "-vf", vf,
            "-c:v", "libx264", "-profile:v", "high", "-preset", "veryfast", "-crf", "24",
            "-c:a", "aac", "-b:a", "128k", "-ac", "2", "-movflags", "+faststart", str(out)]
    _run(args)
    if out.stat().st_size > limits.highlight_bytes:
        raise MediaRejected("highlight_too_large", "The published highlight is over 25 MB. Trim it shorter.")
    thumb = folder / "thumb.jpg"
    _run([exe, "-y", "-hide_banner", "-ss", "0.5", "-i", str(out), "-frames:v", "1", "-vf", "scale=512:-2", str(thumb)], timeout=60)
    final = probe(out)
    asset.width, asset.height, asset.duration_ms = final.width, final.height, final.duration_ms
    asset.derivative_path, asset.derivative_mime = str(out), "video/mp4"
    asset.thumbnail_path = str(thumb)


def _process_audio(asset: MediaAsset) -> None:
    limits = get_settings().media
    info = probe(Path(asset.original_path))
    asset.input_profile = {**asset.input_profile, "audioCodec": info.audio_codec, "durationMs": info.duration_ms}
    if info.audio_codec is None:
        raise MediaRejected("no_audio_track", "This recording has no audio.")
    codec_family = info.audio_codec.split("_")[0] if info.audio_codec.startswith("pcm") else info.audio_codec
    if info.audio_codec not in AUDIO_CODECS and codec_family != "pcm":
        raise MediaRejected("unsupported_codec", f"The {info.audio_codec} audio codec isn't supported.")
    if info.duration_ms is not None and info.duration_ms > limits.voice_seconds * 1000 + 500:
        raise MediaRejected("too_long", "Voice notes can be up to 30 seconds.")
    out = asset_dir(asset) / "voice.m4a"
    exe = ffmpeg_path()
    assert exe is not None
    _run([exe, "-y", "-hide_banner", "-i", asset.original_path, "-vn", "-map_metadata", "-1", "-t", str(limits.voice_seconds),
          "-c:a", "aac", "-b:a", "96k", "-ac", "1", "-movflags", "+faststart", str(out)])
    asset.duration_ms = probe(out).duration_ms
    asset.derivative_path, asset.derivative_mime = str(out), "audio/mp4"


def variant_path(asset: MediaAsset, variant: str) -> tuple[Path, str] | None:
    if asset.processing_state != "ready":
        return None
    if variant == "display" and asset.derivative_path:
        return Path(asset.derivative_path), asset.derivative_mime or "application/octet-stream"
    if variant == "thumb" and asset.thumbnail_path:
        return Path(asset.thumbnail_path), "image/jpeg"
    return None  # originals are never served to recipients


def delete_files(asset: MediaAsset) -> None:
    folder = get_settings().media_root / asset.owner_id / asset.id
    if folder.exists():
        shutil.rmtree(folder, ignore_errors=True)


def describe(asset: MediaAsset) -> dict:
    return {
        "id": asset.id,
        "kind": asset.kind,
        "processingState": asset.processing_state,
        "safeError": asset.safe_error,
        "sourceDevice": asset.source_device,
        "projection": asset.projection,
        "durationMs": asset.duration_ms,
        "width": asset.width,
        "height": asset.height,
        "rotation": asset.rotation_override if asset.rotation_override is not None else asset.rotation,
        "captureAt": asset.capture_at.isoformat() if asset.capture_at else None,
        "captureAtSource": asset.capture_at_source,
        "needsCaptureTime": asset.capture_at is None,
        "inputProfile": {k: v for k, v in (asset.input_profile or {}).items() if k != "exifCaptureLocal"},
    }
