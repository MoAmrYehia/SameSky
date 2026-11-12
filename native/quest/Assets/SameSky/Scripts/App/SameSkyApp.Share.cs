// Share a moment from the headset: say something (text), record a voice note (microphone), or share something
// already uploaded from a phone or camera through the companion. Then pick exactly who sees it, decide
// separately whether the daily story's AI may use it, preview, confirm, and withdraw if it was a mistake.
// Each step is idempotent: a retry never publishes twice (one moment per draft, one share per confirmation).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SameSky.Api;
using SameSky.Interaction;
using SameSky.UI;
using UnityEngine;

namespace SameSky.App
{
    public sealed partial class SameSkyApp
    {
        sealed class Draft
        {
            public string Kind;          // text | voice | existing
            public string Caption = "";
            public byte[] Wav;
            public float Seconds;
            public string Transcript;
            public Moment Existing;
            public HashSet<string> Recipients = new();
            public bool AiAllowed;
            public string MediaId;       // uploaded voice (kept so retries don't upload again)
            public Moment Created;       // created moment (kept so retries don't create again)
        }

        Draft _draft;
        AudioClip _recording;
        float _recordStarted;

        public void ShowShare()
        {
            if (Data.IsSample)
            {
                SampleShare();
                return;
            }
            var others = CurrentRoom.Members.Where(m => m.Id != Data.Me.Id).ToList();
            var l = NewPanel("share");
            l.Title("Share a moment");
            if (others.Count == 0)
            {
                l.Body("No one to share with yet.");
                InviteBlock(l, CurrentRoom);
                l.Row(("share:back", "Back", EnterRoom, PressStyle.Quiet));
                FinishPanel(l);
                return;
            }
            _draft = new Draft();
            if (CurrentRoom.IsDirect) foreach (var o in others) _draft.Recipients.Add(o.Id);
            l.Button("share:text", "Say something", () => { _draft.Kind = "text"; ComposeText(); }, PressStyle.Secondary);
            l.Button("share:voice", "Record a voice note (up to 30 s)", () => { _draft.Kind = "voice"; RecordVoice(); }, PressStyle.Secondary);
            l.Button("share:existing", "Share a photo or clip from my phone", () => { _draft.Kind = "existing"; ChooseExisting(); }, PressStyle.Secondary);
            l.Muted("Photos and clips come from your phone or camera through the SameSky companion; the headset doesn't take pictures of your surroundings.");
            l.Row(("share:back", "Back", EnterRoom, PressStyle.Quiet));
            FinishPanel(l);
        }

        void ComposeText()
        {
            OpenKeyboard("What would you like to share?", SpatialKeyboard.Kind.Text, _draft.Caption, v =>
            {
                CloseKeyboard();
                _draft.Caption = v.Trim();
                if (_draft.Caption.Length == 0) { ShowShare(); return; }
                ChooseRecipients();
            }, ShowShare, null, 500);
        }

        // ------------------------------------------------------------ voice

        void RecordVoice()
        {
            if (!MicPermission.Granted)
            {
                MicPermission.Request(granted =>
                {
                    if (granted) RecordVoice();
                    else
                    {
                        var d = NewPanel("share:mic-denied");
                        d.Title("The microphone is off for SameSky");
                        d.Body("You can allow it in the headset's settings (Apps → Permissions), or say it in words instead.");
                        d.Row(("mic:text", "Type it instead", () => { _draft.Kind = "text"; ComposeText(); }, PressStyle.Primary), ("mic:back", "Back", ShowShare, PressStyle.Quiet));
                        FinishPanel(d);
                        TestProbe.Event("mic", "denied");
                    }
                });
                return;
            }
            if (Microphone.devices.Length == 0)
            {
                var d = NewPanel("share:no-mic");
                d.Title("No microphone found");
                d.Body("This device has no microphone available to SameSky right now.");
                d.Row(("mic:text", "Type it instead", () => { _draft.Kind = "text"; ComposeText(); }, PressStyle.Primary), ("mic:back", "Back", ShowShare, PressStyle.Quiet));
                FinishPanel(d);
                return;
            }
            var l = NewPanel("share:record");
            l.Title("Recording…");
            l.Body("Speak naturally. Press Stop when you're done (30 seconds at most).");
            l.Row(("record:cancel", "Cancel", () => { Microphone.End(null); ShowShare(); }, PressStyle.Quiet), ("record:stop", "Stop", StopRecording, PressStyle.Primary));
            FinishPanel(l);
            _recording = Microphone.Start(null, false, 30, 24000);
            _recordStarted = Time.unscaledTime;
            TestProbe.Event("mic", "recording");
        }

