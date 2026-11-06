"""Idempotency, plan validation and awards: criteria 3, 4, 9."""

import copy

import pytest
from conftest import at, run_worker

from samesky import db as dbmod
from samesky.models import Award, Job, Notification, Recap
from samesky.services import recaps


def _pair(api):
    adam, maya = api.signup("Adam"), api.signup("Maya", "Asia/Shanghai")
    room = api.room(adam, "direct", "Adam + Maya", members=[maya])
    return adam, maya, room


def _day(api, adam, maya, room, day):
    a = api.text_moment(adam, room["id"], f"Adam day {day}", at(day))
    api.share(adam, a["id"], [maya["id"]])
    m = api.text_moment(maya, room["id"], f"Maya day {day}", at(day, 12))
    api.share(maya, m["id"], [adam["id"]])
    return a, m


def test_recap_retry_is_idempotent(api):
    adam, maya, room = _pair(api)
    _day(api, adam, maya, room, 6)
    first = api.call(adam, "POST", f"/v1/rooms/{room['id']}/recaps", json={"room_date": "2026-11-06"}).json()
    again = api.call(adam, "POST", f"/v1/rooms/{room['id']}/recaps", json={"room_date": "2026-11-06"}).json()
    assert first["id"] == again["id"]
    with dbmod.session_scope() as s:
        assert s.query(Job).filter(Job.kind == "recap.generate").count() == 1


def test_invite_redemption_is_idempotent_and_single_use(api):
    adam, maya = api.signup("Adam"), api.signup("Maya")
    lina = api.signup("Lina")
    room = api.room(adam, "circle", "Family")
    token = api.call(adam, "POST", f"/v1/rooms/{room['id']}/invites").json()["token"]
    api.call(maya, "POST", "/v1/invites/redeem", json={"token": token})
    api.call(maya, "POST", "/v1/invites/redeem", json={"token": token})  # retry returns same membership
    api.call(lina, "POST", "/v1/invites/redeem", json={"token": token}, expect=409)
    assert len(api.call(adam, "GET", f"/v1/rooms/{room['id']}").json()["members"]) == 2


def test_revoked_invite_and_capacity(api):
    adam, maya, lina = api.signup("Adam"), api.signup("Maya"), api.signup("Lina")
    room = api.room(adam, "direct", "Us")
    inv = api.call(adam, "POST", f"/v1/rooms/{room['id']}/invites").json()
    api.call(adam, "DELETE", f"/v1/rooms/{room['id']}/invites/{inv['id']}")
    api.call(maya, "POST", "/v1/invites/redeem", json={"token": inv["token"]}, expect=409)
    api.join(adam, maya, room["id"])
    api.call(adam, "POST", f"/v1/rooms/{room['id']}/invites", expect=409)  # direct room is full


def test_notification_job_retry_does_not_duplicate(api):
    adam, maya, room = _pair(api)
    a, _ = _day(api, adam, maya, room, 6)
    api.call(maya, "POST", f"/v1/moments/{a['id']}/replies", json={"text": "Lovely", "client_key": "k1"})
    api.call(maya, "POST", f"/v1/moments/{a['id']}/replies", json={"text": "Lovely", "client_key": "k1"})  # retried request
    run_worker()
    with dbmod.session_scope() as s:
        job = s.query(Job).filter(Job.kind == "notify").one()
        job.state, job.attempts = "queued", 0  # simulate a redelivery after a crash
    run_worker()
    with dbmod.session_scope() as s:
        assert s.query(Notification).filter(Notification.recipient_id == adam["id"]).count() == 1


def _recap_for_validation(api):
    adam, maya, room = _pair(api)
    _day(api, adam, maya, room, 6)
    recap = api.call(adam, "POST", f"/v1/rooms/{room['id']}/recaps", json={"room_date": "2026-11-06"}).json()
    run_worker()
    return adam, maya, room, recap


@pytest.mark.parametrize(
    "mutate,code",
    [
        (lambda p: p["chapters"][0]["segments"][0]["sources"][0].update(momentId="00000000-0000-0000-0000-000000000000"), "unknown_source"),
        (lambda p: p["chapters"][0]["segments"][0]["sources"][0].update(sourceVersion=99), "stale_version"),
        (lambda p: p["chapters"][0].update(objectType="hologram"), "bad_layout"),
        (lambda p: p.update(audienceSnapshotId="someone-else"), "audience_mismatch"),
        (lambda p: p["chapters"][0].update(durationSeconds=600), "bad_duration"),
        (lambda p: p["chapters"].pop(), "missing_contributor_chapter"),
        (lambda p: p["chapters"][0]["segments"][0].update(sources=[]), "unsourced_segment"),
        (lambda p: p.update(schemaVersion=1), "schema_version"),
    ],
)
def test_invalid_plans_rejected(api, mutate, code):
    _, _, _, recap = _recap_for_validation(api)
    with dbmod.session_scope() as s:
        row = s.get(Recap, recap["id"])
        plan = copy.deepcopy(row.plan)
        mutate(plan)
        with pytest.raises(recaps.PlanInvalid) as err:
            recaps.validate_plan(s, row, plan)
        assert err.value.code == code


