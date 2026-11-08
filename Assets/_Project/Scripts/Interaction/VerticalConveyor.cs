using System.Collections.Generic;
using UnityEngine;

namespace ShapeShifty.Interaction
{
    // A vertical belt conveyor: a belt with little boards across it (cleats) runs along a short
    // flat infeed, bends up, climbs straight up, then goes over the top and back down behind.
    // Whoever is between two cleats moves with the belt, so a rat dropped onto the infeed is
    // scooped up and rides the climb on the cleat below it. Anyone wider than the belt doesn't
    // ride. Riders are carried over the top and thrown off forward, past the belt coming back
    // down, onto whatever is beyond it. Hook SwitchOn and SwitchOff up to a lever to start and
    // stop it.
    //
    // The belt's shape is set by the numbers below, in this object's space: the infeed runs
    // along +z at belt height into a bend, and the climb rises at z = 0 with the cleats facing
    // -z. Over the top it turns towards +z, and the return side runs behind and underneath.
    [DefaultExecutionOrder(-50)]
    public class VerticalConveyor : MonoBehaviour
    {
        [Header("Shape")]
        [Tooltip("Height of the belt's surface along the infeed.")]
        [SerializeField] float beltHeight = 0.75f;
        [SerializeField] float infeedLength = 1.8f;
        [Tooltip("Radius of the bend from the infeed up into the climb.")]
        [SerializeField] float bendRadius = 0.8f;
        [Tooltip("Where the climb ends and the belt turns over the top.")]
        [SerializeField] float climbHeight = 3.25f;
        [Tooltip("Gap between the carrying side of the belt and the return side behind it.")]
        [SerializeField] float thickness = 0.24f;
        [Tooltip("Width of the channel the cleats run in. Anyone wider doesn't ride.")]
        [SerializeField] float width = 0.52f;
        [SerializeField] float cleatHeight = 0.36f;

        [Header("Running")]
        [Tooltip("Parent of the cleats, which are spaced evenly round the belt. Each one's origin is where it meets the belt.")]
        [SerializeField] Transform cleats;
        [SerializeField] float speed = 0.6f;
        [SerializeField] bool running = true;
        [Tooltip("How fast riders are thrown off forward (along this object's blue arrow) at the top.")]
        [SerializeField] float dumpSpeed = 3.5f;
        [Tooltip("How long (s) the throw lasts. Long enough to carry them clear of the belt coming back down.")]
        [SerializeField] float dumpTime = 0.4f;

        public bool IsRunning => running;

        // Round the loop: points per quarter turn of a bend.
        const int ArcSteps = 12;

        CharacterController rider;
        List<Vector3> path; // in this object's space, in the way the belt runs; the last joins back to the first
        Vector3[] tangents;
        float[] distances;  // along the loop to each point; the last entry is the full length
        float carryLength;  // the carrying side: from the start of the infeed, up the climb to the top of the head
        float climbTop;     // where the climb ends and the belt starts round the head
        Collider[] boards;  // each cleat's
        float travelled;
        bool riding;
        float dumpTimer;

        float Length => distances[path.Count];

        void Awake() => BuildPath();

        void Start()
        {
            var playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null) rider = playerObject.GetComponent<CharacterController>();
            if (cleats != null)
            {
                boards = new Collider[cleats.childCount];
                for (int i = 0; i < boards.Length; i++) boards[i] = cleats.GetChild(i).GetComponentInChildren<Collider>();
            }
            PlaceCleats();
        }

        public void SwitchOn() => running = true;
        public void SwitchOff() => running = false;

        void Update()
        {
            if (!running) return;
            float step = speed * Time.deltaTime;
            // The rider first, so the cleat below follows them up rather than pushing into them.
            CarryRider(step);
            travelled = Mathf.Repeat(travelled + step, Length);
            PlaceCleats();
            Physics.SyncTransforms();
        }

        // Moves the rider along with the patch of belt they're over, as if fixed to it: along the
        // infeed, round the bend, up the climb and over the top. Gravity keeps them sat on the
        // cleat below. Once over the top, they're thrown off forward.
        void CarryRider(float step)
        {
            if (rider == null || !rider.enabled || rider.radius * 2f >= width)
            {
                riding = false;
                dumpTimer = 0f;
                return;
            }
            if (dumpTimer > 0f)
            {
                dumpTimer -= Time.deltaTime;
                rider.Move(transform.forward * (dumpSpeed * Time.deltaTime));
                return;
            }
            Vector3 centre = transform.InverseTransformPoint(rider.transform.TransformPoint(rider.center));
            float s = ClosestOnCarryingSide(centre);
            Frame(s, out var point, out var tangent, out var normal);
            Vector3 offset = centre - point;
            float along = Vector3.Dot(offset, tangent), outward = Vector3.Dot(offset, normal), side = offset.x;
            // Someone right on top of the head (s at the very end) still counts, so they get thrown off.
            riding = s > 0f && Mathf.Abs(along) < 0.1f && outward > -0.05f
                && outward < cleatHeight + 0.12f && Mathf.Abs(side) < width / 2f;
            if (!riding) return;

            Frame(Mathf.Min(s + step, carryLength), out var nextPoint, out var nextTangent, out var nextNormal);
            Vector3 moved = nextPoint + nextTangent * along + nextNormal * outward + Vector3.right * side;
            rider.Move(transform.TransformVector(moved - centre));
            if (s + step >= carryLength)
            {
                riding = false;
                dumpTimer = dumpTime;
            }
        }

