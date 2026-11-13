// The shots of "I Saved You a Seat", the animated concept trailer: for each one, the set, who is on it, and every key
// of what moves (hands, eyes, faces, the camera). Times are seconds from the start of the shot at 24 frames a second.
// The edit (scripts/demo/trailer_edit.py) cuts them together with the voices, the score and the subtitles.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SameSky.Film
{
    public sealed class ShotDef
    {
        public readonly string Id;
        public readonly float Seconds;
        public readonly Action<FilmStage> Build;
        public ShotDef(string id, float seconds, Action<FilmStage> build) { Id = id; Seconds = seconds; Build = build; }
    }

    public static partial class FilmShots
    {
        public static readonly List<ShotDef> All = new()
        {
            new ShotDef("s01", 7f, S01),
            new ShotDef("s02", 3f, S02),
            new ShotDef("s03", 3f, S03),
            new ShotDef("s04", 3.5f, S04),
            new ShotDef("s05a", 4.5f, S05a),
            new ShotDef("s05b", 3f, S05b),
            new ShotDef("s06", 4f, S06),
            new ShotDef("s07a", 3.5f, S07a),
            new ShotDef("s08", 3.5f, S08),
            new ShotDef("s07b", 4f, S07b),
            new ShotDef("s09", 3f, S09),
            new ShotDef("s10", 3.5f, S10),
            new ShotDef("s11", 3.5f, S11),
            new ShotDef("s12", 5f, S12),
            new ShotDef("s13a", 4f, S13a),
            new ShotDef("s13b", 3.5f, S13b),
            new ShotDef("s14", 5.5f, S14),
            new ShotDef("s15a", 2.5f, S15a),
            new ShotDef("s15b", 2.5f, S15b),
            new ShotDef("s16", 6f, S16),
            new ShotDef("s17", 4.5f, S17),   // half a second more than its place in the cut: the dissolve into the next evening
            new ShotDef("s18", 4.5f, S18),
            new ShotDef("s19", 6f, S19),
            new ShotDef("s20", 4.5f, S20),
            new ShotDef("s21a", 4f, S21a),
            new ShotDef("s21b", 4f, S21b),
            new ShotDef("s22", 3.5f, S22),
            new ShotDef("s23", 6.5f, S23),
            new ShotDef("s24", 5f, S24),
            new ShotDef("test", 5f, Test),
            new ShotDef("testlook", 4f, TestLook),
            new ShotDef("testface", 6f, TestFace),
            new ShotDef("m_sunset", 8f, MediaSunset),
            new ShotDef("m_bench", 1f / 24f, MediaBench),
            new ShotDef("m_sky", 1f / 24f, MediaSky),
            new ShotDef("setwater", 4f, SetWater),
            new ShotDef("sethome", 4f, SetHome),
            new ShotDef("setdesk", 4f, SetDesk),
        };

        // set checks: each set from a few places (the camera jumps every second)
        static void Views(FilmStage s, params (Vector3 from, Vector3 to, float fov)[] views)
        {
            for (var i = 0; i < views.Length; i++)
            {
                s.Camera.Position.Key(i, views[i].from, Ease.Hold).Key(i + 0.99f, views[i].from, Ease.Hold);
                s.Camera.Target.Key(i, views[i].to, Ease.Hold).Key(i + 0.99f, views[i].to, Ease.Hold);
                s.Camera.Fov.Key(i, views[i].fov, Ease.Hold).Key(i + 0.99f, views[i].fov, Ease.Hold);
            }
            s.Camera.Aperture.Key(0f, 8f);
            s.Camera.Life = 0f;
        }

        static void SetWater(FilmStage s)
        {
            var set = FilmSets.Waterfront(s);
            var maya = s.Actor("maya", set.Seat, 0f, 2f);
            Views(s, (new Vector3(0.9f, 1.5f, -3.4f), new Vector3(0.2f, 1.1f, 6f), 38f),
                (new Vector3(1.6f, 1.2f, 1.7f), new Vector3(-0.3f, 1.05f, 0f), 30f),
                (new Vector3(-2.2f, 1.3f, 1.2f), new Vector3(0.3f, 0.8f, 0f), 40f),
                (new Vector3(0.2f, 4f, -7f), new Vector3(0.2f, 0.5f, 3f), 50f));
        }

        static void SetHome(FilmStage s)
        {
            var set = FilmSets.Home(s);
            var adam = s.Actor("adam", set.Seat, 0f, 1f);
            Views(s, (new Vector3(0.2f, 1.35f, 3.4f), new Vector3(-0.1f, 0.9f, 0f), 42f),
                (new Vector3(1.1f, 1.1f, 1.5f), new Vector3(-0.35f, 1.0f, -0.1f), 30f),
                (new Vector3(3.55f, 1.05f, -0.02f), new Vector3(4.28f, 0.92f, -0.66f), 30f),
                (new Vector3(2.4f, 1.6f, 3f), new Vector3(3f, 1f, -0.6f), 50f));
        }

        static void SetDesk(FilmStage s)
        {
            var set = FilmSets.Desk(s);
            var maya = s.Actor("maya", set.Seat, 0f, 2f);
            Views(s, (new Vector3(0.95f, 1.0f, -0.3f), new Vector3(0.2f, 0.82f, 0.2f), 34f),
                (new Vector3(0.3f, 1.5f, -2.4f), new Vector3(0.1f, 0.9f, 0.3f), 40f),
                (new Vector3(-1.2f, 1.1f, -0.5f), new Vector3(0.2f, 0.85f, 0.3f), 36f),
                (new Vector3(0.1f, 1.3f, 0.1f), new Vector3(-0.05f, 1.1f, -0.3f), 40f));
        }

        // a calibration: each of them looking at something level and far away, seen exactly from the side, with the
        // face's assumed direction corrected by 0, 8, 16 degrees (the picture changes every second)
        static void TestFace(FilmStage s)
        {
            var room = FilmSets.Lounge(s);
            var adam = s.Actor("adam", room.Adam, 0f, 1f);
            var maya = s.Actor("maya", room.Maya + new Vector3(3f, 0f, 0f), 180f, 2f);
            adam.Gaze(0f, new Vector3(0f, adam.Eyes.y, 30f), 1f);
            maya.Gaze(0f, new Vector3(3f, maya.Eyes.y, -30f), 1f);
            adam.EyeLife = 0f;
            maya.EyeLife = 0f;
            s.Moves.Add(t =>
            {
                var pitch = 8f * (Mathf.FloorToInt(t) % 3);
                adam.FacePitch = pitch;
                maya.FacePitch = pitch;
            });
            var a = adam.Eyes;
            var m = maya.Eyes;
            for (var i = 0; i < 6; i++)
            {
                var who = i < 3 ? a : m;
                var from = who + new Vector3(i < 3 ? 1.3f : 1.3f, 0f, 0f);
                s.Camera.Position.Key(i, from, Ease.Hold).Key(i + 0.99f, from, Ease.Hold);
                s.Camera.Target.Key(i, who, Ease.Hold).Key(i + 0.99f, who, Ease.Hold);
            }
            s.Camera.Fov.Key(0f, 20f);
            s.Camera.Aperture.Key(0f, 8f);
            s.Camera.Life = 0f;
        }

        // a look check: each face close, neutral then smiling
        static void TestLook(FilmStage s)
        {
            var room = FilmSets.Lounge(s);
            var adam = s.Actor("adam", room.Adam, 0f, 1f);
            var maya = s.Actor("maya", room.Maya, 180f, 2f);
            maya.Look.Key(0f, adam.Eyes);
            maya.LookEyes.Key(0f, 1f);
            maya.LookHead.Key(0f, 0.9f);
            adam.Look.Key(0f, maya.Eyes);
            adam.LookEyes.Key(0f, 1f);
            adam.LookHead.Key(0f, 0.9f);
            foreach (var who in new[] { maya, adam })
            {
                who.Shape("MouthSmileLeft").Key(0.5f, 0f).Key(1.5f, 70f).Key(2.5f, 0f).Key(3.5f, 70f);
                who.Shape("MouthSmileRight").Key(0.5f, 0f).Key(1.5f, 66f).Key(2.5f, 0f).Key(3.5f, 66f);
                who.Shape("CheekSquintLeft").Key(0.5f, 0f).Key(1.5f, 45f).Key(2.5f, 0f).Key(3.5f, 45f);
                who.Shape("CheekSquintRight").Key(0.5f, 0f).Key(1.5f, 45f).Key(2.5f, 0f).Key(3.5f, 45f);
            }
            var mayaFace = maya.Eyes + new Vector3(0f, -0.03f, 0f);
            var adamFace = adam.Eyes + new Vector3(0f, -0.03f, 0f);
            s.Camera.Position.Key(0f, mayaFace + new Vector3(0.22f, 0.02f, -0.62f)).Key(1.99f, mayaFace + new Vector3(0.22f, 0.02f, -0.62f)).Key(2f, adamFace + new Vector3(0.25f, 0.02f, 0.62f), Ease.Hold);
            s.Camera.Target.Key(0f, mayaFace).Key(1.99f, mayaFace).Key(2f, adamFace, Ease.Hold);
            s.Camera.Fov.Key(0f, 30f);
            s.Camera.Aperture.Key(0f, 2.8f);
            s.Camera.Life = 0f;
        }

        // a rig check: both of them at the table, a reach, a grip, a lift, a wave, a smile
        static void Test(FilmStage s)
        {
            var room = FilmSets.Lounge(s);
            var adam = s.Actor("adam", room.Adam, 0f, 1f);
            var maya = s.Actor("maya", room.Maya, 180f, 2f);
            var rest = new Pose(new Vector3(0.2f, FilmSets.TableTop, 0.5f), Quaternion.identity);
            var cup = FilmSets.Cup(s, rest.position);
            var grip = FilmSets.MugHandle(Vector3.right, Vector3.forward);
            Vector3 fingers = new(-0.2f, 0f, 1f), palm = new(-1f, 0f, -0.2f);
            var lifted = new Pose(rest.position + new Vector3(-0.02f, 0.2f, -0.12f), Quaternion.identity);

            adam.Right.Fingers.Key(0f, fingers);
            adam.Right.Palm.Key(0f, palm);
            adam.Right.Aim.Key(0.4f, 0f).Key(1.4f, 1f);
            adam.Right.Weight.Key(0.4f, 0f).Key(1.4f, 1f);
            adam.Right.Goal.Key(0.4f, grip.Wrist(rest, fingers, palm) + new Vector3(0.03f, 0.05f, -0.1f))
                .Key(1.5f, grip.Wrist(rest, fingers, palm), Ease.Soft)
                .Key(2.1f, grip.Wrist(rest, fingers, palm))
                .Key(3.2f, grip.Wrist(lifted, fingers, palm), Ease.Soft);
            adam.Right.Grip.Key(1.3f, 0.1f).Key(2f, 0.8f);
            s.Hold(cup, adam.Right, grip, 2.0f, 99f, rest, rest);
            adam.Look.Key(0f, rest.position + Vector3.up * 0.05f).Key(2.6f, rest.position + Vector3.up * 0.05f).Key(3.3f, maya.Eyes);
            adam.LookEyes.Key(0f, 0f).Key(0.5f, 1f);
            adam.LookHead.Key(0.2f, 0f).Key(1f, 0.7f);
            adam.Shape("MouthSmileLeft").Key(3f, 0f).Key(3.8f, 55f);
            adam.Shape("MouthSmileRight").Key(3f, 0f).Key(3.8f, 50f);
            adam.Lean.Key(0.4f, 0f).Key(1.5f, 6f).Key(3.2f, 1f);

            Vector3 up = new(0.1f, 1f, 0f), out_ = new(0f, 0f, -1f);
            maya.Right.Fingers.Key(0f, up).Key(2.6f, new Vector3(0.35f, 1f, 0f)).Key(2.9f, new Vector3(-0.2f, 1f, 0f)).Key(3.2f, new Vector3(0.35f, 1f, 0f)).Key(3.5f, new Vector3(-0.2f, 1f, 0f)).Key(3.9f, up);
            maya.Right.Palm.Key(0f, out_);
            maya.Right.Aim.Key(1.6f, 0f).Key(2.4f, 1f);
            maya.Right.Weight.Key(1.6f, 0f).Key(2.4f, 1f);
            maya.Right.Goal.Key(1.6f, room.Maya + new Vector3(-0.28f, 1.02f, -0.2f)).Key(2.4f, room.Maya + new Vector3(-0.3f, 1.16f, -0.22f), Ease.Overshoot);
            maya.Look.Key(0f, adam.Eyes);
            maya.LookEyes.Key(0f, 1f);
            maya.LookHead.Key(0f, 0.8f);
            maya.Shape("MouthSmileLeft").Key(1.5f, 10f).Key(2.4f, 70f);
            maya.Shape("MouthSmileRight").Key(1.5f, 10f).Key(2.4f, 66f);
            maya.Shape("CheekSquintLeft").Key(1.5f, 0f).Key(2.4f, 40f);
            maya.Shape("CheekSquintRight").Key(1.5f, 0f).Key(2.4f, 40f);
            maya.Shape("BrowInnerUp").Key(1.5f, 0f).Key(2.2f, 30f);

            s.Camera.Position.Key(0f, 1.55f, 1.3f, -0.5f).Key(5f, 1.42f, 1.26f, -0.42f);
            s.Camera.Target.Key(0f, 0.02f, 1.0f, 0.72f);
            s.Camera.Fov.Key(0f, 36f);
            s.Camera.Aperture.Key(0f, 4f);
        }
    }
}
