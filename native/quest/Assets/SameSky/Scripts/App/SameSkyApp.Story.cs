// "Our day": the approved daily story, played on the room's screen chapter by chapter.
// Every line is linked to the moments it came from; photos, clips and voice play from their sources, with
// captions; pause, skip, replay; react and reply. A generated recap is never shown to the room before each
// contributor approves it (pending ones are offered for review first). The generator is always labelled:
// AI-drafted, chronological (no AI) or precomputed sample.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SameSky.Api;
using SameSky.Interaction;
using SameSky.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Video;

namespace SameSky.App
{
    public sealed partial class SameSkyApp
    {
        Recap _story;
        int _chapter, _item;
        float _itemUntil;
        bool _storyPaused;
        Transform _screenRoot;
        Renderer _screenImage;
        TextMeshPro _screenCaption, _screenMeta;
        GameObject _screenShade;
        VideoPlayer _video;
        RenderTexture _videoTexture;
        AudioSource _storyAudio;
        Layout _storyPanel;
        TextMeshPro _storyLine;
        Renderer _storyProgress;
        float _storyProgressWidth;
        int _pictureChapter = -1;   // the chapter whose picture the screen is showing (-1: none)

        public bool StoryPlaying => _story != null && !_storyPaused;
        public string StoryState => _story == null ? "none" : $"{(_storyPaused ? "paused" : "playing")}:{_chapter}:{_item}";

        public void ShowOurDay()
        {
            StopStory();
            var l = NewPanel("day");
            l.Title("Our day");
            l.Muted("Loading the latest story…");
            FinishPanel(l);
            Run(async ct =>
            {
                var pending = await Data.PendingRecaps(CurrentRoom.Id, ct);
                var recaps = await Data.Recaps(CurrentRoom.Id, ct);
                var mine = pending.FirstOrDefault(r => r.Contributors.Any(c => c.UserId == Data.Me.Id && !c.Approved));
                var shared = recaps.Where(r => r.State == "shared").OrderByDescending(r => r.RoomDate).FirstOrDefault();
                var d = NewPanel("day");
                d.Title("Our day");
                if (mine != null)
                {
                    d.Body($"A story of {mine.RoomDate} is waiting for your approval before anyone sees it.");
                    d.Tag(mine.GeneratorLabel);
                    d.Button("day:review", "Review and approve", () => ReviewRecap(mine), PressStyle.Primary);
                }
                if (shared != null)
                {
                    // the day side by side (visual reference 05): whose room it belongs to, one picture per chapter with
                    // whose it is and what it says, how long it runs, and what it was made from
                    var chapters = shared.Plan?.Chapters ?? new List<RecapChapter>();
                    d.Chip(CurrentRoom.IsDirect ? $"Just the two of you · {CurrentRoom.Name}" : $"Shared in {CurrentRoom.Name}");
                    var shown = chapters.Take(2).ToList();
                    if (shown.Count > 0)
                    {
                        const float th = 0.105f, gap = 0.012f;
                        var strip = d.Block(th + 0.034f);
                        var w = (d.InnerWidth - gap * (shown.Count - 1)) / shown.Count;
                        for (var i = 0; i < shown.Count; i++)
                        {
                            var ch = shown[i];
                            var x = -d.InnerWidth / 2 + w / 2 + i * (w + gap);
                            var moments = ch.PlaybackMomentIds.Select(id => shared.Playback.FirstOrDefault(m => m.Id == id)).Where(m => m != null).ToList();
                            var visual = moments.FirstOrDefault(m => m.Media != null && m.Media.Kind != "audio") ?? moments.FirstOrDefault();
                            Thumb(strip, new Vector3(x, 0.017f, 0), new Vector2(w, th), visual);
                            var label = Ui.Text(strip, "Label", $"<b>{NameOf(ch.ContributorId)}</b> · {Trim(ch.Segments.FirstOrDefault()?.Text, 60)}", Theme.Small, Theme.Ink, w,
                                TextAlignmentOptions.TopLeft, new Vector3(x, -0.0535f, 0), height: 0.03f);
                            label.overflowMode = TextOverflowModes.Ellipsis;
                        }
                    }
                    var seconds = chapters.Sum(c => c.DurationSeconds);
                    d.Body($"<b>{shared.Plan?.Title ?? $"The story of {shared.RoomDate}"}</b>");
                    d.Muted($"{chapters.Count} chapters · {Clock(seconds)} · from {Names(shared.Contributors.Select(c => c.DisplayName).ToList())} · made from {SourceCounts(shared.Playback)}");
                    d.Tag(shared.GeneratorLabel);
                    d.Button("day:play", Icons.With(Icons.Play, $"Play our day · {Clock(seconds)}"), () => PlayStory(shared), mine == null ? PressStyle.Primary : PressStyle.Secondary);
                }
                if (mine == null && shared == null)
                {
                    d.Body(CurrentRoom.Members.Count < 2 ? "Stories start once someone else joins and you both share moments." : "No story for today yet. When you each share a moment, a story is drafted for you both to approve.");
                    if (!Data.IsSample && CurrentRoom.Members.Count > 1)
                        d.Button("day:request", "Draft today's story now", () => Run(async ct2 =>
                        {
                            await Api.RequestRecap(CurrentRoom.Id, DateTime.Now.ToString("yyyy-MM-dd"), ct2);
                            Toast("Drafting. It appears here for approval when ready.");
                            ShowOurDay();
                        }, "Couldn't draft the story"), PressStyle.Secondary);
                }
                FinishPanel(d, ("day:back", "Back", EnterRoom, PressStyle.Dark));
            }, "Couldn't load Our day", ShowOurDay);
        }