        void StopRecording()
        {
            var pos = Microphone.GetPosition(null);
            Microphone.End(null);
            if (_recording == null || pos <= 0) { Toast("Nothing was recorded. Try again."); ShowShare(); return; }
            var samples = new float[pos * _recording.channels];
            _recording.GetData(samples, 0);
            _draft.Seconds = pos / (float)_recording.frequency;
            _draft.Wav = Wav.Encode(samples, _recording.channels, _recording.frequency);
            TestProbe.Event("mic", $"recorded:{_draft.Seconds:F1}s");
            var l = NewPanel("share:voice-review");
            l.Title($"Voice note · {_draft.Seconds:F0} s");
            l.Row(("voice:listen", "Listen", () =>
            {
                var clip = AudioClip.Create("preview", samples.Length / _recording.channels, _recording.channels, _recording.frequency, false);
                clip.SetData(samples, 0);
                AudioSource.PlayClipAtPoint(clip, HeadPosition + Vector3.forward * 0.4f, Prefs.Current.volume);
            }, PressStyle.Secondary), ("voice:again", "Record again", RecordVoice, PressStyle.Secondary));
            l.Muted("Add the words so it has captions? (optional; shown only with your permission)");
            l.Button("voice:words", _draft.Transcript == null ? "Add the words" : "Edit the words", () => OpenKeyboard("What you said (for captions)", SpatialKeyboard.Kind.Text, _draft.Transcript ?? "", v =>
            {
                CloseKeyboard();
                _draft.Transcript = v.Trim().Length > 0 ? v.Trim() : null;
                StopRecordingReview();
            }, StopRecordingReview, null, 2000), PressStyle.Quiet);
            l.Button("voice:next", "Next: who sees it", ChooseRecipients, PressStyle.Primary);
            FinishPanel(l);
        }

        void StopRecordingReview() => ChooseRecipients();

        // ------------------------------------------------------------ something from the phone

        void ChooseExisting()
        {
            var l = NewPanel("share:existing");
            l.Title("From your phone or camera");
            l.Muted("Loading what you've uploaded…");
            FinishPanel(l);
            Run(async ct =>
            {
                var mine = (await Data.Moments(CurrentRoom.Id, ct)).Where(m => m.IsMine && m.SharingState == "private" && m.Media?.ProcessingState == "ready").Take(4).ToList();
                var d = NewPanel("share:existing");
                d.Title("From your phone or camera");
                if (mine.Count == 0)
                {
                    d.Body("Nothing uploaded and unshared yet. In the companion app, choose Share → add a photo or clip; it appears here privately until you share it.");
                    d.Row(("existing:back", "Back", ShowShare, PressStyle.Quiet), ("existing:refresh", "Check again", ChooseExisting, PressStyle.Secondary));
                    FinishPanel(d);
                    return;
                }
                foreach (var m in mine)
                    d.Button("existing:" + m.Id, $"{(m.Kind == "clip" ? "Clip" : "Photo")} · {SourceLabel(m.Media)} · {m.CaptureLocal?.Time} · {Trim(m.Caption, 28)}", () =>
                    {
                        _draft.Existing = m;
                        _draft.Caption = m.Caption;
                        ChooseRecipients();
                    }, PressStyle.Secondary);
                d.Muted("Flat photos and clips stay flat on the room's screen; nothing is turned into 3D.");
                d.Row(("existing:back", "Back", ShowShare, PressStyle.Quiet));
                FinishPanel(d);
            }, "Couldn't load your uploads", ChooseExisting);
        }

