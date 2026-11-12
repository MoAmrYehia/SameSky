// Keepsakes and inbox, Settings, the pause menu and avatar approval in the headset.

using System;
using System.Linq;
using SameSky.Api;
using SameSky.Interaction;
using SameSky.UI;
using UnityEngine;

namespace SameSky.App
{
    public sealed partial class SameSkyApp
    {
        // ------------------------------------------------------------ keepsakes, past stories, approvals, inbox

        public void ShowKeepsakes()
        {
            var l = NewPanel("keepsakes", 0.5f);
            l.Title("Keepsakes");
            l.Muted("Loading…");
            FinishPanel(l);
            Run(async ct =>
            {
                var awards = await Data.Awards(CurrentRoom.Id, ct);
                var recaps = await Data.Recaps(CurrentRoom.Id, ct);
                var pending = await Data.PendingRecaps(CurrentRoom.Id, ct);
                var inbox = (await Data.Inbox(ct)).Where(i => i.RoomId == CurrentRoom.Id).ToList();
                var d = NewPanel("keepsakes", 0.5f);
                d.Title("Keepsakes");
                var earned = awards.Where(a => a.Earned).ToList();
                if (earned.Count == 0 && awards.Count == 0) d.Body("Keepsakes appear on the shelf as you share days together: a first shared day, a conversation continued, seven days of us.");
                else d.Muted("Small things you've earned together. They stand on the walnut shelf to your left.");
                // one row each (visual reference 09): what it is, when it was earned, or how far along it is
                foreach (var a in awards)
                {
                    const float rh = 0.05f;
                    var row = d.Block(rh);
                    var iw = d.InnerWidth;
                    var mark = a.Keepsake switch { "frame" => Icons.Image, "linked_sculptures" => Icons.Heart, _ => Icons.Sprout };
                    Ui.Panel(row, "Bg", new Vector2(iw, rh), new Color(1, 1, 1, 0.55f), 0.014f, new Vector3(0, 0, 0.0006f), new Color(0, 0, 0, 0.06f), 0.0008f);
                    Ui.Panel(row, "Mark", new Vector2(0.034f, 0.034f), a.Earned ? Theme.Green : Theme.Neutral, 0.017f, new Vector3(-iw / 2 + 0.028f, 0, 0.0004f), order: Ui.OrderFill);
                    Ui.Text(row, "Icon", mark, Theme.Body, a.Earned ? Theme.OnDark : Theme.Muted, 0.034f, TMPro.TextAlignmentOptions.Center, new Vector3(-iw / 2 + 0.028f, 0, 0), height: 0.034f);
                    float left = -iw / 2 + 0.054f, width = iw - 0.054f - 0.012f;
                    Ui.Text(row, "Title", $"<b>{a.Title}</b>", Theme.Body * 0.95f, Theme.Ink, width * 0.62f, TMPro.TextAlignmentOptions.Left,
                        new Vector3(left + width * 0.31f, a.Earned ? 0 : 0.008f, 0), height: 0.022f);
                    if (a.Earned)
                        Ui.Text(row, "State", $"earned {Short(a.EarnedAt)}", Theme.Small, Theme.Green, width * 0.36f, TMPro.TextAlignmentOptions.Right,
                            new Vector3(left + width * 0.82f, 0, 0), TMPro.FontStyles.Bold, 0.02f);
                    else
                    {
                        var unit = a.Milestone == "seven_days" ? " shared days" : "";
                        Ui.Text(row, "State", $"{a.Progress} of {a.Target}{unit}", Theme.Small, Theme.Muted, width * 0.36f, TMPro.TextAlignmentOptions.Right,
                            new Vector3(left + width * 0.82f, 0.008f, 0), height: 0.02f);
                        var done = a.Target > 0 ? Mathf.Clamp01(a.Progress / (float)a.Target) : 0f;
                        var fw = Mathf.Max(0.004f, width * done);
                        Ui.Panel(row, "Track", new Vector2(width, 0.004f), new Color(0, 0, 0, 0.1f), 0.002f, new Vector3(left + width / 2, -0.013f, 0.0004f), new Color(1, 1, 1, 0), 0);
                        Ui.Panel(row, "Fill", new Vector2(fw, 0.004f), Theme.Green, 0.002f, new Vector3(left + fw / 2, -0.013f, 0.0002f), new Color(1, 1, 1, 0), 0, Ui.OrderFill);
                    }
                }
                var past = recaps.Where(r => r.State == "shared").OrderByDescending(r => r.RoomDate).Take(3).ToList();
                if (past.Count > 0)
                {
                    d.Gap(0.004f);
                    d.Body("<b>Past stories</b>");
                    foreach (var r in past) d.Button("keepsakes:story:" + r.Id, $"{r.RoomDate} · {r.Plan?.Title ?? "Our day"}", () => PlayStory(r), PressStyle.Secondary);
                }
                var needsMe = pending.Where(r => r.Contributors.Any(c => c.UserId == Data.Me.Id && !c.Approved)).ToList();
                if (needsMe.Count > 0)
                {
                    d.Body($"<b>Waiting for your approval</b> ({needsMe.Count})");
                    foreach (var r in needsMe) d.Button("keepsakes:approve:" + r.Id, $"Review {r.RoomDate}", () => ReviewRecap(r), PressStyle.Primary);
                }
                d.Gap(0.004f);
                d.Body($"<b>Inbox</b> · {inbox.Count(i => !i.Read)} new");
                if (inbox.Count == 0) d.Muted("Nothing new in this room.");
                foreach (var item in inbox.Take(4))
                    d.Button("inbox:" + item.Id, (item.Read ? "" : "● ") + item.Text, () => Run(async c =>
                    {
                        await Data.MarkRead(item.Id, c);
                        if (item.Kind == "recap_ready") ShowOurDay();
                        else EnterRoom();
                    }, "Couldn't open that"), PressStyle.Quiet);
                FinishPanel(d, ("keepsakes:back", "Back", EnterRoom, PressStyle.Dark));
            }, "Couldn't load keepsakes", ShowKeepsakes);
        }

