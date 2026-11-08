using UnityEngine;

namespace ShapeShifty.AI
{
    // A wall-mounted camera that sweeps from side to side, pausing at each end. It watches
    // through its VisionCone (set to see through disguises, so it spots rats too) and stops to
    // follow the player while it's suspicious. Put this on an object at floor level under the
    // camera, facing the middle of the sweep, with the VisionCone's eye height set to the
    // camera's height. Hook SwitchOff and SwitchOn up to a lever to turn it off and on.
    [RequireComponent(typeof(VisionCone))]
    public class SecurityCamera : MonoBehaviour
    {
        [Tooltip("The camera model. A child of this object, so it turns with it; tips down when switched off.")]
        [SerializeField] Transform head;
        [Tooltip("How far (degrees) it turns either side of the way it starts facing.")]
        [SerializeField] float sweepAngle = 30f;
        [Tooltip("Seconds to sweep from one side to the other.")]
        [SerializeField] float sweepTime = 4f;
        [Tooltip("Seconds it holds still at each end of the sweep.")]
        [SerializeField] float pauseTime = 1.5f;
        [Tooltip("How fast (degrees per second) it turns to follow the player.")]
        [SerializeField] float trackSpeed = 90f;
        [Tooltip("How far (degrees) the head tips down when switched off.")]
        [SerializeField] float offDroop = 35f;
        [Tooltip("Lit while on, blinking while it's suspicious, dark while off.")]
        [SerializeField] Renderer statusLight;
        [SerializeField] Material lightOff;
        [SerializeField] Material lightOn;
        [SerializeField] bool isOn = true;

        public bool IsOn => isOn;

        VisionCone vision;
        Quaternion headRest;
        float centreYaw;
        float offset;  // degrees from centreYaw
        float target;  // the end of the sweep it's heading for
        float pause;
        float droop;   // 0 = watching, 1 = switched off

        void Awake()
        {
            vision = GetComponent<VisionCone>();
            headRest = head.localRotation;
            centreYaw = transform.eulerAngles.y;
            target = sweepAngle;
            droop = isOn ? 0f : 1f;
            vision.enabled = isOn;
        }

        public void SwitchOn()
        {
            isOn = true;
            vision.enabled = true;
        }

        public void SwitchOff()
        {
            isOn = false;
            vision.enabled = false;
        }

        void Update()
        {
            if (isOn)
            {
                if (vision.Detection > 0f) Follow(vision.LastSeenPosition);
                else Sweep();
                transform.rotation = Quaternion.Euler(0f, centreYaw + offset, 0f);
            }

            droop = Mathf.MoveTowards(droop, isOn ? 0f : 1f, Time.deltaTime * 2f);
            head.localRotation = headRest * Quaternion.Euler(Mathf.SmoothStep(0f, 1f, droop) * offDroop, 0f, 0f);

            if (statusLight != null)
            {
                bool lit = isOn && (vision.Detection <= 0f || Mathf.Repeat(Time.time * 6f, 1f) > 0.5f);
                statusLight.sharedMaterial = lit ? lightOn : lightOff;
            }
        }

        void Sweep()
        {
            if (pause > 0f)
            {
                pause -= Time.deltaTime;
                return;
            }
            offset = Mathf.MoveTowards(offset, target, 2f * sweepAngle / sweepTime * Time.deltaTime);
            if (offset == target)
            {
                target = -target;
                pause = pauseTime;
            }
        }

        void Follow(Vector3 position)
        {
            Vector3 to = position - transform.position;
            float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            offset = Mathf.MoveTowards(offset, Mathf.DeltaAngle(centreYaw, yaw), trackSpeed * Time.deltaTime);
        }
    }
}
