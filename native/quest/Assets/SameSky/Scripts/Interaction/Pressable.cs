// A hand-operable button built on Meta's Interaction SDK:
//   * PokeInteractable  - press it with an index fingertip (near, within seated reach)
//   * RayInteractable   - point at it and pinch (far)
// Both share one clipped plane surface sized to the button. A press counts when the finger or pinch releases
// while still on the button (the same rule as a UI click), so a press that slides off never fires. Nor does a hand
// that loses tracking mid-press: the SDK then disables its interactors, which unselects and unhovers in the same
// frame (no Cancel event), so a release is only confirmed a frame later, if no unhover came with it. Controllers
// drive the same interactables.

using System;
using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using SameSky.UI;
using TMPro;
using UnityEngine;

namespace SameSky.Interaction
{
    /// <summary>Surface: an invisible press area over a physical thing (a photo, a folded card); a faint wash on hover.</summary>
    public enum PressStyle { Primary, Secondary, Quiet, Key, Danger, Chip, Dark, DockItem, Surface }

    public sealed class Pressable : MonoBehaviour
    {
        /// <summary>Stable identity for logs and automated checks, e.g. "button:Play our day".</summary>
        public string Id;
        public event Action Pressed;

        public static event Action<Pressable, string> AnyEvent; // (pressable, "hover"|"select"|"press"|"cancel"), for the test probe

        public TextMeshPro Label { get; private set; }
        public Vector2 Size { get; private set; }
        public bool Toggled { get; private set; }
        /// <summary>A fingertip or ray is on the button now (for the test probe).</summary>
        public bool Hovered => _interactable && _hovering.Count > 0;
        /// <summary>A fingertip is pushed in, or a pinch is held, on the button now.</summary>
        public bool Selected => _interactable && _selecting.Count > 0;

        Renderer _bg;
        PressStyle _style;
        PokeInteractable _poke;
        RayInteractable _ray;
        readonly HashSet<int> _hovering = new();
        readonly HashSet<int> _selecting = new();
        readonly Dictionary<int, int> _releasedAt = new(); // pointer id -> frame of a release still on the button
        float _lastPress = -10f;
        bool _interactable = true;
        Transform _face;
        static AudioClip _click, _tick;

        public bool Interactable
        {
            get => _interactable;
            set
            {
                _interactable = value;
                if (_poke) _poke.enabled = value;
                if (_ray) _ray.enabled = value;
                if (!value) { _hovering.Clear(); _selecting.Clear(); _releasedAt.Clear(); }
                Redraw();
            }
        }

        public static Pressable Create(Transform parent, string id, string text, Vector2 size, Action onPress,
            PressStyle style = PressStyle.Secondary, Vector3 localPos = default)
        {
            var go = new GameObject("Pressable " + id);
            go.SetActive(false); // Interaction SDK components must be injected before they wake up
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var p = go.AddComponent<Pressable>();
            p.Id = id;
            p.Size = size;
            p._style = style;
            p.Build(text);
            if (onPress != null) p.Pressed += onPress;
            go.SetActive(true);
            p.Redraw();
            return p;
        }

        void Build(string text)
        {
            _face = Ui.Node(transform, "Face").transform;
            // pills, as in the references; keyboard keys stay rounded squares
            var radius = _style switch { PressStyle.Key => 0.008f, PressStyle.Surface => 0.01f, _ => Size.y * 0.5f };
            _bg = Ui.Panel(_face, "Background", Size, Fill(false, false), radius,
                border: _style is PressStyle.DockItem or PressStyle.Dark or PressStyle.Surface ? new Color(1, 1, 1, 0f) : null);
            Label = Ui.Text(_face, "Label", text, _style == PressStyle.Key ? Theme.Button * 1.1f : Theme.Button,
                TextColor(), Size.x - 0.012f, TextAlignmentOptions.Center, height: Size.y);
            Label.fontStyle = _style is PressStyle.Primary or PressStyle.Danger or PressStyle.Dark ? FontStyles.Bold : FontStyles.Normal;
            Label.textWrappingMode = TextWrappingModes.Normal;
            Label.overflowMode = TextOverflowModes.Ellipsis;

            // Interaction SDK surface: a plane at the button face whose normal points toward the person (-Z).
            var surfaceGo = Ui.Node(transform, "Surface");
            var plane = surfaceGo.AddComponent<PlaneSurface>();
            plane.InjectAllPlaneSurface(PlaneSurface.NormalFacing.Backward, false);
            var clipper = surfaceGo.AddComponent<BoundsClipper>();
            clipper.Position = Vector3.zero;
            clipper.Size = new Vector3(Size.x, Size.y, 0.02f);
            var clipped = surfaceGo.AddComponent<ClippedPlaneSurface>();
            clipped.InjectAllClippedPlaneSurface(plane, new IBoundsClipper[] { clipper });

            _poke = gameObject.AddComponent<PokeInteractable>();
            _poke.InjectAllPokeInteractable(clipped);
            _ray = gameObject.AddComponent<RayInteractable>();
            _ray.InjectAllRayInteractable(clipped);
            _poke.WhenPointerEventRaised += OnPointer;
            _ray.WhenPointerEventRaised += OnPointer;

            // Desktop fallback (Editor without a headset) and a target for gaze/debug tooling.
            var col = gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(Size.x, Size.y, 0.01f);
            col.isTrigger = true;
        }

        void OnDestroy()
        {
            if (_poke) _poke.WhenPointerEventRaised -= OnPointer;
            if (_ray) _ray.WhenPointerEventRaised -= OnPointer;
        }

