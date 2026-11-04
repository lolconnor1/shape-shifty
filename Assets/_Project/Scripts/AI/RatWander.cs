using ShapeShifty.Shapes;
using UnityEngine;

namespace ShapeShifty.AI
{
    // Scurries between random spots near a home point (the dumpster), stopping for a
    // moment at each one. Only picks spots on open ground that it can reach in a straight
    // line without running into anything. Freezes on the spot while being scanned.
    public class RatWander : MonoBehaviour
    {
        [Tooltip("The spot the rat stays near.")]
        [SerializeField] Transform home;
        [SerializeField] float roamRadius = 4f;
        [SerializeField] float runSpeed = 3.5f;
        [SerializeField] float turnSpeed = 900f;
        [Tooltip("Seconds stopped between runs (min, max).")]
        [SerializeField] Vector2 pauseTime = new Vector2(0.5f, 2.5f);
        [SerializeField] LayerMask obstacleMask = ~(1 << 2);
        [Tooltip("The visual model, bobbed up and down while running.")]
        [SerializeField] Transform body;

        Scannable scannable;
        Vector3 destination;
        bool running;
        float pauseTimer;
        float bobPhase;

        void Awake() => scannable = GetComponent<Scannable>();

        void Start() => pauseTimer = Random.Range(pauseTime.x, pauseTime.y);

        void Update()
        {
            if (scannable != null && scannable.IsBeingScanned) return; // frozen by the scanner

            if (!running)
            {
                pauseTimer -= Time.deltaTime;
                if (pauseTimer > 0f) return;
                running = TryPickDestination(out destination);
                if (!running) pauseTimer = 0.2f; // boxed in for now; try again shortly
                return;
            }

            Vector3 toDestination = destination - transform.position;
            toDestination.y = 0f;
            if (toDestination.sqrMagnitude < 0.0025f)
            {
                running = false;
                pauseTimer = Random.Range(pauseTime.x, pauseTime.y);
                body.localPosition = Vector3.zero;
                return;
            }

            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toDestination), turnSpeed * Time.deltaTime);
            transform.position = Vector3.MoveTowards(transform.position, destination, runSpeed * Time.deltaTime);
            bobPhase += Time.deltaTime * 30f;
            body.localPosition = Vector3.up * (Mathf.Abs(Mathf.Sin(bobPhase)) * 0.03f);
        }

        bool TryPickDestination(out Vector3 point)
        {
            Vector3 from = transform.position + Vector3.up * 0.2f;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * roamRadius;
                Vector3 candidate = home.position + new Vector3(offset.x, 0f, offset.y);

                // Has to be open ground, not the top of the dumpster or anything else.
                if (!Physics.Raycast(candidate + Vector3.up * 3f, Vector3.down, out RaycastHit ground, 6f, obstacleMask, QueryTriggerInteraction.Ignore)
                    || ground.point.y > home.position.y + 0.3f)
                    continue;

                Vector3 path = ground.point + Vector3.up * 0.2f - from;
                if (path.magnitude < 1f) continue;
                if (Physics.SphereCast(from, 0.12f, path.normalized, out _, path.magnitude, obstacleMask, QueryTriggerInteraction.Ignore))
                    continue;

                point = ground.point;
                return true;
            }
            point = transform.position;
            return false;
        }
    }
}
