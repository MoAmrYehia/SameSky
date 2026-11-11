// First run (live accounts): you -> your room or a code -> invite -> avatar -> usual times -> done.
// Progress is saved after every step (Prefs.setupStep), so closing the app resumes where the person left off.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SameSky.Api;
using SameSky.Interaction;
using SameSky.UI;
using UnityEngine;

namespace SameSky.App
{
    public sealed partial class SameSkyApp
    {
        static readonly string[] SetupSteps = { "about", "room", "invite", "avatar", "times", "done" };
        string _zone;
        InviteResult _invite;
        float _nextMemberPoll;

        void SaveStep(string step)
        {
            Prefs.Current.setupStep = step == "done" ? null : step;
            Prefs.Save();
            TestProbe.Event("setup", step);
        }

        public void ShowSetup()
        {
            var step = Prefs.Current.setupStep;
            if (string.IsNullOrEmpty(step)) step = CurrentRoom == null ? "about" : "invite";
            if (CurrentRoom == null && step is "invite" or "avatar" or "times") step = "room";
            switch (step)
            {
                case "about": SetupAbout(); break;
                case "room": SetupRoom(); break;
                case "invite": SetupInvite(); break;
                case "avatar": SetupAvatar(); break;
                case "times": SetupTimes(); break;
                default: EnterRoom(); break;
            }
        }

        Layout SetupPanel(string step, string title)
        {
            SetDock();
            var l = NewPanel("setup:" + step);
            var i = Array.IndexOf(SetupSteps, step);
            l.Muted($"Getting set up · step {i + 1} of 5");
            l.Title(title);
            return l;
        }

        void SetupFooter(Layout l, string back, Action next, string nextLabel = "Continue", bool canSkip = false)
        {
            var items = new List<(string, string, Action, PressStyle)>();
            if (back != null) items.Add(("setup:back", "Back", () => { SaveStep(back); ShowSetup(); }, PressStyle.Quiet));
            if (canSkip) items.Add(("setup:later", "Finish later", FinishSetupLater, PressStyle.Quiet));
            items.Add(("setup:next", nextLabel, next, PressStyle.Primary));
            l.Row(items.ToArray());
        }

        void FinishSetupLater()
        {
            if (CurrentRoom == null) { Toast("Create a room or join one first."); return; }
            Prefs.Current.setupStep = "invite"; // resume at the invite step next time
            Prefs.Save();
            EnterRoom();
        }

        // ------------------------------------------------------------ 1. about you

        void SetupAbout()
        {
            var me = Data.Me;
            _zone ??= string.IsNullOrEmpty(me.TimeZone) ? TimeZones.Detect() : me.TimeZone;
            var l = SetupPanel("about", $"Welcome, {me.DisplayName}");
            l.Body("Share a few chosen moments, find time that fits both time zones, and sit together at one table.");
            l.Body($"Name people see: {me.DisplayName}");
            l.Button("setup:name", "Change name", () => OpenKeyboard("What should people call you?", SpatialKeyboard.Kind.Text, me.DisplayName,
                v => Run(async ct =>
                {
                    CloseKeyboard();
                    var updated = await Api.UpdateMe(v.Trim(), null, ct);
                    Data.Me.DisplayName = updated.DisplayName;
                    SetupAbout();
                }, "Couldn't save your name"), SetupAbout, max: 60));
            l.Body($"Your time zone: {_zone.Replace('_', ' ')}");
            l.Muted("Used to show local times and overlaps. Change it whenever you travel — no location needed.");
            var zones = TimeZones.Common.Contains(_zone) ? TimeZones.Common : new[] { _zone }.Concat(TimeZones.Common).ToArray();
            var idx = Array.IndexOf(zones, _zone);
            l.Row(("setup:zone-prev", "Previous zone", () => { _zone = zones[(idx - 1 + zones.Length) % zones.Length]; SetupAbout(); }, PressStyle.Secondary),
                  ("setup:zone-next", "Next zone", () => { _zone = zones[(idx + 1) % zones.Length]; SetupAbout(); }, PressStyle.Secondary));
            SetupFooter(l, null, () => Run(async ct =>
            {
                if (_zone != Data.Me.TimeZone) Data.Me.TimeZone = (await Api.UpdateMe(null, _zone, ct)).TimeZone;
                SaveStep(CurrentRoom == null ? "room" : "invite");
                ShowSetup();
            }, "Couldn't save your time zone"));
            FinishPanel(l);
        }

