// Renders the animated trailer's shots, frame by frame, in Edit mode: each shot (FilmShots.cs) builds its stage in an
// empty scene, the stage is posed for every 24th of a second, the film camera draws it with a film-grade copy of the
// app's render pipeline (HDR, soft shadows, ambient occlusion, depth of field, bloom, a grade), and the frames go
// straight to FFmpeg. Nothing is played or captured from the running app, and no project setting is changed on disk:
// the pipeline copy lives in memory for the length of a render.
//
//   one-off, batch:   Unity -batchmode -projectPath native/quest -executeMethod SameSky.Film.FilmRenderer.Run
//                           -filmShots s01,s02|all|media [-filmAction render|frames|stills] [-filmStills 0,1.5,3]
//                           [-filmScale 2] [-filmSize 960x540] [-filmFfmpeg <exe>]
//                     (scripts/demo/trailer.ps1 runs the whole film this way)
//   render console:   Unity -batchmode -projectPath native/quest -filmConsole   (scripts/demo/trailer.ps1 talks to it
//                           through demo-build/film/console/request.json; it recompiles on request, so shots can be
//                           changed and looked at again without starting Unity each time)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

namespace SameSky.Film
{
    [Serializable]
    public sealed class FilmRequest
    {
        public string id = "", action = "stills", shots = "", times = "";
        public int width = 1920, height = 1080, scale = 1, crf = 12;
        public bool refresh;
        public string ffmpeg = "", outDir = "";
    }

    /// <summary>The film's copy of the app's render pipeline, in use while this object lives.</summary>
    public sealed class FilmPipeline : IDisposable
    {
        readonly RenderPipelineAsset _default, _quality;
        readonly bool _async;
        public readonly UniversalRenderPipelineAsset Asset;
        readonly UniversalRendererData _renderer;
        readonly List<UnityEngine.Object> _made = new();

        public FilmPipeline()
        {
            _default = GraphicsSettings.defaultRenderPipeline;
            _quality = QualitySettings.renderPipeline;
            _async = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;   // no placeholder shaders in the first frames
            var source = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/SameSky/Settings/URP-Quest.asset");
            var rendererSource = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/SameSky/Settings/URP-Quest-Renderer.asset");
            if (source == null || rendererSource == null) throw new InvalidOperationException("the app's render pipeline assets are missing");
            _renderer = UnityEngine.Object.Instantiate(rendererSource);
            _renderer.name = "Film renderer";
            _renderer.hideFlags = HideFlags.DontSave;
            _renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            if (_renderer.postProcessData == null) Debug.LogWarning("[SameSky:film] no post-process data: the grade and depth of field are off");
            AddAmbientOcclusion();
            // clustered lighting ("Forward+"): no limit of a few lights on each object, so a street of lamps lights
            // the one long pavement under it
            var rendererData = new SerializedObject(_renderer);
            var mode = rendererData.FindProperty("m_RenderingMode");
            if (mode != null) { mode.intValue = 2; rendererData.ApplyModifiedPropertiesWithoutUndo(); }
            else Debug.LogWarning("[SameSky:film] this renderer has no rendering mode setting: lights stay limited per object");
            Asset = UnityEngine.Object.Instantiate(source);
            Asset.name = "Film pipeline";
            Asset.hideFlags = HideFlags.DontSave;
            _made.Add(_renderer);
            _made.Add(Asset);
            var so = new SerializedObject(Asset);
            void Int(string name, int v) { var p = so.FindProperty(name); if (p != null) p.intValue = v; else Debug.LogWarning("[SameSky:film] pipeline has no " + name); }
            void Flag(string name, bool v) { var p = so.FindProperty(name); if (p != null) p.boolValue = v; else Debug.LogWarning("[SameSky:film] pipeline has no " + name); }
            void Number(string name, float v) { var p = so.FindProperty(name); if (p != null) p.floatValue = v; else Debug.LogWarning("[SameSky:film] pipeline has no " + name); }
            Flag("m_SupportsHDR", true);
            Int("m_HDRColorBufferPrecision", 1);
            Int("m_MSAA", 4);
            Flag("m_RequireDepthTexture", true);
            Flag("m_MainLightShadowsSupported", true);
            Int("m_MainLightShadowmapResolution", 4096);
            Number("m_ShadowDistance", 14f);
            Int("m_ShadowCascadeCount", 2);
            Number("m_Cascade2Split", 0.3f);
            Flag("m_SoftShadowsSupported", true);
            Int("m_SoftShadowQuality", 3);
            Int("m_AdditionalLightsRenderingMode", 1);
            Int("m_AdditionalLightsPerObjectLimit", 8);
            Flag("m_AdditionalLightShadowsSupported", true);
            Int("m_AdditionalLightsShadowmapResolution", 4096);
            Int("m_ColorGradingMode", 1);
            Int("m_ColorGradingLutSize", 32);
            var list = so.FindProperty("m_RendererDataList");
            if (list != null && list.arraySize > 0) list.GetArrayElementAtIndex(0).objectReferenceValue = _renderer;
            so.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = Asset;
            QualitySettings.renderPipeline = Asset;
        }

