// Live viewing session over the API's WebSocket (/v1/realtime; api/samesky/realtime.py).
//   client -> {type:auth, token} then {sessionId}
//   server -> welcome {state, participants[avatar is viewer-bound]}, peer_joined, peer_left, pose, state,
//             reaction, avatar_changed, removed, session_ended, pong, error
//   client -> pose (<= server limit, sanitized server-side), playback {action, positionMs, baseVersion},
//             reaction {kind}, ping {t}, leave
// Poses are ephemeral (never stored). Playback is server-authoritative: the server stamps every change and
// both clients play from the server's state, corrected by a measured clock offset. On a drop the client
// reconnects with backoff and resyncs from the fresh welcome (late join and reconnect use the same path).

using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SameSky.Api
{
    public sealed class RealtimeClient : IDisposable
    {
        public enum Link { Idle, Connecting, Live, Reconnecting, Closed }

        public Link State { get; private set; } = Link.Idle;
        public string SessionId { get; }
        public PlaybackState Playback { get; private set; }
        /// <summary>serverTime - localTime in ms, from the latest ping round trip (or message stamps).</summary>
        public double ClockOffsetMs { get; private set; }
        public double LastRoundTripMs { get; private set; } = -1;
        public int Reconnects { get; private set; }
        public string CloseReason { get; private set; }

        public event Action<JObject> Welcome;
        public event Action<string, string, JObject> PeerJoined; // userId, displayName, public avatar (refresh hint)
        public event Action<string, string> PeerLeft;            // userId, reason
        public event Action<string, JObject, double> Pose;        // userId, pose, serverTime
        public event Action<PlaybackState> PlaybackChanged;
        public event Action<string, string> Reaction;            // userId, kind
        public event Action<string> AvatarChanged;               // userId
        public event Action<string> Ended;                       // reason
        public event Action<Link> LinkChanged;

        readonly string _url;
        readonly Func<string> _token;
        readonly ConcurrentQueue<JObject> _inbox = new();
        readonly CancellationTokenSource _life = new();
        ClientWebSocket _ws;
        readonly SemaphoreSlim _sendLock = new(1, 1);
        float _nextPing;

        public RealtimeClient(string realtimeUrl, string sessionId, Func<string> token)
        {
            _url = realtimeUrl;
            SessionId = sessionId;
            _token = token;
        }

        public void Start() => _ = RunAsync(_life.Token);

        async Task RunAsync(CancellationToken ct)
        {
            var backoff = 1.0;
            while (!ct.IsCancellationRequested && State != Link.Closed)
            {
                SetState(Reconnects == 0 ? Link.Connecting : Link.Reconnecting);
                try
                {
                    using var ws = new ClientWebSocket();
                    ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                    _ws = ws;
                    using (var connectCt = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        connectCt.CancelAfter(TimeSpan.FromSeconds(10));
                        await ws.ConnectAsync(new Uri(_url), connectCt.Token);
                    }
                    await SendRaw(new JObject { ["type"] = "auth", ["token"] = _token() }, ct);
                    await SendRaw(new JObject { ["sessionId"] = SessionId }, ct);
                    backoff = 1.0;
                    await ReceiveLoop(ws, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (Exception e)
                {
                    Debug.Log($"[SameSky] realtime link dropped: {e.GetType().Name}");
                }
                finally { _ws = null; }

                if (State == Link.Closed || ct.IsCancellationRequested) break;
                Reconnects++;
                SetState(Link.Reconnecting);
                try { await Task.Delay(TimeSpan.FromSeconds(backoff), ct); } catch (OperationCanceledException) { break; }
                backoff = Math.Min(backoff * 2, 15);
            }
        }

        async Task ReceiveLoop(ClientWebSocket ws, CancellationToken ct)
        {
            var buffer = new byte[64 * 1024];
            var sb = new StringBuilder();
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    // 4401 bad token, 4403 no longer authorized, 4404 session gone, 4409 replaced by a newer connection
                    var code = (int?)result.CloseStatus ?? 1000;
                    if (code is 4401 or 4403 or 4404 or 4409)
                    {
                        CloseReason = code switch { 4401 => "signed_out", 4403 => "not_authorized", 4404 => "not_found", _ => "replaced" };
                        _inbox.Enqueue(new JObject { ["type"] = "session_ended", ["reason"] = CloseReason });
                        State = Link.Closed;
                    }
                    return;
                }
                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (!result.EndOfMessage) continue;
                try { _inbox.Enqueue(JObject.Parse(sb.ToString())); }
                catch (JsonException) { /* ignore malformed frames */ }
                sb.Clear();
            }
        }

        /// <summary>Dispatches received messages on the main thread. Call once per frame.</summary>
        public void Pump()
        {
            while (_inbox.TryDequeue(out var m)) Dispatch(m);
            if (State == Link.Live && Time.unscaledTime >= _nextPing)
            {
                _nextPing = Time.unscaledTime + 5f;
                _ = Send(new JObject { ["type"] = "ping", ["t"] = NowMs() });
            }
        }

        void Dispatch(JObject m)
        {
            var type = (string)m["type"];
            switch (type)
            {
                case "welcome":
                    SetState(Link.Live);
                    ApplyState(m["state"] as JObject);
                    Welcome?.Invoke(m);
                    break;
                case "state":
                    ApplyState(m);
                    break;
                case "pong":
                    var sent = (double?)m["t"] ?? 0;
                    var now = NowMs();
                    LastRoundTripMs = now - sent;
                    ClockOffsetMs = ((double?)m["serverTime"] ?? now) - (sent + LastRoundTripMs / 2);
                    break;
                case "peer_joined":
                    PeerJoined?.Invoke((string)m["userId"], (string)m["displayName"], m["avatar"] as JObject);
                    break;
                case "peer_left":
                    PeerLeft?.Invoke((string)m["userId"], (string)m["reason"]);
                    break;
                case "pose":
                    Pose?.Invoke((string)m["userId"], m["pose"] as JObject, (double?)m["serverTime"] ?? 0);
                    break;
                case "reaction":
                    Reaction?.Invoke((string)m["userId"], (string)m["kind"]);
                    break;
                case "avatar_changed":
                    AvatarChanged?.Invoke((string)m["userId"]);
                    break;
                case "removed":
                case "session_ended":
                case "error":
                    State = Link.Closed;
                    CloseReason = (string)m["reason"] ?? (string)m["code"] ?? type;
                    LinkChanged?.Invoke(State);
                    Ended?.Invoke(CloseReason);
                    break;
            }
        }

        void ApplyState(JObject s)
        {
            if (s == null) return;
            var next = s.ToObject<PlaybackState>();
            if (Playback != null && next.Version < Playback.Version) return; // stale
            if (next.ServerTime > 0 && LastRoundTripMs < 0) ClockOffsetMs = next.ServerTime - NowMs();
            Playback = next;
            PlaybackChanged?.Invoke(next);
        }

        /// <summary>Where shared playback should be right now, in ms, from the server's last authoritative state.</summary>
        public double PlayheadNowMs()
        {
            if (Playback == null) return 0;
            if (Playback.Paused) return Playback.PlayheadMs;
            var serverNow = NowMs() + ClockOffsetMs;
            return Playback.PlayheadMs + Math.Max(0, serverNow - Playback.StateAt);
        }

        public Task SendPlayback(string action, double? positionMs = null)
        {
            var m = new JObject { ["type"] = "playback", ["action"] = action, ["baseVersion"] = Playback?.Version ?? 0 };
            if (positionMs.HasValue) m["positionMs"] = Math.Max(0, Math.Round(positionMs.Value));
            return Send(m);
        }

        public Task SendReaction(string kind) => Send(new JObject { ["type"] = "reaction", ["kind"] = kind });

        /// <summary>Head and hands relative to the shared table origin; source "xr" when tracked, else "animation".</summary>
        public Task SendPose(JObject pose) => Send(new JObject { ["type"] = "pose", ["pose"] = pose });

        public async Task Leave()
        {
            if (State == Link.Closed) return;
            try { await Send(new JObject { ["type"] = "leave" }); } catch (Exception) { /* closing anyway */ }
            State = Link.Closed;
            _life.Cancel();
        }

        async Task Send(JObject message)
        {
            if (State != Link.Live) return;
            try { await SendRaw(message, _life.Token); }
            catch (Exception) { /* the receive loop notices the drop and reconnects */ }
        }

        async Task SendRaw(JObject message, CancellationToken ct)
        {
            var ws = _ws;
            if (ws == null || ws.State != WebSocketState.Open) return;
            var bytes = Encoding.UTF8.GetBytes(message.ToString(Formatting.None));
            await _sendLock.WaitAsync(ct);
            try { await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct); }
            finally { _sendLock.Release(); }
        }

        void SetState(Link s)
        {
            if (State == Link.Closed || State == s) return;
            State = s;
            _inbox.Enqueue(new JObject { ["type"] = "_link" }); // wake the main thread
            LinkChanged?.Invoke(s);
        }

        static double NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public void Dispose()
        {
            State = Link.Closed;
            _life.Cancel();
            _life.Dispose();
        }
    }
}
