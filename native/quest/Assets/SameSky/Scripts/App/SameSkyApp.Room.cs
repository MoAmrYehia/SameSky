// The room: the cup with a waiting voice note, replies left beside it, photo frames, the partner's seat.
// The defining moment: pick up the cup, hear the waiting message (captions with who and when), put it down,
// leave a reply that stays on the table.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SameSky.Api;
using SameSky.Interaction;
using SameSky.UI;
using TMPro;
using UnityEngine;

namespace SameSky.App
{
    public sealed partial class SameSkyApp
    {
        Transform _roomContent;       // everything per-room, rebuilt on EnterRoom
        GrabbableObject _cup;
        Vector3 _cupRestPos;
        Quaternion _cupRestRot;
        AudioSource _voice;
        Moment _waiting;              // the voice note waiting in the cup
        Transform _cupLabel, _captionPlate;
        TextMeshPro _cupLabelText, _captionText, _captionMeta;
        Pressable _cupPlay, _cupReply;
        bool _heardThisVisit;
        bool _cupVoicePlaying;
        float _cupReturnAt = -1f;
        Transform _partner;
        TextMeshPro _partnerLabel;
        string _presence = "away";     // live | simulated | recorded | away
        readonly List<Transform> _notes = new();
        List<Moment> _moments = new();

        public string Presence => _presence;
        public bool CupHeld => _cup != null && _cup.IsHeld;
        public bool VoicePlaying => _voice != null && _voice.isPlaying;

        // ------------------------------------------------------------ entering the room

        public void EnterRoom()
        {
            if (CurrentRoom == null) { ShowSetup(); return; }
            HidePanel();
            Screen = "room";
            ScreenChanged?.Invoke(Screen);
            TestProbe.Event("screen", "room");
            SetDock(
                ("day", "Our day", ShowOurDay),
                ("share", "Share", ShowShare),
                ("together", "Together", ShowTogether),
                ("keepsakes", "Keepsakes", ShowKeepsakes),
                ("settings", "Settings", ShowSettings),
                ("pause", "Pause", ShowPause),
                ("recenter", "Recenter", () => Recenter()));
            Prefs.Current.lastRoomId = CurrentRoom.Id;
            Prefs.Save();
            Run(PopulateRoom, "Couldn't load your room", EnterRoom);
        }

        void ClearRoom()
        {
            StopVoice();
            if (_roomContent != null) Destroy(_roomContent.gameObject);
            _roomContent = null;
            _cup = null;
            _notes.Clear();
            SetPartner(null, "away");
        }

        async Task PopulateRoom(System.Threading.CancellationToken ct)
        {
            ClearRoom();
            _roomContent = new GameObject("Room content").transform;
            _roomContent.SetParent(_world, false);
            _moments = await Data.Moments(CurrentRoom.Id, ct);
            ct.ThrowIfCancellationRequested();
            var others = CurrentRoom.Members.Where(m => m.Id != Data.Me.Id).ToList();

            // the waiting message: the newest voice note from someone else that's shared with me
            _waiting = _moments.Where(m => m.Kind == "voice" && !m.IsMine && m.SharingState == "shared" && m.Media?.ProcessingState == "ready")
                .OrderByDescending(m => m.CaptureAt).FirstOrDefault();
            BuildCup();

            // photo frames on the table: the newest shared photos and clip posters
            var visuals = _moments.Where(m => m.Media != null && (m.Media.Kind == "image" || m.Media.Kind == "video") && m.SharingState != "withdrawn")
                .OrderByDescending(m => m.CaptureAt).Take(2).ToList();
            for (var i = 0; i < visuals.Count; i++) _ = BuildFrame(visuals[i], Anchor(i == 0 ? "FrameLeft" : "FrameRight"), ct);

            // replies already left on the table
            await RestoreNotes(ct);

            // earned keepsakes stand on the shelf
            BuildKeepsakes(await Data.Awards(CurrentRoom.Id, ct));

            // who's across the table
            _partnerLabel = null; // rebuilt with the room content
            if (Data.IsSample) SetPartner(others.FirstOrDefault(), "simulated");
            else SetPartner(others.FirstOrDefault(), "away");

            if (others.Count == 0 && !Data.IsSample) ShowWaitingForPartner();
            else if (_waiting != null && Data.IsSample && !Prefs.Current.sampleIntroDone) Toast($"{_waiting.OwnerName} left you a voice note. Pick up the cup.", 6f);
        }

