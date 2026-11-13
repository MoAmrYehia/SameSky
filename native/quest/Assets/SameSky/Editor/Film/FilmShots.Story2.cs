// "I Saved You a Seat", continued: saving the moment on the phone, and the cut to the cup (beat 3).

using System.IO;
using SameSky.UI;
using TMPro;
using UnityEngine;

namespace SameSky.Film
{
    public static partial class FilmShots
    {
        static string Frame(string clip, int index) => Path.Combine(FilmSets.Media(clip), $"{index:0000}.jpg");

        /// <summary>A picture file as a texture, optionally only a middle strip of it (a portrait crop of a wide clip).</summary>
        static Texture2D Picture(string file, float cropLeft = 0f, float cropWidth = 1f)
        {
            var full = new Texture2D(2, 2, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            if (File.Exists(file)) full.LoadImage(File.ReadAllBytes(file), false);
            if (cropWidth >= 0.999f) return full;
            int x0 = Mathf.RoundToInt(full.width * cropLeft), w = Mathf.Max(1, Mathf.RoundToInt(full.width * cropWidth));
            var part = new Texture2D(w, full.height, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            part.SetPixels(full.GetPixels(Mathf.Clamp(x0, 0, full.width - w), 0, w, full.height));
            part.Apply(false, false);
            Object.DestroyImmediate(full);
            return part;
        }

        /// <summary>A point on a phone that is held in a known pose: metres from the middle of its screen.</summary>
        static Vector3 OnPhone(Pose phone, float x, float y, float above = 0f) =>
            phone.position + phone.rotation * new Vector3(x, y, -FilmPhone.Depth * 0.5f - 0.0006f - above);

        /// <summary>A fingertip tap: over the spot, down on it at <paramref name="t"/>, off again.</summary>
        static void Tap(FilmArm arm, float t, Vector3 spot, Vector3 normal, float hover = 0.022f)
        {
            arm.Touch.Key(t - 0.34f, spot + normal * hover, Ease.Soft);
            arm.Touch.Key(t, spot + normal * 0.001f, Ease.In);
            arm.Touch.Key(t + 0.1f, spot + normal * 0.001f);
            arm.Touch.Key(t + 0.3f, spot + normal * hover, Ease.Out);
        }

        // ============================================================ 3  SAVE THE MOMENT

        /// <summary>She lifts her phone and films the sunset.</summary>
        static void S06(FilmStage s)
        {
            var set = FilmSets.Waterfront(s);
            var maya = s.Actor("maya", set.Seat, 0f, 2f);
            maya.Lean.Key(0f, 2f).Key(1.6f, 9f, Ease.Soft);
            var phone = new FilmPhone(s, Vector3.zero, Quaternion.identity);
            Vector3 lowF = V(0f, 0.3f, 1f), lowP = V(0f, 1f, -0.3f), upF = V(0f, 1f, 0.06f), upP = V(0f, 0.06f, -1f);
            var filming = set.Seat + V(0.06f, 1.065f, 0.265f);
            On(maya.Right, -0.28f);
            Hand(maya.Right, 0f, set.Seat + V(0.1f, 0.68f, 0.2f), lowF, lowP);
            Hand(maya.Right, 0.4f, set.Seat + V(0.1f, 0.68f, 0.2f), lowF, lowP);
            Hand(maya.Right, 1.6f, filming, upF, upP, Ease.Soft);
            maya.Right.Thumb.Key(0f, 0.3f);
            s.Hold(phone.Root, maya.Right, PhoneInHand, -1f, 99f, default, default);
            var held = PhoneInHand.Thing(filming, upF.normalized, upP.normalized);

            // what the phone shows: the view, a record button, and once she taps it a running clock
            var view = phone.Picture("View", Vector2.zero, new Vector2(FilmPhone.ScreenW, FilmPhone.ScreenH), null, 0.005f);
            var ring = phone.Panel("Record ring", new Vector2(0f, -0.052f), new Vector2(0.016f, 0.016f), new Color(0, 0, 0, 0f), 0.008f, Color.white, Ui.OrderFill);
            Ui.SetPanel(ring, new Vector2(0.016f, 0.016f), new Color(0, 0, 0, 0.15f), 0.008f, Color.white, 0.0012f);
            var dot = phone.Panel("Record dot", new Vector2(0f, -0.052f), new Vector2(0.0115f, 0.0115f), new Color(0.9f, 0.16f, 0.14f), 0.00575f, null, Ui.OrderFill);
            var pill = phone.Panel("Clock", new Vector2(0f, 0.06f), new Vector2(0.024f, 0.0075f), new Color(0.85f, 0.14f, 0.12f, 0.92f), 0.00375f, null, Ui.OrderFill);
            var clock = phone.Text("Clock text", "0:00", new Vector2(0f, 0.06f), 0.0048f, Color.white, 0.024f, TextAlignmentOptions.Center, FontStyles.Bold);
            Texture2D shown = null;
            var last = -1;
            s.Moves.Add(t =>
            {
                phone.Lit(1f);
                var frame = Mathf.Clamp(Mathf.FloorToInt(t * 24f), 0, 191);
                if (frame != last)
                {
                    if (shown != null) Object.DestroyImmediate(shown);
                    shown = Picture(Frame("m_sunset", frame), 0.36f, 0.26f);
                    Ui.SetImage(view, shown);
                    last = frame;
                }
                var recording = t >= 2.3f;
                pill.gameObject.SetActive(recording);
                clock.gameObject.SetActive(recording);
                if (recording)
                {
                    var text = "0:0" + Mathf.Clamp(Mathf.FloorToInt(t - 2.3f), 0, 9);
                    if (clock.text != text) { clock.text = text; clock.ForceMeshUpdate(true, true); }
                }
                var press = Mathf.Clamp01(1f - Mathf.Abs(t - 2.32f) / 0.14f);
                var size = Mathf.Lerp(0.0115f, recording ? 0.0068f : 0.0115f, Mathf.SmoothStep(0f, 1f, (t - 2.3f) / 0.2f)) * (1f - 0.12f * press);
                dot.transform.localScale = new Vector3(size, size, 1f);
                Ui.SetPanel(dot, new Vector2(size, size), new Color(0.9f, 0.16f, 0.14f), recording ? 0.0016f : size * 0.5f, new Color(0, 0, 0, 0), 0f);
            });
            // the other hand comes up and taps record
            Rest(maya.Left, set.Seat + V(-0.13f, 0.63f, 0.17f), V(0.12f, -0.2f, 1f), V(0f, -1f, -0.2f));
            maya.Left.Fingers.Key(1.5f, V(0.12f, -0.2f, 1f)).Key(2.0f, V(0.32f, 0.1f, 1f)).Key(2.9f, V(0.32f, 0.1f, 1f)).Key(3.6f, V(0.12f, -0.2f, 1f));
            maya.Left.Palm.Key(1.5f, V(0f, -1f, -0.2f)).Key(2.0f, V(0.25f, -1f, 0f)).Key(2.9f, V(0.25f, -1f, 0f)).Key(3.6f, V(0f, -1f, -0.2f));
            maya.Left.Goal.Key(1.5f, set.Seat + V(-0.13f, 0.63f, 0.17f)).Key(2.0f, held.position + V(-0.1f, -0.12f, -0.16f), Ease.Soft).Key(2.9f, held.position + V(-0.1f, -0.12f, -0.16f)).Key(3.7f, set.Seat + V(-0.13f, 0.63f, 0.17f), Ease.Soft);
            maya.Left.Grip.Key(1.5f, 0.16f).Key(2.0f, 0.75f).Key(2.9f, 0.75f).Key(3.6f, 0.16f);
            maya.Left.Index.Key(1.5f, 0.16f).Key(2.0f, -0.2f).Key(2.9f, -0.2f).Key(3.6f, 0.16f);
            Tap(maya.Left, 2.3f, OnPhone(held, 0f, -0.052f), held.rotation * Vector3.back);
            maya.Left.TouchWeight.Key(1.8f, 0f).Key(2.05f, 1f).Key(2.65f, 1f).Key(2.95f, 0f);
            maya.Gaze(0f, set.Seat + V(0.1f, 0.75f, 0.25f), 0.6f);
            maya.Gaze(0.5f, V(0f, 1.3f, 0f) + set.SunDirection * 80f, 0.5f);
            maya.Gaze(1.5f, held.position, 0.5f);
            maya.Smile(1.6f, 10f);
            maya.Smile(3f, 40f);

            Cam(s, 0f, V(-0.78f, 1.3f, -0.34f), V(-0.12f, 1.1f, 0.5f), 30f);
            Cam(s, 4f, V(-0.72f, 1.28f, -0.26f), V(-0.14f, 1.15f, 0.5f), 30f, Ease.Soft);
            s.Camera.Focus.Key(0f, held.position).Key(1.2f, held.position);
            s.Camera.Aperture.Key(0f, 3.2f);
            s.Camera.Grade(4f, 12f, 6f, 0.3f, 0.7f);
        }

        sealed class ShareScreen
        {
            public MeshRenderer[] Tiles = new MeshRenderer[4], Badges = new MeshRenderer[4];
            public TextMeshPro[] Numbers = new TextMeshPro[4];
            public MeshRenderer Share, Adam;
            public Transform Root;
            public TextMeshPro ShareText, Count;
            public Vector2[] TileAt = { new(-0.0152f, 0.026f), new(0.0152f, 0.026f), new(-0.0152f, -0.0055f), new(0.0152f, -0.0055f) };
            public Vector2 ShareAt = new(0f, -0.054f), AdamAt = new(-0.012f, -0.0345f);
        }

        /// <summary>The companion's share screen as the film draws it: today's moments as tiles, who they go to, Share.</summary>
        static ShareScreen BuildShare(FilmPhone phone)
        {
            var ui = new ShareScreen();
            ui.Root = new GameObject("Share screen").transform;
            ui.Root.SetParent(phone.Screen, false);
            var at = ui.Root;
            phone.Panel("Background", Vector2.zero, new Vector2(FilmPhone.ScreenW, FilmPhone.ScreenH), Theme.Card, 0.005f, null, Ui.OrderBackground, at);
            phone.Text("Title", "Share today", new Vector2(0f, 0.059f), 0.0062f, Theme.Ink, 0.058f, TextAlignmentOptions.Center, FontStyles.Bold, at);
            ui.Count = phone.Text("Count", "Choose moments for Adam", new Vector2(0f, 0.0505f), 0.0036f, Theme.Muted, 0.058f, TextAlignmentOptions.Center, FontStyles.Normal, at);
            var pictures = new[] { Picture(Frame("m_sunset", 96), 0.22f, 0.5625f), Picture(Frame("m_bench", 0), 0.22f, 0.5625f), Picture(Frame("m_sky", 0), 0.22f, 0.5625f), null };
            for (var i = 0; i < 4; i++)
            {
                var size = new Vector2(0.0275f, 0.0275f);
                ui.Tiles[i] = i < 3 ? phone.Picture("Tile " + i, ui.TileAt[i], size, pictures[i], 0.0035f, at)
                    : phone.Panel("Tile voice", ui.TileAt[i], size, Theme.GreenSoft, 0.0035f, null, Ui.OrderElement, at);
                ui.Badges[i] = phone.Panel("Badge " + i, ui.TileAt[i] + new Vector2(0.0098f, 0.0098f), new Vector2(0.0066f, 0.0066f), Theme.Green, 0.0033f, Color.white, Ui.OrderFill, at);
                ui.Numbers[i] = phone.Text("Badge text " + i, (i + 1).ToString(), ui.TileAt[i] + new Vector2(0.0098f, 0.0098f), 0.0042f, Color.white, 0.007f, TextAlignmentOptions.Center, FontStyles.Bold, at);
                ui.Badges[i].gameObject.SetActive(false);
                ui.Numbers[i].gameObject.SetActive(false);
            }
            phone.Panel("Clip length", ui.TileAt[0] + new Vector2(-0.0068f, -0.0098f), new Vector2(0.0105f, 0.0048f), new Color(0, 0, 0, 0.6f), 0.0024f, null, Ui.OrderFill, at);
            phone.Text("Clip length text", "0:08", ui.TileAt[0] + new Vector2(-0.0068f, -0.0098f), 0.0032f, Color.white, 0.0105f, TextAlignmentOptions.Center, FontStyles.Normal, at);
            phone.Panel("Mic", ui.TileAt[3] + new Vector2(0f, 0.003f), new Vector2(0.008f, 0.008f), Theme.Green, 0.004f, null, Ui.OrderFill, at);
            phone.Text("Voice label", "Voice message", ui.TileAt[3] + new Vector2(0f, -0.0075f), 0.003f, Theme.Ink, 0.027f, TextAlignmentOptions.Center, FontStyles.Normal, at);
            phone.Text("To", "To", new Vector2(-0.0255f, -0.0345f), 0.0038f, Theme.Muted, 0.01f, TextAlignmentOptions.Center, FontStyles.Normal, at);
            ui.Adam = phone.Panel("Adam", ui.AdamAt, new Vector2(0.021f, 0.0082f), Theme.Green, 0.0041f, null, Ui.OrderElement, at);
            phone.Text("Adam text", "Adam", ui.AdamAt, 0.004f, Color.white, 0.021f, TextAlignmentOptions.Center, FontStyles.Bold, at);
            ui.Share = phone.Panel("Share", ui.ShareAt, new Vector2(0.054f, 0.0125f), Theme.Green, 0.00625f, null, Ui.OrderElement, at);
            ui.ShareText = phone.Text("Share text", "Share", ui.ShareAt, 0.0052f, Color.white, 0.054f, TextAlignmentOptions.Center, FontStyles.Bold, at);
            return ui;
        }

        static void Select(ShareScreen ui, int tile, bool on)
        {
            ui.Badges[tile].gameObject.SetActive(on);
            ui.Numbers[tile].gameObject.SetActive(on);
        }

        /// <summary>Maya on the bench with the phone in her left hand, tilted to her face: the staging of the two
        /// phone close-ups. Returns the phone's pose in the world.</summary>
        static Pose PhoneInLap(FilmStage s, WaterfrontSet set, FilmActor maya, FilmPhone phone)
        {
            Vector3 f = V(0.06f, 0.66f, 0.75f), p = V(0.1f, 0.74f, -0.66f);
            var wrist = set.Seat + V(-0.07f, 0.9f, 0.2f);
            Rest(maya.Left, wrist, f, p, -0.28f);
            maya.Left.Thumb.Key(0f, 0.3f);
            s.Hold(phone.Root, maya.Left, PhoneInHand, -1f, 99f, default, default);
            maya.Lean.Key(0f, 5f);
            return PhoneInHand.Thing(wrist, f.normalized, p.normalized);
        }

        /// <summary>Close on the phone: she picks the sunset clip and a photo. Her camera rests on her lap beside it.</summary>
        static void S07a(FilmStage s)
        {
            var set = FilmSets.Waterfront(s);
            var maya = s.Actor("maya", set.Seat, 0f, 2f);
            var phone = new FilmPhone(s, Vector3.zero, Quaternion.identity);
            var held = PhoneInLap(s, set, maya, phone);
            set.Camera.SetPositionAndRotation(set.Seat + V(-0.17f, 0.635f, 0.37f), Quaternion.Euler(-10f, 150f, 0f));
            var ui = BuildShare(phone);
            var normal = held.rotation * Vector3.back;
            Vector3 f = V(-0.34f, -0.5f, 0.8f), p = V(-0.1f, -0.84f, -0.54f);
            On(maya.Right, 0.72f);
            Hand(maya.Right, 0f, set.Seat + V(0.2f, 0.78f, 0.12f), f, p);
            Hand(maya.Right, 0.5f, held.position + V(0.11f, 0.08f, -0.12f), f, p, Ease.Soft);
            maya.Right.Index.Key(0f, -0.15f);
            maya.Right.TouchWeight.Key(0.2f, 0f).Key(0.55f, 1f).Key(2.6f, 1f).Key(3f, 0f);
            Vector2 a = ui.TileAt[0], b = ui.TileAt[1];
            Tap(maya.Right, 1.0f, OnPhone(held, a.x, a.y), normal);
            Tap(maya.Right, 2.1f, OnPhone(held, b.x, b.y), normal);
            Hand(maya.Right, 2.6f, held.position + V(0.11f, 0.08f, -0.12f), f, p);
            Hand(maya.Right, 3.3f, held.position + V(0.15f, 0.04f, -0.14f), f, p, Ease.Soft);
            s.Moves.Add(t =>
            {
                phone.Lit(1f);
                Select(ui, 0, t >= 1.02f);
                Select(ui, 1, t >= 2.12f);
                var text = t >= 2.12f ? "2 moments chosen" : t >= 1.02f ? "1 moment chosen" : "Choose moments for Adam";
                if (ui.Count.text != text) { ui.Count.text = text; ui.Count.ForceMeshUpdate(true, true); }
                for (var i = 0; i < 2; i++)
                {
                    var press = Mathf.Clamp01(1f - Mathf.Abs(t - (i == 0 ? 1.03f : 2.13f)) / 0.12f);
                    ui.Tiles[i].transform.localScale = new Vector3(0.0275f, 0.0275f, 1f) * (1f - 0.07f * press);
                }
            });
            maya.Gaze(0f, held.position, 0.75f);

            Cam(s, 0f, set.Seat + V(0.13f, 1.3f, -0.1f), held.position + V(-0.03f, -0.012f, 0f), 23f);
            Cam(s, 3.5f, set.Seat + V(0.11f, 1.28f, -0.08f), held.position + V(-0.02f, -0.005f, 0f), 23f, Ease.Linear);
            s.Camera.Focus.Key(0f, held.position);
            s.Camera.Aperture.Key(0f, 4f);
            s.Camera.Grade(4f, 10f, 4f, 0.3f, 0.6f);
        }

        /// <summary>She records a voice message: the phone near her mouth, a waveform moving as she speaks.</summary>
        static void S08(FilmStage s)
        {
            var set = FilmSets.Waterfront(s);
            var maya = s.Actor("maya", set.Seat, 0f, 2f);
            var phone = new FilmPhone(s, Vector3.zero, Quaternion.identity);
            Vector3 f = V(0.1f, 0.86f, 0.5f), p = V(0.16f, 0.48f, -0.86f);
            var wrist = set.Seat + V(-0.06f, 1.0f, 0.17f);
            Rest(maya.Left, wrist, f, p, -0.28f);
            maya.Left.Thumb.Key(0f, 0.3f);
            s.Hold(phone.Root, maya.Left, PhoneInHand, -1f, 99f, default, default);
            Rest(maya.Right, set.Seat + V(0.11f, 0.625f, 0.15f), V(-0.14f, -0.2f, 1f), V(0f, -1f, -0.2f));
            var held = PhoneInHand.Thing(wrist, f.normalized, p.normalized);

            phone.Panel("Background", Vector2.zero, new Vector2(FilmPhone.ScreenW, FilmPhone.ScreenH), Theme.Card, 0.005f, null, Ui.OrderBackground);
            phone.Text("Title", "Voice message", new Vector2(0f, 0.058f), 0.0058f, Theme.Ink, 0.058f, TextAlignmentOptions.Center, FontStyles.Bold);
            phone.Text("For", "for Adam", new Vector2(0f, 0.0495f), 0.0038f, Theme.Muted);
            var halo = phone.Panel("Halo", new Vector2(0f, -0.036f), new Vector2(0.03f, 0.03f), new Color(0.271f, 0.4f, 0.302f, 0.25f), 0.015f, null, Ui.OrderElement);
            phone.Panel("Mic", new Vector2(0f, -0.036f), new Vector2(0.02f, 0.02f), Theme.Green, 0.01f, null, Ui.OrderFill);
            phone.Panel("Mic dot", new Vector2(0f, -0.036f), new Vector2(0.0066f, 0.0066f), Color.white, 0.0033f, null, Ui.OrderText);
            var clock = phone.Text("Clock", "0:00", new Vector2(0f, 0.03f), 0.0072f, Theme.Ink, 0.05f, TextAlignmentOptions.Center, FontStyles.Bold);
            phone.Text("Hint", "Recording", new Vector2(0f, -0.058f), 0.0036f, Theme.Muted);
            var bars = new MeshRenderer[15];
            for (var i = 0; i < bars.Length; i++)
                bars[i] = phone.Panel("Bar " + i, new Vector2((i - 7) * 0.0034f, 0.006f), new Vector2(0.0019f, 0.004f), Theme.Green, 0.00095f, null, Ui.OrderFill);
            s.Moves.Add(t =>
            {
                phone.Lit(1f);
                var speaking = t > 0.5f && t < 3.4f;
                for (var i = 0; i < bars.Length; i++)
                {
                    var loud = speaking ? 0.25f + 0.75f * Mathf.PerlinNoise(t * 7f + i * 0.9f, i * 3.7f) * (0.6f + 0.4f * Mathf.Sin(t * 5.3f + i)) : 0.12f;
                    var h = Mathf.Max(0.0022f, 0.021f * Mathf.Abs(loud));
                    bars[i].transform.localScale = new Vector3(0.0019f, h, 1f);
                    Ui.SetPanel(bars[i], new Vector2(0.0019f, h), Theme.Green, 0.00095f, new Color(0, 0, 0, 0), 0f);
                }
                var pulse = 0.03f + 0.004f * Mathf.Sin(t * 6f);
                halo.transform.localScale = new Vector3(pulse, pulse, 1f);
                Ui.SetPanel(halo, new Vector2(pulse, pulse), new Color(0.271f, 0.4f, 0.302f, 0.25f), pulse * 0.5f, new Color(0, 0, 0, 0), 0f);
                var text = "0:0" + Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, t - 0.4f)), 0, 9);
                if (clock.text != text) { clock.text = text; clock.ForceMeshUpdate(true, true); }
            });
            maya.Say(0.45f, Voice("maya-note"), 0.85f);
            var sun = V(0f, 1.3f, 0f) + set.SunDirection * 80f;
            maya.Gaze(0f, sun, 0.55f);
            maya.Smile(0f, 22f);
            maya.Smile(1.6f, 36f);
            maya.Gaze(2.5f, held.position, 0.5f);
            maya.Smile(3.2f, 44f);
            maya.Shape("BrowInnerUp").Key(0.5f, 6f).Key(1.4f, 22f).Key(3.3f, 10f);

