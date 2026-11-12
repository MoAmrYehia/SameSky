// "I Saved You a Seat", continued: the lounge (beats 3's last shot to 7).

using SameSky.UI;
using TMPro;
using UnityEngine;

namespace SameSky.Film
{
    public static partial class FilmShots
    {
        sealed class LoungeScene
        {
            public LoungeSet Room;
            public FilmActor Adam, Maya;
            public Transform Cup, CupCard, PlaceCard;
            public FilmScreen Screen;
            public Pose CupRest;
        }

        /// <summary>The lounge as a shot finds it: the cup Maya left on its coaster, the cards that say what is a
        /// recording and who is here, whoever is sitting, and the screen.</summary>
        static LoungeScene Scene(FilmStage s, float evening, bool adam, bool maya, string place = "not here now", bool cupCard = true, bool screen = false)
        {
            var x = new LoungeScene { Room = FilmSets.Lounge(s, evening) };
            var room = x.Room;
            FilmSets.Coaster(s, V(room.AdamCupAt.x, FilmSets.TableTop, room.AdamCupAt.z));
            x.CupRest = new Pose(room.AdamCupAt, Quaternion.identity);
            x.Cup = FilmSets.Cup(s, room.AdamCupAt);
            if (cupCard) x.CupCard = FilmSets.TentCard(s, V(-0.15f, FilmSets.TableTop, 0.9f), -58f, "Maya", "recorded message");
            if (place != null) x.PlaceCard = FilmSets.TentCard(s, V(0.3f, FilmSets.TableTop, 1.02f), -58f, "Maya", place, 0.11f, 0.056f);
            if (adam) x.Adam = s.Actor("adam", room.Adam, 0f, 1f);
            if (maya)
            {
                x.Maya = s.Actor("maya", room.Maya, 180f, 2f);
                FilmSets.Cup(s, V(0.12f, FilmSets.TableTop, 1.12f), 160f);
            }
            if (screen) x.Screen = FilmSets.Screen(s, room.ScreenAt, V(0.35f, 1.2f, 0.72f));
            return x;
        }

        /// <summary>Maya's sunset on the lounge's screen, from <paramref name="from"/> on.</summary>
        static void Play(FilmStage s, FilmScreen screen, float from, float level = 1.2f, float fadeIn = 0.6f)
        {
            s.Moves.Add(t =>
            {
                var on = Mathf.SmoothStep(0f, 1f, (t - from) / fadeIn);
                var frame = Mathf.FloorToInt(Mathf.Max(0f, t - from) * 24f) % 192;
                screen.Show(Frame("m_sunset", frame), on * level * (0.96f + 0.04f * Mathf.Sin(t * 2.1f)));
            });
        }

        /// <summary>Her two photos in frames on the table, and a card for her voice. Returns the prints' materials.</summary>
        static Material[] Photos(FilmStage s)
        {
            var a = FilmSets.PhotoFrame(s, V(-0.34f, FilmSets.TableTop, 0.8f), -24f, Picture(Frame("m_bench", 0)), 0.2f, 16f / 9f);
            var b = FilmSets.PhotoFrame(s, V(-0.1f, FilmSets.TableTop, 1.0f), -8f, Picture(Frame("m_sky", 0)), 0.17f, 16f / 9f);
            return new[] { a.Find("Photo").GetComponent<MeshRenderer>().sharedMaterial, b.Find("Photo").GetComponent<MeshRenderer>().sharedMaterial };
        }

        static void Lit(Material print, float k)
        {
            print.SetColor("_BaseColor", Color.white * Mathf.Lerp(0.12f, 1f, k));
            print.SetColor("_EmissionColor", new Color(0.35f, 0.35f, 0.35f) * k);
        }

        static readonly Vector3 CupFingers = new(-0.1f, -0.05f, 1f), CupPalm = Vector3.left;
        static Grip CupGrip => FilmSets.MugHandle(Vector3.right, CupFingers);
        static readonly Pose CupNear = new(new Vector3(0.08f, 1.0f, 0.46f), Quaternion.identity);

