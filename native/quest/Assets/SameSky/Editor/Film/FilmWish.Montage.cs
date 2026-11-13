// "Wish You Were Here", shots w06 to w11: the world is alive.
//   w06   Adam through the lit back street, people all round him
//   w07   Maya passes three musicians on the walk, the sea behind her
//   w08   a stranger on a bench moves over for someone
//   w09a  Adam's hand along the quay rail   /   w09b  Maya's hand along the sea wall (a match cut)
//   w10a  cloths on a stall in the wind, her shadow passing behind them
//   w10b  his feet on the wet paving, faster now
//   w11   a hat gets away from its owner, and Maya laughs

using UnityEngine;

namespace SameSky.Film
{
    public static partial class FilmWish
    {
        /// <summary>A laugh: the face opens, the shoulders shake, the head goes back a little.</summary>
        static void Laugh(FilmActor who, float t, float seconds, float strength = 1f)
        {
            who.Smile(t - 0.25f, 30f * strength);
            who.Smile(t, 92f * strength);
            who.Smile(t + seconds, 70f * strength);
            who.Smile(t + seconds + 0.5f, 48f * strength);
            var open = who.Shape("aa");
            open.Key(t - 0.1f, 0f);
            for (var k = 0f; k < seconds; k += 0.23f)
                open.Key(t + k, (k % 0.46f < 0.2f ? 34f : 16f) * strength).Key(t + k + 0.115f, 8f * strength);
            open.Key(t + seconds + 0.15f, 0f);
            who.Both("EyeSquint", t - 0.1f, 0f);
            who.Both("EyeSquint", t + 0.1f, 55f * strength);
            who.Both("EyeSquint", t + seconds, 30f * strength);
            who.Nod.Key(t - 0.1f, 0f).Key(t + 0.2f, -7f * strength).Key(t + seconds * 0.6f, 3f * strength).Key(t + seconds + 0.2f, 0f);
            who.Shrug.Key(t - 0.05f, 0f);
            for (var k = 0f; k < seconds; k += 0.2f)
                who.Shrug.Key(t + k, 4.5f * strength).Key(t + k + 0.1f, 1f * strength);
            who.Shrug.Key(t + seconds + 0.1f, 0f);
        }

        // ------------------------------------------------------------ w06
        static void W06(FilmStage s)
        {
            var set = FilmWishSets.Harbour(s, false, false);
            NightGrade(s, 0.55f);
            const float z = -30.3f;
            var pace = FilmCast.Pace("m_walk_fast_01") * 1.0f;
            var adam = s.Lead("adam", new Vector3(-1f, 0f, z), 90f, 1f);
            adam.Walk(0f, new Vector3(-1f, 0f, z), new Vector3(40f, 0f, z), "m_walk_fast_01", 1f, 0f, 0.2f);
            adam.Gaze(0f, new Vector3(30f, 1.7f, z), 0.4f);
            adam.Gaze(0.5f, new Vector3(6f, 2.2f, -26.6f), 0.7f, 1f);            // a window he passes
            adam.Gaze(1.35f, new Vector3(30f, 1.7f, z), 0.5f, 1f, true);
            adam.Smile(0f, 10f);
            adam.Smile(1.0f, 34f);
            adam.Smile(2.2f, 22f);
            Follow(s, adam, new Vector3(2.2f, 2.0f, -0.9f), WarmKey, 1.7f, 5.5f);
            Follow(s, adam, new Vector3(-1.4f, 2.2f, 1.4f), NightKey, 1.2f, 5f);
            // the street: people coming and going, two who have stopped to talk, someone laughing
            Stroller(s, "Female_Adult_09", new Vector3(11f, 0f, -29.2f), new Vector3(-30f, 0f, -29.2f), "f_walk_neutral_01", 51f);
            Stroller(s, "Male_Adult_12", new Vector3(15f, 0f, -31.6f), new Vector3(-30f, 0f, -31.4f), "m_walk_cool_01", 52f);
            Stroller(s, "Female_Adult_02", new Vector3(-3.2f, 0f, -31.7f), new Vector3(40f, 0f, -31.7f), "f_walk_stroll_01", 53f);
            Stroller(s, "Male_Adult_03", new Vector3(21f, 0f, -29.6f), new Vector3(-30f, 0f, -29.8f), "m_walk_slow_01", 54f);
            Bystander(s, "Female_Adult_02", new Vector3(8.6f, 0f, -27.5f), 150f, "f_gestic_laugh_low", 55f, new Color(0.7f, 0.85f, 1f));
            Bystander(s, "Male_Adult_12", new Vector3(9.5f, 0f, -27.9f), -60f, "m_gestic_laugh_low", 56f, new Color(1f, 0.8f, 0.7f));
            Bystander(s, "Female_Adult_09", new Vector3(3.2f, 0f, -33.4f), 20f, "f_idle_look_around_01", 57f, new Color(0.9f, 0.7f, 0.7f));

            s.Camera.Position.Key(0f, 2.3f, 1.52f, -30.85f, Ease.Linear).Key(2.5f, 2.3f + pace * 2.5f - 0.5f, 1.56f, -30.7f, Ease.Linear);
            s.Camera.Target.Key(0f, -1f, 1.46f, z, Ease.Linear).Key(2.5f, -1f + pace * 2.5f, 1.48f, z, Ease.Linear);
            s.Camera.Fov.Key(0f, 34f);
            s.Camera.Aperture.Key(0f, 2.4f);
            s.Camera.Life = 1.6f;
        }