        // ------------------------------------------------------------ pause

        public void ShowPause()
        {
            SetPaused(true, "menu");
            var l = NewPanel("pause");
            l.Title("Paused");
            l.Body("Sound and stories are paused.");
            var toggles = l.Row(("pause:captions", Prefs.Current.captionsOn ? "Captions: on" : "Captions: off", null, PressStyle.Chip),
                                ("pause:sound", Prefs.Current.soundOn ? "Sound: on" : "Sound: off", null, PressStyle.Chip));
            toggles[0].SetToggled(Prefs.Current.captionsOn);
            toggles[1].SetToggled(Prefs.Current.soundOn);
            toggles[0].Pressed += () => { ToggleCaptions(); toggles[0].SetText(Prefs.Current.captionsOn ? "Captions: on" : "Captions: off"); toggles[0].SetToggled(Prefs.Current.captionsOn); };
            toggles[1].Pressed += () => { ToggleSound(); toggles[1].SetText(Prefs.Current.soundOn ? "Sound: on" : "Sound: off"); toggles[1].SetToggled(Prefs.Current.soundOn); };
            l.Row(("pause:recenter", "Recenter", () => Recenter(), PressStyle.Secondary), ("pause:settings", "Settings", () => { SetPaused(false, "menu"); ShowSettings(); }, PressStyle.Secondary));
            l.Row(("pause:exit", "Leave SameSky", ConfirmExit, PressStyle.Quiet), ("pause:resume", "Resume", () => { SetPaused(false, "menu"); EnterRoom(); }, PressStyle.Primary));
            FinishPanel(l);
        }

        void ToggleCaptions()
        {
            Prefs.Current.captionsOn = !Prefs.Current.captionsOn;
            Prefs.Save();
            TestProbe.Event("captions", Prefs.Current.captionsOn ? "on" : "off");
        }

