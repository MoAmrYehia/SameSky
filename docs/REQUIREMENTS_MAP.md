# Requirements map

Legend: ✅ implemented and automatically tested · 🟢 implemented, exercised manually (desktop/emulator) ·
🟡 implemented, unverified on target hardware/service · ⬜ not implemented (P1/later).

## Competition

| Requirement | Where | Status |
|---|---|---|
| Runs on Quest 3/3S, not glasses-only | `web/room.html` (IWSDK/WebXR, immersive-vr, hand tracking) | 🟡 headset run pending |
| Controller-free navigation throughout | Pinch select/grab, poke dock; no buttons, keyboard, gaze or voice needed | 🟢 desktop / 🟡 hands |
| Seated, quick start, short rewarding session | Seated table layout; first meaningful moment = the glowing cup | 🟢 |
| ≥ 60 fps on Quest | `?debug=1` HUD shows fps/draw calls; ~100 draw calls, canvas UI, no shadows | 🟡 measure on device |
| Launchable build URL + testing instructions | `docs/DEPLOYMENT.md`, `docs/JUDGE_GUIDE.md` | 🟡 hosting pending |
| < 3 min video of actual app | `docs/DEMO_SCRIPT.md` | ⬜ to record |

## Brief P0

| Requirement | Where | Status |
|---|---|---|
| One attractive seated VR room with table and memory objects | `src/xr/env.ts`, `objects.ts`, `main.ts` | 🟢 |
| Direct rooms (2) and circles (≤ 6), revocable single-use invites | `services/rooms.py`; tests `test_recaps_awards.py` | ✅ |
| Explicit 1:1 / 1:many recipients enforced across media, recaps, replies, notifications | `services/access.py`, `moments.py`, `recaps.py`, `notifications.py`; `test_privacy.py` | ✅ |
| Photos, clips, voice notes, captions with sharing controls | `services/media.py`, companion Share | ✅ / 🟢 |
| File imports from other devices; browser camera/mic capture | Share tabs; `test_persistence_media.py` | ✅ files / 🟡 real capture |
| Personalized avatars, two-person live co-presence, synced playback | `shared/avatar.ts`, `xr/avatar3d.ts`, `realtime.py`, `xr/live.ts`; `test_realtime.py`, smoke | ✅ protocol / 🟡 two headsets |
| AI-assisted recap from approved sources | `services/ai.py`, `recaps.py`; mocked draft + fallback tests | ✅ validation / 🟡 live model call |
| Hand-operated playback, reactions, object placement, persistent replies | XR dock/screen/objects; `test_persistence_media.py` | ✅ API / 🟢 room |
| Daily recap archive and milestone shelf | Keepsakes view, XR shelf, `services/awards.py` | ✅ |
| Declared availability, Free now, time-zone overlap | `availability_engine.py`, `availability.py`, `tz.ts`; tests in both languages | ✅ |
| In-app inbox, durable delivery worker, opt-in Web Push | `notifications.py`, `worker.py`, `push.ts`, `public/sw.js` | ✅ inbox/worker / 🟡 real push |
| Judge-friendly sample without registration or AI credentials | `shared/sample.ts`, `SampleSource` | 🟢 |
| Phone/desktop companion over the same rooms and data | `web/src/companion` + `LiveSource` | 🟢 |

## Brief §15 acceptance tests

| # | Test(s) |
|---|---|
| 1 | `test_member_cannot_access_private_or_other_room_content`, `test_preview_and_notifications_stay_with_their_audience` |
| 2 | `test_withdrawal_blocks_reads_and_stale_generation`, `test_withdrawal_after_publication_invalidates_recap_and_media`, `test_removed_member_loses_access_immediately` |
| 3 | `test_recap_retry_is_idempotent`, `test_invite_redemption_is_idempotent_and_single_use`, `test_notification_job_retry_does_not_duplicate`, `test_seven_days_award_once_regardless_of_regeneration` |
| 4 | `test_invalid_plans_rejected` (8 cases), `test_misattributed_source_rejected`, `test_unapproved_chapter_cannot_publish_and_edit_invalidates_approval`, `test_ai_draft_with_unknown_ids_falls_back_to_chronological` |
| 5 | `test_brief_fixture_dubai_shanghai_same_utc_hour`, `test_different_local_dates_and_overnight_windows`, `test_dst_gap_resolves_to_transition_instant`, `test_dst_fold_keeps_full_window`, `test_quiet_hours_and_busy_overrides_remove_time`, `test_travel_zone_change_recomputes` (+ `web/src/shared/tz.test.ts`) |
| 6 | `test_free_now_expires_and_presence_is_not_availability`, `test_free_now_is_room_scoped` |
| 7 | `test_expired_opportunity_not_sent_after_restart`, `test_busy_change_cancels_pending_delivery` |
| 8 | `test_inbox_works_without_push` — real push round trip 🟡 pending |
| 9 | `test_seven_days_award_once_regardless_of_regeneration` |
| 10 | `test_replies_and_placements_survive_restart` |
| 11 | `test_direct_moment_never_reaches_circle`, `test_recap_only_uses_sources_visible_to_whole_audience` |
| 12 | `test_new_and_rejoined_members_get_no_history`, `test_removed_member_cannot_join_or_command` |
| 13 | `test_reply_inherits_audience_and_confirmation_required`, `test_recap_only_uses_sources_visible_to_whole_audience` |
| 14 | `test_group_opportunity_names_only_selected_and_needs_everyone`, `test_suggestion_delivered_once_and_only_to_participants` |
| 15 | `test_two_sessions_sync_and_avatars`, `test_outsider_and_bad_token_rejected` (+ smoke test) — two headsets 🟡 |
| 16 | `test_unsupported_codec_gets_useful_message`, `test_unprocessed_media_cannot_be_published`, `test_filename_is_not_trusted`, `test_phone_photo_orientation_and_location_stripped`; camera denial handled in `Share.tsx` 🟢 |
| 17 | `test_phone_photo_orientation_and_location_stripped` + `test_action_camera_clip_processed_to_playback_profile` — Quest playback 🟡 |
| 18 | Companion stops tracks on stop/unmount (`useMediaStream`); recorded messages labelled in companion and room 🟢; live streaming ⬜ (P1) |

## P1 (not implemented)

More than two live avatars · WebRTC audio/video and TURN · live phone/webcam streaming · 360°/stereo footage ·
mixed-reality table placement · English/Arabic localization · weekly stories and rituals.
