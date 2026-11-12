// "Wish You Were Here", shots w01 to w05: the hook and the two worlds.
//   w01  an aerial over Adam's harbour city at night
//   w02  Maya at the sea wall in the last sun: "You're missing the good part."
//   w03  Adam on his balcony in the blue hour: "Maybe not tonight."
//   w04  Maya turns from the wall and sets off along the coast walk
//   w05  Adam steps out of his street door, sees the traffic, and walks

using UnityEngine;

namespace SameSky.Film
{
    public static partial class FilmWish
    {
        static readonly Color NightKey = new(0.62f, 0.58f, 1f), WarmKey = new(1f, 0.7f, 0.44f);

        static void NightGrade(FilmStage s, float exposure = 0.35f)
        {
            s.Camera.Grade(0f, 14f, 10f, 0.3f, 1.0f, 4f);
            s.Camera.Exposure.Key(0f, exposure);
        }

        static void SunsetGrade(FilmStage s, float exposure = 0f)
        {
            s.Camera.Grade(10f, 12f, 6f, 0.25f, 0.7f, 2f);
            s.Camera.Exposure.Key(0f, exposure);
        }

        /// <summary>Someone in the background who walks from a to b for as long as the shot lasts.</summary>
        static FilmActor Stroller(FilmStage s, string person, Vector3 from, Vector3 to, string clip, float seed, float speed = 1f, Color? tint = null, float offset = 0f)
        {
            var who = s.Extra(person, from, 0f, seed, tint);
            who.Walk(0f, from, to, clip, speed, 0f, offset);
            return who;
        }

        /// <summary>Someone in the background who stays where they are, doing one thing.</summary>
        static FilmActor Bystander(FilmStage s, string person, Vector3 at, float yaw, string clip, float seed, Color? tint = null, float offset = 0f)
        {
            var who = s.Extra(person, at, yaw, seed, tint);
            who.Play(0f, clip, 0f, 1f, offset);
            return who;
        }

        /// <summary>Both hands on a rail or the top of a wall in front of someone, from the start of the shot.</summary>
        static void HandsOn(FilmActor who, Vector3 middle, float apart, Vector3 forward, float grip = 0.4f)
        {
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            foreach (var arm in new[] { who.Left, who.Right })
            {
                var side = arm.IsLeft ? -1f : 1f;
                arm.Goal.Key(0f, middle + right * (side * apart * 0.5f));
                arm.Weight.Key(0f, 1f);
                arm.Fingers.Key(0f, (forward - right * (side * 0.22f) + Vector3.down * 0.12f).normalized);
                arm.Palm.Key(0f, (Vector3.down - forward * 0.15f).normalized);
                arm.Aim.Key(0f, 1f);
                arm.Grip.Key(0f, grip);
            }
        }

        /// <summary>A lit, open room on a building front with someone in it: people seen through warm windows.</summary>
        static void Loggia(FilmStage s, HarbourSet set, float x, float floor, string person, string clip, float seed, float yaw = 0f)
        {
            const float z = -12.5f;
            var wall = FilmStage.Mat(new Color(0.34f, 0.3f, 0.3f), 0.2f);
            s.Box("Loggia floor", new Vector3(x, floor - 0.1f, z + 0.75f), new Vector3(3.6f, 0.2f, 1.6f), wall);
            s.Box("Loggia roof", new Vector3(x, floor + 2.75f, z + 0.75f), new Vector3(3.6f, 0.16f, 1.6f), wall);
            s.Card("Loggia room", new Vector3(x, floor + 1.3f, z + 0.05f), new Vector2(3.4f, 2.6f), Vector3.forward, FilmStage.Glow(new Color(1.5f, 0.95f, 0.5f, 1f), WishKit.ShopGlass((int)(seed * 3f))));
            WishKit.Railing(s, new Vector3(x - 1.75f, floor, z + 1.5f), new Vector3(x + 1.75f, floor, z + 1.5f), 1.05f, set.Iron, 1.2f);
            s.Lamp("Loggia light", new Vector3(x, floor + 2.2f, z + 0.9f), new Color(1f, 0.7f, 0.42f), 2.2f, 4.5f);
            Bystander(s, person, new Vector3(x + 0.3f * Mathf.Sin(seed), floor, z + 0.8f), yaw, clip, seed, null, seed * 0.7f);
        }

