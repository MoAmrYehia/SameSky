// The furniture marketplace: furnish the room you share.
//
// One panel at the table edge with a tab for ready-made looks and one for each part of the room (table, chairs,
// rug, lamp, plant, walls, floor). Touching a piece or a finish puts it in the room straight away, so the choice is
// made by looking at the real thing from the seat; nothing is kept until "Keep" is pressed. Kept furniture is stored
// with the room on the server, so whoever shares the room finds it the same way (in the offline sample it stays on
// this headset). Leaving without keeping puts the room back.
//
// Every piece is included with the app: there are no prices, purchases or accounts to charge.

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

namespace SameSky.App
{
    public sealed partial class SameSkyApp
    {
        Decor _decorSaved;     // how the room is furnished for everyone in it
        Decor _decorPreview;   // what the marketplace is trying out; null when it isn't open
        string _marketTab = "looks";

        /// <summary>What the room shows now, for the test probe: "chairs=tub/sage floor=parquet/natural ...".</summary>
        public string DecorSummary => RoomDecor.Instance != null && RoomDecor.Instance.Current != null ? RoomDecor.Instance.Current.Summary() : "";
        /// <summary>The room shows something that hasn't been kept.</summary>
        public bool DecorUnsaved => _decorPreview != null && _decorSaved != null && !_decorPreview.Same(_decorSaved);
        public int DecorVersion => _decorSaved?.Version ?? 0;

