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
            if (demoLabel) Tint(inst, m.Avatar?.Definition);
            Register(root);
            if (!isSample && m.Avatar != null && m.Avatar.IsPhotoDerived) _ = SwapToApproved(root, m.Avatar);
            return root;
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
                if (!await gltf.LoadGltfBinary(bytes, new Uri(app.Api.Absolute(view.ModelUrl)))) throw new Exception("model didn't load");
                if (root == null) { gltf.Dispose(); return; }
                var holder = new GameObject("Approved avatar " + view.AssetId + "@" + view.AssetVersion).transform;
                holder.SetParent(root, false);
                if (!await gltf.InstantiateMainSceneAsync(holder)) throw new Exception("model didn't instantiate");
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
            if (rig.Skinned) SitSkinned(rig);
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
            if (r.Chest) r.Chest.localScale = r.ChestScale * (1f + Mathf.Sin(t * 1.6f) * 0.006f);
            var live = Time.time - r.LastLive < 1.5f;
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
