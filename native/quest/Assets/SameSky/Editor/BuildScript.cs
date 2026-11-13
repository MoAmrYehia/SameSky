// Reproducible builds (scripts/native/build.ps1 wraps these):
//   Quest APK  : -executeMethod SameSky.EditorTools.BuildScript.Android  -sameskyApi=<url> [-sameskyEnv=lan-dev] [-sameskyRelease]
//                [-sameskySampleOnly] [-sameskyDebugSigned] [-sameskyVersionCode=<n>] [-sameskyOut=<apk>]
//   Simulator  : -executeMethod SameSky.EditorTools.BuildScript.Windows  -sameskyApi=http://127.0.0.1:8000 [-sameskyOut=<exe>]
// Development builds allow cleartext HTTP (LAN testing) and Unity's development flags. Release builds require an
// https API URL, or -sameskySampleOnly for a build with no server at all (it offers the sample day only: the build
// given to the competition's judges), and a keystore from environment variables (SAMESKY_KEYSTORE,
// SAMESKY_KEYSTORE_PASS, SAMESKY_KEY_ALIAS, SAMESKY_KEY_PASS); nothing secret is ever written into the repository.
// -sameskyDebugSigned makes a release-configuration build signed with Android's debug key instead: for looking at
// what a release build contains, never for uploading.

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SameSky.App;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SameSky.EditorTools
{
    public static class BuildScript
    {
        const string ConfigPath = "Assets/SameSky/Resources/SameSkyConfig.json";

        static string Arg(string name, string fallback = null)
        {
            var prefix = "-" + name + "=";
            var hit = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            return hit != null ? hit.Substring(prefix.Length) : fallback;
        }

        static bool Flag(string name) => Environment.GetCommandLineArgs().Any(a => string.Equals(a, "-" + name, StringComparison.OrdinalIgnoreCase));

        public static void Android()
        {
            var release = Flag("sameskyRelease");
            var sampleOnly = Flag("sameskySampleOnly");
            var debugSigned = Flag("sameskyDebugSigned");
            var api = sampleOnly ? "" : Arg("sameskyApi", release ? null : "http://127.0.0.1:8000");
            if (release && !sampleOnly && (api == null || !api.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                Fail("Release builds need -sameskyApi=https://... (cleartext is development-only), or -sameskySampleOnly for a build with no server.");
            var output = Arg("sameskyOut", release ? "Builds/Android/SameSky-release.apk" : "Builds/Android/SameSky-dev.apk");
            WriteConfig(api, Arg("sameskyEnv", sampleOnly ? "sample-only" : release ? "hosted" : "lan-dev"), release ? "release" : "dev");
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            // OpenXR's build-time Meta Quest checks read the *selected* group, which batch mode leaves on Standalone
            // (a null-reference exception in MetaQuestFeature's validation otherwise)
            EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;
            EditorUserBuildSettings.buildAppBundle = false;
            // The dashboard wants a higher version code with every upload, so a release build can be given one.
            var committedCode = PlayerSettings.Android.bundleVersionCode;
            if (int.TryParse(Arg("sameskyVersionCode"), out var code) && code > 0) PlayerSettings.Android.bundleVersionCode = code;
            var signing = "debug key";
            if (release && !debugSigned) { ConfigureSigning(); signing = "release keystore"; }
            else PlayerSettings.Android.useCustomKeystore = false;
            if (release && debugSigned) signing = "debug key (a release-configuration check build: not for upload)";
            var committedIl2CppArgs = PlayerSettings.GetAdditionalIl2CppArgs();
            var pathMap = HomePathMap();
            if (pathMap != null) PlayerSettings.SetAdditionalIl2CppArgs(pathMap);
            var ok = false;
            try { ok = Build(BuildTarget.Android, output, release ? BuildOptions.None : BuildOptions.Development, signing, sampleOnly); }
            finally
            {
                // the keystore's path and alias, this build's version code and this PC's folders are this run's own:
                // they don't belong in ProjectSettings.asset (build.ps1 also puts that file back as it was)
                ClearSigning();
                PlayerSettings.Android.bundleVersionCode = committedCode;
                PlayerSettings.SetAdditionalIl2CppArgs(committedIl2CppArgs);
            }
            Finish(ok);
        }

        /// <summary>
        /// Unity compiles its own IL2CPP runtime sources into the app, and some of their file names go in with them
        /// (the runtime's "not supported" messages name the source file). With Unity or the project under the user's
        /// folder, that is the Windows user name inside every APK. This tells the compiler to write such paths
        /// without the part that says whose PC it was: C:/Users/name/Unity/... becomes ~/Unity/...
        /// </summary>
        static string HomePathMap()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrEmpty(home)) return null;
            var places = new[] { EditorApplication.applicationContentsPath, Path.GetFullPath(".") }.Select(p => p.Replace('\\', '/'));
            if (!places.Any(p => p.StartsWith(home + "/", StringComparison.OrdinalIgnoreCase))) return null;   // nothing of the user's in those paths
            if (home.Contains(" "))
            {
                Debug.LogWarning("[SameSky:build] the user folder's name has a space in it, so its path can't be mapped out of the build: the APK will contain it");
                return null;
            }
            // the compiler matches the text of the path as it was given, so both ways of writing the drive letter
            var flags = string.Join(" ", new[] { home, char.ToLowerInvariant(home[0]) + home.Substring(1), char.ToUpperInvariant(home[0]) + home.Substring(1) }
                .Distinct().Select(h => $"-fmacro-prefix-map={h}=~"));
            return $"--compiler-flags=\"{flags}\"";
        }

        public static void Windows()
        {
            var output = Arg("sameskyOut", "Builds/Windows/SameSky.exe");
            WriteConfig(Arg("sameskyApi", "http://127.0.0.1:8000"), "simulator", "dev");
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            Finish(Build(BuildTarget.StandaloneWindows64, output, BuildOptions.Development, "none", false));
        }

        static void ConfigureSigning()
        {
            string Env(string k) => Environment.GetEnvironmentVariable(k) ?? Fail<string>($"Release signing needs the {k} environment variable (scripts/native/release.ps1 sets them from a password you type).");
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = Env("SAMESKY_KEYSTORE");
            PlayerSettings.Android.keystorePass = Env("SAMESKY_KEYSTORE_PASS");
            PlayerSettings.Android.keyaliasName = Env("SAMESKY_KEY_ALIAS");
            PlayerSettings.Android.keyaliasPass = Env("SAMESKY_KEY_PASS");
        }

        static void ClearSigning()
        {
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.Android.keystoreName = "";
            PlayerSettings.Android.keystorePass = "";
            PlayerSettings.Android.keyaliasName = "";
            PlayerSettings.Android.keyaliasPass = "";
        }

        static void WriteConfig(string api, string environment, string label)
        {
            var commit = Environment.GetEnvironmentVariable("SAMESKY_COMMIT") ?? "";
            var data = new AppConfigData { apiUrl = api, environment = environment, buildLabel = label, commit = commit };
            File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(data, Formatting.Indented));
            AssetDatabase.ImportAsset(ConfigPath);
            Debug.Log($"[SameSky:build] config {environment} {(string.IsNullOrEmpty(api) ? "(no server: sample day only)" : api)} {label} {commit}");
        }

        static bool Build(BuildTarget target, string output, BuildOptions options, string signing, bool sampleOnly)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = output,
                target = target,
                options = options,
            });
            var s = report.summary;
            var summary = new
            {
                target = target.ToString(),
                result = s.result.ToString(),
                output = Path.GetFullPath(output),
                // the file itself: on Android the report's totalSize counts every built file, not the APK
                sizeBytes = File.Exists(output) ? new FileInfo(output).Length : (long)s.totalSize,
                reportTotalSize = s.totalSize,
                seconds = Math.Round(s.totalTime.TotalSeconds, 1),
                errors = s.totalErrors,
                warnings = s.totalWarnings,
                unity = Application.unityVersion,
                packageId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target))),
                version = PlayerSettings.bundleVersion,
                versionCode = target == BuildTarget.Android ? PlayerSettings.Android.bundleVersionCode : 0,
                development = (options & BuildOptions.Development) != 0,
                sampleOnly,
                signing,
            };
            File.WriteAllText(Path.ChangeExtension(Path.GetFullPath(output), ".build.json"), JsonConvert.SerializeObject(summary, Formatting.Indented));
            Debug.Log("[SameSky:build] " + JsonConvert.SerializeObject(summary));
            return s.result == BuildResult.Succeeded;
        }

        static void Finish(bool ok)
        {
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        static void Fail(string message) => Fail<bool>(message);

        static T Fail<T>(string message)
        {
            Debug.LogError("[SameSky:build] " + message);
            if (Application.isBatchMode) EditorApplication.Exit(2);
            throw new BuildFailedException(message);
        }
    }
}
