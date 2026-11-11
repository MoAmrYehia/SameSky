// Together: who's in the room and what time it is for them, the overlaps in your usual times, "Free now"
// (expires on its own), and live sessions: sit together, see each other's head and hands, and watch one
// shared moment in sync. The server owns playback state; both clients follow it and correct drift.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SameSky.Api;
using SameSky.Interaction;
using SameSky.UI;
using UnityEngine;

namespace SameSky.App
{
    public sealed partial class SameSkyApp
    {
        RealtimeClient _live;
        LiveSessionInfo _liveInfo;
        Moment _liveMoment;
        float _nextPose, _nextDriftCheck;
        public double LastDriftMs { get; private set; }

        public string LiveState => _live == null ? "none" : _live.State.ToString();

        public void ShowTogether()
        {
            var l = NewPanel("together", 0.5f);
            l.Title("Together");
            var others = CurrentRoom.Members.Where(m => m.Id != Data.Me.Id).ToList();
            if (others.Count == 0)
            {
                l.Body("You're the only one here so far.");
                if (!Data.IsSample) InviteBlock(l, CurrentRoom);
                l.Row(("together:back", "Back", EnterRoom, PressStyle.Quiet));
                FinishPanel(l);
                return;
            }
            l.Body($"Your time: {DateTime.Now:HH:mm} ({Data.Me.TimeZone.Replace('_', ' ')})");
            foreach (var o in others) l.Body($"{o.DisplayName}: {LocalTimeFor(o.TimeZone)} ({o.TimeZone.Replace('_', ' ')})");
            if (Data.IsSample)
            {
                l.Tag("Sample: Maya's presence here is simulated");
                l.Muted("In your own room this shows when your usual times overlap, lets you say you're free now, and opens a live session when you're both here.");
                l.Row(("together:back", "Back", EnterRoom, PressStyle.Quiet));
                FinishPanel(l);
                return;
            }
            var overlapLine = l.Muted("Finding times that overlap…");
            l.Row(("together:free", "I'm free now (1 hour)", () => Run(async ct =>
                  {
                      var res = await Api.FreeNow(CurrentRoom.Id, ct);
                      Toast($"Shown as free until {Local((string)res["freeNowUntil"])}.");
                  }, "Couldn't update"), PressStyle.Secondary),
                  ("together:busy", "Not free", () => Run(async ct => { await Api.ClearFreeNow(CurrentRoom.Id, ct); Toast("Cleared."); }, "Couldn't update"), PressStyle.Quiet));
            var live = l.Body("Checking for a live session…");
            var buttons = l.Row(("together:join", "Join", null, PressStyle.Primary), ("together:start", "Start watching together", null, PressStyle.Secondary));
            buttons[0].Interactable = false;
            buttons[1].Interactable = false;
            if (_live != null)
            {
                l.Button("together:leave", "Leave the live session", () => _ = LeaveLiveAndRefresh(), PressStyle.Danger);
                l.Muted($"Link: {_live.State} · round trip {(_live.LastRoundTripMs < 0 ? "—" : $"{_live.LastRoundTripMs:F0} ms")} · reconnects {_live.Reconnects}");
            }
            l.Row(("together:times", "My usual times", () => TimesPanel("availability", ShowTogether, null), PressStyle.Quiet), ("together:back", "Back", EnterRoom, PressStyle.Quiet));
            FinishPanel(l);

            Run(async ct =>
            {
                var opp = await Api.Opportunities(CurrentRoom.Id, others.Select(o => o.Id), ct);
                var first = opp.Candidates.FirstOrDefault();
                overlapLine.text = opp.StatusMessage ?? (first != null
                    ? $"Next overlap: {string.Join(" · ", first.Local.Select(x => $"{x.DisplayName} {x.Weekday} {x.Time}–{x.EndTime}"))}"
                    : "No overlap in your usual times this week. Add times in My usual times.");
                var sessions = await Api.Sessions(CurrentRoom.Id, ct);
                var existing = sessions.FirstOrDefault();
                if (existing != null && _live == null)
                {
                    live.text = $"{NameOf(existing.HostId)} started a live session.";
                    buttons[0].Interactable = true;
                    buttons[0].Pressed += () => Run(c => JoinLive(existing, c), "Couldn't join the live session");
                }
                else if (_live == null)
                {
                    live.text = "No live session right now.";
                    var moments = await Data.Moments(CurrentRoom.Id, ct);
                    var watchable = moments.Where(m => m.SharingState == "shared" && m.Media?.ProcessingState == "ready" && (m.Media.Kind == "video" || m.Media.Kind == "audio"))
                        .OrderByDescending(m => m.CaptureAt).FirstOrDefault();
                    if (watchable != null)
                    {
                        buttons[1].Interactable = true;
                        buttons[1].Pressed += () => Run(async c =>
                        {
                            var other = others.First(o => watchable.Audience.Any(a => a.UserId == o.Id) || watchable.OwnerId == o.Id);
                            var created = await Api.CreateSession(CurrentRoom.Id, watchable.Id, other.Id, c);
                            created.RoomId = CurrentRoom.Id;
                            created.MomentId = watchable.Id;
                            await JoinLive(created, c);
                        }, "Couldn't start the live session");
                    }
                    else live.text = "No live session right now. Share a clip or voice note to watch it together.";
                }
                else live.text = $"You're in a live session ({_live.State}).";
            }, "Couldn't load Together", ShowTogether);
        }

