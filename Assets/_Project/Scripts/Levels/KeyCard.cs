using System.Collections.Generic;
using ShapeShifty.Interaction;
using UnityEngine;

namespace ShapeShifty.Levels
{
    // A keycard: a Bone the player can carry about in any form (in a rat's mouth, say) and
    // drop or throw. Card readers take it from anyone who brings it close.
    [RequireComponent(typeof(Bone))]
    public class KeyCard : MonoBehaviour
    {
        public static readonly List<KeyCard> All = new List<KeyCard>();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);
    }
}
