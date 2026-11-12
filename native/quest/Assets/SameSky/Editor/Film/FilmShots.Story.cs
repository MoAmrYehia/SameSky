// "I Saved You a Seat": the story's shots, in order. Each builds its set, puts the people on it and keys what
// happens. Times are seconds from the start of the shot.
//
//   1  the empty seat          s01
//   2  two different days      s02 s03 s04 s05a s05b
//   3  save the moment         s06 s07a s08 s07b s09
//   4  a message you can hold  s10 s11 s12 s13a s13b
//   5  her day, shared         s14 s15a s15b s16 s17
//   6  the seat is filled      s18 s19 s20 s21a s21b
//   7  a new ritual            s22 s23 s24

using System.IO;
using SameSky.UI;
using TMPro;
using UnityEngine;

namespace SameSky.Film
{
    public static partial class FilmShots
    {
        static Vector3 V(float x, float y, float z) => new(x, y, z);
        static string Voice(string id) => Path.Combine(FilmRenderer.Root, "voice", id + ".json");

        static void Cam(FilmStage s, float t, Vector3 from, Vector3 to, float fov, Ease e = Ease.InOut)
        {
            s.Camera.Position.Key(t, from, e);
            s.Camera.Target.Key(t, to, e);
            s.Camera.Fov.Key(t, fov, e);
        }

        /// <summary>A key for a hand: where the wrist is, the way the fingers point and the palm faces.</summary>
        static void Hand(FilmArm arm, float t, Vector3 wrist, Vector3 fingers, Vector3 palm, Ease e = Ease.InOut)
        {
            arm.Goal.Key(t, wrist, e);
            arm.Fingers.Key(t, fingers.normalized, e);
            arm.Palm.Key(t, palm.normalized, e);
        }

        static void On(FilmArm arm, float grip = 0.12f)
        {
            arm.Weight.Key(0f, 1f);
            arm.Aim.Key(0f, 1f);
            arm.Grip.Key(0f, grip);
        }

        /// <summary>A hand lying where it is put for the whole shot (on a lap, a table, an armrest).</summary>
        static void Rest(FilmArm arm, Vector3 wrist, Vector3 fingers, Vector3 palm, float grip = 0.16f)
        {
            On(arm, grip);
            Hand(arm, 0f, wrist, fingers, palm);
        }

        /// <summary>A phone lying in an open hand: its back on the palm, its length along the fingers.</summary>
        static readonly Grip PhoneInHand = new(new Vector3(0f, 0.031f, 0.088f), Quaternion.LookRotation(Vector3.down, Vector3.forward));

        /// <summary>The slow rise and fall of breathing in the shoulders, on top of whatever else they do.</summary>
        static void Breathe(FilmActor a, float seconds, float amount = 1.2f, float period = 3.6f, float phase = 0f)
        {
            for (var t = 0f; t <= seconds + period; t += period / 2f)
            {
                var up = Mathf.RoundToInt(t / (period / 2f)) % 2 == 0;
                a.Shrug.Key(t + phase, a.Shrug.At(t + phase) + (up ? amount : -amount * 0.4f), Ease.Soft);
            }
        }

        // ============================================================ 1  THE EMPTY SEAT

