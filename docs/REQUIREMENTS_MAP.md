# Requirements map

Legend: ✅ implemented and automatically tested · 🟢 implemented, exercised manually (desktop/emulator) ·
🟡 implemented, unverified on target hardware/service · ⬜ not implemented (P1/later).

## Competition

The official rules and FAQ were read again on Devpost on 13 November 2026 (entries close 18 November 2026 at
12:00 PM Pacific time). What an entry must contain, and where each item stands. ❌ here means *not done yet*.

| Requirement (rules, section 4; FAQ) | Where | Status |
|---|---|---|
| A VR/MR application for Meta VR devices, playable on Quest 3/3S (not glasses only). Entertainment track; New Experience division: conceived and built after 24 September 2026, no earlier codebase | `native/quest/` (Unity 6, Meta XR Core and Interaction SDK 209) and `web/room.html` (IWSDK/WebXR); the repository's first commit is from 6 November 2026 | 🟡 native: simulator-tested and APK-built; web: emulator-tested. Neither has run on a headset |
| Hands-first: fully usable with hands from start to finish; nobody ever needs to pair a controller | Native: poke, point and pinch, hand and distance grab, a spatial keyboard (`Scripts/Interaction`, `Scripts/UI/SpatialKeyboard.cs`). Web: pinch select and grab, poke dock. No buttons, keyboard, gaze or voice needed | 🟢 every native acceptance check is made with simulated hands, every web emulator check with emulated hands / 🟡 real hands |
| Seated; quick to get into and out of; a complete moment in under ten minutes | Seated table layout; the first meaningful moment is the cup's voice note, 33 s after a cold start in the simulator (native check 4); pause, resume and leave (check 6) | 🟢 simulator / 🟡 headset |
| Original, not a wrapper around another service | Own API, apps and content. Third-party parts are SDKs and licensed assets ([ASSET_MANIFEST.md](ASSET_MANIFEST.md)); the sample day runs with no service at all | ✅ |
| Performant on Quest hardware: 60 fps minimum | Web room: `?debug=1` HUD shows fps, draw calls and triangles (95–101 draw calls and 123k–136k triangles on a desktop). Native: `metavr perf` or OVR Metrics Tool ([NATIVE_QUICKSTART.md](NATIVE_QUICKSTART.md), section 5) | ❌ not measured: no headset |
| The build judges open: for a Unity app, an APK uploaded to a **new release channel named "Competition"** in the Meta developer dashboard, and the link to it on the Devpost form (a bare APK file is not accepted); for an IWSDK app, a hosted link | Native: `scripts/native/release.ps1 -SampleOnly` (a signed release build with no server: the sample day only), [NATIVE_QUICKSTART.md](NATIVE_QUICKSTART.md), section 6. Web: [DEPLOYMENT.md](DEPLOYMENT.md) | ❌ not uploaded yet. The release build was made and read back with a test key (not debuggable, target API level 34, Quest 3 and 3S, nothing of the build PC in it); the real one needs the entrant's keystore password and Meta developer account. Nothing is hosted |
| Demonstration video under three minutes, showing the project on a Quest or in XR Simulator, public on YouTube or Vimeo | [demo/README.md](demo/README.md): "I Saved You a Seat", 2:11, the native app in Meta XR Simulator (offline sample) with a companion insert; built by `scripts/demo/demo.ps1` | 🟡 made from simulator footage (no headset footage, no live session); ❌ not uploaded yet |
| Text description: inspiration, how it was built, plans for improvement, target launch date | — | ❌ to be written on the Devpost form |
| Testing instructions | [JUDGE_GUIDE.md](JUDGE_GUIDE.md) | 🟢 written from the simulator and emulator runs |
| Everything in English | Apps, the video's subtitles and the documents | ✅ |
| No identifiable person other than the entrant; no brand names, logos or commercial artwork; third-party tools and assets properly licensed | The sample's people are fictional (stock 3D characters, MIT). Its photos and clip are stock, credited, and show no identifiable person; voices are synthesized ([ASSET_MANIFEST.md](ASSET_MANIFEST.md), [asset-provenance.json](asset-provenance.json)) | 🟢 looked through again on 13 November |
| Free, available and unchanged from the deadline until the winners are announced (about 11 December 2026) | — | ❌ follows from the release channel |
| The entrant is a Start programme member with Developer Access by the time of submission | — | the entrant's own step |

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
| No Dubai claim for the Düsseldorf plate; sample media labelled as what it is | `room.html` place note; `origin` and `credit` on sample media → *Sample clip* and *Sample photo* (real stock footage and photographs, credited; before 12 November a *Simulated camera pan*), *Synthesized voice note* | ✅ |
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

