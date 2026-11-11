# Requirements map

Legend: ✅ implemented and automatically tested · 🟢 implemented, exercised manually (desktop/emulator) ·
🟡 implemented, unverified on target hardware/service · ⬜ not implemented (P1/later).

## Competition

| Requirement | Where | Status |
|---|---|---|
| Runs on Quest 3/3S, not glasses-only | `web/room.html` (IWSDK/WebXR, immersive-vr, hand tracking) | 🟡 headset run pending |
| Controller-free navigation throughout | Pinch select/grab, poke dock; no buttons, keyboard, gaze or voice needed | 🟢 desktop / 🟡 hands |
| Seated, quick start, short rewarding session | Seated table layout; first meaningful moment = the glowing cup | 🟢 |
| ≥ 60 fps on Quest | `?debug=1` HUD shows fps/draw calls/triangles; 95–101 draw calls and 123k–136k triangles incl. one bounded soft shadow light, canvas UI | 🟡 measure on device |
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

## Visual enhancement instructions (6 November 2026)

See [VISUAL_QA.md](VISUAL_QA.md) for captures, comparisons and remaining differences, and
[ASSET_MANIFEST.md](ASSET_MANIFEST.md) for provenance.

| Instruction | Where | Status |
|---|---|---|
| Photographic sample media with documented provenance; stop overwriting art with the Pillow generator | `scripts/assets/build_sample_media.py`, `web/public/assets/sample/`, `docs/asset-provenance.json`; Pillow generator moved to `scripts/fixtures/` (git-ignored output) | ✅ |
| Authored, modelled avatar (nose, eyelids, brows, mouth, hair, clothing, hands); compatible `Avatar3D` interface | `scripts/assets/sculpt_characters.py` → `characters.glb`; `src/shared/character.ts`; `src/xr/avatar3d.ts` (root, head, hand anchors, setPresence, setPose, playGesture, update, dispose) | 🟢 desktop |
| Portraits from the same character, outfit, materials and light rig | `src/shared/portrait.ts`, `Avatar` in `src/companion/ui.tsx` | 🟢 |
| Blink, breathing, rest pose and gestures separate from tracking; tracked hands win | `CharacterModel.update`, `Avatar3D.update` | 🟢 desktop / 🟡 tracked input |
| Room composition: screen left, partner right, cup label foreground, compact unified dock; nothing clipped | `src/xr/main.ts` (`framingFor`, `placeAtNdc`, `safeArea`), `window.__sameskySafeArea` | ✅ checked by `capture.mjs` at 1672×941, 1440×900, 1024×768, 390×844 |
| Headset: table-space anchors at seated reach; the head pose is never moved | `applyXrLayout`, `recenter` | 🟡 headset run pending (`?preview=xr` approximation only) |
| CC0 furniture, PBR textures, projected skyline plate, contact shading, one restrained shadow light | `src/xr/env.ts`, `web/scripts/optimize-assets.mjs` | 🟢 |
| sRGB vs data textures, PMREM IBL, tone mapping, unlit UI, disposal | `IBLTexture` + `NeutralToneMapping` in `main.ts`; `toneMapped:false` panels; `dispose()` on env, objects, avatar, panels | 🟢 |
| Memoized caption/label redraws; allocation-free frame loop | `drawCaption`, `FittedPill`/`FittedCard`, `RoomSystem.update`, `CharacterModel.update` | 🟢 code review |
| Locally hosted font; deterministic light and explicit dark themes | `@fontsource-variable/figtree`; `samesky.theme` | ✅ |
| Our day, Share (populated, filmstrip trim, audience), Together, keepsake renders | `src/companion/views/*`, `src/companion/styles.css` | 🟢 |
| Budgets: ~150–200k tris, ≤100–120 draws, 1–2K textures, ~15 MB critical art | Measured: 123k–136k tris, 95–101 draws, ≤2K, about 6.3 MB | 🟢 desktop / 🟡 Quest |

## Realistic scenes and photo-derived avatars (instructions of 7 November 2026)