        /// <summary>The cup's rim, straight down: the picture the phone's ring cuts to. Then the camera comes round to
        /// the table as he will find it.</summary>
        static void S09(FilmStage s)
        {
            var x = Scene(s, 0.5f, false, false);
            var cup = x.Room.AdamCupAt;
            s.Camera.Position.Key(0f, cup + V(0f, 0.482f, -0.012f)).Key(0.8f, cup + V(0f, 0.5f, -0.014f), Ease.Linear).Key(3f, V(0.5f, 1.1f, 0.2f), Ease.Soft);
            s.Camera.Target.Key(0f, cup + V(0f, 0.09f, 0f)).Key(0.8f, cup + V(0f, 0.09f, 0f)).Key(3f, V(0.02f, 0.86f, 0.82f), Ease.Soft);
            s.Camera.Fov.Key(0f, 26f).Key(0.8f, 26f).Key(3f, 31f, Ease.Soft);
            s.Camera.Focus.Key(0f, cup + V(0f, 0.09f, 0f));
            s.Camera.Aperture.Key(0f, 3.2f);
            s.Camera.Life = 0.4f;
            s.Camera.Grade(8f, 6f, 4f, 0.3f, 0.6f);
        }

        // ============================================================ 4  A MESSAGE YOU CAN HOLD

        /// <summary>Adam, that evening: the headset in his hands, then on.</summary>
        static void S10(FilmStage s)
        {
            var home = FilmSets.Home(s, 0.75f);
            var adam = s.Actor("adam", home.Seat, 0f, 1f);
            adam.Lean.Key(0f, 6f).Key(0.9f, 7f).Key(2.5f, -5f, Ease.Soft);
            adam.Gaze(0f, home.Seat + V(0f, 0.76f, 0.38f), 0.75f);
            adam.Gaze(1.5f, home.Seat + V(0f, 1.25f, 3f), 0.2f);
            adam.Smile(0.2f, 8f);
            adam.Smile(1.2f, 36f);
            // where the headset ends up: on his face as it will be once he has straightened
            adam.Update(3.2f, 0f);
            var worn = new Pose(adam.Eyes - adam.FaceForward * 0.012f, Quaternion.LookRotation(adam.FaceForward, Vector3.up));
            adam.Update(0f, 0f);
            var lap = new Pose(home.Seat + V(0f, 0.75f, 0.36f), Quaternion.Euler(-44f, 0f, 0f));
            var headset = FilmProps.Headset(s, lap.position, lap.rotation);
            Vector3 side = V(0.118f, -0.014f, -0.012f), along = V(0f, 0.12f, 1f);
            var grip = Grip.From(new Pose(Vector3.zero, Quaternion.identity), side, along, Vector3.left);
            void Hands(float t, Pose p, Ease e = Ease.InOut)
            {
                var f = p.rotation * along;
                Hand(adam.Right, t, grip.Wrist(p, f, p.rotation * Vector3.left), f, p.rotation * Vector3.left, e);
                Hand(adam.Left, t, p.position + p.rotation * V(-side.x, side.y, side.z), f, p.rotation * Vector3.right, e);
            }
            On(adam.Right, 0.42f);
            On(adam.Left, 0.42f);
            Hands(0f, lap);
            Hands(0.9f, lap);
            var mid = new Pose(Vector3.Lerp(lap.position, worn.position, 0.55f) + V(0f, 0.01f, 0.1f), Quaternion.Slerp(lap.rotation, worn.rotation, 0.5f));
            Hands(1.75f, mid, Ease.In);
            Hands(2.55f, worn, Ease.Out);
            s.Hold(headset, adam.Right, grip, -1f, 99f, lap, lap);

            Cam(s, 0f, V(0.5f, 1.12f, 1.42f), home.Seat + V(0f, 1.02f, 0.14f), 30f);
            Cam(s, 3.5f, V(0.4f, 1.14f, 1.28f), home.Seat + V(0f, 1.08f, 0.14f), 30f, Ease.Soft);
            s.Camera.Focus.Key(0f, home.Seat + V(0f, 1.1f, 0.25f));
            s.Camera.Aperture.Key(0f, 2.8f);
            s.Camera.Grade(10f, 4f, 4f, 0.32f, 0.6f);
            s.Lamp("Face fill", V(0.7f, 1.45f, 1.2f), new Color(1f, 0.86f, 0.7f), 3f, 4f);
        }