        void ShowWaitingForPartner()
        {
            var l = NewPanel("waiting");
            l.Title($"Waiting for someone to join {CurrentRoom.Name}");
            if (CurrentRoom.IsOwner) InviteBlock(l, CurrentRoom);
            else l.Body("When others are here, their shared moments arrive on this table.");
            l.Row(("waiting:room", "Look around the room", HidePanel, PressStyle.Secondary));
            FinishPanel(l);
        }

        // ------------------------------------------------------------ the cup

        void BuildCup()
        {
            var anchor = Anchor("Cup");
            var pos = anchor != null ? anchor.position : new Vector3(0.16f, 0.78f, 0.5f);
            var saucer = Props.Saucer();
            saucer.transform.SetParent(_roomContent, false);
            saucer.transform.position = pos - new Vector3(0, 0.004f, 0);
            pos += new Vector3(0, 0.008f, 0); // the cup sits in the saucer
            var cupGo = Props.Cup();
            cupGo.name = "Cup";
            cupGo.transform.SetParent(_roomContent, true);
            cupGo.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, -30, 0));
            _cupRestPos = cupGo.transform.position;
            _cupRestRot = cupGo.transform.rotation;
            _cup = GrabbableObject.Make(cupGo, "object:cup");
            _cup.Grabbed += OnCupGrabbed;
            _cup.Released += OnCupReleased;
            _voice = cupGo.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _voice.spatialBlend = 1f;
            _voice.minDistance = 0.4f;
            _voice.maxDistance = 6f;
            _voice.rolloffMode = AudioRolloffMode.Logarithmic;

            // label above the cup + a poke alternative to grabbing
            _cupLabel = Ui.Node(_roomContent, "Cup label").transform;
            _cupLabel.position = pos + new Vector3(0, 0.17f, 0);
            Ui.Panel(_cupLabel, "Bg", new Vector2(0.24f, 0.115f), Theme.Card, 0.016f, new Vector3(0, 0, 0.002f));
            _cupLabelText = Ui.Text(_cupLabel, "Text", "", Theme.Small, Theme.Ink, 0.22f, TextAlignmentOptions.Top, new Vector3(0, 0.03f, 0), height: 0.06f);
            _cupPlay = Pressable.Create(_cupLabel, "cup:play", "Play", new Vector2(0.1f, 0.036f), PlayWaiting, PressStyle.Primary, new Vector3(-0.055f, -0.033f, 0));
            _cupReply = Pressable.Create(_cupLabel, "cup:reply", "Reply", new Vector2(0.1f, 0.036f), StartReply, PressStyle.Secondary, new Vector3(0.055f, -0.033f, 0));