        // ------------------------------------------------------------ w07
        static Transform Guitar(FilmStage s, Vector3 at, Quaternion turn)
        {
            var root = new GameObject("Guitar").transform;
            root.SetParent(s.Root, false);
            root.SetPositionAndRotation(at, turn);
            var top = FilmStage.Mat(new Color(0.72f, 0.5f, 0.28f), 0.5f);
            var dark = FilmStage.Mat(new Color(0.16f, 0.1f, 0.07f), 0.4f);
            // the body lies in the guitar's own x-y plane; the neck runs along +x
            s.Cylinder("Lower bout", new Vector3(-0.1f, 0f, 0f), 0.38f, 0.1f, top, root, new Vector3(90f, 0f, 0f));
            s.Cylinder("Upper bout", new Vector3(0.1f, 0f, 0f), 0.29f, 0.1f, top, root, new Vector3(90f, 0f, 0f));
            s.Cylinder("Sound hole", new Vector3(0.03f, 0f, -0.051f), 0.1f, 0.004f, dark, root, new Vector3(90f, 0f, 0f), false);
            s.Box("Neck", new Vector3(0.46f, 0f, -0.03f), new Vector3(0.48f, 0.05f, 0.025f), dark, root);
            s.Box("Head", new Vector3(0.76f, 0f, -0.03f), new Vector3(0.14f, 0.07f, 0.02f), dark, root);
            return root;
        }

