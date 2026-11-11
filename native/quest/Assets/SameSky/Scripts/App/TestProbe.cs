// Evidence for automated and manual acceptance runs (never sends anything anywhere):
//   persistentDataPath/probe/state.json   snapshot twice a second: XR runtime and session, tracked hands,
//                                          active input, screen, mode, cup, audio, captions, frame time
//   persistentDataPath/probe/events.jsonl one line per event: presses (with the input that caused them),
//                                          grabs, screens, errors, recenter, pause/resume
// The same events go to the player log with a "[SameSky:event]" prefix. Development builds only (release
// builds have no probe). No emails, tokens, message bodies or media are ever written.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
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
            _nextWrite = Time.unscaledTime + 0.5f;
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
                ["presence"] = app != null ? app.Presence : "",
                ["cupHeld"] = app != null && app.CupHeld,
                ["voicePlaying"] = app != null && app.VoicePlaying,
                ["story"] = app != null ? app.StoryState : "none",
                ["live"] = app != null ? app.LiveState : "none",
                ["driftMs"] = app != null ? Math.Round(app.LastDriftMs) : 0,
                // what can be pressed right now, with where it is: lets a test aim simulated hands at real targets
                ["targets"] = Targets(),
            };
            _frames = 0;
            _frameTime = 0;
            try { File.WriteAllText(Path.Combine(_dir, "state.json"), JsonConvert.SerializeObject(state, Formatting.Indented)); } catch (IOException) { /* next tick */ }
        }

        static float Round(float v) => (float)Math.Round(v, 3);
        static float[] V(Vector3 v) => new[] { Round(v.x), Round(v.y), Round(v.z) };

        static object HandPositions()
        {
            var rig = FindAnyObjectByType<OVRCameraRig>();
            if (rig == null) return null;
            return new { left = V(rig.leftHandAnchor.position), right = V(rig.rightHandAnchor.position) };
        }

        static List<object> Targets()
        {
            var list = new List<object>();
            foreach (var p in FindObjectsByType<Pressable>(FindObjectsSortMode.None))
            {
                if (!p.isActiveAndEnabled || !p.Interactable) continue;
                var t = p.transform;
                list.Add(new { id = p.Id, at = V(t.position), normal = V(-t.forward), size = new[] { Round(p.Size.x), Round(p.Size.y) } });
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
