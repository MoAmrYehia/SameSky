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
            FontSetup.Run();
            CreateMaterials();
            CharacterSetup.Run();
            Log("characters: Rocketbox stock adults for the sample's fictional people");
            BuildScene();
            AssetDatabase.SaveAssets();
            Log("setup done");
        }

        static void Log(string s) => Debug.Log("[SameSky:setup] " + s);

        // ------------------------------------------------------------ TextMeshPro

        static void ImportTmpEssentials()
        {
            if (File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset")) return;
            // uGUI 2.x ships TMP's essential resources (default font, settings, shaders) as a .unitypackage in the
            // package folder; import it non-interactively (synchronous in batch mode).
            var ugui = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui/package.json");
            var pkg = ugui != null ? Path.Combine(ugui.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage") : null;
            if (pkg == null || !File.Exists(pkg)) throw new InvalidOperationException("TMP Essential Resources.unitypackage not found in com.unity.ugui");
            // AssetDatabase.ImportPackage is asynchronous and doesn't finish inside a batch-mode -executeMethod run,
            // so unpack it ourselves: a .unitypackage is a gzipped tar of <guid>/{pathname, asset, asset.meta}.
            ExtractUnityPackage(pkg);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (!File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
                throw new InvalidOperationException("TMP essentials did not import (Assets/TextMesh Pro/Resources/TMP Settings.asset missing)");
            Log("TMP essentials imported");
        }

        static void ExtractUnityPackage(string package)
        {
            var entries = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, byte[]>>();
            using (var gz = new System.IO.Compression.GZipStream(File.OpenRead(package), System.IO.Compression.CompressionMode.Decompress))
            {
                var header = new byte[512];
                while (ReadFully(gz, header) == 512)
                {
                    var name = System.Text.Encoding.ASCII.GetString(header, 0, 100).TrimEnd('\0', ' ');
                    if (name.Length == 0) break;                       // end-of-archive block
                    var prefix = System.Text.Encoding.ASCII.GetString(header, 345, 155).TrimEnd('\0', ' ');
                    if (prefix.Length > 0) name = prefix + "/" + name;
                    var sizeText = System.Text.Encoding.ASCII.GetString(header, 124, 12).TrimEnd('\0', ' ');
                    var size = sizeText.Length > 0 ? Convert.ToInt64(sizeText, 8) : 0;
                    var data = new byte[size];
                    ReadFully(gz, data);
                    var pad = (512 - size % 512) % 512;
                    if (pad > 0) ReadFully(gz, new byte[pad]);
                    if (header[156] != (byte)'0' && header[156] != 0) continue; // regular files only
                    var parts = name.TrimStart('.', '/').Split('/');
                    if (parts.Length != 2) continue;
                    if (!entries.TryGetValue(parts[0], out var files)) entries[parts[0]] = files = new System.Collections.Generic.Dictionary<string, byte[]>();
                    files[parts[1]] = data;
                }
            }
            foreach (var files in entries.Values)
            {
                if (!files.TryGetValue("pathname", out var pathBytes)) continue;
                var path = System.Text.Encoding.UTF8.GetString(pathBytes).Split('\n')[0].Trim();
                if (!path.StartsWith("Assets/")) continue;
                if (files.TryGetValue("asset", out var asset))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllBytes(path, asset);
                }
                else Directory.CreateDirectory(path);
                if (files.TryGetValue("asset.meta", out var meta)) File.WriteAllBytes(path + ".meta", meta);
            }
        }

        static int ReadFully(Stream s, byte[] buffer)
        {
            var read = 0;
            while (read < buffer.Length)
            {
                var n = s.Read(buffer, read, buffer.Length - read);
                if (n == 0) break;
                read += n;
            }
            return read;
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
            // glTFast's .gltf/.glb importer is editor code compiled with the active build target's defines, so both targets
            // need this define. With it, GltfImporter registers as the default importer for those extensions
            // (glTFast Editor/Scripts/GltfImporter.cs). Without it, the first setup run imported them with DefaultImporter
            // and BuildScene placed placeholders.
            AddScriptingDefine(NamedBuildTarget.Android, GltfDefaultImporterDefine);
            AddScriptingDefine(NamedBuildTarget.Standalone, GltfDefaultImporterDefine);
            // Shader variants compile in the background in the Editor (compiling them on the main thread stalls Play
            // mode for seconds at a time whenever lighting features change, which breaks timed checks). Until a variant
            // is ready it draws as a cyan stand-in; the probe's renders wait for compilation to finish (TestProbe.Snap).
            EditorSettings.asyncShaderCompilation = true;
            Log("player settings applied");
        }

        const string GltfDefaultImporterDefine = "GLTFAST_FORCE_DEFAULT_IMPORTER_ON";

        /// <summary>Adds <paramref name="define"/> to the target's scripting defines, keeping the ones already set.</summary>
        static void AddScriptingDefine(NamedBuildTarget target, string define)
        {
            PlayerSettings.GetScriptingDefineSymbols(target, out var defines);
            if (defines.Contains(define)) return;
            PlayerSettings.SetScriptingDefineSymbols(target, defines.Append(define).ToArray());
            // Defines take effect when scripts recompile after this run; Unity then re-imports the glTF files.
            Log($"added scripting define {define} for {target.TargetName}; run the setup again after the recompile so the scene uses the imported models");
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
            // One soft shadow map from the window light covers the whole room (it is about 5 m from the seat to the
            // glazing): one cascade, 2048, the cheapest soft filter. Cost on a Quest is still to be measured.
            urp.shadowDistance = 9f;
            urp.shadowCascadeCount = 1;
            urp.supportsCameraDepthTexture = false;
            urp.supportsCameraOpaqueTexture = false;
            var so = new SerializedObject(urp);
            void Set(string property, int value)
            {
                var p = so.FindProperty(property);
                if (p == null) return;
                if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = value != 0;
                else p.intValue = value;
            }
            Set("m_MainLightShadowsSupported", 1);
            Set("m_MainLightShadowmapResolution", 2048);
            Set("m_SoftShadowsSupported", 1);
            Set("m_SoftShadowQuality", 1);   // Low
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);
            GraphicsSettings.defaultRenderPipeline = urp;
            for (var i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
                QualitySettings.realtimeReflectionProbes = true;   // the room's one reflection, drawn once at start
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

        /// <summary>A small tileable speckle texture (glazed ceramic, cork), saved under Art/Generated. density is the
        /// share of pixels that take the second colour.</summary>
        static string NoiseTexture(string name, Color baseColor, Color speck, float density, int size)
        {
            var dir = "Assets/SameSky/Art/Generated";
            Directory.CreateDirectory(dir);
            var path = $"{dir}/{name}.png";
            if (!File.Exists(path))
            {
                var rng = new System.Random(name.GetHashCode());
                var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
                for (var y = 0; y < size; y++)
                    for (var x = 0; x < size; x++)
                    {
                        var grain = (float)rng.NextDouble();
                        var c = Color.Lerp(baseColor, speck, grain < density ? 0.55f + 0.45f * (float)rng.NextDouble() : grain * 0.06f);
                        tex.SetPixel(x, y, c);
                    }
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            return path;
        }

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
            // the same panel writing depth, for nearly solid backgrounds (Ui.SolidPanelMaterial)
            var solidPath = $"{Materials}/PanelSolid.mat";
            var solid = AssetDatabase.LoadAssetAtPath<Material>(solidPath);
            if (solid == null)
            {
                solid = new Material(Shader.Find("SameSky/RoundedPanel")) { enableInstancing = true };
                AssetDatabase.CreateAsset(solid, solidPath);
            }
            solid.SetFloat("_ZWrite", 1f);
            EditorUtility.SetDirty(solid);
            // the room's own materials are made by LoungeBuilder; these are the ones props load at runtime too
            var ceramic = Lit("Ceramic", new Color(0.95f, 0.93f, 0.89f), 0.6f, NoiseTexture("ceramic-speckle", new Color(0.97f, 0.95f, 0.91f), new Color(0.45f, 0.4f, 0.34f), 0.012f, 256));
            ceramic.SetTextureScale("_BaseMap", new Vector2(3, 2));
            // black emission, lifted per object (the cup's restrained glow while a hand is near or holds it)
            ceramic.EnableKeyword("_EMISSION");
            ceramic.SetColor("_EmissionColor", Color.black);
            ceramic.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            Lit("Cork", new Color(0.95f, 0.88f, 0.78f), 0.08f, NoiseTexture("cork", new Color(0.72f, 0.57f, 0.4f), new Color(0.48f, 0.35f, 0.22f), 0.22f, 256));
            Lit("Paper", new Color(0.97f, 0.95f, 0.9f), 0.12f);
            Lit("Photo", Color.white, 0.42f);   // prints in the frames; each frame sets its own photo
            Lit("Leaf", new Color(0.33f, 0.47f, 0.3f), 0.25f);   // the keepsake planter's leaves
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
            A("TableTop", new Vector3(0, 0.774f, 0.72f));
            A("Cup", new Vector3(0.15f, 0.778f, 0.45f));
            A("ReplySpot", new Vector3(0.3f, 0.778f, 0.6f));   // replies line up behind the cup, clear of the reply card
            A("FrameLeft", new Vector3(-0.2f, 0.778f, 0.62f), new Vector3(0, -12, 0));
            A("FrameRight", new Vector3(0.24f, 0.778f, 0.86f), new Vector3(0, 14, 0));
            A("PartnerSeat", new Vector3(0, 0, 1.45f), new Vector3(0, 180, 0));
            A("Screen", new Vector3(-0.78f, 1.22f, 1.18f), new Vector3(0, -32, 0));
            A("Dock", new Vector3(0, 0.865f, 0.3f), new Vector3(38, 0, 0));   // at the table's near edge, below the cup's cards in the view
            A("Panel", new Vector3(0, 1.07f, 0.46f));
            A("Keyboard", new Vector3(0, 0.9f, 0.36f));
            A("PreviewSpot", new Vector3(0.9f, 0, 1.05f), new Vector3(0, -140, 0));
            A("Shelf", new Vector3(-2.47f, 1.045f, 0.7f), new Vector3(0, 90, 0));   // the lower walnut shelf in the alcove

            LoungeBuilder.Build(room.transform);
            LoungeBuilder.Light(room.transform);

            // Meta Interaction SDK comprehensive rig (OVRCameraRig + hand/controller interactors), under a movable root
            var playerRoot = new GameObject("PlayerRoot");
            InteractionRigSetup.AddRig(playerRoot.transform);

            var app = new GameObject("SameSky");
            app.AddComponent<SameSkyApp>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Log("scene saved");
        }
    }
}