        void ToggleSound()
        {
            Prefs.Current.soundOn = !Prefs.Current.soundOn;
            Prefs.Save();
            AudioListener.volume = Prefs.Current.soundOn ? Prefs.Current.volume : 0f;
            TestProbe.Event("sound", Prefs.Current.soundOn ? "on" : "off");
        }

        // ------------------------------------------------------------ settings

        public void ShowSettings()
        {
            var l = NewPanel("settings", 0.5f);
            l.Title("Settings");
            static string Hearing() => $"Hearing and reading · volume {Mathf.RoundToInt(Prefs.Current.volume * 100)}%";
            var hearing = l.Muted(Hearing());
            var a = l.Row(("settings:captions", Icons.With(Icons.Captions, Prefs.Current.captionsOn ? "Captions: on" : "Captions: off"), null, PressStyle.Chip),
                          ("settings:sound", Icons.With(Icons.Sound, Prefs.Current.soundOn ? "Sound: on" : "Sound: off"), null, PressStyle.Chip),
                          ("settings:text", Icons.With(Icons.TextSize, $"Text size: {Prefs.Current.textScale:0.##}×"), null, PressStyle.Chip));
            foreach (var chip in a) chip.Label.fontSize *= 0.9f;
            a[0].SetToggled(Prefs.Current.captionsOn);
            a[1].SetToggled(Prefs.Current.soundOn);
            a[0].Pressed += () => { ToggleCaptions(); ShowSettings(); };
            a[1].Pressed += () => { ToggleSound(); ShowSettings(); };
            a[2].Pressed += () =>
            {
                Prefs.Current.textScale = Prefs.Current.textScale >= 1.5f ? 1f : Prefs.Current.textScale + 0.25f;
                Theme.TextScale = Prefs.Current.textScale;
                Prefs.Save();
                ShowSettings();
            };
            void Volume(float step)
            {
                Prefs.Current.volume = Mathf.Clamp01(Prefs.Current.volume + step);
                Prefs.Save();
                AudioListener.volume = Prefs.Current.soundOn ? Prefs.Current.volume : 0;
                hearing.text = Hearing();   // the level is said, not just changed
            }
            l.Row(("settings:volume-down", "Quieter", () => Volume(-0.1f), PressStyle.Secondary),
                  ("settings:volume-up", "Louder", () => Volume(0.1f), PressStyle.Secondary));
            l.Muted("Your seat");
            l.Row(("settings:recenter", Icons.With(Icons.Recenter, "Recenter"), () => Recenter(), PressStyle.Secondary),
                  ("settings:seat-up", "Seat higher", () => { Prefs.Current.seatHeight += 0.05f; Prefs.Save(); Recenter(); }, PressStyle.Secondary),
                  ("settings:seat-down", "Seat lower", () => { Prefs.Current.seatHeight -= 0.05f; Prefs.Save(); Recenter(); }, PressStyle.Secondary));
            if (Data is LiveData)
            {
                l.Gap(0.004f);
                l.Body($"<b>Account</b> · {Data.Me.Email}");
                l.Row(("settings:avatar", "Your avatar", () => ShowAvatarSettings(ShowSettings), PressStyle.Secondary),
                      ("settings:hands", "Your hands", () => ShowHandsSettings(ShowSettings), PressStyle.Secondary),
                      ("settings:times", "Usual times", () => TimesPanel("availability", ShowSettings, null), PressStyle.Secondary));
                l.Body($"<b>Room</b> · {CurrentRoom.Name} ({CurrentRoom.Members.Count} of {CurrentRoom.Capacity})");
                var roomRow = l.Row(("settings:mute", CurrentRoom.Muted ? "Notifications: off" : "Notifications: on", () => Run(async ct =>
                    {
                        CurrentRoom = await Api.UpdateRoom(CurrentRoom.Id, muted: !CurrentRoom.Muted, ct: ct);
                        ShowSettings();
                    }, "Couldn't change notifications"), PressStyle.Chip),
                    ("settings:invite", "Invite", () => { var p = NewPanel("invite"); p.Title("Invite"); InviteBlock(p, CurrentRoom); p.Row(("invite:back", "Back", ShowSettings, PressStyle.Quiet)); FinishPanel(p); }, PressStyle.Secondary),
                    ("settings:leave-room", CurrentRoom.IsOwner ? "Owner" : "Leave room", () => ConfirmLeaveRoom(), PressStyle.Quiet));
                roomRow[0].SetToggled(!CurrentRoom.Muted);
                roomRow[2].Interactable = !CurrentRoom.IsOwner;
                if (Data.Rooms.Count > 1)
                    l.Button("settings:switch-room", "Switch room", () =>
                    {
                        var i = Data.Rooms.FindIndex(r => r.Id == CurrentRoom.Id);
                        CurrentRoom = Data.Rooms[(i + 1) % Data.Rooms.Count];
                        EnterRoom();
                    }, PressStyle.Secondary);
                l.Row(("settings:signout", "Sign out", SignOut, PressStyle.Quiet), ("settings:exit", "Leave SameSky", ConfirmExit, PressStyle.Quiet));
            }
            else
            {
                l.Muted("Sample · fictional people, nothing leaves this device");
                l.Row(("settings:avatar", "Your avatar", () => ShowAvatarSettings(ShowSettings), PressStyle.Secondary),
                      ("settings:hands", "Your hands", () => ShowHandsSettings(ShowSettings), PressStyle.Secondary),
                      ("settings:account", "Use my account", () => { Data = null; ShowLaunch(); }, PressStyle.Secondary));
                l.Row(("settings:reset-sample", "Reset the sample", () => { SampleState.Reset(); _sampleTogether = false; Prefs.Current.sampleIntroDone = false; Prefs.Save(); Toast("The sample is fresh again."); EnterRoom(); }, PressStyle.Quiet),
                      ("settings:exit", Icons.With(Icons.Leave, "Leave SameSky"), ConfirmExit, PressStyle.Quiet));
            }
            // which build this is, in plain words; addresses and the like stay in the logs
            l.Muted($"SameSky {Application.version}{(Debug.isDebugBuild ? " · development build" : "")}");
            FinishPanel(l, ("settings:back", "Back", EnterRoom, PressStyle.Dark));
        }

