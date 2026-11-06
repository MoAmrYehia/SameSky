"""Audience isolation: criteria 1, 2, 11, 12, 13."""

from conftest import at, png_bytes, run_worker


def _setup_family(api):
    adam, maya, lina = api.signup("Adam"), api.signup("Maya", "Asia/Shanghai"), api.signup("Lina")
    direct = api.room(adam, "direct", "Adam + Maya", members=[maya])
    circle = api.room(adam, "circle", "Family circle", members=[maya, lina])
    return adam, maya, lina, direct, circle


def test_member_cannot_access_private_or_other_room_content(api):
    adam, maya, lina, direct, circle = _setup_family(api)
    # Private (unshared) moment: even Maya, a member of the same direct room, can't read it.
    private = api.text_moment(adam, direct["id"], "Just for me", at(6))
    api.call(maya, "GET", f"/v1/moments/{private['id']}", expect=404)
    # Lina is not in the direct room at all.
    shared = api.text_moment(adam, direct["id"], "Saved you a seat", at(6, 15))
    api.share(adam, shared["id"], [maya["id"]])
    api.call(maya, "GET", f"/v1/moments/{shared['id']}")
    api.call(lina, "GET", f"/v1/moments/{shared['id']}", expect=404)
    api.call(lina, "GET", f"/v1/rooms/{direct['id']}/moments", expect=404)
    api.call(lina, "GET", f"/v1/moments/{shared['id']}/replies", expect=404)
    api.call(lina, "POST", f"/v1/moments/{shared['id']}/replies", json={"text": "hi"}, expect=404)
    # Media URL issued to Maya can't be used by Lina (signature is bound to the viewer).
    asset = api.upload(adam, png_bytes(), "image", "p.png").json()
    run_worker()
    photo = api.call(adam, "POST", f"/v1/rooms/{direct['id']}/moments", json={"kind": "photo", "media_asset_id": asset["id"], "capture_at": at(6).isoformat()}).json()
    photo = api.share(adam, photo["id"], [maya["id"]])
    maya_view = api.call(maya, "GET", f"/v1/moments/{photo['id']}").json()
    url = maya_view["media"]["url"]
    assert api.c.get(url).status_code == 200
    forged = url.replace(f"u={maya['id']}", f"u={lina['id']}")
    assert api.c.get(forged).status_code == 403


def test_preview_and_notifications_stay_with_their_audience(api):
    adam, maya, lina, direct, circle = _setup_family(api)
    a = api.text_moment(adam, direct["id"], "Sunset walk by the water", at(6))
    api.share(adam, a["id"], [maya["id"]])
    m = api.text_moment(maya, direct["id"], "Quiet walk after work", at(6, 13))
    api.share(maya, m["id"], [adam["id"]])
    recap = api.call(adam, "POST", f"/v1/rooms/{direct['id']}/recaps", json={"room_date": "2026-11-06"}).json()
    run_worker()
    # Contributor sees the private preview; an outsider sees nothing at all.
    preview = api.call(maya, "GET", f"/v1/recaps/{recap['id']}").json()
    assert preview["state"] == "private_preview" and preview["plan"] is not None
    api.call(lina, "GET", f"/v1/recaps/{recap['id']}", expect=404)
    api.call(adam, "POST", f"/v1/recaps/{recap['id']}/approve")
    api.call(maya, "POST", f"/v1/recaps/{recap['id']}/approve")
    run_worker()
    assert [n["kind"] for n in api.call(adam, "GET", "/v1/notifications").json()["items"]] == ["recap_ready"]
    assert api.call(lina, "GET", "/v1/notifications").json()["items"] == []