        static string Trim(string s, int n) => string.IsNullOrEmpty(s) ? "(no caption)" : s.Length <= n ? s : s.Substring(0, n - 1) + "…";

        // ------------------------------------------------------------ audience, AI consent, preview

        void ChooseRecipients()
        {
            var what = _draft.Kind switch
            {
                "voice" => $"<b>Voice note</b> · {_draft.Seconds:F0} s{(_draft.Transcript != null ? $"\n“{Trim(_draft.Transcript, 70)}”" : "")}",
                "existing" => $"<b>{(_draft.Existing.Kind == "clip" ? "Clip" : "Photo")}</b> from your {SourceLabel(_draft.Existing.Media).ToLowerInvariant()}\n{Trim(_draft.Caption, 70)}",
                _ => $"“{Trim(_draft.Caption, 110)}”",
            };
            AudienceScreen("share:audience", _draft.Existing, _draft.Kind == "voice" ? Icons.Mic : Icons.TextSize, what, CurrentRoom, new[] { CurrentRoom },
                _draft.Recipients, _draft.AiAllowed, null, on => _draft.AiAllowed = on, ShowShare,
                () => Run(Publish, "Sharing didn't finish", () => Run(Publish, "Sharing didn't finish")));
        }

        /// <summary>The audience screen (visual reference 06), four things kept apart: what is being shared; who
        /// receives it (one person or a circle, each recipient named); separately, whether the daily story's AI may
        /// use it; and one final button that repeats who will receive it.</summary>
        void AudienceScreen(string screen, Moment preview, string previewIcon, string previewText, Room room, IReadOnlyList<Room> selectable,
            HashSet<string> recipients, bool ai, Action<Room> pickRoom, Action<bool> setAi, Action back, Action confirm, string tag = null)
        {
            var l = NewPanel(screen, 0.5f);
            l.Title("Who sees it?");
            if (tag != null) l.Tag(tag);

            var strip = l.Block(0.075f);
            Thumb(strip, new Vector3(-l.InnerWidth / 2 + 0.06f, 0, 0), new Vector2(0.12f, 0.075f), preview, previewIcon);
            var whatText = Ui.Text(strip, "What", previewText, Theme.Body * 0.95f, Theme.Ink, l.InnerWidth - 0.135f, TMPro.TextAlignmentOptions.Left,
                new Vector3(0.0675f, 0, 0), height: 0.072f);
            whatText.overflowMode = TMPro.TextOverflowModes.Ellipsis;

            // one person or a circle
            if (selectable.Count > 1 && pickRoom != null)
            {
                var kinds = l.Row(selectable.Select(r => ("audience:room:" + r.Id, Icons.With(r.IsDirect ? Icons.Heart : Icons.Users, r.IsDirect ? "One person" : r.Name),
                    (Action)(() => pickRoom(r)), PressStyle.Chip)).ToArray());
                for (var i = 0; i < kinds.Count; i++) kinds[i].SetToggled(selectable[i].Id == room.Id);
            }
            else l.Chip(room.IsDirect ? $"Just the two of you · {room.Name}" : $"Sharing in {room.Name}");

            // each recipient, named
            var others = room.Members.Where(m => m.Id != Data.Me.Id).Take(4).ToList();
            Pressable final = null;
            string FinalLabel() => recipients.Count == 0 ? "Choose who sees it"
                : Icons.With(Icons.Send, "Share with " + Names(others.Where(o => recipients.Contains(o.Id)).Select(o => o.DisplayName).ToList()));
            var people = l.Row(others.Select(o => ("audience:" + o.Id, o.DisplayName + (string.IsNullOrEmpty(o.Label) ? "" : $" · {o.Label}"), (Action)null, PressStyle.Chip)).ToArray());
            for (var i = 0; i < people.Count; i++)
            {
                var o = others[i];
                var chip = people[i];
                chip.SetToggled(recipients.Contains(o.Id));
                chip.Pressed += () =>
                {
                    if (room.IsDirect) return;   // a one-to-one room has one possible recipient
                    if (!recipients.Remove(o.Id)) recipients.Add(o.Id);
                    chip.SetToggled(recipients.Contains(o.Id));
                    final.SetText(FinalLabel());
                    final.Interactable = recipients.Count > 0;
                };
            }

            // separately: the daily story's AI
            static string AiLabel(bool on) => Icons.With(Icons.Sparkles, on ? "The daily story's AI may use this: on" : "The daily story's AI may use this: off");
            var aiChip = l.Button("share:ai", AiLabel(ai), null, PressStyle.Chip);
            aiChip.SetToggled(ai);
            aiChip.Pressed += () =>
            {
                ai = !ai;
                setAi?.Invoke(ai);
                aiChip.SetToggled(ai);
                aiChip.SetText(AiLabel(ai));
            };
            l.Muted("A separate choice from who sees it. Off: a story can still show it in a plain, in-order recap. Nobody who joins later sees it.");

            final = l.Button("audience:share", FinalLabel(), confirm, PressStyle.Primary);
            final.Interactable = recipients.Count > 0;
            FinishPanel(l, ("audience:back", "Back", back, PressStyle.Dark));
        }

