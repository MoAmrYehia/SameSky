# Photo-derived avatars

Date: 8 November 2026. SameSky shows people as **their own, owner-approved 3D avatar made from their photos**,
with a labelled demo avatar as the alternative. This document covers the user journey, the provider setup, the
data model and API, the rig contract, privacy rules, and exactly what has and hasn't been verified.

## Results (what is proved, and by what)

Each row is proved only by its own evidence; none is inferred from another.

| Capability | Status | Evidence |
|---|---|---|
| **Automated generation** (photo → provider → GLB) | **Blocked, not verified.** The MetaPerson adapter is implemented and covered by tests that use a fake provider, but it has never called the real service: this repository has no Avatar SDK credentials or paid plan. Without credentials the app says “generation isn’t set up on this server” and offers manual import. | `api/samesky/services/avatar_providers.py`; `api/tests/test_avatars.py` (fake provider: submit, poll, export, cleanup, 402/429/credential failures) |
| **Manual import** (a GLB made elsewhere) | **Verified end to end** with a non-photo test mannequin: Settings → Your avatar → Import → server validation → four review views → approval → partner sees it in portraits and seated in the room | `web/scripts/avatar-e2e.mjs` (real UI + API, two accounts); `api/tests/test_avatars.py::test_manual_import_validation` |
| **Owner-approved likeness** (it actually looks like the person) | **Not verified.** No consenting real-photo subjects were available, and no real person’s photo was used. The test mannequin is an original procedural figure, *not* a likeness. Two distinct consenting subjects are still needed. | — |
| **Live co-presence** with the approved model | **Verified on desktop, two browsers, no headset** — and seen from inside an immersive session in Meta's emulator (emulated Quest 3). Adam and Maya join one live session; Adam sees Maya live across the table as her approved model; when Maya stops sharing, Adam’s open room swaps to her labelled demo avatar within seconds, without a reload | `web/scripts/avatar-e2e.mjs`; `api/tests/test_realtime.py::test_avatar_change_tells_live_peers_to_look_again_without_revealing_it` |
| **Headset validation** (Quest 3/3S) | **Not verified on hardware.** No Quest was available. Room input and layout pass in Meta's emulator (`docs/EMULATOR.md`), but seated scale, IK, comfort and frame rate with a skinned avatar are unmeasured on a device. | `web/scripts/xr-emulator-test.mjs` (emulation only) |

## User journey (companion → Settings → *Your avatar*)

1. **Prompt.** Signed-in people who still use the demo avatar see *“Let people recognise you at the table”* on
   *Our day*, with **Create from my photos** and **Keep the demo avatar**. The prompt can be dismissed.
2. **Before you start (consent).** The prompt explains what is made and who processes the photo (Avatar SDK
   MetaPerson by itSeez3D, a third party; not Meta’s avatar system). It also says the photo stays private and is
   deleted after 7 days, that only the person sees the result until they approve it, and that one photo has
   limits. The person picks who may see the approved avatar (*people in my rooms*, *only my one-to-one rooms*,
   *only me*) and agrees to processing. Without provider credentials this step says so and offers **Import a
   model instead**.
3. **Photo.** From a file or the camera. Camera permission is asked separately, the stream always stops, and
   the photo requirements (sharp, even light, neutral expression, hair off the face, glasses off, ≥ 512 px) come
   with a retake path.
4. **Choices a photo can’t make.** Body model (the provider’s female or male base) and glasses removal are chosen
   by the person. SameSky never infers gender or body shape from a photo.
5. **Creating.** Real job states (*waiting*, *building … n%*, *checking the model*), with **Cancel**, **Try
   again** and **Use a different photo**. The page can be left; the current avatar stays in use.
6. **Does this look like you?** Front, three-quarter, side and seated renders in neutral light, beside the private
   source photo, plus a model the person can turn and zoom. Actions: **This looks like me — use this avatar**,
   **Try another photo** or **Discard**.
7. **In use.** The approval records the exact asset version and the sharing scope. Everyone allowed sees that
   version in portraits and in the room. *Sharing and removal* offers: change who can see it (*Only me* = stop
   sharing), delete this avatar, delete source photos, and delete all avatar data. Earlier approved versions can
   be used again. **Use a demo avatar** switches back without deleting anything.

Screens: `docs/visual-qa/after/avatar-{onboarding,consent,review,active}-{1672x941,1440x900,390x844}.jpg`.

## Provider setup (MetaPerson by Avatar SDK)

SameSky calls the provider **only from the server**; keys never reach a browser or the Quest.

1. Create an Avatar SDK developer account and an API application with **client-credentials** access. Per Avatar
   SDK’s 22 October 2026 update, REST API access is on the **Pro** plan. Without it, the provider returns HTTP 402,
   which SameSky reports as `provider_plan_required`. This repository did not buy a plan.
2. Set these on the API **and** the worker (the worker submits, polls every 5 s, downloads and cleans up):

   ```bash
   # .env (example — no real values; keep the real file out of git)
   SAMESKY_AVATAR_PROVIDER=metaperson
   METAPERSON_CLIENT_ID=<your-client-id>
   METAPERSON_CLIENT_SECRET=<your-client-secret>
   # optional
   METAPERSON_API_URL=https://api.avatarsdk.com
   METAPERSON_PLAYER_UID=            # only if your plan issues one
   SAMESKY_AVATAR_EXPORT_ORIGINS=api.avatarsdk.com   # hosts the export download may redirect to
   SAMESKY_AVATAR_SOURCE_RETENTION_DAYS=7
   SAMESKY_AVATAR_MAX_GLB_BYTES=41943040             # 40 MB
   SAMESKY_AVATAR_TIMEOUT_MINUTES=20
   ```

