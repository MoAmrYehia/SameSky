"""Two real sessions: avatars, synchronized play/pause/seek, reconnect, removal: criteria 12, 15."""

import pytest
from conftest import at
from starlette.websockets import WebSocketDisconnect


def _setup(api):
    adam, maya, lina = api.signup("Adam"), api.signup("Maya"), api.signup("Lina")
    room = api.room(adam, "circle", "Family", members=[maya, lina])
    api.call(maya, "PUT", "/v1/me/avatar", json={"skin": "#d29a72", "hair": "long", "hairColor": "#2b1d14", "top": "#e9e0cf", "accessory": "earrings"})
    m = api.text_moment(adam, room["id"], "Watch with me", at(6))
    api.share(adam, m["id"], [maya["id"]])
    session = api.call(adam, "POST", f"/v1/rooms/{room['id']}/sessions", json={"moment_id": m["id"], "participant_ids": [maya["id"]]}).json()
    return adam, maya, lina, room, m, session


def _hello(conn, user, session_id):
    conn.send_json({"type": "auth", "token": user["token"]})
    conn.send_json({"type": "join", "sessionId": session_id})
    return conn


def _next(conn, kind):
    for _ in range(10):
        msg = conn.receive_json()
        if msg["type"] == kind:
            return msg
    raise AssertionError(f"no {kind}")


def test_session_requires_both_can_view(api):
    adam, maya, lina, room, m, session = _setup(api)
    r = api.call(adam, "POST", f"/v1/rooms/{room['id']}/sessions", json={"moment_id": m["id"], "participant_ids": [lina["id"]]}, expect=422)
    assert r.json()["error"]["code"] == "participant_cannot_view"


def test_two_sessions_sync_and_avatars(api):
    adam, maya, lina, room, m, session = _setup(api)
    with api.c.websocket_connect("/v1/realtime") as a:
        welcome = _next(_hello(a, adam, session["id"]), "welcome")
        with api.c.websocket_connect("/v1/realtime") as mconn:
            welcome_m = _next(_hello(mconn, maya, session["id"]), "welcome")
            maya_avatar = next(p for p in welcome_m["participants"] if p["userId"] == maya["id"])["avatar"]
            assert maya_avatar["definition"]["hair"] == "long"
            joined = _next(a, "peer_joined")
            assert joined["userId"] == maya["id"] and joined["avatar"]["definition"]["accessory"] == "earrings"

            a.send_json({"type": "playback", "action": "play", "positionMs": 0, "baseVersion": welcome["state"]["version"]})
            s1 = _next(mconn, "state")
            assert s1["paused"] is False and s1["by"] == adam["id"]
            assert _next(a, "state")["version"] == s1["version"]
            mconn.send_json({"type": "playback", "action": "seek", "positionMs": 4000, "baseVersion": s1["version"]})
            s2 = _next(a, "state")
            _next(mconn, "state")
            assert s2["playheadMs"] == 4000 and s2["version"] == s1["version"] + 1
            a.send_json({"type": "playback", "action": "pause", "baseVersion": s2["version"]})
            s3 = _next(mconn, "state")
            assert s3["paused"] is True and s3["playheadMs"] >= 4000

            # Poses are relayed, sanitized, and out-of-range values are dropped.
            a.send_json({"type": "pose", "pose": {"tracked": True, "source": "xr", "head": {"p": [0, 1.2, 0.6], "q": [0, 0, 0, 1]}}})
            pose = _next(mconn, "pose")
            assert pose["pose"]["head"]["p"] == [0, 1.2, 0.6]
            a.send_json({"type": "pose", "pose": {"head": {"p": [0, 99, 0], "q": [0, 0, 0, 1]}}})
            a.send_json({"type": "ping", "t": 1})
            assert _next(a, "pong")["t"] == 1
            mconn.send_json({"type": "leave"})
        assert _next(a, "peer_left")["userId"] == maya["id"]
        # Reconnect: a late (re)joiner receives the current authoritative state.
        with api.c.websocket_connect("/v1/realtime") as m2:
            again = _next(_hello(m2, maya, session["id"]), "welcome")
            assert again["state"]["version"] == s3["version"] and again["state"]["paused"] is True
            m2.send_json({"type": "leave"})
        a.send_json({"type": "leave"})


def test_removed_member_cannot_join_or_command(api):
    adam, maya, lina, room, m, session = _setup(api)
    api.call(adam, "DELETE", f"/v1/rooms/{room['id']}/members/{maya['id']}")
    with api.c.websocket_connect("/v1/realtime") as conn:
        msg = _hello(conn, maya, session["id"]).receive_json()
        assert msg == {"type": "error", "code": "not_found"}
        with pytest.raises(WebSocketDisconnect):
            conn.receive_json()


def test_outsider_and_bad_token_rejected(api):
    adam, maya, lina, room, m, session = _setup(api)
    with api.c.websocket_connect("/v1/realtime") as conn:
        assert _hello(conn, lina, session["id"]).receive_json()["code"] == "not_found"
    with api.c.websocket_connect("/v1/realtime") as conn:
        conn.send_json({"type": "auth", "token": "nope"})
        with pytest.raises(WebSocketDisconnect):
            conn.receive_json()
