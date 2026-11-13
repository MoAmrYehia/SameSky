// How Adam and Maya look in the animated trailer. The app's two seated adults (Microsoft Rocketbox, MIT) are dressed
// for the film from recoloured copies of their own textures (scripts/demo/trailer_look.py writes them to
// demo-build/film/look): Maya in a muted terracotta cardigan over a cream top, Adam in navy knit and relaxed
// trousers, skin, hair and eyes as the brief describes them. The copies are loaded from disk onto in-memory
// materials, so nothing the app ships changes.

using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SameSky.Film
{
    public static class FilmLook
    {
        static readonly Dictionary<string, Texture2D> Textures = new();

        /// <summary>Where the film textures of the people are read from: the first trailer's by default; a film
        /// with its own look for them names its folder under demo-build.</summary>
        public static string Folder = "film/look";
        public static string LookDir => Path.Combine(FilmRenderer.RepoRoot, "demo-build", Folder);

        public static Texture2D Load(string file, bool linear = false, bool repeat = false)
        {
            if (Textures.TryGetValue(file, out var had) && had != null) return had;
            if (!File.Exists(file)) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear) { name = Path.GetFileNameWithoutExtension(file), hideFlags = HideFlags.HideAndDontSave };
            t.LoadImage(File.ReadAllBytes(file), false);
            t.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            t.anisoLevel = 8;
            t.filterMode = FilterMode.Trilinear;
            t.Apply(true, false);
            Textures[file] = t;
            return t;
        }

        /// <summary>Forget the textures read so far (they are read again from disk): after the look script has run.</summary>
        public static void Reload()
        {
            foreach (var t in Textures.Values) if (t != null) Object.DestroyImmediate(t);
            Textures.Clear();
        }

        /// <summary>Gives a character its film materials: one copy per slot (body, head, hair), with the film's
        /// textures where they exist.</summary>
        public static void Dress(FilmActor actor, float gloss = 0.74f, bool reflections = true, float hairGlow = 0f)
        {
            foreach (var r in actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                for (var i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var source = mats[i].name;
                    var m = new Material(mats[i]) { name = source + " (film)" };
                    var slot = source.Contains("_body") ? "body" : source.Contains("_head") ? "head" : "hair";
                    var colour = Load(Path.Combine(LookDir, $"{actor.Name}_{slot}.png"));
                    if (colour != null) m.SetTexture("_BaseMap", colour);
                    m.SetColor("_BaseColor", Color.white);
                    if (slot == "head")
                    {
                        // the film's head texture says how glossy each part is: skin a soft sheen, the eyes wet
                        if (colour != null)
                        {
                            m.SetFloat("_SmoothnessTextureChannel", 1f);
                            m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                            m.SetFloat("_Smoothness", gloss);
                        }
                        else m.SetFloat("_Smoothness", 0.3f);
                        m.SetFloat("_SpecularHighlights", 1f);
                        m.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
                        // (out of doors under a bright sky, wet eyes that mirror it go pale: a film set there asks for none)
                        m.SetFloat("_EnvironmentReflections", reflections ? 1f : 0f);
                        if (reflections) m.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF"); else m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                    }
                    else if (slot == "body") m.SetFloat("_Smoothness", 0.14f);
                    else if (hairGlow > 0f)
                    {
                        // a lock of hair that hangs in shadow is a flat card, and goes black: a little of its own
                        // colour keeps it hair
                        m.SetTexture("_EmissionMap", m.GetTexture("_BaseMap"));
                        m.SetColor("_EmissionColor", new Color(hairGlow, hairGlow * 0.92f, hairGlow * 0.88f));
                        m.EnableKeyword("_EMISSION");
                        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    }
                    // skin is never quite as dark as cloth in the shade: a faint warm glow of its own colour
                    var glow = slot == "hair" ? null : Load(Path.Combine(LookDir, $"{actor.Name}_{slot}_glow.png"));
                    if (glow != null)
                    {
                        m.SetTexture("_EmissionMap", glow);
                        m.SetColor("_EmissionColor", new Color(0.085f, 0.058f, 0.05f) * SkinGlow);
                        m.EnableKeyword("_EMISSION");
                        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    }
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
        }

        /// <summary>How much skin lifts in the shade (1 as designed).</summary>
        public static float SkinGlow = 1f;
    }
}
