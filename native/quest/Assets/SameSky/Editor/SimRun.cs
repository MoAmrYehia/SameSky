// Play mode in Meta XR Simulator.
//   * Menu: SameSky > Play in Meta XR Simulator (after Window > Meta > Meta XR Simulator > Activate, or with
//     XR_RUNTIME_JSON pointing at the simulator's runtime manifest for this editor process).
//   * Command line (scripts/native/play_in_sim.ps1): Unity -projectPath native/quest -executeMethod SameSky.EditorTools.SimRun.Play
// The request survives the script recompiles and domain reloads that happen while the editor starts (SessionState),
// and the log records which OpenXR runtime the session used, so a run can't silently fall back to another runtime.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SameSky.EditorTools
{
    [InitializeOnLoad]
    public static class SimRun
    {
        const string PendingKey = "SameSky.SimRun.PlayPending";

        static SimRun()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode) return;
                var manifest = System.Environment.GetEnvironmentVariable("XR_RUNTIME_JSON");
                Debug.Log($"[SameSky:sim] play mode; XR_RUNTIME_JSON={(string.IsNullOrEmpty(manifest) ? "(not set: Meta's activation or the system runtime)" : manifest)}");
            };
            // After a domain reload, finish a play request made before it.
            if (SessionState.GetBool(PendingKey, false)) EditorApplication.delayCall += TryPlay;
        }

        [MenuItem("SameSky/Play in Meta XR Simulator")]
        public static void Play()
        {
            SessionState.SetBool(PendingKey, true);
            EditorApplication.delayCall += TryPlay;
        }

        static void TryPlay()
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) { SessionState.SetBool(PendingKey, false); return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryPlay; // still importing or compiling: try again on the next tick
                return;
            }
            SessionState.SetBool(PendingKey, false);
            EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("SameSky/Stop playing")]
        public static void Stop() => EditorApplication.ExitPlaymode();
    }
}
