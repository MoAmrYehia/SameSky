// What the animated trailer has to work with: the two characters' bones and face shapes, and the lounge as the app
// builds it. Batch mode: Unity -batchmode -quit -projectPath native/quest -executeMethod SameSky.Film.FilmProbe.Dump
// Writes demo-build/film/probe.txt.

using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SameSky.Film
{
    public static class FilmProbe
    {
        public static void Dump()
        {
            var sb = new StringBuilder();
            foreach (var who in new[] { "maya", "adam" })
            {
                var prefab = Resources.Load<GameObject>("Characters/" + who);
                sb.AppendLine($"=== {who}: prefab {(prefab != null)}");
                if (prefab == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                var animator = go.GetComponentInChildren<Animator>(true);
                sb.AppendLine($"animator {(animator != null)} human {(animator != null && animator.isHuman)} avatar {(animator != null && animator.avatar != null ? animator.avatar.name : "-")} controller {(animator != null && animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "-")}");
                sb.AppendLine("components on root: " + string.Join(", ", go.GetComponents<Component>().Select(c => c.GetType().Name)));
                if (animator != null && animator.isHuman)
                {
                    foreach (HumanBodyBones b in Enum.GetValues(typeof(HumanBodyBones)))
                    {
                        if (b == HumanBodyBones.LastBone) continue;
                        var t = animator.GetBoneTransform(b);
                        if (t != null) sb.AppendLine($"  bone {b,-26} {t.name,-28} at {t.position.x:F3},{t.position.y:F3},{t.position.z:F3}");
                    }
                }
                foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var m = r.sharedMesh;
                    sb.AppendLine($"  skinned {r.name}: {m.vertexCount} verts, {m.blendShapeCount} shapes, bounds {r.bounds.size}, materials " + string.Join(" | ", r.sharedMaterials.Select(x => x == null ? "null" : $"{x.name} ({x.shader.name})")));
                    if (m.blendShapeCount > 0) sb.AppendLine("    shapes: " + string.Join(", ", Enumerable.Range(0, m.blendShapeCount).Select(m.GetBlendShapeName)));
                }
                foreach (var t in go.GetComponentsInChildren<Transform>(true).Where(t => t.name.ToLowerInvariant().Contains("eye") || t.name.ToLowerInvariant().Contains("jaw") || t.name.ToLowerInvariant().Contains("head")))
                    sb.AppendLine($"  face bone {t.name} (parent {t.parent?.name})");
                UnityEngine.Object.DestroyImmediate(go);
            }
            var scene = EditorSceneManager.OpenScene("Assets/SameSky/Scenes/SameSky.unity", OpenSceneMode.Single);
            sb.AppendLine("=== scene roots");
            foreach (var root in scene.GetRootGameObjects())
            {
                sb.AppendLine($"{root.name} at {root.transform.position} active {root.activeSelf} [{string.Join(", ", root.GetComponents<Component>().Select(c => c.GetType().Name))}]");
                Walk(root.transform, 1, 3, sb);
            }
            sb.AppendLine("=== render settings: ambient " + RenderSettings.ambientMode + " sky " + (RenderSettings.skybox != null ? RenderSettings.skybox.name : "-") + " sun " + (RenderSettings.sun != null ? RenderSettings.sun.name : "-"));
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine($"light {l.name}: {l.type} intensity {l.intensity} color {l.color} shadows {l.shadows} range {l.range} at {l.transform.position} rot {l.transform.eulerAngles} active {l.gameObject.activeInHierarchy}");
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "demo-build", "film"));
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "probe.txt"), sb.ToString());
            Debug.Log("[SameSky:film] probe written");
        }

        static void Walk(Transform t, int depth, int max, StringBuilder sb)
        {
            if (depth > max) return;
            foreach (Transform c in t)
            {
                var mesh = c.GetComponent<MeshFilter>();
                var rend = c.GetComponent<Renderer>();
                sb.AppendLine($"{new string(' ', depth * 2)}{c.name} pos {c.localPosition.x:F2},{c.localPosition.y:F2},{c.localPosition.z:F2} active {c.gameObject.activeSelf}{(mesh != null && mesh.sharedMesh != null ? " mesh " + mesh.sharedMesh.name : "")}{(rend != null && rend.sharedMaterial != null ? " mat " + rend.sharedMaterial.name : "")} children {c.childCount}");
                Walk(c, depth + 1, max, sb);
            }
        }
    }
}
