"""Photo-to-avatar provider adapters (server side only; keys never reach clients).

The adapter boundary is deliberately small: submit validated inputs, query status, fetch the exported
model, delete the provider-side record. The MetaPerson adapter follows Avatar SDK's documented REST flow
(https://api.avatarsdk.com/samples/curl_metaperson_20/ and https://api.avatarsdk.com/):

    POST /o/token/ (client credentials)        -> bearer token
    POST /avatars/ (multipart, pipeline=metaperson_2.0, photo, parameters, export_parameters)
    GET  /avatars/{code}/                       -> status: Pending|Uploading|Queued|Computing|Completed|Failed|Timed Out
    GET  /avatars/{code}/exports/               -> [{status, code, files:[{identity, file}]}]
    GET  /avatars/{code}/exports/{export}/files/avatar/file/  -> zip containing the GLB
    DELETE /avatars/{code}/

There are no webhooks in that API (callers poll), so nothing here accepts an unauthenticated callback.
This adapter has NOT been exercised against the live service from this repository: no credentials were
available. It reports configuration, plan (HTTP 402) and credential failures honestly instead of faking
progress.
"""

from __future__ import annotations

import io
import json
import time
import zipfile
from dataclasses import dataclass
from pathlib import Path
from typing import Protocol
from urllib.parse import urlparse

from ..config import get_settings

PIPELINE = "metaperson_2.0"


@dataclass
class ProviderStatus:
    state: str  # queued | computing | completed | failed | timed_out
    progress: int = 0
    message: str | None = None


class ProviderError(Exception):
    def __init__(self, code: str, message: str, *, retryable: bool = False):
        super().__init__(code)
        self.code = code
        self.message = message
        self.retryable = retryable


class AvatarProvider(Protocol):
    name: str

    def configured(self) -> bool: ...

    def submit(self, photo: Path, options: dict) -> str: ...

    def status(self, ref: str) -> ProviderStatus: ...

    def fetch_export(self, ref: str, max_bytes: int) -> bytes | None: ...

    def delete(self, ref: str) -> bool: ...


def parameters_for(options: dict) -> dict:
    """Computation parameters: a neutral face (no baked smile), glasses removed as the docs recommend."""
    return {"avatar_modifications": {"remove_smile": True, "remove_glasses": bool(options.get("remove_glasses", True))}}


def export_parameters_for(options: dict, outfit: str | None) -> dict:
    """Export a single self-contained GLB with 1K PBR textures and the 51 mobile blendshapes (eye blinks etc.)."""
    params: dict = {
        "format": "glb",
        "lod": "LOD1",
        "embed": True,
        "finalize": True,
        "textures": {
            "list": ["Color", "Normal", "GltfMetallicRoughness", "BodyVisibilityMask", "HeadVisibilityMask"],
            "embed": True,
            "profile": "1K.png",
        },
        "avatar": {
            "list": [
                "AvatarBody", "AvatarEyelashes", "AvatarHead", "AvatarLeftCornea", "AvatarLeftEyeball",
                "AvatarRightCornea", "AvatarRightEyeball", "AvatarTeethLower", "AvatarTeethUpper",
            ]
        },
        "blendshapes": {"list": ["face_modifications", "body_shape", "mobile_51"]},
        "haircuts": {"list": ["HaircutGenerated"]},
    }
    if outfit:
        params["outfits"] = {"list": [outfit]}
    return params


def _first_list(value) -> list[str]:
    """Pull a list of names out of an `available` response whose exact shape the docs don't spell out."""
    if isinstance(value, list):
        return [str(v) for v in value if isinstance(v, (str, int))]
    if isinstance(value, dict):
        for key in ("list", "full_list", "available", "values"):
            if key in value:
                return _first_list(value[key])
        for v in value.values():
            found = _first_list(v)
            if found:
                return found
    return []


