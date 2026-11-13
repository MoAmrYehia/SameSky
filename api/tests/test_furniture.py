"""The furniture marketplace: the catalogue, and a room's furnishing choices shared by its members."""

from __future__ import annotations

from pathlib import Path

from samesky.services import furniture

REPO = Path(__file__).resolve().parents[2]


def _decor(api, user, room_id, expect=200):
    return api.call(user, "GET", f"/v1/rooms/{room_id}/decor", expect=expect)


def _furnish(api, user, room_id, choices, expect=200, **extra):
    return api.call(user, "PUT", f"/v1/rooms/{room_id}/decor", expect=expect, json={"choices": choices, **extra})


def test_catalogue_is_consistent_and_sells_nothing():
    cat = furniture.CATALOG
    slot_ids = [s["id"] for s in cat["slots"]]
    assert len(slot_ids) == len(set(slot_ids)) and {"table", "chairs", "rug", "lamp", "plant", "walls", "floor"} <= set(slot_ids)
    for slot in cat["slots"]:
        item_ids = [i["id"] for i in slot["items"]]
        assert item_ids and len(item_ids) == len(set(item_ids)), slot["id"]
        for item in slot["items"]:
            assert item["name"] and item["blurb"]
            assert all(f in cat["finishes"] for f in item["finishes"]), (slot["id"], item["id"])
            # no prices, currencies or ownership anywhere: every piece is included
            assert not {"price", "currency", "cost", "sku"} & set(item)
    for finish in cat["finishes"].values():
        assert finish["name"] and finish["swatch"].startswith("#") and len(finish["swatch"]) == 7
    # every ready-made look is a complete, valid room
    for look in cat["looks"]:
        assert set(look["choices"]) == set(slot_ids), look["id"]
        assert furniture.resolve(look["choices"]) == look["choices"], look["id"]
    assert cat["looks"][0]["choices"] == furniture.defaults()


def test_native_app_bundles_the_same_catalogue():
    # the headset app draws the pieces, so its bundled copy must be this catalogue (scripts/native/build_native_assets.py)
    bundled = REPO / "native" / "quest" / "Assets" / "SameSky" / "Resources" / "furniture-catalog.json"
    assert bundled.exists(), "run scripts/native/build_native_assets.py"
    assert bundled.read_bytes() == furniture.CATALOG_PATH.read_bytes()


def test_catalogue_needs_an_account(client, api):
    assert client.get("/v1/furniture/catalog").status_code in (401, 403)
    adam = api.signup("Adam")
    cat = api.call(adam, "GET", "/v1/furniture/catalog").json()
    assert cat["version"] == furniture.CATALOG["version"] and cat["slots"]


def test_a_new_room_has_the_default_furniture(api):
    adam = api.signup("Adam")
    room = api.room(adam)
    d = _decor(api, adam, room["id"]).json()
    assert d["roomId"] == room["id"] and d["version"] == 0 and d["updatedBy"] is None
    assert d["choices"] == furniture.defaults()
    assert d["choices"]["table"] == {"item": "pedestal", "finish": "walnut"}
    assert d["choices"]["plant"] == {"item": "leafy", "finish": None}


def test_either_member_furnishes_the_room_they_share(api):
    adam, maya = api.signup("Adam"), api.signup("Maya")
    room = api.room(adam, members=[maya])
    d = _furnish(api, maya, room["id"], {"table": {"item": "tripod", "finish": "oak"}, "rug": {"item": "none"}}).json()
    assert d["version"] == 1 and d["updatedBy"] == maya["id"] and d["updatedAt"]
    assert d["choices"]["table"] == {"item": "tripod", "finish": "oak"}
    assert d["choices"]["rug"] == {"item": "none", "finish": None}
    # slots that weren't mentioned stay as they were, and the other member sees the same room
    assert d["choices"]["chairs"] == furniture.defaults()["chairs"]
    seen = _decor(api, adam, room["id"]).json()
    assert seen["choices"] == d["choices"] and seen["version"] == 1
    # a finish left out means the piece's first finish
    d2 = _furnish(api, adam, room["id"], {"lamp": {"item": "globe"}}).json()
    assert d2["version"] == 2 and d2["choices"]["lamp"] == {"item": "globe", "finish": "brass"}
    assert d2["choices"]["table"] == {"item": "tripod", "finish": "oak"}