        /// <summary>Soft contact shading where things meet (URP's screen-space ambient occlusion), added to the copy.</summary>
        void AddAmbientOcclusion()
        {
            try
            {
                var type = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
                if (type == null) { Debug.LogWarning("[SameSky:film] no ambient occlusion feature in this URP"); return; }
                var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
                feature.name = "Film ambient occlusion";
                feature.hideFlags = HideFlags.DontSave;
                var so = new SerializedObject(feature);
                var settings = so.FindProperty("m_Settings");
                void Set(string name, Action<SerializedProperty> apply)
                {
                    var p = settings?.FindPropertyRelative(name);
                    if (p != null) apply(p); else Debug.LogWarning("[SameSky:film] ambient occlusion has no " + name);
                }
                Set("Intensity", p => p.floatValue = 1.6f);
                Set("Radius", p => p.floatValue = 0.22f);
                Set("Falloff", p => p.floatValue = 60f);
                Set("DirectLightingStrength", p => p.floatValue = 0.3f);
                Set("Source", p => p.intValue = 1);          // depth and normals
                Set("NormalSamples", p => p.intValue = 2);
                Set("Samples", p => p.intValue = 0);         // high
                Set("BlurQuality", p => p.intValue = 0);     // high
                Set("Downsample", p => p.boolValue = false);
                Set("AfterOpaque", p => p.boolValue = false);
                so.ApplyModifiedPropertiesWithoutUndo();
                _renderer.rendererFeatures.Add(feature);
                _renderer.SetDirty();
                _made.Add(feature);
            }
            catch (Exception e) { Debug.LogWarning("[SameSky:film] ambient occlusion is off: " + e.Message); }
        }

        public void Dispose()
        {
            GraphicsSettings.defaultRenderPipeline = _default;
            QualitySettings.renderPipeline = _quality;
            ShaderUtil.allowAsyncCompilation = _async;
            foreach (var o in _made) if (o != null) UnityEngine.Object.DestroyImmediate(o);
        }
    }

