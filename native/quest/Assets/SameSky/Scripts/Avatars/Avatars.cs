// Who sits across the table, and how they move.
//   Sample:        the authored demo character of the fictional person (exported from the companion's kit),
//                  labelled "simulated presence".
//   Live, demo:    a neutral, clearly labelled demo figure (no guessed likeness).
//   Live, photo:   the owner-approved, photo-derived model through a viewer-bound signed URL that the server
//                  re-authorizes on every download; each participant gets their own instance and skeleton.
// Motion: "app animation" (breathing, gaze) unless live poses arrive, then head/hands follow the person's
// tracked pose ("live"). The label says which.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GLTFast;
using Newtonsoft.Json.Linq;
using SameSky.Api;
using UnityEngine;

namespace SameSky.App
{
    public static class Avatars
    {
        static readonly Dictionary<Transform, Rig> Rigs = new();

        sealed class Rig
        {
            public Transform Chest, Neck, Head, Hips;
            public Transform LeftUpLeg, RightUpLeg, LeftLeg, RightLeg, LeftArm, RightArm, LeftForeArm, RightForeArm;
            public Vector3 ChestScale;
            public Quaternion HeadRest, NeckRest;
            public bool Skinned;
            public SeatedFigure Figure;   // an animated seated character: its Animator owns the pose, this only aims the gaze
            public Transform LiveLeft, LiveRight; // tracked-hand proxies when live
            public float LastLive = -10f;
            public Quaternion LiveHead = Quaternion.identity;
        }

        /// <summary>Seats the right figure for <paramref name="m"/> at <paramref name="seat"/> (facing the person).</summary>
        public static Transform SeatFor(Member m, Transform seat, bool isSample)
        {
            GameObject prefab = null;
            var demoLabel = false;
            if (isSample) prefab = Resources.Load<GameObject>("Characters/" + m.DisplayName.ToLowerInvariant());
            if (prefab == null) { prefab = Resources.Load<GameObject>("Characters/demo-figure"); demoLabel = !isSample; }
            if (prefab == null) return null;
            var root = new GameObject("Seated " + m.DisplayName).transform;
            root.SetParent(seat, false);
            var inst = UnityEngine.Object.Instantiate(prefab, root, false);
            inst.name = demoLabel ? "Demo figure" : prefab.name;
            SetLayer(root, PeopleLayer);
            if (demoLabel) Tint(inst, m.Avatar?.Definition);
            Register(root);
            if (!isSample && m.Avatar != null && m.Avatar.IsPhotoDerived) _ = SwapToApproved(root, m.Avatar);
            return root;
        }

        /// <summary>People sit on their own layer so the room's soft face light can reach them and nothing else.</summary>
        public const int PeopleLayer = 30;

        static void SetLayer(Transform root, int layer)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        static readonly Dictionary<string, RenderTexture> Portraits = new();

        /// <summary>A head-and-shoulders portrait of the character that sits in the room for this person: the same
        /// model and materials under the room's light, rendered once into a small texture for cards (visual references
        /// 01 and 03). For the sample's seated characters; null means "show their initial instead" (a live person's
        /// portrait needs their approved model, which only loads while they are seated).</summary>
        public static Texture Portrait(Member m, bool isSample, Transform seatSpace)
        {
            if (m == null || !isSample) return null;
            if (Portraits.TryGetValue(m.Id, out var cached) && cached != null) return cached;
            var prefab = Resources.Load<GameObject>("Characters/" + m.DisplayName.ToLowerInvariant());
            if (prefab == null || prefab.GetComponentInChildren<SeatedFigure>(true) == null) return null;
            // the booth is gone again before this frame is drawn; the picture stays
            var booth = Booth.ForPrefab(prefab, seatSpace);
            var rt = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4, name = "Portrait " + m.Id };
            booth.Render(0f, rt, fieldOfView: 23f, distance: 0.8f);
            booth.Dispose();
            Portraits[m.Id] = rt;
            TestProbe.Event("avatar", "portrait-rendered");
            return rt;
        }

        /// <summary>A private view of one character (visual references 01 and 13): it stands in a booth far below the
        /// floor, turned like the room so the window light falls on the face, and a camera circles its head and
        /// shoulders, drawing into textures a panel shows. Nothing in the room sees the booth. Dispose it when the
        /// review closes.</summary>
        public sealed class Booth : IDisposable
        {
            public readonly Transform Root;
            readonly Camera _camera;
            Vector3 _face;
            /// <summary>The large view (turned by the person) and a fixed side view.</summary>
            public RenderTexture Main { get; private set; }
            public RenderTexture Side { get; private set; }

