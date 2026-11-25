using UnityEngine;

namespace ShapeShifty.Interaction
{
    // Slides back and forth along a line, smoothly turning round at each end, like a
    // fairground duck on its rail. Starts from where it's placed, which is the middle.
    public class Oscillate : MonoBehaviour
    {
        [Tooltip("From the middle to each end, in its parent's space.")]
        [SerializeField] Vector3 offset = new Vector3(2f, 0f, 0f);
        [Tooltip("Seconds for a whole there-and-back.")]
        [SerializeField] float period = 4f;
        [Tooltip("Where in the swing it starts, 0 to 1.")]
        [SerializeField, Range(0f, 1f)] float phase;

        Vector3 middle;

        void Awake() => middle = transform.localPosition;

        void Update()
        {
            float angle = (Time.time / Mathf.Max(0.1f, period) + phase) * Mathf.PI * 2f;
            transform.localPosition = middle + offset * Mathf.Sin(angle);
        }
    }
}
