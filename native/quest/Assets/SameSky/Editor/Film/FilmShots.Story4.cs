// "I Saved You a Seat", the last two beats: the seat is filled, and a new ritual.

using SameSky.UI;
using TMPro;
using UnityEngine;

namespace SameSky.Film
{
    public static partial class FilmShots
    {
        // ============================================================ 6  THE SEAT IS FILLED

        /// <summary>Another evening. Adam's phone lights on the low table: the hour they both have.</summary>
        static void S18(FilmStage s)
        {
            var home = FilmSets.Home(s, 0.9f);
            var adam = s.Actor("adam", home.Seat, 0f, 1f);
            Rest(adam.Right, home.Seat + V(0.15f, 0.665f, 0.24f), V(-0.12f, -0.3f, 1f), V(0f, -1f, -0.25f));
            Rest(adam.Left, home.Seat + V(-0.15f, 0.665f, 0.24f), V(0.12f, -0.3f, 1f), V(0f, -1f, -0.25f));
            FilmProps.Headset(s, V(0.14f, home.TableTop + 0.047f, 0.84f), Quaternion.Euler(0f, 158f, 0f));
            var eye = V(-0.06f, 0.6f, 1.3f);
            var at = V(-0.28f, home.TableTop + 0.074f, 0.95f);
            var normal = (eye + V(0f, 0.3f, 0f) - at).normalized;
            var phone = new FilmPhone(s, at, Quaternion.LookRotation(-normal, Vector3.up));
            // it leans on the books: its lower edge on the table
            at.y = home.TableTop + Mathf.Abs(phone.Root.up.y) * FilmPhone.Height * 0.5f + Mathf.Abs(phone.Root.forward.y) * FilmPhone.Depth * 0.5f;
            phone.Root.position = at;

            phone.Panel("Wallpaper", Vector2.zero, new Vector2(FilmPhone.ScreenW, FilmPhone.ScreenH), Theme.CardDark, 0.005f, null, Ui.OrderBackground);
            phone.Text("Time", "17:02", new Vector2(0f, 0.044f), 0.0125f, Theme.OnDark, 0.06f, TextAlignmentOptions.Center, FontStyles.Bold);
            phone.Text("Day", "Thursday", new Vector2(0f, 0.031f), 0.0042f, new Color(0.8f, 0.82f, 0.78f));
            var cardRoot = new GameObject("Card").transform;
            cardRoot.SetParent(phone.Screen, false);
            phone.Panel("Card back", Vector2.zero, new Vector2(0.058f, 0.04f), Theme.Card, 0.005f, null, Ui.OrderElement, cardRoot);
            phone.Text("App", "SameSky", new Vector2(-0.0135f, 0.0135f), 0.0036f, Theme.Green, 0.026f, TextAlignmentOptions.Left, FontStyles.Bold, cardRoot);
            phone.Text("Title", "You're both free", new Vector2(0f, 0.0062f), 0.0052f, Theme.Ink, 0.052f, TextAlignmentOptions.Left, FontStyles.Bold, cardRoot);
            phone.Text("Body", "Today 17:00–18:30 · 90 min you both have", new Vector2(0f, -0.0012f), 0.0033f, Theme.Muted, 0.052f, TextAlignmentOptions.Left, FontStyles.Normal, cardRoot);
            phone.Panel("Open", new Vector2(0f, -0.0118f), new Vector2(0.05f, 0.009f), Theme.Green, 0.0045f, null, Ui.OrderFill, cardRoot);
            phone.Text("Open text", "Sit with Maya", new Vector2(0f, -0.0118f), 0.0042f, Color.white, 0.05f, TextAlignmentOptions.Center, FontStyles.Bold, cardRoot);
            var glow = s.Lamp("Phone glow", at + normal * 0.12f, new Color(0.9f, 0.93f, 1f), 0f, 1f);
            s.Moves.Add(t =>
            {
                var lit = Mathf.SmoothStep(0f, 1f, (t - 0.7f) / 0.3f);
                phone.Lit(lit);
                phone.Screen.gameObject.SetActive(lit > 0.02f);
                glow.intensity = 0.07f * lit;
                var slide = Mathf.SmoothStep(0f, 1f, (t - 0.95f) / 0.4f);
                cardRoot.gameObject.SetActive(slide > 0.01f);
                cardRoot.localPosition = new Vector3(0f, Mathf.Lerp(0.02f, -0.012f, slide), 0f);
            });
            adam.Lean.Key(0f, -8f).Key(1.6f, -8f).Key(3.2f, 7f, Ease.Soft);
            adam.Gaze(0f, home.Seat + V(0.1f, 0.72f, 0.4f), 0.7f);
            adam.Gaze(1.35f, at, 0.85f, 1f, true);
            adam.Smile(1.9f, 0f);
            adam.Smile(2.9f, 50f);
            adam.Shape("BrowInnerUp").Key(1.5f, 0f).Key(2.2f, 30f).Key(3.4f, 12f);

            Cam(s, 0f, eye, at + V(0f, 0.004f, 0f), 26f);
            Cam(s, 4.5f, eye + V(-0.02f, 0.008f, -0.04f), at + V(0f, 0.004f, 0f), 26f, Ease.Soft);
            s.Camera.Focus.Key(0f, at);
            s.Camera.Aperture.Key(0f, 2.4f);
            s.Camera.Grade(12f, 4f, 4f, 0.32f, 0.6f);
            s.Lamp("Face fill", V(0.7f, 1.4f, 1.2f), new Color(1f, 0.84f, 0.66f), 2.6f, 4f);
        }

