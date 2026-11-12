using System.Collections.Generic;
using UnityEngine;

namespace ShapeShifty.Shapes
{
    // An area where one kind of animal belongs, e.g. the penguin enclosure. While the player
    // is inside it in that form, employees ignore them completely, whatever the form's usual
    // suspicion. Needs a trigger collider covering the area, on Ignore Raycast so nothing
    // else notices it. A mesh collider has to be convex.
    [RequireComponent(typeof(Collider))]
    public class BlendInZone : MonoBehaviour
    {
        [SerializeField] ShapeDefinition shape;

        static readonly List<BlendInZone> active = new List<BlendInZone>();

        Collider area;

        void Awake() => area = GetComponent<Collider>();
        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        // True when something in this form, standing at this point, blends in.
        public static bool Hides(ShapeDefinition form, Vector3 point)
        {
            foreach (var zone in active)
                if (zone.shape == form && (zone.area.ClosestPoint(point) - point).sqrMagnitude < 0.0001f) return true;
            return false;
        }
    }
}
