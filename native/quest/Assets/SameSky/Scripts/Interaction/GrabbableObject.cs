// An object the person can pick up with their hands (Interaction SDK):
//   HandGrabInteractable          pinch or palm grab within reach
//   DistanceHandGrabInteractable  point and pinch from the seat to pull it in
//   GrabInteractable              controllers' grip (hands stay the judged path)
// all driving one Grabbable, which moves the object. Grabbed/Released fire once per hold, however many
// hands or pointers are involved, and a hand that loses tracking releases cleanly (the SDK cancels).

using System;
using Oculus.Interaction;
using Oculus.Interaction.Grab;
using Oculus.Interaction.GrabAPI;
using Oculus.Interaction.HandGrab;
using UnityEngine;

namespace SameSky.Interaction
{
    public sealed class GrabbableObject : MonoBehaviour
    {
        public string Id;
        public event Action Grabbed;
        public event Action Released;
        public static event Action<GrabbableObject, string> AnyEvent; // "grab" | "release", for the test probe

        Grabbable _grabbable;
        bool _held;
        readonly System.Collections.Generic.HashSet<int> _hovering = new();   // pointers (hands, rays) near enough to take it

        public bool IsHeld => _held;
        /// <summary>A hand or pointer is close enough to take it (for a restrained "you can pick this up" highlight).</summary>
        public bool IsHovered => _hovering.Count > 0;

        /// <summary>Makes <paramref name="target"/> grabbable. It must have a collider sized to the object.</summary>
        public static GrabbableObject Make(GameObject target, string id)
        {
            var wasActive = target.activeSelf;
            target.SetActive(false); // inject before the SDK components wake up

            // not GetComponent() ?? AddComponent(): in the Editor a missing component comes back as Unity's "fake null",
            // which C#'s ?? treats as a real object
            var rb = target.TryGetComponent<Rigidbody>(out var existing) ? existing : target.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var grabbable = target.AddComponent<Grabbable>();
            grabbable.InjectOptionalKinematicWhileSelected(true);
            grabbable.InjectOptionalThrowWhenUnselected(false);

            var hand = new GameObject("HandGrab");
            hand.transform.SetParent(target.transform, false);
            var handGrab = hand.AddComponent<HandGrabInteractable>();
            handGrab.InjectAllHandGrabInteractable(GrabTypeFlags.All, rb, GrabbingRule.DefaultPinchRule, GrabbingRule.DefaultPalmRule);
            handGrab.InjectOptionalPointableElement(grabbable);

            var distance = new GameObject("DistanceHandGrab");
            distance.transform.SetParent(target.transform, false);
            var distanceGrab = distance.AddComponent<DistanceHandGrabInteractable>();
            distanceGrab.InjectAllDistanceHandGrabInteractable(GrabTypeFlags.Pinch, rb, GrabbingRule.DefaultPinchRule, GrabbingRule.DefaultPalmRule);
            distanceGrab.InjectOptionalPointableElement(grabbable);

            var controller = new GameObject("ControllerGrab");
            controller.transform.SetParent(target.transform, false);
            var grab = controller.AddComponent<GrabInteractable>();
            grab.InjectAllGrabInteractable(rb);
            grab.InjectOptionalPointableElement(grabbable);

            var g = target.AddComponent<GrabbableObject>();
            g.Id = id;
            g._grabbable = grabbable;
            target.SetActive(wasActive);
            return g;
        }

        void OnEnable()
        {
            if (_grabbable) _grabbable.WhenPointerEventRaised += OnPointer;
        }

        void OnDisable()
        {
            if (_grabbable) _grabbable.WhenPointerEventRaised -= OnPointer;
            _hovering.Clear();
            if (_held) { _held = false; Released?.Invoke(); }
        }

        void Start()
        {
            // OnEnable ran before Make() finished wiring the first time
            _grabbable.WhenPointerEventRaised -= OnPointer;
            _grabbable.WhenPointerEventRaised += OnPointer;
        }

        void OnPointer(PointerEvent e)
        {
            if (e.Type == PointerEventType.Hover) _hovering.Add(e.Identifier);
            else if (e.Type is PointerEventType.Unhover or PointerEventType.Cancel) _hovering.Remove(e.Identifier);

            if (e.Type == PointerEventType.Select && !_held)
            {
                _held = true;
                AnyEvent?.Invoke(this, "grab");
                Grabbed?.Invoke();
            }
            else if ((e.Type == PointerEventType.Unselect || e.Type == PointerEventType.Cancel) && _held && _grabbable.SelectingPointsCount == 0)
            {
                _held = false;
                AnyEvent?.Invoke(this, "release");
                Released?.Invoke();
            }
        }

        /// <summary>Lets go on the person's behalf (a "Place cup" button): disabling the object makes the Interaction
        /// SDK unselect it from whatever hand holds it, which raises Released as a normal release would.</summary>
        public void ForceRelease()
        {
            if (!_held) return;
            gameObject.SetActive(false);
            gameObject.SetActive(true);
        }

        /// <summary>Gently returns the object to a resting pose (e.g. the cup back on its saucer).</summary>
        public void SetRest(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
