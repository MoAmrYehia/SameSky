// The two explicit data modes:
//   SampleData - the bundled, fictional sample day. No account, no network, no provider keys. Everything the
//                person does is kept in sample-state.json, apart from any live account.
//   LiveData   - the signed-in person's real rooms through the SameSky API. If a call fails, the failure is
//                shown and can be retried; live mode never falls back to sample content.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SameSky.Api;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Video;

namespace SameSky.App
{
    public interface IRoomData
    {
        bool IsSample { get; }
        Me Me { get; }
        List<Room> Rooms { get; }
        Task Load(CancellationToken ct = default);
        Task<List<Moment>> Moments(string roomId, CancellationToken ct = default);
        Task<List<Recap>> Recaps(string roomId, CancellationToken ct = default);
        Task<List<Recap>> PendingRecaps(string roomId, CancellationToken ct = default);
        Task<Recap> Approve(string recapId, CancellationToken ct = default);
        Task<List<Reply>> Replies(string momentId, CancellationToken ct = default);
        Task<Reply> AddReply(string momentId, string text, string clientKey, CancellationToken ct = default);
        Task React(string momentId, string kind, CancellationToken ct = default);
        Task<List<RoomObject>> Objects(string roomId, CancellationToken ct = default);
        Task<RoomObject> Place(string roomId, string objectType, string momentId, string replyId, Vector3 p, float yaw, CancellationToken ct = default);
        Task<RoomObject> Move(RoomObject o, Vector3 p, float yaw, CancellationToken ct = default);
        Task<List<Award>> Awards(string roomId, CancellationToken ct = default);
        Task<List<InboxItem>> Inbox(CancellationToken ct = default);
        Task MarkRead(string id, CancellationToken ct = default);
        Media Media { get; }
    }

    // ------------------------------------------------------------------------------------------------ media

    /// <summary>Loads images, voice and video from bundled sample resources ("res:...") or signed API URLs (cached on disk).</summary>
    public sealed class Media
    {
        readonly ApiClient _api;
        readonly Dictionary<string, Texture2D> _textures = new();
        readonly Dictionary<string, AudioClip> _audio = new();
        static string CacheDir => Path.Combine(Application.temporaryCachePath, "media");

        public Media(ApiClient api) => _api = api;

        public async Task<Texture2D> Image(MediaInfo m, bool thumb, CancellationToken ct = default)
        {
            var url = thumb ? (m.ThumbUrl ?? m.PosterUrl ?? m.Url) : (m.Kind == "video" ? m.PosterUrl ?? m.ThumbUrl : m.Url);
            if (string.IsNullOrEmpty(url)) return null;
            var key = $"{m.Id}:{(thumb ? "thumb" : "display")}";
            if (_textures.TryGetValue(key, out var cached) && cached) return cached;
            Texture2D tex;
            if (url.StartsWith("res:")) tex = Resources.Load<Texture2D>(url.Substring(4));
            else
            {
                var bytes = await Cached(key + ".jpg", url, ct);
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = key, wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
                if (!tex.LoadImage(bytes, true)) throw new ApiException(0, "bad_image", "This photo couldn't be shown.");
            }
            if (tex) _textures[key] = tex;
            return tex;
        }

        public async Task<AudioClip> Audio(MediaInfo m, CancellationToken ct = default)
        {
            if (_audio.TryGetValue(m.Id, out var cached) && cached) return cached;
            AudioClip clip;
            if (m.Url != null && m.Url.StartsWith("res:")) clip = Resources.Load<AudioClip>(m.Url.Substring(4));
            else
            {
                if (string.IsNullOrEmpty(m.OggUrl))
                    throw new ApiException(0, "no_native_audio", "This voice note is still being prepared for the headset. Try again in a moment.", true);
                var path = await CachedPath(m.Id + ".ogg", m.OggUrl, ct);
                using var req = UnityWebRequestMultimedia.GetAudioClip("file://" + path, AudioType.OGGVORBIS);
                await ApiClient.SendAsync(req, ct);
                if (req.result != UnityWebRequest.Result.Success) throw new ApiException(0, "bad_audio", "This voice note couldn't be played.");
                clip = DownloadHandlerAudioClip.GetContent(req);
            }
            if (clip) _audio[m.Id] = clip;
            return clip;
        }

        /// <summary>Points a VideoPlayer at the clip: a bundled VideoClip, or a cached file of the signed URL.</summary>
        public async Task Prepare(VideoPlayer player, MediaInfo m, CancellationToken ct = default)
        {
            if (m.Url.StartsWith("res:"))
            {
                player.source = VideoSource.VideoClip;
                player.clip = Resources.Load<VideoClip>(m.Url.Substring(4));
                return;
            }
            var path = await CachedPath(m.Id + ".mp4", m.Url, ct);
            player.source = VideoSource.Url;
            player.url = "file://" + path;
        }

        async Task<byte[]> Cached(string name, string url, CancellationToken ct) => File.ReadAllBytes(await CachedPath(name, url, ct));