        // ------------------------------------------------------------ w01
        static void W01(FilmStage s)
        {
            var set = FilmWishSets.Harbour(s);
            NightGrade(s, 0.45f);
            // people in the lit rooms over the water
            Loggia(s, set, -21f, 6.2f, "Female_Adult_09", "f_dancing_neutral", 3f, 20f);
            Loggia(s, set, -14.5f, 12.4f, "Male_Adult_12", "m_claphands_01", 5f, -15f);
            Loggia(s, set, -24.5f, 12.4f, "Female_Adult_02", "f_gestic_laugh_low", 7f, 25f);
            Loggia(s, set, 13f, 6.2f, "Male_Adult_03", "m_dancing_cool", 9f, -10f);
            var adam = s.Lead("adam", set.Balcony, 0f, 1f);
            adam.Play(0f, "m_idle_breathe_01");
            HandsOn(adam, set.BalconyRail + new Vector3(0f, 0.05f, -0.04f), 0.5f, Vector3.forward);
            // people on the quay
            Stroller(s, "Female_Adult_02", new Vector3(-30f, 0.05f, -1.9f), new Vector3(10f, 0.05f, -1.9f), "f_walk_stroll_01", 11f);
            Stroller(s, "Male_Adult_03", new Vector3(-4f, 0.05f, -2.3f), new Vector3(-40f, 0.05f, -2.3f), "m_walk_neutral_01", 12f);
            Stroller(s, "Male_Adult_12", new Vector3(-18f, 0.05f, -2.6f), new Vector3(20f, 0.05f, -2.6f), "m_walk_cool_01", 13f);
            Stroller(s, "Female_Adult_09", new Vector3(8f, 0.05f, -1.7f), new Vector3(-30f, 0.05f, -1.7f), "f_walk_neutral_01", 14f);
            // the boat crosses below the camera
            var boat = set.Boat;
            s.Moves.Add(t => boat.position = new Vector3(-42f + t * 5.5f, -1.1f + 0.05f * Mathf.Sin(t * 1.3f), 27f));

            s.Camera.Position.Key(0f, -56f, 15.5f, 70f, Ease.Linear).Key(2.5f, -36f, 11.5f, 46f, Ease.Out);
            s.Camera.Target.Key(0f, -10f, 12.5f, -12f).Key(2.5f, -3f, 12.5f, -12f);
            s.Camera.Fov.Key(0f, 36f).Key(2.5f, 33f);
            s.Camera.Roll.Key(0f, -3.5f).Key(2.5f, 0f, Ease.Out);
            s.Camera.Aperture.Key(0f, 8f);
            s.Camera.Life = 0.6f;
        }

        // ------------------------------------------------------------ w02
        static void W02(FilmStage s)
        {
            // (the sun over the sea behind the camera's left shoulder: its light is on whatever turns toward us)
            var set = FilmWishSets.Coast(s, -38f, 6f);
            SunsetGrade(s);
            var at = new Vector3(0.2f, 0f, -0.76f);
            var maya = s.Lead("maya", at, 0f, 2f);
            maya.Play(0f, "f_idle_breathe_01");
            // forearms on the wall
            foreach (var arm in new[] { maya.Left, maya.Right })
            {
                var side = arm.IsLeft ? -1f : 1f;
                arm.Goal.Key(0f, new Vector3(at.x + side * 0.14f, CoastSet.WallTop + 0.05f, -0.46f));
                arm.Weight.Key(0f, 1f);
                arm.Fingers.Key(0f, new Vector3(-side * 0.4f, -0.05f, 1f).normalized);
                arm.Palm.Key(0f, Vector3.down);
                arm.Aim.Key(0f, 1f);
                arm.Grip.Key(0f, 0.12f);
                arm.Elbow = new Vector3(side * 1f, -0.4f, -0.5f);
            }
            maya.Lean.Key(0f, 14f).Key(1.2f, 11f).Key(3.1f, 13f);
            maya.Wind.Key(0f, 0.85f).Key(1.4f, 1f).Key(3.1f, 0.7f);
            maya.WindTo = new Vector3(1f, 0.1f, -0.3f);
            // out at the sun, then to the empty place beside her
            var sea = new Vector3(3f, 1.62f, 40f);
            var beside = new Vector3(-0.86f, 1.6f, -0.96f);
            maya.Gaze(0f, beside, 0.9f);
            maya.Gaze(1.95f, sea, 0.7f, 1f, true);
            maya.Twist.Key(0f, 34f).Key(1.9f, 32f).Key(2.6f, 8f);
            maya.Smile(0f, 46f);
            maya.Smile(1.3f, 52f);
            maya.Smile(2.1f, 38f);
            maya.Smile(2.85f, 64f);
            maya.Shape("BrowInnerUp").Key(0f, 20f).Key(0.8f, 26f).Key(2.2f, 6f);
            maya.Tilt.Key(0f, 5f).Key(1.9f, 4f).Key(2.6f, 0f);
            maya.Say(-0.3f, Voice("maya-good-part"));
            // a few people further along the walk, soft in the distance
            Stroller(s, "Male_Adult_03", new Vector3(12f, 0f, -2.6f), new Vector3(-14f, 0f, -3f), "m_walk_slow_01", 21f);
            Stroller(s, "Female_Adult_09", new Vector3(5f, 0f, -4.6f), new Vector3(30f, 0f, -4.2f), "f_walk_stroll_01", 22f);
            Bystander(s, "Male_Adult_12", new Vector3(8.5f, 0f, -0.9f), 0f, "m_idle_breathe_01", 23f);
            s.Lamp("Soft fill", new Vector3(-1.0f, 1.75f, -1.9f), new Color(1f, 0.8f, 0.68f), 1.1f, 4.5f);

            s.Camera.Position.Key(0f, -1.34f, 1.5f, -0.5f).Key(3.125f, -1.2f, 1.5f, -0.56f, Ease.Linear);
            s.Camera.Target.Key(0f, 0.3f, 1.46f, -0.66f);
            s.Camera.Focus.Key(0f, 0.2f, 1.5f, -0.74f);
            s.Camera.Fov.Key(0f, 26f);
            s.Camera.Aperture.Key(0f, 2f);
        }

