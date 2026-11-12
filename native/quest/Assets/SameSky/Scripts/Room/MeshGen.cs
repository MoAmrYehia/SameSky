// Procedural meshes for the lounge and its props (metres): lathed round things (table, lamp shade, planter, coaster),
// swept arcs (the tub chairs' shells, the curved window frame) and curved panels (the glazing's view, curved walls).
// Used by the editor setup to build the room and at runtime for small props.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SameSky.Geometry
{
    public static class MeshGen
    {
        /// <summary>Revolves a profile of (radius, height) points, bottom to top, around +Y. With planarUv &gt; 0 the
        /// texture is projected from above (x, z times planarUv), for flat round things: a table top, a rug, a cushion.</summary>
        public static Mesh Lathe(string name, Vector2[] profile, int segments = 48, bool capBottom = false, bool capTop = false, float planarUv = 0f)
        {
            var mesh = Lathe(name, profile, segments, capBottom, capTop);
            if (planarUv > 0f)
            {
                var v = mesh.vertices;
                var uv = new Vector2[v.Length];
                for (var i = 0; i < v.Length; i++) uv[i] = new Vector2(v[i].x, v[i].z) * planarUv;
                mesh.uv = uv;
                mesh.RecalculateTangents();
            }
            return mesh;
        }

        static Mesh Lathe(string name, Vector2[] profile, int segments, bool capBottom, bool capTop)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var lengths = new float[profile.Length];
            for (var i = 1; i < profile.Length; i++) lengths[i] = lengths[i - 1] + Vector2.Distance(profile[i], profile[i - 1]);
            var total = Mathf.Max(1e-4f, lengths[^1]);
            for (var s = 0; s <= segments; s++)
            {
                var a = s / (float)segments * Mathf.PI * 2f;
                var c = Mathf.Cos(a);
                var sn = Mathf.Sin(a);
                for (var i = 0; i < profile.Length; i++)
                {
                    verts.Add(new Vector3(profile[i].x * sn, profile[i].y, profile[i].x * c));
                    uvs.Add(new Vector2(s / (float)segments, lengths[i] / total));
                }
            }
            var rows = profile.Length;
            for (var s = 0; s < segments; s++)
                for (var i = 0; i < rows - 1; i++)
                {
                    int a0 = s * rows + i, a1 = a0 + 1, b0 = (s + 1) * rows + i, b1 = b0 + 1;
                    tris.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
                }
            if (capBottom) Cap(verts, uvs, tris, profile[0], segments, false);
            if (capTop) Cap(verts, uvs, tris, profile[^1], segments, true);
            return Finish(name, verts, uvs, tris);
        }

        static void Cap(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector2 ring, int segments, bool up)
        {
            var center = verts.Count;
            verts.Add(new Vector3(0, ring.y, 0));
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (var s = 0; s <= segments; s++)
            {
                var a = s / (float)segments * Mathf.PI * 2f;
                verts.Add(new Vector3(ring.x * Mathf.Sin(a), ring.y, ring.x * Mathf.Cos(a)));
                uvs.Add(new Vector2(0.5f + 0.5f * Mathf.Sin(a), 0.5f + 0.5f * Mathf.Cos(a)));
            }
            for (var s = 0; s < segments; s++)
            {
                if (up) tris.AddRange(new[] { center, center + 1 + s, center + 2 + s });
                else tris.AddRange(new[] { center, center + 2 + s, center + 1 + s });
            }
        }

        /// <summary>Sweeps a closed cross-section (radial offset, height) along an arc of the given radius around +Y,
        /// from startDeg to endDeg (0 = +Z). sectionAt may reshape the section along the arc (t = 0..1).</summary>
        public static Mesh Sweep(string name, float radius, float startDeg, float endDeg, int segments,
            Func<float, Vector2[]> sectionAt, bool capEnds = true)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var first = sectionAt(0f);
            var n = first.Length;
            for (var s = 0; s <= segments; s++)
            {
                var t = s / (float)segments;
                var a = Mathf.Lerp(startDeg, endDeg, t) * Mathf.Deg2Rad;
                var sec = sectionAt(t);
                var dir = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                for (var i = 0; i < n; i++)
                {
                    var p = dir * (radius + sec[i].x) + Vector3.up * sec[i].y;
                    verts.Add(p);
                    uvs.Add(new Vector2(t * radius * Mathf.Abs(endDeg - startDeg) * Mathf.Deg2Rad, i / (float)n));
                }
            }
            for (var s = 0; s < segments; s++)
                for (var i = 0; i < n; i++)
                {
                    var j = (i + 1) % n;
                    int a0 = s * n + i, a1 = s * n + j, b0 = (s + 1) * n + i, b1 = (s + 1) * n + j;
                    tris.AddRange(new[] { a0, b0, a1, a1, b0, b1 });   // outward faces outward, inner faces the axis
                }
            if (capEnds)
            {
                foreach (var end in new[] { 0, segments })
                {
                    var c = verts.Count;
                    var basis = end * n;
                    var centroid = Vector3.zero;
                    for (var i = 0; i < n; i++) centroid += verts[basis + i];
                    verts.Add(centroid / n);
                    uvs.Add(new Vector2(0.5f, 0.5f));
                    for (var i = 0; i < n; i++)
                    {
                        var j = (i + 1) % n;
                        if (end == 0) tris.AddRange(new[] { c, basis + i, basis + j });
                        else tris.AddRange(new[] { c, basis + j, basis + i });
                    }
                }
            }
            return Finish(name, verts, uvs, tris, smooth: true);
        }

        /// <summary>A rounded-rectangle cross-section (width radial, height vertical) for Sweep, centred on x, bottom at y0.</summary>
        public static Vector2[] RoundedSection(float width, float y0, float y1, float corner, int cornerSteps = 4)
        {
            var pts = new List<Vector2>();
            corner = Mathf.Min(corner, width / 2f, (y1 - y0) / 2f);
            var hw = width / 2f;
            // counter-clockwise: inner-bottom, outer-bottom, outer-top, inner-top
            void Arc(Vector2 c, float a0, float a1)
            {
                for (var k = 0; k <= cornerSteps; k++)
                {
                    var a = Mathf.Lerp(a0, a1, k / (float)cornerSteps) * Mathf.Deg2Rad;
                    pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * corner);
                }
            }
            Arc(new Vector2(-hw + corner, y0 + corner), 180, 270);
            Arc(new Vector2(hw - corner, y0 + corner), 270, 360);
            Arc(new Vector2(hw - corner, y1 - corner), 0, 90);
            Arc(new Vector2(-hw + corner, y1 - corner), 90, 180);
            return pts.ToArray();
        }

        /// <summary>A cylindrical panel of the given radius around +Y from startDeg to endDeg (0 = +Z), between heights
        /// y0 and y1. Faces the axis (inward) unless outward is set. UV.x runs along the arc, UV.y up.</summary>
        public static Mesh CurvedPanel(string name, float radius, float startDeg, float endDeg, float y0, float y1, int segments, bool outward = false)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (var s = 0; s <= segments; s++)
            {
                var t = s / (float)segments;
                var a = Mathf.Lerp(startDeg, endDeg, t) * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * radius;
                verts.Add(d + Vector3.up * y0);
                verts.Add(d + Vector3.up * y1);
                uvs.Add(new Vector2(t, 0));
                uvs.Add(new Vector2(t, 1));
            }
            for (var s = 0; s < segments; s++)
            {
                int a0 = s * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                if (outward) tris.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
                else tris.AddRange(new[] { a0, a1, b0, a1, b1, b0 });
            }
            return Finish(name, verts, uvs, tris);
        }

        /// <summary>A flat convex polygon (XZ plane), triangulated as a fan from its first point; faces up or down
        /// whichever way the outline runs.</summary>
        public static Mesh Fan(string name, IList<Vector3> outline, bool faceUp, float uvScale = 1f)
        {
            var verts = new List<Vector3>(outline);
            var uvs = new List<Vector2>();
            foreach (var p in outline) uvs.Add(new Vector2(p.x, p.z) * uvScale);
            var tris = new List<int>();
            var runsUp = Vector3.Cross(outline[1] - outline[0], outline[2] - outline[0]).y > 0;   // (0, 1, 2) as wound
            for (var i = 1; i < outline.Count - 1; i++)
                if (runsUp == faceUp) tris.AddRange(new[] { 0, i, i + 1 });
                else tris.AddRange(new[] { 0, i + 1, i });
            return Finish(name, verts, uvs, tris);
        }

        /// <summary>A wall (vertical, in the YZ plane at x, facing +X or -X) from z0 to z1 and 0 to height. With za &lt; zb it
        /// has an arched opening from za to zb: sill below, straight sides up to springY, then a round top.</summary>
        public static Mesh ArchedWall(string name, float x, float z0, float z1, float height, float za, float zb, float springY, float sillY, bool facePosX, int columns = 40)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var hasOpening = zb - za > 1e-3f;
            var zc = (za + zb) / 2f;
            var r = (zb - za) / 2f;
            var zs = new List<float> { z0, z1 };
            if (hasOpening) for (var i = 0; i <= columns; i++) zs.Add(Mathf.Lerp(za, zb, i / (float)columns));
            zs.Sort();
            float Arch(float z) => springY + Mathf.Sqrt(Mathf.Max(0, r * r - (z - zc) * (z - zc)));
            void Strip(float zA, float zB, float bA, float bB, float tA, float tB)
            {
                var v = verts.Count;
                verts.Add(new Vector3(x, bA, zA)); verts.Add(new Vector3(x, tA, zA));
                verts.Add(new Vector3(x, bB, zB)); verts.Add(new Vector3(x, tB, zB));
                uvs.Add(new Vector2(zA, bA)); uvs.Add(new Vector2(zA, tA)); uvs.Add(new Vector2(zB, bB)); uvs.Add(new Vector2(zB, tB));
                // (v, v+1, v+2) faces +X for zB > zA (cross product of up and +Z)
                if (facePosX) tris.AddRange(new[] { v, v + 1, v + 2, v + 2, v + 1, v + 3 });
                else tris.AddRange(new[] { v, v + 2, v + 1, v + 1, v + 2, v + 3 });
            }
            for (var i = 0; i < zs.Count - 1; i++)
            {
                float zA = zs[i], zB = zs[i + 1];
                if (zB - zA < 1e-5f) continue;
                var mid = (zA + zB) / 2f;
                if (hasOpening && mid > za && mid < zb)
                {
                    if (sillY > 0) Strip(zA, zB, 0, 0, sillY, sillY);   // below the opening
                    Strip(zA, zB, Arch(zA), Arch(zB), height, height);  // above the arch
                }
                else Strip(zA, zB, 0, 0, height, height);
            }
            return Finish(name, verts, uvs, tris);
        }

        static Mesh Finish(string name, List<Vector3> verts, List<Vector2> uvs, List<int> tris, bool smooth = true)
        {
            var m = new Mesh { name = name };
            if (verts.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }
    }
}
