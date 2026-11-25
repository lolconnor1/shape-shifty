using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace ShapeShifty.Levels
{
    // Runs onEntered the first time the player walks into this trigger, e.g. letting more
    // guard dogs out as the player comes out of hiding.
    [RequireComponent(typeof(Collider))]
    public class TriggerZone : MonoBehaviour
    {
        [Tooltip("Seconds after the player walks in before onEntered runs.")]
        [SerializeField] float delay;
        public UnityEvent onEntered = new UnityEvent();

        bool done;

        void OnTriggerEnter(Collider other)
        {
            if (done || !other.CompareTag("Player")) return;
            done = true;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            onEntered.Invoke();
        }
    }
}
