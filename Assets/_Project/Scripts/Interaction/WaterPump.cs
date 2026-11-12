using System.Collections;
using UnityEngine;

namespace ShapeShifty.Interaction
{
    // Pumps a stretch of deep water out for a while, like a flooded pipe: the level drops to
    // Drained Y, holds there, then creeps back up as it refloods. Hook Pump up to a valve's
    // onOpened. Can't be started again while it's running.
    public class WaterPump : MonoBehaviour
    {
        [SerializeField] WaterLevel water;
        [Tooltip("Where the level ends up while pumped out.")]
        [SerializeField] float drainedY = -1f;
        [Tooltip("Seconds to pump down.")]
        [SerializeField] float pumpTime = 5f;
        [Tooltip("Seconds it stays pumped out.")]
        [SerializeField] float holdTime = 6f;
        [Tooltip("Seconds to fill back up.")]
        [SerializeField] float refillTime = 12f;

        public bool IsRunning { get; private set; }

        public void Pump()
        {
            if (IsRunning) return;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            IsRunning = true;
            float full = water.Level;
            yield return water.MoveTo(drainedY, pumpTime);
            yield return new WaitForSeconds(holdTime);
            yield return water.MoveTo(full, refillTime);
            IsRunning = false;
        }
    }
}
