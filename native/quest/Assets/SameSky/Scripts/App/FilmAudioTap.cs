// Writes what the AudioListener hears to a WAV file while the demo film is being captured (FilmRig). Editor only.
// It sits on the listener's GameObject, where OnAudioFilterRead receives the finished mix: the same sound the person
// in the headset gets, spatialised from where their head is.

#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using UnityEngine;

namespace SameSky.App
{
    public sealed class FilmAudioTap : MonoBehaviour
    {
        readonly ConcurrentQueue<float[]> _blocks = new();
        volatile bool _on;
        FileStream _file;
        Thread _writer;
        long _samples;   // per channel

        public int SampleRate { get; private set; }
        public int Channels { get; private set; } = 2;
        public long Samples => _samples;

        public void Begin(string path)
        {
            End();
            SampleRate = AudioSettings.outputSampleRate;
            _samples = 0;
            _file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            _file.Write(new byte[44], 0, 44);   // the header is written at the end, when the length is known
            _on = true;
            _writer = new Thread(Drain) { IsBackground = true, Name = "Film sound" };
            _writer.Start();
        }

        public void End()
        {
            if (_file == null) return;
            _on = false;
            _writer?.Join(3000);
            _writer = null;
            Flush();
            var data = _file.Length - 44;
            _file.Seek(0, SeekOrigin.Begin);
            using (var w = new BinaryWriter(_file, System.Text.Encoding.ASCII, leaveOpen: true))
            {
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write((int)(36 + data)); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                w.Write(System.Text.Encoding.ASCII.GetBytes("fmt ")); w.Write(16); w.Write((short)1); w.Write((short)Channels);
                w.Write(SampleRate); w.Write(SampleRate * Channels * 2); w.Write((short)(Channels * 2)); w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write((int)data);
            }
            _file.Dispose();
            _file = null;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (!_on) return;
            Channels = channels;
            var copy = new float[data.Length];
            Buffer.BlockCopy(data, 0, copy, 0, data.Length * sizeof(float));
            _blocks.Enqueue(copy);
        }

        void Drain()
        {
            while (_on) { Flush(); Thread.Sleep(20); }
        }

        void Flush()
        {
            if (_file == null) return;
            while (_blocks.TryDequeue(out var block))
            {
                var bytes = new byte[block.Length * 2];
                for (var i = 0; i < block.Length; i++)
                {
                    var s = (short)Mathf.Clamp(block[i] * 32767f, -32768f, 32767f);
                    bytes[2 * i] = (byte)(s & 0xff);
                    bytes[2 * i + 1] = (byte)((s >> 8) & 0xff);
                }
                lock (_file) _file.Write(bytes, 0, bytes.Length);
                _samples += block.Length / Math.Max(1, Channels);
            }
        }

        void OnDestroy() => End();
    }
}
#endif
