// Reproducible builds (scripts/native/build.ps1 wraps these):
//   Quest APK  : -executeMethod SameSky.EditorTools.BuildScript.Android  -sameskyApi=<url> [-sameskyEnv=lan-dev] [-sameskyRelease] [-sameskyOut=<apk>]
//   Simulator  : -executeMethod SameSky.EditorTools.BuildScript.Windows  -sameskyApi=http://127.0.0.1:8000 [-sameskyOut=<exe>]
// Development builds allow cleartext HTTP (LAN testing) and Unity's development flags. Release builds require an
// https API URL and a keystore from environment variables (SAMESKY_KEYSTORE, SAMESKY_KEYSTORE_PASS,
// SAMESKY_KEY_ALIAS, SAMESKY_KEY_PASS); nothing secret is ever written into the repository.

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
            var api = Arg("sameskyApi", release ? null : "http://127.0.0.1:8000");
            if (release && (api == null || !api.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                Fail("Release builds need -sameskyApi=https://... (cleartext is development-only).");
            var output = Arg("sameskyOut", release ? "Builds/Android/SameSky-release.apk" : "Builds/Android/SameSky-dev.apk");
            WriteConfig(api, Arg("sameskyEnv", release ? "hosted" : "lan-dev"), release ? "release" : "dev");
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            EditorUserBuildSettings.buildAppBundle = false;
            if (release) ConfigureSigning();
            else PlayerSettings.Android.useCustomKeystore = false;
            Build(BuildTarget.Android, output, release ? BuildOptions.None : BuildOptions.Development);
        }

        public static void Windows()
        {
            var output = Arg("sameskyOut", "Builds/Windows/SameSky.exe");
            WriteConfig(Arg("sameskyApi", "http://127.0.0.1:8000"), "simulator", "dev");
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            Build(BuildTarget.StandaloneWindows64, output, BuildOptions.Development);
        }

        static void ConfigureSigning()
        {
            string Env(string k) => Environment.GetEnvironmentVariable(k) ?? Fail<string>($"Release signing needs the {k} environment variable.");
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = Env("SAMESKY_KEYSTORE");
            PlayerSettings.Android.keystorePass = Env("SAMESKY_KEYSTORE_PASS");
            PlayerSettings.Android.keyaliasName = Env("SAMESKY_KEY_ALIAS");
            PlayerSettings.Android.keyaliasPass = Env("SAMESKY_KEY_PASS");
        }

        static void WriteConfig(string api, string environment, string label)
        {
            var commit = Environment.GetEnvironmentVariable("SAMESKY_COMMIT") ?? "";
            var data = new AppConfigData { apiUrl = api, environment = environment, buildLabel = label, commit = commit };
            File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(data, Formatting.Indented));
            AssetDatabase.ImportAsset(ConfigPath);
            Debug.Log($"[SameSky:build] config {environment} {api} {label} {commit}");
        }

        static void Build(BuildTarget target, string output, BuildOptions options)
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
                sizeBytes = s.totalSize,
                seconds = Math.Round(s.totalTime.TotalSeconds, 1),
                errors = s.totalErrors,
                warnings = s.totalWarnings,
                unity = Application.unityVersion,
                packageId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target))),
                version = PlayerSettings.bundleVersion,
                development = (options & BuildOptions.Development) != 0,
            };
            File.WriteAllText(Path.ChangeExtension(Path.GetFullPath(output), ".build.json"), JsonConvert.SerializeObject(summary, Formatting.Indented));
            Debug.Log("[SameSky:build] " + JsonConvert.SerializeObject(summary));
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
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