        static void W07(FilmStage s)
        {
            // (the sun over the sea, behind the camera: it lights whoever looks this way)
            var set = FilmWishSets.Coast(s, 8f, 6f);
            SunsetGrade(s);
            // the players stand with their backs to the sea wall; we look past them at the walk
            var guitarist = Bystander(s, "Male_Adult_12", new Vector3(11.2f, 0f, -1.5f), 186f, "m_idle_breathe_01", 61f);
            var guitar = Guitar(s, new Vector3(11.19f, 1.1f, -1.68f), Quaternion.Euler(0f, 186f, 0f) * Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(0f, 0f, 24f));
            Vector3 On(float x, float y, float z) => guitar.TransformPoint(new Vector3(x, y, z));
            guitarist.Left.Goal.Key(0f, On(0.5f, 0.02f, -0.07f));
            guitarist.Left.Weight.Key(0f, 1f);
            guitarist.Left.Grip.Key(0f, 0.7f);
            guitarist.Right.Live = t => On(-0.02f, 0.02f + 0.06f * Mathf.Sin(t * 12.6f), -0.1f);
            guitarist.Right.Weight.Key(0f, 1f);
            guitarist.Right.Grip.Key(0f, 0.45f);
            guitarist.Tilt.Key(0f, 3f).Key(0.6f, -3f).Key(1.25f, 3f).Key(1.9f, -3f).Key(2.5f, 3f);
            guitarist.Nod.Key(0f, 2f).Key(0.3f, 6f).Key(0.6f, 2f).Key(0.92f, 6f).Key(1.25f, 2f).Key(1.55f, 6f).Key(1.9f, 2f).Key(2.2f, 6f).Key(2.5f, 2f);
            Bystander(s, "Female_Adult_09", new Vector3(13.55f, 0f, -2.0f), 235f, "f_dancing_neutral", 62f, null, 1.5f);
            Bystander(s, "Male_Adult_03", new Vector3(14.2f, 0f, -3.3f), 240f, "m_claphands_01", 63f);
            // Maya comes along the walk in front of them, and looks
            var maya = s.Lead("maya", new Vector3(10.2f, 0f, -4.3f), 90f, 2f);
            maya.Walk(0f, new Vector3(10.2f, 0f, -4.3f), new Vector3(30f, 0f, -4.4f), "f_walk_stroll_01", 1.05f, 0f, 0.4f);
            maya.Wind.Key(0f, 0.7f);
            maya.WindTo = new Vector3(-1f, 0.1f, -0.35f);
            maya.Gaze(0f, new Vector3(11.2f, 1.6f, -1.5f), 1f);
            maya.Twist.Key(0f, 34f).Key(1.4f, 40f).Key(2.5f, 46f);
            maya.Smile(0f, 40f);
            maya.Smile(0.7f, 82f);
            maya.Both("EyeSquint", 0.3f, 0f);
            maya.Both("EyeSquint", 0.8f, 32f);
            // she lifts a hand to them as she goes by, and her step picks up the beat
            maya.Left.Live = t => maya.Root.transform.position + new Vector3(0.12f, 1.3f + 0.03f * Mathf.Sin(t * 9f), 0.36f);
            maya.Left.Weight.Key(0.7f, 0f).Key(1.1f, 1f, Ease.Out).Key(2.0f, 1f).Key(2.45f, 0f);
            maya.Left.Fingers.Key(0f, new Vector3(0.1f, 1f, 0.1f)).Key(1.2f, new Vector3(0.35f, 1f, 0.1f)).Key(1.4f, new Vector3(-0.2f, 1f, 0.1f)).Key(1.6f, new Vector3(0.35f, 1f, 0.1f)).Key(1.8f, new Vector3(-0.2f, 1f, 0.1f)).Key(2.0f, new Vector3(0.1f, 1f, 0.1f));
            maya.Left.Palm.Key(0f, new Vector3(0f, 0f, 1f));
            maya.Left.Aim.Key(0.7f, 0f).Key(1.1f, 1f).Key(2.0f, 1f).Key(2.45f, 0f);
            for (var k = 0; k < 5; k++) maya.Offset.Key(k * 0.625f, 0f, 0f, 0f).Key(k * 0.625f + 0.31f, 0f, 0.022f, 0f);
            Stroller(s, "Female_Adult_02", new Vector3(20f, 0f, -6.4f), new Vector3(0f, 0f, -6.6f), "f_walk_stroll_01", 64f);

            s.Camera.Position.Key(0f, 11.95f, 1.28f, 0.42f).Key(2.5f, 12.15f, 1.3f, 0.3f, Ease.Linear);
            s.Camera.Target.Key(0f, 11.5f, 1.3f, -4.3f).Key(2.5f, 12.5f, 1.32f, -4.3f);
            s.Camera.Focus.Key(0f, 10.4f, 1.5f, -4.3f).Key(2.5f, 12.9f, 1.5f, -4.3f);
            s.Camera.Fov.Key(0f, 30f);
            s.Camera.Aperture.Key(0f, 2.6f);
            s.Camera.Life = 1.3f;
        }

