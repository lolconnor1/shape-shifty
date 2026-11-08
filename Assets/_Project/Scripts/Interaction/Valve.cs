using UnityEngine;
using UnityEngine.Events;

namespace ShapeShifty.Interaction
{
    // A handwheel valve. Turning it spins the wheel and lets water run for a while, shown by
    // Stream (e.g. water pouring from a spout). What the water does is up to whatever is hooked
    // up to onOpened, like a puddle's Flood. Can't be turned again until the water stops.
    [RequireComponent(typeof(Interactable))]
    public class Valve : MonoBehaviour
    {
        [Tooltip("Spun round its own forward (blue arrow).")]
        [SerializeField] Transform wheel;
        [Tooltip("Turns of the wheel each time it's used.")]
        [SerializeField] float turns = 1.5f;
        [SerializeField] float turnTime = 1.2f;
        [Tooltip("Shown while the water runs. Optional.")]
        [SerializeField] GameObject stream;
        [Tooltip("How long (s) the water runs.")]
        [SerializeField] float flowTime = 3f;
        public UnityEvent onOpened;

        public bool IsFlowing => flowTimer > 0f;

        Interactable interactable;
        Quaternion wheelRest;
        Vector3 streamSize;
        float turnTimer;
        float flowTimer;

        void Awake()
        {
            interactable = GetComponent<Interactable>();
            wheelRest = wheel.localRotation;
            if (stream != null)
            {
                streamSize = stream.transform.localScale;
                stream.SetActive(false);
            }
        }

        void OnEnable() => interactable.Interacted += Open;
        void OnDisable() => interactable.Interacted -= Open;

        void Open()
        {
            turnTimer = turnTime;
            flowTimer = flowTime;
            interactable.enabled = false;
            if (stream != null) stream.SetActive(true);
            onOpened.Invoke();
        }

        void Update()
        {
            if (turnTimer > 0f)
            {
                turnTimer = Mathf.Max(0f, turnTimer - Time.deltaTime);
                float k = Mathf.SmoothStep(0f, 1f, 1f - turnTimer / turnTime);
                wheel.localRotation = wheelRest * Quaternion.Euler(0f, 0f, -360f * turns * k);
            }
            if (flowTimer <= 0f) return;

            flowTimer -= Time.deltaTime;
            if (stream != null)
            {
                // A little wobble so it reads as running water.
                float wobble = 1f + 0.15f * Mathf.Sin(Time.time * 37f);
                stream.transform.localScale = new Vector3(streamSize.x * wobble, streamSize.y, streamSize.z * wobble);
            }
            if (flowTimer > 0f) return;
            if (stream != null) stream.SetActive(false);
            interactable.enabled = true;
        }
    }
}
