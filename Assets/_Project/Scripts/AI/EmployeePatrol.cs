using UnityEngine;

namespace ShapeShifty.AI
{
    // Walks a looping route of waypoints, pausing at each one to glance around. A route of
    // one point makes a guard who stays at their post, optionally sitting down.
    // As soon as the vision cone picks up the player, the employee stops and stares at
    // where they were last seen until detection drains away, then carries on.
    // Investigate() pulls them off their route to go and look at something, like a noise.
    [RequireComponent(typeof(VisionCone))]
    public class EmployeePatrol : MonoBehaviour
    {
        [Header("Route")]
        [Tooltip("Walked in order, looping. Each point's forward (blue arrow) is the way the employee faces while waiting there.")]
        [SerializeField] Transform[] waypoints;
        [SerializeField] float walkSpeed = 1.6f;
        [SerializeField] float turnSpeed = 220f;
        [SerializeField] float waitTime = 3f;
        [Tooltip("How far the employee glances left and right while waiting.")]
        [SerializeField] float lookAroundAngle = 45f;
        [Tooltip("Sit down while waiting at a waypoint (e.g. at a desk).")]
        [SerializeField] bool sitWhileWaiting;
        [Tooltip("Lowered to sit down.")]
        [SerializeField] Transform visual;
        [SerializeField] float sitDrop = 0.45f;

        [Header("Investigating")]
        [Tooltip("Seconds between hearing something and getting up to look.")]
        [SerializeField] float reactionTime = 1.5f;
        [Tooltip("How far the employee glances left and right while looking at something.")]
        [SerializeField] float investigateLookAngle = 25f;

        [Header("Alert")]
        [Tooltip("The \"!\" and detection meter shown above the head while the employee is suspicious. Turned to face the camera.")]
        [SerializeField] GameObject alertIcon;
        [Tooltip("Scaled sideways from its left edge to show how close the employee is to spotting the player.")]
        [SerializeField] Transform meterFill;
        [SerializeField] Color meterEmptyColor = new Color(1f, 0.85f, 0.1f);
        [SerializeField] Color meterFullColor = new Color(1f, 0.1f, 0.05f);
        [Tooltip("The \"?\" shown above the head while investigating something. Turned to face the camera; its first child pops in and bobs.")]
        [SerializeField] GameObject investigateIcon;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        VisionCone vision;
        Renderer meterFillRenderer;
        MaterialPropertyBlock block;
        Transform viewCamera;
        int index;
        bool waiting;
        float waitTimer;
        float visualHeight;

        // Where the employee was sent by Investigate(), and for how long they look once there.
        bool investigating;
        Vector3 investigateSpot;
        Vector3 investigateFacing;
        float investigateTime;
        float investigateTimer;
        float reactionTimer;
        Transform investigateMark;
        float investigateIconTimer;

        public bool IsInvestigating => investigating;

        void Awake()
        {
            vision = GetComponent<VisionCone>();
            meterFillRenderer = meterFill.GetComponentInChildren<Renderer>();
            block = new MaterialPropertyBlock();
            if (visual != null) visualHeight = visual.localPosition.y;
            if (investigateIcon != null) investigateMark = investigateIcon.transform.GetChild(0);
        }

        void Start() => viewCamera = Camera.main != null ? Camera.main.transform : null;

        // Leave the route, walk to spot, look the way facing points for lookTime seconds, then
        // go back to the route.
        public void Investigate(Vector3 spot, Vector3 facing, float lookTime)
        {
            investigating = true;
            investigateSpot = spot;
            investigateFacing = facing;
            investigateTime = lookTime;
            investigateTimer = 0f;
            reactionTimer = reactionTime;
            waiting = false;
        }

        void Update()
        {
            if (vision.Detection > 0f)
                TurnTowards(vision.LastSeenPosition - transform.position);
            else if (investigating)
                Investigating();
            else if (waypoints.Length > 0)
                Patrol();
            UpdateSitting();
        }