def test_only_members_see_or_change_a_room(api):
    adam, maya, sam = api.signup("Adam"), api.signup("Maya"), api.signup("Sam")
    room = api.room(adam, members=[maya])
    other = api.room(sam, name="Sam's room")
    assert _decor(api, sam, room["id"], expect=404).json()["error"]["code"] == "not_found"
    assert _furnish(api, sam, room["id"], {"walls": {"item": "plaster", "finish": "sage"}}, expect=404).json()["error"]["code"] == "not_found"
    # each room has its own furniture
    _furnish(api, sam, other["id"], {"walls": {"item": "plaster", "finish": "sage"}})
    assert _decor(api, adam, room["id"]).json()["choices"]["walls"]["finish"] == "ivory"
    assert _decor(api, sam, other["id"]).json()["choices"]["walls"]["finish"] == "sage"
    # someone removed from the room loses access with the membership
    api.call(adam, "DELETE", f"/v1/rooms/{room['id']}/members/{maya['id']}")
    _decor(api, maya, room["id"], expect=404)


def test_unknown_pieces_and_finishes_are_refused_and_change_nothing(api):
    adam = api.signup("Adam")
    room = api.room(adam)
    cases = [
        ({"ceiling": {"item": "plaster"}}, "unknown_slot"),
        ({"table": {"item": "throne"}}, "unknown_item"),
        ({"table": {"item": "pedestal", "finish": "cognac"}}, "unknown_finish"),   # a chair finish, not a table one
        ({"rug": {"item": "none", "finish": "sage"}}, "unknown_finish"),           # nothing to finish
    ]
    for choices, code in cases:
        r = _furnish(api, adam, room["id"], choices, expect=422)
        assert r.json()["error"]["code"] == code, choices
    # one bad slot refuses the whole change
    _furnish(api, adam, room["id"], {"walls": {"item": "plaster", "finish": "mist"}, "table": {"item": "throne"}}, expect=422)
    d = _decor(api, adam, room["id"]).json()
    assert d["version"] == 0 and d["choices"] == furniture.defaults()
    # malformed bodies are reported by field, never echoed
    r = api.call(adam, "PUT", f"/v1/rooms/{room['id']}/decor", expect=422, json={"choices": {"table": {"finish": "oak"}}})
    assert r.json()["error"]["code"] == "invalid_request"


def test_two_people_changing_the_room_at_once(api):
    adam, maya = api.signup("Adam"), api.signup("Maya")
    room = api.room(adam, members=[maya])
    first = _furnish(api, adam, room["id"], {"chairs": {"item": "tub", "finish": "sage"}}, expected_version=0).json()
    assert first["version"] == 1
    # Maya still had version 0 in front of her
    r = _furnish(api, maya, room["id"], {"chairs": {"item": "tub", "finish": "cognac"}}, expect=409, expected_version=0)
    assert r.json()["error"]["code"] == "version_conflict"
    assert _decor(api, maya, room["id"]).json()["choices"]["chairs"]["finish"] == "sage"
    again = _furnish(api, maya, room["id"], {"chairs": {"item": "tub", "finish": "cognac"}}, expected_version=1).json()
    assert again["version"] == 2 and again["choices"]["chairs"]["finish"] == "cognac"


def test_a_retired_piece_falls_back_to_the_default():
    stored = {"table": {"item": "discontinued", "finish": "walnut"}, "lamp": {"item": "globe", "finish": "neon"}, "attic": {"item": "x"}, "rug": "nonsense",
              "walls": {"item": "plaster", "finish": "rose"}}
    resolved = furniture.resolve(stored)
    assert resolved["table"] == furniture.defaults()["table"]
    assert resolved["lamp"] == furniture.defaults()["lamp"]
    assert resolved["rug"] == furniture.defaults()["rug"]
    assert resolved["walls"] == {"item": "plaster", "finish": "rose"}
    assert "attic" not in resolved