        /// <summary>The lounge. He settles, looks about him, and sees the cup.</summary>
        static void S11(FilmStage s)
        {
            var x = Scene(s, 0.55f, true, false);
            var adam = x.Adam;
            adam.Lean.Key(0f, -3f).Key(1.2f, 0f).Key(2.7f, 8f, Ease.Soft);
            adam.Shrug.Key(0f, 4f).Key(1.2f, -3f, Ease.Soft);
            adam.Gaze(0f, V(0.6f, 1.35f, 3.4f), 0.55f);
            adam.Gaze(1.0f, V(-2.2f, 1.3f, 0.9f), 0.6f, 1f, true);
            adam.Gaze(2.1f, x.Room.AdamCupAt + Vector3.up * 0.06f, 0.7f, 1f, true);
            adam.Shape("BrowInnerUp").Key(2.1f, 0f).Key(2.8f, 30f);

            Cam(s, 0f, V(1.38f, 1.52f, -1.02f), V(0f, 0.93f, 0.8f), 38f);
            Cam(s, 3.5f, V(1.22f, 1.46f, -0.86f), V(0f, 0.93f, 0.8f), 38f, Ease.Linear);
            s.Camera.Focus.Key(0f, V(0f, 1.05f, 0.4f));
            s.Camera.Aperture.Key(0f, 4f);
            s.Camera.Grade(8f, 6f, 4f, 0.3f, 0.6f);
        }

        /// <summary>His hand goes to the cup, closes on the handle, lifts it.</summary>
        static void S12(FilmStage s)
        {
            var x = Scene(s, 0.55f, true, false);
            var adam = x.Adam;
            var grip = CupGrip;
            var rest = x.CupRest;
            var lifted = new Pose(V(0.09f, 0.975f, 0.52f), Quaternion.identity);
            adam.Right.Weight.Key(0.3f, 0f).Key(1.0f, 1f);
            adam.Right.Aim.Key(0.3f, 0f).Key(1.0f, 1f);
            Hand(adam.Right, 0.3f, V(0.098f, 0.82f, 0.445f), CupFingers, CupPalm);
            Hand(adam.Right, 1.45f, grip.Wrist(rest, CupFingers, CupPalm), CupFingers, CupPalm, Ease.Soft);
            Hand(adam.Right, 2.15f, grip.Wrist(rest, CupFingers, CupPalm), CupFingers, CupPalm);
            Hand(adam.Right, 3.3f, grip.Wrist(lifted, CupFingers, CupPalm), CupFingers, CupPalm, Ease.Soft);
            Hand(adam.Right, 5f, grip.Wrist(CupNear, CupFingers, CupPalm), CupFingers, CupPalm);
            adam.Right.Grip.Key(1.0f, 0f).Key(1.4f, 0.1f).Key(1.95f, 0.85f);
            adam.Right.Thumb.Key(1.4f, 0.1f).Key(1.95f, 0.6f);
            s.Hold(x.Cup, adam.Right, grip, 2.0f, 99f, rest, rest, 0.08f);
            adam.Lean.Key(0.3f, 2f).Key(1.45f, 14f, Ease.Soft).Key(2.15f, 14f).Key(3.3f, 6f, Ease.Soft);
            adam.Gaze(0f, x.Room.AdamCupAt + Vector3.up * 0.06f, 0.7f);

            Cam(s, 0f, V(0.62f, 0.97f, 1.0f), V(0.02f, 0.86f, 0.74f), 26f);
            Cam(s, 2.1f, V(0.6f, 0.98f, 0.97f), V(0.02f, 0.86f, 0.74f), 26f);
            Cam(s, 5f, V(0.6f, 1.06f, 1.0f), V(0.05f, 1.02f, 0.52f), 27f, Ease.Soft);
            s.Camera.Focus.Key(0f, x.Room.AdamCupAt + Vector3.up * 0.05f).Key(2.1f, x.Room.AdamCupAt + Vector3.up * 0.05f).Key(3.3f, lifted.position + Vector3.up * 0.05f);
            s.Camera.Aperture.Key(0f, 3.6f);
            s.Camera.Grade(8f, 6f, 4f, 0.3f, 0.6f);
        }

