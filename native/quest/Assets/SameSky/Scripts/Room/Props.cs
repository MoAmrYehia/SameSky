// Small procedural props that carry content: the ceramic cup (with saucer) and wooden photo frames.
// Materials come from Resources/Materials (URP Lit assets made by the project setup, so they ship in builds).

using SameSky.UI;
using UnityEngine;

namespace SameSky.App
{
    public static class Props
    {
        static Material _ceramic, _wood, _photo, _cork, _paper, _shadow;
        static Mesh _cupMesh, _saucerMesh, _coasterMesh;
        static readonly System.Collections.Generic.Dictionary<(int, int), Mesh> TentMeshes = new();

        static Material Ceramic => _ceramic ??= Resources.Load<Material>("Materials/Ceramic") ?? Fallback(new Color(0.93f, 0.91f, 0.87f));
        static Material Wood => _wood ??= Resources.Load<Material>("Materials/Walnut") ?? Fallback(new Color(0.42f, 0.28f, 0.18f));
        static Material Cork => _cork ??= Resources.Load<Material>("Materials/Cork") ?? Fallback(new Color(0.7f, 0.56f, 0.4f));
        static Material Paper => _paper ??= Resources.Load<Material>("Materials/Paper") ?? Fallback(new Color(0.97f, 0.95f, 0.9f));
        static Material Shadow => _shadow ??= Resources.Load<Material>("Materials/ContactShadow");
        static Material Photo => _photo ??= Resources.Load<Material>("Materials/Photo") ?? Fallback(Color.white);
        static Material _bronze, _leaf;
        static Mesh _ringMesh, _potMesh;
        static Material Bronze => _bronze ??= Resources.Load<Material>("Materials/Brass") ?? Fallback(new Color(0.6f, 0.45f, 0.25f));
        static Material Leaf => _leaf ??= Resources.Load<Material>("Materials/Leaf") ?? Fallback(new Color(0.33f, 0.47f, 0.3f));

        /// <summary>A round cork coaster (11 cm) the cup rests on; pivot at the bottom centre.</summary>
        public static GameObject Coaster()
        {
            var go = new GameObject("Coaster");
            go.AddComponent<MeshFilter>().sharedMesh = _coasterMesh ??= SameSky.Geometry.MeshGen.Lathe("Coaster", new[]
            {
                new Vector2(0, 0), new Vector2(0.054f, 0), new Vector2(0.056f, 0.002f), new Vector2(0.056f, 0.006f), new Vector2(0.054f, 0.008f), new Vector2(0, 0.008f),
            }, 40, planarUv: 8f);   // cork grain projected from above, not swirled around the centre
            go.AddComponent<MeshRenderer>().sharedMaterial = Cork;
            return go;
        }

