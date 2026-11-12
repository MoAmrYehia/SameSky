// The shots of "Wish You Were Here", a 75-second animated film: Adam and Maya, far apart, each set out across their
// own city on the same evening and meet on a rooftop terrace above the water. For each shot: the set, who is on it,
// and every key of what moves. Times are seconds from the start of the shot at 24 frames a second; the film runs on
// a grid of 96 beats a minute (one beat is 15 frames), and scripts/demo/wish_cut.json puts the shots in order.
// Sets are in FilmWishSets.cs; the people and their motion clips come from FilmCast.cs.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SameSky.Film
{
    public static partial class FilmWish
    {
        public const float Beat = 0.625f, Bar = 2.5f;

        public static readonly List<ShotDef> All = new()
        {
            new ShotDef("w01", 2.5f, W01),
            new ShotDef("w02", 3.125f, W02),
            new ShotDef("w03", 3.125f, W03),
            new ShotDef("w04", 3.125f, W04),
            new ShotDef("w05", 3.125f, W05),
            new ShotDef("w06", 2.5f, W06),
            new ShotDef("w07", 2.5f, W07),
            new ShotDef("w08", 1.875f, W08),
            new ShotDef("w09a", 1.25f, W09a),
            new ShotDef("w09b", 1.25f, W09b),
            new ShotDef("w10a", 1.25f, W10a),
            new ShotDef("w10b", 1.25f, W10b),
            new ShotDef("w11", 3.125f, W11),
            new ShotDef("w12", 2.5f, W12),
            new ShotDef("w13", 2.5f, W13),
            new ShotDef("w14", 1.25f, W14),
            new ShotDef("w15", 1.25f, W15),
            new ShotDef("w16", 1.25f, W16),
            new ShotDef("w17a", 1.25f, W17a),
            new ShotDef("w17b", 0.625f, W17b),
            new ShotDef("w17c", 0.625f, W17c),
            new ShotDef("w18", 1.25f, W18),
            new ShotDef("w19", 5f, W19),
            new ShotDef("w20", 2.5f, W20),
            new ShotDef("w21", 1.25f, W21),
            new ShotDef("w22", 1.875f, W22),
            new ShotDef("w23", 1.25f, W23),
            new ShotDef("w24", 1.25f, W24),
            new ShotDef("w25", 1.875f, W25),
            new ShotDef("w26", 2.5f, W26),
            new ShotDef("w27", 6.25f, W27),
            new ShotDef("w28", 6.25f, W28),
            new ShotDef("w29", 2.5f, W29),
            new ShotDef("wtest", 6f, Test),
            new ShotDef("wlook", 4f, LookTest),
            new ShotDef("wset_harbour", 7f, SetHarbour),
            new ShotDef("wsit", 4f, SitTest),
            new ShotDef("wset_coast", 6f, SetCoast),
            new ShotDef("wset_terrace", 6f, SetTerrace),
        };

        /// <summary>Where a line's mouth shapes are (written by scripts/demo/trailer_voices.ps1 from wish_lines.json).</summary>
        public static string Voice(string line) => System.IO.Path.Combine(FilmRenderer.RepoRoot, "demo-build", "wish", "voice", line + ".json");

        /// <summary>Up a flight of stairs: a walk whose path climbs. The figure keeps the walking clip (there is no
        /// stair clip), leaning into the climb, so shots frame it from the waist up or from a distance.</summary>
        public static float Climb(FilmActor who, float t, Vector3 from, Vector3 to, string clip, float speed = 0.8f, float fade = 0.3f, float offset = 0f)
        {
            var flat = new Vector3(to.x - from.x, 0f, to.z - from.z).magnitude;
            var arrive = t + flat / Mathf.Max(0.05f, FilmCast.Pace(clip) * speed);
            who.Play(t, clip, fade, speed, offset);
            who.Path.Key(t, from, Ease.Linear).Key(arrive, to, Ease.Linear);
            who.Heading.Key(t, Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg, Ease.Hold);
            who.Lean.Key(t, 9f);
            // each step lifts the body: a small rise and fall at the pace of the feet
            var steps = FilmCast.Clip(clip).length / Mathf.Max(0.1f, speed) * 0.5f;
            var home = who.Offset;
            for (var k = 0; t + k * steps < arrive; k++)
                home.Key(t + k * steps, 0f, 0f, 0f, Ease.InOut).Key(t + (k + 0.5f) * steps, 0f, 0.035f, 0f, Ease.InOut);
            return arrive;
        }

        /// <summary>Sits someone on a chair or bench at <paramref name="seat"/> (the seat's middle on the ground),
        /// facing <paramref name="yaw"/>: the upright seated loop, hands in the lap.</summary>
        public static void Sit(FilmActor who, float t, Vector3 seat, float yaw, float fade = 0f)
        {
            who.Play(t, "f_sit_chair_breathe_01", fade, 1f, FilmCast.IsFemale(who.Name) ? 0f : 1.3f);
            who.Path.Key(t, seat, Ease.Hold);
            who.Heading.Key(t, yaw, Ease.Hold);
        }

        /// <summary>A light that travels with someone: the soft key a film keeps on its lead.</summary>
        public static Light Follow(FilmStage s, FilmActor who, Vector3 offset, Color colour, float intensity, float range = 6f)
        {
            var light = s.Lamp("Light on " + who.Name, who.Root.transform.position + offset, colour, intensity, range);
            s.Moves.Add(t => light.transform.position = who.Root.transform.position + offset);
            return light;
        }

        // a seat check: the two on the terrace's chairs, from the front and from the side
        static void SitTest(FilmStage s)
        {
            var set = FilmWishSets.Terrace(s);
            var adam = s.Lead("adam", set.ChairAHome, 8f, 1f);
            var maya = s.Lead("maya", set.ChairBHome, -10f, 2f);
            adam.Play(0f, "m_sit_chair_breathe_01").Play(1f, "m_sit_chair_idle_look_around", 0f).Play(2f, "f_sit_chair_breathe_01", 0f).Play(3f, "m_sit_down_chair_01", 0f, 1f, 3.2f);
            maya.Play(0f, "f_sit_chair_breathe_01");
            var hips = new System.Func<FilmActor, Vector3>(a => a.Root.transform.InverseTransformPoint(a.Animator.GetBoneTransform(HumanBodyBones.Hips).position));
            s.Notes.Add(() => $"hips in the figure's own space: adam {hips(adam):F3}, maya {hips(maya):F3}; sit-down clip {FilmCast.Clip("m_sit_down_chair_01").length:F2} s, f {FilmCast.Clip("f_sit_down_chair_01").length:F2} s");
            var from = new Vector3(-0.2f, 1.0f, 3.2f);
            var to = new Vector3(3.6f, 0.75f, 3.8f);
            Views(s, (from, to, 30f), (from, to, 30f), (from, to, 30f), (from, to, 30f));
        }

        // a look check: the two leads close, in a low warm sun and then in blue evening light, neutral then smiling
        static void LookTest(FilmStage s)
        {
            s.Box("Floor", new Vector3(0f, -0.05f, 0f), new Vector3(40f, 0.1f, 40f), FilmStage.Mat(new Color(0.5f, 0.48f, 0.45f), 0.2f));
            var sun = s.Sun(new Vector3(16f, 38f, 0f), new Color(1f, 0.72f, 0.48f), 2.2f);
            s.Ambient(new Color(0.42f, 0.46f, 0.62f), new Color(0.4f, 0.36f, 0.36f), new Color(0.2f, 0.17f, 0.15f));
            var adam = s.Lead("adam", new Vector3(-0.42f, 0f, 0f), 180f, 1f);
            var maya = s.Lead("maya", new Vector3(0.42f, 0f, 0f), 180f, 2f);
            adam.Play(0f, "m_idle_breathe_01");
            maya.Play(0f, "f_idle_breathe_01");
            var lens = new Vector3(0f, 1.52f, -1.5f);
            adam.Gaze(0f, lens, 0.9f);
            maya.Gaze(0f, lens, 0.9f);
            foreach (var who in new[] { adam, maya }) { who.Smile(0f, 0f); who.Smile(0.9f, 0f); who.Smile(1.6f, 65f); who.Smile(2.2f, 0f); who.Smile(3.6f, 65f); }
            s.Moves.Add(t =>
            {
                var evening = t >= 2f;
                sun.color = evening ? new Color(0.55f, 0.6f, 1f) : new Color(1f, 0.72f, 0.48f);
                sun.intensity = evening ? 0.9f : 2.2f;
            });
            s.Camera.Position.Key(0f, lens);
            s.Camera.Target.Key(0f, 0f, 1.5f, 0f);
            s.Camera.Fov.Key(0f, 30f);
            s.Camera.Aperture.Key(0f, 5.6f);
            s.Camera.Life = 0f;
        }

        // a rig check: the leads and the four background people on an open floor, walking, idling, laughing, dancing
        static void Test(FilmStage s)
        {
            s.Box("Floor", new Vector3(0f, -0.05f, 0f), new Vector3(40f, 0.1f, 40f), FilmStage.Mat(new Color(0.55f, 0.53f, 0.5f), 0.2f));
            s.Sun(new Vector3(38f, -40f, 0f), new Color(1f, 0.93f, 0.82f), 1.5f);
            s.Ambient(new Color(0.55f, 0.6f, 0.7f), new Color(0.45f, 0.44f, 0.42f), new Color(0.25f, 0.23f, 0.2f));
            s.Pipeline.shadowDistance = 40f;

            var adam = s.Lead("adam", new Vector3(-3f, 0f, 0f), 90f, 1f);
            var maya = s.Lead("maya", new Vector3(3f, 0f, 1.2f), -90f, 2f);
            adam.Walk(0f, new Vector3(-3.5f, 0f, 0f), new Vector3(4f, 0f, 0f), "m_walk_neutral_01");
            maya.Walk(0f, new Vector3(3.5f, 0f, 1.2f), new Vector3(-4f, 0f, 1.2f), "f_walk_stroll_01");
            adam.Gaze(0f, new Vector3(8f, 1.6f, 0f), 0.6f);
            maya.Gaze(0f, new Vector3(-8f, 1.5f, 1.2f), 0.6f);
            maya.Smile(0f, 0f);
            maya.Smile(2f, 60f);

            var older = s.Extra("Male_Adult_03", new Vector3(0f, 0f, 3f), 180f, 3f);
            older.Play(0f, "m_idle_breathe_01");
            var denim = s.Extra("Male_Adult_12", new Vector3(-4f, 0f, 2.2f), 90f, 4f);
            denim.Walk(0f, new Vector3(-4f, 0f, 2.2f), new Vector3(4f, 0f, 2.2f), "m_walk_cool_01");
            var knit = s.Extra("Female_Adult_02", new Vector3(1.3f, 0f, 3f), 180f, 5f);
            knit.Play(0f, "f_gestic_laugh_low");
            var coat = s.Extra("Female_Adult_09", new Vector3(-1.5f, 0f, 3.2f), 180f, 6f);
            coat.Play(0f, "f_dancing_neutral");

            s.Notes.Add(() => $"adam at {adam.Root.transform.position:F2}, pace {FilmCast.Pace("m_walk_neutral_01"):F2} m/s; maya pace {FilmCast.Pace("f_walk_stroll_01"):F2}; head heights {adam.Head.position.y:F2} {maya.Head.position.y:F2}");
            s.Camera.Position.Key(0f, 0f, 1.5f, -7f);
            s.Camera.Target.Key(0f, 0f, 1f, 1f);
            s.Camera.Fov.Key(0f, 44f);
            s.Camera.Aperture.Key(0f, 11f);
            s.Camera.Life = 0f;
        }
    }
}
