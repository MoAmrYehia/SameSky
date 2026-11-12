// The shared lounge (visual reference 02): one believable room around a round walnut table, modelled in metres.
// The person sits at the origin facing +Z. Behind the partner's chair a curved floor-to-ceiling window looks out over
// water to a distant skyline (a CC0 panorama on a far curved plate, so nearby architecture still has real parallax).
// Ivory plaster with an arched alcove and walnut shelves, two cream boucle tub chairs, a slim brass floor lamp, an
// olive tree, a woven rug, soft contact shadows. Lighting is neutral afternoon daylight with gentle warmth: a trilight
// ambient, one window light and a lamp, nothing that tints faces orange. Everything is generated here from
// procedural meshes (Scripts/Room/MeshGen.cs) and the repository's CC0 textures, so it can be edited and rebuilt.

using System.Collections.Generic;
using System.IO;
using SameSky.Geometry;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SameSky.EditorTools
{
    public static class LoungeBuilder
    {
        const string Art = "Assets/SameSky/Art";
        const string Generated = "Assets/SameSky/Art/Generated";
        const string Materials = "Assets/SameSky/Resources/Materials";

        // layout (metres)
        static readonly Vector3 TableCenter = new(0, 0, 0.72f);
        const float TableTop = 0.774f, TableRadius = 0.525f;
        const float GlassRadius = 2.6f, GlassHalfAngle = 62f, Ceiling = 2.8f;
        const float WallX = 2.3f, BackZ = -1.4f;
        const float AlcoveZa = 0.25f, AlcoveZb = 1.15f, AlcoveSill = 0.62f, AlcoveSpring = 1.6f, AlcoveDepth = 0.34f;

        public static void Build(Transform room)
        {
            Directory.CreateDirectory(Generated);
            Directory.CreateDirectory(Materials);
            var geo = new GameObject("Lounge").transform;
            geo.SetParent(room, false);

            // Plaster and boucle take only the CC0 textures' surface relief (normal maps): their colour maps are tan
            // and checked, and the references want ivory walls and plain cream fabric.
            var plaster = Lit("Plaster", new Color(0.94f, 0.925f, 0.89f), 0.08f, null, $"{Art}/Textures/clay_plaster/nor_gl.jpg", new Vector2(2, 1), 0.3f);
            var floor = Lit("Parquet", new Color(0.96f, 0.93f, 0.88f), 0.3f, $"{Art}/Textures/herringbone_parquet/Diffuse.jpg", $"{Art}/Textures/herringbone_parquet/nor_gl.jpg", new Vector2(0.45f, 0.45f), 0.6f);
            // the fleece's colour map is grey (a dark rug under the table): its knit relief on an oatmeal colour instead
            var rug = Lit("RugKnit", new Color(0.84f, 0.8f, 0.73f), 0.04f, null, $"{Art}/Textures/knitted_fleece/nor_gl.jpg", Vector2.one, 0.8f);
            // the veneer scan is grey-washed: tint it a satin mid-brown (close to the web room's #d6a07a, a touch less orange)
            var walnut = Lit("Walnut", new Color(0.8f, 0.64f, 0.52f), 0.4f, $"{Art}/Textures/american_walnut_veneer/Diffuse.jpg", $"{Art}/Textures/american_walnut_veneer/nor_gl.jpg", Vector2.one, 0.5f);
            var boucle = Lit("Boucle", new Color(0.88f, 0.85f, 0.79f), 0.04f, null, $"{Art}/Textures/wool_boucle/nor_gl.jpg", new Vector2(5, 5), 1.6f);
            var bronze = Lit("Bronze", new Color(0.36f, 0.31f, 0.25f), 0.5f, metallic: 0.6f);
            var brass = Lit("Brass", new Color(0.83f, 0.67f, 0.4f), 0.62f, metallic: 1f);
            var ceramic = Lit("CeramicCream", new Color(0.92f, 0.89f, 0.83f), 0.55f);
            var shade = Lit("LampShade", new Color(0.97f, 0.94f, 0.88f), 0.05f, emission: new Color(0.62f, 0.52f, 0.38f));
            shade.SetFloat("_Cull", 0f);   // both sides: the shade is seen from below too
            var shadow = ContactShadowMaterial();
            var sky = Skyline();

            // ------------------------------------------------------------ shell: floor, ceiling, walls, the curved window
            var outline = RoomOutline();
            Add(geo, "Floor", MeshGen.Fan("Lounge floor", outline, faceUp: true, uvScale: 1f), floor, Vector3.zero);
            var ceil = new List<Vector3>();
            foreach (var p in outline) ceil.Add(new Vector3(p.x, Ceiling, p.z));
            BothSides(Add(geo, "Ceiling", MeshGen.Fan("Lounge ceiling", ceil, faceUp: false, uvScale: 0.5f), plaster, Vector3.zero));
            var glassStartZ = TableCenter.z + GlassRadius * Mathf.Cos(GlassHalfAngle * Mathf.Deg2Rad);
            BothSides(Add(geo, "Wall left", MeshGen.ArchedWall("Wall left", -WallX, BackZ, glassStartZ, Ceiling, AlcoveZa, AlcoveZb, AlcoveSpring, AlcoveSill, facePosX: true), plaster, Vector3.zero));
            BothSides(Add(geo, "Wall right", MeshGen.ArchedWall("Wall right", WallX, BackZ, glassStartZ, Ceiling, 0, 0, 0, 0, facePosX: false), plaster, Vector3.zero));
            BothSides(Add(geo, "Wall behind", Quad("Wall behind", new Vector3(WallX, 0, BackZ), new Vector3(-WallX, 0, BackZ), Ceiling), plaster, Vector3.zero));
            BuildAlcove(geo, plaster, walnut, brass);

            // the window: slim bronze mullions on a curved sill and head, open to the view beyond
            var window = new GameObject("Curved window").transform;
            window.SetParent(geo, false);
            window.localPosition = TableCenter;
            Add(window, "Sill", MeshGen.Sweep("Window sill", GlassRadius, -GlassHalfAngle, GlassHalfAngle, 48, _ => MeshGen.RoundedSection(0.1f, 0f, 0.05f, 0.01f)), bronze, Vector3.zero);
            Add(window, "Head", MeshGen.Sweep("Window head", GlassRadius, -GlassHalfAngle, GlassHalfAngle, 48, _ => MeshGen.RoundedSection(0.12f, Ceiling - 0.09f, Ceiling, 0.01f)), bronze, Vector3.zero);
            Add(window, "Transom", MeshGen.Sweep("Window transom", GlassRadius, -GlassHalfAngle, GlassHalfAngle, 48, _ => MeshGen.RoundedSection(0.05f, 2.32f, 2.36f, 0.008f)), bronze, Vector3.zero);
            var mullion = Save(MeshGen.Lathe("Mullion", new[] { new Vector2(0.015f, 0), new Vector2(0.015f, Ceiling) }, 8, true, true));
            for (var i = 0; i <= 8; i++)
            {
                var a = Mathf.Lerp(-GlassHalfAngle, GlassHalfAngle, i / 8f) * Mathf.Deg2Rad;
                Add(window, $"Mullion {i}", mullion, bronze, new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * GlassRadius, register: false);
            }

            // the view: a far curved plate (34 m) with the horizon at seated eye height
            var view = MeshGen.CurvedPanel("Skyline view", 34f, -88f, 88f, -14f, 19.2f, 64);
            var uv = view.uv;
            for (var i = 0; i < uv.Length; i++) uv[i].y = Mathf.Lerp(-0.328f, 1f, uv[i].y);
            view.uv = uv;
            Add(geo, "View", view, sky, TableCenter, castShadows: false);

            // a terrace outside the glazing: real nearby geometry (it moves against the far view as the head moves),
            // and its low parapet covers the stretched lower edge of the skyline plate
            var stone = Lit("TerraceStone", new Color(0.72f, 0.7f, 0.66f), 0.12f, null, $"{Art}/Textures/clay_plaster/nor_gl.jpg", new Vector2(0.5f, 2f), 0.5f);
            const float deckOuter = 4.7f;
            var deckInner = GlassRadius + 0.06f;
            Add(window, "Terrace deck", MeshGen.Sweep("Terrace deck", (deckInner + deckOuter) / 2f, -74f, 74f, 48,
                _ => MeshGen.RoundedSection(deckOuter - deckInner, -0.08f, -0.012f, 0.004f)), stone, Vector3.zero, castShadows: false);
            Add(window, "Terrace parapet", MeshGen.Sweep("Terrace parapet", deckOuter, -74f, 74f, 48,
                _ => MeshGen.RoundedSection(0.2f, -0.08f, 0.42f, 0.02f)), stone, Vector3.zero, castShadows: false);

            // ------------------------------------------------------------ furniture
            BuildTable(geo, walnut, shadow);
            var chairMesh = TubChair(out var cushionMesh);
            Chair(geo, "Partner chair", new Vector3(0, 0, 1.56f), 180f, chairMesh, cushionMesh, boucle, shadow);
            Chair(geo, "Second chair", new Vector3(-1.32f, 0, 1.22f), 112f, chairMesh, cushionMesh, boucle, shadow);
            BuildSideTable(geo, new Vector3(1.12f, 0, 0.62f), walnut, ceramic, shadow);
            BuildLamp(geo, new Vector3(1.6f, 0, 1.72f), brass, shade, shadow);
            BuildPlant(geo, new Vector3(-1.9f, 0, 1.6f), shadow);
            // a lathe profile that ends running inward at the top faces up
            Add(geo, "Rug", MeshGen.Lathe("Rug", new[] { new Vector2(1.3f, 0.002f), new Vector2(1.28f, 0.006f), new Vector2(0, 0.006f) }, 72, planarUv: 2.5f), rug, new Vector3(TableCenter.x, 0, TableCenter.z));
        }

        // ------------------------------------------------------------ lighting

        public static void Light(Transform room)
        {
            var sun = new GameObject("Window light").AddComponent<Light>();
            sun.transform.SetParent(room, false);
            sun.type = LightType.Directional;
            // Late-afternoon light through the glazing, low enough to reach the table and the seat, with real soft
            // shadows: the mullions rake across the floor and the left wall, and the table, chairs, cup and whoever
            // sits opposite all shade what is behind them. One shadow map for the whole room (URP: one cascade).
            sun.color = new Color(1f, 0.94f, 0.85f);   // a little warm, never orange
            sun.intensity = 0.8f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 1f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.5f;
            sun.transform.rotation = Quaternion.Euler(30, 200, 0);
            sun.lightmapBakeType = LightmapBakeType.Realtime;

            var lamp = new GameObject("Lamp light").AddComponent<Light>();
            lamp.transform.SetParent(room, false);
            lamp.transform.position = new Vector3(1.6f, 1.52f, 1.72f);
            lamp.type = LightType.Point;
            lamp.range = 2.4f;
            lamp.intensity = 0.32f;   // a pool of warmth, not a hot spot on the wall beside it
            lamp.color = new Color(1f, 0.86f, 0.68f);
            lamp.shadows = LightShadows.None;

            // A soft key for whoever sits across the table: their face is turned away from the window. It lights only
            // the people layer (Avatars.PeopleLayer), so the room's balance doesn't change.
            var key = new GameObject("Face light").AddComponent<Light>();
            key.transform.SetParent(room, false);
            key.type = LightType.Spot;
            key.transform.position = new Vector3(-0.7f, 2.05f, 0.05f);
            key.transform.LookAt(new Vector3(0f, 1.1f, 1.45f));
            key.spotAngle = 46f;
            key.innerSpotAngle = 30f;
            key.range = 4f;
            key.intensity = 1.5f;
            key.color = new Color(1f, 0.94f, 0.87f);
            key.shadows = LightShadows.None;
            key.cullingMask = 1 << 30;

            // The floor and rug are light, so the ceiling's bounce light is a soft warm white, not a dark ground tone.
            // The light from above is kept moderate: with the window light on top of it, white things lying face-up
            // (paper, the cup's rim, a cushion) would otherwise burn out.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            // With the window light at 0.8, a white thing lying in the sun lands just at 1 (0.6 from above + 0.4 of
            // sun) and in shadow at 0.6, so shadows read without anything burning out. Walls take the side light.
            RenderSettings.ambientSkyColor = new Color(0.6f, 0.61f, 0.62f);
            RenderSettings.ambientEquatorColor = new Color(0.76f, 0.74f, 0.71f);
            RenderSettings.ambientGroundColor = new Color(0.68f, 0.65f, 0.6f);
            var skyMat = AssetDatabase.LoadAssetAtPath<Material>($"{Generated}/Daylight sky.mat");
            if (skyMat == null)
            {
                skyMat = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(skyMat, $"{Generated}/Daylight sky.mat");
            }
            skyMat.SetFloat("_SunSize", 0.025f);
            skyMat.SetFloat("_AtmosphereThickness", 0.7f);
            skyMat.SetColor("_SkyTint", new Color(0.62f, 0.69f, 0.8f));
            skyMat.SetColor("_GroundColor", new Color(0.58f, 0.55f, 0.5f));
            skyMat.SetFloat("_Exposure", 1.05f);
            RenderSettings.skybox = skyMat;               // reflections on walnut and ceramic
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.6f;
            RenderSettings.fog = false;

            // What glossy things reflect: the room itself (the bright glazing in front, the wall behind), not a generic
            // sky. Drawn once at start by the app (cheap: six small faces, static geometry only).
            var reflection = new GameObject("Room reflection").AddComponent<ReflectionProbe>();
            reflection.transform.SetParent(room, false);
            reflection.transform.position = new Vector3(0f, 1.1f, 0.6f);
            reflection.mode = ReflectionProbeMode.Realtime;
            reflection.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            reflection.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            reflection.resolution = 128;
            reflection.hdr = false;
            reflection.size = new Vector3(12f, 8f, 12f);
            reflection.nearClipPlane = 0.1f;
            reflection.farClipPlane = 80f;
            reflection.clearFlags = ReflectionProbeClearFlags.Skybox;
            reflection.cullingMask = 1 << 0;   // the room's own geometry (people, hands and panels are left out)
            reflection.intensity = 0.9f;
            reflection.importance = 1;

            var probes = new GameObject("Light probes").AddComponent<LightProbeGroup>();
            probes.transform.SetParent(room, false);
            var pts = new List<Vector3>();
            for (var x = -1.8f; x <= 1.8f; x += 0.6f)
                for (var y = 0.3f; y <= 2.1f; y += 0.6f)
                    for (var z = -0.9f; z <= 2.7f; z += 0.6f)
                        pts.Add(new Vector3(x, y, z));
            probes.probePositions = pts.ToArray();
        }

        // ------------------------------------------------------------ pieces

        static List<Vector3> RoomOutline()
        {
            var pts = new List<Vector3> { new(-WallX, 0, BackZ), new(WallX, 0, BackZ) };
            for (var i = 0; i <= 32; i++)
            {
                var a = Mathf.Lerp(GlassHalfAngle, -GlassHalfAngle, i / 32f) * Mathf.Deg2Rad;
                pts.Add(TableCenter + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * GlassRadius);
            }
            return pts;
        }

        static void BuildAlcove(Transform geo, Material plaster, Material walnut, Material brass)
        {
            var alcove = new GameObject("Alcove").transform;
            alcove.SetParent(geo, false);
            float x0 = -WallX, x1 = -WallX - AlcoveDepth, zc = (AlcoveZa + AlcoveZb) / 2f, r = (AlcoveZb - AlcoveZa) / 2f;
            // Quad(a, b) faces the cross product of up and (b - a): back faces +X, side A +Z, side B -Z (into the niche)
            Add(alcove, "Back", Quad("Alcove back", new Vector3(x1, AlcoveSill, AlcoveZa), new Vector3(x1, AlcoveSill, AlcoveZb), AlcoveSpring + r - AlcoveSill), plaster, Vector3.zero);
            Add(alcove, "Side A", Quad("Alcove side a", new Vector3(x0, AlcoveSill, AlcoveZa), new Vector3(x1, AlcoveSill, AlcoveZa), AlcoveSpring - AlcoveSill), plaster, Vector3.zero);
            Add(alcove, "Side B", Quad("Alcove side b", new Vector3(x1, AlcoveSill, AlcoveZb), new Vector3(x0, AlcoveSill, AlcoveZb), AlcoveSpring - AlcoveSill), plaster, Vector3.zero);
            Add(alcove, "Sill", Floor("Alcove sill", x1, x0, AlcoveZa, AlcoveZb, AlcoveSill), walnut, Vector3.zero);
            // the vault: half a cylinder along X above the spring line
            var vault = new Mesh { name = "Alcove vault" };
            var v = new List<Vector3>();
            var t = new List<int>();
            const int steps = 20;
            for (var i = 0; i <= steps; i++)
            {
                var a = Mathf.PI * i / steps;
                var y = AlcoveSpring + Mathf.Sin(a) * r;
                var z = zc - Mathf.Cos(a) * r;
                v.Add(new Vector3(x0, y, z));
                v.Add(new Vector3(x1, y, z));
            }
            for (var i = 0; i < steps; i++) t.AddRange(new[] { i * 2, i * 2 + 2, i * 2 + 1, i * 2 + 1, i * 2 + 2, i * 2 + 3 });
            vault.SetVertices(v);
            vault.SetTriangles(t, 0);
            vault.RecalculateNormals();
            vault.RecalculateBounds();
            Add(alcove, "Vault", vault, plaster, Vector3.zero);
            // two walnut shelves, books, a small brass dallah, a warm strip under the top shelf
            foreach (var y in new[] { 1.02f, 1.42f })
                Box(alcove, $"Shelf {y:F2}", new Vector3(x0 - AlcoveDepth / 2f + 0.01f, y, zc), new Vector3(AlcoveDepth - 0.03f, 0.028f, AlcoveZb - AlcoveZa - 0.02f), walnut);
            var books = new[] { new Color(0.42f, 0.5f, 0.42f), new Color(0.78f, 0.72f, 0.62f), new Color(0.55f, 0.36f, 0.28f), new Color(0.88f, 0.85f, 0.78f) };
            for (var i = 0; i < 4; i++)
                Box(alcove, $"Book {i}", new Vector3(x0 - 0.17f, 1.034f + 0.022f * i + 0.011f, zc - 0.14f), new Vector3(0.2f - i * 0.012f, 0.022f, 0.26f - i * 0.015f), Lit($"Book {i}", books[i], 0.2f), yaw: i * 4f);
            var dallah = MeshGen.Lathe("Dallah", new[] { new Vector2(0, 0), new Vector2(0.05f, 0), new Vector2(0.065f, 0.05f), new Vector2(0.05f, 0.11f), new Vector2(0.03f, 0.14f), new Vector2(0.026f, 0.18f), new Vector2(0.04f, 0.205f), new Vector2(0.012f, 0.24f), new Vector2(0, 0.25f) }, 32);
            Add(alcove, "Dallah", dallah, brass, new Vector3(x0 - 0.17f, 1.434f, zc + 0.16f));
            var strip = Lit("Shelf glow", new Color(1f, 0.93f, 0.8f), 0f, emission: new Color(0.9f, 0.72f, 0.48f));
            Box(alcove, "Glow strip", new Vector3(x0 - 0.05f, 1.4f, zc), new Vector3(0.012f, 0.006f, AlcoveZb - AlcoveZa - 0.06f), strip);
            var glow = new GameObject("Alcove glow").AddComponent<Light>();
            glow.transform.SetParent(alcove, false);
            glow.transform.position = new Vector3(x0 - 0.12f, 1.36f, zc);
            glow.type = LightType.Point;
            glow.range = 0.7f;
            glow.intensity = 0.35f;
            glow.color = new Color(1f, 0.85f, 0.62f);
        }

        static void BuildTable(Transform geo, Material walnut, Material shadow)
        {
            // a 1.05 m round top with a softened edge on a single pedestal and a disc foot
            var top = MeshGen.Lathe("Table top", new[]
            {
                new Vector2(0, TableTop - 0.034f), new Vector2(TableRadius - 0.03f, TableTop - 0.034f), new Vector2(TableRadius - 0.006f, TableTop - 0.026f),
                new Vector2(TableRadius, TableTop - 0.014f), new Vector2(TableRadius - 0.004f, TableTop - 0.003f), new Vector2(TableRadius - 0.012f, TableTop), new Vector2(0, TableTop),
            }, 96, planarUv: 1.4f);   // grain runs straight across the top
            var stem = MeshGen.Lathe("Table stem", new[] { new Vector2(0.075f, 0.035f), new Vector2(0.06f, 0.1f), new Vector2(0.055f, TableTop - 0.1f), new Vector2(0.09f, TableTop - 0.035f) }, 40);
            var foot = MeshGen.Lathe("Table foot", new[] { new Vector2(0, 0), new Vector2(0.27f, 0), new Vector2(0.28f, 0.012f), new Vector2(0.26f, 0.035f), new Vector2(0, 0.035f) }, 64);
            var table = new GameObject("Shared table").transform;
            table.SetParent(geo, false);
            table.localPosition = TableCenter;
            Add(table, "Top", top, walnut, Vector3.zero);
            Add(table, "Stem", stem, walnut, Vector3.zero);
            Add(table, "Foot", foot, walnut, Vector3.zero);
            ContactShadow(geo, new Vector3(TableCenter.x, 0.008f, TableCenter.z), 0.5f, shadow);
            ContactShadow(geo, new Vector3(TableCenter.x, 0.008f, TableCenter.z), 0.95f, shadow, 0.35f);   // the top's soft shade on the rug
        }

        static Mesh TubChair(out Mesh cushion)
        {
            // a rounded tub: a seat cushion and a horseshoe shell around the back (-Z), highest there and lower at the
            // arms; the open side faces +Z, so a chair's yaw is the direction it faces
            var shell = Save(MeshGen.Sweep("Tub chair shell", 0.31f, 48f, 312f, 40, t =>
            {
                var back = Mathf.Cos((t - 0.5f) * Mathf.PI * 2f * 132f / 360f);   // 1 at the back, lower at the arms
                var top = 0.56f + 0.17f * Mathf.Clamp01(back);
                return MeshGen.RoundedSection(0.15f, 0.06f, top, 0.06f, 5);
            }));
            cushion = Save(MeshGen.Lathe("Tub chair seat", new[]
            {
                new Vector2(0, 0.06f), new Vector2(0.33f, 0.06f), new Vector2(0.34f, 0.2f), new Vector2(0.33f, 0.4f),
                new Vector2(0.31f, 0.445f), new Vector2(0.26f, 0.46f), new Vector2(0, 0.462f),
            }, 48, planarUv: 3f));
            return shell;
        }

        static void Chair(Transform geo, string name, Vector3 pos, float yaw, Mesh shell, Mesh cushion, Material boucle, Material shadow)
        {
            var chair = new GameObject(name).transform;
            chair.SetParent(geo, false);
            chair.localPosition = pos;
            chair.localRotation = Quaternion.Euler(0, yaw, 0);
            Add(chair, "Shell", shell, boucle, Vector3.zero, register: false);
            Add(chair, "Seat", cushion, boucle, new Vector3(0, 0, 0.02f), register: false);
            ContactShadow(geo, new Vector3(pos.x, 0.009f, pos.z), 0.52f, shadow);
        }

        static void BuildSideTable(Transform geo, Vector3 pos, Material walnut, Material ceramic, Material shadow)
        {
            var mesh = MeshGen.Lathe("Side table", new[]
            {
                new Vector2(0, 0), new Vector2(0.17f, 0), new Vector2(0.16f, 0.02f), new Vector2(0.05f, 0.04f), new Vector2(0.04f, 0.46f),
                new Vector2(0.24f, 0.49f), new Vector2(0.245f, 0.51f), new Vector2(0.235f, 0.52f), new Vector2(0, 0.52f),
            }, 48, planarUv: 1.6f);   // straight grain across the top instead of a swirl around its centre
            Add(geo, "Side table", mesh, walnut, pos);
            var vase = MeshGen.Lathe("Vase", new[] { new Vector2(0, 0), new Vector2(0.05f, 0), new Vector2(0.075f, 0.08f), new Vector2(0.05f, 0.2f), new Vector2(0.035f, 0.24f), new Vector2(0.04f, 0.26f), new Vector2(0, 0.26f) }, 36);
            Add(geo, "Vase", vase, ceramic, pos + new Vector3(0.05f, 0.52f, 0.03f));
            ContactShadow(geo, new Vector3(pos.x, 0.009f, pos.z), 0.3f, shadow);
        }

        static void BuildLamp(Transform geo, Vector3 pos, Material brass, Material shade, Material shadow)
        {
            var lamp = new GameObject("Floor lamp").transform;
            lamp.SetParent(geo, false);
            lamp.localPosition = pos;
            Add(lamp, "Base", MeshGen.Lathe("Lamp base", new[] { new Vector2(0, 0), new Vector2(0.15f, 0), new Vector2(0.15f, 0.012f), new Vector2(0.03f, 0.02f), new Vector2(0, 0.02f) }, 40), brass, Vector3.zero);
            Add(lamp, "Pole", MeshGen.Lathe("Lamp pole", new[] { new Vector2(0.011f, 0.02f), new Vector2(0.011f, 1.42f) }, 12), brass, Vector3.zero);
            Add(lamp, "Shade", MeshGen.Lathe("Lamp shade", new[] { new Vector2(0.24f, 1.36f), new Vector2(0.205f, 1.68f) }, 48), shade, Vector3.zero);
            ContactShadow(geo, new Vector3(pos.x, 0.009f, pos.z), 0.22f, shadow);
        }

        static void BuildPlant(Transform geo, Vector3 pos, Material shadow)
        {
            // the CC0 potted plant (it has its own pot), at a natural floor-plant size by the glazing corner
            var path = AssetDatabase.FindAssets("t:GameObject", new[] { $"{Art}/Models/plant" });
            if (path.Length == 0) return;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(path[0]));
            if (asset == null) return;
            var plant = (GameObject)PrefabUtility.InstantiatePrefab(asset, geo);
            plant.name = "Plant";
            plant.transform.localPosition = pos;
            plant.transform.localRotation = Quaternion.Euler(0, 35, 0);
            var b = Bounds(plant);
            if (b.size.y > 0.01f) plant.transform.localScale = Vector3.one * (0.95f / b.size.y);
            GameObjectUtility.SetStaticEditorFlags(plant, StaticEditorFlags.BatchingStatic);
            ContactShadow(geo, new Vector3(pos.x, 0.009f, pos.z), 0.32f, shadow);
        }

        // ------------------------------------------------------------ helpers

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static Mesh Quad(string name, Vector3 a, Vector3 b, float height)
        {
            // a vertical quad from a to b (left to right as seen from its front), bottom at their y
            var m = new Mesh { name = name };
            m.SetVertices(new List<Vector3> { a, a + Vector3.up * height, b, b + Vector3.up * height });
            var len = Vector3.Distance(a, b);
            m.SetUVs(0, new List<Vector2> { new(0, a.y), new(0, a.y + height), new(len, b.y), new(len, b.y + height) });
            m.SetTriangles(new[] { 0, 1, 2, 2, 1, 3 }, 0);
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        static Mesh Floor(string name, float x0, float x1, float z0, float z1, float y)
        {
            return MeshGen.Fan(name, new List<Vector3> { new(x0, y, z0), new(x0, y, z1), new(x1, y, z1), new(x1, y, z0) }, faceUp: true);
        }

        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, float yaw = 0)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.transform.localScale = size;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        /// <summary>The room's shell is drawn from inside only, but it has to stop the window light from outside
        /// as well: cast shadows from both faces.</summary>
        static GameObject BothSides(GameObject go)
        {
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
            return go;
        }

        static GameObject Add(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, bool register = true, bool castShadows = true, float localYaw = 0)
        {
            if (register) mesh = Save(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, localYaw, 0);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        /// <summary>Stores a generated mesh as an asset (the scene references it) and returns the stored mesh.</summary>
        static Mesh Save(Mesh mesh)
        {
            if (AssetDatabase.Contains(mesh)) return mesh;
            var path = $"{Generated}/{mesh.name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);   // same asset and GUID across setup runs
                Object.DestroyImmediate(mesh);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static void ContactShadow(Transform geo, Vector3 pos, float radius, Material mat, float strength = 1f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Contact shadow";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(geo, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(90, 0, 0);
            go.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            if (strength < 1f)
            {
                var b = new MaterialPropertyBlock();
                b.SetColor("_BaseColor", new Color(0, 0, 0, 0.3f * strength));
                r.SetPropertyBlock(b);
            }
        }

        static Material ContactShadowMaterial()
        {
            var texPath = $"{Generated}/contact-shadow.png";
            if (!File.Exists(texPath))
            {
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                for (var y = 0; y < n; y++)
                    for (var x = 0; x < n; x++)
                    {
                        var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                        var a = Mathf.Clamp01(1f - d);
                        tex.SetPixel(x, y, new Color(1, 1, 1, a * a * (3f - 2f * a)));   // smooth falloff
                    }
                File.WriteAllBytes(texPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texPath);
                var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.SaveAndReimport();
            }
            var m = Unlit("ContactShadow", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath), new Color(0, 0, 0, 0.3f), transparent: true);
            m.renderQueue = 2990;   // before other transparent UI, after the opaque floor
            return m;
        }

        static Material Skyline()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Art}/Textures/window-skyline.jpg");
            if (tex != null)
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex));
                if (ti.wrapMode != TextureWrapMode.Clamp) { ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport(); }
            }
            return Unlit("SkylineView", tex, new Color(0.97f, 0.97f, 0.98f), transparent: false);
        }

        static Material Unlit(string name, Texture tex, Color color, bool transparent)
        {
            var path = $"{Materials}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);
            if (transparent)
            {
                m.SetFloat("_Surface", 1);
                m.SetFloat("_Blend", 0);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Lit(string name, Color color, float smoothness, string albedo = null, string normal = null, Vector2? tiling = null,
            float normalStrength = 1f, float metallic = 0f, Color? emission = null)
        {
            var path = $"{Materials}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            m.SetTexture("_BaseMap", albedo != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(albedo) : null);
            if (normal != null)
            {
                m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
                m.SetFloat("_BumpScale", normalStrength);
                m.EnableKeyword("_NORMALMAP");
            }
            else { m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }
            if (tiling.HasValue) { m.SetTextureScale("_BaseMap", tiling.Value); m.SetTextureScale("_BumpMap", tiling.Value); }
            if (emission.HasValue)
            {
                m.SetColor("_EmissionColor", emission.Value);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else m.DisableKeyword("_EMISSION");
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