        static void HoldCupNear(FilmStage s, LoungeScene x)
        {
            var adam = x.Adam;
            On(adam.Right, 0.85f);
            Hand(adam.Right, 0f, CupGrip.Wrist(CupNear, CupFingers, CupPalm), CupFingers, CupPalm);
            adam.Right.Thumb.Key(0f, 0.6f);
            s.Hold(x.Cup, adam.Right, CupGrip, -1f, 99f, x.CupRest, x.CupRest);
        }

        /// <summary>His face as he listens to her.</summary>
        static void S13a(FilmStage s)
        {
            var x = Scene(s, 0.58f, true, false);
            var adam = x.Adam;
            HoldCupNear(s, x);
            adam.Lean.Key(0f, 6f);
            adam.Gaze(0f, CupNear.position + Vector3.up * 0.08f, 0.65f);
            adam.Gaze(1.5f, V(0f, 1.22f, 1.5f), 0.8f, 1f, true);
            adam.Smile(1.7f, 4f);
            adam.Smile(3.0f, 58f);
            adam.Shape("BrowInnerUp").Key(0.4f, 6f).Key(1.4f, 18f).Key(3.2f, 10f);
            adam.Tilt.Key(1.8f, 0f).Key(3.0f, 4f, Ease.Soft);
            adam.Blinks.Add(2.9f);
            FilmSets.Portrait(s, x.Room, V(0.9f, 1.6f, 1.3f), V(0f, 1.25f, 0.3f));

            Cam(s, 0f, V(0.37f, 1.2f, 1.03f), V(0.02f, 1.24f, 0.3f), 24f);
            Cam(s, 4f, V(0.33f, 1.2f, 0.96f), V(0.02f, 1.24f, 0.3f), 24f, Ease.Linear);
            s.Camera.Focus.Key(0f, V(0f, 1.27f, 0.31f));
            s.Camera.Aperture.Key(0f, 2.2f);
            s.Camera.Grade(8f, 6f, 4f, 0.3f, 0.6f);
        }

        /// <summary>The two chairs again, one of them his now. He sets the cup back down, and the screen wakes.</summary>
        static void S13b(FilmStage s)
        {
            var x = Scene(s, 0.6f, true, false, "not here now", true, true);
            var adam = x.Adam;
            var grip = CupGrip;
            On(adam.Right, 0.85f);
            Hand(adam.Right, 0f, grip.Wrist(CupNear, CupFingers, CupPalm), CupFingers, CupPalm);
            Hand(adam.Right, 0.7f, grip.Wrist(CupNear, CupFingers, CupPalm), CupFingers, CupPalm);
            Hand(adam.Right, 2.0f, grip.Wrist(new Pose(x.CupRest.position + Vector3.up * 0.02f, Quaternion.identity), CupFingers, CupPalm), CupFingers, CupPalm, Ease.Soft);
            Hand(adam.Right, 2.3f, grip.Wrist(x.CupRest, CupFingers, CupPalm), CupFingers, CupPalm);
            adam.Right.Thumb.Key(0f, 0.6f);
            s.Hold(x.Cup, adam.Right, grip, -1f, 2.3f, x.CupRest, x.CupRest, 0.05f);
            adam.Lean.Key(0f, 6f).Key(2.0f, 14f, Ease.Soft).Key(3.5f, 12f);
            adam.Gaze(0f, V(0f, 1.15f, 1.5f), 0.7f);
            adam.Gaze(2.5f, x.Room.ScreenAt, 0.85f, 1f, true);
            adam.Smile(0f, 34f);
            Play(s, x.Screen, 2.6f);

            Cam(s, 0f, V(2.18f, 1.1f, 0.56f), V(-0.1f, 0.98f, 0.66f), 36f);
            Cam(s, 3.5f, V(2.08f, 1.1f, 0.56f), V(-0.1f, 0.98f, 0.66f), 36f, Ease.Linear);
            s.Camera.Focus.Key(0f, V(0f, 1.0f, 0.5f));
            s.Camera.Aperture.Key(0f, 4f);
            s.Camera.Grade(8f, 6f, 4f, 0.3f, 0.6f);
        }

        // ============================================================ 5  HER DAY, SHARED

