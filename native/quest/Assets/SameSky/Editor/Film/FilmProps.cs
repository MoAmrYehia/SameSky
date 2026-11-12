// Meshes, textures and props made for the animated trailer: soft-edged boxes for furniture, generated textures (water
// ripples, a far skyline, lit windows, clouds), and the things people handle in the film (a phone with a simple
// companion screen, keys, a headset, a camera, a paper cup). All of it is built in code, so the film can be rebuilt.

using System;
using System.Collections.Generic;
using SameSky.Geometry;
using SameSky.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace SameSky.Film
{
    public static class FilmMesh
    {
        static readonly Dictionary<string, Mesh> Cache = new();

        static float[] Axis(float half, float r, int n)
        {
            var list = new List<float>();
            for (var i = n; i >= 0; i--) list.Add(-(half - r) - r * Mathf.Tan(Mathf.PI / 4f * i / n));
            for (var i = 0; i <= n; i++) list.Add(half - r + r * Mathf.Tan(Mathf.PI / 4f * i / n));
            return list.ToArray();
        }

        /// <summary>A box with rounded edges and corners, centred on its origin.</summary>
        public static Mesh RoundedBox(Vector3 size, float radius, int steps = 4, float uvScale = 1f)
        {
            radius = Mathf.Min(radius, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.5f - 1e-5f);
            radius = Mathf.Max(radius, 1e-4f);
            var key = $"box {size.x:F4} {size.y:F4} {size.z:F4} {radius:F4} {steps} {uvScale:F3}";
            if (Cache.TryGetValue(key, out var had) && had != null) return had;
            var half = size * 0.5f;
            var axes = new[] { Axis(half.x, radius, steps), Axis(half.y, radius, steps), Axis(half.z, radius, steps) };
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var inner = half - Vector3.one * radius;
            for (var c = 0; c < 3; c++)
            {
                int a = (c + 1) % 3, b = (c + 2) % 3;
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    var start = verts.Count;
                    float[] us = axes[a], vs = axes[b];
                    for (var iv = 0; iv < vs.Length; iv++)
                        for (var iu = 0; iu < us.Length; iu++)
                        {
                            var p = Vector3.zero;
                            p[a] = us[iu];
                            p[b] = vs[iv];
                            p[c] = sign * half[c];
                            var core = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                            var dir = (p - core).normalized;
                            verts.Add(core + dir * radius);
                            normals.Add(dir);
                            uvs.Add(new Vector2(p[a], p[b]) * uvScale);
                        }
                    for (var iv = 0; iv < vs.Length - 1; iv++)
                        for (var iu = 0; iu < us.Length - 1; iu++)
                        {
                            int i00 = start + iv * us.Length + iu, i10 = i00 + 1, i01 = i00 + us.Length, i11 = i01 + 1;
                            if (sign > 0) tris.AddRange(new[] { i00, i10, i01, i10, i11, i01 });
                            else tris.AddRange(new[] { i00, i01, i10, i10, i01, i11 });
                        }
                }
            }
            var mesh = new Mesh { name = key, hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }

        public static Mesh Lathe(string name, Vector2[] profile, int segments = 40)
        {
            var key = "lathe " + name;
            if (Cache.TryGetValue(key, out var had) && had != null) return had;
            var mesh = MeshGen.Lathe(name, profile, segments);
            mesh.hideFlags = HideFlags.HideAndDontSave;
            Cache[key] = mesh;
            return mesh;
        }

        public static Mesh Arc(string name, float radius, float startDeg, float endDeg, float width, float y0, float y1, float corner = 0.004f)
        {
            var key = $"arc {name} {radius:F3} {startDeg:F0} {endDeg:F0} {width:F3} {y0:F3} {y1:F3}";
            if (Cache.TryGetValue(key, out var had) && had != null) return had;
            var mesh = MeshGen.Sweep(name, radius, startDeg, endDeg, 36, _ => MeshGen.RoundedSection(width, y0, y1, corner));
            mesh.hideFlags = HideFlags.HideAndDontSave;
            Cache[key] = mesh;
            return mesh;
        }
    }

    /// <summary>Textures made in code.</summary>
    public static class FilmTex
    {
        static readonly Dictionary<string, Texture2D> Cache = new();

        static Texture2D Make(string key, int w, int h, bool linear, Func<int, int, Color> pixel, TextureWrapMode wrap = TextureWrapMode.Repeat)
        {
            if (Cache.TryGetValue(key, out var had) && had != null) return had;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true, linear) { name = key, wrapMode = wrap, anisoLevel = 8, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color[w * h];
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++) px[y * w + x] = pixel(x, y);
            t.SetPixels(px);
            t.Apply(true, false);
            Cache[key] = t;
            return t;
        }

        /// <summary>A tileable normal map of small water ripples.</summary>
        public static Texture2D Ripples(int size = 256, int seed = 3)
        {
            var rng = new System.Random(seed);
            var waves = new (float kx, float ky, float phase, float amp)[34];
            for (var i = 0; i < waves.Length; i++)
            {
                // whole numbers of waves across the tile, so it repeats without a seam
                // (wave directions all round, more of the long ones: no grid shows through at a distance)
                int kx = rng.Next(-7, 8), ky = rng.Next(-7, 8);
                if (kx == 0 && ky == 0) ky = 3;
                waves[i] = (kx, ky, (float)rng.NextDouble() * 6.283f, (0.5f + (float)rng.NextDouble()) * 3f / Mathf.Sqrt(kx * kx + ky * ky));
            }
            float Height(float u, float v)
            {
                var h = 0f;
                foreach (var w in waves) h += w.amp * Mathf.Sin((u * w.kx + v * w.ky) * 6.2832f + w.phase) * 0.35f;
                return h;
            }
            return Make($"ripples {size} {seed}", size, size, true, (x, y) =>
            {
                float u = x / (float)size, v = y / (float)size, e = 1f / size;
                float dx = Height(u + e, v) - Height(u - e, v), dy = Height(u, v + e) - Height(u, v - e);
                var n = new Vector3(-dx * 2.2f, -dy * 2.2f, 1f).normalized;
                return new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            });
        }

        /// <summary>A sunset sky as a latitude-longitude picture (HDR): gold at the horizon under the sun, rose and
        /// mauve away from it, deep blue overhead, a glowing sun and a few long clouds lit from below. The sun stands
        /// at the given azimuth (degrees from +Z toward +X) and height.</summary>
        public static Texture2D SunsetSky(float sunAzimuth, float sunHeight, int w = 2048, int h = 1024)
        {
            var key = $"sunset {sunAzimuth:F1} {sunHeight:F1} {w}";
            if (Cache.TryGetValue(key, out var had) && had != null) return had;
            var t = new Texture2D(w, h, TextureFormat.RGBAHalf, false, true) { name = key, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            t.wrapModeU = TextureWrapMode.Repeat;
            var px = new Color[w * h];
            float az = sunAzimuth * Mathf.Deg2Rad, el = sunHeight * Mathf.Deg2Rad;
            var sun = new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
            Color Ramp(float e, Color horizon, Color low, Color mid, Color top)
            {
                // e: height above the horizon, 0..1 for 0..90 degrees
                if (e < 0.07f) return Color.Lerp(horizon, low, Mathf.SmoothStep(0f, 1f, e / 0.07f));
                if (e < 0.26f) return Color.Lerp(low, mid, Mathf.SmoothStep(0f, 1f, (e - 0.07f) / 0.19f));
                return Color.Lerp(mid, top, Mathf.SmoothStep(0f, 1f, (e - 0.26f) / 0.74f));
            }
            for (var y = 0; y < h; y++)
            {
                var elevation = (y / (h - 1f) - 0.5f) * Mathf.PI;
                var e = Mathf.Clamp01(elevation / (Mathf.PI * 0.5f));
                for (var x = 0; x < w; x++)
                {
                    var lon = (0.5f - x / (float)w) * Mathf.PI * 2f;      // as Skybox/Panoramic reads it
                    var dir = new Vector3(Mathf.Cos(lon) * Mathf.Cos(elevation), Mathf.Sin(elevation), Mathf.Sin(lon) * Mathf.Cos(elevation));
                    var flat = new Vector2(dir.x, dir.z).normalized;
                    var toward = Mathf.Clamp01(0.5f + 0.5f * Vector2.Dot(flat, new Vector2(sun.x, sun.z).normalized));   // 1 facing the sun
                    var near = Mathf.Pow(toward, 3f);
                    var warm = Ramp(e, new Color(1.25f, 0.56f, 0.2f), new Color(1.0f, 0.4f, 0.24f), new Color(0.52f, 0.28f, 0.4f), new Color(0.09f, 0.12f, 0.3f));
                    var cool = Ramp(e, new Color(0.6f, 0.34f, 0.36f), new Color(0.44f, 0.29f, 0.44f), new Color(0.2f, 0.2f, 0.4f), new Color(0.06f, 0.08f, 0.22f));
                    var c = Color.Lerp(cool, warm, near);
                    if (elevation < 0f) c = Color.Lerp(c, new Color(0.16f, 0.1f, 0.1f), Mathf.Clamp01(-elevation * 6f));
                    var angle = Vector3.Angle(dir, sun);
                    c += new Color(1.5f, 0.62f, 0.2f) * Mathf.Exp(-angle * angle / (2f * 11f * 11f)) * 0.4f;
                    c += new Color(3f, 1.7f, 0.7f) * Mathf.Exp(-angle * angle / (2f * 2.6f * 2.6f)) * 0.6f;
                    if (angle < 1.25f) c += new Color(26f, 15f, 6.5f) * Mathf.SmoothStep(1.25f, 0.9f, angle);
                    // long clouds, a little way up, lit from below
                    if (elevation > 0.02f && elevation < 0.6f)
                    {
                        float u = lon * 2.2f, v = elevation * 26f;
                        float n = 0.55f * Mathf.PerlinNoise(u + 11.3f, v + 3.1f) + 0.3f * Mathf.PerlinNoise(u * 2.3f + 5.7f, v * 2.1f + 9.2f) + 0.15f * Mathf.PerlinNoise(u * 5.1f + 1.9f, v * 4.3f + 17.7f);
                        // the same pattern one turn on, blended in near the join so the picture wraps without a seam
                        float u2 = (lon + Mathf.PI * 2f) * 2.2f;
                        float n2 = 0.55f * Mathf.PerlinNoise(u2 + 11.3f, v + 3.1f) + 0.3f * Mathf.PerlinNoise(u2 * 2.3f + 5.7f, v * 2.1f + 9.2f) + 0.15f * Mathf.PerlinNoise(u2 * 5.1f + 1.9f, v * 4.3f + 17.7f);
                        n = Mathf.Lerp(n, n2, Mathf.SmoothStep(-Mathf.PI * 0.6f, -Mathf.PI, lon));
                        var band = Mathf.SmoothStep(0.02f, 0.09f, elevation) * Mathf.SmoothStep(0.6f, 0.3f, elevation);
                        var cover = Mathf.SmoothStep(0.44f, 0.62f, n) * band * 0.85f;
                        var lit = Color.Lerp(new Color(0.36f, 0.26f, 0.36f), new Color(1.9f, 0.62f, 0.3f), Mathf.Sqrt(near) * Mathf.SmoothStep(0.5f, 0.04f, elevation));
                        c = Color.Lerp(c, lit, cover);
                    }
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }
            t.SetPixels(px);
            t.Apply(false, false);
            Cache[key] = t;
            return t;
        }

        /// <summary>Paving: square stones with darker joints, each stone a slightly different shade.</summary>
        public static Texture2D Pavers(int stones = 8, int seed = 6)
        {
            var rng = new System.Random(seed);
            var shade = new float[stones, stones];
            for (var i = 0; i < stones; i++)
                for (var j = 0; j < stones; j++) shade[i, j] = 0.86f + 0.14f * (float)rng.NextDouble();
            const int cell = 64;
            return Make($"pavers {stones} {seed}", stones * cell, stones * cell, false, (x, y) =>
            {
                int lx = x % cell, ly = y % cell;
                var edge = Mathf.Min(Mathf.Min(lx, cell - 1 - lx), Mathf.Min(ly, cell - 1 - ly));
                var joint = Mathf.SmoothStep(0f, 1f, edge / 2.5f);
                var v = shade[x / cell, y / cell] * Mathf.Lerp(0.5f, 1f, joint) * (0.97f + 0.03f * Mathf.PerlinNoise(x * 0.31f, y * 0.31f));
                return new Color(v, v, v, 1f);
            });
        }

        /// <summary>Glints on water: short bright dashes, denser down the middle.</summary>
        public static Texture2D Sparkle(int seed = 4)
        {
            var rng = new System.Random(seed);
            var on = new float[64, 256];
            for (var j = 0; j < 256; j++)
                for (var i = 0; i < 64; i++)
                {
                    var middle = 1f - Mathf.Abs(i - 31.5f) / 32f;
                    on[i, j] = rng.NextDouble() < 0.05 + 0.3 * middle * middle ? 0.4f + 0.6f * (float)rng.NextDouble() : 0f;
                }
            return Make($"sparkle {seed}", 256, 512, false, (x, y) =>
            {
                var a = on[x / 4, y / 2] * Mathf.Sin((x % 4 + 0.5f) / 4f * Mathf.PI);
                return new Color(1f, 1f, 1f, a);
            });
        }

        /// <summary>A far shore at dusk: building shapes with a few lit windows, clear above the roofs.</summary>
        public static Texture2D Skyline(int w = 2048, int h = 256, int seed = 11)
        {
            var rng = new System.Random(seed);
            var roof = new int[w];
            var lit = new bool[w / 4 + 1, h / 5 + 1];
            var x0 = 0;
            while (x0 < w)
            {
                var width = rng.Next(14, 60);
                var height = (int)(h * (0.16f + 0.62f * Mathf.Pow((float)rng.NextDouble(), 2.2f)));
                for (var x = x0; x < Mathf.Min(w, x0 + width); x++) roof[x] = height;
                x0 += width + (rng.NextDouble() < 0.25 ? rng.Next(2, 10) : 0);
            }
            for (var i = 0; i < lit.GetLength(0); i++)
                for (var j = 0; j < lit.GetLength(1); j++) lit[i, j] = rng.NextDouble() < 0.16;
            return Make($"skyline {w} {h} {seed}", w, h, false, (x, y) =>
            {
                if (y >= roof[x]) return new Color(0, 0, 0, 0);
                var window = lit[x / 4, y / 5] && x % 4 < 2 && y % 5 < 3 && y < roof[x] - 3 && y > 4;
                return window ? new Color(1f, 0.74f, 0.42f, 1f) : new Color(0.075f, 0.085f, 0.13f, 1f);
            }, TextureWrapMode.Clamp);
        }

        /// <summary>A building front at night: rows of windows, some lit warm, some dark.</summary>
        public static Texture2D Windows(int cols = 14, int rows = 10, int seed = 5)
        {
            var rng = new System.Random(seed);
            var state = new float[cols, rows];
            for (var i = 0; i < cols; i++)
                for (var j = 0; j < rows; j++) state[i, j] = rng.NextDouble() < 0.42 ? 0.5f + 0.5f * (float)rng.NextDouble() : 0f;
            const int cell = 32;
            return Make($"windows {cols} {rows} {seed}", cols * cell, rows * cell, false, (x, y) =>
            {
                int i = x / cell, j = y / cell, lx = x % cell, ly = y % cell;
                var inside = lx > 7 && lx < 25 && ly > 6 && ly < 27;
                if (!inside) return new Color(0.07f, 0.075f, 0.1f, 1f);
                var s = state[i, j];
                return s > 0f ? new Color(1f * s, 0.72f * s, 0.4f * s, 1f) : new Color(0.03f, 0.04f, 0.06f, 1f);
            }, TextureWrapMode.Clamp);
        }

        /// <summary>A soft cloud: a few overlapping blobs, alpha only.</summary>
        public static Texture2D Cloud(int seed = 1)
        {
            var rng = new System.Random(seed);
            var blobs = new (float x, float y, float rx, float ry, float a)[9];
            for (var i = 0; i < blobs.Length; i++)
                blobs[i] = (0.2f + 0.6f * (float)rng.NextDouble(), 0.38f + 0.24f * (float)rng.NextDouble(), 0.1f + 0.16f * (float)rng.NextDouble(), 0.07f + 0.07f * (float)rng.NextDouble(), 0.5f + 0.5f * (float)rng.NextDouble());
            return Make($"cloud {seed}", 256, 128, false, (x, y) =>
            {
                float u = x / 256f, v = y / 128f, a = 0f;
                foreach (var b in blobs)
                {
                    float dx = (u - b.x) / b.rx, dy = (v - b.y) / b.ry;
                    a += b.a * Mathf.Exp(-(dx * dx + dy * dy) * 1.6f);
                }
                a = Mathf.Clamp01(a * 0.75f) * Mathf.SmoothStep(0f, 0.12f, u) * Mathf.SmoothStep(0f, 0.12f, 1f - u);
                return new Color(1f, 1f, 1f, a);
            }, TextureWrapMode.Clamp);
        }

        /// <summary>A vertical run of colours, bottom to top.</summary>
        public static Texture2D Gradient(string name, params Color[] stops)
        {
            return Make("gradient " + name, 4, 256, false, (x, y) =>
            {
                var f = y / 255f * (stops.Length - 1);
                var i = Mathf.Min(stops.Length - 2, Mathf.FloorToInt(f));
                return Color.Lerp(stops[i], stops[i + 1], Mathf.SmoothStep(0f, 1f, f - i));
            }, TextureWrapMode.Clamp);
        }

        /// <summary>Flat mid-grey: a detail colour map that changes nothing, where only the detail normal is wanted.</summary>
        public static Texture2D Grey() => Make("grey", 4, 4, true, (x, y) => new Color(0.5f, 0.5f, 0.5f, 0.5f));

        /// <summary>A soft round spot, alpha only (a glow, a contact shade).</summary>
        public static Texture2D Spot() => Make("spot", 128, 128, false, (x, y) =>
        {
            float dx = (x - 63.5f) / 64f, dy = (y - 63.5f) / 64f;
            var a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
            return new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
        }, TextureWrapMode.Clamp);
    }

    /// <summary>The film's phone: a dark slab with a screen, and the companion's share steps drawn on it in the
    /// app's own colours. The screen's parts are laid out in metres from its centre; it faces the phone's -Z.</summary>
    public sealed class FilmPhone
    {
        public const float Width = 0.071f, Height = 0.147f, Depth = 0.0085f;
        public const float ScreenW = 0.064f, ScreenH = 0.138f;
        public readonly Transform Root, Screen;
        public readonly MeshRenderer Glass;
        readonly Material _glass;

        public FilmPhone(FilmStage s, Vector3 position, Quaternion rotation)
        {
            Root = new GameObject("Phone").transform;
            Root.SetParent(s.Root, false);
            Root.SetPositionAndRotation(position, rotation);
            var body = FilmStage.Mat(new Color(0.06f, 0.065f, 0.075f), 0.6f, 0.3f);
            s.Part("Body", FilmMesh.RoundedBox(new Vector3(Width, Height, Depth), 0.004f, 3), Vector3.zero, body, Root);
            _glass = FilmStage.Mat(new Color(0.012f, 0.014f, 0.018f), 0.9f, 0f, null, new Color(0.02f, 0.022f, 0.026f));
            Glass = s.Card("Glass", new Vector3(0f, 0f, -Depth * 0.5f - 0.0002f), new Vector2(ScreenW, ScreenH), Vector3.back, _glass, Root).GetComponent<MeshRenderer>();
            Screen = new GameObject("Screen").transform;
            Screen.SetParent(Root, false);
            Screen.localPosition = new Vector3(0f, 0f, -Depth * 0.5f - 0.0006f);
        }

        /// <summary>How brightly the dark glass itself glows (a lit screen lights a face a little).</summary>
        public void Lit(float amount) => _glass.SetColor("_EmissionColor", Color.Lerp(new Color(0.01f, 0.011f, 0.013f), new Color(0.2f, 0.19f, 0.17f), amount));

        public MeshRenderer Panel(string name, Vector2 centre, Vector2 size, Color fill, float radius = 0.004f, Color? border = null, int order = Ui.OrderElement, Transform parent = null)
        {
            // each layer a hair in front of the one under it (the solid background writes depth)
            var r = Ui.Panel(parent != null ? parent : Screen, name, size, fill, radius, new Vector3(centre.x, centre.y, -0.00012f * order), border ?? new Color(1, 1, 1, 0f), 0.0003f, order);
            return r;
        }

        public MeshRenderer Picture(string name, Vector2 centre, Vector2 size, Texture texture, float radius = 0.003f, Transform parent = null)
        {
            var r = Ui.Image(parent != null ? parent : Screen, name, size, texture, radius, new Vector3(centre.x, centre.y, -0.00012f));
            return r;
        }

        public TextMeshPro Text(string name, string text, Vector2 centre, float lineHeight, Color color, float width = 0.058f,
            TextAlignmentOptions align = TextAlignmentOptions.Center, FontStyles style = FontStyles.Normal, Transform parent = null)
        {
            var t = Ui.Text(parent != null ? parent : Screen, name, text, lineHeight / Theme.TextScale, color, width, align, new Vector3(centre.x, centre.y, 0f), style, lineHeight * 1.4f);
            t.ForceMeshUpdate(true, true);
            return t;
        }

        /// <summary>A point on the screen (metres from its centre), in the world.</summary>
        public Vector3 At(float x, float y, float above = 0f) => Screen.TransformPoint(new Vector3(x, y, -above));
        /// <summary>The way the screen faces.</summary>
        public Vector3 Normal => -Screen.forward;
    }

    public static class FilmProps
    {
        public static Material Res(string name)
        {
            var m = Resources.Load<Material>("Materials/" + name);
            if (m == null) Debug.LogWarning("[SameSky:film] no material " + name);
            return m;
        }

        /// <summary>One of the app's materials in another colour (its texture and relief kept).</summary>
        public static Material Tint(string name, Color colour, float? smoothness = null, Vector2? tiling = null)
        {
            var source = Res(name);
            var m = source != null ? new Material(source) : new Material(FilmStage.LitShader);
            m.name = name + " (film)";
            m.SetColor("_BaseColor", colour);
            if (smoothness.HasValue) m.SetFloat("_Smoothness", smoothness.Value);
            if (tiling.HasValue) m.SetTextureScale("_BaseMap", tiling.Value);
            return m;
        }

        // ------------------------------------------------------------ keys in a bowl
        public static Transform Keys(FilmStage s, Vector3 position, float yaw = 0f)
        {
            var root = new GameObject("Keys").transform;
            root.SetParent(s.Root, false);
            root.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var steel = FilmStage.Mat(new Color(0.72f, 0.72f, 0.7f), 0.75f, 1f);
            var brass = FilmStage.Mat(new Color(0.83f, 0.67f, 0.4f), 0.7f, 1f);
            var leather = FilmStage.Mat(new Color(0.36f, 0.2f, 0.12f), 0.35f);
            // the ring lies flat; two keys and a leather fob fan out from it
            s.Part("Ring", FilmMesh.Arc("key ring", 0.013f, 0f, 360f, 0.0022f, 0f, 0.0022f, 0.001f), new Vector3(0f, 0.001f, 0f), steel, root);
            for (var i = 0; i < 2; i++)
            {
                var key = new GameObject("Key " + i).transform;
                key.SetParent(root, false);
                key.localPosition = new Vector3(0.012f, 0.002f + i * 0.0025f, 0f);
                key.localRotation = Quaternion.Euler(0f, -24f + i * 46f, i * 7f);
                s.Part("Bow", FilmMesh.RoundedBox(new Vector3(0.022f, 0.0022f, 0.02f), 0.001f, 2), new Vector3(0.011f, 0f, 0f), i == 0 ? steel : brass, key);
                s.Part("Blade", FilmMesh.RoundedBox(new Vector3(0.034f, 0.0018f, 0.0075f), 0.0008f, 2), new Vector3(0.038f, 0f, 0f), i == 0 ? steel : brass, key);
            }
            var fob = new GameObject("Fob").transform;
            fob.SetParent(root, false);
            fob.localPosition = new Vector3(-0.012f, 0.003f, 0.004f);
            fob.localRotation = Quaternion.Euler(0f, 160f, 4f);
            s.Part("Leather", FilmMesh.RoundedBox(new Vector3(0.05f, 0.004f, 0.022f), 0.0018f, 3), new Vector3(0.03f, 0f, 0f), leather, fob);
            return root;
        }

        public static Transform Bowl(FilmStage s, Vector3 position, Material material = null)
        {
            var mesh = FilmMesh.Lathe("bowl", new[]
            {
                new Vector2(0f, 0.004f), new Vector2(0.04f, 0f), new Vector2(0.052f, 0.004f), new Vector2(0.092f, 0.04f), new Vector2(0.1f, 0.052f),
                new Vector2(0.097f, 0.054f), new Vector2(0.088f, 0.044f), new Vector2(0.05f, 0.012f), new Vector2(0f, 0.01f),
            });
            var go = s.Part("Bowl", mesh, position, material != null ? material : Tint("CeramicCream", new Color(0.86f, 0.8f, 0.7f), 0.5f));
            go.GetComponent<MeshRenderer>().sharedMaterial.SetFloat("_Cull", 0f);
            return go.transform;
        }

        // ------------------------------------------------------------ a takeaway cup
        public static Transform PaperCup(FilmStage s, Vector3 position)
        {
            var root = new GameObject("Paper cup").transform;
            root.SetParent(s.Root, false);
            root.position = position;
            var paper = FilmStage.Mat(new Color(0.93f, 0.9f, 0.84f), 0.25f);
            var sleeve = FilmStage.Mat(new Color(0.55f, 0.4f, 0.28f), 0.12f);
            var lid = FilmStage.Mat(new Color(0.16f, 0.15f, 0.14f), 0.45f);
            s.Part("Cup", FilmMesh.Lathe("paper cup", new[] { new Vector2(0f, 0f), new Vector2(0.029f, 0f), new Vector2(0.041f, 0.108f), new Vector2(0f, 0.108f) }), Vector3.zero, paper, root);
            s.Part("Sleeve", FilmMesh.Lathe("paper cup sleeve", new[] { new Vector2(0.0338f, 0.036f), new Vector2(0.0396f, 0.084f) }), Vector3.zero, sleeve, root);
            s.Part("Lid", FilmMesh.Lathe("paper cup lid", new[] { new Vector2(0f, 0.118f), new Vector2(0.034f, 0.118f), new Vector2(0.04f, 0.114f), new Vector2(0.0435f, 0.112f), new Vector2(0.0435f, 0.106f), new Vector2(0f, 0.106f) }), Vector3.zero, lid, root);
            return root;
        }

        // ------------------------------------------------------------ a compact camera
        public static Transform Camera(FilmStage s, Vector3 position, float yaw = 0f)
        {
            var root = new GameObject("Camera").transform;
            root.SetParent(s.Root, false);
            root.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var body = FilmStage.Mat(new Color(0.08f, 0.08f, 0.085f), 0.35f, 0.1f);
            var top = FilmStage.Mat(new Color(0.62f, 0.62f, 0.6f), 0.6f, 0.9f);
            var glass = FilmStage.Mat(new Color(0.02f, 0.03f, 0.05f), 0.95f, 0.2f);
            s.Part("Body", FilmMesh.RoundedBox(new Vector3(0.118f, 0.066f, 0.036f), 0.008f, 3), new Vector3(0f, 0.033f, 0f), body, root);
            s.Part("Top", FilmMesh.RoundedBox(new Vector3(0.118f, 0.012f, 0.036f), 0.005f, 3), new Vector3(0f, 0.068f, 0f), top, root);
            s.Cylinder("Lens", new Vector3(0.012f, 0.034f, -0.034f), 0.054f, 0.034f, body, root, new Vector3(90f, 0f, 0f));
            s.Cylinder("Lens ring", new Vector3(0.012f, 0.034f, -0.05f), 0.057f, 0.006f, top, root, new Vector3(90f, 0f, 0f));
            s.Cylinder("Lens glass", new Vector3(0.012f, 0.034f, -0.0525f), 0.04f, 0.003f, glass, root, new Vector3(90f, 0f, 0f));
            s.Cylinder("Dial", new Vector3(0.036f, 0.079f, 0.002f), 0.022f, 0.01f, top, root);
            s.Cylinder("Shutter", new Vector3(-0.036f, 0.077f, -0.004f), 0.013f, 0.007f, top, root);
            s.Part("Grip", FilmMesh.RoundedBox(new Vector3(0.02f, 0.06f, 0.014f), 0.006f, 3), new Vector3(-0.047f, 0.033f, -0.022f), body, root);
            return root;
        }

        // ------------------------------------------------------------ a headset
        /// <summary>A white standalone headset: visor, face pad, head strap. Its origin is between the eyes; it looks
        /// along +Z, as the head wearing it does.</summary>
        public static Transform Headset(FilmStage s, Vector3 position, Quaternion rotation)
        {
            var root = new GameObject("Headset").transform;
            root.SetParent(s.Root, false);
            root.SetPositionAndRotation(position, rotation);
            var shell = FilmStage.Mat(new Color(0.9f, 0.9f, 0.88f), 0.55f);
            var dark = FilmStage.Mat(new Color(0.07f, 0.07f, 0.075f), 0.5f);
            var foam = FilmStage.Mat(new Color(0.12f, 0.12f, 0.125f), 0.08f);
            var strap = FilmStage.Mat(new Color(0.8f, 0.8f, 0.78f), 0.2f);
            s.Part("Visor", FilmMesh.RoundedBox(new Vector3(0.178f, 0.094f, 0.07f), 0.03f, 5), new Vector3(0f, 0f, 0.075f), shell, root);
            s.Part("Front", FilmMesh.RoundedBox(new Vector3(0.15f, 0.07f, 0.012f), 0.006f, 3), new Vector3(0f, 0f, 0.108f), dark, root);
            s.Part("Face pad", FilmMesh.RoundedBox(new Vector3(0.158f, 0.084f, 0.03f), 0.014f, 4), new Vector3(0f, -0.002f, 0.03f), foam, root);
            for (var i = -1; i <= 1; i += 2)
                s.Cylinder("Lens " + i, new Vector3(0.033f * i, -0.002f, 0.014f), 0.044f, 0.004f, dark, root, new Vector3(90f, 0f, 0f));
            // the strap: round the back of the head, and one over the top
            s.Part("Strap", FilmMesh.Arc("headset strap", 0.104f, 58f, 302f, 0.004f, -0.016f, 0.016f, 0.0015f), new Vector3(0f, 0f, -0.07f), strap, root);
            return root;
        }

        // ------------------------------------------------------------ small furniture
        public static Transform Laptop(FilmStage s, Vector3 position, float yaw = 0f)
        {
            var root = new GameObject("Laptop").transform;
            root.SetParent(s.Root, false);
            root.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var alu = FilmStage.Mat(new Color(0.66f, 0.67f, 0.68f), 0.62f, 0.85f);
            s.Part("Base", FilmMesh.RoundedBox(new Vector3(0.31f, 0.008f, 0.215f), 0.0035f, 3), new Vector3(0f, 0.004f, 0f), alu, root);
            s.Part("Lid", FilmMesh.RoundedBox(new Vector3(0.31f, 0.006f, 0.215f), 0.0028f, 3), new Vector3(0f, 0.0115f, 0f), alu, root);
            return root;
        }

        /// <summary>A small stack of books, lying flat.</summary>
        public static Transform Books(FilmStage s, Vector3 position, float yaw, params Color[] colours)
        {
            var root = new GameObject("Books").transform;
            root.SetParent(s.Root, false);
            root.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var y = 0f;
            for (var i = 0; i < colours.Length; i++)
            {
                var thick = 0.022f + 0.008f * (i % 2);
                var size = new Vector3(0.2f - 0.014f * i, thick, 0.27f - 0.02f * i);
                var book = s.Part("Book " + i, FilmMesh.RoundedBox(size, 0.003f, 2), new Vector3(0f, y + thick * 0.5f, 0f), FilmStage.Mat(colours[i], 0.25f), root);
                book.transform.localRotation = Quaternion.Euler(0f, i * 7f - 5f, 0f);
                y += thick;
            }
            return root;
        }

        /// <summary>A leafy plant in a pot: a ceramic planter and a spray of leaves.</summary>
        public static Transform Plant(FilmStage s, Vector3 position, float scale = 1f, int seed = 4)
        {
            var root = new GameObject("Plant").transform;
            root.SetParent(s.Root, false);
            root.position = position;
            root.localScale = Vector3.one * scale;
            var pot = Tint("CeramicCream", new Color(0.83f, 0.78f, 0.7f), 0.4f);
            var leaf = FilmStage.Mat(new Color(0.2f, 0.36f, 0.2f), 0.35f);
            var leafDark = FilmStage.Mat(new Color(0.13f, 0.27f, 0.15f), 0.3f);
            s.Part("Pot", FilmMesh.Lathe("plant pot", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.055f, 0f), new Vector2(0.075f, 0.11f), new Vector2(0.078f, 0.12f), new Vector2(0.07f, 0.12f), new Vector2(0.066f, 0.105f), new Vector2(0f, 0.105f),
            }), Vector3.zero, pot, root);
            var rng = new System.Random(seed);
            for (var i = 0; i < 13; i++)
            {
                float turn = i * 137.5f, lean = 12f + 46f * (float)rng.NextDouble(), length = 0.14f + 0.14f * (float)rng.NextDouble();
                var stem = new GameObject("Leaf " + i).transform;
                stem.SetParent(root, false);
                stem.localPosition = new Vector3(0f, 0.1f, 0f);
                stem.localRotation = Quaternion.Euler(lean, turn, 0f);
                var blade = s.Shape(PrimitiveType.Sphere, "Blade", new Vector3(0f, length * 0.62f, 0f), new Vector3(0.06f + 0.03f * (float)rng.NextDouble(), length, 0.008f), i % 3 == 0 ? leafDark : leaf, stem);
                blade.transform.localRotation = Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 20f - 10f);
            }
            return root;
        }

        /// <summary>A standing lamp with a warm shade, and its light.</summary>
        public static Light FloorLamp(FilmStage s, Vector3 position, float intensity = 3f, float range = 4f, float height = 1.55f)
        {
            var root = new GameObject("Floor lamp").transform;
            root.SetParent(s.Root, false);
            root.position = position;
            var metal = FilmStage.Mat(new Color(0.16f, 0.15f, 0.14f), 0.5f, 0.6f);
            var shade = FilmStage.Mat(new Color(0.96f, 0.9f, 0.78f), 0.05f, 0f, null, new Color(0.75f, 0.5f, 0.26f));
            shade.SetFloat("_Cull", 0f);
            s.Cylinder("Base", new Vector3(0f, 0.012f, 0f), 0.26f, 0.024f, metal, root);
            s.Cylinder("Pole", new Vector3(0f, height * 0.5f, 0f), 0.022f, height, metal, root);
            s.Part("Shade", FilmMesh.Lathe("lamp shade", new[] { new Vector2(0.15f, 0f), new Vector2(0.11f, 0.24f) }), new Vector3(0f, height - 0.1f, 0f), shade, root, null, false);
            var light = s.Lamp("Lamp light", position + new Vector3(0f, height - 0.02f, 0.12f), new Color(1f, 0.78f, 0.52f), intensity, range, false);
            return light;
        }

        /// <summary>A framed picture on a wall, facing <paramref name="normal"/>.</summary>
        public static Transform WallPicture(FilmStage s, Vector3 centre, Vector3 normal, Vector2 size, Color a, Color b, Color c, Material frame = null)
        {
            var root = new GameObject("Picture").transform;
            root.SetParent(s.Root, false);
            root.SetPositionAndRotation(centre, Quaternion.LookRotation(-normal, Vector3.up));
            s.Part("Frame", FilmMesh.RoundedBox(new Vector3(size.x + 0.05f, size.y + 0.05f, 0.03f), 0.006f, 2), new Vector3(0f, 0f, 0.016f), frame != null ? frame : FilmStage.Mat(new Color(0.14f, 0.12f, 0.1f), 0.35f), root);
            var art = FilmTex.Gradient($"art {a} {b} {c}", a, b, c, b);
            var print = s.Shape(PrimitiveType.Quad, "Print", new Vector3(0f, 0f, -0.0005f), new Vector3(size.x, size.y, 1f), FilmStage.Mat(Color.white, 0.1f, 0f, art), root, null, false);
            return root;
        }
    }
}