        void OnPointer(PointerEvent e)
        {
            if (!_interactable) return;
            switch (e.Type)
            {
                case PointerEventType.Hover:
                    if (_hovering.Add(e.Identifier) && _hovering.Count == 1) { Feedback(false); AnyEvent?.Invoke(this, "hover"); }
                    break;
                case PointerEventType.Unhover:
                    _hovering.Remove(e.Identifier);
                    // unselected and unhovered in one frame: the interactor was disabled (tracking lost), not released
                    if (_releasedAt.TryGetValue(e.Identifier, out var frame) && frame == Time.frameCount)
                    {
                        _releasedAt.Remove(e.Identifier);
                        AnyEvent?.Invoke(this, "cancel");
                    }
                    break;
                case PointerEventType.Select:
                    _selecting.Add(e.Identifier);
                    _releasedAt.Remove(e.Identifier);
                    AnyEvent?.Invoke(this, "select");
                    break;
                case PointerEventType.Unselect:
                    if (_selecting.Remove(e.Identifier) && _hovering.Contains(e.Identifier)) _releasedAt[e.Identifier] = Time.frameCount;
                    break;
                case PointerEventType.Cancel:
                    if (_selecting.Remove(e.Identifier)) AnyEvent?.Invoke(this, "cancel");
                    _releasedAt.Remove(e.Identifier);
                    _hovering.Remove(e.Identifier);
                    break;
            }
            Redraw();
        }

        void LateUpdate()
        {
            if (_releasedAt.Count == 0) return;
            // a release from an earlier frame with no unhover beside it: a real press
            List<int> due = null;
            foreach (var kv in _releasedAt)
                if (kv.Value < Time.frameCount) (due ??= new List<int>()).Add(kv.Key);
            if (due == null) return;
            foreach (var id in due) _releasedAt.Remove(id);
            Press();
        }

        /// <summary>Fires the press (also used by the desktop fallback). Debounced against double triggers.</summary>
        public void Press()
        {
            if (!_interactable || !isActiveAndEnabled || Time.unscaledTime - _lastPress < 0.3f) return;
            _lastPress = Time.unscaledTime;
            Feedback(true);
            AnyEvent?.Invoke(this, "press");
            Pressed?.Invoke();
        }

        public void SetText(string text)
        {
            if (Label) Label.text = text;
        }

        public void SetToggled(bool on)
        {
            Toggled = on;
            Redraw();
        }

        void Update()
        {
            // pressed-in depth while selecting: the face recedes a few millimetres, like a real key
            var target = _selecting.Count > 0 ? 0.004f : 0f;
            var p = _face.localPosition;
            p.z = Mathf.MoveTowards(p.z, target, Time.unscaledDeltaTime * 0.08f);
            _face.localPosition = p;
        }

        void Redraw()
        {
            if (_bg == null) return;
            var hover = _hovering.Count > 0 && _interactable;
            Ui.SetFill(_bg, Fill(hover, _selecting.Count > 0), hover ? 1f : 0f);
            if (Label) Label.color = _interactable ? TextColor() : new Color(TextColor().r, TextColor().g, TextColor().b, 0.4f);
        }

        Color Fill(bool hover, bool down)
        {
            Color c = _style switch
            {
                PressStyle.Primary => Theme.Green,
                PressStyle.Danger => Theme.Danger,
                PressStyle.Quiet => new Color(1, 1, 1, 0.22f),
                PressStyle.Chip => Toggled ? Theme.Green : new Color(1, 1, 1, 0.7f),
                PressStyle.Key => new Color(0.98f, 0.97f, 0.95f, 0.95f),
                PressStyle.Dark => Theme.GreenDark,
                PressStyle.DockItem => new Color(1, 1, 1, 0f),   // the dock's bar is the background
                PressStyle.Surface => new Color(1, 1, 1, 0f),    // the photo or card is the button
                _ => Toggled ? Theme.GreenSoft : Theme.Neutral,
            };
            if (_style is PressStyle.DockItem or PressStyle.Surface)
                return down ? new Color(1, 1, 1, 0.22f) : hover ? new Color(1, 1, 1, 0.12f) : c;
            if (!_interactable) c.a *= 0.45f;
            if (down) c = Color.Lerp(c, Color.black, 0.15f);
            else if (hover) c = Color.Lerp(c, Color.white, 0.12f);
            return c;
        }

        Color TextColor() => _style switch
        {
            PressStyle.Primary or PressStyle.Danger or PressStyle.Dark or PressStyle.DockItem => Theme.OnDark,
            PressStyle.Chip => Toggled ? Theme.OnDark : Theme.Ink,
            _ => Theme.Ink,
        };

        void Feedback(bool press)
        {
            _click ??= Tone(1400f, 0.03f, 0.35f);
            _tick ??= Tone(2200f, 0.012f, 0.12f);
            if (SameSky.App.Prefs.Current.soundOn)
                AudioSource.PlayClipAtPoint(press ? _click : _tick, transform.position, SameSky.App.Prefs.Current.volume * (press ? 0.5f : 0.25f));
        }

        static AudioClip Tone(float hz, float seconds, float gain)
        {
            const int rate = 44100;
            var n = Mathf.CeilToInt(rate * seconds);
            var data = new float[n];
            for (var i = 0; i < n; i++)
            {
                var t = (float)i / rate;
                var env = Mathf.Exp(-t / (seconds * 0.25f));
                data[i] = Mathf.Sin(2 * Mathf.PI * hz * t) * env * gain;
            }
            var clip = AudioClip.Create($"tone{hz}", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