        /// <summary>The lounge, late afternoon. A hand sets a second cup down at the empty place, and hesitates.</summary>
        static void S01(FilmStage s)
        {
            var room = FilmSets.Lounge(s, 0.3f);
            var maya = s.Actor("maya", room.Maya, 180f, 2f);
            maya.Offset.Key(0f, V(0f, 0f, -0.27f));
            FilmSets.Coaster(s, V(room.AdamCupAt.x, FilmSets.TableTop, room.AdamCupAt.z));
            FilmSets.Cup(s, room.MayaCupAt, 200f);
            var cup = FilmSets.Cup(s, room.AdamCupAt);
            Vector3 fingers = V(0f, -0.12f, -1f), palm = Vector3.left;
            var grip = FilmSets.MugHandle(Vector3.right, fingers);
            Pose Cup(Vector3 at) => new(at, Quaternion.identity);
            Pose held = Cup(V(0.07f, 0.9f, 1.0f)), above = Cup(room.AdamCupAt + Vector3.up * 0.045f), down = Cup(room.AdamCupAt);

            On(maya.Left, 0.78f);
            Hand(maya.Left, 0f, grip.Wrist(held, fingers, palm), fingers, palm);
            Hand(maya.Left, 1.9f, grip.Wrist(above, fingers, palm), fingers, palm, Ease.Soft);
            Hand(maya.Left, 2.45f, grip.Wrist(down, fingers, palm), fingers, palm);
            Hand(maya.Left, 3.6f, grip.Wrist(down, fingers, palm), fingers, palm);
            // she lets go, and the hand stays a moment before it comes away
            Hand(maya.Left, 4.1f, grip.Wrist(down, fingers, palm) + V(0.012f, 0.012f, 0.02f), fingers, palm);
            Hand(maya.Left, 5.4f, V(0.19f, 0.865f, 1.0f), V(0f, -0.2f, -1f), V(-0.5f, -1f, 0f), Ease.Soft);
            Hand(maya.Left, 6.5f, V(0.15f, 0.805f, 1.12f), V(0.1f, -0.05f, -1f), Vector3.down, Ease.Soft);
            maya.Left.Grip.Key(3.55f, 0.78f).Key(4.05f, 0.1f).Key(5.4f, 0.28f).Key(6.5f, 0.14f);
            s.Hold(cup, maya.Left, grip, -1f, 3.6f, held, down, 0.12f);
            Rest(maya.Right, V(-0.13f, 0.8f, 1.2f), V(0.25f, -0.05f, -1f), Vector3.down);
            maya.Lean.Key(0f, 12f).Key(1.9f, 40f, Ease.Soft).Key(3.7f, 40f).Key(5.4f, 15f, Ease.Soft).Key(7f, 11f);
            maya.Gaze(0f, room.AdamCupAt, 0.5f);

            Cam(s, 0f, V(1.95f, 0.93f, 0.24f), V(0f, 0.84f, 0.12f), 26f);
            Cam(s, 7f, V(1.6f, 0.91f, 0.34f), V(0f, 0.84f, 0.3f), 26f, Ease.Linear);
            s.Camera.Focus.Key(0f, room.AdamCupAt + Vector3.up * 0.05f);
            s.Camera.Aperture.Key(0f, 2.4f);
            s.Camera.Grade(8f, 6f, 4f, 0.3f, 0.6f);
        }

        // ============================================================ 2  TWO DIFFERENT DAYS

