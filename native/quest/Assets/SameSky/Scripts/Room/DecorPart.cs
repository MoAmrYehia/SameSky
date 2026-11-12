// A renderer that takes a finish (see RoomDecor.cs): the material for each finish of the piece it belongs to.
// The project setup fills the table in; nothing is looked up by name at run time, and a build always contains the
// materials a piece can wear.
//
// In a file of its own on purpose: the scene stores these components, and Unity only keeps a dependable reference
// to a component's script when the class has a file named after it.

using System;
using UnityEngine;

namespace SameSky.App
{
    public sealed class DecorPart : MonoBehaviour
    {
        public string Slot, Item;
        public string[] Finishes = Array.Empty<string>();
        public Material[] Materials = Array.Empty<Material>();

        public void Dress(string finish)
        {
            var i = Array.IndexOf(Finishes, finish);
            if (i < 0 || i >= Materials.Length || Materials[i] == null) return;
            if (TryGetComponent<Renderer>(out var r) && r.sharedMaterial != Materials[i]) r.sharedMaterial = Materials[i];
        }
    }
}