        async Task JoinLive(LiveSessionInfo info, CancellationToken ct)
        {
            await LeaveLive();
            _liveInfo = info;
            _liveMoment = await Api.Moment(info.MomentId, ct);
            _live = new RealtimeClient(Api.RealtimeUrl(), info.Id, () => Api.Token);
            _live.Welcome += OnLiveWelcome;
            _live.PeerJoined += (uid, name, avatar) => { SetPresenceLabel("live"); Toast($"{name} joined."); if (avatar?["refresh"]?.Value<bool>() == true) _ = RefreshPartnerAvatar(uid); };
            _live.PeerLeft += (uid, reason) => { SetPresenceLabel("away"); Toast("They left the live session."); };
            _live.Pose += (uid, pose, _) => Avatars.ApplyLivePose(_partner, pose);
            _live.PlaybackChanged += _ => ApplySharedPlayback(force: true);
            _live.Reaction += (uid, kind) => { if (uid != Data.Me.Id) Toast($"{NameOf(uid)} sent {kind}."); };
            _live.AvatarChanged += uid => _ = RefreshPartnerAvatar(uid);
            _live.Ended += reason => { Toast(reason == "not_authorized" ? "You no longer have access to that session." : "The live session ended."); _ = LeaveLiveAndRefresh(); };
            _live.LinkChanged += state => TestProbe.Event("live", state.ToString());
            _live.Start();
            TestProbe.Event("live", "joining");
            EnterRoom();
        }

        void OnLiveWelcome(JObject welcome)
        {
            var others = (welcome["participants"] as JArray)?.OfType<JObject>().Where(p => (string)p["userId"] != Data.Me.Id).ToList();
            var other = others?.FirstOrDefault();
            if (other != null && (bool?)other["online"] == true) SetPresenceLabel("live");
            EnsureScreen();
            _ = PrepareLiveMedia();
        }

        async Task PrepareLiveMedia()
        {
            if (_liveMoment?.Media == null) return;
            if (_liveMoment.Media.Kind == "video")
            {
                await Data.Media.Prepare(_video, _liveMoment.Media);
                _videoTexture ??= new RenderTexture(1280, 720, 0) { name = "Live video" };
                _video.targetTexture = _videoTexture;
                Ui.SetImage(_screenImage, _videoTexture);
                _video.Prepare();
            }
            else _storyAudio.clip = await Data.Media.Audio(_liveMoment.Media);
            _screenMeta.text = $"Watching together · {_liveMoment.OwnerName} · {SourceLabel(_liveMoment.Media)}";
            ShowLiveControls();
            ApplySharedPlayback(force: true);
        }

        void ShowLiveControls()
        {
            var l = NewPanel("live");
            l.Title("Watching together");
            l.Muted("Play, pause and seek are shared: both of you see the same moment.");
            l.Row(("live:back10", "−10 s", () => _ = _live.SendPlayback("seek", Math.Max(0, _live.PlayheadNowMs() - 10000)), PressStyle.Secondary),
                  ("live:play", _live?.Playback?.Paused == false ? "Pause" : "Play", () => _ = _live.SendPlayback(_live.Playback?.Paused == false ? "pause" : "play", _live.PlayheadNowMs()), PressStyle.Primary),
                  ("live:fwd10", "+10 s", () => _ = _live.SendPlayback("seek", _live.PlayheadNowMs() + 10000), PressStyle.Secondary));
            l.Row(("live:heart", "Love", () => _ = _live.SendReaction("heart"), PressStyle.Chip), ("live:hug", "Hug", () => _ = _live.SendReaction("hug"), PressStyle.Chip),
                  ("live:leave", "Leave", () => _ = LeaveLiveAndRefresh(), PressStyle.Danger));
            FinishPanel(l);
        }

