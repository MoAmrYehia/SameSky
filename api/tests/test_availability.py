"""Time-zone-aware availability and call opportunities: criteria 5, 6, 7, 14."""

from datetime import UTC, datetime, timedelta
from zoneinfo import ZoneInfo

from conftest import run_worker

from samesky import db as dbmod
from samesky.models import CallOpportunity, FreeNow, Job, Notification
from samesky.services.availability_engine import (
    ParticipantSchedule,
    WeeklyRule,
    common_overlaps,
    expand_weekly,
    resolve_wall_time,
)

FRI = 4  # 6 November 2026 is a Friday
NOW = datetime(2026, 11, 6, 6, 0, tzinfo=UTC)


def test_brief_fixture_dubai_shanghai_same_utc_hour():
    adam = ParticipantSchedule("adam", "Asia/Dubai", [WeeklyRule(FRI, "17:00", "18:00")])
    maya = ParticipantSchedule("maya", "Asia/Shanghai", [WeeklyRule(FRI, "21:00", "22:00")])
    overlaps = common_overlaps([adam, maya], NOW, days=1)
    assert overlaps == [(datetime(2026, 11, 6, 13, 0, tzinfo=UTC), datetime(2026, 11, 6, 14, 0, tzinfo=UTC))]


def test_different_local_dates_and_overnight_windows():
    # Los Angeles Thursday 22:00-01:00 (overnight) vs Dubai Friday 10:00-12:00.
    la = ParticipantSchedule("la", "America/Los_Angeles", [WeeklyRule(3, "22:00", "01:00")])
    dxb = ParticipantSchedule("dxb", "Asia/Dubai", [WeeklyRule(FRI, "10:00", "12:00")])
    overlaps = common_overlaps([la, dxb], datetime(2026, 11, 5, 0, 0, tzinfo=UTC), days=2)
    # LA Thu 22:00 PST = Fri 06:00 UTC = Fri 10:00 Dubai; overlap is 06:00-08:00 UTC... capped by LA's 01:00 (09:00 UTC).
    assert overlaps == [(datetime(2026, 11, 6, 6, 0, tzinfo=UTC), datetime(2026, 11, 6, 8, 0, tzinfo=UTC))]


def test_dst_gap_resolves_to_transition_instant():
    ny = ZoneInfo("America/New_York")
    # 8 March 2026: clocks jump 02:00 -> 03:00. 02:30 does not exist.
    resolved = resolve_wall_time(datetime(2026, 3, 8, 2, 30), ny, "start")
    assert resolved == datetime(2026, 3, 8, 7, 0, tzinfo=UTC)
    # A window entirely inside the gap covers no time.
    window = (datetime(2026, 3, 7, tzinfo=UTC), datetime(2026, 3, 10, tzinfo=UTC))
    assert expand_weekly([WeeklyRule(6, "02:10", "02:50")], "America/New_York", window) == []


def test_dst_fold_keeps_full_window():
    ny = ZoneInfo("America/New_York")
    # 1 November 2026: 01:00-02:00 happens twice.
    start = resolve_wall_time(datetime(2026, 11, 1, 1, 30), ny, "start")
    end = resolve_wall_time(datetime(2026, 11, 1, 1, 30), ny, "end")
    assert start == datetime(2026, 11, 1, 5, 30, tzinfo=UTC)  # first occurrence (EDT)
    assert end == datetime(2026, 11, 1, 6, 30, tzinfo=UTC)  # second occurrence (EST)
    window = (datetime(2026, 10, 31, tzinfo=UTC), datetime(2026, 11, 3, tzinfo=UTC))
    intervals = expand_weekly([WeeklyRule(6, "00:30", "01:30")], "America/New_York", window)
    assert intervals == [(datetime(2026, 11, 1, 4, 30, tzinfo=UTC), datetime(2026, 11, 1, 6, 30, tzinfo=UTC))]


def test_quiet_hours_and_busy_overrides_remove_time():
    adam = ParticipantSchedule("adam", "Asia/Dubai", [WeeklyRule(FRI, "17:00", "23:00")], quiet_start="22:00", quiet_end="07:00")
    maya = ParticipantSchedule(
        "maya", "Asia/Shanghai", [WeeklyRule(FRI, "21:00", "23:59")],
        busy=[(datetime(2026, 11, 6, 13, 30, tzinfo=UTC), datetime(2026, 11, 6, 14, 0, tzinfo=UTC))],
    )
    overlaps = common_overlaps([adam, maya], NOW, days=1)
    # Maya: 13:00-15:59 UTC minus busy 13:30-14:00; Adam: 13:00-18:00 UTC minus quiet from 18:00.
    assert overlaps == [
        (datetime(2026, 11, 6, 13, 0, tzinfo=UTC), datetime(2026, 11, 6, 13, 30, tzinfo=UTC)),
        (datetime(2026, 11, 6, 14, 0, tzinfo=UTC), datetime(2026, 11, 6, 15, 59, tzinfo=UTC)),
    ]


