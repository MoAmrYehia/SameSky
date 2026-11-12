// Top-to-bottom layout for a world-space panel: titles, text, buttons and rows of buttons.
// Every interactive element is a Pressable (poke or point-and-pinch). Measurements in metres.

using System;
using System.Collections.Generic;
using SameSky.Interaction;
using TMPro;
using UnityEngine;

namespace SameSky.UI
{
    public sealed class Layout
    {
        public readonly Transform Root;
        public readonly float Width;
        float _y;
        public const float Pad = 0.022f;
        public const float ButtonHeight = 0.05f; // ~5 cm: comfortable for a fingertip, readable from the seat

        public readonly List<Pressable> Buttons = new();

        public Layout(Transform root, float width)
        {
            Root = root;
            Width = width;
            _y = -Pad;
        }

        public float Height => -_y + Pad;
        float Left => -Width / 2 + Pad;
        float Inner => Width - Pad * 2;

        TextMeshPro AddText(string name, string text, float size, Color color, FontStyles style, TextAlignmentOptions align)
        {
            var t = Ui.Text(Root, name, text, size, color, Inner, align, new Vector3(0, _y, 0), style);
            t.rectTransform.pivot = new Vector2(0.5f, 1f);
            t.ForceMeshUpdate();
            var h = Mathf.Max(t.preferredHeight, size * Theme.TextScale);
            t.rectTransform.sizeDelta = new Vector2(Inner, h);
            _y -= h + size * 0.45f;
            return t;
        }