        // ------------------------------------------------------------ w03
        static void W03(FilmStage s)
        {
            var set = FilmWishSets.Harbour(s, true, false);
            NightGrade(s, 0.5f);
            var adam = s.Lead("adam", set.Balcony, 0f, 1f);
            adam.Play(0f, "m_idle_breathe_01");
            HandsOn(adam, set.BalconyRail + new Vector3(0f, 0.05f, -0.04f), 0.5f, Vector3.forward);
            adam.Lean.Key(0f, 10f).Key(2.2f, 9f).Key(2.9f, 2f);
            var far = new Vector3(-8f, 9.5f, 70f);
            var street = new Vector3(4f, 0.5f, -7.5f);
            adam.Gaze(0f, far, 0.6f);
            adam.Gaze(1.45f, street, 0.5f, 1f, true);
            adam.Gaze(2.15f, far, 0.55f);
            adam.Smile(0f, 34f);
            adam.Smile(1.2f, 30f);
            adam.Smile(1.7f, 48f);
            adam.Smile(2.7f, 40f);
            adam.Nod.Key(0.15f, 0f).Key(0.4f, 5f).Key(0.75f, -2f).Key(1.1f, 0f);
            adam.Shape("BrowInnerUp").Key(0f, 16f).Key(0.5f, 20f).Key(1.4f, 4f);
            adam.Say(-0.625f, Voice("adam-not-tonight"));
            // he pushes off the rail to go
            foreach (var arm in new[] { adam.Left, adam.Right })
            {
                arm.Weight.Key(2.3f, 1f).Key(2.85f, 0f, Ease.In);
                arm.Aim.Key(2.3f, 1f).Key(2.8f, 0f);
                arm.Grip.Key(2.2f, 0.4f).Key(2.6f, 0.05f);
            }
            adam.Turn.Key(2.45f, 0f).Key(3.125f, 34f, Ease.In);
            s.Lamp("Sky key", new Vector3(1.9f, 11.7f, -9.9f), NightKey, 3.4f, 5.5f);
            s.Lamp("Street glow", new Vector3(0.2f, 9.9f, -10.3f), WarmKey, 0.55f, 3f);

            s.Camera.Position.Key(0f, 1.62f, 10.88f, -8.5f).Key(3.125f, 1.44f, 10.9f, -8.72f, Ease.Linear);
            s.Camera.Target.Key(0f, 0.42f, 10.86f, -11.36f);
            s.Camera.Focus.Key(0f, 0.45f, 10.9f, -11.3f);
            s.Camera.Fov.Key(0f, 25f);
            s.Camera.Aperture.Key(0f, 2.2f);
        }

