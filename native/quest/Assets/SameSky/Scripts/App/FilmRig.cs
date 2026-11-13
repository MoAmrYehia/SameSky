// A capture-only camera for the demo film (docs/demo). Editor only: nothing here is compiled into a build, and
// nothing changes what the person in the headset sees or does.
//
// It draws the same scene, in the same state, with the same renderer as the headset view, from a second camera
// that is either fixed at the seated eye or moves along a few keyframes, and sends each frame to FFmpeg. It can
// also write the app's own sound (whatever the AudioListener hears) to a WAV file beside the video.
//
// Asked for through the probe folder, like the seated renders: film-request.json
//   { "action": "start", "name": "cup", "dir": "<folder>", "ffmpeg": "<ffmpeg.exe>", "seconds": 0, "fps": 30,
//     "width": 1920, "height": 1080, "audio": true,
//     "keys": [ { "t": 0, "at": [0, 0.1, -0.4], "look": [0.1, -0.35, 0.6], "fov": 50 },
//               { "t": 6, "at": [0, 0.0, 0.0], "yaw": 0, "pitch": -14, "fov": 46 } ] }
//   { "action": "stop" }
// Positions are in seat space (metres from the seated eye: x right, y up, z toward the table). A key either looks at
// a point ("look") or along yaw and pitch in degrees. "seconds": 0 records until a stop request.
// "space": "world" takes the keys in world coordinates instead (the probe reports where every button is: a camera can
// be set square to a panel that has turned to face the person).
// "follow": "head" is the first-person view: the camera sits where the headset is and turns with it, eased over
// "ease" seconds so a simulated head's steps don't jolt the picture, and looks down by the keys' pitch (a person looks
// at their hands; the simulator's head looks straight ahead). Panels that turn to face the person face this camera.
// Outputs: <name>.mp4, <name>.wav, and <name>.json with the frame count and the probe clock at the first frame, so
// probe events (which carry that clock) can be placed on the film's timeline.

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace SameSky.App
{
    public sealed class FilmRig : MonoBehaviour
    {
        [Serializable]
        public sealed class Key
        {
            public float t;
            public float[] at;
            public float[] look;
            public float yaw;
            public float pitch = -12f;
            public float fov = 55f;
        }

        [Serializable]
        public sealed class Request
        {
            public string action = "start";
            public string name = "shot";
            public string dir;
            public string ffmpeg;
            public float seconds;
            public int fps = 30, width = 1920, height = 1080, crf = 12;
            public bool audio = true;
            public Key[] keys;
            public string follow;
            public float ease = 0.45f;
            public string space;
        }

        sealed class Frame
        {
            public int Index, Repeat;
            public byte[] Data;
        }

        static FilmRig _instance;
        bool _recording, _stop;

        /// <summary>Called by the probe on its tick: starts or stops a recording when a request file is there.</summary>
        public static void Check(string probeDir, MonoBehaviour host)
        {
            var path = Path.Combine(probeDir, "film-request.json");
            if (!File.Exists(path)) return;
            Request req;
            try { req = JsonConvert.DeserializeObject<Request>(File.ReadAllText(path)); File.Delete(path); }
            catch (Exception) { return; }   // still being written: next tick
            if (req == null) return;
            if (_instance == null) _instance = host.gameObject.AddComponent<FilmRig>();
            if (req.action == "stop") { _instance._stop = true; return; }
            if (_instance._recording) { TestProbe.Event("film-refused", $"{req.name}: already recording"); return; }
            _instance.StartCoroutine(_instance.Record(req));
        }

        static float Smooth(float x) => x * x * (3f - 2f * x);

        static void Pose(Camera cam, Key[] keys, float t, Vector3 seat, Quaternion facing)
        {
            if (keys == null || keys.Length == 0) keys = new[] { new Key() };
            var a = keys[0];
            var b = keys[keys.Length - 1];
            for (var i = 0; i < keys.Length - 1; i++)
                if (t >= keys[i].t && t <= keys[i + 1].t) { a = keys[i]; b = keys[i + 1]; break; }
            if (t <= keys[0].t) b = a = keys[0];
            else if (t >= keys[keys.Length - 1].t) a = b = keys[keys.Length - 1];
            var k = b.t > a.t ? Smooth(Mathf.Clamp01((t - a.t) / (b.t - a.t))) : 0f;
            Vector3 At(Key key) => key.at is { Length: 3 } ? new Vector3(key.at[0], key.at[1], key.at[2]) : Vector3.zero;
            Quaternion Facing(Key key, Vector3 from) =>
                key.look is { Length: 3 }
                    ? Quaternion.LookRotation(new Vector3(key.look[0], key.look[1], key.look[2]) - from, Vector3.up)
                    : Quaternion.Euler(-key.pitch, key.yaw, 0);
            var local = Vector3.Lerp(At(a), At(b), k);
            var turn = Quaternion.Slerp(Facing(a, At(a)), Facing(b, At(b)), k);
            cam.transform.SetPositionAndRotation(seat + facing * local, facing * turn);
            cam.fieldOfView = Mathf.Clamp(Mathf.Lerp(a.fov, b.fov, k), 12f, 110f);
        }

        IEnumerator Record(Request r)
        {
            _recording = true;
            _stop = false;
            var name = string.Concat((r.name ?? "shot").Split(Path.GetInvalidFileNameChars()));
            int w = Mathf.Clamp(r.width, 320, 3840) & ~1, h = Mathf.Clamp(r.height, 180, 2160) & ~1, fps = Mathf.Clamp(r.fps, 10, 60);
            Process ffmpeg = null;
            Thread writer = null;
            FilmAudioTap tap = null;
            var queue = new BlockingCollection<Frame>(new ConcurrentQueue<Frame>());
            var pool = new ConcurrentBag<byte[]>();
            var written = 0;
            string failure = null;
            var go = new GameObject("Film camera");
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;   // drawn by hand, at the film's frame rate
            cam.stereoTargetEye = StereoTargetEyeMask.None;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 300f;
            if (Camera.main != null) cam.cullingMask = Camera.main.cullingMask;
            var msaa = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var resolved = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = msaa;
            var app = SameSkyApp.Instance;
            var eye = app != null ? app.Anchor("SeatEye") : null;
            var seat = eye != null ? eye.position : new Vector3(0, 1.18f, 0);
            var facing = eye != null ? eye.rotation : Quaternion.identity;
            if (r.space == "world") { seat = Vector3.zero; facing = Quaternion.identity; }
            var pending = 0;
            double clockAtFirstFrame = 0;
            var index = 0;
            var followHead = r.follow == "head" && app != null && app.Head != null;
            var eased = followHead ? app.Head.position : Vector3.zero;
            var easedYaw = followHead ? app.Head.eulerAngles.y : 0f;
            var lastFollow = Time.realtimeSinceStartupAsDouble;
            try
            {
                Directory.CreateDirectory(r.dir);
                var video = Path.Combine(r.dir, name + ".mp4");
                var info = new ProcessStartInfo(r.ffmpeg,
                    $"-hide_banner -loglevel error -y -f rawvideo -pix_fmt rgba -s {w}x{h} -framerate {fps} -i - -vf vflip -c:v libx264 -preset ultrafast -crf {Mathf.Clamp(r.crf, 0, 30)} -pix_fmt yuv420p -movflags +faststart \"{video}\"")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardError = true };
                ffmpeg = Process.Start(info);
                var errors = ffmpeg.StandardError.ReadToEndAsync();
                var pipe = ffmpeg.StandardInput.BaseStream;
                writer = new Thread(() =>
                {
                    // frames can come back from the GPU out of order: hold them until their turn
                    var held = new Dictionary<int, Frame>();
                    var next = 0;
                    try
                    {
                        foreach (var f in queue.GetConsumingEnumerable())
                        {
                            held[f.Index] = f;
                            while (held.TryGetValue(next, out var ready))
                            {
                                held.Remove(next);
                                for (var i = 0; i < ready.Repeat; i++) pipe.Write(ready.Data, 0, ready.Data.Length);
                                written += ready.Repeat;
                                next += ready.Repeat;
                                pool.Add(ready.Data);
                            }
                        }
                        pipe.Flush();
                    }
                    catch (Exception e) { failure = e.GetType().Name + ": " + e.Message; }
                    finally { try { pipe.Close(); } catch (Exception) { } }
                }) { IsBackground = true, Name = "Film frames" };
                writer.Start();
                if (r.audio)
                {
                    var listener = FindFirstObjectByType<AudioListener>();
                    if (listener != null)
                    {
                        tap = listener.gameObject.GetComponent<FilmAudioTap>() ?? listener.gameObject.AddComponent<FilmAudioTap>();
                        tap.Begin(Path.Combine(r.dir, name + ".wav"));
                    }
                }
                var total = r.seconds > 0 ? Mathf.CeilToInt(r.seconds * fps) : int.MaxValue;
                var start = Time.realtimeSinceStartupAsDouble;
                clockAtFirstFrame = start;
                TestProbe.Event("film", $"start {name}");
                while (!_stop && index < total && failure == null)
                {
                    yield return new WaitForEndOfFrame();
                    var due = (int)Math.Floor((Time.realtimeSinceStartupAsDouble - start) * fps) + 1;
                    if (index >= due) continue;                         // too early for the next frame of the film
                    var repeat = Mathf.Clamp(due - index, 1, 15);       // a slow moment: this frame stands in for the ones missed
                    if (followHead)
                    {
                        // where the headset is, eased; level (no roll), turned with the head, looking down by the key's pitch
                        var nowAt = Time.realtimeSinceStartupAsDouble;
                        var k = 1f - Mathf.Exp(-(float)(nowAt - lastFollow) / Mathf.Max(0.02f, r.ease));
                        lastFollow = nowAt;
                        eased = Vector3.Lerp(eased, app.Head.position, k);
                        easedYaw = Mathf.LerpAngle(easedYaw, app.Head.eulerAngles.y, k);
                        Pose(cam, r.keys, index / (float)fps, eased, Quaternion.Euler(0, easedYaw, 0));
                    }
                    else Pose(cam, r.keys, index / (float)fps, seat, facing);
                    cam.Render();
                    Graphics.Blit(msaa, resolved);   // resolves the multisampling
                    var mine = index;
                    pending++;
                    AsyncGPUReadback.Request(resolved, 0, TextureFormat.RGBA32, back =>
                    {
                        pending--;
                        if (back.hasError) { failure ??= "the GPU readback failed"; return; }
                        var data = back.GetData<byte>();
                        if (!pool.TryTake(out var bytes) || bytes.Length != data.Length) bytes = new byte[data.Length];
                        data.CopyTo(bytes);
                        queue.Add(new Frame { Index = mine, Repeat = repeat, Data = bytes });
                    });
                    index += repeat;
                }
                for (var waited = 0f; pending > 0 && waited < 5f; waited += Time.unscaledDeltaTime) yield return null;
                queue.CompleteAdding();
                for (var waited = 0f; writer.IsAlive && waited < 30f; waited += Time.unscaledDeltaTime) yield return null;
                if (tap != null) tap.End();
                for (var waited = 0f; !ffmpeg.HasExited && waited < 60f; waited += Time.unscaledDeltaTime) yield return null;
                var said = errors.IsCompleted ? errors.Result.Trim() : "";
                if (failure == null && ffmpeg.HasExited && ffmpeg.ExitCode != 0) failure = "ffmpeg exit " + ffmpeg.ExitCode + (said.Length > 0 ? ": " + said : "");
                File.WriteAllText(Path.Combine(r.dir, name + ".json"), JsonConvert.SerializeObject(new
                {
                    name, fps, width = w, height = h, frames = written, seconds = Math.Round(written / (double)fps, 3),
                    probeClockAtFirstFrame = Math.Round(clockAtFirstFrame, 3),
                    audio = tap != null ? new { file = name + ".wav", sampleRate = tap.SampleRate, channels = tap.Channels, samples = tap.Samples } : null,
                    capture = "Unity Editor Play mode, a second camera in the app's own scene (FilmRig), Meta XR Simulator session",
                    failure,
                }, Formatting.Indented));
            }
            finally
            {
                if (!queue.IsAddingCompleted) queue.CompleteAdding();
                if (tap != null) tap.End();
                cam.targetTexture = null;
                msaa.Release();
                resolved.Release();
                Destroy(msaa);
                Destroy(resolved);
                Destroy(go);
                try { if (ffmpeg != null && !ffmpeg.HasExited) ffmpeg.Kill(); } catch (Exception) { }
                _recording = false;
            }
            TestProbe.Event(failure == null ? "film" : "film-failed", failure == null ? $"stop {name} frames={written}" : $"{name}: {failure}");
        }
    }
}
#endif
