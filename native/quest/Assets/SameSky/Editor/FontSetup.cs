// Text and icon fonts for the native app, from the same faces as the companion:
//   Figtree (SIL Open Font License 1.1; the companion ships @fontsource-variable/figtree). TextMesh Pro reads static
//   fonts, so Art/Fonts/Figtree holds Regular/SemiBold/Bold instances of the variable font (fontTools instancer).
//   Lucide icons (ISC licence; lucide-static 0.460.0, Art/Fonts/Lucide).
// Creates dynamic TextMesh Pro font assets in Resources/Fonts. "SameSkySans SDF" is the app's default (Ui.Font loads
// it): its weight table points bold and semibold text at the real faces, and the icon font is its fallback, so a
// label can mix words and icons (UI/Icons.cs).

using System.Collections.Generic;
using System.IO;
using SameSky.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace SameSky.EditorTools
{
    public static class FontSetup
    {
        const string Source = "Assets/SameSky/Art/Fonts";
        const string Out = "Assets/SameSky/Resources/Fonts";
        const string Latin = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
                             " ·•–—‘’“”…°×éèàçñüöäÉ";

        public static void Run()
        {
            Directory.CreateDirectory(Out);
            var icons = Create($"{Source}/Lucide/lucide.ttf", "SameSkyIcons SDF", 64, 6, Icons.All);
            var semi = Create($"{Source}/Figtree/Figtree-SemiBold.ttf", "SameSkySans SemiBold SDF", 90, 9, Latin);
            var bold = Create($"{Source}/Figtree/Figtree-Bold.ttf", "SameSkySans Bold SDF", 90, 9, Latin);
            var regular = Create($"{Source}/Figtree/Figtree-Regular.ttf", "SameSkySans SDF", 90, 9, Latin);
            if (regular == null) { Debug.LogWarning("[SameSky:setup] Figtree missing: text stays in TextMesh Pro's default font"); return; }
            foreach (var f in new[] { regular, semi, bold })
            {
                if (f == null || icons == null) continue;
                f.fallbackFontAssetTable = new List<TMP_FontAsset> { icons };
                EditorUtility.SetDirty(f);
            }
            var weights = regular.fontWeightTable;
            if (semi != null) weights[6].regularTypeface = semi;   // 600
            if (bold != null) weights[7].regularTypeface = bold;   // 700: <b> and FontStyles.Bold use the real bold face
            EditorUtility.SetDirty(regular);
            AssetDatabase.SaveAssets();
            Debug.Log("[SameSky:setup] fonts: Figtree (regular, semibold, bold) with Lucide icons");
        }

        static TMP_FontAsset Create(string ttf, string name, int pointSize, int padding, string prebuild)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
            if (font == null) { Debug.LogWarning($"[SameSky:setup] font {ttf} missing"); return null; }
            var path = $"{Out}/{name}.asset";
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (fa == null)
            {
                fa = TMP_FontAsset.CreateFontAsset(font, pointSize, padding, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                fa.name = name;
                AssetDatabase.CreateAsset(fa, path);
                fa.atlasTextures[0].name = name + " Atlas";
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
                fa.material.name = name + " Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
            }
            fa.TryAddCharacters(prebuild, out _);   // the common glyphs are in the atlas from the start
            EditorUtility.SetDirty(fa);
            return fa;
        }
    }
}