        // ------------------------------------------------------------ the sample's share (nothing is created or sent)

        Room _sampleShareRoom;
        readonly HashSet<string> _sampleRecipients = new();
        bool _sampleAi;

        void SampleShare() => Run(async ct =>
        {
            var rooms = Data.Rooms.OrderBy(r => r.IsDirect ? 0 : 1).ToList();
            void Everyone()
            {
                _sampleRecipients.Clear();
                foreach (var m in _sampleShareRoom.Members) if (m.Id != Data.Me.Id) _sampleRecipients.Add(m.Id);
            }
            if (_sampleShareRoom == null || rooms.All(r => r.Id != _sampleShareRoom.Id))
            {
                _sampleShareRoom = rooms.FirstOrDefault(r => r.Id == CurrentRoom.Id) ?? rooms[0];
                Everyone();
            }
            // one of the sample's own photos stands in for what you'd bring from your phone
            var all = new List<Moment>();
            foreach (var r in rooms) all.AddRange(await Data.Moments(r.Id, ct));
            var photo = all.FirstOrDefault(m => m.IsMine && m.RoomId == _sampleShareRoom.Id && m.Media?.Kind == "image")
                        ?? all.FirstOrDefault(m => m.IsMine && m.Media?.Kind == "image") ?? all.FirstOrDefault(m => m.Media?.Kind == "image");
            var what = photo == null ? "<b>A photo from your phone</b>"
                : $"<b>{Trim(photo.Caption, 60)}</b>\n{SourceLabel(photo.Media)} photo{(OriginLabel(photo.Media) is { } origin ? " · " + origin : "")}";
            var room = _sampleShareRoom;
            AudienceScreen("share", photo, Icons.Image, what, room, rooms, _sampleRecipients, _sampleAi,
                picked => { _sampleShareRoom = picked; Everyone(); SampleShare(); },
                on => _sampleAi = on, EnterRoom,
                () =>
                {
                    var names = Names(room.Members.Where(m => _sampleRecipients.Contains(m.Id)).Select(m => m.DisplayName).ToList());
                    TestProbe.Event("share", "sample-demo");
                    var done = NewPanel("share:done");
                    done.Title("That's how sharing works");
                    done.Tag("Sample: nothing was sent");
                    done.Body($"In your own room this would now reach {names} and nobody else. The daily story's AI {(_sampleAi ? "could" : "would not")} use it. You could withdraw it at any time.");
                    done.Row(("share:account", "Create an account", ShowSignUp, PressStyle.Secondary), ("share:room", "Back to the room", EnterRoom, PressStyle.Primary));
                    FinishPanel(done);
                }, "Sample: nothing is sent anywhere");
        }, "Couldn't open sharing", EnterRoom);