        // Cleats are only solid on the way up. Over the top and on the way back down they don't
        // catch anyone, so the one behind a rider being thrown off doesn't snag them, and nor do
        // the ones coming down the back.
        void PlaceCleats()
        {
            if (cleats == null || cleats.childCount == 0) return;
            for (int i = 0; i < cleats.childCount; i++)
            {
                float distance = Mathf.Repeat(travelled + Length * i / cleats.childCount, Length);
                Frame(distance, out var point, out var tangent, out var normal);
                cleats.GetChild(i).SetPositionAndRotation(transform.TransformPoint(point), transform.rotation * Quaternion.LookRotation(tangent, normal));
                if (boards != null && boards[i] != null) boards[i].enabled = distance < climbTop;
            }
        }

        // ---------- The belt's path ----------

        // The loop in world space, for building the belt and its frame round it.
        public Vector3[] WorldPath()
        {
            BuildPath();
            var points = new Vector3[path.Count];
            for (int i = 0; i < path.Count; i++) points[i] = transform.TransformPoint(path[i]);
            return points;
        }

        public float CarryLength
        {
            get { BuildPath(); return carryLength; }
        }

        void BuildPath()
        {
            path = new List<Vector3>();
            float r = bendRadius, t = thickness, h = thickness / 2f;
            // Points are (z, y) here. The carrying side: infeed, bend up, climb.
            Add(-r - infeedLength, beltHeight);
            Add(-r, beltHeight);
            Arc(-r, beltHeight + r, r, 0f, 90f);
            Add(0f, climbHeight);
            int climbEnd = path.Count - 1;
            Arc(h, climbHeight, h, -90f, -180f);
            int carryEnd = path.Count - 1;
            // Over the top and down the back, round under the bend, back under the infeed and up
            // round the tail to the start.
            Arc(h, climbHeight, h, -180f, -270f);
            Add(t, beltHeight + r);
            Arc(-r, beltHeight + r, r + t, 90f, 0f);
            Add(-r - infeedLength, beltHeight - t);
            Arc(-r - infeedLength, beltHeight - h, h, 0f, -180f);
            path.RemoveAt(path.Count - 1); // back at the start

            int n = path.Count;
            distances = new float[n + 1];
            for (int i = 0; i < n; i++) distances[i + 1] = distances[i] + Vector3.Distance(path[i], path[(i + 1) % n]);
            carryLength = distances[carryEnd];
            climbTop = distances[climbEnd];
            // Smooth the turn at each point, so the cleats tilt evenly round the bends.
            tangents = new Vector3[n];
            for (int i = 0; i < n; i++)
                tangents[i] = ((path[i] - path[(i + n - 1) % n]).normalized + (path[(i + 1) % n] - path[i]).normalized).normalized;
        }

        void Add(float z, float y) => path.Add(new Vector3(0f, y, z));

        // Round a circle from one angle to another, not including the first point. At angle 0 the
        // point is straight below the centre, at 90 it's towards +z.
        void Arc(float centreZ, float centreY, float radius, float from, float to)
        {
            int steps = Mathf.CeilToInt(Mathf.Abs(to - from) / 90f * ArcSteps);
            for (int i = 1; i <= steps; i++)
            {
                float a = Mathf.Lerp(from, to, (float)i / steps) * Mathf.Deg2Rad;
                Add(centreZ + radius * Mathf.Sin(a), centreY - radius * Mathf.Cos(a));
            }
        }

        // Where on the belt `distance` along it is, which way it's running there, and which way
        // its cleats point (out from the loop), all in this object's space.
        void Frame(float distance, out Vector3 point, out Vector3 tangent, out Vector3 normal)
        {
            distance = Mathf.Repeat(distance, Length);
            int n = path.Count, i = 0;
            while (i < n - 1 && distance > distances[i + 1]) i++;
            float segment = distances[i + 1] - distances[i];
            float f = segment > 0f ? (distance - distances[i]) / segment : 0f;
            point = Vector3.Lerp(path[i], path[(i + 1) % n], f);
            tangent = Vector3.Lerp(tangents[i], tangents[(i + 1) % n], f).normalized;
            normal = new Vector3(0f, tangent.z, -tangent.y);
        }

        float ClosestOnCarryingSide(Vector3 local)
        {
            local.x = 0f;
            float best = 0f, bestDistance = float.MaxValue;
            for (int i = 0; i < path.Count - 1 && distances[i] < carryLength; i++)
            {
                Vector3 a = path[i], b = path[i + 1];
                float f = Mathf.Clamp01(Vector3.Dot(local - a, b - a) / (b - a).sqrMagnitude);
                float d = (Vector3.Lerp(a, b, f) - local).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = Mathf.Lerp(distances[i], distances[i + 1], f);
                }
            }
            return best;
        }

        void OnDrawGizmosSelected()
        {
            var points = WorldPath();
            Gizmos.color = Color.cyan;
            for (int i = 0; i < points.Length; i++) Gizmos.DrawLine(points[i], points[(i + 1) % points.Length]);
        }
    }
}