        /// <summary>"2 photos, a clip and a voice note": what a story was made from.</summary>
        static string SourceCounts(IEnumerable<Moment> moments)
        {
            var parts = new List<string>();
            void Add(string kind, string one, string many)
            {
                var n = moments.Count(m => m.Kind == kind);
                if (n == 1) parts.Add(one);
                else if (n > 1) parts.Add($"{n} {many}");
            }
            Add("photo", "a photo", "photos");
            Add("clip", "a clip", "clips");
            Add("voice", "a voice note", "voice notes");
            Add("text", "a note", "notes");
            return parts.Count == 0 ? "what you both shared" : Names(parts);
        }

        /// <summary>Private preview for a contributor: the draft's lines with their sources, then approve.</summary>
        void ReviewRecap(Recap r)
        {
            var l = NewPanel("review", 0.52f);
            l.Title(r.Plan?.Title ?? "Draft story");
            l.Tag(r.GeneratorLabel);
            l.Muted("Only contributors see this until everyone approves. Each line links to the moment it came from.");
            foreach (var ch in r.Plan?.Chapters ?? new List<RecapChapter>())
                foreach (var s in ch.Segments.Take(3))
                    l.Body((s.Kind == "visual_interpretation" ? "(looks like) " : "") + s.Text);
            l.Muted("Approving publishes it to: " + string.Join(", ", (r.Audience ?? new List<NameRef>()).Select(a => a.DisplayName)));
            l.Row(("review:back", "Not now", ShowOurDay, PressStyle.Quiet), ("review:approve", "Approve", () => Run(async ct =>
            {
                var updated = await Data.Approve(r.Id, ct);
                TestProbe.Event("recap", "approved");
                Toast(updated?.State == "shared" ? "Published to the room." : "Approved. It publishes when everyone has approved.");
                ShowOurDay();
            }, "Approval failed"), PressStyle.Primary));
            FinishPanel(l);
        }

        // ------------------------------------------------------------ playback on the screen