        /// <summary>A soft glow, facing the person, that briefly marks where someone's presence arrives (the same soft
        /// round texture as the contact shadows, in warm white). Its alpha starts at zero; the caller fades it.</summary>
        public static MeshRenderer Glow(Transform parent, Vector3 position, Vector3 viewer, float radius)
        {
            if (Shadow == null) return null;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Arrival glow";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation((position - viewer).normalized, Vector3.up));
            go.transform.localScale = new Vector3(radius * 2f, radius * 2.4f, 1f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = Shadow;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            SetGlow(r, 0f);
            return r;
        }

        public static void SetGlow(MeshRenderer glow, float alpha)
        {
            var b = new MaterialPropertyBlock();
            b.SetColor("_BaseColor", new Color(1f, 0.97f, 0.9f, Mathf.Clamp01(alpha)));
            glow.SetPropertyBlock(b);
        }

        /// <summary>A soft round contact shade on the table under an object: the dimming right where it touches. The
        /// window light's real shadow is separate and falls where the light says.</summary>
        public static GameObject ContactShadow(Transform parent, Vector3 position, float radius, float strength = 1f)
        {
            if (Shadow == null) return null;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Contact shadow";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(position + Vector3.up * 0.0015f, Quaternion.Euler(90, 0, 0));
            go.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = Shadow;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (strength < 1f)
            {
                var b = new MaterialPropertyBlock();
                b.SetColor("_BaseColor", new Color(0, 0, 0, 0.3f * strength));
                r.SetPropertyBlock(b);
            }
            return go;
        }

        /// <summary>A folded paper card standing on the table (an A-frame, 11 x 6 cm faces). Returns the card; its child
        /// "Front" is the face toward the person (local -Z), leaning back 18 degrees, for a label or a button.</summary>
        public static GameObject TentCard(float width = 0.11f, float faceHeight = 0.06f)
        {
            var go = new GameObject("Tent card");
            const float lean = 18f;
            var depth = Mathf.Sin(lean * Mathf.Deg2Rad) * faceHeight;
            var top = Mathf.Cos(lean * Mathf.Deg2Rad) * faceHeight;
            // one mesh per size (a single cached mesh gave every card the first card's size)
            var key = (Mathf.RoundToInt(width * 1000f), Mathf.RoundToInt(faceHeight * 1000f));
            if (!TentMeshes.TryGetValue(key, out var mesh))
            {
                mesh = new Mesh { name = "Tent card" };
                float hw = width / 2f;
                // two slanted faces meeting at the top fold, each two-sided. Every side has its own vertices: sides
                // that shared them got averaged (near zero) normals and rendered black.
                Vector3 fl = new(-hw, 0, -depth), fr = new(hw, 0, -depth), tl = new(-hw, top, 0), tr = new(hw, top, 0);
                Vector3 bl = new(-hw, 0, depth), br = new(hw, 0, depth);
                mesh.vertices = new[] { fl, fr, tl, tr, fl, fr, tl, tr, bl, br, tl, tr, bl, br, tl, tr };
                mesh.triangles = new[]
                {
                    0, 2, 1, 1, 2, 3,       // front, toward the person
                    4, 5, 6, 5, 7, 6,       // front, inside
                    8, 9, 10, 9, 11, 10,    // back, outside
                    12, 14, 13, 13, 14, 15, // back, inside
                };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                TentMeshes[key] = mesh;
            }
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Paper;
            var front = new GameObject("Front").transform;
            front.SetParent(go.transform, false);
            front.localPosition = new Vector3(0, top / 2f, -depth / 2f) + new Vector3(0, Mathf.Sin(lean * Mathf.Deg2Rad), -Mathf.Cos(lean * Mathf.Deg2Rad)) * 0.0015f;
            front.localRotation = Quaternion.Euler(lean, 0, 0);   // local up runs along the face, which leans back toward the eyes
            return go;
        }

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

        /// <summary>The cup's ceramic, for a restrained selection glow (its material carries a black emission that a
        /// property block lifts while a hand is near or holds it).</summary>
        public static MeshRenderer CupBody(GameObject cup)
        {
            var body = cup.transform.Find("Body");
            return body != null ? body.GetComponent<MeshRenderer>() : null;
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
            GameObject Shape(Mesh mesh, Vector3 pos, Material m, Vector3 euler = default, Vector3? scale = null)
            {
                var go = new GameObject(mesh.name);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(euler);
                go.transform.localScale = scale ?? Vector3.one;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = m;
                return go;
            }
            switch (kind)
            {
                case "linked_sculptures":
                    // two linked bronze rings on a walnut base (visual reference 09)
                    _ringMesh ??= SameSky.Geometry.MeshGen.Sweep("Keepsake ring", 0.036f, 0f, 360f, 40,
                        _ => SameSky.Geometry.MeshGen.RoundedSection(0.011f, -0.0055f, 0.0055f, 0.0055f, 3), capEnds: false);
                    Shape(_ringMesh, new Vector3(-0.018f, 0.064f, 0), Bronze, new Vector3(90, 0, 0));
                    Shape(_ringMesh, new Vector3(0.018f, 0.064f, 0), Bronze, new Vector3(0, 0, 90));
                    Part(PrimitiveType.Cylinder, new Vector3(0, 0.008f, 0), new Vector3(0.11f, 0.008f, 0.07f), Wood);
                    break;
                case "memory_tree":
                    // a small ceramic planter with a few leaves: it fills in as days are shared
                    _potMesh ??= SameSky.Geometry.MeshGen.Lathe("Keepsake planter", new[]
                    {
                        new Vector2(0, 0), new Vector2(0.03f, 0), new Vector2(0.041f, 0.05f), new Vector2(0.043f, 0.056f), new Vector2(0.037f, 0.056f),
                        new Vector2(0.035f, 0.048f), new Vector2(0, 0.048f),
                    }, 32);
                    Shape(_potMesh, Vector3.zero, Ceramic);
                    for (var i = 0; i < 5; i++)
                    {
                        var turn = i * 72f + 12f;
                        var lean = i == 0 ? 0f : 28f;
                        var leaf = Part(PrimitiveType.Sphere, Vector3.zero, new Vector3(0.026f, 0.07f, 0.008f), Leaf, new Vector3(lean, turn, 0));
                        leaf.transform.localPosition = new Vector3(0, 0.05f, 0) + leaf.transform.localRotation * new Vector3(0, 0.03f, 0);
                    }
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
            // the print itself: an opaque, lit surface in the frame (a see-through UI image drew over panels in front)
            var print = GameObject.CreatePrimitive(PrimitiveType.Quad);   // faces -Z, toward the person
            print.name = "Photo";
            Object.Destroy(print.GetComponent<Collider>());
            print.transform.SetParent(go.transform, false);
            print.transform.localScale = new Vector3(w, h, 1f);
            print.transform.localPosition = new Vector3(0, (h + 0.022f) / 2f, -0.0005f);
            var r = print.GetComponent<MeshRenderer>();
            r.sharedMaterial = Photo;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (photo != null)
            {
                var b = new MaterialPropertyBlock();
                b.SetTexture("_BaseMap", photo);
                r.SetPropertyBlock(b);
            }
            go.transform.localRotation = Quaternion.Euler(-8f, 0, 0);
            return go;
        }
    }
}