        /// <summary>Her sunset plays on the lounge's screen; he turns to it.</summary>
        static void S14(FilmStage s)
        {
            var x = Scene(s, 0.62f, true, false, "not here now", true, true);
            var adam = x.Adam;
            adam.Twist.Key(0f, 16f);
            adam.Lean.Key(0f, 5f).Key(3f, 9f, Ease.Soft);
            adam.Gaze(0f, x.Room.ScreenAt, 0.9f);
            adam.Smile(1.5f, 6f);
            adam.Smile(3.2f, 46f);
            Play(s, x.Screen, 0.25f);
            // a line under the picture says whose it is
            var plate = Ui.Panel(x.Screen.Root, "Label", new Vector2(0.34f, 0.05f), Theme.CardDark, 0.025f, new Vector3(0f, -x.Screen.Height * 0.5f - 0.052f, 0f), new Color(1, 1, 1, 0.12f), 0.0012f, Ui.OrderBackground);
            var label = Ui.Text(x.Screen.Root, "Label text", "From Maya's day", 0.021f, Theme.OnDark, 0.34f, TextAlignmentOptions.Center, new Vector3(0f, -x.Screen.Height * 0.5f - 0.052f, 0f), FontStyles.Bold, 0.03f);
            label.ForceMeshUpdate(true, true);
            s.Soft("Label back", new Vector3(0f, -x.Screen.Height * 0.5f - 0.052f, 0.004f), new Vector3(0.33f, 0.045f, 0.004f), 0.0018f, FilmStage.Mat(new Color(0.05f, 0.06f, 0.06f), 0.3f), x.Screen.Root, null, false);

            Cam(s, 0f, V(0.52f, 1.34f, -0.44f), V(-1.3f, 1.14f, 0.72f), 34f);
            Cam(s, 5.5f, V(0.4f, 1.33f, -0.3f), V(-1.3f, 1.14f, 0.72f), 34f, Ease.Linear);
            s.Camera.Focus.Key(0f, x.Room.ScreenAt);
            s.Camera.Aperture.Key(0f, 2.8f);
            s.Camera.Grade(8f, 6f, 4f, 0.3f, 0.6f);
        }

        /// <summary>Her photos in frames on the table, as he sees them.</summary>
        static void S15a(FilmStage s)
        {
            var x = Scene(s, 0.62f, false, false, null, false, true);
            var prints = Photos(s);
            FilmSets.TentCard(s, V(0.12f, FilmSets.TableTop, 0.94f), 8f, "Maya's voice", "0:05 · recorded", 0.11f, 0.056f);
            Play(s, x.Screen, -6f);
            s.Moves.Add(t =>
            {
                Lit(prints[1], Mathf.SmoothStep(0f, 1f, (t - 0.1f) / 0.6f));
                Lit(prints[0], Mathf.SmoothStep(0f, 1f, (t - 0.5f) / 0.6f));
            });
            Cam(s, 0f, V(0.03f, 1.14f, 0.22f), V(-0.18f, 0.88f, 0.9f), 30f);
            Cam(s, 2.5f, V(0f, 1.1f, 0.32f), V(-0.2f, 0.88f, 0.9f), 30f, Ease.Linear);
            s.Camera.Focus.Key(0f, V(-0.34f, 0.86f, 0.8f));
            s.Camera.Aperture.Key(0f, 3.2f);
            s.Camera.Grade(8f, 6f, 4f, 0.3f, 0.6f);
        }