`scripts/native/acceptance.ps1` runs the acceptance checks (marked *check n* below) in Meta XR Simulator 207.1 with a
simulated Meta Quest 3 and simulated hands, through the simulator's own controls. Latest full run, on the code as
it is committed: [`acceptance-035823`](native/evidence/2026-11-13/acceptance-035823.md), 13 November 2026,
**9 of 9 passed** (**simulator-tested**; checks 5 and 9 are also **real-service-tested**). Earlier runs:
[`acceptance-022557`](native/evidence/2026-11-13/acceptance-022557.md) (13 November, 9 of 9, with the render tour)
and [`acceptance-125054`](native/evidence/2026-11-12/acceptance-125054.md) (12 November, 9 of 9), where the renders
named below come from. The first rows follow the acceptance checks
in these instructions (the brief itself isn't in this repository); the rows after them cover what was asked for on
12 November: hands that look real, real photographs and footage in the demonstration, and a furniture marketplace.

| Acceptance row | Where | Status |
|---|---|---|
| Native app on the same API and authorization as the companion (no special endpoints) | `native/quest/Assets/SameSky/Scripts` (`Api/ApiClient.cs`, `Api/RealtimeClient.cs`, `App/`) | Compiled (Unity 6000.3.28f1, 0 errors). Live mode simulator-tested against the real local API (check 5) |
| XR session in the simulator: simulated Meta Quest 3, hand input | `scripts/native/play_in_sim.ps1`, `sim.ps1`, the probe (`App/TestProbe.cs`); check 1 | ✅ simulator-tested (OpenXR runtime "Meta XR Simulator 207.1.0", both hands tracked) |
| A dock button pressed by hand | `SameSkyApp.SetDock`, `Interaction/Pressable.cs` (poke, or point and pinch; a press counts on release); check 2 | ✅ simulator-tested: a simulated fingertip pressed *Our day* |
| The cup grabbed by hand | `SameSkyApp.Room.cs` (`BuildCup`), `Interaction/GrabbableObject.cs` (hand grab, distance grab); check 3 | ✅ simulator-tested: a palm grab lifted the cup, which moved with the hand |
| The waiting voice note heard within the first minute | Offline sample day (`App/RoomData.cs`, `Resources/Sample/`), `PlayWaiting` with timed captions; check 4 | ✅ simulator-tested: playing 33.1 s after a fresh start, captions shown, played to the end. Hearing it is a manual step |
| A reply on the table survives an app restart and an API restart | `StartReply` and `RestoreNotes` (`SameSkyApp.Room.cs`); `test_replies_and_placements_survive_restart`; check 5 (`stop.ps1`, `start.ps1`) | ✅ simulator-tested and real-service-tested: a new reply typed on the spatial keyboard came back after both restarts; the server kept it and its placement |
| Recenter, pause and exit | `SameSkyApp.Recenter`, `SetPaused`, `ShowPause`, `ConfirmExit`; check 6 | ✅ simulator-tested (recenter from 35° off to 0°). The system pause (Meta button, headset off) needs a headset |
| Tracking loss without ghost presses | `Pressable.cs` (a release is confirmed a frame later; tracking loss unselects and unhovers at once and counts as a cancel), `GrabbableObject.cs` (clean release); check 7 (`metavr xrsim input source --hand right --source none`) | ✅ simulator-tested: a cancel, no press. The first runs found a real ghost press here; fixed |
| MP4 of the simulator run | Check 8: one file per app session. Simulator 207.1's runtime has no MP4 recorder, so FFmpeg records the simulator window | ✅ 6 of 6 sessions recorded |
| Invite codes typed by hand (headset keypad) | `POST /v1/invites/redeem-code`, `test_invite_codes.py` (6 tests); companion join step; `UI/SpatialKeyboard.cs` code keypad | API ✅ automated, and real-service-tested (`seed.ps1` redeems codes); keypad not simulator-tested |
| Voice notes playable in the native app | Ogg copy (`services/media.py`, `oggUrl`); `RoomData` `Audio()` | API ✅ `test_headset_voice_note_gets_an_ogg_twin_for_native_playback`; the sample note's playback is simulator-tested (check 4) |
| Repeatable local setup: preflight, bootstrap, start/stop/seed, per-user installs | `scripts/native/*.ps1` (and `.sh` for macOS/Linux) | start, stop and seed real-service-tested on the Windows machine (check 5 restarts the API with them) |
| APK for Quest 3/3S | `scripts/native/build.ps1 -Target Android`, `Editor/BuildScript.cs` | ✅ APK-built (development build): 136 MB, ARM64, target API level 34, Quest 3 and 3S, `com.samesky.quest` 0.9.0, SHA-256 in [NATIVE_STATUS.md](NATIVE_STATUS.md). Not installed or started on a headset. No release build yet |
| Installed on a headset and measured there | `docs/NATIVE_QUICKSTART.md` (section 5) | Not done: no Quest |
| Hands that look real | Skin on Meta's tracked hand mesh: `Interaction/HandSkin.cs`, `Shaders/HandSkin.shader`, maps computed by `Editor/HandSkinSetup.cs` (tone, lighter palm, nails, knuckle and palm creases, shadows); six tones or the avatar's in Settings → *Your hands* | Drawn in the simulator: check 1's close-ups (`seat-hands.png`, `seat-palms.png`, `seat-hands-pinch.png`); every check's presses and grabs were made with these hands; unit test for the bundle. Not seen or measured on a headset |
| Actual photographs and video in the demonstration | Sample day: four photographs and one clip from Wikimedia Commons (CC0, public domain, CC BY), listed with hashes in `scripts/assets/sample_media.json`, fetched by `fetch_sample_media.py`, built by `build_real_sample_media.py`; `credit` and `origin: 'stock'` on each (`web/src/shared/sample.ts`), shown in the companion, the web room and the native app | ✅ built, credited and labelled *Sample photo* / *Sample clip*; web type check, unit tests and build pass; native unit tests check the labels and the credit; drawn in the native render tour. Web captures not re-taken |
| A marketplace to customise the furniture | API: `routers/furniture.py`, `services/furniture.py`, `furniture_catalog.json`, `room_decor` (migration `5e7a1c9d2b46`), realtime `decor_changed`. Native: `App/SameSkyApp.Market.cs`, `Room/RoomDecor.cs`, `Room/Furniture.cs`, the pieces in `Editor/LoungeBuilder.cs`; check 9. Every piece is included: nothing is priced or sold | API ✅ `test_furniture.py` (9 tests). Native ✅ simulator-tested and real-service-tested (check 9: a chair fabric chosen and kept by a simulated hand, on the server and back after an app restart); the other tabs and looks are drawn by the render tour, not pressed. Not in the web room or the companion |
| Unit tests for the native app | `native/quest/Assets/SameSky/Tests/EditMode`, `scripts/native/test.ps1` | ✅ 14 of 14 pass in Unity batch mode |
| A build judges can run with nothing behind it (asked for on 13 November: the API isn't hosted) | Sample-only build: `App/AppConfig.cs` (`SampleOnly`), `scripts/native/build.ps1 -SampleOnly`, `scripts/native/release.ps1` (signs with the entrant's keystore; the password is typed at a prompt). The welcome panel offers the sample day and no account buttons, and says why | ✅ simulator-tested as that build ([`acceptance-033533`](native/evidence/2026-11-13/acceptance-033533.md): checks 1 to 4, 6 and 7; 5 and 9 need the service). Release build made and read back with a test key only: see [NATIVE_STATUS.md](NATIVE_STATUS.md) |

## P1 (not implemented)

More than two live avatars · WebRTC audio/video and TURN · live phone/webcam streaming · 360°/stereo footage ·
mixed-reality table placement · English/Arabic localization · weekly stories and rituals.