        /// <summary>Adam's flat, the end of his working day: keys into the bowl by the door.</summary>
        static void S02(FilmStage s)
        {
            var home = FilmSets.Home(s, 0f);
            var adam = s.Actor("adam", V(4.5f, 0.2f, -0.16f), 180f, 1f);
            adam.HideLegs();
            adam.Lean.Key(0f, 6f).Key(1f, 12f).Key(2.4f, 4f);
            Vector3 fingers = V(0f, -0.85f, -0.5f), palm = V(0f, -0.5f, 0.85f);
            var landed = new Pose(home.Bowl + V(0f, 0.014f, 0.01f), Quaternion.Euler(0f, 35f, 0f));
            var keys = FilmProps.Keys(s, landed.position, 35f);
            // in the hand they hang from the fingers
            var grip = Grip.From(new Pose(fingers.normalized * 0.165f, Quaternion.FromToRotation(Vector3.right, fingers.normalized)), Vector3.zero, fingers, palm);
            var over = landed.position + Vector3.up * 0.03f - fingers.normalized * 0.165f;
            On(adam.Right, 0.82f);
            Hand(adam.Right, 0f, V(4.31f, 1.17f, -0.4f), fingers, palm);
            Hand(adam.Right, 0.95f, over, fingers, palm, Ease.Soft);
            Hand(adam.Right, 1.35f, over + V(0f, 0.012f, 0f), fingers, palm);
            Hand(adam.Right, 2.3f, V(4.33f, 1.2f, -0.36f), V(0f, -0.6f, -0.8f), V(0f, -0.8f, 0.6f), Ease.Soft);
            Hand(adam.Right, 3f, V(4.42f, 1.42f, -0.26f), V(0f, -0.5f, -0.85f), V(0f, -0.85f, 0.5f));
            adam.Right.Grip.Key(1f, 0.82f).Key(1.25f, 0.12f).Key(2.3f, 0.2f);
            Rest(adam.Left, V(4.78f, 1.02f, -0.12f), V(0f, -1f, -0.1f), Vector3.left);   // at his side, out of the picture
            s.Hold(keys, adam.Right, grip, -1f, 1f, landed, landed, 0.09f);
            // they slide a little and settle
            s.Moves.Add(t =>
            {
                if (t < 1f) return;
                var k = Mathf.Exp(-(t - 1f) * 7f);
                keys.position = landed.position + V(0.012f * k * Mathf.Cos((t - 1f) * 26f), 0.004f * k, -0.01f * k);
                keys.rotation = landed.rotation * Quaternion.Euler(0f, 14f * k * Mathf.Sin((t - 1f) * 22f), 5f * k);
            });

            Cam(s, 0f, V(3.5f, 1.24f, -0.02f), V(4.27f, 0.95f, -0.66f), 28f);
            Cam(s, 3f, V(3.6f, 1.22f, -0.1f), V(4.27f, 0.94f, -0.66f), 28f, Ease.Linear);
            s.Camera.Focus.Key(0f, home.Bowl + Vector3.up * 0.03f);
            s.Camera.Aperture.Key(0f, 2f);
            s.Camera.Grade(-6f, 0f, 4f, 0.26f, 0.45f);
            s.Lamp("Soft fill", V(3.6f, 1.6f, 0.6f), new Color(0.9f, 0.95f, 1f), 1.1f, 5f);
        }

        /// <summary>Maya's desk, later in her evening: the mug goes down beside the closed laptop.</summary>
        static void S03(FilmStage s)
        {
            var desk = FilmSets.Desk(s);
            var maya = s.Actor("maya", desk.Seat, 0f, 2f);
            var mugAt = V(0.16f, desk.Top, 0.02f);
            var mug = FilmSets.Cup(s, mugAt, 0f, FilmProps.Tint("Ceramic", new Color(0.72f, 0.8f, 0.74f), 0.6f));
            Vector3 fingers = V(0f, -0.1f, 1f), palm = Vector3.left;
            var grip = FilmSets.MugHandle(Vector3.right, fingers);
            Pose Mug(Vector3 at) => new(at, Quaternion.identity);
            Pose held = Mug(V(0.14f, 0.93f, -0.1f)), down = Mug(mugAt);
            On(maya.Right, 0.78f);
            Hand(maya.Right, 0f, grip.Wrist(held, fingers, palm), fingers, palm);
            Hand(maya.Right, 1.15f, grip.Wrist(Mug(mugAt + Vector3.up * 0.03f), fingers, palm), fingers, palm, Ease.Soft);
            Hand(maya.Right, 1.5f, grip.Wrist(down, fingers, palm), fingers, palm);
            Hand(maya.Right, 2f, grip.Wrist(down, fingers, palm), fingers, palm);
            Hand(maya.Right, 2.8f, V(0.3f, desk.Top + 0.035f, -0.04f), V(-0.15f, 0f, 1f), Vector3.down, Ease.Soft);
            maya.Right.Grip.Key(1.7f, 0.78f).Key(2.05f, 0.15f);
            s.Hold(mug, maya.Right, grip, -1f, 1.6f, held, down, 0.1f);
            Rest(maya.Left, V(-0.2f, desk.Top + 0.035f, -0.03f), V(0.2f, 0f, 1f), Vector3.down);
            maya.Lean.Key(0f, 4f).Key(1.4f, 9f).Key(2.6f, 3f, Ease.Soft);
            maya.Shrug.Key(1.6f, 3f).Key(2.6f, -4f, Ease.Soft);
            maya.Gaze(0f, mugAt + Vector3.up * 0.1f, 0.6f);
            maya.Gaze(1.9f, V(0.3f, 1.25f, 3f), 0.5f, 1f, true);
            maya.Smile(2f, 0f);
            maya.Smile(2.8f, 26f);
            maya.Both("EyeBlink", 2f, 0f);
            maya.Both("EyeBlink", 2.6f, 22f);

            Cam(s, 0f, V(1.0f, 1.0f, -0.42f), V(0.1f, 0.86f, 0.06f), 30f);
            Cam(s, 3f, V(0.93f, 0.99f, -0.35f), V(0.1f, 0.86f, 0.06f), 30f, Ease.Linear);
            s.Camera.Focus.Key(0f, mugAt + Vector3.up * 0.05f);
            s.Camera.Aperture.Key(0f, 2.2f);
            s.Camera.Grade(10f, 6f, 4f, 0.3f, 0.6f);
        }

