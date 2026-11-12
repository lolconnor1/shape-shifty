using ShapeShifty.Dialogue;
using ShapeShifty.Shapes;
using UnityEngine;

namespace ShapeShifty.AI
{
    // A penguin in its enclosure. Either waddles between random spots near a home point,
    // standing about for a while at each one, or (Swims Laps) paddles slow circles round the
    // home point at the water's surface. Only waddles to spots level with home that it can
    // reach in a straight line over solid ground, so it never walks into the pool or up a
    // rock. Now and then a waddler flops onto its belly and slides to the spot instead, so
    // the player sees what penguins can do. Freezes while being scanned, and stops to chat
    // with a penguin-shaped player. A swimmer whose pool is drained (Land) carries on as a
    // waddler on the pool floor.
    public class PenguinWander : MonoBehaviour
    {
        [Tooltip("The spot the penguin stays near. For a swimmer, the middle of its laps, at the water's surface.")]
        [SerializeField] Transform home;
        [SerializeField] bool swimsLaps;
        [Tooltip("For a swimmer: where it waddles about instead once the pool has been drained (see Land).")]
        [SerializeField] Transform shore;
        [Tooltip("The visual model: rocked side to side while waddling, laid flat while swimming.")]
        [SerializeField] Transform body;

        [Header("Waddling")]
        [SerializeField] float roamRadius = 3.5f;
        [SerializeField] float walkSpeed = 1.1f;
        [SerializeField] float turnSpeed = 240f;
        [Tooltip("Seconds stood still between walks (min, max).")]
        [SerializeField] Vector2 pauseTime = new Vector2(1.5f, 5f);
        [Tooltip("Degrees rocked to each side per step.")]
        [SerializeField] float waddleAngle = 10f;
        [SerializeField] float waddleRate = 9f;
        [SerializeField] LayerMask obstacleMask = ~(1 << 2);

        [Header("Belly Sliding")]
        [Tooltip("Chance a walk to a far enough spot is a belly slide instead.")]
        [SerializeField, Range(0f, 1f)] float slideChance = 0.3f;
        [SerializeField] float slideMinDistance = 2.5f;
        [Tooltip("Speed a flop starts at (m/s); snow slows it from there.")]
        [SerializeField] float slideSpeed = 4.2f;
        [SerializeField] float slideFriction = 2.5f;
        [Tooltip("Where the body sits while lying flat, so it slides centred on this object.")]
        [SerializeField] Vector3 slideOffset = new Vector3(0f, 0.02f, -0.4f);

        [Header("Swimming")]
        [SerializeField] float lapRadius = 4.5f;
        [SerializeField] float swimSpeed = 1.8f;
        [Tooltip("Where the body sits while lying flat, so it floats centred on this object.")]
        [SerializeField] Vector3 swimOffset = new Vector3(0f, -0.05f, -0.4f);

        Scannable scannable;
        Talkable talkable;
        Vector3 destination;
        bool walking;
        bool sliding;
        float speed;
        float flat; // 0 stood up, 1 lying on its belly
        float pauseTimer;
        float phase;
        float lapAngle;

        void Awake()
        {
            scannable = GetComponent<Scannable>();
            talkable = GetComponent<Talkable>();
        }

        void Start()
        {
            pauseTimer = Random.Range(pauseTime.x, pauseTime.y);
            Vector3 fromHome = transform.position - home.position;
            lapAngle = Mathf.Atan2(fromHome.z, fromHome.x);
        }

        // The water's gone: a swimmer drops to the shore point and waddles about there instead.
        public void Land()
        {
            if (!swimsLaps) return;
            swimsLaps = false;
            if (shore != null) home = shore;
            transform.position = new Vector3(transform.position.x, home.position.y, transform.position.z);
            body.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            walking = false;
            flat = 0f;
            pauseTimer = 1f;
        }