def test_minimum_overlap_length():
    a = ParticipantSchedule("a", "Asia/Dubai", [WeeklyRule(FRI, "17:00", "17:10")])
    b = ParticipantSchedule("b", "Asia/Dubai", [WeeklyRule(FRI, "17:00", "18:00")])
    assert common_overlaps([a, b], NOW, days=1) == []


def _pair(api):
    adam, maya = api.signup("Adam", "Asia/Dubai"), api.signup("Maya", "Asia/Shanghai")
    room = api.room(adam, "direct", "Adam + Maya", members=[maya])
    return adam, maya, room


def test_travel_zone_change_recomputes(api):
    adam, maya, room = _pair(api)
    api.call(adam, "PUT", f"/v1/rooms/{room['id']}/availability/rules", json={"rules": [{"weekday": d, "start": "17:00", "end": "18:00"} for d in range(7)]})
    api.call(maya, "PUT", f"/v1/rooms/{room['id']}/availability/rules", json={"rules": [{"weekday": d, "start": "21:00", "end": "22:00"} for d in range(7)]})
    res = api.call(adam, "POST", f"/v1/rooms/{room['id']}/opportunities", json={"participant_ids": [maya["id"]]}).json()
    assert res["candidates"] and res["candidates"][0]["minutes"] == 60
    assert res["candidates"][0]["message"] == "Your chosen schedules overlap for 60 minutes."
    # Adam travels to London: his 17:00-18:00 is now 17:00-18:00 GMT, which no longer meets 21:00 Shanghai.
    api.call(adam, "PATCH", "/v1/me", json={"time_zone": "Europe/London"})
    res = api.call(adam, "POST", f"/v1/rooms/{room['id']}/opportunities", json={"participant_ids": [maya["id"]]}).json()
    assert res["candidates"] == []


def test_free_now_expires_and_presence_is_not_availability(api):
    adam, maya, room = _pair(api)
    res = api.call(adam, "POST", f"/v1/rooms/{room['id']}/opportunities", json={"participant_ids": [maya["id"]]}).json()
    assert res["allMarkedFreeNow"] is False and res["statusMessage"] is None
    api.call(adam, "POST", f"/v1/rooms/{room['id']}/free-now")
    res = api.call(adam, "POST", f"/v1/rooms/{room['id']}/opportunities", json={"participant_ids": [maya["id"]]}).json()
    assert res["allMarkedFreeNow"] is False  # only one person is marked free
    api.call(maya, "POST", f"/v1/rooms/{room['id']}/free-now")
    res = api.call(adam, "POST", f"/v1/rooms/{room['id']}/opportunities", json={"participant_ids": [maya["id"]]}).json()
    assert res["statusMessage"] == "You're both marked free now. Want to connect?"
    with dbmod.session_scope() as s:
        for row in s.query(FreeNow):
            row.expires_at = datetime.now(UTC) - timedelta(seconds=1)
    res = api.call(adam, "POST", f"/v1/rooms/{room['id']}/opportunities", json={"participant_ids": [maya["id"]]}).json()
    assert res["allMarkedFreeNow"] is False


def test_free_now_is_room_scoped(api):
    adam, maya, room = _pair(api)
    circle = api.room(adam, "circle", "Family", members=[maya])
    api.call(adam, "POST", f"/v1/rooms/{room['id']}/free-now")
    api.call(maya, "POST", f"/v1/rooms/{room['id']}/free-now")
    res = api.call(adam, "POST", f"/v1/rooms/{circle['id']}/opportunities", json={"participant_ids": [maya["id"]]}).json()
    assert res["allMarkedFreeNow"] is False