def test_direct_moment_never_reaches_circle(api):
    adam, maya, lina, direct, circle = _setup_family(api)
    secret = api.text_moment(adam, direct["id"], "DIRECT-ONLY anniversary plan", at(6))
    api.share(adam, secret["id"], [maya["id"]])
    fam_a = api.text_moment(adam, circle["id"], "Family dinner photo", at(6, 12))
    api.share(adam, fam_a["id"], [maya["id"], lina["id"]])
    fam_m = api.text_moment(maya, circle["id"], "Made dumplings", at(6, 11))
    api.share(maya, fam_m["id"], [adam["id"], lina["id"]])
    recap = api.recap_published(adam, circle["id"], "2026-11-06", [adam, maya])
    assert recap["state"] == "shared"
    run_worker()
    blob = str(api.call(lina, "GET", f"/v1/recaps/{recap['id']}").json())
    assert "DIRECT-ONLY" not in blob and secret["id"] not in blob
    # Circle feed, objects, notifications and awards show nothing of the direct room.
    assert all(m["id"] != secret["id"] for m in api.call(lina, "GET", f"/v1/rooms/{circle['id']}/moments").json())
    assert "DIRECT-ONLY" not in str(api.call(lina, "GET", "/v1/notifications").json())
    circle_awards = api.call(lina, "GET", f"/v1/rooms/{circle['id']}/awards").json()
    direct_awards = api.call(adam, "GET", f"/v1/rooms/{direct['id']}/awards").json()
    assert next(a for a in direct_awards if a["milestone"] == "first_shared_day")["earned"] is False
    assert next(a for a in circle_awards if a["milestone"] == "first_shared_day")["earned"] is True
    # Placing the direct moment on the circle table is refused.
    api.call(adam, "POST", f"/v1/rooms/{circle['id']}/objects", json={"object_type": "frame", "moment_id": secret["id"]}, expect=404)


def test_recap_only_uses_sources_visible_to_whole_audience(api):
    adam, maya, lina, direct, circle = _setup_family(api)
    to_maya_only = api.text_moment(adam, circle["id"], "Only for Maya inside the circle", at(6))
    api.share(adam, to_maya_only["id"], [maya["id"]])
    everyone = api.text_moment(maya, circle["id"], "Hello family", at(6, 12))
    api.share(maya, everyone["id"], [adam["id"], lina["id"]])
    recap = api.call(maya, "POST", f"/v1/rooms/{circle['id']}/recaps", json={"room_date": "2026-11-06"}).json()
    run_worker()
    detail = api.call(maya, "GET", f"/v1/recaps/{recap['id']}").json()
    assert "Only for Maya" not in str(detail["plan"])
    # A subset audience (Adam + Maya) may include it, and is a separate recap with its own cache key.
    pair = api.call(adam, "POST", f"/v1/rooms/{circle['id']}/recaps", json={"room_date": "2026-11-06", "recipient_ids": [maya["id"]]}).json()
    assert pair["id"] != recap["id"]
    run_worker()
    assert "Only for Maya" in str(api.call(adam, "GET", f"/v1/recaps/{pair['id']}").json()["plan"])
    api.call(lina, "GET", f"/v1/recaps/{pair['id']}", expect=404)


def test_withdrawal_blocks_reads_and_stale_generation(api):
    adam, maya, lina, direct, circle = _setup_family(api)
    a = api.text_moment(adam, direct["id"], "Rooftop", at(6))
    api.share(adam, a["id"], [maya["id"]])
    m = api.text_moment(maya, direct["id"], "Park", at(6, 12))
    api.share(maya, m["id"], [adam["id"]])
    recap = api.call(adam, "POST", f"/v1/rooms/{direct['id']}/recaps", json={"room_date": "2026-11-06"}).json()
    # Withdraw *before* the queued generation job runs: the job must not produce a preview.
    api.call(adam, "POST", f"/v1/moments/{a['id']}/withdraw", json={})
    run_worker()
    status = api.call(maya, "GET", f"/v1/recaps/{recap['id']}").json()
    assert status["state"] == "invalidated" and status["plan"] is None
    api.call(maya, "GET", f"/v1/moments/{a['id']}", expect=404)


def test_withdrawal_after_publication_invalidates_recap_and_media(api):
    adam, maya, lina, direct, circle = _setup_family(api)
    asset = api.upload(adam, png_bytes(), "image", "p.png").json()
    run_worker()
    photo = api.call(adam, "POST", f"/v1/rooms/{direct['id']}/moments", json={"kind": "photo", "media_asset_id": asset["id"], "capture_at": at(6).isoformat(), "ai_allowed": True, "caption": "x"}).json()
    api.share(adam, photo["id"], [maya["id"]])
    m = api.text_moment(maya, direct["id"], "Park", at(6, 12))
    api.share(maya, m["id"], [adam["id"]])
    recap = api.recap_published(adam, direct["id"], "2026-11-06", [adam, maya])
    assert recap["state"] == "shared"
    url = api.call(maya, "GET", f"/v1/moments/{photo['id']}").json()["media"]["url"]
    assert api.c.get(url).status_code == 200
    api.call(adam, "POST", f"/v1/moments/{photo['id']}/withdraw", json={})
    assert api.c.get(url).status_code == 404  # same still-unexpired signed URL is now refused
    gone = api.call(maya, "GET", f"/v1/recaps/{recap['id']}").json()
    assert gone["state"] == "invalidated" and gone["plan"] is None and gone["playback"] == []
    award = next(a for a in api.call(adam, "GET", f"/v1/rooms/{direct['id']}/awards").json() if a["milestone"] == "first_shared_day")
    assert award["earned"] and award["sourceRecapId"] is None  # generic keepsake stays, link removed


