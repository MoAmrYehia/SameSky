// Small procedural props that carry content: the ceramic cup (with saucer) and wooden photo frames.
// Materials come from Resources/Materials (URP Lit assets made by the project setup, so they ship in builds).

using SameSky.UI;
using UnityEngine;

namespace SameSky.App
{
    public static class Props
    {
        static Material _ceramic, _wood, _photo;
        static Mesh _cupMesh, _saucerMesh;

        static Material Ceramic => _ceramic ??= Resources.Load<Material>("Materials/Ceramic") ?? Fallback(new Color(0.93f, 0.91f, 0.87f));
        static Material Wood => _wood ??= Resources.Load<Material>("Materials/Walnut") ?? Fallback(new Color(0.42f, 0.28f, 0.18f));

        static Material Fallback(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { color = c };
            return m;
        }

        /// <summary>A mug about 9 cm tall with a handle, its pivot at the bottom centre, and a collider for grabbing.</summary>
        public static GameObject Cup()
        {
            var go = new GameObject("Cup");
            var body = new GameObject("Body");
            body.transform.SetParent(go.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = _cupMesh ??= BuildCupMesh();
            body.AddComponent<MeshRenderer>().sharedMaterial = Ceramic;
            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0.005f, 0.045f, 0);
            col.radius = 0.048f;
            col.height = 0.1f;
            return go;
        }

        /// <summary>The saucer the cup rests on; it stays on the table when the cup is lifted.</summary>
        public static GameObject Saucer()
        {
            var saucer = new GameObject("Saucer");
            saucer.AddComponent<MeshFilter>().sharedMesh = _saucerMesh ??= BuildLathe(new[]
            {
                new Vector2(0f, 0f), new Vector2(0.06f, 0f), new Vector2(0.072f, 0.008f), new Vector2(0.074f, 0.011f), new Vector2(0.066f, 0.01f), new Vector2(0f, 0.006f),
            }, 40, "Saucer");
            saucer.AddComponent<MeshRenderer>().sharedMaterial = Ceramic;
            return saucer;
        }

