// A seated, animated character (the sample's stock adults). The Animator plays a seated breathing loop; this adds what
// makes a face read as alive rather than posed:
//   - the head turns toward the person through the humanoid look-at (an IK pass, so it sits on top of the loop);
//   - the eyes move on their own bones: they hold the person's face with small shifts, and now and then glance away
//     (down to the table, out of the window) the way people do, leading the head;
//   - blinks at an uneven, human rate, and usually on a change of gaze;
//   - a faint, slowly varying warmth around the mouth and eyes, a little more on arriving.
// All of it is app animation, labelled as such with the rest of the sample's motion: nothing here is tracked from
// a real face.

using UnityEngine;

namespace SameSky.App
{
    [RequireComponent(typeof(Animator))]
    public sealed class SeatedFigure : MonoBehaviour
    {
        /// <summary>Where to look (world space): the person's head.</summary>
        public Vector3 LookAt;
        /// <summary>How much to look there, 0..1 (eased toward over about a second).</summary>
        public float LookWeight;

        // across the table: the body barely turns, the head most of the way; the clamp keeps it a glance, not a
        // stare over the shoulder
        float _body = 0.05f, _head = 0.55f, _clamp = 0.6f;
        Animator _animator;
        float _weight;
        bool _portrait;

        // the face
        SkinnedMeshRenderer _face;
        int _blinkL = -1, _blinkR = -1, _smileL = -1, _smileR = -1, _cheekL = -1, _cheekR = -1, _squintL = -1, _squintR = -1, _browUp = -1;
        Transform _headBone, _eyeL, _eyeR;
        Quaternion _eyeLRest, _eyeRRest;      // each eye's rotation at rest, in head space
        Vector3 _faceForward;                 // which way the face points, in head space
        Vector3 _eyeDirection;                // where the eyes point now (world), eased
        Vector3 _headTarget;                  // where the head is turning to (world), eased
        float _blink = -1f, _nextBlink, _nextGlance, _glanceUntil, _nextShift, _arrivedAt = -100f, _smile;
        Vector3 _glancePoint, _shift;
        const float MaxEyeTurn = 24f;

