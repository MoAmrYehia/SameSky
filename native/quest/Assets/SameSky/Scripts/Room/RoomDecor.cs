// Furnishes the room from a Decor: shows the chosen piece in every slot and gives it its finish.
//
// The project setup builds every catalogue piece into the scene (Editor/LoungeBuilder.cs). Each piece's root carries a
// DecorItem (which slot it fills, which piece it is) and is switched off unless it is the default. Every part that
// takes a finish carries a DecorPart with its own table of finish -> material, so nothing is looked up by name at
// run time and a build always contains the materials it needs. Walls and floor are always there: they have parts
// but no piece to switch.
//
// DecorBooth draws a small picture of a piece for the marketplace's tiles, from a copy of the real thing.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SameSky.App
{
    public sealed class RoomDecor : MonoBehaviour
    {
        public static RoomDecor Instance { get; private set; }

        /// <summary>What the room shows now (null until something has been applied: the scene's built-in defaults).</summary>
        public Decor Current { get; private set; }

        readonly List<DecorItem> _items = new();
        readonly List<DecorPart> _parts = new();

        void Awake()
        {
            Instance = this;
            GetComponentsInChildren(true, _items);
            GetComponentsInChildren(true, _parts);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Whether the scene holds this piece (a catalogue newer than the build may list pieces it lacks).</summary>
        public bool Has(string slot, string item) => _items.Exists(i => i.Slot == slot && i.Item == item) || _parts.Exists(p => p.Slot == slot && p.Item == item);

        public Transform ItemRoot(string slot, string item)
        {
            var found = _items.Find(i => i.Slot == slot && i.Item == item);
            return found != null ? found.transform : null;
        }

        /// <summary>Shows the room as the decor says. Slots the decor doesn't mention, and pieces this build
        /// doesn't have, stay as they are.</summary>
        public void Apply(Decor decor)
        {
            if (decor == null) return;
            foreach (var kv in decor.Choices)
            {
                var slot = kv.Key;
                var choice = kv.Value;
                if (!Has(slot, choice.Item)) continue;
                foreach (var item in _items)
                    if (item.Slot == slot && item.gameObject.activeSelf != (item.Item == choice.Item)) item.gameObject.SetActive(item.Item == choice.Item);
                if (choice.Finish == null) continue;
                foreach (var part in _parts)
                    if (part.Slot == slot && part.Item == choice.Item) part.Dress(choice.Finish);
            }
            Current = decor.Clone();
            // glossy things mirror the room: draw that picture again now that the room has changed
            foreach (var probe in FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))
                if (probe.mode == ReflectionProbeMode.Realtime) probe.RenderProbe();
        }
    }

    /// <summary>Small pictures of catalogue pieces, drawn once each from a copy of the piece standing far below
    /// the room, in the room's own light.</summary>
    public static class DecorBooth
    {
        const int Layer = 29;   // nothing else draws on it, so the picture shows the piece alone
        static readonly Vector3 Place = new(0f, -60f, 0f);
        static readonly Dictionary<string, RenderTexture> Pictures = new();

        public static Texture Picture(string slot, string item, string finish, int size = 256)
        {
            var key = $"{slot}/{item}/{finish}";
            if (Pictures.TryGetValue(key, out var done) && done != null) return done;
            var source = RoomDecor.Instance != null ? RoomDecor.Instance.ItemRoot(slot, item) : null;
            if (source == null) return null;

            var copy = UnityEngine.Object.Instantiate(source.gameObject);
            copy.name = "Booth " + key;
            copy.transform.SetParent(null, false);
            copy.transform.SetPositionAndRotation(Place, Quaternion.identity);
            copy.SetActive(true);
            foreach (var light in copy.GetComponentsInChildren<Light>(true)) light.enabled = false;
            foreach (var t in copy.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            if (finish != null)
                foreach (var part in copy.GetComponentsInChildren<DecorPart>(true)) part.Dress(finish);
            // a pair of chairs stands a room apart: the picture shows one of them
            if (slot == "chairs")
                for (var i = 1; i < copy.transform.childCount; i++)
                    if (copy.transform.GetChild(i).GetComponentInChildren<DecorPart>(true) != null) copy.transform.GetChild(i).gameObject.SetActive(false);
            // the soft shade under a piece is far wider than the piece and isn't part of it
            foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith("Contact shadow")) r.gameObject.SetActive(false);

            RenderTexture picture = null;
            var renderers = copy.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                var go = new GameObject("Booth camera");
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.stereoTargetEye = StereoTargetEyeMask.None;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.93f, 0.915f, 0.88f, 1f);   // the tile's own paper colour
                cam.cullingMask = 1 << Layer;
                cam.fieldOfView = 24f;
                // from the seat's side of the room, a little above and to one side, like looking across at it;
                // something that lies flat (a rug) is looked down on instead
                var flat = bounds.size.y < 0.1f * Mathf.Max(bounds.size.x, bounds.size.z);
                var from = (flat ? new Vector3(0.25f, 1.1f, -0.7f) : new Vector3(0.45f, 0.42f, -1f)).normalized;
                var radius = Mathf.Max(bounds.extents.magnitude, 0.05f);
                // the box's corner-to-corner radius overstates most pieces: stand a little closer than it asks
                var distance = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.86f;
                cam.transform.position = bounds.center + from * distance;
                cam.transform.LookAt(bounds.center);
                cam.aspect = 1f;
                cam.nearClipPlane = Mathf.Max(0.02f, distance - radius * 1.5f);
                cam.farClipPlane = distance + radius * 1.5f;
                picture = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "Piece " + key, antiAliasing = 4 };
                cam.targetTexture = picture;
                // a soft light from beside the camera, for this picture only: the side facing us is often the
                // side turned away from the window
                var fill = new GameObject("Booth light").AddComponent<Light>();
                fill.type = LightType.Directional;
                fill.cullingMask = 1 << Layer;
                fill.intensity = 0.55f;
                fill.color = new Color(1f, 0.97f, 0.93f);
                fill.shadows = LightShadows.None;
                fill.transform.rotation = Quaternion.LookRotation((-from + new Vector3(-0.35f, -0.3f, 0f)).normalized);
                try { cam.Render(); }
                catch (Exception e) { Debug.LogWarning($"[SameSky] marketplace picture of {key} failed: {e.GetType().Name}"); }
                cam.targetTexture = null;
                fill.enabled = false;
                UnityEngine.Object.Destroy(fill.gameObject);
                UnityEngine.Object.Destroy(go);
            }
            copy.SetActive(false);
            UnityEngine.Object.Destroy(copy);
            if (picture != null) Pictures[key] = picture;
            return picture;
        }

        /// <summary>Forgets every picture (the app is leaving, or the room's light changed).</summary>
        public static void Clear()
        {
            foreach (var p in Pictures.Values)
                if (p != null) { p.Release(); UnityEngine.Object.Destroy(p); }
            Pictures.Clear();
        }
    }
}
