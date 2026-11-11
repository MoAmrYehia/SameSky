// Hand-operable text entry: a keyboard of poke keys laid out like a laptop keyboard just above the table edge,
// with the field being edited shown above it. Works the same with fingertips, point-and-pinch or controllers,
// in the headset and in Meta XR Simulator (the system keyboard isn't available to every runtime).
//   Kind.Text      letters, numbers, punctuation
//   Kind.Email     adds @ . .com shortcuts, no auto-capitals
//   Kind.Password  masked, with a Show toggle
//   Kind.Code      invite codes: only the 31 characters codes use, auto-dash after 4

using System;
using System.Collections.Generic;
using SameSky.Interaction;
using TMPro;
using UnityEngine;

namespace SameSky.UI
{
    public sealed class SpatialKeyboard : MonoBehaviour
    {
        public enum Kind { Text, Email, Password, Code }

        const float Key = 0.04f;
        const float Gap = 0.006f;
        const string CodeChars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

        public string Value { get; private set; } = "";
        public event Action<string> Submitted;
        public event Action Cancelled;
        public event Action<string> Changed;

        Kind _kind;
        int _max;
        bool _shift, _symbols, _show;
        TextMeshPro _field, _hint;
        Transform _keys;
        readonly List<Pressable> _letterKeys = new();
        string _label;

        public static SpatialKeyboard Open(Transform parent, string label, Kind kind, string initial = "", int maxLength = 120, string hint = null)
        {
            var go = new GameObject("Keyboard");
            go.transform.SetParent(parent, false);
            var k = go.AddComponent<SpatialKeyboard>();
            k._kind = kind;
            k._max = maxLength;
            k._label = label;
            k.Value = initial ?? "";
            k._shift = kind == Kind.Text && string.IsNullOrEmpty(initial);
            k.Build(hint);
            return k;
        }

        void Build(string hint)
        {
            var width = 11 * (Key + Gap) + 0.04f;
            // field above the keys
            var fieldGo = Ui.Node(transform, "Field", new Vector3(0, 0.215f, 0.02f)).transform;
            Ui.Panel(fieldGo, "FieldBg", new Vector2(width, 0.085f), Theme.Card, 0.016f);
            Ui.Text(fieldGo, "FieldLabel", _label, Theme.Small, Theme.Muted, width - 0.04f, TextAlignmentOptions.TopLeft, new Vector3(0, 0.03f, 0));
            _field = Ui.Text(fieldGo, "FieldValue", "", Theme.Title * 0.85f, Theme.Ink, width - 0.04f, TextAlignmentOptions.Left, new Vector3(0, -0.008f, 0), height: 0.04f);
            _field.overflowMode = TextOverflowModes.Ellipsis;
            _field.textWrappingMode = TextWrappingModes.NoWrap;
            _hint = Ui.Text(fieldGo, "Hint", hint ?? "", Theme.Small, Theme.Muted, width - 0.04f, TextAlignmentOptions.TopLeft, new Vector3(0, -0.032f, 0));

            Ui.Panel(transform, "KeysBg", new Vector2(width, 5 * (Key + Gap) + 0.03f), new Color(0.13f, 0.16f, 0.15f, 0.82f), 0.02f, new Vector3(0, -0.005f, 0.004f));
            _keys = Ui.Node(transform, "Keys").transform;
            Rebuild();
            Refresh();
        }