        // ------------------------------------------------------------ w08
        static void W08(FilmStage s)
        {
            var set = FilmWishSets.Coast(s, -30f, 6f);
            SunsetGrade(s);
            var seat = set.Bench;
            var man = s.Extra("Male_Adult_03", seat, 0f, 71f);
            Sit(man, 0f, seat + new Vector3(0.05f, 0f, 0f), 0f);
            // he sees her coming, and moves over
            man.Path.Key(0.35f, seat + new Vector3(0.05f, 0f, 0f)).Key(0.9f, seat + new Vector3(-0.52f, 0f, 0f));
            man.Offset.Key(0.35f, 0f, 0f, 0f).Key(0.6f, 0f, 0.035f, 0f).Key(0.9f, 0f, 0f, 0f);
            man.Lean.Key(0.3f, 0f).Key(0.6f, 9f).Key(1.0f, 2f);
            man.Gaze(0f, new Vector3(27f, 1.4f, 30f), 0.4f);
            man.Gaze(0.12f, new Vector3(29.6f, 1.55f, -2.3f), 0.9f);
            // and pats the place beside him
            var free = seat + new Vector3(0.45f, WishKit.SeatHeight + 0.06f, 0.02f);
            man.Right.Goal.Key(0.85f, free + Vector3.up * 0.1f).Key(1.08f, free, Ease.In).Key(1.2f, free + Vector3.up * 0.07f).Key(1.34f, free, Ease.In).Key(1.5f, free + Vector3.up * 0.04f);
            man.Right.Weight.Key(0.7f, 0f).Key(1.0f, 1f);
            man.Right.Fingers.Key(0f, new Vector3(0.9f, -0.1f, 0.4f));
            man.Right.Palm.Key(0f, Vector3.down);
            man.Right.Aim.Key(0.7f, 0f).Key(1.0f, 1f);
            var woman = s.Extra("Female_Adult_02", seat, 0f, 72f);
            var arrive = woman.Walk(0f, new Vector3(30.4f, 0f, -2.2f), new Vector3(28.05f, 0f, -2.55f), "f_walk_stroll_01", 1.2f);
            woman.Play(arrive, "f_idle_breathe_01", 0.35f);
            woman.Heading.Key(arrive, -98f).Key(arrive + 0.5f, -150f);
            woman.Gaze(0f, man.Head.position, 0.8f);
            // Maya goes by behind them
            var maya = s.Lead("maya", new Vector3(22.6f, 0f, -5.3f), 90f, 2f);
            maya.Walk(0f, new Vector3(22.6f, 0f, -5.3f), new Vector3(40f, 0f, -5.2f), "f_walk_neutral_01", 1.05f, 0f, 0.1f);
            maya.Gaze(0f, new Vector3(27f, 1.2f, -3.2f), 0.7f);
            maya.Smile(0f, 40f);

            s.Camera.Position.Key(0f, 26.2f, 1.42f, 0.75f).Key(1.875f, 26.45f, 1.44f, 0.45f, Ease.Linear);
            s.Camera.Target.Key(0f, 27.25f, 0.95f, -3.2f);
            s.Camera.Fov.Key(0f, 34f);
            s.Camera.Aperture.Key(0f, 3.2f);
        }

        // ------------------------------------------------------------ w09a, w09b
        /// <summary>A hand trailing along the top of something at <paramref name="height"/> as its owner walks along
        /// +X beside it; the camera travels with the hand, looking back along it.</summary>
        static void Trailing(FilmStage s, FilmActor who, string clip, float railZ, float height, float walkZ, float ground, float speed)
        {
            var from = new Vector3(4f, ground, walkZ);
            who.Walk(0f, from, from + Vector3.right * 30f, clip, speed, 0f, 0.35f);
            var pace = FilmCast.Pace(clip) * speed;
            var hand = who.Left;
            hand.Live = t => new Vector3(who.Root.transform.position.x + 0.16f + 0.02f * Mathf.Sin(t * 5.3f), height + 0.035f, railZ - 0.01f);
            hand.Weight.Key(0f, 1f);
            hand.Fingers.Key(0f, new Vector3(1f, -0.12f, 0.12f));
            hand.Palm.Key(0f, new Vector3(0f, -1f, 0.1f));
            hand.Aim.Key(0f, 1f);
            // the fingers ride the surface: they lift and settle
            hand.Grip.Key(0f, 0.28f).Key(0.35f, 0.16f).Key(0.7f, 0.3f).Key(1.0f, 0.2f).Key(1.25f, 0.3f);
            hand.Index.Key(0f, 0.12f).Key(0.3f, 0.3f).Key(0.6f, 0.08f).Key(0.95f, 0.26f).Key(1.25f, 0.1f);
            var x0 = from.x + 0.16f;
            s.Camera.Position.Key(0f, x0 + 0.52f, height + 0.17f, railZ + 0.42f, Ease.Linear).Key(1.25f, x0 + 0.5f + pace * 1.25f, height + 0.15f, railZ + 0.4f, Ease.Linear);
            s.Camera.Target.Key(0f, x0 + 0.06f, height + 0.02f, railZ, Ease.Linear).Key(1.25f, x0 + 0.06f + pace * 1.25f, height + 0.02f, railZ, Ease.Linear);
            s.Camera.Fov.Key(0f, 30f);
            s.Camera.Aperture.Key(0f, 2f);
            s.Camera.Life = 0.8f;
        }

