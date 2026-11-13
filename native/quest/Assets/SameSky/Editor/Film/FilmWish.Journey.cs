// "Wish You Were Here", shots w12 to w18: the distance collapses.
//   w12   Adam climbs the city stairway toward the light at its top
//   w13   Maya climbs the steps above the coast (the same framing)
//   w14   his profile, climbing   /   w15  hers, the sea behind her (the same move)
//   w16   up the stairs into the gate's light: the dark skyline gives way...
//   w17a  ...to an amber horizon: the terrace's warm stone, a chair turned to the water
//   w17b  climbing plants in the sun   /   w17c  linen in the breeze
//   w18   Maya at the terrace wall hears footsteps, and turns

using UnityEngine;

namespace SameSky.Film
{
    public static partial class FilmWish
    {
        // a place on the city stairway, d metres along from its foot (it climbs toward -Z); and on the coast steps (toward +X)
        static Vector3 CityStep(HarbourSet set, float d) => set.StairFoot + new Vector3(0f, d * 0.5f, -d);
        static Vector3 CoastStep(CoastSet set, float d) => set.StepsFoot + new Vector3(d, d * 0.5f, 0f);

        // ------------------------------------------------------------ w12, w13: climbing toward us
        static void W12(FilmStage s)
        {
            var set = FilmWishSets.Harbour(s, false, false);
            NightGrade(s, 0.55f);
            const float d0 = 2.6f;
            var adam = s.Lead("adam", CityStep(set, d0), 180f, 1f);
            var v = FilmCast.Pace("m_walk_neutral_01") * 1.0f;
            Climb(adam, 0f, CityStep(set, d0), CityStep(set, d0 + v * 4f), "m_walk_neutral_01", 1.0f, 0f, 0.2f);
            adam.Gaze(0f, set.Gate + Vector3.up * 1.6f, 0.75f);
            adam.Smile(0f, 8f);
            adam.Smile(1.6f, 30f);
            Follow(s, adam, new Vector3(0.9f, 2.1f, -1.6f), WarmKey, 1.3f, 5f);
            // the street he has left, below
            Stroller(s, "Female_Adult_09", new Vector3(24f, 0f, -31f), new Vector3(0f, 0f, -31f), "f_walk_neutral_01", 101f);
            Stroller(s, "Male_Adult_12", new Vector3(15f, 0f, -29.6f), new Vector3(40f, 0f, -29.6f), "m_walk_cool_01", 102f);
            Stroller(s, "Female_Adult_02", new Vector3(19f, 0f, -32.6f), new Vector3(40f, 0f, -32.4f), "f_walk_stroll_01", 103f);

            s.Camera.Position.Key(0f, CityStep(set, d0 + 4.3f) + new Vector3(0.55f, 1.82f, 0f), Ease.Linear).Key(2.5f, CityStep(set, d0 + 4.3f + v * 1.6f) + new Vector3(0.5f, 1.86f, 0f), Ease.Linear);
            s.Camera.Target.Key(0f, CityStep(set, d0) + Vector3.up * 1.45f, Ease.Linear).Key(2.5f, CityStep(set, d0 + v * 2.5f) + Vector3.up * 1.5f, Ease.Linear);
            s.Camera.Fov.Key(0f, 34f);
            s.Camera.Aperture.Key(0f, 2.4f);
            s.Camera.Life = 1.4f;
        }

        static void W13(FilmStage s)
        {
            var set = FilmWishSets.Coast(s, 62f, 5f);
            SunsetGrade(s);
            const float d0 = 2.6f;
            var maya = s.Lead("maya", CoastStep(set, d0), 90f, 2f);
            var v = FilmCast.Pace("f_walk_neutral_01") * 1.0f;
            Climb(maya, 0f, CoastStep(set, d0), CoastStep(set, d0 + v * 4f), "f_walk_neutral_01", 1.0f, 0f, 0.2f);
            maya.Gaze(0f, set.StepsTop + new Vector3(3f, 1.6f, 0f), 0.75f);
            maya.Smile(0f, 20f);
            maya.Smile(1.6f, 44f);
            maya.Wind.Key(0f, 0.8f);
            maya.WindTo = new Vector3(-0.6f, 0.1f, -0.8f);
            // the walk she has left, below
            Stroller(s, "Male_Adult_03", new Vector3(36f, 0f, -4f), new Vector3(0f, 0f, -4.4f), "m_walk_slow_01", 111f);
            Stroller(s, "Female_Adult_02", new Vector3(24f, 0f, -2.6f), new Vector3(60f, 0f, -2.4f), "f_walk_stroll_01", 112f);

            s.Camera.Position.Key(0f, CoastStep(set, d0 + 4.3f) + new Vector3(0f, 1.82f, -0.55f), Ease.Linear).Key(2.5f, CoastStep(set, d0 + 4.3f + v * 1.6f) + new Vector3(0f, 1.86f, -0.5f), Ease.Linear);
            s.Camera.Target.Key(0f, CoastStep(set, d0) + Vector3.up * 1.4f, Ease.Linear).Key(2.5f, CoastStep(set, d0 + v * 2.5f) + Vector3.up * 1.45f, Ease.Linear);
            s.Camera.Fov.Key(0f, 34f);
            s.Camera.Aperture.Key(0f, 2.4f);
            s.Camera.Life = 1.4f;
        }