            Booth(Transform seatSpace)
            {
                Root = new GameObject("Avatar booth").transform;
                Root.SetPositionAndRotation((seatSpace != null ? seatSpace.position : Vector3.zero) + Vector3.down * 40f,
                    seatSpace != null ? seatSpace.rotation : Quaternion.identity);
                // not a child of the booth: loading a model switches the booth's other children off
                _camera = new GameObject("Booth camera").AddComponent<Camera>();
                _camera.enabled = false;
                _camera.stereoTargetEye = StereoTargetEyeMask.None;
                _camera.cullingMask = 1 << PeopleLayer;
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(0.77f, 0.81f, 0.72f, 1f);   // the soft sage behind an initial
                _camera.nearClipPlane = 0.05f;
                _camera.farClipPlane = 4f;
                _face = Root.position + Vector3.up * 1.2f;
            }

            /// <summary>One of the app's own seated characters (the sample's people).</summary>
            public static Booth ForPrefab(GameObject prefab, Transform seatSpace)
            {
                var booth = new Booth(seatSpace);
                var figure = UnityEngine.Object.Instantiate(prefab, booth.Root, false);
                var animator = figure.GetComponentInChildren<Animator>(true);
                if (animator != null)
                {
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.Update(0f);   // the first frame of the seated loop, now
                    // a portrait looks ahead: lift the gaze from the table to straight in front
                    var seated = figure.GetComponentInChildren<SeatedFigure>(true);
                    var head = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
                    if (seated != null && head != null)
                    {
                        seated.HoldPortraitGaze(head.position + booth.Root.forward * 3f + Vector3.up * 0.1f);
                        animator.Update(0f);
                        seated.PoseFaceNow();   // eyes ahead and open, a hint of a smile
                    }
                }
                booth.Frame();
                return booth;
            }

            /// <summary>The owner's own photo-derived model, through their signed URL (private until they approve it).</summary>
            public static async Task<Booth> ForModel(AvatarView view, Transform seatSpace, CancellationToken ct)
            {
                var booth = new Booth(seatSpace);
                await SwapToApproved(booth.Root, view, ct);
                if (booth.Root == null) return null;
                if (booth.Root.GetComponentInChildren<Renderer>(true) == null)
                {
                    booth.Dispose();
                    throw new ApiException(0, "avatar_preview_failed", "The preview couldn't be loaded. Check the connection and try again.", true);
                }
                booth.Frame();
                return booth;
            }