        static void W09a(FilmStage s)
        {
            var set = FilmWishSets.Harbour(s, true, false);
            NightGrade(s, 0.6f);
            var adam = s.Lead("adam", new Vector3(4f, 0.05f, -0.8f), 90f, 1f);
            Trailing(s, adam, "m_walk_neutral_01", -0.3f, 1.1f, -0.8f, 0.05f, 1.15f);
            Follow(s, adam, new Vector3(0.7f, 1.7f, 1.0f), WarmKey, 1.3f, 3.5f);
        }

        static void W09b(FilmStage s)
        {
            var set = FilmWishSets.Coast(s, -30f, 6f);
            SunsetGrade(s);
            var maya = s.Lead("maya", new Vector3(4f, 0f, -0.86f), 90f, 2f);
            Trailing(s, maya, "f_walk_neutral_01", -0.36f, CoastSet.WallTop, -0.86f, 0f, 1.1f);
        }

        // ------------------------------------------------------------ w10a
        static void W10a(FilmStage s)
        {
            // (the sun low over the sea behind the camera: her shadow goes across the cloths with her)
            var set = FilmWishSets.Coast(s, 22f, 4.5f);
            SunsetGrade(s, 0.05f);
            // a stall of cloths on the walk: a frame, and what hangs from it
            const float beam = 2.4f, line = -2.5f;
            foreach (var x in new[] { 17.3f, 20.4f, 23.5f })
                s.Box("Stall post", new Vector3(x, beam * 0.5f, line - 0.06f), new Vector3(0.09f, beam, 0.09f), set.Wood);
            s.Box("Stall beam", new Vector3(20.4f, beam + 0.05f, line - 0.06f), new Vector3(6.5f, 0.09f, 0.09f), set.Wood);
            var colours = new[] { new Color(0.95f, 0.9f, 0.8f), new Color(0.86f, 0.42f, 0.26f), new Color(0.3f, 0.56f, 0.58f), new Color(0.95f, 0.8f, 0.45f), new Color(0.93f, 0.88f, 0.8f) };
            for (var i = 0; i < 5; i++)
                WishKit.Cloth(s, "Cloth " + i, new Vector3(17.9f + i * 1.24f, beam, line), Vector3.left, 1.14f, 2.0f - 0.22f * (i % 2), FilmStage.Mat(colours[i], 0.08f), 1.5f, 1.7f * i + 0.4f, null, 0.03f);
            // she walks along in front of them
            var maya = s.Lead("maya", new Vector3(18.3f, 0f, -1.5f), 90f, 2f);
            maya.Walk(0f, new Vector3(18.3f, 0f, -1.5f), new Vector3(40f, 0f, -1.5f), "f_walk_neutral_01", 1.1f, 0f, 0.2f);
            maya.Wind.Key(0f, 0.9f);
            maya.WindTo = new Vector3(-1f, 0.1f, -0.35f);
            maya.Gaze(0f, new Vector3(19.5f, 1.5f, -2.5f), 0.6f);
            maya.Gaze(0.55f, new Vector3(30f, 1.5f, -1.5f), 0.6f);
            maya.Smile(0f, 45f);
            // her hand goes out to the cloth as she passes
            maya.Right.Live = t => maya.Root.transform.position + new Vector3(0.22f, 1.05f, -0.52f);
            maya.Right.Weight.Key(0f, 0.9f).Key(0.9f, 0.9f).Key(1.25f, 0.3f);
            maya.Right.Fingers.Key(0f, new Vector3(0.5f, 0f, -1f));
            maya.Right.Palm.Key(0f, new Vector3(-0.7f, -0.6f, 0f));
            maya.Right.Aim.Key(0f, 1f);

            var pace = FilmCast.Pace("f_walk_neutral_01") * 1.1f;
            s.Camera.Position.Key(0f, 19.3f, 1.4f, 0.5f, Ease.Linear).Key(1.25f, 19.3f + pace * 0.8f, 1.4f, 0.45f, Ease.Linear);
            s.Camera.Target.Key(0f, 18.9f, 1.32f, -1.9f, Ease.Linear).Key(1.25f, 18.9f + pace * 1.0f, 1.32f, -1.9f, Ease.Linear);
            s.Camera.Fov.Key(0f, 36f);
            s.Camera.Aperture.Key(0f, 4f);
        }

