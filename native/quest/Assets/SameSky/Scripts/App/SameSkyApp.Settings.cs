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
                foreach (var a in awards)
                    d.Body(a.Earned ? $"<b>{a.Title}</b> · earned" : $"{a.Title} · {a.Progress} of {a.Target}");
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
                d.Row(("keepsakes:back", "Back", EnterRoom, PressStyle.Quiet));
                FinishPanel(d);
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
            var a = l.Row(("settings:captions", Prefs.Current.captionsOn ? "Captions: on" : "Captions: off", null, PressStyle.Chip),
                          ("settings:sound", Prefs.Current.soundOn ? "Sound: on" : "Sound: off", null, PressStyle.Chip),
                          ("settings:text", $"Text size: {Prefs.Current.textScale:0.##}×", null, PressStyle.Chip));
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
            l.Row(("settings:volume-down", "Volume −", () => { Prefs.Current.volume = Mathf.Clamp01(Prefs.Current.volume - 0.1f); Prefs.Save(); AudioListener.volume = Prefs.Current.soundOn ? Prefs.Current.volume : 0; }, PressStyle.Secondary),
                  ("settings:volume-up", "Volume +", () => { Prefs.Current.volume = Mathf.Clamp01(Prefs.Current.volume + 0.1f); Prefs.Save(); AudioListener.volume = Prefs.Current.soundOn ? Prefs.Current.volume : 0; }, PressStyle.Secondary));
            l.Row(("settings:recenter", "Recenter", () => Recenter(), PressStyle.Secondary),
                  ("settings:seat-up", "Seat higher", () => { Prefs.Current.seatHeight += 0.05f; Prefs.Save(); Recenter(); }, PressStyle.Secondary),
                  ("settings:seat-down", "Seat lower", () => { Prefs.Current.seatHeight -= 0.05f; Prefs.Save(); Recenter(); }, PressStyle.Secondary));
            if (Data is LiveData)
            {
                l.Gap(0.004f);
                l.Body($"<b>Account</b> · {Data.Me.Email}");
                l.Row(("settings:avatar", "Your avatar", () => ShowAvatarSettings(ShowSettings), PressStyle.Secondary),
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
                l.Body("<b>Sample</b> · fictional people, nothing leaves this device");
                l.Row(("settings:reset-sample", "Reset the sample", () => { SampleState.Reset(); Prefs.Current.sampleIntroDone = false; Prefs.Save(); Toast("The sample is fresh again."); EnterRoom(); }, PressStyle.Quiet),
                      ("settings:account", "Use my account", () => { Data = null; ShowLaunch(); }, PressStyle.Secondary));
                l.Row(("settings:exit", "Leave SameSky", ConfirmExit, PressStyle.Quiet));
            }
            l.Muted("About: " + AppConfig.Describe());
            l.Row(("settings:back", "Back", EnterRoom, PressStyle.Primary));
            FinishPanel(l);
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

        // ------------------------------------------------------------ avatar: review, approve, sharing, delete

        public void ShowAvatarSettings(Action back)
        {
            if (Data is not LiveData)
            {
                var s = NewPanel("avatar");
                s.Title("Avatars");
                s.Body("In the sample everyone is a fictional demo character. With an account you can use a labelled demo avatar or your own approved one.");
                s.Row(("avatar:back", "Back", back, PressStyle.Quiet));
                FinishPanel(s);
                return;
            }
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
                if (active != null)
                {
                    d.Body($"In use: your approved avatar (version {active.Version}).");
                    d.Muted($"Who can see it: {Scope(active.Visibility)}");
                    d.Row(("avatar:scope-rooms", "People in my rooms", () => SetScope(active, "rooms", back), PressStyle.Chip),
                          ("avatar:scope-direct", "1:1 rooms only", () => SetScope(active, "direct", back), PressStyle.Chip),
                          ("avatar:scope-private", "Only me", () => SetScope(active, "private", back), PressStyle.Chip));
                    d.Row(("avatar:demo", "Use the demo avatar", () => Run(async c => { await Api.UseDemoAvatar(c); Toast("Switched to the demo avatar."); ShowAvatarSettings(back); }, "Couldn't switch"), PressStyle.Secondary),
                          ("avatar:delete", "Delete this avatar", () => ConfirmDeleteAvatar(active, back), PressStyle.Danger));
                }
                else d.Body("In use: a labelled demo avatar.");
                if (awaiting != null)
                {
                    d.Gap(0.004f);
                    d.Body("<b>A new avatar is waiting for your review.</b> Only you can see it until you approve it.");
                    d.Button("avatar:review", "Look at it here", () => PreviewOwnAvatar(awaiting, back), PressStyle.Primary);
                }
                if (active == null && awaiting == null)
                    d.Muted("Create one from your photos in the companion (Settings → Your avatar). You'll review and approve it here or there.");
                d.Row(("avatar:back", "Back", back, PressStyle.Quiet));
                FinishPanel(d);
            }, "Couldn't load your avatar", () => ShowAvatarSettings(back));
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

        Transform _ownPreview;

        /// <summary>Private review: the model stands beside the table for the owner only, then approve or reject.</summary>
        void PreviewOwnAvatar(AvatarAssetInfo asset, Action back)
        {
            var l = NewPanel("avatar-review", 0.5f);
            l.Title("Does this look like you?");
            l.Muted("Shown only to you, beside the table. Neutral light; expressions aren't tracked from your face.");
            l.Row(("avatar-review:reject", "Not me — discard", () => Run(async ct => { await Api.RejectAvatar(asset.Id, ct); ClearOwnPreview(); ShowAvatarSettings(back); }, "Couldn't discard"), PressStyle.Quiet),
                  ("avatar-review:approve", "This looks like me", () => Run(async ct =>
                  {
                      await Api.ApproveAvatar(asset.Id, "rooms", ct);
                      ClearOwnPreview();
                      TestProbe.Event("avatar", "approved");
                      Toast("Approved. People in your rooms will see it.");
                      ShowAvatarSettings(back);
                  }, "Couldn't approve"), PressStyle.Primary));
            FinishPanel(l);
            ClearOwnPreview();
            var spot = Anchor("PreviewSpot");
            _ownPreview = new GameObject("Own avatar preview").transform;
            _ownPreview.SetParent(spot != null ? spot : _world, false);
            if (spot == null) _ownPreview.position = new Vector3(0.75f, 0, 0.9f);
            _ = Avatars.SwapToApproved(_ownPreview, new AvatarView { Kind = "photo_derived", AssetId = asset.Id, AssetVersion = asset.Version, ModelUrl = asset.ModelUrl });
        }

        void ClearOwnPreview()
        {
            if (_ownPreview != null) Destroy(_ownPreview.gameObject);
            _ownPreview = null;
        }
    }
}
