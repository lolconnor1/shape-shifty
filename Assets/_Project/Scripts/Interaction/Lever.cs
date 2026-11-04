using UnityEngine;
using UnityEngine.Events;

namespace ShapeShifty.Interaction
{
    // A lever that flips between off (handle up) and on (handle down) each time it's used,
    // swinging the handle across and swapping its lamp from red to green. What it actually
    // does is up to whatever is hooked up to its events.
    [RequireComponent(typeof(Interactable))]
    public class Lever : MonoBehaviour
    {
        [Tooltip("Rotated to swing the handle.")]
        [SerializeField] Transform handle;
        [Tooltip("Local rotation of the handle when off and when on.")]
        [SerializeField] Vector3 offAngles = new Vector3(-30f, 0f, 0f);
        [SerializeField] Vector3 onAngles = new Vector3(-150f, 0f, 0f);
        [SerializeField] float swingTime = 0.25f;
        [SerializeField] Renderer lamp;
        [SerializeField] Material lampOff;
        [SerializeField] Material lampOn;
        [SerializeField] bool isOn;
        [Tooltip("Off: once pulled, the lever stays on for good.")]
        [SerializeField] bool canSwitchBack = true;

        public UnityEvent onSwitchedOn;
        public UnityEvent onSwitchedOff;

        public bool IsOn => isOn;

        Interactable interactable;
        float swing; // 0 = off, 1 = on

        void Awake()
        {
            interactable = GetComponent<Interactable>();
            swing = isOn ? 1f : 0f;
            Apply();
        }

        void OnEnable() => interactable.Interacted += Toggle;
        void OnDisable() => interactable.Interacted -= Toggle;

        public void Toggle()
        {
            isOn = !isOn;
            if (isOn && !canSwitchBack) interactable.enabled = false;
            (isOn ? onSwitchedOn : onSwitchedOff).Invoke();
        }

        void Update()
        {
            float target = isOn ? 1f : 0f;
            if (swing == target) return;
            swing = Mathf.MoveTowards(swing, target, Time.deltaTime / swingTime);
            Apply();
        }

        void Apply()
        {
            float t = swing * swing * (3f - 2f * swing);
            handle.localRotation = Quaternion.Slerp(Quaternion.Euler(offAngles), Quaternion.Euler(onAngles), t);
            if (lamp != null) lamp.sharedMaterial = swing > 0.5f ? lampOn : lampOff;
        }
    }
}
