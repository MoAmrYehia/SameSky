// Realistic hands. Meta's Interaction SDK draws the tracked hands with a dark see-through material; this puts skin
// on those same meshes instead (Shaders/HandSkin.shader, maps baked by Editor/HandSkinSetup.cs): a skin tone the
// person chooses, a lighter palm, nails, knuckle and palm creases, the room's light and shadows. The hands also
// cast a shadow on the table.
//
// Tracking, poses and interaction are untouched: only the material on the hand visual changes. If the SDK ever
// draws a different hand mesh than the one the maps were baked for, the SDK's own material is left in place.

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.Rendering;

namespace SameSky.Interaction
{
    public sealed class HandSkin : MonoBehaviour
    {
        /// <summary>The skin tones offered in Settings, light to deep.</summary>
        public static readonly (string Name, Color Tone)[] Tones =
        {
            ("Tone 1", Hex("#EFCFBA")),
            ("Tone 2", Hex("#DFB89C")),
            ("Tone 3", Hex("#C79C7E")),
            ("Tone 4", Hex("#A67C5E")),
            ("Tone 5", Hex("#805A42")),
            ("Tone 6", Hex("#573B2D")),
        };

        public const int DefaultTone = 2;

        public static HandSkin Instance { get; private set; }
        /// <summary>How many hand meshes wear the skin right now (for the probe and tests).</summary>
        public int Dressed => _dressed.Count;
        public Color Tone { get; private set; }

        Material _left, _right;
        string _leftMesh, _rightMesh;
        int _leftVertices, _rightVertices;
        readonly HashSet<SkinnedMeshRenderer> _dressed = new();
        readonly HashSet<SkinnedMeshRenderer> _skipped = new();
        float _nextScan;

        void Awake()
        {
            Instance = this;
            var left = Resources.Load<Material>("Materials/HandSkinLeft");
            var right = Resources.Load<Material>("Materials/HandSkinRight");
            var info = Resources.Load<TextAsset>("hand-skin");
            if (left == null || right == null || info == null)
            {
                Debug.LogWarning("[SameSky] hand skin not built (run the project setup): the hands keep Meta's default look");
                enabled = false;
                return;
            }
            try
            {
                var json = JObject.Parse(info.text);
                _leftMesh = (string)json["left"]?["mesh"];
                _leftVertices = (int?)json["left"]?["vertices"] ?? 0;
                _rightMesh = (string)json["right"]?["mesh"];
                _rightVertices = (int?)json["right"]?["vertices"] ?? 0;
            }
            catch (Exception) { enabled = false; return; }
            _left = new Material(left) { name = "Hand skin (left)" };
            _right = new Material(right) { name = "Hand skin (right)" };
            SetTone(Tones[DefaultTone].Tone);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_left != null) Destroy(_left);
            if (_right != null) Destroy(_right);
        }

        /// <summary>Colours the hands: the tone is the back of the hand; palm, nails and the flush follow from it.</summary>
        public void SetTone(Color tone)
        {
            Tone = tone;
            if (_left == null) return;
            Color.RGBToHSV(tone, out _, out _, out var value);
            var deep = Mathf.InverseLerp(0.95f, 0.35f, value);   // 0 for the lightest tone, 1 for the deepest
            // palms hold much less pigment than the back of the hand: the deeper the tone, the bigger the difference
            var palm = Color.Lerp(tone, new Color(0.95f, 0.78f, 0.71f), Mathf.Lerp(0.36f, 0.6f, deep));
            // the nail plate shows the pink nail bed through it; its free edge is paler, never chalk white
            var nail = Color.Lerp(Color.Lerp(tone, palm, 0.5f), new Color(0.93f, 0.68f, 0.64f), 0.42f);
            var nailTip = Color.Lerp(nail, new Color(0.95f, 0.91f, 0.85f), 0.55f);
            foreach (var m in new[] { _left, _right })
            {
                m.SetColor("_SkinColor", tone);
                m.SetColor("_PalmColor", palm);
                m.SetColor("_NailColor", nail);
                m.SetColor("_NailTipColor", nailTip);
            }
        }

        /// <summary>A tone from a hex colour such as an avatar's skin ("#d29a72"); false when it isn't one.</summary>
        public static bool TryParseTone(string hex, out Color tone) => ColorUtility.TryParseHtmlString(hex ?? "", out tone);

        void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 1f;   // hand visuals can appear late (tracking found, a rig rebuilt)
            foreach (var visual in FindObjectsByType<HandVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var hand = visual.Hand;
                if (hand == null) continue;       // not awake yet
                var left = hand.Handedness == Handedness.Left;
                foreach (var smr in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (_dressed.Contains(smr) || _skipped.Contains(smr)) continue;
                    var mesh = smr.sharedMesh;
                    var known = mesh != null && mesh.name == (left ? _leftMesh : _rightMesh) && mesh.vertexCount == (left ? _leftVertices : _rightVertices);
                    if (!known)
                    {
                        _skipped.Add(smr);
                        // the SDK keeps a second, switched-off mesh for its older hand skeleton: nothing to report there
                        if (smr.gameObject.activeInHierarchy)
                            Debug.LogWarning($"[SameSky] hand skin: {(mesh != null ? mesh.name : "no mesh")} is not the mesh the skin was made for; left as it is");
                        continue;
                    }
                    var materials = smr.sharedMaterials;
                    for (var i = 0; i < materials.Length; i++) materials[i] = left ? _left : _right;
                    smr.sharedMaterials = materials;
                    smr.shadowCastingMode = ShadowCastingMode.On;
                    smr.receiveShadows = true;
                    _dressed.Add(smr);
                }
            }
            _dressed.RemoveWhere(s => s == null);
            _skipped.RemoveWhere(s => s == null);
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.gray;
    }
}
