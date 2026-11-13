// What a build that is given to other people must not carry from the PC that made it.
//
// Meta's XR SDK writes this PC's address on the local network, and a token for its editor bridge, into
// Resources/DevAgentSettings.asset before every build (its DevAgentBuildProcessor, for the in-headset AI assistant of
// the Immersive Debugger, which this app leaves switched off), and puts the old values back afterwards. In a
// development build that is what the tool is for. In any other build it is this PC's address shipped inside the APK
// for nothing. This step runs after the SDK's and empties both for every build that isn't a development build.
//
// (The other thing of this kind, the Windows user name in the file paths of Unity's IL2CPP runtime sources, is taken
// out by BuildScript.HomePathMap. scripts/native/build.ps1 reads a release APK back and stops if either is in it.)

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SameSky.EditorTools
{
    public sealed class ReleasePrivacy : IPreprocessBuildWithReport
    {
        public int callbackOrder => 1000;   // after the SDK's own build steps (its DevAgentBuildProcessor is 1)

        public void OnPreprocessBuild(BuildReport report)
        {
            if ((report.summary.options & BuildOptions.Development) != 0) return;
            var settings = Resources.Load<ScriptableObject>("DevAgentSettings");
            if (settings == null) return;
            var serialized = new SerializedObject(settings);
            var emptied = 0;
            foreach (var name in new[] { "serverAddress", "accessToken" })
            {
                var field = serialized.FindProperty(name);
                if (field == null || field.propertyType != SerializedPropertyType.String || field.stringValue == "") continue;
                field.stringValue = "";
                emptied++;
            }
            if (emptied == 0) return;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            Debug.Log("[SameSky:build] not a development build: the SDK's developer-tool address and token are left out of it");
        }
    }
}