| Instruction | Where | Status |
|---|---|---|
| “Create from my photos” in onboarding and Settings, labelled demo alternative | `OurDay.tsx` (`AvatarPrompt`), `views/AvatarStudio.tsx` | ✅ e2e |
| Explain/consent (service, who sees it, removal), file or camera capture, photo requirements, retake | `AvatarStudio.tsx` (consent, `PhotoStep`); `POST /me/avatar-consents`, `/me/avatar-sources` | 🟢 UI / ✅ API |
| Owner-chosen options, never inferred | body model + glasses step; `services/avatars.py` (`BODIES`) | ✅ API |
| Real job states, cancel, retry | `avatar_jobs`, `worker.py` (`avatar.generate`), job step polling | ✅ `test_avatars.py` (fake provider) / 🟡 real provider |
| Neutral-light review (front, three-quarter, side, seated, source photo), rotate, retake, regenerate | `ReviewStep`, `portrait.ts` (`renderReviewViews`, `mountAvatarViewer`) | ✅ e2e (test mannequin) |
| “This looks like me — use this avatar” records version and scope | `POST /me/avatar-assets/{id}/approve` (compare-and-set) | ✅ |
| Approved version everywhere; replace, stop sharing, delete sources, delete avatar | `serializers.avatar_view`, `GET /users/{id}/avatar`, `avatar_changed` realtime notice | ✅ API + e2e (open room swaps without reload) |
| Real provider adapter (MetaPerson), no invented endpoints, no purchase | `services/avatar_providers.py` (documented REST flow only) | ✅ fake-provider tests / ❌ never run live (no credentials) |
| Manual GLB import when generation is unavailable, stated as such | `ImportStep`, `POST /me/avatar-assets`, `services/glb.py` | ✅ e2e |
| Tables: sources, jobs, assets + profile active reference; migration | `models.py`, `migrations/versions/1c9e35884ad2_photo_derived_avatars.py` | ✅ upgrade/downgrade/check |
| Secure private uploads, EXIF strip, retention | `store_source` (re-encode), `purge_expired_sources` | ✅ |
| Authorized short-lived model delivery, GLB validation, cache clearing | signed viewer-bound URLs + re-authorization per request; `glb.inspect`; `purgeAvatarCaches`, ref-counted model cache | ✅ |
| Rig: skinning/morphs preserved, per-participant skeleton, units/facing, seated fit, IK; blinks only where supported | `shared/avatarRig.ts` (`SkinnedAvatar`) | 🟢 desktop (test mannequin) / 🟡 real export / 🟡 headset |
| Less amber, fewer glossy wood highlights, quieter dock and panels | `env.ts` (lights, `satin()`), `keepsakes3d.ts` (`WALNUT_TINT`), `ui3d.ts`/`main.ts` (glass) | 🟢 `docs/VISUAL_QA.md` |
| No Dubai claim for the Düsseldorf plate; simulated clip labelled as such | `room.html` place note; `origin` on sample media → *Simulated camera pan*, *Synthesized voice note*, *Sample photo* | ✅ |
| Settings overflow at 1440 px | `styles.css` (`.settings-grid`, `.rule-row` container query); `web/scripts/overflow-check.mjs` | ✅ |
| Captures 1672×941, 1440×900, 390×844 incl. avatar setup/approval | `web/scripts/capture.mjs`, `web/scripts/avatar-e2e.mjs` → `docs/visual-qa/after/` | ✅ |
| Docs: provider setup, config example without secrets, provenance, results table | `docs/AVATARS.md`, `.env.example`, `docs/ASSET_MANIFEST.md`, `docs/asset-provenance.json`, `docs/STATUS.md` | ✅ |
| Two consenting real-photo subjects, recognisable likenesses | — | ❌ not done (no subjects; no real photos used) |
| Quest performance with two skinned avatars | — | ❌ no hardware |

## Full application UX and emulator testing (8 November 2026)

| Request | Where | Status |
|---|---|---|
| A complete first-run journey for a new account | `views/Setup.tsx` (`GetStarted`: you → room or invite link → invite → avatar → usual times → done), routed from `App.tsx` | ✅ `web/scripts/onboarding-e2e.mjs` (1440×900, 390×844) |
| Invite links that are easy to send | `InviteCard` (single-use link, Copy, system Share sheet, New link) in setup, Our day, Together, Share, Settings | ✅ |
| No dead ends while someone hasn't joined yet | Our day "waiting for someone" card; Together "nobody to meet yet" with usual-times presets; Share "nobody has joined yet" (save privately still works); `NoRoom` for views that need a room | ✅ e2e |
| Usual free times without a form | `QuickTimes` presets (weekday evenings, weekend mornings …), full editor in Settings | ✅ e2e |
| Room settings | Settings → Rooms → *Room settings*: mute this room (per person), daily recap draft time (owner) | ✅ e2e |
| Distinct people by default | Varied starter demo avatars per account (face, hair, hair colour, clothing; one neutral skin) — `starter_definition` | ✅ `test_new_people_get_valid_varied_demo_avatars` |
| Test inside Meta's emulator | `web/scripts/xr-emulator-test.mjs` drives IWER's emulated Quest 3 (hands: poke, point + pinch, tracking loss; controller: ray + trigger; recenter; leave); `docs/EMULATOR.md` | ✅ 13 checks / 🟡 real headset still needed |
| Fix what the emulator finds | `src/xr/emulator.ts` (dev-only session shim), leaves instanced at real size + no phantom touch volumes, far-select proxies for table objects, flash/hint spacing, headset reply-sheet placement | ✅ |