        // ------------------------------------------------------------ w10b
        static void W10b(FilmStage s)
        {
            var set = FilmWishSets.Harbour(s, false, false);
            NightGrade(s, 0.5f);
            const float z = -30.9f;
            var pace = FilmCast.Pace("m_walk_fast_01") * 1.12f;
            var adam = s.Lead("adam", new Vector3(8f, 0f, z), 90f, 1f);
            adam.Walk(0f, new Vector3(8f, 0f, z), new Vector3(60f, 0f, z), "m_walk_fast_01", 1.12f, 0f, 0.1f);
            Stroller(s, "Female_Adult_09", new Vector3(13.5f, 0f, -29.9f), new Vector3(-30f, 0f, -29.9f), "f_walk_neutral_01", 81f);

            s.Camera.Position.Key(0f, 8.9f, 0.16f, -32.1f, Ease.Linear).Key(1.25f, 8.9f + pace * 1.25f, 0.16f, -32.1f, Ease.Linear);
            s.Camera.Target.Key(0f, 8.25f, 0.2f, z, Ease.Linear).Key(1.25f, 8.25f + pace * 1.25f, 0.2f, z, Ease.Linear);
            s.Camera.Fov.Key(0f, 30f);
            s.Camera.Aperture.Key(0f, 2.2f);
            s.Camera.Life = 1.2f;
        }

