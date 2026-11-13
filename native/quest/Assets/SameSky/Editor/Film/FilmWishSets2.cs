// More places of "Wish You Were Here": Maya's coast at sunset, and the rooftop terrace above the water.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SameSky.Film
{
    public sealed class CoastSet
    {
        public Light Sun;
        public Vector3 SunDirection;
        public Material Stone, Wood, Iron, Glass, Stucco;
        /// <summary>The sea wall along the walk: the height of its top, and the line of its landward edge.</summary>
        public const float WallTop = 0.98f, WallEdgeZ = -0.5f;
        /// <summary>Where the players sit and stand, and the bench further on (its middle, on the ground; it faces the sea).</summary>
        public Vector3 Players, Bench;
        /// <summary>The steps up the headland: the middle of their lowest edge and their top. They climb toward +X.</summary>
        public Vector3 StepsFoot, StepsTop;
        public const float StepRise = 0.17f, StepTread = 0.34f, StepWidth = 2.6f, StepWall = 1.02f;
        public const int StepCount = 30;
        public float StepHeight(float x) => Mathf.Clamp((x - StepsFoot.x) / StepTread, 0f, StepCount) * StepRise;
    }

    public sealed class TerraceSet
    {
        public Light Sun;
        public Vector3 SunDirection;
        public Material Stone, Wood, Iron, Linen, Wicker, Bulbs;
        /// <summary>The top of the wall round the terrace, and where its sea side runs (its inner edge).</summary>
        public const float WallTop = 0.98f, SeaEdgeZ = 5.3f, EastEdgeX = 7.8f;
        /// <summary>Where Adam comes out (the door of the stair house) and where Maya's steps arrive (a gap in the east wall).</summary>
        public Vector3 Door, Steps;
        /// <summary>The two chairs by the sea wall, and the little table between them.</summary>
        public Transform ChairA, ChairB, Table;
        public Vector3 ChairAHome, ChairBHome;
        /// <summary>A good place to stand at the sea wall, looking out.</summary>
        public Vector3 Rail;
    }

    public static partial class FilmWishSets
    {
        static void Lantern(FilmStage s, Vector3 foot, float size, Material frame, Material glass, float light = 0f)
        {
            if (light > 0f)
            {
                // a flame, not a bulb: it is never quite still
                var flame = s.Lamp("Lantern light", foot + Vector3.up * size * 0.5f, new Color(1f, 0.66f, 0.36f), light, 4.5f);
                var seed = foot.x * 3.7f + foot.z * 1.3f;
                s.Moves.Add(t => flame.intensity = light * (0.86f + 0.2f * Mathf.PerlinNoise(t * 5.5f + seed, seed) + 0.06f * Mathf.Sin(t * 23f + seed)));
                light = 0f;
            }
            s.Soft("Lantern base", foot + Vector3.up * size * 0.04f, new Vector3(size * 0.62f, size * 0.08f, size * 0.62f), size * 0.02f, frame);
            s.Soft("Lantern glass", foot + Vector3.up * size * 0.46f, new Vector3(size * 0.5f, size * 0.74f, size * 0.5f), size * 0.03f, glass, null, null, false);
            s.Soft("Lantern top", foot + Vector3.up * size * 0.88f, new Vector3(size * 0.66f, size * 0.1f, size * 0.66f), size * 0.03f, frame);
            if (light > 0f) s.Lamp("Lantern light", foot + Vector3.up * size * 0.5f, new Color(1f, 0.66f, 0.36f), light, 4.5f);
        }

        // ------------------------------------------------------------ Maya's coast, at sunset
        /// <summary>The sea is toward +Z, the sun low over it; a paved walk runs along a stone sea wall, with low
        /// houses and pines behind. To the east (+X) the walk ends at steps that climb a headland.</summary>
        public static CoastSet Coast(FilmStage s, float sunAzimuth = 24f, float sunHeight = 5f)
        {
            var set = new CoastSet();
            if (s.Pipeline != null) s.Pipeline.shadowDistance = 55f;
            s.Camera.Cam.farClipPlane = 1200f;

            WishKit.Sky(WishKit.DuskSky(sunAzimuth, sunHeight, 0.12f, 1f, 0.85f), 1f);
            set.Sun = s.Sun(new Vector3(sunHeight + 2f, 180f + sunAzimuth, 0f), new Color(1f, 0.66f, 0.42f), 2.7f, true, 0.82f);
            set.SunDirection = -set.Sun.transform.forward;
            s.Ambient(new Color(0.38f, 0.37f, 0.55f), new Color(0.72f, 0.48f, 0.42f) * 0.82f, new Color(0.34f, 0.24f, 0.22f) * 0.62f);
            s.Haze(new Color(0.78f, 0.48f, 0.4f), 0.0011f);

            set.Stone = FilmStage.Mat(new Color(0.74f, 0.66f, 0.56f), 0.2f);
            set.Wood = FilmStage.Mat(new Color(0.5f, 0.34f, 0.24f), 0.3f);
            set.Iron = FilmStage.Mat(new Color(0.1f, 0.1f, 0.1f), 0.45f, 0.6f);
            set.Glass = WishKit.Bulb(new Color(1f, 0.82f, 0.6f), 1.5f);
            set.Stucco = FilmStage.Mat(new Color(0.86f, 0.8f, 0.7f), 0.12f);
            var paving = FilmStage.Mat(new Color(0.78f, 0.66f, 0.54f), 0.24f, 0f, FilmTex.Pavers(), null, new Vector2(40f, 2.4f));
            var earth = FilmStage.Mat(new Color(0.36f, 0.3f, 0.2f), 0.1f);
            var grass = FilmStage.Mat(new Color(0.3f, 0.36f, 0.16f), 0.08f);
            var grassLit = FilmStage.Mat(new Color(0.5f, 0.46f, 0.18f), 0.08f);
            var rock = FilmStage.Mat(new Color(0.42f, 0.36f, 0.32f), 0.15f);

            // the walk, the sea wall, the sea
            s.Box("Walk", new Vector3(-20f, -0.5f, -9.25f), new Vector3(130f, 1f, 17.5f), paving);
            s.Box("Quay face", new Vector3(-20f, -1.4f, -0.2f), new Vector3(130f, 2.8f, 0.6f), set.Stone);
            s.Soft("Sea wall", new Vector3(-21f, CoastSet.WallTop * 0.5f - 0.03f, -0.25f), new Vector3(126f, CoastSet.WallTop - 0.06f, 0.4f), 0.04f, set.Stone);
            s.Soft("Sea wall cap", new Vector3(-21f, CoastSet.WallTop - 0.04f, -0.25f), new Vector3(126f, 0.08f, 0.56f), 0.03f, FilmStage.Mat(new Color(0.8f, 0.73f, 0.63f), 0.3f));
            WishKit.Water(s, -1.7f, new Color(0.03f, 0.05f, 0.075f), new Vector3(0f, 0f, 300f), new Vector2(1400f, 620f), 0.085f, 0.5f, 5f);
            for (var x = -74f; x <= 40f; x += 12f)
            {
                s.Soft("Wall pier", new Vector3(x, 0.62f, -0.25f), new Vector3(0.62f, 1.24f, 0.62f), 0.05f, set.Stone);
                s.Rod("Lamp stem", new Vector3(x, 1.24f, -0.25f), new Vector3(x, 2.5f, -0.25f), 0.07f, set.Iron);
                s.Ball("Lamp globe", new Vector3(x, 2.66f, -0.25f), 0.36f, set.Glass, null, false);
            }

            // houses along the walk: warm stucco, shutters dark, a few lamps already on
            var walls = new[] { new Color(0.9f, 0.72f, 0.5f), new Color(0.86f, 0.56f, 0.42f), new Color(0.92f, 0.84f, 0.7f), new Color(0.82f, 0.62f, 0.56f), new Color(0.78f, 0.7f, 0.52f), new Color(0.9f, 0.78f, 0.62f) };
            var houses = new (float x0, float x1, float h)[] { (-84f, -70f, 9.5f), (-69f, -57f, 7f), (-56f, -42f, 10.5f), (-41f, -30f, 7.5f), (-29f, -16f, 9f), (-15f, -4f, 6.8f), (-3f, 10f, 10f), (11f, 21f, 7.2f), (22f, 34f, 9.2f) };
            for (var i = 0; i < houses.Length; i++)
            {
                var (x0, x1, h) = houses[i];
                WishKit.Block(s, "House " + i, new Vector3((x0 + x1) * 0.5f, 0f, -23.5f), new Vector3(x1 - x0, h, 12f), Color.Lerp(walls[i % walls.Length], new Color(0.7f, 0.62f, 0.56f), 0.35f) * 0.64f, 700 + i * 9, 0.14f, 1.1f, 3.3f, 2.6f);
                if (i % 2 == 0) WishKit.Shop(s, new Vector3((x0 + x1) * 0.5f, 0f, -17.5f), Vector3.forward, 4.4f, new Color(1f, 0.8f, 0.55f), new Color[] { new(0.62f, 0.2f, 0.14f), new(0.2f, 0.36f, 0.42f), new(0.74f, 0.6f, 0.3f) }[i % 3], null, 0.9f, false);
            }
            // pines and planters between the houses and the walk
            var trunk = FilmStage.Mat(new Color(0.24f, 0.16f, 0.12f), 0.1f);
            var pine = FilmStage.Mat(new Color(0.16f, 0.26f, 0.14f), 0.08f);
            var pineLit = FilmStage.Mat(new Color(0.34f, 0.36f, 0.14f), 0.08f);
            var n = 0;
            foreach (var x in new[] { -66f, -49f, -35f, -22f, -9f, 5f, 17f, 29f, 38f })
                WishKit.Tree(s, new Vector3(x, 0f, -12.5f - 2f * Mathf.Sin(x)), 5.2f + 1.6f * Mathf.Abs(Mathf.Sin(x * 0.7f)), 2.6f, trunk, pine, pineLit, 40 + n++);
            var pot = FilmStage.Mat(new Color(0.62f, 0.36f, 0.26f), 0.2f);
            foreach (var x in new[] { -30f, -18f, -6f, 8f, 20f, 32f })
                WishKit.Planter(s, new Vector3(x, 0f, -9.4f), 0.9f, pot, grass, 60 + n++, null, 1.1f);

            // where the players are, and a bench further along
            set.Players = new Vector3(13f, 0f, -5.2f);
            set.Bench = new Vector3(27f, 0f, -3.2f);
            WishKit.Bench(s, set.Bench, 0f, set.Wood, set.Iron, 2.1f);
            WishKit.Bench(s, new Vector3(-12f, 0f, -3.2f), 0f, set.Wood, set.Iron, 2.1f);

            // the headland to the east: steps between low walls, grass, rock, a white house at the top
            set.StepsFoot = new Vector3(43f, 0f, -3.4f);
            set.StepsTop = WishKit.Stairs(s, "Coast steps", set.StepsFoot, Vector3.right, CoastSet.StepCount, CoastSet.StepRise, CoastSet.StepTread, CoastSet.StepWidth, set.Stone, null, CoastSet.StepWall, set.Stucco);
            s.Box("Steps landing", set.StepsTop + new Vector3(4f, -0.15f, 0f), new Vector3(8f, 0.3f, 4f), set.Stone);
            var top = set.StepsTop.y;
            // the ground rises behind the steps (landward) and levels off at the top; the sea side stays open
            var slope = Mathf.Atan2(CoastSet.StepRise, CoastSet.StepTread) * Mathf.Rad2Deg;
            s.Box("Hillside", new Vector3(48.4f, 1.2f, -17.7f), new Vector3(15f, 3f, 25f), earth, null, new Vector3(0f, 0f, slope));
            s.Box("Hill top", new Vector3(76f, top - 1.6f, -14.5f), new Vector3(46f, 3f, 31f), earth);
            s.Box("Hill top sea side", new Vector3(76f, top - 1.6f, 1.6f), new Vector3(46f, 3f, 3.4f), earth);
            s.Box("Hill foot", new Vector3(49f, -1.6f, -17.7f), new Vector3(16f, 4f, 25f), earth);
            for (var i = 0; i < 30; i++)
            {
                var fx = 44f + 40f * WishHash(i, 1);
                var fz = -5.6f - 11f * WishHash(i, 3);
                var y = Mathf.Clamp((fx - 43f) * 0.5f, 0f, top) - 0.1f;
                s.Ball("Bush", new Vector3(fx, y, fz), 1f + 1.5f * WishHash(i, 4), i % 3 == 0 ? grassLit : grass);
            }
            for (var i = 0; i < 9; i++)
                s.Soft("Rock", new Vector3(44f + 4f * i + WishHash(i, 5), -0.9f, 2.8f + 2f * WishHash(i, 6)), new Vector3(2.4f + 2f * WishHash(i, 7), 1.8f, 2f + WishHash(i, 8)), 0.5f, rock, null, new Vector3(20f * WishHash(i, 9), 60f * WishHash(i, 10), 0f));
            WishKit.Block(s, "White house", new Vector3(72f, top - 0.3f, -9f), new Vector3(14f, 6.5f, 10f), new Color(0.94f, 0.9f, 0.84f), 931, 0.2f, 1.2f, 3.3f, 2.8f);

            // far away: a line of hills across the bay, and two sails
            s.Card("Far hills", new Vector3(-420f, 14f, 520f), new Vector2(700f, 44f), new Vector3(0.5f, 0f, -1f), FilmStage.Cutout(new Color(0.5f, 0.3f, 0.34f), Hills(3)));
            s.Card("Far hills 2", new Vector3(520f, 10f, 560f), new Vector2(700f, 34f), new Vector3(-0.5f, 0f, -1f), FilmStage.Cutout(new Color(0.42f, 0.26f, 0.32f), Hills(8)));
            var sail = FilmStage.Mat(new Color(0.95f, 0.9f, 0.82f), 0.1f);
            var hull = FilmStage.Mat(new Color(0.2f, 0.16f, 0.14f), 0.4f);
            foreach (var (at, drift) in new[] { (new Vector3(-60f, -1.6f, 150f), 0.9f), (new Vector3(90f, -1.6f, 230f), -0.6f) })
            {
                var boat = new GameObject("Sailboat").transform;
                boat.SetParent(s.Root, false);
                boat.position = at;
                s.Soft("Hull", new Vector3(0f, 0.5f, 0f), new Vector3(7f, 1.2f, 2.2f), 0.4f, hull, boat);
                s.Rod("Mast", new Vector3(0.4f, 1f, 0f), new Vector3(0.4f, 11f, 0f), 0.14f, hull, boat);
                var cloth = new Mesh { name = "Sail", hideFlags = HideFlags.HideAndDontSave };
                cloth.vertices = new[] { new Vector3(0.5f, 1.6f, 0f), new Vector3(0.5f, 10.8f, 0f), new Vector3(-3.6f, 1.8f, 0.5f), new Vector3(0.55f, 1.6f, 0f), new Vector3(0.55f, 9.6f, 0f), new Vector3(3.4f, 1.9f, -0.4f) };
                cloth.triangles = new[] { 0, 1, 2, 0, 2, 1, 3, 4, 5, 3, 5, 4 };
                cloth.RecalculateNormals();
                s.Part("Sails", cloth, Vector3.zero, sail, boat);
                var home = at;
                var d = drift;
                s.Moves.Add(t => boat.position = home + new Vector3(t * d, 0.06f * Mathf.Sin(t * 1.1f + d), 0f));
            }
            s.Reflections(new Vector3(0f, 1.6f, -2f), 1.1f);
            return set;
        }

        static float WishHash(int i, int salt)
        {
            unchecked
            {
                var h = (uint)(i * 374761393 + salt * 668265263 + 1013904223);
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        /// <summary>A far line of hills: a soft silhouette, clear above.</summary>
        static Texture2D Hills(int seed)
        {
            var t = new Texture2D(1024, 128, TextureFormat.RGBA32, true, false) { name = "hills " + seed, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color[1024 * 128];
            for (var x = 0; x < 1024; x++)
            {
                var u = x / 1024f;
                var ridge = (0.25f + 0.45f * Mathf.PerlinNoise(u * 3.1f + seed, 0.3f) + 0.16f * Mathf.PerlinNoise(u * 11f + seed, 1.7f)) * WishKit.S(0f, 0.12f, u) * WishKit.S(1f, 0.88f, u);
                for (var y = 0; y < 128; y++) px[y * 1024 + x] = new Color(1f, 1f, 1f, y / 128f < ridge ? 1f : 0f);
            }
            t.SetPixels(px);
            t.Apply(true, false);
            return t;
        }

        // ------------------------------------------------------------ the rooftop terrace
        /// <summary>A stone terrace high above the water. Out to sea (+Z) the sun is going down a little to the right;
        /// to the left, across the bay, a city has its lights on under a sky that is already night. A stair house with
        /// a door at the back, steps arriving through the east wall, a pergola with vines and linen, two chairs by the
        /// sea wall.</summary>
        public static TerraceSet Terrace(FilmStage s, float sunAzimuth = 26f, float sunHeight = 2.4f)
        {
            var set = new TerraceSet();
            if (s.Pipeline != null) s.Pipeline.shadowDistance = 34f;
            s.Camera.Cam.farClipPlane = 1500f;

            WishKit.Sky(WishKit.DuskSky(sunAzimuth, sunHeight, 0.95f, 1.05f, 0.8f, 2048, 7f), 1f);
            set.Sun = s.Sun(new Vector3(sunHeight + 3.5f, 180f + sunAzimuth, 0f), new Color(1f, 0.72f, 0.52f), 2.0f, true, 0.8f);
            set.SunDirection = -set.Sun.transform.forward;
            s.Ambient(new Color(0.24f, 0.25f, 0.46f), new Color(0.5f, 0.36f, 0.4f) * 0.8f, new Color(0.3f, 0.22f, 0.2f) * 0.6f);
            s.Haze(new Color(0.5f, 0.34f, 0.4f), 0.0009f);

            set.Stone = FilmStage.Mat(new Color(0.78f, 0.69f, 0.58f), 0.22f, 0f, FilmTex.Pavers(), null, new Vector2(6f, 4f));
            var wall = FilmStage.Mat(new Color(0.84f, 0.77f, 0.67f), 0.16f);
            var cap = FilmStage.Mat(new Color(0.88f, 0.82f, 0.72f), 0.3f);
            set.Wood = FilmStage.Mat(new Color(0.42f, 0.29f, 0.2f), 0.3f);
            set.Iron = FilmStage.Mat(new Color(0.09f, 0.09f, 0.09f), 0.45f, 0.6f);
            set.Linen = FilmStage.Mat(new Color(0.93f, 0.88f, 0.78f), 0.08f, 0f, null, new Color(0.34f, 0.24f, 0.13f));      // thin cloth: the sun comes through
            set.Wicker = FilmStage.Mat(new Color(0.62f, 0.47f, 0.31f), 0.25f);
            set.Bulbs = WishKit.Bulb(new Color(1f, 0.74f, 0.42f), 3.2f);
            var white = FilmStage.Mat(new Color(0.62f, 0.57f, 0.52f), 0.12f);

            // the floor, the house under it, the walls round it
            s.Box("Terrace", new Vector3(0f, -0.2f, 0f), new Vector3(16.6f, 0.4f, 11.6f), set.Stone);
            s.Box("House below", new Vector3(0f, -9f, -0.05f), new Vector3(17f, 17.6f, 11.7f), white);
            // sea wall (with its cap, where hands rest), west wall; the east wall has a gap where the steps arrive
            void Wall(Vector3 a, Vector3 b)
            {
                var mid = (a + b) * 0.5f;
                var along = b - a;
                var size = new Vector3(Mathf.Abs(along.x) + 0.4f, TerraceSet.WallTop - 0.06f, Mathf.Abs(along.z) + 0.4f);
                s.Soft("Wall", new Vector3(mid.x, (TerraceSet.WallTop - 0.06f) * 0.5f, mid.z), size, 0.04f, wall);
                s.Soft("Wall cap", new Vector3(mid.x, TerraceSet.WallTop - 0.04f, mid.z), new Vector3(size.x + 0.14f, 0.08f, size.z + 0.14f), 0.03f, cap);
            }
            Wall(new Vector3(-8.1f, 0f, 5.5f), new Vector3(8.1f, 0f, 5.5f));
            Wall(new Vector3(-8.1f, 0f, -5.5f), new Vector3(-8.1f, 0f, 5.5f));
            Wall(new Vector3(8.1f, 0f, 0.2f), new Vector3(8.1f, 0f, 5.5f));
            Wall(new Vector3(8.1f, 0f, -5.5f), new Vector3(8.1f, 0f, -2.6f));
            set.Steps = new Vector3(8.4f, 0f, -1.2f);
            // the steps that arrive there from below, outside the wall
            WishKit.Stairs(s, "Arriving steps", new Vector3(13.4f, -2.55f, -1.2f), Vector3.left, 15, 0.17f, 0.34f, 2.6f, FilmStage.Mat(new Color(0.74f, 0.66f, 0.56f), 0.2f), null, 1.0f, wall);

            // the stair house at the back, with the door Adam comes out of
            s.Box("Stair house", new Vector3(-4.6f, 1.5f, -6.9f), new Vector3(5.2f, 3.2f, 3f), white);
            s.Box("Stair house roof", new Vector3(-4.6f, 3.16f, -6.8f), new Vector3(5.6f, 0.14f, 3.5f), cap);
            s.Card("Doorway", new Vector3(-4.6f, 1.08f, -5.38f), new Vector2(1.1f, 2.16f), Vector3.forward, FilmStage.Glow(new Color(1.5f, 0.92f, 0.46f, 1f)));
            s.Box("Door frame", new Vector3(-4.6f, 2.22f, -5.36f), new Vector3(1.36f, 0.12f, 0.1f), set.Wood);
            foreach (var x in new[] { -5.22f, -3.98f }) s.Box("Door post", new Vector3(x, 1.1f, -5.36f), new Vector3(0.12f, 2.2f, 0.1f), set.Wood);
            s.Lamp("Doorway light", new Vector3(-4.6f, 1.7f, -4.8f), new Color(1f, 0.68f, 0.4f), 1.5f, 5f);
            // the back wall, and the gap in it where the steps from the street arrive
            s.Box("Back wall", new Vector3(4.225f, 0.75f, -5.7f), new Vector3(8.15f, 1.5f, 0.4f), white);
            s.Box("Back wall end", new Vector3(-1.625f, 0.75f, -5.7f), new Vector3(0.55f, 1.5f, 0.4f), white);
            WishKit.Stairs(s, "His steps", new Vector3(-0.6f, -2.55f, -11.0f), Vector3.forward, 15, 0.17f, 0.34f, 1.4f, FilmStage.Mat(new Color(0.6f, 0.54f, 0.47f), 0.2f), null, 1.0f, white);
            set.Door = new Vector3(-0.6f, 0f, -5.75f);

            // the pergola on the west side: posts, beams, vines over it, linen hanging on the sea side
            var px = new[] { -7.3f, -4.4f, -1.5f };
            var pz = new[] { -1.8f, 2.6f };
            const float beam = 2.62f;
            foreach (var x in px)
                foreach (var z in pz) s.Box("Pergola post", new Vector3(x, beam * 0.5f, z), new Vector3(0.13f, beam, 0.13f), set.Wood);
            foreach (var z in pz) s.Box("Pergola beam", new Vector3(-4.4f, beam + 0.07f, z), new Vector3(6.6f, 0.14f, 0.12f), set.Wood);
            for (var i = 0; i < 9; i++) s.Box("Pergola slat", new Vector3(-7.4f + i * 0.75f, beam + 0.19f, 0.4f), new Vector3(0.07f, 0.1f, 5.2f), set.Wood);
            WishKit.Vine(s, new Vector3(-7.5f, beam + 0.22f, 0.4f), new Vector3(-1.3f, beam + 0.22f, 0.4f), new Vector3(0.3f, 0.14f, 5.1f), 1500, new Color(0.34f, 0.46f, 0.2f), 5, 0.8f);
            WishKit.Vine(s, new Vector3(-7.3f, 0.2f, -1.8f), new Vector3(-7.3f, beam, -1.8f), new Vector3(0.36f, 0.1f, 0.36f), 260, new Color(0.3f, 0.42f, 0.18f), 9, 0.1f);
            WishKit.Vine(s, new Vector3(-1.5f, 0.3f, 2.6f), new Vector3(-1.5f, beam, 2.6f), new Vector3(0.34f, 0.1f, 0.34f), 240, new Color(0.34f, 0.46f, 0.2f), 13, 0.1f);
            WishKit.Vine(s, new Vector3(-7.3f, 0.3f, 2.6f), new Vector3(-7.3f, beam, 2.6f), new Vector3(0.34f, 0.1f, 0.34f), 220, new Color(0.3f, 0.42f, 0.18f), 17, 0.1f);
            WishKit.Cloth(s, "Linen 1", new Vector3(-6.3f, beam, 2.66f), Vector3.right, 1.5f, 2.3f, set.Linen, 1f, 0.7f, null, 0.035f);
            WishKit.Cloth(s, "Linen 2", new Vector3(-2.7f, beam, 2.66f), Vector3.right, 1.5f, 2.3f, set.Linen, 1.1f, 2.9f, null, 0.035f);
            WishKit.Cloth(s, "Linen 3", new Vector3(-1.44f, beam, 0.3f), Vector3.forward, 1.9f, 2.2f, set.Linen, 0.9f, 4.4f, null, 0.035f);
            // under it, a long table and a bench
            s.Soft("Long table", new Vector3(-4.4f, 0.73f, 0.2f), new Vector3(2.4f, 0.06f, 0.9f), 0.02f, set.Wood);
            foreach (var x in new[] { -5.4f, -3.4f }) s.Box("Table leg", new Vector3(x, 0.35f, 0.2f), new Vector3(0.08f, 0.7f, 0.7f), set.Wood);
            Lantern(s, new Vector3(-4.9f, 0.76f, 0.2f), 0.3f, set.Iron, WishKit.Bulb(new Color(1f, 0.7f, 0.4f), 2.4f), 1.2f);

            // lights strung from the pergola to a pole at the far corner, and lanterns on the floor
            s.Rod("Light pole", new Vector3(7.6f, 0f, 5.1f), new Vector3(7.6f, 3.1f, 5.1f), 0.06f, set.Iron);
            WishKit.StringLights(s, new Vector3(-1.5f, beam + 0.15f, 2.6f), new Vector3(7.6f, 3.05f, 5.1f), 0.55f, 20, set.Bulbs, set.Iron, 0.07f);
            WishKit.StringLights(s, new Vector3(-1.5f, beam + 0.15f, -1.8f), new Vector3(7.9f, 2.9f, -5.3f), 0.6f, 20, set.Bulbs, set.Iron, 0.07f);
            s.Rod("Light pole 2", new Vector3(7.9f, 0f, -5.3f), new Vector3(7.9f, 2.95f, -5.3f), 0.06f, set.Iron);
            var lanternGlass = WishKit.Bulb(new Color(1f, 0.68f, 0.38f), 2.2f);
            Lantern(s, new Vector3(0.6f, 0f, 4.75f), 0.42f, set.Iron, lanternGlass, 1.4f);
            Lantern(s, new Vector3(6.9f, 0f, 4.7f), 0.34f, set.Iron, lanternGlass, 1.1f);
            Lantern(s, new Vector3(7.2f, 0f, -4.6f), 0.42f, set.Iron, lanternGlass, 1.1f);

            // two chairs and a small table by the sea wall, turned to the water
            set.ChairAHome = new Vector3(2.7f, 0f, 3.75f);
            set.ChairBHome = new Vector3(4.55f, 0f, 3.7f);
            set.ChairA = WishKit.Chair(s, set.ChairAHome, 8f, set.Iron, set.Wicker);
            set.ChairB = WishKit.Chair(s, set.ChairBHome, -10f, set.Iron, set.Wicker);
            set.Table = WishKit.RoundTable(s, new Vector3(3.62f, 0f, 4.45f), 0.6f, 0.56f, cap, set.Iron);
            set.Rail = new Vector3(3.6f, 0f, 4.95f);

            // pots: an olive tree, lavender, something trailing over the wall
            var pot = FilmStage.Mat(new Color(0.66f, 0.4f, 0.28f), 0.2f);
            var olive = FilmStage.Mat(new Color(0.4f, 0.46f, 0.3f), 0.1f);
            var oliveLit = FilmStage.Mat(new Color(0.6f, 0.56f, 0.3f), 0.1f);
            s.Soft("Olive pot", new Vector3(6.6f, 0.36f, 1.2f), new Vector3(0.8f, 0.72f, 0.8f), 0.08f, pot);
            WishKit.Tree(s, new Vector3(6.6f, 0.7f, 1.2f), 1.5f, 0.85f, FilmStage.Mat(new Color(0.34f, 0.28f, 0.22f), 0.1f), olive, oliveLit, 77);
            WishKit.Planter(s, new Vector3(3.4f, 0f, -4.9f), 0.7f, pot, FilmStage.Mat(new Color(0.42f, 0.36f, 0.5f), 0.1f), 81, null, 0.9f);
            WishKit.Planter(s, new Vector3(5.2f, 0f, -4.9f), 0.62f, pot, olive, 83, null, 1f);
            WishKit.Planter(s, new Vector3(-7.4f, 0f, 4.7f), 0.7f, pot, olive, 85, null, 1.1f);
            WishKit.Vine(s, new Vector3(-6.4f, TerraceSet.WallTop + 0.04f, 5.5f), new Vector3(-3.6f, TerraceSet.WallTop + 0.04f, 5.5f), new Vector3(0.2f, 0.1f, 0.5f), 260, new Color(0.34f, 0.46f, 0.2f), 21, 0.5f);

            // the water far below, and what lies across it: the city's lights to the left, open sea and a headland to the right
            WishKit.Water(s, -14f, new Color(0.02f, 0.03f, 0.06f), new Vector3(0f, 0f, 500f), new Vector2(2400f, 1400f), 0.03f, 0.55f);
            var cityGlow = new Color(1.9f, 1.5f, 1.3f);
            s.Card("City across the bay", new Vector3(-330f, 14f, 560f), new Vector2(620f, 58f), new Vector3(0.5f, 0f, -0.87f), FilmStage.Cutout(cityGlow, FilmTex.Skyline(2048, 256, 61)));
            s.Card("City across the bay 2", new Vector3(-560f, 8f, 520f), new Vector2(520f, 40f), new Vector3(0.73f, 0f, -0.68f), FilmStage.Cutout(cityGlow * 0.8f, FilmTex.Skyline(2048, 256, 67)));
            var towers = new (float x, float z, float w, float h)[] { (-200f, 420f, 26f, 84f), (-262f, 470f, 30f, 118f), (-170f, 480f, 22f, 66f), (-320f, 440f, 28f, 76f), (-232f, 540f, 24f, 100f), (-130f, 520f, 22f, 58f) };
            for (var i = 0; i < towers.Length; i++)
            {
                var (x, z, w, h) = towers[i];
                var block = WishKit.Block(s, "Far tower " + i, new Vector3(x, -14f, z), new Vector3(w, h, w), new Color(0.2f, 0.22f, 0.32f), 800 + i * 3, 0.34f, 2.2f, 3.6f, 2.4f, false);
                block.rotation = Quaternion.Euler(0f, 26f, 0f);
            }
            s.Card("Headland", new Vector3(560f, 4f, 520f), new Vector2(620f, 60f), new Vector3(-0.6f, 0f, -0.8f), FilmStage.Cutout(new Color(0.3f, 0.17f, 0.2f), Hills(15)));
            s.Reflections(new Vector3(2f, 1.6f, 2f), 1.1f);
            return set;
        }
    }

    public static partial class FilmWish
    {
        static void SetCoast(FilmStage s)
        {
            var set = FilmWishSets.Coast(s);
            s.Camera.Grade(8f, 10f, 6f, 0.24f, 0.6f);
            var maya = s.Lead("maya", new Vector3(0f, 0f, -1.1f), 0f, 2f);
            maya.Play(0f, "f_idle_breathe_01");
            var guitar = s.Extra("Male_Adult_12", set.Players, 20f, 4f);
            guitar.Play(0f, "m_idle_breathe_01");
            var walker = s.Extra("Female_Adult_02", new Vector3(20f, 0f, -3f), -90f, 5f);
            walker.Walk(0f, new Vector3(24f, 0f, -3f), new Vector3(4f, 0f, -3f), "f_walk_stroll_01");
            Views(s,
                (new Vector3(-9f, 2.1f, -6.5f), new Vector3(6f, 1.3f, -1f), 40f),          // along the walk toward the sun side
                (new Vector3(1.6f, 1.55f, -2.4f), new Vector3(0f, 1.45f, -1.1f), 30f),    // Maya at the wall
                (new Vector3(8f, 1.7f, -8f), new Vector3(13f, 1.1f, -5f), 38f),           // the players' spot
                (new Vector3(34f, 2.2f, -9f), new Vector3(49f, 3f, -3.4f), 40f),          // the steps up the headland
                (new Vector3(48f, 4.2f, -8.6f), new Vector3(49f, 3.4f, -3.4f), 40f),      // the steps from the side
                (new Vector3(-30f, 14f, -30f), new Vector3(10f, 0f, 0f), 46f));           // from above and behind
        }

        static void SetTerrace(FilmStage s)
        {
            var set = FilmWishSets.Terrace(s);
            s.Camera.Grade(7f, 10f, 6f, 0.24f, 0.75f);
            var maya = s.Lead("maya", set.Rail + new Vector3(-0.5f, 0f, -0.4f), 0f, 2f);
            maya.Play(0f, "f_idle_breathe_01");
            var adam = s.Lead("adam", set.Door + new Vector3(0f, 0f, 0.6f), 30f, 1f);
            adam.Play(0f, "m_idle_breathe_01");
            Views(s,
                (new Vector3(-6.5f, 2.3f, -4.6f), new Vector3(3f, 1f, 4f), 46f),          // across the terrace to the sea, sun side
                (new Vector3(7.2f, 1.7f, -4.6f), new Vector3(-3f, 1.2f, 2.5f), 46f),     // the other diagonal: pergola, linen, the city side
                (new Vector3(1.2f, 1.6f, 1.2f), new Vector3(3.2f, 1.25f, 4.4f), 34f),    // the chairs and Maya
                (new Vector3(0f, 1.7f, -2f), new Vector3(-30f, 3f, 30f), 28f),           // the city across the bay
                (new Vector3(0f, 1.7f, -2f), new Vector3(24f, 2.2f, 30f), 28f),          // the sun going down
                (new Vector3(-2f, 9f, -16f), new Vector3(1f, 0f, 2f), 42f));             // from above and behind
        }
    }
}