        /// <summary>Adam on his sofa: the shoulders drop, a glance at the phone, a tired smile.</summary>
        static void S04(FilmStage s)
        {
            var home = FilmSets.Home(s, 0.12f);
            var adam = s.Actor("adam", home.Seat, 0f, 1f);
            FilmProps.Headset(s, V(0.2f, home.TableTop + 0.047f, 0.72f), Quaternion.Euler(0f, 150f, 0f));
            Rest(adam.Right, home.Seat + V(0.15f, 0.665f, 0.24f), V(-0.12f, -0.3f, 1f), V(0f, -1f, -0.25f));
            var phone = new FilmPhone(s, Vector3.zero, Quaternion.identity);
            Vector3 flatF = V(0.1f, -0.22f, 1f), flatP = V(0f, 1f, 0.22f), upF = V(0.18f, 0.6f, 0.78f), upP = V(0.12f, 0.74f, -0.62f);
            On(adam.Left, -0.28f);
            Hand(adam.Left, 0f, home.Seat + V(-0.17f, 0.67f, 0.2f), flatF, flatP);
            Hand(adam.Left, 1.5f, home.Seat + V(-0.17f, 0.67f, 0.2f), flatF, flatP);
            Hand(adam.Left, 2.35f, home.Seat + V(-0.13f, 0.9f, 0.34f), upF, upP, Ease.Soft);
            adam.Left.Thumb.Key(0f, 0.3f);
            s.Hold(phone.Root, adam.Left, PhoneInHand, -1f, 99f, default, default);
            var glow = s.Lamp("Phone glow", Vector3.zero, new Color(0.82f, 0.9f, 1f), 0f, 1.1f);
            s.Moves.Add(t =>
            {
                var lit = Mathf.SmoothStep(0f, 1f, (t - 1.95f) / 0.3f);
                phone.Lit(lit);
                glow.transform.position = phone.At(0f, 0f, 0.12f);
                glow.intensity = 0.075f * lit;
            });
            // he lets the day go: shoulders down, back into the cushions, eyes shut for a breath
            adam.Shrug.Key(0f, 7f).Key(0.35f, 8f).Key(1.3f, -5f, Ease.Soft);
            adam.Lean.Key(0f, -2f).Key(1.3f, -11f, Ease.Soft).Key(2.4f, -8f);
            adam.Nod.Key(0f, 0f).Key(0.9f, -7f, Ease.Soft).Key(1.7f, 2f, Ease.Soft);
            adam.Both("EyeBlink", 0.3f, 0f);
            adam.Both("EyeBlink", 0.7f, 78f);
            adam.Both("EyeBlink", 1.3f, 78f);
            adam.Both("EyeBlink", 1.6f, 0f);
            adam.Shape("JawOpen").Key(0.4f, 0f).Key(0.8f, 9f).Key(1.4f, 0f);
            adam.Gaze(0f, home.Seat + V(0.3f, 1.2f, 2f), 0.3f);
            adam.Gaze(1.55f, home.Seat + V(-0.14f, 0.92f, 0.36f), 0.7f);
            adam.Smile(2.5f, 0f);
            adam.Smile(3.2f, 42f);
            adam.Shape("BrowInnerUp").Key(2.4f, 0f).Key(3.1f, 28f);

            Cam(s, 0f, V(0.55f, 1.12f, 1.5f), home.Seat + V(0f, 1.02f, 0.1f), 30f);
            Cam(s, 3.5f, V(0.48f, 1.11f, 1.4f), home.Seat + V(0f, 1.03f, 0.1f), 30f, Ease.Linear);
            s.Camera.Focus.Key(0f, home.Seat + V(0f, 1.22f, 0.22f));
            s.Camera.Aperture.Key(0f, 2.8f);
            s.Camera.Grade(-5f, 0f, 4f, 0.28f, 0.45f);
            s.Lamp("Face fill", V(0.75f, 1.45f, 1.2f), new Color(1f, 0.93f, 0.85f), 4.2f, 4f);
        }

