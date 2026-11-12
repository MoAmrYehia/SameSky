// Development-only shortcuts for evidence renders: the test probe opens a screen directly so a fixed seated view of
// it can be saved without steering simulated hands through every menu. Acceptance checks don't use this (they press
// the real buttons), and release builds have no probe to call it.

using System.Linq;
using UnityEngine;

namespace SameSky.App
{
    public sealed partial class SameSkyApp
    {
        /// <summary>Opens a screen by name for a render. Returns false when it can't (no probe, no room yet, unknown name).</summary>
        public bool DevOpen(string screen)
        {
            if (!TestProbe.Enabled || string.IsNullOrEmpty(screen)) return false;
            if (screen == "launch") { ShowLaunch(); return true; }
            if (screen == "sample") { _ = EnterSample(); return true; }
            if (Data == null || CurrentRoom == null) return false;
            switch (screen)
            {
                case "room": EnterRoom(); return true;
                case "day": ShowOurDay(); return true;
                case "share": ShowShare(); return true;
                case "together": ShowTogether(); return true;
                case "keepsakes": ShowKeepsakes(); return true;
                case "settings": ShowSettings(); return true;
                case "pause": ShowPause(); return true;
                case "avatar": ShowAvatarSettings(EnterRoom); return true;
                case "hands": ShowHandsSettings(EnterRoom); return true;
                case "market": ShowMarket("looks"); return true;
                case var tab when tab.StartsWith("market:"): ShowMarket(tab.Substring(7)); return true;
                case var name when name.StartsWith("look:"):
                    // the room in one of the ready-made looks, tried but not kept (the next screen puts it back)
                    var look = FurnitureCatalog.Current.Looks.FirstOrDefault(k => k.Id == name.Substring(5));
                    if (look == null || RoomDecor.Instance == null || _decorSaved == null) return false;
                    HidePanel();
                    _decorPreview = look.Decor.Clone();
                    RoomDecor.Instance.Apply(_decorPreview);
                    return true;
                case "rooms": ShowRooms(); return true;
                case "reply": StartReply(); return true;
                case "story":
                    Run(async ct =>
                    {
                        var recaps = await Data.Recaps(CurrentRoom.Id, ct);
                        var shared = recaps.Where(r => r.State == "shared").OrderByDescending(r => r.RoomDate).FirstOrDefault();
                        if (shared != null) PlayStory(shared);
                        else ShowOurDay();
                    }, "Couldn't open the story");
                    return true;
                case "circle":
                case "direct":
                    var room = Data.Rooms.FirstOrDefault(r => r.Kind == screen);
                    if (room == null) return false;
                    CurrentRoom = room;
                    EnterRoom();
                    return true;
                default:
                    Debug.Log($"[SameSky] DevOpen: unknown screen '{screen}'");
                    return false;
            }
        }
    }
}
