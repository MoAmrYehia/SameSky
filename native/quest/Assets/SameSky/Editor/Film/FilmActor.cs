// A character in the animated trailer. The two rigged adults that sit in the app's lounge (Microsoft Rocketbox, MIT)
// act here: their seated breathing loop underneath, and on top of it what a shot asks for, all keyframed in
// FilmShots.cs:
//   * each arm reaches for a goal (two-bone IK), the hand turned the way the shot needs, fingers closing to a grip
//   * the eyes lead and the head follows toward a look target; blinks on their own and where a shot places them
//   * the torso leans, turns and settles; shoulders rise and drop
//   * the face: any of the model's shapes (smile, brows, lids, jaw), and lip-sync from a line's viseme timings
// Nothing here is a MonoBehaviour: the renderer (FilmRenderer.cs) asks every actor for its pose once per frame, in
// Edit mode, so a shot renders the same way every time.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SameSky.Film
{
    /// <summary>One arm: where the wrist should be, how the hand is turned, how far the fingers close.</summary>
    public sealed class FilmArm
    {
        public readonly Transform Upper, Lower, Hand;
        public readonly bool IsLeft;
        readonly Vector3 _fingersLocal, _palmLocal;
        readonly Transform[][] _digits = new Transform[5][];   // thumb, index, middle, ring, little: three joints each
        static readonly float[] FingerCurl = { 72f, 84f, 52f }, ThumbCurl = { 26f, 24f, 30f };

        /// <summary>World position for the wrist, and how much of it to take (0 leaves the seated pose alone).</summary>
        public readonly Track3 Goal = new();
        public readonly Track Weight = new(0f);
        /// <summary>A goal worked out afresh for each frame (something that moves: another person, a thing being
        /// carried). When it gives a point, that is used instead of Goal.</summary>
        public Func<float, Vector3?> Live;
        /// <summary>World directions: along the fingers, and the way the palm faces. Aim is how much to turn the hand.</summary>
        public readonly Track3 Fingers = new(), Palm = new();
        public readonly Track Aim = new(0f);
        /// <summary>Where the elbow should bend toward (a world direction; down and out by default).</summary>
        public Vector3 Elbow;
        /// <summary>0 an open hand, 1 fingers closed; the thumb follows unless given its own.</summary>
        public readonly Track Grip = new(0f);
        public readonly Track Thumb = new();
        public readonly Track Index = new();   // the index finger alone, for a tap or a point (falls back to Grip)
        /// <summary>Where a fingertip should be (world), how much of the way, and which finger (0 thumb .. 4 little).</summary>
        public readonly Track3 Touch = new();
        public readonly Track TouchWeight = new(0f);
        public int TouchDigit = 1;

        public FilmArm(Animator a, bool left)
        {
            IsLeft = left;
            Upper = a.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            Lower = a.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            Hand = a.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var middle = a.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            var index = a.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            var little = a.GetBoneTransform(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
            var fingers = (middle.position - Hand.position).normalized;
            var across = (index.position - little.position).normalized;
            var palm = (left ? Vector3.Cross(across, fingers) : Vector3.Cross(fingers, across)).normalized;
            var inverse = Quaternion.Inverse(Hand.rotation);
            _fingersLocal = inverse * fingers;
            _palmLocal = inverse * palm;
            Elbow = new Vector3(left ? -0.5f : 0.5f, -1f, -0.3f);
            var first = (int)(left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
            for (var d = 0; d < 5; d++)
            {
                _digits[d] = new Transform[3];
                for (var j = 0; j < 3; j++) _digits[d][j] = a.GetBoneTransform((HumanBodyBones)(first + d * 3 + j));
            }
        }

        /// <summary>The tip of a finger (0 thumb .. 4 little): the last joint, a little way along it.</summary>
        public Vector3 Tip(int digit)
        {
            Transform last = _digits[digit][2], before = _digits[digit][1];
            return last.position + (last.position - before.position).normalized * 0.022f;
        }

        /// <summary>Closes the fingers on top of the pose the hand already has: each joint turns about the line across
        /// the knuckles, the thumb comes in under the palm.</summary>
        public void Curl(float t)
        {
            var grip = Grip.At(t);
            var thumb = Thumb.Empty ? grip : Thumb.At(t);
            var index = Index.Empty ? grip : Index.At(t);
            if (Mathf.Abs(grip) < 0.001f && Mathf.Abs(thumb) < 0.001f && Mathf.Abs(index) < 0.001f) return;
            if (_digits[1][0] == null || _digits[4][0] == null) return;
            var across = (_digits[1][0].position - _digits[4][0].position).normalized;
            var hinge = IsLeft ? across : -across;
            for (var d = 1; d < 5; d++)
            {
                var amount = Mathf.Clamp(d == 1 ? index : grip, -0.6f, 1.2f);
                if (Mathf.Abs(amount) < 0.001f) continue;
                for (var j = 0; j < 3; j++)
                {
                    var bone = _digits[d][j];
                    if (bone != null) bone.rotation = Quaternion.AngleAxis(FingerCurl[j] * amount, hinge) * bone.rotation;
                }
            }
            if (Mathf.Abs(thumb) < 0.001f || _digits[0][0] == null) return;
            thumb = Mathf.Clamp(thumb, -0.6f, 1.2f);
            Vector3 fingers = FingersNow, palm = PalmNow;
            var swing = IsLeft ? -fingers : fingers;
            var adduct = IsLeft ? palm : -palm;
            _digits[0][0].rotation = Quaternion.AngleAxis(ThumbCurl[0] * thumb, swing) * Quaternion.AngleAxis(10f * thumb, adduct) * _digits[0][0].rotation;
            if (_digits[0][1] != null) _digits[0][1].rotation = Quaternion.AngleAxis(ThumbCurl[1] * thumb, swing) * _digits[0][1].rotation;
            if (_digits[0][2] != null) _digits[0][2].rotation = Quaternion.AngleAxis(ThumbCurl[2] * thumb, swing) * _digits[0][2].rotation;
        }

        public Vector3 FingersNow => Hand.rotation * _fingersLocal;
        public Vector3 PalmNow => Hand.rotation * _palmLocal;
        /// <summary>The middle of the palm: where a held thing sits.</summary>
        public Vector3 PalmPoint => Hand.position + FingersNow * 0.075f + PalmNow * 0.028f;

        public void Solve(float t, Quaternion facing)
        {
            var weight = Mathf.Clamp01(Weight.At(t));
            var live = weight > 0.0005f ? Live?.Invoke(t) : null;
            if (live.HasValue) Reach(Vector3.Lerp(Hand.position, live.Value, weight), facing);
            else if (weight > 0.0005f && !Goal.Empty) Reach(Vector3.Lerp(Hand.position, Goal.At(t), weight), facing);
            var aim = Mathf.Clamp01(Aim.At(t));
            if (aim > 0.0005f && !Fingers.Empty && !Palm.Empty)
            {
                var f = Fingers.At(t);
                var p = Palm.At(t);
                if (f.sqrMagnitude > 1e-6f && p.sqrMagnitude > 1e-6f)
                {
                    var want = Quaternion.LookRotation(f.normalized, p.normalized) * Quaternion.Inverse(Quaternion.LookRotation(_fingersLocal, _palmLocal));
                    Hand.rotation = Quaternion.Slerp(Hand.rotation, want, aim);
                }
            }
        }

        /// <summary>After the fingers have closed: brings a fingertip onto its touch target by moving the wrist the
        /// rest of the way (the hand keeps the turn it has).</summary>
        public void TouchUp(float t, Quaternion facing)
        {
            var touch = Mathf.Clamp01(TouchWeight.At(t));
            if (touch < 0.0005f || Touch.Empty) return;
            for (var pass = 0; pass < 2; pass++)
            {
                var delta = (Touch.At(t) - Tip(TouchDigit)) * touch;
                if (delta.sqrMagnitude < 1e-9f) return;
                var turn = Hand.rotation;
                Reach(Hand.position + delta, facing);
                Hand.rotation = turn;
            }
        }

        void Reach(Vector3 target, Quaternion facing)
        {
            {
                var shoulder = Upper.position;
                float upper = (Lower.position - Upper.position).magnitude, lower = (Hand.position - Lower.position).magnitude;
                var to = target - shoulder;
                var distance = Mathf.Clamp(to.magnitude, 0.05f, (upper + lower) * 0.999f);
                var direction = to.normalized;
                // the elbow: on the circle around the shoulder-to-wrist line, toward the hint
                var hint = facing * Elbow;
                var bend = hint - direction * Vector3.Dot(hint, direction);
                bend = bend.sqrMagnitude > 1e-6f ? bend.normalized : Vector3.down;
                var along = (upper * upper - lower * lower + distance * distance) / (2f * distance);
                var height = Mathf.Sqrt(Mathf.Max(0f, upper * upper - along * along));
                var elbow = shoulder + direction * along + bend * height;
                Upper.rotation = Quaternion.FromToRotation(Lower.position - Upper.position, elbow - Upper.position) * Upper.rotation;
                Lower.rotation = Quaternion.FromToRotation(Hand.position - Lower.position, shoulder + direction * distance - Lower.position) * Lower.rotation;
            }
        }
    }

    /// <summary>A spoken line's mouth shapes: the synthesizer's viseme events (scripts/demo/trailer_voices.ps1).</summary>
    [Serializable]
    public sealed class SpeechData
    {
        [Serializable] public sealed class Viseme { public float t; public int v; }
        public float duration;
        public Viseme[] visemes;
    }

    public sealed class FilmActor
    {
        public readonly string Name;
        public readonly GameObject Root;
        public readonly Animator Animator;
        public readonly FilmArm Left, Right;
        readonly SkinnedMeshRenderer _face;
        // what is drawn: the posed mesh, baked from the bones once they are where the frame wants them (in this
        // render path the skinned mesh itself is drawn from the Animator's pose, before anything a shot adds)
        readonly List<Posed> _posed = new();
        sealed class Posed
        {
            public SkinnedMeshRenderer Source;
            public Mesh Mesh, HairMesh;
            public Transform Holder;
            public MeshRenderer Drawn, Hair;
            public int HairAt = -1;
            public int[] HairIndex;       // the vertices the hair is made of
            public float[] HairLoose;     // how free each of them is to move (0 at the scalp and the lashes)
            public float[] HairPhase;
        }
        static readonly List<Vector3> Verts = new(), Normals = new();
        static readonly List<Vector4> Tangents = new();
        static readonly List<Vector2> Uvs = new();
        readonly Transform _stage;
        readonly Dictionary<string, int> _shapes = new();
        readonly Transform _head, _neck, _spine, _chest, _upperChest, _eyeL, _eyeR, _clavL, _clavR;
        Vector3 _faceForward;
        Vector3 _faceRest, _faceRight;
        float _facePitch;

        /// <summary>A correction to where the face is taken to point, in degrees up from what the skeleton's rest pose
        /// says (the rest pose of these figures carries the head a little forward).</summary>
        public float FacePitch
        {
            get => _facePitch;
            set { _facePitch = value; _faceForward = Quaternion.AngleAxis(-value, _faceRight) * _faceRest; }
        }
        readonly Quaternion _eyeLRest, _eyeRRest;
        readonly float _seed;
        readonly Vector3 _homePosition;
        readonly Quaternion _homeRotation;

        // ---- what a shot asks of the body
        /// <summary>Where the person is looking (world), and how much the head (0..1) and the eyes (0..1) go there.</summary>
        public readonly Track3 Look = new();
        public readonly Track LookHead = new(0f), LookEyes = new(0f);
        /// <summary>Something that moves to look at (another person's face): when it gives a point, that is where the
        /// person looks, instead of Look.</summary>
        public Func<float, Vector3?> LookLive;
        /// <summary>Degrees: lean forward, turn to the person's left, lean to their right; shoulders up; head nod (down) and tilt (to their right).</summary>
        public readonly Track Lean = new(0f), Twist = new(0f), Side = new(0f), Shrug = new(0f), Nod = new(0f), Tilt = new(0f);
        /// <summary>The whole person: a small move (metres, world) and a turn (degrees) from where they were put.</summary>
        public readonly Track3 Offset = new();
        public readonly Track Turn = new(0f);
        /// <summary>For a standing figure: where they are (world) and which way they face (degrees), over time. Left
        /// empty, the person stays where they were put.</summary>
        public readonly Track3 Path = new();
        public readonly Track Heading = new();
        /// <summary>Wind in the hair: how hard it blows (0 none, 1 a stiff breeze) and from where to where (world).</summary>
        public readonly Track Wind = new(0f);
        public Vector3 WindTo = Vector3.right;
        readonly List<Vector3> _hairVerts = new();

        // ---- motion clips (standing figures): what plays from when, cross-faded
        sealed class Motion
        {
            public float Start, Speed, Offset, Fade;
            public AnimationClip Clip;
            public AnimationClipPlayable Playable;
            public int Port;
        }
        readonly List<Motion> _motions = new();
        PlayableGraph _graph;
        AnimationMixerPlayable _mixer;
        public bool Standing => _graph.IsValid();
        /// <summary>Face shapes by name (the model's ARKit set: "MouthSmileLeft", "BrowInnerUp", "EyeWideLeft"...), 0..100.</summary>
        public readonly Dictionary<string, Track> Face = new();
        /// <summary>Blink at these times; AutoBlink adds the ordinary ones in between.</summary>
        public readonly List<float> Blinks = new();
        public bool AutoBlink = true;
        /// <summary>How far the head turns toward a look target, in degrees from the chest; the eyes go a little further.</summary>
        public float HeadYawLimit = 70f, HeadDownLimit = 26f, HeadUpLimit = 24f, EyeLimit = 28f;
        /// <summary>How far the lids rest closed (0 wide open), and how much the eyes dart about (0 a fixed stare).</summary>
        public float Lids = 0.12f, EyeLife = 1f;
        readonly List<(float start, SpeechData line, float strength)> _lines = new();

        public Transform Head => _head;
        public Vector3 Eyes => _eyeL != null ? (_eyeL.position + _eyeR.position) * 0.5f : _head.position;
        public Quaternion Facing => Root.transform.rotation;
        /// <summary>The way the face points now.</summary>
        public Vector3 FaceForward => _head.rotation * _faceForward;

        /// <summary>One of the app's two seated figures, with their seated loop (the first trailer's people).</summary>
        public FilmActor(string name, Transform parent, Vector3 position, float yaw, float seed)
            : this(name, Seated(name, parent), parent, position, yaw, seed, false) { }

        static GameObject Seated(string name, Transform parent)
        {
            var prefab = Resources.Load<GameObject>("Characters/" + name);
            if (prefab == null) throw new InvalidOperationException("no character prefab: " + name);
            return UnityEngine.Object.Instantiate(prefab, parent);
        }

        /// <summary>A figure already on the stage (FilmCast). Standing figures have no loop of their own: a shot gives
        /// them motion clips (Play, Walk) and they stand in their rest pose until it does.</summary>
        public FilmActor(string name, GameObject figure, Transform parent, Vector3 position, float yaw, float seed, bool standing = true)
        {
            Name = name;
            _seed = seed;
            _stage = parent;
            Root = figure;
            Root.name = name;
            Root.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            _homePosition = position;
            _homeRotation = Root.transform.rotation;
            Animator = Root.GetComponentInChildren<Animator>(true);
            Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Animator.applyRootMotion = false;
            if (standing)
            {
                Animator.runtimeAnimatorController = null;
                _graph = PlayableGraph.Create("film " + name);
                _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                _mixer = AnimationMixerPlayable.Create(_graph, 0);
                var output = AnimationPlayableOutput.Create(_graph, "pose", Animator);
                output.SetSourcePlayable(_mixer);
            }
            Animator.Rebind();
            Animator.Update(0f);
            foreach (var r in Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                r.updateWhenOffscreen = true;
                if (_face != null || r.sharedMesh == null || r.sharedMesh.blendShapeCount == 0) continue;
                _face = r;
                for (var i = 0; i < r.sharedMesh.blendShapeCount; i++)
                {
                    var full = r.sharedMesh.GetBlendShapeName(i);
                    var cut = full.LastIndexOf('_');
                    if (cut >= 0) _shapes[full.Substring(cut + 1)] = i;
                    _shapes[full] = i;
                }
            }
            _head = Animator.GetBoneTransform(HumanBodyBones.Head);
            _neck = Animator.GetBoneTransform(HumanBodyBones.Neck);
            _spine = Animator.GetBoneTransform(HumanBodyBones.Spine);
            _chest = Animator.GetBoneTransform(HumanBodyBones.Chest);
            _upperChest = Animator.GetBoneTransform(HumanBodyBones.UpperChest);
            _clavL = Animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            _clavR = Animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            foreach (var t in _head.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.EndsWith(" LEye")) _eyeL = t;
                else if (t.name.EndsWith(" REye")) _eyeR = t;
            }
            // Which way the face points, in the head bone's own terms. The seated pose already has the head tipped
            // (down toward the table), so this is read from the skeleton's rest pose, where the head is level and the
            // figure faces along its root: otherwise every look would leave the face tipped down by that much.
            var rest = Quaternion.identity;
            var bind = new Dictionary<string, Quaternion>();
            foreach (var bone in Animator.avatar.humanDescription.skeleton) bind[bone.name] = bone.rotation;
            for (var t = _head; t != null && t != Animator.transform; t = t.parent)
                rest = (bind.TryGetValue(t.name, out var local) ? local : t.localRotation) * rest;
            _faceForward = Quaternion.Inverse(rest) * Vector3.forward;
            if (_eyeL != null && _eyeR != null)
            {
                // (sideways it must still agree with the line between the eyes)
                var across = (_eyeR.position - _eyeL.position).normalized;
                var level = Vector3.Cross(across, Vector3.up).normalized;
                if (Vector3.Dot(level, Root.transform.forward) < 0f) level = -level;
                var now = _head.rotation * _faceForward;
                if (Vector3.Dot(now, level) < 0.5f)
                {
                    Debug.LogWarning($"[SameSky:film] {name}: the rest pose gives no usable face direction, using the eye line");
                    _faceForward = Quaternion.Inverse(_head.rotation) * level;
                }
                var toHead = Quaternion.Inverse(_head.rotation);
                _eyeLRest = toHead * _eyeL.rotation;
                _eyeRRest = toHead * _eyeR.rotation;
            }
            _faceRest = _faceForward;
            _faceRight = Quaternion.Inverse(_head.rotation) * (Root.transform.rotation * Vector3.right);
            Debug.Log($"[SameSky:film] {name}: in the seated pose the face points {Mathf.Asin(Mathf.Clamp((_head.rotation * _faceForward).y, -1f, 1f)) * Mathf.Rad2Deg:F1} degrees above level");
            Left = new FilmArm(Animator, true);
            Right = new FilmArm(Animator, false);
        }

        void MakePosed()
        {
            foreach (var r in Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.sharedMesh == null) continue;
                var holder = new GameObject($"{Name} {r.name} (posed)");
                holder.transform.SetParent(_stage, false);
                holder.layer = r.gameObject.layer;
                var mesh = new Mesh { name = holder.name };
                mesh.MarkDynamic();
                holder.AddComponent<MeshFilter>().sharedMesh = mesh;
                var drawn = holder.AddComponent<MeshRenderer>();
                var mats = r.sharedMaterials;
                var posed = new Posed { Source = r, Mesh = mesh, Holder = holder.transform, Drawn = drawn };
                // hair and lashes (the last material) are drawn by a renderer of their own, so they can go without
                // casting shadows: cut-out hair cards throw hard, cap-like shadows across a forehead
                var hairAt = mats.Length - 1;
                if (mats.Length > 1 && mats[hairAt] != null && (mats[hairAt].name.Contains("opacity") || mats[hairAt].name.Contains("hair")))
                {
                    posed.HairAt = hairAt;
                    var hair = new GameObject("Hair");
                    hair.transform.SetParent(holder.transform, false);
                    hair.layer = holder.layer;
                    posed.HairMesh = new Mesh { name = holder.name + " hair" };
                    posed.HairMesh.MarkDynamic();
                    hair.AddComponent<MeshFilter>().sharedMesh = posed.HairMesh;
                    posed.Hair = hair.AddComponent<MeshRenderer>();
                    posed.Hair.sharedMaterial = mats[hairAt];
                    posed.Hair.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    posed.Hair.receiveShadows = true;
                    var rest = new Material[hairAt];
                    Array.Copy(mats, rest, hairAt);
                    mats = rest;
                }
                drawn.sharedMaterials = mats;
                drawn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                drawn.receiveShadows = true;
                r.forceRenderingOff = true;
                _posed.Add(posed);
            }
        }

        /// <summary>The drawn copies of this person's meshes (for a shot that hides them, or changes a material).</summary>
        public IEnumerable<MeshRenderer> Drawn()
        {
            if (_posed.Count == 0) MakePosed();
            foreach (var p in _posed)
            {
                yield return p.Drawn;
                if (p.Hair != null) yield return p.Hair;
            }
        }

        /// <summary>For a shot that shows someone standing from the waist up: the seated legs are folded away to
        /// nothing, and the person can be raised to standing height with Offset.</summary>
        public void HideLegs()
        {
            foreach (var bone in new[] { HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg })
            {
                var leg = Animator.GetBoneTransform(bone);
                if (leg != null) leg.localScale = Vector3.one * 0.0001f;
            }
        }

        /// <summary>The same key on the left and right of a pair of face shapes ("MouthSmile" -> MouthSmileLeft/Right).</summary>
        public void Both(string shape, float t, float value, Ease ease = Ease.InOut)
        {
            Shape(shape + "Left").Key(t, value, ease);
            Shape(shape + "Right").Key(t, value * 0.94f, ease);
        }

        /// <summary>A smile that reaches the eyes: mouth corners, cheeks and a little squint together.</summary>
        public void Smile(float t, float amount, Ease ease = Ease.InOut)
        {
            amount = Mathf.Min(100f, amount * SmileGain);
            Both("MouthSmile", t, amount, ease);
            Both("CheekSquint", t, amount * 0.6f, ease);
            Both("EyeSquint", t, amount * 0.3f, ease);
        }

        /// <summary>How much of a smile this face needs for it to read (some faces are sterner at rest).</summary>
        public float SmileGain = 1f;

        /// <summary>Where the person looks from time t on. The eyes get there in a few frames; the head follows them
        /// (it turns toward where the eyes were a moment ago), as far as <paramref name="head"/> says.</summary>
        public void Gaze(float t, Vector3 target, float head = 0.75f, float eyes = 1f, bool blink = false)
        {
            if (t <= 0f)
            {
                Look.Key(0f, target);
                LookEyes.Key(0f, eyes);
                LookHead.Key(0f, head);
                return;
            }
            if (!Look.Empty) Look.Key(t, Look.At(t));
            Look.Key(t + 0.1f, target, Ease.Out);
            LookEyes.Key(t, LookEyes.At(t)).Key(t + 0.12f, eyes);
            LookHead.Key(t, LookHead.At(t)).Key(t + 0.55f, head, Ease.Soft);
            if (blink) Blinks.Add(t + 0.03f);
        }

        public Track Shape(string name)
        {
            if (!Face.TryGetValue(name, out var track)) Face[name] = track = new Track();
            return track;
        }

        /// <summary>A spoken line starting at <paramref name="start"/>: its mouth shapes drive the lips.</summary>
        public void Say(float start, string visemeFile, float strength = 1f)
        {
            if (!File.Exists(visemeFile)) { Debug.LogWarning("[SameSky:film] no viseme file " + visemeFile); return; }
            var data = JsonUtility.FromJson<SpeechData>(File.ReadAllText(visemeFile));
            if (data?.visemes == null || data.visemes.Length == 0) return;
            // The desktop voices speak at 16 kHz, and report when each mouth shape comes as if the file were written at
            // that rate; the file is written at 44.1 kHz, so the times come out 2.76 times too long (measured against
            // the sound: the last shape of each line then falls where its last word ends). A line whose shapes run
            // past its own length is on that clock.
            if (data.visemes[data.visemes.Length - 1].t > data.duration * 1.15f)
                foreach (var v in data.visemes) v.t *= 16000f / 44100f;
            _lines.Add((start, data, strength));
        }

        // the synthesizer's 22 visemes -> the model's 15 mouth shapes
        static readonly string[] SapiToShape =
        {
            "Sil", "aa", "aa", "O", "E", "RR", "I", "U", "O", "aa", "O", "aa", "aa", "RR", "nn", "SS", "CH", "TH", "FF", "DD", "KK", "PP",
        };
        static readonly float[] SapiStrength =
        {
            0f, 0.75f, 0.9f, 0.85f, 0.7f, 0.6f, 0.65f, 0.85f, 0.85f, 0.85f, 0.85f, 0.85f, 0.3f, 0.6f, 0.55f, 0.7f, 0.75f, 0.7f, 0.9f, 0.6f, 0.55f, 1f,
        };

        void Mouth(float t, Dictionary<string, float> weights)
        {
            foreach (var (start, line, strength) in _lines)
            {
                var local = t - start;
                if (local < -0.1f || local > line.duration + 0.15f) continue;
                for (var i = 0; i < line.visemes.Length; i++)
                {
                    var v = line.visemes[i];
                    if (v.v <= 0 || v.v >= SapiToShape.Length) continue;
                    var end = i + 1 < line.visemes.Length ? line.visemes[i + 1].t : line.duration;
                    // each shape eases in a little before its sound and out a little after: lips move ahead of the voice
                    var rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(v.t - 0.07f, v.t + 0.02f, local));
                    var fall = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(end - 0.02f, end + 0.08f, local));
                    var w = rise * fall * SapiStrength[v.v] * strength * 0.56f;
                    if (w <= 0f) continue;
                    var name = SapiToShape[v.v];
                    weights[name] = Mathf.Max(weights.TryGetValue(name, out var had) ? had : 0f, w);
                }
            }
        }

        float BlinkAt(float t)
        {
            var lids = 0f;
            void One(float at)
            {
                var u = (t - at) / 0.17f;
                if (u < 0f || u > 1f) return;
                lids = Mathf.Max(lids, u < 0.35f ? u / 0.35f : 1f - (u - 0.35f) / 0.65f);
            }
            foreach (var b in Blinks) One(b);
            if (AutoBlink)
            {
                // ordinary blinks: one somewhere in every stretch of about three and a half seconds, never on a beat
                const float every = 3.4f;
                for (var n = Mathf.FloorToInt(t / every) - 1; n <= Mathf.FloorToInt(t / every); n++)
                {
                    if (n < 0) continue;
                    var at = (n + 0.2f + 0.6f * Mathf.PerlinNoise(n * 0.731f + _seed * 3.3f, _seed * 11.7f)) * every;
                    if (Blinks.Any(b => Mathf.Abs(b - at) < 0.5f)) continue;
                    One(at);
                }
            }
            return lids;
        }

        /// <summary>Poses the whole person for time <paramref name="t"/>; <paramref name="dt"/> moves the breathing loop on.</summary>
        public void Update(float t, float dt)
        {
            var at = (Path.Empty ? _homePosition : Path.At(t)) + (Offset.Empty ? Vector3.zero : Offset.At(t));
            var heading = Heading.Empty ? _homeRotation : Quaternion.Euler(0, Heading.At(t), 0);
            Root.transform.SetPositionAndRotation(at, heading * Quaternion.Euler(0, Turn.At(t), 0));
            if (_graph.IsValid()) Move(t); else Animator.Update(dt);

            var facing = Root.transform.rotation;
            Vector3 right = facing * Vector3.right, up = Vector3.up, forward = facing * Vector3.forward;
            void Turn3(Transform bone, Vector3 axis, float degrees) { if (bone != null && Mathf.Abs(degrees) > 0.001f) bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation; }

            // the torso: breathing is in the loop; a shot adds a lean, a turn, a settle
            float lean = Lean.At(t), twist = Twist.At(t), side = Side.At(t);
            Turn3(_spine, right, lean * 0.45f); Turn3(_chest, right, lean * 0.35f); Turn3(_upperChest, right, lean * 0.2f);
            Turn3(_spine, up, -twist * 0.3f); Turn3(_chest, up, -twist * 0.35f); Turn3(_upperChest, up, -twist * 0.35f);
            Turn3(_spine, forward, side * 0.5f); Turn3(_chest, forward, side * 0.5f);

            // The head turns toward what the person looks at, as far as a neck goes: well to either side, less up and
            // down (the eyes do the rest). On this skeleton the shoulders hang off the neck bone, so they are put back
            // where they were once the neck has turned, and the arms are solved after that.
            var headWeight = Mathf.Clamp01(LookHead.At(t));
            var eyeWeight = Mathf.Clamp01(LookEyes.At(t));
            var live = LookLive?.Invoke(t);
            var target = live ?? (Look.Empty ? _head.position + forward : Look.At(t));
            // the head turns toward where the eyes were a moment ago
            var headTarget = live ?? (Look.Empty ? target : (Look.At(t - 0.12f) + Look.At(t - 0.24f) + Look.At(t - 0.36f)) / 3f);
            Vector3 clavLAt = default, clavRAt = default;
            Quaternion clavLTurn = default, clavRTurn = default;
            if (_clavL != null) { clavLAt = _clavL.position; clavLTurn = _clavL.rotation; }
            if (_clavR != null) { clavRAt = _clavR.position; clavRTurn = _clavR.rotation; }
            if (headWeight > 0.001f && (!Look.Empty || live.HasValue))
            {
                var chest = facing * Quaternion.Euler(0f, -twist, 0f);
                var toChest = Quaternion.Inverse(chest);
                Vector3 nowLocal = toChest * (_head.rotation * _faceForward), wantLocal = toChest * (headTarget - _head.position).normalized;
                float nowYaw = Mathf.Atan2(nowLocal.x, nowLocal.z) * Mathf.Rad2Deg, nowPitch = Mathf.Asin(Mathf.Clamp(nowLocal.y, -1f, 1f)) * Mathf.Rad2Deg;
                var wantYaw = Mathf.Clamp(Mathf.Atan2(wantLocal.x, wantLocal.z) * Mathf.Rad2Deg, -HeadYawLimit, HeadYawLimit);
                var wantPitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(wantLocal.y, -1f, 1f)) * Mathf.Rad2Deg, -HeadDownLimit, HeadUpLimit);
                float yaw = Mathf.LerpAngle(nowYaw, wantYaw, headWeight), pitch = Mathf.Lerp(nowPitch, wantPitch, headWeight);
                var aim = chest * (Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward);
                for (var pass = 0; pass < 2; pass++)
                {
                    var bone = pass == 0 ? _neck : _head;
                    if (bone == null) continue;
                    var turn = Quaternion.FromToRotation(_head.rotation * _faceForward, aim);
                    bone.rotation = (pass == 0 ? Quaternion.Slerp(Quaternion.identity, turn, 0.35f) : turn) * bone.rotation;
                }
            }
            Turn3(_head, right, Nod.At(t));
            Turn3(_head, forward, Tilt.At(t));
            if (_clavL != null) _clavL.SetPositionAndRotation(clavLAt, clavLTurn);
            if (_clavR != null) _clavR.SetPositionAndRotation(clavRAt, clavRTurn);
            var shrug = Shrug.At(t);
            Turn3(_clavL, forward, shrug); Turn3(_clavR, forward, -shrug);

            Left.Solve(t, facing);
            Right.Solve(t, facing);
            Left.Curl(t);
            Right.Curl(t);
            Left.TouchUp(t, facing);
            Right.TouchUp(t, facing);
            var lowered = 0f;
            if (_eyeL != null)
            {
                var neutral = _head.rotation * _faceForward;
                var want = Vector3.Slerp(neutral, (target - Eyes).normalized, eyeWeight);
                var angle = Vector3.Angle(neutral, want);
                if (angle > EyeLimit) want = Vector3.Slerp(neutral, want, EyeLimit / angle);
                if (EyeLife > 0f)
                {
                    // eyes never hold perfectly still: a small quick shift every second or so
                    Vector2 Dart(int n) => new Vector2(Mathf.PerlinNoise(n * 0.37f + _seed * 5.1f, 1.7f) - 0.5f, Mathf.PerlinNoise(2.9f, n * 0.41f + _seed * 3.7f) - 0.5f);
                    const float every = 1.15f;
                    var beat = t / every + _seed * 0.37f;
                    var n0 = Mathf.FloorToInt(beat);
                    var dart = Vector2.Lerp(Dart(n0 - 1), Dart(n0), Mathf.SmoothStep(0f, 1f, (beat - n0) * every / 0.06f));
                    want = Quaternion.AngleAxis(dart.x * 3.2f * EyeLife, up) * Quaternion.AngleAxis(dart.y * 1.6f * EyeLife, right) * want;
                }
                var turn = Quaternion.FromToRotation(neutral, want);
                _eyeL.rotation = turn * (_head.rotation * _eyeLRest);
                _eyeR.rotation = turn * (_head.rotation * _eyeRRest);
                lowered = Mathf.Clamp01((neutral.y - want.y) * 2.5f);
            }

            if (_face != null) PoseFace(t, lowered);
            if (_posed.Count == 0) MakePosed();
            foreach (var p in _posed)
            {
                p.Source.BakeMesh(p.Mesh, true);
                p.Mesh.RecalculateBounds();
                p.Holder.SetPositionAndRotation(p.Source.transform.position, p.Source.transform.rotation);
                if (p.HairMesh == null) continue;
                p.Mesh.GetVertices(Verts);
                p.Mesh.GetNormals(Normals);
                p.Mesh.GetTangents(Tangents);
                BlowHair(p, t);
                if (p.HairMesh.vertexCount != Verts.Count)
                {
                    p.Mesh.GetUVs(0, Uvs);
                    p.HairMesh.Clear();
                    p.HairMesh.SetVertices(Verts);
                    p.HairMesh.SetNormals(Normals);
                    p.HairMesh.SetTangents(Tangents);
                    p.HairMesh.SetUVs(0, Uvs);
                    p.HairMesh.SetTriangles(p.Mesh.GetTriangles(p.HairAt), 0);
                }
                else
                {
                    p.HairMesh.SetVertices(Verts);
                    p.HairMesh.SetNormals(Normals);
                    p.HairMesh.SetTangents(Tangents);
                }
                p.HairMesh.RecalculateBounds();
            }
        }

        /// <summary>Moves the loose ends of the hair with the wind (in the drawn copy of the hair only).</summary>
        void BlowHair(Posed p, float t)
        {
            var wind = Wind.At(t);
            if (wind <= 0.001f || _head == null) return;
            var toFigure = Quaternion.Inverse(p.Holder.rotation);
            if (p.HairIndex == null)
            {
                // which vertices are hair, and how far each hangs below the crown: measured once, as the figure stands
                var seen = new HashSet<int>(p.Mesh.GetTriangles(p.HairAt));
                p.HairIndex = seen.ToArray();
                p.HairLoose = new float[p.HairIndex.Length];
                p.HairPhase = new float[p.HairIndex.Length];
                var head = p.Holder.InverseTransformPoint(_head.position);
                var up = toFigure * Vector3.up;
                var fore = toFigure * (Root.transform.rotation * Vector3.forward);
                var side = Vector3.Cross(up, fore);
                for (var i = 0; i < p.HairIndex.Length; i++)
                {
                    var rel = Verts[p.HairIndex[i]] - head;
                    float y = Vector3.Dot(rel, up), z = Vector3.Dot(rel, fore), x = Vector3.Dot(rel, side);
                    var low = Mathf.Clamp01((0.075f - y) / 0.13f);
                    var lashes = z > 0.055f && Mathf.Abs(x) < 0.06f && y > 0.02f;      // lashes and brows stay put
                    p.HairLoose[i] = lashes ? 0f : low * low * (3f - 2f * low);
                    p.HairPhase[i] = x * 7f + z * 5f + y * 4f;
                }
            }
            var push = toFigure * WindTo.normalized;
            var lift = toFigure * Vector3.up;
            var gust = 0.65f + 0.35f * Mathf.Sin(t * 1.9f + _seed) + 0.2f * Mathf.Sin(t * 4.3f + _seed * 2f);
            for (var i = 0; i < p.HairIndex.Length; i++)
            {
                var loose = p.HairLoose[i];
                if (loose <= 0f) continue;
                var flutter = Mathf.Sin(t * 6.5f + p.HairPhase[i]) * 0.3f + Mathf.Sin(t * 10.3f + p.HairPhase[i] * 1.6f) * 0.1f;
                var amount = wind * loose * 0.026f * (gust + flutter);
                Verts[p.HairIndex[i]] += push * amount + lift * (amount * 0.3f);
            }
        }

        void PoseFace(float t, float lowered)
        {
            var weights = new Dictionary<string, float>();
            foreach (var kv in Face) weights[kv.Key] = kv.Value.At(t) / 100f;
            Mouth(t, weights);
            var lid = Mathf.Max(BlinkAt(t), Lids + lowered * 0.24f);
            foreach (var name in new[] { "EyeBlinkLeft", "EyeBlinkRight" })
                weights[name] = Mathf.Max(weights.TryGetValue(name, out var had) ? had : 0f, lid);
            // every shape is set every frame: one this frame doesn't name goes back to rest
            var mesh = _face.sharedMesh;
            var applied = new float[mesh.blendShapeCount];
            foreach (var kv in weights)
                if (_shapes.TryGetValue(kv.Key, out var index)) applied[index] = Mathf.Max(applied[index], Mathf.Clamp01(kv.Value) * 100f);
            for (var i = 0; i < applied.Length; i++) _face.SetBlendShapeWeight(i, applied[i]);
        }

        public void Dispose()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }

        // ------------------------------------------------------------ motion clips
        /// <summary>From time <paramref name="t"/> the person does this (a Rocketbox clip by name), cross-faded from
        /// whatever they were doing over <paramref name="fade"/> seconds. <paramref name="offset"/> is where in the
        /// clip to begin, in seconds.</summary>
        public FilmActor Play(float t, string clip, float fade = 0.3f, float speed = 1f, float offset = 0f)
        {
            if (!_graph.IsValid()) throw new InvalidOperationException(Name + " is a seated figure: no motion clips");
            var c = FilmCast.Clip(clip);
            var playable = AnimationClipPlayable.Create(_graph, c);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            var port = _mixer.GetInputCount();
            _mixer.SetInputCount(port + 1);
            _graph.Connect(playable, 0, _mixer, port);
            _mixer.SetInputWeight(port, 0f);
            _motions.Add(new Motion { Start = t, Speed = speed, Offset = offset, Fade = fade, Clip = c, Playable = playable, Port = port });
            _motions.Sort((a, b) => a.Start.CompareTo(b.Start));
            return this;
        }

        /// <summary>Walks in a straight line from <paramref name="from"/> to <paramref name="to"/> starting at
        /// <paramref name="t"/>, at the pace the clip's feet need (times <paramref name="speed"/>). Returns the time
        /// of arrival. <paramref name="turn"/>: seconds over which the person turns into the new direction.</summary>
        public float Walk(float t, Vector3 from, Vector3 to, string clip, float speed = 1f, float fade = 0.3f, float offset = 0f, float turn = 0f)
        {
            var pace = FilmCast.Pace(clip) * speed;
            var arrive = t + (to - from).magnitude / Mathf.Max(0.05f, pace);
            Play(t, clip, fade, speed, offset);
            Path.Key(t, from, Ease.Linear).Key(arrive, to, Ease.Linear);
            var way = to - from;
            var yaw = Mathf.Atan2(way.x, way.z) * Mathf.Rad2Deg;
            if (turn > 0f && !Heading.Empty)
            {
                var was = Heading.At(t);
                yaw = was + Mathf.DeltaAngle(was, yaw);
                Heading.Key(t - turn * 0.4f, was).Key(t + turn * 0.6f, yaw);
            }
            else Heading.Key(t, yaw, Ease.Hold);
            return arrive;
        }

        void Move(float t)
        {
            if (_motions.Count == 0) { _graph.Evaluate(0f); return; }
            var now = 0;
            for (var i = 0; i < _motions.Count; i++) if (_motions[i].Start <= t) now = i;
            for (var i = 0; i < _motions.Count; i++) _mixer.SetInputWeight(_motions[i].Port, 0f);
            var m = _motions[now];
            var w = now == 0 || m.Fade <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - m.Start) / m.Fade));
            Set(m, t, w);
            if (w < 1f) Set(_motions[now - 1], t, 1f - w);
            _graph.Evaluate(0f);
        }

        void Set(Motion m, float t, float weight)
        {
            var local = m.Offset + Mathf.Max(0f, t - m.Start) * m.Speed;
            var length = Mathf.Max(0.05f, m.Clip.length);
            local = m.Clip.isLooping ? Mathf.Repeat(local, length) : Mathf.Min(local, length - 0.002f);
            m.Playable.SetTime(local);
            m.Playable.SetTime(local);   // (twice: the first only moves the playable's "previous time")
            _mixer.SetInputWeight(m.Port, weight);
        }
    }
}
