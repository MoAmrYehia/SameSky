// Building blocks for the sets of "Wish You Were Here" (FilmWishSets.cs): skies, water, city blocks with lit windows,
// lamps, strings of lights, cloth in the wind, stairs, furniture, cars and a boat. Everything is made here from
// simple shapes and textures drawn in code: the film uses no outside models or pictures for its places.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SameSky.Film
{
    public static class WishKit
    {
        static readonly Dictionary<string, Texture2D> Cache = new();

        static float Hash(int a, int b, int seed)
        {
            unchecked
            {
                var h = (uint)(a * 374761393 + b * 668265263 + seed * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        /// <summary>0 below a, 1 above b, smooth between (b may be below a: then it falls).</summary>
        public static float S(float a, float b, float x)
        {
            var t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        static Texture2D Make(string key, int w, int h, bool linear, bool hdr, Func<int, int, Color> pixel, TextureWrapMode wrap = TextureWrapMode.Repeat)
        {
            if (Cache.TryGetValue(key, out var had) && had != null) return had;
            var t = new Texture2D(w, h, hdr ? TextureFormat.RGBAHalf : TextureFormat.RGBA32, !hdr, linear || hdr)
            { name = key, wrapMode = wrap, anisoLevel = 8, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color[w * h];
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++) px[y * w + x] = pixel(x, y);
            t.SetPixels(px);
            t.Apply(!hdr, false);
            Cache[key] = t;
            return t;
        }

        // ------------------------------------------------------------ sky
        /// <summary>A sky as a latitude-longitude picture (HDR). <paramref name="shade"/> gives the colour for a
        /// direction (y up).</summary>
        public static Texture2D SkyPicture(string key, Func<Vector3, Color> shade, int w = 2048, int h = 1024)
        {
            var t = Make("sky " + key, w, h, true, true, (x, y) =>
            {
                var elevation = (y / (h - 1f) - 0.5f) * Mathf.PI;
                var lon = (0.5f - x / (float)w) * Mathf.PI * 2f;      // as Skybox/Panoramic reads it
                var c = shade(new Vector3(Mathf.Cos(lon) * Mathf.Cos(elevation), Mathf.Sin(elevation), Mathf.Sin(lon) * Mathf.Cos(elevation)));
                c.a = 1f;
                return c;
            }, TextureWrapMode.Clamp);
            t.wrapModeU = TextureWrapMode.Repeat;
            return t;
        }

        public static void Sky(Texture2D picture, float exposure = 1f)
        {
            var sky = new Material(Shader.Find("Skybox/Panoramic")) { name = "Film sky" };
            sky.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
            sky.SetFloat("_Mapping", 1f);
            sky.SetFloat("_ImageType", 0f);
            sky.SetTexture("_MainTex", picture);
            sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            sky.SetFloat("_Exposure", exposure);
            sky.SetFloat("_Rotation", 0f);
            RenderSettings.skybox = sky;
        }

        static Color Ramp(float e, Color horizon, Color low, Color mid, Color top)
        {
            if (e < 0.06f) return Color.Lerp(horizon, low, Mathf.SmoothStep(0f, 1f, e / 0.06f));
            if (e < 0.24f) return Color.Lerp(low, mid, Mathf.SmoothStep(0f, 1f, (e - 0.06f) / 0.18f));
            return Color.Lerp(mid, top, Mathf.SmoothStep(0f, 1f, (e - 0.24f) / 0.76f));
        }

        static float Clouds(float lon, float elevation, float seed)
        {
            float N(float l)
            {
                float u = l * 2.2f, v = elevation * 26f;
                return 0.55f * Mathf.PerlinNoise(u + 11.3f + seed, v + 3.1f) + 0.3f * Mathf.PerlinNoise(u * 2.3f + 5.7f, v * 2.1f + 9.2f + seed) + 0.15f * Mathf.PerlinNoise(u * 5.1f + 1.9f, v * 4.3f + 17.7f);
            }
            // the same pattern one turn on, blended in near the join so the picture wraps without a seam
            var n = Mathf.Lerp(N(lon), N(lon + Mathf.PI * 2f), S(-Mathf.PI * 0.6f, -Mathf.PI, lon));
            var band = S(0.02f, 0.09f, elevation) * S(0.62f, 0.3f, elevation);
            return S(0.5f, 0.68f, n) * band;
        }

        static float Stars(Vector3 dir, float density)
        {
            // a grid over the sphere; one star in some cells
            var lon = Mathf.Atan2(dir.z, dir.x);
            var lat = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f));
            int i = Mathf.FloorToInt(lon * 260f), j = Mathf.FloorToInt(lat * 260f);
            var r = Hash(i, j, 77);
            return r < density ? 0.4f + 2.2f * Hash(i, j, 91) * Hash(i, j, 13) : 0f;
        }

        /// <summary>The film's sky. The sun stands at <paramref name="sunAzimuth"/> (degrees from +Z toward +X) and
        /// <paramref name="sunHeight"/> (it may be below the horizon); the side of the sky away from it is as dark as
        /// <paramref name="night"/> says (0 a full sunset sky all round, 1 deep blue night with stars).</summary>
        public static Texture2D DuskSky(float sunAzimuth, float sunHeight, float night, float glow = 1f, float cloud = 0.85f, int w = 2048, float spread = 3f)
        {
            float az = sunAzimuth * Mathf.Deg2Rad, el = sunHeight * Mathf.Deg2Rad;
            var sun = new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
            var sunFlat = new Vector2(sun.x, sun.z).normalized;
            var up = Mathf.Clamp01(Mathf.InverseLerp(-8f, 6f, sunHeight));     // how much of the sun's light is left
            return SkyPicture($"dusk 2 {sunAzimuth:F1} {sunHeight:F1} {night:F2} {glow:F2} {cloud:F2} {w} {spread:F1}", dir =>
            {
                var elevation = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f));
                var e = Mathf.Clamp01(elevation / (Mathf.PI * 0.5f));
                var flat = new Vector2(dir.x, dir.z);
                flat = flat.sqrMagnitude > 1e-8f ? flat.normalized : sunFlat;
                var toward = Mathf.Clamp01(0.5f + 0.5f * Vector2.Dot(flat, sunFlat));   // 1 facing the sun
                var near = Mathf.Pow(toward, spread);
                var warm = Ramp(e, new Color(1.25f, 0.56f, 0.2f) * glow, new Color(1.0f, 0.4f, 0.24f) * glow, new Color(0.5f, 0.27f, 0.42f), new Color(0.08f, 0.1f, 0.3f));
                warm = Color.Lerp(Ramp(e, new Color(0.5f, 0.24f, 0.3f), new Color(0.3f, 0.18f, 0.36f), new Color(0.13f, 0.12f, 0.33f), new Color(0.04f, 0.05f, 0.18f)), warm, up);
                var dusk = Ramp(e, new Color(0.6f, 0.34f, 0.36f), new Color(0.44f, 0.29f, 0.44f), new Color(0.2f, 0.2f, 0.4f), new Color(0.06f, 0.08f, 0.22f));
                var dark = Ramp(e, new Color(0.2f, 0.15f, 0.3f), new Color(0.11f, 0.1f, 0.27f), new Color(0.05f, 0.06f, 0.19f), new Color(0.016f, 0.022f, 0.085f));
                var away = Color.Lerp(dusk, dark, night);
                var c = Color.Lerp(away, warm, near);
                if (elevation < 0f) c = Color.Lerp(c, Color.Lerp(new Color(0.16f, 0.1f, 0.1f), new Color(0.03f, 0.03f, 0.06f), night * (1f - near)), Mathf.Clamp01(-elevation * 6f));
                var angle = Vector3.Angle(dir, sun);
                c += new Color(1.5f, 0.62f, 0.2f) * Mathf.Exp(-angle * angle / (2f * 11f * 11f)) * 0.4f * glow * Mathf.Lerp(0.35f, 1f, up);
                if (sunHeight > -1.5f)
                {
                    c += new Color(3f, 1.7f, 0.7f) * Mathf.Exp(-angle * angle / (2f * 2.6f * 2.6f)) * 0.6f * glow;
                    if (angle < 1.25f) c += new Color(9f, 6.6f, 3.4f) * S(1.25f, 0.8f, angle);
                }
                var darkness = night * (1f - near) * S(0.03f, 0.3f, e);
                if (darkness > 0.3f) c += new Color(0.8f, 0.86f, 1f) * Stars(dir, 0.004f) * (darkness - 0.3f) * 0.5f;
                if (cloud > 0f && elevation > 0.02f && elevation < 0.62f)
                {
                    var lon = Mathf.Atan2(dir.z, dir.x);
                    var cover = Clouds(lon, elevation, 0f) * cloud;
                    var lit = Color.Lerp(Color.Lerp(new Color(0.36f, 0.26f, 0.36f), new Color(0.07f, 0.07f, 0.16f), night),
                        new Color(1.9f, 0.62f, 0.3f) * Mathf.Lerp(0.3f, 1f, up), Mathf.Sqrt(near) * S(0.5f, 0.04f, elevation));
                    c = Color.Lerp(c, lit, cover);
                }
                return c;
            }, w, w / 2);
        }

        // ------------------------------------------------------------ water
        /// <summary>A sheet of water at height <paramref name="y"/> that reflects the sky and the lights, its ripples
        /// drifting. Give the stage a reflection (FilmStage.Reflections) for it to have something to show.</summary>
        public static Material Water(FilmStage s, float y, Color deep, Vector3 centre, Vector2 size, float ripple = 0.035f, float mirror = 0.3f, float stretch = 3f)
        {
            var shader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>("Assets/SameSky/Editor/Film/FilmWater.shader");
            if (shader == null) throw new InvalidOperationException("the film's water shader is missing");
            var water = new Material(shader) { name = "Film water" };
            water.SetColor("_Deep", deep);
            water.SetTexture("_BumpMap", FilmTex.Ripples(256, 3));
            water.SetFloat("_Distort", ripple);
            water.SetFloat("_Mirror", mirror);
            water.SetFloat("_Stretch", stretch);
            var sheet = s.Shape(PrimitiveType.Quad, "Water", new Vector3(centre.x, y, centre.z), new Vector3(size.x, size.y, 1f), water, null, new Vector3(90f, 0f, 0f), false);
            sheet.layer = WaterLayer;

            // the world upside down, drawn before each frame from where the film camera would be under the surface
            var go = new GameObject("Water mirror camera");
            go.transform.SetParent(s.Root, false);
            var mirrorCam = go.AddComponent<Camera>();
            mirrorCam.enabled = false;
            RenderTexture picture = null;
            var flip = Matrix4x4.identity;
            flip.m11 = -1f;
            flip.m13 = 2f * y;
            s.Draws.Add(() =>
            {
                var main = s.Camera.Cam;
                var target = main.targetTexture;
                int w = target != null ? Mathf.Max(960, target.width / 2) : 960, h = target != null ? Mathf.Max(540, target.height / 2) : 540;
                if (picture == null || picture.width != w || picture.height != h)
                {
                    if (picture != null) UnityEngine.Object.DestroyImmediate(picture);
                    picture = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR) { name = "Water mirror", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                    picture.Create();
                }
                mirrorCam.CopyFrom(main);
                mirrorCam.enabled = false;
                mirrorCam.targetTexture = picture;
                mirrorCam.cullingMask = main.cullingMask & ~(1 << WaterLayer);
                var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(mirrorCam);
                data.renderPostProcessing = false;
                data.renderShadows = false;
                data.requiresDepthTexture = false;
                data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
                var p = main.transform.position;
                mirrorCam.transform.position = new Vector3(p.x, 2f * y - p.y, p.z);
                mirrorCam.worldToCameraMatrix = main.worldToCameraMatrix * flip;
                mirrorCam.projectionMatrix = main.projectionMatrix;
                mirrorCam.cullingMatrix = mirrorCam.projectionMatrix * mirrorCam.worldToCameraMatrix;
                var was = GL.invertCulling;
                GL.invertCulling = true;
                try { mirrorCam.Render(); }
                finally { GL.invertCulling = was; }
                mirrorCam.targetTexture = null;
                Shader.SetGlobalTexture("_FilmWaterReflection", picture);
            });
            return water;
        }

        /// <summary>The layer the water is on: the mirror camera leaves it out.</summary>
        public const int WaterLayer = 4;

        /// <summary>A light's broken reflection lying on the water: a strip of glints from <paramref name="from"/>
        /// toward <paramref name="toward"/>.</summary>
        public static void LightRoad(FilmStage s, Vector3 from, Vector3 toward, float length, float width, Color colour, float speed = 0.4f, int seed = 4)
        {
            var mesh = new Mesh { name = "Light road", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = new[] { new Vector3(-width * 0.5f, 0f, 0f), new Vector3(width * 0.5f, 0f, 0f), new Vector3(-width * 1.6f, 0f, length), new Vector3(width * 1.6f, 0f, length) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, length / 7f), new Vector2(1f, length / 7f) };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var glints = FilmStage.Additive(colour, FilmTex.Sparkle(seed));
            var road = s.Part("Light road", mesh, from, glints, null, null, false);
            var flat = new Vector3(toward.x - from.x, 0f, toward.z - from.z);
            road.transform.rotation = Quaternion.LookRotation(flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.forward, Vector3.up);
            var phase = seed * 0.37f;
            s.Moves.Add(t => glints.SetTextureOffset("_BaseMap", new Vector2(0f, phase + t * speed)));
        }

        // ------------------------------------------------------------ buildings
        /// <summary>A building front: the wall with its windows (to be tinted by the material), and the windows that
        /// are lit (what glows).</summary>
        public static (Texture2D wall, Texture2D glow) Facade(int cols, int rows, int seed, float lit)
        {
            const int cell = 32;
            cols = Mathf.Clamp(cols, 1, 24);
            rows = Mathf.Clamp(rows, 1, 30);
            bool Window(int lx, int ly) => lx > 8 && lx < 24 && ly > 6 && ly < 26;
            var wall = Make($"facade wall {cols} {rows} {seed}", cols * cell, rows * cell, false, false, (x, y) =>
            {
                int i = x / cell, j = y / cell, lx = x % cell, ly = y % cell;
                if (Window(lx, ly)) return new Color(0.1f, 0.11f, 0.13f, 1f);
                var frame = lx > 6 && lx < 26 && ly > 4 && ly < 28;
                var v = 0.86f + 0.1f * Hash(i, j, seed) + 0.04f * Mathf.PerlinNoise(x * 0.21f, y * 0.21f);
                if (frame) v *= 1.12f;
                if (ly < 3) v *= 0.8f;          // the line of a floor
                return new Color(v, v, v, 1f);
            }, TextureWrapMode.Clamp);
            var glow = Make($"facade glow {cols} {rows} {seed} {lit:F2}", cols * cell, rows * cell, false, false, (x, y) =>
            {
                int i = x / cell, j = y / cell, lx = x % cell, ly = y % cell;
                if (!Window(lx, ly) || Hash(i, j, seed + 1) > lit) return Color.black;
                var kind = Hash(i, j, seed + 2);
                var colour = kind < 0.62f ? new Color(1f, 0.72f, 0.4f) : kind < 0.86f ? new Color(1f, 0.58f, 0.28f) : kind < 0.94f ? new Color(0.55f, 0.7f, 1f) : new Color(1f, 0.86f, 0.66f);
                var power = 0.35f + 0.65f * Hash(i, j, seed + 3);
                var curtain = Hash(i, j, seed + 4) < 0.4f ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(lx * 1.3f)) : 1f;
                var sill = ly < 12 && Hash(i, j, seed + 5) < 0.3f ? 0.45f : 1f;      // something dark in the lower half: a plant, a sofa back
                var bar = lx == 16 || ly == 16 ? 0.35f : 1f;
                return colour * (power * curtain * sill * bar);
            }, TextureWrapMode.Clamp);
            return (wall, glow);
        }

        /// <summary>A city block: four fronts with windows, a flat roof with a parapet and the things that stand on
        /// roofs. <paramref name="centre"/> is the middle of its footprint on the ground.</summary>
        public static Transform Block(FilmStage s, string name, Vector3 centre, Vector3 size, Color wall, int seed, float lit = 0.4f, float glow = 1.6f, float storey = 3.1f, float bay = 2.4f, bool roof = true)
        {
            var root = new GameObject(name).transform;
            root.SetParent(s.Root, false);
            root.position = centre;
            var plain = FilmStage.Mat(wall * 0.7f, 0.12f);
            s.Box("Core", new Vector3(0f, size.y * 0.5f, 0f), new Vector3(size.x - 0.06f, size.y, size.z - 0.06f), plain, root);
            int rows = Mathf.Max(1, Mathf.RoundToInt(size.y / storey));
            void Front(Vector3 at, float width, Vector3 normal, int salt)
            {
                var (wallTex, glowTex) = Facade(Mathf.Max(1, Mathf.RoundToInt(width / bay)), rows, seed + salt, lit);
                var m = FilmStage.Mat(wall, 0.18f, 0f, wallTex, Color.white * glow);
                m.SetTexture("_EmissionMap", glowTex);
                s.Card("Front", at, new Vector2(width, size.y), normal, m, root, true);
            }
            Front(new Vector3(0f, size.y * 0.5f, size.z * 0.5f), size.x, Vector3.forward, 0);
            Front(new Vector3(0f, size.y * 0.5f, -size.z * 0.5f), size.x, Vector3.back, 11);
            Front(new Vector3(size.x * 0.5f, size.y * 0.5f, 0f), size.z, Vector3.right, 23);
            Front(new Vector3(-size.x * 0.5f, size.y * 0.5f, 0f), size.z, Vector3.left, 37);
            if (!roof) return root;
            var dark = FilmStage.Mat(wall * 0.45f, 0.1f);
            s.Box("Roof", new Vector3(0f, size.y + 0.1f, 0f), new Vector3(size.x + 0.3f, 0.2f, size.z + 0.3f), dark, root);
            foreach (var (dx, dz, w, d) in new[] { (0f, 0.5f, size.x + 0.3f, 0.25f), (0f, -0.5f, size.x + 0.3f, 0.25f), (0.5f, 0f, 0.25f, size.z + 0.3f), (-0.5f, 0f, 0.25f, size.z + 0.3f) })
                s.Box("Parapet", new Vector3(dx * size.x, size.y + 0.55f, dz * size.z), new Vector3(w, 0.7f, d), dark, root);
            var things = 1 + (int)(Hash(seed, 3, 5) * 3.99f);
            for (var i = 0; i < things; i++)
            {
                float fx = Hash(seed, i, 7) - 0.5f, fz = Hash(seed, i, 8) - 0.5f, h = 0.8f + 1.8f * Hash(seed, i, 9);
                s.Box("Roof box", new Vector3(fx * (size.x - 3f), size.y + 0.2f + h * 0.5f, fz * (size.z - 3f)), new Vector3(1.4f + 2f * Hash(seed, i, 10), h, 1.2f + 1.6f * Hash(seed, i, 12)), dark, root);
            }
            return root;
        }

        // ------------------------------------------------------------ lights
        /// <summary>The colour of a lamp's glass: bright enough to bloom.</summary>
        public static Material Bulb(Color colour, float power = 3f) => FilmStage.Glow(new Color(colour.r * power, colour.g * power, colour.b * power, 1f));

        public static void StreetLamp(FilmStage s, Vector3 foot, float height, Material iron, Material glass, Transform parent = null, float reach = 0f, Vector3? toward = null, Color? light = null, float power = 0f, float range = 11f)
        {
            if (power > 0f) s.Lamp("Street lamp light", foot + Vector3.up * (height - 0.25f) + (toward ?? Vector3.zero).normalized * reach, light ?? new Color(1f, 0.74f, 0.48f), power, range);
            var top = foot + Vector3.up * height;
            s.Rod("Lamp post", foot, top, 0.09f, iron, parent);
            var head = top;
            if (reach > 0f)
            {
                head = top + (toward ?? Vector3.forward).normalized * reach + Vector3.up * 0.15f;
                s.Rod("Lamp arm", top, head, 0.06f, iron, parent);
            }
            s.Ball("Lamp glass", head + Vector3.up * 0.12f, 0.36f, glass, parent, false);
        }

        /// <summary>A string of small lights hanging from a to b.</summary>
        public static void StringLights(FilmStage s, Vector3 a, Vector3 b, float sag, int bulbs, Material glass, Material wire = null, float size = 0.075f, Transform parent = null)
        {
            Vector3 At(float k) => Vector3.Lerp(a, b, k) + Vector3.down * (sag * 4f * k * (1f - k));
            for (var i = 0; i < bulbs; i++) s.Ball("Bulb", At((i + 0.5f) / bulbs), size, glass, parent, false);
            if (wire == null) return;
            const int parts = 8;
            for (var i = 0; i < parts; i++) s.Rod("Wire", At(i / (float)parts), At((i + 1f) / parts), 0.012f, wire, parent);
        }

        // ------------------------------------------------------------ cloth in the wind
        /// <summary>A sheet of cloth hanging from its top edge (a curtain, a banner, washing on a line), moving in the
        /// wind. <paramref name="origin"/> is the middle of the top edge, <paramref name="along"/> the direction of
        /// that edge; the cloth hangs <paramref name="drop"/> metres.</summary>
        public static GameObject Cloth(FilmStage s, string name, Vector3 origin, Vector3 along, float width, float drop, Material material, float wind = 1f, float seed = 0f, Transform parent = null, float gather = 0f)
        {
            const int nx = 14, ny = 12;
            var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic();
            var verts = new Vector3[(nx + 1) * (ny + 1)];
            var uv = new Vector2[verts.Length];
            var tris = new int[nx * ny * 6];
            for (var j = 0; j <= ny; j++)
                for (var i = 0; i <= nx; i++) uv[j * (nx + 1) + i] = new Vector2(i / (float)nx, 1f - j / (float)ny);
            var k = 0;
            for (var j = 0; j < ny; j++)
                for (var i = 0; i < nx; i++)
                {
                    int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                    tris[k++] = a; tris[k++] = b; tris[k++] = c; tris[k++] = b; tris[k++] = d; tris[k++] = c;
                }
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : s.Root, false);
            go.transform.position = origin;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            material.SetFloat("_Cull", 0f);
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            var right = along.normalized;
            var push = Vector3.Cross(Vector3.up, right).normalized;       // the way the wind bellies the cloth
            var first = true;
            void Pose(float t)
            {
                for (var j = 0; j <= ny; j++)
                {
                    var v = j / (float)ny;                                  // 0 at the top, where it is held
                    for (var i = 0; i <= nx; i++)
                    {
                        var u = i / (float)nx - 0.5f;
                        var free = v * v;
                        var wave = Mathf.Sin(t * 2.1f + u * 5.3f + v * 3.1f + seed) * 0.06f + Mathf.Sin(t * 3.7f - u * 9.1f + v * 5.7f + seed * 2f) * 0.03f + Mathf.Sin(t * 0.9f + seed * 3f) * 0.08f;
                        var belly = (0.1f + wave) * wind * free * drop;
                        var folds = gather > 0f ? Mathf.Sin(u * 22f) * gather * (0.3f + 0.7f * v) : 0f;
                        var sway = Mathf.Sin(t * 1.3f + seed + v * 2f) * 0.035f * wind * free * drop;
                        verts[j * (nx + 1) + i] = right * (u * width * (1f - 0.04f * free * wind) + sway) + Vector3.down * (v * drop * (1f - 0.03f * wind * free)) + push * (belly + folds);
                    }
                }
                mesh.vertices = verts;
                if (first) { mesh.uv = uv; mesh.triangles = tris; first = false; }
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            }
            Pose(0f);
            s.Moves.Add(Pose);
            return go;
        }

        // ------------------------------------------------------------ stairs, walls, rails
        /// <summary>A straight flight of stairs climbing along <paramref name="run"/> from <paramref name="foot"/>
        /// (the middle of the lowest edge). Returns the top landing's near edge.</summary>
        public static Vector3 Stairs(FilmStage s, string name, Vector3 foot, Vector3 run, int steps, float rise, float tread, float width, Material stone, Transform parent = null, float wall = 0f, Material wallMaterial = null)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent != null ? parent : s.Root, false);
            root.position = foot;
            root.rotation = Quaternion.LookRotation(new Vector3(run.x, 0f, run.z).normalized, Vector3.up);
            for (var i = 0; i < steps; i++)
                s.Box("Step", new Vector3(0f, rise * (i + 1) * 0.5f, tread * (i + 0.5f)), new Vector3(width, rise * (i + 1), tread), stone, root);
            if (wall > 0f)
            {
                // a solid wall up each side, following the slope
                var slope = Mathf.Atan2(rise, tread) * Mathf.Rad2Deg;
                var length = new Vector2(steps * tread, steps * rise).magnitude + 0.6f;
                foreach (var side in new[] { -1f, 1f })
                    s.Box("Stair wall", new Vector3(side * (width * 0.5f + 0.16f), steps * rise * 0.5f + wall * 0.5f - 0.1f, steps * tread * 0.5f), new Vector3(0.32f, wall + 0.5f, length), wallMaterial ?? stone, root, new Vector3(-slope, 0f, 0f));
            }
            return root.TransformPoint(new Vector3(0f, steps * rise, steps * tread));
        }

        /// <summary>A railing from a to b: posts, a top rail and thin bars.</summary>
        public static void Railing(FilmStage s, Vector3 a, Vector3 b, float height, Material iron, float spacing = 1.6f, Transform parent = null, bool bars = true)
        {
            var length = (b - a).magnitude;
            var posts = Mathf.Max(1, Mathf.RoundToInt(length / spacing));
            for (var i = 0; i <= posts; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)posts);
                s.Rod("Post", p, p + Vector3.up * height, 0.045f, iron, parent);
            }
            s.Rod("Top rail", a + Vector3.up * height, b + Vector3.up * height, 0.06f, iron, parent);
            if (!bars) return;
            s.Rod("Mid rail", a + Vector3.up * height * 0.55f, b + Vector3.up * height * 0.55f, 0.028f, iron, parent);
            s.Rod("Low rail", a + Vector3.up * height * 0.14f, b + Vector3.up * height * 0.14f, 0.028f, iron, parent);
        }

        // ------------------------------------------------------------ furniture and street things
        /// <summary>A café chair. Returns its root; the seat's top is at <see cref="SeatHeight"/>.</summary>
        public const float SeatHeight = 0.46f;
        public static Transform Chair(FilmStage s, Vector3 position, float yaw, Material frame, Material seat, Transform parent = null)
        {
            var root = new GameObject("Chair").transform;
            root.SetParent(parent != null ? parent : s.Root, false);
            root.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            s.Soft("Seat", new Vector3(0f, SeatHeight - 0.025f, 0f), new Vector3(0.46f, 0.05f, 0.44f), 0.018f, seat, root);
            s.Soft("Back", new Vector3(0f, SeatHeight + 0.3f, -0.215f), new Vector3(0.44f, 0.4f, 0.035f), 0.014f, seat, root, new Vector3(-8f, 0f, 0f));
            foreach (var (x, z) in new[] { (-0.2f, -0.19f), (0.2f, -0.19f), (-0.2f, 0.19f), (0.2f, 0.19f) })
                s.Rod("Leg", new Vector3(x, 0f, z), new Vector3(x * 0.92f, SeatHeight - 0.04f, z * 0.92f), 0.03f, frame, root);
            foreach (var x in new[] { -0.2f, 0.2f })
                s.Rod("Back post", new Vector3(x * 0.92f, SeatHeight - 0.04f, -0.175f), new Vector3(x, SeatHeight + 0.5f, -0.245f), 0.03f, frame, root);
            return root;
        }

        public static Transform RoundTable(FilmStage s, Vector3 position, float diameter, float height, Material top, Material leg, Transform parent = null)
        {
            var root = new GameObject("Table").transform;
            root.SetParent(parent != null ? parent : s.Root, false);
            root.position = position;
            s.Cylinder("Top", new Vector3(0f, height - 0.015f, 0f), diameter, 0.03f, top, root);
            s.Rod("Leg", new Vector3(0f, 0.02f, 0f), new Vector3(0f, height - 0.03f, 0f), 0.06f, leg, root);
            s.Cylinder("Foot", new Vector3(0f, 0.012f, 0f), diameter * 0.55f, 0.024f, leg, root);
            return root;
        }

        /// <summary>A bench for three: slats on two ends. The seat's top is at <see cref="SeatHeight"/>.</summary>
        public static Transform Bench(FilmStage s, Vector3 position, float yaw, Material wood, Material iron, float width = 1.9f, Transform parent = null)
        {
            var root = new GameObject("Bench").transform;
            root.SetParent(parent != null ? parent : s.Root, false);
            root.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            for (var i = 0; i < 4; i++)
                s.Soft("Seat slat", new Vector3(0f, SeatHeight - 0.016f, -0.17f + i * 0.108f), new Vector3(width, 0.032f, 0.092f), 0.008f, wood, root);
            for (var i = 0; i < 3; i++)
                s.Soft("Back slat", new Vector3(0f, SeatHeight + 0.2f + i * 0.135f, -0.255f - i * 0.03f), new Vector3(width, 0.095f, 0.028f), 0.008f, wood, root, new Vector3(-12f, 0f, 0f));
            foreach (var x in new[] { -width * 0.5f + 0.04f, width * 0.5f - 0.04f })
            {
                s.Soft("End", new Vector3(x, SeatHeight * 0.5f - 0.01f, -0.02f), new Vector3(0.05f, SeatHeight - 0.02f, 0.46f), 0.012f, iron, root);
                s.Soft("Back post", new Vector3(x, SeatHeight + 0.3f, -0.28f), new Vector3(0.05f, 0.66f, 0.05f), 0.012f, iron, root, new Vector3(-12f, 0f, 0f));
            }
            return root;
        }

        /// <summary>A shop front on a wall: a lit window, a sign band and an awning over it.</summary>
        public static void Shop(FilmStage s, Vector3 foot, Vector3 normal, float width, Color light, Color awning, Transform parent = null, float power = 1.15f, bool sign = true, float spill = 0f)
        {
            var n = normal.normalized;
            if (spill > 0f) s.Lamp("Shop light", foot + Vector3.up * 1.9f + n * 1.6f, light, spill, 8.5f);
            var across = Vector3.Cross(Vector3.up, n).normalized;
            s.Card("Shop window", foot + Vector3.up * 1.45f + n * 0.09f, new Vector2(width, 2.5f), n, FilmStage.Glow(new Color(light.r * power, light.g * power, light.b * power, 1f), ShopGlass((int)(foot.x * 3f + foot.z * 7f))), parent);
            var frame = FilmStage.Mat(new Color(0.08f, 0.08f, 0.09f), 0.3f);
            s.Box("Sill", foot + Vector3.up * 0.14f + n * 0.05f, Abs(across * width + Vector3.up * 0.28f + n * 0.1f), frame, parent);
            if (sign) s.Card("Sign", foot + Vector3.up * 3.08f + n * 0.1f, new Vector2(width * 0.42f, 0.22f), n, FilmStage.Glow(new Color(light.b * 2.2f + 0.4f, light.g * 1.1f, light.r * 1.4f, 1f)), parent);
            var cloth = FilmStage.Mat(awning, 0.15f);
            var tilt = Quaternion.LookRotation(n, Vector3.up) * Quaternion.Euler(18f, 0f, 0f);
            var go = s.Box("Awning", foot + Vector3.up * 2.72f + n * 0.75f, new Vector3(width + 0.3f, 0.05f, 1.5f), cloth, parent);
            go.transform.rotation = tilt;
        }

        static Vector3 Abs(Vector3 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        /// <summary>What a lit shop window shows from the street: shelves and shapes, soft and out of focus.</summary>
        public static Texture2D ShopGlass(int seed)
        {
            seed = Mathf.Abs(seed) % 6;
            var panes = 3 + seed % 3;
            return Make("shop glass 2 " + seed, 256, 128, false, false, (x, y) =>
            {
                float u = x / 256f, v = y / 128f;
                // the room behind the glass: lit from its ceiling, darker toward the floor, deeper in the middle
                var room = 0.16f + 0.5f * S(0.1f, 0.95f, v) + 0.12f * Mathf.PerlinNoise(u * 3f + seed, v * 2f);
                // lamps hanging in it
                var lamps = 0f;
                for (var i = 0; i < 5; i++)
                {
                    float lu = (i + 0.5f + 0.3f * (Hash(i, seed, 41) - 0.5f)) / 5f, lv = 0.8f + 0.08f * Hash(i, seed, 42);
                    float du = (u - lu) * 2.2f, dv = v - lv;
                    lamps += Mathf.Exp(-(du * du + dv * dv) * 260f) * 1.1f;
                }
                // shelves and what stands on them; a counter; a figure or two
                var cell = Mathf.FloorToInt(u * 22f);
                var shelf = 0f;
                foreach (var sv in new[] { 0.3f, 0.47f, 0.63f })
                {
                    var on = Hash(cell / 5, (int)(sv * 100f), seed + 43) < 0.7f;
                    if (!on) continue;
                    if (v > sv - 0.012f && v < sv) shelf = 1f;                                              // the board
                    var tall = 0.03f + 0.09f * Hash(cell, (int)(sv * 100f), seed + 44);
                    if (v >= sv && v < sv + tall && Hash(cell, (int)(sv * 100f), seed + 45) < 0.75f) shelf = 0.55f + 0.4f * Hash(cell, 3, seed + 46);
                }
                var counter = v < 0.2f ? 0.7f : 0f;
                var figure = 0f;
                for (var i = 0; i < 2; i++)
                {
                    var fu = 0.2f + 0.6f * Hash(i, seed, 47);
                    var body = Mathf.Abs(u - fu) < 0.022f && v > 0.2f && v < 0.52f;
                    var head = (u - fu) * (u - fu) * 4f + (v - 0.56f) * (v - 0.56f) < 0.0009f;
                    if ((body || head) && Hash(i, seed, 48) < 0.6f) figure = 0.85f;
                }
                var dark = Mathf.Max(Mathf.Max(shelf, counter), figure);
                var c = Mathf.Lerp(room, room * 0.22f, dark) + lamps;
                // the frame of the window: mullions, a rail near the top, a sill
                var fu2 = u * panes - Mathf.Floor(u * panes);
                if (fu2 < 0.035f || fu2 > 0.965f || (v > 0.74f && v < 0.765f) || v < 0.03f || v > 0.975f) c *= 0.12f;
                return new Color(c, c * (0.92f + 0.06f * Mathf.PerlinNoise(u * 4f, seed)), c * 0.8f, 1f);
            }, TextureWrapMode.Clamp);
        }

        /// <summary>A shape cut from a side view and given a width: <paramref name="profile"/> is its outline seen
        /// from the side (x is along the length, y up), going round once; the solid is that outline pushed out to
        /// <paramref name="width"/>, flat-faced. The outline has to bulge outward everywhere (no dents).</summary>
        public static Mesh Extrude(string name, Vector2[] profile, float width)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var h = width * 0.5f;
            int n = profile.Length;
            var middle = Vector2.zero;
            foreach (var q in profile) middle += q / n;
            var centre = new Vector3(0f, middle.y, middle.x);
            // a triangle, wound so that it faces away from the middle of the solid
            void Face(Vector3 a, Vector3 b, Vector3 c)
            {
                var v = verts.Count;
                var outward = Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - centre) >= 0f;
                verts.Add(a); verts.Add(outward ? b : c); verts.Add(outward ? c : b);
                tris.Add(v); tris.Add(v + 1); tris.Add(v + 2);
            }
            for (var i = 0; i < n; i++)
            {
                Vector2 p0 = profile[i], p1 = profile[(i + 1) % n];
                Vector3 a = new(-h, p0.y, p0.x), b = new(h, p0.y, p0.x), c = new(-h, p1.y, p1.x), d = new(h, p1.y, p1.x);
                Face(a, b, c);
                Face(b, d, c);
            }
            foreach (var x in new[] { -h, h })
                for (var i = 1; i < n - 1; i++)
                    Face(new Vector3(x, profile[0].y, profile[0].x), new Vector3(x, profile[i].y, profile[i].x), new Vector3(x, profile[i + 1].y, profile[i + 1].x));
            var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh _carBody, _carGlass, _carRoof;

        /// <summary>A lit room seen through a glazed door at night: warm, brighter toward the ceiling, with the
        /// door's own bars across it.</summary>
        public static Texture2D GlazedDoor()
        {
            return Make("glazed door", 128, 256, false, false, (x, y) =>
            {
                float u = x / 128f, v = y / 256f;
                var room = 0.3f + 0.55f * S(0.05f, 0.95f, v) + 0.08f * Mathf.PerlinNoise(u * 3f, v * 4f);
                var lamp = Mathf.Exp(-((u - 0.68f) * (u - 0.68f) * 30f + (v - 0.7f) * (v - 0.7f) * 16f)) * 0.5f;
                var c = room + lamp;
                var bar = Mathf.Abs(u - 0.5f) < 0.025f || Mathf.Abs(v - 0.34f) < 0.012f || Mathf.Abs(v - 0.67f) < 0.012f || u < 0.04f || u > 0.96f || v < 0.02f || v > 0.98f;
                if (bar) c *= 0.1f;
                return new Color(c, c * 0.93f, c * 0.82f, 1f);
            }, TextureWrapMode.Clamp);
        }

        /// <summary>A car: a body, a cabin, wheels, and its lights (on).</summary>
        public static Transform Car(FilmStage s, Vector3 position, float yaw, Color paint, Transform parent = null, float brake = 3f)
        {
            if (_carBody == null)
            {
                _carBody = Extrude("Car body", new[] { new Vector2(-2.12f, 0.3f), new Vector2(-2.18f, 0.6f), new Vector2(-2.08f, 0.8f), new Vector2(-1.5f, 0.88f), new Vector2(1.15f, 0.86f), new Vector2(2.0f, 0.74f), new Vector2(2.16f, 0.56f), new Vector2(2.1f, 0.3f) }, 1.78f);
                _carGlass = Extrude("Car glass", new[] { new Vector2(-1.52f, 0.86f), new Vector2(-0.98f, 1.33f), new Vector2(0.36f, 1.35f), new Vector2(1.1f, 0.86f) }, 1.6f);
                _carRoof = Extrude("Car roof", new[] { new Vector2(-1.04f, 1.31f), new Vector2(-1.0f, 1.39f), new Vector2(0.4f, 1.41f), new Vector2(0.46f, 1.33f) }, 1.64f);
            }
            var root = new GameObject("Car").transform;
            root.SetParent(parent != null ? parent : s.Root, false);
            root.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var body = FilmStage.Mat(paint, 0.5f, 0f);
            var glass = FilmStage.Mat(new Color(0.03f, 0.035f, 0.045f), 0.92f, 0.1f);
            var tyre = FilmStage.Mat(new Color(0.03f, 0.03f, 0.03f), 0.2f);
            s.Part("Body", _carBody, Vector3.zero, body, root);
            s.Part("Cabin", _carGlass, Vector3.zero, glass, root);
            s.Part("Roof", _carRoof, Vector3.zero, body, root);
            foreach (var (x, z) in new[] { (-0.84f, 1.32f), (0.84f, 1.32f), (-0.84f, -1.32f), (0.84f, -1.32f) })
                s.Cylinder("Wheel", new Vector3(x, 0.33f, z), 0.66f, 0.24f, tyre, root, new Vector3(0f, 0f, 90f));
            var red = FilmStage.Glow(new Color(brake * 1.3f, 0.07f * brake, 0.03f * brake, 1f));
            var white = FilmStage.Glow(new Color(3.4f, 3f, 2.4f, 1f));
            s.Box("Tail light", new Vector3(0f, 0.7f, -2.17f), new Vector3(1.52f, 0.09f, 0.08f), red, root, null, false);
            foreach (var x in new[] { -0.6f, 0.6f })
                s.Box("Head light", new Vector3(x, 0.64f, 2.13f), new Vector3(0.34f, 0.1f, 0.08f), white, root, null, false);
            return root;
        }

        /// <summary>A small harbour boat with a lit cabin and a string of lights.</summary>
        public static Transform Boat(FilmStage s, Vector3 position, float yaw, Transform parent = null)
        {
            var root = new GameObject("Boat").transform;
            root.SetParent(parent != null ? parent : s.Root, false);
            root.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var hull = FilmStage.Mat(new Color(0.12f, 0.16f, 0.24f), 0.5f);
            var white = FilmStage.Mat(new Color(0.8f, 0.78f, 0.72f), 0.4f);
            s.Soft("Hull", new Vector3(0f, 0.55f, 0f), new Vector3(4.2f, 1.5f, 15f), 0.5f, hull, root);
            s.Soft("Deck house", new Vector3(0f, 2.05f, -0.8f), new Vector3(3.4f, 1.7f, 8.4f), 0.2f, white, root);
            var lit = FilmStage.Glow(new Color(3.4f, 2.3f, 1.1f, 1f));
            foreach (var side in new[] { -1f, 1f })
                for (var i = 0; i < 6; i++)
                    s.Card("Cabin window", new Vector3(side * 1.715f, 2.2f, -4.1f + i * 1.3f), new Vector2(0.9f, 0.7f), new Vector3(side, 0f, 0f), lit, root);
            s.Rod("Mast", new Vector3(0f, 2.9f, 2.2f), new Vector3(0f, 6.2f, 2.2f), 0.09f, white, root);
            var bulbs = Bulb(new Color(1f, 0.7f, 0.36f), 3.4f);
            StringLights(s, new Vector3(0f, 1.6f, 7.2f), new Vector3(0f, 6.2f, 2.2f), 0.5f, 9, bulbs, null, 0.16f, root);
            StringLights(s, new Vector3(0f, 6.2f, 2.2f), new Vector3(0f, 3.1f, -6.8f), 0.6f, 12, bulbs, null, 0.16f, root);
            s.Ball("Port light", new Vector3(-1.9f, 2.6f, 3.6f), 0.2f, FilmStage.Glow(new Color(4f, 0.2f, 0.15f, 1f)), root, false);
            s.Ball("Starboard light", new Vector3(1.9f, 2.6f, 3.6f), 0.2f, FilmStage.Glow(new Color(0.2f, 3.6f, 0.6f, 1f)), root, false);
            return root;
        }

        /// <summary>A tree: a leaning trunk and a crown of soft balls.</summary>
        public static void Tree(FilmStage s, Vector3 foot, float height, float crown, Material trunk, Material leaves, Material leavesLit, int seed, Transform parent = null)
        {
            var top = foot + new Vector3(0.25f * (Hash(seed, 1, 1) - 0.5f) * height, height, 0.2f * (Hash(seed, 2, 1) - 0.5f) * height);
            s.Rod("Trunk", foot, top, 0.12f + height * 0.03f, trunk, parent);
            for (var i = 0; i < 7; i++)
            {
                var at = top + new Vector3((Hash(seed, i, 3) - 0.5f) * crown * 1.5f, (Hash(seed, i, 4) - 0.25f) * crown * 0.9f, (Hash(seed, i, 5) - 0.5f) * crown * 1.5f);
                s.Ball("Crown", at, crown * (0.8f + 0.5f * Hash(seed, i, 6)), i % 3 == 0 ? leavesLit : leaves, parent);
            }
        }

        /// <summary>A planter with a bush of leaves.</summary>
        public static void Planter(FilmStage s, Vector3 foot, float size, Material pot, Material leaves, int seed, Transform parent = null, float bush = 1f)
        {
            s.Soft("Pot", foot + Vector3.up * size * 0.3f, new Vector3(size, size * 0.6f, size), size * 0.08f, pot, parent);
            for (var i = 0; i < 6; i++)
            {
                var at = foot + new Vector3((Hash(seed, i, 21) - 0.5f) * size * 0.7f, size * 0.6f + (0.15f + 0.5f * Hash(seed, i, 22)) * size * bush, (Hash(seed, i, 23) - 0.5f) * size * 0.7f);
                s.Ball("Leaves", at, size * (0.45f + 0.35f * Hash(seed, i, 24)) * bush, leaves, parent);
            }
        }

        /// <summary>A leaf: a small card with a leaf cut out of it, for climbing plants.</summary>
        public static Texture2D Leaf()
        {
            return Make("leaf", 64, 64, false, false, (x, y) =>
            {
                float u = (x - 31.5f) / 32f, v = (y - 31.5f) / 32f;
                var inside = Mathf.Abs(u) < 0.62f * (1f - v * v) * (0.75f + 0.25f * v) ? 1f : 0f;
                var vein = Mathf.Abs(u) < 0.03f ? 0.75f : 1f;
                var shade = (0.75f + 0.25f * (v * 0.5f + 0.5f)) * vein;
                return new Color(shade, shade, shade, inside);
            }, TextureWrapMode.Clamp);
        }

        /// <summary>A climbing plant along a line from a to b: one mesh of many small leaves, hanging and catching the
        /// light at different angles. They stir in the wind.</summary>
        public static GameObject Vine(FilmStage s, Vector3 a, Vector3 b, Vector3 spread, int leaves, Color colour, int seed, float hang = 0.5f, Transform parent = null, float leafSize = 0.11f)
        {
            // (a little light of their own: thin leaves let the low sun through)
            var material = FilmStage.Mat(colour, 0.25f, 0f, Leaf(), new Color(colour.r * 0.22f, colour.g * 0.2f, colour.b * 0.08f));
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cull", 0f);
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            var mesh = new Mesh { name = "Vine", hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic();
            var verts = new Vector3[leaves * 4];
            var uv = new Vector2[leaves * 4];
            var cols = new Color[leaves * 4];
            var tris = new int[leaves * 6];
            var homes = new (Vector3 at, Quaternion turn, float size, float phase)[leaves];
            for (var i = 0; i < leaves; i++)
            {
                var k = Hash(seed, i, 1);
                var down = Hash(seed, i, 2);
                var at = Vector3.Lerp(a, b, k) + new Vector3((Hash(seed, i, 3) - 0.5f) * spread.x, -down * down * down * hang + (Hash(seed, i, 4) - 0.5f) * spread.y, (Hash(seed, i, 5) - 0.5f) * spread.z);
                var turn = Quaternion.Euler(Hash(seed, i, 6) * 150f - 20f, Hash(seed, i, 7) * 360f, Hash(seed, i, 8) * 80f - 40f);
                homes[i] = (at, turn, leafSize * (0.7f + 0.7f * Hash(seed, i, 9)), Hash(seed, i, 10) * 6.283f);
                var shade = 0.7f + 0.5f * Hash(seed, i, 11);
                for (var c = 0; c < 4; c++) cols[i * 4 + c] = new Color(shade, shade * (0.95f + 0.1f * Hash(seed, i, 12)), shade * 0.9f, 1f);
                uv[i * 4] = new Vector2(0f, 0f); uv[i * 4 + 1] = new Vector2(1f, 0f); uv[i * 4 + 2] = new Vector2(0f, 1f); uv[i * 4 + 3] = new Vector2(1f, 1f);
                tris[i * 6] = i * 4; tris[i * 6 + 1] = i * 4 + 2; tris[i * 6 + 2] = i * 4 + 1; tris[i * 6 + 3] = i * 4 + 1; tris[i * 6 + 4] = i * 4 + 2; tris[i * 6 + 5] = i * 4 + 3;
            }
            var first = true;
            void Pose(float t)
            {
                for (var i = 0; i < leaves; i++)
                {
                    var (at, turn, size, phase) = homes[i];
                    var stir = Quaternion.Euler(Mathf.Sin(t * 2.3f + phase) * 9f, Mathf.Sin(t * 1.7f + phase * 1.3f) * 7f, Mathf.Sin(t * 3.1f + phase * 0.7f) * 11f);
                    var q = turn * stir;
                    Vector3 r = q * Vector3.right * size * 0.5f, u = q * Vector3.up * size;
                    verts[i * 4] = at - r; verts[i * 4 + 1] = at + r; verts[i * 4 + 2] = at - r + u; verts[i * 4 + 3] = at + r + u;
                }
                mesh.vertices = verts;
                if (first) { mesh.uv = uv; mesh.colors = cols; mesh.triangles = tris; first = false; }
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            }
            Pose(0f);
            s.Moves.Add(Pose);
            var go = new GameObject("Vine");
            go.transform.SetParent(parent != null ? parent : s.Root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }
    }
}