        async Task<string> CachedPath(string name, string url, CancellationToken ct)
        {
            Directory.CreateDirectory(CacheDir);
            var path = Path.Combine(CacheDir, Safe(name));
            if (File.Exists(path) && new FileInfo(path).Length > 0) return path;
            var bytes = await _api.Download(url, ct);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        static string Safe(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(':', '_');
        }

        /// <summary>Forget cached media (sign-out, or access revoked): nothing private lingers on the device.</summary>
        public static void ClearCache()
        {
            try { if (Directory.Exists(CacheDir)) Directory.Delete(CacheDir, true); } catch (Exception) { /* best effort */ }
        }
    }

    // ------------------------------------------------------------------------------------------------ live

    public sealed class LiveData : IRoomData
    {
        readonly ApiClient _api;
        public bool IsSample => false;
        public Me Me { get; private set; }
        public List<Room> Rooms { get; private set; } = new();
        public Media Media { get; }
        public ApiClient Api => _api;

        public LiveData(ApiClient api)
        {
            _api = api;
            Media = new Media(api);
        }

        public async Task Load(CancellationToken ct = default)
        {
            Me = await _api.Me(ct);
            Rooms = await _api.Rooms(ct) ?? new List<Room>();
        }

        public Task<List<Moment>> Moments(string roomId, CancellationToken ct = default) => _api.Moments(roomId, null, ct);
        public Task<List<Recap>> Recaps(string roomId, CancellationToken ct = default) => _api.Recaps(roomId, ct);
        public Task<List<Recap>> PendingRecaps(string roomId, CancellationToken ct = default) => _api.PendingRecaps(roomId, ct);
        public Task<Recap> Approve(string recapId, CancellationToken ct = default) => _api.ApproveRecap(recapId, ct);
        public Task<List<Reply>> Replies(string momentId, CancellationToken ct = default) => _api.Replies(momentId, ct);
        public Task<Reply> AddReply(string momentId, string text, string clientKey, CancellationToken ct = default) => _api.AddReply(momentId, text, clientKey, ct);
        public Task React(string momentId, string kind, CancellationToken ct = default) => _api.React(momentId, kind, ct);
        public Task<List<RoomObject>> Objects(string roomId, CancellationToken ct = default) => _api.Objects(roomId, ct);
        public Task<RoomObject> Place(string roomId, string type, string momentId, string replyId, Vector3 p, float yaw, CancellationToken ct = default) =>
            _api.CreateObject(roomId, type, momentId, replyId, p, yaw, ct);
        public Task<RoomObject> Move(RoomObject o, Vector3 p, float yaw, CancellationToken ct = default) => _api.MoveObject(o.Id, o.Version, p, yaw, ct);
        public Task<List<Award>> Awards(string roomId, CancellationToken ct = default) => _api.Awards(roomId, ct);
        public Task<List<InboxItem>> Inbox(CancellationToken ct = default) => _api.Notifications(ct);
        public async Task MarkRead(string id, CancellationToken ct = default) => await _api.MarkRead(id, ct);
    }

    // ------------------------------------------------------------------------------------------------ sample

    public sealed class SampleData : IRoomData
    {
        public bool IsSample => true;
        public Me Me { get; private set; }
        public List<Room> Rooms { get; private set; } = new();
        public Media Media { get; } = new(null);

        public JObject Raw { get; private set; }
        public string Day { get; private set; }
        public Dictionary<string, JObject> People { get; } = new();
        List<Moment> _moments = new();
        List<Recap> _recaps = new();
        List<Reply> _seedReplies = new();
        Dictionary<string, List<Award>> _awards = new();
        List<InboxItem> _inbox = new();

        public const string MayaVoiceNote = "m-maya-voice";
        public const string DirectRoom = "sample-direct";

        public Task Load(CancellationToken ct = default)
        {
            var text = Resources.Load<TextAsset>("Sample/sample-day")?.text
                       ?? throw new InvalidOperationException("The bundled sample is missing from this build.");
            Raw = JObject.Parse(text);
            Day = (string)Raw["day"];
            var me = (string)Raw["me"];
            foreach (var p in (JObject)Raw["people"]) People[p.Key] = (JObject)p.Value;
            Me = new Me
            {
                Id = me, DisplayName = (string)People[me]["name"], TimeZone = (string)People[me]["zone"],
                Email = "sample@samesky.invalid", Avatar = new AvatarView { Kind = "preset_demo", Definition = (JObject)People[me]["avatar"], Version = 1 },
            };
            Rooms = Raw["rooms"].ToObject<List<Room>>();
            _moments = Raw["moments"].ToObject<List<Moment>>();
            _recaps = Raw["recaps"].ToObject<List<Recap>>();
            _seedReplies = Raw["replies"].ToObject<List<Reply>>();
            _awards = Raw["awards"].ToObject<Dictionary<string, List<Award>>>();
            _inbox = Raw["inbox"].ToObject<List<InboxItem>>();
            foreach (var m in _moments) ApplyReaction(m);
            return Task.CompletedTask;
        }

