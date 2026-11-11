// World-space UI building blocks: rounded panels (SameSky/RoundedPanel shader), TextMeshPro 3D text and images.
// No Canvas: every element is a small mesh, so panels can sit on spatial objects and be poked or pointed at
// through the Interaction SDK (see Interaction/Pressable.cs).
// Conventions: a panel's +Z points AWAY from the person (they look along +Z at it); sizes are in metres.

using TMPro;
using UnityEngine;

namespace SameSky.UI
{
    public static class Theme
    {
        // the companion's palette: warm cream cards, deep green actions, ink text
        public static readonly Color Card = new(0.965f, 0.945f, 0.915f, 0.94f);
        public static readonly Color CardDark = new(0.12f, 0.15f, 0.14f, 0.88f);
        public static readonly Color Glass = new(0.93f, 0.93f, 0.92f, 0.55f);
        public static readonly Color Ink = new(0.12f, 0.16f, 0.14f, 1f);
        public static readonly Color Muted = new(0.36f, 0.38f, 0.36f, 1f);
        public static readonly Color OnDark = new(0.96f, 0.95f, 0.92f, 1f);
        public static readonly Color Green = new(0.18f, 0.37f, 0.29f, 1f);
        public static readonly Color GreenSoft = new(0.86f, 0.91f, 0.87f, 1f);
        public static readonly Color Warm = new(0.95f, 0.82f, 0.62f, 1f);
        public static readonly Color Danger = new(0.62f, 0.2f, 0.16f, 1f);
        public static readonly Color Border = new(1f, 1f, 1f, 0.45f);
        public static readonly Color Label = new(0.98f, 0.93f, 0.82f, 1f); // honesty labels (sample, simulated...)

        public const float Body = 0.016f;    // line height in metres (readable at ~0.6-0.8 m)
        public const float Small = 0.012f;
        public const float Title = 0.026f;
        public const float Button = 0.017f;

        static float _scale = 1f;
        /// <summary>Text size preference (1, 1.25, 1.5), applied to text created afterwards.</summary>
        public static float TextScale { get => _scale; set => _scale = Mathf.Clamp(value, 1f, 1.6f); }
    }

    public static class Ui
    {
        static Material _panelMat;
        static Mesh _quad;
        static TMP_FontAsset _font;
        static readonly int SizeId = Shader.PropertyToID("_Size");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BorderColorId = Shader.PropertyToID("_BorderColor");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int BorderId = Shader.PropertyToID("_Border");
        static readonly int GlowId = Shader.PropertyToID("_Glow");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int UseTexId = Shader.PropertyToID("_UseTex");

        public static TMP_FontAsset Font
        {
            get
            {
                if (_font == null) _font = Resources.Load<TMP_FontAsset>("Fonts/SameSkySans SDF") ?? TMP_Settings.defaultFontAsset;
                return _font;
            }
        }

        static Material PanelMaterial
        {
            get
            {
                if (_panelMat == null)
                {
                    // A material asset in Resources keeps the shader in every build (Shader.Find alone would not).
                    _panelMat = Resources.Load<Material>("Materials/Panel");
                    if (_panelMat == null) _panelMat = new Material(Shader.Find("SameSky/RoundedPanel")) { enableInstancing = true, name = "SameSky Panel" };
                }
                return _panelMat;
            }
        }

        static Mesh Quad
        {
            get
            {
                if (_quad != null) return _quad;
                _quad = new Mesh { name = "SameSky UI Quad" };
                _quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0) };
                _quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
                _quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                _quad.RecalculateNormals();
                _quad.RecalculateBounds();
                return _quad;
            }
        }

        public static GameObject Node(Transform parent, string name, Vector3 localPos = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go;
        }

        /// <summary>A rounded rectangle, centered on its transform, facing -Z (toward the person).</summary>
        public static MeshRenderer Panel(Transform parent, string name, Vector2 size, Color fill, float radius = 0.018f,
            Vector3 localPos = default, Color? border = null, float borderWidth = 0.0012f)
        {
            var go = Node(parent, name, localPos);
            go.AddComponent<MeshFilter>().sharedMesh = Quad;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = PanelMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            SetPanel(r, size, fill, radius, border ?? Theme.Border, borderWidth);
            return r;
        }

        public static void SetPanel(Renderer r, Vector2 size, Color fill, float radius, Color border, float borderWidth, float glow = 0, Texture tex = null)
        {
            var b = new MaterialPropertyBlock();
            r.GetPropertyBlock(b);
            b.SetVector(SizeId, new Vector4(size.x, size.y, 0, 0));
            b.SetColor(ColorId, fill);
            b.SetColor(BorderColorId, border);
            b.SetFloat(RadiusId, radius);
            b.SetFloat(BorderId, borderWidth);
            b.SetFloat(GlowId, glow);
            if (tex != null) { b.SetTexture(MainTexId, tex); b.SetFloat(UseTexId, 1); }
            r.SetPropertyBlock(b);
        }

        public static void SetFill(Renderer r, Color fill, float glow = 0)
        {
            var b = new MaterialPropertyBlock();
            r.GetPropertyBlock(b);
            b.SetColor(ColorId, fill);
            b.SetFloat(GlowId, glow);
            r.SetPropertyBlock(b);
        }

        public static void SetImage(Renderer r, Texture tex)
        {
            var b = new MaterialPropertyBlock();
            r.GetPropertyBlock(b);
            b.SetTexture(MainTexId, tex);
            b.SetFloat(UseTexId, tex != null ? 1 : 0);
            r.SetPropertyBlock(b);
        }

        /// <summary>An image with rounded corners (photo, poster, thumbnail).</summary>
        public static MeshRenderer Image(Transform parent, string name, Vector2 size, Texture tex, float radius = 0.012f, Vector3 localPos = default)
        {
            var r = Panel(parent, name, size, Color.white, radius, localPos, new Color(1, 1, 1, 0.6f), 0.001f);
            if (tex != null) SetImage(r, tex);
            return r;
        }

        /// <summary>3D text. lineHeight is in metres (Theme.Body etc.) and follows the person's text-size setting.</summary>
        public static TextMeshPro Text(Transform parent, string name, string text, float lineHeight, Color color,
            float width = 0.4f, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, Vector3 localPos = default,
            FontStyles style = FontStyles.Normal, float height = 0)
        {
            var go = Node(parent, name, localPos);
            var t = go.AddComponent<TextMeshPro>();
            t.font = Font;
            t.text = text;
            t.fontSize = lineHeight * Theme.TextScale * 10f; // TMP 3D: font size 1 is about 0.1 m
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.rectTransform.sizeDelta = new Vector2(width, height > 0 ? height : lineHeight * 2f);
            t.renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // sit a hair in front of whatever panel it is on
            go.transform.localPosition += new Vector3(0, 0, -0.0015f);
            return t;
        }

        /// <summary>Rotates a transform to face the head (yaw, plus an optional upward tilt toward the eyes).</summary>
        public static void FaceHead(Transform t, Vector3 head, bool tiltToEyes = true)
        {
            var to = t.position - head;
            if (!tiltToEyes) to.y = 0;
            if (to.sqrMagnitude < 1e-6f) return;
            t.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
        }

        public static void Clear(Transform t)
        {
            for (var i = t.childCount - 1; i >= 0; i--)
            {
                var c = t.GetChild(i).gameObject;
                c.SetActive(false);
                Object.Destroy(c);
            }
        }
    }
}