    public static class FilmRenderer
    {
        public const int Fps = 24;
        public static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        public static string Root => Path.Combine(RepoRoot, "demo-build", "film");

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        /// <summary>Batch entry point: see the top of this file.</summary>
        public static void Run()
        {
            var q = new FilmRequest
            {
                id = "batch",
                shots = Arg("-filmShots") ?? "all",
                times = Arg("-filmStills") ?? "",
                scale = int.TryParse(Arg("-filmScale"), out var s) ? s : 0,
                ffmpeg = Arg("-filmFfmpeg") ?? "",
                outDir = Arg("-filmOut") ?? "",
            };
            q.action = Arg("-filmAction") ?? (q.times != "" ? "stills" : "render");
            var size = (Arg("-filmSize") ?? "").Split('x');
            if (size.Length == 2 && int.TryParse(size[0], out var w) && int.TryParse(size[1], out var h)) { q.width = w; q.height = h; }
            if (q.scale <= 0) q.scale = q.action == "render" ? 2 : 1;
            try
            {
                Debug.Log("[SameSky:film] " + Execute(q));
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[SameSky:film] failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        public static string FindFfmpeg(string given)
        {
            if (!string.IsNullOrEmpty(given) && File.Exists(given)) return given;
            var dir = Path.Combine(RepoRoot, "api", ".venv", "Lib", "site-packages", "imageio_ffmpeg", "binaries");
            if (Directory.Exists(dir))
                foreach (var f in Directory.GetFiles(dir, "ffmpeg*.exe")) return f;
            throw new FileNotFoundException("FFmpeg not found: pass -filmFfmpeg, or run scripts/native/bootstrap.ps1 (the API's Python environment brings one)");
        }

        public static string Execute(FilmRequest q)
        {
            var report = new StringBuilder();
            List<ShotDef> wanted;
            var library = FilmShots.All.Concat(FilmWish.All).ToList();
            if (q.shots == "all") wanted = FilmShots.All.Where(x => x.Id.Length > 1 && x.Id[0] == 's' && char.IsDigit(x.Id[1])).ToList();
            else if (q.shots == "wish") wanted = FilmWish.All.Where(x => x.Id.Length > 1 && x.Id[0] == 'w' && char.IsDigit(x.Id[1])).ToList();
            else if (q.shots == "media") wanted = FilmShots.All.Where(x => x.Id.StartsWith("m_")).ToList();
            else
            {
                wanted = new List<ShotDef>();
                foreach (var id in q.shots.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    wanted.Add(library.FirstOrDefault(x => x.Id == id.Trim()) ?? throw new ArgumentException("no shot called " + id));
            }
            var stills = q.action == "stills";
            var outDir = string.IsNullOrEmpty(q.outDir) ? Path.Combine(Root, stills ? "stills" : "shots") : q.outDir;
            Directory.CreateDirectory(outDir);
            int width = q.width * Mathf.Max(1, q.scale), height = q.height * Mathf.Max(1, q.scale);
            // kept across the scene changes below: opening a new scene clears away objects nothing in it refers to
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "Film frame", antiAliasing = 1, hideFlags = HideFlags.HideAndDontSave };
            target.Create();
            var read = new Texture2D(width, height, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave };
            using var pipeline = new FilmPipeline();
            FilmLook.Reload();
            try
            {
                foreach (var shot in wanted)
                {
                    var watch = Stopwatch.StartNew();
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    pipeline.Asset.shadowDistance = 14f;
                    var stage = new FilmStage { Pipeline = pipeline.Asset };
                    try
                    {
                        shot.Build(stage);
                        stage.Ready();
                        Draw(stage, target, read);   // a frame nobody sees: textures read for the first time arrive a frame late
                        if (stills) Stills(stage, shot, q, outDir, target, read, report);
                        else if (q.action == "frames") Frames(stage, shot, q, target, read, report);
                        else Video(stage, shot, q, outDir, target, read, report);
                    }
                    finally { stage.Dispose(); }
                    report.AppendLine($"{shot.Id}: {shot.Seconds:F2} s of film in {watch.Elapsed.TotalSeconds:F1} s");
                }
            }
            finally
            {
                RenderTexture.active = null;
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(read);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            return report.ToString();
        }

        static void Draw(FilmStage stage, RenderTexture target, Texture2D read)
        {
            var cam = stage.Camera.Cam;
            cam.targetTexture = target;
            cam.aspect = target.width / (float)target.height;
            stage.BeforeDraw();
            cam.Render();
            var was = RenderTexture.active;
            RenderTexture.active = target;
            read.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
            RenderTexture.active = was;
            cam.targetTexture = null;
        }

        static void Stills(FilmStage stage, ShotDef shot, FilmRequest q, string outDir, RenderTexture target, Texture2D read, StringBuilder report)
        {
            var last = 0f;
            var first = true;
            foreach (var part in q.times.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = float.Parse(part.Trim(), CultureInfo.InvariantCulture);
                if (first) { stage.Apply(0f, 0f); first = false; }
                stage.Apply(t, Mathf.Max(0f, t - last));
                last = t;
                Draw(stage, target, read);
                read.Apply(false, false);
                var file = Path.Combine(outDir, $"{shot.Id}-{t.ToString("00.00", CultureInfo.InvariantCulture)}.jpg");
                File.WriteAllBytes(file, read.EncodeToJPG(92));
                report.AppendLine("still " + file);
                foreach (var note in stage.Notes) report.AppendLine("  " + note());
            }
        }

        /// <summary>A shot as numbered pictures in demo-build/film/media/&lt;shot&gt;: what the lounge's screen and the
        /// phone show (the film's own sunset, filmed on its own waterfront).</summary>
        static void Frames(FilmStage stage, ShotDef shot, FilmRequest q, RenderTexture target, Texture2D read, StringBuilder report)
        {
            var dir = Path.Combine(Root, "media", shot.Id);
            Directory.CreateDirectory(dir);
            var frames = Mathf.RoundToInt(shot.Seconds * Fps);
            for (var f = 0; f < frames; f++)
            {
                stage.Apply(f / (float)Fps, f == 0 ? 0f : 1f / Fps);
                Draw(stage, target, read);
                read.Apply(false, false);
                File.WriteAllBytes(Path.Combine(dir, $"{f:0000}.jpg"), read.EncodeToJPG(90));
            }
            report.AppendLine($"frames {dir} ({frames})");
        }

        static void Video(FilmStage stage, ShotDef shot, FilmRequest q, string outDir, RenderTexture target, Texture2D read, StringBuilder report)
        {
            var file = Path.Combine(outDir, shot.Id + ".mp4");
            var scale = q.scale > 1 ? $",scale={q.width}:{q.height}:flags=lanczos" : "";
            var psi = new ProcessStartInfo(FindFfmpeg(q.ffmpeg),
                $"-y -hide_banner -loglevel error -f rawvideo -pix_fmt rgb24 -s {target.width}x{target.height} -r {Fps} -i - -vf \"vflip{scale}\" -c:v libx264 -preset medium -crf {q.crf} -pix_fmt yuv420p -movflags +faststart \"{file}\"")
            {
                UseShellExecute = false, RedirectStandardInput = true, RedirectStandardError = true, CreateNoWindow = true,
            };
            var errors = new StringBuilder();
            using var ffmpeg = Process.Start(psi);
            ffmpeg.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (errors) errors.AppendLine(e.Data); };
            ffmpeg.BeginErrorReadLine();
            var frames = Mathf.RoundToInt(shot.Seconds * Fps);
            var buffer = new byte[target.width * target.height * 3];
            try
            {
                var pipe = ffmpeg.StandardInput.BaseStream;
                for (var f = 0; f < frames; f++)
                {
                    stage.Apply(f / (float)Fps, f == 0 ? 0f : 1f / Fps);
                    Draw(stage, target, read);
                    read.GetRawTextureData<byte>().CopyTo(buffer);
                    pipe.Write(buffer, 0, buffer.Length);
                }
                pipe.Flush();
            }
            finally
            {
                try { ffmpeg.StandardInput.Close(); } catch { }
                if (!ffmpeg.WaitForExit(120000)) { try { ffmpeg.Kill(); } catch { } }
            }
            if (ffmpeg.ExitCode != 0 || !File.Exists(file)) throw new InvalidOperationException($"FFmpeg failed on {shot.Id}: {errors}");
            report.AppendLine($"video {file} ({frames} frames)");
        }
    }

    /// <summary>The render console: a Unity started with -filmConsole answers requests left in
    /// demo-build/film/console/request.json, recompiling first when asked.</summary>
    [InitializeOnLoad]
    public static class FilmConsole
    {
        const string PendingKey = "SameSky.Film.Pending";
        static readonly List<string> Errors = new();
        static double _next;
        static string Dir => Path.Combine(FilmRenderer.Root, "console");

        static FilmConsole()
        {
            if (!Environment.GetCommandLineArgs().Contains("-filmConsole")) return;
            Directory.CreateDirectory(Dir);
            EditorApplication.update += Tick;
            CompilationPipeline.compilationStarted += _ => Errors.Clear();
            CompilationPipeline.assemblyCompilationFinished += (_, messages) =>
            {
                foreach (var m in messages)
                    if (m.type == CompilerMessageType.Error) Errors.Add($"{m.file}({m.line},{m.column}): {m.message}");
            };
            File.WriteAllText(Path.Combine(Dir, "ready.txt"), DateTime.Now.ToString("o"));
            Debug.Log("[SameSky:film] console ready in " + Dir);
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < _next) return;
            _next = EditorApplication.timeSinceStartup + 0.2;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            var pending = SessionState.GetString(PendingKey, "");
            if (pending != "")
            {
                SessionState.EraseString(PendingKey);
                var waiting = JsonUtility.FromJson<FilmRequest>(pending);
                if (EditorUtility.scriptCompilationFailed) Answer(waiting, "error: the scripts do not compile\n" + string.Join("\n", Errors));
                else Do(waiting);
                return;
            }
            var file = Path.Combine(Dir, "request.json");
            if (!File.Exists(file)) return;
            string text;
            try { text = File.ReadAllText(file); File.Delete(file); }
            catch (IOException) { return; }
            FilmRequest q;
            try { q = JsonUtility.FromJson<FilmRequest>(text); }
            catch (Exception e) { Debug.LogWarning("[SameSky:film] unreadable request: " + e.Message); return; }
            if (q == null) return;
            if (q.action == "quit") { Answer(q, "ok bye"); EditorApplication.Exit(0); return; }
            if (q.refresh)
            {
                q.refresh = false;
                SessionState.SetString(PendingKey, JsonUtility.ToJson(q));
                AssetDatabase.Refresh();
                return;
            }
            Do(q);
        }

        static void Do(FilmRequest q)
        {
            if (q.action == "ping") { Answer(q, "ok ready"); return; }
            try { Answer(q, "ok\n" + FilmRenderer.Execute(q)); }
            catch (Exception e) { Answer(q, "error: " + e); }
        }

        static void Answer(FilmRequest q, string text)
        {
            var file = Path.Combine(Dir, $"ack-{q.id}.txt");
            File.WriteAllText(file + ".tmp", text);
            if (File.Exists(file)) File.Delete(file);
            File.Move(file + ".tmp", file);
            Debug.Log($"[SameSky:film] request {q.id} ({q.action} {q.shots}): {text.Split('\n')[0]}");
        }
    }
}
