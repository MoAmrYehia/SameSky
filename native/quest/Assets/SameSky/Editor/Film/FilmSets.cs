// The trailer's sets. The lounge is the app's own room (copied out of its scene) with a second chair at the person's
// place and film lighting; Adam's flat, Maya's desk and the waterfront are built here for the film. Everything is made
// of the project's own procedural meshes and CC0 textures, of soft-edged boxes, or of textures generated in code.

using System.Collections.Generic;
using SameSky.Geometry;
using SameSky.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace SameSky.Film
{
    public sealed class LoungeSet
    {
        public Transform Root, AdamChair, MayaChair;
        public Light Sun, FaceKey, Lamp, Fill, CameraFill;
        /// <summary>Where each of them sits (on the floor, as the app's seat anchors are), and the table between them.</summary>
        public Vector3 Adam = Vector3.zero, Maya = new(0f, 0f, 1.45f), Table = new(0f, FilmSets.TableTop, 0.72f);
        /// <summary>The cup Maya leaves for Adam (on its coaster, near the middle of the table), and her own.</summary>
        public Vector3 AdamCupAt = new(0.04f, FilmSets.TableTop + 0.008f, 0.74f), MayaCupAt = new(-0.15f, FilmSets.TableTop, 1.04f);
        public Vector3 ScreenAt = new(-1.32f, 1.2f, 0.72f);
    }

    public sealed class FilmScreen
    {
        public Transform Root;
        public Material Picture;
        public Light Glow;
        public float Width, Height;
        Texture2D _frame;

        /// <summary>Shows a picture file on the screen (a frame of a rendered clip), at this brightness.</summary>
        public void Show(string file, float brightness = 1f)
        {
            if (System.IO.File.Exists(file))
            {
                if (_frame == null) _frame = new Texture2D(2, 2, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                _frame.LoadImage(System.IO.File.ReadAllBytes(file), false);
                Picture.SetTexture("_BaseMap", _frame);
            }
            Picture.SetColor("_BaseColor", Color.white * brightness);
            if (Glow != null) Glow.intensity = 1.3f * brightness;
        }
    }

    public sealed class WaterfrontSet
    {
        public Light Sun, Lamp;
        public Material Water;
        public Vector3 Seat = new(-0.3f, 0f, 0f);
        public float SeatTop = 0.465f;
        public Transform PaperCup, Camera;
        public Vector3 SunDirection;
    }

    public sealed class HomeSet
    {
        public Light Sun, Lamp;
        public Vector3 Seat = new(-0.35f, 0f, -0.1f);
        public float TableTop = 0.4225f, ConsoleTop = 0.88f;
        public Vector3 Table = new(-0.1f, 0.4225f, 0.78f), Bowl = new(4.28f, 0.88f, -0.66f);
        public Transform Headset, Phone;
    }

    public sealed class DeskSet
    {
        public Light Lamp;
        public Vector3 Seat = new(-0.05f, 0f, -0.2f);
        public float Top = 0.74f;
        public Vector3 MugAt = new(0.24f, 0.74f, 0.16f);
        public Transform Camera, Laptop;
    }

    public static class FilmSets
    {
        public const float TableTop = 0.774f, TableRadius = 0.525f;

        public static string Media(string name) => System.IO.Path.Combine(FilmRenderer.Root, "media", name);

        // ------------------------------------------------------------ the lounge
        /// <summary>The shared lounge. <paramref name="evening"/> 0 is a bright afternoon, 1 the last warm light with the
        /// lamps doing more of the work.</summary>
        public static LoungeSet Lounge(FilmStage s, float evening = 0.35f)
        {
            var set = new LoungeSet { Root = s.Lounge() };
            // the person's own chair: in the headset it is the chair they really sit on; the film shows the room from
            // outside, so it needs one
            var chairs = set.Root.Find("Furniture/chairs");
            Transform partner = null;
            if (chairs != null)
                foreach (var t in chairs.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Partner chair" && t.gameObject.activeInHierarchy) partner = t;
            if (partner != null)
            {
                set.MayaChair = partner;
                var mine = Object.Instantiate(partner.gameObject, partner.parent).transform;
                mine.name = "Person chair";
                mine.localPosition = new Vector3(0f, 0f, -0.11f);
                mine.localRotation = Quaternion.identity;
                set.AdamChair = mine;
            }
            else Debug.LogWarning("[SameSky:film] the lounge has no partner chair to copy");

            // the view and the sky outside follow the hour
            var view = set.Root.Find("View");
            if (view != null && view.TryGetComponent<MeshRenderer>(out var plate))
            {
                var m = new Material(plate.sharedMaterial);
                var tint = Color.Lerp(new Color(1.25f, 1.16f, 1.02f), new Color(1.05f, 0.62f, 0.42f), evening);
                m.SetColor("_BaseColor", tint);
                m.SetColor("_Color", tint);
                plate.sharedMaterial = m;
            }
            if (RenderSettings.skybox != null)
            {
                var sky = new Material(RenderSettings.skybox);
                sky.SetColor("_SkyTint", Color.Lerp(new Color(0.62f, 0.69f, 0.8f), new Color(0.82f, 0.56f, 0.48f), evening));
                sky.SetFloat("_Exposure", Mathf.Lerp(1.15f, 0.8f, evening));
                sky.SetFloat("_AtmosphereThickness", Mathf.Lerp(0.8f, 1.5f, evening));
                RenderSettings.skybox = sky;
            }

            var warm = Color.Lerp(new Color(1f, 0.9f, 0.76f), new Color(1f, 0.66f, 0.4f), evening);
            set.Sun = s.Sun(new Vector3(Mathf.Lerp(30f, 15f, evening), 204f, 0f), warm, Mathf.Lerp(2.5f, 2.0f, evening));
            s.Ambient(Color.Lerp(new Color(0.52f, 0.57f, 0.66f), new Color(0.4f, 0.38f, 0.5f), evening) * 0.85f,
                Color.Lerp(new Color(0.64f, 0.6f, 0.55f), new Color(0.56f, 0.44f, 0.38f), evening) * 0.8f,
                Color.Lerp(new Color(0.44f, 0.39f, 0.34f), new Color(0.38f, 0.29f, 0.24f), evening) * 0.75f);
            // whoever sits with their back to the window gets a soft key from the room side
            set.FaceKey = s.Spot("Face key", new Vector3(-0.6f, 1.9f, -0.2f), new Vector3(0f, 1.12f, 1.45f), new Color(1f, 0.93f, 0.85f), 4.2f, 5f, 44f);
            set.Lamp = s.Lamp("Floor lamp", new Vector3(1.6f, 1.5f, 1.72f), new Color(1f, 0.8f, 0.56f), Mathf.Lerp(1.6f, 3.6f, evening), 3.8f);
            set.Fill = s.Lamp("Alcove glow", new Vector3(-2.38f, 1.34f, 0.7f), new Color(1f, 0.82f, 0.58f), Mathf.Lerp(0.8f, 1.7f, evening), 1.5f);
            set.CameraFill = s.Lamp("Room fill", new Vector3(1.9f, 1.7f, 0.2f), new Color(0.86f, 0.9f, 1f), 0.7f, 6f);
            // the wall behind the person's own chair: a long picture, a lamp and a plant, so the room is lived in
            // whichever way a shot looks
            FilmProps.WallPicture(s, new Vector3(0.25f, 1.52f, -1.385f), Vector3.forward, new Vector2(1.3f, 0.7f), new Color(0.84f, 0.5f, 0.3f), new Color(0.93f, 0.8f, 0.6f), new Color(0.36f, 0.44f, 0.5f),
                FilmProps.Tint("Walnut", new Color(0.8f, 0.64f, 0.52f)));
            FilmProps.FloorLamp(s, new Vector3(-1.55f, 0f, -1.02f), Mathf.Lerp(1.2f, 2.6f, evening), 3.2f, 1.5f);
            FilmProps.Plant(s, new Vector3(1.75f, 0f, -1.0f), 2.8f, 14);
            s.Reflections(new Vector3(0f, 1.05f, 0.5f), 1f);
            return set;
        }

        /// <summary>For a close shot of a face in the lounge: the window light comes down and a soft key from beside
        /// the camera takes over, so hair and brows don't cut hard shadows across it.</summary>
        public static void Portrait(FilmStage s, LoungeSet room, Vector3 from, Vector3 face, float sun = 0.42f, float key = 3.2f)
        {
            room.Sun.intensity *= sun;
            room.Sun.shadowStrength = 0.6f;
            s.Spot("Portrait key", from, face, new Color(1f, 0.9f, 0.78f), key, 6f, 50f);
        }

        static GameObject Bare(GameObject go, Transform parent, Vector3 position, float yaw)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        /// <summary>The app's ceramic mug: 9 cm tall, its handle toward its own +X, pivot at the bottom centre.</summary>
        public static Transform Cup(FilmStage s, Vector3 position, float yaw = 0f, Material ceramic = null)
        {
            var cup = Bare(SameSky.App.Props.Cup(), s.Root, position, yaw);
            if (ceramic != null) foreach (var r in cup.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = ceramic;
            // something to drink in it
            var tea = FilmStage.Mat(new Color(0.2f, 0.1f, 0.05f), 0.85f);
            s.Cylinder("Tea", new Vector3(0f, 0.074f, 0f), 0.071f, 0.002f, tea, cup.transform, null, false);
            return cup.transform;
        }

        public static Transform Coaster(FilmStage s, Vector3 position) => Bare(SameSky.App.Props.Coaster(), s.Root, position, 0f).transform;

        /// <summary>The grip that has an upright cup with its handle toward <paramref name="handle"/> held by that
        /// handle: the hand comes from the handle's side, palm toward the cup, fingers along <paramref name="fingers"/>.</summary>
        public static Grip MugHandle(Vector3 handle, Vector3 fingers)
        {
            handle = handle.normalized;
            fingers = fingers.normalized;
            var wrist = handle * 0.108f + Vector3.up * 0.05f - fingers * 0.07f;
            var cup = Quaternion.LookRotation(Vector3.Cross(handle, Vector3.up), Vector3.up);   // its +X toward the handle
            return Grip.From(new Pose(Vector3.zero, cup), wrist, fingers, -handle);
        }

        /// <summary>A hand around the cup's body from the side <paramref name="side"/>, fingers along <paramref name="fingers"/>:
        /// the way a cup is set down for someone else. <paramref name="yaw"/> turns the cup (and so its handle).</summary>
        public static Grip MugBody(Vector3 side, Vector3 fingers, float yaw = 0f)
        {
            side = side.normalized;
            fingers = fingers.normalized;
            var wrist = side * 0.078f + Vector3.up * 0.052f - fingers * 0.085f;
            return Grip.From(new Pose(Vector3.zero, Quaternion.Euler(0f, yaw, 0f)), wrist, fingers, -side);
        }

        /// <summary>A standing walnut photo frame, leaning back a little; it faces its own -Z (yaw 0 faces whoever sits
        /// at the origin).</summary>
        public static Transform PhotoFrame(FilmStage s, Vector3 position, float yaw, Texture photo, float width = 0.2f, float aspect = 1.5f)
        {
            var root = new GameObject("Photo frame").transform;
            root.SetParent(s.Root, false);
            root.SetPositionAndRotation(position, Quaternion.Euler(-9f, yaw, 0f));
            var height = width / aspect;
            s.Part("Frame", FilmMesh.RoundedBox(new Vector3(width + 0.024f, height + 0.024f, 0.016f), 0.004f, 3), new Vector3(0f, (height + 0.024f) / 2f, 0.008f), FilmProps.Tint("Walnut", new Color(0.8f, 0.64f, 0.52f)), root);
            var print = FilmStage.Mat(Color.white, 0.3f, 0f, photo, new Color(0.35f, 0.35f, 0.35f));
            if (photo != null) print.SetTexture("_EmissionMap", photo);
            s.Shape(PrimitiveType.Quad, "Photo", new Vector3(0f, (height + 0.024f) / 2f, -0.0006f), new Vector3(width, height, 1f), print, root, null, false);
            return root;
        }

        /// <summary>A folded paper card on the table with two lines of text on the side facing -Z.</summary>
        public static Transform TentCard(FilmStage s, Vector3 position, float yaw, string title, string sub, float width = 0.125f, float faceHeight = 0.064f)
        {
            var card = Bare(SameSky.App.Props.TentCard(width, faceHeight), s.Root, position, yaw).transform;
            var front = card.Find("Front");
            if (front != null)
            {
                var a = Ui.Text(front, "Title", title, 0.0125f, Theme.Ink, width - 0.012f, TextAlignmentOptions.Center, new Vector3(0f, sub == "" ? 0f : 0.011f, 0f), FontStyles.Bold, 0.02f);
                a.ForceMeshUpdate(true, true);
                if (sub != "")
                {
                    var b = Ui.Text(front, "Sub", sub, 0.0082f, Theme.Muted, width - 0.012f, TextAlignmentOptions.Center, new Vector3(0f, -0.011f, 0f), FontStyles.Normal, 0.014f);
                    b.ForceMeshUpdate(true, true);
                }
            }
            return card;
        }

        /// <summary>The lounge's screen: a thin dark plate that shows a picture and glows a little. It faces
        /// <paramref name="toward"/>.</summary>
        public static FilmScreen Screen(FilmStage s, Vector3 centre, Vector3 toward, float width = 0.96f)
        {
            var screen = new FilmScreen { Width = width, Height = width * 9f / 16f };
            screen.Root = new GameObject("Screen").transform;
            screen.Root.SetParent(s.Root, false);
            var normal = (toward - centre).normalized;
            screen.Root.SetPositionAndRotation(centre, Quaternion.LookRotation(-normal, Vector3.up));
            var plate = FilmStage.Mat(new Color(0.03f, 0.032f, 0.036f), 0.55f, 0.2f);
            s.Part("Plate", FilmMesh.RoundedBox(new Vector3(width + 0.03f, screen.Height + 0.03f, 0.014f), 0.006f, 3), new Vector3(0f, 0f, 0.0075f), plate, screen.Root);
            screen.Picture = FilmStage.Glow(Color.black);
            s.Shape(PrimitiveType.Quad, "Picture", new Vector3(0f, 0f, -0.0005f), new Vector3(width, screen.Height, 1f), screen.Picture, screen.Root, null, false);
            screen.Glow = s.Lamp("Screen glow", centre + normal * 0.35f, new Color(1f, 0.72f, 0.5f), 0f, 2.6f);
            return screen;
        }

        // ------------------------------------------------------------ the waterfront at sunset
        public static WaterfrontSet Waterfront(FilmStage s, float sunHeight = 4.5f, float sunAzimuth = 10f)
        {
            var set = new WaterfrontSet();
            if (s.Pipeline != null) s.Pipeline.shadowDistance = 40f;

            // sky, sun and air
            var sky = new Material(Shader.Find("Skybox/Panoramic")) { name = "Sunset sky" };
            sky.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
            sky.SetFloat("_Mapping", 1f);
            sky.SetFloat("_ImageType", 0f);
            sky.SetTexture("_MainTex", FilmTex.SunsetSky(sunAzimuth, sunHeight));
            sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            sky.SetFloat("_Exposure", 1f);
            sky.SetFloat("_Rotation", 0f);
            RenderSettings.skybox = sky;
            set.Sun = s.Sun(new Vector3(sunHeight + 1.5f, 180f + sunAzimuth, 0f), new Color(1f, 0.68f, 0.44f), 2.5f, true, 0.8f);
            set.SunDirection = -set.Sun.transform.forward;
            s.Ambient(new Color(0.36f, 0.36f, 0.54f) * 0.95f, new Color(0.76f, 0.5f, 0.44f) * 0.8f, new Color(0.34f, 0.24f, 0.22f) * 0.6f);
            s.Haze(new Color(0.74f, 0.46f, 0.42f), 0.0012f);

            // the promenade and the water
            var stone = FilmStage.Mat(new Color(0.6f, 0.55f, 0.5f), 0.22f, 0f, FilmTex.Pavers(), null, new Vector2(29f, 3f));
            s.Box("Promenade", new Vector3(0f, -0.6f, -4.75f), new Vector3(140f, 1.2f, 14.5f), stone);
            set.Water = FilmStage.Mat(new Color(0.02f, 0.04f, 0.065f), 0.93f);
            set.Water.SetTexture("_BumpMap", FilmTex.Ripples(256, 3));
            set.Water.SetFloat("_BumpScale", 0.3f);
            set.Water.EnableKeyword("_NORMALMAP");
            set.Water.SetTextureScale("_BaseMap", new Vector2(150f, 96f));
            set.Water.SetTexture("_DetailAlbedoMap", FilmTex.Grey());
            set.Water.SetTexture("_DetailNormalMap", FilmTex.Ripples(256, 9));
            set.Water.SetFloat("_DetailNormalMapScale", 0.45f);
            set.Water.SetFloat("_DetailAlbedoMapScale", 0f);
            set.Water.SetTextureScale("_DetailAlbedoMap", new Vector2(37f, 21f));
            set.Water.EnableKeyword("_DETAIL_MULX2");
            s.Shape(PrimitiveType.Quad, "Water", new Vector3(0f, -0.95f, 202f), new Vector3(900f, 400f, 1f), set.Water, null, new Vector3(90f, 0f, 0f), false);
            var water = set.Water;
            // the sun's road on the water: glints between whoever is looking and the sun
            var road = new Mesh { name = "Sun road", hideFlags = HideFlags.HideAndDontSave };
            road.vertices = new[] { new Vector3(-1.4f, 0f, 0f), new Vector3(1.4f, 0f, 0f), new Vector3(-26f, 0f, 320f), new Vector3(26f, 0f, 320f) };
            road.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 46f), new Vector2(1f, 46f) };
            road.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            road.RecalculateNormals();
            road.RecalculateBounds();
            var glints = FilmStage.Additive(new Color(2.6f, 1.5f, 0.75f, 0.85f), FilmTex.Sparkle());
            var sunRoad = s.Part("Sun road", road, new Vector3(0f, -0.94f, 2.6f), glints, null, null, false);
            var flatSun = new Vector3(set.SunDirection.x, 0f, set.SunDirection.z).normalized;
            sunRoad.transform.rotation = Quaternion.LookRotation(flatSun, Vector3.up);
            s.Moves.Add(t =>
            {
                water.SetTextureOffset("_BaseMap", new Vector2(t * 0.018f, -t * 0.05f));
                water.SetTextureOffset("_DetailAlbedoMap", new Vector2(-t * 0.006f, -t * 0.012f));
                glints.SetTextureOffset("_BaseMap", new Vector2(0f, t * 0.55f));
                glints.SetTextureScale("_BaseMap", new Vector2(1f, 1f));
                // it runs from under the camera toward the sun
                var eye = s.Camera.Position.At(t);
                var start = new Vector3(eye.x, -0.94f, 2.6f) + flatSun * 0.5f;
                sunRoad.transform.position = start;
            });

            // the far shore and a few clouds
            s.Card("Far shore", new Vector3(0f, 9.5f, 330f), new Vector2(760f, 24f), Vector3.back, FilmStage.Cutout(new Color(1.3f, 1.2f, 1.25f), FilmTex.Skyline()));
            // two small clouds that drift, nearer than the painted ones
            var cloudTint = new Color(1.5f, 0.74f, 0.5f, 0.5f);
            var rng = new System.Random(21);
            for (var i = 0; i < 3; i++)
            {
                var at = new Vector3(-150f + 150f * i, 40f + 16f * (float)rng.NextDouble(), 280f);
                var size = new Vector2(90f + 60f * (float)rng.NextDouble(), 14f + 8f * (float)rng.NextDouble());
                var cloud = s.Card("Cloud " + i, at, size, Vector3.back, FilmStage.Sheer(cloudTint, FilmTex.Cloud(i + 1)));
                var home = at;
                var drift = 1.2f + 0.8f * (float)rng.NextDouble();
                s.Moves.Add(t => cloud.transform.position = home + Vector3.right * (t * drift));
            }

            // the railing along the edge
            var iron = FilmStage.Mat(new Color(0.09f, 0.085f, 0.08f), 0.45f, 0.7f);
            for (var x = -19.8f; x <= 20f; x += 1.8f) s.Rod("Post", new Vector3(x, 0f, 2.15f), new Vector3(x, 1.06f, 2.15f), 0.05f, iron);
            foreach (var y in new[] { 1.06f, 0.72f, 0.38f })
                s.Rod("Rail", new Vector3(-22f, y, 2.15f), new Vector3(22f, y, 2.15f), y > 1f ? 0.055f : 0.03f, iron);

            // the bench: wooden slats on iron ends, room for two
            var wood = FilmProps.Tint("Walnut", new Color(0.62f, 0.44f, 0.32f), 0.3f);
            var bench = new GameObject("Bench").transform;
            bench.SetParent(s.Root, false);
            bench.position = new Vector3(0.2f, 0f, 0f);
            for (var i = 0; i < 4; i++)
                s.Soft("Seat slat", new Vector3(0f, set.SeatTop - 0.016f, -0.2f + i * 0.108f), new Vector3(1.78f, 0.032f, 0.092f), 0.008f, wood, bench);
            for (var i = 0; i < 3; i++)
                s.Soft("Back slat", new Vector3(0f, set.SeatTop + 0.2f + i * 0.135f, -0.285f - i * 0.03f), new Vector3(1.78f, 0.095f, 0.028f), 0.008f, wood, bench, new Vector3(-12f, 0f, 0f));
            foreach (var x in new[] { -0.86f, 0.86f })
            {
                s.Soft("End", new Vector3(x, set.SeatTop * 0.5f - 0.01f, -0.05f), new Vector3(0.05f, set.SeatTop - 0.02f, 0.46f), 0.012f, iron, bench);
                s.Soft("Back post", new Vector3(x, set.SeatTop + 0.3f, -0.31f), new Vector3(0.05f, 0.66f, 0.05f), 0.012f, iron, bench, new Vector3(-12f, 0f, 0f));
                s.Soft("Arm", new Vector3(x, set.SeatTop + 0.21f, -0.06f), new Vector3(0.06f, 0.03f, 0.5f), 0.012f, iron, bench);
                s.Soft("Arm post", new Vector3(x, set.SeatTop + 0.1f, 0.16f), new Vector3(0.045f, 0.2f, 0.045f), 0.01f, iron, bench);
            }

            // lamp posts with warm globes
            var globe = FilmStage.Mat(new Color(1f, 0.9f, 0.75f), 0.2f, 0f, null, new Color(3.2f, 2.1f, 1.1f));
            foreach (var x in new[] { -4.4f, 9.5f, -16.5f, 21.5f })
            {
                s.Rod("Lamp post", new Vector3(x, 0f, -1.7f), new Vector3(x, 3.1f, -1.7f), 0.075f, iron);
                s.Ball("Lamp globe", new Vector3(x, 3.25f, -1.7f), 0.34f, globe, null, false);
            }
            set.Lamp = s.Lamp("Lamp post light", new Vector3(-4.4f, 3.1f, -1.7f), new Color(1f, 0.74f, 0.48f), 3.6f, 9f);

            // behind the bench: a hedge, trees, and a lit front far back
            var hedge = FilmStage.Mat(new Color(0.14f, 0.24f, 0.13f), 0.1f);
            s.Soft("Hedge", new Vector3(0f, 0.5f, -3.1f), new Vector3(70f, 1f, 0.9f), 0.2f, hedge);
            var trunk = FilmStage.Mat(new Color(0.16f, 0.11f, 0.08f), 0.1f);
            var leaves = FilmStage.Mat(new Color(0.16f, 0.27f, 0.14f), 0.08f);
            var leavesLit = FilmStage.Mat(new Color(0.3f, 0.32f, 0.13f), 0.08f);
            foreach (var x in new[] { -8.5f, -3.4f, 3.8f, 9.6f, 15f })
            {
                s.Rod("Trunk", new Vector3(x, 0f, -5f), new Vector3(x + 0.2f, 3.2f, -5.1f), 0.3f, trunk);
                for (var i = 0; i < 6; i++)
                {
                    var at = new Vector3(x + 0.2f + 1.3f * Mathf.Sin(i * 2.4f + x), 3.6f + 0.8f * Mathf.Cos(i * 1.7f + x) + (i % 2) * 0.9f, -5.1f + 0.9f * Mathf.Sin(i * 1.3f));
                    s.Ball("Crown", at, 1.7f + 0.5f * Mathf.Sin(i + x), i % 3 == 0 ? leavesLit : leaves);
                }
            }
            s.Card("Fronts", new Vector3(0f, 5.6f, -19f), new Vector2(120f, 13f), Vector3.forward, FilmStage.Cutout(new Color(1.5f, 1.4f, 1.5f), FilmTex.Skyline(512, 64, 31)));

            // on the bench: a takeaway cup on the empty half, her camera at her other side
            set.PaperCup = FilmProps.PaperCup(s, new Vector3(0.52f, set.SeatTop, -0.03f));
            set.Camera = FilmProps.Camera(s, new Vector3(-0.72f, set.SeatTop, -0.06f), 168f);
            s.Reflections(new Vector3(0f, 1.6f, 1f), 1.1f);
            return set;
        }

        // ------------------------------------------------------------ Adam's flat
        /// <summary>A living room with a sofa and a low table, and a console by the door. <paramref name="hour"/> 0 is
        /// daytime (cool light through the blinds), 1 an evening (the lamp on, the sun low and warm).</summary>
        public static HomeSet Home(FilmStage s, float hour = 0f)
        {
            var set = new HomeSet();
            var wall = FilmProps.Tint("Plaster", new Color(0.78f, 0.8f, 0.8f), 0.08f);
            var trim = FilmStage.Mat(new Color(0.9f, 0.9f, 0.88f), 0.3f);
            var floor = FilmProps.Tint("Parquet", new Color(0.82f, 0.74f, 0.66f), 0.34f, new Vector2(3.4f, 3.4f));
            s.Box("Floor", new Vector3(1.5f, -0.05f, 1.2f), new Vector3(9.5f, 0.1f, 6.4f), floor);
            s.Box("Back wall", new Vector3(1.5f, 1.35f, -0.92f), new Vector3(9.5f, 2.7f, 0.12f), wall);
            s.Box("Ceiling", new Vector3(1.5f, 2.72f, 1.2f), new Vector3(9.5f, 0.1f, 6.4f), trim, null, null, false);
            s.Box("Skirting", new Vector3(1.5f, 0.05f, -0.85f), new Vector3(9.5f, 0.1f, 0.025f), trim);
            // the window wall on the left, with blinds
            s.Box("Left wall low", new Vector3(-2.72f, 0.45f, 1.2f), new Vector3(0.12f, 0.9f, 6.4f), wall);
            s.Box("Left wall high", new Vector3(-2.72f, 2.5f, 1.2f), new Vector3(0.12f, 0.4f, 6.4f), wall);
            s.Box("Left wall a", new Vector3(-2.72f, 1.6f, -0.45f), new Vector3(0.12f, 1.4f, 1.0f), wall);
            s.Box("Left wall b", new Vector3(-2.72f, 1.6f, 3.3f), new Vector3(0.12f, 1.4f, 2.2f), wall);
            var slat = FilmStage.Mat(new Color(0.88f, 0.86f, 0.82f), 0.25f);
            for (var y = 0.95f; y < 2.3f; y += 0.085f)
                s.Box("Blind", new Vector3(-2.66f, y, 1.12f), new Vector3(0.07f, 0.008f, 2.3f), slat, null, new Vector3(0f, 0f, 28f));
            var day = Color.Lerp(new Color(0.78f, 0.9f, 1.15f), new Color(1.3f, 0.75f, 0.45f), hour);
            s.Card("Outside", new Vector3(-4.2f, 1.6f, 1.2f), new Vector2(7f, 4f), Vector3.right, FilmStage.Glow(day * 1.5f));
            set.Sun = s.Sun(new Vector3(Mathf.Lerp(31f, 14f, hour), Mathf.Lerp(104f, 96f, hour), 0f),
                Color.Lerp(new Color(0.95f, 0.97f, 1f), new Color(1f, 0.66f, 0.4f), hour), Mathf.Lerp(2.6f, 2.1f, hour));
            s.Ambient(Color.Lerp(new Color(0.5f, 0.55f, 0.62f), new Color(0.3f, 0.29f, 0.36f), hour) * 0.9f,
                Color.Lerp(new Color(0.52f, 0.54f, 0.56f), new Color(0.36f, 0.3f, 0.28f), hour) * 0.85f,
                Color.Lerp(new Color(0.36f, 0.33f, 0.3f), new Color(0.24f, 0.19f, 0.16f), hour) * 0.8f);

            // the sofa
            var cloth = FilmProps.Tint("Boucle", new Color(0.4f, 0.46f, 0.47f), 0.05f, new Vector2(7f, 7f));
            var cushion = FilmProps.Tint("Boucle", new Color(0.46f, 0.52f, 0.53f), 0.05f, new Vector2(6f, 6f));
            var sofa = new GameObject("Sofa").transform;
            sofa.SetParent(s.Root, false);
            sofa.position = new Vector3(-0.1f, 0f, -0.32f);
            s.Soft("Base", new Vector3(0f, 0.2f, 0f), new Vector3(2.16f, 0.26f, 0.96f), 0.05f, cloth, sofa);
            s.Soft("Back", new Vector3(0f, 0.58f, -0.4f), new Vector3(2.16f, 0.72f, 0.2f), 0.08f, cloth, sofa);
            foreach (var x in new[] { -1.08f, 1.08f }) s.Soft("Arm", new Vector3(x, 0.42f, 0f), new Vector3(0.22f, 0.6f, 0.96f), 0.09f, cloth, sofa);
            foreach (var x in new[] { -0.48f, 0.48f })
            {
                s.Soft("Seat", new Vector3(x, 0.4f, 0.04f), new Vector3(0.94f, 0.15f, 0.78f), 0.06f, cushion, sofa);
                s.Soft("Back cushion", new Vector3(x, 0.72f, -0.25f), new Vector3(0.9f, 0.48f, 0.18f), 0.08f, cushion, sofa, new Vector3(-9f, 0f, 0f));
            }
            s.Soft("Throw cushion", new Vector3(0.78f, 0.62f, -0.12f), new Vector3(0.4f, 0.4f, 0.14f), 0.065f, FilmProps.Tint("Boucle", new Color(0.78f, 0.56f, 0.34f), 0.05f, new Vector2(4f, 4f)), sofa, new Vector3(-16f, -24f, 6f));
            foreach (var x in new[] { -1f, 1f })
                foreach (var z in new[] { -0.38f, 0.38f })
                    s.Rod("Foot", new Vector3(x, 0f, z), new Vector3(x, 0.08f, z), 0.045f, FilmStage.Mat(new Color(0.2f, 0.14f, 0.1f), 0.3f), sofa);

            // rug, low table, and what lies on it
            s.Soft("Rug", new Vector3(-0.1f, 0.006f, 0.82f), new Vector3(2.5f, 0.014f, 1.7f), 0.006f, FilmProps.Tint("RugKnit", new Color(0.8f, 0.76f, 0.68f), 0.04f, new Vector2(2f, 2f)));
            var oak = FilmProps.Tint("Walnut", new Color(0.86f, 0.72f, 0.58f), 0.38f);
            s.Soft("Table top", new Vector3(set.Table.x, set.TableTop - 0.0225f, set.Table.z), new Vector3(1.05f, 0.045f, 0.56f), 0.014f, oak);
            foreach (var x in new[] { -0.44f, 0.44f })
                foreach (var z in new[] { -0.2f, 0.2f })
                    s.Rod("Table leg", new Vector3(set.Table.x + x, 0f, set.Table.z + z), new Vector3(set.Table.x + x * 0.96f, set.TableTop - 0.04f, set.Table.z + z * 0.96f), 0.036f, oak);
            FilmProps.Books(s, new Vector3(set.Table.x - 0.33f, set.TableTop, set.Table.z + 0.05f), 14f, new Color(0.2f, 0.3f, 0.38f), new Color(0.82f, 0.78f, 0.7f));

            // the wall behind the sofa
            FilmProps.WallPicture(s, new Vector3(-0.1f, 1.6f, -0.855f), Vector3.forward, new Vector2(1.0f, 0.62f), new Color(0.2f, 0.32f, 0.42f), new Color(0.86f, 0.62f, 0.4f), new Color(0.93f, 0.86f, 0.74f));
            set.Lamp = FilmProps.FloorLamp(s, new Vector3(1.52f, 0f, -0.4f), Mathf.Lerp(0.5f, 3.4f, hour), 4.5f);
            var shelf = FilmProps.Tint("Walnut", new Color(0.5f, 0.38f, 0.3f), 0.3f);
            for (var i = 0; i < 4; i++) s.Soft("Shelf", new Vector3(2.75f, 0.4f + i * 0.42f, -0.7f), new Vector3(1.2f, 0.035f, 0.3f), 0.006f, shelf);
            foreach (var x in new[] { 2.16f, 3.34f }) s.Soft("Shelf side", new Vector3(x, 0.84f, -0.7f), new Vector3(0.035f, 1.68f, 0.3f), 0.006f, shelf);
            var spines = new[] { new Color(0.6f, 0.26f, 0.2f), new Color(0.2f, 0.32f, 0.4f), new Color(0.84f, 0.78f, 0.66f), new Color(0.3f, 0.4f, 0.3f), new Color(0.72f, 0.56f, 0.3f), new Color(0.16f, 0.17f, 0.2f) };
            var rng = new System.Random(8);
            for (var row = 0; row < 3; row++)
            {
                var x = 2.24f;
                while (x < 3.1f - row * 0.2f)
                {
                    float w = 0.024f + 0.03f * (float)rng.NextDouble(), h = 0.2f + 0.1f * (float)rng.NextDouble();
                    s.Soft("Book", new Vector3(x + w / 2f, 0.418f + row * 0.42f + h / 2f, -0.72f), new Vector3(w, h, 0.19f), 0.003f, FilmStage.Mat(spines[rng.Next(spines.Length)], 0.25f));
                    x += w + 0.003f;
                }
            }
            FilmProps.Plant(s, new Vector3(3.05f, 1.68f, -0.7f), 0.9f, 3);
            FilmProps.Plant(s, new Vector3(-2.2f, 0f, -0.45f), 2.6f, 9);

            // by the door: a console with a bowl for keys, a round mirror, a coat on its hook
            var console = FilmProps.Tint("Walnut", new Color(0.7f, 0.55f, 0.44f), 0.36f);
            s.Soft("Console top", new Vector3(4.25f, set.ConsoleTop - 0.02f, -0.68f), new Vector3(1.0f, 0.04f, 0.32f), 0.01f, console);
            foreach (var x in new[] { 3.8f, 4.7f })
                foreach (var z in new[] { -0.8f, -0.56f })
                    s.Rod("Console leg", new Vector3(x, 0f, z), new Vector3(x, set.ConsoleTop - 0.04f, z), 0.03f, console);
            FilmProps.Bowl(s, set.Bowl);
            var mirror = FilmStage.Mat(new Color(0.9f, 0.9f, 0.9f), 0.96f, 1f);
            s.Cylinder("Mirror", new Vector3(4.25f, 1.5f, -0.85f), 0.56f, 0.012f, mirror, null, new Vector3(90f, 0f, 0f));
            s.Cylinder("Mirror rim", new Vector3(4.25f, 1.5f, -0.856f), 0.6f, 0.02f, FilmStage.Mat(new Color(0.83f, 0.67f, 0.4f), 0.6f, 1f), null, new Vector3(90f, 0f, 0f));
            FilmProps.Plant(s, new Vector3(3.9f, set.ConsoleTop, -0.7f), 0.75f, 12);
            s.Box("Door", new Vector3(6.2f, 1.03f, -0.3f), new Vector3(0.06f, 2.06f, 0.95f), trim);
            s.Reflections(new Vector3(0.5f, 1.2f, 1f), 1f);
            return set;
        }

        // ------------------------------------------------------------ Maya's desk, at the end of her day
        public static DeskSet Desk(FilmStage s)
        {
            var set = new DeskSet();
            var wall = FilmProps.Tint("Plaster", new Color(0.86f, 0.8f, 0.72f), 0.08f);
            var floor = FilmProps.Tint("Parquet", new Color(0.72f, 0.6f, 0.5f), 0.3f, new Vector2(3f, 3f));
            s.Box("Floor", new Vector3(0f, -0.05f, 0f), new Vector3(7f, 0.1f, 6f), floor);
            // the window wall behind the desk
            s.Box("Wall low", new Vector3(0f, 0.475f, 0.82f), new Vector3(7f, 0.95f, 0.12f), wall);
            s.Box("Wall high", new Vector3(0f, 2.5f, 0.82f), new Vector3(7f, 0.6f, 0.12f), wall);
            s.Box("Wall left", new Vector3(-2.2f, 1.575f, 0.82f), new Vector3(2.6f, 1.25f, 0.12f), wall);
            s.Box("Wall right", new Vector3(2.4f, 1.575f, 0.82f), new Vector3(2.2f, 1.25f, 0.12f), wall);
            s.Box("Side wall", new Vector3(-2.6f, 1.35f, -1f), new Vector3(0.12f, 2.7f, 6f), wall);
            var frame = FilmStage.Mat(new Color(0.92f, 0.9f, 0.86f), 0.35f);
            s.Box("Window sill", new Vector3(0.2f, 0.96f, 0.76f), new Vector3(2.3f, 0.03f, 0.22f), frame);
            s.Box("Window bar", new Vector3(0.2f, 1.575f, 0.82f), new Vector3(0.04f, 1.25f, 0.05f), frame);
            var dusk = FilmTex.Gradient("dusk", new Color(1.5f, 0.72f, 0.36f), new Color(1.1f, 0.5f, 0.42f), new Color(0.42f, 0.34f, 0.56f), new Color(0.2f, 0.22f, 0.42f));
            s.Card("Dusk", new Vector3(0.2f, 3f, 9f), new Vector2(40f, 16f), Vector3.back, FilmStage.Glow(Color.white * 1.2f, dusk));
            s.Card("Roofs", new Vector3(0.2f, 1.25f, 16f), new Vector2(70f, 5f), Vector3.back, FilmStage.Cutout(new Color(0.9f, 0.9f, 0.9f), FilmTex.Skyline(2048, 256, 23)));
            var curtain = FilmProps.Tint("Boucle", new Color(0.8f, 0.72f, 0.6f), 0.04f, new Vector2(3f, 8f));
            for (var i = 0; i < 5; i++) s.Soft("Curtain", new Vector3(-1.06f - i * 0.085f, 1.5f, 0.7f + (i % 2) * 0.03f), new Vector3(0.1f, 2.1f, 0.07f), 0.03f, curtain);

            // the desk and what is on it
            var top = FilmProps.Tint("Walnut", new Color(0.82f, 0.66f, 0.52f), 0.38f);
            s.Soft("Desk top", new Vector3(0.1f, set.Top - 0.02f, 0.32f), new Vector3(1.7f, 0.04f, 0.74f), 0.012f, top);
            foreach (var x in new[] { -0.68f, 0.88f })
                foreach (var z in new[] { 0.02f, 0.62f })
                    s.Rod("Desk leg", new Vector3(x, 0f, z), new Vector3(x, set.Top - 0.04f, z), 0.04f, FilmStage.Mat(new Color(0.12f, 0.12f, 0.12f), 0.4f, 0.5f));
            set.Laptop = FilmProps.Laptop(s, new Vector3(-0.02f, set.Top, 0.34f), 4f);
            set.Camera = FilmProps.Camera(s, new Vector3(0.5f, set.Top, 0.4f), 205f);
            FilmProps.Plant(s, new Vector3(0.74f, set.Top, 0.56f), 0.7f, 5);
            FilmProps.Books(s, new Vector3(-0.36f, set.Top, 0.42f), -12f, new Color(0.62f, 0.3f, 0.24f), new Color(0.86f, 0.8f, 0.68f), new Color(0.24f, 0.34f, 0.36f));
            // a small desk lamp, lit
            var brass = FilmStage.Mat(new Color(0.83f, 0.67f, 0.4f), 0.62f, 1f);
            s.Cylinder("Lamp base", new Vector3(-0.56f, set.Top + 0.008f, 0.5f), 0.13f, 0.016f, brass);
            s.Rod("Lamp arm", new Vector3(-0.56f, set.Top, 0.5f), new Vector3(-0.5f, set.Top + 0.36f, 0.47f), 0.014f, brass);
            var shade = FilmStage.Mat(new Color(0.95f, 0.88f, 0.74f), 0.05f, 0f, null, new Color(0.8f, 0.52f, 0.26f));
            shade.SetFloat("_Cull", 0f);
            s.Part("Lamp shade", FilmMesh.Lathe("desk lamp shade", new[] { new Vector2(0.09f, 0f), new Vector2(0.05f, 0.11f), new Vector2(0.012f, 0.13f) }), new Vector3(-0.47f, set.Top + 0.3f, 0.455f), shade, null, new Vector3(0f, 0f, -18f), false);
            set.Lamp = s.Lamp("Desk lamp", new Vector3(-0.42f, set.Top + 0.26f, 0.4f), new Color(1f, 0.76f, 0.48f), 1.15f, 2.4f, true);
            // the last of the day through the window, and the room's own dim warmth
            s.Sun(new Vector3(9f, 196f, 0f), new Color(1f, 0.6f, 0.36f), 1.5f, true, 0.75f);
            s.Ambient(new Color(0.34f, 0.32f, 0.44f) * 0.8f, new Color(0.5f, 0.38f, 0.32f) * 0.75f, new Color(0.3f, 0.22f, 0.18f) * 0.7f);
            s.Lamp("Room", new Vector3(1.4f, 1.9f, -1.4f), new Color(1f, 0.82f, 0.66f), 1.1f, 6f);
            s.Reflections(new Vector3(0.2f, 1.2f, -0.4f), 1f);
            return set;
        }
    }
}
