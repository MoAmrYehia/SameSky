// Evidence for automated and manual acceptance runs (never sends anything anywhere):
//   persistentDataPath/probe/state.json   snapshot five times a second: XR runtime and session, tracked hands,
//                                          active input, screen, mode, cup, audio, captions, frame time
//   persistentDataPath/probe/events.jsonl one line per event: presses (with the input that caused them),
//                                          grabs, screens, errors, recenter, pause/resume
//   persistentDataPath/probe/snap-NAME.png a fixed seated-camera render, on request (snap-request.json): the
//                                          same view every run, for comparing with the visual references
// The same events go to the player log with a "[SameSky:event]" prefix. Development builds only (release
// builds have no probe). No emails, tokens, message bodies or media are ever written.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using SameSky.Interaction;
using UnityEngine;
using UnityEngine.XR;

namespace SameSky.App
{
    public sealed class TestProbe : MonoBehaviour
    {
        static TestProbe _instance;
        static readonly Queue<string> Pending = new();
        string _dir;
        float _nextWrite;
        int _frames;
        float _frameTime;
        public static int Presses, Grabs, HoverEvents;
        public static string LastPress = "", LastGrab = "";
        public static string Captions = "";
        public static string AudioPlaying = "";

        void Awake()
        {
            _instance = this;
            _dir = Path.Combine(LocalStore.Root, "probe");
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "events.jsonl"), "");
            Pressable.AnyEvent += (p, kind) =>
            {
                if (kind == "press") { Presses++; LastPress = p.Id; }
                if (kind == "hover") HoverEvents++;
                if (kind is "press" or "cancel") Event(kind, p.Id);
            };
            GrabbableObject.AnyEvent += (g, kind) =>
            {
                if (kind == "grab") { Grabs++; LastGrab = g.Id; }
                Event(kind, g.Id);
            };
            Event("start", Application.version);
        }

        /// <summary>Only development builds and the Editor carry the probe; release builds log nothing.</summary>
        public static bool Enabled => Debug.isDebugBuild;

        public static void Event(string kind, string detail)
        {
            if (!Enabled) return;
            var line = JsonConvert.SerializeObject(new { t = Math.Round(Time.realtimeSinceStartup, 3), kind, detail, input = ActiveInput() });
            Debug.Log("[SameSky:event] " + line);
            if (_instance == null) { Pending.Enqueue(line); return; }
            try { File.AppendAllText(Path.Combine(_instance._dir, "events.jsonl"), line + "\n"); } catch (IOException) { /* next time */ }
        }

        static string ActiveInput()
        {
            try
            {
                var c = OVRInput.GetActiveController();
                if ((c & OVRInput.Controller.Hands) != 0) return "hands";
                if ((c & OVRInput.Controller.Touch) != 0) return "controllers";
                return c.ToString();
            }
            catch (Exception) { return "unknown"; }
        }

        void Update()
        {
            _frames++;
            _frameTime += Time.unscaledDeltaTime;
            while (Pending.Count > 0)
                try { File.AppendAllText(Path.Combine(_dir, "events.jsonl"), Pending.Dequeue() + "\n"); } catch (IOException) { break; }
            if (Time.unscaledTime < _nextWrite) return;
            _nextWrite = Time.unscaledTime + 0.2f; // five times a second: a test steering a simulated hand reads it in a loop
            var app = SameSkyApp.Instance;
            var state = new Dictionary<string, object>
            {
                ["time"] = Math.Round(Time.realtimeSinceStartup, 2),
                ["xrDeviceActive"] = XRSettings.isDeviceActive,
                ["xrDevice"] = XRSettings.loadedDeviceName,
                ["openxrRuntime"] = OpenXrRuntime(),
                ["hands"] = new { left = HandTracked(OVRPlugin.Hand.HandLeft), right = HandTracked(OVRPlugin.Hand.HandRight) },
                ["activeInput"] = ActiveInput(),
                ["fps"] = _frameTime > 0 ? Math.Round(_frames / _frameTime, 1) : 0,
                ["screen"] = app != null ? app.Screen : "none",
                ["mode"] = app?.Data == null ? "none" : app.Data.IsSample ? "sample" : "live",
                ["room"] = app?.CurrentRoom?.Name ?? "",
                ["paused"] = app != null && app.Paused,
                ["presses"] = Presses,
                ["lastPress"] = LastPress,
                ["grabs"] = Grabs,
                ["lastGrab"] = LastGrab,
                ["hovers"] = HoverEvents,
                ["audio"] = AudioPlaying,
                ["captionsOn"] = Prefs.Current.captionsOn,
                ["captions"] = Captions,
                ["head"] = app != null && app.Head != null ? V(app.Head.position) : null,
                ["headForward"] = app != null && app.Head != null ? V(app.Head.forward) : null,
                ["handsAt"] = HandPositions(),
                // where each hand's poke interactor is (the index fingertip that presses buttons), null when untracked
                ["pokeAt"] = PokePoints(),
                ["presence"] = app != null ? app.Presence : "",
                ["cupHeld"] = app != null && app.CupHeld,
                ["voicePlaying"] = app != null && app.VoicePlaying,
                ["story"] = app != null ? app.StoryState : "none",
                ["live"] = app != null ? app.LiveState : "none",
                ["driftMs"] = app != null ? Math.Round(app.LastDriftMs) : 0,
                // how the room is furnished (the marketplace), and whether what it shows has been kept
                ["decor"] = app != null ? app.DecorSummary : "",
                ["decorVersion"] = app != null ? app.DecorVersion : 0,
                ["decorUnsaved"] = app != null && app.DecorUnsaved,
                // how many tracked-hand meshes wear the skin material, and its tone
                ["handSkin"] = HandSkin.Instance != null ? new { dressed = HandSkin.Instance.Dressed, tone = "#" + ColorUtility.ToHtmlStringRGB(HandSkin.Instance.Tone) } : null,
                // what can be pressed right now, with where it is: lets a test aim simulated hands at real targets
                ["targets"] = Targets(),
            };
            _frames = 0;
            _frameTime = 0;
            try { File.WriteAllText(Path.Combine(_dir, "state.json"), JsonConvert.SerializeObject(state, Formatting.Indented)); } catch (IOException) { /* next tick */ }
            CheckSnapRequest();
#if UNITY_EDITOR
            FilmRig.Check(_dir, this);   // the demo film's capture-only camera (Editor only)
#endif
        }

        // ------------------------------------------------------------ fixed seated views for render comparisons

        /// <summary>{"name":"room","yaw":0,"pitch":-12,"fov":60,"width":1600,"height":900,"at":[x,y,z]}: yaw and pitch
        /// in degrees from the seat's forward (negative pitch looks down), vertical field of view, and an optional
        /// offset from the seated eye (seat space, metres).</summary>
        [Serializable]
        sealed class SnapRequest
        {
            public string name = "view";
            public float yaw;
            public float pitch = -12f;
            public float fov = 60f;
            public int width = 1600, height = 900;
            public float[] at;
            /// <summary>Optional: open this screen first (SameSkyApp.DevOpen: day, share, together, keepsakes, settings,
            /// story, circle, direct, room...), wait, then render.</summary>
            public string open;
            public float wait = 1.4f;
            /// <summary>Optional: "hands" looks at the tracked hands from where the head is (their backs), "palms" from
            /// the other side. The seat view is used when no hand is tracked.</summary>
            public string view;
        }

        bool _snapping;

        void CheckSnapRequest()
        {
            if (_snapping) return;
            var path = Path.Combine(_dir, "snap-request.json");
            if (!File.Exists(path)) return;
            SnapRequest req;
            try { req = JsonConvert.DeserializeObject<SnapRequest>(File.ReadAllText(path)); File.Delete(path); }
            catch (Exception) { return; }   // still being written: next tick
            if (req == null) return;
            StartCoroutine(Snap(req));
        }

        IEnumerator Snap(SnapRequest r)
        {
            _snapping = true;
            var name = string.Concat((r.name ?? "view").Split(Path.GetInvalidFileNameChars()));
            var app = SameSkyApp.Instance;
            if (!string.IsNullOrEmpty(r.open))
            {
                var opened = app != null && app.DevOpen(r.open);
                Event("dev-open", $"{r.open}:{(opened ? "ok" : "unavailable")}");
                yield return new WaitForSecondsRealtime(Mathf.Clamp(r.wait, 0.2f, 20f));   // content loads, panels settle
            }
            yield return new WaitForEndOfFrame();
            var eye = app != null ? app.Anchor("SeatEye") : null;
            var position = eye != null ? eye.position : new Vector3(0, 1.18f, 0);
            var facing = eye != null ? eye.rotation : Quaternion.identity;
            if (r.at is { Length: 3 }) position += facing * new Vector3(r.at[0], r.at[1], r.at[2]);
            var look = facing * Quaternion.Euler(-r.pitch, r.yaw, 0);
            var near = 0.02f;
            if (r.view is "hands" or "palms")
            {
                // The hand's own palm joint says where the hand is and which way it faces (its up is the back of the
                // hand, its forward runs along the fingers): look square at the back or at the palm, fingers up.
                var palm = HandJoint(Handedness.Right, "Palm") ?? HandJoint(Handedness.Left, "Palm");
                if (palm != null)
                {
                    var target = palm.position + palm.forward * 0.045f;
                    position = target + palm.up * (r.view == "hands" ? 0.3f : -0.3f);
                    look = Quaternion.LookRotation(target - position, palm.forward);
                    near = 0.14f;   // a panel or the table top between the camera and the hand is left out
                }
            }
            int w = Mathf.Clamp(r.width, 64, 3840), h = Mathf.Clamp(r.height, 64, 2160);
            var go = new GameObject("Snapshot camera");
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.stereoTargetEye = StereoTargetEyeMask.None;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = Mathf.Clamp(r.fov, 20f, 120f);
            cam.nearClipPlane = near;
            cam.farClipPlane = 300f;
            if (Camera.main != null) cam.cullingMask = Camera.main.cullingMask;
            go.transform.SetPositionAndRotation(position, look);
            var msaa = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var resolved = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            cam.targetTexture = msaa;
#if UNITY_EDITOR
            // In the Editor a shader variant draws as a cyan stand-in while it compiles in the background. Draw once
            // to ask for everything this view needs, wait for the compiler, then draw the picture that is kept.
            var primed = false;
            try { cam.Render(); primed = true; }
            catch (Exception) { /* the real attempt below reports it */ }
            for (var waited = 0f; primed && UnityEditor.ShaderUtil.anythingCompiling && waited < 40f; waited += Time.unscaledDeltaTime)
                yield return null;
#endif
            try
            {
                cam.Render();
                Graphics.Blit(msaa, resolved);   // resolves the multisampling
                RenderTexture.active = resolved;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(_dir, $"snap-{name}.png"), tex.EncodeToPNG());
                Event("snap", name);
            }
            catch (Exception e) { Event("snap-failed", $"{name}: {e.GetType().Name}"); }
            finally
            {
                RenderTexture.active = previous;
                cam.targetTexture = null;
                msaa.Release();
                resolved.Release();
                Destroy(msaa);
                Destroy(resolved);
                Destroy(tex);
                Destroy(go);
                _snapping = false;
            }
        }

        /// <summary>A joint of the tracked hand that is being drawn (by the end of its name: "Palm", "IndexTip"...).</summary>
        static Transform HandJoint(Handedness which, string nameEnd)
        {
            foreach (var visual in FindObjectsByType<HandVisual>(FindObjectsSortMode.None))
            {
                if (visual.Hand == null || visual.Hand.Handedness != which || !visual.Hand.IsTrackedDataValid) continue;
                foreach (var smr in visual.GetComponentsInChildren<SkinnedMeshRenderer>(false))
                    foreach (var bone in smr.bones)
                        if (bone != null && bone.name.EndsWith(nameEnd)) return bone;
            }
            return null;
        }

        static float Round(float v) => (float)Math.Round(v, 3);
        static float[] V(Vector3 v) => new[] { Round(v.x), Round(v.y), Round(v.z) };

        static object HandPositions()
        {
            var rig = FindAnyObjectByType<OVRCameraRig>();
            if (rig == null) return null;
            return new { left = V(rig.leftHandAnchor.position), right = V(rig.rightHandAnchor.position) };
        }

        static object PokePoints()
        {
            float[] left = null, right = null;
            foreach (var poke in FindObjectsByType<PokeInteractor>(FindObjectsSortMode.None))
            {
                if (!poke.isActiveAndEnabled || poke.State == InteractorState.Disabled) continue;
                var hand = poke.GetComponentInParent<HandRef>(); // controller pokes have a ControllerRef instead
                if (hand == null) continue;
                if (hand.Handedness == Handedness.Right) right = V(poke.Origin);
                else left = V(poke.Origin);
            }
            return new { left, right };
        }

        static List<object> Targets()
        {
            var list = new List<object>();
            foreach (var p in FindObjectsByType<Pressable>(FindObjectsSortMode.None))
            {
                if (!p.isActiveAndEnabled || !p.Interactable) continue;
                var t = p.transform;
                list.Add(new { id = p.Id, at = V(t.position), normal = V(-t.forward), size = new[] { Round(p.Size.x), Round(p.Size.y) }, hover = p.Hovered, down = p.Selected });
            }
            foreach (var g in FindObjectsByType<GrabbableObject>(FindObjectsSortMode.None))
                if (g.isActiveAndEnabled) list.Add(new { id = g.Id, at = V(g.transform.position + Vector3.up * 0.05f), normal = V(Vector3.up), size = new[] { 0.09f, 0.1f } });
            return list;
        }

        static bool HandTracked(OVRPlugin.Hand which)
        {
            try
            {
                var state = new OVRPlugin.HandState();
                return OVRPlugin.GetHandState(OVRPlugin.Step.Render, which, ref state) && (state.Status & OVRPlugin.HandStatus.HandTracked) != 0;
            }
            catch (Exception) { return false; }
        }

        static string OpenXrRuntime()
        {
#if SAMESKY_OPENXR
            try { return $"{UnityEngine.XR.OpenXR.OpenXRRuntime.name} {UnityEngine.XR.OpenXR.OpenXRRuntime.version}"; } catch (Exception) { return ""; }
#else
            return "";
#endif
        }
    }
}