        // ------------------------------------------------------------ your hands

        /// <summary>The colour the avatar's skin was given, when the avatar says (designed avatars do; photo ones don't).</summary>
        string AvatarSkinHex() => (string)MeAsMember()?.Avatar?.Definition?["skin"];

        /// <summary>Colours the tracked hands: the chosen tone, or the avatar's skin, or a middle tone.</summary>
        void ApplyHandTone()
        {
            var skin = HandSkin.Instance;
            if (skin == null) return;
            var chosen = Prefs.Current.handTone;
            if (chosen >= 0 && chosen < HandSkin.Tones.Length) skin.SetTone(HandSkin.Tones[chosen].Tone);
            else skin.SetTone(HandSkin.TryParseTone(AvatarSkinHex(), out var tone) ? tone : HandSkin.Tones[HandSkin.DefaultTone].Tone);
        }

        void ShowHandsSettings(Action back)
        {
            var l = NewPanel("hands", 0.46f);
            l.Title("Your hands");
            l.Body("Choose the skin tone of your hands. This changes how your own hands look to you.");
            const float cell = 0.058f, gap = 0.01f;
            var strip = l.Block(cell);
            var n = HandSkin.Tones.Length;
            var chosen = Prefs.Current.handTone;
            for (var i = 0; i < n; i++)
            {
                var index = i;
                var p = Pressable.Create(strip, $"hands:tone-{i + 1}", "", new Vector2(cell, cell), () =>
                {
                    Prefs.Current.handTone = index;
                    Prefs.Save();
                    ApplyHandTone();
                    TestProbe.Event("hands", $"tone-{index + 1}");
                    ShowHandsSettings(back);
                }, PressStyle.Surface, new Vector3((i - (n - 1) / 2f) * (cell + gap), 0, 0));
                Ui.Panel(p.transform, "Swatch", new Vector2(cell - 0.014f, cell - 0.014f), HandSkin.Tones[i].Tone, (cell - 0.014f) / 2, new Vector3(0, 0, -0.0012f),
                    i == chosen ? Theme.Green : new Color(0, 0, 0, 0.18f), i == chosen ? 0.003f : 0.0008f, Ui.OrderFill);
                l.Buttons.Add(p);
            }
            var fromAvatar = HandSkin.TryParseTone(AvatarSkinHex(), out _);
            var follow = l.Button("hands:follow", fromAvatar ? "Match my avatar" : "Use the usual tone", () =>
            {
                Prefs.Current.handTone = -1;
                Prefs.Save();
                ApplyHandTone();
                TestProbe.Event("hands", "follow");
                ShowHandsSettings(back);
            }, PressStyle.Chip);
            follow.SetToggled(chosen < 0);
            FinishPanel(l, ("hands:back", "Back", back, PressStyle.Dark));
        }