            Cam(s, 0f, set.Seat + V(0.36f, 1.23f, -0.22f), set.Seat + V(-0.02f, 1.17f, 0.14f), 31f);
            Cam(s, 3.5f, set.Seat + V(0.33f, 1.23f, -0.19f), set.Seat + V(-0.02f, 1.18f, 0.14f), 31f, Ease.Linear);
            s.Camera.Focus.Key(0f, held.position + V(0.02f, 0.04f, -0.05f));
            s.Camera.Aperture.Key(0f, 3.2f);
            s.Camera.Grade(4f, 12f, 6f, 0.3f, 0.7f);
        }

        /// <summary>Adam is chosen; she shares. The screen settles on a ring, which the edit matches to the rim of a cup.</summary>
        static void S07b(FilmStage s)
        {
            var set = FilmSets.Waterfront(s);
            var maya = s.Actor("maya", set.Seat, 0f, 2f);
            var phone = new FilmPhone(s, Vector3.zero, Quaternion.identity);
            var held = PhoneInLap(s, set, maya, phone);
            var ui = BuildShare(phone);
            Select(ui, 0, true);
            Select(ui, 1, true);
            Select(ui, 3, true);
            ui.Numbers[3].text = "3";
            ui.Numbers[3].ForceMeshUpdate(true, true);
            ui.Count.text = "2 moments and a voice message";
            ui.Count.ForceMeshUpdate(true, true);
            var normal = held.rotation * Vector3.back;
            Vector3 f = V(-0.34f, -0.5f, 0.8f), p = V(-0.1f, -0.84f, -0.54f);
            On(maya.Right, 0.72f);
            Hand(maya.Right, 0f, held.position + V(0.11f, 0.08f, -0.12f), f, p);
            maya.Right.Index.Key(0f, -0.15f);
            maya.Right.TouchWeight.Key(0f, 1f).Key(1.7f, 1f).Key(2.1f, 0f);
            maya.Right.Touch.Key(0f, OnPhone(held, ui.AdamAt.x + 0.02f, ui.AdamAt.y, 0.02f));
            Tap(maya.Right, 1.3f, OnPhone(held, ui.ShareAt.x + 0.006f, ui.ShareAt.y), normal);
            Hand(maya.Right, 1.6f, held.position + V(0.11f, 0.08f, -0.12f), f, p);
            Hand(maya.Right, 2.4f, set.Seat + V(0.2f, 0.72f, 0.1f), V(-0.14f, -0.2f, 1f), V(0f, -1f, -0.2f), Ease.Soft);
            maya.Right.Grip.Key(1.6f, 0.72f).Key(2.4f, 0.16f);
            maya.Right.Index.Key(1.6f, -0.15f).Key(2.4f, 0.16f);
            // once shared: the screen turns to a dark card with a pale ring and one line under it
            var dark = phone.Panel("Sent", Vector2.zero, new Vector2(FilmPhone.ScreenW, FilmPhone.ScreenH), Theme.CardDark, 0.005f, null, Ui.OrderText + 1);
            var ringSize = 0.04f;
            var sentRing = phone.Panel("Sent ring", new Vector2(0f, 0.012f), new Vector2(ringSize, ringSize), new Color(0, 0, 0, 0), ringSize * 0.5f, null, Ui.OrderText + 2);
            var sentText = phone.Text("Sent text", "Shared with Adam", new Vector2(0f, -0.024f), 0.0052f, Theme.OnDark, 0.06f, TextAlignmentOptions.Center, FontStyles.Bold);
            foreach (var r in sentText.GetComponentsInChildren<Renderer>(true)) r.sortingOrder = Ui.OrderText + 3;
            var sentSub = phone.Text("Sent sub", "waiting in the lounge", new Vector2(0f, -0.0325f), 0.0036f, new Color(0.8f, 0.82f, 0.78f), 0.06f);
            foreach (var r in sentSub.GetComponentsInChildren<Renderer>(true)) r.sortingOrder = Ui.OrderText + 3;
            s.Moves.Add(t =>
            {
                phone.Lit(1f);
                var press = Mathf.Clamp01(1f - Mathf.Abs(t - 1.33f) / 0.12f);
                ui.Share.transform.localScale = new Vector3(0.054f, 0.0125f, 1f) * (1f - 0.06f * press);
                var sent = Mathf.SmoothStep(0f, 1f, (t - 1.5f) / 0.45f);
                dark.gameObject.SetActive(sent > 0.001f);
                ui.Root.gameObject.SetActive(sent < 0.995f);
                sentRing.gameObject.SetActive(sent > 0.001f);
                sentText.gameObject.SetActive(sent > 0.6f);
                sentSub.gameObject.SetActive(sent > 0.6f);
                var c = Theme.CardDark;
                c.a = sent;
                Ui.SetPanel(dark, new Vector2(FilmPhone.ScreenW, FilmPhone.ScreenH), c, 0.005f, new Color(0, 0, 0, 0), 0f);
                var grow = Mathf.Lerp(0.78f, 1f, Mathf.SmoothStep(0f, 1f, (t - 1.6f) / 0.6f)) + 0.02f * Mathf.Sin(Mathf.Max(0f, t - 2.2f) * 3f);
                var d = ringSize * grow;
                sentRing.transform.localScale = new Vector3(d, d, 1f);
                Ui.SetPanel(sentRing, new Vector2(d, d), new Color(0.2f, 0.16f, 0.12f, sent), d * 0.5f, new Color(0.95f, 0.93f, 0.89f, sent), 0.0022f);
            });
            maya.Gaze(0f, held.position, 0.75f);
            maya.Smile(1.6f, 12f);
            maya.Smile(2.6f, 46f);

            // the camera ends square on the ring, close enough that it fills the frame the way the cup's rim will
            var ringAt = OnPhone(held, 0f, 0.012f);
            Cam(s, 0f, set.Seat + V(0.13f, 1.3f, -0.1f), held.position + V(-0.02f, -0.005f, 0f), 23f);
            Cam(s, 1.9f, set.Seat + V(0.12f, 1.29f, -0.09f), held.position + V(-0.02f, -0.005f, 0f), 23f);
            Cam(s, 4f, ringAt + normal * 0.178f + held.rotation * V(0f, -0.004f, 0f), ringAt, 26f, Ease.Soft);
            s.Camera.Focus.Key(0f, held.position);
            s.Camera.Aperture.Key(0f, 4f);
            s.Camera.Life = 0.5f;
            s.Camera.Grade(4f, 10f, 4f, 0.3f, 0.6f);
        }
    }
}
