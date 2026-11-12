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
        Transform _cupLabel, _captionPlate, _pinchCue;
        TextMeshPro _cupLabelText, _captionText, _captionMeta, _msgTitle, _msgInitial;
        Pressable _cupPlay, _cupReply, _msgPlace, _msgPause;
        MeshRenderer _msgFill, _msgPortrait, _cupBody;
        TextMeshPro _msgTime;
        int _msgSecond = -1;
        bool _voicePausedByUser, _voiceLoading, _msgHasPortrait;
        float _highlightLevel;
        MaterialPropertyBlock _highlightBlock;
        const float MsgTrackLeft = -0.166f, MsgTrackWidth = 0.262f;
        bool _heardThisVisit;
        bool _cupVoicePlaying;
        float _cupReturnAt = -1f;
        Transform _partner;
        Member _partnerMember;         // who the partner chair is for (seated only while they are here)
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
            CloseBooth();
            HidePanel();
            Screen = "room";
            SetPaused(false, "menu"); // back from the pause menu by any path (Resume, or Leave then Stay)
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
            BuildRoomChip();
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
            _roomSettled = false;
            StopArrival();
            SetPartner(null, "away");
        }

        async Task PopulateRoom(System.Threading.CancellationToken ct)
        {
            ClearRoom();
            ClearScreenFor(CurrentRoom.Id);
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

            // who's across the table. The chair is empty unless they are here: live, or (in the sample) simulated
            // presence, which arrives once the waiting message has been heard (visual references 03 and 02).
            _partnerLabel = null; // rebuilt with the room content
            // A circle is asynchronous: a board says whose room this is and who is in it, and nobody is shown seated
            // as if they were here (visual reference 07).
            if (!CurrentRoom.IsDirect) { SetPartner(null, "away"); BuildCircleBoard(others); }
            else if (Data.IsSample) SetPartner(others.FirstOrDefault(), _waiting == null || SampleState.Current.voiceNoteHeard || _sampleTogether ? "simulated" : "away");
            else SetPartner(others.FirstOrDefault(), "away");

            _roomSettled = true;
            if (others.Count == 0 && !Data.IsSample) ShowWaitingForPartner();
            else if (_waiting != null && Data.IsSample && !Prefs.Current.sampleIntroDone) ShowPinchCue();
        }

        /// <summary>The circle's board, standing where a partner would sit: the room's name, that it is asynchronous,
        /// and its members with their place and local time (portraits are membership, not presence).</summary>
        void BuildCircleBoard(List<Member> others)
        {
            var seat = Anchor("PartnerSeat");
            if (seat == null || _roomContent == null) return;
            var board = Ui.Node(_roomContent, "Circle board").transform;
            board.position = seat.position + Vector3.up * 1.2f;
            var people = new List<Member> { MeAsMember() };
            people.AddRange(others.Take(4));
            // tiles are drawn at arm's-length sizes and scaled up to read from across the table (about 1.5 m)
            const float scale = 1.45f, tileW = 0.21f, tileH = 0.094f, gap = 0.014f;
            var rowWidth = (people.Count * tileW + (people.Count - 1) * gap) * scale;
            var w = Mathf.Max(0.78f, rowWidth + 0.08f);
            const float h = 0.37f;
            Ui.Panel(board, "Bg", new Vector2(w, h), new Color(0.96f, 0.95f, 0.92f, 0.9f), 0.04f, new Vector3(0, 0, 0.003f), new Color(1, 1, 1, 0.6f), 0.0015f, Ui.OrderBackground);
            Ui.Text(board, "Name", Icons.With(Icons.Users, CurrentRoom.Name), Theme.Title * 1.5f, Theme.Ink, w - 0.08f, TextAlignmentOptions.Left, new Vector3(0, h / 2 - 0.05f, 0), FontStyles.Bold, 0.05f);
            Ui.Text(board, "About", $"A circle of {CurrentRoom.Members.Count}. What's shared here reaches everyone in it; nobody is shown as here live.",
                Theme.Body * 1.5f, Theme.Muted, w - 0.08f, TextAlignmentOptions.TopLeft, new Vector3(0, h / 2 - 0.12f, 0), height: 0.06f);
            var row = Ui.Node(board, "People", new Vector3(0, -h / 2 + 0.03f + tileH * scale / 2, 0)).transform;
            row.localScale = Vector3.one * scale;
            var x = -(people.Count * tileW + (people.Count - 1) * gap) / 2f + tileW / 2f;
            foreach (var m in people)
            {
                var mine = m.Id == Data.Me.Id;
                var who = mine ? "You" : m.DisplayName + (string.IsNullOrEmpty(m.Label) ? "" : $" · {m.Label}");
                PersonTile(row, new Vector3(x, 0, 0), new Vector2(tileW, tileH), m, who, LocalTimeFor(m.TimeZone), CityOf(m));
                x += tileW + gap;
            }
        }

        /// <summary>The room selector (visual reference 07): each room keeps its own moments, stories and keepsakes.</summary>
        public void ShowRooms()
        {
            var l = NewPanel("rooms");
            l.Title("Your rooms");
            l.Muted("Each room keeps its own moments, stories and keepsakes. Nothing crosses from one to another.");
            foreach (var r in Data.Rooms)
            {
                var room = r;
                var size = room.IsDirect ? "the two of you" : $"{room.Members.Count} people";
                l.Button("rooms:" + room.Id, Icons.With(room.IsDirect ? Icons.Heart : Icons.Users, $"{room.Name} · {size}"), () =>
                {
                    CurrentRoom = room;
                    EnterRoom();
                }, room.Id == CurrentRoom.Id ? PressStyle.Primary : PressStyle.Secondary);
            }
            FinishPanel(l, ("rooms:back", "Back", EnterRoom, PressStyle.Dark));
        }

        /// <summary>A pill above the dock that says which room this is; with more than one room it opens the selector.</summary>
        Pressable _roomChip;

        void BuildRoomChip()
        {
            if (_dock == null || Data == null || CurrentRoom == null) return;
            var several = Data.Rooms.Count > 1;
            var chip = Pressable.Create(_dock, "dock:room", Icons.With(CurrentRoom.IsDirect ? Icons.Heart : Icons.Users, CurrentRoom.Name), new Vector2(0.17f, 0.034f),
                several ? (Action)ShowRooms : null, PressStyle.Dark, new Vector3(-0.178f, 0.054f, 0));
            chip.Label.fontSize *= 0.8f;
            chip.Label.textWrappingMode = TextWrappingModes.NoWrap;
            chip.Interactable = several;
            _roomChip = chip;
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
            var table = pos - new Vector3(0, 0.004f, 0);
            var coaster = Props.Coaster();
            coaster.transform.SetParent(_roomContent, false);
            coaster.transform.position = table;
            Props.ContactShadow(_roomContent, table, 0.085f);
            pos = table + new Vector3(0, 0.008f, 0); // the cup sits on the coaster
            var cupGo = Props.Cup();
            cupGo.name = "Cup";
            cupGo.transform.SetParent(_roomContent, true);
            cupGo.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, -30, 0));
            _cupRestPos = cupGo.transform.position;
            _cupRestRot = cupGo.transform.rotation;
            _cup = GrabbableObject.Make(cupGo, "object:cup");
            _cupBody = Props.CupBody(cupGo);
            _highlightLevel = 0f;
            _cup.Grabbed += OnCupGrabbed;
            _cup.Released += OnCupReleased;
            _voice = cupGo.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _voice.spatialBlend = 1f;
            _voice.minDistance = 0.4f;
            _voice.maxDistance = 6f;
            _voice.rolloffMode = AudioRolloffMode.Logarithmic;

            // a quiet label over the cup while a message waits
            _cupLabel = Ui.Node(_roomContent, "Cup label").transform;
            _cupLabel.position = pos + new Vector3(0, 0.16f, 0);
            _cupLabelText = Ui.Text(_cupLabel, "Text", "", Theme.Body, Theme.Ink, 0.24f, TextAlignmentOptions.Bottom, Vector3.zero, FontStyles.Bold, 0.03f);
            Ui.Panel(_cupLabel, "Underline", new Vector2(0.07f, 0.0028f), Theme.Green, 0.0014f, new Vector3(0, -0.018f, 0.001f), new Color(1, 1, 1, 0), 0);

            // folded cards on the table: the voice note (press to play) and a reply note (press to answer)
            var head = HeadPosition;
            Quaternion Facing(Vector3 at) { var away = at - head; away.y = 0; return Quaternion.LookRotation(away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.forward, Vector3.up); }
            var noteAt = table + new Vector3(-0.135f, 0, 0.015f);
            var note = Props.TentCard(0.112f, 0.062f);
            note.transform.SetParent(_roomContent, false);
            note.transform.SetPositionAndRotation(noteAt, Facing(noteAt));
            Props.ContactShadow(_roomContent, noteAt, 0.075f, 0.6f);
            _cupPlay = Pressable.Create(note.transform.Find("Front"), "cup:play", Icons.Waveform + "<space=0.25em>" + Icons.Play, new Vector2(0.1f, 0.052f), PlayWaiting, PressStyle.Surface);
            _cupPlay.Label.fontSize *= 1.35f;
            var replyAt = table + new Vector3(0.125f, 0, -0.06f);
            var reply = Props.TentCard(0.122f, 0.056f);
            reply.transform.SetParent(_roomContent, false);
            reply.transform.SetPositionAndRotation(replyAt, Facing(replyAt));
            Props.ContactShadow(_roomContent, replyAt, 0.078f, 0.6f);
            _cupReply = Pressable.Create(reply.transform.Find("Front"), "cup:reply", Icons.With(Icons.Reply, "Reply"), new Vector2(0.11f, 0.046f), StartReply, PressStyle.Surface);

            BuildMessageCard(pos);
            RefreshCupLabel();
        }

        /// <summary>The message card (visual reference 03): who left it and when, that it is a recording, the caption as
        /// it plays with the playback position, and what to do next (pause, replay, reply; place the cup while it is
        /// held). It stands to the left of the cup, clear of the hand holding it and of the person across the table.</summary>
        void BuildMessageCard(Vector3 cupPos)
        {
            const float w = 0.36f, h = 0.2f;
            _msgHasPortrait = false;
            _captionPlate = Ui.Node(_roomContent, "Message card").transform;
            _captionPlate.position = cupPos + new Vector3(-0.3f, 0.19f, -0.01f);
            var bg = Theme.CardDark;
            bg.a = 0.985f;   // nearly solid: what stands behind it on the table shouldn't read through the caption
            Ui.Panel(_captionPlate, "Bg", new Vector2(w, h), bg, 0.024f, new Vector3(0, 0, 0.002f), new Color(1, 1, 1, 0.14f), order: Ui.OrderBackground);
            // the speaker: a portrait of the character that sits in the room when there is one, else their initial
            var portrait = new Vector3(-0.14f, 0.058f, 0);
            _msgPortrait = Ui.Panel(_captionPlate, "Portrait", new Vector2(0.052f, 0.052f), Theme.GreenSoft, 0.026f, portrait + new Vector3(0, 0, 0.001f));
            _msgInitial = Ui.Text(_captionPlate, "Initial", "", Theme.Title, Theme.Ink, 0.052f, TextAlignmentOptions.Center, portrait, FontStyles.Bold, 0.052f);
            _msgTitle = Ui.Text(_captionPlate, "Title", "", Theme.Body * 1.15f, Theme.OnDark, 0.17f, TextAlignmentOptions.Left, new Vector3(-0.015f, 0.071f, 0), FontStyles.Bold, 0.028f);
            _msgTitle.textWrappingMode = TextWrappingModes.NoWrap;
            _msgTitle.overflowMode = TextOverflowModes.Ellipsis;
            // a recording, said plainly: the person isn't here now
            var chip = new Vector3(0.122f, 0.071f, 0);
            Ui.Panel(_captionPlate, "Recorded", new Vector2(0.088f, 0.024f), new Color(1, 1, 1, 0.07f), 0.012f, chip + new Vector3(0, 0, 0.001f), new Color(1, 1, 1, 0.22f), 0.0008f);
            Ui.Text(_captionPlate, "RecordedText", Icons.With(Icons.Waveform, "Recorded"), Theme.Small * 0.9f, Theme.GreenSoft, 0.088f, TextAlignmentOptions.Center, chip, height: 0.024f);
            _captionMeta = Ui.Text(_captionPlate, "Meta", "", Theme.Small, Theme.GreenSoft, 0.266f, TextAlignmentOptions.Left, new Vector3(0.033f, 0.042f, 0), height: 0.02f);
            _captionMeta.textWrappingMode = TextWrappingModes.NoWrap;
            _captionMeta.overflowMode = TextOverflowModes.Ellipsis;
            _captionText = Ui.Text(_captionPlate, "Line", "", Theme.Body, Theme.OnDark, 0.332f, TextAlignmentOptions.TopLeft, new Vector3(0, 0.002f, 0), FontStyles.Italic, 0.046f);
            // where the recording is: a thin track and the time
            Ui.Panel(_captionPlate, "Track", new Vector2(MsgTrackWidth, 0.004f), new Color(1, 1, 1, 0.16f), 0.002f, new Vector3(MsgTrackLeft + MsgTrackWidth / 2f, -0.036f, 0.001f), new Color(1, 1, 1, 0), 0);
            _msgFill = Ui.Panel(_captionPlate, "Played", new Vector2(0.004f, 0.004f), Theme.GreenSoft, 0.002f, new Vector3(MsgTrackLeft + 0.002f, -0.036f, 0.0005f), new Color(1, 1, 1, 0), 0, Ui.OrderFill);
            _msgTime = Ui.Text(_captionPlate, "Time", "", Theme.Small * 0.85f, new Color(1, 1, 1, 0.62f), 0.064f, TextAlignmentOptions.Right, new Vector3(0.134f, -0.036f, 0), height: 0.016f);
            const float by = -0.072f, bw = 0.104f, gap = 0.01f;
            _msgPause = Pressable.Create(_captionPlate, "cup:pause", Icons.With(Icons.Pause, "Pause"), new Vector2(bw, 0.036f), ToggleVoicePause, PressStyle.Dark, new Vector3(-bw - gap, by, 0));
            Pressable.Create(_captionPlate, "cup:replay", Icons.With(Icons.Replay, "Replay"), new Vector2(bw, 0.036f), () => { StopVoice(hideCard: false); PlayWaiting(); }, PressStyle.Dark, new Vector3(0, by, 0));
            Pressable.Create(_captionPlate, "msg:reply", Icons.With(Icons.Reply, "Reply"), new Vector2(bw, 0.036f), StartReply, PressStyle.Dark, new Vector3(bw + gap, by, 0));
            // while the cup is in hand: put it back on its coaster (a release does the same). Above the card, clear of
            // the dock below it.
            _msgPlace = Pressable.Create(_captionPlate, "cup:place", Icons.With(Icons.Cup, "Place cup"), new Vector2(0.15f, 0.044f), PlaceCup, PressStyle.Primary, new Vector3(-0.1f, h / 2 + 0.032f, 0));
            _msgPlace.gameObject.SetActive(false);
            _captionPlate.gameObject.SetActive(false);
        }

        /// <summary>Pause or resume the message; once it has finished, the same button closes the card.</summary>
        void ToggleVoicePause()
        {
            if (_voice == null || _waiting == null) return;
            if (_voicePausedByUser)
            {
                _voice.UnPause();
                _voicePausedByUser = false;
                TestProbe.AudioPlaying = $"cup:{_waiting.Id}";
                TestProbe.Event("voice", "resume");
            }
            else if (_cupVoicePlaying && _voice.isPlaying)
            {
                _voice.Pause();
                _voicePausedByUser = true;
                TestProbe.AudioPlaying = "";
                TestProbe.Event("voice", "pause");
            }
            else if (_captionPlate != null) _captionPlate.gameObject.SetActive(false);   // finished: "Close"
            RefreshMessageControls();
        }

        void RefreshMessageControls()
        {
            if (_msgPause == null) return;
            if (_voicePausedByUser) _msgPause.SetText(Icons.With(Icons.Play, "Resume"));
            else if (_cupVoicePlaying) _msgPause.SetText(Icons.With(Icons.Pause, "Pause"));
            else _msgPause.SetText(Icons.With(Icons.Close, "Close"));
        }

        void SetMessageProgress(float played, float length)
        {
            if (_msgFill == null) return;
            var t = length > 0.01f ? Mathf.Clamp01(played / length) : 0f;
            var fw = Mathf.Max(0.004f, MsgTrackWidth * t);
            _msgFill.transform.localScale = new Vector3(fw, 0.004f, 1f);
            _msgFill.transform.localPosition = new Vector3(MsgTrackLeft + fw / 2f, -0.036f, 0.0005f);
            Ui.SetPanel(_msgFill, new Vector2(fw, 0.004f), Theme.GreenSoft, 0.002f, new Color(1, 1, 1, 0), 0);
            var second = Mathf.FloorToInt(played);
            if (second == _msgSecond || _msgTime == null) return;
            _msgSecond = second;
            _msgTime.text = $"{Clock(played)} / {Clock(length)}";
        }

        void RefreshCupLabel()
        {
            if (_cupLabelText == null) return;
            if (_waiting == null)
            {
                _cupLabelText.text = "";
                _cupLabel.gameObject.SetActive(false);
                _cupPlay.Interactable = false;
                _cupReply.Interactable = false;
                return;
            }
            var heard = _heardThisVisit || SampleState.Current.voiceNoteHeard || !Data.IsSample;
            // one teaching cue at a time: the label waits while the pick-up cue points at the cup or the note plays
            _cupLabel.gameObject.SetActive(!_heardThisVisit && _pinchCue == null && !_cupVoicePlaying);
            _cupLabelText.text = "A moment is waiting";
            _cupPlay.Interactable = true;
            _cupReply.Interactable = heard;
            _cupReply.SetText(Icons.With(Icons.Reply, $"Reply to {_waiting.OwnerName}"));
            if (_msgInitial != null) _msgInitial.text = _msgHasPortrait || string.IsNullOrEmpty(_waiting.OwnerName) ? "" : _waiting.OwnerName.Substring(0, 1).ToUpperInvariant();
        }

        // ------------------------------------------------------------ the first-minute cue (visual reference 10)

        void ShowPinchCue()
        {
            HidePinchCue();
            if (_cup == null || _waiting == null) return;
            var cupTop = _cupRestPos + Vector3.up * 0.1f;
            _pinchCue = Ui.Node(_roomContent, "Pinch cue").transform;
            _pinchCue.position = _cupRestPos + new Vector3(0.21f, 0.2f, 0.06f);
            Ui.FaceHead(_pinchCue, HeadPosition);
            const float w = 0.235f, h = 0.074f;
            Ui.Panel(_pinchCue, "Bg", new Vector2(w, h), Theme.GreenDark, 0.024f, new Vector3(0, 0, 0.002f), new Color(1, 1, 1, 0.16f), order: Ui.OrderBackground);
            Ui.Text(_pinchCue, "Icon", Icons.Hand, Theme.Title * 1.15f, Theme.OnDark, 0.044f, TextAlignmentOptions.Center, new Vector3(-w / 2 + 0.03f, 0, 0), height: 0.05f);
            Ui.Panel(_pinchCue, "Rule", new Vector2(0.0012f, h * 0.6f), new Color(1, 1, 1, 0.3f), 0.0006f, new Vector3(-w / 2 + 0.058f, 0, 0.001f), new Color(1, 1, 1, 0), 0);
            var cueText = Ui.Text(_pinchCue, "Text", $"Pick up the cup\nto hear {_waiting.OwnerName}'s message", Theme.Body * 0.95f, Theme.OnDark, w - 0.08f, TextAlignmentOptions.Left, new Vector3(0.033f, 0, 0), height: h - 0.012f);
            cueText.textWrappingMode = TextWrappingModes.NoWrap;
            // a fine leader to the cup, ending in a dot
            var start = _pinchCue.TransformPoint(new Vector3(-w / 2 + 0.02f, -h / 2, 0));
            var mid = (start + cupTop) / 2f;
            var leader = Ui.Panel(_roomContent, "Leader", new Vector2(0.0018f, Vector3.Distance(start, cupTop)), new Color(1, 1, 1, 0.75f), 0.0009f, Vector3.zero, new Color(1, 1, 1, 0), 0).transform;
            leader.position = mid;
            leader.rotation = Quaternion.LookRotation((mid - HeadPosition).normalized, (cupTop - start).normalized);
            leader.SetParent(_pinchCue, true);
            var dot = Ui.Panel(_roomContent, "Dot", new Vector2(0.008f, 0.008f), new Color(1, 1, 1, 0.9f), 0.004f, Vector3.zero, new Color(1, 1, 1, 0), 0).transform;
            dot.position = cupTop;
            dot.rotation = leader.rotation;
            dot.SetParent(_pinchCue, true);
            TestProbe.Event("cue", "pick-up-cup");
            RefreshCupLabel();
        }

        void HidePinchCue()
        {
            if (_pinchCue == null) return;
            Destroy(_pinchCue.gameObject);
            _pinchCue = null;
            RefreshCupLabel();
        }

        void PlaceCup()
        {
            if (_cup == null) return;
            _cup.ForceRelease();
            _cupReturnAt = Time.unscaledTime;   // back on its coaster now
            TestProbe.Event("cup", "placed");
        }

        void OnCupGrabbed()
        {
            _cupReturnAt = -1f;
            HidePinchCue();
            TestProbe.Event("cup", "picked-up");
            // a re-grab (after a tracking blip, or from the other hand) carries on rather than starting over
            if (_voicePausedByUser) { ToggleVoicePause(); return; }
            if (_cupVoicePlaying || _voiceLoading) return;
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
            if (_voiceLoading) return;   // one load at a time: a grab and a press together don't start it twice
            _voiceLoading = true;
            Run(async ct =>
            {
                try
                {
                    var clip = await Data.Media.Audio(_waiting.Media, ct);
                    if (clip == null) throw new ApiException(0, "no_audio", "This voice note couldn't be played.");
                    _voice.clip = clip;
                    _voice.volume = Prefs.Current.volume;
                    _voice.Play();
                    _cupVoicePlaying = true;
                    _voicePausedByUser = false;
                    TestProbe.AudioPlaying = $"cup:{_waiting.Id}";
                    TestProbe.Event("voice", "play");
                    HidePinchCue();
                    RefreshCupLabel();
                    // a recording: the chair stays empty unless they are actually here (live, or the labelled sample)
                    if (_presence is not ("live" or "simulated")) SetPresenceLabel("recorded");
                    _captionPlate.gameObject.SetActive(true);
                    _msgTitle.text = string.IsNullOrWhiteSpace(_waiting.Caption) ? "Voice note" : _waiting.Caption;
                    if (!_msgHasPortrait && _msgPortrait != null)
                    {
                        // who is speaking: the face of the character that sits in the room for them, not a claim they're here
                        var owner = CurrentRoom.Members.FirstOrDefault(x => x.Id == _waiting.OwnerId);
                        var face = Avatars.Portrait(owner, Data.IsSample, Anchor("Seat"));
                        if (face != null)
                        {
                            Ui.SetPanel(_msgPortrait, new Vector2(0.052f, 0.052f), Color.white, 0.026f, new Color(1, 1, 1, 0.55f), 0.001f, 0, face);
                            _msgHasPortrait = true;
                            if (_msgInitial != null) _msgInitial.text = "";
                        }
                    }
                    _captionMeta.text = $"{_waiting.OwnerName} · {WhenLabel(_waiting)}{(OriginLabel(_waiting.Media) is { } o ? " · " + o : "")}";
                    _msgSecond = -1;
                    SetMessageProgress(0f, clip.length);
                    RefreshMessageControls();
                }
                finally { _voiceLoading = false; }
            }, "The voice note couldn't play", PlayWaiting);
        }

        void StopVoice() => StopVoice(hideCard: true);

        void StopVoice(bool hideCard)
        {
            if (_voice != null) _voice.Stop();
            _cupVoicePlaying = false;
            _voicePausedByUser = false;
            if (TestProbe.AudioPlaying.StartsWith("cup:")) TestProbe.AudioPlaying = "";
            TestProbe.Captions = "";
            if (_captionText != null) _captionText.text = "";
            RefreshMessageControls();
            if (hideCard && _captionPlate != null) _captionPlate.gameObject.SetActive(false);
            // stopped part-way: the chair's label goes back from "recorded message" to where they really are
            if (hideCard && _presence == "recorded" && _partnerMember != null && Data != null)
                SetPresenceLabel(Data.IsSample && (_heardThisVisit || SampleState.Current.voiceNoteHeard) ? "simulated" : "away");
        }

        void UpdateRoom()
        {
            if (_roomContent == null) return;
            var head = HeadPosition;
            if (_cupLabel != null && _cupLabel.gameObject.activeSelf) Ui.FaceHead(_cupLabel, head);
            if (_captionPlate != null && _captionPlate.gameObject.activeSelf)
            {
                Ui.FaceHead(_captionPlate, head);
                if (_msgPlace != null && _msgPlace.gameObject.activeSelf != CupHeld) _msgPlace.gameObject.SetActive(CupHeld);
            }
            // the room's name steps aside while the message card is open just above it
            var cardOpen = _captionPlate != null && _captionPlate.gameObject.activeSelf;
            if (_roomChip != null && _roomChip.gameObject.activeSelf == cardOpen) _roomChip.gameObject.SetActive(!cardOpen);

            // a restrained glow on the cup while a hand is near enough to take it, and while it is held
            if (_cupBody != null && _cup != null)
            {
                var want = _cup.IsHovered || _cup.IsHeld ? 1f : 0f;
                var level = Mathf.MoveTowards(_highlightLevel, want, Time.unscaledDeltaTime * 4f);
                if (!Mathf.Approximately(level, _highlightLevel))
                {
                    _highlightLevel = level;
                    _highlightBlock ??= new MaterialPropertyBlock();
                    _cupBody.GetPropertyBlock(_highlightBlock);
                    _highlightBlock.SetColor("_EmissionColor", new Color(0.2f, 0.21f, 0.18f) * level);
                    _cupBody.SetPropertyBlock(_highlightBlock);
                }
            }

            // captions follow the voice note, timed from the embedded cues or the transcript
            if (_voice != null && _voice.isPlaying && _waiting != null)
            {
                var line = CaptionAt(_waiting, (int)(_voice.time * 1000));
                TestProbe.Captions = Prefs.Current.captionsOn ? line : "";
                if (_captionText != null) _captionText.text = !Prefs.Current.captionsOn || line.Length == 0 ? "" : line.StartsWith("(") ? line : $"“{line}”";
                if (_voice.clip != null) SetMessageProgress(_voice.time, _voice.clip.length);
            }
            else if (_cupVoicePlaying && _voice != null && !_voice.isPlaying && !Paused && !_voicePausedByUser)
            {
                // finished: the card stays, so Replay and Reply are at hand
                TestProbe.Event("voice", "finished");
                if (_voice.clip != null) SetMessageProgress(_voice.clip.length, _voice.clip.length);
                StopVoice(hideCard: false);
                // the whole message stays readable on the card once it has played
                if (_captionText != null && Prefs.Current.captionsOn && !string.IsNullOrEmpty(_waiting.Transcript))
                {
                    _captionText.overflowMode = TextOverflowModes.Ellipsis;
                    _captionText.text = $"“{_waiting.Transcript}”";
                }
                _heardThisVisit = true;
                if (Data.IsSample) { SampleState.Current.voiceNoteHeard = true; SampleState.Save(); Prefs.Current.sampleIntroDone = true; Prefs.Save(); }
                // the sample's Maya joins as simulated presence once her message has been heard; a real person stays
                // as they were (here live, or away)
                if (_presence is "recorded" or "away") SetPresenceLabel(Data.IsSample ? "simulated" : "away");
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
            if (_captionPlate != null) _captionPlate.gameObject.SetActive(false);   // the keyboard takes the space in front
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
                    TestProbe.Event("reply", "placed:" + reply.Id); // ids only: acceptance runs match it after restarts
                    Toast(Data.IsSample ? "Your reply is on the table (kept in the sample on this device)." : $"Sent. Your reply waits on the table for {_waiting.OwnerName}.");
                }, "Your reply wasn't saved", StartReply);
            }, null, "A short note. It stays beside the cup.", 280);
        }

        // Places for replies on the table, in the order they fill: to the right behind the cup, then to the left.
        // (Table space, metres from the table's centre: x to the person's right, z away from them.)
        static readonly Vector2[] NoteSlots =
        {
            new(0.395f, -0.144f), new(0.42f, 0.015f), new(0.238f, -0.127f),
            new(-0.376f, -0.137f), new(-0.297f, -0.268f), new(-0.42f, 0f),
        };

        Vector3 TableCentre => Anchor("TableTop") != null ? Anchor("TableTop").position : new Vector3(0, 0.774f, 0.72f);

        Vector3 NextNoteSlot()
        {
            // the first place that is free; once the outer places are taken, a second ring a little nearer the middle
            var c = TableCentre;
            for (var i = 0; i < NoteSlots.Length * 3; i++)
            {
                var slot = NoteSlots[i % NoteSlots.Length];
                var ring = 1f - 0.22f * (i / NoteSlots.Length);
                var pos = new Vector3(c.x + slot.x * ring, c.y + 0.004f, c.z + slot.y * ring);
                if (NotePlaceIsGood(pos)) return pos;
            }
            var last = NoteSlots[_notes.Count % NoteSlots.Length];
            return new Vector3(c.x + last.x, c.y + 0.004f, c.z + last.y);
        }

        /// <summary>True when a saved place can still hold a card: on this table's top, and clear of the cards
        /// already standing. (Places saved under an older room layout may be off the table or too close together.)</summary>
        bool NotePlaceIsGood(Vector3 pos)
        {
            var c = TableCentre;
            if (Mathf.Abs(pos.y - c.y) > 0.05f) return false;
            if (new Vector2(pos.x - c.x, pos.z - c.z).magnitude > 0.46f) return false;
            foreach (var n in _notes)
                if (n != null && Vector3.Distance(new Vector3(n.position.x, pos.y, n.position.z), pos) < 0.12f) return false;
            if (_cup != null && Vector3.Distance(new Vector3(_cupRestPos.x, pos.y, _cupRestPos.z), pos) < 0.11f) return false;
            return true;
        }

        async Task RestoreNotes(System.Threading.CancellationToken ct)
        {
            if (_waiting == null) return;
            var replies = await Data.Replies(_waiting.Id, ct);
            var objects = await Data.Objects(CurrentRoom.Id, ct);
            var restored = new List<string>();
            foreach (var r in replies.Where(r => r.AuthorId == Data.Me.Id))
            {
                var o = objects.FirstOrDefault(x => x.ReplyId == r.Id || x.Id == $"note:{r.Id}");
                var pos = o != null ? new Vector3(o.Transform.X, o.Transform.Y, o.Transform.Z) : NextNoteSlot();
                BuildNote(r, pos);
                restored.Add(r.Id);
            }
            TestProbe.Event("reply", "restored:" + string.Join(",", restored));
        }

        /// <summary>A reply stays on the table as a small folded card: the first lines of what was written, signed,
        /// turned toward the seat. It stands where it was left when that place is still good.</summary>
        void BuildNote(Reply reply, Vector3 pos)
        {
            if (!NotePlaceIsGood(pos)) pos = NextNoteSlot();
            var c = TableCentre;
            var seat = Anchor("SeatEye") != null ? Anchor("SeatEye").position : new Vector3(0, SeatedEyeHeight, 0);
            var away = new Vector3(pos.x - seat.x, 0, pos.z - seat.z);
            var card = Props.TentCard(0.118f, 0.07f);
            card.name = "Note " + reply.Id;
            card.transform.SetParent(_roomContent, false);
            card.transform.SetPositionAndRotation(new Vector3(pos.x, c.y + 0.004f, pos.z), Quaternion.LookRotation(away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.forward, Vector3.up));
            Props.ContactShadow(_roomContent, new Vector3(pos.x, c.y + 0.004f, pos.z), 0.075f, 0.6f);
            var front = card.transform.Find("Front");
            var text = Ui.Text(front, "Text", reply.Text, Theme.Small * 0.92f, Theme.Ink, 0.104f, TextAlignmentOptions.TopLeft, new Vector3(0, 0.008f, 0), height: 0.044f);
            text.overflowMode = TextOverflowModes.Ellipsis;
            Ui.Text(front, "By", $"— {reply.AuthorName}", Theme.Small * 0.72f, Theme.Muted, 0.104f, TextAlignmentOptions.BottomRight, new Vector3(0, -0.024f, 0), height: 0.014f);
            _notes.Add(card.transform);
        }

        // ------------------------------------------------------------ keepsake shelf

        void BuildKeepsakes(List<Award> awards)
        {
            var shelf = Anchor("Shelf");
            if (shelf == null) return;
            // earned ones, and the one under way (the planter stands there while its days fill in)
            var earned = awards.Where(a => a.Earned || a.Progress > 0).ToList();
            for (var i = 0; i < earned.Count; i++)
            {
                var a = earned[i];
                var root = Ui.Node(_roomContent, "Keepsake " + a.Milestone).transform;
                // on the alcove's lower shelf, a little larger than life so they read from the seat; their names and
                // dates are in the Keepsakes panel (labels this far away would be too small to read)
                root.position = shelf.position + shelf.right * (-0.2f + 0.2f * i) + Vector3.up * 0.012f;
                root.rotation = shelf.rotation;
                root.localScale = Vector3.one * 1.5f;
                Props.Keepsake(a.Keepsake, root);
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
            var p = Pressable.Create(frame.transform, "frame:" + m.Id, "", new Vector2(0.2f, 0.14f), () => ShowMoment(m, EnterRoom), PressStyle.Surface, new Vector3(0, 0.07f, -0.012f));
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
                Avatars.Remove(_partner);
                _partner = null;
            }
            _partnerMember = m;
            if (m == null) { _presence = "away"; return; }
            var seat = Anchor("PartnerSeat");
            if (seat == null) return;
            if (_partnerLabel == null && _roomContent != null)
            {
                // above where their head is (or would be), sized to read from across the table (about 1.5 m)
                var label = Ui.Node(_roomContent, "Presence label").transform;
                label.position = seat.position + Vector3.up * 1.5f;
                _partnerLabel = Ui.Text(label, "Text", "", Theme.Body * 1.5f, Theme.Ink, 0.74f, TextAlignmentOptions.Center, height: 0.05f);
                _partnerLabel.textWrappingMode = TextWrappingModes.NoWrap;
                _partnerLabel.enableAutoSizing = true;   // long names shrink rather than spill
                _partnerLabel.fontSizeMax = _partnerLabel.fontSize;
                _partnerLabel.fontSizeMin = _partnerLabel.fontSize * 0.6f;
                Ui.Panel(label, "Bg", new Vector2(0.78f, 0.066f), new Color(0.96f, 0.95f, 0.92f, 0.82f), 0.033f, new Vector3(0, 0, 0.002f), order: Ui.OrderBackground);
            }
            SetPresenceLabel(presence);
        }

        /// <summary>Someone sits in the partner chair only while they are here: live, or the sample's labelled simulated
        /// presence. Away, or only their recording playing: the chair is empty and the label says so.</summary>
        void SeatPartnerFigure()
        {
            var seated = _partnerMember != null && _presence is ("live" or "simulated");
            if (seated && _partner == null)
            {
                var seat = Anchor("PartnerSeat");
                if (seat == null) return;
                // sample: the authored character for this fictional person; live: photo-derived (approved) or a labelled demo figure
                _partner = Avatars.SeatFor(_partnerMember, seat, Data.IsSample);
                if (_partner != null) _partner.name = "Partner " + _partnerMember.Id;
                if (_partner != null && _roomSettled) _partner.gameObject.SetActive(false);   // arrives below
            }
            if (_partner == null) return;
            StopArrival();
            // Arriving while you are in the room: a brief soft glow where they sit, then they are there. Someone
            // already here when the room opens is simply there.
            if (seated && !_partner.gameObject.activeSelf && _roomSettled && isActiveAndEnabled) _arrival = StartCoroutine(Arrive(_partner));
            else if (_partner.gameObject.activeSelf != seated) _partner.gameObject.SetActive(seated);
        }

        bool _roomSettled;            // false while a room is being filled
        Coroutine _arrival;
        MeshRenderer _arrivalGlow;

        void StopArrival()
        {
            if (_arrival != null) StopCoroutine(_arrival);
            _arrival = null;
            if (_arrivalGlow != null) Destroy(_arrivalGlow.gameObject);
            _arrivalGlow = null;
        }

        System.Collections.IEnumerator Arrive(Transform figure)
        {
            var seat = Anchor("PartnerSeat");
            var at = (seat != null ? seat.position + seat.forward * 0.15f : figure.position) + Vector3.up * 1.0f;
            _arrivalGlow = Props.Glow(_world, at, HeadPosition, 0.6f);
            const float rise = 0.2f, fall = 0.9f, peak = 0.7f;
            for (var t = 0f; t < rise; t += Time.unscaledDeltaTime)
            {
                if (_arrivalGlow != null) Props.SetGlow(_arrivalGlow, t / rise * peak);
                yield return null;
            }
            if (figure != null) figure.gameObject.SetActive(true);
            for (var t = 0f; t < fall; t += Time.unscaledDeltaTime)
            {
                var k = 1f - t / fall;
                if (_arrivalGlow != null) Props.SetGlow(_arrivalGlow, k * k * peak);
                yield return null;
            }
            if (_arrivalGlow != null) Destroy(_arrivalGlow.gameObject);
            _arrivalGlow = null;
            _arrival = null;
        }

        public void SetPresenceLabel(string presence)
        {
            _presence = presence;
            TestProbe.Event("presence", presence);
            SeatPartnerFigure();
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
            if (_partner == null || !_partner.gameObject.activeSelf) return;
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
