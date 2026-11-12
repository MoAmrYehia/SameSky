// Play mode in Meta XR Simulator.
//   * Menu: SameSky > Play in Meta XR Simulator (after Window > Meta > Meta XR Simulator > Activate, or with
//     XR_RUNTIME_JSON pointing at the simulator's runtime manifest for this editor process).
//   * Command line (scripts/native/play_in_sim.ps1): Unity -projectPath native/quest -executeMethod SameSky.EditorTools.SimRun.Play
//   * A running editor (scripts/native/acceptance.ps1): a request file, Temp/SameSkySim/request.txt, holding
//     "play <id>" or "stop <id>". The editor answers in Temp/SameSkySim/ack.txt with "<id> <action> <what it did>".
// A play request survives the script recompiles and domain reloads that happen while the editor starts
// (SessionState), and the log records which OpenXR runtime the session used, so a run can't silently fall back to
// another runtime.
//
// Everything here runs from EditorApplication.update, the editor's own tick, which keeps going while another window
// is in front. Two things that looked equivalent are not (both found on 12 November, when runs couldn't start their
// sessions):
//   * EditorApplication.delayCall only runs while the editor is the foreground application. An editor started behind
//     the simulator window sat on its play request until someone clicked it.
//   * A Ctrl+P pressed by a script depends on which editor window has the keyboard, and on Windows handing the
//     editor the foreground at all.

using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SameSky.EditorTools
{
    [InitializeOnLoad]
    public static class SimRun
    {
        const string PendingKey = "SameSky.SimRun.PlayPending";

        static bool _leavingPlayMode;
        static double _nextTick;
        static string _requestDir;

        static SimRun()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode) _leavingPlayMode = true;
                if (state == PlayModeStateChange.EnteredEditMode) _leavingPlayMode = false;
                if (state != PlayModeStateChange.EnteredPlayMode) return;
                var manifest = Environment.GetEnvironmentVariable("XR_RUNTIME_JSON");
                Debug.Log($"[SameSky:sim] play mode; XR_RUNTIME_JSON={(string.IsNullOrEmpty(manifest) ? "(not set: Meta's activation or the system runtime)" : manifest)}");
            };
            EditorApplication.update += Tick;
        }

        /// <summary>Asks for Play mode; it starts on a following tick, once nothing is importing or compiling.</summary>
        [MenuItem("SameSky/Play in Meta XR Simulator")]
        public static void Play() => SessionState.SetBool(PendingKey, true);

        [MenuItem("SameSky/Stop playing")]
        public static void Stop()
        {
            SessionState.SetBool(PendingKey, false);
            EditorApplication.ExitPlaymode();
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < _nextTick) return;
            _nextTick = EditorApplication.timeSinceStartup + 0.2;
            ReadRequest();
            if (SessionState.GetBool(PendingKey, false)) TryPlay();
        }

        static void TryPlay()
        {
            if (_leavingPlayMode) return;   // asked while the last session was still closing: start after it
            if (EditorApplication.isPlayingOrWillChangePlaymode) { SessionState.SetBool(PendingKey, false); return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;   // importing or compiling: later
            SessionState.SetBool(PendingKey, false);
            if (SceneManager.GetActiveScene().path != ProjectSetup.ScenePath) EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            EditorApplication.EnterPlaymode();
        }

        static void ReadRequest()
        {
            try
            {
                if (_requestDir == null) _requestDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "SameSkySim"));
                var path = Path.Combine(_requestDir, "request.txt");
                if (!File.Exists(path)) return;
                var parts = File.ReadAllText(path).Trim().Split(' ');
                File.Delete(path);
                var action = parts[0];
                var id = parts.Length > 1 ? parts[1] : "";
                string did;
                if (action == "play")
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode && !_leavingPlayMode) did = "already";
                    else { did = _leavingPlayMode ? "queued" : "starting"; Play(); }
                }
                else if (action == "stop")
                {
                    SessionState.SetBool(PendingKey, false);
                    if (EditorApplication.isPlaying && !_leavingPlayMode) { did = "stopping"; EditorApplication.ExitPlaymode(); }
                    else did = "already";
                }
                else did = "unknown";
                Debug.Log($"[SameSky:sim] request {action}: {did}");
                File.WriteAllText(Path.Combine(_requestDir, "ack.txt"), $"{id} {action} {did}");
            }
            catch (IOException) { }                  // the file is still being written, or the answer couldn't be: next tick
            catch (UnauthorizedAccessException) { }
        }
    }
}