        // ------------------------------------------------------------ w14, w15: in profile, the same move
        static void W14(FilmStage s)
        {
            var set = FilmWishSets.Harbour(s, false, false);
            NightGrade(s, 0.6f);
            const float d0 = 4.6f;
            var adam = s.Lead("adam", CityStep(set, d0), 180f, 1f);
            var v = FilmCast.Pace("m_walk_fast_01") * 0.82f;
            Climb(adam, 0f, CityStep(set, d0), CityStep(set, d0 + v * 3f), "m_walk_fast_01", 0.82f, 0f, 0.4f);
            adam.Gaze(0f, set.Gate + Vector3.up * 1.7f, 0.8f);
            adam.Smile(0f, 24f);
            adam.Smile(1f, 36f);
            Follow(s, adam, new Vector3(-1.6f, 1.9f, -1.0f), WarmKey, 1.5f, 5f);

            var side = new Vector3(-2.75f, 1.72f, -0.35f);
            s.Camera.Position.Key(0f, CityStep(set, d0) + side, Ease.Linear).Key(1.25f, CityStep(set, d0 + v * 1.25f) + side, Ease.Linear);
            s.Camera.Target.Key(0f, CityStep(set, d0) + new Vector3(0f, 1.5f, -0.3f), Ease.Linear).Key(1.25f, CityStep(set, d0 + v * 1.25f) + new Vector3(0f, 1.5f, -0.3f), Ease.Linear);
            s.Camera.Fov.Key(0f, 30f);
            s.Camera.Aperture.Key(0f, 2.2f);
            s.Camera.Life = 1.2f;
        }

        static void W15(FilmStage s)
        {
            // (the sun over the sea, in the picture behind her)
            var set = FilmWishSets.Coast(s, 14f, 4f);
            SunsetGrade(s, 0.1f);
            const float d0 = 4.6f;
            var maya = s.Lead("maya", CoastStep(set, d0), 90f, 2f);
            var v = FilmCast.Pace("f_walk_fast_01") * 0.82f;
            Climb(maya, 0f, CoastStep(set, d0), CoastStep(set, d0 + v * 3f), "f_walk_fast_01", 0.82f, 0f, 0.4f);
            maya.Gaze(0f, set.StepsTop + new Vector3(3f, 1.7f, 0f), 0.8f);
            maya.Smile(0f, 36f);
            maya.Smile(1f, 52f);
            maya.Wind.Key(0f, 1f);
            maya.WindTo = new Vector3(-1f, 0.1f, -0.2f);
            Follow(s, maya, new Vector3(0.9f, 1.7f, -1.9f), new Color(1f, 0.74f, 0.56f), 1.6f, 5f);

            var side = new Vector3(0.35f, 1.72f, -2.75f);
            s.Camera.Position.Key(0f, CoastStep(set, d0) + side, Ease.Linear).Key(1.25f, CoastStep(set, d0 + v * 1.25f) + side, Ease.Linear);
            s.Camera.Target.Key(0f, CoastStep(set, d0) + new Vector3(0.3f, 1.45f, 0f), Ease.Linear).Key(1.25f, CoastStep(set, d0 + v * 1.25f) + new Vector3(0.3f, 1.45f, 0f), Ease.Linear);
            s.Camera.Fov.Key(0f, 30f);
            s.Camera.Aperture.Key(0f, 2.2f);
            s.Camera.Life = 1.2f;
        }

        // ------------------------------------------------------------ w16: into the light
        static void W16(FilmStage s)
        {
            var set = FilmWishSets.Harbour(s, false, true);
            NightGrade(s, 0.5f);
            var adam = s.Lead("adam", CityStep(set, 7.4f), 180f, 1f);
            var v = FilmCast.Pace("m_walk_fast_01") * 0.9f;
            Climb(adam, 0f, CityStep(set, 7.4f), CityStep(set, 7.4f + v * 2f), "m_walk_fast_01", 0.9f, 0f, 0.1f);
            // up the stairs behind him, faster and faster, until the gate's light is all there is
            s.Camera.Position.Key(0f, CityStep(set, 2.2f) + new Vector3(0.1f, 1.55f, 0f)).Key(1.25f, CityStep(set, 8.1f) + new Vector3(0f, 1.75f, 0f), Ease.In);
            s.Camera.Target.Key(0f, set.Gate + Vector3.up * 1.5f);
            s.Camera.Fov.Key(0f, 40f).Key(1.25f, 30f, Ease.In);
            s.Camera.Aperture.Key(0f, 4f);
            s.Camera.Exposure.Key(0f, 0.5f).Key(0.7f, 0.7f).Key(1.25f, 2.6f, Ease.In);
            s.Camera.Life = 1.5f;
        }

