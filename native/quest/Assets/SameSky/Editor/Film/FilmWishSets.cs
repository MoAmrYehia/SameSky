// The places of "Wish You Were Here": Adam's harbour city in the blue hour, Maya's coast at sunset, and the rooftop
// terrace above the water where the two evenings meet. Each is built from the pieces in FilmWishKit.cs; a shot
// (FilmWish*.cs) asks for one, puts its people on it and lights what it needs. All original to this film.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SameSky.Film
{
    public sealed class HarbourSet
    {
        public Light Moon;
        public Material Iron, Stone, Wood, Bulbs, Glass;
        /// <summary>Adam's balcony: where he stands (feet), and the top of its rail in front of him.</summary>
        public Vector3 Balcony, BalconyRail;
        /// <summary>The street door of his building: just outside it, on the pavement.</summary>
        public Vector3 Door;
        /// <summary>The lane with the hanging lights: its mouth on the quay side and its far end on the back street.</summary>
        public Vector3 LaneNear, LaneFar;
        /// <summary>The stairway beyond the back street: the middle of its lowest edge, its top, and how it runs.</summary>
        public Vector3 StairFoot, StairTop, Gate;
        public const float StairRise = 0.17f, StairTread = 0.34f, StairWidth = 3f, StairWall = 1.02f;
        public const int StairSteps = 30;
        public Transform Boat;
        public readonly List<Transform> Jam = new(), Moving = new();
        /// <summary>The height of the stairway's steps under a point (world), and of the line its side walls follow.</summary>
        public float StairHeight(float z) => Mathf.Clamp((StairFoot.z - z) / StairTread, 0f, StairSteps) * StairRise;
    }

    public static partial class FilmWishSets
    {
        public const float QuayRoadZ = -6.9f, BackStreetZ = -30.5f;

        // ------------------------------------------------------------ Adam's city: a harbour in the blue hour
        /// <summary>The water is toward +Z; the quay runs along X; behind it a road, a first row of buildings, a
        /// narrow back street full of light, a second row, towers. <paramref name="traffic"/>: cars on the quay road.</summary>
        public static HarbourSet Harbour(FilmStage s, bool traffic = true, bool far = true, float moonYaw = 200f)
        {
            var set = new HarbourSet();
            if (s.Pipeline != null) s.Pipeline.shadowDistance = 45f;
            s.Camera.Cam.farClipPlane = 900f;

            // the blue hour: the sun is gone behind the city, a last band of rose low in the west
            WishKit.Sky(WishKit.DuskSky(205f, -7f, 0.85f, 0.7f, 0.7f), 1f);
            set.Moon = s.Sun(new Vector3(40f, moonYaw, 0f), new Color(0.5f, 0.6f, 1f), 0.55f, true, 0.75f);
            s.Ambient(new Color(0.17f, 0.19f, 0.38f), new Color(0.15f, 0.14f, 0.27f), new Color(0.075f, 0.065f, 0.1f));
            s.Haze(new Color(0.13f, 0.12f, 0.25f), 0.0042f);

            set.Iron = FilmStage.Mat(new Color(0.07f, 0.07f, 0.08f), 0.45f, 0.6f);
            set.Stone = FilmStage.Mat(new Color(0.42f, 0.4f, 0.42f), 0.25f, 0f, FilmTex.Pavers(), null, new Vector2(60f, 2f));
            set.Wood = FilmStage.Mat(new Color(0.36f, 0.25f, 0.18f), 0.3f);
            set.Bulbs = WishKit.Bulb(new Color(1f, 0.72f, 0.4f), 3.4f);
            set.Glass = WishKit.Bulb(new Color(1f, 0.8f, 0.55f), 3f);
            var asphalt = FilmStage.Mat(new Color(0.085f, 0.085f, 0.1f), 0.78f);      // wet from an evening shower: it holds the lights
            var kerb = FilmStage.Mat(new Color(0.3f, 0.3f, 0.33f), 0.2f);

            // ground, quay, water
            s.Box("Ground", new Vector3(0f, -1f, -110f), new Vector3(420f, 2f, 220f), asphalt);
            s.Box("Quay paving", new Vector3(0f, 0.02f, -1.7f), new Vector3(240f, 0.06f, 3.4f), set.Stone);
            s.Box("Pavement", new Vector3(0f, 0.04f, -11.25f), new Vector3(240f, 0.1f, 2.5f), kerb);
            s.Box("Quay wall", new Vector3(0f, -1.2f, 0.15f), new Vector3(240f, 2.6f, 0.5f), kerb);
            WishKit.Water(s, -1.25f, new Color(0.01f, 0.016f, 0.04f), new Vector3(0f, 0f, 250f), new Vector2(900f, 500f), 0.04f, 0.42f);
            s.Box("Back street paving", new Vector3(0f, 0.03f, BackStreetZ), new Vector3(240f, 0.06f, 8f), FilmStage.Mat(new Color(0.17f, 0.16f, 0.18f), 0.72f, 0f, FilmTex.Pavers(), null, new Vector2(60f, 2f)));
            WishKit.Railing(s, new Vector3(-95f, 0.05f, -0.3f), new Vector3(95f, 0.05f, -0.3f), 1.05f, set.Iron, 2.4f);
            for (var i = 0; i < 9; i++)
                s.Box("Lane mark", new Vector3(-80f + i * 20f, 0.006f, QuayRoadZ + 0.2f), new Vector3(3f, 0.012f, 0.14f), kerb, null, null, false);

            // lamps along the quay, and their light lying on the water
            for (var x = -90f; x <= 90f; x += 9f)
            {
                WishKit.StreetLamp(s, new Vector3(x, 0f, -1.5f), 4.2f, set.Iron, set.Glass, null, 0f, null, new Color(1f, 0.74f, 0.48f), 2.4f, 12f);
            }

            // the first row, on the water
            var walls = new[]
            {
                new Color(0.42f, 0.33f, 0.3f), new Color(0.3f, 0.32f, 0.4f), new Color(0.46f, 0.4f, 0.33f), new Color(0.34f, 0.3f, 0.36f),
                new Color(0.4f, 0.36f, 0.4f), new Color(0.36f, 0.4f, 0.42f), new Color(0.44f, 0.34f, 0.28f), new Color(0.3f, 0.3f, 0.34f),
            };
            var row1 = new (float x0, float x1, float h)[] { (-96f, -72f, 20f), (-71f, -58f, 16f), (-57f, -43f, 23f), (-42f, -30f, 15f), (-29f, -9f, 19f), (-8f, 8f, 22f), (9f, 16f, 17f), (24f, 37f, 20f), (38f, 52f, 14f), (53f, 70f, 25f), (71f, 96f, 18f) };
            for (var i = 0; i < row1.Length; i++)
            {
                var (x0, x1, h) = row1[i];
                WishKit.Block(s, "Row 1 " + i, new Vector3((x0 + x1) * 0.5f, 0f, -19.5f), new Vector3(x1 - x0, h, 14f), walls[i % walls.Length], 100 + i * 7, 0.44f, 1.7f);
            }
            // the second row, across the back street, and towers behind
            var row2 = new (float x0, float x1, float h)[] { (-98f, -70f, 30f), (-68f, -52f, 38f), (-50f, -30f, 27f), (-28f, -12f, 42f), (-10f, 16f, 31f), (24f, 44f, 36f), (46f, 62f, 28f), (64f, 98f, 40f) };
            for (var i = 0; i < row2.Length; i++)
            {
                var (x0, x1, h) = row2[i];
                WishKit.Block(s, "Row 2 " + i, new Vector3((x0 + x1) * 0.5f, 0f, -42.5f), new Vector3(x1 - x0, h, 16f), walls[(i + 3) % walls.Length] * 0.85f, 300 + i * 5, 0.36f, 1.5f);
            }
            if (far)
            {
                var towers = new (float x, float z, float w, float h)[] { (-80f, -78f, 22f, 64f), (-44f, -84f, 18f, 88f), (-14f, -74f, 20f, 56f), (30f, -90f, 24f, 96f), (62f, -76f, 18f, 70f), (96f, -88f, 22f, 60f), (-120f, -70f, 26f, 48f), (128f, -64f, 26f, 52f) };
                for (var i = 0; i < towers.Length; i++)
                {
                    var (x, z, w, h) = towers[i];
                    WishKit.Block(s, "Tower " + i, new Vector3(x, 0f, z), new Vector3(w, h, w), new Color(0.2f, 0.22f, 0.3f), 500 + i * 3, 0.3f, 1.4f, 3.4f, 2.2f, false);
                    s.Ball("Beacon", new Vector3(x, h + 1.2f, z), 0.9f, FilmStage.Glow(new Color(5f, 0.3f, 0.2f, 1f)), null, false);
                }
                // the far side of the harbour: a low line of lights
                s.Card("Far shore", new Vector3(0f, 11f, 330f), new Vector2(900f, 30f), Vector3.back, FilmStage.Cutout(new Color(1.5f, 1.3f, 1.3f), FilmTex.Skyline(2048, 256, 47)));
                s.Card("Far shore 2", new Vector3(-260f, 9f, 180f), new Vector2(400f, 22f), new Vector3(1f, 0f, -0.4f), FilmStage.Cutout(new Color(1.4f, 1.2f, 1.2f), FilmTex.Skyline(2048, 256, 53)));
            }

            // shops and awnings along the quay fronts and both sides of the back street; lights strung across it
            var shopLights = new[] { new Color(1f, 0.8f, 0.5f), new Color(1f, 0.62f, 0.32f), new Color(0.6f, 0.85f, 1f), new Color(1f, 0.7f, 0.75f), new Color(0.95f, 0.9f, 0.7f) };
            var awnings = new[] { new Color(0.5f, 0.12f, 0.1f), new Color(0.1f, 0.24f, 0.3f), new Color(0.55f, 0.42f, 0.2f), new Color(0.16f, 0.3f, 0.2f) };
            var n = 0;
            for (var x = -66f; x <= 66f; x += 6.5f)
            {
                if (x > 14f && x < 26f) continue;                                    // the lane
                if (Mathf.Abs(x + 5f) < 2.5f) continue;                              // Adam's door
                WishKit.Shop(s, new Vector3(x, 0.05f, -12.5f), Vector3.forward, 4.6f, shopLights[n % 5], awnings[n % 4], null, 1.15f, true, 1.5f);
                WishKit.Shop(s, new Vector3(x + 1.5f, 0f, -26.5f), Vector3.back, 4.6f, shopLights[(n + 2) % 5], awnings[(n + 1) % 4], null, 1.15f, true, 1.7f);
                WishKit.Shop(s, new Vector3(x - 1f, 0f, -34.5f), Vector3.forward, 4.8f, shopLights[(n + 3) % 5], awnings[(n + 2) % 4], null, 1.15f, true, 1.7f);
                n++;
            }
            for (var x = -60f; x <= 60f; x += 5.5f)
            {
                if (x > 15f && x < 25f) continue;
                var zig = (Mathf.RoundToInt((x + 60f) / 5.5f) % 2 == 0) ? 1.6f : -1.6f;
                WishKit.StringLights(s, new Vector3(x - zig, 5.6f, -26.6f), new Vector3(x + zig, 5.6f, -34.4f), 0.7f, 13, set.Bulbs, set.Iron);
            }

            // the lane from the quay to the back street: lights overhead, a few tables
            set.LaneNear = new Vector3(20f, 0f, -12.5f);
            set.LaneFar = new Vector3(20f, 0f, -26.5f);
            for (var z = -13.5f; z >= -26f; z -= 2.5f)
                WishKit.StringLights(s, new Vector3(16.2f, 5.2f, z), new Vector3(23.8f, 5.2f, z - 1.2f), 0.6f, 11, set.Bulbs, set.Iron);
            WishKit.Shop(s, new Vector3(16.02f, 0f, -17f), Vector3.right, 4.2f, shopLights[1], awnings[0], null, 1.15f, true, 1.8f);
            WishKit.Shop(s, new Vector3(16.02f, 0f, -23f), Vector3.right, 4.2f, shopLights[4], awnings[2], null, 1.15f, true, 1.8f);
            WishKit.Shop(s, new Vector3(23.98f, 0f, -19.5f), Vector3.left, 5.2f, shopLights[2], awnings[1], null, 1.15f, true, 1.8f);

            // Adam's building: a balcony on the fourth floor over the water, a street door below
            const float floor = 9.3f;
            var slab = FilmStage.Mat(new Color(0.34f, 0.32f, 0.34f), 0.2f);
            s.Box("Balcony", new Vector3(0.3f, floor - 0.1f, -11.7f), new Vector3(4.4f, 0.2f, 1.7f), slab);
            WishKit.Railing(s, new Vector3(-1.9f, floor, -10.92f), new Vector3(2.5f, floor, -10.92f), 1.06f, set.Iron, 1.1f);
            WishKit.Railing(s, new Vector3(-1.9f, floor, -12.5f), new Vector3(-1.9f, floor, -10.92f), 1.06f, set.Iron, 1.6f);
            WishKit.Railing(s, new Vector3(2.5f, floor, -12.5f), new Vector3(2.5f, floor, -10.92f), 1.06f, set.Iron, 1.6f);
            // the room behind him: a warm doorway, a curtain stirring
            s.Card("Balcony door", new Vector3(0.3f, floor + 1.12f, -12.42f), new Vector2(1.9f, 2.24f), Vector3.forward, FilmStage.Glow(new Color(0.62f, 0.4f, 0.22f, 1f), WishKit.GlazedDoor()));
            WishKit.Cloth(s, "Balcony curtain", new Vector3(-0.1f, floor + 2.28f, -12.36f), Vector3.right, 1.25f, 2.2f, FilmStage.Mat(new Color(0.9f, 0.82f, 0.7f), 0.1f), 0.5f, 1.3f, null, 0.03f);
            s.Lamp("Room light", new Vector3(0.3f, floor + 1.7f, -11.9f), new Color(1f, 0.66f, 0.38f), 1.6f, 4.5f);
            set.Balcony = new Vector3(0.45f, floor, -11.42f);
            set.BalconyRail = new Vector3(0.45f, floor + 1.06f, -10.92f);
            s.Card("Street door", new Vector3(-5f, 1.15f, -12.4f), new Vector2(1.3f, 2.3f), Vector3.forward, FilmStage.Glow(new Color(0.6f, 0.4f, 0.22f, 1f), FilmTex.Gradient("hallway", new Color(0.35f, 0.3f, 0.26f), new Color(0.8f, 0.72f, 0.6f), new Color(1f, 0.95f, 0.85f))));
            s.Lamp("Street door light", new Vector3(-5f, 1.9f, -11.6f), new Color(1f, 0.72f, 0.44f), 1.6f, 5f);
            s.Box("Door step", new Vector3(-5f, 0.07f, -12.2f), new Vector3(1.8f, 0.14f, 0.6f), kerb);
            set.Door = new Vector3(-5f, 0.1f, -11.9f);

            // the stairway beyond the back street, between the walls of two tall buildings
            set.StairFoot = new Vector3(20f, 0f, -35.2f);
            var stairStone = FilmStage.Mat(new Color(0.36f, 0.35f, 0.38f), 0.22f);
            set.StairTop = WishKit.Stairs(s, "Stairway", set.StairFoot, Vector3.back, HarbourSet.StairSteps, HarbourSet.StairRise, HarbourSet.StairTread, HarbourSet.StairWidth, stairStone, null, HarbourSet.StairWall, FilmStage.Mat(new Color(0.3f, 0.28f, 0.3f), 0.2f));
            s.Box("Stair landing", set.StairTop + new Vector3(0f, -0.15f, -3f), new Vector3(4f, 0.3f, 6f), stairStone);
            var top = set.StairTop;
            var gate = FilmStage.Mat(new Color(0.2f, 0.19f, 0.22f), 0.2f);
            s.Box("Gate left", new Vector3(top.x - 2.4f, top.y + 3.2f, top.z - 1.3f), new Vector3(3.3f, 6.4f, 0.5f), gate);
            s.Box("Gate right", new Vector3(top.x + 2.4f, top.y + 3.2f, top.z - 1.3f), new Vector3(3.3f, 6.4f, 0.5f), gate);
            s.Box("Gate lintel", new Vector3(top.x, top.y + 4.5f, top.z - 1.3f), new Vector3(1.6f, 3.8f, 0.5f), gate);
            s.Card("Evening through the gate", new Vector3(top.x, top.y + 1.3f, top.z - 1.5f), new Vector2(1.6f, 2.6f), Vector3.forward,
                FilmStage.Glow(new Color(4.2f, 2.3f, 0.95f, 1f), FilmTex.Gradient("gate", new Color(1f, 0.62f, 0.3f), new Color(1f, 0.8f, 0.5f), new Color(0.95f, 0.7f, 0.62f))));
            set.Gate = new Vector3(top.x, top.y, top.z - 1.3f);
            s.Lamp("Evening on the stairs", new Vector3(top.x, top.y + 1.7f, top.z - 0.4f), new Color(1f, 0.66f, 0.36f), 7f, 15f);
            foreach (var z in new[] { -37.5f, -41.5f, -45f })
            {
                var y = set.StairHeight(z) + 3.2f;
                s.Ball("Wall lamp", new Vector3(22.75f, y, z), 0.3f, set.Glass, null, false);
                s.Lamp("Wall lamp light", new Vector3(22.4f, y - 0.1f, z), new Color(1f, 0.72f, 0.45f), 2.6f, 7f);
            }

            // traffic on the quay road: one lane stuck, the other moving
            if (traffic)
            {
                var paints = new[] { new Color(0.42f, 0.05f, 0.05f), new Color(0.06f, 0.08f, 0.16f), new Color(0.3f, 0.31f, 0.33f), new Color(0.06f, 0.18f, 0.26f), new Color(0.44f, 0.4f, 0.34f), new Color(0.05f, 0.05f, 0.06f), new Color(0.4f, 0.3f, 0.08f), new Color(0.08f, 0.2f, 0.12f) };
                var k = 0;
                for (var x = -84f; x <= 84f; x += 5.5f + 0.9f * Mathf.Abs(Mathf.Sin(x * 1.7f)))
                    set.Jam.Add(WishKit.Car(s, new Vector3(x, 0f, QuayRoadZ - 1.5f), 90f, paints[k++ % paints.Length]));
                for (var x = -80f; x <= 90f; x += 24f + 9f * Mathf.Abs(Mathf.Sin(x)))
                    set.Moving.Add(WishKit.Car(s, new Vector3(x, 0f, QuayRoadZ + 1.6f), -90f, paints[k++ % paints.Length], null, 1.2f));
                var jam = set.Jam.ToArray();
                var moving = set.Moving.ToArray();
                var jamHome = Array.ConvertAll(jam, c => c.position);
                var movingHome = Array.ConvertAll(moving, c => c.position);
                s.Moves.Add(t =>
                {
                    // the stuck lane creeps and stops; the other flows
                    var creep = 0.35f * t + 0.5f * Mathf.Sin(t * 0.8f);
                    for (var i = 0; i < jam.Length; i++) jam[i].position = jamHome[i] + Vector3.right * (creep + 0.15f * Mathf.Sin(t * 1.1f + i));
                    for (var i = 0; i < moving.Length; i++) moving[i].position = movingHome[i] + Vector3.left * (t * 7.5f);
                });
            }

            // a boat crossing the harbour
            set.Boat = WishKit.Boat(s, new Vector3(-30f, -1.1f, 26f), 90f);
            s.Reflections(new Vector3(0f, 1.5f, 14f), 1.25f);
            return set;
        }
    }

    public static partial class FilmWish
    {
        // set checks: a set from a few places (the camera jumps every second)
        static void Views(FilmStage s, params (Vector3 from, Vector3 to, float fov)[] views)
        {
            for (var i = 0; i < views.Length; i++)
            {
                s.Camera.Position.Key(i, views[i].from, Ease.Hold).Key(i + 0.99f, views[i].from, Ease.Hold);
                s.Camera.Target.Key(i, views[i].to, Ease.Hold).Key(i + 0.99f, views[i].to, Ease.Hold);
                s.Camera.Fov.Key(i, views[i].fov, Ease.Hold).Key(i + 0.99f, views[i].fov, Ease.Hold);
            }
            s.Camera.Aperture.Key(0f, 9f);
            s.Camera.Life = 0f;
        }

        static void SetHarbour(FilmStage s)
        {
            var set = FilmWishSets.Harbour(s);
            s.Camera.Grade(2f, 12f, 8f, 0.28f, 0.95f);
            var adam = s.Lead("adam", set.Balcony, 0f, 1f);
            adam.Play(0f, "m_idle_breathe_01");
            var walker = s.Extra("Male_Adult_12", new Vector3(10f, 0f, -30.5f), 90f, 4f);
            walker.Walk(0f, new Vector3(8f, 0f, -30.5f), new Vector3(30f, 0f, -30.5f), "m_walk_cool_01");
            var woman = s.Extra("Female_Adult_09", new Vector3(22f, 0f, -29f), -90f, 5f);
            woman.Walk(0f, new Vector3(26f, 0f, -29.5f), new Vector3(6f, 0f, -29.5f), "f_walk_neutral_01");
            Views(s,
                (new Vector3(-42f, 27f, 74f), new Vector3(-4f, 6f, -12f), 34f),          // the aerial, from over the water
                (new Vector3(-16f, 12f, 26f), new Vector3(-2f, 8f, -12f), 36f),          // lower, nearer
                (new Vector3(3.2f, 11f, -8.6f), new Vector3(0.4f, 10.8f, -11.4f), 30f),  // the balcony, from in front
                (new Vector3(6f, 1.7f, -30.5f), new Vector3(40f, 2f, -30.5f), 40f),      // along the back street
                (new Vector3(20f, 1.6f, -10f), new Vector3(20f, 2f, -40f), 40f),         // down the lane to the stairs
                (new Vector3(13f, 3.6f, -39.5f), new Vector3(20f, 3.4f, -40.5f), 40f),   // the stairway from the side
                (new Vector3(-9f, 1.6f, -9.5f), new Vector3(30f, 1.2f, -8f), 38f));      // the jam on the quay road
        }
    }
}