        void ConfirmLeaveRoom()
        {
            var l = NewPanel("leave-room");
            l.Title($"Leave {CurrentRoom.Name}?");
            l.Body("You'll stop seeing new moments from this room and lose access to what was shared here. Someone has to invite you again to come back.");
            l.Row(("leave:cancel", "Stay", ShowSettings, PressStyle.Secondary), ("leave:confirm", "Leave room", () => Run(async ct =>
            {
                await Api.LeaveRoom(CurrentRoom.Id, ct);
                TestProbe.Event("room", "left");
                await LeaveLive();
                await ((LiveData)Data).Load(ct);
                CurrentRoom = Data.Rooms.FirstOrDefault();
                Prefs.Current.lastRoomId = CurrentRoom?.Id;
                Prefs.Save();
                if (CurrentRoom == null) { Prefs.Current.setupStep = "room"; Prefs.Save(); ShowSetup(); }
                else EnterRoom();
            }, "Couldn't leave the room"), PressStyle.Danger));
            FinishPanel(l);
        }

        // ------------------------------------------------------------ avatar: preview, approve, sharing, delete

        Avatars.Booth _booth;          // the private preview while an avatar screen is open
        Texture2D _reviewSource;       // the owner's own source photo, shown small beside the preview (never to anyone else)

        void CloseBooth()
        {
            _booth?.Dispose();
            _booth = null;
            if (_reviewSource != null) Destroy(_reviewSource);
            _reviewSource = null;
        }

        static readonly float[] PreviewYaw = { 0f, 38f, 90f };
        static readonly string[] PreviewName = { "Front", "Three-quarter", "Side" };

