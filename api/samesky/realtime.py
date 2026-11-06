"""App-owned realtime channel for two-person co-presence and synchronized playback.

Three separate concerns, as the brief requires:
* ephemeral head/hand poses (table-relative, range-checked, rate-limited, never persisted);
* server-authorized playback commands (media id, playhead, paused, server timestamp, session version);
* durable state (sessions/replies/placements) stays in the database and is refetched on reconnect.

Every playback command re-checks membership and source visibility. Removing a member or withdrawing
the source closes their live connection immediately. Disconnected presence expires after 10 s.
"""

from __future__ import annotations

import asyncio
import contextlib
import json
import logging
import math
import time
from dataclasses import dataclass, field

from fastapi import WebSocket, WebSocketDisconnect

from .db import new_session
from .models import Avatar, Moment, User, ViewingSession, utcnow
from .security import TokenError, decode_access_token
from .services.access import active_membership, can_view_moment

log = logging.getLogger("samesky.realtime")

POSE_LIMIT_HZ = 25
PRESENCE_TTL_S = 10.0
MAX_OFFSET_M = 2.5


@dataclass
class Peer:
    user_id: str
    ws: WebSocket
    last_seen: float = field(default_factory=time.monotonic)
    pose_window_start: float = field(default_factory=time.monotonic)
    pose_count: int = 0


@dataclass
class LiveSession:
    session_id: str
    room_id: str
    peers: dict[str, Peer] = field(default_factory=dict)


class Hub:
    def __init__(self) -> None:
        self.sessions: dict[str, LiveSession] = {}
        self.loop: asyncio.AbstractEventLoop | None = None
        self._reaper: asyncio.Task | None = None

    # ---- thread-safe entry points used by synchronous request handlers -------------------------

    def kick_user_from_room(self, room_id: str, user_id: str) -> None:
        self._threadsafe(self._kick(room_id, user_id, "membership_ended"))

    def end_session(self, session_id: str, reason: str) -> None:
        self._threadsafe(self._end(session_id, reason))

    def _threadsafe(self, coro) -> None:
        if self.loop is None or self.loop.is_closed():
            coro.close()
            return
        try:
            running = asyncio.get_running_loop()
        except RuntimeError:
            running = None
        if running is self.loop:
            self.loop.create_task(coro)
        else:
            asyncio.run_coroutine_threadsafe(coro, self.loop)

    async def _kick(self, room_id: str, user_id: str, reason: str) -> None:
        for live in list(self.sessions.values()):
            if live.room_id == room_id and user_id in live.peers:
                peer = live.peers.pop(user_id)
                with contextlib.suppress(Exception):
                    await peer.ws.send_json({"type": "removed", "reason": reason})
                    await peer.ws.close(code=4403)
                await self.broadcast(live, {"type": "peer_left", "userId": user_id, "reason": reason})

    async def _end(self, session_id: str, reason: str) -> None:
        live = self.sessions.pop(session_id, None)
        if live is None:
            return
        for peer in list(live.peers.values()):
            with contextlib.suppress(Exception):
                await peer.ws.send_json({"type": "session_ended", "reason": reason})
                await peer.ws.close(code=4410)

    async def broadcast(self, live: LiveSession, message: dict, exclude: str | None = None) -> None:
        for uid, peer in list(live.peers.items()):
            if uid == exclude:
                continue
            try:
                await peer.ws.send_json(message)
            except Exception:  # noqa: BLE001
                live.peers.pop(uid, None)

    async def reap(self) -> None:
        while True:
            await asyncio.sleep(2)
            now = time.monotonic()
            for live in list(self.sessions.values()):
                for uid, peer in list(live.peers.items()):
                    if now - peer.last_seen > PRESENCE_TTL_S:
                        live.peers.pop(uid, None)
                        with contextlib.suppress(Exception):
                            await peer.ws.close(code=4408)
                        await self.broadcast(live, {"type": "peer_left", "userId": uid, "reason": "presence_expired"})
                if not live.peers:
                    self.sessions.pop(live.session_id, None)

    def ensure_started(self) -> None:
        loop = asyncio.get_running_loop()
        if self.loop is not loop:  # new server loop (restart, or a fresh test client)
            self.loop, self._reaper, self.sessions = loop, None, {}
        if self._reaper is None or self._reaper.done():
            self._reaper = loop.create_task(self.reap())


hub = Hub()


def session_state(session: ViewingSession) -> dict:
    return {
        "type": "state",
        "sessionId": session.id,
        "momentId": session.moment_id,
        "version": session.version,
        "playheadMs": session.playhead_ms,
        "paused": session.paused,
        "stateAt": session.state_at.timestamp() * 1000,
        "serverTime": time.time() * 1000,
    }


def authorize_participant(db, session_id: str, user_id: str) -> ViewingSession | None:
    session = db.get(ViewingSession, session_id)
    if session is None or session.ended_at is not None or user_id not in (session.participant_ids or []):
        return None
    if active_membership(db, session.room_id, user_id) is None:
        return None
    moment = db.get(Moment, session.moment_id)
    if moment is None or not can_view_moment(db, moment, user_id):
        return None
    return session


def _valid_vec(v, n: int, bound: float | None) -> bool:
    if not isinstance(v, list) or len(v) != n:
        return False
    if not all(isinstance(x, (int, float)) and math.isfinite(x) for x in v):
        return False
    return bound is None or all(abs(x) <= bound for x in v)