def test_new_and_rejoined_members_get_no_history(api):
    adam, maya, lina, direct, circle = _setup_family(api)
    old = api.text_moment(adam, circle["id"], "Before Omar joined", at(6))
    api.share(adam, old["id"], [maya["id"], lina["id"]])
    omar = api.signup("Omar")
    api.join(adam, omar, circle["id"])
    api.call(omar, "GET", f"/v1/moments/{old['id']}", expect=404)
    # Lina leaves and is re-invited: her old grant must not come back.
    api.call(adam, "DELETE", f"/v1/rooms/{circle['id']}/members/{lina['id']}")
    api.call(lina, "GET", f"/v1/moments/{old['id']}", expect=404)
    api.join(adam, lina, circle["id"])
    api.call(lina, "GET", f"/v1/moments/{old['id']}", expect=404)
    # A fresh explicit share works.
    new = api.text_moment(adam, circle["id"], "Welcome back", at(7))
    api.share(adam, new["id"], [lina["id"]])
    api.call(lina, "GET", f"/v1/moments/{new['id']}")


def test_reply_inherits_audience_and_confirmation_required(api):
    adam, maya, lina, direct, circle = _setup_family(api)
    d = api.text_moment(adam, direct["id"], "Just us", at(6))
    api.share(adam, d["id"], [maya["id"]])
    reply = api.call(maya, "POST", f"/v1/moments/{d['id']}/replies", json={"text": "Miss you"}).json()
    run_worker()
    assert api.call(lina, "GET", "/v1/notifications").json()["items"] == []
    adam_inbox = api.call(adam, "GET", "/v1/notifications").json()["items"]
    assert [n["resourceId"] for n in adam_inbox] == [reply["id"]]
    # Sharing without the server-issued confirmation token (exact recipients) is refused.
    c = api.text_moment(adam, circle["id"], "Trip photo", at(6))
    api.call(adam, "POST", f"/v1/moments/{c['id']}/share", json={"recipient_ids": [maya["id"]], "confirmation_token": "guess"}, expect=409)
    resolved = api.call(adam, "POST", f"/v1/moments/{c['id']}/audience/resolve", json={"recipient_ids": [maya["id"], lina["id"]]}).json()
    assert resolved["label"] == "Family circle: Lina, Maya" or resolved["label"].startswith("Family circle:")
    # Token for a narrower audience can't be replayed to widen it.
    narrow = api.call(adam, "POST", f"/v1/moments/{c['id']}/audience/resolve", json={"recipient_ids": [maya["id"]]}).json()
    api.call(adam, "POST", f"/v1/moments/{c['id']}/share", json={"recipient_ids": [maya["id"], lina["id"]], "confirmation_token": narrow["confirmationToken"]}, expect=409)
    api.share(adam, c["id"], [maya["id"]])
    # Widening later needs a separate publication, not an edit of this one.
    api.call(adam, "POST", f"/v1/moments/{c['id']}/share", json={"recipient_ids": [maya["id"], lina["id"]], "confirmation_token": resolved["confirmationToken"]}, expect=409)


def test_removed_member_loses_access_immediately(api):
    adam, maya, lina, direct, circle = _setup_family(api)
    c = api.text_moment(adam, circle["id"], "For the family", at(6))
    api.share(adam, c["id"], [maya["id"], lina["id"]])
    api.call(lina, "GET", f"/v1/moments/{c['id']}")
    api.call(adam, "DELETE", f"/v1/rooms/{circle['id']}/members/{lina['id']}")
    api.call(lina, "GET", f"/v1/moments/{c['id']}", expect=404)
    api.call(lina, "GET", f"/v1/rooms/{circle['id']}", expect=404)