        /// <summary>The preview strip (visual reference 13): the owner's own photo, small, when there is one; the
        /// large view, turned with the chips under it; and a fixed side view.</summary>
        void AvatarViews(Layout l, int view, Action<int> turn)
        {
            _booth.EnsureViews();
            _booth.Render(PreviewYaw[view], _booth.Main);
            _booth.Render(90f, _booth.Side);
            const float big = 0.17f, gap = 0.012f;
            var parts = new System.Collections.Generic.List<(Vector2 size, Texture tex, string caption)>();
            if (_reviewSource != null)
            {
                var aspect = Mathf.Clamp(_reviewSource.width / (float)Mathf.Max(1, _reviewSource.height), 0.6f, 1.2f);
                parts.Add((new Vector2(0.1f * aspect, 0.1f), _reviewSource, "Your photo · only you"));
            }
            parts.Add((new Vector2(big, big), _booth.Main, PreviewName[view]));
            parts.Add((new Vector2(big * 0.6f, big * 0.8f), _booth.Side, "Side"));
            var strip = l.Block(big + 0.024f);
            var x = -(parts.Sum(p => p.size.x) + gap * (parts.Count - 1)) / 2f;
            foreach (var p in parts)
            {
                var c = new Vector3(x + p.size.x / 2, 0.012f, 0);
                var r = Ui.Panel(strip, "View", p.size, Color.white, 0.014f, c + new Vector3(0, 0, 0.0004f), new Color(1, 1, 1, 0.7f), 0.001f);
                Ui.SetImage(r, p.tex);
                Ui.Text(strip, "Caption", p.caption, Theme.Small * 0.9f, Theme.Muted, Mathf.Max(p.size.x, 0.1f), TMPro.TextAlignmentOptions.Top, new Vector3(c.x, 0.012f - p.size.y / 2 - 0.014f, 0), height: 0.016f);
                x += p.size.x + gap;
            }
            var chips = l.Row(("avatar:front", PreviewName[0], () => turn(0), PressStyle.Chip),
                              ("avatar:quarter", PreviewName[1], () => turn(1), PressStyle.Chip),
                              ("avatar:side", PreviewName[2], () => turn(2), PressStyle.Chip));
            chips[view].SetToggled(true);
        }

