// Play mode in Meta XR Simulator.
//   * Menu: SameSky > Play in Meta XR Simulator (after Window > Meta > Meta XR Simulator > Activate, or with
//     XR_RUNTIME_JSON pointing at the simulator's runtime manifest for this editor process).
//   * Command line (scripts/native/play_in_sim.ps1): Unity -projectPath native/quest -executeMethod SameSky.EditorTools.SimRun.Play
// Logs which OpenXR runtime the session actually used, so a run can't silently fall back to another runtime.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SameSky.EditorTools
{
    [InitializeOnLoad]
    public static class SimRun
    {
        static SimRun()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode) return;
                var manifest = System.Environment.GetEnvironmentVariable("XR_RUNTIME_JSON");
                Debug.Log($"[SameSky:sim] play mode; XR_RUNTIME_JSON={(string.IsNullOrEmpty(manifest) ? "(not set: Meta's activation or the system runtime)" : manifest)}");
            };
        }

        [MenuItem("SameSky/Play in Meta XR Simulator")]
        public static void Play()
        {
            if (EditorApplication.isPlaying) return;
            EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            EditorApplication.delayCall += () => EditorApplication.EnterPlaymode();
        }

        [MenuItem("SameSky/Stop playing")]
        public static void Stop() => EditorApplication.ExitPlaymode();
    }
}
