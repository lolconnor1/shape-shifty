using ShapeShifty.Dialogue;
using ShapeShifty.Interaction;
using ShapeShifty.Shapes;
using UnityEngine;

namespace ShapeShifty.AI
{
    // A penguin that shows off the belly slide, over and over: waddles round to a ramp, up onto
    // a rock, flops down the ice slide and ploughs into whatever's at the bottom (see
    // PenguinPin), then trudges back round to do it again. Exists so the player can watch how
    // sliding works before they need it. Freezes while being scanned or talked to.
    public class PenguinBowler : MonoBehaviour
    {
        [Tooltip("Waddled through in order, resting at the first; it flops at the last one.")]
        [SerializeField] Transform[] route;
        [Tooltip("The flop heads from the last route point towards this.")]
        [SerializeField] Transform slideAim;
        [Tooltip("The visual model: rocked while waddling, laid flat while sliding.")]
        [SerializeField] Transform body;

        [Header("Waddling")]
        [SerializeField] float walkSpeed = 1.3f;
        [SerializeField] float turnSpeed = 240f;
        [SerializeField] float waddleAngle = 10f;
        [SerializeField] float waddleRate = 9f;
        [Tooltip("Seconds stood at the start of the route between runs (min, max).")]
        [SerializeField] Vector2 restTime = new Vector2(4f, 9f);
        [Tooltip("Seconds looking down the slide before the flop.")]
        [SerializeField] float hesitation = 0.8f;

        [Header("Sliding")]
        [Tooltip("Speed the flop starts at (m/s).")]
        [SerializeField] float flopSpeed = 4.2f;
        [SerializeField] float gravity = 20f;
        [SerializeField] float iceFriction = 0.3f;
        [Tooltip("Braking on snow. More than the player's, so it fetches up just past the pins.")]
        [SerializeField] float snowFriction = 7f;
        [SerializeField] float stopSpeed = 0.8f;
        [Tooltip("Fraction of its speed kept after bowling a pin over.")]
        [SerializeField] float pinDrag = 0.85f;
        [SerializeField] float hitRadius = 0.65f;
        [Tooltip("Where the body sits while lying flat, so it slides centred on this object.")]
        [SerializeField] Vector3 slideOffset = new Vector3(0f, 0.02f, -0.4f);
        [Tooltip("Seconds lying there once it stops.")]
        [SerializeField] float lieTime = 1.2f;
        [SerializeField] LayerMask groundMask = ~(1 << 2);

        enum State { Resting, Walking, Hesitating, Sliding, Lying }

        public bool IsSliding => state == State.Sliding;
        public float Speed => speed;

        State state;
        int next;
        float timer;
        float phase;
        float flat; // 0 stood up, 1 lying on its belly
        float speed;
        Vector3 slideDirection;
        bool onIce;
        Scannable scannable;
        Talkable talkable;

        void Awake()
        {
            scannable = GetComponent<Scannable>();
            talkable = GetComponent<Talkable>();
        }

        void Start()
        {
            state = State.Resting;
            timer = Random.Range(restTime.x, restTime.y);
        }

        void Update()
        {
            if (scannable != null && scannable.IsBeingScanned) return; // frozen by the scanner
            if (talkable != null && talkable.IsTalking) return;      // Talkable turns it to face the player

            switch (state)
            {
                case State.Resting:
                    timer -= Time.deltaTime;
                    if (timer <= 0f) { state = State.Walking; next = 1; }
                    break;
                case State.Walking:
                    if (WalkTowards(route[next].position))
                    {
                        if (next == route.Length - 1) { state = State.Hesitating; timer = hesitation; }
                        else if (next == 0) { state = State.Resting; timer = Random.Range(restTime.x, restTime.y); }
                        else next++;
                    }
                    break;
                case State.Hesitating:
                    slideDirection = slideAim.position - transform.position;
                    slideDirection.y = 0f;
                    slideDirection.Normalize();
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(slideDirection), turnSpeed * Time.deltaTime);
                    timer -= Time.deltaTime;
                    if (timer <= 0f) { state = State.Sliding; speed = flopSpeed; }
                    break;
                case State.Sliding:
                    Slide();
                    break;
                case State.Lying:
                    timer -= Time.deltaTime;
                    if (timer <= 0f) { state = State.Walking; next = 0; }
                    break;
            }
            Pose();
        }

        // Turns on the spot, then waddles. True once it's there.
        bool WalkTowards(Vector3 target)
        {
            Vector3 toTarget = target - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.01f) return true;
            var facing = Quaternion.LookRotation(toTarget);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, turnSpeed * Time.deltaTime);
            if (Quaternion.Angle(transform.rotation, facing) < 30f)
            {
                Vector3 step = Vector3.ClampMagnitude(toTarget, walkSpeed * Time.deltaTime);
                transform.position = SnapToGround(transform.position + step, out _);
                phase += Time.deltaTime * waddleRate;
            }
            return false;
        }

        void Slide()
        {
            transform.position = SnapToGround(transform.position + slideDirection * (speed * Time.deltaTime), out Vector3 normal);
            // Downhill speeds it up; ice hardly slows it, snow does.
            Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, normal) * gravity;
            speed += Vector3.Dot(new Vector3(downhill.x, 0f, downhill.z), slideDirection) * Time.deltaTime;
            speed -= (onIce ? iceFriction : snowFriction) * Time.deltaTime;
            transform.rotation = Quaternion.LookRotation(slideDirection);

            foreach (var pin in PenguinPin.Active)
            {
                if (pin.IsDown) continue;
                Vector3 toPin = pin.transform.position - transform.position;
                toPin.y = 0f;
                if (toPin.sqrMagnitude > hitRadius * hitRadius) continue;
                pin.Topple(slideDirection * speed);
                speed *= pinDrag;
            }

            if (speed < stopSpeed) { state = State.Lying; timer = lieTime; }
        }

        Vector3 SnapToGround(Vector3 position, out Vector3 normal)
        {
            if (Physics.Raycast(position + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 3f, groundMask, QueryTriggerInteraction.Ignore))
            {
                position.y = hit.point.y;
                normal = hit.normal;
                onIce = hit.collider.GetComponent<IcySurface>() != null;
                return position;
            }
            normal = Vector3.up;
            return position;
        }

        void Pose()
        {
            bool lying = state == State.Sliding || state == State.Lying;
            flat = Mathf.MoveTowards(flat, lying ? 1f : 0f, Time.deltaTime / 0.25f);
            float roll = state == State.Walking ? Mathf.Sin(phase) * waddleAngle * (1f - flat) : 0f;
            body.SetLocalPositionAndRotation(slideOffset * flat, Quaternion.Euler(80f * flat, 0f, roll));
        }
    }
}