class MetaPersonProvider:
    name = "metaperson"

    def __init__(self) -> None:
        self._token: str | None = None
        self._token_expires = 0.0

    # --- plumbing ---

    def configured(self) -> bool:
        s = get_settings()
        return bool(s.metaperson_client_id and s.metaperson_client_secret)

    def _client(self):
        import httpx

        return httpx.Client(base_url=get_settings().metaperson_api_url, timeout=60.0, follow_redirects=False)

    def _check(self, response) -> None:
        code = response.status_code
        if code in (401, 403):
            raise ProviderError("provider_credentials", "The avatar service rejected this server's credentials. An administrator needs to check them.")
        if code == 402:
            raise ProviderError("provider_plan_required", "The avatar service account has no active paid plan, so it can't create avatars right now.")
        if code == 429:
            raise ProviderError("provider_busy", "The avatar service is busy or the account quota is used up. We'll try again.", retryable=True)
        if code >= 500:
            raise ProviderError("provider_unavailable", "The avatar service is temporarily unavailable. We'll try again.", retryable=True)
        if code >= 400:
            raise ProviderError("provider_rejected", f"The avatar service rejected the request (HTTP {code}).")

    def _headers(self) -> dict:
        if not self._token or time.time() > self._token_expires:
            s = get_settings()
            with self._client() as client:
                try:
                    r = client.post("/o/token/", auth=(s.metaperson_client_id or "", s.metaperson_client_secret or ""), data={"grant_type": "client_credentials"})
                except Exception as exc:  # noqa: BLE001 - network error; never leak details
                    raise ProviderError("provider_unavailable", "Couldn't reach the avatar service. We'll try again.", retryable=True) from exc
                self._check(r)
                body = r.json()
            self._token = body.get("access_token")
            self._token_expires = time.time() + max(60, int(body.get("expires_in", 3600)) - 120)
            if not self._token:
                raise ProviderError("provider_credentials", "The avatar service didn't issue an access token.")
        headers = {"Authorization": f"Bearer {self._token}"}
        player = get_settings().metaperson_player_uid
        if player:
            headers["X-PlayerUID"] = player
        return headers

    def _request(self, method: str, path: str, **kw):
        with self._client() as client:
            try:
                r = client.request(method, path, headers=self._headers(), **kw)
            except ProviderError:
                raise
            except Exception as exc:  # noqa: BLE001
                raise ProviderError("provider_unavailable", "Couldn't reach the avatar service. We'll try again.", retryable=True) from exc
        return r

    # --- adapter ---

    def _choose_outfit(self, subtype: str, wanted: str | None) -> str | None:
        r = self._request("GET", f"/export_parameters/available/{PIPELINE}/", params={"pipeline_subtype": subtype})
        self._check(r)
        try:
            outfits = _first_list(r.json().get("outfits", {}))
        except ValueError:
            outfits = []
        if wanted and wanted in outfits:
            return wanted
        return outfits[0] if outfits else None

    def submit(self, photo: Path, options: dict) -> str:
        subtype = options.get("body")
        if subtype not in ("female", "male"):
            raise ProviderError("invalid_options", "Choose one of the provider's body models before generating.")
        outfit = self._choose_outfit(subtype, options.get("outfit"))
        if not outfit:
            raise ProviderError("no_outfit", "The avatar service didn't offer an outfit for this body model, so generation was stopped.")
        files = {"photo": (photo.name, photo.read_bytes(), "image/jpeg")}
        data = {
            "name": "SameSky avatar",
            "pipeline": PIPELINE,
            "pipeline_subtype": subtype,
            "parameters": json.dumps(parameters_for(options)),
            "export_parameters": json.dumps(export_parameters_for(options, outfit)),
        }
        r = self._request("POST", "/avatars/", data=data, files=files)
        self._check(r)
        code = r.json().get("code")
        if not code:
            raise ProviderError("provider_rejected", "The avatar service didn't return an avatar id.")
        return str(code)

    def status(self, ref: str) -> ProviderStatus:
        r = self._request("GET", f"/avatars/{ref}/")
        self._check(r)
        body = r.json()
        raw = str(body.get("status", ""))
        progress = int(body.get("progress") or 0)
        mapping = {"Pending": "queued", "Uploading": "queued", "Queued": "queued", "Computing": "computing", "Completed": "completed", "Failed": "failed", "Timed Out": "timed_out"}
        state = mapping.get(raw, "computing")
        message = None
        if state == "failed":
            # status_verbose usually explains a bad (blurry or dark) photo; keep it short and plain.
            message = str(body.get("status_verbose") or "The photo couldn't be turned into an avatar. Try a sharper, evenly lit, front-facing photo.")[:200]
        return ProviderStatus(state=state, progress=max(0, min(100, progress)), message=message)

    def fetch_export(self, ref: str, max_bytes: int) -> bytes | None:
        r = self._request("GET", f"/avatars/{ref}/exports/")
        self._check(r)
        exports = r.json()
        if not isinstance(exports, list) or not exports:
            return None
        done = next((e for e in exports if e.get("status") == "Completed"), None)
        if done is None:
            if all(e.get("status") == "Failed" for e in exports):
                raise ProviderError("export_failed", "The avatar was created but its 3D export failed. Try generating again.")
            return None
        url = f"/avatars/{ref}/exports/{done.get('code')}/files/avatar/file/"
        payload = self._download(url, max_bytes)
        return extract_glb(payload, max_bytes)

    def _download(self, path: str, max_bytes: int) -> bytes:
        """Download from the API host, following redirects only to allow-listed export origins."""
        allowed = set(get_settings().avatar_export_origins)
        target = path
        for _ in range(4):
            r = self._request("GET", target)
            if r.status_code in (301, 302, 303, 307, 308):
                location = r.headers.get("location", "")
                host = urlparse(location).hostname or ""
                if host not in allowed:
                    raise ProviderError("export_origin_not_allowed", "The export came from a host this server isn't configured to trust.")
                target = location
                continue
            self._check(r)
            if len(r.content) > max_bytes * 2:
                raise ProviderError("export_too_large", "The exported model is larger than this server accepts.")
            return r.content
        raise ProviderError("export_redirects", "The export download redirected too many times.")

    def delete(self, ref: str) -> bool:
        r = self._request("DELETE", f"/avatars/{ref}/")
        if r.status_code == 404:
            return True
        self._check(r)
        return True