        // ------------------------------------------------------------ 2. room, or join with a code

        string _roomKind = "direct";

        void SetupRoom()
        {
            var l = SetupPanel("room", "Who is this room for?");
            var kinds = l.Row(("setup:direct", "Just the two of us", () => { _roomKind = "direct"; SetupRoom(); }, PressStyle.Chip),
                              ("setup:circle", "A small circle (up to 6)", () => { _roomKind = "circle"; SetupRoom(); }, PressStyle.Chip));
            kinds[0].SetToggled(_roomKind == "direct");
            kinds[1].SetToggled(_roomKind == "circle");
            l.Muted(_roomKind == "direct" ? "A private room for you and one other person." : "Family or close friends. Each share still goes only to the people you pick.");
            l.Button("setup:create", "Create the room", () => Run(async ct =>
            {
                var room = await Api.CreateRoom(_roomKind, _roomKind == "direct" ? $"{Data.Me.DisplayName}’s room" : "Our circle", _zone ?? Data.Me.TimeZone, ct);
                await SelectRoom(room);
                SaveStep("invite");
                ShowSetup();
            }, "Couldn't create the room"), PressStyle.Primary);
            l.Gap(0.004f);
            l.Body("Someone already invited you?");
            l.Button("setup:have-code", "Type an invite code", () => EnterCode(SetupRoom), PressStyle.Secondary);
            SetupFooter(l, "about", () => Toast("Create a room, or type the code someone gave you."), "Continue");
            FinishPanel(l);
        }

        /// <summary>Code keypad (only the characters codes use). Recovers from expired, used, revoked and full.</summary>
        public void EnterCode(Action back)
        {
            var kb = OpenKeyboard("Invite code (8 characters, like K7PQ-4XMR)", SpatialKeyboard.Kind.Code, "", null, back,
                "Ask the person who invited you to read you the code shown beside their invite.");
            kb.Submitted += code => Run(async ct =>
            {
                if (code.Replace("-", "").Length != 8) { kb.SetHint("Codes have 8 characters.", true); return; }
                kb.SetHint("Checking…");
                try
                {
                    var room = await Api.RedeemCode(code, ct);
                    CloseKeyboard();
                    await SelectRoom(room);
                    Toast($"You joined {room.Name}.");
                    TestProbe.Event("joined", room.Kind);
                    SaveStep("avatar");
                    ShowSetup();
                }
                catch (ApiException e)
                {
                    var advice = e.Code switch
                    {
                        "invite_code_unknown" => "That code doesn't match an invitation. Check each character and try again.",
                        "invite_expired" => "That code has expired. Ask them to make a new one.",
                        "invite_revoked" => "That invitation was cancelled. Ask them for a new code.",
                        "invite_used" => "Someone already used that code. Each code works once — ask for a new one.",
                        "room_full" => "That room is full. Ask the owner to make space or start your own room.",
                        "already_member" => "You're already in that room.",
                        "too_many_attempts" => e.Message,
                        _ => e.Message,
                    };
                    kb.SetHint(advice, true);
                    TestProbe.Event("join-failed", e.Code);
                }
            }, "Couldn't join");
        }

        async Task SelectRoom(Room room)
        {
            CurrentRoom = room;
            Prefs.Current.lastRoomId = room.Id;
            Prefs.Save();
            if (Data is LiveData live) await live.Load(ScreenToken);
            CurrentRoom = Data.Rooms.FirstOrDefault(r => r.Id == room.Id) ?? room;
        }

