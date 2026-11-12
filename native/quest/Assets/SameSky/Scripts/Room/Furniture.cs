// The furniture marketplace: its catalogue, and how a room is furnished.
//
// The catalogue is the file the API validates against (api/samesky/furniture_catalog.json, bundled as
// Resources/furniture-catalog.json by scripts/native/build_native_assets.py): the parts of the room that can be
// furnished ("slots": table, chairs, rug, lamp, plant, walls, floor), the pieces that can fill each one, the finishes
// each piece comes in, and a few ready-made looks. Every piece is included with the app: nothing is priced or sold.
//
// A room's furnishing ("decor") is one choice per slot: a piece and, where the piece has finishes, one of them.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SameSky.App
{
    public sealed class FurnitureItem
    {
        public string Id, Name, Blurb;
        public readonly List<string> Finishes = new();
    }

    public sealed class FurnitureSlot
    {
        public string Id, Name;
        public readonly List<FurnitureItem> Items = new();
        public FurnitureItem Item(string id) => Items.FirstOrDefault(i => i.Id == id);
    }

    public sealed class FurnitureFinish
    {
        public string Id, Name;
        public Color Swatch;
    }

    public sealed class FurnitureLook
    {
        public string Id, Name, Blurb;
        public Decor Decor;
    }

    /// <summary>What fills one slot: a piece and its finish (null when the piece has only one).</summary>
    public readonly struct DecorChoice : IEquatable<DecorChoice>
    {
        public readonly string Item, Finish;
        public DecorChoice(string item, string finish) { Item = item; Finish = finish; }
        public bool Equals(DecorChoice other) => Item == other.Item && Finish == other.Finish;
        public override bool Equals(object obj) => obj is DecorChoice c && Equals(c);
        public override int GetHashCode() => (Item ?? "").GetHashCode() * 31 + (Finish ?? "").GetHashCode();
        public override string ToString() => Finish == null ? Item : $"{Item}/{Finish}";
    }

    /// <summary>How one room is furnished: a choice for every slot, and the server's version of it.</summary>
    public sealed class Decor
    {
        public readonly Dictionary<string, DecorChoice> Choices = new();
        public int Version;

        public DecorChoice this[string slot] => Choices.TryGetValue(slot, out var c) ? c : default;

        public Decor Clone()
        {
            var d = new Decor { Version = Version };
            foreach (var kv in Choices) d.Choices[kv.Key] = kv.Value;
            return d;
        }

        public bool Same(Decor other) =>
            other != null && Choices.Count == other.Choices.Count && Choices.All(kv => other.Choices.TryGetValue(kv.Key, out var c) && c.Equals(kv.Value));

        /// <summary>"chairs=tub/sage floor=parquet/natural ..." for logs and the test probe.</summary>
        public string Summary() => string.Join(" ", Choices.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value}"));

        /// <summary>The slots that differ from another furnishing, as the API takes them.</summary>
        public JObject Changes(Decor from)
        {
            var o = new JObject();
            foreach (var kv in Choices)
                if (from == null || !from.Choices.TryGetValue(kv.Key, out var was) || !was.Equals(kv.Value))
                    o[kv.Key] = new JObject { ["item"] = kv.Value.Item, ["finish"] = kv.Value.Finish };
            return o;
        }
    }

    public sealed class FurnitureCatalog
    {
        static FurnitureCatalog _current;

        /// <summary>The bundled catalogue (an empty one, with a warning, when the bundle is missing).</summary>
        public static FurnitureCatalog Current
        {
            get
            {
                if (_current != null) return _current;
                var text = Resources.Load<TextAsset>("furniture-catalog");
                if (text == null) Debug.LogWarning("[SameSky] furniture-catalog.json is not bundled (scripts/native/build_native_assets.py): the room keeps its built-in furniture");
                try { _current = Parse(text != null ? text.text : "{}"); }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SameSky] furniture catalogue unreadable ({e.GetType().Name}): the room keeps its built-in furniture");
                    _current = new FurnitureCatalog();
                }
                return _current;
            }
        }

        public int Version;
        public string Maker = "", Note = "";
        public readonly List<FurnitureSlot> Slots = new();
        public readonly Dictionary<string, FurnitureFinish> Finishes = new();
        public readonly List<FurnitureLook> Looks = new();

        public FurnitureSlot Slot(string id) => Slots.FirstOrDefault(s => s.Id == id);
        public FurnitureFinish Finish(string id) => id != null && Finishes.TryGetValue(id, out var f) ? f : null;

        public static FurnitureCatalog Parse(string json)
        {
            var root = JObject.Parse(json);
            var c = new FurnitureCatalog { Version = (int?)root["version"] ?? 0, Maker = (string)root["maker"] ?? "", Note = (string)root["note"] ?? "" };
            foreach (var kv in root["finishes"] as JObject ?? new JObject())
                c.Finishes[kv.Key] = new FurnitureFinish
                {
                    Id = kv.Key, Name = (string)kv.Value["name"] ?? kv.Key,
                    Swatch = ColorUtility.TryParseHtmlString((string)kv.Value["swatch"] ?? "", out var col) ? col : Color.gray,
                };
            foreach (var s in root["slots"] as JArray ?? new JArray())
            {
                var slot = new FurnitureSlot { Id = (string)s["id"], Name = (string)s["name"] ?? (string)s["id"] };
                foreach (var i in s["items"] as JArray ?? new JArray())
                {
                    var item = new FurnitureItem { Id = (string)i["id"], Name = (string)i["name"] ?? (string)i["id"], Blurb = (string)i["blurb"] ?? "" };
                    foreach (var f in i["finishes"] as JArray ?? new JArray()) item.Finishes.Add((string)f);
                    if (!string.IsNullOrEmpty(item.Id)) slot.Items.Add(item);
                }
                if (!string.IsNullOrEmpty(slot.Id) && slot.Items.Count > 0) c.Slots.Add(slot);
            }
            foreach (var l in root["looks"] as JArray ?? new JArray())
                c.Looks.Add(new FurnitureLook { Id = (string)l["id"], Name = (string)l["name"] ?? "", Blurb = (string)l["blurb"] ?? "", Decor = c.Resolve(l["choices"]) });
            return c;
        }

        /// <summary>The room as it comes: every slot's first piece in its first finish.</summary>
        public Decor Defaults()
        {
            var d = new Decor();
            foreach (var slot in Slots) d.Choices[slot.Id] = new DecorChoice(slot.Items[0].Id, slot.Items[0].Finishes.FirstOrDefault());
            return d;
        }

        /// <summary>Whether the catalogue has this piece in this finish; a missing finish means the piece's first.</summary>
        public bool TryChoice(string slotId, string itemId, string finish, out DecorChoice choice)
        {
            choice = default;
            var item = Slot(slotId)?.Item(itemId);
            if (item == null) return false;
            if (item.Finishes.Count == 0) { if (finish != null) return false; choice = new DecorChoice(item.Id, null); return true; }
            if (finish == null) finish = item.Finishes[0];
            if (!item.Finishes.Contains(finish)) return false;
            choice = new DecorChoice(item.Id, finish);
            return true;
        }

        /// <summary>Stored choices with the defaults filled in. Anything this catalogue doesn't have falls back to the
        /// default, so a room never points at a piece that can't be drawn (the server applies the same rule).</summary>
        public Decor Resolve(JToken stored, int version = 0)
        {
            var d = Defaults();
            d.Version = version;
            if (stored is JObject o)
                foreach (var kv in o)
                {
                    if (kv.Value is not JObject c) continue;
                    var finish = c["finish"] == null || c["finish"].Type == JTokenType.Null ? null : (string)c["finish"];
                    if (TryChoice(kv.Key, (string)c["item"], finish, out var choice)) d.Choices[kv.Key] = choice;
                }
            return d;
        }
    }
}
