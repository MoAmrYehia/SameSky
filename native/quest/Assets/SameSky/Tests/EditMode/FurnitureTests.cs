// Edit-mode checks for the furniture marketplace's rules (the catalogue the app bundles, what a stored choice
// resolves to, what is sent to the server) and for the skin the tracked hands wear.

using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SameSky.App;
using SameSky.Interaction;
using UnityEngine;

namespace SameSky.Tests
{
    public class FurnitureTests
    {
        [Test]
        public void Bundled_catalogue_covers_the_room_and_sells_nothing()
        {
            var c = FurnitureCatalog.Current;
            Assert.That(c.Version, Is.GreaterThan(0), "the catalogue must be bundled (scripts/native/build_native_assets.py)");
            CollectionAssert.AreEquivalent(new[] { "table", "chairs", "rug", "lamp", "plant", "walls", "floor" }, c.Slots.Select(s => s.Id).ToArray());
            foreach (var slot in c.Slots)
                foreach (var item in slot.Items)
                    foreach (var finish in item.Finishes)
                        Assert.NotNull(c.Finish(finish), $"{slot.Id}/{item.Id} comes in a finish the catalogue doesn't describe: {finish}");
            var text = Resources.Load<TextAsset>("furniture-catalog").text.ToLowerInvariant();
            foreach (var word in new[] { "price", "cost", "currency", "coins" })
                StringAssert.DoesNotContain(word, text, "every piece is included: the catalogue has no prices");
            StringAssert.Contains("no purchases", c.Note);
        }

        [Test]
        public void A_room_starts_as_the_first_look()
        {
            var c = FurnitureCatalog.Current;
            var first = c.Defaults();
            Assert.AreEqual(c.Slots.Count, first.Choices.Count);
            Assert.AreEqual(new DecorChoice("pedestal", "walnut"), first["table"]);
            Assert.AreEqual(new DecorChoice("leafy", null), first["plant"], "a piece without finishes has none chosen");
            Assert.That(c.Looks.Count, Is.GreaterThanOrEqualTo(3));
            Assert.IsTrue(c.Looks[0].Decor.Same(first), "the first look is the room as it comes");
            foreach (var look in c.Looks) Assert.AreEqual(c.Slots.Count, look.Decor.Choices.Count, $"look {look.Id} must fill every slot");
        }

        [Test]
        public void Stored_choices_the_catalogue_lacks_fall_back_to_the_room_as_it_comes()
        {
            var c = FurnitureCatalog.Current;
            var stored = JObject.Parse(@"{
                ""chairs"": { ""item"": ""tub"", ""finish"": ""sage"" },
                ""table"": { ""item"": ""glass-cube"", ""finish"": ""walnut"" },
                ""lamp"": { ""item"": ""globe"", ""finish"": ""neon"" },
                ""rug"": { ""item"": ""none"", ""finish"": null },
                ""plant"": { ""item"": ""snake"", ""finish"": ""walnut"" },
                ""walls"": { ""item"": ""plaster"" },
                ""ceiling"": { ""item"": ""mirror"", ""finish"": null }
            }");
            var d = c.Resolve(stored, 7);
            Assert.AreEqual(7, d.Version);
            Assert.AreEqual(new DecorChoice("tub", "sage"), d["chairs"], "a known piece in a known finish is kept");
            Assert.AreEqual(new DecorChoice("pedestal", "walnut"), d["table"], "an unknown piece falls back");
            Assert.AreEqual(new DecorChoice("drum", "brass"), d["lamp"], "an unknown finish falls back, piece and all");
            Assert.AreEqual(new DecorChoice("none", null), d["rug"], "no rug is a choice too");
            Assert.AreEqual(new DecorChoice("leafy", null), d["plant"], "a finish on a piece that has none is refused");
            Assert.AreEqual(new DecorChoice("plaster", "ivory"), d["walls"], "a missing finish means the piece's first");
            Assert.IsFalse(d.Choices.ContainsKey("ceiling"), "a slot the catalogue doesn't have is ignored");
            Assert.AreEqual(c.Slots.Count, d.Choices.Count);
        }

        [Test]
        public void Only_the_changed_slots_are_sent_to_the_server()
        {
            var c = FurnitureCatalog.Current;
            var saved = c.Defaults();
            var preview = saved.Clone();
            Assert.IsTrue(preview.Same(saved));
            Assert.AreEqual(0, preview.Changes(saved).Count);

            Assert.IsTrue(c.TryChoice("chairs", "tub", "cognac", out var chairs));
            Assert.IsTrue(c.TryChoice("rug", "none", null, out var rug));
            preview.Choices["chairs"] = chairs;
            preview.Choices["rug"] = rug;
            Assert.IsFalse(preview.Same(saved));
            Assert.AreEqual(new DecorChoice("tub", "cream"), saved["chairs"], "trying something on never touches what is kept");

            var changes = preview.Changes(saved);
            CollectionAssert.AreEquivalent(new[] { "chairs", "rug" }, changes.Properties().Select(p => p.Name).ToArray());
            Assert.AreEqual("cognac", (string)changes["chairs"]["finish"]);
            Assert.IsNull((string)changes["rug"]["finish"]);
            StringAssert.Contains("\"rug\":{\"item\":\"none\",\"finish\":null}", changes.ToString(Newtonsoft.Json.Formatting.None), "no finish goes over the wire as null");
            StringAssert.Contains("chairs=tub/cognac", preview.Summary());
            StringAssert.Contains("rug=none", preview.Summary());
        }

        [Test]
        public void Hand_skin_is_bundled_with_its_tones()
        {
            Assert.AreEqual(6, HandSkin.Tones.Length);
            Assert.That(HandSkin.DefaultTone, Is.InRange(0, HandSkin.Tones.Length - 1));
            for (var i = 1; i < HandSkin.Tones.Length; i++)
                Assert.Less(HandSkin.Tones[i].Tone.grayscale, HandSkin.Tones[i - 1].Tone.grayscale, "the tones run light to deep");
            Assert.IsTrue(HandSkin.TryParseTone("#C79C7E", out var tone), "an avatar's skin colour is a hex string");
            Assert.AreEqual(HandSkin.Tones[2].Tone, tone);
            Assert.IsFalse(HandSkin.TryParseTone("not a colour", out _));
            Assert.IsFalse(HandSkin.TryParseTone(null, out _));

            var baked = JObject.Parse(Resources.Load<TextAsset>("hand-skin").text);
            foreach (var side in new[] { "left", "right" })
            {
                Assert.That((int)baked[side]["vertices"], Is.GreaterThan(0), $"the {side} hand's maps were baked for a mesh");
                var material = Resources.Load<Material>(side == "left" ? "Materials/HandSkinLeft" : "Materials/HandSkinRight");
                Assert.NotNull(material, $"the {side} hand's skin material is bundled");
                Assert.AreEqual("SameSky/HandSkin", material.shader.name);
                Assert.NotNull(material.GetTexture("_MaskA"), "nails, palm, flush and creases");
                Assert.NotNull(material.GetTexture("_MaskB"), "detail normal and the wrist fade");
            }
        }
    }
}
