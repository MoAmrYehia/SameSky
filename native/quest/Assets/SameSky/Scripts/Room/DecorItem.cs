// The root of one catalogue piece in the room (see RoomDecor.cs): which slot it fills and which piece it is.
// The project setup puts one on every piece it builds; RoomDecor switches them on and off.
//
// In a file of its own on purpose: the scene stores these components, and Unity only keeps a dependable reference
// to a component's script when the class has a file named after it.

using UnityEngine;

namespace SameSky.App
{
    public sealed class DecorItem : MonoBehaviour
    {
        public string Slot, Item;
    }
}