        public void ShowAvatarSettings(Action back)
        {
            if (Data is not LiveData)
            {
                SampleAvatar(back, 1);
                return;
            }
            CloseBooth();
            var l = NewPanel("avatar", 0.5f);
            l.Title("Your avatar");
            l.Muted("Loading…");
            FinishPanel(l);
            Run(async ct =>
            {
                var studio = await Api.AvatarStudio(ct);
                var d = NewPanel("avatar", 0.5f);
                d.Title("Your avatar");
                var active = studio.Assets.FirstOrDefault(a => a.State == "active");
                var awaiting = studio.Assets.FirstOrDefault(a => a.State == "awaiting_owner_approval");
                var job = studio.Jobs.FirstOrDefault(j => (string)j["state"] is "validated" or "queued" or "processing" or "validating_asset");
                var failed = job == null && awaiting == null ? studio.Jobs.FirstOrDefault() : null;
                if (active != null)
                {
                    d.Body($"In use: your approved avatar (version {active.Version}).");
                    d.Muted($"Who can see it: {Scope(active.Visibility)}");
                    var scopes = d.Row(("avatar:scope-rooms", "People in my rooms", () => SetScope(active, "rooms", back), PressStyle.Chip),
                                       ("avatar:scope-direct", "1:1 rooms only", () => SetScope(active, "direct", back), PressStyle.Chip),
                                       ("avatar:scope-private", "Only me", () => SetScope(active, "private", back), PressStyle.Chip));
                    scopes[active.Visibility == "direct" ? 1 : active.Visibility == "private" ? 2 : 0].SetToggled(true);
                    d.Row(("avatar:demo", "Use the demo avatar", () => Run(async c => { await Api.UseDemoAvatar(c); Toast("Switched to the demo avatar."); ShowAvatarSettings(back); }, "Couldn't switch"), PressStyle.Secondary),
                          ("avatar:delete", "Delete this avatar", () => ConfirmDeleteAvatar(active, back), PressStyle.Danger));
                }
                else d.Body("In use: a labelled demo avatar. Nobody sees a likeness of you.");
                if (job != null)
                {
                    // still being made (visual reference 12): say so, show how far, and keep the demo avatar meanwhile
                    var progress = Mathf.Clamp01((float?)job["progress"] ?? 0f);
                    d.Gap(0.004f);
                    d.Body($"<b>Your avatar is being prepared</b> · {Mathf.RoundToInt(progress * 100)}%");
                    d.Progress(progress);
                    d.Muted("Until it's ready and you've approved it, others keep seeing the labelled demo avatar.");
                    d.Button("avatar:refresh", Icons.With(Icons.Refresh, "Check again"), () => ShowAvatarSettings(back), PressStyle.Secondary);
                }
                else if (failed != null && (string)failed["state"] == "failed")
                    d.Muted($"The last attempt didn't finish: {(string)failed["safeError"] ?? "it couldn't be made from those photos"}. You can try again from the companion.");
                if (awaiting != null)
                {
                    d.Gap(0.004f);
                    d.Body("<b>A new avatar is waiting for your review.</b> Only you can see it until you approve it.");
                    d.Button("avatar:review", "Look at it here", () => PreviewOwnAvatar(awaiting, studio, back, 1, "rooms"), PressStyle.Primary);
                }
                if (active == null && awaiting == null && job == null)
                    d.Muted("Create one from your photos in the companion (Settings → Your avatar). You'll review and approve it here or there.");
                d.Row(("avatar:back", "Back", back, PressStyle.Quiet));
                FinishPanel(d);
            }, "Couldn't load your avatar", () => ShowAvatarSettings(back));
        }

        /// <summary>The sample's preview: the demo character that stands for you, clearly labelled, with what the real
        /// review will let you do.</summary>
        void SampleAvatar(Action back, int view)
        {
            var me = MeAsMember();
            var l = NewPanel("avatar", 0.5f);
            l.Title("Your avatar");
            l.Tag("Sample: a demo character, not made from anyone's photos");
            if (_booth == null)
            {
                var prefab = Resources.Load<GameObject>("Characters/" + (me.DisplayName ?? "").ToLowerInvariant());
                if (prefab != null && prefab.GetComponentInChildren<SeatedFigure>(true) != null) _booth = Avatars.Booth.ForPrefab(prefab, Anchor("Seat"));
            }
            if (_booth != null) AvatarViews(l, view, v => SampleAvatar(back, v));
            l.Muted("With an account, the avatar made from your own photos appears here first, privately. You turn it to check the likeness, then choose Use this avatar, Retake photos or Cancel. Nobody else sees it until you approve it.");
            FinishPanel(l, ("avatar:back", "Back", () => { CloseBooth(); back(); }, PressStyle.Dark));
        }

        static string Scope(string v) => v switch { "rooms" => "people in your rooms", "direct" => "only your one-to-one rooms", "private" => "only you", _ => v };

        void SetScope(AvatarAssetInfo asset, string scope, Action back) => Run(async ct =>
        {
            await Api.SetAvatarVisibility(asset.Id, scope, ct);
            TestProbe.Event("avatar", "scope:" + scope);
            Toast($"Now visible to {Scope(scope)}.");
            ShowAvatarSettings(back);
        }, "Couldn't change who sees it");

        void ConfirmDeleteAvatar(AvatarAssetInfo asset, Action back)
        {
            var l = NewPanel("avatar-delete");
            l.Title("Delete your avatar?");
            l.Body("It stops being shown and can't be downloaded again. Copies others already loaded can't be recalled. Your demo avatar is used instead.");
            l.Row(("avatar-delete:no", "Keep it", () => ShowAvatarSettings(back), PressStyle.Secondary), ("avatar-delete:yes", "Delete", () => Run(async ct =>
            {
                await Api.DeleteAvatar(asset.Id, ct);
                TestProbe.Event("avatar", "deleted");
                Toast("Deleted.");
                ShowAvatarSettings(back);
            }, "Couldn't delete"), PressStyle.Danger));
            FinishPanel(l);
        }

        /// <summary>The owner's private review (visual reference 13): their source photo small, the model's head and
        /// shoulders large and turnable, a side view, who will see it, then an explicit choice.</summary>
        void PreviewOwnAvatar(AvatarAssetInfo asset, AvatarStudio studio, Action back, int view, string scope)
        {
            if (_booth == null)
            {
                var wait = NewPanel("avatar-review", 0.5f);
                wait.Title("Does this look like you?");
                wait.Muted("Preparing your private preview…");
                FinishPanel(wait);
                Run(async ct =>
                {
                    _booth = await Avatars.Booth.ForModel(new AvatarView { Kind = "photo_derived", AssetId = asset.Id, AssetVersion = asset.Version, ModelUrl = asset.ModelUrl }, Anchor("Seat"), ct);
                    var source = studio?.Sources.FirstOrDefault(s => (string)s["view"] == "front") ?? studio?.Sources.FirstOrDefault();
                    var url = (string)source?["imageUrl"];
                    if (!string.IsNullOrEmpty(url))
                    {
                        try
                        {
                            var bytes = await Api.Download(url, ct, 60);
                            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "Avatar source (private)", wrapMode = TextureWrapMode.Clamp };
                            if (tex.LoadImage(bytes, true)) _reviewSource = tex;
                            else Destroy(tex);
                        }
                        catch (ApiException) { /* the preview works without the photo beside it */ }
                    }
                    if (_booth == null) throw new ApiException(0, "avatar_preview_failed", "The preview couldn't be loaded. Try again.", true);
                    PreviewOwnAvatar(asset, studio, back, view, scope);
                }, "The preview couldn't load", () => PreviewOwnAvatar(asset, studio, back, view, scope));
                return;
            }
            var l = NewPanel("avatar-review", 0.5f);
            l.Title("Does this look like you?");
            l.Muted("Only you can see this. Turn it and compare; expressions aren't tracked from your face.");
            AvatarViews(l, view, v => PreviewOwnAvatar(asset, studio, back, v, scope));
            l.Muted("Who sees it once you use it:");
            var scopes = l.Row(("avatar-review:rooms", "People in my rooms", () => PreviewOwnAvatar(asset, studio, back, view, "rooms"), PressStyle.Chip),
                               ("avatar-review:direct", "1:1 rooms only", () => PreviewOwnAvatar(asset, studio, back, view, "direct"), PressStyle.Chip),
                               ("avatar-review:private", "Only me", () => PreviewOwnAvatar(asset, studio, back, view, "private"), PressStyle.Chip));
            scopes[scope == "direct" ? 1 : scope == "private" ? 2 : 0].SetToggled(true);
            l.Button("avatar-review:approve", Icons.With(Icons.Check, "Use this avatar"), () => Run(async ct =>
            {
                await Api.ApproveAvatar(asset.Id, scope, ct);
                CloseBooth();
                TestProbe.Event("avatar", "approved");
                Toast(scope == "private" ? "Approved. Only you see it for now." : $"Approved. Visible to {Scope(scope)}.");
                ShowAvatarSettings(back);
            }, "Couldn't approve"), PressStyle.Primary);
            l.Row(("avatar-review:retake", "Retake photos", () => ConfirmRetake(asset, studio, back), PressStyle.Secondary),
                  ("avatar-review:cancel", "Cancel", () => { CloseBooth(); ShowAvatarSettings(back); }, PressStyle.Quiet));
            FinishPanel(l);
        }

        void ConfirmRetake(AvatarAssetInfo asset, AvatarStudio studio, Action back)
        {
            var l = NewPanel("avatar-retake");
            l.Title("Retake your photos?");
            l.Body("This preview is discarded and nobody ever sees it. New photos are taken in the companion on your phone (Settings → Your avatar), where you also choose what is sent for processing.");
            l.Row(("avatar-retake:no", "Keep reviewing", () => PreviewOwnAvatar(asset, studio, back, 1, "rooms"), PressStyle.Secondary),
                  ("avatar-retake:yes", "Discard and retake", () => Run(async ct =>
                  {
                      await Api.RejectAvatar(asset.Id, ct);
                      CloseBooth();
                      TestProbe.Event("avatar", "rejected");
                      Toast("Discarded. Take new photos in the companion when you're ready.", 5f);
                      ShowAvatarSettings(back);
                  }, "Couldn't discard"), PressStyle.Primary));
            FinishPanel(l);
        }
    }
}