        static void Together(LoungeScene x, float mayaHead = 0.9f, float adamHead = 0.9f)
        {
            x.Maya.Gaze(0f, x.Adam.Eyes, mayaHead);
            x.Adam.Gaze(0f, x.Maya.Eyes, adamHead);
        }

        /// <summary>The opening picture again, and now nobody is missing. A wave: "You made it."</summary>
        static void S19(FilmStage s)
        {
            var x = Scene(s, 0.85f, true, true, "here now", false, true);
            var prints = Photos(s);
            foreach (var p in prints) Lit(p, 1f);
            Play(s, x.Screen, -4f, 0.55f);
            FilmActor adam = x.Adam, maya = x.Maya;
            adam.Gaze(0f, x.Room.AdamCupAt + Vector3.up * 0.06f, 0.65f);
            adam.Gaze(0.45f, maya.Eyes, 0.9f, 1f, true);
            adam.Smile(0.6f, 5f);
            adam.Smile(1.6f, 66f);
            adam.Lean.Key(0f, 7f).Key(1.6f, 2f, Ease.Soft);
            maya.Gaze(0f, adam.Eyes, 0.9f);
            maya.Smile(0f, 24f);
            maya.Smile(1.3f, 72f);
            maya.Both("BrowOuterUp", 0.9f, 0f);
            maya.Both("BrowOuterUp", 1.4f, 46f);
            maya.Both("BrowOuterUp", 2.5f, 12f);
            maya.Shape("BrowInnerUp").Key(0.9f, 0f).Key(1.4f, 30f).Key(2.5f, 8f);
            // her wave: the hand nearer the camera
            var rest = x.Room.Maya + V(0.1f, 0.78f, -0.07f);
            var up = x.Room.Maya + V(0.3f, 1.13f, -0.24f);
            maya.Left.Weight.Key(0.5f, 0f).Key(1.15f, 1f).Key(2.7f, 1f).Key(3.5f, 0f);
            maya.Left.Aim.Key(0.5f, 0f).Key(1.15f, 1f).Key(2.7f, 1f).Key(3.5f, 0f);
            maya.Left.Goal.Key(0.5f, rest).Key(1.2f, up, Ease.Overshoot).Key(2.7f, up + V(0f, -0.02f, 0f)).Key(3.5f, rest, Ease.Soft);
            maya.Left.Palm.Key(0f, Vector3.back);
            maya.Left.Fingers.Key(1.15f, V(0.1f, 1f, 0f)).Key(1.42f, V(-0.3f, 1f, 0f)).Key(1.68f, V(0.3f, 1f, 0f)).Key(1.94f, V(-0.3f, 1f, 0f)).Key(2.2f, V(0.25f, 1f, 0f)).Key(2.6f, V(0f, 1f, 0f));
            maya.Say(1.9f, Voice("maya-made"));

            Cam(s, 0f, V(2.2f, 1.1f, 0.66f), V(0f, 1.0f, 0.72f), 36f);
            Cam(s, 6f, V(2.02f, 1.09f, 0.7f), V(0f, 1.0f, 0.72f), 34f, Ease.Linear);
            s.Camera.Focus.Key(0f, V(0f, 1.1f, 0.72f));
            s.Camera.Aperture.Key(0f, 4f);
            s.Camera.Grade(10f, 6f, 4f, 0.32f, 0.65f);
        }