        // ------------------------------------------------------------ w17: the terrace, in pieces
        static void TerraceGrade(FilmStage s, float exposure = 0f)
        {
            s.Camera.Grade(6f, 5f, 7f, 0.26f, 0.8f, 2f);
            s.Camera.Exposure.Key(0f, exposure);
        }

        static void W17a(FilmStage s)
        {
            var set = FilmWishSets.Terrace(s);
            TerraceGrade(s);
            // out of the light: the sun on the water, the wall's warm stone, a chair turned toward it
            var toSun = new Vector3(set.SunDirection.x, 0f, set.SunDirection.z).normalized;
            var from = new Vector3(2.35f, 1.18f, 1.2f);
            s.Camera.Position.Key(0f, from).Key(1.25f, from + toSun * 0.5f + Vector3.up * 0.03f, Ease.Out);
            s.Camera.Target.Key(0f, from + toSun * 12f + new Vector3(0f, -0.25f, 0f));
            s.Camera.Fov.Key(0f, 36f);
            s.Camera.Aperture.Key(0f, 5.6f);
            s.Camera.Exposure.Key(0f, 2.4f).Key(0.42f, 0f, Ease.Out);
        }

        static void W17b(FilmStage s)
        {
            var set = FilmWishSets.Terrace(s);
            TerraceGrade(s, 0.1f);
            s.Camera.Position.Key(0f, -2.75f, 1.9f, 1.25f).Key(0.625f, -2.68f, 1.98f, 1.3f, Ease.Linear);
            s.Camera.Target.Key(0f, -1.5f, 2.05f, 2.6f);
            s.Camera.Fov.Key(0f, 30f);
            s.Camera.Aperture.Key(0f, 2f);
        }

        static void W17c(FilmStage s)
        {
            var set = FilmWishSets.Terrace(s);
            TerraceGrade(s, 0.1f);
            s.Camera.Position.Key(0f, -3.7f, 1.45f, -0.2f).Key(0.625f, -3.55f, 1.45f, -0.05f, Ease.Linear);
            s.Camera.Target.Key(0f, -2.5f, 1.5f, 2.66f);
            s.Camera.Fov.Key(0f, 34f);
            s.Camera.Aperture.Key(0f, 2.8f);
        }

        // ------------------------------------------------------------ w18: she hears him
        static readonly Vector3 MayaAtWall = new(3.15f, 0f, 4.62f);

        static void W18(FilmStage s)
        {
            var set = FilmWishSets.Terrace(s);
            TerraceGrade(s, 0.15f);
            var maya = s.Lead("maya", MayaAtWall, 0f, 2f);
            maya.Play(0f, "f_idle_breathe_01");
            foreach (var arm in new[] { maya.Left, maya.Right })
            {
                var sideways = arm.IsLeft ? -1f : 1f;
                arm.Goal.Key(0f, new Vector3(MayaAtWall.x + sideways * 0.17f, TerraceSet.WallTop + 0.05f, 5.22f));
                arm.Weight.Key(0f, 1f);
                arm.Fingers.Key(0f, new Vector3(-sideways * 0.3f, -0.05f, 1f).normalized);
                arm.Palm.Key(0f, Vector3.down);
                arm.Aim.Key(0f, 1f);
                arm.Grip.Key(0f, 0.12f);
            }
            maya.Left.Weight.Key(0.75f, 1f).Key(1.15f, 0f);
            maya.Left.Aim.Key(0.75f, 1f).Key(1.1f, 0f);
            maya.Lean.Key(0f, 8f).Key(0.6f, 7f).Key(1.2f, 1f);
            maya.Wind.Key(0f, 0.8f);
            maya.WindTo = new Vector3(-1f, 0.1f, -0.3f);
            // the sea; then a sound behind her
            var door = set.Door + Vector3.up * 1.6f;
            maya.Gaze(0f, new Vector3(8f, 1.5f, 40f), 0.5f);
            maya.Gaze(0.42f, door, 1f, 1f, true);
            maya.Twist.Key(0.45f, 0f).Key(1.1f, 52f);
            maya.Shape("EyeWideLeft").Key(0.5f, 0f).Key(0.8f, 40f).Key(1.25f, 20f);
            maya.Shape("EyeWideRight").Key(0.5f, 0f).Key(0.8f, 40f).Key(1.25f, 20f);
            maya.Shape("BrowInnerUp").Key(0.5f, 0f).Key(0.85f, 34f);
            maya.Smile(0f, 10f);
            maya.Smile(0.9f, 10f);
            maya.Smile(1.25f, 50f);
            s.Lamp("Lantern fill", new Vector3(1.4f, 1.3f, 3.2f), new Color(1f, 0.72f, 0.46f), 1.5f, 4f);

            s.Camera.Position.Key(0f, 1.75f, 1.5f, 2.85f).Key(1.25f, 1.9f, 1.5f, 3.0f, Ease.Linear);
            s.Camera.Target.Key(0f, 3.1f, 1.44f, 4.6f);
            s.Camera.Focus.Key(0f, MayaAtWall + Vector3.up * 1.5f);
            s.Camera.Fov.Key(0f, 30f);
            s.Camera.Aperture.Key(0f, 2.2f);
        }
    }
}
