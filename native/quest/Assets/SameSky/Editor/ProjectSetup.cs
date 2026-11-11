// One-time, repeatable project configuration and scene build for the native Quest app.
//   Unity -batchmode -projectPath native/quest -executeMethod SameSky.EditorTools.ProjectSetup.Run -quit
// (scripts/native/bootstrap.ps1 runs it). Safe to run again: it rebuilds the scene and re-applies settings.
//
// * Player: package com.samesky.quest, IL2CPP + ARM64, Vulkan, linear colour, ASTC, HTTP only in development builds
// * XR: OpenXR on Android (Meta Quest support) and on Windows (Meta XR Simulator), hand tracking
// * URP tuned for a standalone headset (4x MSAA, no HDR, no post-processing, one shadowless main light)
// * Scene: the lounge (CC0 props), anchors the app uses, the Meta Interaction SDK comprehensive rig, the app root

using System;
using System.IO;
using System.Linq;
using SameSky.App;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SameSky.EditorTools
{
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/SameSky/Scenes/SameSky.unity";
        const string Art = "Assets/SameSky/Art";
        const string Settings = "Assets/SameSky/Settings";
        const string Materials = "Assets/SameSky/Resources/Materials";

        [MenuItem("SameSky/Set up project and scene")]
        public static void Run()
        {
            Log("setup start");
            ImportTmpEssentials();
            ConfigurePlayer();
            ConfigureRendering();
            XrSetup.Configure();
            ConfigureImports();
            CreateMaterials();
            BuildScene();
            AssetDatabase.SaveAssets();
            Log("setup done");
        }

        static void Log(string s) => Debug.Log("[SameSky:setup] " + s);

        // ------------------------------------------------------------ TextMeshPro

        static void ImportTmpEssentials()
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0) return;
            var importer = Type.GetType("TMPro.TMP_PackageResourceImporter, Unity.TextMeshPro.Editor");
            var method = importer?.GetMethod("ImportResources", new[] { typeof(bool), typeof(bool), typeof(bool) });
            if (method != null) method.Invoke(null, new object[] { true, false, false });
            else
            {
                // Fallback: import the essential resources package that ships with uGUI 2.x
                var pkg = Directory.GetFiles(Path.GetFullPath("Packages/com.unity.ugui"), "TMP Essential Resources.unitypackage", SearchOption.AllDirectories).FirstOrDefault()
                          ?? Directory.GetFiles(Path.GetFullPath("Library/PackageCache"), "TMP Essential Resources.unitypackage", SearchOption.AllDirectories).FirstOrDefault();
                if (pkg != null) AssetDatabase.ImportPackage(pkg, false);
            }
            AssetDatabase.Refresh();
            Log("TMP essentials imported");
        }

        // ------------------------------------------------------------ player

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "SameSky";
            PlayerSettings.productName = "SameSky";
            PlayerSettings.bundleVersion = "0.9.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.samesky.quest");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.samesky.quest");
            PlayerSettings.Android.bundleVersionCode = 9;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.runInBackground = true; // Windows simulator builds keep running when the simulator window has focus
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            Log("player settings applied");
        }

        // ------------------------------------------------------------ URP for a standalone headset

        static void ConfigureRendering()
        {
            Directory.CreateDirectory(Settings);
            var rendererPath = $"{Settings}/URP-Quest-Renderer.asset";
            var pipelinePath = $"{Settings}/URP-Quest.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (urp == null)
            {
                urp = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(urp, pipelinePath);
            }
            urp.msaaSampleCount = 4;
            urp.supportsHDR = false;
            urp.renderScale = 1f;
            urp.shadowDistance = 4f;
            urp.supportsCameraDepthTexture = false;
            urp.supportsCameraOpaqueTexture = false;
            EditorUtility.SetDirty(urp);
            GraphicsSettings.defaultRenderPipeline = urp;
            for (var i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            Log("URP configured");
        }

        // ------------------------------------------------------------ import settings for the bundled art and sample

        static void ConfigureImports()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Art, "Assets/SameSky/Resources/Sample" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter ti) continue;
                var changed = false;
                var isNormal = path.Contains("nor_gl") || path.EndsWith("/nor_gl.jpg");
                if (isNormal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; changed = true; }
                if (path.Contains("/Sample/Media/") && ti.maxTextureSize != 2048) { ti.maxTextureSize = 2048; ti.mipmapEnabled = true; changed = true; }
                if (path.EndsWith(".hdr") && ti.textureShape != TextureImporterShape.TextureCube) { ti.textureShape = TextureImporterShape.TextureCube; changed = true; }
                var android = ti.GetPlatformTextureSettings("Android");
                if (!android.overridden || android.format != TextureImporterFormat.ASTC_6x6)
                {
                    android.overridden = true;
                    android.format = TextureImporterFormat.ASTC_6x6;
                    android.maxTextureSize = path.Contains("/Sample/Media/") ? 2048 : 1024;
                    ti.SetPlatformTextureSettings(android);
                    changed = true;
                }
                if (changed) ti.SaveAndReimport();
            }
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/SameSky/Resources/Sample" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not AudioImporter ai) continue;
                var s = ai.defaultSampleSettings;
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.6f;
                ai.defaultSampleSettings = s;
                ai.SaveAndReimport();
            }
            Log("imports configured");
        }

        // ------------------------------------------------------------ materials (URP Lit, shipped via Resources)

        static Material Lit(string name, Color color, float smoothness, string albedo = null, string normal = null, Vector2? tiling = null)
        {
            Directory.CreateDirectory(Materials);
            var path = $"{Materials}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            if (albedo != null) m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(albedo));
            if (normal != null)
            {
                m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
                m.EnableKeyword("_NORMALMAP");
            }
            if (tiling.HasValue) m.SetTextureScale("_BaseMap", tiling.Value);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void CreateMaterials()
        {
            Directory.CreateDirectory(Materials);
            AssetDatabase.Refresh();
            var panelPath = $"{Materials}/Panel.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(panelPath) == null)
                AssetDatabase.CreateAsset(new Material(Shader.Find("SameSky/RoundedPanel")) { enableInstancing = true }, panelPath);
            Lit("Ceramic", new Color(0.94f, 0.92f, 0.88f), 0.82f);
            Lit("Walnut", new Color(0.84f, 0.63f, 0.48f), 0.45f, $"{Art}/Textures/american_walnut_veneer/Diffuse.jpg", $"{Art}/Textures/american_walnut_veneer/nor_gl.jpg");
            Lit("Parquet", new Color(0.92f, 0.86f, 0.8f), 0.35f, $"{Art}/Textures/herringbone_parquet/Diffuse.jpg", $"{Art}/Textures/herringbone_parquet/nor_gl.jpg", new Vector2(3, 3));
            Lit("Plaster", new Color(0.93f, 0.91f, 0.88f), 0.08f, $"{Art}/Textures/clay_plaster/Diffuse.jpg", $"{Art}/Textures/clay_plaster/nor_gl.jpg", new Vector2(4, 2));
            Lit("Boucle", new Color(0.86f, 0.83f, 0.78f), 0.05f, null, $"{Art}/Textures/wool_boucle/nor_gl.jpg", new Vector2(4, 4));
            var window = Lit("Window", new Color(0.91f, 0.93f, 0.97f), 0f, $"{Art}/Textures/window-skyline.jpg");
            window.SetColor("_EmissionColor", new Color(0.75f, 0.75f, 0.78f));
            window.EnableKeyword("_EMISSION");
            window.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Art}/Textures/window-skyline.jpg"));
            Log("materials created");
        }

        // ------------------------------------------------------------ the scene

        static void BuildScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var room = new GameObject("Room");
            var anchors = new GameObject("Anchors").transform;
            anchors.SetParent(room.transform, false);
            Transform A(string name, Vector3 pos, Vector3 euler = default)
            {
                var t = new GameObject(name).transform;
                t.SetParent(anchors, false);
                t.localPosition = pos;
                t.localRotation = Quaternion.Euler(euler);
                return t;
            }

            // Seated layout (metres). The person sits at the origin facing +Z; the table is in front of them.
            A("Seat", Vector3.zero);
            A("SeatEye", new Vector3(0, 1.18f, 0));
            A("TableTop", new Vector3(0, 0.74f, 0.72f));
            A("Cup", new Vector3(0.15f, 0.745f, 0.45f));
            A("ReplySpot", new Vector3(0.28f, 0.745f, 0.47f));
            A("FrameLeft", new Vector3(-0.2f, 0.745f, 0.62f), new Vector3(0, -12, 0));
            A("FrameRight", new Vector3(0.24f, 0.745f, 0.86f), new Vector3(0, 14, 0));
            A("PartnerSeat", new Vector3(0, 0, 1.45f), new Vector3(0, 180, 0));
            A("Screen", new Vector3(-0.78f, 1.22f, 1.18f), new Vector3(0, -32, 0));
            A("Dock", new Vector3(0, 0.83f, 0.33f), new Vector3(38, 0, 0));
            A("Panel", new Vector3(0, 1.02f, 0.5f));
            A("Keyboard", new Vector3(0, 0.86f, 0.37f));
            A("PreviewSpot", new Vector3(0.9f, 0, 1.05f), new Vector3(0, -140, 0));
            A("Shelf", new Vector3(1.05f, 0.95f, 0.95f), new Vector3(0, -70, 0));

            BuildLounge(room.transform);
            BuildLighting(room.transform);

            // Meta Interaction SDK comprehensive rig (OVRCameraRig + hand/controller interactors), under a movable root
            var playerRoot = new GameObject("PlayerRoot");
            InteractionRigSetup.AddRig(playerRoot.transform);

            var app = new GameObject("SameSky");
            app.AddComponent<SameSkyApp>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Log("scene saved");
        }

        static GameObject Place(string model, Transform parent, Vector3 pos, float yaw, float scale = 1f, string name = null)
        {
            var path = AssetDatabase.FindAssets("t:GameObject", new[] { $"{Art}/Models/{model}" })
                .Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => p.EndsWith(".gltf") || p.EndsWith(".glb"));
            var asset = path != null ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
            GameObject go;
            if (asset != null) go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            else
            {
                Debug.LogWarning($"[SameSky:setup] model {model} missing; using a placeholder");
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(parent, false);
            }
            go.name = name ?? model;
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.transform.localScale = Vector3.one * scale;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.receiveGI = ReceiveGI.LightProbes;
            return go;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, string material, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{Materials}/{material}.mat");
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
            return go;
        }

        static void BuildLounge(Transform room)
        {
            var geo = new GameObject("Lounge").transform;
            geo.SetParent(room, false);
            Prim(PrimitiveType.Plane, "Floor", geo, new Vector3(0, 0, 0.8f), new Vector3(0.7f, 1, 0.7f), "Parquet");
            Prim(PrimitiveType.Cylinder, "Rug", geo, new Vector3(0, 0.004f, 0.75f), new Vector3(2.1f, 0.004f, 2.1f), "Boucle");
            // walls: back wall with a wide window behind the partner, side walls, a wall behind the person
            Prim(PrimitiveType.Cube, "Wall back left", geo, new Vector3(-2.2f, 1.4f, 3.2f), new Vector3(1.6f, 2.8f, 0.12f), "Plaster");
            Prim(PrimitiveType.Cube, "Wall back right", geo, new Vector3(2.2f, 1.4f, 3.2f), new Vector3(1.6f, 2.8f, 0.12f), "Plaster");
            Prim(PrimitiveType.Cube, "Wall under window", geo, new Vector3(0, 0.4f, 3.2f), new Vector3(2.8f, 0.8f, 0.12f), "Plaster");
            Prim(PrimitiveType.Cube, "Wall over window", geo, new Vector3(0, 2.55f, 3.2f), new Vector3(2.8f, 0.5f, 0.12f), "Plaster");
            Prim(PrimitiveType.Quad, "Window view", geo, new Vector3(0, 1.55f, 3.5f), new Vector3(3.4f, 1.6f, 1), "Window");
            Prim(PrimitiveType.Cube, "Wall left", geo, new Vector3(-3f, 1.4f, 0.8f), new Vector3(0.12f, 2.8f, 5f), "Plaster");
            Prim(PrimitiveType.Cube, "Wall right", geo, new Vector3(3f, 1.4f, 0.8f), new Vector3(0.12f, 2.8f, 5f), "Plaster");
            Prim(PrimitiveType.Cube, "Wall behind", geo, new Vector3(0, 1.4f, -1.6f), new Vector3(6f, 2.8f, 0.12f), "Plaster");
            Prim(PrimitiveType.Cube, "Ceiling", geo, new Vector3(0, 2.8f, 0.8f), new Vector3(6f, 0.05f, 5f), "Plaster");
            // furniture (CC0 Poly Haven): the shared table, the partner's armchair, side table, plants, vase
            Place("table", geo, new Vector3(0, 0, 0.72f), 0);
            Place("armchair", geo, new Vector3(0, 0, 1.55f), 180, 1f, "Partner armchair");
            Place("side-table", geo, new Vector3(1.05f, 0, 0.95f), 0);
            Place("vase", geo, new Vector3(1.05f, 0.62f, 0.95f), 0);
            Place("plant", geo, new Vector3(-1.4f, 0, 2.8f), 30);
            Place("plant", geo, new Vector3(1.5f, 0, 2.8f), -40);
            // keepsake shelf (walnut) beside the side table
            Prim(PrimitiveType.Cube, "Shelf board low", geo, new Vector3(1.7f, 0.8f, 1.3f), new Vector3(0.6f, 0.025f, 0.24f), "Walnut", new Vector3(0, -70, 0));
            Prim(PrimitiveType.Cube, "Shelf board high", geo, new Vector3(1.7f, 1.15f, 1.3f), new Vector3(0.6f, 0.025f, 0.24f), "Walnut", new Vector3(0, -70, 0));
        }

        static void BuildLighting(Transform room)
        {
            var sun = new GameObject("Window light").AddComponent<Light>();
            sun.transform.SetParent(room, false);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.88f);
            sun.intensity = 1.05f;
            sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(38, 200, 0);
            sun.lightmapBakeType = LightmapBakeType.Mixed;

            var lamp = new GameObject("Lamp glow").AddComponent<Light>();
            lamp.transform.SetParent(room, false);
            lamp.transform.position = new Vector3(1.05f, 1.25f, 0.95f);
            lamp.type = LightType.Point;
            lamp.range = 3f;
            lamp.intensity = 0.8f;
            lamp.color = new Color(1f, 0.85f, 0.68f);
            lamp.lightmapBakeType = LightmapBakeType.Baked;

            var hdr = AssetDatabase.LoadAssetAtPath<Cubemap>($"{Art}/Env/lythwood_lounge_1k.hdr");
            if (hdr != null)
            {
                var sky = new Material(Shader.Find("Skybox/Cubemap"));
                sky.SetTexture("_Tex", hdr);
                sky.SetFloat("_Exposure", 0.9f);
                AssetDatabase.CreateAsset(sky, $"{Settings}/Lounge Sky.mat");
                RenderSettings.skybox = sky;
            }
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 0.9f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;

            var probe = new GameObject("Reflection probe").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(room, false);
            probe.transform.position = new Vector3(0, 1.2f, 0.8f);
            probe.size = new Vector3(6, 3, 5);
            probe.mode = ReflectionProbeMode.Baked;

            var probes = new GameObject("Light probes").AddComponent<LightProbeGroup>();
            probes.transform.SetParent(room, false);
            var pts = new System.Collections.Generic.List<Vector3>();
            for (var x = -1.5f; x <= 1.5f; x += 0.75f)
                for (var y = 0.3f; y <= 1.8f; y += 0.5f)
                    for (var z = -0.5f; z <= 2.5f; z += 0.75f)
                        pts.Add(new Vector3(x, y, z));
            probes.probePositions = pts.ToArray();
        }
    }
}