        /// <summary>Adam: "You saved me a seat."</summary>
        static void S20(FilmStage s)
        {
            var x = Scene(s, 0.85f, true, true, "here now", false, true);
            var prints = Photos(s);
            foreach (var p in prints) Lit(p, 1f);
            Play(s, x.Screen, -10f, 0.55f);
            Together(x);
            var adam = x.Adam;
            adam.Smile(0f, 60f);
            adam.Smile(0.8f, 52f);
            adam.Smile(3.3f, 80f);
            adam.Say(0.9f, Voice("adam-saved"));
            adam.Shape("BrowInnerUp").Key(0.6f, 6f).Key(1.4f, 16f).Key(3.4f, 8f);
            adam.Lean.Key(0f, 3f).Key(3.2f, 7f, Ease.Soft);
            adam.Nod.Key(3.1f, 0f).Key(3.4f, 5f).Key(3.8f, 0f);
            x.Maya.Smile(0f, 60f);
            FilmSets.Portrait(s, x.Room, V(-0.9f, 1.6f, 1.5f), V(0f, 1.25f, 0.3f));

            Cam(s, 0f, V(-0.66f, 1.3f, 1.44f), V(0.02f, 1.24f, 0.3f), 25f);
            Cam(s, 4.5f, V(-0.62f, 1.3f, 1.36f), V(0.02f, 1.24f, 0.3f), 25f, Ease.Linear);
            s.Camera.Focus.Key(0f, V(0f, 1.27f, 0.31f));
            s.Camera.Aperture.Key(0f, 2.2f);
            s.Camera.Grade(10f, 6f, 4f, 0.32f, 0.65f);
        }

        static void Watching(FilmStage s, LoungeScene x)
        {
            FilmActor adam = x.Adam, maya = x.Maya;
            adam.Twist.Key(0f, 20f);
            maya.Twist.Key(0f, -20f);
            adam.Gaze(0f, x.Room.ScreenAt, 0.85f);
            maya.Gaze(0f, x.Room.ScreenAt, 0.85f);
            adam.Smile(0f, 34f);
            maya.Smile(0f, 40f);
        }

        /// <summary>They watch her sunset together: the two of them from behind, the screen between them.</summary>
        static void S21a(FilmStage s)
        {
            var x = Scene(s, 0.88f, true, true, "here now", false, true);
            var prints = Photos(s);
            foreach (var p in prints) Lit(p, 1f);
            Play(s, x.Screen, -1f, 1.25f);
            Watching(s, x);
            FilmActor adam = x.Adam, maya = x.Maya;
            adam.Lean.Key(0f, 4f).Key(2.5f, 10f, Ease.Soft);
            // she points something out
            var point = x.Room.Maya + V(-0.42f, 1.04f, -0.26f);
            maya.Right.Weight.Key(0.9f, 0f).Key(1.6f, 1f).Key(2.8f, 1f).Key(3.6f, 0f);
            maya.Right.Aim.Key(0.9f, 0f).Key(1.6f, 1f).Key(2.8f, 1f).Key(3.6f, 0f);
            maya.Right.Goal.Key(0.9f, x.Room.Maya + V(-0.04f, 0.76f, -0.07f)).Key(1.6f, point, Ease.Soft).Key(2.8f, point + V(-0.02f, 0.01f, 0f)).Key(3.6f, x.Room.Maya + V(-0.04f, 0.76f, -0.07f), Ease.Soft);
            maya.Right.Fingers.Key(0f, V(-1f, 0.12f, -0.35f));
            maya.Right.Palm.Key(0f, V(0.1f, -1f, 0f));
            maya.Right.Grip.Key(0.9f, 0f).Key(1.6f, 0.7f).Key(2.8f, 0.7f).Key(3.6f, 0f);
            maya.Right.Index.Key(0.9f, 0f).Key(1.6f, -0.25f).Key(2.8f, -0.25f).Key(3.6f, 0f);

            Cam(s, 0f, V(2.12f, 1.34f, 0.72f), V(-1.1f, 1.06f, 0.72f), 38f);
            Cam(s, 4f, V(1.98f, 1.32f, 0.72f), V(-1.1f, 1.06f, 0.72f), 38f, Ease.Linear);
            s.Camera.Focus.Key(0f, V(-0.4f, 1.1f, 0.72f));
            s.Camera.Aperture.Key(0f, 4f);
            s.Camera.Grade(10f, 6f, 4f, 0.32f, 0.7f);
        }

