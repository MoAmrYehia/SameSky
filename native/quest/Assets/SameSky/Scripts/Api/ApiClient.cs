// SameSky native client: typed REST client for the SameSky API (api/samesky, contracts/openapi.json).
// Every call carries the signed-in person's bearer token and gets exactly the same authorization checks
// as the companion; there is no native-only or demo endpoint. Secrets (AI, avatar provider) stay server-side.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace SameSky.Api
{
    public sealed class ApiClient
    {
        public string BaseUrl { get; private set; }
        public string Token { get; set; }
        public bool SignedIn => !string.IsNullOrEmpty(Token);

        /// <summary>Raised when the server says the token is no longer valid (expired or revoked).</summary>
        public event Action Unauthorized;
        /// <summary>Raised on every completed call with (ok, isNetworkFailure) so the UI can show connection state.</summary>
        public event Action<bool, bool> CallCompleted;

        public int TimeoutSeconds = 20;

        static readonly JsonSerializerSettings JsonSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };

        public ApiClient(string baseUrl, string token = null)
        {
            SetBaseUrl(baseUrl);
            Token = token;
        }

        public void SetBaseUrl(string baseUrl) => BaseUrl = (baseUrl ?? "").TrimEnd('/');

        /// <summary>Resolves server-relative URLs (signed media/model links) against the API host.</summary>
        public string Absolute(string pathOrUrl)
        {
            if (string.IsNullOrEmpty(pathOrUrl)) return pathOrUrl;
            if (pathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                pathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return pathOrUrl;
            return BaseUrl + (pathOrUrl.StartsWith("/") ? pathOrUrl : "/" + pathOrUrl);
        }

        public string RealtimeUrl()
        {
            var b = BaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? "wss://" + BaseUrl.Substring(8)
                : "ws://" + BaseUrl.Substring(BaseUrl.IndexOf("://", StringComparison.Ordinal) + 3);
            return b + "/v1/realtime";
        }

        // ------------------------------------------------------------------ transport

        public Task<T> Get<T>(string path, CancellationToken ct = default) => Send<T>("GET", path, null, ct);
        public Task<T> Post<T>(string path, object body = null, CancellationToken ct = default) => Send<T>("POST", path, body ?? new JObject(), ct);
        public Task<T> Patch<T>(string path, object body, CancellationToken ct = default) => Send<T>("PATCH", path, body, ct);
        public Task<T> Put<T>(string path, object body, CancellationToken ct = default) => Send<T>("PUT", path, body, ct);
        public Task<T> Delete<T>(string path, CancellationToken ct = default) => Send<T>("DELETE", path, null, ct);

        async Task<T> Send<T>(string method, string path, object body, CancellationToken ct)
        {
            using var req = new UnityWebRequest(Absolute(path), method);
            req.downloadHandler = new DownloadHandlerBuffer();
            if (body != null)
            {
                var json = body is string s ? s : JsonConvert.SerializeObject(body, JsonSettings);
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)) { contentType = "application/json" };
                req.SetRequestHeader("Content-Type", "application/json");
            }
            return await Complete<T>(req, ct);
        }

        /// <summary>Multipart upload (media and avatar files). Fields use the API's snake_case names.</summary>
        public async Task<T> Upload<T>(string path, byte[] data, string fileName, string mime, IDictionary<string, string> fields, CancellationToken ct = default)
        {
            var form = new List<IMultipartFormSection> { new MultipartFormFileSection("file", data, fileName, mime) };
            if (fields != null)
                foreach (var kv in fields)
                    if (kv.Value != null) form.Add(new MultipartFormDataSection(kv.Key, kv.Value));
            using var req = UnityWebRequest.Post(Absolute(path), form);
            req.downloadHandler = new DownloadHandlerBuffer();
            return await Complete<T>(req, ct, timeoutSeconds: 120);
        }

        async Task<T> Complete<T>(UnityWebRequest req, CancellationToken ct, int? timeoutSeconds = null)
        {
            req.timeout = timeoutSeconds ?? TimeoutSeconds;
            req.SetRequestHeader("Accept", "application/json");
            if (SignedIn) req.SetRequestHeader("Authorization", "Bearer " + Token);

            await SendAsync(req, ct);
            ct.ThrowIfCancellationRequested();

            if (req.result == UnityWebRequest.Result.ConnectionError || req.result == UnityWebRequest.Result.DataProcessingError)
            {
                CallCompleted?.Invoke(false, true);
                throw new ApiException(0, "network", "Can't reach SameSky right now. Check the connection, then try again.", retryable: true);
            }

            var text = req.downloadHandler?.text ?? "";
            var status = (int)req.responseCode;
            if (status < 200 || status >= 300)
            {
                CallCompleted?.Invoke(false, false);
                var err = ParseError(status, text);
                if (status == 401) Unauthorized?.Invoke();
                throw err;
            }
            CallCompleted?.Invoke(true, false);
            if (typeof(T) == typeof(JToken) || typeof(T) == typeof(object))
                return (T)(object)(string.IsNullOrWhiteSpace(text) ? JValue.CreateNull() : JToken.Parse(text));
            if (string.IsNullOrWhiteSpace(text)) return default;
            return JsonConvert.DeserializeObject<T>(text, JsonSettings);
        }

        static ApiException ParseError(int status, string text)
        {
            try
            {
                var body = JsonConvert.DeserializeObject<ApiErrorBody>(text);
                if (body?.Error != null)
                    return new ApiException(status, body.Error.Code, body.Error.Message, body.Error.Retryable);
            }
            catch (JsonException) { /* not our error shape */ }
            var message = status switch
            {
                >= 500 => "SameSky had a problem on its side. Try again in a moment.",
                404 => "That isn't available any more.",
                _ => "That didn't work.",
            };
            return new ApiException(status, status >= 500 ? "server_error" : "http_" + status, message, status >= 500);
        }

        public static Task SendAsync(UnityWebRequest req, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<bool>();
            var op = req.SendWebRequest();
            if (op.isDone) tcs.TrySetResult(true);
            else op.completed += _ => tcs.TrySetResult(true);
            if (ct.CanBeCanceled)
                ct.Register(() =>
                {
                    try { req.Abort(); } catch (Exception) { /* already disposed */ }
                    tcs.TrySetResult(true);
                });
            return tcs.Task;
        }

        // ------------------------------------------------------------------ account

        public Task<JToken> Health(CancellationToken ct = default) => Get<JToken>("/v1/health", ct);

        public Task<AuthResult> Signup(string email, string password, string displayName, string timeZone, CancellationToken ct = default) =>
            Post<AuthResult>("/v1/auth/signup", new JObject
            {
                ["email"] = email, ["password"] = password, ["display_name"] = displayName, ["time_zone"] = timeZone,
            }, ct);

        public Task<AuthResult> Login(string email, string password, CancellationToken ct = default) =>
            Post<AuthResult>("/v1/auth/login", new JObject { ["email"] = email, ["password"] = password }, ct);

        public Task<Me> Me(CancellationToken ct = default) => Get<Me>("/v1/me", ct);

        public Task<Me> UpdateMe(string displayName = null, string timeZone = null, CancellationToken ct = default)
        {
            var body = new JObject();
            if (displayName != null) body["display_name"] = displayName;
            if (timeZone != null) body["time_zone"] = timeZone;
            return Patch<Me>("/v1/me", body, ct);
        }

        // ------------------------------------------------------------------ rooms and invitations

        public Task<List<Room>> Rooms(CancellationToken ct = default) => Get<List<Room>>("/v1/rooms", ct);
        public Task<Room> Room(string roomId, CancellationToken ct = default) => Get<Room>($"/v1/rooms/{roomId}", ct);

        public Task<Room> CreateRoom(string kind, string name, string reportingZone, CancellationToken ct = default) =>
            Post<Room>("/v1/rooms", new JObject { ["kind"] = kind, ["name"] = name, ["reporting_zone"] = reportingZone }, ct);

        public Task<Room> UpdateRoom(string roomId, bool? muted = null, string dailyDraftTime = null, bool clearDraftTime = false, CancellationToken ct = default)
        {
            var body = new JObject();
            if (muted.HasValue) body["muted"] = muted.Value;
            if (dailyDraftTime != null) body["daily_draft_time"] = dailyDraftTime;
            else if (clearDraftTime) body["daily_draft_time"] = "";
            return Patch<Room>($"/v1/rooms/{roomId}", body, ct);
        }

        public Task<InviteResult> CreateInvite(string roomId, CancellationToken ct = default) => Post<InviteResult>($"/v1/rooms/{roomId}/invites", null, ct);
        public Task<JToken> RevokeInvite(string roomId, string inviteId, CancellationToken ct = default) => Delete<JToken>($"/v1/rooms/{roomId}/invites/{inviteId}", ct);
        public Task<Room> RedeemCode(string code, CancellationToken ct = default) => Post<Room>("/v1/invites/redeem-code", new JObject { ["code"] = code }, ct);
        public Task<Room> RedeemToken(string token, CancellationToken ct = default) => Post<Room>("/v1/invites/redeem", new JObject { ["token"] = token }, ct);
        public Task<JToken> LeaveRoom(string roomId, CancellationToken ct = default) => Delete<JToken>($"/v1/rooms/{roomId}/members/me", ct);
        public Task<List<Award>> Awards(string roomId, CancellationToken ct = default) => Get<List<Award>>($"/v1/rooms/{roomId}/awards", ct);

        // ------------------------------------------------------------------ moments, sharing, replies

        public Task<List<Moment>> Moments(string roomId, string roomDate = null, CancellationToken ct = default) =>
            Get<List<Moment>>($"/v1/rooms/{roomId}/moments" + (roomDate != null ? "?room_date=" + UnityWebRequest.EscapeURL(roomDate) : ""), ct);

        public Task<Moment> Moment(string momentId, CancellationToken ct = default) => Get<Moment>($"/v1/moments/{momentId}", ct);

        public Task<Moment> CreateMoment(string roomId, string kind, string caption, string mediaAssetId, DateTimeOffset captureAt,
            string captureZone, bool aiAllowed, string transcript = null, bool transcriptConsent = false, CancellationToken ct = default)
        {
            var body = new JObject
            {
                ["kind"] = kind, ["caption"] = caption ?? "", ["capture_at"] = captureAt.ToString("o"),
                ["capture_zone"] = captureZone, ["ai_allowed"] = aiAllowed,
            };
            if (mediaAssetId != null) body["media_asset_id"] = mediaAssetId;
            if (transcript != null) { body["transcript"] = transcript; body["transcript_consent"] = transcriptConsent; }
            return Post<Moment>($"/v1/rooms/{roomId}/moments", body, ct);
        }

        public Task<AudienceResolution> ResolveAudience(string momentId, IEnumerable<string> recipientIds, CancellationToken ct = default) =>
            Post<AudienceResolution>($"/v1/moments/{momentId}/audience/resolve", new JObject { ["recipient_ids"] = new JArray(recipientIds) }, ct);

        public Task<Moment> Share(string momentId, IEnumerable<string> recipientIds, string confirmationToken, CancellationToken ct = default) =>
            Post<Moment>($"/v1/moments/{momentId}/share", new JObject
            {
                ["recipient_ids"] = new JArray(recipientIds), ["confirmation_token"] = confirmationToken,
            }, ct);

        public Task<Moment> Withdraw(string momentId, CancellationToken ct = default) =>
            Post<Moment>($"/v1/moments/{momentId}/withdraw", new JObject { ["all_copies"] = false }, ct);

        public Task<List<Reply>> Replies(string momentId, CancellationToken ct = default) => Get<List<Reply>>($"/v1/moments/{momentId}/replies", ct);

        /// <param name="clientKey">Idempotency key: a retried reply with the same key is stored once.</param>
        public Task<Reply> AddReply(string momentId, string text, string clientKey, CancellationToken ct = default) =>
            Post<Reply>($"/v1/moments/{momentId}/replies", new JObject { ["text"] = text, ["client_key"] = clientKey }, ct);

        public Task<JToken> React(string momentId, string kind, CancellationToken ct = default) =>
            Post<JToken>($"/v1/moments/{momentId}/reactions", new JObject { ["kind"] = kind }, ct);

        public Task<MediaInfo> UploadMedia(byte[] data, string fileName, string mime, string kind, string sourceDevice, DateTimeOffset? captureAt, CancellationToken ct = default) =>
            Upload<MediaInfo>("/v1/media", data, fileName, mime, new Dictionary<string, string>
            {
                ["kind"] = kind, ["source_device"] = sourceDevice, ["capture_at"] = captureAt?.ToString("o"),
            }, ct);

        public Task<MediaInfo> Media(string assetId, CancellationToken ct = default) => Get<MediaInfo>($"/v1/media/{assetId}", ct);

        // ------------------------------------------------------------------ room objects (placements)

        public Task<List<RoomObject>> Objects(string roomId, CancellationToken ct = default) => Get<List<RoomObject>>($"/v1/rooms/{roomId}/objects", ct);

        public Task<RoomObject> CreateObject(string roomId, string objectType, string momentId, string replyId, Vector3 p, float yaw, CancellationToken ct = default) =>
            Post<RoomObject>($"/v1/rooms/{roomId}/objects", new JObject
            {
                ["object_type"] = objectType, ["moment_id"] = momentId, ["reply_id"] = replyId,
                ["x"] = p.x, ["y"] = p.y, ["z"] = p.z, ["yaw"] = yaw,
            }, ct);

        public Task<RoomObject> MoveObject(string objectId, int expectedVersion, Vector3 p, float yaw, CancellationToken ct = default) =>
            Patch<RoomObject>($"/v1/objects/{objectId}", new JObject
            {
                ["expected_version"] = expectedVersion, ["x"] = p.x, ["y"] = p.y, ["z"] = p.z, ["yaw"] = yaw,
            }, ct);

        // ------------------------------------------------------------------ daily story (recaps)

        public Task<List<Recap>> Recaps(string roomId, CancellationToken ct = default) => Get<List<Recap>>($"/v1/rooms/{roomId}/recaps", ct);
        public Task<List<Recap>> PendingRecaps(string roomId, CancellationToken ct = default) => Get<List<Recap>>($"/v1/rooms/{roomId}/recaps/pending", ct);
        public Task<Recap> Recap(string recapId, CancellationToken ct = default) => Get<Recap>($"/v1/recaps/{recapId}", ct);

        public Task<Recap> RequestRecap(string roomId, string roomDate, CancellationToken ct = default) =>
            Post<Recap>($"/v1/rooms/{roomId}/recaps", new JObject { ["room_date"] = roomDate }, ct);

        public Task<Recap> ApproveRecap(string recapId, CancellationToken ct = default) => Post<Recap>($"/v1/recaps/{recapId}/approve", null, ct);

        // ------------------------------------------------------------------ time together

        public Task<JObject> Availability(string roomId, CancellationToken ct = default) => Get<JObject>($"/v1/rooms/{roomId}/availability", ct);

        public Task<JObject> SetRules(string roomId, IEnumerable<WeeklyRule> rules, CancellationToken ct = default)
        {
            var arr = new JArray();
            foreach (var r in rules) arr.Add(new JObject { ["weekday"] = r.Weekday, ["start"] = r.Start, ["end"] = r.End });
            return Put<JObject>($"/v1/rooms/{roomId}/availability/rules", new JObject { ["rules"] = arr }, ct);
        }

        public Task<JObject> FreeNow(string roomId, CancellationToken ct = default) => Post<JObject>($"/v1/rooms/{roomId}/free-now", null, ct);
        public Task<JObject> ClearFreeNow(string roomId, CancellationToken ct = default) => Delete<JObject>($"/v1/rooms/{roomId}/free-now", ct);

        public Task<OpportunityResult> Opportunities(string roomId, IEnumerable<string> participantIds, CancellationToken ct = default) =>
            Post<OpportunityResult>($"/v1/rooms/{roomId}/opportunities", new JObject { ["participant_ids"] = new JArray(participantIds), ["days"] = 7 }, ct);

        public Task<List<InboxItem>> Notifications(CancellationToken ct = default) => Get<List<InboxItem>>("/v1/notifications", ct);
        public Task<JToken> MarkRead(string notificationId, CancellationToken ct = default) => Post<JToken>($"/v1/notifications/{notificationId}/read", null, ct);

        // ------------------------------------------------------------------ live viewing sessions

        public Task<List<LiveSessionInfo>> Sessions(string roomId, CancellationToken ct = default) => Get<List<LiveSessionInfo>>($"/v1/rooms/{roomId}/sessions", ct);

        public Task<LiveSessionInfo> CreateSession(string roomId, string momentId, string otherUserId, CancellationToken ct = default) =>
            Post<LiveSessionInfo>($"/v1/rooms/{roomId}/sessions", new JObject
            {
                ["moment_id"] = momentId, ["participant_ids"] = new JArray(otherUserId),
            }, ct);

        public Task<JToken> EndSession(string sessionId, CancellationToken ct = default) => Post<JToken>($"/v1/sessions/{sessionId}/end", null, ct);

        // ------------------------------------------------------------------ avatars

        public Task<UserPublic> UserAvatar(string userId, CancellationToken ct = default) => Get<UserPublic>($"/v1/users/{userId}/avatar", ct);
        public Task<AvatarStudio> AvatarStudio(CancellationToken ct = default) => Get<AvatarStudio>("/v1/me/avatar-studio", ct);

        public Task<AvatarAssetInfo> ApproveAvatar(string assetId, string visibility, CancellationToken ct = default) =>
            Post<AvatarAssetInfo>($"/v1/me/avatar-assets/{assetId}/approve", new JObject { ["visibility"] = visibility, ["confirmed"] = true }, ct);

        public Task<AvatarAssetInfo> RejectAvatar(string assetId, CancellationToken ct = default) => Post<AvatarAssetInfo>($"/v1/me/avatar-assets/{assetId}/reject", null, ct);
        public Task<AvatarAssetInfo> SetAvatarVisibility(string assetId, string visibility, CancellationToken ct = default) =>
            Patch<AvatarAssetInfo>($"/v1/me/avatar-assets/{assetId}", new JObject { ["visibility"] = visibility }, ct);
        public Task<AvatarAssetInfo> DeleteAvatar(string assetId, CancellationToken ct = default) => Delete<AvatarAssetInfo>($"/v1/me/avatar-assets/{assetId}", ct);
        public Task<JToken> UseDemoAvatar(CancellationToken ct = default) => Post<JToken>("/v1/me/avatar/demo", null, ct);

        /// <summary>Downloads raw bytes from a (signed) server URL, e.g. media or an approved avatar model.</summary>
        public async Task<byte[]> Download(string url, CancellationToken ct = default, int timeoutSeconds = 60)
        {
            using var req = UnityWebRequest.Get(Absolute(url));
            req.timeout = timeoutSeconds;
            await SendAsync(req, ct);
            ct.ThrowIfCancellationRequested();
            if (req.result == UnityWebRequest.Result.ConnectionError)
                throw new ApiException(0, "network", "Can't reach SameSky right now.", retryable: true);
            if (req.responseCode < 200 || req.responseCode >= 300)
                throw ParseError((int)req.responseCode, req.downloadHandler?.text ?? "");
            return req.downloadHandler.data;
        }
    }
}