3. Run the worker (`cd api && python -m samesky.worker`). Generation, provider cleanup and the source-photo
   retention purge happen there.
4. Verify with one consenting adult’s photo. Review the result privately, then record whether it passes as a
   recognisable likeness. Keep the review out of public screenshots. Repeat with a second, different person
   (acceptance item 1).

The adapter follows the documented REST flow: `POST /o/token/`, `POST /avatars/` (pipeline `metaperson_2.0`,
`remove_smile`, optional `remove_glasses`), poll `GET /avatars/{code}/`, list `exports/`, download the export zip
(GLB, LOD1, embedded 1K PNG textures, ARKit-style blendshapes), and `DELETE /avatars/{code}/` on cancel or
deletion. The provider has no webhooks, so SameSky accepts no unauthenticated callbacks.

## Data model and API

New tables (migration `1c9e35884ad2_photo_derived_avatars`):

| Table | Holds |
|---|---|
| `avatar_consents` | provider, processing consent, likeness sharing scope, consent text version, withdrawal time |
| `avatar_sources` | private source photo (re-encoded, EXIF/GPS stripped, ≤ 2048 px), size, hash, retention date, deletion status |
| `avatar_jobs` | provider reference, sources, owner-chosen options, idempotency key, state, progress, safe error, provider cleanup |
| `avatar_assets` | validated GLB, content hash, rig summary, version, state (`awaiting_owner_approval` → `active` → `superseded`/`revoked`/`deleted`), visibility, approval time |

`avatars.active_asset_id` points at the approved version. The demo definition stays on the same row, and
`avatars.version` increases on every change so caches refresh.

Owner-only routes live under `/v1/me/avatar-*`; see `docs/API.md`. Others read a person’s avatar only through
`GET /v1/users/{id}/avatar`. That returns the demo definition unless the viewer currently shares a room with
the owner within the approved scope. In that case it adds a **viewer-bound signed model URL** (10 minutes,
HMAC over asset, variant, viewer and expiry). Every model download is **re-authorized**, so revoking access or
removing a member stops new downloads at once.

Live rooms: when someone approves, replaces, stops sharing, deletes or edits their avatar, connected
participants receive `{"type":"avatar_changed","userId":…}`. The notice carries no likeness information; each
client re-asks with its own authorization.

## Validation of imported or generated models

`api/samesky/services/glb.py` inspects the binary glTF before anyone can see it. It rejects:

- files over 40 MB;
- external resources and required extensions other than meshopt (Draco and Basis are rejected);
- truncated buffers and bad accessors;
- more than 250k triangles, 400k vertices, 3,000 nodes, 512 joints or 256 morph targets;
- more than 24 images, or textures over 4096 px;
- models with no skin, or none that maps to a humanoid skeleton (hips, neck, head, both arms and hands).

The rig summary stored with the asset records the joints, the mapped humanoid bones, the morph targets, whether
eye-blink morphs exist, and the triangle, vertex and texture counts.

## Rig contract (room and portraits)

`web/src/shared/avatarRig.ts`:

- **Skeletons.** Each participant gets a clone of the shared GLB with its own skeleton
  (`SkeletonUtils.clone`), from a ref-counted cache keyed by viewer, asset and version. Skinning and morph targets
  are preserved.
- **Bones.** Humanoid names are mapped from Mixamo/Avaturn, Unreal, VRM, Unity and Biped conventions (the
  same table is mirrored on the server). MetaPerson’s exact bone naming isn’t documented publicly, so a real
  export may need a mapping update.
- **Units and facing.** Centimetre exports are detected and scaled to metres. Facing is derived from the arm
  chain; the hips are seated on the measured chair cushion.
- **Pose.** Seated legs, a spine lean, and a neck/head look split 35/65 from tracked or authored head pose.
  Two-bone IK puts the hands on tracked hand positions; tracked hand orientation isn’t applied yet. Eye gaze is
  clamped.
- **Face.** Blinks are animated only when the model has eye-blink morphs. Nothing else is invented: no
  smiles, no lip motion, no expression tracking (Quest Browser exposes none of it to WebXR here).

## Privacy and retention

- **Source photos:** decoded and re-encoded server-side (EXIF and GPS dropped), stored outside every static
  path, served only to their owner with a signed URL and `no-store`. Deleted after 7 days by the worker’s purge,
  or immediately on request. They are never moments, recap inputs, notification images or room media.
- **Results:** only the owner sees a generated or imported result until they approve it. A new generation never
  replaces the approved version.
- **Revocation:** stopping sharing, removal from a room, withdrawing consent, or deletion takes effect for new
  requests at once. Copies already downloaded to a device can’t be recalled; the delete dialog says so.
- **Provider cleanup:** deleting an asset or cancelling a job asks the provider to delete its record; the result
  (`provider_cleanup`) is recorded on the job.
- **Logs and notifications:** no image bytes or signed URLs.

## Test fixture

`web/tests/fixtures/test-mannequin.glb` is an **original procedural artist’s mannequin** generated by
`web/scripts/build-test-rig.mjs`. It has a Mixamo-style 24-joint skin, eye-blink morphs and 17,584 triangles,
in metres, facing +Z. It is not photo-derived and not a person. Its only purpose is to exercise import,
validation, review, approval and the room rig.

## Remaining blockers

1. **Provider credentials and plan** to run a real generation (and confirm MetaPerson’s bone names and blink
   morph names in a real export).
2. **Two consenting adults** for the likeness check (private side-by-side review, recorded approvals).
3. **Quest 3/3S hardware** to measure frame time with two skinned avatars, and to check seated scale, hands,
   chair contact and tracking loss on device.