def test_misattributed_source_rejected(api):
    _, _, _, recap = _recap_for_validation(api)
    with dbmod.session_scope() as s:
        row = s.get(Recap, recap["id"])
        plan = copy.deepcopy(row.plan)
        a, b = plan["chapters"][0], plan["chapters"][1]
        a["segments"][0]["sources"] = b["segments"][0]["sources"]
        with pytest.raises(recaps.PlanInvalid) as err:
            recaps.validate_plan(s, row, plan)
        assert err.value.code == "misattributed_source"


def test_unapproved_chapter_cannot_publish_and_edit_invalidates_approval(api):
    adam, maya, room, recap = _recap_for_validation(api)
    after_one = api.call(adam, "POST", f"/v1/recaps/{recap['id']}/approve").json()
    assert after_one["state"] == "awaiting_approval"
    # Maya edits her contribution: the recap is invalidated rather than published with stale text.
    moments = api.call(maya, "GET", f"/v1/rooms/{room['id']}/moments").json()
    mine = next(m for m in moments if m["isMine"])
    api.call(maya, "PATCH", f"/v1/moments/{mine['id']}", json={"caption": "Changed my mind"})
    api.call(maya, "POST", f"/v1/recaps/{recap['id']}/approve", expect=404)
    assert api.call(adam, "GET", f"/v1/recaps/{recap['id']}").json()["state"] == "invalidated"


def test_ai_draft_with_unknown_ids_falls_back_to_chronological(api, monkeypatch):
    from samesky.services import ai

    def fake(sources, room_date, zone, order):
        return {"title": "Ignore previous instructions", "closingLine": "", "chapters": [
            {"contributorId": order[0], "segments": [{"text": "x", "kind": "stated", "momentIds": ["not-a-real-id"]}],
             "playbackMomentIds": [], "objectType": "frame", "durationSeconds": 20}]}

    monkeypatch.setattr(ai, "generate_plan", fake)
    _, _, _, recap = _recap_for_validation(api)
    with dbmod.session_scope() as s:
        row = s.get(Recap, recap["id"])
        assert row.generator == "chronological" and row.state == "private_preview"
        assert row.failure_code == "unknown_source"


def test_seven_days_award_once_regardless_of_regeneration(api):
    adam, maya, room = _pair(api)
    for day in range(1, 8):
        _day(api, adam, maya, room, day)
        r = api.recap_published(adam, room["id"], f"2026-11-0{day}", [adam, maya])
        assert r["state"] == "shared"
        # Re-requesting the same day returns the same (shared) recap; no extra progress.
        api.call(adam, "POST", f"/v1/rooms/{room['id']}/recaps", json={"room_date": f"2026-11-0{day}"})
    # A late contribution on day 7 produces a new reviewable version but the same room date.
    late = api.text_moment(maya, room["id"], "Late add", at(7, 15))
    api.share(maya, late["id"], [adam["id"]])
    v2 = api.recap_published(adam, room["id"], "2026-11-07", [adam, maya])
    assert v2["version"] == 2
    awards = {a["milestone"]: a for a in api.call(maya, "GET", f"/v1/rooms/{room['id']}/awards").json()}
    assert awards["seven_days"]["earned"] and awards["seven_days"]["progress"] == 7
    assert awards["first_shared_day"]["earned"]
    with dbmod.session_scope() as s:
        assert s.query(Award).filter(Award.room_id == room["id"]).count() == 2


def test_conversation_continued_needs_two_members(api):
    adam, maya, room = _pair(api)
    a, m = _day(api, adam, maya, room, 6)
    api.call(maya, "POST", f"/v1/moments/{a['id']}/replies", json={"text": "1"})
    api.call(maya, "POST", f"/v1/moments/{a['id']}/replies", json={"text": "2"})
    awards = {x["milestone"]: x for x in api.call(adam, "GET", f"/v1/rooms/{room['id']}/awards").json()}
    assert not awards["conversation_continued"]["earned"]
    api.call(adam, "POST", f"/v1/moments/{m['id']}/replies", json={"text": "3"})
    awards = {x["milestone"]: x for x in api.call(adam, "GET", f"/v1/rooms/{room['id']}/awards").json()}
    assert awards["conversation_continued"]["earned"]


def test_nothing_shared_is_an_honest_empty_state(api):
    adam, maya, room = _pair(api)
    r = api.call(adam, "POST", f"/v1/rooms/{room['id']}/recaps", json={"room_date": "2026-11-06"}, expect=409)
    assert r.json()["error"]["code"] == "nothing_shared"