        /// <summary>He leans in and smiles at what she photographed.</summary>
        static void S15b(FilmStage s)
        {
            var x = Scene(s, 0.62f, true, false, null, false, true);
            var prints = Photos(s);
            foreach (var p in prints) Lit(p, 1f);
            Play(s, x.Screen, -9f);
            var adam = x.Adam;
            adam.Lean.Key(0f, 4f).Key(1.3f, 15f, Ease.Soft);
            adam.Gaze(0f, V(-0.1f, 0.9f, 1.0f), 0.7f);
            adam.Gaze(0.9f, V(-0.34f, 0.88f, 0.8f), 0.8f, 1f, true);
            adam.Smile(0.8f, 10f);
            adam.Smile(1.8f, 62f);
            adam.Shape("JawOpen").Key(1.5f, 0f).Key(1.8f, 10f).Key(2.2f, 3f);
            adam.Tilt.Key(1.2f, 0f).Key(2.0f, -4f, Ease.Soft);
            FilmSets.Portrait(s, x.Room, V(-1.2f, 1.5f, 1.3f), V(0f, 1.2f, 0.36f));

            Cam(s, 0f, V(-0.62f, 1.0f, 0.98f), V(0f, 1.2f, 0.36f), 30f);
            Cam(s, 2.5f, V(-0.58f, 1.0f, 0.94f), V(0f, 1.2f, 0.36f), 30f, Ease.Linear);
            s.Camera.Focus.Key(0f, V(0f, 1.26f, 0.32f));
            s.Camera.Aperture.Key(0f, 2.4f);
            s.Camera.Grade(8f, 6f, 4f, 0.3f, 0.6f);
        }

        /// <summary>He answers her: "I wish I'd been there. Tell me everything."</summary>
        static void S16(FilmStage s)
        {
            var x = Scene(s, 0.66f, true, false, null, false, true);
            var prints = Photos(s);
            foreach (var p in prints) Lit(p, 1f);
            Play(s, x.Screen, -12f, 0.9f);
            var adam = x.Adam;
            // the reply card by his hand: a touch starts it recording, and a small light says so
            var card = FilmSets.TentCard(s, V(0.22f, FilmSets.TableTop, 0.47f), 0f, "Reply to Maya", "recording", 0.11f, 0.056f);
            var red = FilmStage.Mat(new Color(0.2f, 0.02f, 0.02f), 0.4f, 0f, null, new Color(0f, 0f, 0f));
            s.Ball("Recording light", V(0.22f, FilmSets.TableTop + 0.059f, 0.47f), 0.011f, red, null, false);
            var glow = s.Lamp("Recording glow", V(0.22f, FilmSets.TableTop + 0.075f, 0.47f), new Color(1f, 0.2f, 0.14f), 0f, 0.5f);
            s.Moves.Add(t =>
            {
                var on = t >= 0.62f ? 0.75f + 0.25f * Mathf.Sin((t - 0.62f) * 5f) : 0f;
                red.SetColor("_EmissionColor", new Color(4f, 0.35f, 0.25f) * on);
                glow.intensity = 0.25f * on;
            });
            Tap(adam.Right, 0.6f, V(0.22f, FilmSets.TableTop + 0.064f, 0.47f), Vector3.up, 0.03f);
            adam.Right.TouchWeight.Key(0.1f, 0f).Key(0.3f, 1f).Key(0.85f, 1f).Key(1.2f, 0f);
            adam.Right.Index.Key(0.1f, 0f).Key(0.4f, -0.25f).Key(0.85f, -0.25f).Key(1.2f, 0f);
            adam.Right.Grip.Key(0.1f, 0f).Key(0.4f, 0.5f).Key(0.85f, 0.5f).Key(1.2f, 0f);
            adam.Say(1.0f, Voice("adam-wish"));
            adam.Gaze(0f, V(-0.3f, 0.9f, 0.8f), 0.6f);
            adam.Gaze(0.95f, V(0f, 1.2f, 1.5f), 0.85f, 1f, true);
            adam.Shape("BrowInnerUp").Key(1.0f, 4f).Key(1.8f, 20f).Key(3.2f, 8f);
            adam.Smile(1.0f, 12f);
            adam.Smile(3.2f, 20f);
            adam.Smile(4.2f, 52f);
            FilmSets.Portrait(s, x.Room, V(1.0f, 1.6f, 1.4f), V(0f, 1.25f, 0.3f));
            adam.Lean.Key(0f, 5f).Key(3.2f, 6f).Key(4.2f, 11f, Ease.Soft);
            adam.Nod.Key(2.3f, 0f).Key(2.6f, 5f).Key(3f, 0f);

            Cam(s, 0f, V(0.46f, 1.25f, 1.1f), V(0.02f, 1.23f, 0.3f), 28f);
            Cam(s, 6f, V(0.4f, 1.25f, 1.0f), V(0.02f, 1.23f, 0.3f), 28f, Ease.Linear);
            s.Camera.Focus.Key(0f, V(0f, 1.27f, 0.31f));
            s.Camera.Aperture.Key(0f, 2.4f);
            s.Camera.Grade(8f, 6f, 4f, 0.3f, 0.6f);
        }