        /// <summary>Their faces in its light; a glance at each other.</summary>
        static void S21b(FilmStage s)
        {
            var x = Scene(s, 0.88f, true, true, "here now", false, true);
            Play(s, x.Screen, -5f, 1.3f);
            Watching(s, x);
            FilmActor adam = x.Adam, maya = x.Maya;
            maya.Gaze(1.5f, adam.Eyes, 0.55f, 1f, true);
            maya.Smile(1.6f, 40f);
            maya.Smile(2.3f, 74f);
            adam.Gaze(2.1f, maya.Eyes, 0.55f, 1f, true);
            adam.Smile(2.2f, 34f);
            adam.Smile(2.9f, 68f);
            maya.Gaze(3.1f, x.Room.ScreenAt, 0.85f);
            adam.Gaze(3.4f, x.Room.ScreenAt, 0.85f);
            adam.Lean.Key(0f, 9f);
            // this is the screen's own view of them: the screen itself is not in the picture
            foreach (var r in x.Screen.Root.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

            Cam(s, 0f, V(-2.0f, 1.22f, 0.72f), V(0.1f, 1.12f, 0.72f), 34f);
            Cam(s, 4f, V(-1.86f, 1.22f, 0.72f), V(0.1f, 1.12f, 0.72f), 34f, Ease.Linear);
            s.Camera.Focus.Key(0f, V(0f, 1.2f, 0.72f));
            s.Camera.Aperture.Key(0f, 3.2f);
            s.Camera.Grade(10f, 6f, 4f, 0.32f, 0.7f);
        }

        // ============================================================ 7  A NEW RITUAL

        /// <summary>The evening is kept: a small frame takes its place on the shelf.</summary>
        static void S22(FilmStage s)
        {
            var x = Scene(s, 0.9f, false, false, null, false, false);
            var home = V(-2.42f, 1.034f, 0.86f);
            var frame = FilmSets.PhotoFrame(s, home, -90f, Picture(Frame("m_sunset", 120)), 0.13f, 16f / 9f);
            var print = frame.Find("Photo").GetComponent<MeshRenderer>().sharedMaterial;
            FilmSets.TentCard(s, V(-2.37f, 1.034f, 0.68f), -90f, "Kept", "your first sunset", 0.09f, 0.046f);
            x.Room.Fill.intensity *= 0.22f;
            s.Camera.Exposure.Key(0f, -0.5f);
            var glow = s.Lamp("Keepsake glow", V(-2.2f, 1.2f, 0.9f), new Color(1f, 0.82f, 0.56f), 0f, 0.9f);
            var stand = Quaternion.Euler(-9f, -90f, 0f);
            s.Moves.Add(t =>
            {
                var k = Mathf.Exp(-t * 5f);
                frame.position = home + Vector3.up * (0.022f * k * Mathf.Abs(Mathf.Cos(t * 11f)));
                frame.rotation = stand * Quaternion.Euler(0f, 0f, 5f * k * Mathf.Sin(t * 13f));
                Lit(print, Mathf.SmoothStep(0f, 1f, (t - 0.25f) / 0.9f));
                glow.intensity = 0.3f * Mathf.SmoothStep(0f, 1f, (t - 0.2f) / 0.6f) * (0.7f + 0.3f * Mathf.Exp(-Mathf.Max(0f, t - 0.8f) * 1.6f));
            });
            Cam(s, 0f, V(-1.42f, 1.22f, 1.02f), V(-2.42f, 1.1f, 0.8f), 30f);
            Cam(s, 3.5f, V(-1.6f, 1.2f, 0.98f), V(-2.42f, 1.1f, 0.8f), 30f, Ease.Soft);
            s.Camera.Focus.Key(0f, home + Vector3.up * 0.05f);
            s.Camera.Aperture.Key(0f, 2.4f);
            s.Camera.Grade(10f, 6f, 4f, 0.32f, 0.65f);
        }

        static LoungeScene Ritual(FilmStage s)
        {
            var x = Scene(s, 0.92f, true, true, "here now", false, true);
            var prints = Photos(s);
            foreach (var p in prints) Lit(p, 1f);
            Play(s, x.Screen, -20f, 0.4f);
            Together(x, 0.9f, 0.9f);
            return x;
        }

        /// <summary>"Same time tomorrow?" "Same seat."</summary>
        static void S23(FilmStage s)
        {
            var x = Ritual(s);
            FilmActor adam = x.Adam, maya = x.Maya;
            maya.Smile(0f, 46f);
            maya.Say(0.5f, Voice("maya-tomorrow"));
            maya.Tilt.Key(0.4f, 0f).Key(1.2f, 6f, Ease.Soft).Key(3.2f, 2f, Ease.Soft);
            maya.Shape("BrowInnerUp").Key(1.6f, 6f).Key(2.4f, 34f).Key(3.4f, 10f);
            adam.Smile(0f, 44f);
            adam.Smile(3.0f, 68f);
            adam.Say(3.4f, Voice("adam-seat"));
            adam.Nod.Key(4.5f, 0f).Key(4.8f, 5f).Key(5.2f, 0f);
            // and she laughs
            maya.Smile(4.9f, 50f);
            maya.Smile(5.5f, 84f);
            maya.Shape("JawOpen").Key(5.1f, 0f).Key(5.5f, 14f).Key(6.1f, 6f);
            maya.Nod.Key(5.1f, 0f).Key(5.6f, -5f, Ease.Soft).Key(6.4f, -1f);
            maya.Both("EyeSquint", 5.1f, 14f);
            maya.Both("EyeSquint", 5.6f, 44f);

            Cam(s, 0f, V(1.95f, 1.1f, 0.72f), V(0f, 1.02f, 0.72f), 36f);
            Cam(s, 6.5f, V(2.2f, 1.2f, 0.68f), V(0f, 1.02f, 0.72f), 38f, Ease.Linear);
            s.Camera.Focus.Key(0f, V(0f, 1.1f, 0.72f));
            s.Camera.Aperture.Key(0f, 4f);
            s.Camera.Grade(10f, 6f, 4f, 0.32f, 0.65f);
        }

        /// <summary>The room, lived in and warm, falling out of focus under the closing words (added in the edit).</summary>
        static void S24(FilmStage s)
        {
            var x = Ritual(s);
            x.Maya.Smile(0f, 60f);
            x.Adam.Smile(0f, 58f);
            Cam(s, 0f, V(2.2f, 1.2f, 0.68f), V(0f, 1.02f, 0.72f), 38f);
            Cam(s, 5f, V(2.22f, 1.28f, 0.6f), V(0f, 1.04f, 0.72f), 40f, Ease.Linear);
            s.Camera.Focus.Key(0f, V(0f, 1.1f, 0.72f)).Key(1.8f, V(1.86f, 1.2f, 0.68f), Ease.Soft);
            s.Camera.Aperture.Key(0f, 4f).Key(1.8f, 1.2f);
            s.Camera.Exposure.Key(0f, 0f).Key(2.2f, -1.25f, Ease.Soft);
            s.Camera.Grade(10f, 6f, 4f, 0.36f, 0.65f);
        }
    }
}
