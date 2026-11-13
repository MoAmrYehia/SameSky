// Where the native app finds the SameSky API, per environment.
//
//   Editor / Meta XR Simulator  -> http://127.0.0.1:8000       (the API on this PC)
//   Headset on the same Wi-Fi   -> http://<this PC's LAN IP>:8000 (development builds only)
//   Hosted                      -> https://... (release builds: TLS only)
//   No server at all            -> "" (a sample-only build: scripts/native/build.ps1 -SampleOnly)
//
// "localhost" on a headset is the headset itself, so a headset build must be given the PC's LAN address.
// Release builds refuse cleartext URLs (and Unity's "Allow downloads over HTTP" is development-only).
// A build with no address it may use is sample-only: it offers the sample day and nothing that needs an account.

using System;
using Newtonsoft.Json;
using UnityEngine;

namespace SameSky.App
{
    [Serializable]
    public sealed class AppConfigData
    {
        /// <summary>API base URL baked in at build time (scripts/native/build.ps1 -ApiUrl ...).</summary>
        public string apiUrl = "http://127.0.0.1:8000";
        /// <summary>"editor", "lan-dev", "hosted" or "sample-only". Informational, shown in Settings > About.</summary>
        public string environment = "editor";
        public string buildLabel = "dev";
        public string commit = "";
    }

    public static class AppConfig
    {
        const string ResourceName = "SameSkyConfig";
        static AppConfigData _data;

        public static AppConfigData Data => _data ??= Load();

        static AppConfigData Load()
        {
            var asset = Resources.Load<TextAsset>(ResourceName);
            var data = asset != null ? JsonConvert.DeserializeObject<AppConfigData>(asset.text) : new AppConfigData();
            // Command line (Windows simulator builds and Editor batch runs): -sameskyApi=http://host:port, or
            // -sameskySampleOnly to run as a build with no server (how the acceptance run checks that kind of build)
            var sampleOnly = false;
            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("-sameskyApi=", StringComparison.OrdinalIgnoreCase))
                    data.apiUrl = arg.Substring("-sameskyApi=".Length);
                else if (string.Equals(arg, "-sameskySampleOnly", StringComparison.OrdinalIgnoreCase))
                    sampleOnly = true;
            }
            if (sampleOnly) { data.apiUrl = ""; data.environment = "sample-only"; }
            return data;
        }

        /// <summary>The API URL to use now: a developer override from Settings (development builds only) or the baked one.</summary>
        public static string ApiUrl
        {
            get
            {
                var overrideUrl = Debug.isDebugBuild ? Prefs.Current.apiUrlOverride : null;
                var url = string.IsNullOrWhiteSpace(overrideUrl) ? Data.apiUrl : overrideUrl.Trim();
                return IsAllowed(url) ? url.TrimEnd('/') : "";
            }
        }

        /// <summary>Release builds accept only https; development builds may also use http (LAN testing).</summary>
        public static bool IsAllowed(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return true;
            return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && (Debug.isDebugBuild || Application.isEditor);
        }

        /// <summary>
        /// True when this build has no SameSky service to talk to: it was built without an API address (the build
        /// given to the competition's judges), or with one this kind of build may not use. The app then offers the
        /// sample day only, and shows nothing that needs an account.
        /// </summary>
        public static bool SampleOnly => string.IsNullOrEmpty(ApiUrl);

        public static string Describe() =>
            $"{Data.environment} · {(SampleOnly ? "sample day only, no server" : ApiUrl)} · {Application.version} {Data.buildLabel}{(string.IsNullOrEmpty(Data.commit) ? "" : " · " + Data.commit)}";
    }
}