        /// <summary>His reply, set down by the cup for her to find. The light goes; the lamps take over.</summary>
        static void S17(FilmStage s)
        {
            var x = Scene(s, 0.66f, true, false, "not here now", false, true);
            var prints = Photos(s);
            foreach (var print in prints) Lit(print, 1f);
            Play(s, x.Screen, -16f, 0.7f);
            var adam = x.Adam;
            var turn = Quaternion.Euler(0f, 205f, 0f);
            Pose start = new(V(0.2f, FilmSets.TableTop + 0.05f, 0.56f), turn), placed = new(V(0.22f, FilmSets.TableTop, 0.72f), turn);
            var card = FilmSets.TentCard(s, placed.position, 205f, "Adam", "voice reply · waiting for Maya", 0.125f, 0.06f);
            Vector3 f = V(0f, -0.5f, 1f), p = V(0f, -1f, -0.5f);
            var grip = Grip.From(new Pose(Vector3.zero, turn), V(0f, 0.09f, -0.095f), f, p);
            On(adam.Right, 0.6f);
            Hand(adam.Right, 0f, grip.Wrist(start, f, p), f, p);
            Hand(adam.Right, 1.0f, grip.Wrist(new Pose(placed.position + Vector3.up * 0.012f, turn), f, p), f, p, Ease.Soft);
            Hand(adam.Right, 1.3f, grip.Wrist(placed, f, p), f, p);
            Hand(adam.Right, 1.6f, grip.Wrist(placed, f, p), f, p);
            Hand(adam.Right, 2.5f, V(0.12f, 0.83f, 0.46f), V(-0.2f, -0.1f, 1f), Vector3.down, Ease.Soft);
            adam.Right.Grip.Key(1.4f, 0.6f).Key(1.75f, 0.1f);
            s.Hold(card, adam.Right, grip, -1f, 1.45f, start, placed, 0.08f);
            adam.Lean.Key(0f, 8f).Key(1.1f, 14f, Ease.Soft).Key(2.6f, 4f, Ease.Soft);
            adam.Gaze(0f, placed.position + Vector3.up * 0.05f, 0.7f);
            adam.Gaze(2.0f, V(0f, 1.15f, 1.5f), 0.75f, 1f, true);
            adam.Smile(1.9f, 6f);
            adam.Smile(2.8f, 40f);
            // evening comes on
            var room = x.Room;
            float sun = room.Sun.intensity, lamp = room.Lamp.intensity, fill = room.Fill.intensity;
            Color sky = RenderSettings.ambientSkyColor, mid = RenderSettings.ambientEquatorColor, ground = RenderSettings.ambientGroundColor;
            s.Moves.Add(t =>
            {
                var k = Mathf.SmoothStep(0f, 1f, (t - 1.9f) / 2.1f);
                room.Sun.intensity = sun * Mathf.Lerp(1f, 0.18f, k);
                room.Lamp.intensity = lamp * Mathf.Lerp(1f, 1.9f, k);
                room.Fill.intensity = fill * Mathf.Lerp(1f, 1.7f, k);
                RenderSettings.ambientSkyColor = sky * Mathf.Lerp(1f, 0.5f, k);
                RenderSettings.ambientEquatorColor = mid * Mathf.Lerp(1f, 0.55f, k);
                RenderSettings.ambientGroundColor = ground * Mathf.Lerp(1f, 0.6f, k);
            });

            Cam(s, 0f, V(0.52f, 1.0f, 1.32f), V(0.14f, 0.85f, 0.72f), 30f);
            Cam(s, 4f, V(0.62f, 1.07f, 1.44f), V(0.12f, 0.87f, 0.7f), 30f, Ease.Soft);
            s.Camera.Focus.Key(0f, placed.position + Vector3.up * 0.04f);
            s.Camera.Aperture.Key(0f, 2.8f);
            s.Camera.Grade(8f, 6f, 4f, 0.3f, 0.6f);
        }
    }
}