        void EnsureScreen()
        {
            if (_screenRoot != null) return;
            var anchor = Anchor("Screen");
            _screenRoot = new GameObject("Story screen").transform;
            _screenRoot.SetParent(_world, false);
            _screenRoot.position = anchor != null ? anchor.position : new Vector3(-0.55f, 1.25f, 1.05f);
            _screenRoot.rotation = anchor != null ? anchor.rotation : Quaternion.LookRotation(new Vector3(0.45f, 0, 1f));
            Ui.Panel(_screenRoot, "Bezel", new Vector2(0.68f, 0.44f), new Color(0.07f, 0.07f, 0.08f, 1f), 0.02f, new Vector3(0, 0, 0.004f), new Color(0, 0, 0, 1), order: Ui.OrderBackground);
            _screenImage = Ui.Image(_screenRoot, "Picture", new Vector2(0.64f, 0.36f), null, 0.008f, new Vector3(0, 0.02f, 0.001f));
            // read from the seat, about 1.4 m away: larger type than a panel at arm's length
            _screenMeta = Ui.Text(_screenRoot, "Meta", "", Theme.Small * 1.55f, Theme.Label, 0.62f, TextAlignmentOptions.TopLeft, new Vector3(0, -0.172f, 0));
            _screenMeta.textWrappingMode = TextWrappingModes.NoWrap;
            _screenMeta.overflowMode = TextOverflowModes.Ellipsis;
            _screenCaption = Ui.Text(_screenRoot, "Caption", "", Theme.Body * 1.5f, Theme.OnDark, 0.6f, TextAlignmentOptions.Bottom, new Vector3(0, -0.11f, -0.002f), height: 0.08f);
            // The caption lies over the foot of the picture: a shade behind it, so white type reads on a bright
            // photograph as well as on a dark one. Shown only while there is a caption (UpdateStory).
            _screenShade = Ui.Panel(_screenRoot, "Caption shade", new Vector2(0.64f, 0.078f), new Color(0.03f, 0.03f, 0.04f, 0.74f), 0.008f,
                new Vector3(0, -0.121f, 0.0004f), new Color(0, 0, 0, 0), 0f, Ui.OrderFill).gameObject;
            _screenShade.SetActive(false);
            _video = _screenRoot.gameObject.AddComponent<VideoPlayer>();
            _video.playOnAwake = false;
            _video.renderMode = VideoRenderMode.RenderTexture;
            _video.audioOutputMode = VideoAudioOutputMode.AudioSource;
            _storyAudio = _screenRoot.gameObject.AddComponent<AudioSource>();
            _storyAudio.spatialBlend = 1f;
            _storyAudio.minDistance = 0.8f;
            _video.SetTargetAudioSource(0, _storyAudio);
        }

        void PlayStory(Recap r)
        {
            EnsureScreen();
            _story = r;
            _chapter = 0;
            _item = -1;
            _storyPaused = false;
            TestProbe.Event("story", "play");
            ShowStoryControls();
            NextItem();
        }

        void ShowStoryControls()
        {
            var l = NewPanel("story", 0.5f);
            _storyPanel = l;
            l.Title(_story.Plan?.Title ?? "Our day");
            l.Tag(_story.GeneratorLabel);
            // The line changes with each item (UpdateStoryText): room is kept for the chapter line and two lines of
            // text, and a longer one ends in an ellipsis instead of running over the progress bar and the buttons.
            // The room is measured with words, because lines of spaces alone have no height to measure.
            _storyLine = l.Body("Chapter\nfirst line\nsecond line");
            _storyLine.rectTransform.sizeDelta += new Vector2(0, Theme.Body * Theme.TextScale * 0.25f);
            _storyLine.overflowMode = TextOverflowModes.Ellipsis;
            _storyLine.text = "";
            var bar = l.Progress(0f);
            _storyProgress = bar.fill;
            _storyProgressWidth = l.Width - Layout.Pad * 2;
            var controls = l.Row(
                ("story:replay", "Replay", () => { _item = -1; NextItem(); }, PressStyle.Secondary),
                ("story:pause", _storyPaused ? "Resume" : "Pause", () => { PauseStory(!_storyPaused); ShowStoryControls(); }, PressStyle.Primary),
                ("story:skip", "Next", NextChapter, PressStyle.Secondary));
            l.Row(("story:heart", "Love", () => ReactToCurrent("heart"), PressStyle.Chip),
                  ("story:hug", "Hug", () => ReactToCurrent("hug"), PressStyle.Chip),
                  ("story:reply", "Reply", ReplyToCurrent, PressStyle.Secondary),
                  ("story:stop", "Done", () => { StopStory(); EnterRoom(); }, PressStyle.Quiet));
            FinishPanel(l);
            UpdateStoryText();
        }

        Moment CurrentStoryMoment()
        {
            var ch = CurrentChapter();
            if (ch == null || _item < 0 || _item >= ch.PlaybackMomentIds.Count) return null;
            return _story.Playback.FirstOrDefault(m => m.Id == ch.PlaybackMomentIds[_item]);
        }

        RecapChapter CurrentChapter() => _story?.Plan?.Chapters != null && _chapter < _story.Plan.Chapters.Count ? _story.Plan.Chapters[_chapter] : null;

        void NextItem()
        {
            var ch = CurrentChapter();
            if (ch == null) { FinishStory(); return; }
            _item++;
            if (_item >= Math.Max(1, ch.PlaybackMomentIds.Count)) { NextChapter(); return; }
            ShowStoryItem();
        }

        void NextChapter()
        {
            _chapter++;
            _item = -1;
            if (CurrentChapter() == null) { FinishStory(); return; }
            TestProbe.Event("story", "chapter:" + _chapter);
            NextItem();
        }

