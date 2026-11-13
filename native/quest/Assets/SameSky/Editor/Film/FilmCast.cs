// The cast and the motion library of the animated film "Wish You Were Here" (FilmWish*.cs).
//
// Motion: clips of the Microsoft Rocketbox animation library (MIT), kept in Editor/Film/Cast/Clips. They are imported
// as Humanoid, so any of the figures can play any of them, with the forward travel of a walk taken out of the pose:
// a figure walks on the spot and the shot moves it along its path at the clip's own speed (FilmActor.Walk).
// People: the two leads are the film's Adam and Maya (the same two Rocketbox adults as in the first trailer, dressed
// by FilmLook), standing this time; the background people are four more Rocketbox adults, kept in
// Editor/Film/Cast/People with their colour textures. Everything here is editor-only and is fetched by
// scripts/demo/wish_fetch.py and wish_cast.py, not kept in git.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SameSky.Film
{
    public static class FilmCast
    {
        const string Dir = "Assets/SameSky/Editor/Film/Cast";
        const string Leads = "Assets/SameSky/Art/Characters/Rocketbox";
        static readonly Dictionary<string, AnimationClip> Clips = new();

        // clips that end (everything else loops)
        static bool Once(string name) =>
            name.Contains("_turn_") || name.Contains("sit_down") || name.Contains("walk_start") || name.Contains("walk_stop") ||
            name.Contains("laugh") || name.Contains("wave") || name.Contains("touch_hair") || name.Contains("look_around");

        // clips whose travel across the ground is taken out of the pose (the shot moves the figure instead)
        static bool Travels(string name) => name.Contains("_walk_") || name.Contains("_run_") || name.Contains("_turn_");

        /// <summary>A motion clip by its Rocketbox name ("m_walk_neutral_01"), imported the way the film needs it.</summary>
        public static AnimationClip Clip(string name)
        {
            if (Clips.TryGetValue(name, out var had) && had != null) return had;
            var path = $"{Dir}/Clips/{name}.fbx";
            if (!File.Exists(path)) path = $"{Leads}/Animations/{name}.fbx";
            if (AssetImporter.GetAtPath(path) is not ModelImporter mi) throw new InvalidOperationException("no motion clip " + name + " (run scripts/demo/wish_fetch.py and wish_cast.py)");
            if (path.StartsWith(Dir)) Configure(mi, name);
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clip == null) throw new InvalidOperationException("no animation in " + path);
            Clips[name] = clip;
            return clip;
        }

        static void Configure(ModelImporter mi, string name)
        {
            var loop = !Once(name);
            var travels = Travels(name);
            var turns = name.Contains("_turn_");
            var ok = mi.animationType == ModelImporterAnimationType.Human && mi.clipAnimations.Length == 1 && mi.clipAnimations[0].name == name
                && mi.clipAnimations[0].loopTime == loop && mi.clipAnimations[0].lockRootPositionXZ == !travels && mi.clipAnimations[0].lockRootRotation == !turns;
            if (ok) return;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.SaveAndReimport();   // a humanoid's default clip settings exist only after an import
            var c = mi.defaultClipAnimations.First();
            c.name = name;
            c.loopTime = loop;
            c.loopPose = loop;
            c.lockRootRotation = !turns;          // a turn is made by the shot, like the travel
            c.keepOriginalOrientation = true;
            c.lockRootHeightY = true;
            c.keepOriginalPositionY = true;
            c.lockRootPositionXZ = !travels;
            c.keepOriginalPositionXZ = true;
            mi.clipAnimations = new[] { c };
            mi.SaveAndReimport();
        }

        /// <summary>How fast a walking clip covers ground, in metres a second at normal speed.</summary>
        public static float Pace(string name)
        {
            var clip = Clip(name);
            var v = new Vector2(clip.averageSpeed.x, clip.averageSpeed.z).magnitude;
            return v > 0.05f ? v : 1.25f;
        }

        // ------------------------------------------------------------ people
        static GameObject Model(string path)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter mi) throw new InvalidOperationException("no figure at " + path + " (run scripts/demo/wish_fetch.py and wish_cast.py)");
            if (mi.animationType != ModelImporterAnimationType.Human || mi.importAnimation || mi.importCameras || mi.importLights)
            {
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.importAnimation = false;
                mi.importBlendShapes = true;
                mi.importCameras = false;
                mi.importLights = false;
                mi.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static Material Skin(string name, Texture colour, float smoothness, bool cutout)
        {
            var m = new Material(FilmStage.LitShader) { name = name };
            m.SetTexture("_BaseMap", colour);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            if (cutout)
            {
                m.SetFloat("_AlphaClip", 1);
                m.SetFloat("_Cutoff", 0.4f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", (float)CullMode.Off);
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            return m;
        }

        // each part of a figure's mesh by what it is: clothes and body, face and hands, hair and lashes
        static void Dress(GameObject figure, Material body, Material head, Material hair)
        {
            foreach (var r in figure.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                for (var i = 0; i < mats.Length; i++)
                {
                    var n = mats[i] != null ? mats[i].name.ToLowerInvariant() : "";
                    mats[i] = n.Contains("opacity") || n.Contains("hair") || n.Contains("lash") ? (hair != null ? hair : head) : n.Contains("head") ? head : body;
                }
                r.sharedMaterials = mats;
                r.quality = SkinQuality.Bone4;
            }
        }

        /// <summary>One of the two leads, standing: "adam" or "maya". FilmStage.Lead dresses them for the film.</summary>
        public static GameObject Lead(string who, Transform parent)
        {
            var (folder, prefix) = who == "maya" ? ("Female_Adult_14", "f017") : ("Male_Adult_07", "m013");
            var figure = UnityEngine.Object.Instantiate(Model($"{Leads}/{folder}/{folder}_facial.fbx"), parent);
            figure.name = who;
            Material Made(string slot) => AssetDatabase.LoadAssetAtPath<Material>($"{Leads}/Generated/{prefix}_{slot}.mat");
            Dress(figure, Made("body"), Made("head"), Made("opacity"));
            return figure;
        }

        public static readonly string[] Extras = { "Female_Adult_02", "Female_Adult_09", "Male_Adult_03", "Male_Adult_12" };

        /// <summary>A background person, standing. <paramref name="tint"/> shifts the colour of their clothes a little, so
        /// the same figure can pass as someone else far from the camera.</summary>
        public static GameObject Extra(string person, Transform parent, Color? tint = null)
        {
            var dir = $"{Dir}/People/{person}";
            var figure = UnityEngine.Object.Instantiate(Model($"{dir}/{person}.fbx"), parent);
            figure.name = person;
            Texture Tex(string file) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{file}");
            var body = Skin(person + " body", Tex("body.jpg"), 0.12f, false);
            if (tint.HasValue) body.SetColor("_BaseColor", tint.Value);
            var head = Skin(person + " head", Tex("head.jpg"), 0.3f, false);
            var hairTex = Tex("hair.png");
            var hair = hairTex != null ? Skin(person + " opacity", hairTex, 0.1f, true) : null;
            Dress(figure, body, head, hair);
            return figure;
        }

        public static bool IsFemale(string person) => person == "maya" || person.StartsWith("Female");
    }
}
