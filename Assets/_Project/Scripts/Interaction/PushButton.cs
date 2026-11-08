using UnityEngine;
using UnityEngine.Events;

namespace ShapeShifty.Interaction
{
    // A big push button. Using it pushes the cap in and lights the lamp for a moment, and it
    // can't be pushed again until it pops back out. What it does is up to whatever is hooked
    // up to onPressed. Any form can push it unless its Interactable says otherwise.
    [RequireComponent(typeof(Interactable))]
    public class PushButton : MonoBehaviour
    {
        [Tooltip("Pushed in along its parent's forward (blue arrow).")]
        [SerializeField] Transform cap;
        [SerializeField] float pushDepth = 0.04f;
        [Tooltip("How long (s) it stays pushed in.")]
        [SerializeField] float resetTime = 1f;
        [SerializeField] Renderer lamp;
        [SerializeField] Material lampOff;
        [SerializeField] Material lampOn;
        public UnityEvent onPressed;

        Interactable interactable;
        Vector3 capOut;
        float timer;

        void Awake()
        {
            interactable = GetComponent<Interactable>();
            capOut = cap.localPosition;
            if (lamp != null) lamp.sharedMaterial = lampOff;
        }

        void OnEnable() => interactable.Interacted += Push;
        void OnDisable() => interactable.Interacted -= Push;

        void Push()
        {
            timer = resetTime;
            interactable.enabled = false;
            cap.localPosition = capOut + Vector3.forward * pushDepth;
            if (lamp != null) lamp.sharedMaterial = lampOn;
            onPressed.Invoke();
        }

        void Update()
        {
            if (timer <= 0f) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            cap.localPosition = capOut;
            if (lamp != null) lamp.sharedMaterial = lampOff;
            interactable.enabled = true;
        }
    }
}