        // ------------------------------------------------------------ 3. invite (and waiting for them)

        void SetupInvite()
        {
            var room = CurrentRoom;
            var others = room.Members.Count(m => m.Id != Data.Me.Id);
            var l = SetupPanel("invite", others > 0 ? $"{room.Name} has {others + 1} people" : $"{room.Name} is ready");
            if (!room.IsOwner)
            {
                l.Body("Only the room's creator can invite people.");
                SetupFooter(l, "about", () => { SaveStep("avatar"); ShowSetup(); });
                FinishPanel(l);
                return;
            }
            InviteBlock(l, room);
            SetupFooter(l, "room", () => { SaveStep("avatar"); ShowSetup(); }, others > 0 ? "Continue" : "Continue while I wait", canSkip: true);
            FinishPanel(l);
        }

        /// <summary>Shows a single-use code for the other person to type (in their headset or the companion), and waits for them.</summary>
        void InviteBlock(Layout l, Room room)
        {
            if (room.Members.Count >= room.Capacity) { l.Body("This room is full."); return; }
            if (_invite == null || DateTimeOffset.TryParse(_invite.ExpiresAt, out var exp) && exp < DateTimeOffset.UtcNow)
            {
                l.Body(room.IsDirect ? "Invite the person you're making this room for." : "Invite people to your circle.");
                l.Button("invite:create", "Make an invite code", () => Run(async ct =>
                {
                    _invite = await Api.CreateInvite(room.Id, ct);
                    TestProbe.Event("invite", "created");
                    ShowSetupOrRoom();
                }, "Couldn't make an invite"), PressStyle.Primary);
                l.Muted("Each code works once, for one person, for 72 hours. Nobody can find your room any other way.");
                return;
            }
            l.Body("Ask them to type this code in SameSky (headset or phone):");
            l.Center(_invite.Code, Theme.Title * 1.6f);
            l.Muted($"Works once · expires {Local(_invite.ExpiresAt)} · they'll only see what's shared after they join");
            l.Row(("invite:new", "New code", () => Run(async ct =>
                  {
                      _invite = await Api.CreateInvite(room.Id, ct);
                      ShowSetupOrRoom();
                  }, "Couldn't make an invite"), PressStyle.Secondary),
                  ("invite:cancel", "Cancel this code", () => Run(async ct =>
                  {
                      await Api.RevokeInvite(room.Id, _invite.Id, ct);
                      _invite = null;
                      Toast("Code cancelled. It can't be used now.");
                      ShowSetupOrRoom();
                  }, "Couldn't cancel the code"), PressStyle.Quiet));
            l.Muted("Waiting for them to join… this updates by itself.");
        }

        void ShowSetupOrRoom()
        {
            if (Screen.StartsWith("setup")) ShowSetup();
            else if (Screen == "together") ShowTogether();
            else EnterRoom();
        }

        /// <summary>While an invite is outstanding, notice when someone joins (polls the room every few seconds).</summary>
        void PollForNewMembers()
        {
            if (_invite == null || CurrentRoom == null || Data is not LiveData || Time.unscaledTime < _nextMemberPoll) return;
            _nextMemberPoll = Time.unscaledTime + 4f;
            var before = CurrentRoom.Members.Count;
            _ = PollOnce(before);
        }

        async Task PollOnce(int before)
        {
            // Background check: failures stay quiet (the next poll tries again); it never replaces the screen.
            try
            {
                var room = await Api.Room(CurrentRoom.Id, ScreenToken);
                if (room.Members.Count > before)
                {
                    var joined = room.Members.Last();
                    _invite = null;
                    await SelectRoom(room);
                    Toast($"{joined.DisplayName} joined {room.Name}.");
                    TestProbe.Event("member-joined", room.Members.Count.ToString());
                    ShowSetupOrRoom();
                }
            }
            catch (Exception) { /* offline or moved on: try again on the next poll */ }
        }

        // ------------------------------------------------------------ 4. avatar