        void ShowStoryItem()
        {
            var ch = CurrentChapter();
            var m = CurrentStoryMoment();
            _video.Stop();
            _storyAudio.Stop();
            var duration = Mathf.Max(5f, ch.DurationSeconds / Mathf.Max(1, ch.PlaybackMomentIds.Count));
            if (m?.Media != null)
            {
                _screenMeta.text = $"{m.OwnerName} · {m.CaptureLocal?.Time} {m.CaptureLocal?.Abbreviation} · {SourceLabel(m.Media)}{(OriginLabel(m.Media) is { } o ? " · " + o : "")}";
                var ct = ScreenToken;
                if (m.Media.Kind == "video") { _pictureChapter = _chapter; _ = PlayVideo(m, ct); }
                else if (m.Media.Kind == "audio") _ = PlayStoryVoice(m, ct);
                else { _pictureChapter = _chapter; _ = ShowStill(m, ct); }
                if (m.Media.DurationMs is > 0) duration = Mathf.Max(duration, m.Media.DurationMs.Value / 1000f + 0.5f);
            }
            else
            {
                _screenMeta.text = ch.ContributorId != null ? $"From {NameOf(ch.ContributorId)}" : "";
                Ui.SetImage(_screenImage, Ui.Blank);
                _pictureChapter = -1;
            }
            _itemUntil = Time.unscaledTime + duration;
            UpdateStoryText();
            TestProbe.Event("story", $"item:{_chapter}:{_item}");
        }

        async Task ShowStill(Moment m, CancellationToken ct)
        {
            try { Ui.SetImage(_screenImage, await Data.Media.Image(m.Media, false, ct)); }
            catch (Exception e) when (e is not OperationCanceledException) { _screenCaption.text = "This photo couldn't load."; }
        }

        async Task PlayVideo(Moment m, CancellationToken ct)
        {
            try
            {
                await Data.Media.Prepare(_video, m.Media, ct);
                if (_videoTexture == null) _videoTexture = new RenderTexture(1280, 720, 0) { name = "Story video" };
                _video.targetTexture = _videoTexture;
                Ui.SetImage(_screenImage, _videoTexture);
                _video.Play();
                TestProbe.AudioPlaying = "video:" + m.Id;
            }
            catch (Exception e) when (e is not OperationCanceledException) { _screenCaption.text = "This clip couldn't play."; }
        }

        async Task PlayStoryVoice(Moment m, CancellationToken ct)
        {
            try
            {
                // A voice note has no picture of its own: it plays over the picture its chapter is showing (the line
                // beside it still describes that picture), and over a plain dark screen when the chapter has none.
                if (_pictureChapter != _chapter) { Ui.SetImage(_screenImage, Ui.Blank); _pictureChapter = -1; }
                var clip = await Data.Media.Audio(m.Media, ct);
                _storyAudio.clip = clip;
                _storyAudio.Play();
                TestProbe.AudioPlaying = "voice:" + m.Id;
            }
            catch (Exception e) when (e is not OperationCanceledException) { _screenCaption.text = "This voice note couldn't play."; }
        }

        void UpdateStory()
        {
            if (_screenShade != null)
            {
                var captioned = _screenCaption != null && !string.IsNullOrEmpty(_screenCaption.text);
                if (_screenShade.activeSelf != captioned) _screenShade.SetActive(captioned);
            }
            if (_story == null || _storyPaused || Paused) return;
            var m = CurrentStoryMoment();
            if (m?.Media?.Kind == "audio" && _storyAudio != null && _storyAudio.isPlaying)
            {
                var line = CaptionAt(m, (int)(_storyAudio.time * 1000));
                _screenCaption.text = Prefs.Current.captionsOn ? line : "";
                TestProbe.Captions = Prefs.Current.captionsOn ? line : "";
            }
            if (Time.unscaledTime >= _itemUntil && (_video == null || !_video.isPlaying) && (_storyAudio == null || !_storyAudio.isPlaying)) NextItem();
            if (_storyProgress != null && _story.Plan != null)
            {
                var total = Mathf.Max(1, _story.Plan.Chapters.Count);
                var frac = (_chapter + Mathf.Clamp01(_item + 1f) / Mathf.Max(1, CurrentChapter()?.PlaybackMomentIds.Count ?? 1)) / total;
                var w = Mathf.Max(0.006f, _storyProgressWidth * Mathf.Clamp01(frac));
                _storyProgress.transform.localScale = new Vector3(w, 0.006f, 1);
                _storyProgress.transform.localPosition = new Vector3(-_storyProgressWidth / 2 + w / 2, _storyProgress.transform.localPosition.y, _storyProgress.transform.localPosition.z);
            }
        }

