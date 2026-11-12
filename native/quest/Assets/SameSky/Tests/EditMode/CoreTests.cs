// Edit-mode checks for the native app's logic that doesn't need a headset: data contracts, the bundled sample
// and its honesty labels, configuration rules, recordings, invite codes and shared-playback timing.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using SameSky.Api;
using SameSky.App;
using UnityEngine;

namespace SameSky.Tests
{
    public class CoreTests
    {
        [Test]
        public void Sample_day_loads_offline_with_honest_labels()
        {
            var data = new SampleData();
            data.Load().Wait();
            Assert.AreEqual("Adam", data.Me.DisplayName);
            Assert.AreEqual(2, data.Rooms.Count);
            var voice = data.FindMoment(SampleData.MayaVoiceNote);
            Assert.NotNull(voice);
            Assert.AreEqual("synthesized", voice.Media.Origin, "the sample voice note must say it is synthesized");
            Assert.That(voice.Media.Captions, Is.Not.Empty, "the voice note needs timed captions");
            Assert.AreEqual("Maya", voice.Media.Captions[0].Speaker);
            var clip = data.FindMoment("m-adam-ride");
            Assert.AreEqual("simulated", clip.Media.Origin, "the sample clip must say it is a simulated camera pan");
            var recaps = data.Recaps(SampleData.DirectRoom).Result;
            Assert.AreEqual("sample", recaps[0].Generator);
            StringAssert.Contains("no AI call", recaps[0].GeneratorLabel);
            Assert.AreEqual("Synthesized voice (sample)", SameSkyApp.OriginLabel(voice.Media));
        }

        [Test]
        public void Sample_media_is_bundled_for_offline_use()
        {
            var data = new SampleData();
            data.Load().Wait();
            foreach (var m in data.Rooms.SelectMany(r => data.Moments(r.Id).Result).Where(m => m.Media != null))
            {
                Assert.That(m.Media.Url, Does.StartWith("res:"), $"{m.Id} must not need the network");
                var path = m.Media.Url.Substring(4);
                var asset = m.Media.Kind switch
                {
                    "image" => (UnityEngine.Object)Resources.Load<Texture2D>(path),
                    "audio" => Resources.Load<AudioClip>(path),
                    _ => Resources.Load<UnityEngine.Video.VideoClip>(path),
                };
                Assert.NotNull(asset, $"{m.Id}: {path} missing from Resources");
            }
        }

        [Test]
        public void Sample_replies_stay_in_the_sample_store()
        {
            var data = new SampleData();
            data.Load().Wait();
            SampleState.Reset();
            var key = "q-test-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            data.AddReply(SampleData.MayaVoiceNote, "See you at the bench", key).Wait();
            data.AddReply(SampleData.MayaVoiceNote, "See you at the bench", key).Wait(); // a retry is the same reply
            Assert.AreEqual(1, SampleState.Current.replies.Count(r => r.id == key));
            Assert.IsFalse(File.Exists(Path.Combine(Application.persistentDataPath, "session.json")) && StoredSession.Load().token == key);
            SampleState.Reset();
        }

        [Test]
        public void Api_shapes_parse_from_server_json()
        {
            const string json = @"{""id"":""m1"",""roomId"":""r1"",""ownerId"":""u2"",""ownerName"":""Sam"",""kind"":""voice"",""caption"":""Back home"",
                ""captureAt"":""2026-11-11T09:00:00+00:00"",""captureLocal"":{""time"":""17:00"",""date"":""2026-11-11"",""zone"":""Asia/Shanghai"",""abbreviation"":""CST""},
                ""roomDate"":""2026-11-11"",""sharingState"":""shared"",""sourceVersion"":1,""sharingVersion"":1,""audience"":[{""userId"":""u1"",""displayName"":""Nora""}],
                ""isMine"":false,""reactions"":[],""media"":{""id"":""a1"",""kind"":""audio"",""processingState"":""ready"",""sourceDevice"":""headset"",""projection"":""flat"",
                ""durationMs"":2100,""width"":null,""height"":null,""url"":""/v1/media/a1/display?u=u1&e=1&s=x"",""oggUrl"":""/v1/media/a1/ogg?u=u1&e=1&s=y""}}";
            var m = JsonConvert.DeserializeObject<Moment>(json);
            Assert.AreEqual("voice", m.Kind);
            Assert.AreEqual("headset", m.Media.SourceDevice);
            Assert.AreEqual("Headset", SameSkyApp.SourceLabel(m.Media));
            Assert.That(m.Media.OggUrl, Does.Contain("/ogg?"));
            Assert.AreEqual("CST", m.CaptureLocal.Abbreviation);
            var api = new ApiClient("http://127.0.0.1:8000/");
            Assert.AreEqual("http://127.0.0.1:8000/v1/media/a1/ogg?u=u1&e=1&s=y", api.Absolute(m.Media.OggUrl));
            Assert.AreEqual("ws://127.0.0.1:8000/v1/realtime", api.RealtimeUrl());
            Assert.AreEqual("wss://api.example.com/v1/realtime", new ApiClient("https://api.example.com").RealtimeUrl());
        }

        [Test]
        public void Only_https_or_development_http_is_allowed()
        {
            Assert.IsTrue(AppConfig.IsAllowed("https://api.example.com"));
            Assert.IsFalse(AppConfig.IsAllowed("ftp://example.com"));
            Assert.IsFalse(AppConfig.IsAllowed(""));
            Assert.IsTrue(AppConfig.IsAllowed("http://127.0.0.1:8000"), "the Editor is a development context");
        }

        [Test]
        public void Wav_recordings_have_a_valid_pcm_header()
        {
            var samples = Enumerable.Range(0, 2400).Select(i => Mathf.Sin(i * 0.1f) * 0.5f).ToArray();
            var wav = Wav.Encode(samples, 1, 24000);
            Assert.AreEqual("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
            Assert.AreEqual("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));
            Assert.AreEqual(24000, BitConverter.ToInt32(wav, 24));
            Assert.AreEqual(44 + samples.Length * 2, wav.Length);
        }

        [Test]
        public void Shared_playback_follows_the_server_clock()
        {
            var rt = new RealtimeClient("ws://127.0.0.1:1/v1/realtime", "s1", () => "t");
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var setPlayback = typeof(RealtimeClient).GetProperty("Playback")!.GetSetMethod(true)!;
            setPlayback.Invoke(rt, new object[] { new PlaybackState { Version = 3, PlayheadMs = 5000, Paused = true, StateAt = now - 2000, ServerTime = now } });
            Assert.AreEqual(5000, rt.PlayheadNowMs(), 1, "paused: the playhead does not move");
            setPlayback.Invoke(rt, new object[] { new PlaybackState { Version = 4, PlayheadMs = 5000, Paused = false, StateAt = now - 2000, ServerTime = now } });
            Assert.AreEqual(7000, rt.PlayheadNowMs(), 150, "playing: the playhead advances from the server stamp");
            rt.Dispose();
        }

        [Test]
        public void Reply_keys_are_stable_for_retries()
        {
            Assert.AreEqual(Ids.ReplyKey("m1", "hello"), Ids.ReplyKey("m1", "hello"));
            Assert.AreNotEqual(Ids.ReplyKey("m1", "hello"), Ids.ReplyKey("m2", "hello"));
            Assert.LessOrEqual(Ids.ReplyKey("m1", "hello").Length, 64, "the API accepts client keys up to 64 characters");
        }
    }
}