            /// <summary>Finds the face: a humanoid's head bone, a bone called head, or the top of what is drawn.</summary>
            public void Frame()
            {
                SetLayer(Root, PeopleLayer);
                Transform head = null;
                var animator = Root.GetComponentInChildren<Animator>(true);
                if (animator != null && animator.isHuman) head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head == null)
                    foreach (var t in Root.GetComponentsInChildren<Transform>(true))
                        if (t.name.ToLowerInvariant().Replace("mixamorig:", "") == "head") { head = t; break; }
                if (head != null) { _face = head.position + Vector3.up * 0.07f; return; }
                var renderers = Root.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) return;
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                _face = new Vector3(b.center.x, b.max.y - 0.13f, b.center.z);
            }

            /// <summary>Draws the head and shoulders from yawDeg around the figure (0 = from the front).</summary>
            public void Render(float yawDeg, RenderTexture target, float fieldOfView = 27f, float distance = 0.95f)
            {
                if (_camera == null || target == null) return;
                var from = Quaternion.AngleAxis(yawDeg, Vector3.up) * Root.forward;
                _camera.fieldOfView = fieldOfView;
                _camera.transform.position = _face + from * distance + Vector3.up * 0.02f;
                _camera.transform.rotation = Quaternion.LookRotation(_face - _camera.transform.position, Vector3.up);
                _camera.targetTexture = target;
                _camera.Render();
                _camera.targetTexture = null;
            }

            /// <summary>Makes the two preview textures (square main view, upright side view) the first time they're needed.</summary>
            public void EnsureViews(int size = 512)
            {
                if (Main == null) Main = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4, name = "Avatar preview" };
                if (Side == null) Side = new RenderTexture(size * 3 / 4, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4, name = "Avatar side view" };
            }

            public void Dispose()
            {
                if (Root != null)
                {
                    Rigs.Remove(Root);
                    // Destroy only takes effect at the end of the frame: switched off now, so a second portrait made in
                    // the same frame (the two tiles on Together) doesn't have this figure standing in its picture
                    Root.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(Root.gameObject);
                }
                if (_camera != null) UnityEngine.Object.Destroy(_camera.gameObject);
                if (Main != null) { Main.Release(); UnityEngine.Object.Destroy(Main); Main = null; }
                if (Side != null) { Side.Release(); UnityEngine.Object.Destroy(Side); Side = null; }
            }
        }

        /// <summary>Takes a seated figure out of the room (and out of the animation registry).</summary>
        public static void Remove(Transform root)
        {
            if (root == null) return;
            Rigs.Remove(root);
            UnityEngine.Object.Destroy(root.gameObject);
        }

        static void Tint(GameObject go, JObject def)
        {
            var top = (string)def?["top"];
            if (top == null || !ColorUtility.TryParseHtmlString(top, out var c)) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var b = new MaterialPropertyBlock();
                r.GetPropertyBlock(b);
                b.SetColor("_BaseColor", Color.Lerp(c, Color.white, 0.25f));
                b.SetColor("baseColorFactor", Color.Lerp(c, Color.white, 0.25f));
                r.SetPropertyBlock(b);
            }
        }

        /// <summary>Loads the approved model for this viewer and replaces the demo figure once it is ready.</summary>
        public static async Task SwapToApproved(Transform root, AvatarView view, CancellationToken ct = default)
        {
            var app = SameSkyApp.Instance;
            if (app == null || string.IsNullOrEmpty(view.ModelUrl)) return;
            try
            {
                var bytes = await app.Api.Download(view.ModelUrl, ct, 120);
                var gltf = new GltfImport();
                // glTFast's generic Load(byte[] data, Uri uri = null, ImportSettings = null, CancellationToken = default)
                // detects glTF-Binary from the bytes; LoadGltfBinary is obsolete since glTFast 6.11.
                if (!await gltf.Load(bytes, new Uri(app.Api.Absolute(view.ModelUrl)))) throw new Exception("model didn't load");
                if (root == null) { gltf.Dispose(); return; }
                var holder = new GameObject("Approved avatar " + view.AssetId + "@" + view.AssetVersion).transform;
                holder.SetParent(root, false);
                if (!await gltf.InstantiateMainSceneAsync(holder)) throw new Exception("model didn't instantiate");
                SetLayer(holder, PeopleLayer);
                FitToSeat(holder);
                foreach (Transform child in root)
                    if (child != holder) child.gameObject.SetActive(false);
                Register(root);
                TestProbe.Event("avatar", "approved-model-shown");
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // keep the labelled demo figure; never show a stale or unauthorized model
                Debug.Log($"[SameSky] approved avatar unavailable: {(e is ApiException a ? a.Code : e.GetType().Name)}");
                TestProbe.Event("avatar", "demo-kept");
            }
        }

        /// <summary>Scales a photo-derived model to adult seated height (cm exports are detected) and sits it down.</summary>
        static void FitToSeat(Transform holder)
        {
            var renderers = holder.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            var height = b.size.y;
            if (height > 10f) holder.localScale *= 0.01f;          // centimetres
            else if (height > 0.1f && height < 1.2f) holder.localScale *= 1.65f / height; // small or kit-scale models
            Register(holder.parent);
        }

        static void Register(Transform root)
        {
            var rig = new Rig();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var n = t.name.ToLowerInvariant().Replace("mixamorig:", "").Replace("mixamorig_", "");
                switch (n)
                {
                    case "chest": case "spine2": case "upperchest": rig.Chest ??= t; break;
                    case "neck": rig.Neck ??= t; break;
                    case "head": rig.Head ??= t; break;
                    case "hips": case "pelvis": rig.Hips ??= t; break;
                    case "leftupleg": case "leftthigh": case "thigh_l": rig.LeftUpLeg ??= t; break;
                    case "rightupleg": case "rightthigh": case "thigh_r": rig.RightUpLeg ??= t; break;
                    case "leftleg": case "leftcalf": case "calf_l": rig.LeftLeg ??= t; break;
                    case "rightleg": case "rightcalf": case "calf_r": rig.RightLeg ??= t; break;
                    case "leftarm": case "upperarm_l": rig.LeftArm ??= t; break;
                    case "rightarm": case "upperarm_r": rig.RightArm ??= t; break;
                    case "leftforearm": case "lowerarm_l": rig.LeftForeArm ??= t; break;
                    case "rightforearm": case "lowerarm_r": rig.RightForeArm ??= t; break;
                }
            }
            rig.Skinned = root.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
            rig.Figure = root.GetComponentInChildren<SeatedFigure>(true);
            if (rig.Skinned && rig.Figure == null) SitSkinned(rig);
            if (rig.Chest) rig.ChestScale = rig.Chest.localScale;
            if (rig.Head) rig.HeadRest = rig.Head.localRotation;
            if (rig.Neck) rig.NeckRest = rig.Neck.localRotation;
            Rigs[root] = rig;
        }

        /// <summary>Seated rest pose for a humanoid skeleton: thighs forward, knees bent, forearms toward the table.</summary>
        static void SitSkinned(Rig r)
        {
            void Bend(Transform t, Vector3 euler) { if (t) t.localRotation *= Quaternion.Euler(euler); }
            Bend(r.LeftUpLeg, new Vector3(-85, 0, 0));
            Bend(r.RightUpLeg, new Vector3(-85, 0, 0));
            Bend(r.LeftLeg, new Vector3(85, 0, 0));
            Bend(r.RightLeg, new Vector3(85, 0, 0));
            Bend(r.LeftArm, new Vector3(0, 0, 55));
            Bend(r.RightArm, new Vector3(0, 0, -55));
            Bend(r.LeftForeArm, new Vector3(0, 40, 0));
            Bend(r.RightForeArm, new Vector3(0, -40, 0));
        }

        /// <summary>App animation: breathing and a gentle gaze toward the person. Live poses take over when fresh.</summary>
        public static void Idle(Transform root, Vector3 viewerHead, string presence)
        {
            if (root == null || !Rigs.TryGetValue(root, out var r)) return;
            var t = Time.time;
            var live = Time.time - r.LastLive < 1.5f;
            if (r.Figure != null && r.Figure.isActiveAndEnabled)
            {
                // the seated loop breathes on its own; the gaze rests on the person, lightly while a recording plays
                r.Figure.LookAt = viewerHead;
                r.Figure.LookWeight = presence == "recorded" ? 0.25f : 0.7f;
                if (r.LiveLeft) r.LiveLeft.gameObject.SetActive(live);
                if (r.LiveRight) r.LiveRight.gameObject.SetActive(live);
                return;
            }
            if (r.Chest) r.Chest.localScale = r.ChestScale * (1f + Mathf.Sin(t * 1.6f) * 0.006f);
            if (r.Head)
            {
                Quaternion target;
                if (live) target = r.LiveHead;
                else
                {
                    var to = viewerHead - r.Head.position;
                    var look = Quaternion.LookRotation(to.normalized, Vector3.up);
                    target = Quaternion.Slerp(r.Head.parent.rotation * r.HeadRest, look, presence == "recorded" ? 0.2f : 0.55f);
                    target *= Quaternion.Euler(Mathf.Sin(t * 0.37f) * 2f, Mathf.Sin(t * 0.21f) * 4f, 0);
                }
                r.Head.rotation = Quaternion.Slerp(r.Head.rotation, target, Time.deltaTime * 3f);
            }
            if (r.LiveLeft) r.LiveLeft.gameObject.SetActive(live);
            if (r.LiveRight) r.LiveRight.gameObject.SetActive(live);
        }

        /// <summary>Applies a live pose (table space) from the realtime session: head orientation and hand proxies.</summary>
        public static void ApplyLivePose(Transform root, JObject pose)
        {
            if (root == null || pose == null || !Rigs.TryGetValue(root, out var r)) return;
            r.LastLive = Time.time;
            var head = pose["head"]?["q"] as JArray;
            // Poses arrive in the sender's seat space (origin at their seat, +Z toward the table centre). Their seat
            // here is the partner seat anchor, which faces the table from the other side, so seat space maps 1:1.
            var seat = root.parent != null ? root.parent : root;
            if (head is { Count: 4 })
                r.LiveHead = seat.rotation * new Quaternion((float)head[0], (float)head[1], (float)head[2], (float)head[3]);
            r.LiveLeft = HandProxy(seat, r.LiveLeft, "Live left hand", pose["left"]?["p"] as JArray);
            r.LiveRight = HandProxy(seat, r.LiveRight, "Live right hand", pose["right"]?["p"] as JArray);
        }

        static Transform HandProxy(Transform seat, Transform proxy, string name, JArray p)
        {
            if (p is not { Count: 3 }) return proxy;
            if (proxy == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                UnityEngine.Object.Destroy(go.GetComponent<Collider>());
                go.name = name;
                go.transform.localScale = Vector3.one * 0.05f;
                go.GetComponent<Renderer>().material.color = new Color(0.95f, 0.85f, 0.75f, 0.85f);
                proxy = go.transform;
            }
            var world = seat.TransformPoint(new Vector3((float)p[0], (float)p[1], (float)p[2]));
            proxy.position = Vector3.Lerp(proxy.position, world, 0.5f);
            return proxy;
        }
    }
}