        void Awake()
        {
            _animator = GetComponent<Animator>();
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.sharedMesh == null || r.sharedMesh.blendShapeCount == 0) continue;
                _face = r;
                break;
            }
            if (_face != null)
            {
                _blinkL = Shape("EyeBlinkLeft");
                _blinkR = Shape("EyeBlinkRight");
                _smileL = Shape("MouthSmileLeft");
                _smileR = Shape("MouthSmileRight");
                _cheekL = Shape("CheekSquintLeft");
                _cheekR = Shape("CheekSquintRight");
                _squintL = Shape("EyeSquintLeft");
                _squintR = Shape("EyeSquintRight");
                _browUp = Shape("BrowInnerUp");
            }
            _headBone = _animator.isHuman ? _animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (_headBone != null)
            {
                foreach (var t in _headBone.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.EndsWith(" LEye")) _eyeL = t;
                    else if (t.name.EndsWith(" REye")) _eyeR = t;
                }
            }
            if (_eyeL != null && _eyeR != null)
            {
                // the face points across the line between the eyes; the figure's own forward settles the sign
                // (the prefab root above this model faces the table; the model's own root may carry an import rotation)
                var across = (_eyeR.position - _eyeL.position).normalized;
                var forward = Vector3.Cross(across, Vector3.up).normalized;
                var facing = transform.parent != null ? transform.parent.forward : transform.forward;
                if (Vector3.Dot(forward, facing) < 0f) forward = -forward;
                var toHead = Quaternion.Inverse(_headBone.rotation);
                _faceForward = toHead * forward;
                _eyeLRest = toHead * _eyeL.rotation;
                _eyeRRest = toHead * _eyeR.rotation;
                _eyeDirection = forward;
            }
            else { _eyeL = null; _eyeR = null; }
            _nextBlink = Time.time + Random.Range(1.2f, 3.5f);
            _nextGlance = Time.time + Random.Range(5f, 9f);
        }

        void OnEnable()
        {
            // sitting down: eyes come up from the table to the person, with a moment of extra warmth
            _arrivedAt = Time.time;
            _weight = 0f;
            _headTarget = Vector3.zero;
        }

        int Shape(string name)
        {
            var mesh = _face.sharedMesh;
            for (var i = 0; i < mesh.blendShapeCount; i++)
                if (mesh.GetBlendShapeName(i).EndsWith("_" + name)) return i;
            return -1;
        }

        void Set(int shape, float weight)
        {
            if (shape >= 0) _face.SetBlendShapeWeight(shape, Mathf.Clamp(weight, 0f, 100f));
        }

        /// <summary>For a portrait: look straight at the target at once, head up from whatever the seated loop has it
        /// doing (leaning over the table, say), eyes open, a hint of a smile. Call PoseFaceNow after the Animator has
        /// been updated.</summary>
        public void HoldPortraitGaze(Vector3 target)
        {
            LookAt = target;
            LookWeight = 1f;
            _weight = 1f;
            _body = 0.4f;
            _head = 1f;
            _clamp = 0f;
            _portrait = true;
            _headTarget = target;
        }

        /// <summary>Applies the face (eyes, lids, expression) right now, outside the frame loop: for a portrait drawn
        /// in the same call.</summary>
        public void PoseFaceNow() => UpdateFace(0f);

        void OnAnimatorIK(int layer)
        {
            if (_animator == null) return;
            var dt = Time.deltaTime;
            _weight = _portrait ? 1f : Mathf.MoveTowards(_weight, Mathf.Clamp01(LookWeight), dt * 0.8f);
            // the head follows a glance only part of the way, and never holds perfectly still
            var want = LookAt;
            if (!_portrait)
            {
                var t = Time.time;
                if (t < _glanceUntil) want = Vector3.Lerp(LookAt, _glancePoint, 0.4f);
                want += new Vector3((Mathf.PerlinNoise(t * 0.13f, 3.1f) - 0.5f) * 0.05f, (Mathf.PerlinNoise(7.7f, t * 0.11f) - 0.5f) * 0.035f, 0f);
            }
            _headTarget = _headTarget == Vector3.zero || _portrait ? want : Vector3.Lerp(_headTarget, want, 1f - Mathf.Exp(-dt * 2.6f));
            _animator.SetLookAtWeight(_weight, _body, _head, 0f, _clamp);
            _animator.SetLookAtPosition(_headTarget);
        }

        void LateUpdate()
        {
            if (!_portrait) UpdateFace(Time.deltaTime);
        }

        void UpdateFace(float dt)
        {
            var now = Time.time;
            var lookingAtPerson = _portrait || now >= _glanceUntil;

            // when to glance away, and where: mostly down toward the table between you, sometimes out of the window
            if (!_portrait && now >= _nextGlance && _weight > 0.3f)
            {
                _glanceUntil = now + Random.Range(0.7f, 1.7f);
                _nextGlance = _glanceUntil + Random.Range(4.5f, 10f);
                var eyes = _eyeL != null ? (_eyeL.position + _eyeR.position) * 0.5f : transform.position + Vector3.up * 1.2f;
                var toPerson = LookAt - eyes;
                var side = Vector3.Cross(Vector3.up, toPerson).normalized;
                _glancePoint = Random.value < 0.6f
                    ? eyes + toPerson * 0.45f + Vector3.down * Random.Range(0.3f, 0.45f) + side * Random.Range(-0.15f, 0.15f)
                    : eyes + toPerson * 0.8f + side * (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.5f, 0.9f) + Vector3.up * Random.Range(-0.05f, 0.12f);
                if (_blink < 0f && Random.value < 0.7f) _blink = 0f;   // people blink as their eyes move
                lookingAtPerson = false;
            }
            // small shifts while holding someone's face: one eye, the other, the mouth
            if (now >= _nextShift)
            {
                _nextShift = now + Random.Range(0.7f, 2.4f);
                var pick = Random.Range(0, 3);
                _shift = pick == 0 ? new Vector3(-0.03f, 0f, 0f) : pick == 1 ? new Vector3(0.03f, 0f, 0f) : new Vector3(0f, -0.06f, 0f);
            }

            // eyes: on their own bones, leading the head, never past what eyes can do
            var lowered = 0f;   // how far the eyes look below where the face points
            if (_eyeL != null && _headBone != null)
            {
                var neutral = _headBone.rotation * _faceForward;
                var eyes = (_eyeL.position + _eyeR.position) * 0.5f;
                var point = _portrait ? LookAt : lookingAtPerson ? LookAt + _shift : _glancePoint;
                var to = point - eyes;
                var want = to.sqrMagnitude > 1e-6f ? to.normalized : neutral;
                want = Vector3.Slerp(neutral, want, _portrait ? 1f : Mathf.Clamp01(_weight * 1.6f));
                var angle = Vector3.Angle(neutral, want);
                if (angle > MaxEyeTurn) want = Vector3.Slerp(neutral, want, MaxEyeTurn / angle);
                _eyeDirection = dt <= 0f ? want : Vector3.Slerp(_eyeDirection, want, 1f - Mathf.Exp(-dt * 16f)).normalized;
                var turn = Quaternion.FromToRotation(neutral, _eyeDirection);
                _eyeL.rotation = turn * (_headBone.rotation * _eyeLRest);
                _eyeR.rotation = turn * (_headBone.rotation * _eyeRRest);
                lowered = Mathf.Clamp01((neutral.y - _eyeDirection.y) * 2.5f);
            }

            if (_face == null) return;

            // blinks: quick to close, slower to open; uneven gaps, now and then two together
            var lids = 0f;
            if (!_portrait)
            {
                if (_blink < 0f && now >= _nextBlink) _blink = 0f;
                if (_blink >= 0f)
                {
                    _blink += dt;
                    var u = _blink / 0.17f;
                    lids = u < 0.35f ? u / 0.35f : 1f - (u - 0.35f) / 0.65f;
                    if (u >= 1f)
                    {
                        _blink = -1f;
                        lids = 0f;
                        _nextBlink = now + (Random.value < 0.15f ? Random.Range(0.18f, 0.3f) : Random.Range(2.2f, 5.8f));
                    }
                }
            }
            // looking down lowers the lids a little
            var lid = Mathf.Max(lids * 100f, lowered * 24f);
            Set(_blinkL, lid);
            Set(_blinkR, lid);

            // expression: a faint warmth that breathes, a little more on arriving and when the eyes come back to you
            var arrival = Mathf.Clamp01(1f - (now - _arrivedAt - 0.6f) / 2.4f) * Mathf.Clamp01((now - _arrivedAt) / 0.6f);
            var target = _portrait ? 16f : (lookingAtPerson ? 15f : 7f) + Mathf.Sin(now * 0.31f) * 4f + arrival * 22f;
            _smile = dt <= 0f ? target : Mathf.MoveTowards(_smile, target, dt * 28f);
            Set(_smileL, _smile);
            Set(_smileR, _smile * 0.92f);
            Set(_cheekL, _smile * 0.4f);
            Set(_cheekR, _smile * 0.4f);
            Set(_squintL, _smile * 0.25f);
            Set(_squintR, _smile * 0.25f);
            Set(_browUp, 4f + arrival * 10f + Mathf.Sin(now * 0.23f + 1.3f) * 2f);
        }
    }
}
