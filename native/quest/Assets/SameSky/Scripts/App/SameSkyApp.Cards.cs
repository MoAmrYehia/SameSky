// Small building blocks the screens share (visual references 05 to 09): a person's portrait disc, a person tile with
// their place and local time, and a media thumbnail. Portraits come from the character that sits in the room for the
// person (Avatars.Portrait); without one, their initial.

using System;
using System.Linq;
using SameSky.Api;
using SameSky.UI;
using TMPro;
using UnityEngine;

namespace SameSky.App
{
    public sealed partial class SameSkyApp
    {
        /// <summary>A round portrait (or initial) for a member, centred at <paramref name="center"/> in parent space.</summary>
        void PortraitDisc(Transform parent, Vector3 center, float radius, Member m)
        {
            // drawn after the tile or card it sits on (same-order see-through panels otherwise sort by distance,
            // and a wider background can wash over it)
            var disc = Ui.Panel(parent, "Portrait", new Vector2(radius * 2, radius * 2), Theme.GreenSoft, radius, center + new Vector3(0, 0, 0.0004f), order: Ui.OrderFill);
            var face = m != null && Data != null ? Avatars.Portrait(m, Data.IsSample, Anchor("Seat")) : null;
            if (face != null)
            {
                Ui.SetPanel(disc, new Vector2(radius * 2, radius * 2), Color.white, radius, new Color(1, 1, 1, 0.6f), 0.001f, 0, face);
                return;
            }
            var initial = string.IsNullOrEmpty(m?.DisplayName) ? "?" : m.DisplayName.Substring(0, 1).ToUpperInvariant();
            Ui.Text(parent, "Initial", initial, radius * 0.95f, Theme.Ink, radius * 2, TextAlignmentOptions.Center, center, FontStyles.Bold, radius * 2);
        }

        /// <summary>The member record for me in the current room (my portrait and zone come from it).</summary>
        Member MeAsMember() => CurrentRoom?.Members.FirstOrDefault(x => x.Id == Data?.Me?.Id)
                               ?? new Member { Id = Data?.Me?.Id, DisplayName = Data?.Me?.DisplayName, TimeZone = Data?.Me?.TimeZone };

        /// <summary>Where someone is, for display: the sample's city, else the city part of their time zone.</summary>
        string CityOf(Member m)
        {
            if (m == null) return "";
            if (Data is SampleData s && s.People.TryGetValue(m.Id, out var p) && p["city"] != null) return (string)p["city"];
            var zone = m.TimeZone ?? "";
            var slash = zone.LastIndexOf('/');
            return (slash >= 0 ? zone.Substring(slash + 1) : zone).Replace('_', ' ');
        }

        /// <summary>A tile for one person: portrait, a small line above (name and place), their local time large,
        /// a small line below.</summary>
        void PersonTile(Transform parent, Vector3 center, Vector2 size, Member m, string top, string big, string bottom)
        {
            Ui.Panel(parent, "Tile", size, new Color(1, 1, 1, 0.6f), 0.016f, center + new Vector3(0, 0, 0.0006f), new Color(0, 0, 0, 0.07f), 0.0008f);
            var r = Mathf.Min(0.036f, size.y / 2 - 0.012f);
            PortraitDisc(parent, center + new Vector3(-size.x / 2 + 0.014f + r, 0, 0), r, m);
            var left = center.x - size.x / 2 + 0.014f + r * 2 + 0.012f;
            var width = center.x + size.x / 2 - 0.01f - left;
            var x = left + width / 2;
            var a = Ui.Text(parent, "Top", top, Theme.Small, Theme.Muted, width, TextAlignmentOptions.Left, new Vector3(x, center.y + size.y / 2 - 0.022f, 0), height: 0.018f);
            a.textWrappingMode = TextWrappingModes.NoWrap;
            a.overflowMode = TextOverflowModes.Ellipsis;
            Ui.Text(parent, "Big", big, Theme.Title * 1.2f, Theme.Ink, width, TextAlignmentOptions.Left, new Vector3(x, center.y - 0.003f, 0), FontStyles.Bold, 0.036f);
            var b = Ui.Text(parent, "Bottom", bottom, Theme.Small, Theme.Muted, width, TextAlignmentOptions.Left, new Vector3(x, center.y - size.y / 2 + 0.02f, 0), height: 0.018f);
            b.textWrappingMode = TextWrappingModes.NoWrap;
            b.overflowMode = TextOverflowModes.Ellipsis;
        }

        /// <summary>A rounded thumbnail of a photo, poster or (for a voice note) a waveform mark, loaded when ready.</summary>
        MeshRenderer Thumb(Transform parent, Vector3 center, Vector2 size, Moment m, string fallbackIcon = null)
        {
            var r = Ui.Panel(parent, "Thumb", size, new Color(0.83f, 0.85f, 0.8f, 1f), 0.012f, center + new Vector3(0, 0, 0.0004f), new Color(1, 1, 1, 0.7f), 0.001f);
            if (m?.Media == null || m.Media.Kind == "audio")
            {
                var mark = m?.Media?.Kind == "audio" ? Icons.Waveform : fallbackIcon ?? Icons.Image;
                Ui.Text(parent, "Mark", mark, Theme.Title * 1.4f, Theme.Green, size.x, TextAlignmentOptions.Center, center, height: size.y);
                return r;
            }
            _ = LoadThumb(r, size, m, ScreenToken);
            return r;
        }

        async System.Threading.Tasks.Task LoadThumb(MeshRenderer r, Vector2 size, Moment m, System.Threading.CancellationToken ct)
        {
            try
            {
                var tex = await Data.Media.Image(m.Media, thumb: true, ct);
                if (tex != null && r != null) Ui.SetPanel(r, size, Color.white, 0.012f, new Color(1, 1, 1, 0.7f), 0.001f, 0, tex);
            }
            catch (Exception) { /* the placeholder stays: a thumbnail is never worth an error screen */ }
        }

        /// <summary>m:ss for a duration in seconds.</summary>
        static string Clock(float seconds) => $"{Mathf.FloorToInt(seconds / 60f)}:{Mathf.FloorToInt(seconds % 60f):00}";

        /// <summary>"Maya", "Maya and Lina", "Maya, Lina and Sam".</summary>
        static string Names(System.Collections.Generic.IReadOnlyList<string> names) => names.Count switch
        {
            0 => "no one",
            1 => names[0],
            2 => $"{names[0]} and {names[1]}",
            _ => string.Join(", ", names.Take(names.Count - 1)) + " and " + names[names.Count - 1],
        };
    }
}