        /// <summary>Follows the server's playback state: pause/play to match, seek when drift exceeds 0.25 s.</summary>
        void ApplySharedPlayback(bool force)
        {
            if (_live?.Playback == null || _liveMoment?.Media == null || _screenRoot == null) return;
            var target = _live.PlayheadNowMs() / 1000.0;
            if (_liveMoment.Media.Kind == "video" && _video != null)
            {
                if (_live.Playback.Paused) { if (_video.isPlaying) _video.Pause(); }
                else if (!_video.isPlaying && _video.isPrepared) _video.Play();
                var drift = (_video.time - target) * 1000.0;
                LastDriftMs = drift;
                if (force || Math.Abs(drift) > 250) _video.time = target;
            }
            else if (_storyAudio != null && _storyAudio.clip != null)
            {
                var clamped = Mathf.Clamp((float)target, 0, _storyAudio.clip.length - 0.05f);
                if (_live.Playback.Paused) { if (_storyAudio.isPlaying) _storyAudio.Pause(); }
                else if (!_storyAudio.isPlaying) { _storyAudio.time = clamped; _storyAudio.Play(); }
                var drift = (_storyAudio.time - target) * 1000.0;
                LastDriftMs = drift;
                if (force || Math.Abs(drift) > 250) _storyAudio.time = clamped;
            }
        }

        void UpdateTogether()
        {
            if (_live == null) return;
            _live.Pump();
            if (Time.unscaledTime >= _nextDriftCheck)
            {
                _nextDriftCheck = Time.unscaledTime + 1f;
                ApplySharedPlayback(force: false);
            }
            if (_live.State == RealtimeClient.Link.Live && Time.unscaledTime >= _nextPose)
            {
                _nextPose = Time.unscaledTime + 1f / 12f;
                _ = _live.SendPose(MyPose());
            }
        }

        /// <summary>My head and hands in seat space (origin at my seat, +Z toward the table centre).</summary>
        JObject MyPose()
        {
            var seat = Anchor("Seat");
            var rig = FindAnyObjectByType<OVRCameraRig>();
            JArray P(Vector3 world) { var p = seat != null ? seat.InverseTransformPoint(world) : world; return new JArray(Math.Round(p.x, 4), Math.Round(p.y, 4), Math.Round(p.z, 4)); }
            JArray Q(Quaternion world) { var q = seat != null ? Quaternion.Inverse(seat.rotation) * world : world; return new JArray(q.x, q.y, q.z, q.w); }
            var pose = new JObject { ["source"] = "xr", ["tracked"] = true, ["head"] = new JObject { ["p"] = P(HeadPosition), ["q"] = Q(_head.rotation) } };
            if (rig != null)
            {
                pose["left"] = new JObject { ["p"] = P(rig.leftHandAnchor.position), ["q"] = Q(rig.leftHandAnchor.rotation) };
                pose["right"] = new JObject { ["p"] = P(rig.rightHandAnchor.position), ["q"] = Q(rig.rightHandAnchor.rotation) };
            }
            return pose;
        }

        async Task RefreshPartnerAvatar(string userId)
        {
            try
            {
                var user = await Api.UserAvatar(userId);
                if (_partner != null && user?.Avatar != null)
                {
                    if (user.Avatar.IsPhotoDerived) await Avatars.SwapToApproved(_partner, user.Avatar);
                    else { SetPartner(null, "away"); SetPartner(CurrentRoom.Members.FirstOrDefault(m => m.Id == userId), _live != null ? "live" : "away"); }
                }
            }
            catch (Exception e) { Debug.Log($"[SameSky] avatar refresh: {e.Message}"); }
        }

        async Task LeaveLive()
        {
            if (_live == null) return;
            var l = _live;
            _live = null;
            await l.Leave();
            l.Dispose();
            TestProbe.Event("live", "left");
            if (_video != null) _video.Stop();
            if (_storyAudio != null) _storyAudio.Stop();
        }

        async Task LeaveLiveAndRefresh()
        {
            await LeaveLive();
            SetPresenceLabel("away");
            EnterRoom();
        }
    }
}
