using UnityEngine;
using UnityEngine.Events;

namespace ShapeShifty.Levels
{
    // The way out of a level, marked with a column of light so it can be spotted from far
    // away. Fires onReached the first time the player steps into its trigger; whatever comes
    // next (like loading the next level) gets hooked up to that.
    [RequireComponent(typeof(Collider))]
    public class LevelExit : MonoBehaviour
    {
        public UnityEvent onReached;

        bool reached;

        void OnTriggerEnter(Collider other)
        {
            if (reached || !other.CompareTag("Player")) return;
            reached = true;
            Debug.Log("Player reached the level exit.", this);
            onReached.Invoke();
        }
    }
}