        void SetupAvatar()
        {
            var l = SetupPanel("avatar", "How others see you");
            var av = Data.Me.Avatar;
            if (av != null && av.IsPhotoDerived)
                l.Body("You're using your own approved avatar. You can change who sees it in Settings.");
            else
            {
                l.Body("You're shown as a labelled demo avatar for now.");
                l.Muted("To be recognisable at the table, create an avatar from your photos in the SameSky companion (Settings → Your avatar). You review and approve it before anyone sees it, and you can switch back any time.");
            }
            l.Button("avatar:review", "Review avatar in the headset", () => ShowAvatarSettings(() => { SaveStep("avatar"); ShowSetup(); }), PressStyle.Secondary);
            SetupFooter(l, "invite", () => { SaveStep("times"); ShowSetup(); }, "Keep this for now");
            FinishPanel(l);
        }

        // ------------------------------------------------------------ 5. usual times

        static readonly (string id, string label, int[] days, string start, string end)[] TimePresets =
        {
            ("weekday-evenings", "Weekday evenings · 19:00–22:00", new[] { 0, 1, 2, 3, 4 }, "19:00", "22:00"),
            ("weekday-mornings", "Weekday mornings · 07:00–08:30", new[] { 0, 1, 2, 3, 4 }, "07:00", "08:30"),
            ("weekend-mornings", "Weekend mornings · 09:00–12:00", new[] { 5, 6 }, "09:00", "12:00"),
            ("weekend-evenings", "Weekend evenings · 18:00–22:00", new[] { 5, 6 }, "18:00", "22:00"),
        };

        readonly HashSet<string> _chosenTimes = new();

        void SetupTimes() => TimesPanel("times", () => { SaveStep("done"); ShowSetupDone(); }, "invite");

        void TimesPanel(string step, Action after, string back)
        {
            var l = step == "times" ? SetupPanel("times", "When are you usually free?") : NewPanel("availability");
            if (step != "times") l.Title("When are you usually free?");
            l.Muted($"In your time zone ({Data.Me.TimeZone}). Others only ever see the overlaps, never your whole week.");
            foreach (var p in TimePresets)
            {
                var b = l.Button("times:" + p.id, p.label, null, PressStyle.Chip);
                b.SetToggled(_chosenTimes.Contains(p.id));
                b.Pressed += () =>
                {
                    if (!_chosenTimes.Remove(p.id)) _chosenTimes.Add(p.id);
                    b.SetToggled(_chosenTimes.Contains(p.id));
                };
            }
            var save = (Action)(() => Run(async ct =>
            {
                if (_chosenTimes.Count > 0 && CurrentRoom != null)
                {
                    var rules = TimePresets.Where(p => _chosenTimes.Contains(p.id))
                        .SelectMany(p => p.days.Select(d => new WeeklyRule { Weekday = d, Start = p.start, End = p.end }));
                    await Api.SetRules(CurrentRoom.Id, rules, ct);
                    Toast("Saved your usual times.");
                }
                after();
            }, "Couldn't save your times"));
            if (step == "times") SetupFooter(l, back, save, _chosenTimes.Count > 0 ? "Save" : "Skip for now");
            else l.Row(("times:back", "Back", ShowSettings, PressStyle.Quiet), ("times:save", "Save", save, PressStyle.Primary));
            FinishPanel(l);
        }

        void ShowSetupDone()
        {
            var l = NewPanel("setup:done");
            l.Title("You're all set");
            l.Body(CurrentRoom.Members.Count > 1
                ? "Your room is ready. Share a moment, or pick up anything waiting on the table."
                : "Your room is ready. When they join, their moments arrive on the table.");
            l.Button("setup:enter", "Enter the room", EnterRoom, PressStyle.Primary);
            FinishPanel(l);
        }

        static string Local(string iso) =>
            DateTimeOffset.TryParse(iso, out var t) ? t.ToLocalTime().ToString("ddd HH:mm") : iso;
    }
}