def test_group_opportunity_names_only_selected_and_needs_everyone(api):
    adam, maya = api.signup("Adam", "Asia/Dubai"), api.signup("Maya", "Asia/Shanghai")
    lina, omar = api.signup("Lina", "Europe/London"), api.signup("Omar", "Asia/Dubai")
    circle = api.room(adam, "circle", "Family", members=[maya, lina, omar])
    for u in (adam, maya, lina, omar):
        api.call(u, "POST", f"/v1/rooms/{circle['id']}/free-now")
    res = api.call(adam, "POST", f"/v1/rooms/{circle['id']}/opportunities", json={"participant_ids": [maya["id"], lina["id"]]}).json()
    names = {p["displayName"] for p in res["participants"]}
    assert names == {"Adam", "Maya", "Lina"} and "Omar" not in str(res)
    assert res["statusMessage"] == "Maya and Lina and you are all marked free now. Want to connect?" or "all marked free now" in res["statusMessage"]
    api.call(lina, "DELETE", f"/v1/rooms/{circle['id']}/free-now")
    res = api.call(adam, "POST", f"/v1/rooms/{circle['id']}/opportunities", json={"participant_ids": [maya["id"], lina["id"]]}).json()
    assert res["allMarkedFreeNow"] is False


def _setup_suggestion(api):
    adam, maya, room = _pair(api)
    every_day = {"rules": [{"weekday": d, "start": "00:00", "end": "00:00"} for d in range(7)]}  # 24h windows
    api.call(adam, "PUT", f"/v1/rooms/{room['id']}/availability/rules", json=every_day)
    api.call(maya, "PUT", f"/v1/rooms/{room['id']}/availability/rules", json=every_day)
    res = api.call(adam, "POST", f"/v1/rooms/{room['id']}/opportunities", json={"participant_ids": [maya["id"]]}).json()
    start = res["candidates"][0]["startUtc"]
    opp = api.call(adam, "POST", f"/v1/rooms/{room['id']}/opportunities/suggest", json={"participant_ids": [maya["id"]], "start_utc": start}).json()
    return adam, maya, room, opp


def test_suggestion_delivered_once_and_only_to_participants(api):
    adam, maya, room, opp = _setup_suggestion(api)
    run_worker()
    run_worker()
    items = api.call(maya, "GET", "/v1/notifications").json()["items"]
    assert [i["kind"] for i in items] == ["call_opportunity"]
    assert api.call(adam, "GET", "/v1/notifications").json()["items"] == []  # the suggester isn't notified
    connect = api.call(maya, "POST", f"/v1/opportunities/{opp['id']}/respond", json={"action": "connect"}).json()
    assert connect["builtInCalling"] is False  # never simulates a call


def test_busy_change_cancels_pending_delivery(api):
    adam, maya, room, opp = _setup_suggestion(api)
    start = datetime.fromisoformat(opp["startUtc"])
    api.call(maya, "POST", "/v1/availability/exceptions", json={"kind": "busy", "start": (start - timedelta(hours=1)).isoformat(), "end": (start + timedelta(days=8)).isoformat()})
    run_worker()
    assert api.call(maya, "GET", "/v1/notifications").json()["items"] == []
    with dbmod.session_scope() as s:
        assert s.get(CallOpportunity, opp["id"]).state == "cancelled"


def test_expired_opportunity_not_sent_after_restart(api):
    adam, maya, room, opp = _setup_suggestion(api)
    # Simulate a crashed worker that left the job leased, then restart after the overlap ended.
    with dbmod.session_scope() as s:
        job = s.query(Job).filter(Job.kind == "notify").one()
        job.state, job.lease_until = "running", datetime.now(UTC) - timedelta(minutes=1)
        o = s.get(CallOpportunity, opp["id"])
        o.end_at = datetime.now(UTC) - timedelta(minutes=5)
        job.expires_at = o.end_at
    run_worker()
    with dbmod.session_scope() as s:
        assert s.query(Notification).count() == 0
        assert s.query(Job).filter(Job.kind == "notify").one().state == "expired"


def test_daily_suggestion_limit(api):
    adam, maya, room, opp = _setup_suggestion(api)
    run_worker()
    later = (datetime.fromisoformat(opp["startUtc"]) + timedelta(hours=1)).isoformat()
    second = api.call(adam, "POST", f"/v1/rooms/{room['id']}/opportunities/suggest", json={"participant_ids": [maya["id"]], "start_utc": later}).json()
    run_worker()
    # Spacing rule (4 hours between suggestions) suppresses the second one.
    items = api.call(maya, "GET", "/v1/notifications").json()["items"]
    assert len(items) == 1 and second["id"] != opp["id"]


def test_call_link_scheme_validation(api):
    adam, maya, room = _pair(api)
    api.call(adam, "PATCH", "/v1/me", json={"call_link": "javascript:alert(1)"}, expect=422)
    api.call(adam, "PATCH", "/v1/me", json={"call_link": "https://meet.example.test/abc"})