        static Mesh BuildCupMesh()
        {
            // outer wall up, rim, inner wall down (thin ceramic), with a bit of base taper
            var profile = new[]
            {
                new Vector2(0f, 0.002f), new Vector2(0.03f, 0f), new Vector2(0.036f, 0.004f), new Vector2(0.04f, 0.03f),
                new Vector2(0.042f, 0.085f), new Vector2(0.041f, 0.09f), new Vector2(0.037f, 0.088f), new Vector2(0.036f, 0.03f),
                new Vector2(0.03f, 0.012f), new Vector2(0f, 0.012f),
            };
            var lathe = BuildLathe(profile, 48, "Cup body");
            // handle: a half torus on the +X side
            var handle = new Mesh();
            const int seg = 20, ring = 10;
            const float R = 0.024f, r = 0.006f;
            var verts = new Vector3[(seg + 1) * (ring + 1)];
            var tris = new int[seg * ring * 6];
            for (var i = 0; i <= seg; i++)
            {
                var a = Mathf.Lerp(-Mathf.PI / 2f, Mathf.PI / 2f, i / (float)seg);
                var c = new Vector3(0.04f + Mathf.Cos(a) * R * 0.9f, 0.047f + Mathf.Sin(a) * R, 0);
                var tangent = new Vector3(-Mathf.Sin(a) * 0.9f, Mathf.Cos(a), 0).normalized;
                var n1 = Vector3.Cross(tangent, Vector3.forward).normalized;
                for (var j = 0; j <= ring; j++)
                {
                    var b = j / (float)ring * Mathf.PI * 2f;
                    verts[i * (ring + 1) + j] = c + (n1 * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b)) * r;
                }
            }
            var t = 0;
            for (var i = 0; i < seg; i++)
                for (var j = 0; j < ring; j++)
                {
                    int a0 = i * (ring + 1) + j, a1 = a0 + 1, b0 = a0 + ring + 1, b1 = b0 + 1;
                    tris[t++] = a0; tris[t++] = b0; tris[t++] = a1;
                    tris[t++] = a1; tris[t++] = b0; tris[t++] = b1;
                }
            handle.vertices = verts;
            handle.triangles = tris;
            handle.RecalculateNormals();
            var combine = new[] { new CombineInstance { mesh = lathe, transform = Matrix4x4.identity }, new CombineInstance { mesh = handle, transform = Matrix4x4.identity } };
            var mesh = new Mesh { name = "Cup" };
            mesh.CombineMeshes(combine, true, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh BuildLathe(Vector2[] profile, int segments, string name)
        {
            var verts = new Vector3[profile.Length * (segments + 1)];
            var uvs = new Vector2[verts.Length];
            var tris = new int[(profile.Length - 1) * segments * 6];
            for (var s = 0; s <= segments; s++)
            {
                var a = s / (float)segments * Mathf.PI * 2f;
                for (var p = 0; p < profile.Length; p++)
                {
                    verts[s * profile.Length + p] = new Vector3(Mathf.Cos(a) * profile[p].x, profile[p].y, Mathf.Sin(a) * profile[p].x);
                    uvs[s * profile.Length + p] = new Vector2(s / (float)segments, p / (float)(profile.Length - 1));
                }
            }
            var t = 0;
            for (var s = 0; s < segments; s++)
                for (var p = 0; p < profile.Length - 1; p++)
                {
                    int a0 = s * profile.Length + p, a1 = a0 + 1, b0 = a0 + profile.Length, b1 = b0 + 1;
                    tris[t++] = a0; tris[t++] = a1; tris[t++] = b0;
                    tris[t++] = a1; tris[t++] = b1; tris[t++] = b0;
                }
            var m = new Mesh { name = name, vertices = verts, uv = uvs, triangles = tris };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Earned keepsakes as small objects: a frame, two linked rings, a little memory tree.</summary>
        public static void Keepsake(string kind, Transform parent)
        {
            GameObject Part(PrimitiveType t, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default)
            {
                var go = GameObject.CreatePrimitive(t);
                Object.Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(parent, false);
                go.transform.localPosition = pos;
                go.transform.localScale = scale;
                go.transform.localRotation = Quaternion.Euler(euler);
                go.GetComponent<Renderer>().sharedMaterial = m;
                return go;
            }
            switch (kind)
            {
                case "linked_sculptures":
                    Part(PrimitiveType.Cylinder, new Vector3(-0.025f, 0.06f, 0), new Vector3(0.05f, 0.004f, 0.05f), Ceramic, new Vector3(90, 0, 20));
                    Part(PrimitiveType.Cylinder, new Vector3(0.025f, 0.06f, 0), new Vector3(0.05f, 0.004f, 0.05f), Ceramic, new Vector3(90, 90, -20));
                    Part(PrimitiveType.Cube, new Vector3(0, 0.01f, 0), new Vector3(0.1f, 0.02f, 0.06f), Wood);
                    break;
                case "memory_tree":
                    Part(PrimitiveType.Cylinder, new Vector3(0, 0.05f, 0), new Vector3(0.012f, 0.05f, 0.012f), Wood);
                    Part(PrimitiveType.Sphere, new Vector3(0, 0.12f, 0), new Vector3(0.09f, 0.08f, 0.09f), Ceramic);
                    Part(PrimitiveType.Cylinder, new Vector3(0, 0.005f, 0), new Vector3(0.06f, 0.005f, 0.06f), Wood);
                    break;
                default: // frame
                    Part(PrimitiveType.Cube, new Vector3(0, 0.06f, 0), new Vector3(0.09f, 0.11f, 0.012f), Wood, new Vector3(-10, 0, 0));
                    Part(PrimitiveType.Cube, new Vector3(0, 0.062f, -0.007f), new Vector3(0.07f, 0.085f, 0.002f), Ceramic, new Vector3(-10, 0, 0));
                    break;
            }
        }

        /// <summary>A standing walnut photo frame (about 20 cm wide), leaning slightly back; pivot at the bottom centre.</summary>
        public static GameObject Frame(Texture photo, int width, int height)
        {
            var aspect = Mathf.Clamp(width / (float)Mathf.Max(1, height), 0.6f, 2f);
            var w = 0.2f;
            var h = w / aspect;
            var go = new GameObject("Frame");
            var border = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(border.GetComponent<Collider>());
            border.transform.SetParent(go.transform, false);
            border.transform.localScale = new Vector3(w + 0.022f, h + 0.022f, 0.016f);
            border.transform.localPosition = new Vector3(0, (h + 0.022f) / 2f, 0.008f);
            border.GetComponent<Renderer>().sharedMaterial = Wood;
            Ui.Image(go.transform, "Photo", new Vector2(w, h), photo, 0.002f, new Vector3(0, (h + 0.022f) / 2f, -0.0005f));
            go.transform.localRotation = Quaternion.Euler(-8f, 0, 0);
            return go;
        }
    }
}