        void Investigating()
        {
            if (reactionTimer > 0f)
            {
                reactionTimer -= Time.deltaTime;
                return;
            }
            if (!WalkTo(investigateSpot)) return;

            investigateTimer += Time.deltaTime;
            float glance = Mathf.Sin(investigateTimer / investigateTime * Mathf.PI * 2f) * investigateLookAngle;
            TurnTowards(Quaternion.Euler(0f, glance, 0f) * investigateFacing);
            if (investigateTimer >= investigateTime) investigating = false;
        }

        void UpdateSitting()
        {
            if (visual == null || !sitWhileWaiting) return;
            bool sitting = waiting && !investigating && vision.Detection <= 0f;
            Vector3 p = visual.localPosition;
            p.y = Mathf.MoveTowards(p.y, sitting ? visualHeight - sitDrop : visualHeight, Time.deltaTime * 1.5f);
            visual.localPosition = p;
        }

        // After the camera has moved, so the icons face where the camera is this frame.
        void LateUpdate()
        {
            UpdateAlertIcon();
            UpdateInvestigateIcon();
        }

        void Patrol()
        {
            Transform point = waypoints[index];
            if (waiting)
            {
                waitTimer += Time.deltaTime;
                float glance = Mathf.Sin(waitTimer / waitTime * Mathf.PI * 2f) * lookAroundAngle;
                TurnTowards(Quaternion.Euler(0f, glance, 0f) * point.forward);
                if (waitTimer >= waitTime)
                {
                    waiting = false;
                    index = (index + 1) % waypoints.Length;
                }
                return;
            }

            if (WalkTo(point.position))
            {
                waiting = true;
                waitTimer = 0f;
            }
        }

        // Steps towards destination; true once there.
        bool WalkTo(Vector3 destination)
        {
            Vector3 toPoint = destination - transform.position;
            toPoint.y = 0f;
            if (toPoint.sqrMagnitude < 0.01f) return true;

            TurnTowards(toPoint);
            // Turn on the spot first, then walk, so corners don't get cut.
            if (Vector3.Angle(transform.forward, toPoint) < 30f)
            {
                Vector3 goal = new Vector3(destination.x, transform.position.y, destination.z);
                transform.position = Vector3.MoveTowards(transform.position, goal, walkSpeed * Time.deltaTime);
            }
            return false;
        }

        void TurnTowards(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
        }

        void UpdateAlertIcon()
        {
            bool show = vision.Detection > 0f;
            if (alertIcon.activeSelf != show) alertIcon.SetActive(show);
            if (!show) return;

            FaceCamera(alertIcon.transform);
            meterFill.localScale = new Vector3(Mathf.Max(vision.Detection, 0.001f), 1f, 1f);
            block.SetColor(BaseColorId, Color.Lerp(meterEmptyColor, meterFullColor, vision.Detection));
            meterFillRenderer.SetPropertyBlock(block);
        }

        // The "?" while checking something out. Gives way to the "!" if the player is seen.
        void UpdateInvestigateIcon()
        {
            if (investigateIcon == null) return;
            bool show = investigating && vision.Detection <= 0f;
            if (investigateIcon.activeSelf != show)
            {
                investigateIcon.SetActive(show);
                investigateIconTimer = 0f;
            }
            if (!show) return;

            investigateIconTimer += Time.deltaTime;
            FaceCamera(investigateIcon.transform);
            // Pops in with a little overshoot, then bobs gently.
            float pop = Mathf.Clamp01(investigateIconTimer / 0.25f);
            investigateMark.localScale = Vector3.one * (Mathf.SmoothStep(0f, 1f, pop) * (1f + 0.25f * Mathf.Sin(pop * Mathf.PI)));
            investigateMark.localPosition = Vector3.up * (Mathf.Sin(investigateIconTimer * 4f) * 0.05f);
        }

        void FaceCamera(Transform icon)
        {
            if (viewCamera == null) return;
            Vector3 facing = viewCamera.forward;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f) icon.rotation = Quaternion.LookRotation(facing);
        }
    }
}
