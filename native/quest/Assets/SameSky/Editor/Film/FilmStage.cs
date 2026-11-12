// The stage for one shot of the animated trailer: a set, the people on it, lights, and a film camera with depth of
// field and a grade. A shot (FilmShots.cs) builds one of these and keys what moves; the renderer (FilmRenderer.cs)
// asks it to pose everything for each frame. Edit mode only, nothing here ships in the app.

using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SameSky.Film
{
    /// <summary>The film camera: where it is, what it looks at, what is sharp, and how the picture is graded.</summary>
    public sealed class FilmCamera
    {
        public readonly Camera Cam;
        readonly DepthOfField _dof;
        readonly ColorAdjustments _color;
        readonly Bloom _bloom;
        readonly Vignette _vignette;
        readonly WhiteBalance _white;

        public readonly Track3 Position = new(), Target = new(), Focus = new();
        public readonly Track Fov = new(32f), Roll = new(0f), Aperture = new(2.4f), Exposure = new(0f);
        /// <summary>A hand-held breath of movement, in millimetres and hundredths of a degree. 0 is a locked-off camera.</summary>
        public float Life = 1f;
        public float Seed;

        public FilmCamera(Transform parent)
        {
            var go = new GameObject("Film camera");
            go.transform.SetParent(parent, false);
            Cam = go.AddComponent<Camera>();
            Cam.enabled = false;
            Cam.stereoTargetEye = StereoTargetEyeMask.None;
            Cam.clearFlags = CameraClearFlags.Skybox;
            Cam.nearClipPlane = 0.03f;
            Cam.farClipPlane = 400f;
            Cam.allowHDR = true;
            Cam.allowMSAA = true;
            var data = Cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = true;
            data.requiresDepthTexture = true;
            data.dithering = true;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            _bloom = profile.Add<Bloom>(true);
            _bloom.threshold.Override(1.0f);
            _bloom.intensity.Override(0.55f);
            _bloom.scatter.Override(0.7f);
            _bloom.highQualityFiltering.Override(true);
            _vignette = profile.Add<Vignette>(true);
            _vignette.intensity.Override(0.26f);
            _vignette.smoothness.Override(0.55f);
            _color = profile.Add<ColorAdjustments>(true);
            _color.postExposure.Override(0f);
            _color.contrast.Override(6f);
            _color.saturation.Override(8f);
            _white = profile.Add<WhiteBalance>(true);
            _white.temperature.Override(6f);
            _white.tint.Override(2f);
            _dof = profile.Add<DepthOfField>(true);
            _dof.mode.Override(DepthOfFieldMode.Bokeh);
            _dof.focusDistance.Override(1.2f);
            _dof.focalLength.Override(45f);
            _dof.aperture.Override(2.4f);
            _dof.bladeCount.Override(7);
            var volume = new GameObject("Film grade").AddComponent<Volume>();
            volume.transform.SetParent(parent, false);
            volume.isGlobal = true;
            volume.priority = 10;
            volume.sharedProfile = profile;
        }

        /// <summary>The grade of a shot: warmth (white-balance temperature), saturation, contrast, vignette, bloom.</summary>
        public void Grade(float temperature = 6f, float saturation = 8f, float contrast = 6f, float vignette = 0.26f, float bloom = 0.55f, float tint = 2f)
        {
            _white.temperature.value = temperature;
            _white.tint.value = tint;
            _color.saturation.value = saturation;
            _color.contrast.value = contrast;
            _vignette.intensity.value = vignette;
            _bloom.intensity.value = bloom;
        }

        public void Apply(float t)
        {
            var position = Position.At(t);
            var target = Target.At(t);
            if (Life > 0f)
            {
                position += new Vector3(Drift.Wander(t, Seed + 1f), Drift.Wander(t, Seed + 2f), Drift.Wander(t, Seed + 3f)) * 0.0025f * Life;
                target += new Vector3(Drift.Wander(t, Seed + 4f), Drift.Wander(t, Seed + 5f), 0f) * 0.004f * Life;
            }
            var forward = target - position;
            if (forward.sqrMagnitude < 1e-8f) forward = Vector3.forward;
            Cam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward.normalized, Vector3.up) * Quaternion.Euler(0, 0, Roll.At(t)));
            var fov = Mathf.Clamp(Fov.At(t), 8f, 100f);
            Cam.fieldOfView = fov;
            var sharp = Focus.Empty ? target : Focus.At(t);
            _dof.focusDistance.value = Mathf.Max(0.12f, Vector3.Dot(sharp - position, Cam.transform.forward));
            _dof.focalLength.value = Mathf.Clamp(12f / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad), 12f, 200f);   // the lens that gives this view on a 24 mm frame
            _dof.aperture.value = Mathf.Clamp(Aperture.At(t), 1f, 32f);
            _color.postExposure.value = Exposure.At(t);
        }
    }

    /// <summary>How a thing sits in a hand, in the hand's own terms: x across (the palm's direction crossed with the
    /// fingers'), y the way the palm faces, z along the fingers; from the wrist.</summary>
    public readonly struct Grip
    {
        public readonly Vector3 Offset;
        public readonly Quaternion Rotation;
        public Grip(Vector3 offset, Quaternion rotation) { Offset = offset; Rotation = rotation; }

        /// <summary>The grip that has the thing at <paramref name="thing"/> when the wrist is at <paramref name="wrist"/>
        /// with the hand turned so (world directions).</summary>
        public static Grip From(Pose thing, Vector3 wrist, Vector3 fingers, Vector3 palm)
        {
            var q = Quaternion.Inverse(Quaternion.LookRotation(fingers, palm));
            return new Grip(q * (thing.position - wrist), q * thing.rotation);
        }

        /// <summary>Where the wrist has to be for the thing to be at <paramref name="thing"/> with the hand turned so.</summary>
        public Vector3 Wrist(Pose thing, Vector3 fingers, Vector3 palm) => thing.position - Quaternion.LookRotation(fingers, palm) * Offset;

        /// <summary>Where the thing is when the wrist is at <paramref name="wrist"/> with the hand turned so.</summary>
        public Pose Thing(Vector3 wrist, Vector3 fingers, Vector3 palm)
        {
            var q = Quaternion.LookRotation(fingers, palm);
            return new Pose(wrist + q * Offset, q * Rotation);
        }
    }

    public sealed class FilmStage
    {
        public readonly Transform Root;
        public readonly FilmCamera Camera;
        public readonly List<FilmActor> Actors = new();
        /// <summary>Anything else that moves: props carried by a hand, a light coming up, a screen changing. Called after the people are posed.</summary>
        public readonly List<Action<float>> Moves = new();
        public readonly Dictionary<string, Vector3> Anchors = new();
        /// <summary>The film's render pipeline, for a shot that needs its shadows to reach further or stay closer.</summary>
        public UniversalRenderPipelineAsset Pipeline;
        /// <summary>Run right before every frame is drawn, after everything is posed (a screen's picture, say).</summary>
        public readonly List<Action> Draws = new();
        /// <summary>Lines a shot wants in the render report for each still (a hand's distance from its goal, say).</summary>
        public readonly List<Func<string>> Notes = new();
        Cubemap _reflection;
        Vector3? _reflectFrom;
        float _reflectIntensity = 1f;
        float _time;
        static Shader _lit, _unlit;

        /// <summary>The time of the frame being posed.</summary>
        public float Now => _time;

        public FilmStage()
        {
            ForgetAppMeshes();
            Root = new GameObject("Film stage").transform;
            Camera = new FilmCamera(Root);
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.reflectionIntensity = 1f;
        }

        /// <summary>The app's props keep the meshes they build (cup, coaster, tent card) for next time; a mesh made for
        /// one shot does not outlive that shot's scene, so each shot has them built again.</summary>
        static void ForgetAppMeshes()
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            foreach (var name in new[] { "_cupMesh", "_saucerMesh", "_coasterMesh", "_ringMesh", "_potMesh" })
                typeof(SameSky.App.Props).GetField(name, flags)?.SetValue(null, null);
            (typeof(SameSky.App.Props).GetField("TentMeshes", flags)?.GetValue(null) as System.Collections.IDictionary)?.Clear();
        }

        public static Shader LitShader => _lit != null ? _lit : _lit = Shader.Find("Universal Render Pipeline/Lit");
        public static Shader UnlitShader => _unlit != null ? _unlit : _unlit = Shader.Find("Universal Render Pipeline/Unlit");

        // ------------------------------------------------------------ people
        public FilmActor Actor(string name, Vector3 position, float yaw, float seed = 0f)
        {
            var actor = new FilmActor(name, Root, position, yaw, seed);
            // (measured from the side, each looking at something level: how far the rest pose carries the head forward)
            if (name == "adam") { actor.SmileGain = 1.4f; actor.Lids = 0.2f; actor.FacePitch = 17f; }
            else { actor.Lids = 0.15f; actor.FacePitch = 4f; }
            FilmLook.Dress(actor);
            SetLayer(actor.Root, 0);
            Actors.Add(actor);
            return actor;
        }

        /// <summary>Adam or Maya standing, for the film that walks ("Wish You Were Here"): no seated loop, motion
        /// comes from clips (FilmActor.Play, Walk).</summary>
        public FilmActor Lead(string name, Vector3 position, float yaw, float seed = 0f)
        {
            var actor = new FilmActor(name, FilmCast.Lead(name, Root), Root, position, yaw, seed);
            if (name == "adam") { actor.SmileGain = 1.4f; actor.Lids = 0.2f; actor.FacePitch = 17f; }
            else { actor.Lids = 0.15f; actor.FacePitch = 4f; }
            FilmLook.Dress(actor, 0.5f, false, 0.3f);
            SetLayer(actor.Root, 0);
            Actors.Add(actor);
            return actor;
        }

        /// <summary>A background person (FilmCast.Extras), standing.</summary>
        public FilmActor Extra(string person, Vector3 position, float yaw, float seed = 0f, Color? tint = null)
        {
            var actor = new FilmActor(person, FilmCast.Extra(person, Root, tint), Root, position, yaw, seed);
            actor.AutoBlink = false;
            SetLayer(actor.Root, 0);
            Actors.Add(actor);
            return actor;
        }

        public static void SetLayer(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        // ------------------------------------------------------------ materials and simple shapes
        public static Material Mat(Color color, float smoothness = 0.3f, float metallic = 0f, Texture texture = null, Color? emission = null, Vector2? tiling = null)
        {
            var m = new Material(LitShader) { color = color };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (texture != null) { m.SetTexture("_BaseMap", texture); if (tiling.HasValue) m.SetTextureScale("_BaseMap", tiling.Value); }
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return m;
        }

        /// <summary>A surface that only glows (a screen, a window far away, the sun on water): no lighting falls on it.</summary>
        public static Material Glow(Color color, Texture texture = null)
        {
            var m = new Material(UnlitShader);
            m.SetColor("_BaseColor", color);
            if (texture != null) m.SetTexture("_BaseMap", texture);
            return m;
        }

        public GameObject Shape(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material, Transform parent = null, Vector3? euler = null, bool shadows = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var collider = go.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            go.transform.SetParent(parent != null ? parent : Root, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        public GameObject Box(string name, Vector3 centre, Vector3 size, Material material, Transform parent = null, Vector3? euler = null, bool shadows = true) =>
            Shape(PrimitiveType.Cube, name, centre, size, material, parent, euler, shadows);

        /// <summary>A cylinder by its height and diameter (Unity's is two units tall).</summary>
        public GameObject Cylinder(string name, Vector3 centre, float diameter, float height, Material material, Transform parent = null, Vector3? euler = null, bool shadows = true) =>
            Shape(PrimitiveType.Cylinder, name, centre, new Vector3(diameter, height / 2f, diameter), material, parent, euler, shadows);

        public GameObject Ball(string name, Vector3 centre, float diameter, Material material, Transform parent = null, bool shadows = true) =>
            Shape(PrimitiveType.Sphere, name, centre, Vector3.one * diameter, material, parent, null, shadows);

        /// <summary>A part with a mesh of its own (a rounded box, something lathed).</summary>
        public GameObject Part(string name, Mesh mesh, Vector3 position, Material material, Transform parent = null, Vector3? euler = null, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : Root, false);
            go.transform.localPosition = position;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        /// <summary>A box with soft edges: furniture, cushions, anything that should catch the light along its corners.</summary>
        public GameObject Soft(string name, Vector3 centre, Vector3 size, float radius, Material material, Transform parent = null, Vector3? euler = null, bool shadows = true) =>
            Part(name, FilmMesh.RoundedBox(size, radius), centre, material, parent, euler, shadows);

        /// <summary>A round bar from a to b.</summary>
        public GameObject Rod(string name, Vector3 a, Vector3 b, float diameter, Material material, Transform parent = null)
        {
            var go = Shape(PrimitiveType.Cylinder, name, (a + b) * 0.5f, new Vector3(diameter, (b - a).magnitude * 0.5f, diameter), material, parent);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            return go;
        }

        /// <summary>A see-through surface that only glows (clouds, a soft halo): no lighting, no depth.</summary>
        public static Material Sheer(Color color, Texture texture = null)
        {
            var m = new Material(UnlitShader);
            m.SetColor("_BaseColor", color);
            if (texture != null) m.SetTexture("_BaseMap", texture);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        /// <summary>Light added on top of what is behind (glints on water).</summary>
        public static Material Additive(Color color, Texture texture = null)
        {
            var m = Sheer(color, texture);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            return m;
        }

        /// <summary>A surface that only glows, cut out by its texture's alpha (a far skyline).</summary>
        public static Material Cutout(Color color, Texture texture)
        {
            var m = Glow(color, texture);
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = (int)RenderQueue.AlphaTest;
            return m;
        }

        /// <summary>A flat card facing <paramref name="normal"/> (a picture, a screen, a far skyline).</summary>
        public GameObject Card(string name, Vector3 centre, Vector2 size, Vector3 normal, Material material, Transform parent = null, bool shadows = false)
        {
            var go = Shape(PrimitiveType.Quad, name, centre, new Vector3(size.x, size.y, 1f), material, parent, null, shadows);
            go.transform.localRotation = Quaternion.LookRotation(-normal.normalized, Mathf.Abs(normal.normalized.y) > 0.99f ? Vector3.forward : Vector3.up);
            return go;
        }

        // ------------------------------------------------------------ light
        public Light Sun(Vector3 euler, Color color, float intensity, bool shadows = true, float strength = 0.85f)
        {
            var light = new GameObject("Sun").AddComponent<Light>();
            light.transform.SetParent(Root, false);
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(euler);
            light.color = color;
            light.intensity = intensity;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            light.shadowStrength = strength;
            light.shadowBias = 0.02f;
            light.shadowNormalBias = 0.3f;
            RenderSettings.sun = light;
            return light;
        }

        public Light Lamp(string name, Vector3 position, Color color, float intensity, float range, bool shadows = false)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(Root, false);
            light.type = LightType.Point;
            light.transform.position = position;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            light.shadowStrength = 0.7f;
            light.shadowBias = 0.03f;
            return light;
        }

        public Light Spot(string name, Vector3 position, Vector3 lookAt, Color color, float intensity, float range, float angle, bool shadows = false)
        {
            var light = Lamp(name, position, color, intensity, range, shadows);
            light.type = LightType.Spot;
            light.spotAngle = angle;
            light.innerSpotAngle = angle * 0.55f;
            light.transform.rotation = Quaternion.LookRotation((lookAt - position).normalized, Vector3.up);
            return light;
        }

        public void Ambient(Color sky, Color equator, Color ground)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
        }

        public void Haze(Color color, float density)
        {
            RenderSettings.fog = density > 0f;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = color;
            RenderSettings.fogDensity = density;
        }

        /// <summary>What glossy things reflect: the set itself, seen once from <paramref name="from"/> when the shot
        /// is ready (everything built and posed for its first frame).</summary>
        public void Reflections(Vector3 from, float intensity = 1f)
        {
            _reflectFrom = from;
            _reflectIntensity = intensity;
        }

        /// <summary>Called by the renderer once the shot has built its stage.</summary>
        public void Ready()
        {
            Apply(0f, 0f);
            if (!_reflectFrom.HasValue) return;
            if (_reflection == null) _reflection = new Cubemap(256, TextureFormat.RGBAHalf, true) { name = "Film reflection" };
            var probe = new GameObject("Reflection camera").AddComponent<Camera>();
            probe.transform.position = _reflectFrom.Value;
            probe.enabled = false;
            probe.allowHDR = true;
            probe.nearClipPlane = 0.05f;
            probe.farClipPlane = 300f;
            var ok = false;
            try { ok = probe.RenderToCubemap(_reflection); }
            catch (Exception e) { Debug.LogWarning("[SameSky:film] no reflection: " + e.Message); }
            UnityEngine.Object.DestroyImmediate(probe.gameObject);
            if (!ok) { Debug.LogWarning("[SameSky:film] the set could not be drawn into a reflection: glossy things reflect the sky"); return; }
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = _reflection;
            RenderSettings.reflectionIntensity = _reflectIntensity;
        }

        public void BeforeDraw()
        {
            foreach (var draw in Draws) draw();
        }

        // ------------------------------------------------------------ things in hands
        /// <summary>A thing is in a hand from <paramref name="from"/> to <paramref name="until"/> (held the way
        /// <paramref name="grip"/> says), lying at <paramref name="before"/> until then and at <paramref name="after"/>
        /// afterwards. The hand's keys are written with <see cref="Grip.Wrist"/> so it arrives where the thing is.</summary>
        public void Hold(Transform thing, FilmArm arm, Grip grip, float from, float until, Pose before, Pose after, float blend = 0.1f)
        {
            Moves.Add(t =>
            {
                var q = Quaternion.LookRotation(arm.FingersNow, arm.PalmNow);
                var held = new Pose(arm.Hand.position + q * grip.Offset, q * grip.Rotation);
                Pose pose;
                if (t < from - blend) pose = before;
                else if (t < from) pose = Blend(before, held, Mathf.SmoothStep(0f, 1f, (t - (from - blend)) / blend));
                else if (t <= until) pose = held;
                else if (t < until + blend) pose = Blend(held, after, Mathf.SmoothStep(0f, 1f, (t - until) / blend));
                else pose = after;
                thing.SetPositionAndRotation(pose.position, pose.rotation);
            });
        }

        static Pose Blend(Pose a, Pose b, float k) => new(Vector3.Lerp(a.position, b.position, k), Quaternion.Slerp(a.rotation, b.rotation, k));

        // ------------------------------------------------------------ the lounge, as the app builds it
        /// <summary>The app's own lounge (walls, curved window with the skyline, the table and chairs as a room comes),
        /// copied out of the app's scene. Its anchors (Seat, PartnerSeat, Cup, TableTop, Screen...) land in Anchors.</summary>
        public Transform Lounge()
        {
            var film = SceneManager.GetActiveScene();
            var app = EditorSceneManager.OpenScene("Assets/SameSky/Scenes/SameSky.unity", OpenSceneMode.Additive);
            Transform lounge = null;
            foreach (var root in app.GetRootGameObjects())
            {
                if (root.name != "Room") continue;
                var source = root.transform.Find("Lounge");
                lounge = UnityEngine.Object.Instantiate(source.gameObject, Root).transform;
                lounge.name = "Lounge";
                var anchors = root.transform.Find("Anchors");
                if (anchors != null) foreach (Transform a in anchors) Anchors[a.name] = a.position;
            }
            SceneManager.SetActiveScene(app);
            var sky = RenderSettings.skybox;
            SceneManager.SetActiveScene(film);
            EditorSceneManager.CloseScene(app, true);
            if (lounge == null) throw new InvalidOperationException("the app's scene has no Room/Lounge");
            RenderSettings.skybox = sky;
            // the room as it comes (the scene keeps only the first piece of every slot switched on); the shot lights it
            foreach (var light in lounge.GetComponentsInChildren<Light>(true)) light.gameObject.SetActive(false);
            return lounge;
        }

        // ------------------------------------------------------------ frame by frame
        public void Apply(float t, float dt)
        {
            _time = t;
            Shader.SetGlobalFloat("_FilmTime", t);
            foreach (var actor in Actors) actor.Update(t, dt);
            foreach (var move in Moves) move(t);
            Camera.Apply(t);
        }

        public void Dispose()
        {
            foreach (var actor in Actors) actor.Dispose();
        }
    }
}
