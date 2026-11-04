using UnityEngine;

namespace ShapeShifty.AI
{
    // Walks a looping route of waypoints, pausing at each one to glance around.
    // As soon as the vision cone picks up the player, the employee stops and stares at
    // where they were last seen until detection drains away, then carries on.
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

        [Header("Alert")]
        [Tooltip("The \"!\" and detection meter shown above the head while the employee is suspicious. Turned to face the camera.")]
        [SerializeField] GameObject alertIcon;
        [Tooltip("Scaled sideways from its left edge to show how close the employee is to spotting the player.")]
        [SerializeField] Transform meterFill;
        [SerializeField] Color meterEmptyColor = new Color(1f, 0.85f, 0.1f);
        [SerializeField] Color meterFullColor = new Color(1f, 0.1f, 0.05f);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        VisionCone vision;
        Renderer meterFillRenderer;
        MaterialPropertyBlock block;
        Transform viewCamera;
        int index;
        bool waiting;
        float waitTimer;

        void Awake()
        {
            vision = GetComponent<VisionCone>();
            meterFillRenderer = meterFill.GetComponentInChildren<Renderer>();
            block = new MaterialPropertyBlock();
        }

        void Start() => viewCamera = Camera.main != null ? Camera.main.transform : null;

        void Update()
        {
            if (vision.Detection > 0f)
                TurnTowards(vision.LastSeenPosition - transform.position);
            else if (waypoints.Length > 0)
                Patrol();
        }

        // After the camera has moved, so the icon faces where the camera is this frame.
        void LateUpdate() => UpdateAlertIcon();

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

            Vector3 toPoint = point.position - transform.position;
            toPoint.y = 0f;
            if (toPoint.sqrMagnitude < 0.01f)
            {
                waiting = true;
                waitTimer = 0f;
                return;
            }

            TurnTowards(toPoint);
            // Turn on the spot first, then walk, so corners don't get cut.
            if (Vector3.Angle(transform.forward, toPoint) < 30f)
            {
                Vector3 goal = new Vector3(point.position.x, transform.position.y, point.position.z);
                transform.position = Vector3.MoveTowards(transform.position, goal, walkSpeed * Time.deltaTime);
            }
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

            if (viewCamera != null)
            {
                Vector3 facing = viewCamera.forward;
                facing.y = 0f;
                if (facing.sqrMagnitude > 0.0001f) alertIcon.transform.rotation = Quaternion.LookRotation(facing);
            }

            meterFill.localScale = new Vector3(Mathf.Max(vision.Detection, 0.001f), 1f, 1f);
            block.SetColor(BaseColorId, Color.Lerp(meterEmptyColor, meterFullColor, vision.Detection));
            meterFillRenderer.SetPropertyBlock(block);
        }
    }
}