        async Task Publish(CancellationToken ct)
        {
            var l = NewPanel("share:publishing");
            l.Title("Sharing…");
            var status = l.Body("");
            FinishPanel(l);
            var d = _draft;
            if (d.Created == null)
            {
                switch (d.Kind)
                {
                    case "text":
                        d.Created = await Api.CreateMoment(CurrentRoom.Id, "text", d.Caption, null, DateTimeOffset.Now, Data.Me.TimeZone, d.AiAllowed, ct: ct);
                        break;
                    case "existing":
                        d.Created = d.Existing;
                        break;
                    case "voice":
                        if (d.MediaId == null)
                        {
                            status.text = "Uploading your voice note…";
                            var media = await Api.UploadMedia(d.Wav, "voice.wav", "audio/wav", "audio", "headset", DateTimeOffset.Now, ct);
                            d.MediaId = media.Id;
                        }
                        status.text = "Preparing it for playback…";
                        await WaitForMedia(d.MediaId, ct);
                        d.Created = await Api.CreateMoment(CurrentRoom.Id, "voice", d.Caption, d.MediaId, DateTimeOffset.Now, Data.Me.TimeZone, d.AiAllowed,
                            d.Transcript, d.Transcript != null, ct);
                        break;
                }
            }
            status.text = "Confirming who sees it…";
            var resolved = await Api.ResolveAudience(d.Created.Id, d.Recipients, ct);
            var shared = await Api.Share(d.Created.Id, d.Recipients, resolved.ConfirmationToken, ct);
            TestProbe.Event("share", d.Kind);
            _draft = null;
            var done = NewPanel("share:done");
            done.Title("Shared");
            done.Body(resolved.Label ?? "Shared.");
            done.Row(("share:withdraw", "Withdraw it", () => Run(async ct2 =>
            {
                await Api.Withdraw(shared.Id, ct2);
                TestProbe.Event("withdraw", shared.Id);
                Toast("Withdrawn. It's gone from their room and from any story.");
                EnterRoom();
            }, "Couldn't withdraw"), PressStyle.Quiet), ("share:room", "Back to the room", EnterRoom, PressStyle.Primary));
            FinishPanel(done);
        }

        async Task WaitForMedia(string mediaId, CancellationToken ct)
        {
            var started = Time.unscaledTime;
            while (true)
            {
                var m = await Api.Media(mediaId, ct);
                if (m.ProcessingState == "ready") return;
                if (m.ProcessingState == "failed") throw new ApiException(422, "media_failed", m.SafeError ?? "This recording couldn't be processed. Try recording again.");
                if (Time.unscaledTime - started > 90f) throw new ApiException(0, "processing_slow", "Processing is taking longer than usual (is the worker running?). Try again in a moment.", true);
                await Task.Delay(1500, ct);
            }
        }
    }

    /// <summary>Microphone permission: Android/Quest asks at runtime; desktop runtimes (Editor, simulator) don't.</summary>
    public static class MicPermission
    {
        public static bool Granted
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
#else
                return true;
#endif
            }
        }

        public static void Request(Action<bool> done)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted += _ => done(true);
            callbacks.PermissionDenied += _ => done(false);
            callbacks.PermissionDeniedAndDontAskAgain += _ => done(false);
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone, callbacks);
#else
            done(true);
#endif
        }
    }

    /// <summary>16-bit PCM WAV, accepted by the API's voice pipeline (which makes AAC and Ogg copies).</summary>
    public static class Wav
    {
        public static byte[] Encode(float[] samples, int channels, int rate)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            var dataLen = samples.Length * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataLen);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)channels);
            w.Write(rate);
            w.Write(rate * channels * 2);
            w.Write((short)(channels * 2));
            w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(dataLen);
            foreach (var s in samples) w.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
            return ms.ToArray();
        }
    }
}
