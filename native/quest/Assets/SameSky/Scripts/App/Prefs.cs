// Persistent local state, stored as JSON in Application.persistentDataPath (app-private storage on Quest).
//   prefs.json         comfort and accessibility preferences, last mode/room, first-run progress
//   session.json       the signed-in account's API token (live mode only)
//   sample-state.json  everything done in the offline sample; never mixed with live rooms
// Writes go to a temp file first and are then swapped in, so a crash mid-write never corrupts a file.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace SameSky.App
{
    public static class LocalStore
    {
        static string _root;

        /// <summary>Per-profile data folder. "-sameskyProfile=b" lets two simulator instances on one PC keep
        /// separate sessions and preferences (Meta XR Simulator multi-instance testing).</summary>
        public static string Root
        {
            get
            {
                if (_root != null) return _root;
                var profile = "";
                foreach (var arg in Environment.GetCommandLineArgs())
                    if (arg.StartsWith("-sameskyProfile=", StringComparison.OrdinalIgnoreCase))
                        profile = arg.Substring("-sameskyProfile=".Length).Trim();
                foreach (var c in Path.GetInvalidFileNameChars()) profile = profile.Replace(c, '_');
                _root = string.IsNullOrEmpty(profile) ? Application.persistentDataPath : Path.Combine(Application.persistentDataPath, "profile-" + profile);
                return _root;
            }
        }

        public static T Load<T>(string name) where T : new()
        {
            var path = Path.Combine(Root, name);
            try
            {
                if (File.Exists(path)) return JsonConvert.DeserializeObject<T>(File.ReadAllText(path)) ?? new T();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SameSky] {name} unreadable, starting fresh ({e.GetType().Name})");
            }
            return new T();
        }

        public static void Save<T>(string name, T value)
        {
            var path = Path.Combine(Root, name);
            var tmp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Root);
                File.WriteAllText(tmp, JsonConvert.SerializeObject(value, Formatting.Indented));
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SameSky] could not save {name}: {e.Message}");
            }
        }

        public static void Delete(string name)
        {
            var path = Path.Combine(Root, name);
            try { if (File.Exists(path)) File.Delete(path); } catch (Exception) { /* best effort */ }
        }
    }

    [Serializable]
    public sealed class Prefs
    {
        // comfort and accessibility
        public bool captionsOn = true;
        public float textScale = 1f;      // 1, 1.25 or 1.5
        public float volume = 0.9f;       // 0..1, master
        public bool soundOn = true;
        public float seatHeight = 0f;     // metres added to the seated eye height after Recenter
        public bool reduceMotion = false;

        // where the person was
        public string mode;               // "sample" | "live" | null (never chose)
        public string lastRoomId;
        public bool sampleIntroDone;      // has heard the waiting voice note at least once
        public string setupStep;          // first-run step to resume ("about", "room", "invite", ...)

        // development only (ignored in release builds)
        public string apiUrlOverride;

        static Prefs _current;
        public static Prefs Current => _current ??= LocalStore.Load<Prefs>("prefs.json");
        public static event Action Changed;

        public static void Save()
        {
            LocalStore.Save("prefs.json", Current);
            Changed?.Invoke();
        }
    }

    [Serializable]
    public sealed class StoredSession
    {
        public string token;
        public string userId;
        public string email;
        public string apiUrl;   // the server this token belongs to
        public string savedAt;

        public static StoredSession Load() => LocalStore.Load<StoredSession>("session.json");
        public static void Store(StoredSession s) => LocalStore.Save("session.json", s);
        public static void Clear() => LocalStore.Delete("session.json");
    }

    /// <summary>What happened in the offline sample. Kept apart from live data on purpose.</summary>
    [Serializable]
    public sealed class SampleState
    {
        public List<SampleReply> replies = new();
        public Dictionary<string, float[]> placements = new();  // objectKey -> x, y, z, yaw
        public Dictionary<string, string> reactions = new();    // momentId -> kind
        public bool recapApprovedByMe;
        public bool voiceNoteHeard;
        public bool storyFinished;
        public int heartsSent;

        static SampleState _current;
        public static SampleState Current => _current ??= LocalStore.Load<SampleState>("sample-state.json");
        public static void Save() => LocalStore.Save("sample-state.json", Current);

        public static void Reset()
        {
            _current = new SampleState();
            Save();
        }
    }

    [Serializable]
    public sealed class SampleReply
    {
        public string id;
        public string momentId;
        public string text;
        public string createdAt;
        public float[] place; // x, y, z, yaw beside the object it answers
    }
}