        void UpdateStoryText()
        {
            if (_storyLine == null) return;
            var ch = CurrentChapter();
            if (ch == null) { _storyLine.text = _story?.Plan?.ClosingLine ?? ""; return; }
            var seg = ch.Segments.Count > 0 ? ch.Segments[Mathf.Clamp(_item, 0, ch.Segments.Count - 1)] : null;
            var prefix = seg?.Kind == "visual_interpretation" ? "<i>(looks like)</i> " : "";
            _storyLine.text = $"<b>Chapter {_chapter + 1} of {_story.Plan.Chapters.Count} · {NameOf(ch.ContributorId)}</b>\n{prefix}{seg?.Text}";
            if (_screenCaption != null && CurrentStoryMoment()?.Media?.Kind != "audio") _screenCaption.text = Prefs.Current.captionsOn ? seg?.Text ?? "" : "";
        }

        public void PauseStory(bool paused)
        {
            if (_story == null) return;
            _storyPaused = paused;
            if (paused) { _video?.Pause(); _storyAudio?.Pause(); }
            else
            {
                if (_video != null && _video.isPrepared) _video.Play();
                if (_storyAudio != null && _storyAudio.clip != null) _storyAudio.UnPause();
                _itemUntil = Mathf.Max(_itemUntil, Time.unscaledTime + 2f);
            }
            TestProbe.Event("story", paused ? "paused" : "resumed");
        }

        void FinishStory()
        {
            TestProbe.Event("story", "finished");
            if (Data.IsSample) { SampleState.Current.storyFinished = true; SampleState.Save(); }
            var closing = _story?.Plan?.ClosingLine;
            StopStory();
            var l = NewPanel("story-done");
            l.Title("That's our day");
            if (!string.IsNullOrEmpty(closing)) l.Body(closing);
            l.Row(("storydone:replay", "Watch again", ShowOurDay, PressStyle.Secondary), ("storydone:room", "Back to the room", EnterRoom, PressStyle.Primary));
            FinishPanel(l);
        }

        string _screenRoomId;

        /// <summary>The screen belongs to one room: leaving for another (or for the launch screen) takes it down with
        /// whatever it last showed, so nothing from one room is on view in the next.</summary>
        void ClearScreenFor(string roomId)
        {
            if (_screenRoomId == roomId) return;
            _screenRoomId = roomId;
            StopStory();
            if (_screenRoot != null) Destroy(_screenRoot.gameObject);
            _screenRoot = null;
            _screenImage = null;
            _screenMeta = null;
            _screenCaption = null;
            _screenShade = null;
            _video = null;
            _storyAudio = null;
        }

        void StopStory()
        {
            _story = null;
            _storyLine = null;
            _storyProgress = null;
            _pictureChapter = -1;
            if (_video != null) _video.Stop();
            if (_storyAudio != null) _storyAudio.Stop();
            if (_screenCaption != null) _screenCaption.text = "";
            if (TestProbe.AudioPlaying.StartsWith("video:")) TestProbe.AudioPlaying = "";
        }

        void ReactToCurrent(string kind)
        {
            var m = CurrentStoryMoment() ?? _story?.Playback.FirstOrDefault();
            if (m != null) React(m, kind);
        }

        void ReplyToCurrent()
        {
            var m = CurrentStoryMoment() ?? _story?.Playback.FirstOrDefault();
            if (m == null) return;
            PauseStory(true);
            StartReplyTo(m, () => { PauseStory(false); ShowStoryControls(); });
        }

        /// <summary>Plays a single moment on the screen (from a frame or the moment panel).</summary>
        void PlayMomentOnScreen(Moment m)
        {
            EnsureScreen();
            StopStory();
            var single = new Recap
            {
                Id = "single", GeneratorLabel = "Single moment", State = "shared",
                Plan = new RecapPlan { Title = m.Caption, Chapters = new List<RecapChapter> { new() { ContributorId = m.OwnerId, PlaybackMomentIds = new List<string> { m.Id }, DurationSeconds = 8, Segments = new List<RecapSegment> { new() { Text = m.Caption, Kind = "stated" } } } } },
                Playback = new List<Moment> { m },
            };
            PlayStory(single);
        }

        string NameOf(string userId) =>
            CurrentRoom?.Members.FirstOrDefault(x => x.Id == userId)?.DisplayName ?? (Data is SampleData s ? s.Name(userId) : "Someone");
    }
}
