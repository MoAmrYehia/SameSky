// "Wish You Were Here", shots w19 to w29: the reunion, the line, the title, the stinger. All on the rooftop terrace.
//   w19  the wide: he is in the doorway, she has turned; they cross the terrace to each other
//   w20  the embrace, her face and then his
//   w21  their hands   w22  she pulls him toward the view   w23  a glance at the wall
//   w24  a chair pulled closer   w25  both laughing   w26  the two of them under one sky
//   w27  "Took you long enough."  "Traffic."
//   w28  up over their heads into the sky, for the title
//   w29  "Same time tomorrow?"  He pulls his chair closer.

using UnityEngine;

namespace SameSky.Film
{
    public static partial class FilmWish
    {
        static readonly Vector3 Meet = new(1.0f, 0f, 0.6f);
        // the line the two of them meet along (from his side to hers), and what is to his right of it
        static readonly Vector3 Along = new Vector3(0.36f, 0f, 0.93f).normalized;
        static readonly Vector3 Aside = new(Along.z, 0f, -Along.x);
        static float YawOf(Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;

        /// <summary>A walk that takes exactly from <paramref name="t0"/> to <paramref name="t1"/>: the clip is played
        /// as fast as that needs.</summary>
        static void WalkBy(FilmActor who, float t0, float t1, Vector3 from, Vector3 to, string clip, float fade = 0.3f)
        {
            var speed = (to - from).magnitude / Mathf.Max(0.1f, t1 - t0) / FilmCast.Pace(clip);
            who.Walk(t0, from, to, clip, speed, fade);
        }

        static Transform Chest(FilmActor who) =>
            who.Animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? who.Animator.GetBoneTransform(HumanBodyBones.Chest);

        /// <summary>Their arms round each other. <paramref name="weight"/> brings the arms up (0 hanging, 1 holding).</summary>
        static void Hold(FilmActor adam, FilmActor maya, Track weight)
        {
            Transform ac = Chest(adam), mc = Chest(maya);
            // her right hand over his left shoulder, her left round his side to his back
            maya.Right.Live = t => ac.position - Along * 0.17f + Vector3.up * 0.1f - Aside * 0.1f;
            maya.Left.Live = t => ac.position - Along * 0.18f - Vector3.up * 0.04f + Aside * 0.15f;
            // his right arm across her shoulders, his left round her waist
            adam.Right.Live = t => mc.position + Along * 0.16f + Vector3.up * 0.03f - Aside * 0.07f;
            adam.Left.Live = t => mc.position + Along * 0.15f - Vector3.up * 0.2f + Aside * 0.09f;
            foreach (var arm in new[] { maya.Left, maya.Right, adam.Left, adam.Right })
            {
                arm.Elbow = new Vector3(arm.IsLeft ? -1f : 1f, -0.55f, 0.15f);
                arm.Grip.Key(0f, 0.25f);
                arm.Live = Gate(arm.Live);
            }
            System.Func<float, Vector3?> Gate(System.Func<float, Vector3?> goal) => t => weight.At(t) > 0.001f ? goal(t) : null;
            adam.Moves2(weight);
            maya.Moves2(weight);
        }

        static void Moves2(this FilmActor who, Track weight)
        {
            // (the arms' own weights follow the shot's one track)
            for (var t = 0f; t <= 8f; t += 0.05f)
            {
                var w = weight.At(t);
                who.Left.Weight.Key(t, w, Ease.Linear);
                who.Right.Weight.Key(t, w, Ease.Linear);
            }
        }

        // ------------------------------------------------------------ w19
        static void W19(FilmStage s)
        {
            var set = FilmWishSets.Terrace(s);
            TerraceGrade(s, 0.1f);
            Vector3 aEnd = Meet - Along * 0.2f + Aside * 0.045f, mEnd = Meet + Along * 0.2f - Aside * 0.045f;
            // he comes up the last of the steps, and stops when he sees her
            var top = set.Door;
            var below = top + new Vector3(0f, -0.68f, -1.36f);
            var adam = s.Lead("adam", below, 0f, 1f);
            var up = Climb(adam, 0f, below, top, "m_walk_neutral_01", 1.25f, 0f, 0.3f);
            adam.Lean.Key(up, 9f).Key(up + 0.35f, 1f);
            adam.Play(up, "m_idle_breathe_01", 0.3f);
            adam.Path.Key(up, top).Key(1.6f, top);
            WalkBy(adam, 1.6f, 4.3f, top, aEnd, "m_walk_fast_01", 0.4f);
            adam.Play(4.3f, "m_idle_breathe_01", 0.35f);
            adam.Heading.Key(4.3f, YawOf(Along), Ease.Hold);
            // she has turned from the wall; then she is on her way to him
            var maya = s.Lead("maya", MayaAtWall, -70f, 2f);
            maya.Play(0f, "f_idle_breathe_01");
            var toHim = YawOf(mEnd - MayaAtWall);
            if (toHim > 0f) toHim -= 360f;
            maya.Heading.Key(0f, -70f).Key(0.7f, -84f).Key(1.4f, toHim);
            maya.Play(0.65f, "f_turn_left_90", 0.25f);
            WalkBy(maya, 1.35f, 4.3f, MayaAtWall, mEnd, "f_walk_fast_01", 0.35f);
            maya.Play(4.3f, "f_idle_breathe_01", 0.35f);
            var facing = YawOf(-Along);
            if (facing > 0f) facing -= 360f;
            maya.Heading.Key(4.3f, facing, Ease.Hold);
            adam.Gaze(0f, maya.Head.position, 0.8f);
            maya.Gaze(0f, adam.Head.position, 0.9f);
            adam.LookLive = t => maya.Head.position;
            maya.LookLive = t => adam.Head.position;
            adam.Smile(0f, 10f);
            adam.Smile(1.3f, 60f);
            maya.Smile(0f, 40f);
            maya.Smile(0.8f, 85f);
            maya.Wind.Key(0f, 0.6f);
            maya.WindTo = new Vector3(-1f, 0.1f, -0.3f);
            // as they reach each other their arms come up
            Hold(adam, maya, new Track().Key(0f, 0f).Key(4.0f, 0f).Key(4.75f, 1f));
            adam.Lean.Key(4.1f, 2f).Key(4.8f, 8f);
            maya.Lean.Key(4.1f, 2f).Key(4.8f, 6f);

            // from behind his shoulder on the steps, up and over the wall as they cross to each other
            s.Camera.Position.Key(0f, -1.0f, 1.55f, -9.2f).Key(5f, -0.2f, 3.5f, -7.3f, Ease.Soft);
            s.Camera.Target.Key(0f, 0.5f, 1.15f, 2f).Key(5f, 1.0f, 1.05f, 1.0f);
            s.Camera.Fov.Key(0f, 42f).Key(5f, 37f);
            s.Camera.Aperture.Key(0f, 8f);
            s.Camera.Life = 0.7f;
        }

        // ------------------------------------------------------------ w20
        static void W20(FilmStage s)
        {
            var set = FilmWishSets.Terrace(s);
            TerraceGrade(s, 0.15f);
            Vector3 a = Meet - Along * 0.15f + Aside * 0.045f, m = Meet + Along * 0.15f - Aside * 0.045f;
            var adam = s.Lead("adam", a, YawOf(Along), 1f);
            var maya = s.Lead("maya", m, YawOf(-Along), 2f);
            adam.Play(0f, "m_idle_breathe_01");
            maya.Play(0f, "f_idle_breathe_01");
            adam.Lean.Key(0f, 9f).Key(1.2f, 10f).Key(2.5f, 8f);
            maya.Lean.Key(0f, 6f);
            maya.Offset.Key(0f, 0f, 0.045f, 0f);
            Hold(adam, maya, new Track(1f));
            // her face over his shoulder: the breath she has been holding goes out of her
            maya.Gaze(0f, a - Along * 2f - Aside * 0.9f + Vector3.up * 1.45f, 0.5f);
            maya.Tilt.Key(0f, -7f).Key(1.3f, -10f);
            maya.Smile(0f, 55f);
            maya.Smile(0.9f, 72f);
            maya.Shape("BrowInnerUp").Key(0f, 30f).Key(0.8f, 44f).Key(1.4f, 30f);
            foreach (var lid in new[] { "EyeBlinkLeft", "EyeBlinkRight" }) maya.Shape(lid).Key(0.25f, 0f).Key(0.55f, 92f).Key(2.5f, 92f);
            maya.AutoBlink = false;
            maya.Shrug.Key(0.3f, 0f).Key(0.7f, 4f).Key(1.2f, 0f);
            maya.Wind.Key(0f, 0.5f);
            maya.WindTo = new Vector3(-1f, 0.1f, -0.3f);
            // and his over hers
            adam.Gaze(0f, m + Along * 2f + Aside * 0.9f + Vector3.up * 1.5f, 0.45f);
            adam.Tilt.Key(0f, -5f).Key(2.5f, -7f);
            adam.Smile(0f, 40f);
            adam.Smile(1.8f, 58f);
            adam.Smile(2.3f, 70f);
            adam.AutoBlink = false;
            foreach (var lid in new[] { "EyeBlinkLeft", "EyeBlinkRight" }) adam.Shape(lid).Key(0f, 90f).Key(1.95f, 90f).Key(2.2f, 0f);
            s.Lamp("Door glow", a - Along * 1.4f - Aside * 0.8f + Vector3.up * 1.7f, new Color(1f, 0.72f, 0.48f), 1.7f, 4f);

            Vector3 mayaFace = m + Vector3.up * 1.5f, adamFace = a + Vector3.up * 1.6f;
            Vector3 cam1 = a - Along * 0.95f - Aside * 0.36f + Vector3.up * 1.58f, cam2 = m + Along * 0.95f + Aside * 0.36f + Vector3.up * 1.6f;
            s.Camera.Position.Key(0f, cam1).Key(1.35f, cam1 - Aside * 0.07f, Ease.Linear).Key(1.351f, cam2, Ease.Hold).Key(2.5f, cam2 + Aside * 0.07f, Ease.Linear);
            s.Camera.Target.Key(0f, mayaFace).Key(1.35f, mayaFace).Key(1.351f, adamFace, Ease.Hold);
            s.Camera.Fov.Key(0f, 30f);
            s.Camera.Aperture.Key(0f, 2f);
            s.Camera.Life = 0.6f;
        }

        // ------------------------------------------------------------ w21
        static void W21(FilmStage s)
        {
            var set = FilmWishSets.Terrace(s);
            TerraceGrade(s, 0.15f);
            Vector3 a = Meet - Along * 0.42f, m = Meet + Along * 0.42f;
            var adam = s.Lead("adam", a, YawOf(Along), 1f);
            var maya = s.Lead("maya", m, YawOf(-Along), 2f);
            adam.Play(0f, "m_idle_breathe_01");
            maya.Play(0f, "f_idle_breathe_01");
            var p = Meet + Vector3.up * 0.99f + Aside * 0.13f;
            // his hand, open; hers comes down into it; they close
            adam.Right.Goal.Key(0f, p - Along * 0.13f - Vector3.up * 0.03f).Key(0.5f, p - Along * 0.1f - Vector3.up * 0.012f).Key(1.0f, p - Along * 0.1f + Vector3.up * 0.01f);
            adam.Right.Weight.Key(0f, 1f);
            adam.Right.Fingers.Key(0f, Along + Vector3.up * 0.05f);
            adam.Right.Palm.Key(0f, Vector3.up - Aside * 0.25f);
            adam.Right.Aim.Key(0f, 1f);
            adam.Right.Grip.Key(0f, 0.05f).Key(0.6f, 0.08f).Key(0.95f, 0.5f);
            maya.Left.Goal.Key(0f, p + Along * 0.26f + Vector3.up * 0.16f).Key(0.6f, p + Along * 0.1f + Vector3.up * 0.036f, Ease.Soft).Key(1.0f, p + Along * 0.1f + Vector3.up * 0.05f);
            maya.Left.Weight.Key(0f, 1f);
            maya.Left.Fingers.Key(0f, -Along - Vector3.up * 0.25f).Key(0.6f, -Along - Vector3.up * 0.04f);
            maya.Left.Palm.Key(0f, Vector3.down);
            maya.Left.Aim.Key(0f, 1f);
            maya.Left.Grip.Key(0f, 0.1f).Key(0.6f, 0.12f).Key(0.95f, 0.55f);

            var from = p + Aside * 0.62f + Vector3.up * 0.08f - Along * 0.04f;
            s.Camera.Position.Key(0f, from).Key(1.25f, from - Aside * 0.07f, Ease.Linear);
            s.Camera.Target.Key(0f, p + Vector3.up * 0.02f);
            s.Camera.Fov.Key(0f, 28f);
            s.Camera.Aperture.Key(0f, 2f);
            s.Camera.Life = 0.6f;
        }

        // ------------------------------------------------------------ w22
        static void W22(FilmStage s)
        {
            var set = FilmWishSets.Terrace(s);
            TerraceGrade(s, 0.15f);
            const float v = 1.3f, T = 1.875f;
            Vector3 a0 = Meet + Along * 0.1f, m0 = a0 + Along * 0.86f + Aside * 0.2f;
            var adam = s.Lead("adam", a0, YawOf(Along), 1f);
            var maya = s.Lead("maya", m0, YawOf(Along), 2f);
            WalkBy(adam, 0f, 4f, a0, a0 + Along * v * 4f, "m_walk_neutral_01", 0f);
            WalkBy(maya, 0f, 4f, m0, m0 + Along * v * 4f, "f_walk_fast_01", 0f);
            // her hand behind her holds his, held out in front of him
            System.Func<float, Vector3?> link = t => adam.Root.transform.position + Along * 0.5f + Aside * 0.13f + Vector3.up * (0.98f + 0.015f * Mathf.Sin(t * 8f));
            adam.Right.Live = link;
            adam.Right.Weight.Key(0f, 1f);
            adam.Right.Grip.Key(0f, 0.5f);
            maya.Left.Live = link;
            maya.Left.Weight.Key(0f, 1f);
            maya.Left.Grip.Key(0f, 0.55f);
            maya.Left.Elbow = new Vector3(-0.6f, -0.6f, -0.6f);
            // she looks back at him, laughing; then at where she is taking him
            maya.Gaze(0f, a0 + Vector3.up * 1.62f, 1f);
            maya.Twist.Key(0f, 52f).Key(1.0f, 50f).Key(1.5f, 6f);
            maya.LookLive = t => t < 1.05f ? adam.Head.position : (Vector3?)null;
            maya.Gaze(1.05f, new Vector3(9f, 1.5f, 30f), 0.6f, 1f, true);
            Laugh(maya, 0.1f, 1.1f, 0.85f);
            maya.Wind.Key(0f, 0.9f);
            maya.WindTo = new Vector3(-1f, 0.1f, -0.3f);
            adam.Gaze(0f, m0 + Vector3.up * 1.5f, 0.6f);
            adam.LookLive = t => t < 1.2f ? maya.Head.position : (Vector3?)null;
            adam.Gaze(1.2f, new Vector3(9f, 1.6f, 30f), 0.6f, 1f, true);
            adam.Smile(0f, 55f);
            adam.Shape("BrowInnerUp").Key(0f, 20f).Key(1.3f, 35f);
            Follow(s, maya, -Along * 1.2f + Aside * 0.2f + Vector3.up * 1.7f, new Color(1f, 0.72f, 0.5f), 1.0f, 4f);

            var lead = m0 + Along * 2.5f - Aside * 0.85f + Vector3.up * 1.5f;
            s.Camera.Position.Key(0f, lead, Ease.Linear).Key(T, lead + Along * (v * T - 0.25f), Ease.Linear);
            s.Camera.Target.Key(0f, m0 - Along * 0.3f + Vector3.up * 1.42f, Ease.Linear).Key(T, m0 - Along * 0.3f + Along * v * T + Vector3.up * 1.42f, Ease.Linear);
            s.Camera.Fov.Key(0f, 33f);
            s.Camera.Aperture.Key(0f, 2.6f);
            s.Camera.Life = 1.2f;
        }

        // ------------------------------------------------------------ w23
        static void AtTheWall(FilmActor who, Vector3 at)
        {
            foreach (var arm in new[] { who.Left, who.Right })
            {
                var sideways = arm.IsLeft ? -1f : 1f;
                arm.Goal.Key(0f, new Vector3(at.x + sideways * 0.15f, TerraceSet.WallTop + 0.05f, 5.2f));
                arm.Weight.Key(0f, 1f);
                arm.Fingers.Key(0f, new Vector3(-sideways * 0.4f, -0.05f, 1f).normalized);
                arm.Palm.Key(0f, Vector3.down);
                arm.Aim.Key(0f, 1f);
                arm.Grip.Key(0f, 0.12f);
                arm.Elbow = new Vector3(sideways, -0.4f, -0.5f);
            }
            who.Lean.Key(0f, 13f);
        }

        static void W23(FilmStage s)
        {
            var set = FilmWishSets.Terrace(s);
            TerraceGrade(s, 0.1f);
            Vector3 m = new(3.3f, 0f, 4.74f), a = new(2.62f, 0f, 4.72f);
            var adam = s.Lead("adam", a, 0f, 1f);
            var maya = s.Lead("maya", m, 0f, 2f);
            adam.Play(0f, "m_idle_breathe_01");
            maya.Play(0f, "f_idle_breathe_01");
            AtTheWall(adam, a);
            AtTheWall(maya, m);
            var sea = new Vector3(8f, 1.45f, 40f);
            // he looks at her instead of the view; she catches him at it
            adam.Gaze(0f, sea, 0.6f);
            adam.Gaze(0.12f, m + Vector3.up * 1.5f, 0.55f, 1f);
            adam.Gaze(0.98f, sea + new Vector3(-10f, 0f, 0f), 0.6f, 1f, true);
            adam.Smile(0f, 20f);
            adam.Smile(0.5f, 52f);
            adam.Smile(1.1f, 68f);
            maya.Gaze(0f, sea, 0.6f);
            maya.Gaze(0.58f, a + Vector3.up * 1.6f, 0.3f, 1f);
            maya.Smile(0f, 30f);
            maya.Smile(0.6f, 34f);
            maya.Smile(0.95f, 62f);
            maya.Shape("BrowOuterUpLeft").Key(0.6f, 0f).Key(0.85f, 50f).Key(1.25f, 20f);
            maya.Shape("BrowInnerUp").Key(0.6f, 0f).Key(0.85f, 26f);
            maya.Wind.Key(0f, 0.9f);
            maya.WindTo = new Vector3(-1f, 0.1f, -0.3f);

            s.Camera.Position.Key(0f, 3.02f, 1.5f, 6.95f).Key(1.25f, 2.96f, 1.5f, 6.85f, Ease.Linear);
            s.Camera.Target.Key(0f, 2.96f, 1.47f, 4.72f);
            s.Camera.Fov.Key(0f, 25f);
            s.Camera.Aperture.Key(0f, 2.2f);
            s.Camera.Life = 0.7f;
        }

        // ------------------------------------------------------------ the two chairs
        const float ChairACloser = 3.78f, AdamSeatYaw = 9f, MayaSeatYaw = -12f;

        /// <summary>The two of them sitting by the wall: his chair where he pulled it to.</summary>
        static (FilmActor adam, FilmActor maya) Seated(FilmStage s, TerraceSet set, float chairAX = ChairACloser)
        {
            set.ChairA.SetPositionAndRotation(new Vector3(chairAX, 0f, 3.72f), Quaternion.Euler(0f, AdamSeatYaw, 0f));
            set.ChairB.SetPositionAndRotation(set.ChairBHome, Quaternion.Euler(0f, MayaSeatYaw, 0f));
            var adam = s.Lead("adam", set.ChairA.position, AdamSeatYaw, 1f);
            var maya = s.Lead("maya", set.ChairBHome, MayaSeatYaw, 2f);
            Sit(adam, 0f, set.ChairA.position, AdamSeatYaw);
            Sit(maya, 0f, set.ChairBHome, MayaSeatYaw);
            maya.Wind.Key(0f, 0.5f);
            maya.WindTo = new Vector3(-1f, 0.1f, -0.3f);
            return (adam, maya);
        }

        static readonly Vector3 Horizon = new(9f, 1.3f, 60f);

        static void W24(FilmStage s)
        {
            var set = FilmWishSets.Terrace(s);
            TerraceGrade(s, 0.1f);
            var maya = s.Lead("maya", set.ChairBHome, MayaSeatYaw, 2f);
            set.ChairB.rotation = Quaternion.Euler(0f, MayaSeatYaw, 0f);
            Sit(maya, 0f, set.ChairBHome, MayaSeatYaw);
            // he reaches for the other chair by its back and brings it over beside hers
            Vector3 from = new(3.2f, 0f, 3.76f), to = new(ChairACloser, 0f, 3.72f);
            var slide = new Track().Key(0f, 0f).Key(0.25f, 0f).Key(0.85f, 1f, Ease.Soft);
            var chair = set.ChairA;
            s.Moves.Add(t =>
            {
                var k = slide.At(t);
                chair.SetPositionAndRotation(Vector3.Lerp(from, to, k) + Vector3.up * (0.012f * Mathf.Sin(k * Mathf.PI)), Quaternion.Euler(0f, Mathf.Lerp(16f, AdamSeatYaw, k), 0f));
            });
            var adam = s.Lead("adam", to + new Vector3(-0.04f, 0f, -0.52f), 0f, 1f);
            adam.Play(0f, "m_idle_breathe_01");
            adam.Left.Live = t => chair.TransformPoint(new Vector3(-0.1f, WishKit.SeatHeight + 0.53f, -0.25f));
            adam.Left.Weight.Key(0f, 1f).Key(1.0f, 1f).Key(1.25f, 0.4f);
            adam.Left.Fingers.Key(0f, new Vector3(0f, -0.5f, 1f));
            adam.Left.Palm.Key(0f, new Vector3(0f, -1f, -0.3f));
            adam.Left.Aim.Key(0f, 1f);
            adam.Left.Grip.Key(0f, 0.6f);
            adam.Side.Key(0f, -15f).Key(0.25f, -15f).Key(0.9f, -1f, Ease.Soft);
            adam.Lean.Key(0f, 15f).Key(0.9f, 7f);
            adam.Twist.Key(0f, 14f).Key(0.9f, 0f);
            adam.Gaze(0f, from + Vector3.up * 0.9f, 0.6f);
            adam.Gaze(0.7f, set.ChairBHome + Vector3.up * 1.2f, 0.8f, 1f, true);
            adam.Smile(0f, 30f);
            adam.Smile(1.0f, 64f);
            // she turns round to watch him do it
            maya.Gaze(0f, to + new Vector3(-0.04f, 1.65f, -0.52f), 1f);
            maya.Twist.Key(0f, 44f);
            maya.Smile(0f, 50f);
            maya.Smile(0.9f, 80f);
            maya.Shape("BrowInnerUp").Key(0.3f, 0f).Key(0.7f, 30f);
            maya.Wind.Key(0f, 0.6f);
            maya.WindTo = new Vector3(-1f, 0.1f, -0.3f);

            s.Camera.Position.Key(0f, 2.5f, 1.3f, 1.3f).Key(1.25f, 2.65f, 1.3f, 1.4f, Ease.Linear);
            s.Camera.Target.Key(0f, 4.0f, 1.02f, 3.7f);
            s.Camera.Fov.Key(0f, 34f);
            s.Camera.Aperture.Key(0f, 3.2f);
            s.Camera.Life = 0.6f;
        }

        static void W25(FilmStage s)
        {
            var set = FilmWishSets.Terrace(s);
            TerraceGrade(s, 0.1f);
            var (adam, maya) = Seated(s, set);
            Laugh(maya, 0.12f, 1.5f);
            Laugh(adam, 0.3f, 1.3f, 0.85f);
            // each to the other, and away, and back
            maya.Gaze(0f, adam.Root.transform.position + Vector3.up * 1.3f, 0.7f);
            maya.Gaze(0.75f, Horizon, 0.4f, 1f, true);
            maya.Gaze(1.35f, adam.Root.transform.position + Vector3.up * 1.3f, 0.6f);
            maya.Twist.Key(0f, 26f).Key(0.75f, 22f).Key(1.0f, 4f).Key(1.4f, 20f);
            maya.Side.Key(0f, -4f).Key(0.6f, -11f).Key(1.3f, -3f);
            adam.Gaze(0f, Horizon, 0.5f);
            adam.Gaze(0.28f, maya.Root.transform.position + Vector3.up * 1.2f, 0.7f, 1f);
            adam.Twist.Key(0.25f, 0f).Key(0.7f, -24f);
            adam.Side.Key(0.3f, 0f).Key(0.9f, 7f).Key(1.6f, 2f);

            s.Camera.Position.Key(0f, 4.3f, 1.2f, 6.2f).Key(1.875f, 4.2f, 1.2f, 6.08f, Ease.Linear);
            s.Camera.Target.Key(0f, 4.17f, 1.16f, 3.72f);
            s.Camera.Fov.Key(0f, 20f);
            s.Camera.Aperture.Key(0f, 2.4f);
            s.Camera.Life = 0.8f;
        }

        static void W26(FilmStage s)
        {
            var set = FilmWishSets.Terrace(s);
            TerraceGrade(s, 0.05f);
            var (adam, maya) = Seated(s, set);
            adam.Gaze(0f, Horizon, 0.5f);
            maya.Gaze(0f, Horizon, 0.5f);
            maya.Gaze(0.9f, adam.Root.transform.position + Vector3.up * 1.3f, 0.6f);
            maya.Twist.Key(0.9f, 0f).Key(1.5f, 20f);
            maya.Side.Key(1.0f, 0f).Key(1.8f, -8f);
            adam.Smile(0f, 40f);
            maya.Smile(0f, 50f);

            s.Camera.Position.Key(0f, 1.3f, 3.1f, -6.6f).Key(2.5f, 0.95f, 4.5f, -9.6f, Ease.Soft);
            s.Camera.Target.Key(0f, 4.1f, 1.6f, 8f).Key(2.5f, 4.1f, 2.6f, 8f);
            s.Camera.Fov.Key(0f, 40f).Key(2.5f, 43f);
            s.Camera.Aperture.Key(0f, 8f);
            s.Camera.Life = 0.5f;
        }

        // ------------------------------------------------------------ w27: the line
        static void W27(FilmStage s)
        {
            var set = FilmWishSets.Terrace(s);
            TerraceGrade(s, 0.1f);
            var (adam, maya) = Seated(s, set);
            Vector3 adamFace = set.ChairA.position + new Vector3(0f, 1.24f, 0.06f), mayaFace = set.ChairBHome + new Vector3(0f, 1.2f, 0.06f);
            // "Took you long enough."
            maya.Gaze(0f, Horizon, 0.5f);
            maya.Gaze(0.2f, adamFace, 0.75f, 1f, true);
            maya.Twist.Key(0.2f, 0f).Key(0.8f, 22f).Key(5.5f, 22f).Key(6.1f, 2f);
            maya.Smile(0f, 26f);
            maya.Smile(0.6f, 44f);
            maya.Smile(2.3f, 52f);
            maya.Shape("BrowOuterUpLeft").Key(0.9f, 0f).Key(1.3f, 46f).Key(2.2f, 12f);
            maya.Shape("BrowOuterUpRight").Key(0.9f, 0f).Key(1.3f, 38f).Key(2.2f, 10f);
            maya.Tilt.Key(0.4f, 0f).Key(1.2f, 6f).Key(2.4f, 3f);
            maya.Say(0.4f, Voice("maya-long-enough"));
            // he does not look round. "Traffic."
            adam.Gaze(0f, Horizon, 0.5f);
            adam.Smile(0f, 22f);
            adam.Smile(2.6f, 10f);
            adam.Gaze(3.0f, mayaFace, 0.12f, 1f);
            adam.Say(3.15f, Voice("adam-traffic"));
            adam.Shrug.Key(3.25f, 0f).Key(3.55f, 5f).Key(4.1f, 0f);
            adam.Shape("BrowInnerUp").Key(3.2f, 0f).Key(3.5f, 34f).Key(4.2f, 8f);
            adam.Both("BrowOuterUp", 3.2f, 0f);
            adam.Both("BrowOuterUp", 3.5f, 24f);
            adam.Both("BrowOuterUp", 4.2f, 0f);
            adam.Smile(4.15f, 10f);
            adam.Smile(4.55f, 46f);
            adam.Gaze(4.5f, Horizon, 0.5f, 1f, true);
            // it lands
            Laugh(maya, 4.45f, 0.9f, 0.8f);
            maya.Side.Key(4.9f, 0f).Key(5.25f, -11f, Ease.Out).Key(5.7f, -2f);
            adam.Side.Key(5.2f, 0f).Key(5.4f, -4f).Key(5.9f, 0f);
            adam.Smile(5.3f, 64f);
            maya.Gaze(5.5f, Horizon, 0.5f, 1f, true);

            Vector3 two = new(4.72f, 1.2f, 6.05f), twoAt = new(4.2f, 1.18f, 3.72f);
            Vector3 single = new(3.25f, 1.2f, 5.7f);
            Vector3 wide = new(4.17f, 1.24f, 6.05f), wideAt = new(4.17f, 1.17f, 3.72f);
            s.Camera.Position.Key(0f, two).Key(2.75f, two + new Vector3(-0.08f, 0f, -0.06f), Ease.Linear)
                .Key(2.751f, single, Ease.Hold).Key(4.75f, single + new Vector3(0.04f, 0f, -0.07f), Ease.Linear)
                .Key(4.751f, wide, Ease.Hold).Key(6.25f, wide + new Vector3(0f, 0.01f, 0.1f), Ease.Linear);
            s.Camera.Target.Key(0f, twoAt).Key(2.75f, twoAt).Key(2.751f, adamFace, Ease.Hold).Key(4.75f, adamFace).Key(4.751f, wideAt, Ease.Hold);
            s.Camera.Focus.Key(0f, mayaFace).Key(2.75f, mayaFace).Key(2.751f, adamFace, Ease.Hold).Key(4.75f, adamFace).Key(4.751f, (adamFace + mayaFace) * 0.5f, Ease.Hold);
            s.Camera.Fov.Key(0f, 18f).Key(2.75f, 18f).Key(2.751f, 19f, Ease.Hold).Key(4.75f, 19f).Key(4.751f, 19f, Ease.Hold);
            s.Camera.Aperture.Key(0f, 2.2f);
            s.Camera.Life = 0.6f;
        }

        // ------------------------------------------------------------ w28: the sky, for the title
        static void W28(FilmStage s)
        {
            var set = FilmWishSets.Terrace(s);
            TerraceGrade(s, 0.05f);
            var (adam, maya) = Seated(s, set);
            adam.Gaze(0f, Horizon, 0.5f);
            maya.Gaze(0f, Horizon, 0.5f);
            adam.Smile(0f, 40f);
            maya.Smile(0f, 46f);
            // up from behind their heads, over the wall, into the sky the two of them are under
            s.Camera.Position.Key(0f, 4.17f, 1.72f, 2.1f).Key(1.5f, 4.17f, 4.1f, 4.9f, Ease.Soft).Key(6.25f, 4.17f, 4.6f, 7.0f, Ease.Linear);
            s.Camera.Target.Key(0f, 4.17f, 1.55f, 12f).Key(1.5f, 4.7f, 4.6f, 60f, Ease.Soft).Key(6.25f, 4.7f, 5.3f, 60f, Ease.Linear);
            s.Camera.Fov.Key(0f, 40f);
            s.Camera.Aperture.Key(0f, 8f);
            s.Camera.Life = 0.3f;
        }

        // ------------------------------------------------------------ w29: the stinger
        static void W29(FilmStage s)
        {
            var set = FilmWishSets.Terrace(s);
            TerraceGrade(s, 0.1f);
            var (adam, maya) = Seated(s, set);
            Vector3 adamFace = set.ChairA.position + new Vector3(0f, 1.24f, 0.06f), mayaFace = set.ChairBHome + new Vector3(0f, 1.2f, 0.06f);
            // "Same time tomorrow?"
            maya.Gaze(0f, Horizon, 0.5f);
            maya.Gaze(0.45f, adamFace, 0.75f, 1f, true);
            maya.Twist.Key(0.45f, 0f).Key(1.0f, 22f);
            maya.Smile(0f, 34f);
            maya.Smile(1.2f, 52f);
            maya.Smile(2.1f, 74f);
            maya.Shape("BrowInnerUp").Key(0.9f, 0f).Key(1.4f, 40f).Key(2.3f, 14f);
            maya.Both("BrowOuterUp", 0.9f, 0f);
            maya.Both("BrowOuterUp", 1.4f, 30f);
            maya.Say(-0.12f, Voice("maya-tomorrow"));
            // his answer: the chair comes closer
            adam.Gaze(0f, Horizon, 0.5f);
            adam.Gaze(1.0f, mayaFace, 0.7f, 1f, true);
            adam.Twist.Key(1.0f, 0f).Key(1.5f, -20f);
            adam.Smile(0f, 30f);
            adam.Smile(1.5f, 42f);
            adam.Smile(2.05f, 80f);
            var chair = set.ChairA;
            Vector3 from = chair.position, to = from + new Vector3(0.15f, 0f, 0f);
            var hop = new Track().Key(0f, 0f).Key(1.5f, 0f).Key(1.9f, 1f, Ease.Out);
            s.Moves.Add(t =>
            {
                var k = hop.At(t);
                chair.position = Vector3.Lerp(from, to, k) + Vector3.up * (0.02f * Mathf.Sin(k * Mathf.PI));
            });
            for (var t = 1.4f; t <= 2.0f; t += 0.05f) adam.Path.Key(t, Vector3.Lerp(from, to, hop.At(t)), Ease.Linear);
            adam.Offset.Key(1.45f, 0f, 0f, 0f).Key(1.68f, 0f, 0.03f, 0f).Key(1.92f, 0f, 0f, 0f);
            adam.Lean.Key(1.35f, 0f).Key(1.6f, 8f).Key(2.0f, 0f);
            foreach (var arm in new[] { adam.Left, adam.Right })
            {
                var sideways = arm.IsLeft ? -0.25f : 0.25f;
                arm.Live = t => chair.TransformPoint(new Vector3(sideways, WishKit.SeatHeight + 0.01f, 0.02f));
                arm.Weight.Key(1.15f, 0f).Key(1.45f, 1f).Key(1.95f, 1f).Key(2.3f, 0f);
                arm.Grip.Key(1.2f, 0.1f).Key(1.45f, 0.7f).Key(2.0f, 0.3f);
            }

            s.Camera.Position.Key(0f, 4.33f, 1.2f, 5.9f).Key(2.5f, 4.26f, 1.2f, 5.75f, Ease.Linear);
            s.Camera.Target.Key(0f, 4.22f, 1.18f, 3.72f);
            s.Camera.Fov.Key(0f, 18f);
            s.Camera.Aperture.Key(0f, 2.2f);
            s.Camera.Life = 0.6f;
        }
    }
}
