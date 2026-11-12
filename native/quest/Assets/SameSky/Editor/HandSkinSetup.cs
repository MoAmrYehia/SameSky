// Realistic hands: bakes two detail maps for each of Meta's tracked hand meshes (the OpenXR hand that the Interaction
// SDK's hand visual draws) and builds the skin materials the app puts on them (Scripts/Interaction/HandSkin.cs,
// Shaders/HandSkin.shader).
//
// Nothing here is painted by hand or taken from a photo of anyone: every texel is computed from the mesh and its
// joints. Each texel knows where it is on the hand, so the maps can say: this is a nail (the top of the last finger
// bone), this is the palm side, this is a knuckle wrinkle or a flexion crease or one of the three palm lines, this
// skin is a little flushed (finger pads, knuckles), this is where the wrist fades out.
//
//   hand-<side>-a.png   R nail   G palm side   B flush   A cavity (1 = open skin, lower inside creases)
//   hand-<side>-b.png   R,G tangent-space normal   B wrist opacity   A nail free edge and lunula (paler)
//
// The skin tone is not in the maps: the shader mixes it from colours the app sets, so any tone can be chosen.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SameSky.EditorTools
{
    public static class HandSkinSetup
    {
        const string MeshFolder = "Packages/com.meta.xr.sdk.interaction/Runtime/Meshes";
        const string Out = "Assets/SameSky/Art/Generated/Hands";
        const string Materials = "Assets/SameSky/Resources/Materials";
        const string Info = "Assets/SameSky/Resources/hand-skin.json";
        const int Size = 1024;

        static readonly string[] FingerNames = { "Thumb", "Index", "Middle", "Ring", "Little" };

        sealed class Crease
        {
            public Vector3 At, Dir, Up, Side;
            public float Radius, Dorsal, Palmar;
            public float[] PalmarLines;   // offsets along the finger, in radii
        }

        sealed class Finger
        {
            public Vector3[] Chain;       // the free part of the finger: knuckle ... tip
            public Vector3 Distal, Fwd, Up, Side;
            public float Length, Radius;
            public bool Thumb;
            public readonly List<Crease> Creases = new();
            public Vector3 Knuckle;
        }

        public static void Run()
        {
            var shader = Shader.Find("SameSky/HandSkin");
            if (shader == null) { Debug.LogWarning("[SameSky:setup] hand skin: shader SameSky/HandSkin not found"); return; }
            Directory.CreateDirectory(Out);
            Directory.CreateDirectory(Materials);
            var info = new List<string>();
            foreach (var left in new[] { true, false })
            {
                var path = $"{MeshFolder}/{(left ? "OpenXRLeftHand" : "OpenXRRightHand")}.fbx";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var smr = model != null ? model.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
                if (smr == null || smr.sharedMesh == null) { Debug.LogWarning($"[SameSky:setup] hand skin: no skinned mesh in {path}"); continue; }
                try
                {
                    Bake(model, smr, left, shader);
                    info.Add($"\"{(left ? "left" : "right")}\": {{ \"mesh\": \"{smr.sharedMesh.name}\", \"vertices\": {smr.sharedMesh.vertexCount} }}");
                }
                catch (Exception e) { Debug.LogWarning($"[SameSky:setup] hand skin ({(left ? "left" : "right")}): {e.Message}\n{e.StackTrace}"); }
            }
            File.WriteAllLines(Info, new[] { "{", "  " + string.Join(", ", info), "}" });
            AssetDatabase.ImportAsset(Info);
        }

        static void Bake(GameObject model, SkinnedMeshRenderer smr, bool left, Shader shader)
        {
            var mesh = smr.sharedMesh;
            var side = left ? "left" : "right";

            // ---------------------------------------------------------------- joints, in the mesh's own space
            var bones = smr.bones;
            var bind = mesh.bindposes;
            var boneToMesh = new Dictionary<Transform, Matrix4x4>();
            for (var i = 0; i < bones.Length && i < bind.Length; i++)
                if (bones[i] != null) boneToMesh[bones[i]] = bind[i].inverse;
            var rootToMesh = smr.transform.worldToLocalMatrix;
            Matrix4x4 ToMesh(Transform t)
            {
                if (boneToMesh.TryGetValue(t, out var m)) return m;
                if (t.parent == null || t == model.transform) return rootToMesh * t.localToWorldMatrix;
                return ToMesh(t.parent) * Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale);
            }
            var joints = new Dictionary<string, Matrix4x4>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("XRHand_")) joints[t.name.Substring(7)] = ToMesh(t);
            Vector3 P(string joint)
            {
                if (!joints.TryGetValue(joint, out var m)) throw new InvalidOperationException($"joint {joint} not found");
                return m.MultiplyPoint3x4(Vector3.zero);
            }

            var wrist = P("Wrist");
            var palmLength = Vector3.Distance(P("MiddleProximal"), wrist);
            var mm = palmLength / 95f;   // a millimetre on this hand (the maps scale with the mesh, whatever its units)
            // (index knuckle - wrist) x (little knuckle - wrist) points out of the palm on a left hand and out of the
            // back on a right hand (Unity's left-handed axes)
            var handUp = Vector3.Cross(P("IndexProximal") - wrist, P("LittleProximal") - wrist).normalized * (left ? -1f : 1f);

            // which of a joint's own axes run along the finger and out of its back: read off the index finger, where
            // both directions are known, then used for every joint (the thumb is turned, so it needs its own axes)
            var probe = joints["IndexProximal"];
            var along = (P("IndexIntermediate") - P("IndexProximal")).normalized;
            int fwdAxis = 0, upAxis = 0;
            float fwdSign = 1, upSign = 1, bestFwd = -1, bestUp = -1;
            for (var axis = 0; axis < 3; axis++)
            {
                var d = Vector3.Dot(((Vector3)probe.GetColumn(axis)).normalized, along);
                if (Mathf.Abs(d) > bestFwd) { bestFwd = Mathf.Abs(d); fwdAxis = axis; fwdSign = Mathf.Sign(d); }
            }
            for (var axis = 0; axis < 3; axis++)
            {
                if (axis == fwdAxis) continue;
                var d = Vector3.Dot(((Vector3)probe.GetColumn(axis)).normalized, handUp);
                if (Mathf.Abs(d) > bestUp) { bestUp = Mathf.Abs(d); upAxis = axis; upSign = Mathf.Sign(d); }
            }
            Vector3 JointUp(string joint) => ((Vector3)joints[joint].GetColumn(upAxis)).normalized * upSign;
            Vector3 Ortho(Vector3 v, Vector3 axis) => (v - axis * Vector3.Dot(v, axis)).normalized;

            // ---------------------------------------------------------------- vertices
            var verts = mesh.vertices;
            var normals = mesh.normals;
            var uvs = mesh.uv;
            var tris = mesh.triangles;
            var weights = mesh.boneWeights;
            if (uvs.Length != verts.Length) throw new InvalidOperationException("the hand mesh has no UVs");
            var boneUp = new Vector3[bones.Length];
            for (var i = 0; i < bones.Length; i++)
                boneUp[i] = bones[i] != null && bones[i].name.StartsWith("XRHand_") && joints.ContainsKey(bones[i].name.Substring(7))
                    ? JointUp(bones[i].name.Substring(7)) : handUp;
            var vertUp = new Vector3[verts.Length];
            for (var i = 0; i < verts.Length; i++)
            {
                var w = weights.Length == verts.Length ? weights[i] : default;
                var up = Vector3.zero;
                if (w.weight0 > 0) up += boneUp[w.boneIndex0] * w.weight0;
                if (w.weight1 > 0) up += boneUp[w.boneIndex1] * w.weight1;
                if (w.weight2 > 0) up += boneUp[w.boneIndex2] * w.weight2;
                if (w.weight3 > 0) up += boneUp[w.boneIndex3] * w.weight3;
                vertUp[i] = up.sqrMagnitude > 1e-8f ? up.normalized : handUp;
            }

            // ---------------------------------------------------------------- fingers
            var fingers = new Finger[5];
            for (var f = 0; f < 5; f++)
            {
                var n = FingerNames[f];
                var thumb = f == 0;
                var chain = thumb
                    ? new[] { P(n + "Proximal"), P(n + "Distal"), P(n + "Tip") }
                    : new[] { P(n + "Proximal"), P(n + "Intermediate"), P(n + "Distal"), P(n + "Tip") };
                var finger = new Finger { Chain = chain, Thumb = thumb, Distal = P(n + "Distal"), Knuckle = chain[0] };
                var tip = P(n + "Tip");
                finger.Length = Vector3.Distance(tip, finger.Distal);
                finger.Fwd = (tip - finger.Distal) / finger.Length;
                finger.Up = Ortho(JointUp(n + "Distal"), finger.Fwd);
                finger.Side = Vector3.Cross(finger.Up, finger.Fwd);
                fingers[f] = finger;
            }
            // each finger's thickness, measured on the mesh around its middle
            var sum = new float[5];
            var count = new int[5];
            for (var i = 0; i < verts.Length; i++)
            {
                var f = Nearest(fingers, verts[i], out var d, out var seg, out var t);
                var middle = fingers[f].Thumb ? seg == 0 && t > 0.35f && t < 0.9f : seg == 1 && t > 0.15f && t < 0.85f;
                if (!middle || d > 14f * mm) continue;
                sum[f] += d;
                count[f]++;
            }
            for (var f = 0; f < 5; f++) fingers[f].Radius = count[f] > 0 ? sum[f] / count[f] : (f == 0 ? 10f : 8f) * mm;

            for (var f = 0; f < 5; f++)
            {
                var finger = fingers[f];
                var n = FingerNames[f];
                var c = finger.Chain;
                Crease At(int i, string joint, float dorsal, float palmar, params float[] lines)
                {
                    var dir = ((i + 1 < c.Length ? c[i + 1] - c[i] : c[i] - c[i - 1]).normalized + (i > 0 ? (c[i] - c[i - 1]).normalized : Vector3.zero)).normalized;
                    var up = Ortho(JointUp(joint), dir);
                    return new Crease { At = c[i], Dir = dir, Up = up, Side = Vector3.Cross(up, dir), Radius = finger.Radius, Dorsal = dorsal, Palmar = palmar, PalmarLines = lines };
                }
                if (finger.Thumb)
                {
                    finger.Creases.Add(At(0, n + "Proximal", 0.45f, 0.8f, 0f));
                    finger.Creases.Add(At(1, n + "Distal", 0.9f, 0.9f, -0.11f, 0.11f));
                }
                else
                {
                    // the crease where a finger joins the palm lies well past the knuckle joint itself
                    var baseOffset = 0.45f * Vector3.Distance(c[0], c[1]) / finger.Radius;
                    finger.Creases.Add(At(0, n + "Proximal", 0.3f, 0.85f, baseOffset));
                    finger.Creases.Add(At(1, n + "Intermediate", 1f, 0.95f, -0.11f, 0.11f));
                    finger.Creases.Add(At(2, n + "Distal", 0.7f, 0.85f, 0f));
                }
            }

            // ---------------------------------------------------------------- the palm's own frame and its three lines
            var middleKnuckle = P("MiddleProximal");
            var uAxis = (middleKnuckle - wrist).normalized;
            var across = P("IndexProximal") - P("LittleProximal");
            var vAxis = Ortho(across, uAxis);
            var palmWidth = Vector3.Dot(across, vAxis);
            var vZero = Vector3.Dot((P("IndexProximal") + P("LittleProximal")) / 2f - wrist, vAxis);
            Vector2[] Line(Vector2 a, Vector2 b, Vector2 c2)
            {
                var pts = new Vector2[17];
                for (var i = 0; i < pts.Length; i++)
                {
                    var t = i / (pts.Length - 1f);
                    var p = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * b + t * t * c2;
                    pts[i] = new Vector2(p.x * palmLength, p.y * palmWidth);
                }
                return pts;
            }
            var palmLines = new[]
            {
                Line(new Vector2(0.80f, -0.62f), new Vector2(0.74f, -0.15f), new Vector2(0.93f, 0.18f)),   // heart line
                Line(new Vector2(0.80f, 0.62f), new Vector2(0.66f, 0.15f), new Vector2(0.50f, -0.45f)),    // head line
                Line(new Vector2(0.80f, 0.62f), new Vector2(0.58f, 0.02f), new Vector2(0.08f, 0.10f)),     // life line
            };
            var tMin = float.MaxValue;
            foreach (var v in verts) tMin = Mathf.Min(tMin, Vector3.Dot(v - wrist, uAxis) / palmLength);

            // ---------------------------------------------------------------- every texel's place on the hand
            var n2 = Size * Size;
            var pos = new Vector3[n2];
            var nor = new Vector3[n2];
            var ups = new Vector3[n2];
            var texel = new float[n2];     // metres per texel there
            var covered = new bool[n2];
            for (var t = 0; t < tris.Length; t += 3)
            {
                int i0 = tris[t], i1 = tris[t + 1], i2 = tris[t + 2];
                Vector2 a = uvs[i0] * Size, b = uvs[i1] * Size, c = uvs[i2] * Size;
                var areaUv = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y);
                if (Mathf.Abs(areaUv) < 1e-6f) continue;
                var areaWorld = Vector3.Cross(verts[i1] - verts[i0], verts[i2] - verts[i0]).magnitude;
                var perTexel = Mathf.Sqrt(areaWorld / Mathf.Abs(areaUv));
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - 1)), x1 = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) + 1));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)) - 1)), y1 = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)) + 1));
                for (var y = y0; y <= y1; y++)
                    for (var x = x0; x <= x1; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        var w0 = ((b.x - p.x) * (c.y - p.y) - (c.x - p.x) * (b.y - p.y)) / areaUv;
                        var w1 = ((c.x - p.x) * (a.y - p.y) - (a.x - p.x) * (c.y - p.y)) / areaUv;
                        var w2 = 1f - w0 - w1;
                        const float eps = -0.02f;   // a hair over the edge, so neighbouring triangles leave no gaps
                        if (w0 < eps || w1 < eps || w2 < eps) continue;
                        var k = y * Size + x;
                        pos[k] = verts[i0] * w0 + verts[i1] * w1 + verts[i2] * w2;
                        nor[k] = (normals[i0] * w0 + normals[i1] * w1 + normals[i2] * w2).normalized;
                        ups[k] = (vertUp[i0] * w0 + vertUp[i1] * w1 + vertUp[i2] * w2).normalized;
                        texel[k] = perTexel;
                        covered[k] = true;
                    }
            }

            // ---------------------------------------------------------------- the maps
            var nail = new float[n2];
            var palm = new float[n2];
            var flush = new float[n2];
            var cavity = new float[n2];
            var edge = new float[n2];
            var opacity = new float[n2];
            var height = new float[n2];
            var nailTexels = 0;
            for (var k = 0; k < n2; k++)
            {
                if (!covered[k]) { cavity[k] = 1f; continue; }
                var p = pos[k];
                var palmSide = Vector3.Dot(nor[k], ups[k]);             // +1 on the back of the hand, -1 on the palm
                var palmar = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, -0.25f, palmSide));
                palm[k] = palmar;
                var f = Nearest(fingers, p, out var distance, out _, out _);
                var finger = fingers[f];
                var onFinger = distance < 1.7f * finger.Radius;

                // nail: the top of the last bone, rounded at the cuticle, straight at the free edge
                float nailMask = 0, groove = 0, pale = 0, pad = 0;
                var q = p - finger.Distal;
                var a = Vector3.Dot(q, finger.Fwd) / finger.Length;
                if (onFinger && a > 0.1f && a < 1.25f)
                {
                    float h = Vector3.Dot(q, finger.Up), l = Vector3.Dot(q, finger.Side);
                    var around = Mathf.Sqrt(h * h + l * l);
                    var theta = Mathf.Atan2(Mathf.Abs(l), h) * Mathf.Rad2Deg;
                    float a0 = finger.Thumb ? 0.38f : 0.42f, a1 = 0.95f, round = 0.2f, maxTheta = finger.Thumb ? 66f : 62f;
                    var shape = 1f;
                    if (a < a0 + round) { var s = (a0 + round - a) / round; shape = s >= 1f ? 0f : Mathf.Sqrt(1f - s * s); }
                    var inside = Mathf.Min((maxTheta * shape - theta) * Mathf.Deg2Rad * around, (a1 - a) * finger.Length);
                    if (a < a0) inside = Mathf.Min(inside, (a - a0) * finger.Length);
                    nailMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.25f * mm, 0.25f * mm, inside));
                    if (inside < 0) groove = Mathf.Exp(-Sq((inside + 0.35f * mm) / (0.4f * mm)));
                    // the free edge, and a hint of the pale half-moon at the cuticle
                    pale = nailMask * Mathf.Max(0.85f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a1 - 0.075f, a1 - 0.04f, a)),
                        0.22f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a0 + 0.16f, a0 + 0.06f, a)) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(40f, 20f, theta)));
                    pad = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.5f, a)) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(95f, 130f, theta));
                    if (nailMask > 0.5f) nailTexels++;
                }
                nail[k] = nailMask;
                edge[k] = pale;

                // knuckle wrinkles on the back, flexion creases on the palm side
                float dorsalCrease = 0, palmarCrease = 0, knuckle = 0;
                if (onFinger)
                    foreach (var c in finger.Creases)
                    {
                        var r = c.Radius;
                        var d = p - c.At;
                        if (d.sqrMagnitude > Sq(3.4f * r)) continue;
                        var x = Vector3.Dot(d, c.Dir);
                        var w = d - c.Dir * x;
                        var around = w.magnitude;
                        if (around < 0.45f * r || around > 2f * r) continue;
                        var theta = Mathf.Atan2(Mathf.Abs(Vector3.Dot(w, c.Side)), Vector3.Dot(w, c.Up)) * Mathf.Rad2Deg;
                        var back = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(88f, 55f, theta));
                        if (back > 0 && c.Dorsal > 0)
                        {
                            // The skin over a knuckle folds in ovals around it: short creases across the finger that
                            // curve away at the sides, never quite evenly spaced and never equally deep.
                            var arc = theta * Mathf.Deg2Rad * around;
                            var wobble = (Fbm(p / mm * 0.42f + c.At / mm) - 0.5f) * 0.16f;
                            // far wider than the finger, so across its back they read as creases, not rings
                            var rho = Mathf.Sqrt(Sq(x / (0.95f * r)) + Sq(arc / (5f * r))) + wobble;
                            var toRing = Mathf.Abs(Mathf.Repeat(rho / 0.3f + 0.5f, 1f) - 0.5f) * 0.3f;
                            var ring = Mathf.Exp(-Sq(toRing / 0.04f));
                            var patch = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.5f, 1f, rho));
                            var depth = 0.45f + 0.55f * Noise(p / mm * 0.8f + c.At / mm * 3f);
                            dorsalCrease = Mathf.Max(dorsalCrease, ring * patch * back * c.Dorsal * depth);
                            knuckle = Mathf.Max(knuckle, Mathf.Exp(-Sq(x / (0.85f * r))) * back * c.Dorsal);
                        }
                        var front = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(95f, 125f, theta));
                        if (front > 0 && c.Palmar > 0)
                            foreach (var offset in c.PalmarLines)
                                palmarCrease = Mathf.Max(palmarCrease, Mathf.Exp(-Sq((x - offset * r) / (0.085f * r))) * front * c.Palmar
                                                                       * (0.6f + 0.4f * Noise(p / mm * 0.5f)));
                    }

                // the palm's three lines
                var lineCrease = 0f;
                var u = Vector3.Dot(p - wrist, uAxis);
                var along2 = u / palmLength;
                if (palmar > 0.6f && along2 > 0f && along2 < 1.02f)
                {
                    var here = new Vector2(u, Vector3.Dot(p - wrist, vAxis) - vZero);
                    foreach (var line in palmLines)
                    {
                        var best = float.MaxValue;
                        for (var i = 0; i + 1 < line.Length; i++) best = Mathf.Min(best, SegmentDistance(here, line[i], line[i + 1]));
                        // a palm line is a soft fold, deeper in some stretches than in others
                        lineCrease = Mathf.Max(lineCrease, Mathf.Exp(-Sq(best / (0.75f * mm))) * palmar * (0.45f + 0.5f * Fbm(p / mm * 0.2f)));
                    }
                }

                // how dark a fold reads: knuckle wrinkles and palm lines only a little, the nail's edge a touch
                cavity[k] = 1f - Mathf.Clamp01(Mathf.Max(Mathf.Max(0.34f * dorsalCrease, 0.6f * palmarCrease), Mathf.Max(0.5f * lineCrease, 0.5f * groove)));
                var mottle = Fbm(p / mm * 0.11f) - 0.5f;
                flush[k] = Mathf.Clamp01(0.06f + 0.22f * palmar + 0.5f * pad + 0.32f * knuckle * (1f - palmar) + 0.25f * mottle);
                var fine = Noise(p / mm * 1.15f) - 0.5f;   // the skin's own fine grain
                height[k] = (-0.14f * dorsalCrease - 0.26f * palmarCrease - 0.22f * lineCrease + 0.3f * nailMask - 0.24f * groove
                             + 0.06f * fine * (1f - nailMask)) * mm;
                var t = along2;
                opacity[k] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Mathf.Lerp(tMin, 0f, 0.2f), 0.06f, t));
            }

            // texels outside the mesh take their neighbours' values, so no dark seam shows when the maps are filtered
            var layers = new[] { nail, palm, flush, cavity, edge, opacity, height, texel };
            Dilate(layers, covered, 12);

            // ---------------------------------------------------------------- write
            var mapA = new Color32[n2];
            var mapB = new Color32[n2];
            for (var y = 0; y < Size; y++)
                for (var x = 0; x < Size; x++)
                {
                    var k = y * Size + x;
                    int xl = Mathf.Max(0, x - 1), xr = Mathf.Min(Size - 1, x + 1), yd = Mathf.Max(0, y - 1), yu = Mathf.Min(Size - 1, y + 1);
                    var step = Mathf.Max(texel[k], 1e-6f);
                    var dx = Mathf.Clamp((height[y * Size + xr] - height[y * Size + xl]) / ((xr - xl) * step), -1.4f, 1.4f);
                    var dy = Mathf.Clamp((height[yu * Size + x] - height[yd * Size + x]) / ((yu - yd) * step), -1.4f, 1.4f);
                    var n = new Vector3(-dx, -dy, 1f).normalized;
                    mapA[k] = new Color32(B(nail[k]), B(palm[k]), B(flush[k]), B(cavity[k]));
                    mapB[k] = new Color32(B(n.x * 0.5f + 0.5f), B(n.y * 0.5f + 0.5f), B(opacity[k]), B(edge[k]));
                }
            var pathA = $"{Out}/hand-{side}-a.png";
            var pathB = $"{Out}/hand-{side}-b.png";
            WriteMap(pathA, mapA);
            WriteMap(pathB, mapB);

            var matPath = $"{Materials}/HandSkin{(left ? "Left" : "Right")}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
            mat.shader = shader;
            mat.SetTexture("_MaskA", AssetDatabase.LoadAssetAtPath<Texture2D>(pathA));
            mat.SetTexture("_MaskB", AssetDatabase.LoadAssetAtPath<Texture2D>(pathB));
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);

            Debug.Log($"[SameSky:setup] hand skin {side}: mesh {mesh.name} ({verts.Length} vertices), palm {palmLength / mm:F0} units = {palmLength:F4}, " +
                      $"joint axes along {fwdSign:+0;-0}{"xyz"[fwdAxis]} ({bestFwd:F2}) up {upSign:+0;-0}{"xyz"[upAxis]} ({bestUp:F2}), " +
                      $"finger radii {fingers[0].Radius / mm:F1} {fingers[1].Radius / mm:F1} {fingers[2].Radius / mm:F1} {fingers[3].Radius / mm:F1} {fingers[4].Radius / mm:F1} mm, " +
                      $"nail texels {nailTexels}, thumb up vs hand up {Vector3.Dot(fingers[0].Up, handUp):F2}, wrist end {tMin:F2}");
        }

        // ---------------------------------------------------------------- helpers

        static float Sq(float v) => v * v;
        static byte B(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        /// <summary>The finger whose free part (knuckle to tip) passes closest to p.</summary>
        static int Nearest(Finger[] fingers, Vector3 p, out float distance, out int segment, out float along)
        {
            var best = 0;
            distance = float.MaxValue;
            segment = 0;
            along = 0;
            for (var f = 0; f < fingers.Length; f++)
            {
                var c = fingers[f].Chain;
                for (var i = 0; i + 1 < c.Length; i++)
                {
                    var ab = c[i + 1] - c[i];
                    var t = Mathf.Clamp01(Vector3.Dot(p - c[i], ab) / ab.sqrMagnitude);
                    var d = Vector3.Distance(p, c[i] + ab * t);
                    if (d < distance) { distance = d; best = f; segment = i; along = t; }
                }
            }
            return best;
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-12f));
            return Vector2.Distance(p, a + ab * t);
        }

        static void Dilate(float[][] layers, bool[] covered, int passes)
        {
            var known = (bool[])covered.Clone();
            for (var pass = 0; pass < passes; pass++)
            {
                var next = (bool[])known.Clone();
                for (var y = 0; y < Size; y++)
                    for (var x = 0; x < Size; x++)
                    {
                        var k = y * Size + x;
                        if (known[k]) continue;
                        int found = 0;
                        var from = new int[4];
                        if (x > 0 && known[k - 1]) from[found++] = k - 1;
                        if (x < Size - 1 && known[k + 1]) from[found++] = k + 1;
                        if (y > 0 && known[k - Size]) from[found++] = k - Size;
                        if (y < Size - 1 && known[k + Size]) from[found++] = k + Size;
                        if (found == 0) continue;
                        foreach (var layer in layers)
                        {
                            var v = 0f;
                            for (var i = 0; i < found; i++) v += layer[from[i]];
                            layer[k] = v / found;
                        }
                        next[k] = true;
                    }
                known = next;
            }
        }

        static void WriteMap(string path, Color32[] pixels)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = false;            // these are masks and a normal, not colours
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = false;
            ti.mipmapEnabled = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Trilinear;
            ti.anisoLevel = 4;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            var android = ti.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = Size;
            android.format = TextureImporterFormat.ASTC_4x4;
            ti.SetPlatformTextureSettings(android);
            ti.SaveAndReimport();
        }

        // value noise on the hand's own coordinates: no seams where the texture islands meet
        static float Hash(int x, int y, int z)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + y * 668265263 + z * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffffff) / (float)0xffffff;
            }
        }

        static float Noise(Vector3 p)
        {
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y), z = Mathf.FloorToInt(p.z);
            float fx = p.x - x, fy = p.y - y, fz = p.z - z;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            fz = fz * fz * (3 - 2 * fz);
            float L(float a, float b, float t) => a + (b - a) * t;
            return L(L(L(Hash(x, y, z), Hash(x + 1, y, z), fx), L(Hash(x, y + 1, z), Hash(x + 1, y + 1, z), fx), fy),
                     L(L(Hash(x, y, z + 1), Hash(x + 1, y, z + 1), fx), L(Hash(x, y + 1, z + 1), Hash(x + 1, y + 1, z + 1), fx), fy), fz);
        }

        static float Fbm(Vector3 p) => Noise(p) * 0.55f + Noise(p * 2.1f) * 0.3f + Noise(p * 4.3f) * 0.15f;
    }
}