        void Rebuild()
        {
            Ui.Clear(_keys);
            _letterKeys.Clear();
            string[] rows = _kind == Kind.Code
                ? new[] { "23456789", "ABCDEFGHJK", "MNPQRSTUV", "WXYZ" }
                : _symbols
                    ? new[] { "1234567890", "@#&_-+()/", "*\"':;!?,.", "~=%$" }
                    : new[] { "1234567890", "qwertyuiop", "asdfghjkl", "zxcvbnm" };
            var y = 2 * (Key + Gap);
            for (var r = 0; r < rows.Length; r++)
            {
                var row = rows[r];
                var extra = r == 3 && _kind != Kind.Code ? 1.5f : 0f; // room for shift and backspace
                var x0 = -(row.Length - 1) * (Key + Gap) / 2;
                for (var i = 0; i < row.Length; i++)
                {
                    var ch = row[i];
                    var keyLabel = _shift && char.IsLetter(ch) ? char.ToUpperInvariant(ch).ToString() : ch.ToString();
                    var p = Pressable.Create(_keys, "key:" + ch, keyLabel, new Vector2(Key, Key), () => Type(ch), PressStyle.Key,
                        new Vector3(x0 + i * (Key + Gap), y, 0));
                    if (char.IsLetter(ch)) _letterKeys.Add(p);
                }
                if (r == 3 && _kind != Kind.Code)
                {
                    var right = x0 + (row.Length - 1) * (Key + Gap) + Key * 1.5f;
                    var left = x0 - Key * 1.5f;
                    Pressable.Create(_keys, "key:shift", _symbols ? "abc" : "Shift", new Vector2(Key * 1.8f, Key), () =>
                    {
                        if (_symbols) { _symbols = false; Rebuild(); }
                        else { _shift = !_shift; Rebuild(); }
                    }, PressStyle.Key, new Vector3(left, y, 0));
                    Pressable.Create(_keys, "key:backspace", "Delete", new Vector2(Key * 1.8f, Key), Backspace, PressStyle.Key, new Vector3(right, y, 0));
                }
                else if (r == 3)
                {
                    Pressable.Create(_keys, "key:backspace", "Delete", new Vector2(Key * 2.2f, Key), Backspace, PressStyle.Key,
                        new Vector3(x0 + row.Length * (Key + Gap) + Key * 0.7f, y, 0));
                }
                y -= Key + Gap;
            }

            // bottom row
            var bottom = new List<(string id, string label, Action act, float w, PressStyle s)>();
            bottom.Add(("key:cancel", "Cancel", () => Cancelled?.Invoke(), 2.2f, PressStyle.Quiet));
            if (_kind != Kind.Code) bottom.Add(("key:symbols", _symbols ? "abc" : "?123", () => { _symbols = !_symbols; _shift = false; Rebuild(); }, 1.6f, PressStyle.Key));
            if (_kind == Kind.Email)
            {
                bottom.Add(("key:at", "@", () => TypeText("@"), 1f, PressStyle.Key));
                bottom.Add(("key:dot", ".", () => TypeText("."), 1f, PressStyle.Key));
                bottom.Add(("key:dotcom", ".com", () => TypeText(".com"), 1.6f, PressStyle.Key));
            }
            else if (_kind == Kind.Password)
            {
                bottom.Add(("key:space", "space", () => TypeText(" "), 3.2f, PressStyle.Key));
                bottom.Add(("key:show", _show ? "Hide" : "Show", () => { _show = !_show; Rebuild(); Refresh(); }, 1.6f, PressStyle.Key));
            }
            else if (_kind == Kind.Text)
            {
                bottom.Add(("key:space", "space", () => TypeText(" "), 4.2f, PressStyle.Key));
                bottom.Add(("key:comma", ",", () => TypeText(","), 1f, PressStyle.Key));
                bottom.Add(("key:period", ".", () => TypeText("."), 1f, PressStyle.Key));
            }
            bottom.Add(("key:done", "Done", Submit, 2.2f, PressStyle.Primary));
            var total = 0f;
            foreach (var b in bottom) total += b.w * Key + Gap;
            var x = -total / 2;
            foreach (var b in bottom)
            {
                var w = b.w * Key;
                Pressable.Create(_keys, b.id, b.label, new Vector2(w, Key), b.act, b.s, new Vector3(x + w / 2, y, 0));
                x += w + Gap;
            }
        }

        void Type(char ch)
        {
            if (_kind == Kind.Code) ch = char.ToUpperInvariant(ch);
            var s = _shift && char.IsLetter(ch) ? char.ToUpperInvariant(ch).ToString() : ch.ToString();
            TypeText(s);
            if (_shift && _kind == Kind.Text) { _shift = false; Rebuild(); }
        }

        void TypeText(string s)
        {
            if (_kind == Kind.Code)
            {
                var raw = Value.Replace("-", "");
                foreach (var c in s.ToUpperInvariant())
                    if (CodeChars.IndexOf(c) >= 0 && raw.Length < 8) raw += c;
                Value = raw.Length > 4 ? raw.Substring(0, 4) + "-" + raw.Substring(4) : raw;
            }
            else if (Value.Length + s.Length <= _max) Value += s;
            Refresh();
        }

        void Backspace()
        {
            if (Value.Length == 0) return;
            Value = Value.Substring(0, Value.Length - 1);
            if (_kind == Kind.Code && Value.EndsWith("-")) Value = Value.Substring(0, Value.Length - 1);
            Refresh();
        }

        void Submit() => Submitted?.Invoke(_kind == Kind.Email ? Value.Trim() : Value);

        public void SetHint(string text, bool error = false)
        {
            if (_hint == null) return;
            _hint.text = text ?? "";
            _hint.color = error ? Theme.Danger : Theme.Muted;
        }

        void Refresh()
        {
            var shown = _kind == Kind.Password && !_show ? new string('•', Value.Length) : Value;
            _field.text = shown + "<color=#2f5d46>|</color>";
            Changed?.Invoke(Value);
        }
    }
}