        // Something else (a PenguinPin, say) has taken over moving it: forget where it was going.
        void OnDisable()
        {
            walking = false;
            sliding = false;
            flat = 0f;
            pauseTimer = 1f;
            if (body != null) body.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        void Update()
        {
            if (scannable != null && scannable.IsBeingScanned) return; // frozen by the scanner
            if (talkable != null && talkable.IsTalking)
            {
                body.localRotation = Quaternion.identity; // stand still; Talkable turns it to face the player
                return;
            }
            if (swimsLaps) Swim();
            else Waddle();
        }

        void Swim()
        {
            lapAngle += swimSpeed / lapRadius * Time.deltaTime;
            var round = new Vector3(Mathf.Cos(lapAngle), 0f, Mathf.Sin(lapAngle));
            transform.SetPositionAndRotation(home.position + round * lapRadius, Quaternion.LookRotation(new Vector3(-round.z, 0f, round.x)));
            phase += Time.deltaTime * 3f;
            body.SetLocalPositionAndRotation(swimOffset + Vector3.up * (Mathf.Sin(phase) * 0.03f), Quaternion.Euler(80f, 0f, 0f));
        }

        void Waddle()
        {
            if (!walking)
            {
                // Gets back up if it was lying down, then stands about.
                flat = Mathf.MoveTowards(flat, 0f, Time.deltaTime / 0.3f);
                body.SetLocalPositionAndRotation(slideOffset * flat, Quaternion.Euler(80f * flat, 0f, 0f));
                pauseTimer -= Time.deltaTime;
                if (pauseTimer > 0f) return;
                walking = TryPickDestination(out destination);
                if (!walking) { pauseTimer = 0.5f; return; } // boxed in for now; try again shortly
                Vector3 trip = destination - transform.position;
                trip.y = 0f;
                sliding = trip.magnitude >= slideMinDistance && Random.value < slideChance;
                speed = slideSpeed;
                return;
            }

            Vector3 toDestination = destination - transform.position;
            toDestination.y = 0f;
            if (toDestination.sqrMagnitude < 0.0025f || (sliding && speed < 0.8f))
            {
                walking = false;
                sliding = false;
                pauseTimer = Random.Range(pauseTime.x, pauseTime.y);
                return;
            }

            // Turns on the spot first, then waddles off, or flops down and slides.
            var facing = Quaternion.LookRotation(toDestination);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, turnSpeed * Time.deltaTime);
            if (sliding)
            {
                if (Quaternion.Angle(transform.rotation, facing) > 10f) return;
                flat = Mathf.MoveTowards(flat, 1f, Time.deltaTime / 0.25f);
                transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);
                speed -= slideFriction * Time.deltaTime;
                body.SetLocalPositionAndRotation(slideOffset * flat, Quaternion.Euler(80f * flat, 0f, 0f));
                return;
            }
            if (Quaternion.Angle(transform.rotation, facing) < 30f)
                transform.position = Vector3.MoveTowards(transform.position, destination, walkSpeed * Time.deltaTime);
            phase += Time.deltaTime * waddleRate;
            body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(phase) * waddleAngle);
        }

        bool TryPickDestination(out Vector3 point)
        {
            Vector3 from = transform.position + Vector3.up * 0.3f;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * roamRadius;
                Vector3 candidate = home.position + new Vector3(offset.x, 0f, offset.y);
                if (!LevelGround(candidate)) continue;

                Vector3 path = candidate - transform.position;
                path.y = 0f;
                float length = path.magnitude;
                if (length < 1f) continue;
                if (Physics.SphereCast(from, 0.2f, path / length, out _, length, obstacleMask, QueryTriggerInteraction.Ignore))
                    continue;
                bool solid = true;
                for (float d = 0.5f; d < length && solid; d += 0.5f)
                    solid = LevelGround(transform.position + path / length * d);
                if (!solid) continue;

                point = new Vector3(candidate.x, home.position.y, candidate.z);
                return true;
            }
            point = transform.position;
            return false;
        }

        // Ground at this spot, at the same height as home: not water, a hole or a rock.
        bool LevelGround(Vector3 spot) =>
            Physics.Raycast(new Vector3(spot.x, home.position.y + 3f, spot.z), Vector3.down, out RaycastHit ground, 6f, obstacleMask, QueryTriggerInteraction.Ignore)
            && Mathf.Abs(ground.point.y - home.position.y) < 0.3f;
    }
}