        // ------------------------------------------------------------ w04
        static void W04(FilmStage s)
        {
            var set = FilmWishSets.Coast(s);
            SunsetGrade(s);
            var start = new Vector3(0.2f, 0f, -0.86f);
            var maya = s.Lead("maya", start, 0f, 2f);
            maya.Play(0f, "f_idle_breathe_01");
            maya.Path.Key(0f, start).Key(0.5f, start).Key(1.35f, new Vector3(0.45f, 0f, -1.25f));
            maya.Heading.Key(0f, 0f).Key(0.45f, 0f).Key(1.3f, 90f);
            maya.Play(0.4f, "f_turn_right_90", 0.2f);
            maya.Walk(1.35f, new Vector3(0.45f, 0f, -1.25f), new Vector3(30f, 0f, -1.6f), "f_walk_neutral_01", 1.05f, 0.35f);
            maya.Wind.Key(0f, 0.8f);
            maya.WindTo = new Vector3(-1f, 0.1f, -0.35f);
            var ahead = new Vector3(40f, 2.6f, -2.5f);
            maya.Gaze(0f, new Vector3(6f, 1.7f, 60f), 0.6f);
            maya.Gaze(0.3f, ahead, 0.8f, 1f, true);
            maya.Smile(0f, 50f);
            maya.Smile(1.5f, 30f);
            // the walk ahead of her has people on it
            Stroller(s, "Male_Adult_12", new Vector3(26f, 0f, -3.4f), new Vector3(-10f, 0f, -3.8f), "m_walk_cool_01", 31f);
            Stroller(s, "Female_Adult_02", new Vector3(34f, 0f, -5.2f), new Vector3(4f, 0f, -5.6f), "f_walk_stroll_01", 32f);
            Stroller(s, "Male_Adult_03", new Vector3(9f, 0f, -6.4f), new Vector3(40f, 0f, -5.8f), "m_walk_slow_01", 33f);
            Bystander(s, "Female_Adult_09", set.Bench + new Vector3(-0.4f, 0f, 0f), 0f, "f_sit_chair_breathe_01", 34f);

            s.Camera.Position.Key(0f, -3.9f, 1.42f, -3.7f).Key(3.125f, -3.3f, 1.62f, -3.4f);
            s.Camera.Target.Key(0f, 0.6f, 1.28f, -1.0f).Key(1.4f, 1.2f, 1.26f, -1.1f).Key(3.125f, 4.4f, 1.3f, -1.5f);
            s.Camera.Focus.Key(0f, start + Vector3.up * 1.4f).Key(3.125f, 2.6f, 1.4f, -1.3f);
            s.Camera.Fov.Key(0f, 34f);
            s.Camera.Aperture.Key(0f, 3.2f);
        }

        // ------------------------------------------------------------ w05
        static void W05(FilmStage s)
        {
            var set = FilmWishSets.Harbour(s, true, false);
            NightGrade(s, 0.5f);
            var inside = new Vector3(-5f, 0.1f, -12.25f);
            var kerb = new Vector3(-4.5f, 0.1f, -10.45f);
            var adam = s.Lead("adam", inside, 0f, 1f);
            var out1 = adam.Walk(0f, inside, kerb, "m_walk_neutral_01", 1.3f, 0f, 0.3f);
            adam.Play(out1, "m_idle_breathe_01", 0.3f);
            adam.Path.Key(out1, kerb).Key(2.3f, kerb);
            adam.Heading.Key(out1, 15f).Key(2.15f, 15f).Key(2.6f, 90f);
            adam.Walk(2.35f, kerb, new Vector3(20f, 0.1f, -10.6f), "m_walk_neutral_01", 1.15f, 0.35f);
            // the cars, not moving, all the way down the road; then the way he will go instead
            var jam = new Vector3(40f, 0.9f, -8.4f);
            adam.Gaze(0f, new Vector3(-4f, 1.5f, -4f), 0.5f);
            adam.Gaze(0.9f, jam, 0.9f, 1f, true);
            adam.Gaze(2.3f, new Vector3(30f, 1.6f, -10.6f), 0.8f, 1f, true);
            adam.Shape("BrowInnerUp").Key(1.3f, 0f).Key(1.6f, 40f).Key(2.3f, 6f);
            adam.Smile(0f, 0f);
            adam.Smile(1.6f, 0f);
            adam.Smile(2.1f, 36f);
            // a slow shake of the head at it
            adam.Tilt.Key(1.5f, 0f).Key(1.72f, 5f).Key(1.95f, -5f).Key(2.15f, 3f).Key(2.3f, 0f);
            adam.Shrug.Key(1.45f, 0f).Key(1.7f, 5f).Key(2.1f, 0f);
            Follow(s, adam, new Vector3(-1.4f, 2.0f, 0.2f), WarmKey, 1.2f, 5f);
            // other people out for the evening
            Stroller(s, "Female_Adult_09", new Vector3(9f, 0.1f, -11.4f), new Vector3(-20f, 0.1f, -11.4f), "f_walk_neutral_01", 41f);
            Stroller(s, "Male_Adult_12", new Vector3(16f, 0.1f, -10.9f), new Vector3(60f, 0.1f, -10.9f), "m_walk_cool_01", 42f);
            Stroller(s, "Female_Adult_02", new Vector3(-2f, 0.05f, -2.2f), new Vector3(30f, 0.05f, -2.2f), "f_walk_stroll_01", 43f);

            s.Camera.Position.Key(0f, -8.3f, 1.6f, -11.0f).Key(3.125f, -7.9f, 1.66f, -10.8f);
            s.Camera.Target.Key(0f, 2f, 1.15f, -9.0f).Key(3.125f, 3.5f, 1.15f, -9.2f);
            s.Camera.Focus.Key(0f, kerb + Vector3.up * 1.5f);
            s.Camera.Fov.Key(0f, 31f);
            s.Camera.Aperture.Key(0f, 2.6f);
        }
    }
}
