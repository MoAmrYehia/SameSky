"""Short, hand-typed invite codes (used by the native Quest app's keypad and the companion)."""

from __future__ import annotations

from datetime import timedelta

from sqlalchemy import update

from samesky import db as dbmod
from samesky.models import Invitation, utcnow
from samesky.security import INVITE_CODE_ALPHABET, normalize_invite_code


def _invite(api, owner, room_id):
    return api.call(owner, "POST", f"/v1/rooms/{room_id}/invites").json()


def test_invitation_carries_a_short_code_without_look_alike_characters(api):
    adam = api.signup("Adam")
    room = api.room(adam)
    inv = _invite(api, adam, room["id"])
    code = inv["code"]
    assert len(code) == 9 and code[4] == "-"
    assert all(ch in INVITE_CODE_ALPHABET for ch in code.replace("-", ""))
    assert not set(code) & set("ILO01")
    # The link token still works and is unrelated to the code.
    assert inv["token"] and inv["token"].replace("-", "") != code.replace("-", "")


def test_code_redeems_once_typed_loosely(api):
    adam, maya, sam = api.signup("Adam"), api.signup("Maya"), api.signup("Sam")
    room = api.room(adam, kind="circle", name="Family")
    code = _invite(api, adam, room["id"])["code"]
    loose = " " + code.lower().replace("-", " ") + " "
    joined = api.call(maya, "POST", "/v1/invites/redeem-code", json={"code": loose}).json()
    assert joined["id"] == room["id"]
    # Single use: someone else holding the same code is refused, the redeemer's retry is idempotent.
    r = api.call(sam, "POST", "/v1/invites/redeem-code", json={"code": code}, expect=409)
    assert r.json()["error"]["code"] == "invite_used"
    again = api.call(maya, "POST", "/v1/invites/redeem-code", json={"code": code}).json()
    assert again["id"] == room["id"]


def test_expired_revoked_and_full_codes_explain_themselves(api):
    adam, maya, sam = api.signup("Adam"), api.signup("Maya"), api.signup("Sam")
    room = api.room(adam)  # direct: two people

    revoked = _invite(api, adam, room["id"])
    api.call(adam, "DELETE", f"/v1/rooms/{room['id']}/invites/{revoked['id']}")
    r = api.call(maya, "POST", "/v1/invites/redeem-code", json={"code": revoked["code"]}, expect=409)
    assert r.json()["error"]["code"] == "invite_revoked"

    expired = _invite(api, adam, room["id"])
    with dbmod.session_scope() as s:
        s.execute(update(Invitation).where(Invitation.id == expired["id"]).values(expires_at=utcnow() - timedelta(minutes=1)))
    r = api.call(maya, "POST", "/v1/invites/redeem-code", json={"code": expired["code"]}, expect=409)
    assert r.json()["error"]["code"] == "invite_expired"

    first, second = _invite(api, adam, room["id"]), _invite(api, adam, room["id"])
    api.call(maya, "POST", "/v1/invites/redeem-code", json={"code": first["code"]})
    r = api.call(sam, "POST", "/v1/invites/redeem-code", json={"code": second["code"]}, expect=409)
    assert r.json()["error"]["code"] == "room_full"
    # None of these were guesses, so none count against the attempt budget.
    api.call(sam, "POST", "/v1/invites/redeem-code", json={"code": "ZZZZ-ZZZZ"}, expect=404)


def test_wrong_codes_are_rate_limited_per_account(api):
    adam, mallory, maya = api.signup("Adam"), api.signup("Mallory"), api.signup("Maya")
    room = api.room(adam)
    real = _invite(api, adam, room["id"])["code"]
    for i in range(8):
        r = api.call(mallory, "POST", "/v1/invites/redeem-code", json={"code": f"AAAA-AAA{INVITE_CODE_ALPHABET[i]}"}, expect=404)
        assert r.json()["error"]["code"] == "invite_code_unknown"
    # Locked out, even with the right code; other people are unaffected.
    r = api.call(mallory, "POST", "/v1/invites/redeem-code", json={"code": real}, expect=429)
    assert r.json()["error"]["code"] == "too_many_attempts"
    assert api.call(maya, "POST", "/v1/invites/redeem-code", json={"code": real}).json()["id"] == room["id"]


def test_malformed_codes_are_rejected_and_counted(api):
    adam = api.signup("Adam")
    assert normalize_invite_code("abcd-efgh") == "ABCDEFGH"
    assert normalize_invite_code("ABCD-EFG1") is None  # 1 is not in the alphabet
    assert normalize_invite_code("ABC") is None
    r = api.call(adam, "POST", "/v1/invites/redeem-code", json={"code": "OOOO-0000"}, expect=404)
    assert r.json()["error"]["code"] == "invite_code_unknown"


def test_only_an_hmac_of_the_code_is_stored(api):
    adam = api.signup("Adam")
    room = api.room(adam)
    inv = _invite(api, adam, room["id"])
    with dbmod.session_scope() as s:
        stored = s.get(Invitation, inv["id"])
        assert stored.code_hash and inv["code"].replace("-", "") not in stored.code_hash
        assert len(stored.code_hash) == 64