def extract_glb(payload: bytes, max_bytes: int) -> bytes:
    """The export is a zip holding the GLB (or the GLB itself). Bounded, in-memory, no paths written."""
    if payload[:4] == b"glTF":
        return payload
    try:
        archive = zipfile.ZipFile(io.BytesIO(payload))
    except zipfile.BadZipFile:
        raise ProviderError("export_unreadable", "The exported model archive couldn't be read.") from None
    candidates = [i for i in archive.infolist() if i.filename.lower().endswith(".glb") and not i.is_dir()]
    if not candidates:
        raise ProviderError("export_unreadable", "The exported archive didn't contain a GLB model.")
    best = max(candidates, key=lambda i: i.file_size)
    if best.file_size > max_bytes:
        raise ProviderError("export_too_large", "The exported model is larger than this server accepts.")
    with archive.open(best) as fh:
        data = fh.read(max_bytes + 1)
    if len(data) > max_bytes:
        raise ProviderError("export_too_large", "The exported model is larger than this server accepts.")
    return data


_PROVIDERS: dict[str, AvatarProvider] = {}
_override: AvatarProvider | None = None


def set_test_provider(provider: AvatarProvider | None) -> None:
    """Tests inject a fake provider; production never calls this."""
    global _override
    _override = provider


def get_provider(name: str | None = None) -> AvatarProvider | None:
    name = name or get_settings().avatar_provider
    if _override is not None and (_override.name == name or name == get_settings().avatar_provider):
        return _override
    if name == "metaperson":
        return _PROVIDERS.setdefault("metaperson", MetaPersonProvider())
    return None