def sanitize_pose(raw: dict) -> dict | None:
    out = {"tracked": bool(raw.get("tracked")), "source": raw.get("source") if raw.get("source") in ("xr", "pointer", "animation") else "animation"}
    for part in ("head", "left", "right"):
        node = raw.get(part)
        if node is None:
            continue
        if not isinstance(node, dict) or not _valid_vec(node.get("p"), 3, MAX_OFFSET_M) or not _valid_vec(node.get("q"), 4, 1.01):
            return None
        out[part] = {"p": [round(x, 4) for x in node["p"]], "q": [round(x, 4) for x in node["q"]]}
    if "head" not in out:
        return None
    return out


async def realtime_endpoint(ws: WebSocket) -> None:
    await ws.accept()
    hub.ensure_started()
    user_id: str | None = None
    live: LiveSession | None = None
    try:
        auth = await asyncio.wait_for(ws.receive_json(), timeout=10)
        if auth.get("type") != "auth":
            await ws.close(code=4401)
            return
        try:
            _, user_id = decode_access_token(str(auth.get("token", "")))
        except TokenError:
            await ws.close(code=4401)
            return
        join = await asyncio.wait_for(ws.receive_json(), timeout=10)
        session_id = str(join.get("sessionId", ""))
        with new_session() as db:
            session = authorize_participant(db, session_id, user_id)
            if session is None:
                await ws.send_json({"type": "error", "code": "not_found"})
                await ws.close(code=4404)
                return
            state = session_state(session)
            participants = list(session.participant_ids)
            avatars = {a.user_id: {"definition": a.definition, "version": a.version} for a in db.query(Avatar).filter(Avatar.user_id.in_(participants))}
            names = {u.id: u.display_name for u in db.query(User).filter(User.id.in_(participants))}
            room_id = session.room_id
        live = hub.sessions.setdefault(session_id, LiveSession(session_id=session_id, room_id=room_id))
        old = live.peers.get(user_id)
        if old is not None:
            with contextlib.suppress(Exception):
                await old.ws.close(code=4409)
        live.peers[user_id] = Peer(user_id=user_id, ws=ws)
        await ws.send_json({
            "type": "welcome", "userId": user_id, "state": state,
            "participants": [{"userId": p, "displayName": names.get(p, "Member"), "avatar": avatars.get(p), "online": p in live.peers} for p in participants],
        })
        await hub.broadcast(live, {"type": "peer_joined", "userId": user_id, "displayName": names.get(user_id), "avatar": avatars.get(user_id)}, exclude=user_id)

        while True:
            message = await ws.receive_json()
            peer = live.peers.get(user_id)
            if peer is None:
                break
            peer.last_seen = time.monotonic()
            kind = message.get("type")
            if kind == "ping":
                await ws.send_json({"type": "pong", "t": message.get("t"), "serverTime": time.time() * 1000})
            elif kind == "pose":
                now = time.monotonic()
                if now - peer.pose_window_start >= 1.0:
                    peer.pose_window_start, peer.pose_count = now, 0
                peer.pose_count += 1
                if peer.pose_count > POSE_LIMIT_HZ:
                    continue  # drop excess updates
                pose = sanitize_pose(message.get("pose") or {})
                if pose is None:
                    continue
                await hub.broadcast(live, {"type": "pose", "userId": user_id, "pose": pose, "serverTime": time.time() * 1000}, exclude=user_id)
            elif kind == "playback":
                await _handle_playback(live, user_id, message, ws)
            elif kind == "reaction" and message.get("kind") in ("heart", "smile", "wow", "hug", "sun"):
                await hub.broadcast(live, {"type": "reaction", "userId": user_id, "kind": message["kind"]})
            elif kind == "leave":
                break
    except (WebSocketDisconnect, asyncio.TimeoutError, json.JSONDecodeError, RuntimeError):
        pass
    finally:
        if live is not None and user_id is not None:
            peer = live.peers.get(user_id)
            if peer is not None and peer.ws is ws:
                live.peers.pop(user_id, None)
                await hub.broadcast(live, {"type": "peer_left", "userId": user_id, "reason": "left"})
            if not live.peers:
                hub.sessions.pop(live.session_id, None)


async def _handle_playback(live: LiveSession, user_id: str, message: dict, ws: WebSocket) -> None:
    action = message.get("action")
    if action not in ("play", "pause", "seek"):
        return
    with new_session() as db:
        session = authorize_participant(db, live.session_id, user_id)
        if session is None:
            await ws.send_json({"type": "removed", "reason": "not_authorized"})
            await ws.close(code=4403)
            live.peers.pop(user_id, None)
            return
        base = message.get("baseVersion")
        if isinstance(base, int) and base < session.version - 5:
            await ws.send_json(session_state(session))  # far behind: resync instead of applying
            return
        now = utcnow()
        current = session.playhead_ms
        if not session.paused:
            current += int((now - session.state_at).total_seconds() * 1000)
        position = message.get("positionMs")
        if isinstance(position, (int, float)) and math.isfinite(position) and 0 <= position <= 15 * 60 * 1000:
            current = int(position)
        session.playhead_ms = max(0, current)
        session.paused = action == "pause" or (action == "seek" and session.paused)
        session.state_at = now
        session.version += 1
        db.commit()
        state = session_state(session)
        state["by"] = user_id
    await hub.broadcast(live, state)