        static void LapHands(FilmActor maya, Vector3 seat)
        {
            Rest(maya.Left, seat + V(-0.13f, 0.63f, 0.17f), V(0.12f, -0.2f, 1f), V(0f, -1f, -0.2f));
            Rest(maya.Right, seat + V(0.11f, 0.625f, 0.15f), V(-0.14f, -0.2f, 1f), V(0f, -1f, -0.2f));
        }

        /// <summary>The waterfront. Maya watches the sun go down, and turns to share it with the empty place beside her.</summary>
        static void S05a(FilmStage s)
        {
            var set = FilmSets.Waterfront(s);
            var maya = s.Actor("maya", set.Seat, 0f, 2f);
            LapHands(maya, set.Seat);
            var sun = V(0f, 1.3f, 0f) + set.SunDirection * 80f;
            var beside = V(0.52f, 1.1f, 0.02f);
            maya.Gaze(0f, sun, 0.6f);
            maya.Smile(0.6f, 6f);
            maya.Smile(1.5f, 46f);
            maya.Gaze(2.2f, beside, 0.92f, 1f, true);
            maya.Twist.Key(2.2f, 0f).Key(3.1f, -16f, Ease.Soft);
            maya.Smile(3.3f, 40f);
            maya.Smile(4.2f, 6f);
            maya.Gaze(3.8f, V(0.52f, 0.6f, -0.03f), 0.85f);

            Cam(s, 0f, V(0.8f, 1.5f, -3.3f), V(0.25f, 0.95f, 8f), 36f);
            Cam(s, 4.5f, V(0.66f, 1.47f, -2.78f), V(0.25f, 0.95f, 8f), 36f, Ease.Linear);
            s.Camera.Focus.Key(0f, set.Seat + V(0f, 1.1f, 0f));
            s.Camera.Aperture.Key(0f, 5.6f);
            s.Camera.Grade(4f, 14f, 6f, 0.28f, 0.7f);
        }

        /// <summary>Close on her: "You would've loved this."</summary>
        static void S05b(FilmStage s)
        {
            var set = FilmSets.Waterfront(s);
            var maya = s.Actor("maya", set.Seat, 0f, 2f);
            LapHands(maya, set.Seat);
            var beside = V(0.52f, 1.1f, 0.02f);
            maya.Twist.Key(0f, -16f);
            maya.Gaze(0f, beside, 0.92f);
            maya.Smile(0f, 10f);
            maya.Shape("BrowInnerUp").Key(0f, 12f).Key(1.2f, 34f).Key(2.9f, 22f);
            maya.Say(0.35f, Voice("maya-loved"), 0.9f);
            maya.Gaze(2.5f, V(0.52f, 0.62f, -0.03f), 0.8f, 1f, true);
            maya.Smile(2.3f, 8f);
            maya.Smile(3f, 24f);

            Cam(s, 0f, V(0.72f, 1.2f, 1.5f), V(0.08f, 1.12f, 0f), 27f);
            Cam(s, 3f, V(0.66f, 1.2f, 1.42f), V(0.08f, 1.12f, 0f), 27f, Ease.Linear);
            s.Camera.Focus.Key(0f, set.Seat + V(0.02f, 1.24f, -0.05f));
            s.Camera.Aperture.Key(0f, 2.2f);
            s.Camera.Grade(4f, 12f, 6f, 0.3f, 0.7f);
        }
    }
}
