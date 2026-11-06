using ShapeShifty.AI;
using UnityEngine;

namespace ShapeShifty.Interaction
{
    // Something that makes a racket when used, like a rattly old machine: its warning light
    // flashes for a while and the listed employees leave what they're doing to come and look
    // at it. Can't be set off again until it has gone quiet.
    [RequireComponent(typeof(Interactable))]
    public class Distraction : MonoBehaviour
    {
        [Tooltip("Employees who come to check it out.")]
        [SerializeField] EmployeePatrol[] listeners;
        [Tooltip("Where they stand to look at it. Its forward (blue arrow) is the way they face.")]
        [SerializeField] Transform inspectSpot;
        [Tooltip("How far apart (m) they stand when more than one comes to look, side by side across the spot.")]
        [SerializeField] float spacing = 1.4f;
        [Tooltip("How long (s) they look at it once they get there.")]
        [SerializeField] float inspectTime = 6f;
        [Tooltip("How long (s) the warning light flashes.")]
        [SerializeField] float alarmTime = 10f;
        [SerializeField] Renderer warningLight;
        [SerializeField] Material lightOff;
        [SerializeField] Material lightOn;
        [SerializeField] float flashRate = 4f;

        public bool IsGoingOff => alarmTimer > 0f;

        Interactable interactable;
        float alarmTimer;

        void Awake() => interactable = GetComponent<Interactable>();

        void OnEnable() => interactable.Interacted += GoOff;
        void OnDisable() => interactable.Interacted -= GoOff;

        void GoOff()
        {
            alarmTimer = alarmTime;
            interactable.enabled = false;
            for (int i = 0; i < listeners.Length; i++)
            {
                float side = (i - (listeners.Length - 1) * 0.5f) * spacing;
                listeners[i].Investigate(inspectSpot.position + inspectSpot.right * side, inspectSpot.forward, inspectTime);
            }
        }

        void Update()
        {
            if (alarmTimer <= 0f) return;
            alarmTimer -= Time.deltaTime;
            bool lit = alarmTimer > 0f && Mathf.Repeat(alarmTimer * flashRate, 1f) > 0.5f;
            if (warningLight != null) warningLight.sharedMaterial = lit ? lightOn : lightOff;
            if (alarmTimer <= 0f) interactable.enabled = true;
        }
    }
}
