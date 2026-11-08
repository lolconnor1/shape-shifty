using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ShapeShifty.Interaction
{
    // A plate set in the floor that sinks and lights up while enough weight rests on it:
    // loose objects with a Rigidbody (like a dumped load) and, if allowed, the player. Its
    // trigger is the space just above the plate. Hook onPressed and onReleased up to a hatch,
    // a door and so on.
    [RequireComponent(typeof(Collider))]
    public class PressurePlate : MonoBehaviour
    {
        [Tooltip("Total Rigidbody mass needed to press it.")]
        [SerializeField] float requiredMass = 1f;
        [Tooltip("The player presses it too, in any form.")]
        [SerializeField] bool playerCanPress;
        [Tooltip("Sinks while pressed.")]
        [SerializeField] Transform top;
        [SerializeField] float sinkDepth = 0.04f;
        [SerializeField] Renderer lamp;
        [SerializeField] Material lampOff;
        [SerializeField] Material lampOn;
        public UnityEvent onPressed;
        public UnityEvent onReleased;

        public bool IsPressed { get; private set; }

        static readonly List<PressurePlate> active = new List<PressurePlate>();

        readonly HashSet<Rigidbody> bodies = new HashSet<Rigidbody>();
        bool playerOn;
        Vector3 topUp;
        float sink; // 0 = up, 1 = down

        // True if body is resting on any pressure plate.
        public static bool IsOnAny(Rigidbody body)
        {
            foreach (var plate in active)
                if (plate.bodies.Contains(body)) return true;
            return false;
        }

        void Awake()
        {
            topUp = top.localPosition;
            if (lamp != null) lamp.sharedMaterial = lampOff;
        }

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player")) playerOn = true;
            else if (other.attachedRigidbody != null && !other.attachedRigidbody.isKinematic) bodies.Add(other.attachedRigidbody);
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player")) playerOn = false;
            else if (other.attachedRigidbody != null) bodies.Remove(other.attachedRigidbody);
        }

        void Update()
        {
            // Destroyed objects never send OnTriggerExit.
            bodies.RemoveWhere(body => body == null);
            float mass = 0f;
            foreach (var body in bodies) mass += body.mass;
            bool pressed = mass >= requiredMass || (playerCanPress && playerOn);
            if (pressed != IsPressed)
            {
                IsPressed = pressed;
                if (lamp != null) lamp.sharedMaterial = pressed ? lampOn : lampOff;
                (pressed ? onPressed : onReleased).Invoke();
            }

            float target = IsPressed ? 1f : 0f;
            if (sink == target) return;
            sink = Mathf.MoveTowards(sink, target, Time.deltaTime * 8f);
            top.localPosition = topUp + Vector3.down * (sinkDepth * sink);
        }
    }
}