        void ApplyReaction(Moment m)
        {
            if (SampleState.Current.reactions.TryGetValue(m.Id, out var kind) && m.Reactions.All(r => r.UserId != Me.Id))
                m.Reactions.Add(new ReactionRef { UserId = Me.Id, Kind = kind });
        }

        public Moment FindMoment(string id) => _moments.FirstOrDefault(m => m.Id == id);
        public string Name(string userId) => People.TryGetValue(userId, out var p) ? (string)p["name"] : "Someone";

        public Task<List<Moment>> Moments(string roomId, CancellationToken ct = default) =>
            Task.FromResult(_moments.Where(m => m.RoomId == roomId).ToList());

        public Task<List<Recap>> Recaps(string roomId, CancellationToken ct = default) =>
            Task.FromResult(_recaps.Where(r => r.RoomId == roomId).ToList());

        public Task<List<Recap>> PendingRecaps(string roomId, CancellationToken ct = default) => Task.FromResult(new List<Recap>());

        public Task<Recap> Approve(string recapId, CancellationToken ct = default)
        {
            SampleState.Current.recapApprovedByMe = true;
            SampleState.Save();
            return Task.FromResult(_recaps.FirstOrDefault(r => r.Id == recapId));
        }

        public Task<List<Reply>> Replies(string momentId, CancellationToken ct = default)
        {
            var mine = SampleState.Current.replies.Where(r => r.momentId == momentId).Select(r => new Reply
            {
                Id = r.id, MomentId = r.momentId, AuthorId = Me.Id, AuthorName = Me.DisplayName, Text = r.text, CreatedAt = r.createdAt,
                Label = "Your reply (sample)",
            });
            return Task.FromResult(_seedReplies.Where(r => r.MomentId == momentId).Concat(mine).ToList());
        }

        public Task<Reply> AddReply(string momentId, string text, string clientKey, CancellationToken ct = default)
        {
            var existing = SampleState.Current.replies.FirstOrDefault(r => r.id == clientKey);
            if (existing == null)
            {
                existing = new SampleReply { id = clientKey, momentId = momentId, text = text, createdAt = DateTimeOffset.UtcNow.ToString("o") };
                SampleState.Current.replies.Add(existing);
                SampleState.Save();
            }
            return Task.FromResult(new Reply { Id = existing.id, MomentId = momentId, AuthorId = Me.Id, AuthorName = Me.DisplayName, Text = existing.text, CreatedAt = existing.createdAt, Label = "Your reply (sample)" });
        }

        public Task React(string momentId, string kind, CancellationToken ct = default)
        {
            SampleState.Current.reactions[momentId] = kind;
            SampleState.Save();
            var m = FindMoment(momentId);
            if (m != null) { m.Reactions.RemoveAll(r => r.UserId == Me.Id); ApplyReaction(m); }
            return Task.CompletedTask;
        }

        public Task<List<RoomObject>> Objects(string roomId, CancellationToken ct = default)
        {
            var list = new List<RoomObject>();
            foreach (var kv in SampleState.Current.placements)
            {
                var v = kv.Value;
                list.Add(new RoomObject { Id = kv.Key, ObjectType = kv.Key.Split(':')[0], Transform = new RoomObjectTransform { X = v[0], Y = v[1], Z = v[2], Yaw = v[3] }, Version = 1 });
            }
            return Task.FromResult(list);
        }

        public Task<RoomObject> Place(string roomId, string type, string momentId, string replyId, Vector3 p, float yaw, CancellationToken ct = default)
        {
            var id = $"{type}:{replyId ?? momentId}";
            SampleState.Current.placements[id] = new[] { p.x, p.y, p.z, yaw };
            SampleState.Save();
            return Task.FromResult(new RoomObject { Id = id, ObjectType = type, MomentId = momentId, ReplyId = replyId, Transform = new RoomObjectTransform { X = p.x, Y = p.y, Z = p.z, Yaw = yaw }, Version = 1 });
        }

        public Task<RoomObject> Move(RoomObject o, Vector3 p, float yaw, CancellationToken ct = default) => Place(null, o.ObjectType, o.MomentId, o.ReplyId, p, yaw, ct);

        public Task<List<Award>> Awards(string roomId, CancellationToken ct = default) =>
            Task.FromResult(_awards.TryGetValue(roomId, out var a) ? a : new List<Award>());

        public Task<List<InboxItem>> Inbox(CancellationToken ct = default) => Task.FromResult(_inbox);

        public Task MarkRead(string id, CancellationToken ct = default)
        {
            var item = _inbox.FirstOrDefault(i => i.Id == id);
            if (item != null) item.Read = true;
            return Task.CompletedTask;
        }
    }

    public static class Ids
    {
        /// <summary>Idempotency key for a reply: the same text on the same moment within a session is one reply.</summary>
        public static string ReplyKey(string momentId, string text)
        {
            using var sha = SHA256.Create();
            var h = sha.ComputeHash(Encoding.UTF8.GetBytes(momentId + "\n" + text + "\n" + SystemInfo.deviceUniqueIdentifier));
            return "q-" + BitConverter.ToString(h, 0, 12).Replace("-", "").ToLowerInvariant();
        }
    }
}
