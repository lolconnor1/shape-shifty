using System.Collections.Generic;
using UnityEngine;

namespace ShapeShifty.Interaction
{
    // Water deep enough to swim in, like the penguin pool. Forms that can swim (see
    // ShapeDefinition.canSwim) float and swim while in it; everything else sinks to the
    // bottom, and drowns if it stays under too long (see PlayerBreath). Needs a trigger collider filling the water from the bottom up to the surface,
    // on Ignore Raycast so nothing else notices it. A mesh collider has to be convex.
    [RequireComponent(typeof(Collider))]
    public class DeepWater : MonoBehaviour
    {
        static readonly List<DeepWater> active = new List<DeepWater>();

        Collider volume;

        public float SurfaceY => volume.bounds.max.y;

        void Awake() => volume = GetComponent<Collider>();
        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        // The water this point is in, if any.
        public static DeepWater At(Vector3 point)
        {
            foreach (var water in active)
                if ((water.volume.ClosestPoint(point) - point).sqrMagnitude < 0.0001f) return water;
            return null;
        }
    }
}