        // ------------------------------------------------------------ w11
        static void W11(FilmStage s)
        {
            var set = FilmWishSets.Coast(s, 58f, 6f);
            SunsetGrade(s);
            const float z = -2.7f, x0 = 30f;
            var pace = FilmCast.Pace("f_walk_neutral_01");
            var maya = s.Lead("maya", new Vector3(x0, 0f, z), 90f, 2f);
            maya.Walk(0f, new Vector3(x0, 0f, z), new Vector3(60f, 0f, z), "f_walk_neutral_01", 1.0f, 0f, 0.3f);
            maya.Wind.Key(0f, 1f);
            maya.WindTo = new Vector3(-1f, 0.1f, -0.2f);
            float CamX(float t) => x0 + 2.75f + pace * t - 0.12f * t;
            // a straw hat comes down the wind past her head
            var hat = new GameObject("Hat").transform;
            hat.SetParent(s.Root, false);
            var straw = FilmStage.Mat(new Color(0.9f, 0.76f, 0.46f), 0.25f);
            s.Cylinder("Brim", Vector3.zero, 0.4f, 0.012f, straw, hat);
            s.Cylinder("Crown", new Vector3(0f, 0.055f, 0f), 0.2f, 0.1f, straw, hat);
            s.Cylinder("Band", new Vector3(0f, 0.022f, 0f), 0.205f, 0.03f, FilmStage.Mat(new Color(0.3f, 0.12f, 0.1f), 0.2f), hat);
            const float pass = 1.0f, hatSpeed = 4.4f;                       // when it reaches her, and how fast it travels
            var passX = x0 + pace * pass;
            s.Moves.Add(t =>
            {
                var x = passX - hatSpeed * (t - pass);
                var y = 1.52f + 0.5f * (pass - t) + 0.07f * Mathf.Sin(t * 6f);
                if (t > pass) y = Mathf.Max(0.12f, 1.52f - 0.75f * (t - pass) * (t - pass) - 0.25f * (t - pass));
                hat.position = new Vector3(x, y, z + 0.4f + 0.06f * Mathf.Sin(t * 3.3f));
                hat.rotation = Quaternion.Euler(t * 260f, t * 90f, 25f + t * 410f);
            });
            // she sees it, leans out of its way, and laughs after it
            maya.Gaze(0f, new Vector3(x0 + 20f, 1.5f, z - 2f), 0.5f);
            maya.Gaze(0.45f, new Vector3(passX + 2.5f, 1.9f, z + 0.4f), 0.6f, 1f, true);
            maya.Smile(0f, 26f);
            maya.Shape("EyeWideLeft").Key(0.45f, 0f).Key(0.7f, 60f).Key(pass + 0.25f, 0f);
            maya.Shape("EyeWideRight").Key(0.45f, 0f).Key(0.7f, 60f).Key(pass + 0.25f, 0f);
            maya.Shape("BrowInnerUp").Key(0.45f, 0f).Key(0.7f, 55f).Key(pass + 0.5f, 12f);
            maya.Side.Key(pass - 0.35f, 0f).Key(pass - 0.05f, 13f, Ease.Out).Key(pass + 0.5f, 3f).Key(pass + 1f, 0f);
            maya.Offset.Key(pass - 0.35f, 0f, 0f, 0f).Key(pass - 0.05f, 0f, -0.03f, -0.16f, Ease.Out).Key(pass + 0.8f, 0f, 0f, -0.1f).Key(pass + 1.6f, 0f, 0f, 0f);
            maya.Gaze(pass + 0.08f, new Vector3(x0 - 12f, 1.0f, z + 1.4f), 1f, 1f, true);
            maya.Twist.Key(pass + 0.05f, 0f).Key(pass + 0.45f, 40f).Key(pass + 0.9f, 36f).Key(pass + 1.35f, 0f);
            maya.Gaze(pass + 0.95f, new Vector3(x0 + 30f, 1.5f, z - 1f), 0.6f, 1f, true);
            Laugh(maya, pass + 0.2f, 1.8f);
            // its owner goes after it, past us and past her
            var owner = s.Extra("Male_Adult_03", new Vector3(x0 + 9f, 0f, z + 0.85f), -90f, 91f);
            var run = FilmCast.Pace("m_walk_fast_01") * 1.8f;
            var startX = CamX(1.12f) + 0.3f + run * 1.12f;                   // he comes into the picture just after the hat
            owner.Walk(0f, new Vector3(startX, 0f, z + 0.85f), new Vector3(startX - 40f, 0f, z + 0.6f), "m_walk_fast_01", 1.8f, 0f, 0.1f);
            owner.Lean.Key(0f, 22f);
            owner.Right.Live = t => owner.Root.transform.position + new Vector3(-0.6f, 1.05f + 0.05f * Mathf.Sin(t * 11f), -0.1f);
            owner.Right.Weight.Key(0f, 0.95f);
            owner.Right.Fingers.Key(0f, new Vector3(-1f, 0.1f, 0f));
            owner.Right.Palm.Key(0f, Vector3.down);
            owner.Right.Aim.Key(0f, 1f);
            Stroller(s, "Female_Adult_09", new Vector3(24f, 0f, -5.6f), new Vector3(0f, 0f, -5.4f), "f_walk_stroll_01", 92f);

            s.Camera.Position.Key(0f, CamX(0f), 1.46f, z - 0.42f, Ease.Linear).Key(3.125f, CamX(3.125f), 1.48f, z - 0.36f, Ease.Linear);
            s.Camera.Target.Key(0f, x0, 1.4f, z + 0.06f, Ease.Linear).Key(3.125f, x0 + pace * 3.125f, 1.42f, z, Ease.Linear);
            s.Camera.Fov.Key(0f, 33f);
            s.Camera.Aperture.Key(0f, 2.6f);
            s.Camera.Life = 1.3f;
        }

    }
}