            // captions plate (hidden until something plays)
            _captionPlate = Ui.Node(_roomContent, "Captions").transform;
            Ui.Panel(_captionPlate, "Bg", new Vector2(0.42f, 0.085f), Theme.CardDark, 0.02f, new Vector3(0, 0, 0.002f));
            _captionMeta = Ui.Text(_captionPlate, "Meta", "", Theme.Small, Theme.Label, 0.4f, TextAlignmentOptions.TopLeft, new Vector3(0, 0.026f, 0));
            _captionText = Ui.Text(_captionPlate, "Line", "", Theme.Body, Theme.OnDark, 0.4f, TextAlignmentOptions.TopLeft, new Vector3(0, 0.004f, 0), height: 0.05f);
            _captionPlate.gameObject.SetActive(false);
            RefreshCupLabel();
        }

        void RefreshCupLabel()
        {
            if (_cupLabelText == null) return;
            if (_waiting == null)
            {
                _cupLabelText.text = "Nothing waiting in the cup.\n<size=80%>Voice notes shared with you arrive here.</size>";
                _cupPlay.Interactable = false;
                _cupReply.Interactable = false;
                return;
            }
            var origin = OriginLabel(_waiting.Media);
            _cupLabelText.text = $"<b>{_waiting.OwnerName} left you a voice note</b>\n<size=80%>{WhenLabel(_waiting)}{(origin != null ? " · " + origin : "")}\nPick up the cup, or press Play</size>";
            _cupPlay.Interactable = true;
            _cupReply.Interactable = _heardThisVisit || SampleState.Current.voiceNoteHeard || !Data.IsSample;
        }

        void OnCupGrabbed()
        {
            _cupReturnAt = -1f;
            TestProbe.Event("cup", "picked-up");
            PlayWaiting();
        }

        void OnCupReleased()
        {
            TestProbe.Event("cup", "released");
            _cupReturnAt = Time.unscaledTime + 1.5f; // settle back on the table after a moment
        }

        void PlayWaiting()
        {
            if (_waiting == null) { Toast("Nothing is waiting in the cup yet."); return; }
            Run(async ct =>
            {
                var clip = await Data.Media.Audio(_waiting.Media, ct);
                if (clip == null) throw new ApiException(0, "no_audio", "This voice note couldn't be played.");
                _voice.clip = clip;
                _voice.volume = Prefs.Current.volume;
                _voice.Play();
                _cupVoicePlaying = true;
                TestProbe.AudioPlaying = $"cup:{_waiting.Id}";
                TestProbe.Event("voice", "play");
                SetPresenceLabel("recorded");
                _captionPlate.gameObject.SetActive(Prefs.Current.captionsOn);
                _captionMeta.text = $"{_waiting.OwnerName.ToUpperInvariant()} · {WhenLabel(_waiting)}{(OriginLabel(_waiting.Media) is { } o ? " · " + o : "")}";
            }, "The voice note couldn't play", PlayWaiting);
        }

        void StopVoice()
        {
            if (_voice != null) _voice.Stop();
            _cupVoicePlaying = false;
            if (TestProbe.AudioPlaying.StartsWith("cup:")) TestProbe.AudioPlaying = "";
            TestProbe.Captions = "";
            if (_captionPlate != null) _captionPlate.gameObject.SetActive(false);
        }

        void UpdateRoom()
        {
            if (_roomContent == null) return;
            var head = HeadPosition;
            if (_cupLabel != null) Ui.FaceHead(_cupLabel, head);

            // captions follow the voice note, timed from the embedded cues or the transcript
            if (_voice != null && _voice.isPlaying && _waiting != null)
            {
                var line = CaptionAt(_waiting, (int)(_voice.time * 1000));
                TestProbe.Captions = Prefs.Current.captionsOn ? line : "";
                if (_captionText != null) _captionText.text = line;
                if (_captionPlate != null)
                {
                    _captionPlate.gameObject.SetActive(Prefs.Current.captionsOn);
                    var at = _cup.transform.position + Vector3.up * 0.26f;
                    _captionPlate.position = Vector3.Lerp(_captionPlate.position, at, Time.unscaledDeltaTime * 6f);
                    Ui.FaceHead(_captionPlate, head);
                }
            }
            else if (_cupVoicePlaying && _voice != null && !_voice.isPlaying && !Paused)
            {
                // finished
                TestProbe.Event("voice", "finished");
                StopVoice();
                _heardThisVisit = true;
                if (Data.IsSample) { SampleState.Current.voiceNoteHeard = true; SampleState.Save(); Prefs.Current.sampleIntroDone = true; Prefs.Save(); }
                SetPresenceLabel(Data.IsSample ? "simulated" : "away");
                RefreshCupLabel();
                Toast("Leave a reply? Press Reply by the cup.");
            }

            // put the cup back on the table once let go
            if (_cup != null && !_cup.IsHeld && _cupReturnAt > 0 && Time.unscaledTime > _cupReturnAt)
            {
                var t = _cup.transform;
                t.position = Vector3.Lerp(t.position, _cupRestPos, Time.unscaledDeltaTime * 5f);
                t.rotation = Quaternion.Slerp(t.rotation, _cupRestRot, Time.unscaledDeltaTime * 5f);
                if ((t.position - _cupRestPos).sqrMagnitude < 1e-6f) _cupReturnAt = -1f;
            }

            AnimatePartner(head);
            foreach (var n in _notes) if (n != null) Ui.FaceHead(n, head, tiltToEyes: false);
        }

        static string CaptionAt(Moment m, int ms)
        {
            var cues = m.Media?.Captions;
            if (cues != null && cues.Count > 0)
            {
                var cue = cues.LastOrDefault(c => c.StartMs <= ms) ?? cues[0];
                return cue.Text;
            }
            return string.IsNullOrEmpty(m.Transcript) ? "(No words for this one: the person didn't add a transcript.)" : m.Transcript;
        }

        // ------------------------------------------------------------ replies left on the table

        void StartReply()
        {
            if (_waiting == null) return;
            OpenKeyboard($"Reply to {_waiting.OwnerName}", SpatialKeyboard.Kind.Text, "", text =>
            {
                text = text.Trim();
                if (text.Length == 0) { CloseKeyboard(); return; }
                CloseKeyboard();
                Run(async ct =>
                {
                    var reply = await Data.AddReply(_waiting.Id, text, Ids.ReplyKey(_waiting.Id, text), ct);
                    var slot = NextNoteSlot();
                    var placed = await Data.Place(CurrentRoom.Id, "note", _waiting.Id, reply.Id, slot, 0f, ct);
                    BuildNote(reply, slot);
                    TestProbe.Event("reply", "placed");
                    Toast(Data.IsSample ? "Your reply is on the table (kept in the sample on this device)." : $"Sent. Your reply waits on the table for {_waiting.OwnerName}.");
                }, "Your reply wasn't saved", StartReply);
            }, null, "A short note. It stays beside the cup.", 280);
        }

        Vector3 NextNoteSlot()
        {
            var basePos = Anchor("ReplySpot") != null ? Anchor("ReplySpot").position : _cupRestPos + new Vector3(0.12f, 0, -0.02f);
            return basePos + new Vector3(0.075f * (_notes.Count % 3), 0.002f * _notes.Count, -0.06f * (_notes.Count / 3));
        }

        async Task RestoreNotes(System.Threading.CancellationToken ct)
        {
            if (_waiting == null) return;
            var replies = await Data.Replies(_waiting.Id, ct);
            var objects = await Data.Objects(CurrentRoom.Id, ct);
            foreach (var r in replies.Where(r => r.AuthorId == Data.Me.Id))
            {
                var o = objects.FirstOrDefault(x => x.ReplyId == r.Id || x.Id == $"note:{r.Id}");
                var pos = o != null ? new Vector3(o.Transform.X, o.Transform.Y, o.Transform.Z) : NextNoteSlot();
                BuildNote(r, pos);
            }
        }

        void BuildNote(Reply reply, Vector3 pos)
        {
            var note = Ui.Node(_roomContent, "Note " + reply.Id).transform;
            note.position = pos + Vector3.up * 0.045f;
            Ui.Panel(note, "Paper", new Vector2(0.11f, 0.075f), new Color(0.99f, 0.97f, 0.9f, 1f), 0.006f, new Vector3(0, 0, 0.001f), new Color(0.8f, 0.75f, 0.65f, 1f));
            Ui.Text(note, "Text", reply.Text, Theme.Small * 0.9f, Theme.Ink, 0.1f, TextAlignmentOptions.TopLeft, new Vector3(0, 0.022f, 0), height: 0.05f).overflowMode = TextOverflowModes.Ellipsis;
            Ui.Text(note, "By", $"— {reply.AuthorName}", Theme.Small * 0.7f, Theme.Muted, 0.1f, TextAlignmentOptions.BottomRight, new Vector3(0, -0.026f, 0));
            _notes.Add(note);
        }

        // ------------------------------------------------------------ keepsake shelf

        void BuildKeepsakes(List<Award> awards)
        {
            var shelf = Anchor("Shelf");
            if (shelf == null) return;
            var earned = awards.Where(a => a.Earned).ToList();
            for (var i = 0; i < earned.Count; i++)
            {
                var a = earned[i];
                var root = Ui.Node(_roomContent, "Keepsake " + a.Milestone).transform;
                root.position = shelf.position + shelf.right * (-0.18f + 0.18f * i) + Vector3.up * 0.02f;
                root.rotation = shelf.rotation;
                Props.Keepsake(a.Keepsake, root);
                var label = Ui.Node(root, "Label", new Vector3(0, 0.2f, 0)).transform;
                Ui.Text(label, "Text", a.Title + "\n<size=75%>earned " + Short(a.EarnedAt) + "</size>", Theme.Small * 0.8f, Theme.Ink, 0.16f, TextAlignmentOptions.Center);
                _notes.Add(label); // billboards toward the person like the notes
            }
        }

        static string Short(string iso) => DateTimeOffset.TryParse(iso, out var t) ? t.ToString("d MMM") : "";

        // ------------------------------------------------------------ frames

        async Task BuildFrame(Moment m, Transform anchor, System.Threading.CancellationToken ct)
        {
            if (anchor == null) return;
            var tex = await Data.Media.Image(m.Media, thumb: false, ct);
            if (ct.IsCancellationRequested || _roomContent == null) return;
            var frame = Props.Frame(tex, m.Media.Width ?? 16, m.Media.Height ?? 10);
            frame.transform.SetParent(_roomContent, false);
            frame.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            var tag = OriginLabel(m.Media);
            var p = Pressable.Create(frame.transform, "frame:" + m.Id, "", new Vector2(0.2f, 0.14f), () => ShowMoment(m, EnterRoom), PressStyle.Quiet, new Vector3(0, 0.07f, -0.012f));
            p.Label.text = "";
            Ui.Text(frame.transform, "Caption", $"{m.OwnerName} · {m.CaptureLocal?.Time} {m.CaptureLocal?.Abbreviation}{(tag != null ? " · " + tag : "")}", Theme.Small * 0.75f, Theme.Muted, 0.2f, TextAlignmentOptions.Top, new Vector3(0, -0.012f, -0.01f));
        }

        /// <summary>One shared moment, up close: photo or poster, caption, who and when, source label, react, reply.</summary>
        public void ShowMoment(Moment m, Action back)
        {
            var l = NewPanel("moment:" + m.Kind, 0.5f);
            l.Title(m.Caption.Length > 0 ? m.Caption : m.Kind == "voice" ? "Voice note" : "Moment");
            l.Muted($"{m.OwnerName} · {m.CaptureLocal?.Time} {m.CaptureLocal?.Abbreviation} ({m.CaptureLocal?.Zone?.Replace('_', ' ')}) · {SourceLabel(m.Media)}");
            if (OriginLabel(m.Media) is { } origin) l.Tag(origin);
            if (m.Media != null && m.Media.Kind != "audio")
            {
                var img = l.Image(null, (float)(m.Media.Width ?? 16) / (m.Media.Height ?? 10), 0.22f);
                Run(async ct => Ui.SetImage(img, await Data.Media.Image(m.Media, false, ct)), "The photo couldn't load");
            }
            if (m.Kind == "voice" && !string.IsNullOrEmpty(m.Transcript)) l.Body($"“{m.Transcript}”");
            l.Muted("Shared with " + string.Join(", ", m.Audience.Select(a => a.DisplayName)));
            var reactions = new (string, string, Action, PressStyle)[]
            {
                ("react:heart", "Love", () => React(m, "heart"), PressStyle.Chip),
                ("react:hug", "Hug", () => React(m, "hug"), PressStyle.Chip),
                ("react:sun", "Sunny", () => React(m, "sun"), PressStyle.Chip),
            };
            var row = l.Row(reactions);
            var mine = m.Reactions.FirstOrDefault(r => r.UserId == Data.Me.Id)?.Kind;
            for (var i = 0; i < row.Count; i++) row[i].SetToggled(new[] { "heart", "hug", "sun" }[i] == mine);
            l.Row(("moment:back", "Back", back, PressStyle.Quiet),
                  ("moment:reply", "Reply", () => { _waiting ??= m; StartReplyTo(m, back); }, PressStyle.Secondary),
                  ("moment:play", m.Kind == "clip" ? "Play on the screen" : "Hear it", () => PlayMomentOnScreen(m), PressStyle.Primary));
            FinishPanel(l);
        }

        void React(Moment m, string kind) => Run(async ct =>
        {
            await Data.React(m.Id, kind, ct);
            TestProbe.Event("react", kind);
            Toast("Sent.");
        }, "The reaction wasn't sent");

        void StartReplyTo(Moment m, Action back)
        {
            OpenKeyboard($"Reply to {m.OwnerName}", SpatialKeyboard.Kind.Text, "", text =>
            {
                CloseKeyboard();
                if (text.Trim().Length == 0) { back(); return; }
                Run(async ct =>
                {
                    await Data.AddReply(m.Id, text.Trim(), Ids.ReplyKey(m.Id, text.Trim()), ct);
                    Toast("Reply sent.");
                    back();
                }, "Your reply wasn't saved");
            }, back, null, 280);
        }

        // ------------------------------------------------------------ the person across the table

        void SetPartner(Member m, string presence)
        {
            if (_partner != null && (m == null || !_partner.name.EndsWith(m.Id)))
            {
                _partner.gameObject.SetActive(false);
                _partner = null;
            }
            _presence = m == null ? "away" : presence;
            if (m == null) return;
            var seat = Anchor("PartnerSeat");
            if (seat == null) return;
            if (_partner == null)
            {
                // sample: the authored demo character for this fictional person; live: Avatars.Load (photo-derived or labelled demo)
                _partner = Avatars.SeatFor(m, seat, Data.IsSample);
                if (_partner != null) _partner.name = "Partner " + m.Id;
            }
            if (_partnerLabel == null && _roomContent != null)
            {
                var label = Ui.Node(_roomContent, "Presence label").transform;
                label.position = seat.position + Vector3.up * 1.38f;
                _partnerLabel = Ui.Text(label, "Text", "", Theme.Small, Theme.Ink, 0.36f, TextAlignmentOptions.Center);
                Ui.Panel(label, "Bg", new Vector2(0.36f, 0.045f), Theme.Glass, 0.02f, new Vector3(0, 0, 0.002f));
            }
            SetPresenceLabel(presence);
        }

        public void SetPresenceLabel(string presence)
        {
            _presence = presence;
            TestProbe.Event("presence", presence);
            if (_partnerLabel == null) return;
            var other = CurrentRoom?.Members.FirstOrDefault(x => x.Id != Data?.Me?.Id);
            var name = other?.DisplayName ?? "";
            var local = other != null ? LocalTimeFor(other.TimeZone) : "";
            _partnerLabel.text = presence switch
            {
                "live" => $"<b>{name}</b> · here now · {local}",
                "simulated" => $"<b>{name}</b> · simulated presence (fictional sample)",
                "recorded" => $"<b>{name}</b> · recorded message, not live",
                _ => $"<b>{name}</b> · not here now · {local} for them",
            };
        }

        void AnimatePartner(Vector3 head)
        {
            if (_partner == null) return;
            Avatars.Idle(_partner, head, _presence);
        }

        // ------------------------------------------------------------ labels

        public static string OriginLabel(MediaInfo media) => media?.Origin switch
        {
            "synthesized" => "Synthesized voice (sample)",
            "simulated" => "Simulated camera pan (sample)",
            "stock" => "Stock photo (sample)",
            _ => null,
        };

        public static string SourceLabel(MediaInfo media) => media == null ? "Text" : media.SourceDevice switch
        {
            "phone" => "Phone", "tablet" => "Tablet", "webcam" => "Webcam", "laptop" => "Laptop", "camera" => "Camera",
            "action_camera" => "Action camera", "drone" => "Drone", "glasses" => "AI glasses", "headset" => "Headset",
            _ => "Device",
        };

        string WhenLabel(Moment m) => m.CaptureLocal == null ? "" :
            $"{m.CaptureLocal.Time} for {m.OwnerName} ({m.CaptureLocal.Abbreviation})";

        public static string LocalTimeFor(string zone)
        {
            try
            {
                var tz = FindZone(zone);
                var t = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
                return t.ToString("HH:mm");
            }
            catch (Exception) { return ""; }
        }

        static TimeZoneInfo FindZone(string iana)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(iana); }
            catch (Exception)
            {
                // Windows (Editor / simulator) only knows Windows ids for some zones
                var map = new Dictionary<string, string>
                {
                    ["Asia/Dubai"] = "Arabian Standard Time", ["Asia/Shanghai"] = "China Standard Time", ["Europe/London"] = "GMT Standard Time",
                    ["Europe/Berlin"] = "W. Europe Standard Time", ["America/New_York"] = "Eastern Standard Time", ["America/Los_Angeles"] = "Pacific Standard Time",
                    ["Asia/Kolkata"] = "India Standard Time", ["Asia/Tokyo"] = "Tokyo Standard Time", ["Asia/Singapore"] = "Singapore Standard Time",
                    ["Asia/Riyadh"] = "Arab Standard Time", ["Africa/Cairo"] = "Egypt Standard Time", ["Australia/Sydney"] = "AUS Eastern Standard Time",
                    ["America/Chicago"] = "Central Standard Time",
                };
                return map.TryGetValue(iana, out var win) ? TimeZoneInfo.FindSystemTimeZoneById(win) : TimeZoneInfo.Utc;
            }
        }
    }
}
