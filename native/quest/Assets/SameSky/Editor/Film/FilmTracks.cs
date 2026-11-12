// Keyframes for the animated trailer: a value over time with an easing per key. Everything that moves in a shot (a
// hand's goal, a head turn, a smile, the camera) is one of these, written out in FilmShots.cs.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SameSky.Film
{
    public enum Ease { Linear, InOut, In, Out, Soft, Overshoot, Hold }

    public static class Easing
    {
        public static float Apply(Ease e, float x)
        {
            x = Mathf.Clamp01(x);
            switch (e)
            {
                case Ease.Linear: return x;
                case Ease.In: return x * x;
                case Ease.Out: return 1f - (1f - x) * (1f - x);
                case Ease.Soft: return x * x * x * (x * (6f * x - 15f) + 10f);            // slow out of and into rest
                case Ease.Overshoot: { var s = 1.4f; var y = x - 1f; return y * y * ((s + 1f) * y + s) + 1f; }   // settles past, then back
                case Ease.Hold: return x >= 1f ? 1f : 0f;
                default: return x * x * (3f - 2f * x);
            }
        }
    }

    /// <summary>A number over time. The easing of a key shapes the way into it.</summary>
    public sealed class Track
    {
        readonly List<(float t, float v, Ease e)> _keys = new();
        public Track() { }
        public Track(float constant) { _keys.Add((0f, constant, Ease.InOut)); }
        /// <summary>Adds a key (a key already at that time is replaced).</summary>
        public Track Key(float t, float v, Ease e = Ease.InOut)
        {
            _keys.RemoveAll(k => Mathf.Abs(k.t - t) < 1e-4f);
            _keys.Add((t, v, e));
            _keys.Sort((a, b) => a.t.CompareTo(b.t));
            return this;
        }
        public bool Empty => _keys.Count == 0;

        public float At(float t, float fallback = 0f)
        {
            if (_keys.Count == 0) return fallback;
            if (t <= _keys[0].t) return _keys[0].v;
            for (var i = 1; i < _keys.Count; i++)
            {
                if (t > _keys[i].t) continue;
                var a = _keys[i - 1];
                var b = _keys[i];
                return Mathf.LerpUnclamped(a.v, b.v, Easing.Apply(b.e, (t - a.t) / Mathf.Max(1e-5f, b.t - a.t)));
            }
            return _keys[_keys.Count - 1].v;
        }
    }

    /// <summary>A point over time.</summary>
    public sealed class Track3
    {
        readonly List<(float t, Vector3 v, Ease e)> _keys = new();
        public Track3 Key(float t, Vector3 v, Ease e = Ease.InOut)
        {
            _keys.RemoveAll(k => Mathf.Abs(k.t - t) < 1e-4f);
            _keys.Add((t, v, e));
            _keys.Sort((a, b) => a.t.CompareTo(b.t));
            return this;
        }
        public Track3 Key(float t, float x, float y, float z, Ease e = Ease.InOut) => Key(t, new Vector3(x, y, z), e);
        public bool Empty => _keys.Count == 0;

        public Vector3 At(float t)
        {
            if (_keys.Count == 0) return Vector3.zero;
            if (t <= _keys[0].t) return _keys[0].v;
            for (var i = 1; i < _keys.Count; i++)
            {
                if (t > _keys[i].t) continue;
                var a = _keys[i - 1];
                var b = _keys[i];
                return Vector3.LerpUnclamped(a.v, b.v, Easing.Apply(b.e, (t - a.t) / Mathf.Max(1e-5f, b.t - a.t)));
            }
            return _keys[_keys.Count - 1].v;
        }
    }

    /// <summary>Small steady life: a slow, never-repeating drift for breathing, weight shifts and a held hand.</summary>
    public static class Drift
    {
        public static float Wave(float t, float period, float phase = 0f) => Mathf.Sin((t / period + phase) * Mathf.PI * 2f);
        public static float Wander(float t, float seed) =>
            (Mathf.PerlinNoise(t * 0.37f + seed * 13.1f, seed * 7.7f) - 0.5f) * 2f;
    }
}