        /// <summary>Loads how this room is furnished and shows it. A room that can't say keeps its usual furniture:
        /// the room still opens.</summary>
        async Task LoadDecor(CancellationToken ct)
        {
            var room = RoomDecor.Instance;
            if (room == null || CurrentRoom == null) return;
            var roomId = CurrentRoom.Id;
            Decor loaded;
            try { loaded = await Data.GetDecor(roomId, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                Debug.LogWarning($"[SameSky] furniture not loaded ({e.GetType().Name}): the room keeps its usual furniture");
                loaded = FurnitureCatalog.Current.Defaults();
            }
            ct.ThrowIfCancellationRequested();
            if (CurrentRoom == null || CurrentRoom.Id != roomId) return;
            _decorSaved = loaded;
            if (Screen == "market" && DecorUnsaved) return;   // someone is trying things out: don't pull the room from under them
            _decorPreview = null;
            room.Apply(loaded);
            TestProbe.Event("decor", $"shown v{loaded.Version}");
        }

        /// <summary>Leaving the marketplace without keeping: the room goes back to how it is furnished.</summary>
        void EndDecorPreview()
        {
            if (_decorPreview == null) return;
            var unsaved = DecorUnsaved;
            _decorPreview = null;
            if (!unsaved || _decorSaved == null || RoomDecor.Instance == null) return;
            RoomDecor.Instance.Apply(_decorSaved);
            TestProbe.Event("decor", "put back");
        }

        public void ShowMarket() => ShowMarket(_marketTab);

        void ShowMarket(string tab)
        {
            var catalog = FurnitureCatalog.Current;
            if (catalog.Slots.Count == 0 || RoomDecor.Instance == null || _decorSaved == null)
            {
                Toast("The marketplace isn't ready yet. Try again in a moment.");
                return;
            }
            _decorPreview ??= _decorSaved.Clone();
            _marketTab = tab == "looks" || catalog.Slot(tab) != null ? tab : "looks";

            var l = NewPanel("market", 0.6f);
            l.Title("Marketplace");
            l.Muted($"Furnish the room you share. {catalog.Note}");
            if (DecorUnsaved) l.Tag("Trying it out · not kept yet");

            var tabs = new List<(string id, string label)> { ("looks", "Looks") };
            tabs.AddRange(catalog.Slots.Select(s => (s.Id, s.Name)));
            for (var i = 0; i < tabs.Count; i += 4)
            {
                var row = tabs.Skip(i).Take(4).ToList();
                var chips = l.Row(row.Select(t => ($"market:tab-{t.id}", t.label, (Action)(() => ShowMarket(t.id)), PressStyle.Chip)).ToArray());
                for (var k = 0; k < chips.Count; k++)
                {
                    chips[k].SetToggled(row[k].id == _marketTab);
                    chips[k].Label.fontSize *= 0.92f;
                }
            }

            if (_marketTab == "looks") MarketLooks(l, catalog);
            else MarketSlot(l, catalog, catalog.Slot(_marketTab));

            if (DecorUnsaved)
                FinishPanel(l,
                    ("market:keep", Icons.With(Icons.Check, "Keep"), KeepDecor, PressStyle.Primary),
                    ("market:undo", "Put it back", () => { EndDecorPreview(); ShowMarket(); }, PressStyle.Secondary),
                    ("market:back", "Back", EnterRoom, PressStyle.Dark));
            else
                FinishPanel(l, ("market:back", "Back", EnterRoom, PressStyle.Dark));
        }

        // ------------------------------------------------------------ tabs

        const float MarketTile = 0.172f, MarketGap = 0.012f;

        /// <summary>A tile that is a button: a card, something on it, a name under it. Returns the tile to add to.</summary>
        Transform MarketCard(Layout l, Transform strip, string id, float x, float height, bool chosen, string name, Action onPress)
        {
            var p = Pressable.Create(strip, id, "", new Vector2(MarketTile, height), onPress, PressStyle.Surface, new Vector3(x, 0, 0));
            l.Buttons.Add(p);
            Ui.Panel(p.transform, "Card", new Vector2(MarketTile, height), chosen ? Color.white : new Color(1f, 1f, 1f, 0.55f), 0.014f, new Vector3(0, 0, 0.0008f),
                chosen ? Theme.Green : new Color(0, 0, 0, 0.1f), chosen ? 0.003f : 0.0008f);
            Ui.Text(p.transform, "Name", name, Theme.Small, Theme.Ink, MarketTile - 0.012f, TextAlignmentOptions.Center,
                new Vector3(0, -height / 2f + 0.021f, 0), chosen ? FontStyles.Bold : FontStyles.Normal, 0.032f);
            return p.transform;
        }

        void MarketSlot(Layout l, FurnitureCatalog catalog, FurnitureSlot slot)
        {
            var room = RoomDecor.Instance;
            var choice = _decorPreview[slot.Id];
            var chosenItem = slot.Item(choice.Item) ?? slot.Items[0];

            if (slot.Items.Count > 1)
            {
                const float height = 0.17f, picture = 0.116f;
                var strip = l.Block(height);
                for (var i = 0; i < slot.Items.Count; i++)
                {
                    var item = slot.Items[i];
                    var chosen = item.Id == chosenItem.Id;
                    // shown in the finish it would arrive in: the current one when the piece comes in it
                    var finish = item.Finishes.Contains(choice.Finish) ? choice.Finish : item.Finishes.FirstOrDefault();
                    var tile = MarketCard(l, strip, $"market:item-{slot.Id}-{item.Id}", (i - (slot.Items.Count - 1) / 2f) * (MarketTile + MarketGap), height, chosen, item.Name,
                        () => TryDecor(slot.Id, item.Id, finish));
                    var tex = room.ItemRoot(slot.Id, item.Id) != null && room.ItemRoot(slot.Id, item.Id).childCount > 0 ? DecorBooth.Picture(slot.Id, item.Id, finish) : null;
                    // pictures draw after their card (the same draw order would let the card cover them)
                    if (tex != null) Ui.Image(tile, "Picture", new Vector2(picture, picture), tex, 0.01f, new Vector3(0, 0.02f, -0.0006f)).sortingOrder = Ui.OrderFill;
                    else
                    {
                        Ui.Panel(tile, "Empty", new Vector2(picture, picture), new Color(0, 0, 0, 0.05f), 0.01f, new Vector3(0, 0.02f, -0.0006f), new Color(0, 0, 0, 0.12f), 0.0008f, Ui.OrderFill);
                        Ui.Text(tile, "None", Icons.Close, Theme.Title, Theme.Muted, picture, TextAlignmentOptions.Center, new Vector3(0, 0.02f, -0.0008f), height: picture);
                    }
                }
            }
            l.Body($"<b>{chosenItem.Name}</b> · {chosenItem.Blurb}");
            l.Muted($"By {catalog.Maker} · included");

            if (chosenItem.Finishes.Count == 0) return;
            l.Muted($"Finish · {catalog.Finish(choice.Finish)?.Name ?? choice.Finish}");
            var pictures = slot.Items.Count == 1 && room.ItemRoot(slot.Id, chosenItem.Id) != null;   // one piece: show it in each finish
            if (pictures || slot.Items.Count == 1)
            {
                // the finishes are the choice here: a tile for each
                const float height = 0.15f, picture = 0.1f;
                var n = chosenItem.Finishes.Count;
                var perRow = Mathf.Min(n, 3);
                for (var start = 0; start < n; start += perRow)
                {
                    var strip = l.Block(height);
                    var count = Mathf.Min(perRow, n - start);
                    for (var i = 0; i < count; i++)
                    {
                        var finishId = chosenItem.Finishes[start + i];
                        var finish = catalog.Finish(finishId);
                        var tile = MarketCard(l, strip, $"market:finish-{slot.Id}-{finishId}", (i - (count - 1) / 2f) * (MarketTile + MarketGap), height, finishId == choice.Finish,
                            finish?.Name ?? finishId, () => TryDecor(slot.Id, chosenItem.Id, finishId));
                        var tex = pictures ? DecorBooth.Picture(slot.Id, chosenItem.Id, finishId) : null;
                        if (tex != null) Ui.Image(tile, "Picture", new Vector2(picture * 1.5f, picture), tex, 0.01f, new Vector3(0, 0.018f, -0.0006f)).sortingOrder = Ui.OrderFill;
                        else Ui.Panel(tile, "Swatch", new Vector2(picture * 1.5f, picture), finish?.Swatch ?? Color.gray, 0.01f, new Vector3(0, 0.018f, -0.0006f), new Color(0, 0, 0, 0.15f), 0.0008f, Ui.OrderFill);
                    }
                }
                return;
            }
            // several pieces: the finishes are a row of colour dots under them
            const float dot = 0.052f, dotGap = 0.012f;
            var dots = l.Block(dot);
            for (var i = 0; i < chosenItem.Finishes.Count; i++)
            {
                var finishId = chosenItem.Finishes[i];
                var finish = catalog.Finish(finishId);
                var on = finishId == choice.Finish;
                var p = Pressable.Create(dots, $"market:finish-{slot.Id}-{finishId}", "", new Vector2(dot, dot), () => TryDecor(slot.Id, chosenItem.Id, finishId),
                    PressStyle.Surface, new Vector3((i - (chosenItem.Finishes.Count - 1) / 2f) * (dot + dotGap), 0, 0));
                Ui.Panel(p.transform, "Swatch", new Vector2(dot - 0.012f, dot - 0.012f), finish?.Swatch ?? Color.gray, (dot - 0.012f) / 2f, new Vector3(0, 0, -0.0012f),
                    on ? Theme.Green : new Color(0, 0, 0, 0.2f), on ? 0.003f : 0.0008f, Ui.OrderFill);
                l.Buttons.Add(p);
            }
        }

        void MarketLooks(Layout l, FurnitureCatalog catalog)
        {
            const float height = 0.15f, dot = 0.026f;
            var strip = l.Block(height);
            var n = Mathf.Min(catalog.Looks.Count, 3);
            for (var i = 0; i < n; i++)
            {
                var look = catalog.Looks[i];
                var tile = MarketCard(l, strip, $"market:look-{look.Id}", (i - (n - 1) / 2f) * (MarketTile + MarketGap), height, _decorPreview.Same(look.Decor), look.Name, () =>
                {
                    _decorPreview = look.Decor.Clone();
                    _decorPreview.Version = _decorSaved.Version;
                    RoomDecor.Instance.Apply(_decorPreview);
                    TestProbe.Event("decor", $"try look {look.Id}");
                    ShowMarket();
                });
                // the look in four colours: walls, chairs, table, rug
                var colours = new[] { "walls", "chairs", "table", "rug" }.Select(s => catalog.Finish(look.Decor[s].Finish)?.Swatch).Where(c => c.HasValue).Select(c => c.Value).ToList();
                for (var k = 0; k < colours.Count; k++)
                    Ui.Panel(tile, "Colour", new Vector2(dot, dot), colours[k], dot / 2f, new Vector3((k - (colours.Count - 1) / 2f) * (dot + 0.006f), 0.046f, -0.0006f),
                        new Color(0, 0, 0, 0.18f), 0.0008f, Ui.OrderFill);
                Ui.Text(tile, "Blurb", look.Blurb, Theme.Small * 0.86f, Theme.Muted, MarketTile - 0.016f, TextAlignmentOptions.Center, new Vector3(0, 0.004f, -0.0006f), height: 0.05f);
            }
            l.Muted("A look changes everything at once. Then change any piece in the other tabs.");
        }

        // ------------------------------------------------------------ trying and keeping

        void TryDecor(string slot, string item, string finish)
        {
            if (_decorPreview == null || !FurnitureCatalog.Current.TryChoice(slot, item, finish, out var choice)) return;
            _decorPreview.Choices[slot] = choice;
            RoomDecor.Instance.Apply(_decorPreview);
            TestProbe.Event("decor", $"try {slot}={choice}");
            ShowMarket();
        }

        void KeepDecor()
        {
            var wanted = _decorPreview;
            if (wanted == null || !DecorUnsaved) { EnterRoom(); return; }
            Run(async ct =>
            {
                Decor kept;
                try { kept = await Data.SaveDecor(CurrentRoom.Id, wanted, _decorSaved, ct); }
                catch (ApiException e) when (e.Code == "version_conflict")
                {
                    // someone else furnished the room in the meantime: show theirs and stay in the marketplace
                    _decorSaved = await Data.GetDecor(CurrentRoom.Id, ct);
                    _decorPreview = null;
                    RoomDecor.Instance.Apply(_decorSaved);
                    TestProbe.Event("decor", $"conflict, shown v{_decorSaved.Version}");
                    Toast("Someone changed the room just now. This is how they left it.");
                    ShowMarket();
                    return;
                }
                _decorSaved = kept;
                _decorPreview = null;
                RoomDecor.Instance.Apply(kept);
                TestProbe.Event("decor", $"kept v{kept.Version} {kept.Summary()}");
                var other = CurrentRoom.Members.FirstOrDefault(m => m.Id != Data.Me.Id);
                Toast(Data.IsSample ? "Kept. In the sample it stays on this headset."
                    : other != null && CurrentRoom.IsDirect ? $"Kept. {other.DisplayName} will find the room this way."
                    : "Kept for everyone in this room.");
                EnterRoom();
            }, "Couldn't keep the furniture", KeepDecor);
        }

        /// <summary>The room was refurnished by someone else while we sit in it together.</summary>
        void OnDecorChangedElsewhere() => Run(LoadDecor, "Couldn't load the room's furniture");
    }
}