        public TextMeshPro Title(string text) => AddText("Title", text, Theme.Title, Theme.Ink, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        public TextMeshPro Body(string text, Color? color = null) => AddText("Body", text, Theme.Body, color ?? Theme.Ink, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        public TextMeshPro Muted(string text) => AddText("Note", text, Theme.Small, Theme.Muted, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        public TextMeshPro Center(string text, float size) => AddText("Center", text, size, Theme.Ink, FontStyles.Bold, TextAlignmentOptions.Top);

        /// <summary>A small pill that says what something is (sample, simulated, synthesized, recorded, live...).</summary>
        public TextMeshPro Tag(string text)
        {
            var w = Mathf.Min(Inner, 0.012f + text.Length * Theme.Small * 0.52f * Theme.TextScale);
            var h = Theme.Small * 1.9f * Theme.TextScale;
            Ui.Panel(Root, "Tag", new Vector2(w, h), new Color(0.98f, 0.92f, 0.78f, 0.95f), h / 2, new Vector3(Left + w / 2, _y - h / 2, 0.0005f));
            var t = Ui.Text(Root, "TagText", text, Theme.Small, new Color(0.35f, 0.25f, 0.1f), w, TextAlignmentOptions.Center, new Vector3(Left + w / 2, _y - h / 2, 0), height: h);
            _y -= h + 0.008f;
            return t;
        }

        public void Gap(float metres = 0.012f) => _y -= metres;

        /// <summary>The width available to content (the panel minus its side padding).</summary>
        public float InnerWidth => Inner;

        /// <summary>Reserves a strip of the given height for custom content (tiles, thumbnails, a progress row) and
        /// returns a node at its centre: x runs right, y up, and the strip is InnerWidth wide.</summary>
        public Transform Block(float height)
        {
            var node = Ui.Node(Root, "Block", new Vector3(0, _y - height / 2, 0)).transform;
            _y -= height + 0.012f;
            return node;
        }

        /// <summary>A small green chip that says who something belongs to or is shared with (an audience, a room).</summary>
        public TextMeshPro Chip(string text)
        {
            var w = Mathf.Min(Inner, 0.02f + text.Length * Theme.Small * 0.56f * Theme.TextScale);
            var h = Theme.Small * 2f * Theme.TextScale;
            Ui.Panel(Root, "Chip", new Vector2(w, h), new Color(0.271f, 0.4f, 0.302f, 0.14f), h / 2, new Vector3(Left + w / 2, _y - h / 2, 0.0005f), new Color(0.271f, 0.4f, 0.302f, 0.45f), 0.0008f);
            var t = Ui.Text(Root, "ChipText", text, Theme.Small, Theme.Green, w, TextAlignmentOptions.Center, new Vector3(Left + w / 2, _y - h / 2, 0), FontStyles.Bold, h);
            _y -= h + 0.008f;
            return t;
        }

        /// <summary>The short green stroke under a headline (the references' underline accent).</summary>
        public void Underline(float width = 0.07f)
        {
            _y += 0.004f;
            Ui.Panel(Root, "Underline", new Vector2(width, 0.003f), Theme.Green, 0.0015f, new Vector3(Left + width / 2, _y - 0.0015f, 0.0004f), new Color(1, 1, 1, 0), 0);
            _y -= 0.014f;
        }

        /// <summary>A labelled on/off switch: the label on the left, a pill switch on the right. Returns the switch;
        /// pressing it flips the state and calls changed.</summary>
        public Pressable Switch(string id, string label, bool on, Action<bool> changed)
        {
            const float sw = 0.066f, sh = 0.036f;
            var rowH = ButtonHeight * 0.85f;
            var y = _y - rowH / 2;
            var text = Ui.Text(Root, "SwitchLabel", label, Theme.Body, Theme.Ink, Inner - sw - 0.01f, TextAlignmentOptions.Left, new Vector3(Left + (Inner - sw - 0.01f) / 2, y, 0), height: rowH);
            text.fontStyle = FontStyles.Normal;
            var state = on;
            Pressable p = null;
            Transform knob = null;
            void Show()
            {
                p.SetToggled(state);
                knob.localPosition = new Vector3(state ? sw / 2 - sh / 2 : -sw / 2 + sh / 2, 0, -0.0012f);
            }
            p = Pressable.Create(Root, id, "", new Vector2(sw, sh), () => { state = !state; Show(); changed?.Invoke(state); }, PressStyle.Chip,
                new Vector3(Left + Inner - sw / 2, y, 0));
            knob = Ui.Panel(p.transform, "Knob", new Vector2(sh - 0.008f, sh - 0.008f), Color.white, (sh - 0.008f) / 2, Vector3.zero, new Color(0, 0, 0, 0.12f), 0.0008f, Ui.OrderFill).transform;
            Show();
            Buttons.Add(p);
            _y -= rowH + 0.012f;
            return p;
        }

        public Pressable Button(string id, string label, Action onPress, PressStyle style = PressStyle.Secondary, float width = 0)
        {
            var w = width > 0 ? width : Inner;
            var p = Pressable.Create(Root, id, label, new Vector2(w, ButtonHeight), onPress, style, new Vector3(Left + w / 2, _y - ButtonHeight / 2, 0));
            Buttons.Add(p);
            _y -= ButtonHeight + 0.012f;
            return p;
        }

        /// <summary>Buttons side by side, sharing the width. Each entry: (id, label, action, style).</summary>
        public List<Pressable> Row(params (string id, string label, Action action, PressStyle style)[] items)
        {
            var list = new List<Pressable>();
            if (items.Length == 0) return list;
            const float gap = 0.012f;
            var w = (Inner - gap * (items.Length - 1)) / items.Length;
            for (var i = 0; i < items.Length; i++)
            {
                var x = Left + w / 2 + i * (w + gap);
                var it = items[i];
                var p = Pressable.Create(Root, it.id, it.label, new Vector2(w, ButtonHeight), it.action, it.style, new Vector3(x, _y - ButtonHeight / 2, 0));
                list.Add(p);
                Buttons.Add(p);
            }
            _y -= ButtonHeight + 0.012f;
            return list;
        }

        /// <summary>An image (photo, poster) at full inner width with the given aspect ratio.</summary>
        public MeshRenderer Image(Texture tex, float aspect = 1.6f, float maxHeight = 0.2f)
        {
            var w = Inner;
            var h = Mathf.Min(w / Mathf.Max(0.1f, aspect), maxHeight);
            if (h < w / aspect) w = h * aspect;
            var r = Ui.Image(Root, "Image", new Vector2(w, h), tex, 0.01f, new Vector3(0, _y - h / 2, 0.0003f));
            _y -= h + 0.012f;
            return r;
        }

        /// <summary>A thin progress bar (chapter progress, upload progress).</summary>
        public (Renderer track, Renderer fill) Progress(float value)
        {
            var h = 0.006f;
            var track = Ui.Panel(Root, "Track", new Vector2(Inner, h), new Color(0, 0, 0, 0.12f), h / 2, new Vector3(0, _y - h / 2, 0.0004f));
            var fw = Mathf.Max(h, Inner * Mathf.Clamp01(value));
            var fill = Ui.Panel(Root, "Fill", new Vector2(fw, h), Theme.Green, h / 2, new Vector3(Left + fw / 2, _y - h / 2, 0.0002f), order: Ui.OrderFill);
            _y -= h + 0.012f;
            return (track, fill);
        }
    }
}