## Native Quest application (instructions of 11 November 2026)

A native Unity client for Quest in `native/quest/`, tested in Meta XR Simulator, plus an APK. It uses Unity
6000.3.28f1, the Meta XR Core and Interaction SDK 209, OpenXR, URP and glTFast. The statuses come from
[NATIVE_STATUS.md](NATIVE_STATUS.md), which keeps the kinds of evidence apart. The legend above doesn't apply: a
desktop or IWER result says nothing about the native client.

`scripts/native/acceptance.ps1` runs the acceptance checks (marked *check n* below) in the simulator. None has
run yet: Unity, the simulator and the Android SDK are on one Windows machine. The rows follow the acceptance
checks in these instructions; the brief itself isn't in this repository.

| Acceptance row | Where | Status |
|---|---|---|
| Native app on the same API and authorization as the companion (no special endpoints) | `native/quest/Assets/SameSky/Scripts` (`Api/ApiClient.cs`, `Api/RealtimeClient.cs`, `App/`) | Implemented. Compiled at `cf2a5a3` (0 errors); the C# edits since are not compiled. Not simulator-tested |
| XR session in the simulator: simulated Meta Quest 3, hand input | `scripts/native/play_in_sim.ps1`, `sim.ps1`, the probe (`App/TestProbe.cs`); check 1 | Not run yet |
| A dock button pressed by hand | `SameSkyApp.SetDock`, `Interaction/Pressable.cs` (poke, or point and pinch; a press counts on release); check 2 | Implemented and compiled; not simulator-tested |
| The cup grabbed by hand | `SameSkyApp.Room.cs` (`BuildCup`), `Interaction/GrabbableObject.cs` (hand grab, distance grab); check 3 | Implemented and compiled; not simulator-tested |
| The waiting voice note heard within the first minute | Offline sample day (`App/RoomData.cs`, `Resources/Sample/`), `PlayWaiting` with timed captions; check 4 | Implemented and compiled; not simulator-tested |
| A reply on the table survives an app restart and an API restart | `StartReply` and `RestoreNotes` (`SameSkyApp.Room.cs`); `test_replies_and_placements_survive_restart`; check 5 (`stop.ps1`, `start.ps1`) | API ✅ automated. App not simulator-tested; the probe events check 5 relies on are not compiled yet |
| Recenter, pause and exit | `SameSkyApp.Recenter`, `SetPaused`, `ShowPause`, `ConfirmExit`; check 6 | Implemented and compiled; not simulator-tested. The system pause (Meta button, headset off) needs a headset |
| Tracking loss without ghost presses | `Pressable.cs` (a cancelled select never becomes a press), `GrabbableObject.cs` (clean release); check 7 (`metavr xrsim input source --hand right --source none`) | Implemented and compiled; not simulator-tested |
| MP4 of the simulator run | Check 8 (`metavr xrsim record start` and `stop`, one file per app session) | Not recorded yet |
| Invite codes typed by hand (headset keypad) | `POST /v1/invites/redeem-code`, `test_invite_codes.py` (6 tests); companion join step; `UI/SpatialKeyboard.cs` code keypad | API ✅ automated, and real-service-tested (`seed.ps1` redeems codes); keypad not simulator-tested |
| Voice notes playable in the native app | Ogg copy (`services/media.py`, `oggUrl`); `RoomData` `Audio()` | API ✅ `test_headset_voice_note_gets_an_ogg_twin_for_native_playback`; app playback not simulator-tested |
| Repeatable local setup: preflight, bootstrap, start/stop/seed, per-user installs | `scripts/native/*.ps1` (and `.sh` for macOS/Linux) | start, stop and seed real-service-tested on the Windows machine |
| APK for Quest 3/3S | `scripts/native/build.ps1 -Target Android`, `Editor/BuildScript.cs` | Not built: the Android SDK and NDK aren't unpacked yet (`install_unity_user.ps1`) |
| Installed on a headset and measured there | `docs/NATIVE_QUICKSTART.md` (section 5) | Not done: no Quest |

## P1 (not implemented)

More than two live avatars · WebRTC audio/video and TURN · live phone/webcam streaming · 360°/stereo footage ·
mixed-reality table placement · English/Arabic localization · weekly stories and rituals.
