// The sample's fictional people as skinned, rigged adults (visual references 01 and 02): Microsoft Rocketbox
// avatars (MIT licence, Art/Characters/Rocketbox/LICENSE.txt) imported as Humanoid with their facial rigs, given URP
// materials from their own texture sets, and seated with Rocketbox's "sit at a table, breathing" loop. One prefab
// per person in Resources/Characters (maya, adam), which Avatars.SeatFor loads for the sample day. These are stock
// characters standing in for fictional people: no scan or photo of anyone who uses the app.

using System.IO;
using System.Linq;
using SameSky.App;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace SameSky.EditorTools
{
    public static class CharacterSetup
    {
        const string Root = "Assets/SameSky/Art/Characters/Rocketbox";
        const string Generated = Root + "/Generated";
        const string Out = "Assets/SameSky/Resources/Characters";

        // The partner seat's space (the seat anchor is on the floor and faces the table; +Z points at the person). The
        // pelvis goes over the tub chair's cushion (centre 0.11 m behind the anchor), a little forward of its middle;
        // the height comes from what the pose touches: forearms on the table (top 0.774 m, near edge 0.205 m in front
        // of the anchor), or else the feet on the floor.
        const float PelvisZ = -0.06f, TableNearEdge = 0.205f, WristOnTable = 0.804f, AnkleHeight = 0.075f;

        sealed class Person
        {
            public string Id, Folder, Prefix, Clip;
        }

        static readonly Person[] People =
        {
            new() { Id = "maya", Folder = "Female_Adult_14", Prefix = "f017", Clip = "f_sit_table_breathe_01" },
            new() { Id = "adam", Folder = "Male_Adult_07", Prefix = "m013", Clip = "m_sit_table_breathe_01" },
        };

        public static void Run()
        {
            Directory.CreateDirectory(Generated);
            AssetDatabase.Refresh();
            foreach (var p in People) Build(p);
            AssetDatabase.SaveAssets();
        }

        static void Log(string m) => Debug.Log("[SameSky:setup] " + m);

        static void Build(Person p)
        {
            var dir = $"{Root}/{p.Folder}";
            var fbx = $"{dir}/{p.Folder}_facial.fbx";
            if (!File.Exists(fbx)) { Debug.LogWarning($"[SameSky:setup] {fbx} is missing: {p.Id} keeps the figure already in Resources/Characters"); return; }
            ConfigureTextures(dir);
            if (!ConfigureModel(fbx)) return;
            var clip = ConfigureClip($"{Root}/Animations/{p.Clip}.fbx");
            if (clip == null) { Debug.LogWarning($"[SameSky:setup] no seated clip for {p.Id}"); return; }
            var controller = Controller($"{Generated}/{p.Id}-seated.controller", clip);
            // Skin keeps a soft highlight; knit and hair are matte. None of them mirrors the room: a bright interior
            // reflected at glancing angles turned brown hair grey.
            var skin = Material($"{p.Prefix}_head", $"{dir}/{p.Prefix}_head_color.jpg", $"{dir}/{p.Prefix}_head_normal.png", 0.32f, false, highlights: true);
            var body = Material($"{p.Prefix}_body", $"{dir}/{p.Prefix}_body_color.jpg", $"{dir}/{p.Prefix}_body_normal.png", 0.12f, false, highlights: false);
            var hair = Material($"{p.Prefix}_opacity", $"{dir}/{p.Prefix}_opacity_color.png", null, 0.1f, true, highlights: false);
            MakePrefab(p, fbx, controller, clip, skin, body, hair);
        }

        static void ConfigureTextures(string dir)
        {
            foreach (var file in Directory.GetFiles(dir).Where(f => f.EndsWith(".jpg") || f.EndsWith(".png")))
            {
                var path = file.Replace('\\', '/');
                if (AssetImporter.GetAtPath(path) is not TextureImporter ti) continue;
                var name = Path.GetFileNameWithoutExtension(path);
                var normal = name.EndsWith("_normal");
                var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                var size = name.Contains("_head_") ? 2048 : 1024;
                var alpha = name.Contains("_opacity");
                if (ti.textureType == type && ti.maxTextureSize == size && ti.alphaIsTransparency == alpha && ti.mipmapEnabled) continue;
                ti.textureType = type;
                ti.maxTextureSize = size;
                ti.alphaIsTransparency = alpha;
                ti.mipmapEnabled = true;
                ti.SaveAndReimport();
            }
        }

        static bool ConfigureModel(string fbx)
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
            if (mi.animationType != ModelImporterAnimationType.Human || mi.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel
                || mi.importAnimation || !mi.importBlendShapes || mi.importCameras || mi.importLights)
            {
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.importAnimation = false;
                mi.importBlendShapes = true;
                mi.importCameras = false;
                mi.importLights = false;
                mi.SaveAndReimport();
            }
            var avatar = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogWarning($"[SameSky:setup] {fbx}: Unity couldn't map a humanoid skeleton");
                return false;
            }
            return true;
        }

        static AnimationClip ConfigureClip(string path)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter mi) return null;
            if (mi.animationType != ModelImporterAnimationType.Human || mi.clipAnimations.Length != 1 || !mi.clipAnimations[0].loopTime)
            {
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.importAnimation = true;
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.SaveAndReimport();   // a humanoid's default clip settings exist only after an import
                var c = mi.defaultClipAnimations.First();
                c.name = Path.GetFileNameWithoutExtension(path);
                c.loopTime = true;
                c.loopPose = true;
                // seated: no root motion; keep the pelvis where the animator put it
                c.lockRootRotation = true;
                c.keepOriginalOrientation = true;
                c.lockRootHeightY = true;
                c.keepOriginalPositionY = true;
                c.lockRootPositionXZ = true;
                c.keepOriginalPositionXZ = true;
                mi.clipAnimations = new[] { c };
                mi.SaveAndReimport();
            }
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }

        static AnimatorController Controller(string path, AnimationClip clip)
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (c == null) c = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = c.layers[0].stateMachine;
            var state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Seated");
            if (state == null) state = sm.AddState("Seated");
            state.motion = clip;
            sm.defaultState = state;
            var layers = c.layers;
            layers[0].iKPass = true;   // SeatedFigure turns the head toward the person in OnAnimatorIK
            c.layers = layers;
            EditorUtility.SetDirty(c);
            return c;
        }

        static Material Material(string name, string color, string normal, float smoothness, bool cutout, bool highlights)
        {
            var path = $"{Generated}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(color));
            m.SetColor("_BaseColor", Color.white);
            var n = normal != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(normal) : null;
            m.SetTexture("_BumpMap", n);
            if (n != null) { m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1f); }
            else m.DisableKeyword("_NORMALMAP");
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            m.SetFloat("_SpecularHighlights", highlights ? 1f : 0f);
            if (highlights) m.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            else m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            if (cutout)
            {
                // hair and lashes: cut out (no sorting problems in a headset), both sides, alpha to coverage with MSAA
                m.SetFloat("_AlphaClip", 1);
                m.SetFloat("_Cutoff", 0.4f);
                m.SetFloat("_AlphaToMask", 1);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", (float)CullMode.Off);
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void MakePrefab(Person p, string fbx, AnimatorController controller, AnimationClip clip, Material skin, Material body, Material hair)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var root = new GameObject(p.Id);
            var figure = (GameObject)PrefabUtility.InstantiatePrefab(model);
            figure.transform.SetParent(root.transform, false);
            figure.name = p.Folder;

            // materials by what each part of the mesh is: face and hands (head set), clothes and body, hair and lashes
            var parts = new System.Collections.Generic.List<string>();
            foreach (var r in figure.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                for (var i = 0; i < mats.Length; i++)
                {
                    var n = mats[i] != null ? mats[i].name.ToLowerInvariant() : "";
                    parts.Add($"{r.name}[{i}]={n}");
                    mats[i] = n.Contains("opacity") || n.Contains("hair") || n.Contains("lash") ? hair : n.Contains("head") ? skin : body;
                }
                r.sharedMaterials = mats;
                r.quality = SkinQuality.Bone4;
                r.updateWhenOffscreen = false;
            }
            Log($"{p.Id}: {string.Join(", ", parts)}");
            // what the face can do: blend shapes on the mesh, and the eye and jaw bones the humanoid mapping found
            foreach (var r in figure.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = r.sharedMesh;
                if (mesh == null || mesh.blendShapeCount == 0) continue;
                var names = new System.Collections.Generic.List<string>();
                for (var i = 0; i < mesh.blendShapeCount; i++) names.Add(mesh.GetBlendShapeName(i));
                Log($"{p.Id} blend shapes on {r.name} ({mesh.blendShapeCount}): {string.Join(", ", names)}");
            }

            if (!figure.TryGetComponent<Animator>(out var animator)) animator = figure.AddComponent<Animator>();
            string Bone(HumanBodyBones b) { var t = animator.GetBoneTransform(b); return t != null ? t.name : "-"; }
            Log($"{p.Id} face bones: head {Bone(HumanBodyBones.Head)}, neck {Bone(HumanBodyBones.Neck)}, eyes {Bone(HumanBodyBones.LeftEye)} / {Bone(HumanBodyBones.RightEye)}, jaw {Bone(HumanBodyBones.Jaw)}");
            var headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            if (headBone != null)
            {
                var under = new System.Collections.Generic.List<string>();
                foreach (var t in headBone.GetComponentsInChildren<Transform>(true)) if (t != headBone) under.Add(t.name);
                Log($"{p.Id} bones under the head ({under.Count}): {string.Join(", ", under)}");
            }
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            if (!figure.TryGetComponent<SeatedFigure>(out _)) figure.AddComponent<SeatedFigure>();

            // seat the figure: pose the first frame, measure the bones in seat space, move the figure
            clip.SampleAnimation(figure, 0f);
            Vector3 At(HumanBodyBones b)
            {
                var t = animator.GetBoneTransform(b);
                return t == null ? Vector3.zero : root.transform.InverseTransformPoint(t.position);
            }
            if (animator.GetBoneTransform(HumanBodyBones.Hips) != null)
            {
                var hips = At(HumanBodyBones.Hips);
                figure.transform.localPosition += new Vector3(-hips.x, 0f, PelvisZ - hips.z);
                clip.SampleAnimation(figure, 0f);
                Vector3 lh = At(HumanBodyBones.LeftHand), rh = At(HumanBodyBones.RightHand), lf = At(HumanBodyBones.LeftFoot), rf = At(HumanBodyBones.RightFoot);
                var handsOnTable = Mathf.Min(lh.z, rh.z) > TableNearEdge + 0.05f;
                var lift = handsOnTable ? WristOnTable - Mathf.Min(lh.y, rh.y) : AnkleHeight - Mathf.Min(lf.y, rf.y);
                figure.transform.localPosition += new Vector3(0f, lift, 0f);
                clip.SampleAnimation(figure, 0f);
                Log($"{p.Id} seated ({(handsOnTable ? "forearms on the table" : "feet on the floor")}, seat space): hips {At(HumanBodyBones.Hips):F2}, head {At(HumanBodyBones.Head):F2}, hands {At(HumanBodyBones.LeftHand):F2} {At(HumanBodyBones.RightHand):F2}, feet {At(HumanBodyBones.LeftFoot):F2} {At(HumanBodyBones.RightFoot):F2}");
            }
            Directory.CreateDirectory(Out);
            PrefabUtility.SaveAsPrefabAsset(root, $"{Out}/{p.Id}.prefab");
            Object.DestroyImmediate(root);
        }
    }
}
