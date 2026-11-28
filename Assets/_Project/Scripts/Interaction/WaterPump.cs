using System.Collections;
using ShapeShifty.Levels;
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
            yield return Move(full, drainedY, pumpTime);
            yield return Move(drainedY, drainedY, holdTime);
            yield return Move(drainedY, full, refillTime);
            IsRunning = false;
        }

        // Waits out any camera shot, so the player doesn't lose time to it.
        IEnumerator Move(float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += CameraShot.Playing ? 0f : Time.deltaTime)
            {
                water.SetLevel(Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            water.SetLevel(to);
        }
    }
}
